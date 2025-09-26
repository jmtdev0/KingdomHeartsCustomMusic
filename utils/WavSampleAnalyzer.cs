using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KingdomHeartsMusicPatcher.utils
{
    public static class WavSampleAnalyzer
    {
        public static int GetTotalSamples(string wavPath)
        {
            using var reader = new WaveFileReader(wavPath);
            WaveFormat format = reader.WaveFormat;

            int bytesPerSample = (format.BitsPerSample / 8) * format.Channels;
            if (bytesPerSample == 0)
                throw new Exception("Invalid WAV format: bytesPerSample = 0");

            long dataLength = reader.Length;
            return (int)(dataLength / bytesPerSample);
        }

        public static int GetTotalSamples_NoNAudio(string wavPath)
        {
            byte[] wav = File.ReadAllBytes(wavPath);

            byte[] fmtPattern = Encoding.ASCII.GetBytes("fmt ");
            byte[] dataPattern = Encoding.ASCII.GetBytes("data");

            int fmtPos = SearchBytePattern(wav, fmtPattern);
            int dataPos = SearchBytePattern(wav, dataPattern);

            if (fmtPos == -1 || dataPos == -1)
                throw new Exception("Invalid WAV: missing 'fmt ' or 'data' chunk.");

            // Move to the beginning of the fmt chunk data (skip 'fmt ' + chunk size)
            int fmtDataPos = fmtPos + 8;
            if (fmtDataPos + 16 > wav.Length)
                throw new Exception("Invalid WAV: incomplete 'fmt ' chunk.");

            short numChannels = BitConverter.ToInt16(wav, fmtDataPos + 2);
            short bitsPerSample = BitConverter.ToInt16(wav, fmtDataPos + 14);
            int dataSize = BitConverter.ToInt32(wav, dataPos + 4);

            if (numChannels <= 0 || bitsPerSample <= 0)
                throw new Exception($"Invalid WAV: bitsPerSample={bitsPerSample}, numChannels={numChannels}");

            int bytesPerSample = (bitsPerSample / 8) * numChannels;
            if (bytesPerSample == 0)
                throw new Exception("Invalid WAV: bytesPerSample calculated as 0");

            return dataSize / bytesPerSample;
        }

        // Returns true if the WAV appears to contain loop points using any of these methods:
        // - LoopStart/LoopEnd ASCII tags
        // - RIFF 'smpl' chunk
        // - RIFF 'wsmp' chunk (DirectMusic/WSMP)
        // - RIFF 'cue ' chunk (>= 2 cue points heuristic)
        // Outputs discovered LoopStart and TotalSamples (endIndex+1) when available, -1 otherwise.
        public static bool HasLoopPoints(string wavPath, out int loopStartSample, out int totalSamplesCount)
        {
            loopStartSample = -1; totalSamplesCount = -1;
            try
            {
                byte[] data = File.ReadAllBytes(wavPath);
                // 1) textual tags
                int loopStart = SearchTag("LoopStart", data);
                int loopEndCount = SearchTag("LoopEnd", data);
                if (loopStart != -1 || loopEndCount != -1)
                {
                    loopStartSample = loopStart;
                    totalSamplesCount = loopEndCount;
                    return true;
                }
                // 2) RIFF smpl
                if (TryGetLoopFromSmpl(data, out int smplStart, out int smplEndInclusive))
                {
                    loopStartSample = smplStart;
                    totalSamplesCount = smplEndInclusive >= 0 ? smplEndInclusive + 1 : -1;
                    return true;
                }
                // 3) RIFF wsmp
                if (TryGetLoopFromWsmp(data, out int wsmpStart, out int wsmpEndInclusive))
                {
                    loopStartSample = wsmpStart;
                    totalSamplesCount = wsmpEndInclusive >= 0 ? wsmpEndInclusive + 1 : -1;
                    return true;
                }
                // 4) RIFF cue (heuristic)
                if (TryGetLoopFromCue(data, out int cueStart, out int cueEndInclusive))
                {
                    loopStartSample = cueStart;
                    totalSamplesCount = cueEndInclusive >= 0 ? cueEndInclusive + 1 : -1;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetLoopFromSmpl(byte[] wav, out int loopStart, out int loopEndInclusive)
        {
            loopStart = -1; loopEndInclusive = -1;
            try
            {
                byte[] smpl = Encoding.ASCII.GetBytes("smpl");
                int smplPos = SearchBytePattern(wav, smpl);
                if (smplPos < 0 || smplPos + 8 > wav.Length) return false;
                int chunkSize = SafeReadInt32LE(wav, smplPos + 4);
                int dataPos = smplPos + 8;
                if (dataPos + Math.Min(chunkSize, 0x28) > wav.Length) return false;
                uint numLoops = SafeReadUInt32LE(wav, dataPos + 0x1C);
                int loopsBase = dataPos + 0x24;
                for (int i = 0; i < numLoops; i++)
                {
                    int lp = loopsBase + i * 24; // 6 uint32 each
                    if (lp + 24 > wav.Length) break;
                    uint type = SafeReadUInt32LE(wav, lp + 4);
                    uint start = SafeReadUInt32LE(wav, lp + 8);
                    uint end = SafeReadUInt32LE(wav, lp + 12);
                    if (type == 0)
                    {
                        loopStart = unchecked((int)start);
                        loopEndInclusive = unchecked((int)end);
                        return loopStart >= 0 && loopEndInclusive >= 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetLoopFromWsmp(byte[] wav, out int loopStart, out int loopEndInclusive)
        {
            loopStart = -1; loopEndInclusive = -1;
            try
            {
                byte[] wsmp = Encoding.ASCII.GetBytes("wsmp");
                int wsmpPos = SearchBytePattern(wav, wsmp);
                if (wsmpPos < 0 || wsmpPos + 8 > wav.Length) return false;
                int chunkSize = SafeReadInt32LE(wav, wsmpPos + 4);
                int dataPos = wsmpPos + 8;
                if (dataPos + 16 > wav.Length) return false; // header
                uint loopCount = SafeReadUInt32LE(wav, dataPos + 12);
                if (loopCount == 0) return false;
                int loopPos = dataPos + 16; // first WaveSampleLoop
                if (loopPos + 16 > wav.Length) return false;
                // DWORD cbSize; DWORD ulLoopType; DWORD ulLoopStart; DWORD ulLoopLength;
                // Some files omit cbSize; try to detect by sanity.
                uint cbSize = SafeReadUInt32LE(wav, loopPos + 0);
                uint type = SafeReadUInt32LE(wav, loopPos + 4);
                uint start = SafeReadUInt32LE(wav, loopPos + 8);
                uint length = SafeReadUInt32LE(wav, loopPos + 12);
                if (cbSize > 32 && type == 0)
                {
                    // Maybe structure without cbSize; shift by -4
                    type = cbSize; // previous read was actually type
                    start = SafeReadUInt32LE(wav, loopPos + 4);
                    length = SafeReadUInt32LE(wav, loopPos + 8);
                    cbSize = 0;
                }
                if (length > 0)
                {
                    loopStart = unchecked((int)start);
                    loopEndInclusive = unchecked((int)(start + length - 1));
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryGetLoopFromCue(byte[] wav, out int loopStart, out int loopEndInclusive)
        {
            loopStart = -1; loopEndInclusive = -1;
            try
            {
                byte[] cue = Encoding.ASCII.GetBytes("cue ");
                int cuePos = SearchBytePattern(wav, cue);
                if (cuePos < 0 || cuePos + 8 > wav.Length) return false;
                int chunkSize = SafeReadInt32LE(wav, cuePos + 4);
                int dataPos = cuePos + 8;
                if (dataPos + 4 > wav.Length) return false;
                uint pointCount = SafeReadUInt32LE(wav, dataPos + 0);
                if (pointCount == 0) return false;
                int pointBase = dataPos + 4;
                int entrySize = 24; // dwName, dwPosition, fccChunk, dwChunkStart, dwBlockStart, dwSampleOffset
                int available = wav.Length - pointBase;
                int maxPointsWeCanRead = Math.Min((int)pointCount, available / entrySize);
                if (maxPointsWeCanRead <= 0) return false;
                int minOffset = int.MaxValue, maxOffset = -1;
                for (int i = 0; i < maxPointsWeCanRead; i++)
                {
                    int ep = pointBase + i * entrySize;
                    int sampleOffset = unchecked((int)SafeReadUInt32LE(wav, ep + 20));
                    if (sampleOffset < minOffset) minOffset = sampleOffset;
                    if (sampleOffset > maxOffset) maxOffset = sampleOffset;
                }
                if (minOffset != int.MaxValue && maxOffset >= 0)
                {
                    loopStart = minOffset;
                    loopEndInclusive = maxOffset;
                    // Heuristic: require at least two cue points to consider it a loop definition
                    return maxPointsWeCanRead >= 2;
                }
            }
            catch { }
            return false;
        }

        private static int SearchTag(string tag, byte[] data)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(tag);
            int pos = SearchBytePattern(data, pattern);
            if (pos == -1) return -1;
            pos += pattern.Length;
            return GetTagData(pos, data);
        }

        private static int GetTagData(int position, byte[] data)
        {
            if (position < 0 || position >= data.Length) return -1;
            while (position < data.Length && (data[position] - 0x30 < 0 || data[position] - 0x30 > 9)) position++;
            int initial = position;
            while (position < data.Length && (data[position] - 0x30 >= 0 && data[position] - 0x30 <= 9)) position++;
            if (initial >= data.Length || position <= initial) return -1;
            string value = Encoding.ASCII.GetString(data, initial, position - initial);
            return int.TryParse(value, out var tagData) ? tagData : -1;
        }

        private static int SearchBytePattern(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
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

        private static int SafeReadInt32LE(byte[] data, int pos)
        {
            if (pos < 0 || pos + 4 > data.Length) return 0;
            return BitConverter.ToInt32(data, pos);
        }
        private static uint SafeReadUInt32LE(byte[] data, int pos)
        {
            if (pos < 0 || pos + 4 > data.Length) return 0u;
            return BitConverter.ToUInt32(data, pos);
        }
    }
}
