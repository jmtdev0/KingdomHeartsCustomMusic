using System;
using System.ComponentModel;
using System.Windows;

namespace KingdomHeartsCustomMusic.utils
{
    public partial class ProgressWindow : Window, INotifyPropertyChanged
    {
        private string _progressText = "";
        private double _progressValue = 0;
        private string _detailText = "";
        private bool _isIndeterminate = true;
        private bool _showTrackProgress = false;
        private string _trackProgressText = "";
        private double _trackProgressValue = 0;
        private string _currentTrackText = "";

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        public string DetailText
        {
            get => _detailText;
            set
            {
                _detailText = value;
                OnPropertyChanged(nameof(DetailText));
            }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                _isIndeterminate = value;
                OnPropertyChanged(nameof(IsIndeterminate));
            }
        }

        public bool ShowTrackProgress
        {
            get => _showTrackProgress;
            set
            {
                _showTrackProgress = value;
                OnPropertyChanged(nameof(ShowTrackProgress));
            }
        }

        public string TrackProgressText
        {
            get => _trackProgressText;
            set
            {
                _trackProgressText = value;
                OnPropertyChanged(nameof(TrackProgressText));
            }
        }

        public double TrackProgressValue
        {
            get => _trackProgressValue;
            set
            {
                _trackProgressValue = value;
                OnPropertyChanged(nameof(TrackProgressValue));
            }
        }

        public string CurrentTrackText
        {
            get => _currentTrackText;
            set
            {
                _currentTrackText = value;
                OnPropertyChanged(nameof(CurrentTrackText));
            }
        }

        public ProgressWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateProgress(string text, double value = -1, string detail = "")
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText = text;
                DetailText = detail;
                
                if (value >= 0)
                {
                    IsIndeterminate = false;
                    ProgressValue = value;
                }
                else
                {
                    IsIndeterminate = true;
                }
            });
        }

        public void UpdateTrackProgress(int currentTrack, int totalTracks, string currentTrackName = "")
        {
            Dispatcher.Invoke(() =>
            {
                ShowTrackProgress = true;
                double percentage = totalTracks > 0 ? (double)currentTrack / totalTracks * 100 : 0;
                
                TrackProgressValue = percentage;
                TrackProgressText = $"Processing audio files: {currentTrack} of {totalTracks}";
                
                if (!string.IsNullOrEmpty(currentTrackName))
                {
                    CurrentTrackText = $"Current: {currentTrackName}";
                }
                else
                {
                    CurrentTrackText = "";
                }
            });
        }

        public void SetTrackProgressCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                TrackProgressValue = 100;
                TrackProgressText = "? All audio files processed!";
                CurrentTrackText = "Finalizing patch...";
            });
        }

        public void SetTrackProgressError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                TrackProgressText = "? Error processing audio files";
                CurrentTrackText = errorMessage;
            });
        }

        public void HideTrackProgress()
        {
            Dispatcher.Invoke(() =>
            {
                ShowTrackProgress = false;
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Prevent closing during operation
            e.Cancel = true;
        }
    }
}