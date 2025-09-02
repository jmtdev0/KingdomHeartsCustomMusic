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
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

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

        // NEW: Persist entered paths per tab (keyed by PcNumber) so filtering doesn't lose values
        private readonly Dictionary<string, string> _pathValuesKH1 = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pathValuesKH2 = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pathValuesBBS = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pathValuesReCOM = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pathValuesDDD = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] DefaultPatchNames = new[]
        {
            "KH1CustomPatch", "KH2CustomPatch", "BBSCustomPatch", "ReCOMCustomPatch", "DDDCustomPatch", "KHCustomPatch"
        };

        private int _creditsClickCount = 0;
        private static readonly string[] MemeExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
        private static readonly Regex YouTubeRegex = new(
            @"^(https?://)?(www\.)?(youtube\.com/watch\?v=|youtu\.be/)[^\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // GitHub update settings
        private const string GitHubOwner = "jmtdev0";
        private const string GitHubRepo = "KingdomHeartsCustomMusic";

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
            WorldListPanelKH1.Children.Clear();
            _trackBindingsKH1.Clear();
            _trackCheckboxesKH1.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelKH1, _trackBindingsKH1, _trackCheckboxesKH1, _pathValuesKH1);
            }
            UpdateShowAssignedOnlyVisibility();
        }
        private void RenderTrackListKH2(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelKH2 == null) return;
            WorldListPanelKH2.Children.Clear();
            _trackBindingsKH2.Clear();
            _trackCheckboxesKH2.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelKH2, _trackBindingsKH2, _trackCheckboxesKH2, _pathValuesKH2);
            }
            UpdateShowAssignedOnlyVisibility();
        }
        private void RenderTrackListBBS(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelBBS == null) return;
            WorldListPanelBBS.Children.Clear();
            _trackBindingsBBS.Clear();
            _trackCheckboxesBBS.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelBBS, _trackBindingsBBS, _trackCheckboxesBBS, _pathValuesBBS);
            }
            UpdateShowAssignedOnlyVisibility();
        }
        private void RenderTrackListReCOM(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelReCOM == null) return;
            WorldListPanelReCOM.Children.Clear();
            _trackBindingsReCOM.Clear();
            _trackCheckboxesReCOM.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelReCOM, _trackBindingsReCOM, _trackCheckboxesReCOM, _pathValuesReCOM);
            }
            UpdateShowAssignedOnlyVisibility();
        }
        private void RenderTrackListDDD(IEnumerable<TrackInfo> tracks)
        {
            if (WorldListPanelDDD == null) return;
            WorldListPanelDDD.Children.Clear();
            _trackBindingsDDD.Clear();
            _trackCheckboxesDDD.Clear();
            foreach (var track in tracks)
            {
                AddTrackRow(track, WorldListPanelDDD, _trackBindingsDDD, _trackCheckboxesDDD, _pathValuesDDD);
            }
            UpdateShowAssignedOnlyVisibility();
        }

        private void AddTrackRow(
            TrackInfo track,
            StackPanel containerPanel,
            List<(TrackInfo Track, TextBox PathTextBox)> bindingList,
            Dictionary<TrackInfo, CheckBox> selectionMap,
            Dictionary<string, string> pathMap)
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
            checkBox.Checked += (s, e) => { if (!string.IsNullOrEmpty(track.PcNumber)) selectionSet.Add(track.PcNumber); };
            checkBox.Unchecked += (s, e) => { if (!string.IsNullOrEmpty(track.PcNumber)) selectionSet.Remove(track.PcNumber); };

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

            // Initialize from persisted map
            if (!string.IsNullOrWhiteSpace(track.PcNumber) && pathMap.TryGetValue(track.PcNumber!, out var stored))
            {
                textbox.Text = stored ?? string.Empty;
            }

            // Real-time validation and persistence
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

                if (!string.IsNullOrEmpty(track.PcNumber))
                {
                    if (string.IsNullOrWhiteSpace(value)) pathMap.Remove(track.PcNumber);
                    else pathMap[track.PcNumber] = value;
                }

                // Update visibility of the "Hide empty tracks" checkbox for all tabs
                UpdateShowAssignedOnlyVisibility();
            };

            // Accessibility
            System.Windows.Automation.AutomationProperties.SetLabeledBy(checkBox, label);
            System.Windows.Automation.AutomationProperties.SetName(checkBox, $"Select track: {track.Description}");
            System.Windows.Automation.AutomationProperties.SetLabeledBy(textbox, label);
            System.Windows.Automation.AutomationProperties.SetName(textbox, $"Audio file path or YouTube URL for: {track.Description}");

            // Browse button
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

            button.MouseEnter += (s, e) => { button.Background = new SolidColorBrush(Color.FromRgb(64, 64, 64)); };
            button.MouseLeave += (s, e) => { button.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)); };

            button.Click += (s, e) =>
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Audio files (*.wav;*.mp3;*.mp4)|*.wav;*.mp3;*.mp4",
                    Title = "Select Audio File"
                };
                if (dialog.ShowDialog() == true)
                {
                    textbox.Text = dialog.FileName; // TextChanged will persist in pathMap
                    System.Windows.Automation.AutomationProperties.SetHelpText(textbox, $"Selected file: {dialog.FileName}");
                }
            };

            // Focus effects
            textbox.GotFocus += (s, e) => { textbox.BorderThickness = new Thickness(2); };
            textbox.LostFocus += (s, e) => { textbox.BorderThickness = new Thickness(1); };

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
            string? ResolveCanonicalPc(IEnumerable<TrackInfo> tracks, string rawKey)
            {
                if (string.IsNullOrWhiteSpace(rawKey)) return null;
                var key = rawKey.Trim();

                // If it's already exactly a PcNumber on a known track, return it
                var exact = tracks.FirstOrDefault(t => string.Equals(t.PcNumber, key, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact.PcNumber;

                // If key starts with "NNN - ..." or "NN - ...", extract candidate before first " - "
                var parts = key.Split(new[] { " - " }, 2, StringSplitOptions.None);
                var candidate = parts.Length > 0 ? parts[0].Trim() : key;

                // If candidate is numeric, try padded/unpadded match
                if (int.TryParse(candidate, out var num))
                {
                    var padded = num.ToString().PadLeft(3, '0');
                    var byPadded = tracks.FirstOrDefault(t => string.Equals(t.PcNumber, padded, StringComparison.OrdinalIgnoreCase));
                    if (byPadded != null) return byPadded.PcNumber;

                    var byUnpadded = tracks.FirstOrDefault(t => t.PcNumber != null && t.PcNumber.TrimStart('0').Equals(candidate.TrimStart('0'), StringComparison.OrdinalIgnoreCase));
                    if (byUnpadded != null) return byUnpadded.PcNumber;
                }

                // Try matching full composite "PC - Description" to find exact track
                var byComposite = tracks.FirstOrDefault(t => ($"{t.PcNumber} - {t.Description}").Equals(key, StringComparison.OrdinalIgnoreCase));
                if (byComposite != null) return byComposite.PcNumber;

                // Try matching by description substring
                var byDesc = tracks.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Description) && key.IndexOf(t.Description, StringComparison.OrdinalIgnoreCase) >= 0);
                if (byDesc != null) return byDesc.PcNumber;

                // As last resort, if the rawKey itself looks numeric return padded form
                if (int.TryParse(key, out var n)) return n.ToString().PadLeft(3, '0');

                return null;
            }

            Dictionary<string, string> BuildTabDict(Dictionary<string, string> pathMap, List<TrackInfo> tracks)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in pathMap.Where(k => !string.IsNullOrWhiteSpace(k.Value)))
                {
                    var rawKey = kvp.Key?.Trim() ?? string.Empty;
                    var path = kvp.Value;

                    var canonical = ResolveCanonicalPc(tracks, rawKey);
                    if (!string.IsNullOrWhiteSpace(canonical))
                    {
                        // Use canonical PcNumber as key (this is what the loader expects)
                        dict[canonical] = path!;
                    }
                    else
                    {
                        // Fallback: still save under the raw key so data isn't lost
                        dict[rawKey] = path!;
                    }
                }
                return dict;
            }

            var config = new RoutesConfig
            {
                Tracks = new Dictionary<string, Dictionary<string, string>>
                {
                    ["kh1"] = BuildTabDict(_pathValuesKH1, _tracksKH1),
                    ["kh2"] = BuildTabDict(_pathValuesKH2, _tracksKH2),
                    ["bbs"] = BuildTabDict(_pathValuesBBS, _tracksBBS),
                    ["recom"] = BuildTabDict(_pathValuesReCOM, _tracksReCOM),
                    ["ddd"] = BuildTabDict(_pathValuesDDD, _tracksDDD)
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
            void ApplyForTab(string key, List<(TrackInfo Track, TextBox PathTextBox)> bindings, Dictionary<string, string> pathMap)
            {
                if (!config.TryGetValue(key, out var tabCfg)) return;
                foreach (var kvp in tabCfg)
                {
                    var rawKey = kvp.Key?.Trim() ?? string.Empty;
                    // Key may be "PC - Description" or just "PC". Extract PC candidate before the first " - "
                    var parts = rawKey.Split(new[] { " - " }, 2, StringSplitOptions.None);
                    var incoming = parts.Length > 0 ? parts[0].Trim() : rawKey;
                    var filePath = kvp.Value;
                    if (string.IsNullOrWhiteSpace(incoming)) continue;

                    // Resolve canonical pcNumber by looking for a matching binding
                    string? resolvedPc = null;

                    // Try exact match against bindings
                    var bindingExact = bindings.FirstOrDefault(b => string.Equals(b.Track.PcNumber, incoming, StringComparison.OrdinalIgnoreCase));
                    if (bindingExact.Track != null)
                        resolvedPc = bindingExact.Track.PcNumber;

                    // If incoming is numeric, try padded/unpadded variants
                    if (resolvedPc == null && int.TryParse(incoming, out var num))
                    {
                        string padded = num.ToString().PadLeft(3, '0');
                        var bindingPadded = bindings.FirstOrDefault(b => string.Equals(b.Track.PcNumber, padded, StringComparison.OrdinalIgnoreCase));
                        if (bindingPadded.Track != null)
                            resolvedPc = bindingPadded.Track.PcNumber;

                        if (resolvedPc == null)
                        {
                            // Try unpadded match
                            var unpadded = num.ToString();
                            var bindingUnpadded = bindings.FirstOrDefault(b => b.Track.PcNumber != null && b.Track.PcNumber.TrimStart('0').Equals(unpadded.TrimStart('0'), StringComparison.OrdinalIgnoreCase));
                            if (bindingUnpadded.Track != null)
                                resolvedPc = bindingUnpadded.Track.PcNumber;
                        }
                    }

                    // If still unresolved, try to match by full rawKey against a binding's full description composite
                    if (resolvedPc == null)
                    {
                        var byDesc = bindings.FirstOrDefault(b => ($"{b.Track.PcNumber} - {b.Track.Description}").Equals(rawKey, StringComparison.OrdinalIgnoreCase));
                        if (byDesc.Track != null)
                            resolvedPc = byDesc.Track.PcNumber;
                    }

                    // Fallback: use incoming as-is
                    if (resolvedPc == null)
                        resolvedPc = incoming;

                    // Persist under resolved canonical PC number so AddTrackRow can find it by track.PcNumber
                    pathMap[resolvedPc] = filePath;

                    // Also set textbox if binding exists for resolvedPc
                    var targetBinding = bindings.FirstOrDefault(b => string.Equals(b.Track.PcNumber, resolvedPc, StringComparison.OrdinalIgnoreCase));
                    if (targetBinding.PathTextBox != null)
                    {
                        targetBinding.PathTextBox.Text = filePath;
                    }
                }
            }

            ApplyForTab("kh1", _trackBindingsKH1, _pathValuesKH1);
            ApplyForTab("kh2", _trackBindingsKH2, _pathValuesKH2);
            ApplyForTab("bbs", _trackBindingsBBS, _pathValuesBBS);
            ApplyForTab("recom", _trackBindingsReCOM, _pathValuesReCOM);
            ApplyForTab("ddd", _trackBindingsDDD, _pathValuesDDD);
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
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Logger.Log($"EnsureYouTubeDownloadsAsync: found {urls.Count} distinct URL(s)");

            if (urls.Count == 0)
                return downloads;

            string appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string rootDir = Path.Combine(appDir, "Generated Patches");
            Directory.CreateDirectory(rootDir);

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

            // Regex to capture percentages like " 12.3%" or "99%"
            var percentRegex = new Regex(@"(?<!\d)(?<num>\d{1,3})(?:\.\d+)?%", RegexOptions.Compiled);

            int index = 0;
            foreach (var url in urls)
            {
                index++;
                int lastPct = -1;
                void UpdatePct(int pct)
                {
                    if (pct < 0 || pct > 100) return;
                    if (pct == lastPct) return;
                    lastPct = pct;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = $"Downloading YouTube audio ({index}/{urls.Count}, {pct}%)";
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    ProgressText.Text = $"Downloading YouTube audio ({index}/{urls.Count}, 0%)";
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

                    string outputTemplate = "%(title)s [%(id)s].%(ext)s";

                    var psi = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = $"-x --audio-format mp3 --no-part --newline -o \"{outputTemplate}\"{ffmpegArg} --postprocessor-args \"FFmpegExtractAudio:-filter:a volume=4\" \"{url}\"",
                        WorkingDirectory = downloadsDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    Logger.Log($"Running: \"{psi.FileName}\" {psi.Arguments}");

                    var preFiles = new HashSet<string>(Directory.GetFiles(downloadsDir, "*.mp3"), StringComparer.OrdinalIgnoreCase);

                    using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            Logger.Log($"yt-dlp[{index}] OUT: {e.Data}");
                            var m = percentRegex.Match(e.Data);
                            if (m.Success && int.TryParse(m.Groups["num"].Value, out var p)) UpdatePct(Math.Min(100, p));
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            Logger.Log($"yt-dlp[{index}] ERR: {e.Data}");
                            var m = percentRegex.Match(e.Data);
                            if (m.Success && int.TryParse(m.Groups["num"].Value, out var p)) UpdatePct(Math.Min(100, p));
                        }
                    };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    await proc.WaitForExitAsync();
                    UpdatePct(100);

                    Logger.Log($"yt-dlp[{index}] ExitCode: {proc.ExitCode}");

                    string? resolved = Directory.GetFiles(downloadsDir, "*.mp3")
                        .FirstOrDefault(p => Path.GetFileName(p).Contains(videoId, StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrEmpty(resolved))
                    {
                        var candidates = Directory.GetFiles(downloadsDir, "*.mp3")
                            .Where(p => !preFiles.Contains(p))
                            .Select(p => new FileInfo(p))
                            .OrderByDescending(f => f.LastWriteTimeUtc)
                            .ToList();
                        if (candidates.Count > 0)
                            resolved = candidates.First().FullName;
                    }

                    if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                    {
                        var stem = Path.GetFileNameWithoutExtension(resolved);
                        var ext = Path.GetExtension(resolved);
                        var idTag = $"[{videoId}]";
                        string newStem = stem.Replace($" {idTag}", string.Empty).Replace(idTag, string.Empty).Trim();
                        string newPath = Path.Combine(downloadsDir, newStem + ext);

                        int dup = 1;
                        while (!string.Equals(newPath, resolved, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
                        {
                            newPath = Path.Combine(downloadsDir, $"{newStem} ({dup++}){ext}");
                        }
                        if (!string.Equals(newPath, resolved, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Move(resolved, newPath);
                                Logger.Log($"Renamed '{resolved}' -> '{newPath}'");
                                resolved = newPath;
                            }
                            catch (Exception rnEx)
                            {
                                Logger.Log($"Rename failed, keeping original filename. Reason: {rnEx.Message}");
                            }
                        }

                        downloads[url] = resolved!;
                        Logger.Log($"Downloaded file mapped by ID: {url} -> {resolved}");
                    }
                    else
                    {
                        Logger.Log($"No MP3 found for URL {url} (ID {videoId})");
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

                // Confirmación antes de comenzar el proceso pesado
                var confirm = MessageBox.Show(
                    "Patch generation can take several minutes, especially if you included multiple YouTube URLs.\n\nDo you want to continue?",
                    "Confirm Patch Generation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    Logger.Log("User cancelled patch generation at confirmation dialog");
                    ProgressText.Text = string.Empty;
                    return;
                }

                // Snapshot previo (antes de descargar)
                var bindingsSnapshot = currentTrackBindings
                    .Select(t => (t.Track, t.PathTextBox.Text))
                    .ToList();

                Logger.Log($"Bindings snapshot count: {bindingsSnapshot.Count}");

                // Preparar rutas de salida y comprobar overwrite ANTES de descargar YouTube
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
                string tempToolsDir = Path.GetTempPath();
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

                // Wire progress callback with item-level percentage
                void ProgressCallback(int current, int total, string phase, int itemPercent)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (total <= 0)
                        {
                            ProgressText.Text = string.Empty;
                            return;
                        }
                        if (phase == "Encoding")
                        {
                            // Clamp percent and show at least 1% while running to avoid 0% -> Done
                            int pct = Math.Max(0, Math.Min(100, itemPercent));
                            ProgressText.Text = $"Encoding ({current}/{total}, {pct}%)";
                        }
                        else
                        {
                            ProgressText.Text = $"Preparing ({current}/{total}, 0%)";
                        }
                    });
                }

                GeneratePatchButton.IsEnabled = false;
                ProgressText.Text = "Preparing (0/0, 0%)";

                PatchPackager.PatchResult? result = null;

                int encodedCount = await Task.Run(() =>
                {
                    Logger.Log("ProcessTracks: begin");
                    var includedTracks = PatchTrackProcessor.ProcessTracks(
                        resolvedSnapshot,
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
            ReapplyCurrentTabFilterAndSort();
         }
         private void TrackSortComboBoxKH2_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
            ReapplyCurrentTabFilterAndSort();
         }
         private void TrackSortComboBoxBBS_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
            ReapplyCurrentTabFilterAndSort();
         }
         private void TrackSortComboBoxReCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
            ReapplyCurrentTabFilterAndSort();
         }

         private void TrackSortComboBoxDDD_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {
            ReapplyCurrentTabFilterAndSort();
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
            var filtered = ApplyFilterWithAssigned(_tracksKH1, GetSearchTextKH1(), _pathValuesKH1, ShowAssignedOnlyKH1.IsChecked == true);
            if (TrackSortComboBoxKH1.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListKH1(filtered);
        }
        private void TrackSearchTextBoxKH2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksKH2 == null) return;
            var filtered = ApplyFilterWithAssigned(_tracksKH2, GetSearchTextKH2(), _pathValuesKH2, ShowAssignedOnlyKH2.IsChecked == true);
            if (TrackSortComboBoxKH2.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListKH2(filtered);
        }
        private void TrackSearchTextBoxBBS_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksBBS == null) return;
            var filtered = ApplyFilterWithAssigned(_tracksBBS, GetSearchTextBBS(), _pathValuesBBS, ShowAssignedOnlyBBS.IsChecked == true);
            if (TrackSortComboBoxBBS.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListBBS(filtered);
        }
        private void TrackSearchTextBoxReCOM_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksReCOM == null) return;
            var filtered = ApplyFilterWithAssigned(_tracksReCOM, GetSearchTextReCOM(), _pathValuesReCOM, ShowAssignedOnlyReCOM.IsChecked == true);
            if (TrackSortComboBoxReCOM.SelectedIndex == 1)
                filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
            RenderTrackListReCOM(filtered);
        }
        private void TrackSearchTextBoxDDD_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_tracksDDD == null) return;
            var filtered = ApplyFilterWithAssigned(_tracksDDD, GetSearchTextDDD(), _pathValuesDDD, ShowAssignedOnlyDDD.IsChecked == true);
            if (TrackSortComboBoxDDD.SelectedIndex == 1)
            {
                filtered = filtered
                    .OrderBy(t => string.IsNullOrWhiteSpace(t.Description) ? "\uFFFF" : t.Description, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(t => t.PcNumber);
            }
            RenderTrackListDDD(filtered);
        }

        private void UpdateShowAssignedOnlyVisibility()
        {
            // KH1
            ShowAssignedOnlyKH1.Visibility = _pathValuesKH1.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            // KH2
            ShowAssignedOnlyKH2.Visibility = _pathValuesKH2.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            // BBS
            ShowAssignedOnlyBBS.Visibility = _pathValuesBBS.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            // ReCOM
            ShowAssignedOnlyReCOM.Visibility = _pathValuesReCOM.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            // DDD
            ShowAssignedOnlyDDD.Visibility = _pathValuesDDD.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private IEnumerable<TrackInfo> ApplyFilterWithAssigned(IEnumerable<TrackInfo> source, string filter, Dictionary<string, string> pathMap, bool showAssignedOnly)
        {
            var filtered = ApplyFilter(source, filter);
            if (!showAssignedOnly) return filtered;
            return filtered.Where(t => !string.IsNullOrWhiteSpace(t.PcNumber) && pathMap.ContainsKey(t.PcNumber));
        }

        private void ReapplyCurrentTabFilterAndSort()
        {
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            string tabHeader = selectedTab?.Header.ToString() ?? "";
            if (tabHeader.Equals("Kingdom Hearts I"))
            {
                var filtered = ApplyFilterWithAssigned(_tracksKH1, GetSearchTextKH1(), _pathValuesKH1, ShowAssignedOnlyKH1.IsChecked == true);
                if (TrackSortComboBoxKH1.SelectedIndex == 1)
                    filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
                RenderTrackListKH1(filtered);
            }
            else if (tabHeader.Equals("Kingdom Hearts II"))
            {
                var filtered = ApplyFilterWithAssigned(_tracksKH2, GetSearchTextKH2(), _pathValuesKH2, ShowAssignedOnlyKH2.IsChecked == true);
                if (TrackSortComboBoxKH2.SelectedIndex == 1)
                    filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
                RenderTrackListKH2(filtered);
            }
            else if (tabHeader.Equals("Birth by Sleep"))
            {
                var filtered = ApplyFilterWithAssigned(_tracksBBS, GetSearchTextBBS(), _pathValuesBBS, ShowAssignedOnlyBBS.IsChecked == true);
                if (TrackSortComboBoxBBS.SelectedIndex == 1)
                    filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
                RenderTrackListBBS(filtered);
            }
            else if (tabHeader.Equals("Chain of Memories"))
            {
                var filtered = ApplyFilterWithAssigned(_tracksReCOM, GetSearchTextReCOM(), _pathValuesReCOM, ShowAssignedOnlyReCOM.IsChecked == true);
                if (TrackSortComboBoxReCOM.SelectedIndex == 1)
                    filtered = filtered.OrderBy(t => t.Description, StringComparer.CurrentCultureIgnoreCase);
                RenderTrackListReCOM(filtered);
            }
            else if (tabHeader.Equals("Dream Drop Distance"))
            {
                var filtered = ApplyFilterWithAssigned(_tracksDDD, GetSearchTextDDD(), _pathValuesDDD, ShowAssignedOnlyDDD.IsChecked == true);
                if (TrackSortComboBoxDDD.SelectedIndex == 1)
                {
                    filtered = filtered
                        .OrderBy(t => string.IsNullOrWhiteSpace(t.Description) ? "\uFFFF" : t.Description, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(t => t.PcNumber);
                }
                RenderTrackListDDD(filtered);
            }
        }

        // Checkbox change handlers
        private void ShowAssignedOnlyKH1_CheckedChanged(object sender, RoutedEventArgs e) => ReapplyCurrentTabFilterAndSort();
        private void ShowAssignedOnlyKH2_CheckedChanged(object sender, RoutedEventArgs e) => ReapplyCurrentTabFilterAndSort();
        private void ShowAssignedOnlyBBS_CheckedChanged(object sender, RoutedEventArgs e) => ReapplyCurrentTabFilterAndSort();
        private void ShowAssignedOnlyReCOM_CheckedChanged(object sender, RoutedEventArgs e) => ReapplyCurrentTabFilterAndSort();
        private void ShowAssignedOnlyDDD_CheckedChanged(object sender, RoutedEventArgs e) => ReapplyCurrentTabFilterAndSort();

        // Hook visibility updates from AddTrackRow persistence
        private void AfterTextChangeUpdateAssignedVisibility()
        {
            UpdateShowAssignedOnlyVisibility();
        }

        private async void UpdateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GeneratePatchButton.IsEnabled = false;
                ProgressText.Text = "Comprobando actualizaciones...";

                var latest = await GetLatestReleaseInfoAsync();
                if (latest == null)
                {
                    MessageBox.Show("No se pudo obtener información de la release más reciente.", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Version current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0,0,0,0);
                if (!Version.TryParse(latest.TagName?.TrimStart('v') ?? "0.0.0", out var latestVersion))
                {
                    latestVersion = new Version(0,0,0,0);
                }

                if (latestVersion <= current)
                {
                    MessageBox.Show($"No hay actualizaciones. Versión actual: {current}", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Buscar asset .exe
                var asset = latest.Assets?.FirstOrDefault(a => a.Name != null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset == null)
                {
                    MessageBox.Show("No se encontró un .exe en los assets de la última release.", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Descargar
                ProgressText.Text = "Descargando actualización...";
                var tmp = Path.Combine(Path.GetTempPath(), "khcm_update");
                Directory.CreateDirectory(tmp);
                var tmpExe = Path.Combine(tmp, asset.Name!);
                await DownloadFileAsync(asset.BrowserDownloadUrl!, tmpExe);

                // Verificar tamaño mínimo
                if (!File.Exists(tmpExe) || new FileInfo(tmpExe).Length == 0)
                {
                    MessageBox.Show("La descarga falló o el archivo está vacío.", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // SHA256 verification: try to find a checksum asset or parse release body
                string? expectedHash = null;
                var checksumAsset = latest.Assets?.FirstOrDefault(a => a.Name != null && (
                    a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.EndsWith(".sha256.txt", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.EndsWith(".sha256sum", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals(asset.Name + ".sha256", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals(asset.Name + ".sha256.txt", StringComparison.OrdinalIgnoreCase)
                ));

                if (checksumAsset != null)
                {
                    var tmpChk = Path.Combine(tmp, checksumAsset.Name!);
                    await DownloadFileAsync(checksumAsset.BrowserDownloadUrl!, tmpChk);
                    try
                    {
                        var txt = File.ReadAllText(tmpChk);
                        // Try to find a line that references the exe name
                        foreach (var line in txt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (line.IndexOf(asset.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var m = Regex.Match(line, @"\b([A-Fa-f0-9]{64})\b");
                                if (m.Success) { expectedHash = m.Groups[1].Value; break; }
                            }
                        }
                        if (expectedHash == null)
                        {
                            var m = Regex.Match(txt, "\\b([A-Fa-f0-9]{64})\\b");
                            if (m.Success) expectedHash = m.Groups[1].Value;
                        }
                    }
                    catch { /* ignore parsing errors */ }
                }

                // Fallback: try to parse release body for a SHA256 next to filename or alone
                if (expectedHash == null && !string.IsNullOrWhiteSpace(latest.Body))
                {
                    var body = latest.Body!;
                    foreach (var line in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf(asset.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var m = Regex.Match(line, "\\b([A-Fa-f0-9]{64})\\b");
                            if (m.Success) { expectedHash = m.Groups[1].Value; break; }
                        }
                    }
                    if (expectedHash == null)
                    {
                        var m = Regex.Match(body, "\\b([A-Fa-f0-9]{64})\\b");
                        if (m.Success) expectedHash = m.Groups[1].Value;
                    }
                }

                if (expectedHash == null)
                {
                    var ask = MessageBox.Show("No se encontró una suma SHA256 para esta release. ¿Continuar sin verificación?",
                                              "Verificación SHA256",
                                              MessageBoxButton.YesNo,
                                              MessageBoxImage.Warning);
                    if (ask != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                else
                {
                    // Compute SHA256 of downloaded file
                    string computed;
                    using (var fs = File.OpenRead(tmpExe))
                    using (var sha = SHA256.Create())
                    {
                        var hash = await sha.ComputeHashAsync(fs);
                        computed = string.Concat(hash.Select(b => b.ToString("x2")));
                    }
                    if (!string.Equals(computed, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show($"SHA256 mismatch:\nExpected: {expectedHash}\nComputed: {computed}", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Lanzar actualizador por lotes
                ProgressText.Text = "Actualizando... reiniciando la aplicación";
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath!;

                var bat = Path.Combine(tmp, "update.bat");
                var batContents = new StringBuilder();
                batContents.AppendLine("@echo off");
                batContents.AppendLine("ping 127.0.0.1 -n 2 >nul");
                batContents.AppendLine($":loop");
                batContents.AppendLine($"tasklist /FI \"PID eq {Process.GetCurrentProcess().Id}\" | find \"{Path.GetFileName(currentExe)}\" >nul");
                batContents.AppendLine("if %ERRORLEVEL%==0 (");
                batContents.AppendLine("    timeout /t 1 >nul");
                batContents.AppendLine("    goto loop");
                batContents.AppendLine(")");
                batContents.AppendLine($"copy /Y \"{tmpExe}\" \"{currentExe}\"");
                batContents.AppendLine($"start \"\" \"{currentExe}\"");
                batContents.AppendLine($"del \"{tmpExe}\"");
                batContents.AppendLine($"del \"%~f0\"");

                File.WriteAllText(bat, batContents.ToString(), Encoding.ASCII);

                var psi = new ProcessStartInfo
                {
                    FileName = bat,
                    UseShellExecute = true,
                    WorkingDirectory = tmp
                };

                Process.Start(psi);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar: {ex.Message}", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GeneratePatchButton.IsEnabled = true;
                ProgressText.Text = string.Empty;
            }
        }

        private static readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate });

        private async Task<GitHubRelease?> GetLatestReleaseInfoAsync()
        {
            var api = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            var req = new HttpRequestMessage(HttpMethod.Get, api);
            req.Headers.UserAgent.ParseAdd("KHCM-Updater/1.0");

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }

        private async Task DownloadFileAsync(string url, string dest)
        {
            using var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.CopyToAsync(fs);
        }

        private class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }
            [JsonPropertyName("body")]
            public string? Body { get; set; }
            public List<GitHubAsset>? Assets { get; set; }
        }
        private class GitHubAsset
        {
            public string? Name { get; set; }
            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}