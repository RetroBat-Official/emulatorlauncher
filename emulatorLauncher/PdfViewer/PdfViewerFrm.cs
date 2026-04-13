using EmulatorLauncher.Common;
using System;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace EmulatorLauncher
{
    public partial class PdfViewerFrm : Form
    {
        public PdfViewerFrm(string pdfFile)
        {
            InitializeComponent();
            KeyPreview = true;

            var host = new ElementHost { Dock = DockStyle.Fill };
            var viewer = new PdfViewerControl();
            viewer.EscapePressed += () => this.Close();
            host.Child = viewer;
            Controls.Add(host);

            if (Path.GetExtension(pdfFile).ToLowerInvariant() == ".pdf")
            {
                string folder = PdfExtractor.BeginExtractPdfPages(pdfFile, 300, new Action<int, int>((upToPage, total) =>
                {
                    viewer.Dispatcher.Invoke(new Action(() => viewer.NotifyPagesAvailable(upToPage, total)));
                }));

                if (folder != null)
                    viewer.LoadFolder(folder);
            }
            else
                viewer.LoadImage(pdfFile);
        }

        protected override void Dispose(bool disposing)
        {
            // Temp folder lifetime is managed by PdfExtractor cache, not here
            if (disposing && components != null)
                components.Dispose();

            base.Dispose(disposing);
        }
    }
}