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

            if (core == "mame078" || core == "mame2003")
            {
                coreSettings["mame2003_skip_disclaimer"] = "enabled";
                coreSettings["mame2003_skip_warnings"] = "enabled";
            }

            if (core == "mame078plus" || core == "mame2003_plus")
            {
                coreSettings["mame2003-plus_skip_disclaimer"] = "enabled";
                coreSettings["mame2003-plus_skip_warnings"] = "enabled";
                // coreSettings["mame2003-plus_analog"] = "digital";
            }

            if (core == "theodore")
                coreSettings["theodore_autorun"] = "enabled";

            ConfigureAtari800(coreSettings, system, core);
            ConfigureVirtualJaguar(coreSettings, system, core);
            ConfigureSNes9xNext(coreSettings, system, core);
            ConfigureMupen64(coreSettings, system, core);
            ConfigurePuae(coreSettings, system, core);
            ConfigureFlycast(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPsxHW(retroarchConfig, coreSettings, system, core);

            if (coreSettings.IsDirty)
                coreSettings.Save(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), true);
        }

        private void ConfigureAtari800(ConfigFile coreSettings, string system, string core)
        {
            if (core != "atari800")
                return;

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

        private void ConfigureSNes9xNext(ConfigFile coreSettings, string system, string core)
        {
            if (core != "snes9x" && core != "snes9x_next")
                return;

            coreSettings["snes9x_2010_reduce_sprite_flicker"] = SystemConfig["enabled"];

            // Reduce slowdown
            if (SystemConfig.isOptSet("reduce_slowdown"))
                coreSettings["snes9x_2010_overclock_cycles"] = SystemConfig["reduce_slowdown"];
            else
                coreSettings["snes9x_2010_overclock_cycles"] = "compatible";
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
        }

        private void ConfigureMednafenPsxHW(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_psx_hw")
                return;

            coreSettings["beetle_psx_hw_skip_bios"] = "enabled";

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

            // widescreen
            if (SystemConfig.isOptSet("widescreen_hack"))
            {
                coreSettings["beetle_psx_hw_widescreen_hack"] = SystemConfig["widescreen_hack"];

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
                coreSettings["beetle_psx_hw_widescreen_hack"] = "disabled";


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
