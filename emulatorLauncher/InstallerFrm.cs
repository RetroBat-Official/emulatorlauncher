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
using System.IO;
using System.Globalization;

namespace emulatorLauncher
{
    partial class InstallerFrm : Form
    {
        public static void ShowMessage(string message)
        {
            using (InstallerFrm frm = new InstallerFrm())
            {
                frm.SetLabel(message);
                frm.SetupButtons(InstallerButtons.Ok);
                frm.SetupLayout(InstallerLayout.ButtonsAndText);
                frm.ShowDialog();
            }
        }

        private string _url;
        private string _installFolder;
        private bool _isUpdate;

        private JoystickListener _joy;
        
        public InstallerFrm()
        {
            InitializeComponent();

            Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, this.Font.Size, FontStyle.Regular);

            button1.GotFocus += button1_GotFocus;
            button2.GotFocus += button2_GotFocus;

            _flex = new Flex(this);
            _flex.AddControl(pictureBox1, FlexMode.Size | FlexMode.Position | FlexMode.KeepProportions);
            _flex.AddControl(button1, FlexMode.Size);
            _flex.AddControl(button2, FlexMode.Size);
            _flex.AddControl(label1, FlexMode.Padding);
            _flex.AddControl(progressBar1, FlexMode.Size);

            SetupButtons(InstallerButtons.YesNo);
            SetupLayout(InstallerLayout.ButtonsAndText);

        }

        public InstallerFrm(Installer installer)
            : this()
        {
            if (installer == null)
                return;
            
            if (string.IsNullOrEmpty(installer.ServerVersion))
                label1.Text = string.Format(Properties.Resources.EmulatorNotInstalled, installer.DefaultFolderName);
            else
                label1.Text = string.Format(Properties.Resources.UpdateAvailable, installer.DefaultFolderName, installer.ServerVersion, installer.GetInstalledVersion());

            _url = installer.PackageUrl;
            _installFolder = installer.GetInstallFolder();
            _isUpdate = !string.IsNullOrEmpty(installer.ServerVersion);

            SetupLayout(InstallerLayout.ButtonsAndText);            
        }

        public InstallerFrm(string name, string url, string installFolder) : this()
        {
            _url = url;
            _installFolder = installFolder;

            label1.Text = string.Format(Properties.Resources.CoreNotInstalled, name);

            SetupLayout(InstallerLayout.ButtonsAndText);
        }
                        
        enum InstallerButtons
        {
            YesNo,
            Ok
        }
        enum InstallerLayout
        {
            Text,
            ButtonsAndText,
            ProgressAndText
        }

        public void SetLabel(string label)
        {
            this.label1.Text = label;
        }

        void SetupButtons(InstallerButtons buttons)
        {
            switch (buttons)
            {
                case InstallerButtons.YesNo:                                
                    button1.Text = Properties.Resources.Yes;            
                    button2.Text = Properties.Resources.No;
                    button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    button2.Visible = true;
                    tableLayoutPanel1.ColumnStyles[1].SizeType = SizeType.Percent;
                    tableLayoutPanel1.ColumnStyles[1].Width = 1f;
                    tableLayoutPanel1.ColumnStyles[2].SizeType = SizeType.Percent;
                    tableLayoutPanel1.ColumnStyles[2].Width = 50;
                    tableLayoutPanel1.ColumnStyles[0].SizeType = SizeType.Percent;
                    tableLayoutPanel1.ColumnStyles[0].Width = 50;
                    break;
                case InstallerButtons.Ok:
                    button1.Text = Properties.Resources.Ok;
                    button1.Anchor = AnchorStyles.Top;
                    button1.Focus();
                    button2.Visible = false;
                    tableLayoutPanel1.ColumnStyles[1].SizeType = SizeType.Absolute;
                    tableLayoutPanel1.ColumnStyles[1].Width = 0;
                    tableLayoutPanel1.ColumnStyles[2].SizeType = SizeType.Absolute;
                    tableLayoutPanel1.ColumnStyles[2].Width = 0;
                    tableLayoutPanel1.ColumnStyles[0].SizeType = SizeType.Percent;
                    tableLayoutPanel1.ColumnStyles[0].Width = 100;
                    break;
            }
        }

