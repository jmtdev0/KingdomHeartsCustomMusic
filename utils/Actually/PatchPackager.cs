using System.IO;
using System.IO.Compression;
using System.Windows;
using static KingdomHeartsCustomMusic.utils.TrackListLoader;

namespace KingdomHeartsCustomMusic.utils
{
    public static class PatchPackager
    {
        public static void CreateFinalPatch(string patchBasePath, string patchZipPath, string patchFinalPath, List<TrackInfo> includedTracks, IProgress<string>? progress = null)
        {
            try
            {
                PatchLogger.LogStep("Starting CreateFinalPatch");
                PatchLogger.Log($"Parameters - PatchBasePath: {patchBasePath}, PatchZipPath: {patchZipPath}, PatchFinalPath: {patchFinalPath}");
                PatchLogger.Log($"Included tracks count: {includedTracks.Count}");

                // Ensure the "patches" directory exists
                string patchesDir = Path.GetDirectoryName(patchFinalPath)!;
                PatchLogger.LogStep("Creating patches directory");
                PatchLogger.Log($"Patches directory: {patchesDir}");
                progress?.Report("📁 Creating patches directory...");
                Directory.CreateDirectory(patchesDir);

                // Verify patch base path exists and has content
                PatchLogger.LogStep("Verifying patch base path");
                progress?.Report("🔍 Verifying patch content...");
                
                if (!Directory.Exists(patchBasePath))
                {
                    throw new DirectoryNotFoundException($"Patch base path does not exist: {patchBasePath}");
                }

                var files = Directory.GetFiles(patchBasePath, "*", SearchOption.AllDirectories);
                PatchLogger.Log($"Found {files.Length} files in patch base path");
                progress?.Report($"📊 Found {files.Length} patch files");
                
                foreach (var file in files.Take(10)) // Log first 10 files
                {
                    PatchLogger.Log($"  File: {Path.GetRelativePath(patchBasePath, file)}");
                }
                if (files.Length > 10)
                {
                    PatchLogger.Log($"  ... and {files.Length - 10} more files");
                }

                // Create the zip file from the patch content
                PatchLogger.LogStep("Creating ZIP file from patch content");
                PatchLogger.Log($"Creating ZIP: {patchZipPath}");
                progress?.Report("🗜️ Compressing patch files...");
                
                ZipFile.CreateFromDirectory(patchBasePath, patchZipPath);
                PatchLogger.Log("ZIP file created successfully");

                // Verify ZIP file was created
                if (!File.Exists(patchZipPath))
                {
                    throw new FileNotFoundException($"ZIP file was not created: {patchZipPath}");
                }

                var zipInfo = new FileInfo(patchZipPath);
                PatchLogger.Log($"ZIP file size: {zipInfo.Length} bytes");
                progress?.Report($"✅ Compression complete: {zipInfo.Length / 1024.0:F1} KB");

                // Rename the .zip to .kh1pcpatch/.kh2pcpatch
                PatchLogger.LogStep("Renaming ZIP to patch file");
                progress?.Report("🔄 Finalizing patch file...");
                
                if (File.Exists(patchFinalPath))
                {
                    PatchLogger.Log($"Deleting existing patch file: {patchFinalPath}");
                    File.Delete(patchFinalPath); // Avoid exceptions if the file already exists
                }

                PatchLogger.Log($"Moving {patchZipPath} to {patchFinalPath}");
                File.Move(patchZipPath, patchFinalPath);
                PatchLogger.Log("File moved successfully");

                // Delete temporary folder that contained the patch content
                PatchLogger.LogStep("Cleaning up temporary patch directory");
                PatchLogger.Log($"Deleting temporary directory: {patchBasePath}");
                progress?.Report("🧹 Cleaning up temporary files...");
                
                Directory.Delete(patchBasePath, recursive: true);
                PatchLogger.Log("Temporary directory deleted");

                // Get file size for display
                var fileInfo = new FileInfo(patchFinalPath);
                string fileSize = fileInfo.Length > 1024 * 1024 
                    ? $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB"
                    : $"{fileInfo.Length / 1024.0:F1} KB";

                PatchLogger.Log($"Final patch file size: {fileSize}");
                progress?.Report($"🎉 Patch generation complete! Size: {fileSize}");

                string gameVersion = patchFinalPath.Contains("kh1pcpatch") ? "Kingdom Hearts I" : "Kingdom Hearts II";

                string message = $"🎉 Patch Created Successfully!\n\n" +
                                $"✨ Game: {gameVersion}\n" +
                                $"🎵 Tracks included: {includedTracks.Count}\n" +
                                $"📦 File size: {fileSize}\n" +
                                $"📁 Location: {patchFinalPath}\n\n" +
                                $"What would you like to do next?";

                PatchLogger.LogStep("Showing completion dialog to user");
                var result = MessageBox.Show(
                    message,
                    "Patch Generation Complete",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes,
                    MessageBoxOptions.DefaultDesktopOnly);

                PatchLogger.Log($"User selected: {result}");

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        // User wants to apply the patch now
                        PatchLogger.LogStep("User chose to apply patch immediately");
                        ApplyGeneratedPatch(patchFinalPath);
                        break;
                    
                    case MessageBoxResult.No:
                        // User wants to see the patch file location
                        PatchLogger.LogStep("User chose to show patch location");
                        ShowPatchLocation(patchFinalPath);
                        break;
                    
                    case MessageBoxResult.Cancel:
                    default:
                        // User just wants to close
                        PatchLogger.LogStep("User chose to close dialog");
                        break;
                }

                PatchLogger.LogStep("CreateFinalPatch completed successfully");
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in CreateFinalPatch", ex);
                progress?.Report($"❌ Error creating patch: {ex.Message}");
                throw;
            }
        }

