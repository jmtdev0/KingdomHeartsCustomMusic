using System.Diagnostics;
using System.IO;
using System.Text;

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

            var argsList = new List<string>
            {
                "-Q", // quiet to reduce pipe spam
                $"\"{inputWavPath}\"",
                $"-o \"{outputOggPath}\"",
                $"-q {quality}"
            };

            if (loopStart.HasValue && loopEnd.HasValue)
            {
                argsList.Add($"-c LoopStart={loopStart.Value}");
                argsList.Add($"-c LoopEnd={loopEnd.Value - 1}"); // oggenc expects LoopEnd as last sample index
            }

            string arguments = string.Join(" ", argsList);
            Logger.Log($"Running oggenc: '{oggencPath}' {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = oggencPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = false };
            var sbOut = new StringBuilder();
            var sbErr = new StringBuilder();
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) { sbOut.AppendLine(e.Data); /* keep quiet */ } };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { sbErr.AppendLine(e.Data); Logger.Log($"oggenc ERR: {e.Data}"); } };

            if (!proc.Start())
                throw new Exception("Failed to start oggenc process");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            const int timeoutMs = 300000; // 5 minutes
            if (!proc.WaitForExit(timeoutMs))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException("oggenc timed out.");
            }

            Logger.Log($"oggenc exit code: {proc.ExitCode}");
            if (proc.ExitCode != 0)
            {
                var err = sbErr.ToString();
                var stdout = sbOut.ToString();
                throw new Exception($"oggenc failed (code {proc.ExitCode}).\n{err}\n{stdout}");
            }

            if (!File.Exists(outputOggPath))
            {
                throw new FileNotFoundException("oggenc did not produce the expected output", outputOggPath);
            }
        }
    }
}
