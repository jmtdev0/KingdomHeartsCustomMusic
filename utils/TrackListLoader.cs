using System.Globalization;
using System.IO;

namespace KingdomHeartsCustomMusic.utils
{
    public static class TrackListLoader
    {
        public record TrackInfo(string PcNumber, string Description, string LocationBgm, string LocationDat, string Folder, string? FileName = null);

        public static List<TrackInfo> LoadTrackList(string path)
        {
            // If path ends with .xlsx, change to .csv
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Replace(".xlsx", ".csv");
            }

            // Si el archivo no existe, buscar en la carpeta resources en la raíz del proyecto
            if (!File.Exists(path))
            {
                var projectRoot = AppDomain.CurrentDomain.BaseDirectory;
                var resourcesPath = Path.Combine(projectRoot, "resources", Path.GetFileName(path));
                if (File.Exists(resourcesPath))
                {
                    path = resourcesPath;
                }
                else
                {
                    throw new System.IO.FileNotFoundException($"Track list file not found: {path}");
                }
            }

            var list = new List<TrackInfo>();
            var lines = File.ReadAllLines(path);

            // Prepare header for flexible ordering (BBS/ReCOM/DDD can have Filename first)
            var header = lines.Length > 0 ? ParseCsvLine(lines[0]) : Array.Empty<string>();
            int IndexOf(string name) => Array.FindIndex(header, h => string.Equals(h?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            
            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                var columns = ParseCsvLine(line);
                
                // Detectar el tipo de CSV por el nombre del archivo
                bool isBBS = path.Contains("BBS", StringComparison.OrdinalIgnoreCase);
                bool isReCOM = path.Contains("ReCOM", StringComparison.OrdinalIgnoreCase);
                bool isDDD = path.Contains("DDD", StringComparison.OrdinalIgnoreCase);
                
                if (isBBS)
                {
                    // Try to read via header indices to support Filename column and reordering
                    int idxLocation = IndexOf("Location");
                    int idxFolder = IndexOf("Folder");
                    int idxPcNumber = IndexOf("PC Number");
                    int idxPspName = IndexOf("PSP Name");
                    int idxDescription = IndexOf("Description");
                    int idxFileName = IndexOf("Filename");

                    if (idxLocation >= 0 && idxFolder >= 0 && idxPcNumber >= 0 && idxPspName >= 0 && idxDescription >= 0)
                    {
                        string Get(int idx) => idx >= 0 && idx < columns.Length ? columns[idx] : string.Empty;
                        var location = Get(idxLocation);
                        var folder = Get(idxFolder);
                        var pcNumber = Get(idxPcNumber);
                        var pspName = Get(idxPspName);
                        var description = Get(idxDescription);
                        var fileName = Get(idxFileName);

                        string fullDescription;
                        if (!string.IsNullOrWhiteSpace(pspName) && !string.IsNullOrWhiteSpace(description))
                            fullDescription = $"{pspName} - {description}";
                        else if (!string.IsNullOrWhiteSpace(pspName))
                            fullDescription = pspName;
                        else
                            fullDescription = description;

                        list.Add(new TrackInfo(pcNumber, fullDescription, location, "", folder, string.IsNullOrWhiteSpace(fileName) ? null : fileName));
                        continue;
                    }

                    // Fallback to legacy fixed order if header missing
                    if (columns.Length >= 5)
                    {
                        var location = columns[0];           // Location (solo una columna)
                        var folder = columns[1];             // Folder
                        var pcNumber = columns[2];           // PC Number
                        var pspName = columns[3];            // PSP Name
                        var description = columns[4];        // Description

                        string fullDescription;
                        if (!string.IsNullOrWhiteSpace(pspName) && !string.IsNullOrWhiteSpace(description))
                            fullDescription = $"{pspName} - {description}";
                        else if (!string.IsNullOrWhiteSpace(pspName))
                            fullDescription = pspName;
                        else
                            fullDescription = description;

                        list.Add(new TrackInfo(pcNumber, fullDescription, location, "", folder));
                    }
                }
                else if (isReCOM || isDDD)
                {
                    // Prefer header-based parsing (supports Filename)
                    int idxLocation = IndexOf("Location");
                    int idxFolder = IndexOf("Folder");
                    int idxPcNumber = IndexOf("PC Number");
                    int idxConsoleName = isReCOM ? IndexOf("PlayStation2 Name") : IndexOf("3DS Name");
                    int idxDescription = IndexOf("Description");
                    int idxFileName = IndexOf("Filename");

                    if (idxLocation >= 0 && idxFolder >= 0 && idxPcNumber >= 0 && idxDescription >= 0)
                    {
                        string Get(int idx) => idx >= 0 && idx < columns.Length ? columns[idx] : string.Empty;
                        var location = Get(idxLocation);
                        var folder = Get(idxFolder);
                        var pcNumber = Get(idxPcNumber);
                        var consoleName = Get(idxConsoleName);
                        var description = Get(idxDescription);
                        var fileName = Get(idxFileName);
                        
                        string fullDescription;
                        if (!string.IsNullOrWhiteSpace(consoleName) && !string.IsNullOrWhiteSpace(description))
                            fullDescription = $"{consoleName} - {description}";
                        else if (!string.IsNullOrWhiteSpace(consoleName))
                            fullDescription = consoleName;
                        else
                            fullDescription = description;

                        list.Add(new TrackInfo(pcNumber, fullDescription, location, "", folder, string.IsNullOrWhiteSpace(fileName) ? null : fileName));
                    }
                    else if (columns.Length >= 5)
                    {
                        // Fallback to legacy fixed positions
                        var location = columns[0];
                        var folder = columns[1];
                        var pcNumber = columns[2];
                        var consoleName = columns[3];
                        var description = columns[4];

                        string fullDescription;
                        if (!string.IsNullOrWhiteSpace(consoleName) && !string.IsNullOrWhiteSpace(description))
                            fullDescription = $"{consoleName} - {description}";
                        else if (!string.IsNullOrWhiteSpace(consoleName))
                            fullDescription = consoleName;
                        else
                            fullDescription = description;

                        list.Add(new TrackInfo(pcNumber, fullDescription, location, "", folder));
                    }
                }
                else if (!isBBS && !isReCOM && !isDDD && columns.Length >= 6)
                {
                    // Estructura KH1/KH2: Location (.bgm),Location (.dat),Folder,PC Number,PlayStation2 Name,Description
                    var locationBgm = columns[0];        // Location (.bgm)
                    var locationDat = columns[1];        // Location (.dat)
                    var folder = columns[2];             // Folder
                    var pcNumber = columns[3];           // PC Number
                    var ps2Name = columns[4];            // PlayStation2 Name
                    var description = columns[5];       // Description
                    
                    string fullDescription;
                    if (!string.IsNullOrWhiteSpace(ps2Name) && !string.IsNullOrWhiteSpace(description))
                        fullDescription = $"{ps2Name} - {description}";
                    else if (!string.IsNullOrWhiteSpace(ps2Name))
                        fullDescription = ps2Name;
                    else
                        fullDescription = description;

                    list.Add(new TrackInfo(pcNumber, fullDescription, locationBgm, locationDat, folder));
                }
            }

            return list;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add the last field
            result.Add(current.ToString());

            return result.ToArray();
        }
    }
}