        private static void ApplyGeneratedPatch(string patchFilePath)
        {
            try
            {
                PatchLogger.LogStep("Starting ApplyGeneratedPatch");
                PatchLogger.Log($"Patch file: {patchFilePath}");

                // Show progress window
                var progressWindow = new PatchProgressWindow();
                progressWindow.Show();

                var progress = new Progress<string>(message => 
                {
                    progressWindow.UpdateProgress(message);
                    PatchLogger.Log($"Progress: {message}");
                });

                // Use the interactive patch application method
                bool success = KHNativePatcher.ApplyPatchInteractive(progress);
                
                progressWindow.Close();
                PatchLogger.Log($"Patch application result: {success}");

                if (!success)
                {
                    // If user cancelled or there was an error, show the patch location
                    PatchLogger.Log("Patch application failed, showing patch location");
                    ShowPatchLocation(patchFilePath);
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in ApplyGeneratedPatch", ex);
                MessageBox.Show(
                    $"❌ Error during patch application:\n\n{ex.Message}\n\n" +
                    $"The patch file has been created successfully at:\n{patchFilePath}",
                    "Patch Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                ShowPatchLocation(patchFilePath);
            }
        }

        private static void ShowPatchLocation(string patchFilePath)
        {
            try
            {
                PatchLogger.LogStep("Showing patch location to user");
                PatchLogger.Log($"Opening explorer for: {patchFilePath}");

                // Open the folder and select the patch file
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{patchFilePath}\"");
                
                MessageBox.Show(
                    $"📁 Patch file location opened in Explorer.\n\n" +
                    $"File: {Path.GetFileName(patchFilePath)}\n" +
                    $"Location: {Path.GetDirectoryName(patchFilePath)}\n\n" +
                    $"You can apply this patch later using:\n" +
                    $"• The 'Apply Existing Patch' button in this application\n" +
                    $"• KHPCPatchManager\n" +
                    $"• OpenKH or other Kingdom Hearts modding tools",
                    "Patch File Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in ShowPatchLocation", ex);
                MessageBox.Show(
                    $"📦 Patch created successfully!\n\n" +
                    $"Location: {patchFilePath}\n\n" +
                    $"You can apply this patch using:\n" +
                    $"• The 'Apply Existing Patch' button in this application\n" +
                    $"• KHPCPatchManager\n" +
                    $"• OpenKH or other Kingdom Hearts modding tools",
                    "Patch File Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }

    // Enhanced progress window for patch application
    public class PatchProgressWindow : Window
    {
        private readonly System.Windows.Controls.TextBlock _statusText;
        private readonly System.Windows.Controls.ProgressBar _progressBar;

        public PatchProgressWindow()
        {
            Title = "Processing Patch";
            Width = 450;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 29));

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "🎵 Kingdom Hearts Patch Application",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            _statusText = new System.Windows.Controls.TextBlock
            {
                Text = "Initializing patch application...",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            _progressBar = new System.Windows.Controls.ProgressBar
            {
                Height = 8,
                IsIndeterminate = true,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48))
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(_statusText);
            stackPanel.Children.Add(_progressBar);
            Content = stackPanel;
        }

        public void UpdateProgress(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _statusText.Text = message;
            });
        }
    }
}
