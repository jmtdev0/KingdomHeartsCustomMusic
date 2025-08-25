using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace KingdomHeartsCustomMusic.utils
{
    public static class PatchApplicator
    {
        private static readonly string[] KnownPatchApplicators = 
        {
            "KHPCPatchManager.exe",
            "PatchManager.exe", 
            "KHPatchApplicator.exe",
            "OpenKH.exe"
        };

        // Common Kingdom Hearts installation paths
        private static readonly string[] KnownKHPaths = 
        {
            @"C:\Program Files\Epic Games\KH_1.5_2.5\Image\en",
            @"C:\Program Files\Epic Games\KH_1.5_2.5\Image\dt",
            @"C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS -HD 1.5+2.5 ReMIX-\Image\en",
            @"C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS -HD 1.5+2.5 ReMIX-\Image\dt",
            @"C:\Program Files\Epic Games\KH_2.8\Image\en",
            @"C:\Program Files\Epic Games\KH_2.8\Image\dt",
            @"C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS HD 2.8 Final Chapter Prologue\Image\en",
            @"C:\Program Files (x86)\Steam\steamapps\common\KINGDOM HEARTS HD 2.8 Final Chapter Prologue\Image\dt"
        };

        public static void ApplyPatch(string patchFilePath)
        {
            try
            {
                // First, try to find KHPCPatchManager specifically (most compatible)
                string? applicatorPath = FindKHPCPatchManager();

                // If not found, try other patch applicators
                if (applicatorPath == null)
                {
                    string utilsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "utils");
                    applicatorPath = FindPatchApplicator(utilsDir);
                }

                // If still not found, try the base directory
                if (applicatorPath == null)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    applicatorPath = FindPatchApplicator(baseDir);
                }

                // If still not found, ask user to locate it
                if (applicatorPath == null)
                {
                    applicatorPath = PromptForPatchApplicator();
                }

                if (applicatorPath == null)
                {
                    MessageBox.Show(
                        "? No patch applicator found.\n\n" +
                        "To apply patches automatically, please place one of these tools in your utils folder:\n" +
                        "• KHPCPatchManager.exe (Recommended)\n" +
                        "• OpenKH.exe\n" +
                        "• PatchManager.exe\n\n" +
                        "You can manually apply the patch using your preferred tool.",
                        "Patch Applicator Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    // Open the patch folder so user can find the file
                    OpenPatchFolder(patchFilePath);
                    return;
                }

                // Try to apply the patch with smart automation
                if (TryApplyPatchIntelligent(applicatorPath, patchFilePath))
                {
                    MessageBox.Show(
                        "? Patch applied successfully!\n\n" +
                        "Your Kingdom Hearts game now has custom music.\n" +
                        "Restart the game to hear your changes.",
                        "Patch Applied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // If intelligent application fails, try GUI fallback
                    TryApplyPatchGUI(applicatorPath, patchFilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"? Error applying patch:\n\n{ex.Message}\n\n" +
                    "You can manually apply the patch using your preferred tool.",
                    "Patch Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                OpenPatchFolder(patchFilePath);
            }
        }

        private static bool TryApplyPatchIntelligent(string applicatorPath, string patchFilePath)
        {
            string applicatorName = Path.GetFileNameWithoutExtension(applicatorPath).ToLowerInvariant();
            
            // For KHPCPatchManager, try the standard CLI approach first
            if (applicatorName.Contains("khpcpatchmanager"))
            {
                // Method 1: Try standard CLI (auto-detection)
                if (TryApplyPatchCLI(applicatorPath, patchFilePath))
                {
                    return true;
                }

                // Method 2: If auto-detection fails, try with automated path input
                return TryApplyPatchWithAutomatedInput(applicatorPath, patchFilePath);
            }
            else
            {
                // For other applicators, use standard approach
                return TryApplyPatchCLI(applicatorPath, patchFilePath);
            }
        }

        private static bool TryApplyPatchWithAutomatedInput(string applicatorPath, string patchFilePath)
        {
            try
            {
                // Find Kingdom Hearts installation
                string? khPath = FindKingdomHeartsInstallation(patchFilePath);
                
                if (khPath == null)
                {
                    // If we can't find KH installation, let the user handle it via GUI
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = applicatorPath,
                    Arguments = $"\"{patchFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(applicatorPath)
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    // If the process asks for input (Kingdom Hearts path), provide it automatically
                    if (process.StandardInput != null)
                    {
                        // Give the process a moment to start and potentially ask for input
                        System.Threading.Thread.Sleep(2000);
                        
                        // Send the KH path if the process is still running and waiting for input
                        if (!process.HasExited)
                        {
                            process.StandardInput.WriteLine(khPath);
                            process.StandardInput.Flush();
                        }
                    }

                    // Wait for completion with longer timeout for large patches
                    bool completed = process.WaitForExit(90000); // 90 seconds
                    
                    if (completed && process.ExitCode == 0)
                    {
                        return true;
                    }
                    else if (!completed)
                    {
                        process.Kill();
                        throw new TimeoutException("Patch application timed out after 90 seconds");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Automated input application failed: {ex.Message}");
                return false;
            }

            return false;
        }

        private static string? FindKingdomHeartsInstallation(string patchFilePath)
        {
            // Determine which game based on patch file extension
            string patchExtension = Path.GetExtension(patchFilePath).ToLowerInvariant();
            
            // Filter paths based on game type
            var relevantPaths = patchExtension switch
            {
                ".kh1pcpatch" or ".kh2pcpatch" or ".compcpatch" or ".bbspcpatch" => KnownKHPaths.Where(p => p.Contains("1.5+2.5") || p.Contains("KH_1.5_2.5")),
                ".dddpcpatch" => KnownKHPaths.Where(p => p.Contains("2.8") || p.Contains("KH_2.8")),
                _ => KnownKHPaths
            };

            // Check each relevant path
            foreach (string path in relevantPaths)
            {
                if (Directory.Exists(path))
                {
                    // Verify this directory contains the expected game files
                    if (ValidateKingdomHeartsDirectory(path, patchExtension))
                    {
                        return path;
                    }
                }
            }

            // If no standard path found, try to detect in common game directories
            return SearchForKingdomHeartsInstallation(patchExtension);
        }

        private static bool ValidateKingdomHeartsDirectory(string path, string patchExtension)
        {
            try
            {
                string[] expectedFiles = patchExtension switch
                {
                    ".kh1pcpatch" => new[] { "kh1_first.pkg", "kh1_second.pkg" },
                    ".kh2pcpatch" => new[] { "kh2_first.pkg", "kh2_second.pkg" },
                    ".compcpatch" => new[] { "Recom.pkg" },
                    ".bbspcpatch" => new[] { "bbs_first.pkg", "bbs_second.pkg" },
                    ".dddpcpatch" => new[] { "kh3d_first.pkg", "kh3d_second.pkg" },
                    _ => new[] { "kh1_first.pkg", "kh2_first.pkg" } // Default check
                };

                return expectedFiles.Any(file => File.Exists(Path.Combine(path, file)));
            }
            catch
            {
                return false;
            }
        }

        private static string? SearchForKingdomHeartsInstallation(string patchExtension)
        {
            try
            {
                // Common game installation roots
                string[] gameRoots = {
                    @"C:\Program Files\Epic Games",
                    @"C:\Program Files (x86)\Steam\steamapps\common",
                    @"D:\Program Files\Epic Games",
                    @"D:\Program Files (x86)\Steam\steamapps\common",
                    @"E:\Program Files\Epic Games",
                    @"E:\Program Files (x86)\Steam\steamapps\common"
                };

                foreach (string root in gameRoots)
                {
                    if (!Directory.Exists(root)) continue;

                    // Look for Kingdom Hearts directories
                    var khDirectories = Directory.GetDirectories(root, "*Kingdom Hearts*", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.GetDirectories(root, "*KH_*", SearchOption.TopDirectoryOnly));

                    foreach (string khDir in khDirectories)
                    {
                        string imagePath = Path.Combine(khDir, "Image");
                        if (Directory.Exists(imagePath))
                        {
                            // Check en and dt subfolders
                            foreach (string subFolder in new[] { "en", "dt" })
                            {
                                string fullPath = Path.Combine(imagePath, subFolder);
                                if (Directory.Exists(fullPath) && ValidateKingdomHeartsDirectory(fullPath, patchExtension))
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

        private static string? FindKHPCPatchManager()
        {
            // Look for KHPCPatchManager specifically (best compatibility)
            string[] searchDirs = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "utils"),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KHPCPatchManager-Code"),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            foreach (string dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                string khpcPath = Path.Combine(dir, "KHPCPatchManager.exe");
                if (File.Exists(khpcPath))
                {
                    return khpcPath;
                }

                // Also search subdirectories for KHPCPatchManager
                try
                {
                    var foundFiles = Directory.GetFiles(dir, "KHPCPatchManager.exe", SearchOption.AllDirectories);
                    if (foundFiles.Length > 0)
                    {
                        return foundFiles[0];
                    }
                }
                catch
                {
                    // Ignore access denied errors
                }
            }

            return null;
        }

        private static string? FindPatchApplicator(string directory)
        {
            if (!Directory.Exists(directory))
                return null;

            foreach (string applicatorName in KnownPatchApplicators)
            {
                string fullPath = Path.Combine(directory, applicatorName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // Look for any exe that might be a patch applicator
            var exeFiles = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories);
            foreach (string exeFile in exeFiles)
            {
                string fileName = Path.GetFileName(exeFile).ToLowerInvariant();
                if (fileName.Contains("patch") || fileName.Contains("kh") || fileName.Contains("openkh"))
                {
                    return exeFile;
                }
            }

            return null;
        }

        private static string? PromptForPatchApplicator()
        {
            var result = MessageBox.Show(
                "?? Patch applicator not found automatically.\n\n" +
                "Would you like to locate your patch management tool manually?\n" +
                "(Recommended: KHPCPatchManager.exe)",
                "Locate Patch Applicator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return null;

            var dialog = new OpenFileDialog
            {
                Title = "Select KHPCPatchManager or other patch applicator",
                Filter = "KHPCPatchManager (KHPCPatchManager.exe)|KHPCPatchManager.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static bool TryApplyPatchCLI(string applicatorPath, string patchFilePath)
        {
            try
            {
                string applicatorName = Path.GetFileNameWithoutExtension(applicatorPath).ToLowerInvariant();
                
                // Build command line arguments based on the applicator
                string arguments = BuildCommandLineArguments(applicatorName, patchFilePath);

                var psi = new ProcessStartInfo
                {
                    FileName = applicatorPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    WorkingDirectory = Path.GetDirectoryName(applicatorPath)
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    // Give it reasonable time to complete (patches can be large)
                    bool completed = process.WaitForExit(60000); // 60 seconds timeout
                    
                    if (completed && process.ExitCode == 0)
                    {
                        return true;
                    }
                    else if (!completed)
                    {
                        process.Kill();
                        throw new TimeoutException("Patch application timed out after 60 seconds");
                    }
                    else
                    {
                        string error = process.StandardError.ReadToEnd();
                        if (!string.IsNullOrEmpty(error))
                        {
                            throw new Exception($"Patch application failed: {error}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't show to user yet (we'll try other methods)
                System.Diagnostics.Debug.WriteLine($"CLI application failed: {ex.Message}");
                return false;
            }

            return false;
        }

        private static string BuildCommandLineArguments(string applicatorName, string patchFilePath)
        {
            // KHPCPatchManager supports direct patch file as argument
            if (applicatorName.Contains("khpcpatchmanager"))
            {
                return $"\"{patchFilePath}\"";
            }
            
            // OpenKH might use different syntax
            if (applicatorName.Contains("openkh"))
            {
                return $"patchmanager install \"{patchFilePath}\"";
            }

            // Default: just pass the patch file
            return $"\"{patchFilePath}\"";
        }

        private static void TryApplyPatchGUI(string applicatorPath, string patchFilePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = applicatorPath,
                    Arguments = $"\"{patchFilePath}\"",
                    UseShellExecute = true
                };

                Process.Start(psi);
                
                MessageBox.Show(
                    "?? Patch applicator opened in GUI mode.\n\n" +
                    "The patch file has been pre-loaded if supported.\n" +
                    "Please follow the on-screen instructions to complete the installation.\n\n" +
                    "If the game path is not detected automatically, you'll need to locate your Kingdom Hearts installation folder.",
                    "Manual Application Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to open patch applicator: {ex.Message}");
            }
        }

        private static void OpenPatchFolder(string patchFilePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(patchFilePath);
                if (directory != null && Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", $"/select,\"{patchFilePath}\"");
                }
            }
            catch
            {
                // Ignore if can't open folder
            }
        }
    }
}