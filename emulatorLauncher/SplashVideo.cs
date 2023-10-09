using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;

namespace EmulatorLauncher
{

    public partial class SplashVideo : Form
    {
        private static SplashVideo _instance;

        public static void Start(string videoPath, int maxDuration = 10000)
        {
            if (!File.Exists(videoPath))
                return;

            if (_instance != null)
                return;

            var thread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(DoWork));
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start(new SplashVideoInfo() { VideoPath = videoPath, MaxDuration = maxDuration });
        }

        public static void ShutDown()
        {
            try
            {
                if (_instance == null)
                    return;

                _instance.ShutDownInstance();
                _instance = null;
            }
            catch { }
        }

        class SplashVideoInfo
        {
            public string VideoPath { get; set; }
            public int MaxDuration { get; set; }
        }

        static void DoWork(object userdata)
        {
            var info = userdata as SplashVideoInfo;
            if (info == null)
                return;

            new SplashVideo(info).ShowDialog();
        }

        private System.Windows.Forms.Timer _timer;
        private readonly System.Windows.Forms.Integration.ElementHost _elementHost;
        private readonly System.Windows.Controls.MediaElement _mediaElement;
        private readonly SplashVideoInfo _info;

        private SplashVideo(SplashVideoInfo info)
        {
            _info = info;

            this.Opacity = 0;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.BackColor = System.Drawing.Color.Black;

            _mediaElement = new System.Windows.Controls.MediaElement();
            _mediaElement.LoadedBehavior = System.Windows.Controls.MediaState.Play;
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaFailed += OnMediaFailed;
            _mediaElement.MediaEnded += OnMediaEnded;

            _mediaElement.Source = new Uri(info.VideoPath);

            _elementHost = new System.Windows.Forms.Integration.ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _mediaElement,
                BackColor = System.Drawing.Color.Black
            };

            Controls.Add(_elementHost);

            _instance = this;
        }

        public void ShutDownInstance()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(() => { ShutDownInstance(); }));
                return;
            }

            try { Close(); }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            try { _mediaElement.Source = null; }
            catch { }

            _instance = null;
        }

        void _timer_Tick(object sender, EventArgs e)
        {
            Close();
        }

        private int _fadeStart;
        private Timer _fadeTimer;

        private void OnMediaOpened(object sender, System.Windows.RoutedEventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            this.Opacity = 0.0001;

            _fadeStart = Environment.TickCount;

            _fadeTimer = new Timer();
            _fadeTimer.Interval = 1;
            _fadeTimer.Tick += new EventHandler(_fadeTimer_Tick);
            _fadeTimer.Start();

            if (_info.MaxDuration > 0)
            {
                _timer = new Timer();
                _timer.Interval = _info.MaxDuration;
                _timer.Tick += new EventHandler(_timer_Tick);
                _timer.Start();
            }
        }

        const int _fadeDuration = 250;

        void _fadeTimer_Tick(object sender, EventArgs e)
        {
            int fadeTime = Environment.TickCount - _fadeStart;
            if (fadeTime > _fadeDuration)
            {
                this.Opacity = 1;

                _fadeTimer.Dispose();
                _fadeTimer = null;
                return;
            }

            double opacity = (double)fadeTime / (double)_fadeDuration;
            this.Opacity = opacity;
        }
        
        void OnMediaFailed(object sender, System.Windows.ExceptionRoutedEventArgs e)
        {
            Close();
        }

        private void OnMediaEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
