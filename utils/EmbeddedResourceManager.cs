using System.IO;
using System.Reflection;

namespace KingdomHeartsCustomMusic.utils
{
    public static class EmbeddedResourceManager
    {
        private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "KingdomHeartsCustomMusic");

        /// <summary>
        /// Extrae un recurso embebido a un archivo temporal
        /// </summary>
        /// <param name="resourceName">Nombre del recurso embebido</param>
        /// <param name="tempFileName">Nombre del archivo temporal</param>
        /// <returns>Ruta completa del archivo temporal creado</returns>
        public static string ExtractEmbeddedResource(string resourceName, string tempFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = $"KingdomHeartsCustomMusic.{resourceName.Replace('\\', '.').Replace('/', '.')}";
            
            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded resource not found: {fullResourceName}");
            }

            // Crear directorio temporal para la aplicación
            Directory.CreateDirectory(TempDir);
            
            string tempFilePath = Path.Combine(TempDir, tempFileName);
            
            // Solo extraer si el archivo no existe o es diferente
            bool needsExtraction = true;
            if (File.Exists(tempFilePath))
            {
                var existingFileInfo = new FileInfo(tempFilePath);
                if (existingFileInfo.Length == stream.Length)
                {
                    needsExtraction = false;
                }
            }

            if (needsExtraction)
            {
                using var fileStream = File.Create(tempFilePath);
                stream.CopyTo(fileStream);
            }
            
