using NAudio.Wave;
using System.IO;

namespace KingdomHeartsCustomMusic.utils
{
    public static class WavProcessingHelper
    {
        public static string EnsureWavFormat(string inputPath)
        {
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            return ext switch
            {
                ".wav" => inputPath,
                ".mp3" => ConvertMp3ToWav(inputPath),
                ".mp4" => ConvertMp4ToWav(inputPath),
                _ => throw new InvalidOperationException("Unsupported file format. Only WAV, MP3 and MP4 are accepted.")
            };
        }

        private static string ConvertMp3ToWav(string mp3Path)
        {
            string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");

            using (var reader = new Mp3FileReader(mp3Path))
            using (var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader))
            using (var wavWriter = new WaveFileWriter(wavPath, pcmStream.WaveFormat))
            {
                pcmStream.CopyTo(wavWriter);
            }

            return wavPath;
        }

        private static string ConvertMp4ToWav(string mp4Path)
        {
            string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");

            using (var reader = new MediaFoundationReader(mp4Path))
            using (var wavWriter = new WaveFileWriter(wavPath, reader.WaveFormat))
            {
                reader.CopyTo(wavWriter);
            }

            return wavPath;
        }
    }
}
