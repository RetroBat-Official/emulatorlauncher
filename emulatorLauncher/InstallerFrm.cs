using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class InstallerFrm : Form
    {
        private JoystickListener _joy;
        private Installer _installer;

        public InstallerFrm(Installer installer)
        {
            InitializeComponent();

            _installer = installer;

            if (string.IsNullOrEmpty(_installer.ServerVersion))
                label1.Text = installer.FolderName + " is not installed.\r\nInstall now ?";
            else
                label1.Text = "An update is available for " + installer.FolderName + " :\r\nUpdate version : " + installer.ServerVersion + ".\r\nInstalled version : " + installer.GetInstalledVersion() + ".\r\nInstall now ?";

            this.Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, this.Font.Size, FontStyle.Regular);

            tableLayoutPanel2.RowStyles[2].SizeType = SizeType.Absolute;
            tableLayoutPanel2.RowStyles[2].Height = 0;

            button1.GotFocus += button1_GotFocus;
            button2.GotFocus += button2_GotFocus;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetupPad();
        }

        private void SetupPad()
        {
            PadToKey mapping = new PadToKey();

            string name = "emulatorlauncher";

            try { name = System.Diagnostics.Process.GetCurrentProcess().ProcessName; }
            catch { }

            var app = new PadToKeyApp() { Name = name };
            app.Input.Add(new PadToKeyInput() { Name = InputKey.a, Code = "KEY_ENTER" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.b, Key = "(%{F4})" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.x, Key = "(%{F4})" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.left, Code = "KEY_LEFT" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.right, Code = "KEY_RIGHT" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.down, Code = "KEY_DOWN" });
            app.Input.Add(new PadToKeyInput() { Name = InputKey.up, Code = "KEY_UP" });
            mapping.Applications.Add(app);

            _joy = new JoystickListener(Program.Controllers.Where(c => c.Config.DeviceName != "Keyboard").ToArray(), mapping);
        }

        protected override void Dispose(bool disposing)
        {
            if (_joy != null)
            {
                _joy.Dispose();
                _joy = null;
            }

            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        void button2_GotFocus(object sender, EventArgs e)
        {
            button2.BackColor = Color.DarkSlateGray;
            button1.BackColor = Color.FromArgb(32,32,32);
        }

        void button1_GotFocus(object sender, EventArgs e)
        {
            button1.BackColor = Color.DarkSlateGray;
            button2.BackColor = Color.FromArgb(32, 32, 32);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            tableLayoutPanel2.RowStyles[1].SizeType = SizeType.Absolute;
            tableLayoutPanel2.RowStyles[1].Height = 0;

            tableLayoutPanel2.RowStyles[2].SizeType = SizeType.Percent;
            tableLayoutPanel2.RowStyles[2].Height = 50;

            label1.Text = "Downloading update";
            progressBar1.Value = 0;

            this.Refresh();

            _installer.DownloadAndInstall((o, pe) => 
            { 
                progressBar1.Value = pe.ProgressPercentage;
                if (pe.ProgressPercentage == 100)
                {
                    label1.Text = "Installation...";
                    Refresh();
                }
            });

            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_installer.ServerVersion))
                DialogResult = System.Windows.Forms.DialogResult.OK;

            Close();
        }
    }
}
