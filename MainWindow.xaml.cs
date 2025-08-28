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

namespace KingdomHeartsCustomMusic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsKH1 = new();
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsKH2 = new();
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsBBS = new();
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsReCOM = new();
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsDDD = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesKH1 = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesKH2 = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesBBS = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesReCOM = new();
        private readonly Dictionary<TrackInfo, CheckBox> _trackCheckboxesDDD = new();
        private List<TrackInfo> _tracksKH1;
        private List<TrackInfo> _tracksKH2;
        private List<TrackInfo> _tracksBBS;
        private List<TrackInfo> _tracksReCOM;
        private List<TrackInfo> _tracksDDD;

        private static readonly string[] DefaultPatchNames = new[]
        {
            "KH1CustomPatch", "KH2CustomPatch", "BBSCustomPatch", "ReCOMCustomPatch", "DDDCustomPatch", "KHCustomPatch"
        };

        public MainWindow()
        {
            InitializeComponent();
            LoadTracks();

            // Hacer que los ComboBox se desplieguen al hacer clic en cualquier parte
            TrackSortComboBoxKH1.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxKH2.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxBBS.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxReCOM.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;
            TrackSortComboBoxDDD.PreviewMouseLeftButtonDown += ComboBox_PreviewMouseLeftButtonDown;

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
        private void RenderTrackListBBS(IEnumerable<TrackInfo> tracks)
        {
            var currentValues = _trackBindingsBBS.ToDictionary(b => b.Track.PcNumber, b => b.TextBox.Text);
            WorldListPanelBBS.Children.Clear();
            _trackBindingsBBS.Clear();
            _trackCheckboxesBBS.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelBBS, _trackBindingsBBS, _trackCheckboxesBBS);
                var binding = _trackBindingsBBS.Last();
                if (currentValues.TryGetValue(track.PcNumber, out var value))
                {
                    binding.TextBox.Text = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }
            }
        }
        private void RenderTrackListReCOM(IEnumerable<TrackInfo> tracks)
        {
            var currentValues = _trackBindingsReCOM.ToDictionary(b => b.Track.PcNumber, b => b.TextBox.Text);
            WorldListPanelReCOM.Children.Clear();
            _trackBindingsReCOM.Clear();
            _trackCheckboxesReCOM.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelReCOM, _trackBindingsReCOM, _trackCheckboxesReCOM);
                var binding = _trackBindingsReCOM.Last();
                if (currentValues.TryGetValue(track.PcNumber, out var value))
                {
                    binding.TextBox.Text = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                }
            }
        }
        private void RenderTrackListDDD(IEnumerable<TrackInfo> tracks)
        {
            // Guardar los valores actuales, ignorando duplicados
            var currentValues = new Dictionary<string, string>();
            foreach (var b in _trackBindingsDDD)
            {
                if (!currentValues.ContainsKey(b.Track.PcNumber))
                    currentValues[b.Track.PcNumber] = b.TextBox.Text;
            }
            WorldListPanelDDD.Children.Clear();
            _trackBindingsDDD.Clear();
            _trackCheckboxesDDD.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelDDD, _trackBindingsDDD, _trackCheckboxesDDD);
                var binding = _trackBindingsDDD.Last();
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

            // Accessibility: associate label and assign accessible names
            System.Windows.Automation.AutomationProperties.SetLabeledBy(checkBox, label);
            System.Windows.Automation.AutomationProperties.SetName(checkBox, $"Select track: {track.Description}");
            System.Windows.Automation.AutomationProperties.SetLabeledBy(textbox, label);
            System.Windows.Automation.AutomationProperties.SetName(textbox, $"Audio file path for: {track.Description}");

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
            System.Windows.Automation.AutomationProperties.SetName(button, $"Browse for {track.Description}");

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
                    // Accessibility: expose selected file via HelpText
                    System.Windows.Automation.AutomationProperties.SetHelpText(textbox, $"Selected file: {dialog.FileName}");
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
                        .ToDictionary(kvp => kvp.Track.PcNumber.ToString(), kvp => kvp.TextBox.Text),

                    ["bbs"] = _trackBindingsBBS
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.TextBox.Text))
                        .ToDictionary(kvp => kvp.Track.PcNumber.ToString(), kvp => kvp.TextBox.Text),

                    ["recom"] = _trackBindingsReCOM
                        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.TextBox.Text))
                        .ToDictionary(kvp => kvp.Track.PcNumber.ToString(), kvp => kvp.TextBox.Text),

                    ["ddd"] = _trackBindingsDDD
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

            if (config.TryGetValue("bbs", out var bbsConfig))
            {
                foreach (var (pcNumber, filePath) in bbsConfig)
                {
                    var binding = _trackBindingsBBS.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.TextBox != null)
                    {
                        binding.TextBox.Text = filePath;
                        // Add visual feedback for loaded files
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    }
                }
            }

            if (config.TryGetValue("recom", out var recomConfig))
            {
                foreach (var (pcNumber, filePath) in recomConfig)
                {
                    var binding = _trackBindingsReCOM.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
                    if (binding.TextBox != null)
                    {
                        binding.TextBox.Text = filePath;
                        // Add visual feedback for loaded files
                        binding.TextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204));
                    }
                }
            }

            if (config.TryGetValue("ddd", out var dddConfig))
            {
                foreach (var (pcNumber, filePath) in dddConfig)
                {
                    var binding = _trackBindingsDDD.FirstOrDefault(b => b.Track.PcNumber == pcNumber);
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
                bindings = _trackBindingsKH1;
                checkboxes = _trackCheckboxesKH1;
            }
            else if (tabHeader.Equals("Kingdom Hearts II"))
            {
                bindings = _trackBindingsKH2;
                checkboxes = _trackCheckboxesKH2;
            }
            else if (tabHeader.Equals("Birth by Sleep"))
            {
                bindings = _trackBindingsBBS;
                checkboxes = _trackCheckboxesBBS;
            }
            else if (tabHeader.Equals("Chain of Memories"))
            {
                bindings = _trackBindingsReCOM;
                checkboxes = _trackCheckboxesReCOM;
            }
            else if (tabHeader.Equals("Dream Drop Distance"))
            {
                bindings = _trackBindingsDDD;
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

        private async void GeneratePatchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Detectar la pestaña activa correctamente
                var selectedTab = MainTabControl.SelectedItem as TabItem;
                string tabHeader = selectedTab?.Header.ToString() ?? "";
                
                bool isKH1 = tabHeader.Equals("Kingdom Hearts I");
                bool isKH2 = tabHeader.Equals("Kingdom Hearts II");
                bool isBBS = tabHeader.Equals("Birth by Sleep");
                bool isReCOM = tabHeader.Equals("Chain of Memories");
                bool isDDD = tabHeader.Equals("Dream Drop Distance");

                // Usar la lista de tracks correspondiente
                List<(TrackInfo Track, TextBox TextBox)> currentTrackBindings;
                
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
                    .Where(t => !string.IsNullOrWhiteSpace(t.TextBox.Text))
                    .ToList();

                if (selectedTracks.Count == 0)
                {
                    MessageBox.Show("⚠️ No tracks selected. Please select at least one audio file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

                // Get custom patch name (if any) or default for tab
                string? patchNameInput = PatchNameTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(patchNameInput))
                {
                    patchNameInput = GetDefaultPatchNameForSelectedTab();
                }

                // Ensure output folder exists in application directory (not temp)
                string outputDir = Path.Combine(appDir, "patches");
                Directory.CreateDirectory(outputDir);

                string gameExtension = isKH1 ? "kh1pcpatch" : 
                                     isKH2 ? "kh2pcpatch" : 
                                     isBBS ? "bbspcpatch" :
                                     isReCOM ? "recompcpatch" :
                                     "dddpcpatch";
                string baseFileName = patchNameInput!;

                string patchZip = Path.Combine(appDir, "KHCustomPatch.zip"); // Temporary, gets overwritten
                string patchFinal = Path.Combine(outputDir, $"{baseFileName}.{gameExtension}");

                // Confirm overwrite if destination exists
                if (File.Exists(patchFinal))
                {
                    var overwrite = MessageBox.Show($"The patch '{baseFileName}.{gameExtension}' already exists in the output folder.\n\nDo you want to overwrite it?",
                                                    "Confirm overwrite",
                                                    MessageBoxButton.YesNo,
                                                    MessageBoxImage.Question);
                    if (overwrite != MessageBoxResult.Yes)
                    {
                        ProgressText.Text = string.Empty;
                        return;
                    }
                }

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

                var bindingsSnapshot = currentTrackBindings
                        .Select(t => (t.Track, t.TextBox.Text))
                        .ToList();

                PatchPackager.PatchResult? result = null;

                int encodedCount = await Task.Run(() =>
                {
                    var includedTracks = PatchTrackProcessor.ProcessTracks(
                        bindingsSnapshot,
                        encoderExe,
                        encoderDir,
                        scdTemplate,
                        patchBasePath,
                        ProgressCallback
                    );

                    if (includedTracks.Count == 0)
                    {
                        return 0;
                    }

                    Dispatcher.Invoke(() => ProgressText.Text = "Packaging...");
                    result = PatchPackager.CreateFinalPatch(patchBasePath, patchZip, patchFinal, includedTracks);
                    return includedTracks.Count;
                });

                if (encodedCount == 0)
                {
                    ProgressText.Text = string.Empty;
                    MessageBox.Show("❌ No tracks were processed successfully.\n\nPlease check that your audio files are valid and try again.", "Processing Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProgressText.Text = "✅ Done";

                if (result != null)
                {
                    // Show completion dialog with owner = this to keep it on top
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
        private void TrackSortComboBoxBBS_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksBBS == null) return;
            if (TrackSortComboBoxBBS.SelectedIndex == 1)
                RenderTrackListBBS(_tracksBBS.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListBBS(_tracksBBS);
        }
        private void TrackSortComboBoxReCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksReCOM == null) return;
            if (TrackSortComboBoxReCOM.SelectedIndex == 1)
                RenderTrackListReCOM(_tracksReCOM.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase));
            else
                RenderTrackListReCOM(_tracksReCOM);
        }

        private void TrackSortComboBoxDDD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tracksDDD == null) return;
            if (TrackSortComboBoxDDD.SelectedIndex == 1)
            {
                // Orden alfabético, los vacíos al final (usando el mayor valor Unicode)
                var ordered = _tracksDDD
                    .OrderBy(t => string.IsNullOrWhiteSpace(t.Description) ? "\uFFFF" : t.Description, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(t => t.PcNumber);
                RenderTrackListDDD(ordered);
            }
            else
            {
                RenderTrackListDDD(_tracksDDD);
            }
        }

        #endregion
    }
}