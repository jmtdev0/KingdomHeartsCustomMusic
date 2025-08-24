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
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                string excelKH1 = Path.Combine(baseDir, "resources", "All Games Track List - KH1.xlsx");
                string excelKH2 = Path.Combine(baseDir, "resources", "All Games Track List - KH2.xlsx");

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
            // Paths declaration, initialization and checks

            // Get which game is selected
            bool isKH1 = ((TabItem)MainTabControl.SelectedItem).Header.ToString().Contains("Kingdom Hearts I");

            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

            string encoderDir = Path.Combine(projectRoot, "utils", "SingleEncoder");
            string encoderExe = Path.Combine(encoderDir, "SingleEncoder.exe");
            string encoderOutput = Path.Combine(encoderDir, "output");
            string scdTemplate = Path.Combine(encoderDir, "original.scd");
            string patchBasePath = Path.Combine(encoderDir, "patches");

            // Get custom patch name (if any)
            string? patchNameInput = PatchNameTextBox.Text?.Trim();
            bool hasCustomName = !string.IsNullOrEmpty(patchNameInput);

            // Ensure output folder
            string outputDir = Path.Combine(projectRoot, "patches");
            Directory.CreateDirectory(outputDir);

            string? baseFileName = hasCustomName
                ? patchNameInput
                : (isKH1 ? "KHCustomPatch" : "KHCustomPatch");

            string patchZip = Path.Combine(projectRoot, "KHCustomPatch.zip"); // Temporary, gets overwritten
            string patchFinal = Path.Combine(outputDir, $"{baseFileName}.{(isKH1 ? "kh1pcpatch" : "kh2pcpatch")}");

            // Track processing

            var currentTrackBindings = isKH1
                ? _trackBindingsKH1
                : _trackBindingsKH2;

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
                MessageBox.Show("⚠️ No tracks selected. Please select at least one audio file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Patch creation and packaging
            PatchPackager.CreateFinalPatch(patchBasePath, patchZip, patchFinal, includedTracks);
        }

        #endregion
    }
}