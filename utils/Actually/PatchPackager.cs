using System.IO;
using System.IO.Compression;
using static KingdomHeartsMusicPatcher.utils.TrackListLoader;

namespace KingdomHeartsMusicPatcher.utils
{
    public static class PatchPackager
    {
        public class PatchResult
        {
            public required string Game { get; init; }
            public required int Tracks { get; init; }
            public required string FileSize { get; init; }
            public required string FinalPath { get; init; }
        }

        public static PatchResult CreateFinalPatch(string patchBasePath, string patchZipPath, string patchFinalPath, List<TrackInfo> includedTracks)
        {
            // Ensure the "patches" directory exists
            string patchesDir = Path.GetDirectoryName(patchFinalPath)!;
            Directory.CreateDirectory(patchesDir);

            // Create the zip file from the patch content
            ZipFile.CreateFromDirectory(patchBasePath, patchZipPath);

            // Rename the .zip to .kh1pcpatch/.kh2pcpatch
            if (File.Exists(patchFinalPath))
                File.Delete(patchFinalPath); // Avoid exceptions if the file already exists

            File.Move(patchZipPath, patchFinalPath);

            // Delete temporary folder that contained the patch content
            Directory.Delete(patchBasePath, recursive: true);

            // Get file size for display
            var fileInfo = new FileInfo(patchFinalPath);
            string fileSize = fileInfo.Length > 1024 * 1024 
                ? $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB"
                : $"{fileInfo.Length / 1024.0:F1} KB";

            string gameVersion;
            if (patchFinalPath.EndsWith("kh1pcpatch"))
                gameVersion = "Kingdom Hearts I";
            else if (patchFinalPath.EndsWith("kh2pcpatch"))
                gameVersion = "Kingdom Hearts II";
            else if (patchFinalPath.EndsWith("bbspcpatch"))
                gameVersion = "Birth by Sleep";
            else if (patchFinalPath.EndsWith("compcpatch"))
                gameVersion = "Chain of Memories";
            else if (patchFinalPath.EndsWith("dddpcpatch"))
                gameVersion = "Dream Drop Distance";
            else
                gameVersion = "Unknown";

            return new PatchResult
            {
                Game = gameVersion,
                Tracks = includedTracks.Count,
                FileSize = fileSize,
                FinalPath = patchFinalPath
            };
        }
    }
}
