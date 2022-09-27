using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;

namespace emulatorLauncher.libRetro
{
    partial class LibRetroGenerator : Generator
    {
        const string RetroArchNetPlayPatchedName = "RETROBAT";

        public string RetroarchPath { get; set; }
        public string RetroarchCorePath { get; set; }

        public string CurrentHomeDirectory { get; set; }

        public LibRetroGenerator()
        {
            RetroarchPath = AppConfig.GetFullPath("retroarch");

            RetroarchCorePath = AppConfig.GetFullPath("retroarch.cores");
            if (string.IsNullOrEmpty(RetroarchCorePath))
                RetroarchCorePath = Path.Combine(RetroarchPath, "cores");
        }

        private void Configure(string system, string core, string rom, ScreenResolution resolution)
        {
            var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"), new ConfigFileOptions() { CaseSensitive = true });

            retroarchConfig["global_core_options"] = "true";
            retroarchConfig["core_options_path"] = ""; //',             '"/userdata/system/configs/retroarch/cores/retroarch-core-options.cfg"')          
            retroarchConfig["rgui_extended_ascii"] = "true";
            retroarchConfig["rgui_show_start_screen"] = "false";           
            retroarchConfig["menu_driver"] = "ozone";
            retroarchConfig["ui_menubar_enable"] = "false";
            retroarchConfig["video_fullscreen"] = "true";
            retroarchConfig["video_window_save_positions"] = "false";
            retroarchConfig["notification_show_autoconfig"] = "false";
            retroarchConfig["notification_show_remap_load"] = "false";
            retroarchConfig["driver_switch_enable"] = "true";
            retroarchConfig["input_driver"] = "dinput";

            BindBoolFeature(retroarchConfig, "pause_nonactive", "use_guns", "true", "false", true); // Pause when calibrating gun...
            BindBoolFeature(retroarchConfig, "input_autodetect_enable", "disableautocontrollers", "true", "false", true);

            SetupUIMode(retroarchConfig);

            // Resolution & monitor
            if (Features.IsSupported("MonitorIndex"))
            {
                if (SystemConfig.isOptSet("MonitorIndex"))
                {
                    int monitorId;
                    if (int.TryParse(SystemConfig["MonitorIndex"], out monitorId) && monitorId < Screen.AllScreens.Length)
                        retroarchConfig["video_monitor_index"] = (monitorId + 1).ToString();
                }
                else
                {
                    retroarchConfig["video_monitor_index"] = "0";
                }                   
            }

            if (resolution == null)
            {
                if (!SystemConfig.isOptSet("MonitorIndex"))
                {
                    Rectangle emulationStationBounds;
                    if (IsEmulationStationWindowed(out emulationStationBounds))
                    {
                        int width = emulationStationBounds.Width;
                        int height = emulationStationBounds.Height;
                        var res = ScreenResolution.CurrentResolution;

                        if (emulationStationBounds.Left == 0 && emulationStationBounds.Top == 0)
                        {
                            emulationStationBounds.X = (res.Width - width) / 2 - SystemInformation.FrameBorderSize.Width;
                            emulationStationBounds.Y = (res.Height - height - SystemInformation.CaptionHeight - SystemInformation.MenuHeight) / 2 - SystemInformation.FrameBorderSize.Height;
                        }

                        retroarchConfig["video_windowed_position_x"] = emulationStationBounds.X.ToString();
                        retroarchConfig["video_windowed_position_y"] = emulationStationBounds.Y.ToString();
                        retroarchConfig["video_windowed_position_width"] = width.ToString();
                        retroarchConfig["video_windowed_position_height"] = height.ToString();
                        retroarchConfig["video_fullscreen"] = "false";
                        retroarchConfig["video_window_save_positions"] = "true";

                        resolution = ScreenResolution.FromSize(width, height); // For bezels
                    }
                    else
                        retroarchConfig["video_windowed_fullscreen"] = "true";
                }
                else
                    retroarchConfig["video_windowed_fullscreen"] = "true";
            }
            else
            {
                retroarchConfig["video_fullscreen_x"] = resolution.Width.ToString();
                retroarchConfig["video_fullscreen_y"] = resolution.Height.ToString();
                retroarchConfig["video_windowed_fullscreen"] = "false";
            }

            if (resolution == null && retroarchConfig["video_monitor_index"] != "0")
                resolution = ScreenResolution.FromScreenIndex(retroarchConfig["video_monitor_index"].ToInteger() - 1);

            // Folders
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

            try 
            {
                string cacheDirectory = Path.Combine(Path.GetTempPath(), "retroarch");
                Directory.CreateDirectory(cacheDirectory);
                retroarchConfig["cache_directory"] = cacheDirectory;
            }
            catch { }
            
            // Savestates
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

            if (SystemConfig.isOptSet("incrementalsavestates") && !SystemConfig.getOptBoolean("incrementalsavestates"))
            {
                retroarchConfig["savestate_auto_index"] = "false";
                retroarchConfig["savestate_max_keep"] = "50";
            }
            else
            {
                retroarchConfig["savestate_auto_index"] = "true";
                retroarchConfig["savestate_max_keep"] = "0";
            }

            BindBoolFeature(retroarchConfig, "savestate_auto_save", "autosave", "true", "false");
            BindBoolFeature(retroarchConfig, "savestate_auto_load", "autosave", "true", "false");
            BindFeature(retroarchConfig, "state_slot", "state_slot", "0");

            // Shaders
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shader") && SystemConfig["shader"] != "None")
                retroarchConfig["video_shader_enable"] = "true";
            else if (Features.IsSupported("shaderset"))
                retroarchConfig["video_shader_enable"] = "false";

            // Aspect ratio
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
            else if (core == "tgbdual")
                retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString();
            else if (system == "wii")
                retroarchConfig["aspect_ratio_index"] = "22";
            else
                retroarchConfig["aspect_ratio_index"] = "";
            
            // Rewind
            if (!SystemConfig.isOptSet("rewind"))
                retroarchConfig["rewind_enable"] = systemNoRewind.Contains(system) ? "false" : "true"; // AUTO
            else if (SystemConfig.getOptBoolean("rewind"))
                retroarchConfig["rewind_enable"] = "true";
            else
                retroarchConfig["rewind_enable"] = "false";

            // Audio
            BindFeature(retroarchConfig, "audio_driver", "audio_driver", "xaudio"); // Audio driver
            BindFeature(retroarchConfig, "audio_resampler", "audio_resampler", "sinc");
            BindFeature(retroarchConfig, "audio_resampler_quality", "audio_resampler_quality", "3");
            BindFeature(retroarchConfig, "audio_volume", "audio_volume", "0.000000");
            BindFeature(retroarchConfig, "audio_mixer_volume", "audio_mixer_volume", "0.000000");
            BindFeature(retroarchConfig, "audio_dsp_plugin", "audio_dsp_plugin", "");

            // Misc
            BindBoolFeature(retroarchConfig, "video_smooth", "smooth", "true", "false");
            BindBoolFeature(retroarchConfig, "video_scale_integer", "integerscale", "true", "false");
            BindBoolFeature(retroarchConfig, "video_threaded", "video_threaded", "true", "false");
            BindBoolFeature(retroarchConfig, "fps_show", "showFPS", "true", "false");
            BindBoolFeature(retroarchConfig, "video_frame_delay_auto", "video_frame_delay_auto", "true", "false"); // Auto frame delay (input delay reduction via frame timing)
            BindBoolFeature(retroarchConfig, "quit_press_twice", "PressTwice", "true", "false"); // Press hotkeys twice to exit
            
            BindFeature(retroarchConfig, "video_font_enable", "OnScreenMsg", "true"); // OSD notifications
            BindFeature(retroarchConfig, "video_rotation", "RotateVideo", "0"); // video rotation
            BindFeature(retroarchConfig, "screen_orientation", "RotateScreen", "0"); // screen orientation
            BindFeature(retroarchConfig, "crt_switch_resolution", "CRTSwitch", "0"); // CRT Switch
            BindFeature(retroarchConfig, "crt_switch_resolution_super", "CRTSuperRes", "0"); // CRT Resolution
            
            // Stats
            if (SystemConfig.isOptSet("DrawStats"))
            {
                if (SystemConfig["DrawStats"] == "fps_only")
                {
                    retroarchConfig["fps_show"] = "true";
                    retroarchConfig["memory_show"] = "false";
                    retroarchConfig["statistics_show"] = "false";
                }
                else if (SystemConfig["DrawStats"] == "mem_only")
                {
                    retroarchConfig["fps_show"] = "false";
                    retroarchConfig["memory_show"] = "true";
                    retroarchConfig["statistics_show"] = "false";
                }
                else if (SystemConfig["DrawStats"] == "fps_mem")
                {
                    retroarchConfig["fps_show"] = "true";
                    retroarchConfig["memory_show"] = "true";
                    retroarchConfig["statistics_show"] = "false";
                }
                else if (SystemConfig["DrawStats"] == "tech_stats")
                {
                    retroarchConfig["fps_show"] = "false";
                    retroarchConfig["memory_show"] = "false";
                    retroarchConfig["statistics_show"] = "true";
                }
            }
            else
            {
                retroarchConfig["fps_show"] = "false";
                retroarchConfig["memory_show"] = "false";
                retroarchConfig["statistics_show"] = "false";
            }

            // Default controllers
            retroarchConfig["input_libretro_device_p1"] = coreToP1Device.ContainsKey(core) ? coreToP1Device[core] : "1";
            retroarchConfig["input_libretro_device_p2"] = coreToP2Device.ContainsKey(core) ? coreToP2Device[core] : "1";

            if (Controllers.Count > 2 && (core == "snes9x_next" || core == "snes9x"))
                retroarchConfig["input_libretro_device_p2"] = "257";

            if (core == "mednafen_psx" || core == "pcsx_rearmed" || core == "duckstation")
            {
                if (SystemConfig.isOptSet("psxcontroller1"))
                    retroarchConfig["input_libretro_device_p1"] = SystemConfig["psxcontroller1"];
                if (SystemConfig.isOptSet("psxcontroller2"))
                    retroarchConfig["input_libretro_device_p2"] = SystemConfig["psxcontroller2"];
            }

            if (LibretroControllers.WriteControllersConfig(retroarchConfig, system, core))
                UseEsPadToKey = false;

            // Core, services & bezel configs
            ConfigureRetroachievements(retroarchConfig);
            ConfigureNetPlay(retroarchConfig);
            ConfigureAIService(retroarchConfig);
            ConfigureRunahead(system, retroarchConfig);
            ConfigureCoreOptions(retroarchConfig, system, core);
            ConfigureBezels(retroarchConfig, system, rom, resolution);
            
            // Video driver
            ConfigureVideoDriver(core, retroarchConfig);
            ConfigureGPUIndex(retroarchConfig);
            ConfigureVSync(retroarchConfig);

            // Language
            SetLanguage(retroarchConfig);

            // Custom overrides : allow the user to configure directly retroarch.cfg via batocera.conf via lines like : snes.retroarch.menu_driver=rgui
            foreach (var user_config in SystemConfig)
                if (user_config.Name.StartsWith("retroarch."))
                    retroarchConfig[user_config.Name.Substring("retroarch.".Length)] = user_config.Value;
                        
            if (retroarchConfig.IsDirty)
                retroarchConfig.Save(Path.Combine(RetroarchPath, "retroarch.cfg"), true);
        }

