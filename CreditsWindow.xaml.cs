using System.Windows;
using System.Windows.Input;

namespace KingdomHeartsCustomMusic
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
    }
}