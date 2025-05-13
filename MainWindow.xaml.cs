using KingdomHeartsCustomMusic.utils;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

            var checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            selectionMap[track] = checkBox;

            var label = new TextBlock
            {
                Foreground = Brushes.White,
                Text = track.Description,
                Width = 250,
                VerticalAlignment = VerticalAlignment.Center
            };

            var textbox = new TextBox
            {
                Width = 350,
                Margin = new Thickness(10, 0, 10, 0),
                IsReadOnly = true
            };

            var button = new Button
            {
                Content = "Browse...",
                Background = System.Windows.Media.Brushes.Gray,
                Foreground = System.Windows.Media.Brushes.White,
                Padding = new Thickness(10, 2, 10, 2)
            };

            button.Click += (s, e) =>
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3"
                };
                if (dialog.ShowDialog() == true)
                    textbox.Text = dialog.FileName;
            };

            row.Children.Add(checkBox);
            row.Children.Add(label);
            row.Children.Add(textbox);
            row.Children.Add(button);

            containerPanel.Children.Add(row);
            bindingList.Add((track, textbox));
        }

        #endregion

        #region Button events

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Seleccionar todos los checkboxes
            foreach (var checkbox in _trackCheckboxesKH1.Values)
            {
                checkbox.IsChecked = true;
            }
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // Deseleccionar todos los checkboxes
            foreach (var checkbox in _trackCheckboxesKH1.Values)
            {
                checkbox.IsChecked = false;
            }
        }


        private void AssignToSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3"
            };

            if (dialog.ShowDialog() != true)
                return;

            string selectedFile = dialog.FileName;

            // Verificamos en qué tab estamos
            bool isKH1 = ((TabItem)MainTabControl.SelectedItem).Header.ToString() == "Kingdom Hearts I";

            var bindings = isKH1 ? _trackBindingsKH1 : _trackBindingsKH2;
            var checkboxes = isKH1 ? _trackCheckboxesKH1 : _trackCheckboxesKH2;

            foreach (var (track, textbox) in bindings)
            {
                if (checkboxes.TryGetValue(track, out var checkbox) && checkbox.IsChecked == true)
                {
                    textbox.Text = selectedFile;
                }
            }

            // Deseleccionar todos los checkbox después de aplicar
            foreach (var checkbox in checkboxes.Values)
            {
                checkbox.IsChecked = false;
            }
        }

        private void GeneratePatchButton_Click(object sender, RoutedEventArgs e)
        {
            // Paths declaration, initialization and checks

            // Get which game is selected
            bool isKH1 = ((TabItem)MainTabControl.SelectedItem).Header.ToString() == "Kingdom Hearts I";

            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));

            string encoderDir = Path.Combine(projectRoot, "utils", "SingleEncoder");
            string encoderExe = Path.Combine(encoderDir, "SingleEncoder.exe");
            string encoderOutput = Path.Combine(encoderDir, "output");
            string scdTemplate = Path.Combine(encoderDir, "original.scd");
            string patchBasePath = Path.Combine(encoderDir, "patches");

            // Recoger nombre personalizado del patch (si hay)
            string? patchNameInput = PatchNameTextBox.Text?.Trim();
            bool hasCustomName = !string.IsNullOrEmpty(patchNameInput);

            // Asegurar carpeta de salida
            string outputDir = Path.Combine(projectRoot, "patches");
            Directory.CreateDirectory(outputDir);

            string? baseFileName = hasCustomName
                ? patchNameInput
                : (isKH1 ? "KHCustomPatch" : "KHCustomPatch");

            string patchZip = Path.Combine(projectRoot, "KHCustomPatch.zip"); // Temporal, se sobreescribe
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
                MessageBox.Show("No tracks selected. Please select at least one audio file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Patch creation and packaging

            PatchPackager.CreateFinalPatch(patchBasePath, patchZip, patchFinal, includedTracks);
        }

        #endregion







    }

}