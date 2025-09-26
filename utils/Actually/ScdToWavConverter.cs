using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace KingdomHeartsMusicPatcher.utils
{
    // Utility to convert SCD -> WAV and preserve loop points by writing a RIFF smpl chunk.
    public static class ScdToWavConverter
    {
        public static void ConvertWithLoopsPreserved(string scdPath, string wavOutPath)
        {
            // Ensure embedded tools are unpacked to temp and use them
            var toolsSetup = EmbeddedResourceManager.SetupTools(Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic_Tools"));
            string tools = toolsSetup.ToolsDirectory;
            ConvertWithLoopsPreserved(scdPath, wavOutPath, tools, toolsSetup);
        }

        // toolsRoot: base tools folder containing vgmstream/oggdec/ffmpeg subfolders if available
        public static void ConvertWithLoopsPreserved(string scdPath, string wavOutPath, string toolsRoot)
            => ConvertWithLoopsPreserved(scdPath, wavOutPath, toolsRoot, null);

        private static void ConvertWithLoopsPreserved(string scdPath, string wavOutPath, string toolsRoot, ToolsSetupResult? setup)
        {
            if (!File.Exists(scdPath)) throw new FileNotFoundException("SCD not found", scdPath);
            Directory.CreateDirectory(Path.GetDirectoryName(wavOutPath)!);

            Logger.Log($"[ScdToWav] start | scd='{scdPath}', out='{wavOutPath}', tools='{toolsRoot}'");

            byte[] scdBytes = File.ReadAllBytes(scdPath);
            if (!TryGetFirstEntry(scdBytes, out var entry))
                throw new Exception("Invalid SCD: could not locate first entry");

            var loopInfo = ExtractLoopInfoFromEntry(entry, out var sampleRate);
            Logger.Log($"[ScdToWav] LoopInfo | has={loopInfo.HasLoop}, start={loopInfo.LoopStartSample}, endIncl={loopInfo.LoopEndInclusive}, sr={sampleRate}");

            // Resolve tools (prefer extracted embedded ones)
            string oggdec = string.Empty;
            string ffmpeg = string.Empty;
            string vgmstream = string.Empty;

            if (setup != null)
            {
                if (setup.HasFfmpeg) ffmpeg = setup.FfmpegPath;
                if (setup.HasVgmstream) vgmstream = setup.VgmstreamPath;
            }

            string? devRoot = TryGetProjectRoot();
            if (string.IsNullOrEmpty(ffmpeg))
            {
                ffmpeg = ResolveDecoderCandidate(new[]
                {
                    Path.Combine(toolsRoot, "ffmpeg", "ffmpeg.exe"),
                    Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "ffmpeg.exe"),
                    devRoot == null ? null : Path.Combine(devRoot, "utils", "Actually", "ffmpeg.exe"),
                    Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                    FindOnPath("ffmpeg.exe"),
                }) ?? string.Empty;
            }
            if (string.IsNullOrEmpty(vgmstream))
            {
                vgmstream = ResolveDecoderCandidate(new[]
                {
                    Path.Combine(toolsRoot, "vgmstream", "vgmstream-cli.exe"),
                    Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "vgmstream-cli.exe"),
                    Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "vgmstream", "vgmstream-cli.exe"),
                    devRoot == null ? null : Path.Combine(devRoot, "utils", "Actually", "vgmstream-cli.exe"),
                    devRoot == null ? null : Path.Combine(devRoot, "utils", "Actually", "vgmstream", "vgmstream-cli.exe"),
                    Path.Combine(AppContext.BaseDirectory, "vgmstream-cli.exe"),
                    FindOnPath("vgmstream-cli.exe"),
                }) ?? string.Empty;
            }
            // oggdec is optional; we can use ffmpeg
            oggdec = ResolveDecoderCandidate(new[]
            {
                Path.Combine(toolsRoot, "oggdec", "oggdec.exe"),
                Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "oggdec.exe"),
                devRoot == null ? null : Path.Combine(devRoot, "utils", "Actually", "oggdec.exe"),
                Path.Combine(AppContext.BaseDirectory, "oggdec.exe"),
                FindOnPath("oggdec.exe"),
            }) ?? string.Empty;

            Logger.Log($"[ScdToWav] Tools | vgmstream='{vgmstream}', oggdec='{oggdec}', ffmpeg='{ffmpeg}'");

            bool decoded = false;
            string? tempOggPath = null;

            int parsedTotalSamples = -1;
            int parsedLoopStart = -1;
            int parsedLoopEnd = -1;
            int parsedSampleRate = -1;

            try
            {
                // Prefer extracting embedded OGG and decoding with oggdec/ffmpeg (gives exact full stream without loop semantics)
                if (TryExtractOggFromEntry(entry, out tempOggPath))
                {
                    bool triedExternal = false;

                    if (!string.IsNullOrEmpty(oggdec) && File.Exists(oggdec))
                    {
                        triedExternal = true;
                        try
                        {
                            RunProcess(oggdec, $"\"{tempOggPath}\" -o \"{wavOutPath}\"", Path.GetDirectoryName(tempOggPath)!);
                            decoded = File.Exists(wavOutPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"[ScdToWav] oggdec failed, will try ffmpeg/vgmstream fallback: {ex.Message}");
                            decoded = false;
                        }
                    }

                    if (!decoded && !string.IsNullOrEmpty(ffmpeg) && File.Exists(ffmpeg))
                    {
                        triedExternal = true;
                        try
                        {
                            RunProcess(ffmpeg, $"-y -i \"{tempOggPath}\" -c:a pcm_s16le \"{wavOutPath}\"", Path.GetDirectoryName(tempOggPath)!);
                            decoded = File.Exists(wavOutPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"[ScdToWav] ffmpeg failed, will try vgmstream fallback: {ex.Message}");
                            decoded = false;
                        }
                    }

                    if (!decoded)
                    {
                        if (!triedExternal)
                            Logger.Log("[ScdToWav] oggdec/ffmpeg not found; falling back to vgmstream");
                        else
                            Logger.Log("[ScdToWav] External OGG decode failed; falling back to vgmstream");
                    }
                }

                // Fallback to vgmstream when OGG extraction unavailable or decode failed
                if (!decoded)
                {
                    if (!string.IsNullOrEmpty(vgmstream) && File.Exists(vgmstream))
                    {
                        // Decode full stream once ignoring loops: -E (ignore loops), -l 1 (single pass), -f 0 (no fade)
                        string stdout = RunProcessCapture(vgmstream, $"-E -l 1 -f 0 -o \"{wavOutPath}\" \"{scdPath}\"", Path.GetDirectoryName(scdPath)!);
                        decoded = File.Exists(wavOutPath);

                        // Parse vgmstream stdout for loop + total info
                        if (TryParseVgmstreamStdout(stdout, out int sr, out int ls, out int le, out int total))
                        {
                            parsedSampleRate = sr;
                            parsedLoopStart = ls;
                            parsedLoopEnd = le; // typically exclusive
                            parsedTotalSamples = total;
                            Logger.Log($"[ScdToWav] Parsed vgmstream info | sr={sr}, loopStart={ls}, loopEnd={le}, total={total}");
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(tempOggPath))
                        {
                            // Build a helpful error with search paths
                            var searched = new StringBuilder();
                            searched.AppendLine(Path.Combine(toolsRoot, "vgmstream", "vgmstream-cli.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "vgmstream-cli.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "vgmstream", "vgmstream-cli.exe"));
                            if (devRoot != null) searched.AppendLine(Path.Combine(devRoot, "utils", "Actually", "vgmstream-cli.exe"));
                            if (devRoot != null) searched.AppendLine(Path.Combine(devRoot, "utils", "Actually", "vgmstream", "vgmstream-cli.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "vgmstream-cli.exe"));
                            searched.AppendLine(Path.Combine(toolsRoot, "oggdec", "oggdec.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "oggdec.exe"));
                            if (devRoot != null) searched.AppendLine(Path.Combine(devRoot, "utils", "Actually", "oggdec.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "oggdec.exe"));
                            searched.AppendLine(Path.Combine(toolsRoot, "ffmpeg", "ffmpeg.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "utils", "Actually", "ffmpeg.exe"));
                            if (devRoot != null) searched.AppendLine(Path.Combine(devRoot, "utils", "Actually", "ffmpeg.exe"));
                            searched.AppendLine(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"));
                            searched.AppendLine("PATH: " + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty));
                            throw new Exception("Decoder not found. Place vgmstream-cli.exe (recommended), oggdec.exe or ffmpeg.exe under tools.\nSearched:\n" + searched.ToString());
                        }
                        else
                        {
                            throw new Exception("OGG extracted but no decoder (oggdec/ffmpeg) or vgmstream available.");
                        }
                    }
                }
            }
            finally
            {
                try { if (!string.IsNullOrEmpty(tempOggPath) && File.Exists(tempOggPath)) File.Delete(tempOggPath); } catch { }
            }

            if (!decoded) throw new Exception("Audio decode failed; output WAV was not produced.");

            // Prefer sample rate from vgmstream, but keep SCD loop points when available
            if (parsedSampleRate > 0) sampleRate = parsedSampleRate;

            // Validate SCD loop data against parsed total (if any). If invalid, clear to allow fallback
            if (loopInfo.HasLoop)
            {
                bool looksValid = IsReasonableLoop(loopInfo, parsedTotalSamples);
                if (!looksValid)
                {
                    Logger.Log($"[ScdToWav] SCD loop looked invalid for this stream (total={parsedTotalSamples}); will try vgmstream loop markers instead");
                    loopInfo.HasLoop = false;
                }
            }

            // Fall back to vgmstream loop values only if SCD didn't provide valid ones
            if (!loopInfo.HasLoop && parsedLoopStart >= 0 && parsedLoopEnd > parsedLoopStart)
            {
                loopInfo.HasLoop = true;
                // vgmstream loop end is typically exclusive
                loopInfo.LoopStartSample = parsedLoopStart;
                loopInfo.LoopEndInclusive = parsedLoopEnd - 1;
            }

            // Inject SMPL loop if present
            if (loopInfo.HasLoop)
            {
                try
                {
                    if (parsedTotalSamples > 0)
                        ClampLoopWithTotal(ref loopInfo, parsedTotalSamples);
                    else
                        ClampLoopToWav(wavOutPath, ref loopInfo);

                    WriteSmplLoopChunk(wavOutPath, loopInfo, sampleRate);
                    Logger.Log($"[ScdToWav] SMPL loop written | start={loopInfo.LoopStartSample}, endIncl={loopInfo.LoopEndInclusive}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ScdToWav] Failed to write SMPL loop: {ex}");
                }
            }

            Logger.Log("[ScdToWav] done");
        }

        private static bool IsReasonableLoop(LoopInfo loop, int total)
        {
            if (!loop.HasLoop) return false;
            if (loop.LoopStartSample < 0) return false;
            if (loop.LoopEndInclusive < 0) return false;
            if (total > 0)
            {
                if (loop.LoopStartSample >= total) return false;
                if (loop.LoopEndInclusive >= total) return false;
                if (loop.LoopEndInclusive <= loop.LoopStartSample) return false;
            }
            // Reject ultra-tiny starts (garbage) if total is known
            if (total > 0 && loop.LoopStartSample < Math.Min(1024, total / 10000)) return false; // <0.01% or <1024 samples
            return true;
        }

        public static string ResolveToolsPath(string baseDir)
        {
            var candidates = new[]
            {
                Path.Combine(baseDir, "tools"),
                Path.Combine(baseDir, "utils", "SingleEncoder", "tools"),
            };
            foreach (var p in candidates) if (Directory.Exists(p)) return p;
            return Path.Combine(baseDir, "tools");
        }

        private static string? ResolveDecoderCandidate(IEnumerable<string?> candidates)
        {
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                try { if (File.Exists(c)) return c; } catch { }
            }
            return null;
        }

        private static string? FindOnPath(string exeName)
        {
            try
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(pathEnv)) return null;
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    var d = (dir ?? string.Empty).Trim();
                    if (d.Length == 0) continue;
                    string p = Path.Combine(d, exeName);
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        private static string? TryGetProjectRoot()
        {
            try
            {
                // Go up 4 levels from bin folder to repo root (same pattern used elsewhere in app)
                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            }
            catch { return null; }
        }

        private static void ClampLoopWithTotal(ref LoopInfo loop, int total)
        {
            if (!loop.HasLoop) return;
            if (total <= 0) { loop.HasLoop = false; return; }
            if (loop.LoopStartSample < 0) loop.LoopStartSample = 0;
            if (loop.LoopStartSample >= total) loop.LoopStartSample = total - 1;
            if (loop.LoopEndInclusive < 0) loop.LoopEndInclusive = total - 1;
            if (loop.LoopEndInclusive >= total) loop.LoopEndInclusive = total - 1;
            if (loop.LoopEndInclusive <= loop.LoopStartSample) loop.HasLoop = false;
        }

        private static string RunProcessCapture(string exe, string args, string? cwd)
        {
            Logger.Log($"[ScdToWav] Run: {exe} {args}");
            using var p = new Process();
            p.StartInfo.FileName = exe;
            p.StartInfo.Arguments = args;
            var exeDir = Path.GetDirectoryName(exe);
            p.StartInfo.WorkingDirectory = !string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir)
                ? exeDir
                : (cwd ?? Environment.CurrentDirectory);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbOut.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbErr.AppendLine(e.Data); };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
            Logger.Log($"[ScdToWav] Exit={p.ExitCode}\nSTDOUT:\n{sbOut}\nSTDERR:\n{sbErr}");
            if (p.ExitCode != 0)
            {
                string hint = string.Empty;
                if (p.ExitCode == unchecked((int)0xC0000135))
                {
                    hint = "Missing dependencies for the external tool. If this is vgmstream-cli.exe, copy ALL DLLs from the vgmstream Windows release next to the .exe (e.g., libvgmstream.dll, libvorbis*.dll, libmpg123-0.dll, libgcc_s_*.dll, libstdc++-6.dll, libwinpthread-1.dll, codec DLLs). Alternatively, use a static build or add that folder to PATH.";
                }
                else if (p.ExitCode == unchecked((int)0xC000007B))
                {
                    hint = "Invalid image format (32/64-bit mismatch). Ensure the tool architecture matches this app (x64) and its DLLs are the same architecture.";
                }
                var toolName = Path.GetFileName(exe);
                var extra = string.IsNullOrEmpty(hint) ? string.Empty : $"\nHint: {hint}";
                throw new Exception($"{toolName} failed with exit code {p.ExitCode}{extra}");
            }
            return sbOut.ToString();
        }

        private static bool TryParseVgmstreamStdout(string stdout, out int sampleRate, out int loopStart, out int loopEnd, out int totalSamples)
        {
            sampleRate = -1; loopStart = -1; loopEnd = -1; totalSamples = -1;
            if (string.IsNullOrEmpty(stdout)) return false;
            try
            {
                using var sr = new StringReader(stdout);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("sample rate:", StringComparison.OrdinalIgnoreCase))
                    {
                        // sample rate: 44100 Hz
                        var parts = line.Split(' ');
                        foreach (var part in parts)
                        {
                            if (int.TryParse(part, out var v)) { sampleRate = v; break; }
                        }
                    }
                    else if (line.StartsWith("loop start:", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0)
                        {
                            var rest = line[(idx + 1)..].Trim();
                            var num = rest.Split(' ')[0];
                            if (int.TryParse(num, out var v)) loopStart = v;
                        }
                    }
                    else if (line.StartsWith("loop end:", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0)
                        {
                            var rest = line[(idx + 1)..].Trim();
                            var num = rest.Split(' ')[0];
                            if (int.TryParse(num, out var v)) loopEnd = v;
                        }
                    }
                    else if (line.StartsWith("stream total samples:", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(':');
                        if (idx >= 0)
                        {
                            var rest = line[(idx + 1)..].Trim();
                            var num = rest.Split(' ')[0];
                            if (int.TryParse(num, out var v)) totalSamples = v;
                        }
                    }
                }
            }
            catch { }
            return sampleRate > 0 && (loopStart >= 0 && loopEnd > loopStart || totalSamples > 0);
        }

        private static void RunProcess(string exe, string args, string? cwd)
        {
            Logger.Log($"[ScdToWav] Run: {exe} {args}");
            using var p = new Process();
            p.StartInfo.FileName = exe;
            p.StartInfo.Arguments = args;
            // Use the tool folder as working directory so adjacent DLLs are resolved correctly
            var exeDir = Path.GetDirectoryName(exe);
            p.StartInfo.WorkingDirectory = !string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir)
                ? exeDir
                : (cwd ?? Environment.CurrentDirectory);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbOut.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) sbErr.AppendLine(e.Data); };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
            Logger.Log($"[ScdToWav] Exit={p.ExitCode}\nSTDOUT:\n{sbOut}\nSTDERR:\n{sbErr}");

            if (p.ExitCode != 0)
            {
                // Map common fatal Windows status codes to helpful messages
                string hint = string.Empty;
                // STATUS_DLL_NOT_FOUND = 0xC0000135 (-1073741515)
                if (p.ExitCode == unchecked((int)0xC0000135))
                {
                    hint = "Missing dependencies for the external tool. If this is vgmstream-cli.exe, copy ALL DLLs from the vgmstream Windows release next to the .exe (e.g., libvgmstream.dll, libvorbis*.dll, libmpg123-0.dll, libgcc_s_*.dll, libstdc++-6.dll, libwinpthread-1.dll, codec DLLs). Alternatively, use a static build or add that folder to PATH.";
                }
                // STATUS_INVALID_IMAGE_FORMAT = 0xC000007B (-1073741701)
                else if (p.ExitCode == unchecked((int)0xC000007B))
                {
                    hint = "Invalid image format (32/64-bit mismatch). Ensure the tool architecture matches this app (x64) and its DLLs are the same architecture.";
                }

                var toolName = Path.GetFileName(exe);
                var extra = string.IsNullOrEmpty(hint) ? string.Empty : $"\nHint: {hint}";
                throw new Exception($"{toolName} failed with exit code {p.ExitCode}{extra}");
            }
        }

        private struct LoopInfo
        {
            public bool HasLoop;
            public int LoopStartSample;
            public int LoopEndInclusive;
        }

        private static LoopInfo ExtractLoopInfoFromEntry(byte[] entry, out int sampleRate)
        {
            sampleRate = (int)SafeRead32(entry, 0x08); // meta+0x08 in our writer (44100 for most streams)
            int loopStartBytes = (int)SafeRead32(entry, 0x10);
            int loopEndBytes = (int)SafeRead32(entry, 0x14);
            int loopStartSample = (int)SafeRead32(entry, 0x28);
            int totalSamplesCount = (int)SafeRead32(entry, 0x2C);

            // Prefer deriving loop sample positions from OGG page granules based on stored byte offsets
            if (TryFindOggPages(entry, out var pageOffsets, out int headerSize) && loopStartBytes > 0)
            {
                int? startSample = null;
                for (int i = 0; i < pageOffsets.Count; i++)
                {
                    int idx = pageOffsets[i];
                    int rel = idx - headerSize;
                    if (rel >= loopStartBytes)
                    {
                        ulong gran = SafeRead64(entry, idx + 6);
                        startSample = gran > int.MaxValue ? int.MaxValue : unchecked((int)gran);
                        break;
                    }
                }
                if (startSample.HasValue)
                {
                    int endSample = -1;
                    if (loopEndBytes > 0)
                    {
                        for (int i = 0; i < pageOffsets.Count; i++)
                        {
                            int idx = pageOffsets[i];
                            int rel = idx - headerSize;
                            if (rel >= loopEndBytes)
                            {
                                ulong gran = SafeRead64(entry, idx + 6);
                                long le = unchecked((long)gran) - 1; // inclusive
                                endSample = le < 0 ? -1 : (le > int.MaxValue ? int.MaxValue : (int)le);
                                break;
                            }
                        }
                    }
                    else if (totalSamplesCount > 0)
                    {
                        endSample = totalSamplesCount - 1;
                    }
                    if (endSample < 0 && pageOffsets.Count > 0)
                    {
                        int lastIdx = pageOffsets[^1];
                        ulong lastGran = SafeRead64(entry, lastIdx + 6);
                        long le = unchecked((long)lastGran) - 1;
                        endSample = le < 0 ? -1 : (le > int.MaxValue ? int.MaxValue : (int)le);
                    }
                    if (endSample > startSample.Value)
                    {
                        return new LoopInfo { HasLoop = true, LoopStartSample = startSample.Value, LoopEndInclusive = endSample };
                    }
                }
            }

            // Fallback: some encoders write absolute sample positions directly at 0x28/0x2C
            if (loopStartSample > 0 && totalSamplesCount > 0 && totalSamplesCount > loopStartSample && totalSamplesCount < 50_000_000)
            {
                return new LoopInfo { HasLoop = true, LoopStartSample = loopStartSample, LoopEndInclusive = totalSamplesCount - 1 };
            }

            return new LoopInfo { HasLoop = false, LoopStartSample = -1, LoopEndInclusive = -1 };
        }

        private static bool TryFindOggPages(byte[] data, out List<int> pageOffsets, out int headerSize)
        {
            pageOffsets = new List<int>(); headerSize = 0;
            var oggs = new byte[] { 0x4F, 0x67, 0x67, 0x53 };
            int cursor = 0; int guard = 0;
            while (cursor >= 0 && cursor < data.Length && guard++ < 10000)
            {
                int idx = Search(data, oggs, cursor);
                if (idx < 0) break;
                pageOffsets.Add(idx);
                cursor = idx + 4;
            }
            if (pageOffsets.Count == 0) return false;
            headerSize = pageOffsets[0];
            for (int i = 0; i < pageOffsets.Count; i++)
            {
                int idx = pageOffsets[i];
                ulong granule = SafeRead64(data, idx + 6);
                if (granule != 0UL) { headerSize = idx; break; }
            }
            return true;
        }

        private static int GetOggPageSize(byte[] data, int pageOffset)
        {
            // Validate header availability
            if (pageOffset < 0 || pageOffset + 27 > data.Length) return 0;
            // 'OggS'
            if (!(data[pageOffset] == 0x4F && data[pageOffset + 1] == 0x67 && data[pageOffset + 2] == 0x67 && data[pageOffset + 3] == 0x53))
                return 0;
            int segCount = data[pageOffset + 26];
            int segTableStart = pageOffset + 27;
            int segTableEnd = segTableStart + segCount;
            if (segTableEnd > data.Length) return 0;
            int payload = 0;
            for (int i = 0; i < segCount; i++) payload += data[segTableStart + i];
            int total = 27 + segCount + payload;
            if (pageOffset + total > data.Length) total = data.Length - pageOffset; // clamp
            return total;
        }

        private static bool TryExtractOggFromEntry(byte[] entry, out string? tempOggPath)
        {
            tempOggPath = null;
            if (!TryFindOggPages(entry, out var pages, out int headerSize)) return false;
            if (pages.Count == 0) return false;

            // Start from the very first Ogg page so codec headers are present
            int oggStart = pages[0];
            // Compute end of last page using the page's own segment table
            int lastPage = pages[^1];
            int lastSize = GetOggPageSize(entry, lastPage);
            if (lastSize <= 0) lastSize = entry.Length - lastPage; // fallback
            int oggEnd = Math.Min(entry.Length, lastPage + lastSize);
            int oggLen = oggEnd - oggStart;
            if (oggLen <= 0) return false;

            string tmp = Path.Combine(Path.GetTempPath(), $"scd_extract_{Guid.NewGuid():N}.ogg");
            File.WriteAllBytes(tmp, new ReadOnlySpan<byte>(entry, oggStart, oggLen).ToArray());
            tempOggPath = tmp;
            Logger.Log($"[ScdToWav] OGG extracted to temp: '{tempOggPath}', size={oggLen} (start={oggStart}, end={oggEnd}, pages={pages.Count})");
            return true;
        }

        private static bool TryGetFirstEntry(byte[] scd, out byte[] entry)
        {
            entry = Array.Empty<byte>();
            try
            {
                uint tablesOffset = SafeRead16(scd, 0x0E);
                uint headersEntries = SafeRead16(scd, (int)tablesOffset + 0x04);
                uint headersOffset = SafeRead32(scd, (int)tablesOffset + 0x0C);
                if (headersEntries == 0) return false;
                uint entryBegin = SafeRead32(scd, (int)headersOffset + 0x00);
                uint entryEnd = (headersEntries == 1) ? (uint)scd.Length : SafeRead32(scd, (int)headersOffset + 0x04);
                uint entrySize = entryEnd - entryBegin;
                if (entrySize <= 0 || entryBegin + entrySize > scd.Length) return false;
                entry = new byte[entrySize];
                Buffer.BlockCopy(scd, (int)entryBegin, entry, 0, (int)entrySize);
                return true;
            }
            catch { return false; }
        }

        private static void ClampLoopToWav(string wavPath, ref LoopInfo loop)
        {
            if (!loop.HasLoop) return;
            int total = WavSampleAnalyzer.GetTotalSamples_NoNAudio(wavPath);
            if (total <= 0) { loop.HasLoop = false; return; }
            if (loop.LoopStartSample < 0) loop.LoopStartSample = 0;
            if (loop.LoopStartSample >= total) loop.LoopStartSample = total - 1;
            if (loop.LoopEndInclusive < 0) loop.LoopEndInclusive = total - 1;
            if (loop.LoopEndInclusive >= total) loop.LoopEndInclusive = total - 1;
            if (loop.LoopEndInclusive <= loop.LoopStartSample) loop.HasLoop = false;
        }

        private static void WriteSmplLoopChunk(string wavPath, LoopInfo loop, int sampleRate)
        {
            byte[] wav = File.ReadAllBytes(wavPath);
            if (!IsRiffWave(wav)) throw new Exception("Output is not a valid RIFF/WAVE file");
            int pos = 12; // after RIFF/WAVE
            int dataPos = -1;
            int dataSize = 0;
            while (pos + 8 <= wav.Length)
            {
                uint ckId = SafeRead32BE(wav, pos);
                int ckSize = (int)SafeRead32(wav, pos + 4);
                int next = pos + 8 + ckSize + (ckSize % 2 == 1 ? 1 : 0);
                if (ckId == 0x64617461) // 'data'
                {
                    dataPos = pos; dataSize = ckSize; break;
                }
                pos = next;
            }
            if (dataPos < 0) throw new Exception("WAV data chunk not found");

            byte[] smpl = BuildSmplChunk(loop, sampleRate);
            // Insert SMPL after the data chunk for better compatibility
            int insertAt = dataPos + 8 + dataSize + (dataSize % 2 == 1 ? 1 : 0);
            byte[] outWav = new byte[wav.Length + smpl.Length];
            Buffer.BlockCopy(wav, 0, outWav, 0, insertAt);
            Buffer.BlockCopy(smpl, 0, outWav, insertAt, smpl.Length);
            Buffer.BlockCopy(wav, insertAt, outWav, insertAt + smpl.Length, wav.Length - insertAt);
            int riffSize = outWav.Length - 8; WriteInt32(outWav, 4, riffSize);
            File.WriteAllBytes(wavPath, outWav);
        }

        private static byte[] BuildSmplChunk(LoopInfo loop, int sampleRate)
        {
            int chunkDataSize = 36 + 24; // one loop
            int chunkSize = chunkDataSize;
            byte[] smpl = new byte[8 + chunkDataSize];
            // 'smpl'
            smpl[0] = (byte)'s'; smpl[1] = (byte)'m'; smpl[2] = (byte)'p'; smpl[3] = (byte)'l';
            WriteInt32(smpl, 4, chunkSize);
            // header fields
            WriteUInt32(smpl, 8, 0u); // Manufacturer
            WriteUInt32(smpl, 12, 0u); // Product
            uint samplePeriod = sampleRate > 0 ? (uint)(1000000000.0 / sampleRate) : 0u; // ns per sample
            WriteUInt32(smpl, 16, samplePeriod);
            WriteUInt32(smpl, 20, 60u); // MIDIUnityNote
            WriteUInt32(smpl, 24, 0u);  // MIDIPitchFraction
            WriteUInt32(smpl, 28, 0u);  // SMPTEFormat
            WriteUInt32(smpl, 32, 0u);  // SMPTEOffset
            WriteUInt32(smpl, 36, 1u);
            WriteUInt32(smpl, 40, 0u);
            // loop
            WriteUInt32(smpl, 44, 0u);  // CuePointID
            WriteUInt32(smpl, 48, 0u);  // Type (forward)
            WriteUInt32(smpl, 52, unchecked((uint)Math.Max(0, loop.LoopStartSample)));
            WriteUInt32(smpl, 56, unchecked((uint)Math.Max(loop.LoopStartSample, loop.LoopEndInclusive)));
            WriteUInt32(smpl, 60, 0u);  // Fraction
            WriteUInt32(smpl, 64, 0u);  // PlayCount (0=infinite)
            return smpl;
        }

        private static bool IsRiffWave(byte[] wav)
        {
            if (wav.Length < 12) return false;
            return wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F' &&
                   wav[8] == 'W' && wav[9] == 'A' && wav[10] == 'V' && wav[11] == 'E';
        }

        private static int Search(byte[] data, byte[] pattern, int start)
        {
            for (int i = start; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static ushort SafeRead16(byte[] data, int pos)
        {
            if (pos < 0 || pos + 2 > data.Length) return 0;
            return BitConverter.ToUInt16(data, pos);
        }
        private static uint SafeRead32(byte[] data, int pos)
        {
            if (pos < 0 || pos + 4 > data.Length) return 0;
            return BitConverter.ToUInt32(data, pos);
        }
        private static ulong SafeRead64(byte[] data, int pos)
        {
            if (pos < 0 || pos + 8 > data.Length) return 0UL;
            return BitConverter.ToUInt64(data, pos);
        }
        private static uint SafeRead32BE(byte[] data, int pos)
        {
            if (pos < 0 || pos + 4 > data.Length) return 0;
            uint b0 = data[pos], b1 = data[pos + 1], b2 = data[pos + 2], b3 = data[pos + 3];
            return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
        }
        private static void WriteInt32(byte[] data, int pos, int value)
        {
            var b = BitConverter.GetBytes(value);
            Buffer.BlockCopy(b, 0, data, pos, 4);
        }
        private static void WriteUInt32(byte[] data, int pos, uint value)
        {
            var b = BitConverter.GetBytes(value);
            Buffer.BlockCopy(b, 0, data, pos, 4);
        }
    }
}
