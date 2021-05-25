using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using emulatorLauncher.Tools;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace emulatorLauncher
{
    class BezelFiles
    {
        public string PngFile { get; set; }
        public string InfoFile { get; set; }

        public BezelInfo BezelInfos
        {
            get
            {
                if (_infos == null)
                {
                    _infos = new BezelInfo();

                    if (!string.IsNullOrEmpty(InfoFile) && File.Exists(InfoFile))
                    {
                        try { _infos = JsonSerializer.DeserializeFile<BezelInfo>(InfoFile); }
                        catch { }
                    }

                }

                return _infos;
            }
        }

                    
        private BezelInfo _infos;

          

        public FakeBezelFrm ShowFakeBezel(ScreenResolution resolution)
        {
            int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

            var bezel = new FakeBezelFrm();
            bezel.TopMost = true;

            var file = GetStretchedBezel(PngFile, resX, resY);
            if (!bezel.SelectBezel(file, resX, resY))
            {
                bezel.Dispose();
                return null;
            }

            bezel.Show();
            return bezel;
        }

        public static BezelFiles GetBezelFiles(string systemName, string rom, ScreenResolution resolution)
        {
            int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

            float screenRatio = (float)resX / (float)resY;

            if (screenRatio < 1.4)
                return null;

            return GetBezelFiles(systemName, rom);
        }

        public static BezelFiles GetBezelFiles(string systemName, string rom)
        {
            if (systemName == null || rom == null)
                return null;

            string overlayUser = Program.AppConfig.GetFullPath("decorations");

            string overlaySystem = Program.AppConfig.GetFullPath("system.decorations");
            if (string.IsNullOrEmpty(overlaySystem) || !Directory.Exists(overlaySystem))
                overlaySystem = Path.Combine(Program.AppConfig.GetFullPath("home"), "decorations");

            string bezel = Directory.Exists(overlayUser) && !string.IsNullOrEmpty(Program.SystemConfig["bezel"]) ? Program.SystemConfig["bezel"] : "default";

            if (Program.SystemConfig.isOptSet("forceNoBezel") && Program.SystemConfig.getOptBoolean("forceNoBezel"))
                bezel = null;
            else if (!Program.SystemConfig.isOptSet("bezel"))
                bezel = "thebezelproject";

            if (string.IsNullOrEmpty(bezel) || bezel == "none")
                return null;

            string romBase = Path.GetFileNameWithoutExtension(rom);

            string overlay_info_file = overlayUser + "/" + bezel + "/games/" + systemName + "/" + romBase + ".info";
            string overlay_png_file = overlayUser + "/" + bezel + "/games/" + systemName + "/" + romBase + ".png";

            if (!File.Exists(overlay_png_file) && !string.IsNullOrEmpty(overlaySystem))
            {
                overlay_info_file = overlaySystem + "/" + bezel + "/games/" + systemName + "/" + romBase + ".info";
                overlay_png_file = overlaySystem + "/" + bezel + "/games/" + systemName + "/" + romBase + ".png";
            }

            if (!File.Exists(overlay_png_file))
            {
                overlay_info_file = overlayUser + "/" + bezel + "/games/" + romBase + ".info";
                overlay_png_file = overlayUser + "/" + bezel + "/games/" + romBase + ".png";
            }

            if (!string.IsNullOrEmpty(overlaySystem) && !File.Exists(overlay_png_file))
            {
                overlay_info_file = overlaySystem + "/" + bezel + "/games/" + romBase + ".info";
                overlay_png_file = overlaySystem + "/" + bezel + "/games/" + romBase + ".png";
            }

            if (!File.Exists(overlay_png_file))
            {
                overlay_info_file = overlayUser + "/" + bezel + "/systems/" + systemName + ".info";
                overlay_png_file = overlayUser + "/" + bezel + "/systems/" + systemName + ".png";
            }

            if (!string.IsNullOrEmpty(overlaySystem) && !File.Exists(overlay_png_file))
            {
                overlay_info_file = overlaySystem + "/" + bezel + "/systems/" + systemName + ".info";
                overlay_png_file = overlaySystem + "/" + bezel + "/systems/" + systemName + ".png";
            }

            if (!File.Exists(overlay_png_file))
            {
                overlay_info_file = overlayUser + "/" + bezel + "/default.info";
                overlay_png_file = overlayUser + "/" + bezel + "/default.png";
            }

            if (!string.IsNullOrEmpty(overlaySystem) && !File.Exists(overlay_png_file))
            {
                overlay_info_file = overlaySystem + "/" + bezel + "/default.info";
                overlay_png_file = overlaySystem + "/" + bezel + "/default.png";
            }

            if (!File.Exists(overlay_png_file))
            {
                overlay_info_file = overlayUser + "/default_unglazed/systems/" + systemName + ".info";
                overlay_png_file = overlayUser + "/default_unglazed/systems/" + systemName + ".png";
            }

            if (!string.IsNullOrEmpty(overlaySystem) && !File.Exists(overlay_png_file))
            {
                overlay_info_file = overlaySystem + "/default_unglazed/systems/" + systemName + ".info";
                overlay_png_file = overlaySystem + "/default_unglazed/systems/" + systemName + ".png";
            }

            if (!File.Exists(overlay_png_file))
            {
                overlay_info_file = overlayUser + "/default/systems/" + systemName + ".info";
                overlay_png_file = overlayUser + "/default/systems/" + systemName + ".png";
            }

            if (!string.IsNullOrEmpty(overlaySystem) && !File.Exists(overlay_png_file))
            {
                overlay_info_file = overlaySystem + "/default/systems/" + systemName + ".info";
                overlay_png_file = overlaySystem + "/default/systems/" + systemName + ".png";
            }

            if (!File.Exists(overlay_png_file))
                return null;

            if (!File.Exists(overlay_info_file))
                overlay_info_file = null;

            return new BezelFiles() { PngFile = overlay_png_file, InfoFile = overlay_info_file };
        }
        
        public static string GetStretchedBezel(string overlay_png_file, int resX, int resY)
        {
            var f = Path.GetFileNameWithoutExtension(overlay_png_file);
            var d = Path.GetFileName(Path.GetDirectoryName(overlay_png_file));
            var fn = "bezel." + d + "." + f + "." + resX + "x" + resY + ".png";
            string output_png_file = Path.Combine(Path.GetTempPath(), fn);

            if (File.Exists(output_png_file))
                overlay_png_file = output_png_file;
            else
            {
                try
                {
                    using (Image img = Image.FromFile(overlay_png_file))
                    {
                        if (img.Width == resX && img.Height == resY)
                            return overlay_png_file;

                        using (Bitmap bmp = new Bitmap(resX, resY))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                Rectangle rect = Misc.GetPictureRect(img.Size, new Rectangle(0, 0, resX, resY));
                                if (rect.X != 0 && rect.Y != 0)
                                {
                                    g.ExcludeClip(rect);
                                    g.FillRectangle(Brushes.Black, new Rectangle(0, 0, bmp.Width, bmp.Height));
                                    g.ResetClip();
                                }

                                g.DrawImage(img, rect);
                            }

                            bmp.Save(output_png_file, System.Drawing.Imaging.ImageFormat.Png);
                            overlay_png_file = output_png_file;
                        }
                    }
                }
                catch { }
            }
            return overlay_png_file;
        }

    }

    [DataContract]
    class BezelInfo
    {
        public bool IsValid()
        {
            return (width.HasValue && height.HasValue && top.HasValue && left.HasValue && bottom.HasValue && right.HasValue);
        }

        [DataMember]
        public int? width { get; set; }
        [DataMember]
        public int? height { get; set; }
        [DataMember]
        public int? top { get; set; }
        [DataMember]
        public int? left { get; set; }
        [DataMember]
        public int? bottom { get; set; }
        [DataMember]
        public int? right { get; set; }
        [DataMember]
        public float? opacity { get; set; }
        [DataMember]
        public float? messagex { get; set; }
        [DataMember]
        public float? messagey { get; set; }
    }
}
