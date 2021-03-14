using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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

            if (core == "theodore")
                coreSettings["theodore_autorun"] = "enabled";

            ConfigureMame2003(coreSettings, system, core);
            ConfigureAtari800(coreSettings, system, core);
            ConfigureVirtualJaguar(coreSettings, system, core);
            ConfigureSNes9x(coreSettings, system, core);
            ConfigureMupen64(coreSettings, system, core);
            ConfigurePuae(coreSettings, system, core);
            ConfigureFlycast(retroarchConfig, coreSettings, system, core);
            ConfigureMame(coreSettings, system, core);
            ConfigureMesen(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPsxHW(retroarchConfig, coreSettings, system, core);
            ConfigureCap32(coreSettings, system, core);
            ConfigureQuasi88(coreSettings, system, core);
            ConfigureGenesisPlusGX(coreSettings, system, core);
            ConfigureGenesisPlusGXWide(retroarchConfig, coreSettings, system, core);
            ConfigurePotator(coreSettings, system, core);

            if (coreSettings.IsDirty)
                coreSettings.Save(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), true);
        }

        private void ConfigureMame2003(ConfigFile coreSettings, string system, string core)
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

        private void ConfigureQuasi88(ConfigFile coreSettings, string system, string core)
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

        private void ConfigureCap32(ConfigFile coreSettings, string system, string core)
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

        private void ConfigureAtari800(ConfigFile coreSettings, string system, string core)
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

        private void ConfigureVirtualJaguar(ConfigFile coreSettings, string system, string core)
        {
            if (core != "virtualjaguar")
                return;

            coreSettings["virtualjaguar_usefastblitter"] = "enabled";
        }
		
		private void ConfigureSNes9x(ConfigFile coreSettings, string system, string core)
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
		
        private void ConfigureGenesisPlusGX(ConfigFile coreSettings, string system, string core)
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

        }
		
		private void ConfigureGenesisPlusGXWide(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "genesis_plus_gx_wide")
                return;
			
            if (SystemConfig.isOptSet("ratio"))
             {
                int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                if (idx > 0)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
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
		
		private void ConfigureMame(ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame")
                return;
				
			coreSettings["mame_mame_paths_enable"] = "enabled";
            coreSettings["mame_mame_mouse_enable"] = "enabled";
            coreSettings["mame_mame_read_config"] = "enabled";
            coreSettings["mame_mame_softlists_auto_media"] = "enabled";
            coreSettings["mame_mame_write_config"] = "enabled";

            if (SystemConfig.isOptSet("alternate_renderer"))
                coreSettings["mame_alternate_renderer"] = SystemConfig["alternate_renderer"];
            else 
				coreSettings["mame_alternate_renderer"] = "disabled";

            if (SystemConfig.isOptSet("internal_resolution"))
                coreSettings["mame_altres"] = SystemConfig["internal_resolution"];
            else 
				coreSettings["mame_altres"] = "640x480";

            if (SystemConfig.isOptSet("boot_from_cli"))
                coreSettings["mame_boot_from_cli"] = SystemConfig["boot_from_cli"];
            else 
				coreSettings["mame_boot_from_cli"] = "enabled";

            if (SystemConfig.isOptSet("boot_to_bios"))
                coreSettings["mame_boot_to_bios"] = SystemConfig["boot_to_bios"];
            else 
				coreSettings["mame_boot_to_bios"] = "disabled";
				
            if (SystemConfig.isOptSet("boot_to_osd"))
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
		
		private void ConfigurePotator(ConfigFile coreSettings, string system, string core)
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

        private void ConfigureMupen64(ConfigFile coreSettings, string system, string core)
        {
            if (core != "mupen64plus_next" && core != "mupen64plus_next_gles3")
                return;

            // BilinearMode
            if (SystemConfig.isOptSet("BilinearMode"))
                coreSettings["mupen64plus-BilinearMode"] = SystemConfig["BilinearMode"];

            // Multisampling aa
            if (SystemConfig.isOptSet("MultiSampling"))
                coreSettings["mupen64plus-MultiSampling"] = SystemConfig["MultiSampling"];

            // Texture filter
            if (SystemConfig.isOptSet("Texture_filter"))
                coreSettings["mupen64plus-txFilterMode"] = SystemConfig["Texture_filter"];

            // Texture Enhancement
            if (SystemConfig.isOptSet("Texture_Enhancement"))
                coreSettings["mupen64plus-txEnhancementMode"] = SystemConfig["Texture_Enhancement"];
        }

        private void ConfigurePuae(ConfigFile coreSettings, string system, string core)
        {
            if (core != "puae")
                return;

            coreSettings["puae_video_options_display"] = SystemConfig["enabled"];

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
                coreSettings["puae_video_standard"] = SystemConfig["PAL"];

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
        }

        private void ConfigureFlycast(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "flycast")
                return;

            coreSettings["reicast_threaded_rendering"] = "enabled";

            // widescreen hack
            if (SystemConfig.isOptSet("widescreen_hack"))
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
                    }
                }
            }
            else
                coreSettings["reicast_widescreen_hack"] = "disabled";

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
                coreSettings["beetle_psx_hw_skip_bios"] = "disabled";

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
