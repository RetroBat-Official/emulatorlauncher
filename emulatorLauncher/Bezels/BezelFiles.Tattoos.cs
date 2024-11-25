using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class BezelFiles
    {
        public static string GetTattooImage(string inputPng, string outputPng, string emulator)
        {
            SimpleLogger.Instance.Info("[GENERATOR] Tattoo enabled, fetching right tattoo file.");

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
                    {
                        SimpleLogger.Instance.Info("[GENERATOR] Tattoo file not found: " + tattooName);
                        return inputPng;
                    }
                    tattoo = Image.FromFile(tattooFile);
                }
            }
            catch 
            { 
                SimpleLogger.Instance.Info("[GENERATOR] Error loading tattoo file: " + tattooFile);
                return inputPng; 
            }

            Image back = Image.FromFile(inputPng);

            // Preserve the original DPI values of the tattoo before normalization
            float originalTattooDpiX = tattoo.HorizontalResolution;
            float originalTattooDpiY = tattoo.VerticalResolution;

            // Normalize both images to 96x96 DPI to avoid scaling issues
            if (back.HorizontalResolution != 96 || back.VerticalResolution != 96)
            {
                back = SetImageToStandardDpi(back, 96, 96);  // Set the background image to 96 DPI
            }

            if (tattoo.HorizontalResolution != 96 || tattoo.VerticalResolution != 96)
            {
                tattoo = SetImageToStandardDpi(tattoo, 96, 96);  // Set the tattoo image to 96 DPI
            }

            // Calculate resizing factor for the tattoo based on original DPI and normalized DPI
            float dpiScaleFactorX = originalTattooDpiX / 96.0f;
            float dpiScaleFactorY = originalTattooDpiY / 96.0f;

            // Resize the tattoo to account for its original DPI, so it doesn't appear too large
            int newTattooWidth = (int)(tattoo.Width / dpiScaleFactorX);
            int newTattooHeight = (int)(tattoo.Height / dpiScaleFactorY);
            tattoo = ResizeImage(tattoo, newTattooWidth, newTattooHeight);

            // Convert both images to RGBA
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
                        g.DrawImage(tattoo, w - (tw/2), margin);
                        break;
                    case "SE":
                        g.DrawImage(tattoo, w - (tw/2), h - (th/2) - margin);
                        break;
                    case "SW":
                        g.DrawImage(tattoo, 0, h - (th/2) - margin);
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

            SimpleLogger.Instance.Info("[GENERATOR] Tattoo file: " + tattooName);
            return outputPng;
        }

        static List<string> gbSystems = new List<string>() { "gb", "gbc" };
        static List<string> ngpSystems = new List<string>() { "ngp", "ngpc" };
        static List<string> jaguarSystems = new List<string>() { "jaguar", "jaguarcd" };
        static List<string> megadriveSystems = new List<string>() { "genesis", "megadrive", "megadrive-msu", "sega32x", "segacd" };
        static List<string> n64Systems = new List<string>() { "n64", "n64dd" };
        static List<string> nesSystems = new List<string>() { "fds", "nes" };
        static List<string> pceSystems = new List<string>() { "pcengine", "pcenginecd" };
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
            else if (gbSystems.Contains(system))
                system = "gb";
            else if (pceSystems.Contains(system))
                system = "pcengine";
            else if (jaguarSystems.Contains(system))
                system = "jaguar";
            else if (ngpSystems.Contains(system))
                system = "ngp";

            string ret = system;

            if (system == "3ds")
            {
                bool revert = Program.SystemConfig.getOptBoolean("gamepadbuttons");
                switch (emulator)
                {
                    case "libretro":
                        {
                            if (Program.SystemConfig["citra_analog_function"] == "C-Stick and Touchscreen Pointer" || !Program.SystemConfig.isOptSet("citra_analog_function"))
                                ret = revert ? "3ds_stylus_cstick_revert" : "3ds_stylus_cstick";
                            else if (Program.SystemConfig["citra_analog_function"] == "Touchscreen Pointer")
                                ret = revert ? "3ds_stylus_revert" : "3ds_stylus";
                            else if (Program.SystemConfig["citra_analog_function"] == "C-Stick")
                                ret = revert ? "3ds_revert" : "3ds";
                            break;
                        }
                    case "citra":
                    case "citra-canary":
                    case "lime3ds":
                        if (Program.SystemConfig["n3ds_motion"] == "sdl")
                            ret = revert ? "3ds_stylus_cstick_revert" : "3ds_stylus_cstick";
                        else
                            ret = revert ? "3ds_revert" : "3ds";
                        break;
                    case "bizhawk":
                        if (Program.SystemConfig["bizhawk3ds_analog_function"] == "C-Stick and Touchscreen Pointer" || !Program.SystemConfig.isOptSet("bizhawk3ds_analog_function"))
                            ret = revert ? "3ds_stylus_cstick_revert" : "3ds_stylus_cstick";
                        else if (Program.SystemConfig["bizhawk3ds_analog_function"] == "Touchscreen Pointer")
                            ret = revert ? "3ds_stylus_revert" : "3ds_stylus";
                        else if (Program.SystemConfig["bizhawk3ds_analog_function"] == "C-Stick")
                            ret = revert ? "3ds_revert" : "3ds";
                        break;
                }
            }
            else if (system == "dreamcast")
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
            else if (system == "gamegear")
            {
                ret = "mastersystem";
            }
            else if (system == "gb")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "gambatte":
                                ret = "gb_gambatte";
                                break;
                        }
                        break;
                    case "mgba":
                        ret = "unknown";
                        break;
                    case "mesen":
                    case "mednafen":
                        ret = "gb_turbo";
                        break;
                }
            }
            else if (system == "gba")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "mgba":
                                ret = "gba_turbo";
                                break;
                            case "mednafen_gba":
                                ret = "gba_solar";
                                break;
                            case "gpsp":
                                ret = "gba_gpsp";
                                break;
                        }
                        break;
                    case "mgba":
                    case "nosgba":
                        ret = "unknown";
                        break;
                }
            }
            else if (system == "jaguar")
            {
                switch (emulator)
                {
                    case "bizhawk":
                    case "libretro":
                    case "phoenix":
                        ret = "jaguar";
                        break;
                    case "bigpemu":
                        ret = "jaguar_bigpemu";
                        break;
                }
            }
            else if (system == "lynx")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "handy":
                                ret = "lynx_lr_handy";
                                break;
                        }
                        break;
                    case "mednafen":
                        ret = "lynx_mednafen";
                        break;
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
                    case "libretro":
                        if (Program.SystemConfig.isOptSet("lr_n64_buttons") && Program.SystemConfig["lr_n64_buttons"] == "xbox")
                            ret = "n64_xbox";
                        else
                            ret = "n64";
                        break;
                    case "ares":
                    case "bizhawk":
                        if (Program.SystemConfig.isOptSet("ares64_inputprofile") && Program.SystemConfig["ares64_inputprofile"] == "zr")
                            ret = "n64-standalone_zr";
                        else if (Program.SystemConfig.isOptSet("ares64_inputprofile") && Program.SystemConfig["ares64_inputprofile"] == "xbox")
                            ret = "n64_xbox";
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
                            else if (profile == "xbox")
                                ret = "n64_xbox";
                            break;
                        }
                        else
                            ret = "n64-standalone";
                        break;
                }
            }
            else if (system == "nds")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "melondsds":
                                ret = "nds_melondsds";
                                break;
                            case "desmume":
                            case "desmume2015":
                                ret = "nds";
                                break;
                            case "melonds":
                                ret = "nds_melonds";
                                break;
                            case "noods":
                                ret = "nds_noods";
                                break;
                        }
                        break;
                    case "melonds":
                        if (Program.SystemConfig.getOptBoolean("melonds_leftstick"))
                            ret = "nds_melonds_standalone_ls";
                        else
                            ret = "nds_melonds_standalone";
                        break;
                    case "bizhawk":
                        if (Program.SystemConfig.getOptBoolean("bizhawk_nds_mouse"))
                            ret = "nds_bizhawk_mouse";
                        else
                            ret = "nds_bizhawk";
                        break;
                }
            }
            else if (system == "neogeo")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "fbneo":
                                {
                                    if (Program.SystemConfig.isOptSet("fbneo_controller"))
                                    {
                                        string layout = Program.SystemConfig["fbneo_controller"];
                                        if (layout == "5")
                                            ret = "neogeo_classic";
                                        else if (layout == "517")
                                            ret = "neogeo_modern";
                                        else if (layout == "261")
                                            ret = "neogeo_6buttonpad";
                                    }
                                    else
                                        ret = "neogeo_classic";
                                    break;
                                }
                            case "fbalpha":
                                {
                                    if (Program.SystemConfig.isOptSet("fba_controller"))
                                    {
                                        string layout = Program.SystemConfig["fba_controller"];
                                        if (layout == "5")
                                            ret = "neogeo_fbalpha";
                                        else if (layout == "517")
                                            ret = "neogeo_fbalpha_modern";
                                    }
                                    else
                                        ret = "neogeo_fbalpha";
                                    break;
                                }
                            case "fbalpha2012_neogeo":
                                ret = "fbalpha2012_neogeo";
                                break;
                            case "geolith":
                                ret = "neogeo_geolith";
                                break;
                        }
                        break;
                    case "raine":
                    case "mame64":
                        ret = "unknown";
                        break;
                }
            }
            else if (system == "neogeocd")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "fbneo":
                                {
                                    if (Program.SystemConfig.isOptSet("fbneo_controller"))
                                    {
                                        string layout = Program.SystemConfig["fbneo_controller"];
                                        if (layout == "5")
                                            ret = "neogeocd";
                                        else if (layout == "517")
                                            ret = "neogeocd_modern";
                                        else if (layout == "261")
                                            ret = "neogeocd_6buttonpad";
                                    }
                                    else
                                        ret = "neogeocd";
                                    break;
                                }
                            case "neocd":
                                ret = "neogeocd";
                                break;
                        }
                        break;
                    case "raine":
                        ret = "unknown";
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
            else if (system == "ngp")
            {
                switch (emulator)
                {
                    case "mednafen":
                        ret = "ngp_turbo";
                        break;
                }
            }
            else if (system == "pcengine")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "mednafen_pce":
                            case "mednafen_pce_fast":
                                if (Program.SystemConfig.getOptBoolean("pce_6button"))
                                    ret = "pcengine_6buttons";
                                else
                                    ret = "pcengine";
                                break;
                            case "fbneo":
                                ret = "pcengine_simple";
                                break;
                        }
                        break;
                    case "mednafen":
                        ret = "pcengine_6buttons";
                        break;
                    case "mesen":
                        if (Program.SystemConfig["mesen_controller1"] == "PceAvenuePad6")
                            ret = "pcengine_simple_6buttons";
                        else
                            ret = "pcengine_simple";
                        break;
                    case "ares":
                        ret = "pcengine_simple";
                        break;
                    case "bizhawk":
                        ret = "pcengine_bizhawk";
                        break;
                    case "magicengine":
                        ret = "pcengine_simple_6buttons";
                        break;
                }
            }
            else if (system == "pcfx")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "mednafen_pcfx":
                                ret = "pcfx";
                                break;
                        }
                        break;
                    case "mednafen":
                        ret = "pcfx";
                        break;
                    case "bizhawk":
                        ret = "pcfx_bizhawk";
                        break;
                }
            }
            else if (system == "psx")
            {
                if (Program.SystemConfig.isOptSet("psx_triggerswap"))
                    ret = "psx_triggers_rstick";
            }
            else if (system == "ps2")
            {
                if (Program.SystemConfig.isOptSet("pcsx2_triggersdriving") && !string.IsNullOrEmpty(Program.SystemConfig["pcsx2_triggersdriving"]))
                {
                    string triggersDriving = Program.SystemConfig["pcsx2_triggersdriving"];
                    switch (triggersDriving)
                    {
                        case "square_cross":
                            ret = "psx_triggers_square_cross";
                            break;
                        case "righty":
                            ret = "psx_triggers_rstick";
                            break;
                        case "lefty":
                            ret = "psx_triggers_lstick";
                            break;
                    }
                }
                else
                    ret = "psx";
            }
            else if (system == "ps3")
            {
                ret = "psx";
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
            else if (system == "supegrafx")
            {
                switch (emulator)
                {
                    case "libretro":
                        switch (core)
                        {
                            case "mednafen_supergrafx":
                                if (Program.SystemConfig.getOptBoolean("sgx_6button"))
                                    ret = "pcengine_6buttons";
                                else
                                    ret = "pcengine";
                                break;
                            case "fbneo":
                                ret = "pcengine_simple";
                                break;
                        }
                        break;
                    case "mednafen":
                        ret = "pcengine_6buttons";
                        break;
                    case "mesen":
                        if (Program.SystemConfig["mesen_controller1"] == "PceAvenuePad6")
                            ret = "pcengine_simple_6buttons";
                        else
                            ret = "pcengine_simple";
                        break;
                    case "ares":
                        ret = "pcengine_simple";
                        break;
                    case "bizhawk":
                        ret = "pcengine_bizhawk";
                        break;
                    case "magicengine":
                        ret = "pcengine_simple_6buttons";
                        break;
                }
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

        // Helper method to set image DPI without resizing it
        private static Bitmap SetImageToStandardDpi(Image image, float targetDpiX, float targetDpiY)
        {
            Bitmap result = new Bitmap(image.Width, image.Height);
            result.SetResolution(targetDpiX, targetDpiY);

            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(image, 0, 0, image.Width, image.Height);
            }

            return result;
        }
    }
}
