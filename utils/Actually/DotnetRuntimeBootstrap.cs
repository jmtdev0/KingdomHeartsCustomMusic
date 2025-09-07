using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace KingdomHeartsMusicPatcher.utils
{
    internal static class DotnetRuntimeBootstrap
    {
        // Known stable CDN pattern for dotnet runtime ZIPs
        // Example: https://dotnetcli.azureedge.net/dotnet/Runtime/5.0.17/dotnet-runtime-5.0.17-win-x64.zip
        public static string EnsureDotnetRuntime(string baseToolsDir, string version = "5.0.17")
        {
            try
            {
                string runtimeRoot = Path.Combine(baseToolsDir, "dotnet-runtime", version);
                string marker = Path.Combine(runtimeRoot, ".provisioned");
                if (File.Exists(marker))
                    return runtimeRoot;

                Directory.CreateDirectory(runtimeRoot);

                string zipName = $"dotnet-runtime-{version}-win-x64.zip";
                string url = $"https://dotnetcli.azureedge.net/dotnet/Runtime/{version}/{zipName}";
                string zipPath = Path.Combine(runtimeRoot, zipName);

                using (var http = new HttpClient())
                using (var resp = http.GetAsync(url).GetAwaiter().GetResult())
                {
                    resp.EnsureSuccessStatusCode();
                    using (var fs = File.Create(zipPath))
                    {
                        resp.Content.CopyToAsync(fs).GetAwaiter().GetResult();
                    }
                }

                ZipFile.ExtractToDirectory(zipPath, runtimeRoot, overwriteFiles: true);
                try { File.Delete(zipPath); } catch { }

                File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
                Logger.Log($".NET runtime {version} provisioned at '{runtimeRoot}'");
                return runtimeRoot;
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to provision .NET runtime for SingleEncoder", ex);
                throw;
            }
        }
    }
}
