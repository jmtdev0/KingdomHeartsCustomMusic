using System;
using System.IO;

namespace KingdomHeartsCustomMusic.utils
{
    public static class PatchLogger
    {
        private static string? _logFilePath;
        private static readonly object _lockObject = new object();
        private static bool _isInitialized = false;

        public static void InitializeLog(string operation)
        {
            lock (_lockObject)
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KingdomHeartsCustomMusic", "Logs");
                Directory.CreateDirectory(logDir);
                
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(logDir, $"PatchApplication_{operation}_{timestamp}.log");
                _isInitialized = true;
                
                WriteToLog($"=== Kingdom Hearts Custom Music - Patch Application Log ===");
                WriteToLog($"Operation: {operation}");
                WriteToLog($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                WriteToLog($"Application Version: 1.0.0");
                WriteToLog($"Operating System: {Environment.OSVersion}");
                WriteToLog($"Machine Name: {Environment.MachineName}");
                WriteToLog($"User: {Environment.UserName}");
                WriteToLog($"Working Directory: {Environment.CurrentDirectory}");
                WriteToLog($"=======================================================");
                WriteToLog("");
            }
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                InitializeLog("AutoInitialized");
            }
        }

        public static void Log(string message)
        {
            EnsureInitialized();
            WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void LogError(string message, Exception? ex = null)
        {
            EnsureInitialized();
            WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}");
            if (ex != null)
            {
                WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] Exception Type: {ex.GetType().Name}");
                WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] Exception Message: {ex.Message}");
                WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] Stack Trace: {ex.StackTrace}");
            }
        }

        public static void LogStep(string step, string details = "")
        {
            EnsureInitialized();
            WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] STEP: {step}");
            if (!string.IsNullOrEmpty(details))
            {
                WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}]   ?? {details}");
            }
        }

        public static void LogFileInfo(string filePath, string description = "")
        {
            EnsureInitialized();
            try
            {
                if (File.Exists(filePath))
                {
                    var info = new FileInfo(filePath);
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] FILE: {description} {filePath}");
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}]   ?? Size: {info.Length:N0} bytes ({info.Length / (1024.0 * 1024.0):F2} MB)");
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}]   ?? Modified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}]   ?? Read-only: {info.IsReadOnly}");
                }
                else
                {
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] FILE: {description} {filePath} (NOT FOUND)");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] FILE ERROR: Could not get info for {filePath}: {ex.Message}");
            }
        }

        public static void LogDirectoryInfo(string dirPath, string description = "")
        {
            EnsureInitialized();
            try
            {
                if (Directory.Exists(dirPath))
                {
                    var info = new DirectoryInfo(dirPath);
                    var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] DIR: {description} {dirPath}");
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}]   ?? Files: {files.Length}");
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}]   ?? Created: {info.CreationTime:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] DIR: {description} {dirPath} (NOT FOUND)");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"[{DateTime.Now:HH:mm:ss.fff}] DIR ERROR: Could not get info for {dirPath}: {ex.Message}");
            }
        }

        public static string? GetLogFilePath()
        {
            return _logFilePath;
        }

        public static void FinalizeLog(bool success)
        {
            if (!_isInitialized) return;
            
            WriteToLog("");
            WriteToLog("=======================================================");
            WriteToLog($"Operation completed: {(success ? "SUCCESS" : "FAILURE")}");
            WriteToLog($"End time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            WriteToLog($"Log file location: {_logFilePath}");
            WriteToLog("=======================================================");
        }

        private static void WriteToLog(string message)
        {
            if (_logFilePath == null) return;

            lock (_lockObject)
            {
                try
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
                catch
                {
                    // Ignore logging errors - we don't want logging to break the application
                }
            }
        }
    }
}