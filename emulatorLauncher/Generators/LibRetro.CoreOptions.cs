using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using emulatorLauncher.Tools;

namespace emulatorLauncher.libRetro
{
    partial class LibRetroGenerator : Generator
    {
        private void ConfigureCoreOptions(ConfigFile retroarchConfig, string system, string core)
        {
            var coreSettings = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), new ConfigFileOptions() { CaseSensitive = true });

            ConfigureStella(retroarchConfig, coreSettings, system, core);
            ConfigureOpera(retroarchConfig, coreSettings, system, core);
            Configure4Do(retroarchConfig, coreSettings, system, core);
            ConfigureBlueMsx(retroarchConfig, coreSettings, system, core);
            ConfigureTheodore(retroarchConfig, coreSettings, system, core);
            ConfigureHandy(retroarchConfig, coreSettings, system, core);
            ConfigureFCEumm(retroarchConfig, coreSettings, system, core);
            ConfigureNestopia(retroarchConfig, coreSettings, system, core);
            ConfigureO2em(retroarchConfig, coreSettings, system, core);
            ConfigureMame2003(retroarchConfig, coreSettings, system, core);
            ConfigureAtari800(retroarchConfig, coreSettings, system, core);
            ConfigureVirtualJaguar(retroarchConfig, coreSettings, system, core);
            ConfigureSNes9x(retroarchConfig, coreSettings, system, core);
            ConfigureMupen64(retroarchConfig, coreSettings, system, core);
            ConfigurePuae(retroarchConfig, coreSettings, system, core);
            ConfigureFlycast(retroarchConfig, coreSettings, system, core);
            ConfigureMesen(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPsxHW(retroarchConfig, coreSettings, system, core);
            ConfigureCap32(retroarchConfig, coreSettings, system, core);
            ConfigureQuasi88(retroarchConfig, coreSettings, system, core);
            ConfigureGenesisPlusGX(retroarchConfig, coreSettings, system, core);
            ConfigureGenesisPlusGXWide(retroarchConfig, coreSettings, system, core);
            ConfigurePotator(retroarchConfig, coreSettings, system, core);
            ConfigureDosboxPure(retroarchConfig, coreSettings, system, core);
            ConfigureKronos(retroarchConfig, coreSettings, system, core);
            ConfigurePicodrive(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenSaturn(retroarchConfig, coreSettings, system, core);
            ConfigureCitra(retroarchConfig, coreSettings, system, core);
            ConfigureFbneo(retroarchConfig, coreSettings, system, core);
            ConfigureGambatte(retroarchConfig, coreSettings, system, core);
            ConfigurePpsspp(retroarchConfig, coreSettings, system, core);
            ConfigureMame(retroarchConfig, coreSettings, system, core);
            ConfigureFbalphaCPS1(retroarchConfig, coreSettings, system, core);
            ConfigureFbalphaCPS2(retroarchConfig, coreSettings, system, core);
            ConfigureFbalphaCPS3(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPce(retroarchConfig, coreSettings, system, core);
            ConfigureNeocd(retroarchConfig, coreSettings, system, core);

            if (coreSettings.IsDirty)
                coreSettings.Save(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), true);

            // Disable Bezel as default if a widescreen ratio is set. Can be manually set.
            if (SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
            {
                int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                if (idx == 1 || idx == 2 || idx == 4 || idx == 6 || idx == 7 || idx == 9 || idx == 14 || idx == 16 || idx == 18 || idx == 19)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                }
            }
        }

        /// <summary>
        /// Injects keyboard actions for lightgun games
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="playerId"></param>
        private void ConfigurePlayer1LightgunKeyboardActions(ConfigFile retroarchConfig)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            var keyb = Controllers.Where(c => c.Name == "Keyboard" && c.Config != null && c.Config.Input != null).Select(c => c.Config).FirstOrDefault();
            if (keyb != null)
            {
                var start = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.start);
                retroarchConfig["input_player1_gun_start"] = start == null ? "nul" : LibretroControllers.GetConfigValue(start);

                var select = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.select);
                retroarchConfig["input_player1_gun_select"] = select == null ? "nul" : LibretroControllers.GetConfigValue(select);
            }
            else
            {
                retroarchConfig["input_player1_gun_start"] = "enter";
                retroarchConfig["input_player1_gun_select"] = "space";
            }
        }

        private void ConfigureNeocd(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "neocd")
                return;

            coreSettings["neocd_per_content_saves"] = "On";

            BindFeature(coreSettings, "neocd_bios", "neocd_bios", "uni-bioscd.rom (CDZ, Universe 3.3)");
            BindFeature(coreSettings, "neocd_cdspeedhack", "neocd_cdspeedhack", "Off");
            BindFeature(coreSettings, "neocd_loadskip", "neocd_loadskip", "Off");
            BindFeature(coreSettings, "neocd_region", "neocd_region", "USA");
        }

        private void ConfigureMednafenPce(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_pce")
                return;

            coreSettings["pce_show_advanced_input_settings"] = "enabled";

            BindFeature(coreSettings, "pce_psgrevision", "pce_psgrevision", "auto");
            BindFeature(coreSettings, "pce_resamp_quality", "pce_resamp_quality", "3");
            BindFeature(coreSettings, "pce_ocmultiplier", "pce_ocmultiplier", "1");
            BindFeature(coreSettings, "pce_nospritelimit", "pce_nospritelimit", "disabled");
            BindFeature(coreSettings, "pce_cdimagecache", "pce_cdimagecache", "disabled");
            BindFeature(coreSettings, "pce_cdbios", "pce_cdbios", "System Card 3");
            BindFeature(coreSettings, "pce_cdspeed", "pce_cdspeed", "1");
            BindFeature(coreSettings, "pce_palette", "pce_palette", "Composite");
            BindFeature(coreSettings, "pce_scaling", "pce_scaling", "auto");
            BindFeature(coreSettings, "pce_hires_blend", "pce_hires_blend", "disabled");
            BindFeature(coreSettings, "pce_h_overscan", "pce_h_overscan", "auto");
            BindFeature(coreSettings, "pce_adpcmextraprec", "pce_adpcmextraprec", "12-bit");
            BindFeature(coreSettings, "pce_adpcmvolume", "pcecdvolume", "100");
            BindFeature(coreSettings, "pce_cddavolume", "pcecdvolume", "100");
            BindFeature(coreSettings, "pce_cdpsgvolume", "pcecdvolume", "100");
        }

        private void ConfigureFbalphaCPS3(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps3")
                return;

            coreSettings["fbalpha2012_cps3_frameskip"] = "0";
            coreSettings["fbalpha2012_cps3_aspect"] = "DAR";

            BindFeature(coreSettings, "fbalpha2012_cps3_cpu_speed_adjust", "fbalpha2012_cps3_cpu_speed_adjust", "100");
            BindFeature(coreSettings, "fbalpha2012_cps3_hiscores", "fbalpha2012_cps3_hiscores", "enabled");
            BindFeature(coreSettings, "fbalpha2012_cps3_controls_p1", "fbalpha2012_cps3_controls_p1", "gamepad");
            BindFeature(coreSettings, "fbalpha2012_cps3_controls_p2", "fbalpha2012_cps3_controls_p2", "gamepad");
            BindFeature(coreSettings, "fbalpha2012_cps3_lr_controls_p1", "fbalpha2012_cps3_lr_controls_p1", "normal");
            BindFeature(coreSettings, "fbalpha2012_cps3_lr_controls_p2", "fbalpha2012_cps3_lr_controls_p2", "normal");
        }

        private void ConfigureFbalphaCPS2(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps2")
                return;

            coreSettings["fba2012cps2_frameskip"] = "disabled";
            coreSettings["fba2012cps2_aspect"] = "DAR";

            BindFeature(coreSettings, "fba2012cps2_auto_rotate", "fba2012cps1_auto_rotate", "enabled");
            BindFeature(coreSettings, "fba2012cps2_cpu_speed_adjust", "fba2012cps1_cpu_speed_adjust", "100");
            BindFeature(coreSettings, "fba2012cps2_hiscores", "fba2012cps1_hiscores", "enabled");
            BindFeature(coreSettings, "fba2012cps2_lowpass_filter", "fba2012cps1_lowpass_filter", "disabled");
            BindFeature(coreSettings, "fba2012cps2_lowpass_range", "fba2012cps1_lowpass_range", "50");
            BindFeature(coreSettings, "fba2012cps2_controls", "fba2012cps2_controls", "gamepad");
        }

        private void ConfigureFbalphaCPS1(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps1")
                return;

            coreSettings["fba2012cps1_frameskip"] = "disabled";
            coreSettings["fba2012cps1_aspect"] = "DAR";

            BindFeature(coreSettings, "fba2012cps1_auto_rotate", "fba2012cps1_auto_rotate", "enabled");
            BindFeature(coreSettings, "fba2012cps1_cpu_speed_adjust", "fba2012cps1_cpu_speed_adjust", "100");
            BindFeature(coreSettings, "fba2012cps1_hiscores", "fba2012cps1_hiscores", "enabled");
            BindFeature(coreSettings, "fba2012cps1_lowpass_filter", "fba2012cps1_lowpass_filter", "disabled");
            BindFeature(coreSettings, "fba2012cps1_lowpass_range", "fba2012cps1_lowpass_range", "50");
        }

        private void ConfigurePpsspp(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "ppsspp")
                return;

            coreSettings["ppsspp_cpu_core"] = "jit";
            coreSettings["ppsspp_auto_frameskip"] = "disabled";
            coreSettings["ppsspp_frameskip"] = "0";
            coreSettings["ppsspp_frameskiptype"] = "number of frames";
            coreSettings["ppsspp_rendering_mode"] = "buffered";
            coreSettings["ppsspp_locked_cpu_speed"] = "off";
            coreSettings["ppsspp_cheats"] = "enabled";
            coreSettings["ppsspp_button_preference"] = "cross";

            switch (SystemConfig["PerformanceMode"])
            {
                case "Fast":
                    coreSettings["ppsspp_block_transfer_gpu"] = "disabled";
                    coreSettings["ppsspp_spline_quality"] = "low";
                    coreSettings["ppsspp_software_skinning"] = "enabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "enabled";
                    coreSettings["ppsspp_vertex_cache"] = "enabled";
                    coreSettings["ppsspp_fast_memory"] = "enabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "enabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "enabled";
                    coreSettings["ppsspp_force_lag_sync"] = "disabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "enabled";
                    break;
                case "Balanced":
                    coreSettings["ppsspp_block_transfer_gpu"] = "enabled";
                    coreSettings["ppsspp_spline_quality"] = "medium";
                    coreSettings["ppsspp_software_skinning"] = "disabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "enabled";
                    coreSettings["ppsspp_vertex_cache"] = "enabled";
                    coreSettings["ppsspp_fast_memory"] = "enabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "disabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "disabled";
                    coreSettings["ppsspp_force_lag_sync"] = "disabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "disabled";
                    break;
                case "Accurate":
                    coreSettings["ppsspp_block_transfer_gpu"] = "enabled";
                    coreSettings["ppsspp_spline_quality"] = "high";
                    coreSettings["ppsspp_software_skinning"] = "disabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "disabled";
                    coreSettings["ppsspp_vertex_cache"] = "disabled";
                    coreSettings["ppsspp_fast_memory"] = "disabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "disabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "disabled";
                    coreSettings["ppsspp_force_lag_sync"] = "enabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "disabled";
                    break;
                default:
                    coreSettings["ppsspp_block_transfer_gpu"] = "enabled";
                    coreSettings["ppsspp_spline_quality"] = "medium";
                    coreSettings["ppsspp_software_skinning"] = "disabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "enabled";
                    coreSettings["ppsspp_vertex_cache"] = "enabled";
                    coreSettings["ppsspp_fast_memory"] = "enabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "disabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "disabled";
                    coreSettings["ppsspp_force_lag_sync"] = "disabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "disabled";
                    break;
            }

            BindFeature(coreSettings, "ppsspp_internal_resolution", "ppsspp_internal_resolution", "1440x816");
            BindFeature(coreSettings, "ppsspp_texture_anisotropic_filtering", "ppsspp_texture_anisotropic_filtering", "off");
            BindFeature(coreSettings, "ppsspp_texture_filtering", "ppsspp_texture_filtering", "auto");
            BindFeature(coreSettings, "ppsspp_texture_scaling_type", "ppsspp_texture_scaling_type", "xbrz");
            BindFeature(coreSettings, "ppsspp_texture_scaling_level", "ppsspp_texture_scaling_level", "auto");
            BindFeature(coreSettings, "ppsspp_texture_deposterize", "ppsspp_texture_deposterize", "disabled");
            BindFeature(coreSettings, "ppsspp_language", "ppsspp_language", "automatic");
            BindFeature(coreSettings, "ppsspp_io_timing_method", "ppsspp_io_timing_method", "Fast");
            BindFeature(coreSettings, "ppsspp_ignore_bad_memory_access", "ppsspp_ignore_bad_memory_access", "enabled");
            BindFeature(coreSettings, "ppsspp_texture_replacement", "ppsspp_texture_replacement", "disabled");
        }

        private void ConfigureGambatte(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "gambatte")
                return;

            coreSettings["gambatte_gb_bootloader"] = "enabled";
            coreSettings["gambatte_gbc_color_correction_mode"] = "accurate";
            coreSettings["gambatte_gbc_color_correction"] = "GBC only";
            coreSettings["gambatte_up_down_allowed"] = "disabled";

            BindFeature(coreSettings, "gambatte_gb_hwmode", "gambatte_gb_hwmode", "Auto");
            BindFeature(coreSettings, "gambatte_mix_frames", "gambatte_mix_frames", "lcd_ghosting");
            BindFeature(coreSettings, "gambatte_gb_internal_palette", "gambatte_gb_internal_palette", "GB - DMG");
            BindFeature(coreSettings, "gambatte_gb_colorization", "gambatte_gb_colorization", "auto");
        }

        private void ConfigureFbneo(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbneo")
                return;

            coreSettings["fbneo-allow-depth-32"] = "enabled";
            coreSettings["fbneo-allow-patched-romsets"] = "enabled";
            coreSettings["fbneo-memcard-mode"] = "per-game";
            coreSettings["fbneo-hiscores"] = "enabled";
            coreSettings["fbneo-load-subsystem-from-parent"] = "enabled";
            coreSettings["fbneo-fm-interpolation"] = "4-point 3rd order";
            coreSettings["fbneo-sample-interpolation"] = "4-point 3rd order";

            BindFeature(coreSettings, "fbneo-neogeo-mode", "fbneo-neogeo-mode", "UNIBIOS");
            BindFeature(coreSettings, "fbneo-vertical-mode", "fbneo-vertical-mode", "disabled");
            BindFeature(coreSettings, "fbneo-lightgun-hide-crosshair", "fbneo-lightgun-hide-crosshair", "disabled");

            if (SystemConfig["fbneo-vertical-mode"] == "enabled")
                SystemConfig["bezel"] = "none";

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "4";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_aux_a_mbtn"] = "2"; // # for all games ?
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void ConfigureCitra(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "citra")
                return;

            coreSettings["citra_use_libretro_save_path"] = "LibRetro Default";
            coreSettings["citra_is_new_3ds"] = "New 3DS";

            if (SystemConfig.isOptSet("citra_layout_option"))
            {
                coreSettings["citra_layout_option"] = SystemConfig["citra_layout_option"];
                if ((SystemConfig["citra_layout_option"] == "Large Screen, Small Screen") && !SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
                {
                    retroarchConfig["aspect_ratio_index"] = "1";
                    SystemConfig["bezel"] = "none";
                }
                else if ((SystemConfig["citra_layout_option"] == "Single Screen Only") && !SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
                {
                    retroarchConfig["aspect_ratio_index"] = "2";
                    SystemConfig["bezel"] = "none";
                }
                else
                    SystemConfig["bezel"] = SystemConfig["bezel"];
            }
            else
                coreSettings["citra_layout_option"] = "Default Top-Bottom Screen";

            BindFeature(coreSettings, "citra_mouse_show_pointer", "citra_mouse_show_pointer", "enabled");
            BindFeature(coreSettings, "citra_region_value", "citra_region_value", "Auto");
            BindFeature(coreSettings, "citra_resolution_factor", "citra_resolution_factor", "1x (Native)");
            BindFeature(coreSettings, "citra_swap_screen", "citra_swap_screen", "Top");
            BindFeature(coreSettings, "citra_mouse_touchscreen", "citra_mouse_touchscreen", "enabled");
        }

        private void ConfigureMednafenSaturn(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_saturn")
                return;

            coreSettings["beetle_saturn_autortc"] = "enabled";
            coreSettings["beetle_saturn_shared_ext"] = "enabled";
            coreSettings["beetle_saturn_shared_int"] = "enabled";

            BindFeature(coreSettings, "beetle_saturn_autortc_lang", "beetle_saturn_autortc_lang", "english");
            BindFeature(coreSettings, "beetle_saturn_cart", "beetle_saturn_cart", "Auto Detect");
            BindFeature(coreSettings, "beetle_saturn_cdimagecache", "beetle_saturn_cdimagecache", "disabled");
            BindFeature(coreSettings, "beetle_saturn_midsync", "beetle_saturn_midsync", "disabled");
            BindFeature(coreSettings, "beetle_saturn_multitap_port1", "beetle_saturn_multitap_port1", "disabled");
            BindFeature(coreSettings, "beetle_saturn_multitap_port2", "beetle_saturn_multitap_port2", "disabled");
            BindFeature(coreSettings, "beetle_saturn_region", "beetle_saturn_region", "Auto Detect");
            BindFeature(coreSettings, "beetle_saturn_midsync", "beetle_saturn_midsync", "disabled");

            // NEW
            BindFeature(coreSettings, "beetle_saturn_virtuagun_crosshair", "beetle_saturn_virtuagun_crosshair", "cross", true);
            BindFeature(coreSettings, "beetle_saturn_mouse_sensitivity", "beetle_saturn_mouse_sensitivity", "100%");
            BindFeature(coreSettings, "beetle_saturn_virtuagun_input", "beetle_saturn_virtuagun_input", "lightgun", true);

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "260";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void ConfigurePicodrive(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "picodrive")
                return;

            coreSettings["picodrive_ramcart"] = "disabled";

            BindFeature(coreSettings, "picodrive_overclk68k", "overclk68k", "disabled");
            BindFeature(coreSettings, "picodrive_overscan", "overscan", "disabled");
            BindFeature(coreSettings, "picodrive_region", "region", "Auto");
            BindFeature(coreSettings, "picodrive_renderer", "renderer", "accurate");
            BindFeature(coreSettings, "picodrive_audio_filter", "audio_filter", "disabled");
            BindFeature(coreSettings, "picodrive_drc", "dynamic_recompiler", "disabled");
            BindFeature(coreSettings, "picodrive_input1", "input1", "3 button pad");
            BindFeature(coreSettings, "picodrive_input2", "input2", "3 button pad");
        }

        private void ConfigureKronos(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "kronos")
                return;

            coreSettings["kronos_use_beetle_saves"] = "enabled";
            coreSettings["kronos_multitap_port2"] = "disabled";
            coreSettings["kronos_sh2coretype"] = "kronos";

            BindFeature(coreSettings, "kronos_addon_cartridge", "addon_cartridge", "512K_backup_ram");
            BindFeature(coreSettings, "kronos_force_downsampling", "force_downsampling", "disabled");
            BindFeature(coreSettings, "kronos_language_id", "language_id", "English");
            BindFeature(coreSettings, "kronos_meshmode", "meshmode", "disabled");
            BindFeature(coreSettings, "kronos_multitap_port1", "multitap_port1", "disabled");
            BindFeature(coreSettings, "kronos_polygon_mode", "polygon_mode", "cpu_tesselation");
            BindFeature(coreSettings, "kronos_resolution_mode", "resolution_mode", "original");
            BindFeature(coreSettings, "kronos_use_cs", "use_cs", "disabled");
            BindFeature(coreSettings, "kronos_videocoretype", "videocoretype", "opengl");
            BindFeature(coreSettings, "kronos_videoformattype", "videoformattype", "auto");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "260";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void ConfigureHandy(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "handy")
                return;

            coreSettings["handy_rot"] = "None";
        }

        private void ConfigureTheodore(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "theodore")
                return;

            coreSettings["theodore_autorun"] = "enabled";
        }


        static List<KeyValuePair<string, string>> operaHacks = new List<KeyValuePair<string, string>>() 
            {
                new KeyValuePair<string, string>("crashnburn", "timing_hack1"),
                new KeyValuePair<string, string>("dinopark tycoon", "timing_hack3"),
                new KeyValuePair<string, string>("microcosm", "timing_hack5"),
                new KeyValuePair<string, string>("aloneinthedark", "timing_hack6"),
                new KeyValuePair<string, string>("samuraishowdown", "hack_graphics_step_y")
            };

        private void ConfigureOpera(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "opera")
                return;

            coreSettings["opera_dsp_threaded"] = "enabled";

            BindFeature(coreSettings, "opera_high_resolution", "high_resolution", "enabled");
            BindFeature(coreSettings, "opera_cpu_overclock", "cpu_overclock", "1.0x (12.50Mhz)");
            BindFeature(coreSettings, "opera_active_devices", "active_devices", "1");

            // Game hacks
            string rom = SystemConfig["rom"].AsIndexedRomName();
            foreach (var hackName in operaHacks.Select(h => h.Value).Distinct())
                coreSettings["opera_" + hackName] = operaHacks.Any(h => h.Value == hackName && rom.Contains(h.Key)) ? "enabled" : "disabled";

            // If ROM includes the word 'Disc', assume it's a multi disc game, and enable shared nvram if the option isn't set.
            if (Features.IsSupported("opera_nvram_storage"))
            {
                if (SystemConfig.isOptSet("nvram_storage"))
                    coreSettings["opera_nvram_storage"] = SystemConfig["nvram_storage"];
                else if (!string.IsNullOrEmpty(SystemConfig["rom"]) && SystemConfig["rom"].ToLower().Contains("disc"))
                    coreSettings["opera_nvram_storage"] = "shared";
                else
                    coreSettings["opera_nvram_storage"] = "per game";
            }

            // Lightgun
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "260";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void ConfigureStella(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "stella")
                return;

            // Lightgun
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "4";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void Configure4Do(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "4do")
                return;

            BindFeature(coreSettings, "4do_high_resolution", "high_resolution", "enabled");
            BindFeature(coreSettings, "4do_cpu_overclock", "cpu_overclock", "1.0x (12.50Mhz)");
            BindFeature(coreSettings, "4do_active_devices", "active_devices", "1");

            // Game hacks
            string rom = SystemConfig["rom"].AsIndexedRomName();
            foreach (var hackName in operaHacks.Select(h => h.Value).Distinct())
                coreSettings["4do_" + hackName] = operaHacks.Any(h => h.Value == hackName && rom.Contains(h.Key)) ? "enabled" : "disabled";

            // If ROM includes the word 'Disc', assume it's a multi disc game, and enable shared nvram if the option isn't set.
            if (Features.IsSupported("4do_nvram_storage"))
            {
                if (SystemConfig.isOptSet("nvram_storage"))
                    coreSettings["4do_nvram_storage"] = SystemConfig["nvram_storage"];
                else if (!string.IsNullOrEmpty(SystemConfig["rom"]) && SystemConfig["rom"].ToLower().Contains("disc"))
                    coreSettings["4do_nvram_storage"] = "shared";
                else
                    coreSettings["4do_nvram_storage"] = "per game";
            }

            // Lightgun
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "260";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void ConfigureBlueMsx(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "bluemsx")
                return;

            coreSettings["bluemsx_overscan"] = (system == "msx2" || system == "msx2+" || system == "msxturbor") ? "MSX2" : "enabled";

            if (system == "spectravideo")
                coreSettings["bluemsx_msxtype"] = "SVI - Spectravideo SVI-328 MK2";
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

            var sysDevices = new Dictionary<string, string>() { { "msx", "257" }, { "msx1", "257" }, { "msx2", "257" }, { "colecovision", "1" } };

            if (sysDevices.ContainsKey(system))
                retroarchConfig["input_libretro_device_p1"] = sysDevices[system];

            if (sysDevices.ContainsKey(system))
                retroarchConfig["input_libretro_device_p2"] = sysDevices[system];
        }

        private void ConfigureFCEumm(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fceumm")
                return;

            if (Features.IsSupported("fceumm_cropoverscan"))
            {
                if (SystemConfig.isOptSet("fceumm_cropoverscan") && SystemConfig["fceumm_cropoverscan"] == "none")
                {
                    coreSettings["fceumm_overscan_h"] = "disabled";
                    coreSettings["fceumm_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("fceumm_cropoverscan") && SystemConfig["fceumm_cropoverscan"] == "h")
                {
                    coreSettings["fceumm_overscan_h"] = "enabled";
                    coreSettings["fceumm_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("fceumm_cropoverscan") && SystemConfig["fceumm_cropoverscan"] == "v")
                {
                    coreSettings["fceumm_overscan_h"] = "disabled";
                    coreSettings["fceumm_overscan_v"] = "enabled";
                }
                else
                {
                    coreSettings["fceumm_overscan_h"] = "enabled";
                    coreSettings["fceumm_overscan_v"] = "enabled";
                }
            }

            BindFeature(coreSettings, "fceumm_palette", "fceumm_palette", "default");
            BindFeature(coreSettings, "fceumm_ntsc_filter", "fceumm_ntsc_filter", "disabled");
            BindFeature(coreSettings, "fceumm_sndquality", "fceumm_sndquality", "Low");
            BindFeature(coreSettings, "fceumm_overclocking", "fceumm_overclocking", "disabled");
            BindFeature(coreSettings, "fceumm_nospritelimit", "fceumm_nospritelimit", "enabled");
            BindFeature(coreSettings, "fceumm_show_crosshair", "fceumm_show_crosshair", "enabled");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p2"] = "258";
                retroarchConfig["input_player2_mouse_index"] = "0";
                retroarchConfig["input_player2_gun_trigger_mbtn"] = "1";

                coreSettings["fceumm_zapper_mode"] = "lightgun";
            }
        }

        private void ConfigureNestopia(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "nestopia")
                return;

            if (Features.IsSupported("nestopia_cropoverscan"))
            {
                if (SystemConfig.isOptSet("nestopia_cropoverscan") && SystemConfig["nestopia_cropoverscan"] == "none")
                {
                    coreSettings["nestopia_overscan_h"] = "disabled";
                    coreSettings["nestopia_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("nestopia_cropoverscan") && SystemConfig["nestopia_cropoverscan"] == "h")
                {
                    coreSettings["nestopia_overscan_h"] = "enabled";
                    coreSettings["nestopia_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("nestopia_cropoverscan") && SystemConfig["nestopia_cropoverscan"] == "v")
                {
                    coreSettings["nestopia_overscan_h"] = "disabled";
                    coreSettings["nestopia_overscan_v"] = "enabled";
                }
                else
                {
                    coreSettings["nestopia_overscan_h"] = "enabled";
                    coreSettings["nestopia_overscan_v"] = "enabled";
                }
            }

            BindFeature(coreSettings, "nestopia_nospritelimit", "nestopia_nospritelimit", "disabled");
            BindFeature(coreSettings, "nestopia_palette", "nestopia_palette", "consumer");
            BindFeature(coreSettings, "nestopia_blargg_ntsc_filter", "nestopia_blargg_ntsc_filter", "disabled");
            BindFeature(coreSettings, "nestopia_overclock", "nestopia_overclock", "1x");
            BindFeature(coreSettings, "nestopia_select_adapter", "nestopia_select_adapter", "auto");
            BindFeature(coreSettings, "nestopia_show_crosshair", "nestopia_show_crosshair", "disabled");
            BindFeature(coreSettings, "nestopia_favored_system", "nestopia_favored_system", "auto");
            BindFeature(coreSettings, "nestopia_button_shift", "nestopia_button_shift", "disabled");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p2"] = "262";
                retroarchConfig["input_player2_mouse_index"] = "0";
                retroarchConfig["input_player2_gun_trigger_mbtn"] = "1";

                coreSettings["nestopia_zapper_device"] = "lightgun";

                CreateInputRemap("Nestopia", SystemConfig["rom"], cfg =>
                {
                    cfg["input_libretro_device_p1"] = "1";
                    cfg["input_libretro_device_p2"] = "262";
                    cfg["input_remap_port_p1"] = "0";
                    cfg["input_remap_port_p2"] = "1";
                });
            }
            else
                DeleteInputRemap("Nestopia", SystemConfig["rom"]);

            // Use remap to force input devices, or it does not load

        }

        private void ConfigureO2em(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "o2em")
                return;

            coreSettings["o2em_vkbd_transparency"] = "25";

            // Emulated Hardware
            if (Features.IsSupported("o2em_bios"))
            {
                if (SystemConfig.isOptSet("o2em_bios"))
                    coreSettings["o2em_bios"] = SystemConfig["o2em_bios"];
                else if (system == "videopacplus")
                    coreSettings["o2em_bios"] = "g7400.bin";
                else
                    coreSettings["o2em_bios"] = "o2rom.bin";
            }

            BindFeature(coreSettings, "o2em_region", "o2em_region", "auto");
            BindFeature(coreSettings, "o2em_swap_gamepads", "o2em_swap_gamepads", "disabled");
            BindFeature(coreSettings, "o2em_crop_overscan", "o2em_crop_overscan", "enabled");
            BindFeature(coreSettings, "o2em_mix_frames", "o2em_mix_frames", "disabled");

            // Audio Filter
            if (Features.IsSupported("o2em_low_pass_range"))
            {
                if (SystemConfig.isOptSet("o2em_low_pass_range") && SystemConfig["o2em_low_pass_range"] != "0")
                {
                    coreSettings["o2em_low_pass_filter"] = "enabled";
                    coreSettings["o2em_low_pass_range"] = SystemConfig["o2em_low_pass_range"];
                }
                else
                {
                    coreSettings["o2em_low_pass_filter"] = "disabled";
                    coreSettings["o2em_low_pass_range"] = "0";
                }
            }
        }

        private void ConfigureMame2003(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame078plus" && core != "mame2003_plus")
                return;

            coreSettings["mame2003-plus_skip_disclaimer"] = "enabled";
            coreSettings["mame2003-plus_skip_warnings"] = "enabled";
            coreSettings["mame2003-plus_xy_device"] = "lightgun";

            BindFeature(coreSettings, "mame2003-plus_analog", "mame2003-plus_analog", "digital");
            BindFeature(coreSettings, "mame2003-plus_frameskip", "mame2003-plus_frameskip", "0");
            BindFeature(coreSettings, "mame2003-plus_input_interface", "mame2003-plus_input_interface", "retropad");
            BindFeature(coreSettings, "mame2003-plus_neogeo_bios", "mame2003-plus_neogeo_bios", "unibios33");
            BindFeature(coreSettings, "mame2003-plus_tate_mode", "mame2003-plus_tate_mode", "disabled");

            if (SystemConfig["mame2003-plus_tate_mode"] == "enabled")
                SystemConfig["bezel"] = "none";
        }

        private void ConfigureQuasi88(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "quasi88")
                return;

            BindFeature(coreSettings, "q88_basic_mode", "q88_basic_mode", "N88 V2");
            BindFeature(coreSettings, "q88_cpu_clock", "q88_cpu_clock", "4");
            BindFeature(coreSettings, "q88_pcg-8100", "q88_pcg-8100", "disabled");
        }

        private void ConfigureCap32(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "cap32")
                return;

            // Virtual Keyboard by default (select+start) change to (start+Y)
            coreSettings["cap32_combokey"] = "y";

            //  Auto Select Model
            if (system == "gx4000")
                coreSettings["cap32_model"] = "6128+";
            else
                BindFeature(coreSettings, "cap32_model", "cap32_model", "6128+", true);

            BindFeature(coreSettings, "cap32_ram", "cap32_ram", "128");
        }

        private void ConfigureAtari800(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "atari800")
                return;

            bool atari800 = (system == "atari800");
            bool atariXE = !atari800 && system.IndexOf("xe", StringComparison.InvariantCultureIgnoreCase) >= 0;

            if (atari800)
            {
                var romExt = Path.GetExtension(Program.SystemConfig["rom"]).ToLower();

                coreSettings["atari800_internalbasic"] = (romExt == ".bas" ? "enabled" : "disabled");
                coreSettings["atari800_cassboot"] = (romExt == ".cas" ? "enabled" : "disabled");
                coreSettings["atari800_opt1"] = "disabled"; // detect card type

                BindFeature(coreSettings, "atari800_system", "atari800_system", "800XL (64K)", true);
                BindFeature(coreSettings, "atari800_ntscpal", "atari800_ntscpal", "NTSC");
                BindFeature(coreSettings, "atari800_sioaccel", "atari800_sioaccel", "enabled");
                BindFeature(coreSettings, "atari800_artifacting", "atari800_artifacting", "disabled");
            }
            else if (atariXE)
            {
                coreSettings["atari800_system"] = "130XE (128K)";
                coreSettings["atari800_internalbasic"] = "disabled";
                coreSettings["atari800_opt1"] = "enabled";
                coreSettings["atari800_cassboot"] = "disabled";

                BindFeature(coreSettings, "atari800_ntscpal", "atari800_ntscpal", "NTSC");
                BindFeature(coreSettings, "atari800_sioaccel", "atari800_sioaccel", "enabled");
                BindFeature(coreSettings, "atari800_artifacting", "atari800_artifacting", "disabled");
            }
            else // Atari 5200
            {
                coreSettings["atari800_system"] = "5200";
                coreSettings["atari800_opt1"] = "enabled"; // detect card type
                coreSettings["atari800_cassboot"] = "disabled";
            }

            if (string.IsNullOrEmpty(AppConfig["bios"]))
                return;

            var atariCfg = ConfigFile.FromFile(Path.Combine(RetroarchPath, ".atari800.cfg"), new ConfigFileOptions() { CaseSensitive = true, KeepEmptyValues = true, KeepEmptyLines = true });
            if (!atariCfg.Any())
                atariCfg.AppendLine("Atari 800 Emulator, Version 3.1.0");

            string biosPath = AppConfig.GetFullPath("bios");
            atariCfg["ROM_OS_A_PAL"] = Path.Combine(biosPath, "ATARIOSA.ROM");
            atariCfg["ROM_OS_BB01R2"] = Path.Combine(biosPath, "ATARIXL.ROM");
            atariCfg["ROM_BASIC_C"] = Path.Combine(biosPath, "ATARIBAS.ROM");
            atariCfg["ROM_400/800_CUSTOM"] = Path.Combine(biosPath, "ATARIOSB.ROM");
            atariCfg["ROM_XL/XE_CUSTOM"] = Path.Combine(biosPath, "ATARIXL.ROM");
            atariCfg["ROM_5200"] = Path.Combine(biosPath, "5200.ROM");
            atariCfg["ROM_5200_CUSTOM"] = Path.Combine(biosPath, "atari5200.ROM");

            atariCfg["OS_XL/XE_VERSION"] = "AUTO";
            atariCfg["OS_5200_VERSION"] = "AUTO";
            atariCfg["BASIC_VERSION"] = "AUTO";
            atariCfg["XEGS_GAME_VERSION"] = "AUTO";
            atariCfg["OS_400/800_VERSION"] = "AUTO";

            atariCfg["CASSETTE_FILENAME"] = null;
            atariCfg["CASSETTE_LOADED"] = "0";
            atariCfg["CARTRIDGE_FILENAME"] = null;
            atariCfg["CARTRIDGE_TYPE"] = "0";

            if (atari800)
            {
                atariCfg["MACHINE_TYPE"] = "Atari XL/XE";
                atariCfg["RAM_SIZE"] = "64";
                atariCfg["DISABLE_BASIC"] = "0";
            }
            else if (atariXE)
            {
                atariCfg["MACHINE_TYPE"] = "Atari XL/XE";
                atariCfg["RAM_SIZE"] = "128";
                atariCfg["DISABLE_BASIC"] = "1";

                var rom = Program.SystemConfig["rom"];
                if (File.Exists(rom))
                {
                    atariCfg["CARTRIDGE_FILENAME"] = rom;

                    try
                    {
                        var ln = new FileInfo(rom).Length;
                        if (ln == 131072)
                            atariCfg["CARTRIDGE_TYPE"] = "14";
                        else if (ln == 65536)
                            atariCfg["CARTRIDGE_TYPE"] = "13";
                    }
                    catch { }
                }
            }
            else // Atari 5200
            {
                atariCfg["ROM_OS_A_PAL"] = "";
                atariCfg["ROM_OS_BB01R2"] = "";
                atariCfg["ROM_BASIC_C"] = "";
                atariCfg["ROM_400/800_CUSTOM"] = "";

                atariCfg["MACHINE_TYPE"] = "Atari 5200";
                atariCfg["RAM_SIZE"] = "16";
                atariCfg["DISABLE_BASIC"] = "1";
            }

            atariCfg.Save(Path.Combine(RetroarchPath, ".atari800.cfg"), false);
        }

        private void ConfigureVirtualJaguar(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "virtualjaguar")
                return;

            BindFeature(coreSettings, "virtualjaguar_usefastblitter", "usefastblitter", "enabled");
            BindFeature(coreSettings, "virtualjaguar_bios", "bios_vj", "enabled");
            BindFeature(coreSettings, "virtualjaguar_doom_res_hack", "doom_res_hack", "disabled");
        }

        private void ConfigureSNes9x(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "snes9x" && core != "snes9x_next")
                return;

            coreSettings["snes9x_show_advanced_av_settings"] = "enabled";

            BindFeature(coreSettings, "snes9x_blargg", "snes9x_blargg", "disabled"); // Emulated video signal
            BindFeature(coreSettings, "snes9x_overscan", "snes9x_overscan", "auto"); // Overscan
            BindFeature(coreSettings, "snes9x_region", "snes9x_region", "auto"); // Region
            BindFeature(coreSettings, "snes9x_gfx_hires", "snes9x_gfx_hires", "disabled"); // Internal resolution
            BindFeature(coreSettings, "snes9x_hires_blend", "snes9x_hires_blend", "disabled"); // Pixel blending
            BindFeature(coreSettings, "snes9x_audio_interpolation", "snes9x_audio_interpolation", "none"); // Audio interpolation
            BindFeature(coreSettings, "snes9x_overclock_superfx", "snes9x_overclock_superfx", "100%"); // SuperFX overclock
            BindFeature(coreSettings, "snes9x_block_invalid_vram_access", "snes9x_block_invalid_vram_access", "enabled"); // Block invalid VRAM access

            // Unsafe hacks (config must be done in Core options)
            if (SystemConfig.isOptSet("SnesUnsafeHacks") && SystemConfig["SnesUnsafeHacks"] == "config")
            {
                coreSettings["snes9x_echo_buffer_hack"] = "enabled";
                coreSettings["snes9x_overclock_cycles"] = "enabled";
                coreSettings["snes9x_randomize_memory"] = "enabled";
                coreSettings["snes9x_reduce_sprite_flicker"] = "enabled";
            }
            else
            {
                coreSettings["snes9x_echo_buffer_hack"] = "disabled";
                coreSettings["snes9x_overclock_cycles"] = "disabled";
                coreSettings["snes9x_randomize_memory"] = "disabled";
                coreSettings["snes9x_reduce_sprite_flicker"] = "disabled";
            }

            // Advanced video options (config must be done in Core options menu)
            if (SystemConfig.isOptSet("SnesAdvancedVideoOptions") && SystemConfig["SnesAdvancedVideoOptions"] == "config")
            {
                coreSettings["snes9x_layer_1"] = "disabled";
                coreSettings["snes9x_layer_2"] = "disabled";
                coreSettings["snes9x_layer_3"] = "disabled";
                coreSettings["snes9x_layer_4"] = "disabled";
                coreSettings["snes9x_layer_5"] = "disabled";
                coreSettings["snes9x_gfx_clip"] = "disabled";
                coreSettings["snes9x_gfx_transp"] = "disabled";
            }
            else
            {
                coreSettings["snes9x_layer_1"] = "enabled";
                coreSettings["snes9x_layer_2"] = "enabled";
                coreSettings["snes9x_layer_3"] = "enabled";
                coreSettings["snes9x_layer_4"] = "enabled";
                coreSettings["snes9x_layer_5"] = "enabled";
                coreSettings["snes9x_gfx_clip"] = "enabled";
                coreSettings["snes9x_gfx_transp"] = "enabled";
            }

            // Advanced audio options (config must be done in Core options menu)
            if (SystemConfig.isOptSet("SnesAdvancedAudioOptions") && SystemConfig["SnesAdvancedAudioOptions"] == "config")
            {
                coreSettings["snes9x_sndchan_1"] = "disabled";
                coreSettings["snes9x_sndchan_2"] = "disabled";
                coreSettings["snes9x_sndchan_3"] = "disabled";
                coreSettings["snes9x_sndchan_4"] = "disabled";
                coreSettings["snes9x_sndchan_5"] = "disabled";
                coreSettings["snes9x_sndchan_6"] = "disabled";
                coreSettings["snes9x_sndchan_7"] = "disabled";
                coreSettings["snes9x_sndchan_8"] = "disabled";
            }
            else
            {
                coreSettings["snes9x_sndchan_1"] = "enabled";
                coreSettings["snes9x_sndchan_2"] = "enabled";
                coreSettings["snes9x_sndchan_3"] = "enabled";
                coreSettings["snes9x_sndchan_4"] = "enabled";
                coreSettings["snes9x_sndchan_5"] = "enabled";
                coreSettings["snes9x_sndchan_6"] = "enabled";
                coreSettings["snes9x_sndchan_7"] = "enabled";
                coreSettings["snes9x_sndchan_8"] = "enabled";
            }

            coreSettings["snes9x_show_lightgun_settings"] = "enabled";
            BindFeature(coreSettings, "snes9x_lightgun_mode", "snes9x_lightgun_mode", "Lightgun"); // Lightgun mode

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "260";
                retroarchConfig["input_player2_mouse_index"] = "0";
                retroarchConfig["input_player2_gun_trigger_mbtn"] = "1";
            }
        }

        private void ConfigureGenesisPlusGX(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "genesis_plus_gx")
                return;

            coreSettings["genesis_plus_gx_bram"] = "per game";
            coreSettings["genesis_plus_gx_ym2413"] = "auto";

            BindFeature(coreSettings, "genesis_plus_gx_addr_error", "addr_error", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_lock_on", "lock_on", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_ym2612", "ym2612", "mame (ym2612)");
            BindFeature(coreSettings, "genesis_plus_gx_audio_filter", "audio_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_blargg_ntsc_filter", "ntsc_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_lcd_filter", "lcd_filter", "lcd_filter");
            BindFeature(coreSettings, "genesis_plus_gx_overscan", "overscan", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_render", "render", "single field");
            BindFeature(coreSettings, "genesis_plus_gx_force_dtack", "genesis_plus_gx_force_dtack", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_overclock", "genesis_plus_gx_overclock", "100%");
            BindFeature(coreSettings, "genesis_plus_gx_no_sprite_limit", "genesis_plus_gx_no_sprite_limit", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_bios", "genesis_plus_gx_bios", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_add_on", "genesis_plus_gx_add_on", "auto");

            BindFeature(coreSettings, "genesis_plus_gx_gun_cursor", "gun_cursor", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_gun_input", "gun_input", "lightgun");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                var gunInfo = GunGames.GetGameInformation(system, SystemConfig["rom"]);
                if (gunInfo != null && gunInfo.GunType == "justifier")
                {
                    retroarchConfig["input_libretro_device_p2"] = "772";
                    retroarchConfig["input_player2_mouse_index"] = "0";
                    retroarchConfig["input_player2_gun_trigger_mbtn"] = "1";
                    retroarchConfig["input_player2_gun_offscreen_shot_mbtn"] = "2";
                    retroarchConfig["input_player2_gun_start_mbtn"] = "3";
                }
                else
                {
                    if (system == "mastersystem")
                        retroarchConfig["input_libretro_device_p1"] = "260";
                    else
                        retroarchConfig["input_libretro_device_p1"] = "516"; 

                    retroarchConfig["input_player1_mouse_index"] = "0";
                    retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                    retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                    retroarchConfig["input_player1_gun_start_mbtn"] = "3";
                }

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);

                // Use remap to force input devices, or it does not load
                CreateInputRemap("Genesis Plus GX", SystemConfig["rom"], cfg =>
                {
                    cfg["input_libretro_device_p1"] = "1";
                    cfg["input_libretro_device_p2"] = gunInfo != null && gunInfo.GunType == "justifier" ? "772" : "516";
                    cfg["input_remap_port_p1"] = "0";
                    cfg["input_remap_port_p2"] = "1";
                });
            }
            else
                DeleteInputRemap("Genesis Plus GX", SystemConfig["rom"]);
        }

        private void ConfigureGenesisPlusGXWide(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "genesis_plus_gx_wide")
                return;

            if (SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
            {
                int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                if (idx == 1 || idx == 2 || idx == 4 || idx == 6 || idx == 7 || idx == 9 || idx == 14 || idx == 16 || idx == 18 || idx == 19)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                }
            }
            else
            {
                retroarchConfig["aspect_ratio_index"] = "1";
                retroarchConfig["video_aspect_ratio_auto"] = "false";
                SystemConfig["bezel"] = "none";
            }

            coreSettings["genesis_plus_gx_wide_bram"] = "per game";
            coreSettings["genesis_plus_gx_wide_ym2413"] = "auto";
            coreSettings["genesis_plus_gx_wide_overscan"] = "disabled";

            BindFeature(coreSettings, "genesis_plus_gx_wide_addr_error", "addr_error", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_lock_on", "lock_on", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_ym2612", "ym2612", "mame (ym2612)");
            BindFeature(coreSettings, "genesis_plus_gx_wide_audio_filter", "audio_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_blargg_ntsc_filter", "ntsc_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_lcd_filter", "lcd_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_overscan", "overscan", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_render", "render", "single field");
            BindFeature(coreSettings, "genesis_plus_gx_wide_force_dtack", "genesis_plus_gx_force_dtack", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_overclock", "genesis_plus_gx_overclock", "100%");
            BindFeature(coreSettings, "genesis_plus_gx_wide_no_sprite_limit", "genesis_plus_gx_no_sprite_limit", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_bios", "genesis_plus_gx_bios", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_add_on", "genesis_plus_gx_add_on", "auto");
            BindFeature(coreSettings, "genesis_plus_gx_wide_h40_extra_columns", "h40_extra_columns", "10");

            BindFeature(coreSettings, "genesis_plus_gx_wide_gun_cursor", "gun_cursor", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_gun_input", "gun_input", "lightgun");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                var gunInfo = GunGames.GetGameInformation(system, SystemConfig["rom"]);
                if (gunInfo != null && gunInfo.GunType == "justifier")
                {
                    retroarchConfig["input_libretro_device_p2"] = "772";
                    retroarchConfig["input_player2_mouse_index"] = "0";
                    retroarchConfig["input_player2_gun_trigger_mbtn"] = "1";
                    retroarchConfig["input_player2_gun_offscreen_shot_mbtn"] = "2";
                    retroarchConfig["input_player2_gun_start_mbtn"] = "3";
                }
                else
                {
                    retroarchConfig["input_libretro_device_p1"] = "516"; // "260";
                    retroarchConfig["input_player1_mouse_index"] = "0";
                    retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                    retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                    retroarchConfig["input_player1_gun_start_mbtn"] = "3";
                }

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);

                // Use remap to force input devices, or it does not load
                CreateInputRemap("Genesis Plus GX Wide", SystemConfig["rom"], cfg =>
                {
                    cfg["input_libretro_device_p1"] = "1";
                    cfg["input_libretro_device_p2"] = gunInfo != null && gunInfo.GunType == "justifier" ? "772" : "516";
                    cfg["input_remap_port_p1"] = "0";
                    cfg["input_remap_port_p2"] = "1";
                });
            }
            else
                DeleteInputRemap("Genesis Plus GX Wide", SystemConfig["rom"]);
        }

        private void ConfigureMame(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame")
                return;

            string softLists = "enabled";

            MessSystem messSystem = MessSystem.GetMessSystem(system);
            if (messSystem != null)
            {
                CleanupMameMessConfigFiles(messSystem);

                // If we have a know system name, disable softlists as we run with CLI
                if (!string.IsNullOrEmpty(messSystem.MachineName))
                    softLists = "disabled";
            }

            coreSettings["mame_softlists_enable"] = softLists;
            coreSettings["mame_softlists_auto_media"] = softLists;

            coreSettings["mame_read_config"] = "enabled";
            coreSettings["mame_write_config"] = "enabled";
            coreSettings["mame_mouse_enable"] = "enabled";
            coreSettings["mame_mame_paths_enable"] = "disabled";

            BindFeature(coreSettings, "mame_alternate_renderer", "alternate_renderer", "disabled");
            BindFeature(coreSettings, "mame_altres", "internal_resolution", "640x480");
            BindFeature(coreSettings, "mame_cheats_enable", "cheats_enable", "disabled");
            BindFeature(coreSettings, "mame_mame_4way_enable", "4way_enable", "enabled");
            BindFeature(coreSettings, "mame_lightgun_mode", "lightgun_mode", "lightgun");

            BindFeature(coreSettings, "mame_boot_from_cli", "boot_from_cli", "enabled", true);
            BindFeature(coreSettings, "mame_boot_to_bios", "boot_to_bios", "disabled", true);
            BindFeature(coreSettings, "mame_boot_to_osd", "boot_to_osd", "disabled", true);
        }

        private void CleanupMameMessConfigFiles(MessSystem messSystem)
        {
            try
            {
                // Remove image_directories node in cfg file
                string cfgPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "cfg", messSystem.MachineName + ".cfg");
                if (File.Exists(cfgPath))
                {
                    XDocument xml = XDocument.Load(cfgPath);

                    var image_directories = xml.Descendants().FirstOrDefault(d => d.Name == "image_directories");
                    if (image_directories != null)
                    {
                        image_directories.Remove();
                        xml.Save(cfgPath);
                    }
                }
            }
            catch { }

            try
            {
                // Remove medias declared in ini file
                string iniPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "ini", messSystem.MachineName + ".ini");
                if (File.Exists(iniPath))
                {
                    var lines = File.ReadAllLines(iniPath);
                    var newLines = lines.Where(l =>
                        !l.StartsWith("cartridge") && !l.StartsWith("floppydisk") &&
                        !l.StartsWith("cassette") && !l.StartsWith("cdrom") &&
                        !l.StartsWith("romimage") && !l.StartsWith("memcard") &&
                        !l.StartsWith("quickload") && !l.StartsWith("harddisk") &&
                        !l.StartsWith("autoboot_command") && !l.StartsWith("autoboot_delay") && !l.StartsWith("autoboot_script") &&
                        !l.StartsWith("printout")
                        ).ToArray();

                    if (lines.Length != newLines.Length)
                        File.WriteAllLines(iniPath, newLines);
                }
            }
            catch { }
        }

        private void ConfigurePotator(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "potator")
                return;

            BindFeature(coreSettings, "potator_lcd_ghosting", "lcd_ghosting", "0");
            BindFeature(coreSettings, "potator_palette", "palette", "default");
        }

        private void ConfigureMupen64(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mupen64plus_next" && core != "mupen64plus_next_gles3")
                return;

            //coreSettings["mupen64plus-cpucore"] = "pure_interpreter";
            coreSettings["mupen64plus-rsp-plugin"] = "hle";
            coreSettings["mupen64plus-EnableLODEmulation"] = "True";
            coreSettings["mupen64plus-EnableCopyAuxToRDRAM"] = "True";
            coreSettings["mupen64plus-EnableHWLighting"] = "True";
            coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "True";
            coreSettings["mupen64plus-GLideN64IniBehaviour"] = "early";
            coreSettings["mupen64plus-parallel-rdp-native-tex-rect"] = "True";
            coreSettings["mupen64plus-parallel-rdp-synchronous"] = "True";

            BindFeature(coreSettings, "mupen64plus-cpucore", "mupen64plus-cpucore", "pure_interpreter"); // CPU core
            BindFeature(coreSettings, "mupen64plus-rdp-plugin", "RDP_Plugin", "gliden64"); // Plugin selection           

            // Set RSP plugin: HLE for Glide, LLE for Parallel
            if (SystemConfig.isOptSet("RDP_Plugin") && coreSettings["mupen64plus-rdp-plugin"] == "parallel")
                coreSettings["mupen64plus-rsp-plugin"] = "parallel";
            else
                coreSettings["mupen64plus-rsp-plugin"] = "hle";

            // Overscan (Glide)
            if (SystemConfig.isOptSet("CropOverscan") && SystemConfig.getOptBoolean("CropOverscan"))
            {
                coreSettings["mupen64plus-OverscanBottom"] = "0";
                coreSettings["mupen64plus-OverscanLeft"] = "0";
                coreSettings["mupen64plus-OverscanRight"] = "0";
                coreSettings["mupen64plus-OverscanTop"] = "0";
            }
            else
            {
                coreSettings["mupen64plus-OverscanBottom"] = "15";
                coreSettings["mupen64plus-OverscanLeft"] = "18";
                coreSettings["mupen64plus-OverscanRight"] = "13";
                coreSettings["mupen64plus-OverscanTop"] = "12";
            }

            // Performance presets
            if (SystemConfig.isOptSet("PerformanceMode") && SystemConfig.getOptBoolean("PerformanceMode"))
            {
                coreSettings["mupen64plus-EnableCopyColorToRDRAM"] = "Off";
                coreSettings["mupen64plus-EnableCopyDepthToRDRAM"] = "Off";
                coreSettings["mupen64plus-EnableFBEmulation"] = "False";
                coreSettings["mupen64plus-ThreadedRenderer"] = "False";
                coreSettings["mupen64plus-HybridFilter"] = "False";
                coreSettings["mupen64plus-BackgroundMode"] = "OnePiece";
                coreSettings["mupen64plus-EnableLegacyBlending"] = "True";
                coreSettings["mupen64plus-txFilterIgnoreBG"] = "True";
            }
            else
            {
                coreSettings["mupen64plus-EnableCopyColorToRDRAM"] = "TripleBuffer";
                coreSettings["mupen64plus-EnableCopyDepthToRDRAM"] = "Software";
                coreSettings["mupen64plus-EnableFBEmulation"] = "True";
                coreSettings["mupen64plus-ThreadedRenderer"] = "True";
                coreSettings["mupen64plus-HybridFilter"] = "True";
                coreSettings["mupen64plus-BackgroundMode"] = "Stripped";
                coreSettings["mupen64plus-EnableLegacyBlending"] = "False";
                coreSettings["mupen64plus-txFilterIgnoreBG"] = "False";

            }

            // Hi Res textures methods
            if (SystemConfig.isOptSet("TexturesPack"))
            {
                if (SystemConfig["TexturesPack"] == "legacy")
                {
                    coreSettings["mupen64plus-EnableTextureCache"] = "True";
                    coreSettings["mupen64plus-txHiresEnable"] = "True";
                    coreSettings["mupen64plus-txCacheCompression"] = "True";
                    coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "False";
                    coreSettings["mupen64plus-EnableEnhancedTextureStorage"] = "False";
                    coreSettings["mupen64plus-EnableEnhancedHighResStorage"] = "False";
                }
                else if (SystemConfig["TexturesPack"] == "cache")
                {
                    coreSettings["mupen64plus-EnableTextureCache"] = "True";
                    coreSettings["mupen64plus-txHiresEnable"] = "True";
                    coreSettings["mupen64plus-txCacheCompression"] = "True";
                    coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "True";
                    coreSettings["mupen64plus-EnableEnhancedTextureStorage"] = "True";
                    coreSettings["mupen64plus-EnableEnhancedHighResStorage"] = "True";
                }
            }
            else
            {
                coreSettings["mupen64plus-EnableTextureCache"] = "False";
                coreSettings["mupen64plus-txHiresEnable"] = "False";
                coreSettings["mupen64plus-txCacheCompression"] = "False";
                coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "False";
                coreSettings["mupen64plus-EnableEnhancedTextureStorage"] = "False";
                coreSettings["mupen64plus-EnableEnhancedHighResStorage"] = "False";
            }

            // Widescreen (Glide)
            if (SystemConfig.isOptSet("Widescreen") && SystemConfig.getOptBoolean("Widescreen"))
            {
                coreSettings["mupen64plus-aspect"] = "16:9 adjusted";
                retroarchConfig["aspect_ratio_index"] = "1";
                SystemConfig["bezel"] = "none";
            }
            else
                coreSettings["mupen64plus-aspect"] = "4/3";

            // Player packs
            BindFeature(coreSettings, "mupen64plus-pak1", "mupen64plus-pak1", "memory");
            BindFeature(coreSettings, "mupen64plus-pak2", "mupen64plus-pak2", "none");
            BindFeature(coreSettings, "mupen64plus-pak3", "mupen64plus-pak3", "none");
            BindFeature(coreSettings, "mupen64plus-pak4", "mupen64plus-pak4", "none");

            // Glide
            BindFeature(coreSettings, "mupen64plus-txEnhancementMode", "Texture_Enhancement", "As Is");
            BindFeature(coreSettings, "mupen64plus-43screensize", "43screensize", "640x480");
            BindFeature(coreSettings, "mupen64plus-169screensize", "169screensize", "960x540");
            BindFeature(coreSettings, "mupen64plus-BilinearMode", "BilinearMode", "3point");
            BindFeature(coreSettings, "mupen64plus-MultiSampling", "MultiSampling", "0");
            BindFeature(coreSettings, "mupen64plus-txFilterMode", "Texture_filter", "None");

            // Parallel
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-deinterlace-method", "mupen64plus-parallel-rdp-deinterlace-method", "none");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-dither-filter", "mupen64plus-parallel-rdp-dither-filter", "True");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-divot-filter", "mupen64plus-parallel-rdp-divot-filter", "True");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-downscaling", "mupen64plus-parallel-rdp-downscaling", "disable");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-gamma-dither", "mupen64plus-parallel-rdp-gamma-dither", "disable");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-native-texture-lod", "mupen64plus-parallel-rdp-native-texture-lod", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-overscan", "mupen64plus-parallel-rdp-overscan", "16");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-super-sampled-read-back", "mupen64plus-parallel-rdp-super-sampled-read-back", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-super-sampled-read-back-dither", "mupen64plus-parallel-rdp-super-sampled-read-back-dither", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-upscaling", "mupen64plus-parallel-rdp-upscaling", "1x");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-vi-aa", "mupen64plus-parallel-rdp-vi-aa", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-vi-bilinear", "mupen64plus-parallel-rdp-vi-bilinear", "False");
        }

        private void ConfigureDosboxPure(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "dosbox_pure")
                return;

            coreSettings["dosbox_pure_advanced"] = "true";
            coreSettings["dosbox_pure_auto_mapping"] = "true";
            coreSettings["dosbox_pure_bind_unused"] = "true";
            coreSettings["dosbox_pure_savestate"] = "on";

            BindFeature(coreSettings, "dosbox_pure_aspect_correction", "ratio", "true");
            BindFeature(coreSettings, "dosbox_pure_cga", "cga", "early_auto");
            BindFeature(coreSettings, "dosbox_pure_cpu_core", "cpu_core", "auto");
            BindFeature(coreSettings, "dosbox_pure_cpu_type", "cpu_type", "auto");
            BindFeature(coreSettings, "dosbox_pure_cycles", "cycles", "auto");
            BindFeature(coreSettings, "dosbox_pure_gus", "gus", "false");
            BindFeature(coreSettings, "dosbox_pure_hercules", "hercules", "white");
            BindFeature(coreSettings, "dosbox_pure_machine", "machine", "svga");
            BindFeature(coreSettings, "dosbox_pure_memory_size", "memory_size", "16");
            BindFeature(coreSettings, "dosbox_pure_menu_time", "menu_time", "5");
            BindFeature(coreSettings, "dosbox_pure_midi", "midi", "scummvm/extra/Roland_SC-55.sf2");
            BindFeature(coreSettings, "dosbox_pure_on_screen_keyboard", "on_screen_keyboard", "true");
            BindFeature(coreSettings, "dosbox_pure_sblaster_adlib_emu", "sblaster_adlib_emu", "default");
            BindFeature(coreSettings, "dosbox_pure_sblaster_adlib_mode", "sblaster_adlib_mode", "auto");
            BindFeature(coreSettings, "dosbox_pure_sblaster_conf", "sblaster_conf", "A220 I7 D1 H5");
            BindFeature(coreSettings, "dosbox_pure_sblaster_type", "sblaster_type", "sb16");
            BindFeature(coreSettings, "dosbox_pure_svga", "svga", "vesa_nolfb");
            BindFeature(coreSettings, "dosbox_pure_keyboard_layout", "keyboard_layout", "us");
            BindFeature(coreSettings, "dosbox_pure_force60fps", "dosbox_pure_force60fps", "false");
            BindFeature(coreSettings, "dosbox_pure_perfstats", "dosbox_pure_perfstats", "none");
            BindFeature(coreSettings, "dosbox_pure_conf", "dosbox_pure_conf", "false");
            BindFeature(coreSettings, "dosbox_pure_voodoo", "dosbox_pure_voodoo", "off");
            BindFeature(coreSettings, "dosbox_pure_voodoo_perf", "dosbox_pure_voodoo_perf", "1");
            BindFeature(coreSettings, "dosbox_pure_bootos_ramdisk", "dosbox_pure_bootos_ramdisk", "false");
            BindFeature(coreSettings, "dosbox_pure_bootos_forcenormal", "dosbox_pure_bootos_forcenormal", "false");
        }

        private void ConfigurePuae(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "puae")
                return;

            coreSettings["puae_video_options_display"] = "enabled";
            coreSettings["puae_use_whdload"] = "hdfs";

            BindFeature(coreSettings, "puae_model", "model", "auto");
            BindFeature(coreSettings, "puae_cpu_compatibility", "cpu_compatibility", "normal");
            BindFeature(coreSettings, "puae_cpu_multiplier", "cpu_multiplier", "default");
            BindFeature(coreSettings, "puae_video_resolution", "video_resolution", "auto");
            BindFeature(coreSettings, "puae_zoom_mode", "zoom_mode", "auto");
            BindFeature(coreSettings, "puae_video_standard", "video_standard", "PAL auto");
            BindFeature(coreSettings, "puae_use_whdload_prefs", "whdload", "config");
            BindFeature(coreSettings, "puae_retropad_options", "pad_options", "jump");
            BindFeature(coreSettings, "puae_floppy_speed", "floppy_speed", "100");
            BindFeature(coreSettings, "puae_floppy_sound", "floppy_sound", "75");
            BindFeature(coreSettings, "puae_kickstart", "puae_kickstart", "auto");
        }

        private void ConfigureFlycast(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "flycast")
                return;

            coreSettings["reicast_threaded_rendering"] = "enabled";
            coreSettings["reicast_enable_purupuru"] = "enabled"; // Enable controller force feedback

            BindFeature(coreSettings, "reicast_widescreen_hack", "widescreen_hack", "disabled");
            BindFeature(coreSettings, "reicast_widescreen_cheats", "widescreen_hack", "disabled");

            if (SystemConfig["widescreen_hack"] == "enabled")
            {
                retroarchConfig["aspect_ratio_index"] = "1";
                SystemConfig["bezel"] = "none";
            }

            BindFeature(coreSettings, "reicast_anisotropic_filtering", "anisotropic_filtering", "off");
            BindFeature(coreSettings, "reicast_texupscale", "texture_upscaling", "off");
            BindFeature(coreSettings, "reicast_render_to_texture_upscaling", "render_to_texture_upscaling", "1x");
            BindFeature(coreSettings, "reicast_force_wince", "force_wince", "disabled");
            BindFeature(coreSettings, "reicast_cable_type", "cable_type", "VGA (RGB)");
            BindFeature(coreSettings, "reicast_internal_resolution", "internal_resolution", "640x480");
            BindFeature(coreSettings, "reicast_force_freeplay", "reicast_force_freeplay", "disabled");
            BindFeature(coreSettings, "reicast_allow_service_buttons", "reicast_allow_service_buttons", "disabled");
            BindFeature(coreSettings, "reicast_boot_to_bios", "reicast_boot_to_bios", "disabled");
            BindFeature(coreSettings, "reicast_hle_bios", "reicast_hle_bios", "disabled");
            BindFeature(coreSettings, "reicast_per_content_vmus", "reicast_per_content_vmus", "disabled");
            BindFeature(coreSettings, "reicast_language", "reicast_language", "English");
            BindFeature(coreSettings, "reicast_region", "reicast_region", "Japan");
            BindFeature(coreSettings, "reicast_dump_textures", "reicast_dump_textures", "disabled");
            BindFeature(coreSettings, "reicast_custom_textures", "reicast_custom_textures", "disabled");
            BindFeature(coreSettings, "reicast_alpha_sorting", "reicast_alpha_sorting", "per-triangle (normal)");
            BindFeature(coreSettings, "reicast_enable_rttb", "reicast_enable_rttb", "disabled");
            BindFeature(coreSettings, "reicast_mipmapping", "reicast_mipmapping", "disabled");

            // toadd
            BindFeature(coreSettings, "reicast_synchronous_rendering", "reicast_synchronous_rendering", "enabled");
            BindFeature(coreSettings, "reicast_frame_skipping", "reicast_frame_skipping", "disabled");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "4";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void ConfigureMesen(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mesen")
                return;

            coreSettings["mesen_aspect_ratio"] = "Auto";

            BindFeature(coreSettings, "mesen_hdpacks", "hd_packs", "disabled");
            BindFeature(coreSettings, "mesen_ntsc_filter", "ntsc_filter", "Disabled");
            BindFeature(coreSettings, "mesen_palette", "palette", "Default");
            BindFeature(coreSettings, "mesen_shift_buttons_clockwise", "shift_buttons", "disabled");
            BindFeature(coreSettings, "mesen_fake_stereo", "fake_stereo", "disabled");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p2"] = "262";
                retroarchConfig["input_player2_mouse_index"] = "0";
                retroarchConfig["input_player2_gun_trigger_mbtn"] = "1";
            }
        }

        private void ConfigurePcsxRearmed(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "pcsx_rearmed")
                return;

            if (Features.IsSupported("neon_enhancement"))
            {
                switch (SystemConfig["neon_enhancement"])
                {
                    case "enabled":
                        coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "enabled";
                        coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "disabled";
                        break;
                    case "enabled_with_speedhack":
                        coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "enabled";
                        coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "enabled";
                        break;
                    default:
                        coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "disabled";
                        coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "disabled";
                        break;
                }
            }
        }

        private void ConfigureMednafenPsxHW(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_psx_hw")
                return;

            // widescreen
            BindFeature(coreSettings, "beetle_psx_hw_widescreen_hack", "widescreen_hack", "disabled");

            if (coreSettings["beetle_psx_hw_widescreen_hack"] == "enabled")
            {
                int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                if (idx > 0)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                }
                else
                {
                    retroarchConfig["aspect_ratio_index"] = "1";
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                    coreSettings["beetle_psx_hw_widescreen_hack_aspect_ratio"] = "16:9";
                }
            }

            // PGXP
            if (SystemConfig.isOptSet("pgxp") && SystemConfig.getOptBoolean("pgxp"))
            {
                coreSettings["beetle_psx_hw_pgxp_mode"] = "memory only";
                coreSettings["beetle_psx_hw_pgxp_texture"] = "enabled";
                coreSettings["beetle_psx_hw_pgxp_vertex"] = "enabled";
            }
            else
            {
                coreSettings["beetle_psx_hw_pgxp_mode"] = "disabled";
                coreSettings["beetle_psx_hw_pgxp_texture"] = "disabled";
                coreSettings["beetle_psx_hw_pgxp_vertex"] = "disabled";
            }

            BindFeature(coreSettings, "beetle_psx_hw_internal_resolution", "internal_resolution", "1x(native)");
            BindFeature(coreSettings, "beetle_psx_hw_filter", "texture_filtering", "nearest");
            BindFeature(coreSettings, "beetle_psx_hw_dither_mode", "dither_mode", "disabled");
            BindFeature(coreSettings, "beetle_psx_hw_msaa", "msaa", "1x");
            BindFeature(coreSettings, "beetle_psx_hw_analog_toggle", "analog_toggle", "enabled");
            BindFeature(coreSettings, "beetle_psx_hw_widescreen_hack_aspect_ratio", "widescreen_hack_aspect_ratio", "16:9");
            BindFeature(coreSettings, "beetle_psx_hw_pal_video_timing_override", "pal_video_timing_override", "disabled");
            BindFeature(coreSettings, "beetle_psx_hw_skip_bios", "skip_bios", "enabled");

            // NEW
            BindFeature(coreSettings, "beetle_psx_hw_gun_input_mode", "gun_input_mode", "lightgun", true);
            BindFeature(coreSettings, "beetle_psx_hw_gun_cursor", "gun_cursor", "cross", true);

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                retroarchConfig["input_libretro_device_p1"] = "260";
                retroarchConfig["input_player1_mouse_index"] = "0";
                retroarchConfig["input_player1_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player1_gun_aux_a_mbtn"] = "2";
                retroarchConfig["input_player1_gun_start_mbtn"] = "3";

                ConfigurePlayer1LightgunKeyboardActions(retroarchConfig);
            }
        }

        private void CreateInputRemap(string cleanSystemName, string romName, Action<ConfigFile> createRemap)
        {
            DeleteInputRemap(cleanSystemName, romName);
            if (createRemap == null)
                return;

            string remapName = Path.GetFileName(Path.GetDirectoryName(romName));

            string dir = Path.Combine(RetroarchPath, "config", "remaps", cleanSystemName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, remapName + ".rmp");

            this.AddFileForRestoration(path);

            var cfg = ConfigFile.FromFile(path, new ConfigFileOptions() { CaseSensitive = true });
            createRemap(cfg);
            cfg.Save(path, true);
        }

        private void DeleteInputRemap(string cleanSystemName, string romName)
        {
            string remapName = Path.GetFileName(Path.GetDirectoryName(romName));

            string dir = Path.Combine(RetroarchPath, "config", "remaps", cleanSystemName);
            string path = Path.Combine(dir, remapName + ".rmp");

            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch { }
        }
    }
}
