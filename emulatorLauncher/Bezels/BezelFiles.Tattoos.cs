using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class BezelFiles
    {
        public static string GetTattooImage(string inputPng, string outputPng, string emulator)
        {
            string tattooFile = "";
            Image tattoo = null;
            string system = Program.SystemConfig["system"];
            string core = Program.SystemConfig["core"];

            string tattooName = GetTattooName(system, core, emulator);

            try
            {
                if (Program.SystemConfig.getOptBoolean("tattoo"))
                {
                    string rom = Program.SystemConfig["rom"];
                    string gameName = rom != null ? Path.GetFileNameWithoutExtension(rom) : "system";
                    tattooFile = Path.Combine(Program.AppConfig.GetFullPath("tattoos"), "games", gameName + ".png");
                    if (!File.Exists(tattooFile))
                        tattooFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tattoos", "games", gameName + ".png");
                    if (!File.Exists(tattooFile))
                        tattooFile = Path.Combine(Program.AppConfig.GetFullPath("tattoos"), "default", tattooName);
                    if (!File.Exists(tattooFile))
                        tattooFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tattoos", "default", tattooName);
                    if (!File.Exists(tattooFile))
                        return inputPng;
                    tattoo = Image.FromFile(tattooFile);
                }
            }
            catch
            {
                Console.Error.WriteLine($"Error opening controller overlay: {tattooFile}");
                return inputPng;
            }

            Image back = Image.FromFile(inputPng);
            back = ConvertToRgba(back);
            tattoo = ConvertToRgba(tattoo);

            var backSize = FastImageSize(inputPng);
            var tattooSize = FastImageSize(tattooFile);
            int w = backSize.Item1, h = backSize.Item2;
            int tw = tattooSize.Item1, th = tattooSize.Item2;

            if (Program.SystemConfig.isOptSet("resize_tattoo") && Program.SystemConfig.getOptBoolean("resize_tattoo"))
            {
                if (tw > w || th > h)
                {
                    float pcent = (float)w / tw;
                    th = (int)(th * pcent);
                    tattoo = ResizeImage(tattoo, w, th);
                }
            }
            else
            {
                int twtemp = (int)((225.0 / 1920) * w);
                float pcent = (float)twtemp / tw;
                th = (int)(th * pcent);
                tattoo = ResizeImage(tattoo, twtemp, th);
                tw = twtemp;
            }

            Bitmap tattooCanvas = new Bitmap(back.Width, back.Height);
            using (Graphics g = Graphics.FromImage(tattooCanvas))
            {
                g.Clear(Color.Transparent);
            }

            int margin = (int)((20.0 / 1080) * h);
            string corner = Program.SystemConfig.isOptSet("tattoo_corner") ? Program.SystemConfig["tattoo_corner"] : "NW";

            using (Graphics g = Graphics.FromImage(tattooCanvas))
            {
                switch (corner)
                {
                    case "NE":
                        g.DrawImage(tattoo, w - tw, margin);
                        break;
                    case "SE":
                        g.DrawImage(tattoo, w - tw, h - th - margin);
                        break;
                    case "SW":
                        g.DrawImage(tattoo, 0, h - th - margin);
                        break;
                    default: // NW
                        g.DrawImage(tattoo, 0, margin);
                        break;
                }
            }

            Bitmap finalImage = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(back, 0, 0);
                g.DrawImage(tattooCanvas, 0, 0);
            }

            finalImage.Save(outputPng, ImageFormat.Png);

            return outputPng;
        }

        static List<string> megadriveSystems = new List<string>() { "megadrive", "megadrive-msu", "sega32x", "segacd" };
        static List<string> n64Systems = new List<string>() { "n64", "n64dd" };
        static List<string> nesSystems = new List<string>() { "fds", "nes" };
        static List<string> snesSystems = new List<string>() { "satellaview", "snes", "snes-msu1", "sgb", "sufami" };
        static List<string> md3buttonsLibretro = new List<string>() { "257", "1025", "1537", "773", "2" };
        private static string GetTattooName(string system, string core, string emulator)
        {
            if (megadriveSystems.Contains(system))
                system = "megadrive";
            else if (nesSystems.Contains(system))
                system = "nes";
            else if (snesSystems.Contains(system))
                system = "snes";
            else if (n64Systems.Contains(system))
                system = "n64";

            string ret = system;

            if (system == "dreamcast")
            {
                if (Program.SystemConfig.getOptBoolean("dreamcast_use_shoulders"))
                    ret = "dreamcast_lr";
            }
            else if (system == "gamecube" || system == "gc")
            {
                if (emulator == "dolphin" && Program.SystemConfig.getOptBoolean("gamecubepad0"))
                    ret = "unknown";
                else if (Program.SystemConfig.isOptSet("gamecube_buttons"))
                {
                    switch (Program.SystemConfig["gamecube_buttons"])
                    {
                        case "position":
                            ret = "gamecube_position";
                            break;
                        case "xbox":
                            ret = "gamecube_xbox";
                            break;
                        case "reverse_ab":
                            ret = "gamecube_xy";
                            break;
                    }
                }
            }
            else if (system == "mastersystem")
            {
                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                    ret = "mastersystem_rotate";
            }
            else if (system == "megadrive")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "genesis_plus_gx":
                            case "genesis_plus_gx_wide":
                                {
                                    bool buttons3 = md3buttonsLibretro.Contains(Program.SystemConfig["genesis_plus_gx_controller"]);
                                    if (buttons3)
                                        ret = "megadrive_3buttons";
                                    else if (!buttons3 && Program.SystemConfig.isOptSet("megadrive_control_layout"))
                                    {
                                        if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                                            ret = "megadrive_lr_zc";
                                        else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                                            ret = "megadrive_lr_yz";
                                    }
                                }
                                break;
                            case "picodrive":
                                {
                                    bool buttons3 = Program.SystemConfig.getOptBoolean("md_3buttons");
                                    if (buttons3)
                                        ret = "megadrive_3buttons";
                                    else if (!buttons3 && Program.SystemConfig.isOptSet("megadrive_control_layout"))
                                    {
                                        if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                                            ret = "megadrive_lr_zc";
                                        else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                                            ret = "megadrive_lr_yz";
                                    }
                                }
                                break;
                            case "fbneo":
                                {
                                    if (Program.SystemConfig.isOptSet("megadrive_control_layout"))
                                    {
                                        if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                                            ret = "megadrive_lr_zc";
                                        else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                                            ret = "megadrive_lr_yz";
                                    }
                                }
                                break;
                        }
                        break;
                    case "mednafen":
                        {
                            bool buttons3 = Program.SystemConfig["mednafen_controller_type"] == "gamepad" || Program.SystemConfig["mednafen_controller_type"] == "gamepad2";
                            if (buttons3)
                                ret = "megadrive_3buttons";
                            else if (!buttons3 && Program.SystemConfig.isOptSet("megadrive_control_layout"))
                            {
                                if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                                    ret = "megadrive_lr_zc";
                                else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                                    ret = "megadrive_lr_yz";
                            }
                        }
                        break;
                    case "kega-fusion":
                        {
                            if (Program.SystemConfig.isOptSet("megadrive_control_layout"))
                            {
                                if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                                    ret = "megadrive_lr_zc";
                                else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                                    ret = "megadrive_lr_yz";
                            }
                        }
                        break;
                    case "bizhawk":
                    case "jgenesis":
                        {
                            bool buttons3 = Program.SystemConfig.getOptBoolean("md_3buttons");
                            if (buttons3)
                                ret = "megadrive_3buttons";
                            else if (!buttons3 && Program.SystemConfig.isOptSet("megadrive_control_layout"))
                            {
                                if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                                    ret = "megadrive_lr_zc";
                                else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                                    ret = "megadrive_lr_yz";
                            }
                        }
                        break;
                }
            }
            else if (system == "n64")
            {
                switch (emulator)
                {
                    case "ares":
                    case "bizhawk":
                        if (Program.SystemConfig.isOptSet("ares64_inputprofile") && Program.SystemConfig["ares64_inputprofile"] == "zr")
                            ret = "n64-standalone_zr";
                        else
                            ret = "n64-standalone";
                        break;
                    case "mupen64":
                    case "simple64":
                        if (Program.SystemConfig.isOptSet("mupen64_inputprofile1") && !string.IsNullOrEmpty(Program.SystemConfig["mupen64_inputprofile1"]))
                        {
                            string profile = Program.SystemConfig["mupen64_inputprofile1"];
                            if (profile == "c_face_zl")
                                ret = "n64-standalone_face_zl";
                            else if (profile == "c_stick")
                                ret = "n64-standalone_zr";
                            else if (profile == "c_face")
                                ret = "n64-standalone_face_zr";
                            break;
                        }
                        else
                            ret = "n64-standalone";
                        break;
                }
            }
            else if (system == "nes")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "fceumm":
                            case "nestopia":
                                if (Program.SystemConfig.getOptBoolean("rotate_buttons") && Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                                    ret = "nes_rotate_turbo";
                                else if (Program.SystemConfig.getOptBoolean("rotate_buttons") && !Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                                    ret = "nes_rotate";
                                else if (!Program.SystemConfig.getOptBoolean("rotate_buttons") && Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                                    ret = "nes_turbo";
                                break;
                            case "mesen":
                                bool turbo = Program.SystemConfig["mesen_nes_turbo"] != "Disabled" || !Program.SystemConfig.isOptSet("mesen_nes_turbo");
                                if (Program.SystemConfig.getOptBoolean("rotate_buttons") && turbo)
                                    ret = "nes_rotate_turbo";
                                else if (Program.SystemConfig.getOptBoolean("rotate_buttons") && !turbo)
                                    ret = "nes_rotate";
                                else if (!Program.SystemConfig.getOptBoolean("rotate_buttons") && turbo)
                                    ret = "nes_turbo";
                                break;
                        }
                        break;
                    case "mednafen":
                    case "mesen":
                        if (Program.SystemConfig.getOptBoolean("rotate_buttons") && Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                            ret = "nes_rotate_turbo";
                        else if (Program.SystemConfig.getOptBoolean("rotate_buttons") && !Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                            ret = "nes_rotate";
                        else if (!Program.SystemConfig.getOptBoolean("rotate_buttons") && Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                            ret = "nes_turbo";
                        break;
                    case "ares":
                    case "bizhawk":
                    case "jgenesis":
                        if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                            ret = "nes_rotate";
                        break;
                }
            }
            else if (system == "saturn")
            {
                bool switchTriggers = Program.SystemConfig.getOptBoolean("saturn_invert_triggers");
                if (Program.SystemConfig.isOptSet("saturn_padlayout") && !string.IsNullOrEmpty(Program.SystemConfig["saturn_padlayout"]))
                {
                    switch (Program.SystemConfig["saturn_padlayout"])
                    {
                        case "lr_yz":
                            if (switchTriggers)
                                ret = "saturn_lr_yz_invert_triggers";
                            else
                                ret = "saturn_lr_yz";
                            break;
                        case "lr_xz":
                            if (switchTriggers)
                                ret = "saturn_lr_xz_invert_triggers";
                            else
                                ret = "saturn_lr_xz";
                            break;
                        case "lr_zc":
                            if (switchTriggers)
                                ret = "saturn_invert_triggers";
                            break;
                    }
                }

            }
            else if (system == "snes")
            {
                if (Program.SystemConfig.getOptBoolean("buttonsInvert"))
                    ret = "snes_invert";
            }
            
            return ret + ".png";
        }

        private static Image ConvertToRgba(Image image)
        {
            Bitmap bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(image, 0, 0);
            }
            return bmp;
        }

        private static Image ResizeImage(Image image, int width, int height)
        {
            Bitmap resized = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, width, height);
            }
            return resized;
        }

        private static Tuple<int, int> FastImageSize(string filePath)
        {
            using (Image img = Image.FromFile(filePath))
            {
                return Tuple.Create(img.Width, img.Height);
            }
        }
    }
}
