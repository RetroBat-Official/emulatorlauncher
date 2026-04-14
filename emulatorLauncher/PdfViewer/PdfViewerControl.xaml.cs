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
        private string _folder;
        private int _totalPages;
        private int _availablePages;
        private double _zoom = 1.0;
        private double _zoomStep = 0.25;
        private const double ZoomMin = 1.0;
        private const double ZoomMax = 4.0;
        private const double PanStep = 50.0;
        private double _fitRatio = 1.0;

        public PdfViewerControl()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                UpdateMargins();
                Focus();
                ComputeZoomStep(ImageCurrent.Source as BitmapSource);
                ApplyZoom(1.0);
            };
            SizeChanged += (s, e) =>
            {
                UpdateMargins();
                ComputeZoomStep(ImageCurrent.Source as BitmapSource);
                ApplyZoom(_zoom);
            };
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

            var source = new BitmapImage(new Uri(image, UriKind.Absolute));
            ImageCurrent.Source = new BitmapImage(new Uri(image, UriKind.Absolute));
            return true;
        }

        public bool LoadFolder(string ppmFolder)
        {
            _folder = ppmFolder;
            _pages = Directory.GetFiles(ppmFolder, "*.ppm");
            if (_pages.Length == 0)
                return false;

            Array.Sort(_pages);
            _index = 0;
            _availablePages = _pages.Length;

            var source = LoadPpm(_pages[0]);
            ImageCurrent.Source = LoadPpm(_pages[0]);
            return true;
        }

        public void NotifyPagesAvailable(int upToPage, int total)
        {
            _totalPages = total;

            if (!string.IsNullOrEmpty(_folder))
            {
                _pages = Directory.GetFiles(_folder, "*.ppm");
                Array.Sort(_pages);
                _availablePages = _pages.Length;
            }

            UpdatePageIndicator();
        }

        private void UpdatePageIndicator()
        {
            bool stillLoading = _availablePages < _totalPages;
            // _zoom = 1.0 means fitted, show as 100%
            string zoomStr = _zoom > 1.0 ? $"  {(int)(_zoom * 100)}%" : "";
            PageLabel.Text = $"{_index + 1} / {(_totalPages > 0 ? _totalPages.ToString() : "?")}"
                           + zoomStr
                           + (stillLoading ? "  …" : "");
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

            if (e.Key == Key.PageUp) { ApplyZoom(_zoom + _zoomStep); return; }
            if (e.Key == Key.PageDown) { ApplyZoom(_zoom - _zoomStep); return; }

            if (_zoom > 1.0)
            {
                // When zoomed in, arrow keys pan — but flip page if at the edge
                if (e.Key == Key.Right)
                {
                    if (Scroller.HorizontalOffset >= Scroller.ScrollableWidth)
                        Show(_index + 1, true);
                    else
                        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + PanStep);
                }
                else if (e.Key == Key.Left)
                {
                    if (Scroller.HorizontalOffset <= 0)
                        Show(_index - 1, false);
                    else
                        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - PanStep);
                }
                else if (e.Key == Key.Down)
                    Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + PanStep);
                else if (e.Key == Key.Up)
                    Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset - PanStep);
            }
            else
            {
                // When not zoomed, arrow keys flip pages
                if (e.Key == Key.Right) Show(_index + 1, true);
                if (e.Key == Key.Left) Show(_index - 1, false);
            }
        }

        private void Show(int newIndex, bool left)
        {
            if (newIndex < 0 || newIndex >= _pages.Length) return;
            if (!File.Exists(_pages[newIndex])) return;

            _zoom = 1.0;
            ApplyZoom(_zoom);
            Scroller.ScrollToHorizontalOffset(0);
            Scroller.ScrollToVerticalOffset(0);

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
                ComputeZoomStep(ImageCurrent.Source as BitmapSource);

                ImageNext.Source = null;
                BorderNext.Visibility = System.Windows.Visibility.Hidden;

                _index = newIndex;
                _animating = false;
                UpdatePageIndicator();
            };

            tCur.BeginAnimation(TranslateTransform.XProperty, outAnim);
            tNext.BeginAnimation(TranslateTransform.XProperty, inAnim);
        }

        private void ComputeZoomStep(BitmapSource image)
        {
            if (image == null || ActualWidth == 0 || ActualHeight == 0) return;

            double fitX = ActualWidth / image.PixelWidth;
            double fitY = ActualHeight / image.PixelHeight;
            _fitRatio = Math.Min(fitX, fitY);

            // One step = 10% of the fitted display size
            _zoomStep = _fitRatio * 0.2;
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

        private void ApplyZoom(double newZoom)
        {
            _zoom = Math.Max(ZoomMin, Math.Min(ZoomMax, newZoom));

            // Apply scaling relative to fit ratio so zoom 1.0 = fitted size
            double scale = _fitRatio * _zoom;
            var transform = new ScaleTransform(scale, scale);
            ImageCurrent.LayoutTransform = transform;
            ImageNext.LayoutTransform = transform;

            if (_zoom <= 1.0)
            {
                Scroller.ScrollToHorizontalOffset(0);
                Scroller.ScrollToVerticalOffset(0);
                ScrollViewer.SetHorizontalScrollBarVisibility(Scroller, ScrollBarVisibility.Disabled);
                ScrollViewer.SetVerticalScrollBarVisibility(Scroller, ScrollBarVisibility.Disabled);
            }
            else
            {
                ScrollViewer.SetHorizontalScrollBarVisibility(Scroller, ScrollBarVisibility.Auto);
                ScrollViewer.SetVerticalScrollBarVisibility(Scroller, ScrollBarVisibility.Auto);
            }

            UpdatePageIndicator();
        }
    }
}
