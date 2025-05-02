using KingdomHeartsCustomMusic.utils;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using static KingdomHeartsCustomMusic.utils.TrackListLoader;

namespace KingdomHeartsCustomMusic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<(TrackInfo Track, TextBox TextBox)> _trackBindings = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadTracksFromExcel();
        }

        private void LoadTracksFromExcel()
        {
            try
            {
                string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources/All Games Track List - KH1.xlsx");
                var tracks = LoadTrackList(excelPath);

                foreach (var track in tracks)
                {
                    AddTrackRow(track);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading track list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddTrackRow(TrackInfo track)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

            var label = new TextBlock
            {
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
                    Filter = "WAV files (*.wav)|*.wav"
                };
                if (dialog.ShowDialog() == true)
                    textbox.Text = dialog.FileName;
            };

            row.Children.Add(label);
            row.Children.Add(textbox);
            row.Children.Add(button);
            WorldListPanel.Children.Add(row);

            _trackBindings.Add((track, textbox));
        }

        private void GeneratePatchButton_Click(object sender, RoutedEventArgs e)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));

            string encoderDir = Path.Combine(projectRoot, "utils", "SingleEncoder");
            string encoderExe = Path.Combine(encoderDir, "SingleEncoder.exe");
            string encoderOutput = Path.Combine(encoderDir, "output");
            string scdTemplate = Path.Combine(encoderDir, "original.scd");

            string patchBasePath = Path.Combine(projectRoot, "patches");
            string patchZip = Path.Combine(projectRoot, "KHCustomPatch.zip");
            string patchFinal = Path.Combine(projectRoot, "KHCustomPatch.kh1pcpatch");

            if (!File.Exists(encoderExe) || !File.Exists(scdTemplate))
            {
                MessageBox.Show("SingleEncoder.exe or original.scd not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Directory.Exists(patchBasePath))
                Directory.Delete(patchBasePath, true);
            Directory.CreateDirectory(patchBasePath);

            if (File.Exists(patchZip)) File.Delete(patchZip);
            if (File.Exists(patchFinal)) File.Delete(patchFinal);

            var includedTracks = new List<TrackInfo>();

            foreach (var (track, textbox) in _trackBindings)
            {
                string originalWavPath = textbox.Text;
                if (string.IsNullOrWhiteSpace(originalWavPath) || !File.Exists(originalWavPath))
                    continue;

                try
                {
                    // Copiar el WAV a la carpeta de ejecución de SingleEncoder
                    string wavFileName = Path.GetFileName(originalWavPath);
                    string tempWavPath = Path.Combine(encoderDir, wavFileName);
                    File.Copy(originalWavPath, tempWavPath, overwrite: true);

                    // Ejecutar SingleEncoder con el archivo copiado
                    var psi = new ProcessStartInfo
                    {
                        FileName = encoderExe,
                        Arguments = $"\"{scdTemplate}\" \"{tempWavPath}\" 10 -fl",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = encoderDir
                    };

                    using (var proc = Process.Start(psi))
                    {
                        proc.WaitForExit();
                        if (proc.ExitCode != 0)
                        {
                            string error = proc.StandardError.ReadToEnd();
                            MessageBox.Show($"Failed to encode '{track.Description}':\n{error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            File.Delete(tempWavPath);
                            continue;
                        }
                    }

                    // Copiar el archivo generado al destino
                    string baseName = $"music{track.Number}";
                    string targetScdPath = Path.Combine(
                        patchBasePath,
                        track.Location,
                        track.Folder,
                        baseName + ".dat",
                        baseName + ".win32.scd"
                    );

                    string generatedScd = Path.Combine(encoderOutput, "original.scd");
                    if (!File.Exists(generatedScd))
                        throw new FileNotFoundException("SingleEncoder did not produce the expected output file.", generatedScd);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetScdPath)!);
                    File.Copy(generatedScd, targetScdPath, overwrite: true);

                    includedTracks.Add(track);

                    // Limpieza del .wav y .ogg intermedios
                    File.Delete(tempWavPath);
                    string tempOggPath = Path.Combine(encoderDir, Path.GetFileNameWithoutExtension(tempWavPath) + ".ogg");
                    if (File.Exists(tempOggPath)) File.Delete(tempOggPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to process track '{track.Description}':\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            }

            if (includedTracks.Count == 0)
            {
                MessageBox.Show("No tracks selected. Please select at least one WAV file.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Crear archivo .kh1pcpatch
            ZipFile.CreateFromDirectory(patchBasePath, patchZip);
            File.Move(patchZip, patchFinal);

            string summary = $"Patch created with {includedTracks.Count} track(s):\n" +
                             string.Join("\n", includedTracks.Select(t => $"- {t.Description}"));

            MessageBox.Show(summary, "Patch created successfully", MessageBoxButton.OK, MessageBoxImage.Information);
        }


    }

}