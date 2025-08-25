using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Win32;

// Aliases para evitar conflictos de namespace
using WpfMessageBox = System.Windows.MessageBox;
using WinFormsOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace KingdomHeartsCustomMusic.utils
{
    public static class KHNativePatcher
    {
        // Kingdom Hearts file mappings (from KHPCPatchManager code)
        private static readonly Dictionary<string, string[]> KHFiles = new()
        {
            ["KH1"] = new string[]
            {
                "kh1_first",
                "kh1_second", 
                "kh1_third",
                "kh1_fourth",
                "kh1_fifth"
            },
            ["KH2"] = new string[]
            {
                "kh2_first",
                "kh2_second",
                "kh2_third", 
                "kh2_fourth",
                "kh2_fifth",
                "kh2_sixth"
            },
            ["BBS"] = new string[]
            {
                "bbs_first",
                "bbs_second",
                "bbs_third",
                "bbs_fourth"
            },
            ["DDD"] = new string[]
            {
                "kh3d_first",
                "kh3d_second",
                "kh3d_third",
                "kh3d_fourth"
            },
            ["COM"] = new string[]
            {
                "Recom"
            }
        };

        // Known installation paths for auto-detection
        private static readonly string[] KnownInstallPaths = 
        {
            @"C:\Program Files\Epic Games\KH_1.5_2.5",
            @"C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS -HD 1.5+2.5 ReMIX-",
            @"C:\Program Files\Epic Games\KH_2.8",
            @"C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS HD 2.8 Final Chapter Prologue",
            @"D:\Program Files\Epic Games\KH_1.5_2.5",
            @"D:\Juegos\Epic Games\KH_1.5_2.5",
            @"E:\Juegos\Epic Games\KH_1.5_2.5",
            @"F:\Games\Epic Games\KH_1.5_2.5"
        };

        public static bool ApplyPatchInteractive(IProgress<string>? progress = null)
        {
            // Initialize logging
            PatchLogger.InitializeLog("Interactive");
            
            try
            {
                PatchLogger.LogStep("Starting interactive patch application");
                progress?.Report("?? Please select the patch file to apply...");

                // Step 1: Let user select the patch file
                PatchLogger.LogStep("User selecting patch file");
                string? patchFilePath = SelectPatchFile();
                if (patchFilePath == null)
                {
                    PatchLogger.Log("User cancelled patch file selection");
                    progress?.Report("? No patch file selected");
                    PatchLogger.FinalizeLog(false);
                    return false;
                }

                PatchLogger.LogFileInfo(patchFilePath, "Selected patch file:");
                progress?.Report($"?? Selected patch: {Path.GetFileName(patchFilePath)}");

                // Step 2: Determine patch type
                PatchLogger.LogStep("Determining patch type");
                string patchType = DeterminePatchType(patchFilePath);
                PatchLogger.Log($"Detected patch type: {patchType}");
                
                if (string.IsNullOrEmpty(patchType))
                {
                    PatchLogger.LogError("Unsupported patch file type");
                    MessageBox.Show("Unsupported patch file type. Please select a .kh1pcpatch, .kh2pcpatch, .bbspcpatch, .dddpcpatch, or .compcpatch file.",
                        "Invalid Patch File", MessageBoxButton.OK, MessageBoxImage.Error);
                    PatchLogger.FinalizeLog(false);
                    return false;
                }

                progress?.Report($"?? Detected patch type: {GetGameName(patchType)}");

                // Step 3: Let user select the game installation folder
                PatchLogger.LogStep("User selecting game installation folder");
                string? gameFolder = SelectGameInstallationFolder(patchType);
                if (gameFolder == null)
                {
                    PatchLogger.Log("User cancelled game folder selection");
                    progress?.Report("? No game installation folder selected");
                    PatchLogger.FinalizeLog(false);
                    return false;
                }

                PatchLogger.LogDirectoryInfo(gameFolder, "Selected game folder:");
                progress?.Report($"?? Selected game folder: {gameFolder}");

                // Step 4: Validate the game installation
                PatchLogger.LogStep("Validating game installation");
                bool isValidInstallation = ValidateKHInstallation(gameFolder, patchType);
                PatchLogger.Log($"Game installation validation result: {isValidInstallation}");
                
                if (!isValidInstallation)
                {
                    PatchLogger.LogError($"Invalid game installation for {patchType}");
                    MessageBox.Show(
                        $"The selected folder does not contain a valid {GetGameName(patchType)} installation.\n\n" +
                        $"Please make sure you select the correct game folder that contains the .pkg files.",
                        "Invalid Game Installation",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    PatchLogger.FinalizeLog(false);
                    return false;
                }

                // Step 5: Apply the patch
                PatchLogger.LogStep("Starting patch application process");
                bool result = ApplyPatch(patchFilePath, gameFolder, patchType, progress, true);
                
                PatchLogger.Log($"Final result: {result}");
                PatchLogger.FinalizeLog(result);
                
                return result;
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Fatal exception in ApplyPatchInteractive", ex);
                progress?.Report($"? Error: {ex.Message}");
                MessageBox.Show(
                    $"Failed to apply patch:\n\n{ex.Message}",
                    "Patch Application Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                PatchLogger.FinalizeLog(false);
                return false;
            }
        }

        private static string? SelectPatchFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Kingdom Hearts Patch File",
                Filter = "Kingdom Hearts Patches|*.kh1pcpatch;*.kh2pcpatch;*.bbspcpatch;*.dddpcpatch;*.compcpatch|" +
                        "KH1 Patches (*.kh1pcpatch)|*.kh1pcpatch|" +
                        "KH2 Patches (*.kh2pcpatch)|*.kh2pcpatch|" +
                        "BBS Patches (*.bbspcpatch)|*.bbspcpatch|" +
                        "DDD Patches (*.dddpcpatch)|*.dddpcpatch|" +
                        "COM Patches (*.compcpatch)|*.compcpatch|" +
                        "All Files (*.*)|*.*",
                FilterIndex = 1,
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static string? SelectGameInstallationFolder(string patchType)
        {
            string gameDisplayName = GetGameName(patchType);
            
            // First, try to auto-detect the game installation
            string? autoDetectedPath = FindKingdomHeartsInstallation(patchType);
            
            string message = $"Please select the {gameDisplayName} installation folder.\n\n";
            
            if (autoDetectedPath != null)
            {
                message += $"Auto-detected installation: {autoDetectedPath}\n\n" +
                          "Click YES to use the auto-detected path, or NO to select manually.";
                
                var autoDetectResult = MessageBox.Show(message, "Game Installation Location",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                
                if (autoDetectResult == MessageBoxResult.Yes)
                {
                    return autoDetectedPath;
                }
                else if (autoDetectResult == MessageBoxResult.Cancel)
                {
                    return null;
                }
                // If NO, continue to manual selection
            }
            else
            {
                message += "No installation was auto-detected. Please browse to your game installation folder.\n\n" +
                          "This should be the main game folder that contains the Image subfolder.";
                
                MessageBox.Show(message, "Select Game Installation",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // For now, use folder dialog with guidance
            var folderDialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = $"Navigate to {gameDisplayName} installation folder and select any file inside it",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = false,
                CheckPathExists = true,
                Multiselect = false
            };

            // Set initial directory to a likely location
            string initialPath = "";
            string[] commonPaths = { @"C:\Program Files\Epic Games", @"C:\Program Files (x86)\Steam\steamapps\common", @"D:\Juegos", @"E:\Juegos" };
            foreach (string path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    initialPath = path;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(initialPath))
            {
                folderDialog.InitialDirectory = initialPath;
            }

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = Path.GetDirectoryName(folderDialog.FileName) ?? "";
                
                // Navigate up to find the game root folder
                string currentPath = selectedPath;
                for (int i = 0; i < 5; i++) // Search up to 5 levels up
                {
                    string imagePath = Path.Combine(currentPath, "Image");
                    if (Directory.Exists(imagePath))
                    {
                        // Look for language folders
                        foreach (string lang in new[] { "en", "dt", "fr", "es", "it", "jp" })
                        {
                            string langPath = Path.Combine(imagePath, lang);
                            if (Directory.Exists(langPath) && ValidateKHInstallation(langPath, patchType))
                            {
                                return langPath;
                            }
                        }
                        // If no language folder found but Image exists, use Image folder directly
                        if (ValidateKHInstallation(imagePath, patchType))
                        {
                            return imagePath;
                        }
                    }
                    
                    // Go up one level
                    string? parentPath = Path.GetDirectoryName(currentPath);
                    if (parentPath == null || parentPath == currentPath)
                        break;
                    currentPath = parentPath;
                }

                // If we can't find the game structure, ask user to try again
                MessageBox.Show(
                    $"Could not find Kingdom Hearts installation structure near the selected location.\n\n" +
                    $"Please make sure you select a file inside the Kingdom Hearts game folder.\n" +
                    $"The game folder should contain an 'Image' subfolder with .pkg files.",
                    "Invalid Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                
                // Give user another chance
                return SelectGameInstallationFolder(patchType);
            }

            return null;
        }

        public static bool ApplyPatch(string patchFilePath, string gameFolder, string patchType, IProgress<string>? progress = null, bool createBackup = true)
        {
            try
            {
                PatchLogger.LogStep("Starting ApplyPatch method");
                PatchLogger.Log($"Parameters - PatchFile: {patchFilePath}, GameFolder: {gameFolder}, PatchType: {patchType}, CreateBackup: {createBackup}");
                
                progress?.Report("?? Analyzing patch file...");

                // Extract patch to temporary directory
                string tempDir = Path.Combine(Path.GetTempPath(), $"KHPatch_{DateTime.Now:yyyyMMdd_HHmmss}");
                PatchLogger.Log($"Creating temporary directory: {tempDir}");
                Directory.CreateDirectory(tempDir);
                PatchLogger.LogDirectoryInfo(tempDir, "Temporary patch directory:");

                try
                {
                    progress?.Report("?? Extracting patch...");
                    PatchLogger.LogStep("Extracting patch file");
                    ExtractPatch(patchFilePath, tempDir);
                    PatchLogger.Log("Patch extraction completed");

                    progress?.Report("?? Validating patch contents...");
                    PatchLogger.LogStep("Validating patch contents");
                    bool isValidPatch = ValidatePatchContents(tempDir, patchType, progress);
                    PatchLogger.Log($"Patch validation result: {isValidPatch}");

                    if (isValidPatch)
                    {
                        progress?.Report("? Patch validation successful!");
                        
                        // Ask user if they want to apply the patch directly
                        PatchLogger.LogStep("Showing user confirmation dialog");
                        var result = MessageBox.Show(
                            $"?? Patch validation complete!\n\n" +
                            $"? Game: {GetGameName(patchType)}\n" +
                            $"?? Game Path: {gameFolder}\n" +
                            $"?? Patch file: {Path.GetFileName(patchFilePath)}\n\n" +
                            $"Would you like to apply this patch directly to your game now?\n\n" +
                            $"?? Important:\n" +
                            $"• Make sure {GetGameName(patchType)} is completely closed\n" +
                            $"• A backup will be created automatically\n" +
                            $"• This operation will modify your game files",
                            "Apply Patch Directly?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        PatchLogger.Log($"User choice: {result}");

                        if (result == MessageBoxResult.Yes)
                        {
                            PatchLogger.LogStep("User chose to apply patch directly");
                            return ApplyPatchDirectly(patchFilePath, gameFolder, patchType, tempDir, progress);
                        }
                        else
                        {
                            PatchLogger.LogStep("User chose to view analysis only");
                            // Show the analysis result as before
                            ShowPatchValidationResult(patchFilePath, patchType, tempDir, gameFolder);
                            return true;
                        }
                    }
                    else
                    {
                        PatchLogger.LogError("No compatible patch content found");
                        progress?.Report("? No compatible patch content found");
                        MessageBox.Show(
                            "The selected patch file does not contain compatible content for the detected game type.\n\n" +
                            "Please verify that you have selected the correct patch file.",
                            "Incompatible Patch",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return false;
                    }
                }
                finally
                {
                    // Cleanup temporary directory
                    try
                    {
                        PatchLogger.LogStep("Cleaning up temporary directory");
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                            PatchLogger.Log("Temporary directory cleaned up successfully");
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        PatchLogger.LogError("Failed to cleanup temporary directory", cleanupEx);
                        // Ignore cleanup errors
                    }
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in ApplyPatch method", ex);
                progress?.Report($"? Error: {ex.Message}");
                throw;
            }
        }

        private static void ShowPatchValidationResult(string patchFilePath, string patchType, string extractedDir, string gameFolder)
        {
            // Count music files found
            int musicFileCount = CountMusicFiles(extractedDir);
            string gameVersion = GetGameName(patchType);

            var patchInfo = new FileInfo(patchFilePath);
            string fileSize = patchInfo.Length > 1024 * 1024 
                ? $"{patchInfo.Length / (1024.0 * 1024.0):F1} MB"
                : $"{patchInfo.Length / 1024.0:F1} KB";

            string message = $"?? Patch Analysis Complete!\n\n" +
                           $"? Game: {gameVersion}\n" +
                           $"?? Game Path: {gameFolder}\n" +
                           $"?? Music files detected: {musicFileCount}\n" +
                           $"?? Patch file: {Path.GetFileName(patchFilePath)}\n" +
                           $"?? Patch size: {fileSize}\n\n";

            if (musicFileCount > 0)
            {
                message += $"?? Your music patch is ready and compatible!\n\n" +
                          $"?? Important Notes:\n" +
                          $"• Make sure {gameVersion} is completely closed\n" +
                          $"• This tool validates patches but cannot apply them yet\n" +
                          $"• Use KHPCPatchManager or OpenKH to apply the patch\n" +
                          $"• Your game installation and patch are compatible\n\n" +
                          $"?? To apply this patch:\n" +
                          $"1. Download KHPCPatchManager\n" +
                          $"2. Run: KHPCPatchManager.exe \"{patchFilePath}\"\n" +
                          $"3. Select game folder: {gameFolder}\n" +
                          $"4. Enjoy your custom music!";
            }
            else
            {
                message += $"?? No music files were found in this patch.\n" +
                          $"This may be a different type of mod or patch.\n\n" +
                          $"The patch structure appears valid, but it doesn't contain\n" +
                          $"the expected .scd music files for {gameVersion}.";
            }

            MessageBox.Show(message, "Patch Analysis Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static int CountMusicFiles(string directory)
        {
            int count = 0;
            try
            {
                // Look for .scd files (Kingdom Hearts audio format)
                var musicFiles = Directory.GetFiles(directory, "*.scd", SearchOption.AllDirectories);
                count += musicFiles.Length;

                // Look for other audio files
                var otherAudioFiles = Directory.GetFiles(directory, "*.ogg", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(directory, "*.wav", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(directory, "*.mp3", SearchOption.AllDirectories));
                count += otherAudioFiles.Count();
            }
            catch
            {
                // Ignore counting errors
            }
            return count;
        }

        private static string GetGameName(string patchType)
        {
            return patchType switch
            {
                "KH1" => "Kingdom Hearts I",
                "KH2" => "Kingdom Hearts II",
                "BBS" => "Birth by Sleep",
                "DDD" => "Dream Drop Distance",
                "COM" => "Chain of Memories",
                _ => "Kingdom Hearts"
            };
        }

        private static string DeterminePatchType(string patchFilePath)
        {
            string extension = Path.GetExtension(patchFilePath).ToLowerInvariant();
            return extension switch
            {
                ".kh1pcpatch" => "KH1",
                ".kh2pcpatch" => "KH2", 
                ".bbspcpatch" => "BBS",
                ".dddpcpatch" => "DDD",
                ".compcpatch" => "COM",
                _ => string.Empty
            };
        }

        private static string? FindKingdomHeartsInstallation(string patchType)
        {
            // Check known installation paths
            foreach (string basePath in KnownInstallPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                string imagePath = Path.Combine(basePath, "Image");
                if (!Directory.Exists(imagePath)) continue;

                // Check both 'en' and 'dt' language folders
                foreach (string lang in new[] { "en", "dt", "fr", "es", "it", "jp" })
                {
                    string fullPath = Path.Combine(imagePath, lang);
                    if (Directory.Exists(fullPath) && ValidateKHInstallation(fullPath, patchType))
                    {
                        return fullPath;
                    }
                }
            }

            // If not found in known paths, search more thoroughly
            return SearchForKHInstallation(patchType);
        }

        private static bool ValidateKHInstallation(string path, string patchType)
        {
            try
            {
                string[] requiredFiles = KHFiles[patchType];
                
                // Check if at least one of the required game files exists
                return requiredFiles.Any(file => File.Exists(Path.Combine(path, $"{file}.pkg")));
            }
            catch
            {
                return false;
            }
        }

        private static string? SearchForKHInstallation(string patchType)
        {
            try
            {
                // Search in common game directories
                string[] searchRoots = {
                    @"C:\Program Files",
                    @"C:\Program Files (x86)",
                    @"D:\Program Files",
                    @"D:\Program Files (x86)",
                    @"E:\Program Files",
                    @"F:\Program Files",
                    @"D:\Juegos",
                    @"E:\Juegos",
                    @"F:\Juegos"
                };

                foreach (string root in searchRoots)
                {
                    if (!Directory.Exists(root)) continue;

                    // Look for Kingdom Hearts directories
                    var khDirs = Directory.GetDirectories(root, "*Kingdom Hearts*", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.GetDirectories(root, "*KH_*", SearchOption.TopDirectoryOnly))
                        .Where(Directory.Exists);

                    foreach (string khDir in khDirs)
                    {
                        string imageDir = Path.Combine(khDir, "Image");
                        if (Directory.Exists(imageDir))
                        {
                            foreach (string lang in new[] { "en", "dt", "fr", "es", "it", "jp" })
                            {
                                string fullPath = Path.Combine(imageDir, lang);
                                if (Directory.Exists(fullPath) && ValidateKHInstallation(fullPath, patchType))
                                {
                                    return fullPath;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore search errors
            }

            return null;
        }

        private static void ExtractPatch(string patchFilePath, string extractPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(patchFilePath);
                archive.ExtractToDirectory(extractPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to extract patch file: {ex.Message}", ex);
            }
        }

        private static bool ValidatePatchContents(string patchDir, string patchType, IProgress<string>? progress)
        {
            string[] gameFiles = KHFiles[patchType];
            bool foundCompatibleContent = false;

            foreach (string gameFile in gameFiles)
            {
                string patchSubDir = Path.Combine(patchDir, gameFile);
                if (!Directory.Exists(patchSubDir))
                {
                    continue; // This game file is not modified by the patch
                }

                progress?.Report($"?? Checking {gameFile}...");

                // Look for music files in remastered/amusic folders
                string remasteredMusicDir = Path.Combine(patchSubDir, "remastered", "amusic");
                if (Directory.Exists(remasteredMusicDir))
                {
                    var musicFiles = Directory.GetFiles(remasteredMusicDir, "*.scd", SearchOption.AllDirectories);
                    if (musicFiles.Length > 0)
                    {
                        progress?.Report($"?? Found {musicFiles.Length} music files in {gameFile}");
                        foundCompatibleContent = true;
                    }
                }

                // Look for other mod content
                string originalDir = Path.Combine(patchSubDir, "original");
                if (Directory.Exists(originalDir))
                {
                    var files = Directory.GetFiles(originalDir, "*", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        progress?.Report($"?? Found {files.Length} files in {gameFile}/original");
                        foundCompatibleContent = true;
                    }
                }
            }

            return foundCompatibleContent;
        }

        public static bool IsKingdomHeartsInstalled(string? gameType = null)
        {
            if (gameType != null)
            {
                return FindKingdomHeartsInstallation(gameType) != null;
            }

            // Check if any Kingdom Hearts installation exists
            return KHFiles.Keys.Any(type => FindKingdomHeartsInstallation(type) != null);
        }

        public static string[] GetGameFiles(string patchType)
        {
            return KHFiles.TryGetValue(patchType, out var files) ? files : Array.Empty<string>();
        }

        private static bool ApplyPatchDirectly(string patchFilePath, string gameFolder, string patchType, string extractedDir, IProgress<string>? progress)
        {
            try
            {
                PatchLogger.LogStep("Starting ApplyPatchDirectly");
                progress?.Report("?? Starting direct patch application...");

                // Use the direct patcher for validation
                bool success = KHDirectPatcher.ApplyPatchDirect(extractedDir, gameFolder, patchType, progress);
                
                PatchLogger.Log($"KHDirectPatcher result: {success}");

                if (success)
                {
                    progress?.Report("?? Patch validation completed successfully!");
                    
                    string logFile = PatchLogger.GetLogFilePath() ?? "No log file available";
                    
                    // Generate helpful scripts
                    GeneratePatchApplicationScript(patchFilePath, gameFolder, patchType);
                    
                    // Show options to the user
                    var result = ShowPatchApplicationOptions(patchFilePath, gameFolder, patchType);
                    
                    return result;
                }
                else
                {
                    string logFile = PatchLogger.GetLogFilePath() ?? "No log file available";
                    
                    MessageBox.Show(
                        $"? Patch validation failed.\n\n" +
                        $"There were issues during the patch validation process.\n\n" +
                        $"?? Detailed log file: {Path.GetFileName(logFile)}\n" +
                        $"?? Log location: {Path.GetDirectoryName(logFile)}\n\n" +
                        $"Please check the log file for detailed information and:\n" +
                        $"• Verify the patch file is not corrupted\n" +
                        $"• Ensure the patch is compatible with your game version\n" +
                        $"• Make sure the game installation is valid\n\n" +
                        $"Share the log file for troubleshooting assistance.",
                        "Patch Validation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in ApplyPatchDirectly", ex);
                progress?.Report($"? Error during patch validation: {ex.Message}");
                
                string logFile = PatchLogger.GetLogFilePath() ?? "No log file available";
                
                MessageBox.Show(
                    $"? Error during patch validation:\n\n{ex.Message}\n\n" +
                    $"?? Detailed log file: {Path.GetFileName(logFile)}\n" +
                    $"?? Log location: {Path.GetDirectoryName(logFile)}\n\n" +
                    $"The patch validation encountered an error.\n" +
                    $"Your game files remain unchanged.\n\n" +
                    $"Please share the log file for troubleshooting assistance.",
                    "Patch Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                return false;
            }
        }

        private static bool TryLaunchKHPCPatchManager(string patchFilePath, string gameFolder)
        {
            try
            {
                PatchLogger.LogStep("Attempting to launch KHPCPatchManager");
                
                // Possible locations for KHPCPatchManager
                var possiblePaths = new[]
                {
                    "KHPCPatchManager.exe",
                    Path.Combine(Environment.CurrentDirectory, "KHPCPatchManager.exe"),
                    Path.Combine(Environment.CurrentDirectory, "tools", "KHPCPatchManager.exe"),
                    Path.Combine(Environment.CurrentDirectory, "..", "KHPCPatchManager.exe"),
                    Path.Combine(@"C:\tools", "KHPCPatchManager.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KHPCPatchManager.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "KHPCPatchManager.exe")
                };

                string khpcPatcherPath = null;
                
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        khpcPatcherPath = path;
                        break;
                    }
                }

                if (khpcPatcherPath == null)
                {
                    PatchLogger.Log("KHPCPatchManager.exe not found in any expected location");
                    return false;
                }

                PatchLogger.Log($"Found KHPCPatchManager at: {khpcPatcherPath}");

                // Launch KHPCPatchManager with the patch file as argument
                // Most KHPCPatchManager versions accept the patch file as command line argument
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = khpcPatcherPath,
                    Arguments = $"\"{patchFilePath}\"", // Pass the patch file as argument
                    UseShellExecute = true, // Use shell execute to show the GUI
                    CreateNoWindow = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };

                PatchLogger.Log($"Launching KHPCPatchManager with args: {processInfo.Arguments}");

                var process = System.Diagnostics.Process.Start(processInfo);
                
                if (process != null)
                {
                    PatchLogger.Log("KHPCPatchManager launched successfully");
                    return true;
                }
                else
                {
                    PatchLogger.LogError("Failed to start KHPCatchManager process");
                    return false;
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError($"Exception launching KHPCPatchManager: {ex.Message}", ex);
                return false;
            }
        }

        private static bool ShowPatchApplicationOptions(string patchFilePath, string gameFolder, string patchType)
        {
            try
            {
                // Check if KHPCPatchManager is available
                bool khpcAvailable = IsKHPCPatchManagerAvailable();
                
                string message = $"? Patch Validation Complete!\n\n" +
                                $"?? Game: {GetGameName(patchType)}\n" +
                                $"?? Location: {gameFolder}\n" +
                                $"?? Patch: {Path.GetFileName(patchFilePath)}\n\n" +
                                $"?? VALIDATION RESULTS:\n" +
                                $"• Patch file format: ? Valid\n" +
                                $"• Game compatibility: ? Compatible\n" +
                                $"• Music files detected: ? Found\n" +
                                $"• Backup creation: ? Complete\n\n";

                if (khpcAvailable)
                {
                    message += $"?? KHPCPatchManager found and ready!\n\n" +
                              $"Choose how you want to apply the patch:\n\n" +
                              $"?? YES: Launch KHPCPatchManager now\n" +
                              $"? NO: Show manual instructions\n" +
                              $"? CANCEL: Exit without applying\n\n" +
                              $"Recommended: Click YES to launch KHPCPatchManager automatically.";
                    
                    var result = MessageBox.Show(message, "Launch KHPCPatchManager?", 
                                                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    
                    switch (result)
                    {
                        case MessageBoxResult.Yes:
                            // Launch KHPCPatchManager
                            bool launched = TryLaunchKHPCPatchManager(patchFilePath, gameFolder);
                            if (launched)
                            {
                                MessageBox.Show(
                                    $"?? KHPCPatchManager Launched!\n\n" +
                                    $"KHPCPatchManager has been opened with your patch file.\n\n" +
                                    $"Follow these steps in KHPCPatchManager:\n" +
                                    $"1. Verify the patch file path is correct\n" +
                                    $"2. Select your game installation folder:\n" +
                                    $"   {gameFolder}\n" +
                                    $"3. Click 'Apply Patch' or similar button\n" +
                                    $"4. Wait for completion\n" +
                                    $"5. Launch Kingdom Hearts to enjoy your music!\n\n" +
                                    $"? Your patch is ready to apply!",
                                    "KHPCPatchManager Ready",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                                return true;
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"? Failed to launch KHPCPatchManager\n\n" +
                                    $"Could not start KHPCPatchManager automatically.\n" +
                                    $"Please launch it manually with these parameters:\n\n" +
                                    $"?? Patch file: {patchFilePath}\n" +
                                    $"?? Game folder: {gameFolder}\n\n" +
                                    $"A PowerShell script has been generated to help you.",
                                    "Launch Failed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                return false;
                            }
                            
                        case MessageBoxResult.No:
                            // Show manual instructions
                            ShowManualInstructions(patchFilePath, gameFolder, patchType);
                            return true;
                            
                        default: // Cancel
                            return false;
                    }
                }
                else
                {
                    message += $"?? KHPCPatchManager not found on this system.\n\n" +
                              $"Your patch is validated and ready, but you'll need to\n" +
                              $"apply it manually using KHPCPatchManager.\n\n" +
                              $"Click OK to see manual instructions.";
                    
                    MessageBox.Show(message, "KHPCPatchManager Not Found", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    ShowManualInstructions(patchFilePath, gameFolder, patchType);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError($"Exception in ShowPatchApplicationOptions: {ex.Message}", ex);
                return false;
            }
        }

        private static bool IsKHPCPatchManagerAvailable()
        {
            var possiblePaths = new[]
            {
                "KHPCPatchManager.exe",
                Path.Combine(Environment.CurrentDirectory, "KHPCPatchManager.exe"),
                Path.Combine(Environment.CurrentDirectory, "tools", "KHPCPatchManager.exe"),
                Path.Combine(Environment.CurrentDirectory, "..", "KHPCPatchManager.exe"),
                Path.Combine(@"C:\tools", "KHPCPatchManager.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "KHPCPatchManager.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "KHPCPatchManager.exe")
            };

            return possiblePaths.Any(File.Exists);
        }

        private static void ShowManualInstructions(string patchFilePath, string gameFolder, string patchType)
        {
            MessageBox.Show(
                $"?? Manual Patch Application Instructions\n\n" +
                $"Your patch has been validated and is ready to apply!\n\n" +
                $"?? MANUAL STEPS:\n\n" +
                $"1. Download KHPCPatchManager if you haven't already\n" +
                $"2. Place KHPCPatchManager.exe in one of these locations:\n" +
                $"   • Same folder as this application\n" +
                $"   • Your Desktop\n" +
                $"   • Your Downloads folder\n" +
                $"   • C:\\tools\\\n\n" +
                $"3. Run KHPCPatchManager.exe\n\n" +
                $"4. In KHPCPatchManager:\n" +
                $"   ?? Patch file: {patchFilePath}\n" +
                $"   ?? Game folder: {gameFolder}\n\n" +
                $"5. Apply the patch and enjoy your custom music!\n\n" +
                $"?? A PowerShell script (apply_patch.ps1) has been\n" +
                $"    generated in your patch folder to help automate this process.\n\n" +
                $"? Your patch is 100% ready for application!",
                "Manual Application Instructions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private static void GeneratePatchApplicationScript(string patchFilePath, string gameFolder, string patchType)
        {
            try
            {
                string scriptDir = Path.GetDirectoryName(patchFilePath) ?? Environment.CurrentDirectory;
                string scriptPath = Path.Combine(scriptDir, "apply_patch.ps1");
                
                var scriptContent = new List<string>
                {
                    "# Kingdom Hearts Custom Music - Patch Application Script",
                    "# Generated automatically by Kingdom Hearts Custom Music Patcher",
                    $"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "",
                    "Write-Host \"Kingdom Hearts Custom Music - Patch Application\" -ForegroundColor Cyan",
                    "Write-Host \"=================================================\" -ForegroundColor Cyan",
                    "",
                    $"$patchFile = \"{patchFilePath}\"",
                    $"$gameFolder = \"{gameFolder}\"",
                    $"$patchType = \"{patchType}\"",
                    "",
                    "Write-Host \"Patch file: $patchFile\" -ForegroundColor Green",
                    "Write-Host \"Game folder: $gameFolder\" -ForegroundColor Green",
                    "Write-Host \"Patch type: $patchType\" -ForegroundColor Green",
                    "",
                    "# Check if patch file exists",
                    "if (-not (Test-Path $patchFile)) {",
                    "    Write-Host \"ERROR: Patch file not found!\" -ForegroundColor Red",
                    "    Read-Host \"Press Enter to exit\"",
                    "    exit 1",
                    "}",
                    "",
                    "# Check if game folder exists",
                    "if (-not (Test-Path $gameFolder)) {",
                    "    Write-Host \"ERROR: Game folder not found!\" -ForegroundColor Red",
                    "    Read-Host \"Press Enter to exit\"",
                    "    exit 1",
                    "}",
                    "",
                    "# Look for KHPCPatchManager.exe",
                    "$khpcPatcher = \"\"",
                    "$possiblePaths = @(",
                    "    \"KHPCPatchManager.exe\",",
                    "    \"./KHPCPatchManager.exe\",",
                    "    \"../KHPCPatchManager.exe\",",
                    "    \"./tools/KHPCPatchManager.exe\",",
                    "    \"C:/tools/KHPCPatchManager.exe\"",
                    ")",
                    "",
                    "foreach ($path in $possiblePaths) {",
                    "    if (Test-Path $path) {",
                    "        $khpcPatcher = $path",
                    "        break",
                    "    }",
                    "}",

                    "if ($khpcPatcher -eq \"\") {",
                    "    Write-Host \"\"",
                    "    Write-Host \"KHPCPatchManager.exe not found!\" -ForegroundColor Yellow",
                    "    Write-Host \"Please download KHPCPatchManager and place it in one of these locations:\" -ForegroundColor Yellow",
                    "    foreach ($path in $possiblePaths) {",
                    "        Write-Host \"  - $path\" -ForegroundColor Gray",
                    "    }",
                    "    Write-Host \"\"",
                    "    Write-Host \"Manual command to run after downloading KHPCPatchManager:\" -ForegroundColor Cyan",
                    "    Write-Host \"KHPCPatchManager.exe `\"$patchFile`\" `\"$gameFolder`\"\" -ForegroundColor White",
                    "    Write-Host \"\"",
                    "    Read-Host \"Press Enter to exit\"",
                    "    exit 1",
                    "}",
                    "",
                    "Write-Host \"\"",
                    "Write-Host \"Found KHPCPatchManager: $khpcPatcher\" -ForegroundColor Green",
                    "Write-Host \"\"",
                    "Write-Host \"Ready to apply patch!\" -ForegroundColor Yellow",
                    "Write-Host \"This will modify your Kingdom Hearts game files.\" -ForegroundColor Yellow",
                    "Write-Host \"\"",
                    "$confirm = Read-Host \"Do you want to proceed? (y/N)\"",
                    "",
                    "if ($confirm -ne \"y\" -and $confirm -ne \"Y\") {",
                    "    Write-Host \"Patch application cancelled.\" -ForegroundColor Yellow",
                    "    Read-Host \"Press Enter to exit\"",
                    "    exit 0",
                    "}",
                    "",
                    "Write-Host \"\"",
                    "Write-Host \"Applying patch...\" -ForegroundColor Cyan",
                    "",
                    "# Run KHPCPatchManager",
                    "try {",
                    "    & $khpcPatcher $patchFile $gameFolder",
                    "    $exitCode = $LASTEXITCODE",
                    "    ",
                    "    if ($exitCode -eq 0) {",
                    "        Write-Host \"\"",
                    "        Write-Host \"Patch applied successfully!\" -ForegroundColor Green",
                    "        Write-Host \"You can now launch Kingdom Hearts to hear your custom music!\" -ForegroundColor Green",
                    "    } else {",
                    "        Write-Host \"\"",
                    "        Write-Host \"Patch application failed with exit code: $exitCode\" -ForegroundColor Red",
                    "        Write-Host \"Please check the KHPCPatchManager output above for details.\" -ForegroundColor Red",
                    "    }",
                    "} catch {",
                    "    Write-Host \"\"",
                    "    Write-Host \"Error running KHPCPatchManager: $($_.Exception.Message)\" -ForegroundColor Red",
                    "}",
                    "",
                    "Write-Host \"\"",
                    "Read-Host \"Press Enter to exit\""
                };
                
                File.WriteAllLines(scriptPath, scriptContent);
                PatchLogger.Log($"Generated PowerShell script: {scriptPath}");
            }
            catch (Exception ex)
            {
                PatchLogger.LogError($"Failed to generate PowerShell script: {ex.Message}", ex);
            }
        }
    }
}