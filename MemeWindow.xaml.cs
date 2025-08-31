using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;

namespace KingdomHeartsCustomMusic
{
    public partial class MemeWindow : Window
    {
        private readonly string _imagePath;
        public MemeWindow(string imagePath)
        {
            InitializeComponent();
            _imagePath = imagePath;
            LoadImage();

            // Close on ESC for consistency
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };

            // Improve resizing quality
            SizeChanged += (_, __) =>
            {
                RenderOptions.SetBitmapScalingMode(MemeImage, BitmapScalingMode.HighQuality);
            };
        }

        private void LoadImage()
        {
            if (!File.Exists(_imagePath)) return;

            var ext = Path.GetExtension(_imagePath)?.ToLowerInvariant();
            if (ext == ".gif")
            {
                // Use WpfAnimatedGif for animated gifs
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new System.Uri(_imagePath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                ImageBehavior.SetAnimatedSource(MemeImage, null); // reset any previous
                MemeImage.Source = null; // ensure static image isn't used
                ImageBehavior.SetAutoStart(MemeImage, true);
                ImageBehavior.SetRepeatBehavior(MemeImage, RepeatBehavior.Forever);
                ImageBehavior.SetAnimatedSource(MemeImage, bmp);
            }
            else
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new System.Uri(_imagePath);
                bmp.CacheOption = BitmapCacheOption.OnLoad; // allow file to be released
                bmp.EndInit();
                MemeImage.Source = bmp;
            }
        }
    }
}
