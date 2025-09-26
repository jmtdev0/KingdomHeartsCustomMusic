using System.IO;
using System.Reflection;

namespace KingdomHeartsMusicPatcher.utils
{
    public static class EmbeddedResourceManager
    {
        private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "KingdomHeartsMusicPatcher");

        private static Stream? OpenResourceStream(string resourceName)
        {
            // Normalize separators
            var normalized = resourceName.Replace('\\', '.').Replace('/', '.');
            var assembly = Assembly.GetExecutingAssembly();

            // 1) Try exact logical name (when <LogicalName> was used)
            var s = assembly.GetManifestResourceStream(normalized);
            if (s != null) return s;

            // 2) Try prefixed with RootNamespace
            var prefixed = $"KingdomHeartsMusicPatcher.{normalized}";
            s = assembly.GetManifestResourceStream(prefixed);
            if (s != null) return s;

            // 3) Try EndsWith match (last resort, to tolerate minor mismatches)
            try
            {
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith(normalized, System.StringComparison.OrdinalIgnoreCase))
                    {
                        s = assembly.GetManifestResourceStream(name);
                        if (s != null) return s;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Extrae un recurso embebido a un archivo temporal
        /// </summary>
        /// <param name="resourceName">Nombre del recurso embebido</param>
        /// <param name="tempFileName">Nombre del archivo temporal</param>
        /// <returns>Ruta completa del archivo temporal creado</returns>
        public static string ExtractEmbeddedResource(string resourceName, string tempFileName)
        {
            using var stream = OpenResourceStream(resourceName);
            if (stream == null)
            {
                Logger.Log($"[ERM] ExtractEmbeddedResource missing: {resourceName}");
                throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
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
            using var stream = OpenResourceStream(resourceName);
            if (stream == null)
            {
                Logger.Log($"[ERM] ExtractEmbeddedResourceToPath missing: {resourceName}");
                throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
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
            using var stream = OpenResourceStream(resourceName);
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

        private static bool TryExtractOriginalScdVariant(string resourceName, string destinationPath, out string path)
        {
            path = string.Empty;
            if (!ResourceExists(resourceName)) return false;
            path = ExtractEmbeddedResourceToPath(resourceName, destinationPath);
            return File.Exists(path);
        }

        /// <summary>
        /// Configura todas las herramientas necesarias en un directorio temporal
        /// </summary>
        /// <param name="baseDirectory">Directorio base donde crear la estructura</param>
        /// <returns>Información sobre qué herramientas están disponibles</returns>
        public static ToolsSetupResult SetupTools(string baseDirectory)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                Logger.Log($"[ERM] Resource names count={names.Length}");
                foreach (var n in names)
                {
                    if (n.Contains("vgmstream", System.StringComparison.OrdinalIgnoreCase) || n.Contains("KHPCPatchManager", System.StringComparison.OrdinalIgnoreCase))
                        Logger.Log($"[ERM] Resource: {n}");
                }
            }
            catch { }

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

            // vgmstream dir
            string vgmDir = Path.Combine(result.ToolsDirectory, "vgmstream");
            Directory.CreateDirectory(vgmDir);

            // SingleEncoder
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

            // original.scd (genérico y por juego si están embebidos)
            if (ResourceExists("utils.SingleEncoder.original.scd"))
            {
                result.OriginalScdPath = ExtractEmbeddedResourceToPath(
                    "utils.SingleEncoder.original.scd",
                    Path.Combine(result.EncoderDirectory, "original.scd"));
                result.HasOriginalScd = true;
            }

            // Variantes por juego (acepta utils/SingleEncoder/<game>/original.scd)
            // KH1
            if (TryExtractOriginalScdVariant("utils.SingleEncoder.KH1.original.scd", Path.Combine(result.EncoderDirectory, "KH1.original.scd"), out var kh1) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.kh1.original.scd", Path.Combine(result.EncoderDirectory, "KH1.original.scd"), out kh1) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.kh1.original.win32.scd", Path.Combine(result.EncoderDirectory, "KH1.original.scd"), out kh1))
            {
                result.OriginalScdKH1Path = kh1;
                result.HasOriginalScdKH1 = true;
            }
            // KH2
            if (TryExtractOriginalScdVariant("utils.SingleEncoder.KH2.original.scd", Path.Combine(result.EncoderDirectory, "KH2.original.scd"), out var kh2) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.kh2.original.scd", Path.Combine(result.EncoderDirectory, "KH2.original.scd"), out kh2) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.kh2.original.win32.scd", Path.Combine(result.EncoderDirectory, "KH2.original.scd"), out kh2))
            {
                result.OriginalScdKH2Path = kh2;
                result.HasOriginalScdKH2 = true;
            }
            // BBS
            if (TryExtractOriginalScdVariant("utils.SingleEncoder.BBS.original.scd", Path.Combine(result.EncoderDirectory, "BBS.original.scd"), out var bbs) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.bbs.original.scd", Path.Combine(result.EncoderDirectory, "BBS.original.scd"), out bbs))
            {
                result.OriginalScdBBSPath = bbs;
                result.HasOriginalScdBBS = true;
            }
            // ReCOM
            if (TryExtractOriginalScdVariant("utils.SingleEncoder.ReCOM.original.scd", Path.Combine(result.EncoderDirectory, "ReCOM.original.scd"), out var recom) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.recom.original.scd", Path.Combine(result.EncoderDirectory, "ReCOM.original.scd"), out recom))
            {
                result.OriginalScdReCOMPath = recom;
                result.HasOriginalScdReCOM = true;
            }
            // DDD
            if (TryExtractOriginalScdVariant("utils.SingleEncoder.DDD.original.scd", Path.Combine(result.EncoderDirectory, "DDD.original.scd"), out var ddd) ||
                TryExtractOriginalScdVariant("utils.SingleEncoder.ddd.original.scd", Path.Combine(result.EncoderDirectory, "DDD.original.scd"), out ddd))
            {
                result.OriginalScdDDDPath = ddd;
                result.HasOriginalScdDDD = true;
            }

            // Herramientas adicionales
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

            // KHPCPatchManager
            if (ResourceExists("utils.KHPCPatchManager.exe"))
            {
                result.PatchManagerPath = ExtractEmbeddedResourceToPath(
                    "utils.KHPCPatchManager.exe",
                    Path.Combine(result.ToolsDirectory, "KHPCPatchManager.exe"));
                result.HasPatchManager = true;
            }
            else
            {
                Logger.Log("[ERM] KHPCPatchManager not found as embedded resource (utils.KHPCPatchManager.exe)");
            }

            // yt-dlp
            if (ResourceExists("utils.Actually.yt-dlp.exe"))
            {
                result.YtDlpPath = ExtractEmbeddedResourceToPath(
                    "utils.Actually.yt-dlp.exe",
                    Path.Combine(result.ToolsDirectory, "yt-dlp.exe"));
                result.HasYtDlp = true;
            }

            // ffmpeg (for yt-dlp postprocessing and decoding OGG)
            if (ResourceExists("utils.Actually.ffmpeg.exe"))
            {
                result.FfmpegPath = ExtractEmbeddedResourceToPath(
                    "utils.Actually.ffmpeg.exe",
                    Path.Combine(result.ToolsDirectory, "ffmpeg.exe"));
                result.HasFfmpeg = true;
            }

            // vgmstream (CLI + required DLLs)
            bool vgmRes = ResourceExists("utils.Actually.vgmstream.vgmstream-cli.exe");
            Logger.Log($"[ERM] vgmstream-cli resource exists={vgmRes}");
            if (vgmRes)
            {
                result.VgmstreamPath = ExtractEmbeddedResourceToPath(
                    "utils.Actually.vgmstream.vgmstream-cli.exe",
                    Path.Combine(vgmDir, "vgmstream-cli.exe"));
                result.HasVgmstream = true;
            }
            // Optional/runtime DLLs bundled in repo
            TryExtractIfExists("utils.Actually.vgmstream.avcodec-vgmstream-59.dll", Path.Combine(vgmDir, "avcodec-vgmstream-59.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.avformat-vgmstream-59.dll", Path.Combine(vgmDir, "avformat-vgmstream-59.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.avutil-vgmstream-57.dll", Path.Combine(vgmDir, "avutil-vgmstream-57.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.swresample-vgmstream-4.dll", Path.Combine(vgmDir, "swresample-vgmstream-4.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libatrac9.dll", Path.Combine(vgmDir, "libatrac9.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libcelt-0061.dll", Path.Combine(vgmDir, "libcelt-0061.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libcelt-0110.dll", Path.Combine(vgmDir, "libcelt-0110.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libg719_decode.dll", Path.Combine(vgmDir, "libg719_decode.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libmpg123-0.dll", Path.Combine(vgmDir, "libmpg123-0.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libspeex-1.dll", Path.Combine(vgmDir, "libspeex-1.dll"));
            TryExtractIfExists("utils.Actually.vgmstream.libvorbis.dll", Path.Combine(vgmDir, "libvorbis.dll"));

            // Provision .NET 5 runtime for SingleEncoder if missing (hostfxr)
            if (result.HasSingleEncoder)
            {
                try
                {
                    string? dotnetRoot = System.Environment.GetEnvironmentVariable("DOTNET_ROOT");
                    bool hostfxrPresent = false;
                    if (!string.IsNullOrEmpty(dotnetRoot))
                    {
                        hostfxrPresent = File.Exists(Path.Combine(dotnetRoot, @"host", @"fxr", @"5.0.17", @"hostfxr.dll"));
                    }
                    if (!hostfxrPresent)
                    {
                        var runtimeRoot = DotnetRuntimeBootstrap.EnsureDotnetRuntime(result.ToolsDirectory, "5.0.17");
                        System.Environment.SetEnvironmentVariable("DOTNET_ROOT", runtimeRoot);
                        Logger.Log($"DOTNET_ROOT set to '{runtimeRoot}' for SingleEncoder");
                    }
                }
                catch { }
            }

            return result;
        }

        private static void TryExtractIfExists(string resourceName, string destination)
        {
            if (ResourceExists(resourceName))
            {
                try
                {
                    ExtractEmbeddedResourceToPath(resourceName, destination);
                }
                catch (System.Exception ex)
                {
                    Logger.Log($"[ERM] Extract failed for {resourceName}: {ex.Message}");
                }
            }
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

        // Genérico (compatibilidad hacia atrás)
        public bool HasOriginalScd { get; set; }
        public string OriginalScdPath { get; set; } = "";

        // Por juego (opcional)
        public bool HasOriginalScdKH1 { get; set; }
        public string OriginalScdKH1Path { get; set; } = "";
        public bool HasOriginalScdKH2 { get; set; }
        public string OriginalScdKH2Path { get; set; } = "";
        public bool HasOriginalScdBBS { get; set; }
        public string OriginalScdBBSPath { get; set; } = "";
        public bool HasOriginalScdReCOM { get; set; }
        public string OriginalScdReCOMPath { get; set; } = "";
        public bool HasOriginalScdDDD { get; set; }
        public string OriginalScdDDDPath { get; set; } = "";

        public bool HasAdpcmEncode { get; set; }
        public bool HasOggEnc { get; set; }

        public bool HasPatchManager { get; set; }
        public string PatchManagerPath { get; set; } = "";

        // yt-dlp
        public bool HasYtDlp { get; set; }
        public string YtDlpPath { get; set; } = "";

        // ffmpeg
        public bool HasFfmpeg { get; set; }
        public string FfmpegPath { get; set; } = "";

        // vgmstream
        public bool HasVgmstream { get; set; }
        public string VgmstreamPath { get; set; } = "";

        // Setup is complete when we have a valid SCD template (generic or per-game)
        // and at least one encoder tool available (oggenc or adpcmencode3).
        public bool IsCompleteSetup =>
            (HasOriginalScd || HasOriginalScdKH1 || HasOriginalScdKH2 || HasOriginalScdBBS || HasOriginalScdReCOM || HasOriginalScdDDD)
            && (HasOggEnc || HasAdpcmEncode);

        public List<string> GetMissingTools()
        {
            var missing = new List<string>();

            if (!(HasOriginalScd || HasOriginalScdKH1 || HasOriginalScdKH2 || HasOriginalScdBBS || HasOriginalScdReCOM || HasOriginalScdDDD))
                missing.Add("original.scd");
            if (!HasAdpcmEncode)
                missing.Add("adpcmencode3.exe");
            if (!HasOggEnc)
                missing.Add("oggenc.exe");
            if (!HasPatchManager)
                missing.Add("KHPCPatchManager.exe");
            if (!HasYtDlp)
                missing.Add("yt-dlp.exe");
            if (!HasFfmpeg)
                missing.Add("ffmpeg.exe");
            if (!HasVgmstream)
                missing.Add("vgmstream-cli.exe");

            return missing;
        }
    }
}