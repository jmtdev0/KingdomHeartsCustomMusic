using System.Configuration;
using System.Data;
using System.Windows;
using KingdomHeartsCustomMusic.utils;

namespace KingdomHeartsCustomMusic
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            // Limpiar archivos temporales al cerrar la aplicación
            EmbeddedResourceManager.CleanupTempFiles();
            base.OnExit(e);
        }
    }
}
