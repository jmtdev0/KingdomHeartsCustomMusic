using System.Windows;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Linq;

namespace KingdomHeartsCustomMusic
{
    public partial class InstructionsWindow : Window
    {
        public InstructionsWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDownCloseOnEsc;
            try
            {
                var full = AppInfo.GetVersion() ?? string.Empty;
                var m = Regex.Match(full, @"\d+\.\d+\.\d+");
                string shortVer;
                if (m.Success)
                {
                    shortVer = m.Value;
                }
                else
                {
                    var parts = full.Split('.');
                    if (parts.Length >= 3)
                        shortVer = string.Join('.', parts.Take(3));
                    else
                        shortVer = full;
                }

                VersionSubtitle.Text = $"Version: {shortVer}";
            }
            catch { }
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