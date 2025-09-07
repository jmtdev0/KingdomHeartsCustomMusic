using System.Windows;
using KingdomHeartsMusicPatcher.utils;

namespace KingdomHeartsMusicPatcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try { EmbeddedResourceManager.CleanupTempFiles(); } catch { }
        }
    }
}
