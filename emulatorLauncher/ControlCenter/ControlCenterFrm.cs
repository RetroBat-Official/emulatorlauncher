using EmulatorLauncher.Common;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Windows.Forms;

namespace EmulatorLauncher.ControlCenter
{
    public partial class ControlCenterFrm : Form
    {
        public static bool IsRunning { get; private set; }

        private bool _tatoo = false;
        private OverlayForm _overlay;

        public ControlCenterFrm()
        {
            IsRunning = true;

            InitializeComponent();

            Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, this.Font.Size, FontStyle.Regular);

            this.BackColor = Color.FromArgb(26, 26, 30);
            label1.BackColor = Color.FromArgb(18, 18, 20);

            this.ShowInTaskbar = true;
            this.Opacity = 0.0;

            label1.Text = Program.CurrentGame?.Name;
            label3.Text = Program.CurrentGame?.Description;
            label3.Font = SystemFonts.MessageBoxFont;

            pictureBox1.ImageLocation = GetMediaPath(Program.CurrentGame.Thumbnail);

            string manualPath = GetMediaPath(Program.CurrentGame.Manual);
            btnManual.Visible = btnManual.Enabled = manualPath != null && PdfExtractor.GetPdfPageCount(manualPath) > 0;

            btnMap.Visible = btnMap.Enabled = GetMediaPath(Program.CurrentGame.Map) != null;
            btnTatoo.Visible = btnTatoo.Enabled = BezelFiles.GetDefaultTatoo() != null;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.Location = new Point(
                (Screen.PrimaryScreen.Bounds.X + Screen.PrimaryScreen.Bounds.Width) / 2 - Width / 2,
                (Screen.PrimaryScreen.Bounds.Y + Screen.PrimaryScreen.Bounds.Height) / 2 - Height / 2);


            ReorganizeButtonLayout();
        }
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            IntPtr HWND_TOPMOST = new IntPtr(-1);
            IntPtr HWND_NOTOPMOST = new IntPtr(-2);

            _overlay = new OverlayForm();
            _overlay.Show();
            _overlay.Activate();
           
            User32.SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE | SWP.SHOWWINDOW);
            User32.SetWindowPos(_overlay.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE | SWP.SHOWWINDOW);
            User32.SetWindowPos(_overlay.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE | SWP.SHOWWINDOW);
            User32.SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE | SWP.SHOWWINDOW | SWP.FRAMECHANGED);            

            Activate();
            StartFadeIn();
        }

        private void ReorganizeButtonLayout()
        {
            var buttons = new System.Windows.Forms.Button[] { btnCancel, btnKill, btnManual, btnMap, btnTatoo };

            btnCancel.TabIndex = 0;

            int top = btnCancel.Top;
            int h = btnCancel.Height;
            int spacing = btnCancel.Top - btnKill.Bottom;

            int idx = buttons.Length -1;
            foreach (var btn in buttons)
            {
                if (!btn.Visible)
                    continue;

                if (btn != btnCancel)
                {
                    btn.TabIndex = idx;
                    idx--;
                }

                btn.Height = h;
                btn.Location = new Point(btn.Left, top);

                top = top - h - spacing;
            }
                         
            label3.Height = top + h - label3.Top;
        }

        private Timer _fadeTimer;
        private const int FadeDurationMs = 150;
        private const int TimerIntervalMs = 15;

        private void StartFadeIn()
        {
            double step = (double)TimerIntervalMs / FadeDurationMs;

            _fadeTimer = new Timer { Interval = TimerIntervalMs };
            _fadeTimer.Tick += (s, e) =>
            {
                if (this.Opacity >= 1.0)
                {
                    this.Opacity = 1.0;
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    return;
                }

                this.Opacity += step;
            };

            _fadeTimer.Start();
        }

        private string GetMediaPath(string item)
        {
            if (string.IsNullOrEmpty(item))
                return item;

            var romPath = Program.SystemConfig.GetFullPath("rom");

            string gamelistPath = romPath;
            while (!File.Exists(Path.Combine(gamelistPath, "gamelist.xml")))
            {
                gamelistPath = Path.GetDirectoryName(gamelistPath);
                if (gamelistPath.Length <= 2)
                {
                    gamelistPath = null;
                    break;
                }
            }

            if (gamelistPath != null)
            {
                string img = item;
                if (img.StartsWith("./"))
                    img = gamelistPath + img.Substring(1);

                if (File.Exists(img))
                    return img;
            }

            return null;
        }        

        protected override void OnClosed(EventArgs e)
        {            
            if (_overlay != null)
            {
                _overlay.Dispose();
                _overlay = null;
            }

            base.OnClosed(e);

            IsRunning = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (_overlay != null)
            {
                _overlay.Dispose();
                _overlay = null;
            }

            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        private IntPtr GetEmulatorHWnd()
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + Process.GetCurrentProcess().Id);
            var moc = searcher.Get();

            foreach (var mo in moc)
            {
                try
                {
                    var processId = Convert.ToInt32(mo["ProcessID"]);
                    var px = Process.GetProcessById(processId);
                    if (px != null)
                        return px.MainWindowHandle;
                }
                catch { }
            }

            return IntPtr.Zero;
        }

        private static void KillChildrenProcesses(int pid, bool root = true)
        {
            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            var moc = searcher.Get();

            foreach (var mo in moc)
                KillChildrenProcesses(Convert.ToInt32(mo["ProcessID"]), false);

            if (root)
                return;

            try
            {
                var proc = Process.GetProcessById(pid);
                if (proc != null)
                    proc.Kill();
            }
            catch { }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            KillChildrenProcesses(Process.GetCurrentProcess().Id);
            Close();
            DialogResult = DialogResult.Abort;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnTatoo_Click(object sender, EventArgs e)
        {
            _tatoo = !_tatoo;

            if (_tatoo)
            {
                btnTatoo.Text = "Box";
                pictureBox1.ImageLocation = BezelFiles.GetDefaultTatoo();
            }
            else
            {
                btnTatoo.Text = "Tatoo";
                pictureBox1.ImageLocation = GetMediaPath(Program.CurrentGame.Thumbnail);
            }
        }

        private void btnManual_Click(object sender, EventArgs e)
        {
            using (var frm = new PdfViewerFrm(GetMediaPath(Program.CurrentGame.Manual)))
                frm.ShowDialog(this);
        }

        private void btnMap_Click(object sender, EventArgs e)
        {
            using (var frm = new PdfViewerFrm(GetMediaPath(Program.CurrentGame.Map)))
                frm.ShowDialog(this);
        }
    }
}
