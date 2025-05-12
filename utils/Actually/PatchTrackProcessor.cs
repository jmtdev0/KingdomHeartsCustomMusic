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

            foreach (var (track, filePath) in trackBindings)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    continue;

                try
                {
                    string tempWavPath = WavProcessingHelper.EnsureWavFormat(filePath);
                    int totalSamples = WavSampleAnalyzer.GetTotalSamples(tempWavPath);

                    // Copiar el WAV al directorio del encoder
                    string baseName = $"music{track.PcNumber}";
                    string encoderWavPath = Path.Combine(encoderDir, baseName + ".wav");
                    File.Copy(tempWavPath, encoderWavPath, overwrite: true);

                    RunSingleEncoder(encoderExe, encoderDir, scdTemplate, encoderWavPath, totalSamples);

                    // Ruta de salida del SCD generado
                    string sourceScdPath = Path.Combine(encoderDir, "output", "original.scd");

                    // Copiar a BGM si aplica
                    if (!string.IsNullOrWhiteSpace(track.LocationBgm))
                    {
                        string bgmScdPath = Path.Combine(
                            patchBasePath,
                            track.LocationBgm,
                            "remastered",
                            "amusic",
                            baseName + ".bgm",
                            baseName + ".win32.scd"
                        );

                        Directory.CreateDirectory(Path.GetDirectoryName(bgmScdPath)!);
                        File.Copy(sourceScdPath, bgmScdPath, overwrite: true);
                    }

                    // Copiar a DAT si aplica
                    if (!string.IsNullOrWhiteSpace(track.LocationDat))
                    {
                        string datScdPath = Path.Combine(
                            patchBasePath,
                            track.LocationDat,
                            "remastered",
                            "amusic",
                            baseName + ".dat",
                            baseName + ".win32.scd"
                        );

                        Directory.CreateDirectory(Path.GetDirectoryName(datScdPath)!);
                        File.Copy(sourceScdPath, datScdPath, overwrite: true);
                    }

                    includedTracks.Add(track);

                    File.Delete(tempWavPath);
                    File.Delete(encoderWavPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to process track '{track.Description}':\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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