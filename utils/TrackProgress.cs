namespace KingdomHeartsCustomMusic.utils
{
    public class TrackProgress
    {
        public int CurrentTrack { get; set; }
        public int TotalTracks { get; set; }
        public string CurrentTrackName { get; set; } = "";
        public string Phase { get; set; } = "";
    }

    public interface ITrackProgressReporter
    {
        void ReportProgress(TrackProgress progress);
        void ReportCompleted();
        void ReportError(string errorMessage);
    }
}