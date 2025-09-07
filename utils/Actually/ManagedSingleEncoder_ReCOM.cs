using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace KingdomHeartsMusicPatcher.utils
{
    // Specialised managed encoder for ReCOM SCDs only.
    // Mirrors ManagedSingleEncoder but applies ReCOM-specific behavior.
    internal static class ManagedSingleEncoder_ReCOM
    {
        private static string ResolveToolsPath(string encoderDir)
        {
            var candidates = new[]
            {
                Path.Combine(encoderDir, "tools"),
                Path.Combine(AppContext.BaseDirectory, "utils", "SingleEncoder", "tools")
            };
            foreach (var p in candidates)
                if (Directory.Exists(p)) return p;
            return Path.Combine(encoderDir, "tools");
        }

        private static string OggEncPath(string encoderDir) => Path.Combine(ResolveToolsPath(encoderDir), "oggenc", "oggenc.exe");
        private static string AdpcmEncPath(string encoderDir) => Path.Combine(ResolveToolsPath(encoderDir), "adpcmencode3", "adpcmencode3.exe");

        public static void Encode(string inputScdTemplate, string inputWav, int quality, bool fullLoop, string encoderDir, Action<int>? progress = null)
        {
            void Report(int p) { try { progress?.Invoke(Math.Max(0, Math.Min(100, p))); } catch { } }

            Report(0);
            Logger.Log($"ManagedSingleEncoder_ReCOM.Encode start | template='{inputScdTemplate}', wav='{inputWav}', quality={quality}, fullLoop={fullLoop}, encoderDir='{encoderDir}'");
            var outputDir = Path.Combine(encoderDir, "output");
            Directory.CreateDirectory(outputDir);

            // Validate inputs
            if (!File.Exists(inputScdTemplate))
            {
                throw new FileNotFoundException($"SCD template not found: {inputScdTemplate}");
            }
            if (!File.Exists(inputWav))
            {
                throw new FileNotFoundException($"Input WAV not found: {inputWav}");
            }

            // Validate required tools
            var ogg = OggEncPath(encoderDir);
            var adpcm = AdpcmEncPath(encoderDir);
            Logger.Log($"[ReCOM] Tools resolved | oggenc='{ogg}', adpcmencode3='{adpcm}', toolsDir='{ResolveToolsPath(encoderDir)}'");
            if (!File.Exists(ogg)) Logger.Log($"[ReCOM] WARNING: oggenc.exe not found at {ogg}");
            if (!File.Exists(adpcm)) Logger.Log($"[ReCOM] WARNING: adpcmencode3.exe not found at {adpcm}");

            // Load template
            byte[] oldSCD = File.ReadAllBytes(inputScdTemplate);
            uint tables_offset = Read(oldSCD, 16, 0x0e);
            uint headers_entries = Read(oldSCD, 16, (int)tables_offset + 0x04);
            uint headers_offset = Read(oldSCD, 32, (int)tables_offset + 0x0c);
            int file_size = (int)Read(oldSCD, 32, (int)headers_offset);
            Logger.Log($"[ReCOM] Template parsed | tables_offset=0x{tables_offset:X}, headers_entries={headers_entries}, headers_offset=0x{headers_offset:X}, initial_file_size={file_size}");

            var SCDs = new System.Collections.Generic.List<byte[]>();
            int[] entry_offsets = new int[headers_entries + 1];
            entry_offsets[0] = file_size;
            uint codec = getCodec(headers_entries, headers_offset, oldSCD);
            Logger.Log($"[ReCOM] Detected codec: 0x{codec:X}");

            for (int i = 0; i < headers_entries; i++)
            {
                uint entry_begin = Read(oldSCD, 32, (int)headers_offset + i * 0x04);
                uint entry_end = (i == headers_entries - 1) ? (uint)oldSCD.Length : Read(oldSCD, 32, (int)headers_offset + (i + 1) * 0x04);
                uint entry_size = entry_end - entry_begin;
                byte[] entry = new byte[entry_size];
                Array.Copy(oldSCD, entry_begin, entry, 0, entry_size);

                Logger.Log($"[ReCOM] Entry[{i}] begin=0x{entry_begin:X}, end=0x{entry_end:X}, size={entry_size}");

                byte[] newEntry;
                if (Read(entry, 32, 0x0c) != 0xFFFFFFFF)
                {
                    byte[] wav = File.ReadAllBytes(inputWav);
                    if (codec == 0x6)
                    {
                        Report(5);
                        // Read loop from tags; optionally override to full loop
                        int LoopStart_Sample = searchTag("LoopStart", wav);
                        int Total_Samples = searchTag("LoopEnd", wav);
                        Logger.Log($"[ReCOM] Loop tags | LoopStart={LoopStart_Sample}, LoopEnd={Total_Samples}");
                        if (fullLoop)
                        {
                            GetFullLoopFromWav(wav, out LoopStart_Sample, out Total_Samples);
                            Logger.Log($"[ReCOM] FullLoop override | LoopStart={LoopStart_Sample}, TotalSamples={Total_Samples}");
                        }

                        // Encode Vorbis
                        Report(15);
                        WavtoOGG(inputWav, LoopStart_Sample, Total_Samples, quality, ogg);
                        Report(60);
                        string oggPath = Path.ChangeExtension(inputWav, ".ogg");
                        Logger.Log($"[ReCOM] OGG expected at '{oggPath}' (exists={File.Exists(oggPath)})");
                        newEntry = OGGtoSCD(wav, entry, oggPath, LoopStart_Sample, Total_Samples);
                        Report(85);
                    }
                    else
                    {
                        Report(10);
                        WavtoMSADPCM(inputWav, adpcm, encoderDir);
                        Report(60);
                        string msadpcmPath = Path.Combine(encoderDir, "adpcm" + $"{Path.GetFileNameWithoutExtension(inputWav)}.wav");
                        Logger.Log($"[ReCOM] MSADPCM expected at '{msadpcmPath}' (exists={File.Exists(msadpcmPath)})");
                        newEntry = MSADPCMtoSCD(wav, entry, msadpcmPath);
                        Report(85);
                    }
                }
                else
                {
                    Logger.Log($"[ReCOM] Entry[{i}] is dummy; copying as-is");
                    newEntry = entry;
                }

                SCDs.Add(newEntry);
                file_size += newEntry.Length;
                entry_offsets[i + 1] = file_size;
                Logger.Log($"[ReCOM] Entry[{i}] newSize={newEntry.Length}, cumulativeFileSize={file_size}");
            }

            byte[] finalSCD = new byte[file_size];
            Array.Copy(oldSCD, finalSCD, entry_offsets[0]);
            Report(90);
            for (int i = 0; i < headers_entries; i++)
            {
                Write(finalSCD, entry_offsets[i], 32, (int)headers_offset + i * 0x04);
                Array.Copy(SCDs[i], 0, finalSCD, entry_offsets[i], SCDs[i].Length);
            }
            Write(finalSCD, file_size, 32, 0x10);
            string outputPath = Path.Combine(outputDir, "original.scd");
            File.WriteAllBytes(outputPath, finalSCD);
            Report(100);
            Logger.Log($"ManagedSingleEncoder_ReCOM.Encode done | wrote '{outputPath}', size={finalSCD.Length}");
        }

        private static void GetFullLoopFromWav(byte[] wav, out int loopStart, out int totalSamples)
        {
            loopStart = 0; totalSamples = -1;
            byte[] fmtPattern = Encoding.ASCII.GetBytes("fmt ");
            byte[] datPattern = Encoding.ASCII.GetBytes("data");
            int typepos = SearchBytePattern(0, wav, fmtPattern);
            int datapos = SearchBytePattern(0, wav, datPattern);
            if (typepos != -1 && datapos != -1)
            {
                short blockAlign = BitConverter.ToInt16(wav, typepos + 20);
                int datasize = BitConverter.ToInt32(wav, datapos + 4);
                if (datasize < wav.Length && blockAlign > 0)
                {
                    loopStart = 0;
                    totalSamples = datasize / blockAlign; // per-channel samples
                }
            }
        }

        private static void WavtoOGG(string inputWAV, int LoopStart_Sample, int Total_Samples, int Quality, string oggEncPath)
        {
            var args = (LoopStart_Sample == -1 && Total_Samples == -1)
                ? $" \"{inputWAV}\" -s 0 -q \"{Quality}\""
                : $" \"{inputWAV}\" -s 0 -q \"{Quality}\" -c LoopStart=\"{LoopStart_Sample}\" -c LoopEnd=\"{Total_Samples - 1}\"";
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            using var p = new Process();
            p.StartInfo.FileName = oggEncPath;
            p.StartInfo.Arguments = args;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(inputWAV) ?? Environment.CurrentDirectory;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbOut.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbErr.AppendLine(e.Data); };

            Logger.Log($"[ReCOM] Run oggenc | exe='{p.StartInfo.FileName}', args='{p.StartInfo.Arguments}', cwd='{p.StartInfo.WorkingDirectory}'");
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
            Logger.Log($"[ReCOM] oggenc exit {p.ExitCode}\nSTDOUT:\n{sbOut}\nSTDERR:\n{sbErr}");
            if (p.ExitCode != 0)
            {
                throw new Exception($"oggenc failed with exit code {p.ExitCode}");
            }
        }

        private static byte[] OGGtoSCD(byte[] wav, byte[] entry, string oggPath, int LoopStart_Sample, int Total_Samples)
        {
            Logger.Log($"[ReCOM] OGGtoSCD | ogg='{oggPath}', exists={File.Exists(oggPath)}");
            byte[] ogg = File.ReadAllBytes(oggPath);
            uint meta_offset = 0;
            uint extradata_offset = meta_offset + 0x20;

            // Collect all OggS page offsets
            byte[] oggs = new byte[] { 0x4F, 0x67, 0x67, 0x53 };
            var page_offsets = new System.Collections.Generic.List<int>();
            int cursor = 0; int guard = 0;
            while (cursor >= 0 && cursor < ogg.Length && guard++ < 10_000)
            {
                int idx = SearchBytePattern(cursor, ogg, oggs);
                if (idx < 0) break;
                page_offsets.Add(idx);
                cursor = idx + 4;
            }
            Logger.Log($"[ReCOM] OGGtoSCD | pages found={page_offsets.Count}");
            if (page_offsets.Count == 0) throw new Exception("No OGG pages found");

            // Determine header size as the first page with non-zero granule position
            int vorbis_header_size = page_offsets[0];
            for (int i = 0; i < page_offsets.Count; i++)
            {
                int idx = page_offsets[i];
                ulong granule = Read64(ogg, idx + 6);
                Logger.Log($"[ReCOM] OGGtoSCD | page[{i}] @0x{idx:X}, granule={granule}");
                if (granule != 0)
                {
                    vorbis_header_size = idx;
                    break;
                }
            }
            Logger.Log($"[ReCOM] OGGtoSCD | headerSize={vorbis_header_size}");

            //Write Stream Size
            int streamSize = ogg.Length - vorbis_header_size;
            Write(entry, streamSize, 32, (int)meta_offset);

            //Find Loop Offsets by scanning granule counts (best effort)
            int LoopStart = 0;
            int LoopEnd = (Total_Samples != -1) ? streamSize : 0;
            if (LoopStart_Sample > 0)
            {
                for (int i = 0; i < page_offsets.Count; i++)
                {
                    int idx = page_offsets[i];
                    if (idx <= vorbis_header_size) continue; // skip header pages
                    if (idx + 6 < ogg.Length)
                    {
                        uint pageGranuleLow32 = Read(ogg, 32, idx + 6);
                        if (LoopStart_Sample <= pageGranuleLow32)
                        {
                            LoopStart = idx - vorbis_header_size;
                            break;
                        }
                    }
                }
            }
            else
            {
                LoopStart = 0;
            }
            Logger.Log($"[ReCOM] OGGtoSCD | LoopStart={LoopStart}, LoopEnd={LoopEnd}, streamSize={streamSize}");

            //Write LoopStart and LoopEnd
            Write(entry, LoopStart, 32, (int)meta_offset + 0x10);
            Write(entry, LoopEnd, 32, (int)meta_offset + 0x14);
            //Write Channels and Sample Rate from first identification header bytes
            uint Channels = Read(ogg, 8, page_offsets[0] + 0x27);
            uint Sample_Rate = Read(ogg, 32, page_offsets[0] + 0x28);
            Write(entry, (int)Channels, 8, (int)meta_offset + 0x04);
            Write(entry, (int)Sample_Rate, 32, (int)meta_offset + 0x08);
            Logger.Log($"[ReCOM] OGGtoSCD | Channels={Channels}, SampleRate={Sample_Rate}");

            // Aux info
            uint aux_chunk_count = Read(entry, 32, (int)meta_offset + 0x1c);
            uint aux_chunk_size = 0;
            if (aux_chunk_count > 0)
            {
                aux_chunk_size = Read(entry, 32, (int)extradata_offset + 0x04);
                extradata_offset = extradata_offset + aux_chunk_size;
                uint mark_entries = Read(entry, 32, (int)meta_offset + 0x30);
                Write(entry, LoopStart_Sample, 32, (int)meta_offset + 0x28);
                Write(entry, Total_Samples, 32, (int)meta_offset + 0x2C);
                if (mark_entries == 1)
                {
                    int mark = searchTag("MARK1", wav);
                    Write(entry, mark != -1 ? mark : LoopStart_Sample, 32, (int)meta_offset + 0x34);
                }
                else
                {
                    for (int i = 0; i < mark_entries; i++)
                    {
                        int mark = searchTag("MARK" + (i + 1), wav);
                        Write(entry, mark != -1 ? mark : 0, 32, (int)meta_offset + 0x34 + i * 0x04);
                    }
                }
            }

            //Write Vorbis Header Size
            Write(entry, vorbis_header_size, 32, (int)extradata_offset + 0x14);
            //Set Encryption Key to 0
            Write(entry, 0x00, 8, (int)extradata_offset + 0x02);

            //Create Seek Table
            var seek_offsets = new System.Collections.Generic.List<int>();
            uint previous_granule = Read(ogg, 32, vorbis_header_size + 0x06);
            for (int i = 0; i < page_offsets.Count; i++)
            {
                int idx = page_offsets[i];
                if (idx <= vorbis_header_size) continue;
                uint current_granule = Read(ogg, 32, idx + 0x06);
                if (current_granule - previous_granule >= 2048)
                {
                    seek_offsets.Add(idx - vorbis_header_size);
                    previous_granule = current_granule;
                }
            }
            if (seek_offsets.Count == 0) seek_offsets.Add(0);

            byte[] seek_table = new byte[seek_offsets.Count * 4];
            for (int i = 0; i < seek_offsets.Count; i++) Write(seek_table, seek_offsets[i], 32, i * 4);
            Write(entry, seek_table.Length, 32, (int)extradata_offset + 0x10);
            Write(entry, 0x20 + vorbis_header_size + (int)aux_chunk_size + seek_table.Length, 32, (int)meta_offset + 0x18);

            int file_size = (int)(extradata_offset + 0x20 + seek_table.Length + ogg.Length);
            while (file_size % 16 != 0) file_size++;

            byte[] newEntry = new byte[file_size];
            Array.Copy(entry, newEntry, extradata_offset + 0x20);
            Array.Copy(seek_table, 0, newEntry, extradata_offset + 0x20, seek_table.Length);
            Array.Copy(ogg, 0, newEntry, extradata_offset + 0x20 + seek_table.Length, ogg.Length);
            try { File.Delete(oggPath); } catch { }
            Logger.Log($"[ReCOM] OGGtoSCD | newEntrySize={newEntry.Length}");
            return newEntry;
        }

        private static void WavtoMSADPCM(string inputWAV, string adpcmEncPath, string encoderDir)
        {
            string outputWAV = Path.Combine(encoderDir, "adpcm" + $"{Path.GetFileNameWithoutExtension(inputWAV)}.wav");
            var sbOut = new StringBuilder(); var sbErr = new StringBuilder();
            using var p = new Process();
            p.StartInfo.FileName = adpcmEncPath;
            p.StartInfo.Arguments = $" \"{inputWAV}\" \"{outputWAV}\"";
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(inputWAV) ?? Environment.CurrentDirectory;
            p.StartInfo.UseShellExecute = false; 
            p.StartInfo.RedirectStandardOutput = true; 
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbOut.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbErr.AppendLine(e.Data); };
            Logger.Log($"[ReCOM] Run adpcmencode3 | exe='{p.StartInfo.FileName}', args='{p.StartInfo.Arguments}', cwd='{p.StartInfo.WorkingDirectory}'");
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
            Logger.Log($"[ReCOM] adpcmencode3 exit {p.ExitCode}\nSTDOUT:\n{sbOut}\nSTDERR:\n{sbErr}");
            if (p.ExitCode != 0) throw new Exception($"adpcmencode3 failed with exit code {p.ExitCode}");
        }

        private static byte[] MSADPCMtoSCD(byte[] wav, byte[] entry, string msadpcmPath)
        {
            Logger.Log($"[ReCOM] MSADPCMtoSCD | path='{msadpcmPath}', exists={File.Exists(msadpcmPath)}");
            byte[] msadpcm = File.ReadAllBytes(msadpcmPath);
            uint meta_offset = 0; uint extradata_offset = meta_offset + 0x20;
            uint Channels = Read(msadpcm, 8, 0x16); uint Sample_Rate = Read(msadpcm, 32, 0x18);
            Write(entry, (int)Channels, 8, (int)meta_offset + 0x04);
            Write(entry, (int)Sample_Rate, 32, (int)meta_offset + 0x08);
            byte[] pattern = new byte[] { 0x64, 0x61, 0x74, 0x61 };
            int data_offset = SearchBytePattern(0, msadpcm, pattern) + 8;
            Write(entry, msadpcm.Length - data_offset, 32, (int)meta_offset);
            int file_size = (int)(extradata_offset + 0x32 + msadpcm.Length - data_offset);
            while (file_size % 16 != 0) file_size++;
            byte[] newEntry = new byte[file_size];
            Array.Copy(entry, newEntry, extradata_offset);
            Array.Copy(msadpcm, 0x14, newEntry, extradata_offset, 0x32);
            Array.Copy(msadpcm, data_offset, newEntry, extradata_offset + 0x32, msadpcm.Length - data_offset);
            try { File.Delete(msadpcmPath); } catch { }
            Logger.Log($"[ReCOM] MSADPCMtoSCD | newEntrySize={newEntry.Length}");
            return newEntry;
        }

        private static uint Read(byte[] file, int bits, int position)
        {
            int bytes = bits / 8;
            if (position < 0 || position + bytes > file.Length) return 0;
            uint num = 0;
            for (int i = 0; i < bytes; i++) num |= (uint)(file[position + i] << (8 * i));
            return num;
        }

        private static ulong Read64(byte[] file, int position)
        {
            if (position < 0 || position + 8 > file.Length) return 0UL;
            ulong num = 0UL;
            for (int i = 0; i < 8; i++) num |= (ulong)file[position + i] << (8 * i);
            return num;
        }

        private static void Write(byte[] file, int value, int bits, int position)
        {
            int bytes = bits / 8;
            byte[] val = BitConverter.GetBytes((uint)value);
            for (int i = 0; i < bytes; i++) if (position + i < file.Length) file[position + i] = val[i];
        }

        private static uint getCodec(uint headers_entries, uint headers_offset, byte[] data)
        {
            uint entry_codec = 0xFFFFFFFF;
            for (int i = 0; i < headers_entries; i++)
            {
                uint entry_begin = Read(data, 32, (int)headers_offset + i * 0x04);
                entry_codec = Read(data, 32, (int)entry_begin + 0x0c);
                if (entry_codec != 0xFFFFFFFF) break;
            }
            return entry_codec;
        }

        private static int SearchBytePattern(int position, byte[] data, byte[] pattern)
        {
            if (position < 0) position = 0;
            int patternLength = pattern.Length; int totalLength = data.Length;
            if (patternLength == 0 || totalLength == 0 || position >= totalLength) return -1;
            byte firstMatchByte = pattern[0];
            for (int i = position; i < totalLength; i++)
            {
                if (firstMatchByte == data[i] && totalLength - i >= patternLength)
                {
                    bool match = true;
                    for (int j = 1; j < patternLength; j++) { if (data[i + j] != pattern[j]) { match = false; break; } }
                    if (match) return i;
                }
            }
            return -1;
        }

        private static int searchTag(string tag, byte[] data)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(tag);
            int value = SearchBytePattern(0, data, pattern);
            if (value != -1)
            {
                value = value + pattern.Length;
                value = getTagData(value, data);
            }
            return value;
        }

        private static int getTagData(int position, byte[] data)
        {
            if (position < 0 || position >= data.Length) return -1;
            while (position < data.Length && (data[position] - 0x30 < 0 || data[position] - 0x30 > 9)) position++;
            int initial_position = position;
            while (position < data.Length && data[position] - 0x30 >= 0 && data[position] - 0x30 <= 9) position++;
            if (initial_position >= data.Length || position <= initial_position) return -1;
            string value = Encoding.ASCII.GetString(data, initial_position, position - initial_position);
            return int.TryParse(value, out var tagData) ? tagData : -1;
        }
    }
}
