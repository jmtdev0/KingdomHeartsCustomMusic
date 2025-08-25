using KingdomHeartsCustomMusic.utils;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static KingdomHeartsCustomMusic.utils.TrackListLoader;

// Aliases para evitar conflictos de namespace
using WpfMessageBox = System.Windows.MessageBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WinFormsMessageBox = System.Windows.Forms.MessageBox;

namespace KingdomHeartsCustomMusic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Main lists for track bindings
        private readonly List<(TrackInfo Track, WpfTextBox TextBox)> _trackBindingsKH1 = new();
        private readonly List<(TrackInfo Track, WpfTextBox TextBox)> _trackBindingsKH2 = new();
        private readonly Dictionary<TrackInfo, WpfCheckBox> _trackCheckboxesKH1 = new();
        private readonly Dictionary<TrackInfo, WpfCheckBox> _trackCheckboxesKH2 = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadTracks();
        }

        #region XAML initialization

        private void LoadTracks()
        {
            try
            {
                // Use application directory instead of AppDomain.CurrentDomain.BaseDirectory for distributed executable
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                string excelKH1 = Path.Combine(baseDir, "resources", "All Games Track List - KH1.xlsx");
                string excelKH2 = Path.Combine(baseDir, "resources", "All Games Track List - KH2.xlsx");

                // Verify files exist before trying to load them
                if (!File.Exists(excelKH1))
                {
                    MessageBox.Show(
                        $"❌ KH1 track list not found!\n\n" +
                        $"Expected location: {excelKH1}\n\n" +
                        $"Please ensure the resources folder is in the same directory as the executable.",
                        "Missing Track List",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                if (!File.Exists(excelKH2))
                {
                    MessageBox.Show(
                        $"❌ KH2 track list not found!\n\n" +
                        $"Expected location: {excelKH2}\n\n" +
                        $"Please ensure the resources folder is in the same directory as the executable.",
                        "Missing Track List",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var tracksKH1 = TrackListLoader.LoadTrackList(excelKH1);
                var tracksKH2 = TrackListLoader.LoadTrackList(excelKH2);

                foreach (var track in tracksKH1)
                    AddTrackRow(track, WorldListPanelKH1, _trackBindingsKH1, _trackCheckboxesKH1);

                foreach (var track in tracksKH2)
                    AddTrackRow(track, WorldListPanelKH2, _trackBindingsKH2, _trackCheckboxesKH2);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading track lists:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddTrackRow(
            TrackInfo track,
            StackPanel containerPanel,
            List<(TrackInfo, TextBox)> bindingList,
            Dictionary<TrackInfo, CheckBox> selectionMap)
        {
            // Create a modern styled border container for each track
            var trackBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)), // #FF252526
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // Checkbox
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Track name
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Path textbox
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Browse button

            // Checkbox with modern styling
            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
                Foreground = Brushes.White
            };
            Grid.SetColumn(checkBox, 0);
            selectionMap[track] = checkBox;

            // Track description with better typography
            var label = new TextBlock
            {
                Foreground = Brushes.White,
                Text = track.Description,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(label, 1);

            // Modern styled textbox
            var textbox = new TextBox
            {
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), // #FF2D2D30
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)), // #FF404040
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 11
            };
            Grid.SetColumn(textbox, 2);

            // Modern styled browse button
            var button = new Button
            {
                Content = "📁 Browse",
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), // #FF2D2D30
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)), // #FF404040
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            Grid.SetColumn(button, 3);

            // Add hover effects to the button
            button.MouseEnter += (s, e) => {
                button.Background = new SolidColorBrush(Color.FromRgb(64, 64, 64));
            };
            button.MouseLeave += (s, e) => {
                button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            };

            button.Click += (s, e) =>
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Audio files (*.wav;*.mp3;*.mp4)|*.wav;*.mp3;*.mp4",
                    Title = "Select Audio File"
                };
                if (dialog.ShowDialog() == true)
                {
                    textbox.Text = dialog.FileName;
                    // Add a subtle visual feedback
                    textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #FF007ACC
                }
            };

            // Add focus effects to textbox
            textbox.GotFocus += (s, e) => {
                textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #FF007ACC
                textbox.BorderThickness = new Thickness(2);
            };
            textbox.LostFocus += (s, e) => {
                textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)); // #FF404040
                textbox.BorderThickness = new Thickness(1);
            };

            row.Children.Add(checkBox);
            row.Children.Add(label);
            row.Children.Add(textbox);
            row.Children.Add(button);

            trackBorder.Child = row;
            containerPanel.Children.Add(trackBorder);
            bindingList.Add((track, textbox));
        }

        #endregion

        #region Button events

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var config = new RoutesConfig
            {
                Tracks = new Dictionary<string, Dictionary<string, string>>
                {
                    ["kh1"] = _trackBindingsKH1
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.TextBox.Text))
                        .ToDictionary(kvp => kvp.Track.PcNumber.ToString(), kvp => kvp.TextBox.Text),

                    ["kh2"] = _trackBindingsKH2
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.TextBox.Text))
                        .ToDictionary(kvp => kvp.Track.PcNumber.ToString(), kvp => kvp.TextBox.Text)
                }
            };

            var dialog = new SaveFileDialog
            {
                Title = "Save Configuration",
                Filter = "Config Files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "kh_music_config"
            };

            if (dialog.ShowDialog() == true)
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show("✅ Configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void ApplyLoadedConfig(Dictionary<string, Dictionary<string, string>> config)
        {
            if (config.TryGetValue("kh1", out var kh1Config))
            {
                foreach (var (pcNumber, filePath) in kh1Config)
                {
                    var binding = _trackBindingsKH1.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.TextBox != null)
                    {
                        binding.TextBox.Text = filePath;
                        // Add visual feedback for loaded files
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    }
                }
            }

            if (config.TryGetValue("kh2", out var kh2Config))
            {
                foreach (var (pcNumber, filePath) in kh2Config)
                {
                    var binding = _trackBindingsKH2.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.TextBox != null)
                    {
                        binding.TextBox.Text = filePath;
                        // Add visual feedback for loaded files
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    }
                }
            }
        }

        private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load Configuration",
                Filter = "Config Files (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string json = File.ReadAllText(dialog.FileName);
                var config = JsonSerializer.Deserialize<RoutesConfig>(json);

                if (config?.Tracks != null)
                {
                    ApplyLoadedConfig(config.Tracks);
                    MessageBox.Show("✅ Configuration loaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error loading configuration:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Select all checkboxes
            foreach (var checkbox in _trackCheckboxesKH1.Values)
            {
                checkbox.IsChecked = true;
            }
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Deselect all checkboxes
            foreach (var checkbox in _trackCheckboxesKH1.Values)
            {
                checkbox.IsChecked = false;
            }
        }

        private void AssignToSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3",
                Title = "Select Audio File for Multiple Tracks"
            };

            if (dialog.ShowDialog() != true)
                return;

            string selectedFile = dialog.FileName;

            // Check which tab we are on
            bool isKH1 = ((TabItem)MainTabControl.SelectedItem).Header.ToString().Contains("Kingdom Hearts I");

            var bindings = isKH1 ? _trackBindingsKH1 : _trackBindingsKH2;
            var checkboxes = isKH1 ? _trackCheckboxesKH1 : _trackCheckboxesKH2;

            int assignedCount = 0;
            foreach (var (track, textbox) in bindings)
            {
                if (checkboxes.TryGetValue(track, out var checkbox) && checkbox.IsChecked == true)
                {
                    textbox.Text = selectedFile;
                    textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    assignedCount++;
                }
            }

            // Deselect all checkboxes after applying
            foreach (var checkbox in checkboxes.Values)
            {
                checkbox.IsChecked = false;
            }

            // Show feedback message
            if (assignedCount > 0)
            {
                MessageBox.Show($"🎵 Audio file assigned to {assignedCount} track(s)!", "Assignment Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GeneratePatchButton_Click(object sender, RoutedEventArgs e)
        {
            utils.ProgressWindow? progressWindow = null;
            try
            {
                // Initialize logging for patch generation
                PatchLogger.InitializeLog("PatchGeneration");
                PatchLogger.LogStep("Starting GeneratePatchButton_Click");

                // Get which game is selected
                bool isKH1 = ((TabItem)MainTabControl.SelectedItem).Header.ToString().Contains("Kingdom Hearts I");
                PatchLogger.Log($"Selected game: {(isKH1 ? "Kingdom Hearts I" : "Kingdom Hearts II")}");

                // Use application directory as base, not project root when distributed
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                PatchLogger.Log($"Application directory: {appDir}");
                
                // For distributed executable, files should be in the same directory structure
                string encoderDir = Path.Combine(appDir, "utils", "SingleEncoder");
                string encoderExe = Path.Combine(encoderDir, "SingleEncoder.exe");
                string scdTemplate = Path.Combine(encoderDir, "original.scd");
                string patchBasePath = Path.Combine(encoderDir, "patches");

                PatchLogger.Log($"Encoder directory: {encoderDir}");
                PatchLogger.Log($"Encoder executable: {encoderExe}");
                PatchLogger.Log($"SCD template: {scdTemplate}");
                PatchLogger.Log($"Patch base path: {patchBasePath}");

                // Validate that all required files exist
                PatchLogger.LogStep("Validating required files");

                if (!Directory.Exists(encoderDir))
                {
                    PatchLogger.LogError($"SingleEncoder directory not found: {encoderDir}");
                    return;
                }

                if (!File.Exists(encoderExe))
                {
                    PatchLogger.LogError($"SingleEncoder.exe not found: {encoderExe}");
                    return;
                }

                if (!File.Exists(scdTemplate))
                {
                    PatchLogger.LogError($"SCD template not found: {scdTemplate}");
                    return;
                }

                PatchLogger.Log("All required files validated successfully");

                // Test SingleEncoder with a simple command to see if it's working
                PatchLogger.LogStep("Testing SingleEncoder executable");

                if (!TestSingleEncoder(encoderExe, encoderDir))
                {
                    PatchLogger.LogError("SingleEncoder test failed");
                    return;
                }

                // Get custom patch name (if any)
                string? patchNameInput = PatchNameTextBox.Text?.Trim();
                bool hasCustomName = !string.IsNullOrEmpty(patchNameInput);
                PatchLogger.Log($"Custom patch name: {(hasCustomName ? patchNameInput : "None")}");

                // Ensure output folder exists
                string outputDir = Path.Combine(appDir, "patches");
                PatchLogger.Log($"Output directory: {outputDir}");
                Directory.CreateDirectory(outputDir);

                string? baseFileName = hasCustomName
                    ? patchNameInput
                    : (isKH1 ? "KHCustomPatch" : "KHCustomPatch");

                string patchZip = Path.Combine(appDir, "KHCustomPatch.zip"); // Temporary, gets overwritten
                string patchFinal = Path.Combine(outputDir, $"{baseFileName}.{(isKH1 ? "kh1pcpatch" : "kh2pcpatch")}");

                PatchLogger.Log($"Base filename: {baseFileName}");
                PatchLogger.Log($"Temporary ZIP: {patchZip}");
                PatchLogger.Log($"Final patch file: {patchFinal}");

                // Track processing
                var currentTrackBindings = isKH1
                    ? _trackBindingsKH1
                    : _trackBindingsKH2;

                PatchLogger.LogStep("Analyzing selected tracks");

                var selectedTracks = currentTrackBindings
                    .Where(t => !string.IsNullOrWhiteSpace(t.TextBox.Text))
                    .ToList();

                PatchLogger.Log($"Total tracks in game: {currentTrackBindings.Count}");
                PatchLogger.Log($"Selected tracks: {selectedTracks.Count}");

                foreach (var (track, textBox) in selectedTracks.Take(10)) // Log first 10 selected tracks
                {
                    PatchLogger.Log($"  Track: {track.Description} -> {Path.GetFileName(textBox.Text)}");
                }
                if (selectedTracks.Count > 10)
                {
                    PatchLogger.Log($"  ... and {selectedTracks.Count - 10} more tracks");
                }

                if (selectedTracks.Count == 0)
                {
                    PatchLogger.Log("No tracks selected, showing warning to user");
                    PatchLogger.FinalizeLog(false);
                    MessageBox.Show("⚠️ No tracks selected. Please select at least one audio file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create and show progress window
                progressWindow = new utils.ProgressWindow();
                progressWindow.Owner = this;
                progressWindow.Show();

                // Initialize progress
                progressWindow.UpdateProgress("🚀 Starting patch generation...", 0);

                // Create progress reporters
                var trackProgressReporter = new ProgressWindowTrackReporter(progressWindow);
                IProgress<string> progress = new Progress<string>(message => 
                {
                    progressWindow.UpdateProgress(message, -1);
                    PatchLogger.Log($"Progress: {message}");
                });

                // Ensure patches base directory exists
                PatchLogger.LogStep("Creating patch base directory");
                Directory.CreateDirectory(patchBasePath);

                PatchLogger.LogStep("Starting track processing");

                var includedTracks = PatchTrackProcessor.ProcessTracks(
                    currentTrackBindings
                        .Select(t => (t.Track, t.TextBox.Text))
                        .ToList(),
                    encoderExe,
                    encoderDir,
                    scdTemplate,
                    patchBasePath,
                    progress,
                    trackProgressReporter
                );

                PatchLogger.Log($"Track processing completed. Included tracks: {includedTracks.Count}");

                if (includedTracks.Count == 0)
                {
                    PatchLogger.Log("No tracks were processed successfully");
                    PatchLogger.FinalizeLog(false);
                    progressWindow?.Close();
                    return;
                }

                // Hide track progress and show general progress
                progressWindow.HideTrackProgress();
                progressWindow.UpdateProgress("📦 Creating patch file...", 90);

                // Patch creation and packaging
                PatchLogger.LogStep("Starting patch packaging");

                PatchPackager.CreateFinalPatch(patchBasePath, patchZip, patchFinal, includedTracks, progress);

                PatchLogger.LogStep("Patch generation completed successfully");
                PatchLogger.FinalizeLog(true);

                progressWindow.UpdateProgress("✅ Patch generation complete!", 100);
                progressWindow?.Close();
                progressWindow = null;

                // Only show final completion message
                MessageBox.Show(
                    $"✅ Patch generated successfully!\n\n" +
                    $"🎵 Tracks included: {includedTracks.Count}",
                    "Patch Generation Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in GeneratePatchButton_Click", ex);
                PatchLogger.FinalizeLog(false);
                
                progressWindow?.Close();
            }
        }

        private bool TestSingleEncoder(string encoderExe, string encoderDir)
        {
            try
            {
                PatchLogger.LogStep("Running SingleEncoder test");
                
                var psi = new ProcessStartInfo
                {
                    FileName = encoderExe,
                    Arguments = "--help", // Most .NET console apps respond to --help
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = encoderDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        PatchLogger.LogError("Could not start SingleEncoder test process");
                        return false;
                    }

                    bool finished = proc.WaitForExit(10000); // 10 seconds for test
                    
                    if (!finished)
                    {
                        PatchLogger.LogError("SingleEncoder test timed out");
                        proc.Kill();
                        return false;
                    }

                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();

                    PatchLogger.Log($"SingleEncoder test exit code: {proc.ExitCode}");
                    if (!string.IsNullOrEmpty(output))
                    {
                        PatchLogger.Log($"SingleEncoder test output: {output}");
                    }
                    if (!string.IsNullOrEmpty(error))
                    {
                        PatchLogger.Log($"SingleEncoder test error: {error}");
                    }

                    // For this test, we just want to make sure it runs without crashing
                    // Exit codes -1, 0, or 1 are usually acceptable for help commands
                    PatchLogger.Log("SingleEncoder test completed");
                    return true;
                }
            }
            catch (Exception ex)
            {
                PatchLogger.LogError("Exception in SingleEncoder test", ex);
                return false;
            }
        }
        #endregion

        private void ApplyPatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Path to KHPCPatchManager.exe in the root of the project
                string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
                string khpcPatchManagerPath = Path.Combine(projectRoot, "KHPCPatchManager.exe");

                // Check if KHPCPatchManager.exe exists
                if (!File.Exists(khpcPatchManagerPath))
                {
                    MessageBox.Show(
                        $"❌ KHPCPatchManager.exe not found!\n\n" +
                        $"Expected location: {khpcPatchManagerPath}\n\n" +
                        $"Please ensure KHPCPatchManager.exe is in the root folder of the project.",
                        "KHPCPatchManager Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Launch KHPCPatchManager.exe
                var processInfo = new ProcessStartInfo
                {
                    FileName = khpcPatchManagerPath,
                    UseShellExecute = true,
                    WorkingDirectory = projectRoot
                };

                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Error launching KHPCPatchManager:\n\n{ex.Message}",
                    "Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ViewLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KingdomHeartsCustomMusic", "Logs");
                
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // Open the logs folder in Windows Explorer
                System.Diagnostics.Process.Start("explorer.exe", logDir);
                
                // Get the most recent log file for reference
                var logFiles = Directory.GetFiles(logDir, "*.log", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToArray();

                string recentLogInfo = "";
                if (logFiles.Length > 0)
                {
                    var recentLog = logFiles[0];
                    var logInfo = new FileInfo(recentLog);
                    recentLogInfo = $"\n\n📄 Most recent log:\n{Path.GetFileName(recentLog)}\n📅 {logInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
                }

                WpfMessageBox.Show(
                    $"📂 Logs folder opened in Windows Explorer.\n\n" +
                    $"Location: {logDir}\n" +
                    $"📊 Total log files: {logFiles.Length}" +
                    recentLogInfo + "\n\n" +
                    $"Look for files named:\n" +
                    $"• 'PatchApplication_PatchGeneration_*.log' for patch creation logs\n" +
                    $"• 'PatchApplication_Interactive_*.log' for patch application logs\n\n" +
                    $"These files contain detailed information about operations\n" +
                    $"that can be shared for troubleshooting assistance.",
                    "Application Logs",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    $"❌ Could not open logs folder:\n\n{ex.Message}\n\n" +
                    $"You can manually navigate to:\n" +
                    $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KingdomHeartsCustomMusic", "Logs")}",
                    "Error Opening Logs",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}