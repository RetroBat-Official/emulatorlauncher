using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    public partial class FakeBezelFrm : Form
    {
        public FakeBezelFrm()
        {
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Load += OnLoaded;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (FakeBackground != null)
            {
                FakeBackground.Dispose();
                FakeBackground = null;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public FakeBezelBackgroundFrm FakeBackground { get; set; }        

        void OnLoaded(object sender, EventArgs e)
        {
            this.TopMost = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Add the layered extended style (WS_EX_LAYERED) to this window.
                CreateParams createParams = base.CreateParams;
                if (!DesignMode)
                    createParams.ExStyle |= (int) (WS_EX.LAYERED | WS_EX.NOACTIVATE | WS_EX.TRANSPARENT);

                return createParams;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_NCHITTEST)
            {
                message.Result = (IntPtr) (int) -1; // HT_TRANSPARENT
                return;
            }

            base.WndProc(ref message);            
        }

        public bool SelectBezel(string bitmapPath, int resX, int resY)
        {
            try
            {
                var bitmap = new Bitmap(System.Drawing.Bitmap.FromFile(bitmapPath), resX, resY);
                return SelectBitmap(bitmap, 255);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("Error loading " + (bitmapPath ?? "[null]"), ex);
            }

            return false;
        }

        private bool SelectBitmap(Bitmap bitmap, int opacity)
        {
            // Does this bitmap contain an alpha channel?
            if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
                return false;

            // Get device contexts
            IntPtr screenDc = User32.GetDC(IntPtr.Zero);
            IntPtr memDc = Gdi32.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                // Get handle to the new bitmap and select it into the current 
                // device context.
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                hOldBitmap = Gdi32.SelectObject(memDc, hBitmap);

                // Set parameters for layered window update.
                SIZE newSize = new SIZE(bitmap.Width, bitmap.Height);
                POINT sourceLocation = new POINT(0, 0);
                POINT newLocation = new POINT(this.Left, this.Top);
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = (byte)opacity;
                blend.AlphaFormat = AC_SRC_ALPHA;

                // Update the window.
                User32.UpdateLayeredWindow(
                    this.Handle,     // Handle to the layered window
                    screenDc,        // Handle to the screen DC
                    ref newLocation, // New screen position of the layered window
                    ref newSize,     // New size of the layered window
                    memDc,           // Handle to the layered window surface DC
                    ref sourceLocation, // Location of the layer in the DC
                    0,               // Color key of the layered window
                    ref blend,       // Transparency of the layered window
                    ULW_ALPHA        // Use blend as the blend function
                    );

                return true;
            }
            catch 
            {
                throw;
            }
            finally
            {
                // Release device context.
                User32.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    Gdi32.SelectObject(memDc, hOldBitmap);
                    Gdi32.DeleteObject(hBitmap);
                }
                Gdi32.DeleteDC(memDc);
            }
        }

        #region Native Methods and Structures
        const Int32 WM_NCHITTEST = 0x84;
        const Int32 ULW_ALPHA = 0x02;
        const byte AC_SRC_OVER = 0x00;
        const byte AC_SRC_ALPHA = 0x01;
        #endregion

        public Rectangle ViewPort { get; set; }
    }

    public partial class FakeBezelBackgroundFrm : Form
    {
        public FakeBezelBackgroundFrm()
        {
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.Black;
        }
        
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams createParams = base.CreateParams;

                if (!DesignMode)
                    createParams.ExStyle |= (int)(WS_EX.NOACTIVATE);

                return createParams;
            }
        }
        
        const Int32 WM_NCHITTEST = 0x84;

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_NCHITTEST)
            {
                message.Result = (IntPtr)(int)-1; // HT_TRANSPARENT
                return;
            }

            base.WndProc(ref message);
        }
    }
}