using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher
{

    class ProgressInformation : IDisposable
    {
        System.Windows.Forms.Form _frm;
        System.Windows.Forms.Label _label;

        public ProgressInformation(string text = "")
        {
            if (!Kernel32.IsRunningInConsole())
            {
                var frm = new System.Windows.Forms.Form();
                _frm = frm;

                frm.Font = System.Drawing.SystemFonts.MessageBoxFont;
                frm.Text = "";
                frm.ControlBox = false;
                frm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
                frm.Size = new System.Drawing.Size(280, 80);
                frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                frm.ShowInTaskbar = false;

                _label = new System.Windows.Forms.Label()
                {
                    Text = text,
                    AutoSize = false,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Dock = System.Windows.Forms.DockStyle.Fill
                };

                frm.Controls.Add(_label);
                frm.Show();

                System.Windows.Forms.Application.DoEvents();
            }
            else if (!string.IsNullOrEmpty(text))
                Console.WriteLine(text);
        }

        public void SetText(string text)
        {
            if (_label != null)
            {
                _label.Text = text;
                System.Windows.Forms.Application.DoEvents();
            }
            else
                Console.WriteLine(text);
        }

        public void Dispose()
        {
            if (_frm != null)
            {
                _frm.Dispose();
                _frm = null;
            }
        }
    }

}
