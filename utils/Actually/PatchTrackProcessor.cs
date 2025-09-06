using System.IO;
using System.Windows;
using static KingdomHeartsCustomMusic.utils.TrackListLoader;
using System.Text;

namespace KingdomHeartsCustomMusic.utils
{


    public static class PatchTrackProcessor
    {
        public static List<TrackInfo> ProcessTracks(
    List<(TrackInfo Track, string FilePath)> trackBindings,
    string encoderDir,
    string scdTemplate,
    string patchBasePath,
    Action<int,int,string,int>? onProgress = null)
        {
            var includedTracks = new List<TrackInfo>();

            // 1. Group by audio file (distinct songs to encode)
            var trackGroups = trackBindings
                .Where(tb => !string.IsNullOrWhiteSpace(tb.FilePath) && File.Exists(tb.FilePath))
                .GroupBy(tb => tb.FilePath)
                .ToDictionary(g => g.Key, g => g.Select(tb => tb.Track).ToList());

            Logger.Log($"ProcessTracks: distinct input files: {trackGroups.Count}");
            Logger.Log($"ProcessTracks: encoderDir='{encoderDir}', scdTemplate='{scdTemplate}', patchBasePath='{patchBasePath}'");

            int totalEncodes = trackGroups.Count;
            int completedEncodes = 0; // number of completed items
            // Initial status: before encoding starts
            onProgress?.Invoke(0, totalEncodes, "Preparing", 0);
            onProgress?.Invoke(0, totalEncodes, "Encoding", 0);

            // 2. For each distinct audio: generate SCD only once
            var generatedScds = new Dictionary<string, string>(); // filePath -> generated SCD path

            foreach (var kvp in trackGroups)
            {
                string filePath = kvp.Key;
                List<TrackInfo> tracks = kvp.Value;

                try
                {
                    // Report current progress BEFORE starting this encode
                    onProgress?.Invoke(completedEncodes + 1, totalEncodes, "Encoding", 0);

                    Logger.Log($"Encoding source: '{filePath}' for {tracks.Count} track(s)");

                    // Convert to WAV if needed
                    string tempWavPath = WavProcessingHelper.EnsureWavFormat(filePath);
                    Logger.Log($"WAV ready at: '{tempWavPath}' (exists: {File.Exists(tempWavPath)})");

                    int totalSamples = WavSampleAnalyzer.GetTotalSamples(tempWavPath);
                    Logger.Log($"Total samples: {totalSamples}");

                    // Copy to encoder directory
                    string encoderWavPath = Path.Combine(encoderDir, "music.wav");
                    File.Copy(tempWavPath, encoderWavPath, overwrite: true);
                    Logger.Log($"Copied WAV to encoder dir: '{encoderWavPath}' (exists: {File.Exists(encoderWavPath)})");

                    // In-process encoding using managed SingleEncoder implementation
                    bool scdBuilt = false;
                    string outputDir = Path.Combine(encoderDir, "output");
                    Directory.CreateDirectory(outputDir);

                    try
                    {
                        Logger.Log($"Calling ManagedSingleEncoder.Encode with template='{scdTemplate}'");
                        // Relay sub-progress from the encoder to the UI
                        ManagedSingleEncoder.Encode(
                            scdTemplate,
                            encoderWavPath,
                            quality: 10,
                            fullLoop: true,
                            encoderDir: encoderDir,
                            progress: p => onProgress?.Invoke(completedEncodes + 1, totalEncodes, "Encoding", p)
                        );
                        scdBuilt = true;
                    }
                    catch (Exception exSingle)
                    {
                        Logger.Log($"Managed SingleEncoder failed. Exception: {exSingle}");
                        throw new Exception("SingleEncoder failed. The fallback has been disabled because its output is not supported by the game. Please ensure the SCD template matches the expected version and tools (oggenc/adpcmencode3) are available.", exSingle);
                    }

                    // Save the generated SCD
                    string generatedScdPath = Path.Combine(encoderDir, "output", "original.scd");
                    Logger.Log($"Expecting encoder output: '{generatedScdPath}', exists: {File.Exists(generatedScdPath)}");
                    if (!scdBuilt || !File.Exists(generatedScdPath))
                    {
                        Logger.Log($"Encoder output missing. scdBuilt={scdBuilt}, pathExists={File.Exists(generatedScdPath)}");
                        throw new Exception($"Encoder did not produce output at '{generatedScdPath}'");
                    }

                    // Move it to a temporary path identifiable by hash or name
                    string hash = Path.GetFileNameWithoutExtension(filePath).GetHashCode().ToString("X");
                    string renamedScd = Path.Combine(encoderDir, "output", $"generated_{hash}.scd");
                    File.Copy(generatedScdPath, renamedScd, overwrite: true);
                    Logger.Log($"Generated SCD copied to: '{renamedScd}'");

                    generatedScds[filePath] = renamedScd;

                    // Cleanup temporary files
                    try { File.Delete(tempWavPath); } catch { }
                    try { File.Delete(encoderWavPath); } catch { }

                    // Encoding for this item completed successfully
                    completedEncodes++;
                    onProgress?.Invoke(completedEncodes, totalEncodes, "Encoding", 100);
                }
                catch (Exception ex)
                {
                    Logger.LogException($"Failed to encode file '{filePath}'", ex);
                    try
                    {
                        MessageBox.Show($"Failed to encode file '{filePath}':\n{ex.Message}", "Encoding Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch { }
                }
            }

            Logger.Log($"Generated SCDs: {generatedScds.Count}");

            // Helper to detect KH2 location identifiers
            static bool IsKh2Location(string loc)
            {
                if (string.IsNullOrWhiteSpace(loc)) return false;
                return loc.IndexOf("kh2", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
                        Logger.Log($"SCD copied: '{sourceScd}' -> '{targetPath}'");
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
                        if (!string.IsNullOrWhiteSpace(track.FileName))
                        {
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
                        bool datIsKh2 = IsKh2Location(track.LocationDat);
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
                            if (datIsKh2)
                            {
                                // KH2 layout: put the .win32.scd directly under folder
                                datPath = Path.Combine(
                                    patchBasePath,
                                    track.LocationDat,
                                    track.Folder,
                                    $"music{track.PcNumber}.win32.scd"
                                );
                            }
                            else
                            {
                                datPath = Path.Combine(
                                    patchBasePath,
                                    track.LocationDat,
                                    track.Folder,
                                    $"music{track.PcNumber}.dat",
                                    $"music{track.PcNumber}.win32.scd"
                                );
                            }
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
                            bool locIsKh2 = IsKh2Location(loc);
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
                                if (locIsKh2)
                                {
                                    // KH2: no extra subfolder
                                    bgmPath = Path.Combine(
                                        patchBasePath,
                                        loc,
                                        track.Folder,
                                        $"music{track.PcNumber}.win32.scd"
                                    );
                                }
                                else
                                {
                                    // Formato estándar (KH1)
                                    bgmPath = Path.Combine(
                                        patchBasePath,
                                        loc,
                                        track.Folder,
                                        $"music{track.PcNumber}.bgm",
                                        $"music{track.PcNumber}.win32.scd"
                                    );
                                }
                            }

                            CopyTo(bgmPath);
                        }
                    }

                    includedTracks.Add(track);
                }
                catch (Exception ex)
                {
                    Logger.LogException($"Failed to copy SCD for track '{track.Description}'", ex);
                    try
                    {
                        MessageBox.Show($"Failed to copy SCD for track '{track.Description}':\n{ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch { }
                }
            }


            return includedTracks;
        }
    }

}