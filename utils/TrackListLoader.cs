using ClosedXML.Excel;

namespace KingdomHeartsCustomMusic.utils
{
    public static class TrackListLoader
    {
        public record TrackInfo(int Number, string Description, string Location, string Folder);

        public static List<TrackInfo> LoadTrackList(string path)
        {
            using var workbook = new XLWorkbook(path);
            var worksheet = workbook.Worksheet("KH1");
            var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header

            var list = new List<TrackInfo>();

            foreach (var row in rows)
            {
                var number = row.Cell(4).GetValue<int>(); // PC Number
                var description = $"{row.Cell(5).GetString()} - {row.Cell(6).GetString()}"; // PlayStation2 Name - Description
                var location = row.Cell(2).GetString(); // Location (.dat)
                var folder = row.Cell(3).GetString(); // Folder

                list.Add(new TrackInfo(number, description, location, folder));
            }

            return list;
        }

    }
}
