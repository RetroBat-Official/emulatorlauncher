using EmulatorLauncher.Common;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmulatorLauncher.ControlCenter
{
    class OverlayForm : Form
    {
        public bool ExclusiveFullScreen { get; private set; }

        private bool _shadeBackground = true;
        private Image _background;

        public OverlayForm(bool exclusiveFullScreen = false)
        {
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;

            BackColor = Color.Black;            
            ShowInTaskbar = false;
            DoubleBuffered = true;

            ExclusiveFullScreen = exclusiveFullScreen;

            var bounds = Screen.PrimaryScreen.Bounds;
            _background = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(_background))
                g.CopyFromScreen(System.Drawing.Point.Empty, System.Drawing.Point.Empty, bounds.Size);

            Opacity = 1;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!ExclusiveFullScreen)
            {
                Opacity = 0.7;
                
                if (_background != null)
                {
                    _background.Dispose();
                    _background = null;
                }

                Refresh();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_background != null)
            {
                e.Graphics.DrawImageUnscaled(_background, Point.Empty);

                if (_shadeBackground)
                {
                    using (var sb = new SolidBrush(Color.FromArgb(178, 0, 0, 0)))
                        e.Graphics.FillRectangle(sb, ClientRectangle);
                }
            }
            else
                e.Graphics.Clear(BackColor);
        }

        protected override CreateParams CreateParams
        {
            get
            {                
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;

                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

                return cp;
            }
        }

        const int WM_ERASEBKGND = 0x0014;
        const int WM_MOUSEACTIVATE = 0x0021;
        const int MA_NOACTIVATE = 3;
        
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }
             
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }

            base.WndProc(ref m);
        }
    }

}
