using KingdomHeartsCustomMusic.utils;

namespace KingdomHeartsCustomMusic
{
    public class ProgressWindowTrackReporter : ITrackProgressReporter
    {
        private readonly ProgressWindow _window;

        public ProgressWindowTrackReporter(ProgressWindow window)
        {
            _window = window;
        }

        public void ReportProgress(TrackProgress progress)
        {
            _window.UpdateTrackProgress(
                progress.CurrentTrack, 
                progress.TotalTracks, 
                progress.CurrentTrackName
            );
        }

        public void ReportCompleted()
        {
            _window.SetTrackProgressCompleted();
        }

        public void ReportError(string errorMessage)
        {
            _window.SetTrackProgressError(errorMessage);
        }
    }
}