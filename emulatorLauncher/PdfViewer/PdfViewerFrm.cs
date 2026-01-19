using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace EmulatorLauncher
{
    public partial class PdfViewerFrm : Form
    {
        private string _path;

        public PdfViewerFrm(string pdfFile)
        {
            InitializeComponent();
            KeyPreview = true;

            var host = new ElementHost { Dock = DockStyle.Fill };

            var viewer = new PdfViewerControl();
            viewer.EscapePressed += () => { this.Close(); };
            host.Child = viewer;

            Controls.Add(host);

            if (Path.GetExtension(pdfFile).ToLowerInvariant() == ".pdf")
            {
                using (new WaitCursor())
                    _path = PdfExtractor.ExtractPdfPages(pdfFile, 300);

                viewer.LoadFolder(_path);
            }
            else
                viewer.LoadImage(pdfFile);
        }

        protected override void Dispose(bool disposing)
        {
            if (!string.IsNullOrEmpty(_path) && Directory.Exists(_path))
            {
                try { Directory.Delete(_path, true); }
                catch { }
            }

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
