using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EmulatorLauncher
{
    /// <summary>
    /// Logique d'interaction pour PdfViewserControl.xaml
    /// </summary>
    public partial class PdfViewerControl : UserControl
    {
        private string[] _pages;
        private int _index;
        private bool _animating;

        public PdfViewerControl()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                UpdateMargins();
                Focus();
            };
            SizeChanged += (s, e) => UpdateMargins();
            KeyDown += OnKeyDown;
        }

        public event Action EscapePressed;

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                EscapePressed?.Invoke();
                e.Handled = true;
            }
        }

        private void UpdateMargins()
        {
            double m = Math.Min(ActualWidth, ActualHeight) * 0.04;

            var thickness = new Thickness(m);

            BorderCurrent.Margin = thickness;
            BorderNext.Margin = thickness;
        }

        public bool LoadImage(string image)
        {
            if (!File.Exists(image))
                return false;

            _pages = new string[] { image };
            _index = 0;

            ImageCurrent.Source = new BitmapImage(new Uri(image, UriKind.Absolute));
            return true;
        }

        public bool LoadFolder(string ppmFolder)
        {
            _pages = Directory.GetFiles(ppmFolder, "*.ppm");
            if (_pages.Length == 0)
                return false;

            Array.Sort(_pages);
            _index = 0;

            ImageCurrent.Source = LoadPpm(_pages[0]);
            return true;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true; // évite propagation (scroll parent)

            if (e.Delta < 0)
                Show(_index + 1, true);
            else if (e.Delta > 0)
                Show(_index - 1, false);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_animating) return;

            if (e.Key == Key.Right) Show(_index + 1, true);
            if (e.Key == Key.Left) Show(_index - 1, false);
        }

        private void Show(int newIndex, bool left)
        {
            if (newIndex < 0 || newIndex >= _pages.Length) return;

            _animating = true;
            double w = ActualWidth;
            int dir = left ? -1 : 1;

            ImageNext.Source = LoadPpm(_pages[newIndex]);
            BorderNext.Visibility = System.Windows.Visibility.Visible;

            var tCur = (TranslateTransform)BorderCurrent.RenderTransform;
            var tNext = (TranslateTransform)BorderNext.RenderTransform;

            tNext.X = -dir * w;
            
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var outAnim = new DoubleAnimation
            {
                From = 0,
                To = dir * w,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = ease
            };

            var inAnim = new DoubleAnimation
            {
                From = -dir * w,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = ease
            };

            outAnim.Completed += (s, e) =>
            {
                // IMPORTANT : arrêter les animations
                tCur.BeginAnimation(TranslateTransform.XProperty, null);
                tNext.BeginAnimation(TranslateTransform.XProperty, null);

                // Reset positions
                tCur.X = 0;
                tNext.X = 0;

                // Swap images
                ImageCurrent.Source = ImageNext.Source;

                ImageNext.Source = null;
                BorderNext.Visibility = System.Windows.Visibility.Hidden;

                _index = newIndex;
                _animating = false;
            };

            tCur.BeginAnimation(TranslateTransform.XProperty, outAnim);
            tNext.BeginAnimation(TranslateTransform.XProperty, inAnim);
        }

        private BitmapSource LoadPpm(string path)
        {
            using (var bmp = PpmImageLoader.LoadPpm(path))
                return ToBitmapSource(bmp);
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        static BitmapSource ToBitmapSource(Bitmap bmp)
        {
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            try
            {
                var wb = new WriteableBitmap(
                    bmp.Width,
                    bmp.Height,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgr24,
                    null);

                wb.WritePixels(
                    new System.Windows.Int32Rect(0, 0, bmp.Width, bmp.Height),
                    data.Scan0,
                    data.Stride * data.Height,
                    data.Stride);

                wb.Freeze(); // CRUCIAL
                return wb;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }
}
