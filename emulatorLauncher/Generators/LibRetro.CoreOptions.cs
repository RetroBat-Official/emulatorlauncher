using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace emulatorLauncher.libRetro
{
    partial class LibRetroGenerator : Generator
    {
        private void ConfigureCoreOptions(ConfigFile retroarchConfig, string system, string core)
        {
            var coreSettings = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), new ConfigFileOptions() { CaseSensitive = true });

            if (core == "bluemsx")
            {
                coreSettings["bluemsx_overscan"] = "enabled";

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
            }

            if (core == "theodore")
                coreSettings["theodore_autorun"] = "enabled";

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
            else
                SystemConfig["bezel"] = SystemConfig["bezel"];

        }
        
        private void ConfigureNeocd(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "neocd")
                return;

            coreSettings["neocd_per_content_saves"] = "On";

            if (SystemConfig.isOptSet("neocd_bios"))
                coreSettings["neocd_bios"] = SystemConfig["neocd_bios"];
            else
                coreSettings["neocd_bios"] = "uni-bioscd.rom (CDZ, Universe 3.3)";

            if (SystemConfig.isOptSet("neocd_cdspeedhack"))
                coreSettings["neocd_cdspeedhack"] = SystemConfig["neocd_cdspeedhack"];
            else
                coreSettings["neocd_cdspeedhack"] = "Off";

            if (SystemConfig.isOptSet("neocd_loadskip"))
                coreSettings["neocd_loadskip"] = SystemConfig["neocd_loadskip"];
            else
                coreSettings["neocd_loadskip"] = "Off";

            if (SystemConfig.isOptSet("neocd_region"))
                coreSettings["neocd_region"] = SystemConfig["neocd_region"];
            else
                coreSettings["neocd_region"] = "USA";

        }
        
        private void ConfigureMednafenPce(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_pce")
                return;

            coreSettings["pce_show_advanced_input_settings"] = "enabled";

            if (SystemConfig.isOptSet("pce_psgrevision"))
                coreSettings["pce_psgrevision"] = SystemConfig["pce_psgrevision"];
            else
                coreSettings["pce_psgrevision"] = "auto";

            if (SystemConfig.isOptSet("pce_resamp_quality"))
                coreSettings["pce_resamp_quality"] = SystemConfig["pce_resamp_quality"];

            if (SystemConfig.isOptSet("pce_ocmultiplier"))
                coreSettings["pce_ocmultiplier"] = SystemConfig["pce_ocmultiplier"];
            else
                coreSettings["pce_ocmultiplier"] = "1";

            if (SystemConfig.isOptSet("pce_nospritelimit"))
                coreSettings["pce_nospritelimit"] = SystemConfig["pce_nospritelimit"];
            else
                coreSettings["pce_nospritelimit"] = "disabled";

            if (SystemConfig.isOptSet("pce_cdimagecache"))
                coreSettings["pce_cdimagecache"] = SystemConfig["pce_cdimagecache"];
            else
                coreSettings["pce_cdimagecache"] = "disabled";

            if (SystemConfig.isOptSet("pce_cdbios"))
                coreSettings["pce_cdbios"] = SystemConfig["pce_cdbios"];
            else
                coreSettings["pce_cdbios"] = "System Card 3";
            
            if (SystemConfig.isOptSet("pce_cdspeed"))
                coreSettings["pce_cdspeed"] = SystemConfig["pce_cdspeed"];
            else
                coreSettings["pce_cdspeed"] = "1";

            if (SystemConfig.isOptSet("pce_palette"))
                coreSettings["pce_palette"] = SystemConfig["pce_palette"];
            else
                coreSettings["pce_palette"] = "Composite";

            if (SystemConfig.isOptSet("pce_scaling"))
                coreSettings["pce_scaling"] = SystemConfig["pce_scaling"];
            else
                coreSettings["pce_scaling"] = "auto";

            if (SystemConfig.isOptSet("pce_hires_blend"))
                coreSettings["pce_hires_blend"] = SystemConfig["pce_hires_blend"];
            else
                coreSettings["pce_hires_blend"] = "disabled";

            if (SystemConfig.isOptSet("pce_h_overscan"))
                coreSettings["pce_h_overscan"] = SystemConfig["pce_h_overscan"];
            else
                coreSettings["pce_h_overscan"] = "auto";
            
            if (SystemConfig.isOptSet("pce_adpcmextraprec"))
                coreSettings["pce_adpcmextraprec"] = SystemConfig["pce_adpcmextraprec"];
            else
                coreSettings["pce_adpcmextraprec"] = "12-bit";
            
            if (SystemConfig.isOptSet("pcecdvolume"))
            {
                coreSettings["pce_adpcmvolume"] = SystemConfig["pcecdvolume"];
                coreSettings["pce_cddavolume"] = SystemConfig["pcecdvolume"];
                coreSettings["pce_cdpsgvolume"] = SystemConfig["pcecdvolume"];
            }
            else
            {
                coreSettings["pce_adpcmvolume"] = "100";
                coreSettings["pce_cddavolume"] = "100";
                coreSettings["pce_cdpsgvolume"] = "100";
            }

        }

        private void ConfigureFbalphaCPS3(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps3")
                return;

            coreSettings["fbalpha2012_cps3_frameskip"] = "0";
            coreSettings["fbalpha2012_cps3_aspect"] = "DAR";

            if (SystemConfig.isOptSet("fbalpha2012_cps3_cpu_speed_adjust"))
                coreSettings["fbalpha2012_cps3_cpu_speed_adjust"] = SystemConfig["fbalpha2012_cps3_cpu_speed_adjust"];
            else
                coreSettings["fbalpha2012_cps3_cpu_speed_adjust"] = "100";

            if (SystemConfig.isOptSet("fbalpha2012_cps3_hiscores"))
                coreSettings["fbalpha2012_cps3_hiscores"] = SystemConfig["fbalpha2012_cps3_hiscores"];
            else
                coreSettings["fbalpha2012_cps3_hiscores"] = "enabled";

            if (SystemConfig.isOptSet("fbalpha2012_cps3_controls_p1"))
                coreSettings["fbalpha2012_cps3_controls_p1"] = SystemConfig["fbalpha2012_cps3_controls_p1"];
            else
                coreSettings["fbalpha2012_cps3_controls_p1"] = "gamepad";

            if (SystemConfig.isOptSet("fbalpha2012_cps3_controls_p1"))
                coreSettings["fbalpha2012_cps3_controls_p2"] = SystemConfig["fbalpha2012_cps3_controls_p2"];
            else
                coreSettings["fbalpha2012_cps3_controls_p2"] = "gamepad";

            if (SystemConfig.isOptSet("fbalpha2012_cps3_lr_controls_p1"))
                coreSettings["fbalpha2012_cps3_lr_controls_p1"] = SystemConfig["fbalpha2012_cps3_lr_controls_p1"];
            else
                coreSettings["fbalpha2012_cps3_lr_controls_p1"] = "normal";

            if (SystemConfig.isOptSet("fbalpha2012_cps3_lr_controls_p2"))
                coreSettings["fbalpha2012_cps3_lr_controls_p2"] = SystemConfig["fbalpha2012_cps3_lr_controls_p2"];
            else
                coreSettings["fbalpha2012_cps3_lr_controls_p2"] = "normal";

        }

        private void ConfigureFbalphaCPS2(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps2")
                return;

            coreSettings["fba2012cps1_frameskip"] = "disabled";
            coreSettings["fba2012cps1_aspect"] = "DAR";

            if (SystemConfig.isOptSet("fba2012cps1_auto_rotate"))
                coreSettings["fba2012cps1_auto_rotate"] = SystemConfig["fba2012cps1_auto_rotate"];
            else
                coreSettings["fba2012cps1_auto_rotate"] = "enabled";

            if (SystemConfig.isOptSet("fba2012cps1_cpu_speed_adjust"))
                coreSettings["fba2012cps1_cpu_speed_adjust"] = SystemConfig["fba2012cps1_cpu_speed_adjust"];
            else
                coreSettings["fba2012cps1_cpu_speed_adjust"] = "100";

            if (SystemConfig.isOptSet("fba2012cps1_hiscores"))
                coreSettings["fba2012cps1_hiscores"] = SystemConfig["fba2012cps1_hiscores"];
            else
                coreSettings["fba2012cps1_hiscores"] = "enabled";

            if (SystemConfig.isOptSet("fba2012cps1_lowpass_filter"))
                coreSettings["fba2012cps1_lowpass_filter"] = SystemConfig["fba2012cps1_lowpass_filter"];
            else
                coreSettings["fba2012cps1_lowpass_filter"] = "disabled";

            if (SystemConfig.isOptSet("fba2012cps1_lowpass_range"))
                coreSettings["fba2012cps1_lowpass_range"] = SystemConfig["fba2012cps1_lowpass_range"];
            else
                coreSettings["fba2012cps1_lowpass_range"] = "50";

            if (SystemConfig.isOptSet("fba2012cps2_controls"))
                coreSettings["fba2012cps2_controls"] = SystemConfig["fba2012cps2_controls"];
            else
                coreSettings["fba2012cps2_controls"] = "gamepad";

        }

        private void ConfigureFbalphaCPS1(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps1")
                return;

            coreSettings["fba2012cps1_frameskip"] = "disabled";
            coreSettings["fba2012cps1_aspect"] = "DAR";

            if (SystemConfig.isOptSet("fba2012cps1_auto_rotate"))
                coreSettings["fba2012cps1_auto_rotate"] = SystemConfig["fba2012cps1_auto_rotate"];
            else
                coreSettings["fba2012cps1_auto_rotate"] = "enabled";

            if (SystemConfig.isOptSet("fba2012cps1_cpu_speed_adjust"))
                coreSettings["fba2012cps1_cpu_speed_adjust"] = SystemConfig["fba2012cps1_cpu_speed_adjust"];
            else
                coreSettings["fba2012cps1_cpu_speed_adjust"] = "100";

            if (SystemConfig.isOptSet("fba2012cps1_hiscores"))
                coreSettings["fba2012cps1_hiscores"] = SystemConfig["fba2012cps1_hiscores"];
            else
                coreSettings["fba2012cps1_hiscores"] = "enabled";

            if (SystemConfig.isOptSet("fba2012cps1_lowpass_filter"))
                coreSettings["fba2012cps1_lowpass_filter"] = SystemConfig["fba2012cps1_lowpass_filter"];
            else
                coreSettings["fba2012cps1_lowpass_filter"] = "disabled";

            if (SystemConfig.isOptSet("fba2012cps1_lowpass_range"))
                coreSettings["fba2012cps1_lowpass_range"] = SystemConfig["fba2012cps1_lowpass_range"];
            else
                coreSettings["fba2012cps1_lowpass_range"] = "50";

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

            if (SystemConfig.isOptSet("PerformanceMode"))
            {
                if ((SystemConfig["PerformanceMode"] == "Fast"))
                {
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
                }
                else if ((SystemConfig["PerformanceMode"] == "Balanced"))
                {
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
                }
                else if ((SystemConfig["PerformanceMode"] == "Accurate"))
                {
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
                }
            }
            else
            {
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
            }

            if (SystemConfig.isOptSet("ppsspp_internal_resolution"))
                coreSettings["ppsspp_internal_resolution"] = SystemConfig["ppsspp_internal_resolution"];
            else
                coreSettings["ppsspp_internal_resolution"] = "1440x816";

            if (SystemConfig.isOptSet("ppsspp_texture_anisotropic_filtering"))
                coreSettings["ppsspp_texture_anisotropic_filtering"] = SystemConfig["ppsspp_texture_anisotropic_filtering"];
            else
                coreSettings["ppsspp_texture_anisotropic_filtering"] = "off";

            if (SystemConfig.isOptSet("ppsspp_texture_filtering"))
                coreSettings["ppsspp_texture_filtering"] = SystemConfig["ppsspp_texture_filtering"];
            else
                coreSettings["ppsspp_texture_filtering"] = "auto";

            if (SystemConfig.isOptSet("ppsspp_texture_scaling_type"))
                coreSettings["ppsspp_texture_scaling_type"] = SystemConfig["ppsspp_texture_scaling_type"];
            else
                coreSettings["ppsspp_texture_scaling_type"] = "xbrz";

            if (SystemConfig.isOptSet("ppsspp_texture_scaling_level"))
                coreSettings["ppsspp_texture_scaling_level"] = SystemConfig["ppsspp_texture_scaling_level"];
            else
                coreSettings["ppsspp_texture_scaling_level"] = "auto";

            if (SystemConfig.isOptSet("ppsspp_texture_deposterize"))
                coreSettings["ppsspp_texture_deposterize"] = SystemConfig["ppsspp_texture_deposterize"];
            else
                coreSettings["ppsspp_texture_deposterize"] = "disabled";

            if (SystemConfig.isOptSet("ppsspp_language"))
                coreSettings["ppsspp_language"] = SystemConfig["ppsspp_language"];
            else
                coreSettings["ppsspp_language"] = "automatic";

            if (SystemConfig.isOptSet("ppsspp_io_timing_method"))
                coreSettings["ppsspp_io_timing_method"] = SystemConfig["ppsspp_io_timing_method"];
            else
                coreSettings["ppsspp_io_timing_method"] = "Fast";

            if (SystemConfig.isOptSet("ppsspp_ignore_bad_memory_access"))
                coreSettings["ppsspp_ignore_bad_memory_access"] = SystemConfig["ppsspp_ignore_bad_memory_access"];
            else
                coreSettings["ppsspp_ignore_bad_memory_access"] = "enabled";

            if (SystemConfig.isOptSet("ppsspp_texture_replacement"))
                coreSettings["ppsspp_texture_replacement"] = SystemConfig["ppsspp_texture_replacement"];
            else
                coreSettings["ppsspp_texture_replacement"] = "disabled";

        }

        private void ConfigureGambatte(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "gambatte")
                return;

            coreSettings["gambatte_gb_bootloader"] = "enabled";
            coreSettings["gambatte_gbc_color_correction_mode"] = "accurate";
            coreSettings["gambatte_gbc_color_correction"] = "GBC only";
            coreSettings["gambatte_up_down_allowed"] = "disabled";

            if (SystemConfig.isOptSet("gambatte_gb_hwmode"))
                coreSettings["gambatte_gb_hwmode"] = SystemConfig["gambatte_gb_hwmode"];
            else
                coreSettings["gambatte_gb_hwmode"] = "Auto";

            if (SystemConfig.isOptSet("gambatte_mix_frames"))
                coreSettings["gambatte_mix_frames"] = SystemConfig["gambatte_mix_frames"];
            else
                coreSettings["gambatte_mix_frames"] = "lcd_ghosting";

            if (SystemConfig.isOptSet("gambatte_gb_internal_palette"))
                coreSettings["gambatte_gb_internal_palette"] = SystemConfig["gambatte_gb_internal_palette"];
            else
                coreSettings["gambatte_gb_internal_palette"] = "GB - DMG";

            if (SystemConfig.isOptSet("gambatte_gb_colorization"))
                coreSettings["gambatte_gb_colorization"] = SystemConfig["gambatte_gb_colorization"];
            else
                coreSettings["gambatte_gb_colorization"] = "auto";

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


            if (SystemConfig.isOptSet("fbneo-neogeo-mode"))
                coreSettings["fbneo-neogeo-mode"] = SystemConfig["fbneo-neogeo-mode"];
            else
                coreSettings["fbneo-neogeo-mode"] = "UNIBIOS";

            if (SystemConfig.isOptSet("fbneo-vertical-mode"))
            {
                coreSettings["fbneo-vertical-mode"] = SystemConfig["fbneo-vertical-mode"];
                if (SystemConfig["fbneo-vertical-mode"] == "enabled")
                    SystemConfig["bezel"] = "none";
            }
            else
                coreSettings["fbneo-vertical-mode"] = "disabled";

            if (SystemConfig.isOptSet("fbneo-lightgun-hide-crosshair"))
                coreSettings["fbneo-lightgun-hide-crosshair"] = SystemConfig["fbneo-lightgun-hide-crosshair"];
            else
                coreSettings["fbneo-lightgun-hide-crosshair"] = "disabled";

        }

        private void ConfigureCitra(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "citra")
                return;

            coreSettings["citra_use_libretro_save_path"] = "LibRetro Default";
            coreSettings["citra_is_new_3ds"] = "New 3DS";

            /*            if (SystemConfig.isOptSet("citra_layout_option"))
                            coreSettings["citra_layout_option"] = SystemConfig["citra_layout_option"];
                        else
                            coreSettings["citra_layout_option"] = "Default Top-Bottom Screen";
            */
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
            {
                coreSettings["citra_layout_option"] = "Default Top-Bottom Screen";
            }

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
            {
                coreSettings["citra_layout_option"] = "Default Top-Bottom Screen";
            }

            if (SystemConfig.isOptSet("citra_mouse_show_pointer"))
                coreSettings["citra_mouse_show_pointer"] = SystemConfig["citra_mouse_show_pointer"];
            else
                coreSettings["citra_mouse_show_pointer"] = "enabled";

            if (SystemConfig.isOptSet("citra_region_value"))
                coreSettings["citra_region_value"] = SystemConfig["citra_region_value"];
            else
                coreSettings["citra_region_value"] = "Auto";

            if (SystemConfig.isOptSet("citra_resolution_factor"))
                coreSettings["citra_resolution_factor"] = SystemConfig["citra_resolution_factor"];
            else
                coreSettings["citra_resolution_factor"] = "1x (Native)";

            if (SystemConfig.isOptSet("citra_swap_screen"))
                coreSettings["citra_swap_screen"] = SystemConfig["citra_swap_screen"];
            else
                coreSettings["citra_swap_screen"] = "Top";

            if (SystemConfig.isOptSet("citra_mouse_touchscreen"))
                coreSettings["citra_mouse_touchscreen"] = SystemConfig["citra_mouse_touchscreen"];
            else
                coreSettings["citra_mouse_touchscreen"] = "enabled";

        }

        private void ConfigureMednafenSaturn(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_saturn")
                return;

            coreSettings["beetle_saturn_autortc"] = "enabled";
            coreSettings["beetle_saturn_shared_ext"] = "enabled";
            coreSettings["beetle_saturn_shared_int"] = "enabled";

            if (SystemConfig.isOptSet("beetle_saturn_autortc_lang"))
                coreSettings["beetle_saturn_autortc_lang"] = SystemConfig["beetle_saturn_autortc_lang"];
            else
                coreSettings["beetle_saturn_autortc_lang"] = "english";

            if (SystemConfig.isOptSet("beetle_saturn_cart"))
                coreSettings["beetle_saturn_cart"] = SystemConfig["beetle_saturn_cart"];
            else
                coreSettings["beetle_saturn_cart"] = "Auto Detect";

            if (SystemConfig.isOptSet("beetle_saturn_cdimagecache"))
                coreSettings["beetle_saturn_cdimagecache"] = SystemConfig["beetle_saturn_cdimagecache"];
            else
                coreSettings["beetle_saturn_cdimagecache"] = "disabled";

            if (SystemConfig.isOptSet("beetle_saturn_midsync"))
                coreSettings["beetle_saturn_midsync"] = SystemConfig["beetle_saturn_midsync"];
            else
                coreSettings["beetle_saturn_midsync"] = "disabled";

            if (SystemConfig.isOptSet("beetle_saturn_multitap_port1"))
                coreSettings["beetle_saturn_multitap_port1"] = SystemConfig["beetle_saturn_multitap_port1"];
            else
                coreSettings["beetle_saturn_multitap_port1"] = "disabled";

            if (SystemConfig.isOptSet("beetle_saturn_multitap_port2"))
                coreSettings["beetle_saturn_multitap_port2"] = SystemConfig["beetle_saturn_multitap_port2"];
            else
                coreSettings["beetle_saturn_multitap_port2"] = "disabled";

            if (SystemConfig.isOptSet("beetle_saturn_region"))
                coreSettings["beetle_saturn_region"] = SystemConfig["beetle_saturn_region"];
            else
                coreSettings["beetle_saturn_region"] = "Auto Detect";

            if (SystemConfig.isOptSet("beetle_saturn_midsync"))
                coreSettings["beetle_saturn_midsync"] = SystemConfig["beetle_saturn_midsync"];
            else
                coreSettings["beetle_saturn_midsync"] = "disabled";

        }

        private void ConfigurePicodrive(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "picodrive")
                return;

            coreSettings["picodrive_ramcart"] = "disabled";

            if (SystemConfig.isOptSet("overclk68k"))
                coreSettings["picodrive_overclk68k"] = SystemConfig["overclk68k"];
            else
                coreSettings["picodrive_overclk68k"] = "disabled";

            if (SystemConfig.isOptSet("overscan"))
                coreSettings["picodrive_overscan"] = SystemConfig["overscan"];
            else
                coreSettings["picodrive_overscan"] = "disabled";

            if (SystemConfig.isOptSet("region"))
                coreSettings["picodrive_region"] = SystemConfig["region"];
            else
                coreSettings["picodrive_region"] = "Auto";

            if (SystemConfig.isOptSet("renderer"))
                coreSettings["picodrive_renderer"] = SystemConfig["renderer"];
            else
                coreSettings["picodrive_renderer"] = "accurate";

            if (SystemConfig.isOptSet("audio_filter"))
                coreSettings["picodrive_audio_filter"] = SystemConfig["audio_filter"];
            else
                coreSettings["picodrive_audio_filter"] = "disabled";

            if (SystemConfig.isOptSet("dynamic_recompiler"))
                coreSettings["picodrive_drc"] = SystemConfig["dynamic_recompiler"];
            else
                coreSettings["picodrive_drc"] = "disabled";

            if (SystemConfig.isOptSet("input1"))
                coreSettings["picodrive_input1"] = SystemConfig["input1"];
            else
                coreSettings["picodrive_input2"] = "3 button pad";

            if (SystemConfig.isOptSet("input2"))
                coreSettings["picodrive_input2"] = SystemConfig["input2"];
            else
                coreSettings["picodrive_input2"] = "3 button pad";

        }

        private void ConfigureKronos(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "kronos")
                return;

            coreSettings["kronos_use_beetle_saves"] = "enabled";
            coreSettings["kronos_multitap_port2"] = "disabled";
            coreSettings["kronos_sh2coretype"] = "kronos";

            if (SystemConfig.isOptSet("addon_cartridge"))
                coreSettings["kronos_addon_cartridge"] = SystemConfig["addon_cartridge"];
            else
                coreSettings["kronos_addon_cartridge"] = "512K_backup_ram";

            if (SystemConfig.isOptSet("force_downsampling"))
                coreSettings["kronos_force_downsampling"] = SystemConfig["force_downsampling"];
            else
                coreSettings["kronos_force_downsampling"] = "disabled";

            if (SystemConfig.isOptSet("language_id"))
                coreSettings["kronos_language_id"] = SystemConfig["language_id"];
            else
                coreSettings["kronos_language_id"] = "English";

            if (SystemConfig.isOptSet("meshmode"))
                coreSettings["kronos_meshmode"] = SystemConfig["meshmode"];
            else
                coreSettings["kronos_meshmode"] = "disabled";

            if (SystemConfig.isOptSet("multitap_port1"))
                coreSettings["kronos_multitap_port1"] = SystemConfig["multitap_port1"];
            else
                coreSettings["kronos_multitap_port1"] = "disabled";

            if (SystemConfig.isOptSet("polygon_mode"))
                coreSettings["kronos_polygon_mode"] = SystemConfig["polygon_mode"];
            else
                coreSettings["kronos_polygon_mode"] = "cpu_tesselation";

            if (SystemConfig.isOptSet("resolution_mode"))
                coreSettings["kronos_resolution_mode"] = SystemConfig["resolution_mode"];
            else
                coreSettings["kronos_resolution_mode"] = "original";

            if (SystemConfig.isOptSet("use_cs"))
                coreSettings["kronos_use_cs"] = SystemConfig["use_cs"];
            else
                coreSettings["kronos_use_cs"] = "disabled";

            if (SystemConfig.isOptSet("videocoretype"))
                coreSettings["kronos_videocoretype"] = SystemConfig["videocoretype"];
            else
                coreSettings["kronos_videocoretype"] = "opengl";

            if (SystemConfig.isOptSet("videoformattype"))
                coreSettings["kronos_videoformattype"] = SystemConfig["videoformattype"];
            else
                coreSettings["kronos_videoformattype"] = "auto";

        }

        private void ConfigureMame2003(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame078plus" && core != "mame2003_plus")
                return;

            coreSettings["mame2003-plus_skip_disclaimer"] = "enabled";
            coreSettings["mame2003-plus_skip_warnings"] = "enabled";

            if (SystemConfig.isOptSet("mame2003-plus_analog"))
                coreSettings["mame2003-plus_analog"] = SystemConfig["mame2003-plus_analog"];
            else
                coreSettings["mame2003-plus_analog"] = "digital";

            if (SystemConfig.isOptSet("mame2003-plus_frameskip"))
                coreSettings["mame2003-plus_frameskip"] = SystemConfig["mame2003-plus_frameskip"];
            else
                coreSettings["mame2003-plus_frameskip"] = "0";

            if (SystemConfig.isOptSet("mame2003-plus_input_interface"))
                coreSettings["mame2003-plus_input_interface"] = SystemConfig["mame2003-plus_input_interface"];
            else
                coreSettings["mame2003-plus_input_interface"] = "retropad";

            if (SystemConfig.isOptSet("mame2003-plus_tate_mode"))
            {
                coreSettings["mame2003-plus_tate_mode"] = SystemConfig["mame2003-plus_tate_mode"];
                if (SystemConfig["mame2003-plus_tate_mode"] == "enabled")
                    SystemConfig["bezel"] = "none";
            }
            else
                coreSettings["mame2003-plus_tate_mode"] = "disabled";

            if (SystemConfig.isOptSet("mame2003-plus_neogeo_bios"))
                coreSettings["mame2003-plus_neogeo_bios"] = SystemConfig["mame2003-plus_neogeo_bios"];
            else
                coreSettings["mame2003-plus_neogeo_bios"] = "unibios33";

        }

        private void ConfigureQuasi88(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "quasi88")
                return;

            if (SystemConfig.isOptSet("q88_basic_mode"))
                coreSettings["q88_basic_mode"] = SystemConfig["q88_basic_mode"];
            else
                coreSettings["q88_basic_mode"] = "N88 V2";

            if (SystemConfig.isOptSet("q88_cpu_clock"))
                coreSettings["q88_cpu_clock"] = SystemConfig["q88_cpu_clock"];
            else
                coreSettings["q88_cpu_clock"] = "4";

            if (SystemConfig.isOptSet("q88_pcg-8100"))
                coreSettings["q88_pcg-8100"] = SystemConfig["q88_pcg-8100"];
            else
                coreSettings["q88_pcg-8100"] = "disabled";

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
            else if (SystemConfig.isOptSet("cap32_model"))
                coreSettings["cap32_model"] = SystemConfig["cap32_model"];
            else
                coreSettings["cap32_model"] = "6128";

            //  Ram size
            if (SystemConfig.isOptSet("cap32_ram"))
                coreSettings["cap32_ram"] = SystemConfig["cap32_ram"];
            else
                coreSettings["cap32_ram"] = "128";
        }

        private void ConfigureAtari800(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "atari800")
                return;

            if (system == "atari800")
            {
                coreSettings["RAM_SIZE"] = "64";
                coreSettings["STEREO_POKEY"] = "1";
                coreSettings["BUILTIN_BASIC"] = "1";

                if (SystemConfig.isOptSet("atari800_system"))
                    coreSettings["atari800_system"] = SystemConfig["atari800_system"];
                else
                    coreSettings["atari800_system"] = "800XL (64K)";

                if (SystemConfig.isOptSet("atari800_ntscpal"))
                    coreSettings["atari800_ntscpal"] = SystemConfig["atari800_ntscpal"];
                else
                    coreSettings["atari800_ntscpal"] = "NTSC";

                if (SystemConfig.isOptSet("atari800_sioaccel"))
                    coreSettings["atari800_sioaccel"] = SystemConfig["atari800_sioaccel"];
                else
                    coreSettings["atari800_sioaccel"] = "enabled";

                if (SystemConfig.isOptSet("atari800_artifacting"))
                    coreSettings["atari800_artifacting"] = SystemConfig["atari800_artifacting"];
                else
                    coreSettings["atari800_artifacting"] = "disabled";
            }
            else
            {
                coreSettings["atari800_system"] = "5200";
                coreSettings["RAM_SIZE"] = "16";
                coreSettings["STEREO_POKEY"] = "0";
                coreSettings["BUILTIN_BASIC"] = "0";
            }


            if (string.IsNullOrEmpty(AppConfig["bios"]))
                return;

            var atariCfg = ConfigFile.FromFile(Path.Combine(RetroarchPath, ".atari800.cfg"), new ConfigFileOptions() { CaseSensitive = true, KeepEmptyLines = true });

            string biosPath = AppConfig.GetFullPath("bios");

            if (system == "atari800")
            {
                atariCfg["ROM_OS_A_PAL"] = Path.Combine(biosPath, "ATARIOSA.ROM");
                atariCfg["ROM_OS_BB01R2"] = Path.Combine(biosPath, "ATARIXL.ROM");
                atariCfg["ROM_BASIC_C"] = Path.Combine(biosPath, "ATARIBAS.ROM");
                atariCfg["ROM_400/800_CUSTOM"] = Path.Combine(biosPath, "ATARIOSB.ROM");

                atariCfg["MACHINE_TYPE"] = "Atari XL/XE";
                atariCfg["RAM_SIZE"] = "64";
            }
            else
            {
                atariCfg["ROM_OS_A_PAL"] = "";
                atariCfg["ROM_OS_BB01R2"] = "";
                atariCfg["ROM_BASIC_C"] = "";
                atariCfg["ROM_400/800_CUSTOM"] = "";

                atariCfg["ROM_5200"] = Path.Combine(biosPath, "5200.ROM");
                atariCfg["ROM_5200_CUSTOM"] = Path.Combine(biosPath, "atari5200.ROM");
                atariCfg["MACHINE_TYPE"] = "Atari 5200";
                atariCfg["RAM_SIZE"] = "16";

            }

            atariCfg.Save(Path.Combine(RetroarchPath, ".atari800.cfg"), false);
        }

        private void ConfigureVirtualJaguar(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "virtualjaguar")
                return;

            coreSettings["virtualjaguar_usefastblitter"] = "enabled";
        }

        private void ConfigureSNes9x(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "snes9x")
                return;

            if (SystemConfig.isOptSet("ntsc_filter"))
                coreSettings["snes9x_blargg"] = SystemConfig["ntsc_filter"];
            else
                coreSettings["snes9x_blargg"] = "disabled";

            if (SystemConfig.isOptSet("overscan"))
                coreSettings["snes9x_overscan"] = SystemConfig["overscan"];
            else
                coreSettings["snes9x_overscan"] = "enabled";

            if (SystemConfig.isOptSet("gfx_hires"))
                coreSettings["snes9x_gfx_hires"] = SystemConfig["gfx_hires"];
            else
                coreSettings["snes9x_gfx_hires"] = "enabled";
        }

        private void ConfigureGenesisPlusGX(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "genesis_plus_gx")
                return;

            coreSettings["genesis_plus_gx_bram"] = "per game";
            coreSettings["genesis_plus_gx_ym2413"] = "auto";

            if (SystemConfig.isOptSet("addr_error"))
                coreSettings["genesis_plus_gx_addr_error"] = SystemConfig["addr_error"];
            else
                coreSettings["genesis_plus_gx_addr_error"] = "enabled";

            if (SystemConfig.isOptSet("lock_on"))
                coreSettings["genesis_plus_gx_lock_on"] = SystemConfig["lock_on"];
            else
                coreSettings["genesis_plus_gx_lock_on"] = "disabled";

            if (SystemConfig.isOptSet("ym2612"))
                coreSettings["genesis_plus_gx_ym2612"] = SystemConfig["ym2612"];
            else
                coreSettings["genesis_plus_gx_ym2612"] = "mame (ym2612)";

            if (SystemConfig.isOptSet("audio_filter"))
                coreSettings["genesis_plus_gx_audio_filter"] = SystemConfig["audio_filter"];
            else
                coreSettings["genesis_plus_gx_audio_filter"] = "disabled";

            if (SystemConfig.isOptSet("ntsc_filter"))
                coreSettings["genesis_plus_gx_blargg_ntsc_filter"] = SystemConfig["ntsc_filter"];
            else
                coreSettings["genesis_plus_gx_blargg_ntsc_filter"] = "disabled";

            if (SystemConfig.isOptSet("lcd_filter"))
                coreSettings["genesis_plus_gx_lcd_filter"] = SystemConfig["lcd_filter"];
            else
                coreSettings["genesis_plus_gx_lcd_filter"] = "disabled";

            if (SystemConfig.isOptSet("overscan"))
                coreSettings["genesis_plus_gx_overscan"] = SystemConfig["overscan"];
            else
                coreSettings["genesis_plus_gx_overscan"] = "disabled";

            if (SystemConfig.isOptSet("render"))
                coreSettings["genesis_plus_gx_render"] = SystemConfig["render"];
            else
                coreSettings["genesis_plus_gx_render"] = "single field";

            if (SystemConfig.isOptSet("gun_cursor"))
                coreSettings["genesis_plus_gx_gun_cursor"] = SystemConfig["gun_cursor"];
            else
                coreSettings["genesis_plus_gx_gun_cursor"] = "disabled";

            if (SystemConfig.isOptSet("gun_input"))
                coreSettings["genesis_plus_gx_gun_input"] = SystemConfig["gun_input"];
            else
                coreSettings["genesis_plus_gx_gun_input"] = "lightgun";
            
            if (SystemConfig.isOptSet("genesis_plus_gx_force_dtack"))
                coreSettings["genesis_plus_gx_force_dtack"] = SystemConfig["genesis_plus_gx_force_dtack"];
            else
                coreSettings["genesis_plus_gx_force_dtack"] = "enabled";
            
            if (SystemConfig.isOptSet("genesis_plus_gx_overclock"))
                coreSettings["genesis_plus_gx_overclock"] = SystemConfig["genesis_plus_gx_overclock"];
            else
                coreSettings["genesis_plus_gx_overclock"] = "100%";
            
            if (SystemConfig.isOptSet("genesis_plus_gx_no_sprite_limit"))
                coreSettings["genesis_plus_gx_no_sprite_limit"] = SystemConfig["genesis_plus_gx_no_sprite_limit"];
            else
                coreSettings["genesis_plus_gx_no_sprite_limit"] = "disabled";
            
            if (SystemConfig.isOptSet("genesis_plus_gx_bios"))
                coreSettings["genesis_plus_gx_bios"] = SystemConfig["genesis_plus_gx_bios"];
            else
                coreSettings["genesis_plus_gx_bios"] = "disabled";
            
            if (SystemConfig.isOptSet("genesis_plus_gx_add_on"))
                coreSettings["genesis_plus_gx_add_on"] = SystemConfig["genesis_plus_gx_add_on"];
            else
                coreSettings["genesis_plus_gx_add_on"] = "auto";

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

            coreSettings["genesis_plus_gx_wide_blargg_ntsc_filter"] = "disabled";
            coreSettings["genesis_plus_gx_wide_lcd_filter"] = "disabled";
            coreSettings["genesis_plus_gx_wide_overscan"] = "disabled";

            if (SystemConfig.isOptSet("addr_error"))
                coreSettings["genesis_plus_gx_wide_addr_error"] = SystemConfig["addr_error"];
            else
                coreSettings["genesis_plus_gx_wide_addr_error"] = "enabled";

            if (SystemConfig.isOptSet("lock_on"))
                coreSettings["genesis_plus_gx_wide_lock_on"] = SystemConfig["lock_on"];
            else
                coreSettings["genesis_plus_gx_wide_lock_on"] = "disabled";

            if (SystemConfig.isOptSet("ym2612"))
                coreSettings["genesis_plus_gx_wide_ym2612"] = SystemConfig["ym2612"];
            else
                coreSettings["genesis_plus_gx_wide_ym2612"] = "mame (ym2612)";

            if (SystemConfig.isOptSet("audio_filter"))
                coreSettings["genesis_plus_gx_wide_audio_filter"] = SystemConfig["audio_filter"];
            else
                coreSettings["genesis_plus_gx_wide_audio_filter"] = "disabled";

            if (SystemConfig.isOptSet("render"))
                coreSettings["genesis_plus_gx_wide_render"] = SystemConfig["render"];
            else
                coreSettings["genesis_plus_gx_wide_render"] = "single field";

            if (SystemConfig.isOptSet("gun_cursor"))
                coreSettings["genesis_plus_gx_wide_gun_cursor"] = SystemConfig["gun_cursor"];
            else
                coreSettings["genesis_plus_gx_wide_gun_cursor"] = "disabled";

            if (SystemConfig.isOptSet("gun_input"))
                coreSettings["genesis_plus_gx_wide_gun_input"] = SystemConfig["gun_input"];
            else
                coreSettings["genesis_plus_gx_wide_gun_input"] = "lightgun";

            if (SystemConfig.isOptSet("h40_extra_columns"))
                coreSettings["genesis_plus_gx_wide_h40_extra_columns"] = SystemConfig["h40_extra_columns"];
            else
                coreSettings["genesis_plus_gx_wide_h40_extra_columns"] = "10";

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

            if (SystemConfig.isOptSet("alternate_renderer"))
                coreSettings["mame_alternate_renderer"] = SystemConfig["alternate_renderer"];
            else
                coreSettings["mame_alternate_renderer"] = "disabled";

            if (SystemConfig.isOptSet("internal_resolution"))
                coreSettings["mame_altres"] = SystemConfig["internal_resolution"];
            else
                coreSettings["mame_altres"] = "640x480";

            if (SystemConfig.isOptSet("boot_from_cli") && Features.IsSupported("boot_from_cli"))
                coreSettings["mame_boot_from_cli"] = SystemConfig["boot_from_cli"];
            else
                coreSettings["mame_boot_from_cli"] = "enabled";

            if (SystemConfig.isOptSet("boot_to_bios") && Features.IsSupported("boot_to_bios"))
                coreSettings["mame_boot_to_bios"] = SystemConfig["boot_to_bios"];
            else
                coreSettings["mame_boot_to_bios"] = "disabled";

            if (SystemConfig.isOptSet("boot_to_osd") && Features.IsSupported("boot_to_osd"))
                coreSettings["mame_boot_to_osd"] = SystemConfig["boot_to_osd"];
            else
                coreSettings["mame_boot_to_osd"] = "disabled";
           
            if (SystemConfig.isOptSet("cheats_enable"))
                coreSettings["mame_cheats_enable"] = SystemConfig["cheats_enable"];
            else
                coreSettings["mame_cheats_enable"] = "disabled";

            if (SystemConfig.isOptSet("lightgun_mode"))
                coreSettings["mame_lightgun_mode"] = SystemConfig["lightgun_mode"];
            else
                coreSettings["mame_lightgun_mode"] = "lightgun";

            if (SystemConfig.isOptSet("4way_enable"))
                coreSettings["mame_mame_4way_enable"] = SystemConfig["mame_4way_enable"];
            else
                coreSettings["mame_mame_4way_enable"] = "enabled";

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

            if (SystemConfig.isOptSet("lcd_ghosting"))
                coreSettings["potator_lcd_ghosting"] = SystemConfig["lcd_ghosting"];
            else
                coreSettings["potator_lcd_ghosting"] = "0";

            if (SystemConfig.isOptSet("palette"))
                coreSettings["potator_palette"] = SystemConfig["palette"];
            else
                coreSettings["potator_palette"] = "default";

        }

        private void ConfigureMupen64(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mupen64plus_next" && core != "mupen64plus_next_gles3")
                return;

            coreSettings["mupen64plus-cpucore"] = "pure_interpreter";
            coreSettings["mupen64plus-rdp-plugin"] = "gliden64";
            coreSettings["mupen64plus-rsp-plugin"] = "hle";
            coreSettings["mupen64plus-EnableLODEmulation"] = "True";
            coreSettings["mupen64plus-EnableCopyAuxToRDRAM"] = "True";
            coreSettings["mupen64plus-EnableHWLighting"] = "True";
            coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "True";
            coreSettings["mupen64plus-GLideN64IniBehaviour"] = "early";

            // Overscan
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

            // Texture Enhancement
            if (SystemConfig.isOptSet("Texture_Enhancement"))
                coreSettings["mupen64plus-txEnhancementMode"] = SystemConfig["Texture_Enhancement"];
            else
                coreSettings["mupen64plus-txEnhancementMode"] = "As Is";

            // Widescreen
            if (SystemConfig.isOptSet("Widescreen") && SystemConfig.getOptBoolean("Widescreen"))
            {
                coreSettings["mupen64plus-aspect"] = "16:9 adjusted";
                retroarchConfig["aspect_ratio_index"] = "1";
                SystemConfig["bezel"] = "none";
            }
            else
                coreSettings["mupen64plus-aspect"] = "4/3";

            // 4:3 resolution
            if (SystemConfig.isOptSet("43screensize"))
                coreSettings["mupen64plus-43screensize"] = SystemConfig["43screensize"];
            else
                coreSettings["mupen64plus-43screensize"] = "960x720";

            // 16:9 resolution
            if (SystemConfig.isOptSet("169screensize"))
                coreSettings["mupen64plus-169screensize"] = SystemConfig["169screensize"];
            else
                coreSettings["mupen64plus-169screensize"] = "1280x720";

            // BilinearMode
            if (SystemConfig.isOptSet("BilinearMode"))
                coreSettings["mupen64plus-BilinearMode"] = SystemConfig["BilinearMode"];
            else
                coreSettings["mupen64plus-BilinearMode"] = "3point";

            // BilinearMode
            if (SystemConfig.isOptSet("BilinearMode"))
                coreSettings["mupen64plus-BilinearMode"] = SystemConfig["BilinearMode"];
            else
                coreSettings["mupen64plus-BilinearMode"] = "3point";

            // Multisampling aa
            if (SystemConfig.isOptSet("MultiSampling"))
                coreSettings["mupen64plus-MultiSampling"] = SystemConfig["MultiSampling"];
            else
                coreSettings["mupen64plus-MultiSampling"] = "0";

            // Texture filter
            if (SystemConfig.isOptSet("Texture_filter"))
                coreSettings["mupen64plus-txFilterMode"] = SystemConfig["Texture_filter"];
            else
                coreSettings["mupen64plus-txFilterMode"] = "None";

            // Player 1 pack
            if (SystemConfig.isOptSet("mupen64plus-pak1"))
                coreSettings["mupen64plus-pak1"] = SystemConfig["mupen64plus-pak1"];
            else
                coreSettings["mupen64plus-pak1"] = "memory";

            // Player 2 pack
            if (SystemConfig.isOptSet("mupen64plus-pak2"))
                coreSettings["mupen64plus-pak2"] = SystemConfig["mupen64plus-pak2"];
            else
                coreSettings["mupen64plus-pak2"] = "none";

            // Player 3 pack
            if (SystemConfig.isOptSet("mupen64plus-pak3"))
                coreSettings["mupen64plus-pak3"] = SystemConfig["mupen64plus-pak3"];
            else
                coreSettings["mupen64plus-pak3"] = "none";

            // Player 4 pack
            if (SystemConfig.isOptSet("mupen64plus-pak4"))
                coreSettings["mupen64plus-pak4"] = SystemConfig["mupen64plus-pak4"];
            else
                coreSettings["mupen64plus-pak4"] = "none";
        }

        private void ConfigureDosboxPure(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "dosbox_pure")
                return;

            coreSettings["dosbox_pure_advanced"] = "true";
            coreSettings["dosbox_pure_auto_mapping"] = "true";
            coreSettings["dosbox_pure_bind_unused"] = "true";
            coreSettings["dosbox_pure_savestate"] = "on";

            if (SystemConfig.isOptSet("ratio"))
                coreSettings["dosbox_pure_aspect_correction"] = SystemConfig["ratio"];
            else
                coreSettings["dosbox_pure_aspect_correction"] = "true";

            if (SystemConfig.isOptSet("cga"))
                coreSettings["dosbox_pure_cga"] = SystemConfig["cga"];
            else
                coreSettings["dosbox_pure_cga"] = "early_auto";

            if (SystemConfig.isOptSet("cpu_core"))
                coreSettings["dosbox_pure_cpu_core"] = SystemConfig["cpu_core"];
            else
                coreSettings["dosbox_pure_cpu_core"] = "auto";

            if (SystemConfig.isOptSet("cpu_type"))
                coreSettings["dosbox_pure_cpu_type"] = SystemConfig["cpu_type"];
            else
                coreSettings["dosbox_pure_cpu_type"] = "auto";

            if (SystemConfig.isOptSet("cycles"))
                coreSettings["dosbox_pure_cycles"] = SystemConfig["cycles"];
            else
                coreSettings["dosbox_pure_cycles"] = "auto";

            if (SystemConfig.isOptSet("gus"))
                coreSettings["dosbox_pure_gus"] = SystemConfig["gus"];
            else
                coreSettings["dosbox_pure_gus"] = "false";

            if (SystemConfig.isOptSet("hercules"))
                coreSettings["dosbox_pure_hercules"] = SystemConfig["hercules"];
            else
                coreSettings["dosbox_pure_hercules"] = "white";

            if (SystemConfig.isOptSet("machine"))
                coreSettings["dosbox_pure_machine"] = SystemConfig["machine"];
            else
                coreSettings["dosbox_pure_machine"] = "svga";

            if (SystemConfig.isOptSet("memory_size"))
                coreSettings["dosbox_pure_memory_size"] = SystemConfig["memory_size"];
            else
                coreSettings["dosbox_pure_memory_size"] = "16";

            if (SystemConfig.isOptSet("menu_time"))
                coreSettings["dosbox_pure_menu_time"] = SystemConfig["menu_time"];
            else
                coreSettings["dosbox_pure_menu_time"] = "5";

            if (SystemConfig.isOptSet("midi"))
                coreSettings["dosbox_pure_midi"] = SystemConfig["midi"];
            else
                coreSettings["dosbox_pure_midi"] = "scummvm/extra/Roland_SC-55.sf2";

            if (SystemConfig.isOptSet("on_screen_keyboard"))
                coreSettings["dosbox_pure_on_screen_keyboard"] = SystemConfig["on_screen_keyboard"];
            else
                coreSettings["dosbox_pure_on_screen_keyboard"] = "true";

            if (SystemConfig.isOptSet("sblaster_adlib_emu"))
                coreSettings["dosbox_pure_sblaster_adlib_emu"] = SystemConfig["sblaster_adlib_emu"];
            else
                coreSettings["dosbox_pure_sblaster_adlib_emu"] = "default";

            if (SystemConfig.isOptSet("sblaster_adlib_mode"))
                coreSettings["dosbox_pure_sblaster_adlib_mode"] = SystemConfig["sblaster_adlib_mode"];
            else
                coreSettings["dosbox_pure_sblaster_adlib_mode"] = "auto";

            if (SystemConfig.isOptSet("sblaster_conf"))
                coreSettings["dosbox_pure_sblaster_conf"] = SystemConfig["sblaster_conf"];
            else
                coreSettings["dosbox_pure_sblaster_conf"] = "A220 I7 D1 H5";

            if (SystemConfig.isOptSet("sblaster_type"))
                coreSettings["dosbox_pure_sblaster_type"] = SystemConfig["sblaster_type"];
            else
                coreSettings["dosbox_pure_sblaster_type"] = "sb16";

            if (SystemConfig.isOptSet("svga"))
                coreSettings["dosbox_pure_svga"] = SystemConfig["svga"];
            else
                coreSettings["dosbox_pure_svga"] = "vesa_nolfb";

            if (SystemConfig.isOptSet("keyboard_layout"))
                coreSettings["dosbox_pure_keyboard_layout"] = SystemConfig["keyboard_layout"];
            else
                coreSettings["dosbox_pure_keyboard_layout"] = "us";

        }

        private void ConfigurePuae(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "puae")
                return;

            if (SystemConfig.isOptSet("config_source") && SystemConfig.getOptBoolean("config_source") || (!SystemConfig.isOptSet("config_source")))
            {
                coreSettings["puae_video_options_display"] = "enabled";
                coreSettings["puae_use_whdload"] = "hdfs";

                // model
                if (SystemConfig.isOptSet("model"))
                    coreSettings["puae_model"] = SystemConfig["model"];
                else
                    coreSettings["puae_model"] = "auto";

                // cpu compatibility
                if (SystemConfig.isOptSet("cpu_compatibility"))
                    coreSettings["puae_cpu_compatibility"] = SystemConfig["cpu_compatibility"];
                else
                    coreSettings["puae_cpu_compatibility"] = "normal";

                // cpu multiplier
                if (SystemConfig.isOptSet("cpu_multiplier"))
                    coreSettings["puae_cpu_multiplier"] = SystemConfig["cpu_multiplier"];
                else
                    coreSettings["puae_cpu_multiplier"] = "default";

                // video resolution
                if (SystemConfig.isOptSet("video_resolution"))
                    coreSettings["puae_video_resolution"] = SystemConfig["video_resolution"];
                else
                    coreSettings["puae_video_resolution"] = "auto";

                // zoom_mode
                if (SystemConfig.isOptSet("zoom_mode"))
                    coreSettings["puae_zoom_mode"] = SystemConfig["zoom_mode"];
                else
                    coreSettings["puae_zoom_mode"] = "auto";

                // video_standard
                if (SystemConfig.isOptSet("video_standard"))
                    coreSettings["puae_video_standard"] = SystemConfig["video_standard"];
                else
                    coreSettings["puae_video_standard"] = "PAL auto";

                // whdload
                if (SystemConfig.isOptSet("whdload"))
                    coreSettings["puae_use_whdload_prefs"] = SystemConfig["whdload"];
                else
                    coreSettings["puae_use_whdload_prefs"] = "config";

                // Jump on B
                if (SystemConfig.isOptSet("pad_options"))
                    coreSettings["puae_retropad_options"] = SystemConfig["pad_options"];
                else
                    coreSettings["puae_retropad_options"] = "jump";

                // floppy speed
                if (SystemConfig.isOptSet("floppy_speed"))
                    coreSettings["puae_floppy_speed"] = SystemConfig["floppy_speed"];
                else
                    coreSettings["puae_floppy_speed"] = "100";

                // floppy sound
                if (SystemConfig.isOptSet("floppy_sound"))
                    coreSettings["puae_floppy_sound"] = SystemConfig["floppy_sound"];
                else
                    coreSettings["puae_floppy_sound"] = "75";
                
                // Kickstart
                if (SystemConfig.isOptSet("puae_kickstart"))
                    coreSettings["puae_kickstart"] = SystemConfig["puae_kickstart"];
                else
                    coreSettings["puae_kickstart"] = "auto";

            }
        }

        private void ConfigureFlycast(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "flycast")
                return;

            if (SystemConfig.isOptSet("config_source") && SystemConfig.getOptBoolean("config_source") || (!SystemConfig.isOptSet("config_source")))
            {
                coreSettings["reicast_threaded_rendering"] = "enabled";

                // widescreen hack
                /* if (SystemConfig.isOptSet("widescreen_hack"))
				{
					coreSettings["reicast_widescreen_hack"] = SystemConfig["widescreen_hack"];

					if (SystemConfig["widescreen_hack"] == "enabled" && !SystemConfig.isOptSet("ratio"))
					{
						int idx = ratioIndexes.IndexOf("16/9");
						if (idx >= 0)
						{
							retroarchConfig["aspect_ratio_index"] = idx.ToString();
							retroarchConfig["video_aspect_ratio_auto"] = "false";
							SystemConfig["bezel"] = "none";
							coreSettings["reicast_widescreen_cheats"] = "enabled";
						}
					}
				}
				else
				{    
					coreSettings["reicast_widescreen_cheats"] = "disabled";
					coreSettings["reicast_widescreen_hack"] = "disabled";
				}
				*/
                if (SystemConfig.isOptSet("widescreen_hack"))
                {
                    coreSettings["reicast_widescreen_hack"] = SystemConfig["widescreen_hack"];
                    if (SystemConfig["widescreen_hack"] == "enabled")
                    {
                        retroarchConfig["aspect_ratio_index"] = "1";
                        SystemConfig["bezel"] = "none";
                    }
                }
                else
                {
                    coreSettings["reicast_widescreen_hack"] = "disabled";
                    coreSettings["reicast_widescreen_cheats"] = "disabled";
                }

                // anisotropic filtering
                if (SystemConfig.isOptSet("anisotropic_filtering"))
                    coreSettings["reicast_anisotropic_filtering"] = SystemConfig["anisotropic_filtering"];
                else
                    coreSettings["reicast_anisotropic_filtering"] = "off";

                // texture upscaling (xBRZ)
                if (SystemConfig.isOptSet("texture_upscaling"))
                    coreSettings["reicast_texupscale"] = SystemConfig["texture_upscaling"];
                else
                    coreSettings["reicast_texupscale"] = "off";

                // render to texture upscaling
                if (SystemConfig.isOptSet("render_to_texture_upscaling"))
                    coreSettings["reicast_render_to_texture_upscaling"] = SystemConfig["render_to_texture_upscaling"];
                else
                    coreSettings["reicast_render_to_texture_upscaling"] = "1x";

                // force wince game compatibility
                if (SystemConfig.isOptSet("force_wince"))
                    coreSettings["reicast_force_wince"] = SystemConfig["force_wince"];
                else
                    coreSettings["reicast_force_wince"] = "disabled";

                // cable type
                if (SystemConfig.isOptSet("cable_type"))
                    coreSettings["reicast_cable_type"] = SystemConfig["cable_type"];
                else
                    coreSettings["reicast_cable_type"] = "VGA (RGB)";

                // internal resolution
                if (SystemConfig.isOptSet("internal_resolution"))
                    coreSettings["reicast_internal_resolution"] = SystemConfig["internal_resolution"];
                else
                    coreSettings["reicast_internal_resolution"] = "1280x960";
            }

        }

        private void ConfigureMesen(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mesen")
                return;

            coreSettings["mesen_aspect_ratio"] = "Auto";

            if (SystemConfig.isOptSet("hd_packs"))
                coreSettings["mesen_hdpacks"] = SystemConfig["hd_packs"];
            else
                coreSettings["mesen_hdpacks"] = "disabled";

            if (SystemConfig.isOptSet("ntsc_filter"))
                coreSettings["mesen_ntsc_filter"] = SystemConfig["ntsc_filter"];
            else
                coreSettings["mesen_ntsc_filter"] = "Disabled";

            if (SystemConfig.isOptSet("palette"))
                coreSettings["mesen_palette"] = SystemConfig["palette"];
            else
                coreSettings["mesen_palette"] = "Default";

            if (SystemConfig.isOptSet("shift_buttons"))
                coreSettings["mesen_shift_buttons_clockwise"] = SystemConfig["shift_buttons"];
            else
                coreSettings["mesen_shift_buttons_clockwise"] = "disabled";

            if (SystemConfig.isOptSet("fake_stereo"))
                coreSettings["mesen_fake_stereo"] = SystemConfig["fake_stereo"];
            else
                coreSettings["mesen_fake_stereo"] = "disabled";

        }

        private void ConfigurePcsxRearmed(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "pcsx_rearmed")
                return;

            // video resolution
            if (SystemConfig.isOptSet("neon_enhancement") && SystemConfig["neon_enhancement"] != "disabled")
            {
                if (SystemConfig["neon_enhancement"] == "enabled")
                {
                    coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "enabled";
                    coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "disabled";
                }
                else if (SystemConfig["neon_enhancement"] == "enabled_with_speedhack")
                {
                    coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "enabled";
                    coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "enabled";
                }
            }
        }

        private void ConfigureMednafenPsxHW(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_psx_hw")
                return;

            // coreSettings["beetle_psx_hw_skip_bios"] = "enabled";

            // video resolution
            if (SystemConfig.isOptSet("internal_resolution"))
                coreSettings["beetle_psx_hw_internal_resolution"] = SystemConfig["internal_resolution"];
            else
                coreSettings["beetle_psx_hw_internal_resolution"] = "1x(native)";

            // texture filtering
            if (SystemConfig.isOptSet("texture_filtering"))
                coreSettings["beetle_psx_hw_filter"] = SystemConfig["texture_filtering"];
            else
                coreSettings["beetle_psx_hw_filter"] = "nearest";

            // dithering pattern
            if (SystemConfig.isOptSet("dither_mode"))
                coreSettings["beetle_psx_hw_dither_mode"] = SystemConfig["dither_mode"];
            else
                coreSettings["beetle_psx_hw_dither_mode"] = "disabled";

            // anti aliasing
            if (SystemConfig.isOptSet("msaa"))
                coreSettings["beetle_psx_hw_msaa"] = SystemConfig["msaa"];
            else
                coreSettings["beetle_psx_hw_msaa"] = "1x";

            // force analog
            if (SystemConfig.isOptSet("analog_toggle"))
                coreSettings["beetle_psx_hw_analog_toggle"] = SystemConfig["analog_toggle"];
            else
                coreSettings["beetle_psx_hw_analog_toggle"] = "enabled";

            // widescreen
            if (SystemConfig.isOptSet("widescreen_hack"))
            {
                coreSettings["beetle_psx_hw_widescreen_hack"] = SystemConfig["widescreen_hack"];

                if (SystemConfig["widescreen_hack"] == "enabled")
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
            }
            else
                coreSettings["beetle_psx_hw_widescreen_hack"] = "disabled";

            // widescreen aspect ratio
            if (SystemConfig.isOptSet("widescreen_hack_aspect_ratio"))
                coreSettings["beetle_psx_hw_widescreen_hack_aspect_ratio"] = SystemConfig["widescreen_hack_aspect_ratio"];
            else
                coreSettings["beetle_psx_hw_widescreen_hack_aspect_ratio"] = "16:9";

            // force NTSC timings
            if (SystemConfig.isOptSet("pal_video_timing_override"))
                coreSettings["beetle_psx_hw_pal_video_timing_override"] = SystemConfig["pal_video_timing_override"];
            else
                coreSettings["beetle_psx_hw_pal_video_timing_override"] = "disabled";

            // skip BIOS
            if (SystemConfig.isOptSet("skip_bios"))
                coreSettings["beetle_psx_hw_skip_bios"] = SystemConfig["skip_bios"];
            else
                coreSettings["beetle_psx_hw_skip_bios"] = "enabled";

            /*
            // 32BPP
            if (SystemConfig.isOptSet("32bits_color_depth") && SystemConfig.getOptBoolean("32bits_color_depth"))
            {
                coreSettings["beetle_psx_hw_depth"] = "32bpp";
                coreSettings["beetle_psx_hw_dither_mode"] = "disabled";
            }
            else
            {
                coreSettings["beetle_psx_hw_depth"] = "16bpp(native)";
                coreSettings["beetle_psx_hw_dither_mode"] = "1x(native)";
            }
			*/

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

        }
    }
}
