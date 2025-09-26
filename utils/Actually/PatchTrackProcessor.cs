using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text;

namespace KingdomHeartsMusicPatcher.utils
{
    internal static class PatchTrackProcessor
    {
        public static List<TrackListLoader.TrackInfo> ProcessTracks(
            List<(TrackListLoader.TrackInfo Track, string Value)> snapshot,
            string encoderDir,
            string scdTemplate,
            string patchBasePath,
            Action<int, int, string, int>? progress,
            bool isReCOM)
        {
            var includedTracks = new List<TrackListLoader.TrackInfo>();

            var trackGroups = snapshot
                .Where(tb => !string.IsNullOrWhiteSpace(tb.Value) && File.Exists(tb.Value))
                .GroupBy(tb => tb.Value)
                .ToDictionary(g => g.Key, g => g.Select(tb => tb.Track).ToList());

            Logger.Log($"ProcessTracks: distinct input files: {trackGroups.Count}");
            Logger.Log($"ProcessTracks: encoderDir='{encoderDir}', scdTemplate='{scdTemplate}', patchBasePath='{patchBasePath}', isReCOM={isReCOM}");

            int totalEncodes = trackGroups.Count;
            int completedEncodes = 0;
            progress?.Invoke(0, totalEncodes, "Preparing", 0);
            progress?.Invoke(0, totalEncodes, "Encoding", 0);

            var generatedScds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in trackGroups)
            {
                string filePath = kvp.Key;
                List<TrackListLoader.TrackInfo> tracks = kvp.Value;

                try
                {
                    progress?.Invoke(completedEncodes + 1, totalEncodes, "Encoding", 0);

                    Logger.Log($"Encoding source: '{filePath}' for {tracks.Count} track(s)");

                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext == ".scd")
                    {
                        Logger.Log($"Input is .scd; skipping encode and using directly: '{filePath}'");
                        generatedScds[filePath] = filePath;
                        completedEncodes++;
                        progress?.Invoke(completedEncodes, totalEncodes, "Encoding", 100);
                        continue;
                    }

                    string tempWavPath = WavProcessingHelper.EnsureWavFormat(filePath);
                    bool shouldDeleteTempWav = !string.Equals(tempWavPath, filePath, StringComparison.OrdinalIgnoreCase);
                    Logger.Log($"WAV ready at: '{tempWavPath}' (exists: {File.Exists(tempWavPath)}), willDeleteTemp={shouldDeleteTempWav}");

                    int totalSamples = WavSampleAnalyzer.GetTotalSamples(tempWavPath);
                    Logger.Log($"Total samples: {totalSamples}");

                    // Decide fullLoop based on presence of loop points
                    bool hasLoopPoints = false;
                    try
                    {
                        if (string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            hasLoopPoints = WavSampleAnalyzer.HasLoopPoints(filePath, out _, out _);
                        }
                        else
                        {
                            hasLoopPoints = WavSampleAnalyzer.HasLoopPoints(tempWavPath, out _, out _);
                        }
                    }
                    catch { }
                    bool fullLoop = !hasLoopPoints;
                    Logger.Log($"Loop detection: hasLoopPoints={hasLoopPoints}, fullLoop={fullLoop}");

                    string encoderWavPath = Path.Combine(encoderDir, "music.wav");
                    File.Copy(tempWavPath, encoderWavPath, overwrite: true);
                    Logger.Log($"Copied WAV to encoder dir: '{encoderWavPath}' (exists: {File.Exists(encoderWavPath)})");

                    bool scdBuilt = false;
                    string outputDir = Path.Combine(encoderDir, "output");
                    Directory.CreateDirectory(outputDir);

                    try
                    {
                        if (isReCOM)
                        {
                            Logger.Log($"Calling ManagedSingleEncoder_ReCOM.Encode with template='{scdTemplate}'");
                            ManagedSingleEncoder_ReCOM.Encode(
                                scdTemplate,
                                encoderWavPath,
                                quality: 10,
                                fullLoop: fullLoop,
                                encoderDir: encoderDir,
                                progress: p => progress?.Invoke(completedEncodes + 1, totalEncodes, "Encoding", p)
                            );
                        }
                        else
                        {
                            Logger.Log($"Calling ManagedSingleEncoder.Encode with template='{scdTemplate}'");
                            ManagedSingleEncoder.Encode(
                                scdTemplate,
                                encoderWavPath,
                                quality: 10,
                                fullLoop: fullLoop,
                                encoderDir: encoderDir,
                                progress: p => progress?.Invoke(completedEncodes + 1, totalEncodes, "Encoding", p)
                            );
                        }
                        scdBuilt = true;
                    }
                    catch (Exception exSingle)
                    {
                        Logger.Log($"Managed encoder failed. Exception: {exSingle}");
                        throw new Exception("SingleEncoder failed. The fallback has been disabled because its output is not supported by the game. Please ensure the SCD template matches the expected version and tools (oggenc/adpcmencode3) are available.", exSingle);
                    }

                    string generatedScdPath = Path.Combine(encoderDir, "output", "original.scd");
                    Logger.Log($"Expecting encoder output: '{generatedScdPath}', exists: {File.Exists(generatedScdPath)}");
                    if (!scdBuilt || !File.Exists(generatedScdPath))
                    {
                        Logger.Log($"Encoder output missing. scdBuilt={scdBuilt}, pathExists={File.Exists(generatedScdPath)}");
                        throw new Exception($"Encoder did not produce output at '{generatedScdPath}'");
                    }

                    string hash = Path.GetFileNameWithoutExtension(filePath).GetHashCode().ToString("X");
                    string renamedScd = Path.Combine(encoderDir, "output", $"generated_{hash}.scd");
                    File.Copy(generatedScdPath, renamedScd, overwrite: true);
                    Logger.Log($"Generated SCD copied to: '{renamedScd}'");

                    generatedScds[filePath] = renamedScd;

                    if (shouldDeleteTempWav)
                    {
                        try { File.Delete(tempWavPath); } catch { }
                    }
                    try { File.Delete(encoderWavPath); } catch { }

                    completedEncodes++;
                    progress?.Invoke(completedEncodes, totalEncodes, "Encoding", 100);
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

            static bool IsKh2Location(string loc)
            {
                if (string.IsNullOrWhiteSpace(loc)) return false;
                return loc.IndexOf("kh2", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            foreach (var (track, filePath) in snapshot)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !generatedScds.TryGetValue(filePath, out string sourceScd))
                    continue;

                try
                {
                    bool isBBS = track.Folder.Contains("original\\sound\\win\\bgm");
                    bool isReCOMTrack = track.Folder.Contains("original\\STREAM");
                    bool isDDD = track.Folder.Contains("original\\sound\\jp\\output\\BGM");
                    bool isSpecialFormat = isBBS || isReCOMTrack || isDDD;

                    void CopyTo(string targetPath)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(sourceScd, targetPath, overwrite: true);
                        Logger.Log($"SCD copied: '{sourceScd}' -> '{targetPath}'");
                    }

                    string ResolveBbsFileName()
                    {
                        if (!string.IsNullOrWhiteSpace(track.FileName))
                            return track.FileName!;
                        return $"music{track.PcNumber.PadLeft(3, '0')}.win32.scd";
                    }
                    string ResolveRecomFileName()
                    {
                        if (!string.IsNullOrWhiteSpace(track.FileName))
                            return track.FileName!;
                        return $"{track.PcNumber}.win32.scd";
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
                            else if (isReCOMTrack)
                            {
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
                                    bgmPath = Path.Combine(
                                        patchBasePath,
                                        loc,
                                        track.Folder,
                                        $"music{track.PcNumber}.win32.scd"
                                    );
                                }
                                else
                                {
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