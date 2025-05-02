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
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsKH1 = new();
        private readonly List<(TrackInfo Track, TextBox TextBox)> _trackBindingsKH2 = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadTracks();
        }

        private void LoadTracks()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                string excelKH1 = Path.Combine(baseDir, "resources", "All Games Track List - KH1.xlsx");
                string excelKH2 = Path.Combine(baseDir, "resources", "All Games Track List - KH2.xlsx");

                var tracksKH1 = TrackListLoader.LoadTrackList(excelKH1);
                //var tracksKH2 = TrackListLoader.LoadTrackList(excelKH2);

                foreach (var track in tracksKH1)
                    AddTrackRow(track, WorldListPanelKH1, _trackBindingsKH1);

                //foreach (var track in tracksKH2)
                //    AddTrackRow(track, WorldListPanelKH2, _trackBindingsKH2);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading track lists:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TabIsKh1()
        {
            return ((TabControl)LogicalTreeHelper.FindLogicalNode(this, "tabControl"))?.SelectedIndex == 0;
        }

        private void AddTrackRow(TrackInfo track, StackPanel containerPanel, List<(TrackInfo, TextBox)> bindingList)
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
                    Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3"
                };
                if (dialog.ShowDialog() == true)
                    textbox.Text = dialog.FileName;
            };

            row.Children.Add(label);
            row.Children.Add(textbox);
            row.Children.Add(button);

            containerPanel.Children.Add(row);
            bindingList.Add((track, textbox));
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

            // 🧠 Detectar pestaña activa y usar la lista correspondiente
            var selectedTracks = TabIsKh1() ? _trackBindingsKH1 : _trackBindingsKH2;
            var includedTracks = new List<TrackInfo>();

            foreach (var (track, textbox) in selectedTracks)
            {
                string originalPath = textbox.Text;
                if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
                    continue;

                string extension = Path.GetExtension(originalPath).ToLower();
                string workingPath = originalPath;

                // Si es MP3, convertirlo a WAV temporal
                if (extension == ".mp3")
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "KHCustomMusic");
                    Directory.CreateDirectory(tempDir);
                    workingPath = AudioConverter.ConvertMp3ToWav(originalPath, tempDir);
                }

                try
                {
                    // Copiar WAV a la carpeta de ejecución
                    string wavFileName = Path.GetFileName(workingPath);
                    string tempWavPath = Path.Combine(encoderDir, wavFileName);
                    File.Copy(workingPath, tempWavPath, overwrite: true);

                    var psi = new ProcessStartInfo
                    {
                        FileName = encoderExe,
                        Arguments = $"\"{scdTemplate}\" \"{tempWavPath}\" 10 -fl",
                        UseShellExecute = false, // 👈 ahora sí, ya no se necesita redirección
                        CreateNoWindow = false,
                        WorkingDirectory = encoderDir
                    };

                    using (var proc = Process.Start(psi))
                    {
                        proc.WaitForExit();

                        if (proc.ExitCode != 0)
                        {
                            MessageBox.Show($"Failed to encode '{track.Description}': process exited with code {proc.ExitCode}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            File.Delete(tempWavPath);
                            continue;
                        }
                    }

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

                    // Limpieza de temporales

                    if (workingPath != originalPath && File.Exists(workingPath))
                        File.Delete(workingPath);

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

            ZipFile.CreateFromDirectory(patchBasePath, patchZip);
            File.Move(patchZip, patchFinal);

            string summary = $"Patch created with {includedTracks.Count} track(s):\n" +
                             string.Join("\n", includedTracks.Select(t => $"- {t.Description}"));

            MessageBox.Show(summary, "Patch created successfully", MessageBoxButton.OK, MessageBoxImage.Information);
        }





    }

}