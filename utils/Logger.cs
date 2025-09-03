using System;
using System.IO;
using System.Text;
using System.Threading;

namespace KingdomHeartsCustomMusic.utils
{
    public static class Logger
    {
        private static readonly object _lock = new();
        private static string _logFilePath = string.Empty;
        private static bool _initialized;

        public static string LogFilePath => _logFilePath;

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var rootDir = Path.Combine(baseDir, "Generated Patches");
                Directory.CreateDirectory(rootDir);
                var logsDir = Path.Combine(rootDir, "logs");
                Directory.CreateDirectory(logsDir);
                var fileName = $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log";
                _logFilePath = Path.Combine(logsDir, fileName);
                _initialized = true;
                // Log the application version at startup
                try
                {
                    var version = KingdomHeartsCustomMusic.AppInfo.GetVersion();
                    Log($"Logger initialized. Log file: {_logFilePath}");
                    Log($"Application version: {version}");
                }
                catch {
                    Log($"Logger initialized. Log file: {_logFilePath}");
                }
            }
            catch
            {
                // ignore logging setup failures
            }
        }

        public static void Log(string message)
        {
            try
            {
                if (!_initialized) Initialize();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [T{Thread.CurrentThread.ManagedThreadId}] {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // swallow logging errors
            }
        }

        public static void LogException(string prefix, Exception ex)
        {
            Log($"{prefix}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
