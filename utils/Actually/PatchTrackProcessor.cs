using System.IO;
using System.Windows;
using static KingdomHeartsCustomMusic.utils.TrackListLoader;

namespace KingdomHeartsCustomMusic.utils
{


    public static class PatchTrackProcessor
    {
        public static List<TrackInfo> ProcessTracks(
    List<(TrackInfo Track, string FilePath)> trackBindings,
    string encoderExe,
    string encoderDir,
    string scdTemplate,
    string patchBasePath)
        {
            var includedTracks = new List<TrackInfo>();

            // 1. Group by audio file
            var trackGroups = trackBindings
                .Where(tb => !string.IsNullOrWhiteSpace(tb.FilePath) && File.Exists(tb.FilePath))
                .GroupBy(tb => tb.FilePath)
                .ToDictionary(g => g.Key, g => g.Select(tb => tb.Track).ToList());

            // 2. For each distinct audio: generate SCD only once
            var generatedScds = new Dictionary<string, string>(); // filePath -> generated SCD path

            foreach (var kvp in trackGroups)
            {
                string filePath = kvp.Key;
                List<TrackInfo> tracks = kvp.Value;

                try
                {
                    // Convert to WAV if needed
                    string tempWavPath = WavProcessingHelper.EnsureWavFormat(filePath);
                    int totalSamples = WavSampleAnalyzer.GetTotalSamples(tempWavPath);

                    // Copy to encoder directory
                    string encoderWavPath = Path.Combine(encoderDir, "music.wav");
                    File.Copy(tempWavPath, encoderWavPath, overwrite: true);

                    RunSingleEncoder(encoderExe, encoderDir, scdTemplate, encoderWavPath, totalSamples);

                    // Save the generated SCD
                    string generatedScdPath = Path.Combine(encoderDir, "output", "original.scd");

                    // Move it to a temporary path identifiable by hash or name
                    string hash = Path.GetFileNameWithoutExtension(filePath).GetHashCode().ToString("X");
                    string renamedScd = Path.Combine(encoderDir, "output", $"generated_{hash}.scd");
                    File.Copy(generatedScdPath, renamedScd, overwrite: true);

                    generatedScds[filePath] = renamedScd;

                    // Cleanup
                    File.Delete(tempWavPath);
                    File.Delete(encoderWavPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to encode file '{filePath}':\n{ex.Message}", "Encoding Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 3. For each track: copy the generated SCD to its corresponding path
            foreach (var (track, filePath) in trackBindings)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !generatedScds.TryGetValue(filePath, out string sourceScd))
                    continue;

                try
                {
                    if (!string.IsNullOrWhiteSpace(track.LocationDat))
                    {
                        string datPath = Path.Combine(
                            patchBasePath,
                            track.LocationDat,
                            "remastered",
                            "amusic",
                            $"music{track.PcNumber}.dat",
                            $"music{track.PcNumber}.win32.scd"
                        );

                        Directory.CreateDirectory(Path.GetDirectoryName(datPath)!);
                        File.Copy(sourceScd, datPath, overwrite: true);
                    }

                    if (!string.IsNullOrWhiteSpace(track.LocationBgm))
                    {
                        string bgmPath = Path.Combine(
                            patchBasePath,
                            track.LocationBgm,
                            "remastered",
                            "amusic",
                            $"music{track.PcNumber}.bgm",
                            $"music{track.PcNumber}.win32.scd"
                        );

                        Directory.CreateDirectory(Path.GetDirectoryName(bgmPath)!);
                        File.Copy(sourceScd, bgmPath, overwrite: true);
                    }

                    includedTracks.Add(track);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy SCD for track '{track.Description}':\n{ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }


            return includedTracks;
        }


        private static void RunSingleEncoder(string encoderExe, string encoderDir, string scdTemplate, string wavPath, int totalSamples)
        {
            string baseName = Path.GetFileNameWithoutExtension(wavPath);
            string args = $"\"{scdTemplate}\" \"{wavPath}\" 10 -fl";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = encoderExe,
                Arguments = args,
                UseShellExecute = true,
                CreateNoWindow = true,
                WorkingDirectory = encoderDir
            };

            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    throw new Exception($"SingleEncoder exited with code {proc.ExitCode}");
            }
        }

    }

}