            return tempFilePath;
        }

        /// <summary>
        /// Extrae un recurso embebido a una ruta específica con estructura de directorios
        /// </summary>
        /// <param name="resourceName">Nombre del recurso embebido</param>
        /// <param name="targetPath">Ruta completa donde extraer el archivo</param>
        /// <returns>Ruta del archivo extraído</returns>
        public static string ExtractEmbeddedResourceToPath(string resourceName, string targetPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = $"KingdomHeartsCustomMusic.{resourceName.Replace('\\', '.').Replace('/', '.')}";

            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded resource not found: {fullResourceName}");
            }

            // Crear el directorio si no existe
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // Solo extraer si el archivo no existe o es diferente
            bool needsExtraction = true;
            if (File.Exists(targetPath))
            {
                var existingFileInfo = new FileInfo(targetPath);
                if (existingFileInfo.Length == stream.Length)
                {
                    needsExtraction = false;
                }
            }

            if (needsExtraction)
            {
                using var fileStream = File.Create(targetPath);
                stream.CopyTo(fileStream);
            }
            
            return targetPath;
        }

        /// <summary>
        /// Verifica si un recurso embebido existe
        /// </summary>
        /// <param name="resourceName">Nombre del recurso embebido</param>
        /// <returns>True si el recurso existe</returns>
        public static bool ResourceExists(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = $"KingdomHeartsCustomMusic.{resourceName.Replace('\\', '.').Replace('/', '.')}";

            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            return stream != null;
        }

        /// <summary>
        /// Obtiene todas las rutas de los archivos Excel necesarios
        /// </summary>
        /// <returns>Tupla con las rutas de los archivos KH1 y KH2</returns>
        public static (string KH1Path, string KH2Path) GetTrackListPaths()
        {
            string kh1Path = ExtractEmbeddedResource("resources.All Games Track List - KH1.xlsx", "All Games Track List - KH1.xlsx");
            string kh2Path = ExtractEmbeddedResource("resources.All Games Track List - KH2.xlsx", "All Games Track List - KH2.xlsx");
            
            return (kh1Path, kh2Path);
        }

        /// <summary>
        /// Configura todas las herramientas necesarias en un directorio temporal
        /// </summary>
        /// <param name="baseDirectory">Directorio base donde crear la estructura</param>
        /// <returns>Información sobre qué herramientas están disponibles</returns>
        public static ToolsSetupResult SetupTools(string baseDirectory)
        {
            var result = new ToolsSetupResult
            {
                BaseDirectory = baseDirectory,
                ToolsDirectory = Path.Combine(baseDirectory, "utils"),
                EncoderDirectory = Path.Combine(baseDirectory, "utils", "SingleEncoder"),
                ToolsSubDirectory = Path.Combine(baseDirectory, "utils", "SingleEncoder", "tools")
            };

            // Crear directorios
            Directory.CreateDirectory(result.ToolsDirectory);
            Directory.CreateDirectory(result.EncoderDirectory);
            Directory.CreateDirectory(result.ToolsSubDirectory);
            Directory.CreateDirectory(Path.Combine(result.ToolsSubDirectory, "adpcmencode3"));
            Directory.CreateDirectory(Path.Combine(result.ToolsSubDirectory, "oggenc"));

            // Extraer SingleEncoder si está embebido
            if (ResourceExists("utils.SingleEncoder.SingleEncoder.exe"))
            {
                result.SingleEncoderPath = ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.SingleEncoder.exe",
                    Path.Combine(result.EncoderDirectory, "SingleEncoder.exe"));
                result.HasSingleEncoder = true;
            }

            if (ResourceExists("utils.SingleEncoder.SingleEncoder.dll"))
            {
                ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.SingleEncoder.dll",
                    Path.Combine(result.EncoderDirectory, "SingleEncoder.dll"));
            }

            if (ResourceExists("utils.SingleEncoder.SingleEncoder.runtimeconfig.json"))
            {
                ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.SingleEncoder.runtimeconfig.json",
                    Path.Combine(result.EncoderDirectory, "SingleEncoder.runtimeconfig.json"));
            }

            if (ResourceExists("utils.SingleEncoder.original.scd"))
            {
                result.OriginalScdPath = ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.original.scd",
                    Path.Combine(result.EncoderDirectory, "original.scd"));
                result.HasOriginalScd = true;
            }

            // Extraer herramientas adicionales
            if (ResourceExists("utils.SingleEncoder.tools.adpcmencode3.adpcmencode3.exe"))
            {
                ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.tools.adpcmencode3.adpcmencode3.exe",
                    Path.Combine(result.ToolsSubDirectory, "adpcmencode3", "adpcmencode3.exe"));
                result.HasAdpcmEncode = true;
            }

            if (ResourceExists("utils.SingleEncoder.tools.oggenc.oggenc.exe"))
            {
                ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.tools.oggenc.oggenc.exe",
                    Path.Combine(result.ToolsSubDirectory, "oggenc", "oggenc.exe"));
                result.HasOggEnc = true;
            }

            // Extraer KHPCPatchManager si está embebido
            if (ResourceExists("utils.KHPCPatchManager.exe"))
            {
                result.PatchManagerPath = ExtractEmbeddedResourceToPath(
                    "utils.KHPCPatchManager.exe",
                    Path.Combine(result.ToolsDirectory, "KHPCPatchManager.exe"));
                result.HasPatchManager = true;
            }

            return result;
        }

        /// <summary>
        /// Limpia los archivos temporales al cerrar la aplicación
        /// </summary>
        public static void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(TempDir))
                {
                    Directory.Delete(TempDir, true);
                }
            }
            catch
            {
                // Si no se puede limpiar, no es crítico
            }
        }
    }

    /// <summary>
    /// Resultado del setup de herramientas
    /// </summary>
    public class ToolsSetupResult
    {
        public string BaseDirectory { get; set; } = "";
        public string ToolsDirectory { get; set; } = "";
        public string EncoderDirectory { get; set; } = "";
        public string ToolsSubDirectory { get; set; } = "";
        
        public bool HasSingleEncoder { get; set; }
        public string SingleEncoderPath { get; set; } = "";
        
        public bool HasOriginalScd { get; set; }
        public string OriginalScdPath { get; set; } = "";
        
        public bool HasAdpcmEncode { get; set; }
        public bool HasOggEnc { get; set; }
        
        public bool HasPatchManager { get; set; }
        public string PatchManagerPath { get; set; } = "";

        public bool IsCompleteSetup => HasSingleEncoder && HasOriginalScd;
        
        public List<string> GetMissingTools()
        {
            var missing = new List<string>();
            
            if (!HasSingleEncoder)
                missing.Add("SingleEncoder.exe");
            if (!HasOriginalScd)
                missing.Add("original.scd");
            if (!HasAdpcmEncode)
                missing.Add("adpcmencode3.exe");
            if (!HasOggEnc)
                missing.Add("oggenc.exe");
            if (!HasPatchManager)
                missing.Add("KHPCPatchManager.exe");
                
            return missing;
        }
    }
}