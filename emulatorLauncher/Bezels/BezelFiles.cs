using System;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Runtime.Serialization;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher;
using System.Collections.Generic;

namespace EmulatorLauncher
{
    partial class BezelFiles
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
            "{userpath}\\{bezel}\\games\\{gamesystem}\\{rom}.png",              // decorations\thebezelproject\games\mame\1942.png
            "{systempath}\\{bezel}\\games\\{gamesystem}\\{rom}.png",            // system\decorations\thebezelproject\games\mame\1942.png
            "{userpath}\\{bezel}\\games\\{rom}.png",                            // decorations\thebezelproject\games\1942.png
            "{systempath}\\{bezel}\\games\\{rom}.png",                          // system\decorations\thebezelproject\games\1942.png

            // Bezels with same IndexedRomName -> Uses * instead of rom name
            "{userpath}\\{bezel}\\games\\{gamesystem}\\*.png",                  // decorations\thebezelproject\games\mame\*.png
            "{systempath}\\{bezel}\\games\\{gamesystem}\\*.png",                // system\decorations\thebezelproject\games\mame\*.png
            "{userpath}\\{bezel}\\games\\*.png",                                // decorations\thebezelproject\games\1942.png
            "{systempath}\\{bezel}\\games\\*.png",                              // system\decorations\thebezelproject\games\1942.png

            // System bezels
            "{userpath}\\{bezel}\\systems\\{system}.png",                       // decorations\thebezelproject\systems\mame.png
            "{systempath}\\{bezel}\\systems\\{system}.png",                     // system\decorations\thebezelproject\systems\mame.png
            "{userpath}\\{bezel}\\default.png",                                 // decorations\thebezelproject\default.png
            "{systempath}\\{bezel}\\default.png",                               // system\decorations\thebezelproject\default.png
            
            // Default_unglazed
            "{userpath}\\default_unglazed\\systems\\{system}.png",              // decorations\default_unglazed\systems\mame.png
            "{systempath}\\default_unglazed\\systems\\{system}.png",            // system\decorations\default_unglazed\systems\mame.png

