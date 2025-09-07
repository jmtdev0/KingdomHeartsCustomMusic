using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace KingdomHeartsMusicPatcher
{
    public partial class CreditsWindow : Window
    {
        public CreditsWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDownCloseOnEsc;
        }

        private void OnPreviewKeyDownCloseOnEsc(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true };
                Process.Start(psi);
            }
            catch { }
            e.Handled = true;
        }
    }
}