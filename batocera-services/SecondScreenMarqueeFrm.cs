using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Pipes;
using System.IO;
using EmulatorLauncher.Common;
using System.Globalization;
using EmulatorLauncher.Common.EmulationStation;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace batocera_services
{
    public partial class SecondScreenMarqueeFrm : Form
    {
        private NamedPipeServer _server;

        public SecondScreenMarqueeFrm()
        {               
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);

            InitializeComponent();

            webBrowser1.DocumentCompleted += OnDocumentCompleted;            
            _server = new NamedPipeServer(OnMessageReceived);

            this.BackColor = Color.FromArgb(10, 10, 10);
            this.ForeColor = Color.White;
            
            LoadDefaultLogo();

            // Start a separate thread to process the queue
            Thread processingThread = new Thread(ProcessQueue);
            processingThread.Start();
                        
            this.WindowState = FormWindowState.Maximized;
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.None;

            // Find the second monitor (if available)
            Screen secondScreen = Screen.AllScreens.Length > 1 ? Screen.AllScreens[1] : Screen.PrimaryScreen;

            // Set the form's location and size to fill the second monitor
            this.Location = secondScreen.WorkingArea.Location;
            this.Size = secondScreen.WorkingArea.Size;

            this.BringToFront();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }

            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

         static string ColorToHtml(Color color)
        {
            // Format the color values as hexadecimal and create the HTML color string
            string htmlColor = String.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            return htmlColor;
        }

        private void SetImageData(string base64, string contentType)
        {
            int paddingPercent = 5;
            string padding = (this.Size.Height * paddingPercent / 100).ToString() + "px " + (this.Size.Width * paddingPercent / 100).ToString() + "px ";

            string htmlContent =
            @"<html>
              <meta charset='UTF-8'>
              <meta http-equiv='X-UA-Compatible' content='IE=edge' />
              <meta name='viewport' content='width=device-width, initial-scale=1.0'>
              <head>
                  <style>
                      html { height: 100%; width:100%; }
                      body { background-color:" + ColorToHtml(this.BackColor) + @"; height: 100%; width:100%; margin: 0; display: flex; align-items: center; justify-content: center; }
                      .content 
				      {
                        margin:" + padding + @";
					    height: 100% !important; 
                        width:100%;                        
                        background-position:center;
					    background-repeat: no-repeat;
                        background-size: contain;
					    background-image: url('data:" + contentType + ";base64," + base64 + @"');					  
					  }
                  </style>
              </head>
              <body><div class='content'/></body>
              </html>";
            
            _loadDocument.Reset();

            webBrowser1.DocumentText = htmlContent;         
       
            while(!_loadDocument.WaitOne(0))
                Application.DoEvents();
        }

        private AutoResetEvent _loadDocument = new AutoResetEvent(false);

        private void OnDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            _loadDocument.Set();
        }
        
        private static double ExtractAttributeValue(string svgString, string attribute)
        {
            int startIndex = svgString.IndexOf(attribute + "=\"") + attribute.Length + 2;
            int endIndex = svgString.IndexOf("\"", startIndex);

            if (startIndex >= 0 && endIndex > startIndex)
            {
                string valueString = svgString.Substring(startIndex, endIndex - startIndex);

                double result;
                if (double.TryParse(valueString, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return result;
            }

            return 0.0; // Default value if extraction or parsing fails
        }
                
        private AutoResetEvent _queueChangedEvent = new AutoResetEvent(false);
        private AutoResetEvent _exitEvent = new AutoResetEvent(false);

        private List<string> _queue = new List<string>();
        private object _lock = new object();

        // Dispose of the message queue when the form is closed
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _exitEvent.Set();
            base.OnFormClosed(e);
        }

        private void OnMessageReceived(string s)
        {
            // Add the message to the queue
            lock (_lock)
                _queue.Add(s);

            _queueChangedEvent.Set();
        }

        private void ProcessQueue()
        {
            try
            {
                while (true)
                {
                    // Wait for the signal that there are items in the queue or the form is closed
                    if (WaitHandle.WaitAny(new WaitHandle[] { _queueChangedEvent, _exitEvent }) == 1)
                        break;

                    string message = null;

                    lock (_lock)
                    {
                        message = _queue.LastOrDefault();
                        _queue.Clear();
                    }

                    if (!string.IsNullOrEmpty(message))
                        Invoke(new Action(() => { ProcessMessage(message); }));
                }
            }
            catch
            {

            }
        }

        private void ProcessMessage(string s)
        {
            if (s == null)
                return;

            if (this.InvokeRequired)
            {
                this.Invoke(new MethodInvoker(() => { OnMessageReceived(s); }));
                return;
            }

            if (s == "quit")
            {
                Close();
                return;
            }

            var args = s.SplitCommandLine();
            if (args.Length == 0)
                return;

            string eventName = args[0];
            string arg1 = args.Length > 1 ? args[1] : "";
            string arg2 = args.Length > 2 ? args[2] : "";
            string arg3 = args.Length > 3 ? args[3] : "";

            try
            {
                switch (eventName)
                {
                    case "config-changed":
                        _cachedData.Clear();
                        break;

                    case "system-selected":
                        try
                        {
                            LoadSystemLogo(arg1);
                        }
                        catch
                        {
                            LoadDefaultLogo();
                        }
                        break;
                        
                    case "game-selected":
                        try
                        {
                            LoadGameLogo(arg1, arg2);
                        }
                        catch
                        {
                            try
                            {
                                LoadSystemLogo(arg1);
                            }
                            catch
                            {
                                LoadDefaultLogo();
                            }
                        }

                        break;
                }
            }
            catch { }

        }

        private void LoadDefaultLogo()
        {
            string base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(FixSVG(Encoding.UTF8.GetString(Properties.Resources.splash))));
            SetImageData(base64String, "image/svg+xml");            
        }

        class CachedData
        {
            public string ContentType { get; set; }
            public string Base64 { get; set; }
        }

        static Dictionary<string, CachedData> _cachedData = new Dictionary<string,CachedData>();

        private void LoadSystemLogo(string system)
        {
            CachedData value;
            if (_cachedData.TryGetValue(system, out value))
            {
                SetImageData(value.Base64, value.ContentType);
                return;
            }

            using (var ms = new MemoryStream())
            {
                var logo = WebTools.DownloadToStream(ms, "http://" + "127.0.0.1:1234/systems/" + system + "/logo");
                LoadImageFromEmulationStationServiceResponse(ms, logo, system);
            }
        }

        private void LoadGameLogo(string system, string rom)
        {
            string id = EmulationStationServices.getFileDataId(rom);

            using (var ms = new MemoryStream())
            {
                var logo = WebTools.DownloadToStream(ms, "http://" + "127.0.0.1:1234/systems/" + system + "/games/" + id + "/media/marquee");
                LoadImageFromEmulationStationServiceResponse(ms, logo);
            }
        }

        private static string FixSVG(string data)
        {
            if (!data.Contains("viewBox="))
            {
                double h = ExtractAttributeValue(data, "height");
                double w = ExtractAttributeValue(data, "width");

                if (h != 0 && w != 0)
                {
                    data = data.Replace("<svg ", "<svg viewBox=\"0 0 " + (w).ToString(CultureInfo.InvariantCulture) + " " + (h).ToString(CultureInfo.InvariantCulture) + "\" ");
                    data = data.Replace("<svg\n", "<svg viewBox=\"0 0 " + (w).ToString(CultureInfo.InvariantCulture) + " " + (h).ToString(CultureInfo.InvariantCulture) + "\"\n");
                    data = data.Replace("<svg\r", "<svg viewBox=\"0 0 " + (w).ToString(CultureInfo.InvariantCulture) + " " + (h).ToString(CultureInfo.InvariantCulture) + "\"\r");
                }
            }
            return data;
        }

        private void LoadImageFromEmulationStationServiceResponse(MemoryStream ms, ResponseStreamInfo logo, string cacheIndex = null)
        {
            ms.Position = 0;

            if (logo.ContentType == "image/svg+xml")
            {
                using (StreamReader reader = new StreamReader(ms, Encoding.UTF8))
                {
                    string data = reader.ReadToEnd();

                    // If viewBox is missing, fix it or the browser will not stretch the SVG
                    data = FixSVG(data);

                    string base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));

                    if (cacheIndex != null)
                        _cachedData[cacheIndex] = new CachedData() { Base64 = base64String, ContentType = logo.ContentType };

                    SetImageData(base64String, logo.ContentType);
                }
            }
            else
            {
                string base64String = Convert.ToBase64String(ms.ToArray());

                if (cacheIndex != null)
                    _cachedData[cacheIndex] = new CachedData() { Base64 = base64String, ContentType = logo.ContentType };

                SetImageData(base64String, logo.ContentType);
            }
        }

    }
}
