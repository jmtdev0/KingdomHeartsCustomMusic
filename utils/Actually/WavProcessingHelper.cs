using NAudio.Wave;
using System.IO;

namespace KingdomHeartsCustomMusic.utils
{
    public static class WavProcessingHelper
    {
        public static string EnsureWavFormat(string inputPath)
        {
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            if (ext == ".wav")
            {
                return inputPath;
            }
            else if (ext == ".mp3")
            {
                return ConvertMp3ToWav(inputPath);
            }
            else
            {
                throw new InvalidOperationException("Unsupported file format. Only WAV and MP3 are accepted.");
            }
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
    }
}
