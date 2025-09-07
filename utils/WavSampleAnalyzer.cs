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

            short numChannels = BitConverter.ToInt16(wav, fmtPos + 2);
            short bitsPerSample = BitConverter.ToInt16(wav, fmtPos + 14);
            int dataSize = BitConverter.ToInt32(wav, dataPos + 4);

            if (numChannels <= 0 || bitsPerSample <= 0)
                throw new Exception($"Invalid WAV: bitsPerSample={bitsPerSample}, numChannels={numChannels}");

            int bytesPerSample = (bitsPerSample / 8) * numChannels;
            if (bytesPerSample == 0)
                throw new Exception("Invalid WAV: bytesPerSample calculated as 0");

            return dataSize / bytesPerSample;
        }


        private static int SearchBytePattern(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
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