        private void ConfigureRunahead(string system, ConfigFile retroarchConfig)
        {
            // if (!Features.IsSupported("runahead"))
            //    return;

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
        }

        private void ConfigureVideoDriver(string core, ConfigFile retroarchConfig)
        {
            if (!Features.IsSupported("video_driver"))
                return;

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
        }

        private void ConfigureGPUIndex(ConfigFile retroarchConfig)
        {
            if (!Features.IsSupported("GPUIndex"))
                return;

            if (SystemConfig.isOptSet("GPUIndex"))
            {
                if (retroarchConfig["video_driver"] == "d3d10")
                    retroarchConfig["d3d10_gpu_index"] = SystemConfig["GPUIndex"];

                if (retroarchConfig["video_driver"] == "d3d11")
                    retroarchConfig["d3d11_gpu_index"] = SystemConfig["GPUIndex"];

                if (retroarchConfig["video_driver"] == "d3d12")
                    retroarchConfig["d3d12_gpu_index"] = SystemConfig["GPUIndex"];

                if (retroarchConfig["video_driver"] == "vulkan")
                    retroarchConfig["vulkan_gpu_index"] = SystemConfig["GPUIndex"];
            }
            else
            {
                retroarchConfig["d3d10_gpu_index"] = "0";
                retroarchConfig["d3d11_gpu_index"] = "0";
                retroarchConfig["d3d12_gpu_index"] = "0";
                retroarchConfig["vulkan_gpu_index"] = "0";
            }            
        }

