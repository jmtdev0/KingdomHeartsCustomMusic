using System.IO;
using System.IO.Compression;
using System.Windows;
using static KingdomHeartsCustomMusic.utils.TrackListLoader;

namespace KingdomHeartsCustomMusic.utils
{
    public static class PatchPackager
    {
        public static void CreateFinalPatch(string patchBasePath, string patchZipPath, string patchFinalPath, List<TrackInfo> includedTracks)
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

            string gameVersion = patchFinalPath.Contains("kh1pcpatch") ? "Kingdom Hearts I" : "Kingdom Hearts II";

            MessageBox.Show(
                $"🎉 Patch Created Successfully!\n\n" +
                $"✨ Game: {gameVersion}\n" +
                $"🎵 Tracks included: {includedTracks.Count}\n" +
                $"📦 File size: {fileSize}\n" +
                $"📁 Location: {patchFinalPath}\n\n" +
                $"Your custom music patch is ready to use! 🎮",
                "Patch Generation Complete", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
    }
}
