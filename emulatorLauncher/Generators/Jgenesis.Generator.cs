using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;
using System.Linq;
using System;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("jgenesis");

            bool gui = false;

            string exe = Path.Combine(path, "jgenesis-cli.exe");

            if (SystemConfig.isOptSet("jgen_gui") && SystemConfig.getOptBoolean("jgen_gui"))
            {
                exe = Path.Combine(path, "jgenesis-gui.exe");
                gui = true;
            }
            
            if (!File.Exists(exe))
                return null;

            string hardware = GetJgenesisHardware(system);

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string[] extensions = new string[] { ".cue", ".sms", ".gg", ".md", ".chd", ".nes", ".sfc", ".gb", ".gbc", ".bin" };

            if (Path.GetExtension(rom).ToLower() == ".zip" || Path.GetExtension(rom).ToLower() == ".7z")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            // settings (toml configuration)
            SetupTomlConfiguration(path, system, fullscreen);

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;


            // command line parameters
            var commandArray = new List<string>();
            commandArray.Add("-f");
            commandArray.Add("\"" + rom + "\"");

            if (hardware != null && gui == false)
            {
                commandArray.Add("--hardware");
                commandArray.Add(hardware);
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupTomlConfiguration(string path, string system, bool fullscreen)
        {
            string settingsFile = Path.Combine(path, "jgenesis-config.toml");

            if (!File.Exists(settingsFile))
            {
                string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "jgenesis", "jgenesis-config-template.toml");
                if (!File.Exists(templateFile))
                    return;

                try { File.Copy(templateFile, settingsFile); }
                catch
                { }
            }

            using (IniFile ini = new IniFile(settingsFile, IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
            {

                string jgenSystem = GetJgenesisSystem(system);
                if (jgenSystem == null)
                    return;

                ini.WriteValue("common", "launch_in_fullscreen", fullscreen ? "true" : "false");
                
                if (SystemConfig.isOptSet("jgen_renderer") && !string.IsNullOrEmpty(SystemConfig["jgen_renderer"]))
                    ini.WriteValue("common", "wgpu_backend", "\"" + SystemConfig["jgen_renderer"] + "\"");
                else
                    ini.WriteValue("common", "wgpu_backend", "\"" + "Auto" + "\"");

                if (SystemConfig.isOptSet("jgen_vsync") && !string.IsNullOrEmpty(SystemConfig["jgen_vsync"]))
                    ini.WriteValue("common", "vsync_mode", "\"" + SystemConfig["jgen_vsync"] + "\"");
                else
                    ini.WriteValue("common", "vsync_mode", "\"" + "Enabled" + "\"");

                BindBoolIniFeature(ini, "common", "force_integer_height_scaling", "integerscale", "true", "false");

                if (SystemConfig.isOptSet("jgen_filter") && !string.IsNullOrEmpty(SystemConfig["jgen_filter"]))
                    ini.WriteValue("common", "filter_mode", "\"" + SystemConfig["jgen_filter"] + "\"");
                else
                    ini.WriteValue("common", "filter_mode", "\"" + "Linear" + "\"");

                if (SystemConfig.isOptSet("jgen_shader") && !string.IsNullOrEmpty(SystemConfig["jgen_shader"]))
                    ini.WriteValue("common", "preprocess_shader", "\"" + SystemConfig["jgen_shader"] + "\"");
                else
                    ini.WriteValue("common", "preprocess_shader", "\"" + "None" + "\"");

                if (SystemConfig.isOptSet("jgen_scanlines") && !string.IsNullOrEmpty(SystemConfig["jgen_scanlines"]))
                    ini.WriteValue("common", "scanlines", "\"" + SystemConfig["jgen_scanlines"] + "\"");
                else
                    ini.WriteValue("common", "scanlines", "\"" + "None" + "\"");

                ConfigureGameboy(ini, jgenSystem);
                ConfigureGenesis(ini, jgenSystem);
                ConfigureNes(ini, jgenSystem);
                ConfigureSMS(ini, jgenSystem, system);
                ConfigureSnes(ini, jgenSystem);

                SetupControllers(ini, jgenSystem);

                // Save toml file
                ini.Save();
            }
        }

        private void ConfigureGameboy(IniFile ini, string system)
        {
            if (system != "game_boy")
                return;

            if (SystemConfig.isOptSet("jgen_gb_palette") && !string.IsNullOrEmpty(SystemConfig["jgen_gb_palette"]))
                ini.WriteValue("game_boy", "gb_palette", "\"" + SystemConfig["jgen_gb_palette"] + "\"");
            else
                ini.WriteValue("game_boy", "gb_palette", "\"" + "GreenTint" + "\"");

            if (SystemConfig.isOptSet("jgen_gb_ratio") && !string.IsNullOrEmpty(SystemConfig["jgen_gb_ratio"]))
                ini.WriteValue("game_boy", "aspect_ratio", "\"" + SystemConfig["jgen_gb_ratio"] + "\"");
            else
                ini.WriteValue("game_boy", "aspect_ratio", "\"" + "SquarePixels" + "\"");

            if (SystemConfig.isOptSet("jgen_gbc_colorcorrect") && !string.IsNullOrEmpty(SystemConfig["jgen_gbc_colorcorrect"]))
                ini.WriteValue("game_boy", "gbc_color_correction", "\"" + SystemConfig["jgen_gbc_colorcorrect"] + "\"");
            else
                ini.WriteValue("game_boy", "gbc_color_correction", "\"" + "GbcLcd" + "\"");

            BindBoolIniFeature(ini, "game_boy", "audio_60hz_hack", "jgen_gb_60fps", "true", "false");
            BindBoolIniFeature(ini, "game_boy", "force_dmg_mode", "jgen_gb_dmg", "true", "false");
            BindBoolIniFeature(ini, "game_boy", "pretend_to_be_gba", "jgen_gb_gba", "true", "false");
        }

        private void ConfigureGenesis(IniFile ini, string system)
        {
            if (system != "genesis" && system != "sega_cd")
                return;

            if (SystemConfig.isOptSet("jgen_genesis_region") && !string.IsNullOrEmpty(SystemConfig["jgen_genesis_region"]))
                ini.WriteValue("genesis", "forced_region", "\"" + SystemConfig["jgen_genesis_region"] + "\"");
            else
                ini.Remove("genesis", "forced_region");

            if (SystemConfig.isOptSet("jgen_genesis_timing") && !string.IsNullOrEmpty(SystemConfig["jgen_genesis_timing"]))
                ini.WriteValue("genesis", "forced_timing_mode", "\"" + SystemConfig["jgen_genesis_timing"] + "\"");
            else
                ini.Remove("genesis", "forced_timing_mode");

            if (SystemConfig.isOptSet("jgen_genesis_ratio") && !string.IsNullOrEmpty(SystemConfig["jgen_genesis_ratio"]))
                ini.WriteValue("genesis", "aspect_ratio", "\"" + SystemConfig["jgen_genesis_ratio"] + "\"");
            else
                ini.WriteValue("genesis", "aspect_ratio", "\"" + "Ntsc" + "\"");

            BindBoolIniFeature(ini, "genesis", "remove_sprite_limits", "jgen_spritelimit", "true", "false");

            if (SystemConfig["jgen_genesis_pad"] == "3btn")
            {
                ini.WriteValue("inputs", "genesis_p1_type", "\"" + "ThreeButton" + "\"");
                ini.WriteValue("inputs", "genesis_p2_type", "\"" + "ThreeButton" + "\"");
            }
            else
            {
                ini.WriteValue("inputs", "genesis_p1_type", "\"" + "SixButton" + "\"");
                ini.WriteValue("inputs", "genesis_p2_type", "\"" + "SixButton" + "\"");
            }

            if (system == "sega_cd")
            {
                string regionbios = "bios_CD_U.bin";
                if (SystemConfig.isOptSet("jgen_genesis_region") && !string.IsNullOrEmpty(SystemConfig["jgen_genesis_region"]))
                {
                    switch (SystemConfig["jgen_genesis_region"])
                    {
                        case "Europe":
                            regionbios = "bios_CD_E.bin";
                            break;
                        case "Americas":
                            regionbios = "bios_CD_J.bin";
                            break;
                        case "Japan":
                            regionbios = "bios_CD_U.bin";
                            break;
                    }
                }

                string segaCdBios = Path.Combine(AppConfig.GetFullPath("bios"), regionbios);

                ini.WriteValue("sega_cd", "bios_path", "'" + segaCdBios + "'");
                BindBoolIniFeature(ini, "sega_cd", "enable_ram_cartridge", "jgen_segacd_ramcart", "false", "true");
                BindBoolIniFeature(ini, "sega_cd", "load_disc_into_ram", "jgen_segacd_loadtoram", "true", "false");
            }
        }

        private void ConfigureNes(IniFile ini, string system)
        {
            if (system != "nes")
                return;

            if (SystemConfig.isOptSet("jgen_nes_timing") && !string.IsNullOrEmpty(SystemConfig["jgen_nes_timing"]))
                ini.WriteValue("nes", "forced_timing_mode", "\"" + SystemConfig["jgen_nes_timing"] + "\"");
            else
                ini.Remove("nes", "forced_timing_mode");

            if (SystemConfig.isOptSet("jgen_nes_ratio") && !string.IsNullOrEmpty(SystemConfig["jgen_nes_ratio"]))
                ini.WriteValue("nes", "aspect_ratio", "\"" + SystemConfig["jgen_nes_ratio"] + "\"");
            else
                ini.WriteValue("nes", "aspect_ratio", "\"" + "Ntsc" + "\"");

            BindBoolIniFeature(ini, "nes", "remove_sprite_limit", "jgen_spritelimit", "true", "false");
            BindBoolIniFeature(ini, "nes", "pal_black_border", "jgen_nes_palborder", "true", "false");
            BindBoolIniFeature(ini, "nes", "audio_60hz_hack", "jgen_nes_audiohack", "false", "true");
        }

        private void ConfigureSMS(IniFile ini, string system, string esSystem)
        {
            if (system != "smsgg")
                return;

            BindBoolIniFeature(ini, "smsgg", "remove_sprite_limit", "jgen_spritelimit", "true", "false");

            if (SystemConfig.isOptSet("jgen_sms_ratio") && !string.IsNullOrEmpty(SystemConfig["jgen_sms_ratio"]))
                ini.WriteValue("smsgg", "sms_aspect_ratio", "\"" + SystemConfig["jgen_sms_ratio"] + "\"");
            else
                ini.WriteValue("smsgg", "sms_aspect_ratio", "\"" + "Ntsc" + "\"");

            if (SystemConfig.isOptSet("jgen_gg_ratio") && !string.IsNullOrEmpty(SystemConfig["jgen_gg_ratio"]))
                ini.WriteValue("smsgg", "gg_aspect_ratio", "\"" + SystemConfig["jgen_gg_ratio"] + "\"");
            else
                ini.WriteValue("smsgg", "gg_aspect_ratio", "\"" + "GgLcd" + "\"");

            if (SystemConfig.isOptSet("jgen_sms_region") && !string.IsNullOrEmpty(SystemConfig["jgen_sms_region"]))
                ini.WriteValue("smsgg", "sms_region", "\"" + SystemConfig["jgen_sms_region"] + "\"");
            else
                ini.WriteValue("smsgg", "sms_region", "\"" + "International" + "\"");

            if (SystemConfig.isOptSet("jgen_sms_timing") && !string.IsNullOrEmpty(SystemConfig["jgen_sms_timing"]))
                ini.WriteValue("smsgg", "sms_timing_mode", "\"" + SystemConfig["jgen_sms_timing"] + "\"");
            else
                ini.WriteValue("smsgg", "sms_timing_mode", "\"" + "Ntsc" + "\"");

            if (SystemConfig.isOptSet("jgen_sms_model") && !string.IsNullOrEmpty(SystemConfig["jgen_sms_model"]))
                ini.WriteValue("smsgg", "sms_model", "\"" + SystemConfig["jgen_sms_model"] + "\"");
            else
                ini.WriteValue("smsgg", "sms_model", esSystem == "gamegear" ? "\"" + "Sms1" + "\"" : "\"" + "Sms2" + "\"");

            BindBoolIniFeature(ini, "smsgg", "fm_sound_unit_enabled", "jgen_sms_fmchip", "false", "true");
        }

        private void ConfigureSnes(IniFile ini, string system)
        {
            if (system != "snes")
                return;

            if (SystemConfig.isOptSet("jgen_snes_timing") && !string.IsNullOrEmpty(SystemConfig["jgen_snes_timing"]))
                ini.WriteValue("snes", "forced_timing_mode", "\"" + SystemConfig["jgen_snes_timing"] + "\"");
            else
                ini.Remove("snes", "forced_timing_mode");

            if (SystemConfig.isOptSet("jgen_snes_ratio") && !string.IsNullOrEmpty(SystemConfig["jgen_snes_ratio"]))
                ini.WriteValue("snes", "aspect_ratio", "\"" + SystemConfig["jgen_snes_ratio"] + "\"");
            else
                ini.WriteValue("snes", "aspect_ratio", "\"" + "Ntsc" + "\"");

            BindBoolIniFeature(ini, "snes", "audio_60hz_hack", "jgen_snes_audiohack", "false", "true");
            BindIniFeature(ini, "snes", "gsu_overclock_factor", "jgen_snes_superfx_overclock", "1");

            SetupGuns(ini, system);
        }

        private void SetupGuns(IniFile ini, string jgenSystem)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
            {
                ini.WriteValue("inputs", "snes_p2_type", "\"" + "Gamepad" + "\"");
                ini.WriteValue("inputs", "nes_p2_type", "\"" + "Gamepad" + "\"");
                return;
            }

            if (jgenSystem == "snes")
            {
                ini.WriteValue("inputs", "snes_p2_type", "\"" + "SuperScope" + "\"");

                ini.WriteValue("inputs.snes_keyboard.super_scope", "fire", "\"" + "MouseLeft" + "\"");
                ini.WriteValue("inputs.snes_keyboard.super_scope", "cursor", "\"" + "MouseRight" + "\"");
                ini.WriteValue("inputs.snes_keyboard.super_scope", "pause", "\"" + "MouseMiddle" + "\"");

                ini.WriteValue("inputs.snes_super_scope", "fire", "\"" + "MouseLeft" + "\"");
                ini.WriteValue("inputs.snes_super_scope", "cursor", "\"" + "MouseRight" + "\"");
                ini.WriteValue("inputs.snes_super_scope", "pause", "\"" + "MouseMiddle" + "\"");
                return;
            }

            if (jgenSystem == "nes")
            {
                ini.WriteValue("inputs", "nes_p2_type", "\"" + "Zapper" + "\"");

                ini.WriteValue("inputs.nes_zapper", "fire", "\"" + "MouseLeft" + "\"");
                ini.WriteValue("inputs.nes_zapper", "force_offscreen", "\"" + "MouseRight" + "\"");
            }
        }

        private string GetJgenesisSystem(string System)
        {
            switch (System)
            {
                case "nes":
                    return "nes";
                case "snes":
                    return "snes";
                case "segacd":
                    return "sega_cd";
                case "megadrive":
                    return "genesis";
                case "mastersystem":
                case "gamegear":
                    return "smsgg";
                case "gb":
                case "gbc":
                    return "game_boy";
            }
            return null;
        }

        private string GetJgenesisHardware(string System)
        {
            switch (System)
            {
                case "nes":
                    return "Nes";
                case "snes":
                    return "Snes";
                case "segacd":
                    return "SegaCd";
                case "megadrive":
                    return "Genesis";
                case "mastersystem":
                case "gamegear":
                    return "MasterSystem";
                case "gb":
                case "gbc":
                    return "GameBoy";
            }
            return null;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