        /// <summary>
        /// Synchronization options
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureVSync(ConfigFile retroarchConfig)
        {
            BindFeature(retroarchConfig, "video_hard_sync", "video_hard_sync", "false");
            BindFeature(retroarchConfig, "video_swap_interval", "video_swap_interval", "1");
            BindFeature(retroarchConfig, "video_black_frame_insertion", "video_black_frame_insertion", "0");
            BindFeature(retroarchConfig, "vrr_runloop_enable", "vrr_runloop_enable", "false");

            if (Features.IsSupported("video_vsync"))
            {
                if (SystemConfig.isOptSet("video_vsync"))
                {
                    if (SystemConfig["video_vsync"] != "adaptative")
                    {
                        retroarchConfig["video_vsync"] = SystemConfig["video_vsync"];
                        retroarchConfig["video_adaptive_vsync"] = "false";
                    }
                    else
                    {
                        retroarchConfig["video_vsync"] = "true";
                        retroarchConfig["video_adaptive_vsync"] = "true";
                    }
                }
                else if (SystemConfig.isOptSet("VSync") && !SystemConfig.getOptBoolean("VSync"))
                {
                    retroarchConfig["video_vsync"] = "false";
                    retroarchConfig["video_adaptive_vsync"] = "false";
                }
                else
                {
                    retroarchConfig["video_vsync"] = "true";
                    retroarchConfig["video_adaptive_vsync"] = "false";
                }
            }
            else if (SystemConfig.isOptSet("VSync") && !SystemConfig.getOptBoolean("VSync"))
            {
                retroarchConfig["video_vsync"] = "false";
                retroarchConfig["video_adaptive_vsync"] = "false";
            }           
        }

