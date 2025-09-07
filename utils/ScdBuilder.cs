using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KingdomHeartsMusicPatcher.utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    public static class ScdBuilder
    {

        public static void ReplaceOggInScd(
    string originalScdPath,
    string oggPath,
    string outputScdPath,
    int loopStartSample,
    int loopEndSample)
        {
            Logger.Log($"ScdBuilder.ReplaceOggInScd: template='{originalScdPath}', ogg='{oggPath}', out='{outputScdPath}', loopStart={loopStartSample}, loopEnd={loopEndSample}");
            if (!File.Exists(originalScdPath))
                throw new FileNotFoundException("SCD template not found.", originalScdPath);
            if (!File.Exists(oggPath))
                throw new FileNotFoundException("OGG file not found.", oggPath);

            byte[] scdTemplate = File.ReadAllBytes(originalScdPath);
            byte[] oggData = File.ReadAllBytes(oggPath);

            if (oggData.Length < 64 || oggData[0] != (byte)'O' || oggData[1] != (byte)'g' || oggData[2] != (byte)'g' || oggData[3] != (byte)'S')
                throw new InvalidDataException("Provided OGG does not start with OggS header.");

            // Read structure
            ushort tablesOffset = ReadUInt16(scdTemplate, 0x0E);
            uint entryTableOffset = ReadUInt32(scdTemplate, tablesOffset + 0x0C);
            uint oldEntryOffset = ReadUInt32(scdTemplate, (int)entryTableOffset);

            // Copy original entry block
            int entryOffset = (int)oldEntryOffset;
            byte[] entry = new byte[scdTemplate.Length - entryOffset];
            Array.Copy(scdTemplate, entryOffset, entry, 0, entry.Length);

            // Layout
            int metaOffset = 0;
            int extradataOffset = metaOffset + 0x20;

            // Vorbis header size
            int vorbisHeaderSize = GetVorbisHeaderSize(oggData);
            if (vorbisHeaderSize <= 0)
                throw new InvalidDataException("Could not detect Vorbis header size in OGG.");
            int streamSize = oggData.Length - vorbisHeaderSize;

            // Audio properties
            byte channels = oggData[0x27];
            int sampleRate = BitConverter.ToInt32(oggData, 0x28);

            // Write metadata
            WriteUInt32(entry, (uint)streamSize, metaOffset + 0x00);
            WriteByte(entry, channels, metaOffset + 0x04);
            WriteUInt32(entry, (uint)sampleRate, metaOffset + 0x08);
            WriteUInt32(entry, (uint)loopStartSample, metaOffset + 0x28);
            WriteUInt32(entry, (uint)loopEndSample, metaOffset + 0x2C);
            WriteUInt32(entry, (uint)0, metaOffset + 0x10); // LoopStartOffset
            WriteUInt32(entry, (uint)oggData.Length, metaOffset + 0x14); // LoopEndOffset

            // No seek table or aux chunks
            WriteUInt32(entry, 0, extradataOffset + 0x10); // Seek table size
            WriteUInt32(entry, (uint)vorbisHeaderSize, extradataOffset + 0x14); // Vorbis header size
            WriteByte(entry, 0x00, extradataOffset + 0x02); // Encryption key = 0

            int extradataSize = 0x20 + vorbisHeaderSize;
            WriteUInt32(entry, (uint)extradataSize, metaOffset + 0x18);

            // Build final result
            List<byte> result = new();
            result.AddRange(scdTemplate.Take(entryOffset));
            int newEntryOffset = result.Count;
            result.AddRange(entry.Take(extradataOffset + 0x20)); // until ogg
            result.AddRange(oggData); // full .ogg

            // Align to 16 bytes
            while (result.Count % 16 != 0)
                result.Add(0);

            byte[] finalData = result.ToArray();

            // Patch table offset and file size
            WriteUInt32(finalData, (uint)newEntryOffset, (int)entryTableOffset);
            WriteUInt32(finalData, (uint)finalData.Length, 0x10);

            File.WriteAllBytes(outputScdPath, finalData);
            Logger.Log($"ScdBuilder: wrote SCD '{outputScdPath}' size={finalData.Length} bytes");
        }



        // ----- Utility methods -----

        private static ushort ReadUInt16(byte[] data, int offset) =>
            BitConverter.ToUInt16(data, offset);

        private static uint ReadUInt32(byte[] data, int offset) =>
            BitConverter.ToUInt32(data, offset);

        private static void WriteUInt32(byte[] data, uint value, int offset)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, data, offset, 4);
        }

        private static void WriteByte(byte[] data, byte value, int offset)
        {
            data[offset] = value;
        }

        private static int GetVorbisHeaderSize(byte[] ogg)
        {
            byte[] pattern = new byte[] { 0x05, 0x76, 0x6F, 0x72, 0x62, 0x69, 0x73 }; // 0x05 'vorbis'
            int offset = SearchBytePattern(ogg, pattern);
            return offset != -1 ? offset : 0x40; // fallback
        }

        private static int SearchBytePattern(byte[] data, byte[] pattern, int start = 0)
        {
            for (int i = start; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }
    }

}
