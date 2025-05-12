using ClosedXML.Excel;

namespace KingdomHeartsCustomMusic.utils
{
    public static class TrackListLoader
    {
        public record TrackInfo(string PcNumber, string Description, string LocationBgm, string LocationDat, string Folder);

        public static List<TrackInfo> LoadTrackList(string path)
        {
            using var workbook = new XLWorkbook(path);
            var worksheet = workbook.Worksheet("Tracks");
            var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

            var list = new List<TrackInfo>();

            foreach (var row in rows)
            {
                var locationBgm = row.Cell(1).GetString(); // Location (.dat)
                var locationDat = row.Cell(2).GetString(); // Location (.dat)
                var folder = row.Cell(3).GetString(); // Folder
                var pcNumber = row.Cell(4).GetValue<string>(); // PC Number
                var description = $"{row.Cell(5).GetString()} - {row.Cell(6).GetString()}"; // PlayStation2 Name - Description

                list.Add(new TrackInfo(pcNumber, description, locationBgm, locationDat, folder));
            }

            return list;
        }

    }
}