        /// <summary>
        /// AI service for game translations
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureAIService(ConfigFile retroarchConfig)
        {
            // if (!Features.IsSupported("ai_service_enabled"))
            //    return;

            if (SystemConfig.isOptSet("ai_service_enabled") && SystemConfig.getOptBoolean("ai_service_enabled"))
            {
                retroarchConfig["ai_service_enable"] = "true";
                retroarchConfig["ai_service_mode"] = "0";
                retroarchConfig["ai_service_source_lang"] = "0";

                if (!string.IsNullOrEmpty(SystemConfig["ai_service_url"]))
                    retroarchConfig["ai_service_url"] = SystemConfig["ai_service_url"] + "&mode=Fast&output=png&target_lang=" + SystemConfig["ai_target_lang"];
                else
                    retroarchConfig["ai_service_url"] = "http://" + "ztranslate.net/service?api_key=BATOCERA&mode=Fast&output=png&target_lang=" + SystemConfig["ai_target_lang"];

                BindBoolFeature(retroarchConfig, "ai_service_pause", "ai_service_pause", "true", "false");
            }
            else
                retroarchConfig["ai_service_enable"] = "false";
        }

        /// <summary>
        ///  Netplay management : netplaymode client -netplayport " + std::to_string(options.port) + " -netplayip
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureNetPlay(ConfigFile retroarchConfig)
        {
            retroarchConfig["netplay_mode"] = "false";

            if (SystemConfig["netplay"] == "true" && !string.IsNullOrEmpty(SystemConfig["netplaymode"]))
            {
                // Security : hardcore mode disables save states, which would kill netplay
                retroarchConfig["cheevos_hardcore_mode_enable"] = "false";

                retroarchConfig["netplay_ip_port"] = SystemConfig["netplay.port"]; // netplayport
                retroarchConfig["netplay_nickname"] = SystemConfig["netplay.nickname"];

                retroarchConfig["netplay_mitm_server"] = SystemConfig["netplay.relay"];
                retroarchConfig["netplay_use_mitm_server"] = string.IsNullOrEmpty(SystemConfig["netplay.relay"]) ? "false" : "true";

                retroarchConfig["netplay_client_swap_input"] = "false";

                if (SystemConfig["netplaymode"] == "client" || SystemConfig["netplaymode"] == "spectator")
                {
                    retroarchConfig["netplay_mode"] = "true";
                    retroarchConfig["netplay_ip_address"] = SystemConfig["netplayip"];
                    retroarchConfig["netplay_ip_port"] = SystemConfig["netplayport"];
                    retroarchConfig["netplay_client_swap_input"] = "true";
                }

                // connect as client
                if (SystemConfig["netplaymode"] == "client")
                {
                    if (SystemConfig.isOptSet("netplaypass"))
                        retroarchConfig["netplay_password"] = SystemConfig["netplaypass"];
                    else
                        retroarchConfig.DisableAll("netplay_password");
                }

                // connect as spectator
                if (SystemConfig["netplaymode"] == "spectator")
                {
                    retroarchConfig["netplay_spectator_mode_enable"] = "true";
                    retroarchConfig["netplay_start_as_spectator"] = "true";

                    if (SystemConfig.isOptSet("netplaypass"))
                        retroarchConfig["netplay_spectate_password"] = SystemConfig["netplaypass"];
                    else
                        retroarchConfig.DisableAll("netplay_spectate_password");
                }
                else if (base.SystemConfig["netplaymode"] == "host-spectator")
                {
                    retroarchConfig["netplay_spectator_mode_enable"] = "true";
                    retroarchConfig["netplay_start_as_spectator"] = "true";
                    retroarchConfig["netplay_mode"] = "false";
                }
                else
                {
                    if (SystemConfig["netplaymode"] != "host")
                        retroarchConfig["netplay_spectator_mode_enable"] = "false";

                    retroarchConfig["netplay_start_as_spectator"] = "false";
                }

                // Netplay host passwords
                if (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator")
                {
                    if (SystemConfig["netplaymode"] == "host")
                        retroarchConfig["netplay_spectator_mode_enable"] = SystemConfig.getOptBoolean("netplay.spectator") ? "true" : "false";

                    retroarchConfig["netplay_password"] = SystemConfig["netplay.password"];
                    retroarchConfig["netplay_spectate_password"] = SystemConfig["netplay.spectatepassword"];
                }

                // Netplay hide the gameplay
                BindBoolFeature(retroarchConfig, "netplay_public_announce", "netplay_public_announce", "true", "false");
            }

            BindBoolFeature(retroarchConfig, "content_show_netplay", "netplay", "true", "false");
        }

        /// <summary>
        /// Retroachievements / Cheevos
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureRetroachievements(ConfigFile retroarchConfig)
        {
            if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
            {
                // Since 1.10, token is stored & password is reset
                retroarchConfig.DisableAll("cheevos_token");

                retroarchConfig["cheevos_enable"] = "true";
                retroarchConfig["cheevos_username"] = SystemConfig["retroachievements.username"];
                retroarchConfig["cheevos_password"] = SystemConfig["retroachievements.password"];
                retroarchConfig["cheevos_hardcore_mode_enable"] = SystemConfig["retroachievements.hardcore"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_leaderboards_enable"] = SystemConfig["retroachievements.leaderboards"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_verbose_enable"] = SystemConfig["retroachievements.verbose"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_auto_screenshot"] = SystemConfig["retroachievements.screenshot"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_challenge_indicators"] = SystemConfig["retroachievements.challenge_indicators"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_start_active"] = SystemConfig["retroachievements.encore"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_richpresence_enable"] = SystemConfig["retroachievements.richpresence"] == "true" ? "true" : "false";
            }
            else
                retroarchConfig["cheevos_enable"] = "false";
        }

        class UIModeSetting
        {
            public UIModeSetting(string name, string minimal, string recommanded, string full)
            {
                Name = name;
                Minimal = minimal;
                Recommanded = recommanded;
                Full = full;
            }

            public string Name { get; private set; }
            public string Minimal { get; private set; }
            public string Recommanded { get; private set; }
            public string Full { get; private set; }

            public string GetValue(UIModeType type)
            {
                if (type == UIModeType.Minimal)
                    return Minimal;

                if (type == UIModeType.Recommanded)
                    return Recommanded;

                return Full;
            }
        }

        enum UIModeType
        {
            Minimal,
            Recommanded,
            Full
        }

        static UIModeSetting[] UIModes = new UIModeSetting[]
        {
            new UIModeSetting("desktop_menu_enable", "false", "false", "true"),
            new UIModeSetting("content_show_add", "false", "false", "true"),
            new UIModeSetting("content_show_contentless_cores", "false", "false", "true"),
            new UIModeSetting("content_show_explore", "false", "false", "true"),
            new UIModeSetting("content_show_favorite", "false", "false", "true"),
            new UIModeSetting("content_show_favorites", "false", "false", "true"),
            new UIModeSetting("content_show_history", "false", "true", "true"),
            new UIModeSetting("content_show_images", "false", "false", "true"),
            new UIModeSetting("content_show_music", "false", "false", "true"),
            new UIModeSetting("content_show_netplay", "false", "true", "true"),
            new UIModeSetting("content_show_playlists", "false", "false", "true"),
            new UIModeSetting("content_show_video", "false", "false", "true"),
            new UIModeSetting("menu_show_advanced_settings", "false", "false", "true"),
            new UIModeSetting("menu_show_configurations", "false", "false", "true"),
            new UIModeSetting("menu_show_core_updater", "false", "false", "true"),
            new UIModeSetting("menu_show_dump_disc", "false", "false", "true"),
            new UIModeSetting("menu_show_help", "false", "true", "true"),
            new UIModeSetting("menu_show_information", "false", "true", "true"),
            new UIModeSetting("menu_show_latency", "false", "true", "true"),
            new UIModeSetting("menu_show_legacy_thumbnail_updater", "false", "false", "true"),
            new UIModeSetting("menu_show_load_content", "false", "false", "true"),
            //new UIModeSetting("menu_show_load_content_animation", "false", "false", "true"), // not a menu element but a notification
            new UIModeSetting("menu_show_load_core", "false", "false", "true"),
            new UIModeSetting("menu_show_load_disc", "false", "false", "true"),
            new UIModeSetting("menu_show_online_updater", "false", "true", "true"),
            new UIModeSetting("menu_show_overlays", "false", "false", "true"),
            new UIModeSetting("menu_show_reboot", "false", "true", "true"),
            new UIModeSetting("menu_show_restart_retroarch", "false", "false", "true"),
            new UIModeSetting("menu_show_rewind", "false", "true", "true"),
            new UIModeSetting("menu_show_shutdown", "false", "true", "true"),
            new UIModeSetting("menu_show_video_layout", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_add_to_favorites", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_cheats", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_close_content", "false", "false", "true"),
            new UIModeSetting("settings_show_video", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_controls", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_core_options_flush", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_download_thumbnails", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_options", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_recording", "false", "true", "true"),           
            new UIModeSetting("quick_menu_show_reset_core_association", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_restart_content", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_save_content_dir_overrides", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_save_core_overrides", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_save_game_overrides", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_set_core_association", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_shaders", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_start_recording", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_start_streaming", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_streaming", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_take_screenshot", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_undo_save_load_state", "false", "false", "true"),
            // quick_menu_show_save_load_state always true
            new UIModeSetting("settings_show_ai_service", "false", "true", "true"),
            new UIModeSetting("settings_show_audio", "false", "true", "true"),
            new UIModeSetting("settings_show_configuration", "false", "true", "true"),
            new UIModeSetting("settings_show_directory", "false", "false", "true"),
            new UIModeSetting("settings_show_drivers", "false", "true", "true"),
            new UIModeSetting("settings_show_file_browser", "false", "false", "true"),
            new UIModeSetting("settings_show_frame_throttle", "false", "true", "true"),
            new UIModeSetting("settings_show_input", "false", "true", "true"),
            new UIModeSetting("settings_show_latency", "false", "true", "true"),
            new UIModeSetting("settings_show_logging", "false", "true", "true"),
            new UIModeSetting("settings_show_network", "false", "true", "true"),
            new UIModeSetting("settings_show_onscreen_display", "false", "true", "true"),
            new UIModeSetting("settings_show_playlists", "false", "false", "true"),
            new UIModeSetting("settings_show_power_management", "false", "true", "true"),
            new UIModeSetting("settings_show_recording", "false", "true", "true"),
            new UIModeSetting("settings_show_saving", "false", "true", "true"),
            new UIModeSetting("settings_show_user", "false", "true", "true"),
            new UIModeSetting("settings_show_user_interface", "false", "true", "true"),
            new UIModeSetting("settings_show_video", "false", "true", "true"),
            new UIModeSetting("kiosk_mode_enable", "true", "false", "false")
        };

        // Retroarch menu : different level of options appearing or not in the retroarch menu
        private void SetupUIMode(ConfigFile retroarchConfig)
        {
            UIModeType type = UIModeType.Recommanded;

            if (SystemConfig["UIMode"] == "Kid" || SystemConfig["UIMode"] == "Kiosk" || SystemConfig["OptionsMenu"] == "minimal")
                type = UIModeType.Minimal;
            else if (SystemConfig["OptionsMenu"] == "full")
                type = UIModeType.Full;

            foreach(var item in UIModes)
                retroarchConfig[item.Name] = item.GetValue(type);
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

        private void ConfigureBezels(ConfigFile retroarchConfig, string systemName, string rom, ScreenResolution resolution)
        {
            retroarchConfig["input_overlay_hide_in_menu"] = "false";
            retroarchConfig["input_overlay_enable"] = "false";
            retroarchConfig["video_message_pos_x"] = "0.05";
            retroarchConfig["video_message_pos_y"] = "0.05";

            if (systemName == "wii")
                return;

            var bezelInfo = BezelFiles.GetBezelFiles(systemName, rom, resolution);
            if (bezelInfo == null)
                return;

            string overlay_png_file = bezelInfo.PngFile;

            Size imageSize;

            try
            {
                imageSize = GetImageSize(overlay_png_file);
            }
            catch 
            {
                return;
            }

            BezelInfo infos = bezelInfo.BezelInfos;

             // if image is not at the correct size, find the correct size
            bool bezelNeedAdaptation = false;
            bool viewPortUsed = true;

            if (!infos.IsValid())
                viewPortUsed = false;

         // for testing ->   
            //resolution = ScreenResolution.Parse("2280x1080x32x60");
            //resolution = ScreenResolution.Parse("3840x2160x32x60");                    
            
            int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

            float screenRatio  = (float) resX / (float) resY;
            float bezelRatio = (float)imageSize.Width / (float) imageSize.Height;

            if (viewPortUsed)
            {
                if (resX != infos.width.GetValueOrDefault() || resY != infos.height.GetValueOrDefault())
                {
                    if (screenRatio < 1.6) // use bezels only for 16:10, 5:3, 16:9 and wider aspect ratios
                        return;
                    else
                        bezelNeedAdaptation = true;
                }

                if (!SystemConfig.isOptSet("ratio"))
                {
                    if (systemName == "mame")
                        retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString();
                    else
                        retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("custom").ToString(); // overwritten from the beginning of this file                
                }
            }
            else
            {
                 // when there is no information about width and height in the .info, assume that the tv is HD 16/9 and infos are core provided
                if (screenRatio < 1.6) // use bezels only for 16:10, 5:3, 16:9 and wider aspect ratios
                    return;

                infos.width = imageSize.Width;
                infos.height = imageSize.Height;
                bezelNeedAdaptation = true;
                
                if (!SystemConfig.isOptSet("ratio"))
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
                float wratio = resX / (float)infos.width;
                float hratio = resY / (float)infos.height;

                int xoffset = resX - infos.width.Value;
                int yoffset = resY - infos.height.Value;

                bool stretchImage = false;

                if (resX < infos.width || resY < infos.height) // If width or height < original, can't add black borders. Just stretch
                    stretchImage = true;
                else if (Math.Abs(screenRatio - bezelRatio) < 0.2) // FCA : About the same ratio ? Just stretch
                    stretchImage = true;

                if (viewPortUsed)
                {
                    if (stretchImage)
                    {
                        retroarchConfig["custom_viewport_x"] = ((int)(infos.left * wratio)).ToString();
                        retroarchConfig["custom_viewport_y"] = ((int)(infos.top * hratio)).ToString();
                        retroarchConfig["custom_viewport_width"] = ((int)((infos.width - infos.left - infos.right) * wratio)).ToString();
                        retroarchConfig["custom_viewport_height"] = ((int)((infos.height - infos.top - infos.bottom) * hratio)).ToString();
                        retroarchConfig["video_message_pos_x"] = (infos.messagex.Value * wratio).ToString(CultureInfo.InvariantCulture);
                        retroarchConfig["video_message_pos_y"] = (infos.messagey.Value * hratio).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        retroarchConfig["custom_viewport_x"] = ((int)(infos.left + xoffset / 2)).ToString();
                        retroarchConfig["custom_viewport_y"] = ((int)(infos.top + yoffset / 2)).ToString();
                        retroarchConfig["custom_viewport_width"] = ((int)((infos.width - infos.left - infos.right))).ToString();
                        retroarchConfig["custom_viewport_height"] = ((int)((infos.height - infos.top - infos.bottom))).ToString();
                        retroarchConfig["video_message_pos_x"] = (infos.messagex.Value + xoffset / 2).ToString(CultureInfo.InvariantCulture);
                        retroarchConfig["video_message_pos_y"] = (infos.messagey.Value + yoffset / 2).ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (!stretchImage)
                    overlay_png_file = BezelFiles.GetStretchedBezel(overlay_png_file, resX, resY);
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

            if (retroarchConfig["video_fullscreen"] != "true")
                retroarchConfig["input_overlay_show_mouse_cursor"] = "true";
            else
                retroarchConfig["input_overlay_show_mouse_cursor"] = "false";

            StringBuilder fd = new StringBuilder();
            fd.AppendLine("overlays = 1");
            fd.AppendLine("overlay0_overlay = \"" + overlay_png_file + "\"");
            fd.AppendLine("overlay0_full_screen = true");
            fd.AppendLine("overlay0_descs = 0");
            File.WriteAllText(overlay_cfg_file, fd.ToString());
        }

        private static Size GetImageSize(string file)
        {
            using (Image img = Image.FromFile(file))
                return img.Size;
        }

        private string _dosBoxTempRom;

        public override void Cleanup()
        {
            if (SystemConfig["core"] == "atari800")
                Environment.SetEnvironmentVariable("HOME", CurrentHomeDirectory);

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

            string subCore = null;

            if (!string.IsNullOrEmpty(core))
            {
                int split = core.IndexOfAny(new char[] { ':', '/' });
                if (split >= 0)
                {
                    subCore = core.Substring(split + 1);
                    core = core.Substring(0, split);

                    SystemConfig["subcore"] = subCore;
                }
            }

            if (Path.GetExtension(rom).ToLowerInvariant() == ".game")
                core = Path.GetFileNameWithoutExtension(rom);
            else if (Path.GetExtension(rom).ToLowerInvariant() == ".libretro")
            {
                core = Path.GetFileNameWithoutExtension(rom);

                if (core == "xrick")
                    rom = Path.Combine(Path.GetDirectoryName(rom), "xrick", "data.zip");
                else if (core == "dinothawr")
                    rom = Path.Combine(Path.GetDirectoryName(rom), "dinothawr", "dinothawr.game");
                else
                    rom = null;
            }
            
            if (string.IsNullOrEmpty(core))
            {
                ExitCode = ExitCodes.MissingCore;
                SimpleLogger.Instance.Error("[LibretroGenerator] Core was not provided");
                return null;
            }
            else
            {
                string corePath = Path.Combine(RetroarchCorePath, core + "_libretro.dll");
                if (!File.Exists(corePath))
                {
                    try
                    {

                        string url = Installer.GetUpdateUrl("cores/" + core + "_libretro.dll.zip");
                        if (!WebTools.UrlExists(url))
                        {
                            // Automatic install of missing core
                            var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"));

                            url = retroarchConfig["core_updater_buildbot_cores_url"];
                            if (!string.IsNullOrEmpty(url))
                                url += core + "_libretro.dll.zip";
                        }

                        if (WebTools.UrlExists(url))
                        {
                            using (var frm = new InstallerFrm(core, url, RetroarchCorePath))
                                frm.ShowDialog();
                        }
                    }
                    catch { }

                    if (!File.Exists(corePath))
                    {
                        SimpleLogger.Instance.Error("[LibretroGenerator] Core is not installed");
                        ExitCode = ExitCodes.MissingCore;
                        return null;
                    }
                }
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
                if (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator")
                    commandArray.Add("--host");
                else if (SystemConfig["netplaymode"] == "client" || SystemConfig["netplaymode"] == "spectator")
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

            if (core == "atari800")
            {
                // Special case : .atari800.cfg is loaded from path in 'HOME' environment variable
                CurrentHomeDirectory = Environment.GetEnvironmentVariable("HOME");
                Environment.SetEnvironmentVariable("HOME", RetroarchPath);
            }

            MessSystem messSystem = core == "mame" ? MessSystem.GetMessSystem(system, subCore) : null;
            if (messSystem != null && !string.IsNullOrEmpty(messSystem.MachineName))
            {
                var messArgs = messSystem.GetMameCommandLineArguments(system, rom);
                messArgs = messArgs.Replace("\\\"", "\"");
                messArgs = "\"" + messArgs.Replace("\"", "\\\"") + "\"";
                messArgs = (messArgs + " " + args).Trim();

                return new ProcessStartInfo()
                {
                    FileName = Path.Combine(RetroarchPath, emulator == "angle" ? "retroarch_angle.exe" : "retroarch.exe"),
                    WorkingDirectory = RetroarchPath,
                    Arguments = ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" " + messArgs).Trim()
                };
            }

            string retroarch = Path.Combine(RetroarchPath, emulator == "angle" ? "retroarch_angle.exe" : "retroarch.exe");
            if (emulator != "angle" && SystemConfig["netplay"] == "true" && (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator"))
                retroarch = GetNetPlayPatchedRetroarch();

            return new ProcessStartInfo()
            {
                FileName = retroarch,
                WorkingDirectory = RetroarchPath,
                Arguments =
                    string.IsNullOrEmpty(rom) ?
                        ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" " + args).Trim() :
                        ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" \"" + rom + "\" " + args).Trim()
            };
        }


        /// <summary>
        /// Patch Retroarch to display @RETROBAT in netplay architecture
        /// </summary>
        /// <returns></returns>
        private string GetNetPlayPatchedRetroarch()
        {
            string fn = Path.Combine(RetroarchPath, "retroarch.exe");
            if (!File.Exists(fn))
                return fn;

            string patched = Path.Combine(RetroarchPath, "retroarch.patched." + RetroArchNetPlayPatchedName + ".exe");
            if (File.Exists(patched) && new FileInfo(fn).Length == new FileInfo(patched).Length)
                return patched;

            try { File.Delete(patched); }
            catch { }

            var toFind = "username=%s&core_name=%s&core_version=%s&game_name=%s&game_crc=%08lX&port=%hu&mitm_server=%s&has_password=%d&has_spectate_password=%d&force_mitm=%d&retroarch_version=%s&frontend=%s&subsystem_name=%s"
                .Select(c => (byte)c)
                .ToArray();

            var toSet = toFind.ToArray();
            var toSubst = "&subsystem_name=%s".Select(c => (byte)c).ToArray();
            int idx = toFind.IndexOf(toSubst);
            if (idx < 0)
                return fn;

            var bytes = File.ReadAllBytes(fn);
            int index = bytes.IndexOf(toFind);
            if (index < 0)
            {
                toFind = "username=%s&core_name=%s&core_version=%s&game_name=%s&game_crc=%08lX&port=%d&mitm_server=%s&has_password=%d&has_spectate_password=%d&force_mitm=%d&retroarch_version=%s&frontend=%s&subsystem_name=%s"
               .Select(c => (byte)c)
               .ToArray();

                toSet = toFind.ToArray();
                toSubst = "&subsystem_name=%s".Select(c => (byte)c).ToArray();
                idx = toFind.IndexOf(toSubst);
                if (idx < 0)
                    return fn;

                index = bytes.IndexOf(toFind);
                if (index < 0)
                    return fn;
            }

            string patchString = "@" + RetroArchNetPlayPatchedName;

            var toPatch = patchString.Select(c => (byte)c).ToArray();
            for (int i = 0; i < patchString.Length + 1; i++)
            {
                if (i == patchString.Length)
                    toSet[idx + i] = 0;
                else
                    toSet[idx + i] = toPatch[i];
            }

            for (int i = 0; i < toSet.Length; i++)
                bytes[index + i] = toSet[i];

            File.WriteAllBytes(patched, bytes);
            return patched;
        }

        static List<string> ratioIndexes = new List<string> { "4/3", "16/9", "16/10", "16/15", "21/9", "1/1", "2/1", "3/2", "3/4", "4/1", "4/4", "5/4", "6/5", "7/9", "8/3",
                "8/7", "19/12", "19/14", "30/17", "32/9", "config", "squarepixel", "core", "custom", "full" };

        static List<string> systemNoRewind = new List<string>() { "nds", "3ds", "sega32x", "wii", "gamecube", "gc", "psx", "zxspectrum", "odyssey2", "n64", "dreamcast", "atomiswave", "naomi", "naomi2", "neogeocd", "saturn", "mame", "hbmame", "fbneo" };
        static List<string> systemNoRunahead = new List<string>() { "nds", "3ds", "sega32x", "wii", "gamecube", "n64", "dreamcast", "atomiswave", "naomi", "naomi2", "neogeocd", "saturn" };
        
        static Dictionary<string, string> coreToP1Device = new Dictionary<string, string>() { { "atari800", "513" }, { "cap32", "513" }, { "81", "257" }, { "fuse", "513" } };
        static Dictionary<string, string> coreToP2Device = new Dictionary<string, string>() { { "atari800", "513" }, { "fuse", "513" } };

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
    

}
