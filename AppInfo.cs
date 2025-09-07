using System.Reflection;

namespace KingdomHeartsMusicPatcher
{
    public static class AppInfo
    {
        public static string GetVersion()
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            try
            {
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(info)) return info;
            }
            catch { }

            try
            {
                var fileVer = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
                if (!string.IsNullOrWhiteSpace(fileVer)) return fileVer;
            }
            catch { }

            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }
    }
}
