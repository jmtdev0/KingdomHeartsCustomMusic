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
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace KingdomHeartsCustomMusic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<(TrackInfo Track, TextBox PathTextBox)> _trackBindingsKH1 = new();
        private readonly List<(TrackInfo Track, TextBox PathTextBox)> _trackBindingsKH2 = new();
        private readonly List<(TrackInfo Track, TextBox PathTextBox)> _trackBindingsBBS = new();
        private readonly List<(TrackInfo Track, TextBox PathTextBox)> _trackBindingsReCOM = new();
        private readonly List<(TrackInfo Track, TextBox PathTextBox)> _trackBindingsDDD = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesKH1 = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesKH2 = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesBBS = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesReCOM = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesDDD = new();
        private List<TrackInfo> _tracksKH1 = new();
        private List<TrackInfo> _tracksKH2 = new();
        private List<TrackInfo> _tracksBBS = new();
        private List<TrackInfo> _tracksReCOM = new();
        private List<TrackInfo> _tracksDDD = new();

        // Persist selection across filtering per tab (PcNumber)
        private readonly HashSet<string> _selectedPcNumbersKH1 = new();
        private readonly HashSet<string> _selectedPcNumbersKH2 = new();
        private readonly HashSet<string> _selectedPcNumbersBBS = new();
        private readonly HashSet<string> _selectedPcNumbersReCOM = new();
        private readonly HashSet<string> _selectedPcNumbersDDD = new();

        private static readonly string[] DefaultPatchNames = new[]
        {
            "KH1CustomPatch", "KH2CustomPatch", "BBSCustomPatch", "ReCOMCustomPatch", "DDDCustomPatch", "KHCustomPatch"
        };

        private int _creditsClickCount = 0;
        private static readonly string[] MemeExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
        private static readonly Regex YouTubeRegex = new(
            @"^(https?://)?(www\.)?(youtube\.com/watch\?v=|youtu\.be/)[^\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public MainWindow()
        {
            Logger.Initialize();
            InitializeComponent();
            
            // Diferir la carga hasta que el árbol visual esté listo (evita NRE por nombres no materializados)
            Loaded += (_, __) =>
            {
                try { LoadTracks(); }
                catch { /* mostrado en LoadTracks */ }
            };

            // Hacer que los ComboBox se desplieguen al hacer clic en cualquier parte
            TrackSortComboBoxKH1!.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxKH2!.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxBBS!.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxReCOM!.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxDDD!.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;

            // Cambiar el texto del botón de generación de parche según la pestaña activa
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
            UpdateGeneratePatchButtonText();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Esperar a que el cambio de pestaña se complete antes de actualizar el texto
            Dispatcher.BeginInvoke(new Action(UpdateGeneratePatchButtonText), System.Windows.Threading.DispatcherPriority.Background);
        }

        private string GetDefaultPatchNameForSelectedTab()
        {
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            var header = selectedTab?.Header?.ToString() ?? string.Empty;
            return header switch
            {
                "Kingdom Hearts I" => "KH1CustomPatch",
                "Kingdom Hearts II" => "KH2CustomPatch",
                "Birth by Sleep" => "BBSCustomPatch",
                "Chain of Memories" => "ReCOMCustomPatch",
                "Dream Drop Distance" => "DDDCustomPatch",
                _ => "KHCustomPatch"
            };
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
                    else if (selectedTab.Header.ToString().Equals("Birth by Sleep"))
                        GeneratePatchButton.Content = "✨ Generate Music Patch (BBS)";
                    else if (selectedTab.Header.ToString().Equals("Chain of Memories"))
                        GeneratePatchButton.Content = "✨ Generate Music Patch (ReCOM)";
                    else if (selectedTab.Header.ToString().Equals("Dream Drop Distance"))
                        GeneratePatchButton.Content = "✨ Generate Music Patch (DDD)";
                }
            }

            // Set default Patch Name for the active tab when empty or when it currently holds another default
            if (PatchNameTextBox != null)
            {
                var defaultName = GetDefaultPatchNameForSelectedTab();
                if (string.IsNullOrWhiteSpace(PatchNameTextBox.Text) || DefaultPatchNames.Contains(PatchNameTextBox.Text))
                {
                    PatchNameTextBox.Text = defaultName;
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
                // Obtener la raíz del proyecto (no la carpeta bin)
                var projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                var kh1Csv = Path.Combine(projectRoot, "resources", "All Games Track List - KH1.csv");
                var kh2Csv = Path.Combine(projectRoot, "resources", "All Games Track List - KH2.csv");
                var bbsCsv = Path.Combine(projectRoot, "resources", "All Games Track List - BBS.csv");
                var recomCsv = Path.Combine(projectRoot, "resources", "All Games Track List - ReCOM.csv");
                var dddCsv = Path.Combine(projectRoot, "resources", "All Games Track List - DDD.csv");

                _tracksKH1 = TrackListLoader.LoadTrackList(kh1Csv);
                _tracksKH2 = TrackListLoader.LoadTrackList(kh2Csv);
                _tracksBBS = TrackListLoader.LoadTrackList(bbsCsv);
                _tracksReCOM = TrackListLoader.LoadTrackList(recomCsv);
                _tracksDDD = TrackListLoader.LoadTrackList(dddCsv);

                RenderTrackListKH1(_tracksKH1);
                RenderTrackListKH2(_tracksKH2);
                RenderTrackListBBS(_tracksBBS);
                RenderTrackListReCOM(_tracksReCOM);
                RenderTrackListDDD(_tracksDDD);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading track lists:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderTrackListKH1(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelKH1 == null) return; // safety
            // Guardar los valores actuales (solo PcNumber no vacíos)
            var currentValues = _trackBindingsKH1
                .Where(b => !string.IsNullOrEmpty(b.Track.PcNumber))
                .ToDictionary(b => b.Track.PcNumber!, b => b.PathTextBox.Text);
            WorldListPanelKH1.Children.Clear();
            _trackBindingsKH1.Clear();
            _trackCheckboxesKH1.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelKH1, _trackBindingsKH1, _trackCheckboxesKH1);
                // Restaurar valor si existe
                var binding = _trackBindingsKH1.Last();
                if (!string.IsNullOrEmpty(track.PcNumber) && currentValues.TryGetValue(track.PcNumber!, out var value))
                {
                    binding.PathTextBox.Text = value ?? string.Empty;
                }
            }
        }
        private void RenderTrackListKH2(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelKH2 == null) return;
            var currentValues = _trackBindingsKH2
                .Where(b => !string.IsNullOrEmpty(b.Track.PcNumber))
                .ToDictionary(b => b.Track.PcNumber!, b => b.PathTextBox.Text);
            WorldListPanelKH2.Children.Clear();
            _trackBindingsKH2.Clear();
            _trackCheckboxesKH2.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelKH2, _trackBindingsKH2, _trackCheckboxesKH2);
                var binding = _trackBindingsKH2.Last();
                if (!string.IsNullOrEmpty(track.PcNumber) && currentValues.TryGetValue(track.PcNumber!, out var value))
                {
                    binding.PathTextBox.Text = value ?? string.Empty;
                }
            }
        }
        private void RenderTrackListBBS(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelBBS == null) return;
            var currentValues = _trackBindingsBBS
                .Where(b => !string.IsNullOrEmpty(b.Track.PcNumber))
                .ToDictionary(b => b.Track.PcNumber!, b => b.PathTextBox.Text);
            WorldListPanelBBS.Children.Clear();
            _trackBindingsBBS.Clear();
            _trackCheckboxesBBS.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelBBS, _trackBindingsBBS, _trackCheckboxesBBS);
                var binding = _trackBindingsBBS.Last();
                if (!string.IsNullOrEmpty(track.PcNumber) && currentValues.TryGetValue(track.PcNumber!, out var value))
                {
                    binding.PathTextBox.Text = value ?? string.Empty;
                }
            }
        }
        private void RenderTrackListReCOM(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelReCOM == null) return;
            var currentValues = _trackBindingsReCOM
                .Where(b => !string.IsNullOrEmpty(b.Track.PcNumber))
                .ToDictionary(b => b.Track.PcNumber!, b => b.PathTextBox.Text);
            WorldListPanelReCOM.Children.Clear();
            _trackBindingsReCOM.Clear();
            _trackCheckboxesReCOM.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelReCOM, _trackBindingsReCOM, _trackCheckboxesReCOM);
                var binding = _trackBindingsReCOM.Last();
                if (!string.IsNullOrEmpty(track.PcNumber) && currentValues.TryGetValue(track.PcNumber!, out var value))
                {
                    binding.PathTextBox.Text = value ?? string.Empty;
                }
            }
        }
        private void RenderTrackListDDD(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelDDD == null) return;
            // Guardar los valores actuales, ignorando duplicados y PcNumber nulos
            var currentValues = new Dictionary<string, string>();
            foreach (var b in _trackBindingsDDD)
            {
                if (!string.IsNullOrEmpty(b.Track.PcNumber) && !currentValues.ContainsKey(b.Track.PcNumber!))
                    currentValues[b.Track.PcNumber!] = b.PathTextBox.Text;
            }
            WorldListPanelDDD.Children.Clear();
            _trackBindingsDDD.Clear();
            _trackCheckboxesDDD.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelDDD, _trackBindingsDDD, _trackCheckboxesDDD);
                var binding = _trackBindingsDDD.Last();
                if (!string.IsNullOrEmpty(track.PcNumber) && currentValues.TryGetValue(track.PcNumber!, out var value))
                {
                    binding.PathTextBox.Text = value ?? string.Empty;
                }
            }
        }

        private void AddTrackRow(
            TrackInfo track,
            StackPanel containerPanel,
            List<(TrackInfo Track, TextBox PathTextBox)> bindingList,
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
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Browse button auto-size

            // Checkbox with modern styling
            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
                Foreground = Brushes.White
            };
            Grid.SetColumn(checkBox, 0);
            selectionMap[track] = checkBox;

            // Determine selection set by selectionMap reference
            HashSet<string> selectionSet = _selectedPcNumbersKH1;
            if (ReferenceEquals(selectionMap, _trackCheckboxesKH2)) selectionSet = _selectedPcNumbersKH2;
            else if (ReferenceEquals(selectionMap, _trackCheckboxesBBS)) selectionSet = _selectedPcNumbersBBS;
            else if (ReferenceEquals(selectionMap, _trackCheckboxesReCOM)) selectionSet = _selectedPcNumbersReCOM;
            else if (ReferenceEquals(selectionMap, _trackCheckboxesDDD)) selectionSet = _selectedPcNumbersDDD;

            // Apply persisted selection
            if (!string.IsNullOrEmpty(track.PcNumber) && selectionSet.Contains(track.PcNumber))
            {
                checkBox.IsChecked = true;
            }

            // Keep selectionSet in sync
            checkBox.Checked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(track.PcNumber)) selectionSet.Add(track.PcNumber);
            };
            checkBox.Unchecked += (s, e) =>
            {
                if (!string.IsNullOrEmpty(track.PcNumber)) selectionSet.Remove(track.PcNumber);
            };

            // Track description with better typography
            var label = new TextBlock
            {
                Foreground = Brushes.White,
                Text = track.Description ?? string.Empty,
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

            // Real-time validation: red for YouTube URL; yellow for existing local file; gray for empty/others
            textbox.TextChanged += (s, e) =>
            {
                var value = textbox.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value) && YouTubeRegex.IsMatch(value))
                {
                    textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 20, 60)); // red for YouTube URL
                }
                else if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                {
                    textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // folder-like yellow for file path
                }
                else
                {
                    textbox.BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)); // default gray
                }
            };

            // Accessibility: associate label and assign accessible names
            System.Windows.Automation.AutomationProperties.SetLabeledBy(checkBox, label);
            System.Windows.Automation.AutomationProperties.SetName(checkBox, $"Select track: {track.Description}");
            System.Windows.Automation.AutomationProperties.SetLabeledBy(textbox, label);
            System.Windows.Automation.AutomationProperties.SetName(textbox, $"Audio file path or YouTube URL for: {track.Description}");

            // Modern styled browse button
            var button = new Button
            {
                Content = "📁 Browse / YouTube URL",
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)), // #FF2D2D30
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)), // #FF404040
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "You can select a local file or paste a YouTube video URL (no playlists). Example: https://www.youtube.com/watch?v=KzlhVb7iReo"
            };
            Grid.SetColumn(button, 3);
            System.Windows.Automation.AutomationProperties.SetName(button, $"Browse file or paste YouTube URL for {track.Description}");

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
                    // Accessibility: expose selected file via HelpText
                    System.Windows.Automation.AutomationProperties.SetHelpText(textbox, $"Selected file: {dialog.FileName}");
                }
            };

            // Add focus effects to textbox (only thickness)
            textbox.GotFocus += (s, e) => {
                textbox.BorderThickness = new Thickness(2);
            };
            textbox.LostFocus += (s, e) => {
                textbox.BorderThickness = new Thickness(1);
            };

            row.Children.Add(checkBox);
            row.Children.Add(label);
            row.Children.Add(textbox);
            row.Children.Add(button);

            trackBorder.Child = row;
            containerPanel.Children.Add(trackBorder);
            bindingList.Add((Track: track, PathTextBox: textbox));
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
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.PathTextBox.Text) && !string.IsNullOrEmpty(kvp.Track.PcNumber))
                        .ToDictionary(kvp => kvp.Track.PcNumber!, kvp => kvp.PathTextBox.Text),

                    ["kh2"] = _trackBindingsKH2
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.PathTextBox.Text) && !string.IsNullOrEmpty(kvp.Track.PcNumber))
                        .ToDictionary(kvp => kvp.Track.PcNumber!, kvp => kvp.PathTextBox.Text),

                    ["bbs"] = _trackBindingsBBS
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.PathTextBox.Text) && !string.IsNullOrEmpty(kvp.Track.PcNumber))
                        .ToDictionary(kvp => kvp.Track.PcNumber!, kvp => kvp.PathTextBox.Text),

                    ["recom"] = _trackBindingsReCOM
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.PathTextBox.Text) && !string.IsNullOrEmpty(kvp.Track.PcNumber))
                        .ToDictionary(kvp => kvp.Track.PcNumber!, kvp => kvp.PathTextBox.Text),

                    ["ddd"] = _trackBindingsDDD
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.PathTextBox.Text) && !string.IsNullOrEmpty(kvp.Track.PcNumber))
                        .ToDictionary(kvp => kvp.Track.PcNumber!, kvp => kvp.PathTextBox.Text)
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
                    if (binding.PathTextBox != null)
                    {
                        binding.PathTextBox.Text = filePath;
                    }
                }
            }

            if (config.TryGetValue("kh2", out var kh2Config))
            {
                foreach (var (pcNumber, filePath) in kh2Config)
                {
                    var binding = _trackBindingsKH2.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.PathTextBox != null)
                    {
                        binding.PathTextBox.Text = filePath;
                    }
                }
            }

            if (config.TryGetValue("bbs", out var bbsConfig))
            {
                foreach (var (pcNumber, filePath) in bbsConfig)
                {
                    var binding = _trackBindingsBBS.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.PathTextBox != null)
                    {
                        binding.PathTextBox.Text = filePath;
                    }
                }
            }

            if (config.TryGetValue("recom", out var recomConfig))
            {
                foreach (var (pcNumber, filePath) in recomConfig)
                {
                    var binding = _trackBindingsReCOM.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.PathTextBox != null)
                    {
                        binding.PathTextBox.Text = filePath;
                    }
                }
            }

            if (config.TryGetValue("ddd", out var dddConfig))
            {
                foreach (var (pcNumber, filePath) in dddConfig)
                {
                    var binding = _trackBindingsDDD.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.PathTextBox != null)
                    {
                        binding.PathTextBox.Text = filePath;
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
            // Select all checkboxes for the active tab
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            string tabHeader = selectedTab?.Header.ToString() ?? "";

            if (tabHeader.Equals("Kingdom Hearts I"))
            {
                foreach (var checkbox in _trackCheckboxesKH1.Values) checkbox.IsChecked = true;
            }
            else if (tabHeader.Equals("Kingdom Hearts II"))
            {
                foreach (var checkbox in _trackCheckboxesKH2.Values) checkbox.IsChecked = true;
            }
            else if (tabHeader.Equals("Birth by Sleep"))
            {
                foreach (var checkbox in _trackCheckboxesBBS.Values) checkbox.IsChecked = true;
            }
            else if (tabHeader.Equals("Chain of Memories"))
            {
                foreach (var checkbox in _trackCheckboxesReCOM.Values) checkbox.IsChecked = true;
            }
            else if (tabHeader.Equals("Dream Drop Distance"))
            {
                foreach (var checkbox in _trackCheckboxesDDD.Values) checkbox.IsChecked = true;
            }
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Deselect all checkboxes for the active tab
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            string tabHeader = selectedTab?.Header.ToString() ?? "";

            if (tabHeader.Equals("Kingdom Hearts I"))
            {
                foreach (var checkbox in _trackCheckboxesKH1.Values) checkbox.IsChecked = false;
            }
            else if (tabHeader.Equals("Kingdom Hearts II"))
            {
                foreach (var checkbox in _trackCheckboxesKH2.Values) checkbox.IsChecked = false;
            }
            else if (tabHeader.Equals("Birth by Sleep"))
            {
                foreach (var checkbox in _trackCheckboxesBBS.Values) checkbox.IsChecked = false;
            }
            else if (tabHeader.Equals("Chain of Memories"))
            {
                foreach (var checkbox in _trackCheckboxesReCOM.Values) checkbox.IsChecked = false;
            }
            else if (tabHeader.Equals("Dream Drop Distance"))
            {
                foreach (var checkbox in _trackCheckboxesDDD.Values) checkbox.IsChecked = false;
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
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            string tabHeader = selectedTab?.Header.ToString() ?? "";

            List<(TrackInfo, TextBox)> bindings;
            Dictionary<TrackInfo, CheckBox> checkboxes;

            if (tabHeader.Equals("Kingdom Hearts I"))
            {
                bindings = _trackBindingsKH1.Select(b => (b.Track, b.PathTextBox)).ToList();
                checkboxes = _trackCheckboxesKH1;
            }
            else if (tabHeader.Equals("Kingdom Hearts II"))
            {
                bindings = _trackBindingsKH2.Select(b => (b.Track, b.PathTextBox)).ToList();
                checkboxes = _trackCheckboxesKH2;
            }
            else if (tabHeader.Equals("Birth by Sleep"))
            {
                bindings = _trackBindingsBBS.Select(b => (b.Track, b.PathTextBox)).ToList();
                checkboxes = _trackCheckboxesBBS;
            }
            else if (tabHeader.Equals("Chain of Memories"))
            {
                bindings = _trackBindingsReCOM.Select(b => (b.Track, b.PathTextBox)).ToList();
                checkboxes = _trackCheckboxesReCOM;
            }
            else if (tabHeader.Equals("Dream Drop Distance"))
            {
                bindings = _trackBindingsDDD.Select(b => (b.Track, b.PathTextBox)).ToList();
                checkboxes = _trackCheckboxesDDD;
            }
            else
            {
                return; // Tab no reconocida
            }

            int assignedCount = 0;
            foreach (var (track, textbox) in bindings)
            {
                if (checkboxes.TryGetValue(track, out var checkbox) && checkbox.IsChecked == true)
                {
                    textbox.Text = selectedFile;
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

        private static string? TryExtractYouTubeId(string url)
        {
            try
            {
                var uri = new Uri(url.Trim());
                if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                {
                    // https://youtu.be/ID or /ID?si=...
                    var seg = uri.AbsolutePath.Trim('/');
                    return string.IsNullOrWhiteSpace(seg) ? null : seg;
                }
                if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var id = query.Get("v");
                    return string.IsNullOrWhiteSpace(id) ? null : id;
                }
            }
            catch { }
            return null;
        }

        private async Task<Dictionary<string, string>> EnsureYouTubeDownloadsAsync(IEnumerable<(TrackInfo Track, string Value)> bindingsSnapshot)
        {
            var downloads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var urls = bindingsSnapshot
                .Select(b => b.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v) && YouTubeRegex.IsMatch(v!))
                .Select(v => v!) // assert non-null after filtering
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Logger.Log($"EnsureYouTubeDownloadsAsync: found {urls.Count} distinct URL(s)");

            if (urls.Count == 0)
                return downloads; // nothing to do

            string appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string rootDir = Path.Combine(appDir, "Generated Patches");
            Directory.CreateDirectory(rootDir);

            // Migrate legacy Downloaded Tracks at app root into new location
            try
            {
                string legacyDl = Path.Combine(appDir, "Downloaded Tracks");
                string newDl = Path.Combine(rootDir, "Downloaded Tracks");
                if (Directory.Exists(legacyDl) && !Directory.Exists(newDl))
                {
                    Directory.Move(legacyDl, newDl);
                }
            }
            catch { }

            string downloadsDir = Path.Combine(rootDir, "Downloaded Tracks");
            Directory.CreateDirectory(downloadsDir);
            Logger.Log($"Downloads directory: {downloadsDir}");

            // Setup tools to get yt-dlp path (extracted to temp utils)
            var toolsSetup = EmbeddedResourceManager.SetupTools(Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic_Tools"));
            string ytDlpPath = toolsSetup.HasYtDlp ? toolsSetup.YtDlpPath : string.Empty;
            Logger.Log($"yt-dlp available: {toolsSetup.HasYtDlp}, path: {ytDlpPath}");

            // Try to locate ffmpeg
            string? ffmpegPath = null;
            if (toolsSetup.HasFfmpeg && File.Exists(toolsSetup.FfmpegPath))
            {
                ffmpegPath = toolsSetup.FfmpegPath;
                Logger.Log($"ffmpeg embedded at: {ffmpegPath}");
            }
            else
            {
                var appFfmpeg = Path.Combine(appDir, "ffmpeg.exe");
                if (File.Exists(appFfmpeg))
                {
                    ffmpegPath = appFfmpeg;
                    Logger.Log($"ffmpeg found next to app: {ffmpegPath}");
                }
                else
                {
                    // Search PATH
                    try
                    {
                        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                        var candidates = pathEnv.Split(Path.PathSeparator)
                                                .Where(p => !string.IsNullOrWhiteSpace(p))
                                                .Select(p => Path.Combine(p.Trim(), "ffmpeg.exe"));
                        ffmpegPath = candidates.FirstOrDefault(File.Exists);
                        if (!string.IsNullOrEmpty(ffmpegPath))
                            Logger.Log($"ffmpeg found in PATH: {ffmpegPath}");
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(ytDlpPath) || !File.Exists(ytDlpPath))
            {
                Logger.Log("yt-dlp not found; skipping downloads");
                MessageBox.Show(
                    "yt-dlp.exe not available. Place it under utils/Actually in the project and rebuild to enable YouTube downloads.",
                    "yt-dlp Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return downloads;
            }

            int index = 0;
            foreach (var url in urls)
            {
                index++;
                Dispatcher.Invoke(() =>
                {
                    ProgressText.Text = $"Downloading YouTube audio {index}/{urls.Count}";
                });

                var videoId = TryExtractYouTubeId(url);
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    Logger.Log($"Cannot extract YouTube ID from URL: {url}");
                    continue;
                }

                try
                {
                    Logger.Log($"Start download {index}/{urls.Count}: {url}");
                    Logger.Log($"Output dir: {downloadsDir}");

                    var ffmpegArg = !string.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath)
                        ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\""
                        : string.Empty;
                    if (string.IsNullOrEmpty(ffmpegArg))
                        Logger.Log("ffmpeg not found; proceeding without --ffmpeg-location (yt-dlp may still find it if in PATH)");

                    // Force deterministic output name to map by ID
                    string outputTemplate = "%(id)s.%(ext)s";

                    var psi = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = $"-x --audio-format mp3 --no-part --no-progress -o \"{outputTemplate}\"{ffmpegArg} --postprocessor-args \"FFmpegExtractAudio:-filter:a volume=4\" \"{url}\"",
                        WorkingDirectory = downloadsDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    Logger.Log($"Running: \"{psi.FileName}\" {psi.Arguments}");

                    using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) Logger.Log($"yt-dlp[{index}] OUT: {e.Data}"); };
                    proc.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Log($"yt-dlp[{index}] ERR: {e.Data}"); };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    await proc.WaitForExitAsync();

                    Logger.Log($"yt-dlp[{index}] ExitCode: {proc.ExitCode}");

                    // Resolve expected mp3 by ID
                    string expectedMp3 = Path.Combine(downloadsDir, $"{videoId}.mp3");
                    string? resolved = null;
                    if (File.Exists(expectedMp3))
                    {
                        resolved = expectedMp3;
                    }
                    else
                    {
                        // Fallback: search by ID in filename
                        var candidate = Directory.GetFiles(downloadsDir, "*.mp3")
                            .FirstOrDefault(p => Path.GetFileName(p).Contains(videoId, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(candidate)) resolved = candidate;
                    }

                    if (!string.IsNullOrEmpty(resolved))
                    {
                        downloads[url] = resolved!;
                        Logger.Log($"Downloaded file mapped by ID: {url} -> {resolved}");
                    }
                    else
                    {
                        Logger.Log($"No MP3 found matching video ID {videoId}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException($"Exception during download {index}/{urls.Count} for {url}", ex);
                }
            }

            Logger.Log($"EnsureYouTubeDownloadsAsync completed. Resolved {downloads.Count} file(s)");
            return downloads;
        }

        private async void GeneratePatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Log("GeneratePatchButton_Click: started");
                // Detectar la pestaña activa correctamente
                var selectedTab = MainTabControl.SelectedItem as TabItem;
                string tabHeader = selectedTab?.Header.ToString() ?? "";
                
                bool isKH1 = tabHeader.Equals("Kingdom Hearts I");
                bool isKH2 = tabHeader.Equals("Kingdom Hearts II");
                bool isBBS = tabHeader.Equals("Birth by Sleep");
                bool isReCOM = tabHeader.Equals("Chain of Memories");
                bool isDDD = tabHeader.Equals("Dream Drop Distance");

                Logger.Log($"Tab flags - KH1:{isKH1}, KH2:{isKH2}, BBS:{isBBS}, ReCOM:{isReCOM}, DDD:{isDDD}");

                // Usar la lista de tracks correspondiente
                List<(TrackInfo Track, TextBox PathTextBox)> currentTrackBindings;
                
                if (isKH1)
                    currentTrackBindings = _trackBindingsKH1;
                else if (isKH2)
                    currentTrackBindings = _trackBindingsKH2;
                else if (isBBS)
                    currentTrackBindings = _trackBindingsBBS;
                else if (isReCOM)
                    currentTrackBindings = _trackBindingsReCOM;
                else if (isDDD)
                    currentTrackBindings = _trackBindingsDDD;
                else
                    return; // Tab no reconocida

                var selectedTracks = currentTrackBindings
                    .Where(t => !string.IsNullOrWhiteSpace(t.PathTextBox.Text))
                    .ToList();
                Logger.Log($"Selected tracks (non-empty inputs): {selectedTracks.Count}");

                if (selectedTracks.Count == 0)
                {
                    Logger.Log("No tracks selected; aborting");
                    MessageBox.Show("⚠️ No tracks selected. Please select at least one audio file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Snapshot previo (antes de descargar)
                var bindingsSnapshot = currentTrackBindings
                    .Select(t => (t.Track, t.PathTextBox.Text))
                    .ToList();
                Logger.Log($"Bindings snapshot count: {bindingsSnapshot.Count}");

                // Preparar rutas de salida y comprobar overwrite ANTES de descargar nada
                string appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                // Carpeta de salida bajo 'Generated Patches/Patches'
                string rootDir = Path.Combine(appDir, "Generated Patches");
                Directory.CreateDirectory(rootDir);
                string outputDir = Path.Combine(rootDir, "Patches");

                // Obtener nombre de parche (personalizado o por defecto de la pestaña)
                string? patchNameInput = PatchNameTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(patchNameInput))
                {
                    patchNameInput = GetDefaultPatchNameForSelectedTab();
                }
                string baseFileName = patchNameInput!;

                // Migración de carpetas antiguas (appDir\patches/appDir\Patches) a la nueva ruta
                try
                {
                    string legacyLowercase = Path.Combine(appDir, "patches");
                    string legacyUppercase = Path.Combine(appDir, "Patches");
                    if (Directory.Exists(legacyLowercase))
                    {
                        Directory.CreateDirectory(outputDir);
                        foreach (var file in Directory.GetFiles(legacyLowercase))
                        {
                            var dest = Path.Combine(outputDir, Path.GetFileName(file));
                            if (!File.Exists(dest)) File.Move(file, dest);
                        }
                        foreach (var dir in Directory.GetDirectories(legacyLowercase))
                        {
                            var destDir = Path.Combine(outputDir, Path.GetFileName(dir));
                            if (!Directory.Exists(destDir)) Directory.Move(dir, destDir);
                        }
                        Directory.Delete(legacyLowercase, true);
                    }
                    if (Directory.Exists(legacyUppercase))
                    {
                        Directory.CreateDirectory(outputDir);
                        foreach (var file in Directory.GetFiles(legacyUppercase))
                        {
                            var dest = Path.Combine(outputDir, Path.GetFileName(file));
                            if (!File.Exists(dest)) File.Move(file, dest);
                        }
                        foreach (var dir in Directory.GetDirectories(legacyUppercase))
                        {
                            var destDir = Path.Combine(outputDir, Path.GetFileName(dir));
                            if (!Directory.Exists(destDir)) Directory.Move(dir, destDir);
                        }
                        if (!legacyUppercase.Equals(outputDir, StringComparison.OrdinalIgnoreCase))
                            Directory.Delete(legacyUppercase, true);
                    }
                }
                catch { }
                Directory.CreateDirectory(outputDir);

                string gameExtension = isKH1 ? "kh1pcpatch" :
                                     isKH2 ? "kh2pcpatch" : 
                                     isBBS ? "bbspcpatch" :
                                     isReCOM ? "recompcpatch" :
                                     "dddpcpatch";

                string patchZip = Path.Combine(outputDir, "KHCustomPatch.zip");
                string patchFinal = Path.Combine(outputDir, $"{baseFileName}.{gameExtension}");
                Logger.Log($"Planned output - patchZip: {patchZip}, patchFinal: {patchFinal}");

                // Confirm overwrite si el destino ya existe (antes de descargar YouTube)
                if (File.Exists(patchFinal))
                {
                    Logger.Log("Destination patch already exists; asking for overwrite (pre-download)");
                    var overwrite = MessageBox.Show($"The patch '{baseFileName}.{gameExtension}' already exists in the output folder.\n\nDo you want to overwrite it?",
                                                    "Confirm overwrite",
                                                    MessageBoxButton.YesNo,
                                                    MessageBoxImage.Question);
                    if (overwrite != MessageBoxResult.Yes)
                    {
                        Logger.Log("User chose not to overwrite; aborting before downloads");
                        ProgressText.Text = string.Empty;
                        return;
                    }
                }

                // Descargar YouTube primero (si procede)
                ProgressText.Text = "Downloading YouTube audio 0/0";
                var urlToFile = await EnsureYouTubeDownloadsAsync(bindingsSnapshot);
                Logger.Log($"URL to file resolved: {urlToFile.Count}");

                // Reemplazar URLs por rutas locales
                var resolvedSnapshot = bindingsSnapshot
                    .Select(b => (b.Track, Value: urlToFile.TryGetValue(b.Item2, out var path) ? path : b.Item2))
                    .ToList();
                Logger.Log("Resolved snapshot ready for processing");

                // Setup tools from embedded resources (después de confirmar overwrite)
                string tempToolsDir = Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic_Tools");
                var toolsSetup = EmbeddedResourceManager.SetupTools(tempToolsDir);
                Logger.Log($"Tools setup - Encoder: {toolsSetup.HasSingleEncoder}, SCD: {toolsSetup.HasOriginalScd}, PatchManager: {toolsSetup.HasPatchManager}");

                // Elegir template original.scd por juego cuando esté disponible
                string scdTemplate = toolsSetup.OriginalScdPath;
                if (isKH1 && toolsSetup.HasOriginalScdKH1) scdTemplate = toolsSetup.OriginalScdKH1Path;
                else if (isKH2 && toolsSetup.HasOriginalScdKH2) scdTemplate = toolsSetup.OriginalScdKH2Path;
                else if (isBBS && toolsSetup.HasOriginalScdBBS) scdTemplate = toolsSetup.OriginalScdBBSPath;
                else if (isReCOM && toolsSetup.HasOriginalScdReCOM) scdTemplate = toolsSetup.OriginalScdReCOMPath;
                else if (isDDD && toolsSetup.HasOriginalScdDDD) scdTemplate = toolsSetup.OriginalScdDDDPath;

                // Check if we have the minimum required tools
                if (!toolsSetup.IsCompleteSetup)
                {
                    var missing = toolsSetup.GetMissingTools();
                    Logger.Log($"Missing tools: {string.Join(", ", missing)}");
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
                string patchBasePath = Path.Combine(encoderDir, "patches");
                Logger.Log($"Paths - encoderDir: {encoderDir}, scdTemplate: {scdTemplate}, patchBasePath: {patchBasePath}");

                // Wire progress callback
                void ProgressCallback(int current, int total, string phase)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (total <= 0)
                        {
                            ProgressText.Text = string.Empty;
                            return;
                        }
                        var label = phase == "Encoding" ? "Encoding" : "Preparing";
                        ProgressText.Text = $"{label}: {current} / {total}";
                    });
                }

                GeneratePatchButton.IsEnabled = false;
                ProgressText.Text = "Preparing...";

                PatchPackager.PatchResult? result = null;

                int encodedCount = await Task.Run(() =>
                {
                    Logger.Log("ProcessTracks: begin");
                    var includedTracks = PatchTrackProcessor.ProcessTracks(
                        resolvedSnapshot, // use resolved values (local files)
                        encoderDir,
                        scdTemplate,
                        patchBasePath,
                        ProgressCallback
                    );
                    Logger.Log($"ProcessTracks: completed. Included tracks: {includedTracks.Count}");

                    if (includedTracks.Count == 0)
                    {
                        return 0;
                    }

                    Dispatcher.Invoke(() => ProgressText.Text = "Packaging...");
                    Logger.Log("PatchPackager.CreateFinalPatch: begin");
                    result = PatchPackager.CreateFinalPatch(patchBasePath, patchZip, patchFinal, includedTracks);
                    Logger.Log($"PatchPackager.CreateFinalPatch: completed. Final: {result?.FinalPath}");
                    return includedTracks.Count;
                });

                Logger.Log($"Encoded count: {encodedCount}");

                if (encodedCount == 0)
                {
                    ProgressText.Text = string.Empty;
                    MessageBox.Show("❌ No tracks were processed successfully.\n\nPlease check that your audio files are valid and try again.", "Processing Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    Logger.Log("No tracks processed successfully");
                    return;
                }

                ProgressText.Text = "✅ Done";

                if (result != null)
                {
                    Logger.Log($"Patch completed successfully: {result.FinalPath}");
                    var msg = $"🎉 Patch Created Successfully!\n\n" +
                              $"✨ Game: {result.Game}\n" +
                              $"🎵 Tracks included: {result.Tracks}\n" +
                              $"📦 File size: {result.FileSize}\n" +
                              $"📁 Location: {result.FinalPath}\n\n" +
                              $"Your custom music patch is ready to be applied! 🚀";
                    MessageBox.Show(this, msg, "Patch Generation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ProgressText.Text = string.Empty;
                Logger.LogException("GeneratePatchButton_Click exception", ex);
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
            finally
            {
                GeneratePatchButton.IsEnabled = true;
                Logger.Log("GeneratePatchButton_Click: finished");
            }
        }

        private void InstructionsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new InstructionsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void InstructionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var win = new InstructionsWindow();
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

        private void CreditsButton_Click(object sender, RoutedEventArgs e)
        {
            _creditsClickCount++;
            if (_creditsClickCount >= 15)
            {
                _creditsClickCount = 0; // reset counter
                if (TryShowMeme()) return; // show meme instead of credits
            }

            var win = new CreditsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private void CreditsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _creditsClickCount++;
            if (_creditsClickCount >= 15)
            {
                _creditsClickCount = 0; // reset counter
                if (TryShowMeme()) return;
            }

            var win = new CreditsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        private string? TryGetMemesFolder()
        {
            try
            {
                // 1) Next to the executable
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeMemes = Path.Combine(baseDir, "memes");
                if (Directory.Exists(exeMemes)) return exeMemes;

                // 2) Project root (when running from IDE)
                var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
                string rootMemes = Path.Combine(projectRoot, "memes");
                if (Directory.Exists(rootMemes)) return rootMemes;
            }
            catch { }
            return null;
        }

        private bool TryShowMeme()
        {
            var memesFolder = TryGetMemesFolder();
            if (string.IsNullOrEmpty(memesFolder)) return false;

            string[] files;
            try
            {
                files = Directory.GetFiles(memesFolder)
                    .Where(f => MemeExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();
            }
            catch { return false; }

            if (files.Length == 0) return false;

            var rnd = new Random();
            var pick = files[rnd.Next(files.Length)];

            var win = new MemeWindow(pick)
            {
                Owner = this
            };
            win.ShowDialog();
            return true;
        }

        #endregion // End Button events

        #region Sort ComboBoxes

        private void TrackSortComboBoxKH1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksKH1 == null) return;
            var filtered = ApplyFilter(_tracksKH1, GetSearchTextKH1());
            if (TrackSortComboBoxKH1.SelectedIndex == 1)
                RenderTrackListKH1(filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListKH1(filtered);
        }
        private void TrackSortComboBoxKH2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksKH2 == null) return;
            var filtered = ApplyFilter(_tracksKH2, GetSearchTextKH2());
            if (TrackSortComboBoxKH2.SelectedIndex == 1)
                RenderTrackListKH2(filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListKH2(filtered);
        }
        private void TrackSortComboBoxBBS_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksBBS == null) return;
            var filtered = ApplyFilter(_tracksBBS, GetSearchTextBBS());
            if (TrackSortComboBoxBBS.SelectedIndex == 1)
                RenderTrackListBBS(filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListBBS(filtered);
        }
        private void TrackSortComboBoxReCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksReCOM == null) return;
            var filtered = ApplyFilter(_tracksReCOM, GetSearchTextReCOM());
            if (TrackSortComboBoxReCOM.SelectedIndex == 1)
                RenderTrackListReCOM(filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListReCOM(filtered);
        }

        private void TrackSortComboBoxDDD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksDDD == null) return;
            var filtered = ApplyFilter(_tracksDDD, GetSearchTextDDD());
            if (TrackSortComboBoxDDD.SelectedIndex == 1)
            {
                // Orden alfabético, los vacíos al final (usando el mayor valor Unicode)
                var ordered = filtered
                    .OrderBy(t => string.IsNullOrWhiteSpace(t.Description) ? "\uFFFF" : t.Description, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(t => t.PcNumber);
                RenderTrackListDDD(ordered);
            }
            else
            {
                RenderTrackListDDD(filtered);
            }
        }

        #endregion

        // Search filter helpers
        private string GetSearchTextKH1() => (TrackSearchTextBoxKH1?.Text ?? string.Empty).Trim();
        private string GetSearchTextKH2() => (TrackSearchTextBoxKH2?.Text ?? string.Empty).Trim();
        private string GetSearchTextBBS() => (TrackSearchTextBoxBBS?.Text ?? string.Empty).Trim();
        private string GetSearchTextReCOM() => (TrackSearchTextBoxReCOM?.Text ?? string.Empty).Trim();
        private string GetSearchTextDDD() => (TrackSearchTextBoxDDD?.Text ?? string.Empty).Trim();

        private IEnumerable<TrackInfo> ApplyFilter(IEnumerable<TrackInfo> source, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return source;
            return source.Where(t => (t.Description ?? string.Empty)
                                        .Contains(filter, StringComparison.CurrentCultureIgnoreCase));
        }

        private void TrackSearchTextBoxKH1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksKH1 == null) return;
            var filtered = ApplyFilter(_tracksKH1, GetSearchTextKH1());
            if (TrackSortComboBoxKH1.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListKH1(filtered);
        }
        private void TrackSearchTextBoxKH2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksKH2 == null) return;
            var filtered = ApplyFilter(_tracksKH2, GetSearchTextKH2());
            if (TrackSortComboBoxKH2.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListKH2(filtered);
        }
        private void TrackSearchTextBoxBBS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksBBS == null) return;
            var filtered = ApplyFilter(_tracksBBS, GetSearchTextBBS());
            if (TrackSortComboBoxBBS.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListBBS(filtered);
        }
        private void TrackSearchTextBoxReCOM_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksReCOM == null) return;
            var filtered = ApplyFilter(_tracksReCOM, GetSearchTextReCOM());
            if (TrackSortComboBoxReCOM.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListReCOM(filtered);
        }
        private void TrackSearchTextBoxDDD_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksDDD == null) return;
            var filtered = ApplyFilter(_tracksDDD, GetSearchTextDDD());
            if (TrackSortComboBoxDDD.SelectedIndex == 1)
            {
                filtered = filtered
                    .OrderBy(t => string.IsNullOrWhiteSpace(t.Description) ? "\uFFFF" : t.Description, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(t => t.PcNumber);
            }
            RenderTrackListDDD(filtered);
        }
    }
}