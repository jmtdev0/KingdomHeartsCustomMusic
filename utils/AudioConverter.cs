using NAudio.Wave;
using System.IO;

public static class AudioConverter
{
    public static string ConvertMp3ToWav(string mp3Path, string outputDirectory)
    {
        string wavName = Path.GetFileNameWithoutExtension(mp3Path) + "_converted.wav";
        string wavPath = Path.Combine(outputDirectory, wavName);

        using var reader = new Mp3FileReader(mp3Path);
        using var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
        WaveFileWriter.CreateWaveFile(wavPath, pcmStream);

        return wavPath;
    }
}
