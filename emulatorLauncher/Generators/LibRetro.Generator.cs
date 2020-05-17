using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher.libRetro
{
    class LibRetroGenerator : Generator
    {
        public string RetroarchPath { get; set; }
        public string RetroarchCorePath { get; set; }

        public LibRetroGenerator()
        {
            RetroarchPath = AppConfig.GetFullPath("retroarch");

            RetroarchCorePath = AppConfig.GetFullPath("retroarch.cores");
            if (string.IsNullOrEmpty(RetroarchCorePath))
                RetroarchCorePath = Path.Combine(RetroarchPath, "cores");
        }

        private void ConfigureCoreOptions(string system, string core)
        {
            var coreSettings = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"));

            if (core == "atari800")
            {
                if (system == "atari800")
                {
                    coreSettings["atari800_system"] = "800XL (64K)";
                    coreSettings["RAM_SIZE"] = "64";
                    coreSettings["STEREO_POKEY"] = "1";
                    coreSettings["BUILTIN_BASIC"] = "1";
                }
                else
                {
                    coreSettings["atari800_system"] = "5200";
                    coreSettings["RAM_SIZE"] = "16";
                    coreSettings["STEREO_POKEY"] = "0";
                    coreSettings["BUILTIN_BASIC"] = "0";
                }
            }

            if (core == "bluemsx")
            {
                coreSettings["bluemsx_overscan"] = "enabled";

                if (system == "colecovision")
                    coreSettings["bluemsx_msxtype"] = "ColecoVision";
                else if (system == "msx1")
                    coreSettings["bluemsx_msxtype"] = "MSX";
                else if (system == "msx2")
                    coreSettings["bluemsx_msxtype"] = "MSX2";
                else if (system == "msx2+")
                    coreSettings["bluemsx_msxtype"] = "MSX2+";
                else if (system == "msxturbor")
                    coreSettings["bluemsx_msxtype"] = "MSXturboR";
                else
                    coreSettings["bluemsx_msxtypec"] = "Auto";
            }

            if (core == "mame078" || core == "mame2003")
            {
                coreSettings["mame2003_skip_disclaimer"] = "enabled";
                coreSettings["mame2003_skip_warnings"] = "enabled";
            }

            if (core == "mame078plus" || core == "mame2003_plus")
            {
                coreSettings["mame2003-plus_skip_disclaimer"] = "enabled";
                coreSettings["mame2003-plus_skip_warnings"] = "enabled";
                //coreSettings["mame2003-plus_analog"] = "digital";
            }

            if (core == "virtualjaguar")
                coreSettings["virtualjaguar_usefastblitter"] = "enabled";

            if (core == "flycast")
                coreSettings["reicast_threaded_rendering"] = "enabled";

            if (coreSettings.IsDirty)
                coreSettings.Save(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), true);
        }

        private void Configure(string system, string rom, ScreenResolution resolution)
        {
            var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"));

            retroarchConfig["rgui_extended_ascii"] = "true";
            retroarchConfig["rgui_show_start_screen"] = "false";

            retroarchConfig["quit_press_twice"] = "false";
            retroarchConfig["pause_nonactive"] = "false";
            retroarchConfig["video_fullscreen"] = "true";
            retroarchConfig["menu_driver"] = "ozone";

            if (SystemConfig.isOptSet("monitor"))
            {
                int monitorId;
                if (int.TryParse(SystemConfig["monitor"], out monitorId))
                    retroarchConfig["video_monitor_index"] = (monitorId + 1).ToString();
                else
                    retroarchConfig["video_monitor_index"] = "0";
            }
            else
                retroarchConfig["video_monitor_index"] = "0";

            if (resolution == null)
                retroarchConfig["video_windowed_fullscreen"] = "true";
            else
            {
                retroarchConfig["video_fullscreen_x"] = resolution.Width.ToString();
                retroarchConfig["video_fullscreen_y"] = resolution.Height.ToString();
                retroarchConfig["video_windowed_fullscreen"] = "false";
            }

            if (!string.IsNullOrEmpty(AppConfig["bios"]))
            {
                if (Directory.Exists(AppConfig["bios"]))
                    retroarchConfig["system_directory"] = AppConfig.GetFullPath("bios");
                else if (retroarchConfig["system_directory"] != @":\system" && !Directory.Exists(retroarchConfig["system_directory"]))
                    retroarchConfig["system_directory"] = @":\system";
            }

            if (!string.IsNullOrEmpty(AppConfig["thumbnails"]))
            {
                if (Directory.Exists(AppConfig["thumbnails"]))
                    retroarchConfig["thumbnails_directory"] = AppConfig.GetFullPath("thumbnails");
                else if (retroarchConfig["thumbnails_directory"] != @":\thumbnails" && !Directory.Exists(retroarchConfig["thumbnails_directory"]))
                    retroarchConfig["thumbnails_directory"] = @":\thumbnails";
            }

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]))
            {
                if (Directory.Exists(AppConfig["screenshots"]))
                    retroarchConfig["screenshot_directory"] = AppConfig.GetFullPath("screenshots");
                else if (retroarchConfig["screenshot_directory"] != @":\screenshots" && !Directory.Exists(retroarchConfig["screenshot_directory"]))
                    retroarchConfig["screenshot_directory"] = @":\screenshots";
            }

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
            {
                retroarchConfig["savestate_directory"] = Path.Combine(AppConfig.GetFullPath("saves"), system);
                retroarchConfig["savefile_directory"] = Path.Combine(AppConfig.GetFullPath("saves"), system);
            }

            if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                retroarchConfig["video_smooth"] = "true";
            else
                retroarchConfig["video_smooth"] = "false";

            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shader") && SystemConfig["shader"] != "None")
            {
                retroarchConfig["video_shader_enable"] = "true";
                retroarchConfig["video_smooth"] = "false";     // seems to be necessary for weaker SBCs
                retroarchConfig["video_shader_dir"] = AppConfig.GetFullPath("shaders");
            }
            else
                retroarchConfig["video_shader_enable"] = "false";

            if (SystemConfig.isOptSet("ratio"))
            {
                if (SystemConfig["ratio"] == "custom")
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                else
                {
                    int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                    if (idx >= 0)
                    {
                        retroarchConfig["aspect_ratio_index"] = idx.ToString();
                        retroarchConfig["video_aspect_ratio_auto"] = "false";
                    }
                    else
                    {
                        retroarchConfig["video_aspect_ratio_auto"] = "true";
                        retroarchConfig["aspect_ratio_index"] = "";
                    }
                }
            }
            else
            {
                if (SystemConfig["core"] == "tgbdual")
                    retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString();

                if (system == "wii")
                    retroarchConfig["aspect_ratio_index"] = "22";
                else
                    retroarchConfig["aspect_ratio_index"] = "";
            }

            if (SystemConfig["core"] == "cap32")
                retroarchConfig["cap32_combokey"] = "y";

            if (!SystemConfig.isOptSet("rewind"))
                retroarchConfig["rewind_enable"] = systemNoRewind.Contains(system) ? "false" : "true"; // AUTO
            else if (SystemConfig.getOptBoolean("rewind"))
                retroarchConfig["rewind_enable"] = "true";
            else
                retroarchConfig["rewind_enable"] = "false";

            if (SystemConfig.isOptSet("integerscale") && SystemConfig.getOptBoolean("integerscale"))
                retroarchConfig["video_scale_integer"] = "true";
            else
                retroarchConfig["video_scale_integer"] = "false";

            if (SystemConfig.isOptSet("video_threaded") && SystemConfig.getOptBoolean("video_threaded"))
                retroarchConfig["video_threaded"] = "true";
            else
                retroarchConfig["video_threaded"] = "false";

            if (SystemConfig.isOptSet("showFPS") && SystemConfig.getOptBoolean("showFPS"))
                retroarchConfig["fps_show"] = "true";
            else
                retroarchConfig["fps_show"] = "false";            

            if (SystemConfig.isOptSet("runahead") && SystemConfig["runahead"].ToInteger() > 0 && !systemNoRunahead.Contains(system))
            {
                retroarchConfig["run_ahead_enabled"] = "true";
                retroarchConfig["run_ahead_frames"] = SystemConfig["runahead"];

                if (SystemConfig.isOptSet("secondinstance") && SystemConfig.getOptBoolean("secondinstance"))
                    retroarchConfig["run_ahead_secondary_instance"] = "true";
                else
                    retroarchConfig["run_ahead_secondary_instance"] = "false";
            }
            else
            {
                retroarchConfig["run_ahead_enabled"] = "false";
                retroarchConfig["run_ahead_frames"] = "0";
                retroarchConfig["run_ahead_secondary_instance"] = "false";
            }

            if (SystemConfig.isOptSet("autosave") && SystemConfig.getOptBoolean("autosave"))
            {
                retroarchConfig["savestate_auto_save"] = "true";
                retroarchConfig["savestate_auto_load"] = "true";
            }
            else
            {
                retroarchConfig["savestate_auto_save"] = "false";
                retroarchConfig["savestate_auto_load"] = "false";
            }

            retroarchConfig["input_libretro_device_p1"] = "1";
            retroarchConfig["input_libretro_device_p2"] = "1";
            
            if (coreToP1Device.ContainsKey(SystemConfig["core"]))
                retroarchConfig["input_libretro_device_p1"] = coreToP1Device[SystemConfig["core"]];

            if (coreToP2Device.ContainsKey(SystemConfig["core"]))
                retroarchConfig["input_libretro_device_p2"] = coreToP2Device[SystemConfig["core"]];

            if (Controllers.Count > 2 && (SystemConfig["core"] == "snes9x_next" || SystemConfig["core"] == "snes9x"))
                retroarchConfig["input_libretro_device_p2"] = "257";

            if (SystemConfig["core"] == "atari800")
            {
                retroarchConfig["input_libretro_device_p1"] = "513";
                retroarchConfig["input_libretro_device_p2"] = "513";
            }
            
            if (SystemConfig["core"] == "bluemsx")
            {
                if (systemToP1Device.ContainsKey(system))
                    retroarchConfig["input_libretro_device_p1"] = systemToP1Device[system];

                if (systemToP2Device.ContainsKey(system))
                    retroarchConfig["input_libretro_device_p2"] = systemToP2Device[system];
            }

            if (SystemConfig["retroachievements"] == "true" && systemToRetroachievements.Contains(system))
            {
                retroarchConfig["cheevos_enable"] = "true";
                retroarchConfig["cheevos_username"] = SystemConfig["retroachievements.username"];
                retroarchConfig["cheevos_password"] = SystemConfig["retroachievements.password"];
                retroarchConfig["cheevos_hardcore_mode_enable"] = SystemConfig["retroachievements.hardcore"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_leaderboards_enable"] = SystemConfig["retroachievements.leaderboards"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_verbose_enable"] = SystemConfig["retroachievements.verbose"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_auto_screenshot"] = SystemConfig["retroachievements.screenshot"] == "true" ? "true" : "false";
            }
            else
                retroarchConfig["cheevos_enable"] = "false";

            // Netplay management : netplaymode client -netplayport " + std::to_string(options.port) + " -netplayip
            if (SystemConfig["netplay"] == "true" && !string.IsNullOrEmpty(SystemConfig["netplaymode"]))
            {
                // Security : hardcore mode disables save states, which would kill netplay
                retroarchConfig["cheevos_hardcore_mode_enable"] = "false";

                retroarchConfig["netplay_mode"] = "false";
                retroarchConfig["netplay_ip_port"] = SystemConfig["netplay.port"]; // netplayport
                retroarchConfig["netplay_nickname"] = SystemConfig["netplay.nickname"];
                retroarchConfig["netplay_mitm_server"] = SystemConfig["netplay.relay"];
                retroarchConfig["netplay_use_mitm_server"] = string.IsNullOrEmpty(SystemConfig["netplay.relay"]) ? "true" : "false";

                retroarchConfig["netplay_spectator_mode_enable"] = SystemConfig.getOptBoolean("netplay.spectator") ? "true" : "false";
                retroarchConfig["netplay_client_swap_input"] = "false";

                if (SystemConfig["netplaymode"] == "client")
                {
                    retroarchConfig["netplay_mode"] = "true";
                    retroarchConfig["netplay_ip_address"] = SystemConfig["netplayip"];
                    retroarchConfig["netplay_ip_port"] = SystemConfig["netplayport"];
                    retroarchConfig["netplay_client_swap_input"] = "true";
                }
            }

            // AI service for game translations
            if (SystemConfig.isOptSet("ai_service_enabled") && SystemConfig.getOptBoolean("ai_service_enabled"))
            {
                retroarchConfig["ai_service_enable"] = "true";
                retroarchConfig["ai_service_mode"] = "0";
                retroarchConfig["ai_service_source_lang"] = "0";

                if (!string.IsNullOrEmpty(SystemConfig["ai_service_url"]))
                    retroarchConfig["ai_service_url"] = SystemConfig["ai_service_url"] + "&mode=Fast&output=png&target_lang=" + SystemConfig["ai_target_lang"];
                else
                    retroarchConfig["ai_service_url"] = "http://" + "ztranslate.net/service?api_key=BATOCERA&mode=Fast&output=png&target_lang=" + SystemConfig["ai_target_lang"];

                if (SystemConfig.isOptSet("ai_service_pause") && SystemConfig.getOptBoolean("ai_service_pause"))
                    retroarchConfig["ai_service_pause"] = "true";
                else
                    retroarchConfig["ai_service_pause"] = "false";
            }
            else
                retroarchConfig["ai_service_enable"] = "false";

            // bezel

            writeBezelConfig(retroarchConfig, system, rom, resolution);

            if (LibretroControllers.WriteControllersConfig(retroarchConfig, system))
                UsePadToKey = false;

            // custom : allow the user to configure directly retroarch.cfg via batocera.conf via lines like : snes.retroarch.menu_driver=rgui
            foreach (var user_config in SystemConfig)
                if (user_config.Name.StartsWith("retroarch."))
                    retroarchConfig[user_config.Name.Substring("retroarch.".Length)] = user_config.Value;

            if (retroarchConfig.IsDirty)
                retroarchConfig.Save(Path.Combine(RetroarchPath, "retroarch.cfg"), true);
        }

        private void writeBezelConfig(ConfigFile retroarchConfig, string systemName, string rom, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("decorations");

            string bezel = Directory.Exists(path) && !string.IsNullOrEmpty(SystemConfig["bezel"]) ? SystemConfig["bezel"] : "default";
            if (SystemConfig.isOptSet("forceNoBezel") && SystemConfig.getOptBoolean("forceNoBezel"))
                bezel = null;

            retroarchConfig["input_overlay_hide_in_menu"] = "false";
            retroarchConfig["input_overlay_enable"] = "false";
            retroarchConfig["video_message_pos_x"] = "0.05";
            retroarchConfig["video_message_pos_y"] = "0.05";

            if (string.IsNullOrEmpty(bezel) || bezel == "none")
                return;

            if (systemName == "wii")
                return;

            string romBase = Path.GetFileNameWithoutExtension(rom);

            string overlay_info_file = path + "/" + bezel + "/games/" + systemName + "/" + romBase + ".info";
            string overlay_png_file = path + "/" + bezel + "/games/" + systemName + "/" + romBase + ".png";

            if (!File.Exists(overlay_png_file))
            {
                overlay_info_file = path + "/" + bezel + "/games/" + systemName + "/" + romBase + ".info";
                overlay_png_file = path + "/" + bezel + "/games/" + systemName + "/" + romBase + ".png";

                if (!File.Exists(overlay_png_file))
                {
                    overlay_info_file = path + "/" + bezel + "/games/" + romBase + ".info";
                    overlay_png_file = path + "/" + bezel + "/games/" + romBase + ".png";
                }

                if (!File.Exists(overlay_png_file))
                {
                    overlay_info_file = path + "/" + bezel + "/systems/" + systemName + ".info";
                    overlay_png_file = path + "/" + bezel + "/systems/" + systemName + ".png";
                }

                if (!File.Exists(overlay_png_file))
                {
                    overlay_info_file = path + "/" + bezel + "/default.info";
                    overlay_png_file = path + "/" + bezel + "/default.png";
                }

                if (!File.Exists(overlay_png_file))
                {
                    overlay_info_file = path + "/default/systems/" + systemName + ".info";
                    overlay_png_file = path + "/default/systems/" + systemName + ".png";
                }

                if (!File.Exists(overlay_png_file))
                    return;
            }

            string overlay_cfg_file = Path.Combine(RetroarchPath, "custom-overlay.cfg");

            retroarchConfig["input_overlay_enable"] = "true";
            retroarchConfig["input_overlay_scale"] = "1.0";
            retroarchConfig["input_overlay"] = overlay_cfg_file;
            retroarchConfig["input_overlay_hide_in_menu"] = "true";
            retroarchConfig["input_overlay_opacity"] = "1.0";
            retroarchConfig["input_overlay_show_mouse_cursor"] = "false";

            StringBuilder fd = new StringBuilder();
            fd.AppendLine("overlays = 1");
            fd.AppendLine("overlay0_overlay = \"" + overlay_png_file + "\"");
            fd.AppendLine("overlay0_full_screen = true");
            fd.AppendLine("overlay0_descs = 0");
            File.WriteAllText(overlay_cfg_file, fd.ToString());
        }

        private string _dosBoxTempRom;

        public override void Cleanup()
        {
            if (_dosBoxTempRom != null && File.Exists(_dosBoxTempRom))
                File.Delete(_dosBoxTempRom);

            base.Cleanup();
        }

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (string.IsNullOrEmpty(RetroarchPath))
                return null;

            if (Path.GetExtension(rom).ToLowerInvariant() == ".libretro")
                core = Path.GetFileNameWithoutExtension(rom);

            if (core != null && core.IndexOf("dosbox", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                string bat = Path.Combine(rom, "dosbox.bat");
                if (File.Exists(bat))
                    rom = bat;
                else
                {
                    string ext = Path.GetExtension(rom).ToLower();
                    if ((ext == ".dosbox" || ext == ".dos" || ext == ".pc") && File.Exists(rom))
                    {
                        string tempRom = Path.Combine(Path.GetDirectoryName(rom), "dosbox.conf");
                        if (File.Exists(tempRom) && !new FileInfo(tempRom).Attributes.HasFlag(FileAttributes.Hidden))
                            rom = tempRom;
                        else
                        {
                            try
                            {
                                if (File.Exists(tempRom))
                                    File.Delete(tempRom);
                            }
                            catch { }

                            try
                            {
                                File.Copy(rom, tempRom);
                                new FileInfo(tempRom).Attributes |= FileAttributes.Hidden;
                                rom = tempRom;
                                _dosBoxTempRom = tempRom;
                            }
                            catch { }
                        }
                    }
                }
            }

            Configure(system, rom, resolution);
            ConfigureCoreOptions(system, core);

            List<string> commandArray = new List<string>();

            string subSystem = SubSystem.GetSubSystem(core, system);
            if (!string.IsNullOrEmpty(subSystem))
            {
                commandArray.Add("--subsystem");
                commandArray.Add(subSystem);
            }

            if (!string.IsNullOrEmpty(SystemConfig["netplaymode"]))
            {
                // Netplay mode
                if (SystemConfig["netplaymode"] == "host")
                    commandArray.Add("--host");
                else if (SystemConfig["netplaymode"] == "client")
                {
                    commandArray.Add("--connect");
                    commandArray.Add(SystemConfig["netplay.server.address"]);
                }
            }

            // RetroArch 1.7.8 requires the shaders to be passed as command line argument      
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shader") && SystemConfig["shader"] != "None")
            {
                string shaderFilename = SystemConfig["shader"] + ".glslp";
                string videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), shaderFilename).Replace("/", "\\");
                if (File.Exists(videoShader))
                {
                    commandArray.Add("--set-shader");
                    commandArray.Add("\"" + videoShader + "\"");
                }
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = Path.Combine(RetroarchPath, "retroarch.exe"),
                WorkingDirectory = RetroarchPath,                
                Arguments =
                    Path.GetExtension(rom).ToLowerInvariant() == ".libretro" ?
                        ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" " + args).Trim() :
                        ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" \"" + rom + "\" " + args).Trim()
            };
        }

        static List<string> ratioIndexes = new List<string> { "4/3", "16/9", "16/10", "16/15", "21/9", "1/1", "2/1", "3/2", "3/4", "4/1", "4/4", "5/4", "6/5", "7/9", "8/3",
                "8/7", "19/12", "19/14", "30/17", "32/9", "config", "squarepixel", "core", "custom" };

        static List<string> systemToRetroachievements = new List<string> { 
            "atari2600", "atari7800", "atarijaguar", "colecovision", "nes", "snes", "virtualboy", "n64", "sg1000", "mastersystem", "megadrive", 
            "segacd", "sega32x", "saturn", "pcengine", "pcenginecd", "supergrafx", "psx", "mame", "fbneo", "neogeo", "lightgun", "apple2", 
            "lynx", "wswan", "wswanc", "gb", "gbc", "gba", "nds", "pokemini", "gamegear", "ngp", "ngpc"};

        static List<string> systemNoRewind = new List<string>() { "sega32x", "wii", "gamecube", "gc", "psx", "zxspectrum", "odyssey2", "n64", "dreamcast", "atomiswave", "naomi", "neogeocd", "saturn", "mame", "fbneo" };
        static List<string> systemNoRunahead = new List<string>() { "sega32x", "wii", "gamecube", "n64", "dreamcast", "atomiswave", "naomi", "neogeocd", "saturn" };

        static Dictionary<string, string> systemToP1Device = new Dictionary<string, string>() { { "msx", "257" }, { "msx1", "257" }, { "msx2", "257" }, { "colecovision", "1" } };
        static Dictionary<string, string> systemToP2Device = new Dictionary<string, string>() { { "msx", "257" }, { "msx1", "257" }, { "msx2", "257" }, { "colecovision", "1" } };

        static Dictionary<string, string> coreToP1Device = new Dictionary<string, string>() { { "cap32", "513" }, { "81", "257" }, { "fuse", "513" } };
        static Dictionary<string, string> coreToP2Device = new Dictionary<string, string>() { { "fuse", "513" } };
    }


    class SubSystem
    {
        static public List<SubSystem> subSystems = new List<SubSystem>()
        {
            new SubSystem("fbneo", "colecovision", "cv"),

            new SubSystem("fbneo", "msx", "msx"),                        
            new SubSystem("fbneo", "msx1", "msx"),

            new SubSystem("fbneo", "supergrafx", "sgx"),
            new SubSystem("fbneo", "pcengine", "pce"),
            new SubSystem("fbneo", "pcenginecd", "pce"),

            new SubSystem("fbneo", "turbografx", "tg"),
            new SubSystem("fbneo", "turbografx16", "tg"),
            
            new SubSystem("fbneo", "gamegear", "gg"),
            new SubSystem("fbneo", "mastersystem", "sms"),
            new SubSystem("fbneo", "megadrive", "md"),

            new SubSystem("fbneo", "sg1000", "sg1k"),
            new SubSystem("fbneo", "sg-1000", "sg1k"),
            
            new SubSystem("fbneo", "zxspectrum", "spec"),

            new SubSystem("fbneo", "neogeocd", "neocd")            
        };

        public static string GetSubSystem(string core, string system)
        {
            var sub = subSystems.FirstOrDefault(s => s.Core.Equals(core, StringComparison.InvariantCultureIgnoreCase) && s.System.Equals(system, StringComparison.InvariantCultureIgnoreCase));
            if (sub != null)
                return sub.SubSystemId;

            return null;
        }

        public SubSystem(string core, string system, string subSystem)
        {
            System = system;
            Core = core;
            SubSystemId = subSystem;
        }

        public string System { get; set; }
        public string Core { get; set; }
        public string SubSystemId { get; set; }
    }
}