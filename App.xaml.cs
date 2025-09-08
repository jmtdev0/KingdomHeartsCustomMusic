using System.Windows;
using KingdomHeartsMusicPatcher.utils;
using System.Runtime.InteropServices;

namespace KingdomHeartsMusicPatcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set a stable AUMID to improve taskbar grouping/icon resolution
            try
            {
                var hr = SetCurrentProcessExplicitAppUserModelID("KingdomHeartsMusicPatcher");
                try { Logger.Log($"[Icon] SetCurrentProcessExplicitAppUserModelID hr=0x{hr:X8}"); } catch { }
            }
            catch (System.Exception ex)
            {
                try { Logger.LogException("[Icon] SetCurrentProcessExplicitAppUserModelID failed", ex); } catch { }
            }

            base.OnStartup(e);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try { EmbeddedResourceManager.CleanupTempFiles(); } catch { }
        }
    }
}