        void SetupLayout(InstallerLayout layout)
        {
            switch (layout)
            {
                case InstallerLayout.Text:
                    label1.TextAlign = ContentAlignment.MiddleCenter;
                    tableLayoutPanel2.RowStyles[1].SizeType = SizeType.Absolute;
                    tableLayoutPanel2.RowStyles[1].Height = 0;
                    tableLayoutPanel2.RowStyles[2].SizeType = SizeType.Absolute;
                    tableLayoutPanel2.RowStyles[2].Height = 0;
                    tableLayoutPanel2.RowStyles[0].SizeType = SizeType.Percent;
                    tableLayoutPanel2.RowStyles[0].Height = 100;
                    break;
                case InstallerLayout.ButtonsAndText:
                    label1.TextAlign = ContentAlignment.BottomCenter;
                    tableLayoutPanel2.RowStyles[2].SizeType = SizeType.Absolute;
                    tableLayoutPanel2.RowStyles[2].Height = 0;
                    tableLayoutPanel2.RowStyles[0].SizeType = SizeType.Percent;
                    tableLayoutPanel2.RowStyles[0].Height = 47;
                    tableLayoutPanel2.RowStyles[1].SizeType = SizeType.Percent;
                    tableLayoutPanel2.RowStyles[1].Height = 53;
                    break;
                case InstallerLayout.ProgressAndText:
                    label1.TextAlign = ContentAlignment.BottomCenter;
                    progressBar1.Value = 0;                  
                    tableLayoutPanel2.RowStyles[1].SizeType = SizeType.Absolute;
                    tableLayoutPanel2.RowStyles[1].Height = 0;
                    tableLayoutPanel2.RowStyles[0].SizeType = SizeType.Percent;
                    tableLayoutPanel2.RowStyles[0].Height = 50;
                    tableLayoutPanel2.RowStyles[2].SizeType = SizeType.Percent;
                    tableLayoutPanel2.RowStyles[2].Height = 50;
                    break;
            }
        }

        private Flex _flex;

        class Flex
        {
            public Flex(Form frm)
            {
                _frm = frm;
                InitialSize = frm.Size;
                InitialFontSize = frm.Font.Size;

                frm.SizeChanged += new EventHandler(frm_SizeChanged);

                frm_SizeChanged(this, EventArgs.Empty);
            }

            void frm_SizeChanged(object sender, EventArgs e)
            {
                if (InitialSize.Height <= 0 || InitialSize.Width <= 0)
                    return;

                float szw = _frm.Width / (float)InitialSize.Width;
                float szh = _frm.Height / (float)InitialSize.Height;

                _frm.Font = new Font(SystemFonts.MessageBoxFont.FontFamily.Name, (int)Math.Max(6, InitialFontSize * szh), FontStyle.Regular);

                foreach (var c in _controls)
                {
                    float szA = c.Mode.HasFlag(FlexMode.KeepProportions) ? szh : szw;

                    if (c.Mode.HasFlag(FlexMode.Position))
                        c.Control.Location = new Point((int)(c.InitialPosition.X * szA), (int)(c.InitialPosition.Y * szh));

                    if (c.Mode.HasFlag(FlexMode.Size))
                        c.Control.Size = new Size((int)(c.InitialSize.Width * szA), (int)(c.InitialSize.Height * szh));

                    if (c.Mode.HasFlag(FlexMode.Padding))
                        c.Control.Padding = new Padding(
                            (int)(c.InitialPadding.Left * szA),
                            (int)(c.InitialPadding.Top * szh),
                            (int)(c.InitialPadding.Right * szA),
                            (int)(c.InitialPadding.Bottom * szh)
                            );
                }
            }

            public Size InitialSize { get; set; }
            public float InitialFontSize { get; set; }

            private Form _frm;

            public void AddControl(Control c, FlexMode mode)
            {
                _controls.Add(new ControlFlex()
                {
                    Control = c,
                    Mode = mode,
                    InitialSize = c.Size,
                    InitialPosition = c.Location,
                    InitialPadding = c.Padding
                });
            }

