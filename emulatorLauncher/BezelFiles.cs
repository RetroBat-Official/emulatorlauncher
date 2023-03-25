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
                        try 
                        {
                            string data = File.ReadAllText(InfoFile);
                            if (data != null && data.Trim().EndsWith(".info"))
                            {
                                string linkedFile = Path.Combine(Path.GetDirectoryName(InfoFile), data);
                                if (File.Exists(linkedFile))
                                    data = File.ReadAllText(linkedFile);
                            }

                            _infos = JsonSerializer.DeserializeString<BezelInfo>(data); 
                        }
                        catch { }
                    }

                }

                return _infos;
            }
            set
            {
                _infos = value;
            }
        }

                    
        private BezelInfo _infos;


        private static Size GetImageSize(string file)
        {
            using (Image img = Image.FromFile(file))
                return img.Size;
        }

        public FakeBezelFrm ShowFakeBezel(ScreenResolution resolution, bool useFakeBackground = false, int monitorIndex = -1)
        {
            var screen = Screen.PrimaryScreen;

            if (monitorIndex > 0 && monitorIndex < Screen.AllScreens.Length)
                screen = Screen.AllScreens[monitorIndex];

            int resX = (resolution == null ? screen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? screen.Bounds.Height : resolution.Height);

            FakeBezelBackgroundFrm fakeBackground = null;
            if (useFakeBackground)
            {
                fakeBackground = new FakeBezelBackgroundFrm();
                fakeBackground.Bounds = screen.Bounds;
                fakeBackground.Show();
            }

            var bezel = new FakeBezelFrm();
            bezel.Bounds = screen.Bounds;
            bezel.TopMost = true;

            bool stretchImage = false;

            if (BezelInfos != null && BezelInfos.IsValid())
            {
                Size imageSize = new Size(resX, resY);

                try { imageSize = GetImageSize(PngFile); }
                catch { }

                float screenRatio = (float)resX / (float)resY;
                float bezelRatio = (float)imageSize.Width / (float)imageSize.Height;

                if (!BezelInfos.IsValid())
                    stretchImage = true;
                else if (resX < BezelInfos.width || resY < BezelInfos.height) // If width or height < original, can't add black borders. Just stretch
                    stretchImage = true;
                else if (Math.Abs(screenRatio - bezelRatio) < 0.2) // FCA : About the same ratio ? Just stretch
                    stretchImage = true;

                var infos = BezelInfos;
                float wratio = resX / (float)infos.width;
                float hratio = resY / (float)infos.height;
                int xoffset = resX - infos.width.Value;
                int yoffset = resY - infos.height.Value;

                if (stretchImage)
                {
                    int custom_viewport_x = (int)(infos.left * wratio);
                    int custom_viewport_y = (int)(infos.top * hratio);
                    int custom_viewport_width = (int)((infos.width - infos.left - infos.right) * wratio);
                    int custom_viewport_height = (int)((infos.height - infos.top - infos.bottom) * hratio);


                    bezel.ViewPort = new Rectangle(
                        screen.Bounds.Left + custom_viewport_x,
                        screen.Bounds.Top + custom_viewport_y, custom_viewport_width, custom_viewport_height);
                }
                else
                {
                    int custom_viewport_x = (int)(infos.left + xoffset / 2);
                    int custom_viewport_y = (int)(infos.top + yoffset / 2);
                    int custom_viewport_width = (int)((infos.width - infos.left - infos.right));
                    int custom_viewport_height = (int)((infos.height - infos.top - infos.bottom));

                    bezel.ViewPort = new Rectangle(
                        screen.Bounds.Left + custom_viewport_x,
                        screen.Bounds.Top + custom_viewport_y, custom_viewport_width, custom_viewport_height);
                }
            }
            else
                bezel.ViewPort = new Rectangle(0, 0, resX, resY);

            var file = stretchImage ? PngFile : GetStretchedBezel(PngFile, resX, resY);
            if (!bezel.SelectBezel(file, resX, resY))
            {
                bezel.Dispose();
                return null;
            }

            bezel.FakeBackground = fakeBackground;
            bezel.Show();
            return bezel;
        }

        static string[] bezelPaths =
        {            
            // Bezels with exact rom name -> Uses {rom} for rom name
            "{userpath}\\{bezel}\\games\\{system}\\{rom}.png",
            "{systempath}\\{bezel}\\games\\{system}\\{rom}.png",
            "{userpath}\\{bezel}\\games\\{rom}.png",
            "{systempath}\\{bezel}\\games\\{rom}.png",

            // Bezels with same IndexedRomName -> Uses * instead of rom name
            "{userpath}\\{bezel}\\games\\{system}\\*.png",
            "{systempath}\\{bezel}\\games\\{system}\\*.png",
            "{userpath}\\{bezel}\\games\\*.png",
            "{systempath}\\{bezel}\\games\\*.png",

            // System bezels
            "{userpath}\\{bezel}\\systems\\{system}.png",
            "{systempath}\\{bezel}\\systems\\{system}.png",
            "{userpath}\\{bezel}\\default.png",
            "{systempath}\\{bezel}\\default.png",
            
            // Default_unglazed
            "{userpath}\\default_unglazed\\systems\\{system}.png",
            "{systempath}\\default_unglazed\\systems\\{system}.png",

            // Default
            "{userpath}\\default\\systems\\{system}.png",
            "{systempath}\\default\\systems\\{system}.png"
        };

        private static string FindBezel(string overlayUser, string overlaySystem, string bezel, string systemName, string romName)
        {
            string indexedRomName = romName.AsIndexedRomName();

            foreach (var path in bezelPaths)
            {
                if (string.IsNullOrEmpty(overlaySystem) && path.StartsWith("{systempath}"))
                    continue;

                if (string.IsNullOrEmpty(bezel) && path.StartsWith("{bezel}"))
                    continue;

                string result = path
                    .Replace("{userpath}", overlayUser ?? "")
                    .Replace("{systempath}", overlaySystem ?? "")
                    .Replace("{bezel}", bezel ?? "")
                    .Replace("{system}", systemName ?? "")
                    .Replace("{rom}", romName ?? "");

                if (result.Contains("*"))
                {
                    string dir = Path.GetDirectoryName(result);
                    if (Directory.Exists(dir))
                    {
                        foreach (var file in Directory.GetFiles(dir, Path.GetFileName(result)))
                        {
                            if (Path.GetFileNameWithoutExtension(file).AsIndexedRomName() == indexedRomName)
                                return Path.GetFullPath(file);
                        }
                    }

                    continue;
                }
                
                if (File.Exists(result))
                    return Path.GetFullPath(result);
            }

            return null;
        }
      
        public static BezelFiles GetBezelFiles(string systemName, string rom, ScreenResolution resolution)
        {
            if (systemName == null || rom == null)
                return null;

            int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);


            string overlayUser = Program.AppConfig.GetFullPath("decorations");

            string overlaySystem = Program.AppConfig.GetFullPath("system.decorations");
            if (string.IsNullOrEmpty(overlaySystem) || !Directory.Exists(overlaySystem))
                overlaySystem = Path.Combine(Program.AppConfig.GetFullPath("home"), "decorations");

            string bezel = Directory.Exists(overlayUser) && !string.IsNullOrEmpty(Program.SystemConfig["bezel"]) ? Program.SystemConfig["bezel"] : "default";

            if (Program.SystemConfig.isOptSet("forceNoBezel") && Program.SystemConfig.getOptBoolean("forceNoBezel"))
                bezel = null;
            else if (!Program.SystemConfig.isOptSet("bezel"))
            {
                if (!string.IsNullOrEmpty(Program.CurrentGame.Bezel) && File.Exists(Program.CurrentGame.Bezel))
                    return new BezelFiles() { PngFile = Program.CurrentGame.Bezel };

                bezel = "thebezelproject";
            }

            if (string.IsNullOrEmpty(bezel) || bezel == "none")
                return null;

            float screenRatio = (float)resX / (float)resY;
            if (screenRatio < 1.4)
            {
                if (Program.SystemConfig.isOptSet("use_guns") && Program.SystemConfig.getOptBoolean("use_guns") && Misc.IsSindenLightGunConnected())
                    return CreateSindenBorderBezel(null, resolution);

                return null;
            }

            string overlay_png_file = FindBezel(overlayUser, overlaySystem, bezel, systemName, Path.GetFileNameWithoutExtension(rom));
            if (string.IsNullOrEmpty(overlay_png_file))
            {
                if (Program.SystemConfig.isOptSet("use_guns") && Program.SystemConfig.getOptBoolean("use_guns") && Misc.IsSindenLightGunConnected())
                    return CreateSindenBorderBezel(null, resolution);

                return null;
            }

            string overlay_info_file = Path.ChangeExtension(overlay_png_file, ".info");
            if (!File.Exists(overlay_info_file))
                overlay_info_file = null;

            var ret = new BezelFiles() { PngFile = overlay_png_file, InfoFile = overlay_info_file };

            if (Program.SystemConfig.isOptSet("use_guns") && Program.SystemConfig.getOptBoolean("use_guns") && Misc.IsSindenLightGunConnected())
                return CreateSindenBorderBezel(ret);

            return ret;
        }

        public static BezelFiles CreateSindenBorderBezel(BezelFiles input, ScreenResolution resolution = null)
        {
            if (input == null)
                input = new BezelFiles();

            if (resolution == null)
                resolution = ScreenResolution.CurrentResolution;

            try
            {
                Dictionary<string, string> LightgunOptions = new Dictionary<string, string>();

                var px = System.Diagnostics.Process.GetProcessesByName("Lightgun").FirstOrDefault();
                if (px != null)
                {
                    var cmd = px.GetProcessCommandline().SplitCommandLine().FirstOrDefault();
                    if (cmd != null)
                    {
                        cmd = cmd.Replace("\"", "") + ".config";
                        if (File.Exists(cmd))
                        {
                            foreach (var setting in File.ReadAllText(cmd).ExtractStrings("<add ", "/>"))
                            {
                                var key = setting.ExtractString("key=\"", "\"");
                                var value = setting.ExtractString("value=\"", "\"");

                                LightgunOptions[key] = value;
                            }
                        }
                    }
                }

                bool showPrimaryBorder = LightgunOptions.ContainsKey("chkShowPrimaryBorder") ? LightgunOptions["chkShowPrimaryBorder"].ToInteger() != 0 : true;
                bool showSecondaryBorder = LightgunOptions.ContainsKey("chkShowSecondaryBorder") ? LightgunOptions["chkShowSecondaryBorder"].ToInteger() != 0 : false;

                int primaryBorderWidth = LightgunOptions.ContainsKey("txtPrimaryBorderWidth") ? LightgunOptions["txtPrimaryBorderWidth"].ToInteger() : 2;
                int secondaryBorderWidth = LightgunOptions.ContainsKey("txtSecondaryBorderWidth") ? LightgunOptions["txtSecondaryBorderWidth"].ToInteger() : 0;

                Color primaryColor = Color.White;

                try
                {
                    primaryColor = Color.FromArgb(LightgunOptions["txtColorPrimaryR"].ToInteger(), LightgunOptions["txtColorPrimaryG"].ToInteger(), LightgunOptions["txtColorPrimaryB"].ToInteger());
                }
                catch { }

                Color secondaryColor = Color.Black;

                try
                {
                    secondaryColor = Color.FromArgb(LightgunOptions["txtColorSecondaryR"].ToInteger(), LightgunOptions["txtColorSecondaryG"].ToInteger(), LightgunOptions["txtColorSecondaryB"].ToInteger());
                }
                catch { }

                using (Image img = File.Exists(input.PngFile) ? Image.FromFile(input.PngFile) : null)
                {
                    int resX = img != null ? img.Width : resolution.Width;
                    int resY = img != null ? img.Height : resolution.Height;

                    var fn = "sinden.bezel." + resX + "x" + resY + ".png";
                    if (File.Exists(input.PngFile))
                    {
                        var f = Path.GetFileNameWithoutExtension(input.PngFile);
                        var d = Path.GetFileName(Path.GetDirectoryName(input.PngFile));

                        fn = "sinden.bezel." + d + "." + f + "." + resX + "x" + resY + ".png";
                    }

                    string output_png_file = Path.Combine(Path.GetTempPath(), fn);

                    int primaryBorderSize = (resY * primaryBorderWidth) / 100;
                    int secondaryBorderSize = (resY * secondaryBorderWidth) / 100;

                    int borderSize = (showSecondaryBorder ? secondaryBorderSize : 0) + (showPrimaryBorder ? primaryBorderSize : 0);

                    using (Bitmap bmp = new Bitmap(resX, resY))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            var rect = new Rectangle(0, 0, resX, resY);
                            rect.Inflate(-borderSize, -borderSize);

                            if (showPrimaryBorder)
                            {
                                g.ExcludeClip(rect);
                                g.Clear(primaryColor);
                                g.ResetClip();
                            }

                            if (showSecondaryBorder)
                            {
                                if (showPrimaryBorder)
                                    rect.Inflate(primaryBorderSize, primaryBorderSize);

                                g.ExcludeClip(rect);
                                g.Clear(secondaryColor);
                                g.ResetClip();

                                if (showPrimaryBorder)
                                    rect.Inflate(-primaryBorderSize, -primaryBorderSize);
                            }

                            if (img != null)
                            {
                                g.DrawImage(img, rect);

                                rect.Inflate(1, 1);
                                rect.Width--;
                                rect.Height--;
                                g.DrawRectangle(Pens.Black, rect);
                            }
                        }

                        bmp.Save(output_png_file, System.Drawing.Imaging.ImageFormat.Png);
                        input.PngFile = output_png_file;
                    }

                    if (input.BezelInfos == null)
                        input.BezelInfos = new BezelInfo();

                    input.BezelInfos.opacity = 1;

                    if (input.BezelInfos.width.GetValueOrDefault() == 0)
                        input.BezelInfos.width = resX;

                    if (input.BezelInfos.height.GetValueOrDefault() == 0)
                        input.BezelInfos.height = resY;

                    if (input.BezelInfos.top.GetValueOrDefault() < borderSize)
                        input.BezelInfos.top = borderSize;

                    if (input.BezelInfos.top.GetValueOrDefault() < borderSize)
                        input.BezelInfos.top = borderSize;

                    if (input.BezelInfos.left.GetValueOrDefault() < borderSize)
                        input.BezelInfos.left = borderSize;

                    if (input.BezelInfos.bottom.GetValueOrDefault() < borderSize)
                        input.BezelInfos.bottom = borderSize;

                    if (input.BezelInfos.right.GetValueOrDefault() < borderSize)
                        input.BezelInfos.right = borderSize;
                }
            }
            catch { }

            return input;
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
