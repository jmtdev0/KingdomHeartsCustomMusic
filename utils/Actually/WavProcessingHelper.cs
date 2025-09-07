using NAudio.Wave;
using System.Diagnostics;
using System.IO;

namespace KingdomHeartsMusicPatcher.utils
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
            try
            {
                using (var reader = new Mp3FileReader(mp3Path))
                using (var pcmStream = WaveFormatConversionStream.CreatePcmStream(reader))
                using (var wavWriter = new WaveFileWriter(wavPath, pcmStream.WaveFormat))
                {
                    pcmStream.CopyTo(wavWriter);
                }
                return wavPath;
            }
            catch (Exception ex)
            {
                Logger.Log($"NAudio MP3->WAV conversion failed, trying ffmpeg. Reason: {ex.Message}");
                if (TryFfmpegConvert(mp3Path, wavPath)) return wavPath;
                Logger.Log("FFmpeg not found or failed to convert MP3 to WAV.");
                throw;
            }
        }

        private static string ConvertMp4ToWav(string mp4Path)
        {
            string wavPath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");
            try
            {
                using (var reader = new MediaFoundationReader(mp4Path))
                using (var wavWriter = new WaveFileWriter(wavPath, reader.WaveFormat))
                {
                    reader.CopyTo(wavWriter);
                }
                return wavPath;
            }
            catch (Exception ex)
            {
                Logger.Log($"MediaFoundation MP4->WAV conversion failed, trying ffmpeg. Reason: {ex.Message}");
                if (TryFfmpegConvert(mp4Path, wavPath)) return wavPath;
                Logger.Log("FFmpeg not found or failed to convert MP4 to WAV.");
                throw;
            }
        }

        private static bool TryFfmpegConvert(string inputPath, string wavPath)
        {
            var ffmpegPath = TryFindFfmpeg();
            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                return false;

            try
            {
                // -y overwrite, -vn no video, pcm_s16le 44.1kHz stereo
                var args = $"-y -i \"{inputPath}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{wavPath}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi)!;
                proc.WaitForExit();
                Logger.Log($"ffmpeg exited {proc.ExitCode} for input '{inputPath}'");
                return proc.ExitCode == 0 && File.Exists(wavPath) && new FileInfo(wavPath).Length > 0;
            }
            catch (Exception ex)
            {
                Logger.Log($"ffmpeg conversion exception: {ex.Message}");
                return false;
            }
        }

        private static string? TryFindFfmpeg()
        {
            try
            {
                // 1) Temp tools dir used by EmbeddedResourceManager
                var tempTool = Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic_Tools", "utils", "ffmpeg.exe");
                if (File.Exists(tempTool)) return tempTool;

                // 2) Next to application
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var appFfmpeg = Path.Combine(baseDir, "ffmpeg.exe");
                if (File.Exists(appFfmpeg)) return appFfmpeg;

                // 3) PATH
                var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var p in pathEnv.Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    var candidate = Path.Combine(p.Trim(), "ffmpeg.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
            return null;
        }
    }
}
