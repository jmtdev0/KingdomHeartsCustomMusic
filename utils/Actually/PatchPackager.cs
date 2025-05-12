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
            // Asegurarse de que el directorio "patches" existe
            string patchesDir = Path.GetDirectoryName(patchFinalPath)!;
            Directory.CreateDirectory(patchesDir);

            // Crear el archivo zip desde el contenido del patch
            ZipFile.CreateFromDirectory(patchBasePath, patchZipPath);

            // Renombrar el .zip a .kh1pcpatch
            if (File.Exists(patchFinalPath))
                File.Delete(patchFinalPath); // Evita excepciones si el archivo ya existe

            File.Move(patchZipPath, patchFinalPath);

            // Eliminar carpeta temporal que contenía el contenido del patch
            Directory.Delete(patchBasePath, recursive: true);

            MessageBox.Show($"🎉 Patch created successfully with {includedTracks.Count} tracks!\n\nPath:\n{patchFinalPath}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

    }
}
