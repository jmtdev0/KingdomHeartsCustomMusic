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
    string patchBasePath,
    Action<int,int,string>? onProgress = null)
        {
            var includedTracks = new List<TrackInfo>();

            // 1. Group by audio file (distinct songs to encode)
            var trackGroups = trackBindings
                .Where(tb => !string.IsNullOrWhiteSpace(tb.FilePath) && File.Exists(tb.FilePath))
                .GroupBy(tb => tb.FilePath)
                .ToDictionary(g => g.Key, g => g.Select(tb => tb.Track).ToList());

            int totalEncodes = trackGroups.Count;
            int currentEncode = 0;
            onProgress?.Invoke(currentEncode, totalEncodes, "Preparing");

            // 2. For each distinct audio: generate SCD only once
            var generatedScds = new Dictionary<string, string>(); // filePath -> generated SCD path

            foreach (var kvp in trackGroups)
            {
                string filePath = kvp.Key;
                List<TrackInfo> tracks = kvp.Value;

                try
                {
                    currentEncode++;
                    onProgress?.Invoke(currentEncode, totalEncodes, "Encoding");

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
                    // Para BBS, ReCOM y DDD, usar solo LocationBgm ya que no tienen LocationDat separado
                    bool isBBS = track.Folder.Contains("original\\sound\\win\\bgm");
                    bool isReCOM = track.Folder.Contains("original\\STREAM");
                    bool isDDD = track.Folder.Contains("original\\sound\\jp\\output\\BGM");
                    bool isSpecialFormat = isBBS || isReCOM || isDDD;

                    // Helper local function to copy to a target path
                    void CopyTo(string targetPath)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(sourceScd, targetPath, overwrite: true);
                    }

                    // Resolve filenames using CSV when available
                    string ResolveBbsFileName()
                    {
                        if (!string.IsNullOrWhiteSpace(track.FileName))
                            return track.FileName!; 
                        return $"music{track.PcNumber.PadLeft(3, '0')}.win32.scd";
                    }
                    string ResolveRecomFileName()
                    {
                        // ReCOM filenames end with .win32.scd per analysis CSV
                        if (!string.IsNullOrWhiteSpace(track.FileName))
                            return track.FileName!;
                        return $"{track.PcNumber}.win32.scd"; // fallback
                    }
                    string ResolveDddFileName()
                    {
                        // DDD uses bgm_XXX in PC, but our PC build expects musicXXX.win32.scd in folder structure.
                        // However, we added Filename (bgm_XXX) only for reference; target file on disk remains musicXXX.win32.scd unless CSV provides a full filename.
                        if (!string.IsNullOrWhiteSpace(track.FileName))
                        {
                            // If CSV Filename has an extension, use it directly; if it's bgm_XXX, map to musicXXX.win32.scd
                            if (track.FileName!.EndsWith(".win32.scd", StringComparison.OrdinalIgnoreCase))
                                return track.FileName!;
                            var digits = new string(track.PcNumber.PadLeft(3, '0'));
                            return $"music{digits}.win32.scd";
                        }
                        return $"music{track.PcNumber.PadLeft(3, '0')}.win32.scd";
                    }

                    if (!string.IsNullOrWhiteSpace(track.LocationDat) && !isSpecialFormat)
                    {
                        string datPath;
                        
                        // Check if it's vagstream (KH2 special case)
                        if (track.Folder.Contains("vagstream"))
                        {
                            datPath = Path.Combine(
                                patchBasePath,
                                track.LocationDat,
                                track.Folder,
                                $"{track.PcNumber}.win32.scd"
                            );
                        }
                        else
                        {
                            // Standard format (KH1 and KH2 bgm)
                            datPath = Path.Combine(
                                patchBasePath,
                                track.LocationDat,
                                track.Folder,
                                $"music{track.PcNumber}.dat",
                                $"music{track.PcNumber}.win32.scd"
                            );
                        }

                        CopyTo(datPath);
                    }

                    if (!string.IsNullOrWhiteSpace(track.LocationBgm))
                    {
                        // BBS can especificar múltiples ubicaciones separadas por comas (ej."bbs_first, bbs_fourth")
                        var bgmLocations = isBBS
                            ? track.LocationBgm.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                            : new[] { track.LocationBgm };

                        foreach (var loc in bgmLocations)
                        {
                            string bgmPath;
                            
                            if (isBBS)
                            {
                                var fileName = ResolveBbsFileName();
                                bgmPath = Path.Combine(
                                    patchBasePath,
                                    loc,
                                    track.Folder,
                                    fileName
                                );
                            }
                            else if (isReCOM)
                            {
                                // ReCOM: usar nombre de archivo del CSV si está presente, de lo contrario, usar pcNumber.win32.scd
                                var fileName = !string.IsNullOrWhiteSpace(track.FileName) ? track.FileName! : ResolveRecomFileName();
                                bgmPath = Path.Combine(
                                    patchBasePath,
                                    loc,
                                    track.Folder,
                                    fileName
                                );
                            }
                            else if (isDDD)
                            {
                                // DDD: usa el mapeo de nombre de archivo cuando está disponible; de lo contrario, usa musicXXX.win32.scd
                                var fileName = !string.IsNullOrWhiteSpace(track.FileName) ? ResolveDddFileName() : $"music{track.PcNumber.PadLeft(3, '0')}.win32.scd";
                                bgmPath = Path.Combine(
                                    patchBasePath,
                                    loc,
                                    track.Folder,
                                    fileName
                                );
                            }
                            else if (track.Folder.Contains("vagstream"))
                            {
                                // Verificar si es vagstream (caso especial de KH2)
                                bgmPath = Path.Combine(
                                    patchBasePath,
                                    loc,
                                    track.Folder,
                                    $"{track.PcNumber}.win32.scd"
                                );
                            }
                            else
                            {
                                // Formato estándar (bgm de KH1 y KH2)
                                bgmPath = Path.Combine(
                                    patchBasePath,
                                    loc,
                                    track.Folder,
                                    $"music{track.PcNumber}.bgm",
                                    $"music{track.PcNumber}.win32.scd"
                                );
                            }

                            CopyTo(bgmPath);
                        }
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
            string args = $"\"{scdTemplate}\" \"{wavPath}\" 10 -fl";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = encoderExe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = encoderDir,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
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