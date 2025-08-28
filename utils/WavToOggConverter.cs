using System.Diagnostics;
using System.IO;

namespace KingdomHeartsCustomMusic.utils
{
    public static class WavToOggConverter
    {
        /// <summary>
        /// Converts a WAV file to OGG Vorbis using oggenc.exe
        /// </summary>
        /// <param name="oggencPath">Full path to oggenc.exe</param>
        /// <param name="inputWavPath">Full path to the input WAV file</param>
        /// <param name="outputOggPath">Full path to the output OGG file</param>
        /// <param name="quality">Vorbis quality (0–10), default is 10</param>
        /// <param name="loopStart">Loop start sample (optional)</param>
        /// <param name="loopEnd">Loop end sample (optional)</param>
        public static void ConvertWavToOgg(
            string oggencPath,
            string inputWavPath,
            string outputOggPath,
            int quality = 10,
            int? loopStart = null,
            int? loopEnd = null)
        {
            if (!File.Exists(oggencPath))
                throw new FileNotFoundException("oggenc.exe not found", oggencPath);
            if (!File.Exists(inputWavPath))
                throw new FileNotFoundException("Input WAV not found", inputWavPath);

            var args = new List<string>
        {
            $"\"{inputWavPath}\"",
            $"-o \"{outputOggPath}\"",
            $"-q {quality}"
        };

            if (loopStart.HasValue && loopEnd.HasValue)
            {
                args.Add($"-c LoopStart={loopStart.Value}");
                args.Add($"-c LoopEnd={loopEnd.Value - 1}"); // oggenc expects LoopEnd as last sample index
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = oggencPath,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"oggenc failed:\n{error}\n{output}");
            }
        }
    }
}
