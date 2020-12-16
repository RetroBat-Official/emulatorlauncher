using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace VPinballLauncher
{
    public partial class LoadingForm : Form
    {
        public LoadingForm()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            InitializeComponent();

            this.Size = new System.Drawing.Size(1024 + 20, 768 + 20);
            this.Font = SystemFonts.MessageBoxFont;

            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ShowInTaskbar = false;            
            Text = "Chargement en cours...";
            BackColor = Color.Black;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
        }

        private Timer t = new Timer();

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            t.Interval = 10;
            t.Tick += new EventHandler(t_Tick);
            t.Start();
        }

        bool _watchVPinMame = true;

        void t_Tick(object sender, EventArgs e)
        {
            IntPtr hPlayer = FindWindow("VPPlayer", null);

            if (hPlayer != IntPtr.Zero && this.TopMost)
            {
                TopMost = false;
                return;
            }

            if (hPlayer != IntPtr.Zero && IsWindowVisible(hPlayer))
            {
                t.Dispose();
                t = null;

                Close();
                return;
            }

            if (_watchVPinMame)
            {
                hPlayer = FindWindow("#32770", "Please answer and press OK...");
                if (hPlayer != IntPtr.Zero)
                {
                    _watchVPinMame = false;

                    TopMost = false;

                    SetActiveWindow(hPlayer);
                    SetForegroundWindow(hPlayer);
                    SetActiveWindow(hPlayer);
                }
            }
        }

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string className, int nothing);

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "LoadingForm";
            this.ResumeLayout(false);
        }

        public Image Image { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Image != null)
            {
                Size sz = new System.Drawing.Size(1024, 768);

                Rectangle zz = ClientRectangle;
                Rectangle rc = new Rectangle(zz.Width / 2 - sz.Width / 2, zz.Height / 2 - sz.Height / 2, sz.Width, sz.Height);
                e.Graphics.DrawImage(Image, rc);
            }

            if (!string.IsNullOrEmpty(WarningText))
            {
                Rectangle rc = ClientRectangle;

                if (Image != null)
                {
                    int h = rc.Height / 3;
                    rc.Y += h + h;
                    rc.Height = h;
                }

                TextRenderer.DrawText(e.Graphics, WarningText, this.Font, rc, Color.White, Color.Transparent, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string WarningText { get; set; }
    }
}
