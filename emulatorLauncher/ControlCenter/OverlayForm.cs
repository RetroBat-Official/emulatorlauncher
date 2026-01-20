using System;
using System.Drawing;
using System.Windows.Forms;

namespace EmulatorLauncher.ControlCenter
{
    class OverlayForm : Form
    {
        public OverlayForm()
        {          
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;

            BackColor = Color.Black;
            Opacity = 0.7;
            ShowInTaskbar = false;
        }

        public void SetBackgroundImage(Image image)
        {
            _background = image;
            this.Opacity = 1;
            Refresh();
        }
       
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_background != null)
            {
                e.Graphics.DrawImageUnscaled(_background, Point.Empty);

                using (var sb = new SolidBrush(Color.FromArgb(178, 0, 0, 0)))
                    e.Graphics.FillRectangle(sb, ClientRectangle);
            }
            else
                base.OnPaintBackground(e);
        }
     
        private Image _background;

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

        const int WM_MOUSEACTIVATE = 0x0021;
        const int MA_NOACTIVATE = 3;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }

            base.WndProc(ref m);
        }
    }

}
