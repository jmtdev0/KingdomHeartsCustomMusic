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

namespace KingdomHeartsCustomMusic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsKH1 = new();
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsKH2 = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesKH1 = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesKH2 = new();
        private List<TrackInfo> _tracksKH1;
        private List<TrackInfo> _tracksKH2;

        public MainWindow()
        {
            InitializeComponent();
            LoadTracks();

            // Hacer que los ComboBox se desplieguen al hacer clic en cualquier parte
            TrackSortComboBoxKH1.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxKH2.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;

            // Cambiar el texto del botón de generación de parche según la pestaña activa
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
            UpdateGeneratePatchButtonText();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Esperar a que el cambio de pestaña se complete antes de actualizar el texto
            Dispatcher.BeginInvoke(new Action(UpdateGeneratePatchButtonText), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateGeneratePatchButtonText()
        {
            if (GeneratePatchButton != null)
            {
                var selectedTab = MainTabControl.SelectedItem as TabItem;
                if (selectedTab != null)
                {
                    if (selectedTab.Header.ToString().Equals("Kingdom Hearts I"))
                        GeneratePatchButton.Content = "✨ Generate Music Patch (KH1)";
                    else if (selectedTab.Header.ToString().Equals("Kingdom Hearts II"))
                        GeneratePatchButton.Content = "✨ Generate Music Patch (KH2)";
                }
            }
        }

        private void ComboBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo != null && !combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = true;
                e.Handled = true;
            }
        }

        #region XAML initialization

        private void LoadTracks()
        {
            try
            {
                // Usar recursos embebidos para los archivos Excel
                var (excelKH1, excelKH2) = EmbeddedResourceManager.GetTrackListPaths();

                _tracksKH1 = TrackListLoader.LoadTrackList(excelKH1);
                _tracksKH2 = TrackListLoader.LoadTrackList(excelKH2);

                RenderTrackListKH1(_tracksKH1);
                RenderTrackListKH2(_tracksKH2);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading track lists:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderTrackListKH1(IEnumerable<TrackInfo> tracks)
        {
            // Guardar los valores actuales
            var currentValues = _trackBindingsKH1.ToDictionary(b => b.Track.PcNumber, b => b.TextBox.Text);
            WorldListPanelKH1.Children.Clear();
            _trackBindingsKH1.Clear();
            _trackCheckboxesKH1.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelKH1, _trackBindingsKH1, _trackCheckboxesKH1);
                // Restaurar valor si existe
                var binding = _trackBindingsKH1.Last();
                if (currentValues.TryGetValue(track.PcNumber, out var value))
                {
                    binding.TextBox.Text = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }
            }
        }
        private void RenderTrackListKH2(IEnumerable<TrackInfo> tracks)
        {
            var currentValues = _trackBindingsKH2.ToDictionary(b => b.Track.PcNumber, b => b.TextBox.Text);
            WorldListPanelKH2.Children.Clear();
            _trackBindingsKH2.Clear();
            _trackCheckboxesKH2.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelKH2, _trackBindingsKH2, _trackCheckboxesKH2);
                var binding = _trackBindingsKH2.Last();
                if (currentValues.TryGetValue(track.PcNumber, out var value))
                {
                    binding.TextBox.Text = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }
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
            try
            {
                // Get which game is selected
                bool isKH1 = ((TabItem)MainTabControl.SelectedItem).Header.ToString().Contains("Kingdom Hearts I");

                // Setup tools from embedded resources
                string appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string tempToolsDir = Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic_Tools");
                
                var toolsSetup = EmbeddedResourceManager.SetupTools(tempToolsDir);

                // Check if we have the minimum required tools
                if (!toolsSetup.IsCompleteSetup)
                {
                    var missing = toolsSetup.GetMissingTools();
                    var message = "❌ Missing required tools for patch generation!\n\n";
                    
                    if (missing.Contains("SingleEncoder.exe") || missing.Contains("original.scd"))
                    {
                        message += "🔴 Critical missing files:\n";
                        foreach (var tool in missing.Where(t => t == "SingleEncoder.exe" || t == "original.scd"))
                            message += $"  • {tool}\n";
                        message += "\n";
                    }
                    
                    if (missing.Any(t => t != "SingleEncoder.exe" && t != "original.scd"))
                    {
                        message += "⚠️ Optional missing files:\n";
                        foreach (var tool in missing.Where(t => t != "SingleEncoder.exe" && t != "original.scd"))
                            message += $"  • {tool}\n";
                        message += "\n";
                    }
                    
                    message += "📋 To complete setup:\n" +
                              "1. Obtain the missing files from KHPCSoundTools\n" +
                              "2. Place them in your project's utils/ folder\n" +
                              "3. Rebuild the application\n\n" +
                              "🔗 Get KHPCSoundTools from:\n" +
                              "• https://github.com/OpenKH/KHPCSoundTools\n" +
                              "• Kingdom Hearts modding community\n\n" +
                              "💡 Once embedded, these tools will be included in your .exe!";
                    
                    MessageBox.Show(message, "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Use the extracted tools
                string encoderDir = toolsSetup.EncoderDirectory;
                string encoderExe = toolsSetup.SingleEncoderPath;
                string scdTemplate = toolsSetup.OriginalScdPath;
                string patchBasePath = Path.Combine(encoderDir, "patches");

                // Get custom patch name (if any)
                string? patchNameInput = PatchNameTextBox.Text?.Trim();
                bool hasCustomName = !string.IsNullOrEmpty(patchNameInput);

                // Ensure output folder exists in application directory (not temp)
                string outputDir = Path.Combine(appDir, "patches");
                Directory.CreateDirectory(outputDir);

                string? baseFileName = hasCustomName
                    ? patchNameInput
                    : (isKH1 ? "KHCustomPatch" : "KHCustomPatch");

                string patchZip = Path.Combine(appDir, "KHCustomPatch.zip"); // Temporary, gets overwritten
                string patchFinal = Path.Combine(outputDir, $"{baseFileName}.{(isKH1 ? "kh1pcpatch" : "kh2pcpatch")}");

                // Track processing
                var currentTrackBindings = isKH1
                    ? _trackBindingsKH1
                    : _trackBindingsKH2;

                var selectedTracks = currentTrackBindings
                    .Where(t => !string.IsNullOrWhiteSpace(t.TextBox.Text))
                    .ToList();

                if (selectedTracks.Count == 0)
                {
                    MessageBox.Show("⚠️ No tracks selected. Please select at least one audio file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Ensure patches base directory exists
                Directory.CreateDirectory(patchBasePath);

                var includedTracks = PatchTrackProcessor.ProcessTracks(
                    currentTrackBindings
                        .Select(t => (t.Track, t.TextBox.Text))
                        .ToList(),
                    encoderExe,
                    encoderDir,
                    scdTemplate,
                    patchBasePath
                );

                if (includedTracks.Count == 0)
                {
                    MessageBox.Show("❌ No tracks were processed successfully.\n\nPlease check that your audio files are valid and try again.", "Processing Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Patch creation and packaging
                PatchPackager.CreateFinalPatch(patchBasePath, patchZip, patchFinal, includedTracks);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"❌ Error generating patch:\n\n{ex.Message}\n\n" +
                    $"Please check:\n" +
                    $"• All required files are present\n" +
                    $"• Audio files are not corrupted\n" +
                    $"• You have write permissions to the application folder",
                    "Patch Generation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void InstructionsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new InstructionsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void CreditsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new CreditsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void InstructionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new InstructionsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void CreditsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new CreditsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void ApplyPatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Setup tools from embedded resources
                string tempToolsDir = Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic_Tools");
                var toolsSetup = EmbeddedResourceManager.SetupTools(tempToolsDir);
                
                if (!toolsSetup.HasPatchManager)
                {
                    MessageBox.Show(
                        $"❌ KHPCPatchManager.exe not available!\n\n" +
                        $"This tool is not embedded in the current build.\n\n" +
                        $"To enable patch application:\n" +
                        $"1. Obtain KHPCPatchManager.exe\n" +
                        $"2. Place it in your project's utils/ folder\n" +
                        $"3. Rebuild the application\n\n" +
                        $"The tool will then be embedded and available automatically.",
                        "KHPCPatchManager Not Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                var psi = new ProcessStartInfo
                {
                    FileName = toolsSetup.PatchManagerPath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(toolsSetup.PatchManagerPath)
                };
                Process.Start(psi);
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

        private void TrackSortComboBoxKH1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksKH1 == null) return;
            if (TrackSortComboBoxKH1.SelectedIndex == 1)
                RenderTrackListKH1(_tracksKH1.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListKH1(_tracksKH1);
        }
        private void TrackSortComboBoxKH2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksKH2 == null) return;
            if (TrackSortComboBoxKH2.SelectedIndex == 1)
                RenderTrackListKH2(_tracksKH2.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListKH2(_tracksKH2);
        }

        #endregion
    }
}