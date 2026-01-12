using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    public partial class ControlCenterFrm : Form
    {
        public ControlCenterFrm()
        {
            InitializeComponent();

            Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, this.Font.Size, FontStyle.Regular);

            this.ShowInTaskbar = false;
            //        this.FormBorderStyle = FormBorderStyle.None;

            label2.Text = Program.CurrentGame?.Name;
            pictureBox1.ImageLocation = GetMediaPath(Program.CurrentGame.Thumbnail);
            controlButton1.Visible = controlButton1.Enabled = GetMediaPath(Program.CurrentGame.Manual) != null;
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

        private JoystickListener _joy;

        private void SetupPad()
        {
            if (Program.Controllers == null || Program.Controllers.Count == 0)
                return;

            PadToKey mapping = new PadToKey();

            string name = "emulatorlauncher";

            try { name = System.Diagnostics.Process.GetCurrentProcess().ProcessName; }
            catch { }

            var app = new PadToKeyApp() { Name = name };
            app.Input.Add(new PadToKeyInput() { Name = InputKey.a, Code = "KEY_SPACE" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.b, Key = "(%{F4})" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.x, Key = "(%{F4})" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.left, Code = "KEY_LEFT" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.right, Code = "KEY_RIGHT" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.down, Code = "KEY_DOWN" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.up, Code = "KEY_UP" });
            mapping.Applications.Add(app);

            _joy = new JoystickListener(Program.Controllers.Where(c => c.Config.DeviceName != "Keyboard").ToArray(), mapping);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (overlay != null)
            {
                overlay.Dispose();
                overlay = null;
            }

            base.OnClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (_joy != null)
            {
                _joy.Dispose();
                _joy = null;
            }

            if (overlay != null)
            {
                overlay.Dispose();
                overlay = null;
            }

            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        public class OverlayForm : Form
        {
            public OverlayForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                Bounds = Screen.PrimaryScreen.Bounds;

                BackColor = Color.Black;
                Opacity = 0.6; // transparence fond
                ShowInTaskbar = false;
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

        OverlayForm overlay;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetupPad();

            User32.SetForegroundWindow(Handle);
            this.Activate();

            User32.ShowWindow(Handle, SW.MINIMIZE);
            User32.ShowWindow(Handle, SW.RESTORE);
            
            this.Location = new Point(
                (Screen.PrimaryScreen.Bounds.X + Screen.PrimaryScreen.Bounds.Width) / 2 - Width / 2,
                (Screen.PrimaryScreen.Bounds.Y + Screen.PrimaryScreen.Bounds.Height) / 2 - Height / 2);

            overlay = new OverlayForm();
            overlay.Show();

        }

        private static void KillChildrenProcesses(int pid, bool root = true)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
                KillChildrenProcesses(Convert.ToInt32(mo["ProcessID"]), false);

            if (root)
                return;

            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
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

        private void controlButton1_Click(object sender, EventArgs e)
        {
            Process.Start(GetMediaPath(Program.CurrentGame.Manual));
        }
    }

    class ControlButton : System.Windows.Forms.Button
    {
        public ControlButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {            
            Rectangle rc = ClientRectangle;
            e.Graphics.FillRectangle(this.Focused ? Brushes.DarkCyan : Brushes.Black, ClientRectangle);
            rc.Width--; rc.Height--;
            e.Graphics.DrawRectangle(this.Focused ? Pens.White : Pens.Gray, rc);

            TextFormatFlags tf = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, tf);      
        }
    }
}
