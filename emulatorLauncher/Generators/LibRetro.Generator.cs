using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;

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

                if (system == "spectravideo")
                    coreSettings["bluemsx_msxtype"] = "SVI - Spectravideo SVI-328";
                else if (system == "colecovision")
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

        private void Configure(string system, string core, string rom, ScreenResolution resolution)
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
                string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                    catch { }

                retroarchConfig["savestate_directory"] = savePath;
                retroarchConfig["savefile_directory"] = savePath;

                retroarchConfig["savestate_thumbnail_enable"] = "true";
                retroarchConfig["savestates_in_content_dir"] = "false";
                retroarchConfig["savefiles_in_content_dir"] = "false";
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

            retroarchConfig["netplay_mode"] = "false";

            // Netplay management : netplaymode client -netplayport " + std::to_string(options.port) + " -netplayip
            if (SystemConfig["netplay"] == "true" && !string.IsNullOrEmpty(SystemConfig["netplaymode"]))
            {
                // Security : hardcore mode disables save states, which would kill netplay
                retroarchConfig["cheevos_hardcore_mode_enable"] = "false";

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

            if (LibretroControllers.WriteControllersConfig(retroarchConfig, system, core))
                UsePadToKey = false;

            // custom : allow the user to configure directly retroarch.cfg via batocera.conf via lines like : snes.retroarch.menu_driver=rgui
            foreach (var user_config in SystemConfig)
                if (user_config.Name.StartsWith("retroarch."))
                    retroarchConfig[user_config.Name.Substring("retroarch.".Length)] = user_config.Value;

            if (SystemConfig.isOptSet("video_driver"))
            {
                _video_driver = retroarchConfig["video_driver"];
                retroarchConfig["video_driver"] = SystemConfig["video_driver"];
            }
            else if (core == "dolphin" && retroarchConfig["video_driver"] != "d3d11" && retroarchConfig["video_driver"] != "vulkan")
            {
                _video_driver = retroarchConfig["video_driver"];
                retroarchConfig["video_driver"] = "d3d11";
            }

            SetLanguage(retroarchConfig);

            if (retroarchConfig.IsDirty)
                retroarchConfig.Save(Path.Combine(RetroarchPath, "retroarch.cfg"), true);
        }

        private void SetLanguage(ConfigFile retroarchConfig)
        {
            Func<string, string> shortLang = new Func<string, string>(s =>
            {
                s = s.ToLowerInvariant();

                int cut = s.IndexOf("_");
                if (cut >= 0)
                    return s.Substring(0, cut);

                return s;
            });

            var lang = SystemConfig["Language"];
            bool foundLang = false;

            retro_language rl = (retro_language)9999999;
            if (Languages.TryGetValue(lang, out rl))
                foundLang = true;
            else
            {
                lang = shortLang(lang);

                foundLang = Languages.TryGetValue(lang, out rl);
                if (!foundLang)
                {
                    var ret = Languages.Where(l => shortLang(l.Key) == lang).ToList();
                    if (ret.Any())
                    {
                        foundLang = true;
                        rl = ret.First().Value;
                    }
                }
            }

            if (foundLang)
                retroarchConfig["user_language"] = ((int)rl).ToString();
        }

        private string _video_driver;

        private void writeBezelConfig(ConfigFile retroarchConfig, string systemName, string rom, ScreenResolution resolution)
        {
            string overlayUser = AppConfig.GetFullPath("decorations");

            string overlaySystem = AppConfig.GetFullPath("system.decorations");
            if (string.IsNullOrEmpty(overlaySystem) || !Directory.Exists(overlaySystem))
                overlaySystem = Path.Combine(AppConfig.GetFullPath("home"), "decorations");

            string bezel = Directory.Exists(overlayUser) && !string.IsNullOrEmpty(SystemConfig["bezel"]) ? SystemConfig["bezel"] : "default";

            if (SystemConfig.isOptSet("forceNoBezel") && SystemConfig.getOptBoolean("forceNoBezel"))
                bezel = null;
            else if (!SystemConfig.isOptSet("bezel"))
                bezel = "thebezelproject";

            retroarchConfig["input_overlay_hide_in_menu"] = "false";
            retroarchConfig["input_overlay_enable"] = "false";
            retroarchConfig["video_message_pos_x"] = "0.05";
            retroarchConfig["video_message_pos_y"] = "0.05";

            if (string.IsNullOrEmpty(bezel) || bezel == "none")
                return;

            if (systemName == "wii")
                return;

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
                return;

            BezelInfo infos = new BezelInfo();

            if (File.Exists(overlay_info_file))
            {
                try { infos = JsonSerializer.DeserializeFile<BezelInfo>(overlay_info_file); }
                catch { }
            }

             // if image is not at the correct size, find the correct size
            bool bezelNeedAdaptation = false;
            bool viewPortUsed = true;

            if (!infos.width.HasValue || !infos.height.HasValue || !infos.top.HasValue || !infos.left.HasValue || !infos.bottom.HasValue || !infos.right.HasValue)
                viewPortUsed = false;

         // for testing ->   resolution = ScreenResolution.Parse("2280x1080x32x60");

            int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

            float gameRatio  = (float) resX / (float) resY;
            float infosRatio = 1920f / 1080f;

            if (viewPortUsed)
            {
                if (resX != infos.width || resY != infos.height)
                {
                    infosRatio = (float)infos.width / (float)infos.height;

                    if (gameRatio < infosRatio - 0.1) // keep a margin
                        return;
                    else
                        bezelNeedAdaptation = true;
                }
                                    
                retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("custom").ToString(); // overwritten from the beginning of this file                
            }
            else
            {
                 // when there is no information about width and height in the .info, assume that the tv is HD 16/9 and infos are core provided
                if (gameRatio < infosRatio - 0.1) // keep a margin
                    return;
                else
                {

                    // No info on the bezel, let's get the bezel image width and height and apply the
                    // ratios from usual 4:3 1920x1080 bezels (example: theBezelProject)

                    using (Image img = Image.FromFile(overlay_png_file))
                    {
                        infos.width = img.Width;
                        infos.height = img.Height;
                    }
                    infos.top    = (int)infos.height * 2 / 1080;
                    infos.left   = (int)infos.width * 241 / 1920; // 241 = (1920 - (1920 / (4:3))) / 2 + 1 pixel = where viewport start;
                    infos.bottom = (int)infos.height * 2 / 1080;
                    infos.right  = (int)infos.width * 241 / 1920;
                    bezelNeedAdaptation = true;
                }
                
                retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString(); // overwritten from the beginning of this file
            }

            string overlay_cfg_file = Path.Combine(RetroarchPath, "custom-overlay.cfg");
            
            retroarchConfig["input_overlay_enable"] = "true";
            retroarchConfig["input_overlay_scale"] = "1.0";
            retroarchConfig["input_overlay"] = overlay_cfg_file;
            retroarchConfig["input_overlay_hide_in_menu"] = "true";
                    
            if (!infos.opacity.HasValue)
                infos.opacity = 1.0f;
            if (!infos.messagex.HasValue)
                infos.messagex = 0.0f;
            if (!infos.messagey.HasValue)
                infos.messagey = 0.0f;

            retroarchConfig["input_overlay_opacity"] = infos.opacity.ToString().Replace(",", "."); // "1.0";
            // for testing : retroarchConfig["input_overlay_opacity"] = "0.5";

            if (bezelNeedAdaptation)
            {
                float wratio = resX / (float) infos.width;
                float hratio = resY / (float) infos.height;

                bool bezel_stretch = false;

                // If width or height < original, can't add black borders, need to stretch
                if (resX < infos.width || resY < infos.height)
                    bezel_stretch = true;

                if (bezel_stretch)
                {
                    retroarchConfig["custom_viewport_x"] = ((int) (infos.left * wratio)).ToString();
                    retroarchConfig["custom_viewport_y"] = ((int)(infos.top * hratio)).ToString();
                    retroarchConfig["custom_viewport_width"] = ((int) ((infos.width - infos.left - infos.right) * wratio)).ToString();
                    retroarchConfig["custom_viewport_height"] = ((int)((infos.height - infos.top - infos.bottom) * hratio)).ToString();
                    retroarchConfig["video_message_pos_x"] = (infos.messagex.Value * wratio).ToString(CultureInfo.InvariantCulture);
                    retroarchConfig["video_message_pos_y"] = (infos.messagey.Value * hratio).ToString(CultureInfo.InvariantCulture);
                }
                else
                {                    
                    int xoffset = resX - infos.width.Value;
                    int yoffset = resY - infos.height.Value;
                    retroarchConfig["custom_viewport_x"]      = ((int) (infos.left.Value + xoffset/2)).ToString();
                    retroarchConfig["custom_viewport_y"]      = ((int) (infos.top.Value + yoffset/2)).ToString();
                    retroarchConfig["custom_viewport_width"]  = ((int) ((infos.width.Value - infos.left.Value - infos.right.Value))).ToString();
                    retroarchConfig["custom_viewport_height"] = ((int)((infos.height.Value - infos.top.Value - infos.bottom.Value))).ToString();
                    retroarchConfig["video_message_pos_x"] = (infos.messagex.Value + xoffset / 2).ToString(CultureInfo.InvariantCulture);
                    retroarchConfig["video_message_pos_y"] = (infos.messagey.Value + yoffset / 2).ToString(CultureInfo.InvariantCulture);

                    int borderw = 0;
                    int borderh = 0;
                    if (wratio > 1)
                        borderw = xoffset / 2;
                    if (hratio > 1)
                        borderh = yoffset / 2;

                    var f = Path.GetFileNameWithoutExtension(overlay_png_file);
                    var d = Path.GetFileName(Path.GetDirectoryName(overlay_png_file));
                    var fn = "bezel." + d + "." + f + "." + resX +"x" + resY + ".png";
                    string output_png_file = Path.Combine(Path.GetTempPath(), fn);

                    if (File.Exists(output_png_file))
                        overlay_png_file = output_png_file;
                    else
                    {
                        try
                        {
                            using (Image img = Image.FromFile(overlay_png_file))
                            {
                                using (Bitmap bmp = new Bitmap(infos.width.Value + 2 * borderw, infos.height.Value + 2 * borderh))
                                {
                                    using (Graphics g = Graphics.FromImage(bmp))
                                    {
                                        g.ExcludeClip(new Rectangle(borderw, borderh, img.Width, img.Height));
                                        g.FillRectangle(Brushes.Black, new Rectangle(0, 0, bmp.Width, bmp.Height));
                                        g.ResetClip();

                                        g.DrawImage(img, new Rectangle(borderw, borderh, img.Width, img.Height));
                                    }

                                    bmp.Save(output_png_file, System.Drawing.Imaging.ImageFormat.Png);
                                    overlay_png_file = output_png_file;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            else
            {
                if (viewPortUsed)
                {
                    retroarchConfig["custom_viewport_x"] = infos.left.Value.ToString();
                    retroarchConfig["custom_viewport_y"] = infos.top.Value.ToString();
                    retroarchConfig["custom_viewport_width"] = (infos.width.Value - infos.left.Value - infos.right.Value).ToString();
                    retroarchConfig["custom_viewport_height"] = (infos.height.Value - infos.top.Value - infos.bottom.Value).ToString();
                }

                retroarchConfig["video_message_pos_x"] = infos.messagex.Value.ToString(CultureInfo.InvariantCulture);
                retroarchConfig["video_message_pos_y"] = infos.messagey.Value.ToString(CultureInfo.InvariantCulture);
            }
            
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

            if (!string.IsNullOrEmpty(_video_driver))
            {
                var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"));
                retroarchConfig["video_driver"] = _video_driver;
                retroarchConfig.Save(Path.Combine(RetroarchPath, "retroarch.cfg"), true);
            }

            base.Cleanup();
        }

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (string.IsNullOrEmpty(RetroarchPath))
                return null;

            if (Path.GetExtension(rom).ToLowerInvariant() == ".libretro")
            {
                core = Path.GetFileNameWithoutExtension(rom);

                if (core == "xrick")
                    rom = Path.Combine(Path.GetDirectoryName(rom), "xrick", "data.zip");
                else if (core == "dinothawr")
                    rom = Path.Combine(Path.GetDirectoryName(rom), "dinothawr", "dinothawr.game");
                else
                    rom = null;
            }

            // Extension used by hypseus .daphne but lr-daphne starts with .zip
            if (system == "daphne" || core == "daphne")
            {
                string datadir = Path.GetDirectoryName(rom);
                string romName = Path.GetFileNameWithoutExtension(rom);

                //romName = os.path.splitext(os.path.basename(rom))[0]
                rom = Path.GetFullPath(datadir + "/roms/" + romName + ".zip");
            }

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

            Configure(system, core, rom, resolution);
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
                    commandArray.Add("--connect " + SystemConfig["netplayip"]);
                    commandArray.Add("--port " + SystemConfig["netplayport"]);
                }
            }

            // RetroArch 1.7.8 requires the shaders to be passed as command line argument      
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shader") && SystemConfig["shader"] != "None")
            {
                string videoDriver = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"))["video_driver"];
                bool isOpenGL = (emulator != "angle") && (videoDriver == "gl");

                string shaderFilename = SystemConfig["shader"] + (isOpenGL ? ".glslp" : ".slangp");

                string videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), shaderFilename).Replace("/", "\\");
                if (!File.Exists(videoShader))
                    videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), isOpenGL ? "shaders_glsl" : "shaders_slang", shaderFilename).Replace("/", "\\");

                if (!File.Exists(videoShader))
                    videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), isOpenGL ? "glsl" : "slang", shaderFilename).Replace("/", "\\");

                if (!File.Exists(videoShader))
                    videoShader = Path.Combine(RetroarchPath, "shaders", isOpenGL ? "shaders_glsl" : "shaders_slang", shaderFilename).Replace("/", "\\");

                if (!File.Exists(videoShader) && !isOpenGL && shaderFilename.Contains("zfast-"))
                    videoShader = Path.Combine(RetroarchPath, "shaders", isOpenGL ? "shaders_glsl" : "shaders_slang", "crt/crt-geom.slangp").Replace("/", "\\");

                if (File.Exists(videoShader))
                {
                    commandArray.Add("--set-shader");
                    commandArray.Add("\"" + videoShader + "\"");
                }
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = Path.Combine(RetroarchPath, emulator == "angle" ? "retroarch_angle.exe" : "retroarch.exe"),
                WorkingDirectory = RetroarchPath,
                Arguments =
                    string.IsNullOrEmpty(rom) ?
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

        static List<string> systemNoRewind = new List<string>() { "3ds", "sega32x", "wii", "gamecube", "gc", "psx", "zxspectrum", "odyssey2", "n64", "dreamcast", "atomiswave", "naomi", "neogeocd", "saturn", "mame", "fbneo" };
        static List<string> systemNoRunahead = new List<string>() { "3ds", "sega32x", "wii", "gamecube", "n64", "dreamcast", "atomiswave", "naomi", "neogeocd", "saturn" };

        static Dictionary<string, string> systemToP1Device = new Dictionary<string, string>() { { "msx", "257" }, { "msx1", "257" }, { "msx2", "257" }, { "colecovision", "1" } };
        static Dictionary<string, string> systemToP2Device = new Dictionary<string, string>() { { "msx", "257" }, { "msx1", "257" }, { "msx2", "257" }, { "colecovision", "1" } };

        static Dictionary<string, string> coreToP1Device = new Dictionary<string, string>() { { "cap32", "513" }, { "81", "257" }, { "fuse", "513" } };
        static Dictionary<string, string> coreToP2Device = new Dictionary<string, string>() { { "fuse", "513" } };

        static Dictionary<string, retro_language> Languages = new Dictionary<string, retro_language>()
        {
            {"en", retro_language.RETRO_LANGUAGE_ENGLISH},
            {"ja", retro_language.RETRO_LANGUAGE_JAPANESE},
            {"fr", retro_language.RETRO_LANGUAGE_FRENCH},
            {"es", retro_language.RETRO_LANGUAGE_SPANISH},
            {"de", retro_language.RETRO_LANGUAGE_GERMAN},
            {"it", retro_language.RETRO_LANGUAGE_ITALIAN},
            {"nl", retro_language.RETRO_LANGUAGE_DUTCH},
            {"pt_BR", retro_language.RETRO_LANGUAGE_PORTUGUESE_BRAZIL},
            {"pt_PT", retro_language.RETRO_LANGUAGE_PORTUGUESE_PORTUGAL},
            {"pt", retro_language.RETRO_LANGUAGE_PORTUGUESE_PORTUGAL},
            {"ru", retro_language.RETRO_LANGUAGE_RUSSIAN},
            {"ko", retro_language.RETRO_LANGUAGE_KOREAN},
            {"zh_CN", retro_language.RETRO_LANGUAGE_CHINESE_SIMPLIFIED},
            {"zh_SG", retro_language.RETRO_LANGUAGE_CHINESE_SIMPLIFIED},
            {"zh_HK", retro_language.RETRO_LANGUAGE_CHINESE_TRADITIONAL},
            {"zh_TW", retro_language.RETRO_LANGUAGE_CHINESE_TRADITIONAL},
            {"zh", retro_language.RETRO_LANGUAGE_CHINESE_SIMPLIFIED},
            {"eo", retro_language.RETRO_LANGUAGE_ESPERANTO},
            {"pl", retro_language.RETRO_LANGUAGE_POLISH},
            {"vi", retro_language.RETRO_LANGUAGE_VIETNAMESE},
            {"ar", retro_language.RETRO_LANGUAGE_ARABIC},
            {"el", retro_language.RETRO_LANGUAGE_GREEK},
        };
    }

    // https://github.com/libretro/RetroArch/blob/master/libretro-common/include/libretro.h#L260
    enum retro_language
    {
        RETRO_LANGUAGE_ENGLISH = 0,
        RETRO_LANGUAGE_JAPANESE = 1,
        RETRO_LANGUAGE_FRENCH = 2,
        RETRO_LANGUAGE_SPANISH = 3,
        RETRO_LANGUAGE_GERMAN = 4,
        RETRO_LANGUAGE_ITALIAN = 5,
        RETRO_LANGUAGE_DUTCH = 6,
        RETRO_LANGUAGE_PORTUGUESE_BRAZIL = 7,
        RETRO_LANGUAGE_PORTUGUESE_PORTUGAL = 8,
        RETRO_LANGUAGE_RUSSIAN = 9,
        RETRO_LANGUAGE_KOREAN = 10,
        RETRO_LANGUAGE_CHINESE_TRADITIONAL = 11,
        RETRO_LANGUAGE_CHINESE_SIMPLIFIED = 12,
        RETRO_LANGUAGE_ESPERANTO = 13,
        RETRO_LANGUAGE_POLISH = 14,
        RETRO_LANGUAGE_VIETNAMESE = 15,
        RETRO_LANGUAGE_ARABIC = 16,
        RETRO_LANGUAGE_GREEK = 17,
        RETRO_LANGUAGE_TURKISH = 18,
        RETRO_LANGUAGE_SLOVAK = 19,
        RETRO_LANGUAGE_PERSIAN = 20,
        RETRO_LANGUAGE_HEBREW = 21,
        RETRO_LANGUAGE_ASTURIAN = 22//,
        //      RETRO_LANGUAGE_LAST,

        /* Ensure sizeof(enum) == sizeof(int) */
        //        RETRO_LANGUAGE_DUMMY = INT_MAX
    };

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
    
    [DataContract]
    class BezelInfo
    {
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