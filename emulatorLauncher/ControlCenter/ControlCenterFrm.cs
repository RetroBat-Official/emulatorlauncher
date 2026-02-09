using EmulatorLauncher.Common;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace EmulatorLauncher.ControlCenter
{
    partial class ControlCenterFrm : Form
    {
        // List emulators that don't pause when the focus is lost, and can be suspended with NtSuspendProcess
        static string[] emulatorsToSuspend = new string[] { "mame64", "m2emulator", "hypseus" };

        private OverlayForm _overlay;

        private bool _tatoo = false;       
        private IntPtr _emulatorHwnd;
        private bool _isEmulatorSuspended;
        private System.Drawing.Text.PrivateFontCollection _pfc;

        public ControlCenterFrm()
        {
            InitializeComponent();

            var fontTtf = Path.Combine(Program.LocalPath, "resources", "opensans_hebrew_condensed_regular.ttf");
            if (File.Exists(fontTtf))
            {
                _pfc = new System.Drawing.Text.PrivateFontCollection();
                _pfc.AddFontFile(fontTtf);
                Font = new Font(_pfc.Families[0], this.Font.Size, FontStyle.Regular);
            }
            else
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, this.Font.Size, FontStyle.Regular);

            btnCancel.Text = Properties.Resources.BackToGame;
            btnKill.Text = Properties.Resources.KillEmulator;
            btnManual.Text = Properties.Resources.Manual;
            btnTatoo.Text = Properties.Resources.Tatoo;
            btnMap.Text = Properties.Resources.Map;

            this.Opacity = 0.0;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            label1.Text = Program.CurrentGame?.Name;
            label3.Text = Program.CurrentGame?.Description;
            label3.Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, label3.Font.Size, FontStyle.Regular);

            pictureBox1.ImageLocation = GetMediaPath(Program.CurrentGame?.Thumbnail);

            btnMap.Visible = btnMap.Enabled = GetMediaPath(Program.CurrentGame?.Map) != null;
            btnTatoo.Visible = btnTatoo.Enabled = BezelFiles.GetDefaultTatoo() != null;
            
            string manualPath = GetMediaPath(Program.CurrentGame?.Manual);
            btnManual.Visible = btnManual.Enabled = manualPath != null && PdfExtractor.GetPdfPageCount(manualPath) > 0;

            label1.MouseDown += OnTitleMouseDown;

            _emulatorHwnd = GetEmulatorHWnd();
            _overlay = new OverlayForm(User32.IsExclusiveFullScreen(_emulatorHwnd));
            SuspendEmulator(true);
        }

        private bool SuspendEmulator(bool suspend)
        {
            if (!emulatorsToSuspend.Contains(Program.SystemConfig["emulator"]))
                return false;

            if (_isEmulatorSuspended == suspend)
                return false;

            _isEmulatorSuspended = suspend;

            User32.GetWindowThreadProcessId(_emulatorHwnd, out uint dwProcessId);
            if (dwProcessId != 0)
            {                
                IntPtr processHandle = Kernel32.OpenProcess(ProcessAccessFlags.All, false, dwProcessId);
                if (processHandle != IntPtr.Zero)
                {                    
                    if (suspend)
                        Kernel32.NtSuspendProcess(processHandle);
                    else
                        Kernel32.NtResumeProcess(processHandle);

                    Kernel32.CloseHandle(processHandle);
                    return true;
                }
            }

            return false;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _overlay.Show();

            if (_overlay.ExclusiveFullScreen)
            {
                IntPtr HWND_TOPMOST = new IntPtr(-1);
                User32.SetWindowPos(_overlay.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE);
                _overlay.Refresh();          // force le repaint
                Application.DoEvents();      // laisse Windows afficher la fenêtre
            }

            this.Location = new Point(
                (Screen.PrimaryScreen.Bounds.X + Screen.PrimaryScreen.Bounds.Width) / 2 - Width / 2,
                (Screen.PrimaryScreen.Bounds.Y + Screen.PrimaryScreen.Bounds.Height) / 2 - Height / 2);

            ReorganizeButtonLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            User32.SetWindowLong(Handle, GWL.HWNDPARENT, _overlay.Handle);

            IntPtr HWND_TOPMOST = new IntPtr(-1);
            User32.SetWindowPos(_overlay.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE);
            User32.SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP.NOSIZE | SWP.NOMOVE);

            User32.ForceForegroundWindow(Handle);
            StartFadeIn();
            
            User32.ForceForegroundWindow(Handle);
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

            // pictureBox1.Height = top + h - pictureBox1.Top;
            label3.Height = top + h - label3.Top;
        }

        private System.Windows.Forms.Timer _fadeTimer;
        private const int FadeDurationMs = 150;
        private const int TimerIntervalMs = 15;

        private void StartFadeIn()
        {
            double step = (double)TimerIntervalMs / FadeDurationMs;

            _fadeTimer = new System.Windows.Forms.Timer { Interval = TimerIntervalMs };
            _fadeTimer.Tick += (s, e) =>
            {
                if (this.Opacity >= 1.0)
                {
                    this.Opacity = 1.0;
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    SynchronizationContext.Current?.Post((state) => { User32.ForceForegroundWindow((IntPtr) state); }, Handle);
                    return;
                }

                this.Opacity += step;
            };

            _fadeTimer.Start();
        }

        private void OnTitleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                User32.ReleaseCapture();

                const int WM_NCLBUTTONDOWN = 0xA1;
                const int HTCAPTION = 0x2;

                User32.SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (SuspendEmulator(false))
                Thread.Sleep(350);

            if (_emulatorHwnd != IntPtr.Zero)
            {
                if (User32.GetWindowStyle(_emulatorHwnd).HasFlag(WS.MINIMIZE))
                    User32.ShowWindowAsync(_emulatorHwnd, SW.RESTORE);

                Thread.Sleep(50);
                User32.ForceForegroundWindow(_emulatorHwnd);
            }

            if (_overlay != null)
            {
                _overlay.Dispose();
                _overlay = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_overlay != null)
            {
                _overlay.Dispose();
                _overlay = null;
            }

            if (_pfc != null)
            {
                _pfc.Dispose();
                _pfc = null;
            }

            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);

            SynchronizationContext.Current?.Post((state) => 
            {
                var emulatorHwnd = (IntPtr)state;
                if (emulatorHwnd != IntPtr.Zero && User32.IsWindow(emulatorHwnd))
                {
                    if (User32.GetWindowStyle(emulatorHwnd).HasFlag(WS.MINIMIZE))
                    {
                        User32.ShowWindowAsync(emulatorHwnd, SW.RESTORE);
                        Thread.Sleep(50);
                        User32.ForceForegroundWindow(emulatorHwnd);
                    }
                }
            }, _emulatorHwnd);
        }

        private IntPtr GetEmulatorHWnd()
        {
            var hWnds = new List<IntPtr>();

            foreach (var processId in Process.GetCurrentProcess().GetChildrenProcessIds())
            {
                try
                {
                    bool found = false;

                    try
                    {
                        var px = Process.GetProcessById(processId);

                        if (px != null && px.ProcessName != null)
                        {
                            // Exclude TeknoParrotUi, which is an intermediate process
                            if (px.ProcessName.Equals("TeknoParrotUi", StringComparison.InvariantCultureIgnoreCase))
                                continue;
                        }

                        if (px != null && User32.IsWindow(px.MainWindowHandle) && User32.IsWindowVisible(px.MainWindowHandle))
                        {
                            found = true;
                            hWnds.Add(px.MainWindowHandle);
                        }
                    }
                    catch { }

                    if (!found)
                        hWnds.AddRange(User32.FindProcessWnds(processId).Where(hWnd => !User32.GetWindowStyle(hWnd).HasFlag(WS.CHILD)));
                }
                catch { }
            }

            hWnds = hWnds.Distinct().ToList();
            if (hWnds.Any())
                return hWnds.FirstOrDefault();        

            return IntPtr.Zero;
        }

        private static void KillChildrenProcesses(int pid, bool root = true)
        {
            IEnumerable<int> processIds = ProcessExtensions.GetChildrenProcessIds(pid);
            if (!root)
                processIds = processIds.Union(new int[] { pid });

            foreach (var processId in processIds.Reverse())
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    if (proc != null)
                        proc.Kill();
                }
                catch { }
            }
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

        private void button1_Click(object sender, EventArgs e)
        {
            SuspendEmulator(false);

            foreach (var pxHandle in Job.ChildProcesses)
            {
                var pxid = Kernel32.GetProcessId(pxHandle);
                if (pxid != 0)
                    KillChildrenProcesses(pxid, false);
            }

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
