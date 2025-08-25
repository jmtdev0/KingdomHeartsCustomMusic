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
            IProgress<string>? progress = null,
            ITrackProgressReporter? trackProgress = null)
        {
            try
            {
                PatchLogger.LogStep("Starting ProcessTracks");
                PatchLogger.Log($"Parameters - EncoderExe: {encoderExe}, EncoderDir: {encoderDir}");
                PatchLogger.Log($"ScdTemplate: {scdTemplate}, PatchBasePath: {patchBasePath}");
                PatchLogger.Log($"Track bindings count: {trackBindings.Count}");

                var includedTracks = new List<TrackInfo>();

                // Ensure all paths are absolute and exist
                PatchLogger.LogStep("Validating encoder paths");
                progress?.Report("🔍 Validating encoder paths...");

                if (!Directory.Exists(encoderDir))
                {
                    PatchLogger.LogError($"Encoder directory not found: {encoderDir}");
                    throw new DirectoryNotFoundException($"Encoder directory not found: {encoderDir}");
                }

                if (!File.Exists(encoderExe))
                {
                    PatchLogger.LogError($"SingleEncoder.exe not found: {encoderExe}");
                    throw new FileNotFoundException($"SingleEncoder.exe not found: {encoderExe}");
                }

                if (!File.Exists(scdTemplate))
                {
                    PatchLogger.LogError($"SCD template not found: {scdTemplate}");
                    throw new FileNotFoundException($"SCD template not found: {scdTemplate}");
                }

                // Ensure output directory exists
                string outputDir = Path.Combine(encoderDir, "output");
                PatchLogger.Log($"Creating output directory: {outputDir}");
                Directory.CreateDirectory(outputDir);

                // 1. Group by audio file
                PatchLogger.LogStep("Grouping tracks by audio file");
                progress?.Report("📊 Analyzing audio files...");

                var trackGroups = trackBindings
                    .Where(tb => !string.IsNullOrWhiteSpace(tb.FilePath) && File.Exists(tb.FilePath))
                    .GroupBy(tb => tb.FilePath)
                    .ToDictionary(g => g.Key, g => g.Select(tb => tb.Track).ToList());

                PatchLogger.Log($"Found {trackGroups.Count} unique audio files to process");
                progress?.Report($"📁 Found {trackGroups.Count} unique audio files to process");

                // 2. For each distinct audio: generate SCD only once
                var generatedScds = new Dictionary<string, string>(); // filePath -> generated SCD path

                int processedCount = 0;
                int totalAudioFiles = trackGroups.Count;

                foreach (var kvp in trackGroups)
                {
                    processedCount++;
                    string filePath = kvp.Key;
                    List<TrackInfo> tracks = kvp.Value;

                    string fileName = Path.GetFileName(filePath);
                    
                    // Report track progress
                    trackProgress?.ReportProgress(new TrackProgress
                    {
                        CurrentTrack = processedCount,
                        TotalTracks = totalAudioFiles,
                        CurrentTrackName = fileName,
                        Phase = "Processing"
                    });

                    PatchLogger.LogStep($"Processing audio file {processedCount}/{totalAudioFiles}: {fileName}");
                    PatchLogger.Log($"Tracks using this file: {tracks.Count}");
                    progress?.Report($"🎵 Processing {processedCount}/{totalAudioFiles}: {fileName}");

                    try
                    {
                        // Convert to WAV if needed
                        PatchLogger.LogStep("Converting to WAV format");
                        progress?.Report($"🔄 Converting to WAV: {fileName}...");
                        
                        string tempWavPath = WavProcessingHelper.EnsureWavFormat(filePath);
                        PatchLogger.Log($"WAV file created: {tempWavPath}");
                        
                        if (!File.Exists(tempWavPath))
                        {
                            throw new FileNotFoundException($"WAV file was not created: {tempWavPath}");
                        }

                        PatchLogger.LogStep("Analyzing WAV samples");
                        progress?.Report($"📊 Analyzing audio samples...");
                        
                        int totalSamples = WavSampleAnalyzer.GetTotalSamples(tempWavPath);
                        PatchLogger.Log($"Total samples: {totalSamples}");

                        // Copy to encoder directory with absolute path
                        PatchLogger.LogStep("Copying WAV to encoder directory");
                        progress?.Report($"📁 Preparing for encoding...");
                        
                        string encoderWavPath = Path.Combine(encoderDir, "music.wav");
                        PatchLogger.Log($"Copying {tempWavPath} to {encoderWavPath}");
                        File.Copy(tempWavPath, encoderWavPath, overwrite: true);
                        PatchLogger.Log("WAV file copied successfully");

                        PatchLogger.LogStep("Running SingleEncoder");
                        progress?.Report($"⚡ Encoding audio: {fileName}... (this may take 1-2 minutes)");
                        
                        RunSingleEncoder(encoderExe, encoderDir, scdTemplate, encoderWavPath, totalSamples, progress);
                        PatchLogger.Log("SingleEncoder completed");

                        // Save the generated SCD
                        string generatedScdPath = Path.Combine(outputDir, "original.scd");
                        PatchLogger.Log($"Looking for generated SCD: {generatedScdPath}");
                        
                        if (!File.Exists(generatedScdPath))
                        {
                            throw new FileNotFoundException($"Generated SCD not found: {generatedScdPath}");
                        }

                        var scdInfo = new FileInfo(generatedScdPath);
                        PatchLogger.Log($"Generated SCD size: {scdInfo.Length} bytes");

                        // Move it to a temporary path identifiable by hash or name
                        string hash = Path.GetFileNameWithoutExtension(filePath).GetHashCode().ToString("X");
                        string renamedScd = Path.Combine(outputDir, $"generated_{hash}.scd");
                        PatchLogger.Log($"Renaming SCD to: {renamedScd}");
                        File.Copy(generatedScdPath, renamedScd, overwrite: true);

                        generatedScds[filePath] = renamedScd;
                        PatchLogger.Log($"SCD generation completed for {fileName}");
                        progress?.Report($"✅ Completed: {fileName}");

                        // Cleanup temporary files
                        PatchLogger.LogStep("Cleaning up temporary files");
                        if (File.Exists(tempWavPath) && tempWavPath != filePath)
                        {
                            File.Delete(tempWavPath);
                            PatchLogger.Log("Temporary WAV deleted");
                        }
                        if (File.Exists(encoderWavPath))
                        {
                            File.Delete(encoderWavPath);
                            PatchLogger.Log("Encoder WAV deleted");
                        }
                    }
                    catch (Exception ex)
                    {
                        PatchLogger.LogError($"Failed to encode file '{fileName}'", ex);
                        progress?.Report($"❌ Failed: {fileName} - {ex.Message}");
                        
                        trackProgress?.ReportError($"Failed to process: {fileName}");
                        
                        // Continue with next file instead of stopping everything
                        continue;
                    }
                }

                PatchLogger.LogStep($"Audio processing completed. Generated {generatedScds.Count} SCD files");
                progress?.Report($"🎵 Audio processing complete! Generated {generatedScds.Count} SCD files");

                // 3. For each track: copy the generated SCD to its corresponding path
                PatchLogger.LogStep("Copying SCD files to patch structure");
                progress?.Report("📁 Building patch structure...");

                int trackCopyCount = 0;
                int totalTracksToCopy = trackBindings.Count(tb => !string.IsNullOrWhiteSpace(tb.FilePath) && generatedScds.ContainsKey(tb.FilePath));

                foreach (var (track, filePath) in trackBindings)
                {
                    if (string.IsNullOrWhiteSpace(filePath) || !generatedScds.TryGetValue(filePath, out string sourceScd))
                        continue;

                    trackCopyCount++;
                    PatchLogger.Log($"Copying SCD for track {trackCopyCount}: {track.Description}");
                    progress?.Report($"📋 Creating patch entry {trackCopyCount}/{totalTracksToCopy}: {track.Description}");

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

                            PatchLogger.Log($"Creating DAT path: {datPath}");
                            Directory.CreateDirectory(Path.GetDirectoryName(datPath)!);
                            File.Copy(sourceScd, datPath, overwrite: true);
                            PatchLogger.Log("DAT SCD copied successfully");
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

                            PatchLogger.Log($"Creating BGM path: {bgmPath}");
                            Directory.CreateDirectory(Path.GetDirectoryName(bgmPath)!);
                            File.Copy(sourceScd, bgmPath, overwrite: true);
                            PatchLogger.Log("BGM SCD copied successfully");
                        }

                        includedTracks.Add(track);
                        PatchLogger.Log($"Track {track.Description} processed successfully");
                    }
                    catch (Exception ex)
                    {
                        PatchLogger.LogError($"Failed to copy SCD for track '{track.Description}'", ex);
                        progress?.Report($"⚠️ Warning: Failed to process {track.Description}");
                    }
                }

                // Report completion
                trackProgress?.ReportCompleted();

                PatchLogger.LogStep($"ProcessTracks completed successfully. Included {includedTracks.Count} tracks");
                progress?.Report($"✅ Track processing complete! Included {includedTracks.Count} tracks");
                
                return includedTracks;
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in ProcessTracks", ex);
                progress?.Report($"❌ Error in track processing: {ex.Message}");
                trackProgress?.ReportError($"Processing failed: {ex.Message}");
                throw;
            }
        }

        private static void RunSingleEncoder(string encoderExe, string encoderDir, string scdTemplate, string wavPath, int totalSamples, IProgress<string>? progress = null)
        {
            try
            {
                PatchLogger.LogStep("Starting RunSingleEncoder");
                PatchLogger.Log($"EncoderExe: {encoderExe}");
                PatchLogger.Log($"EncoderDir: {encoderDir}");
                PatchLogger.Log($"ScdTemplate: {scdTemplate}");
                PatchLogger.Log($"WavPath: {wavPath}");
                PatchLogger.Log($"TotalSamples: {totalSamples}");

                // Use absolute paths for arguments
                string absoluteTemplate = Path.GetFullPath(scdTemplate);
                string absoluteWav = Path.GetFullPath(wavPath);
                
                PatchLogger.Log($"Absolute template path: {absoluteTemplate}");
                PatchLogger.Log($"Absolute WAV path: {absoluteWav}");

                // Verify files exist before running
                if (!File.Exists(absoluteTemplate))
                {
                    throw new FileNotFoundException($"SCD template not found: {absoluteTemplate}");
                }
                if (!File.Exists(absoluteWav))
                {
                    throw new FileNotFoundException($"WAV file not found: {absoluteWav}");
                }

                // Log file sizes to detect potential issues
                var templateInfo = new FileInfo(absoluteTemplate);
                var wavInfo = new FileInfo(absoluteWav);
                PatchLogger.Log($"Template file size: {templateInfo.Length} bytes");
                PatchLogger.Log($"WAV file size: {wavInfo.Length} bytes ({wavInfo.Length / (1024.0 * 1024.0):F2} MB)");

                string args = $"\"{absoluteTemplate}\" \"{absoluteWav}\" 10 -fl";
                PatchLogger.Log($"Command arguments: {args}");

                // Check if we have write permissions in the output directory
                string outputDir = Path.Combine(encoderDir, "output");
                string testFile = Path.Combine(outputDir, "write_test.tmp");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    PatchLogger.Log("Write permissions verified in output directory");
                }
                catch (Exception ex)
                {
                    PatchLogger.LogError($"No write permissions in output directory: {outputDir}", ex);
                    throw new UnauthorizedAccessException($"Cannot write to output directory: {outputDir}");
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.GetFullPath(encoderExe),
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath(encoderDir),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                PatchLogger.Log($"Working directory: {psi.WorkingDirectory}");
                PatchLogger.Log($"Starting process: {psi.FileName}");

                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    if (proc == null)
                    {
                        throw new Exception("Failed to start SingleEncoder process");
                    }

                    PatchLogger.Log($"SingleEncoder process started with PID: {proc.Id}");
                    PatchLogger.Log("SingleEncoder process started, waiting for completion...");
                    progress?.Report("⚡ SingleEncoder running...");

                    // Read output asynchronously to prevent blocking
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();

                    proc.OutputDataReceived += (sender, e) => {
                        if (e.Data != null)
                        {
                            outputBuilder.AppendLine(e.Data);
                            PatchLogger.Log($"SingleEncoder output: {e.Data}");
                        }
                    };

                    proc.ErrorDataReceived += (sender, e) => {
                        if (e.Data != null)
                        {
                            errorBuilder.AppendLine(e.Data);
                            PatchLogger.Log($"SingleEncoder error: {e.Data}");
                        }
                    };

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    // Wait with periodic logging to show progress
                    bool finished = false;
                    int timeoutSeconds = 120; // Increased timeout to 2 minutes
                    int elapsedSeconds = 0;
                    
                    while (!finished && elapsedSeconds < timeoutSeconds)
                    {
                        finished = proc.WaitForExit(5000); // Check every 5 seconds
                        if (!finished)
                        {
                            elapsedSeconds += 5;
                            PatchLogger.Log($"SingleEncoder still running... {elapsedSeconds}s elapsed");
                            progress?.Report($"⚡ Encoding... {elapsedSeconds}s elapsed (max {timeoutSeconds}s)");
                            
                            // Check if the process is still responsive
                            try
                            {
                                if (proc.HasExited)
                                {
                                    finished = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // Process might have exited between checks
                                finished = true;
                                break;
                            }
                        }
                    }
                    
                    if (!finished)
                    {
                        PatchLogger.LogError($"SingleEncoder process timed out after {timeoutSeconds} seconds");
                        progress?.Report($"❌ Encoding timed out after {timeoutSeconds} seconds");
                        
                        try
                        {
                            // Try to get current output before killing
                            var currentOutput = outputBuilder.ToString();
                            var currentError = errorBuilder.ToString();
                            
                            if (!string.IsNullOrEmpty(currentOutput))
                            {
                                PatchLogger.Log($"Output before timeout: {currentOutput}");
                            }
                            if (!string.IsNullOrEmpty(currentError))
                            {
                                PatchLogger.Log($"Error before timeout: {currentError}");
                            }
                            
                            proc.Kill();
                            PatchLogger.Log("SingleEncoder process killed due to timeout");
                        }
                        catch (Exception killEx)
                        {
                            PatchLogger.LogError("Failed to kill SingleEncoder process", killEx);
                        }
                        
                        throw new TimeoutException($"SingleEncoder process timed out after {timeoutSeconds} seconds");
                    }

                    // Process completed, get final output
                    proc.WaitForExit(); // Ensure all output is read
                    string output = outputBuilder.ToString();
                    string error = errorBuilder.ToString();

                    PatchLogger.Log($"SingleEncoder exit code: {proc.ExitCode}");
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        PatchLogger.Log($"SingleEncoder final output: {output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        PatchLogger.Log($"SingleEncoder final error: {error}");
                    }
                    
                    if (proc.ExitCode != 0)
                    {
                        string errorMsg = $"SingleEncoder exited with code {proc.ExitCode}.";
                        if (!string.IsNullOrEmpty(output)) errorMsg += $"\nOutput: {output}";
                        if (!string.IsNullOrEmpty(error)) errorMsg += $"\nError: {error}";
                        
                        PatchLogger.LogError(errorMsg);
                        progress?.Report($"❌ Encoding failed with exit code {proc.ExitCode}");
                        throw new Exception(errorMsg);
                    }

                    // Verify output file was created
                    string expectedOutput = Path.Combine(encoderDir, "output", "original.scd");
                    if (File.Exists(expectedOutput))
                    {
                        var outputInfo = new FileInfo(expectedOutput);
                        PatchLogger.Log($"Output SCD created successfully: {outputInfo.Length} bytes");
                        progress?.Report("✅ Audio encoding completed successfully");
                    }
                    else
                    {
                        PatchLogger.LogError($"Expected output file not created: {expectedOutput}");
                        progress?.Report("❌ Output file not created");
                        throw new FileNotFoundException($"SingleEncoder did not create expected output: {expectedOutput}");
                    }

                    PatchLogger.Log("SingleEncoder completed successfully");
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in RunSingleEncoder", ex);
                progress?.Report($"❌ Encoding error: {ex.Message}");
                throw;
            }
        }
    }
}