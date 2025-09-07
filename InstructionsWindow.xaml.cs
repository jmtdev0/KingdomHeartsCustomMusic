using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace KingdomHeartsMusicPatcher
{
    public partial class InstructionsWindow : Window
    {
        public InstructionsWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDownCloseOnEsc;
            try
            {
                var raw = AppInfo.GetVersion() ?? string.Empty;
                string versionOnly = ExtractSimpleVersion(raw);
                VersionRun.Text = string.IsNullOrWhiteSpace(versionOnly) ? "unknown" : versionOnly;
            }
            catch { }
        }

        private static string ExtractSimpleVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var m = Regex.Match(raw, "\\d+(?:\\.\\d+){1,3}");
            if (!m.Success) return raw;
            var parts = m.Value.Split('.');
            if (parts.Length >= 3)
                return $"{parts[0]}.{parts[1]}.{parts[2]}";
            return m.Value;
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