            // Default
            "{userpath}\\default\\systems\\{system}.png",                       // decorations\default\systems\mame.png
            "{systempath}\\default\\systems\\{system}.png"                      // system\decorations\default\systems\mame.png
        };

        private static bool IsPng(string filename)
        {
            try
            {
                // Open the file in binary mode and read the first 8 bytes
                using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[8];
                    fileStream.Read(header, 0, 8);

                    // Check if the bytes match the PNG file signature
                    if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static string FindBezel(string overlayUser, string overlaySystem, string bezel, string systemName, string romName, string perGameSystem, string emulator)
        {
            string indexedRomName = romName.AsIndexedRomName();

            // specific cases (eg for 3ds (depending on layout))
            systemName = GetSpecificBezels(systemName, emulator);

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
                    .Replace("{gamesystem}", perGameSystem ?? "")
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
                {
                    // Check if it's a real PNG file, or a file containing the relative path to another png
                    if (!IsPng(result) && new FileInfo(result).Length < 1024)
                    {
                        try
                        {
                            string link = File.ReadAllText(result);
                            if (link[0] == '.')
                            {
                                string relative = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(result)), link));
                                if (!File.Exists(relative) || !IsPng(relative))
                                    continue;

                                result = relative;
                            }
                        }
                        catch 
                        {
                            continue;
                        }
                    }

                    return Path.GetFullPath(result);
                }
            }

            return null;
        }
      
        public static BezelFiles GetBezelFiles(string systemName, string rom, ScreenResolution resolution, string emulator = null)
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
                {
                    var customBz = new BezelFiles() { PngFile = Program.CurrentGame.Bezel };

                    if (Program.SystemConfig.getOptBoolean("tattoo"))
                    {
                        string newPngFile = Program.CurrentGame.Bezel.Replace(".png", "_tattoo.png");
                        customBz.PngFile = GetTattooImage(customBz.PngFile, newPngFile, emulator);
                    }

                    if (Program.SystemConfig.getOptBoolean("use_guns") && RawLightgun.IsSindenLightGunConnected())
                        return CreateSindenBorderBezel(customBz);
                    else
                        customBz.BezelInfos = BezelInfo.FromImage(customBz.PngFile);

                    return customBz;
                }

                bezel = "thebezelproject";
            }

            if (string.IsNullOrEmpty(bezel))
                return null;

            if (bezel == "none")
            {
                if (Program.SystemConfig.getOptBoolean("use_guns") && RawLightgun.IsSindenLightGunConnected())
                    return CreateSindenBorderBezel(null, resolution);

                return null;
            }

            float screenRatio = (float)resX / (float)resY;
            if (screenRatio < 1.4)
            {
                if (Program.SystemConfig.getOptBoolean("use_guns") && RawLightgun.IsSindenLightGunConnected())
                    return CreateSindenBorderBezel(null, resolution);

                return null;
            }

            string perGameSystem = systemName;

            // Special case for FBNEO with TheBezelProject : Use mame decorations if installed
            if (systemName == "fbneo" && bezel == "thebezelproject" && Directory.Exists(Path.Combine(overlayUser, bezel, "games", "mame")) && !Directory.Exists(Path.Combine(overlayUser, bezel, "games", "fbneo")))
                perGameSystem = "mame";

            string overlay_png_file = FindBezel(overlayUser, overlaySystem, bezel, systemName, Path.GetFileNameWithoutExtension(rom), perGameSystem, emulator);
            if (string.IsNullOrEmpty(overlay_png_file))
            {
                if (Program.SystemConfig.getOptBoolean("use_guns") && RawLightgun.IsSindenLightGunConnected())
                    return CreateSindenBorderBezel(null, resolution);

                return null;
            }

            // If default bezel and game is vertcial, switch to default vertical bezel
            if (systemName == "mame" || systemName == "fbneo")
            {
                var romInfo = Mame64Generator.MameGameInfo.GetGameInfo(Path.GetFileNameWithoutExtension(rom));

                if (romInfo != null && romInfo.Vertical)
                {
                    if ((overlay_png_file.Contains("default\\systems") || overlay_png_file.Contains("default_unglazed\\systems")) && overlay_png_file.EndsWith(systemName + ".png"))
                    {
                        bezel = "arcade_vertical_default";
                        overlay_png_file = FindBezel(overlayUser, overlaySystem, bezel, systemName, Path.GetFileNameWithoutExtension(rom), perGameSystem, emulator);
                    }
                }
            }

            string overlay_info_file = Path.ChangeExtension(overlay_png_file, ".info");
            if (!File.Exists(overlay_info_file))
                overlay_info_file = null;

            var ret = new BezelFiles() { PngFile = overlay_png_file, InfoFile = overlay_info_file };

            if (Program.SystemConfig.getOptBoolean("tattoo"))
            {
                string newPngFile = overlay_png_file.Replace(".png", "_tattoo.png");
                ret.PngFile = GetTattooImage(overlay_png_file, newPngFile, emulator);
            }

            if (Program.SystemConfig.getOptBoolean("use_guns") && RawLightgun.IsSindenLightGunConnected())
                return CreateSindenBorderBezel(ret);
            else if (overlay_info_file == null)
                ret.BezelInfos = BezelInfo.FromImage(ret.PngFile);

            return ret;
        }

        private static string GetSpecificBezels(string system, string emulator)
        {
            string core = Program.SystemConfig["core"];
            if (system == "3ds")
            {
                switch (emulator)
                {
                    case "citra":
                    case "citra-canary":
                        if (Program.SystemConfig.isOptSet("citraqt_layout_option") && Program.SystemConfig["citraqt_layout_option"] == "3")
                            return "3ds_side_by_side";
                        else if (Program.SystemConfig.isOptSet("citraqt_layout_option") && Program.SystemConfig["citraqt_layout_option"] == "2")
                            return "3ds_hybrid";
                        break;
                    case "libretro":
                        if (Program.SystemConfig.isOptSet("citra_layout_option") && Program.SystemConfig["citra_layout_option"] == "Side by Side")
                            return "3ds_side_by_side";
                        else if (Program.SystemConfig.isOptSet("citra_layout_option") && Program.SystemConfig["citra_layout_option"] == "Large Screen, Small Screen")
                            return "3ds_lr_hybrid";
                        break;
                    case "lime3ds":
                        if (Program.SystemConfig.isOptSet("lime_layout_option") && Program.SystemConfig["lime_layout_option"] == "3")
                            return "3ds_side_by_side";
                        else if (Program.SystemConfig.isOptSet("lime_layout_option") && Program.SystemConfig["lime_layout_option"] == "2")
                            return "3ds_hybrid";
                        break;
                }
            }

            if (system == "nds")
            {
                switch (emulator)
                {
                    case "melonds":
                        if (Program.SystemConfig.isOptSet("melonds_screen_sizing") && (Program.SystemConfig["melonds_screen_sizing"] == "4" || Program.SystemConfig["melonds_screen_sizing"] == "5"))
                            return "nds_melonds_single_screen";
                        else if (Program.SystemConfig.isOptSet("melonds_screen_layout") && Program.SystemConfig["melonds_screen_layout"] == "2")
                            return "nds_melonds_side_by_side";
                        else if (Program.SystemConfig.isOptSet("melonds_screen_layout") && Program.SystemConfig["melonds_screen_layout"] == "3")
                            return "nds_melonds_hybrid";
                        else
                            return "nds_melonds";
                    case "bizhawk":
                        if (Program.SystemConfig.isOptSet("bizhawk_melonds_layout") && Program.SystemConfig["bizhawk_melonds_layout"] == "2")
                            return "nds_side_by_side";
                        else if (Program.SystemConfig.isOptSet("bizhawk_melonds_layout") && (Program.SystemConfig["bizhawk_melonds_layout"] == "3" || Program.SystemConfig["bizhawk_melonds_layout"] == "4"))
                            return "nds_single_screen";
                        break;
                    case "libretro":
                    {
                        switch (core)
                        {
                            case "melondsds":
                                if (Program.SystemConfig.isOptSet("melondsds_screen_layout") && (Program.SystemConfig["melondsds_screen_layout"] == "left-right" || Program.SystemConfig["melondsds_screen_layout"] == "right-left"))
                                    return "nds_side_by_side";
                                else if (Program.SystemConfig.isOptSet("melondsds_screen_layout") && (Program.SystemConfig["melondsds_screen_layout"] == "hybrid-top" || Program.SystemConfig["melondsds_screen_layout"] == "hybrid-bottom"))
                                    return "nds_lr_hybrid";
                                else if (Program.SystemConfig.isOptSet("melondsds_screen_layout") && (Program.SystemConfig["melondsds_screen_layout"] == "top" || Program.SystemConfig["melondsds_screen_layout"] == "bottom"))
                                    return "nds_single_screen";
                                break;
                            case "desmume":
                            case "desmume2015":
                                if (Program.SystemConfig.isOptSet("desmume_screens_layout") && (Program.SystemConfig["desmume_screens_layout"] == "left/right" || Program.SystemConfig["desmume_screens_layout"] == "right/left"))
                                    return "nds_side_by_side";
                                else if (Program.SystemConfig.isOptSet("desmume_screens_layout") && (Program.SystemConfig["desmume_screens_layout"] == "hybrid/top" || Program.SystemConfig["desmume_screens_layout"] == "hybrid/bottom"))
                                    return "nds_lr_desmume_hybrid";
                                else if (Program.SystemConfig.isOptSet("desmume_screens_layout") && (Program.SystemConfig["desmume_screens_layout"] == "top only" || Program.SystemConfig["desmume_screens_layout"] == "bottom only"))
                                    return "nds_single_screen";
                                break;
                            case "melonds":
                                if (Program.SystemConfig.isOptSet("melonds_screen_layout") && (Program.SystemConfig["melonds_screen_layout"] == "Left/Right" || Program.SystemConfig["melonds_screen_layout"] == "Right/Left"))
                                    return "nds_side_by_side";
                                else if (Program.SystemConfig.isOptSet("melonds_screen_layout") && (Program.SystemConfig["melonds_screen_layout"] == "Hybrid Top" || Program.SystemConfig["melonds_screen_layout"] == "duplicate"))
                                    return "nds_lr_hybrid";
                                else if (Program.SystemConfig.isOptSet("melonds_screen_layout") && (Program.SystemConfig["melonds_screen_layout"] == "Top Only" || Program.SystemConfig["melonds_screen_layout"] == "Bottom Only"))
                                    return "nds_single_screen";
                                break;
                            case "noods":
                                if (Program.SystemConfig.isOptSet("noods_screenArrangement") && Program.SystemConfig["noods_screenArrangement"] == "Horizontal")
                                    return "nds_side_by_side";
                                else if (Program.SystemConfig.isOptSet("noods_screenArrangement") && Program.SystemConfig["noods_screenArrangement"] == "Vertical")
                                    return "nds";
                                else if (Program.SystemConfig.isOptSet("noods_screenArrangement") && Program.SystemConfig["noods_screenArrangement"] == "Single Screen")
                                    return "nds_single_screen";
                                break;
                            }
                        break;
                    }
                }
            }
            return system;
        }

        public static BezelFiles CreateSindenBorderBezel(BezelFiles input, ScreenResolution resolution = null)
        {
            if (input == null)
                input = new BezelFiles();

            if (resolution == null)
                resolution = ScreenResolution.CurrentResolution;

            try
            {
                var conf = SindenLightgunConfiguration.GetConfiguration(Program.SystemConfig);

                bool showPrimaryBorder = conf.ShowPrimaryBorder;
                bool showSecondaryBorder = conf.ShowSecondaryBorder;
                float primaryBorderWidth = conf.PrimaryBorderWidth;
                float secondaryBorderWidth = conf.SecondaryBorderWidth;
                Color primaryColor = conf.PrimaryColor;
                Color secondaryColor = conf.SecondaryColor;

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

                    int primaryBorderSize = (int) ((resY * primaryBorderWidth) / 100.0f);
                    int secondaryBorderSize = (int) ((resY * secondaryBorderWidth) / 100.0f);

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

                    if (input.BezelInfos == null || !input.BezelInfos.IsValid())
                    {
                        input.BezelInfos = BezelInfo.FromImage(input.PngFile);
                        return input;
                    }

                    input.BezelInfos.opacity = 1;

                    if (input.BezelInfos.width.GetValueOrDefault() == 0)
                        input.BezelInfos.width = resX;

                    if (input.BezelInfos.height.GetValueOrDefault() == 0)
                        input.BezelInfos.height = resY;

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
        public static BezelInfo FromImage(string fileName)
        {
            if (!File.Exists(fileName))
                return new BezelInfo();

            const byte MIN_ALPHA = 235;

            var ret = new BezelInfo();

            try
            {
                using (Image img = Image.FromFile(fileName))
                {
                    RECT bounds = new RECT(-1, -1, -1, -1);

                    // Detect transparency in png file
                    using (var bmp = new Bitmap(img))
                    {
                        BitmapData dstData = bmp.LockBits(new Rectangle(0, 0, bmp.Size.Width, bmp.Size.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                        int y1 = bmp.Height / 2;

                        for (int x = 4; x < bmp.Size.Width; x++)
                        {
                            UInt32 color = (UInt32)Marshal.ReadInt32(dstData.Scan0, (dstData.Stride * y1) + (4 * x));
                            byte a = (byte)(color >> 24);
                            if (a < MIN_ALPHA)
                            {
                                bounds.left = x;
                                break;
                            }
                        }

                        for (int x = bmp.Size.Width - 4; x >= 0; x--)
                        {
                            UInt32 color = (UInt32)Marshal.ReadInt32(dstData.Scan0, (dstData.Stride * y1) + (4 * x));
                            byte a = (byte)(color >> 24);
                            if (a < MIN_ALPHA)
                            {
                                bounds.right = x + 1;
                                break;
                            }
                        }

                        int x1 = bmp.Width / 2;
                        for (int y = 0; y < bmp.Size.Height; y++)
                        {
                            UInt32 color = (UInt32)Marshal.ReadInt32(dstData.Scan0, (dstData.Stride * y) + (4 * x1));
                            byte a = (byte)(color >> 24);
                            if (a < MIN_ALPHA)
                            {
                                bounds.top = y;
                                break;
                            }
                        }

                        for (int y = bmp.Size.Height - 1; y >= 0; y--)
                        {
                            UInt32 color = (UInt32)Marshal.ReadInt32(dstData.Scan0, (dstData.Stride * y) + (4 * x1));
                            byte a = (byte)(color >> 24);
                            if (a < MIN_ALPHA)
                            {
                                bounds.bottom = y + 1;
                                break;
                            }
                        }

                        bmp.UnlockBits(dstData);
                    }

                    if (bounds.left < 0 || bounds.right < 0 || bounds.top < 0 || bounds.right < 0)
                        return new BezelInfo();

                    ret.width = img.Size.Width;
                    ret.height = img.Size.Height;
                    ret.left = bounds.left;
                    ret.top = bounds.top;
                    ret.bottom = img.Size.Height - bounds.bottom;
                    ret.right = img.Size.Width - bounds.right;
                    ret.opacity = 1;
                    ret.IsEstimated = true;
                }

                return ret;
            }
            catch { }

            return new BezelInfo();
        }

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

        public bool IsEstimated { get; private set; }
    }
}