            class ControlFlex
            {
                public Control Control { get; set; }
                public Size InitialSize { get; set; }
                public Point InitialPosition { get; set; }
                public Padding InitialPadding { get; set; }

                public FlexMode Mode { get; set; }
            }

            private List<ControlFlex> _controls = new List<ControlFlex>();
        }

        [Flags]
        enum FlexMode
        {
            Position = 1,
            Size = 2,
            Padding = 4,
            KeepProportions = 128
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetupPad();
        }

        public void UpdateAll()
        {
            progressBar1.Visible = false;
            progressBar1.Value = 0;
            label1.Text = Properties.Resources.LookingForUpdates;            
            SetupLayout(InstallerLayout.Text);

            Show();
            Refresh();

            string currentEmulator = null;
            bool shown = false;

            try
            {
                Installer.UpdateAll((o, pe) =>
                {
                    if (!shown)
                    {
                        SetupLayout(InstallerLayout.ProgressAndText);
                        progressBar1.Visible = true;
                        shown = true;
                    }

                    progressBar1.Value = pe.ProgressPercentage;

                    string emul = pe.UserState as string;
                    if (emul != null && emul != currentEmulator)
                    {
                        currentEmulator = emul;
                        label1.Text = string.Format(Properties.Resources.Updating, currentEmulator);
                        Refresh();
                    }
                });

            }
            catch (Exception ex)
            {
                _url = null;

                SetupLayout(InstallerLayout.ButtonsAndText);
                SetupButtons(InstallerButtons.Ok);
                SetLabel(string.Format(Properties.Resources.ErrorOccured, ex.Message));
                return;
            }

            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }


        public bool UnCompressFile(string fileName, string destinationPath)
        {
            progressBar1.Visible = false;
            progressBar1.Value = 0;
            label1.Text = string.Format(Properties.Resources.UnCompressing, Path.GetFileNameWithoutExtension(fileName));
            SetupLayout(InstallerLayout.Text);

            Show();
            Refresh();
          
            bool shown = false;

            try
            {
                Zip.Extract(fileName, destinationPath, null, (o, pe) =>
                {
                    if (!shown)
                    {
                        SetupLayout(InstallerLayout.ProgressAndText);
                        progressBar1.Visible = true;
                        shown = true;
                        Refresh();
                    }

                    progressBar1.Value = pe.ProgressPercentage;
                });

            }
            catch (Exception ex)
            {
                SetupLayout(InstallerLayout.ButtonsAndText);
                SetupButtons(InstallerButtons.Ok);
                SetLabel(string.Format(Properties.Resources.ErrorOccured, ex.Message));

                Visible = false;
                ShowDialog();
                return false;
            }

            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
            return true;
        }

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
            if (!string.IsNullOrEmpty(_url) && !string.IsNullOrEmpty(_installFolder))
                DownloadAndInstall(_url, _installFolder);
            else
            {
                DialogResult = System.Windows.Forms.DialogResult.OK;
                Close();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_isUpdate)
                DialogResult = System.Windows.Forms.DialogResult.OK;

            Close();
        }

        public void DownloadAndInstall(string url, string installFolder)
        {
            SetupLayout(InstallerLayout.ProgressAndText);
            label1.Text = string.Format(Properties.Resources.Downloading, Path.GetFileNameWithoutExtension(url));

            Show();
            Refresh();

            try
            {
                Installer.DownloadAndInstall(url, installFolder, (o, pe) =>
                {
                    progressBar1.Value = pe.ProgressPercentage;
                    if (pe.ProgressPercentage == 100)
                    {
                        label1.Text = Properties.Resources.Installing;
                        Refresh();
                    }
                });
            }
            catch (Exception ex)
            {
                _url = null;

                SetupLayout(InstallerLayout.ButtonsAndText);
                SetupButtons(InstallerButtons.Ok);
                SetLabel(string.Format(Properties.Resources.ErrorOccured, ex.Message));
                return;
            }

            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }
        
    }
}
