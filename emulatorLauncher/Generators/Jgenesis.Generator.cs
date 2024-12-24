using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;
using System.Linq;
using System;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private SaveStatesWatcher _saveStatesWatcher;
        private int _saveStateSlot;
        static List<string> _mdSystems = new List<string>() { "sega_cd", "genesis", "sega_32x" };
        static List<string> _noZipSystems = new List<string>() { "sega_cd" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("jgenesis");

            bool gui = false;

            string exe = Path.Combine(path, "jgenesis-cli.exe");

            if (SystemConfig.isOptSet("jgen_gui") && SystemConfig.getOptBoolean("jgen_gui"))
            {
                SimpleLogger.Instance.Info("[INFO] Running jgenesis-gui, saveStates will not autoload");
                exe = Path.Combine(path, "jgenesis-gui.exe");
                gui = true;
            }
            
            if (!File.Exists(exe))
                return null;

            string hardware = GetJgenesisHardware(system);
            string jGenSystem = GetJgenesisSystem(system);

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string[] extensions = new string[] { ".cue", ".sms", ".gg", ".md", ".chd", ".nes", ".sfc", ".gb", ".gbc", ".bin" };

            if (_noZipSystems.Contains(jGenSystem) && (Path.GetExtension(rom).ToLower() == ".zip" || Path.GetExtension(rom).ToLower() == ".7z" || Path.GetExtension(rom).ToLower() == ".squashfs"))
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            string savesSystem = GetSavesSystem(system);

            if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
                string emulatorPath = Path.Combine(path, "states", savesSystem);

                _saveStatesWatcher = new JGenesisSaveStatesMonitor(rom, emulatorPath, localPath, Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png"));
                _saveStatesWatcher.PrepareEmulatorRepository();
                _saveStateSlot = _saveStatesWatcher.Slot;
            }
            else
                _saveStatesWatcher = null;

            // settings (toml configuration)
            SetupTomlConfiguration(path, jGenSystem, system, savesSystem, fullscreen);

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            
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

            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(SystemConfig["state_file"]) && File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("--load-save-state");
                commandArray.Add(_saveStateSlot.ToString());
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupTomlConfiguration(string path, string jGenSystem, string system, string savesSystem, bool fullscreen)
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

            using (IniFileJGenesis ini = new IniFileJGenesis(settingsFile, IniOptionsJGenesis.UseSpaces | IniOptionsJGenesis.KeepEmptyValues | IniOptionsJGenesis.KeepEmptyLines))
            {
                // Paths
                string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "jgenesis");
                if (!Directory.Exists(savesPath))
                    try { Directory.CreateDirectory(savesPath); } catch { }

                ini.DeleteSection("game_boy");

                ini.WriteValue("common", "save_path", "\"Custom\"");

                ini.WriteValue("common", "custom_save_path", "'" + savesPath + "'");
                ini.WriteValue("common", "state_path", "\"EmulatorFolder\"");
                ini.WriteValue("common", "custom_state_path", "\"\"");

                BindBoolIniFeatureOn(ini, "common", "audio_sync", "jgen_async", "true", "false");

                if (SystemConfig.isOptSet("jgen_prescale") && !string.IsNullOrEmpty(SystemConfig["jgen_prescale"]))
                {
                    ini.WriteValue("common", "auto_prescale", "false");
                    ini.WriteValue("common", "prescale_factor", SystemConfig["jgen_prescale"].ToIntegerString());
                }
                else
                {
                    ini.WriteValue("common", "prescale_factor", "3");
                    ini.WriteValue("common", "auto_prescale", "true");
                }

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

                ConfigureGameboy(ini, jGenSystem);
                ConfigureGenesis(ini, jGenSystem);
                ConfigureNes(ini, jGenSystem);
                ConfigureSMS(ini, jGenSystem, system);
                ConfigureSnes(ini, jGenSystem);

                SetupControllers(ini, jGenSystem);

                // Save toml file
                ini.Save();
            }
        }

        private void ConfigureGameboy(IniFileJGenesis ini, string system)
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

        private void ConfigureGenesis(IniFileJGenesis ini, string system)
        {
            if (system != "genesis" && system != "sega_cd" && system != "sega_32x")
                return;

            BindBoolIniFeatureOn(ini, "genesis", "adjust_aspect_ratio_in_2x_resolution", "jgen_gen_aspectadjust", "true", "false");

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

            if (SystemConfig.getOptBoolean("md_3buttons"))
            {
                ini.WriteValue("input.genesis", "p1_type", "\"" + "ThreeButton" + "\"");
                ini.WriteValue("input.genesis", "p2_type", "\"" + "ThreeButton" + "\"");
            }
            else
            {
                ini.WriteValue("input.genesis", "p1_type", "\"" + "SixButton" + "\"");
                ini.WriteValue("input.genesis", "p2_type", "\"" + "SixButton" + "\"");
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
                BindBoolIniFeatureOn(ini, "sega_cd", "enable_ram_cartridge", "jgen_segacd_ramcart", "true", "false");
                BindBoolIniFeature(ini, "sega_cd", "load_disc_into_ram", "jgen_segacd_loadtoram", "true", "false");
            }
        }

        private void ConfigureNes(IniFileJGenesis ini, string system)
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
            BindBoolIniFeatureOn(ini, "nes", "audio_60hz_hack", "jgen_nes_audiohack", "true", "false");

            // Cropping
            if (SystemConfig.isOptSet("jgen_nes_crop_sides") && !string.IsNullOrEmpty(SystemConfig["jgen_nes_crop_sides"]))
            {
                string cropSide = SystemConfig["jgen_nes_crop_sides"].ToIntegerString();
                ini.WriteValue("nes.overscan", "top", cropSide);
                ini.WriteValue("nes.overscan", "bottom", cropSide);
            }
            else
            {
                ini.WriteValue("nes.overscan", "top", "0");
                ini.WriteValue("nes.overscan", "bottom", "0");
            }

            if (SystemConfig.isOptSet("jgen_nes_crop_topdown") && !string.IsNullOrEmpty(SystemConfig["jgen_nes_crop_topdown"]))
            {
                string cropVert = SystemConfig["jgen_nes_crop_topdown"].ToIntegerString();
                ini.WriteValue("nes.overscan", "left", cropVert);
                ini.WriteValue("nes.overscan", "right", cropVert);
            }
            else
            {
                ini.WriteValue("nes.overscan", "left", "0");
                ini.WriteValue("nes.overscan", "right", "0");
            }

            SetupGuns(ini, system);
        }

        private void ConfigureSMS(IniFileJGenesis ini, string system, string esSystem)
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

            BindBoolIniFeatureOn(ini, "smsgg", "fm_sound_unit_enabled", "jgen_sms_fmchip", "true", "false");
            BindBoolIniFeature(ini, "smsgg", "sms_crop_vertical_border", "jgen_sms_cropvert", "true", "false");
            BindBoolIniFeature(ini, "smsgg", "sms_crop_left_border", "jgen_sms_cropleft", "true", "false");
        }

        private void ConfigureSnes(IniFileJGenesis ini, string system)
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

            BindBoolIniFeatureOn(ini, "snes", "audio_60hz_hack", "jgen_snes_audiohack", "true", "false");
            BindIniFeatureSlider(ini, "snes", "gsu_overclock_factor", "jgen_snes_superfx_overclock", "1");

            SetupGuns(ini, system);
        }

        private void SetupGuns(IniFileJGenesis ini, string jgenSystem)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
            {
                ini.WriteValue("input.snes", "p2_type", "\"" + "Gamepad" + "\"");
                ini.WriteValue("input.nes", "p2_type", "\"" + "Gamepad" + "\"");
                return;
            }

            if (jgenSystem == "snes")
            {
                ini.DeleteSection("input.snes.mapping_1.super_scope");
                ini.WriteValue("[[input.snes.mapping_2.super_scope]]", "", null);

                ini.WriteValue("input.snes", "p2_type", "\"" + "SuperScope" + "\"");

                ini.WriteValue("[[input.snes.mapping_1.super_scope.fire]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.snes.mapping_1.super_scope.fire]]", "button", "\"" + "Left" + "\"");
                ini.WriteValue("[[input.snes.mapping_1.super_scope.cursor]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.snes.mapping_1.super_scope.cursor]]", "button", "\"" + "Right" + "\"");
                ini.WriteValue("[[input.snes.mapping_1.super_scope.pause]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.snes.mapping_1.super_scope.pause]]", "button", "\"" + "Middle" + "\"");
                return;
            }

            if (jgenSystem == "nes")
            {
                ini.DeleteSection("input.nes.mapping_1.zapper");
                ini.WriteValue("[[input.nes.mapping_2.zapper]]", "", null);

                ini.WriteValue("input.nes", "p2_type", "\"" + "Zapper" + "\"");

                ini.WriteValue("[[input.nes.mapping_1.zapper.fire]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.nes.mapping_1.zapper.fire]]", "button", "\"" + "Left" + "\"");
                ini.WriteValue("[[input.nes.mapping_1.zapper.force_offscreen]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.nes.mapping_1.zapper.force_offscreen]]", "button", "\"" + "Right" + "\"");
                return;
            }
        }

        private string GetJgenesisSystem(string System)
        {
            switch (System)
            {
                case "nes":
                case "famicom":
                    return "nes";
                case "snes":
                case "superfamicom":
                case "sfamicom":
                    return "snes";
                case "segacd":
                case "megacd":
                    return "sega_cd";
                case "megadrive":
                    return "genesis";
                case "mastersystem":
                case "gamegear":
                    return "smsgg";
                case "gb":
                case "gbc":
                    return "game_boy";
                case "sega32x":
                case "mega32x":
                    return "sega_32x";
            }
            return null;
        }

        private string GetJgenesisHardware(string System)
        {
            switch (System)
            {
                case "nes":
                case "famicom":
                    return "Nes";
                case "snes":
                case "superfamicom":
                case "sfamicom":
                    return "Snes";
                case "segacd":
                case "megacd":
                    return "SegaCd";
                case "megadrive":
                case "genesis":
                    return "Genesis";
                case "mastersystem":
                case "gamegear":
                case "gg":
                case "ms":
                    return "MasterSystem";
                case "gb":
                case "gbc":
                case "gameboy":
                case "gameboycolor":
                    return "GameBoy";
                case "sega32x":
                case "mega32x":
                    return "Sega32X";
            }
            return null;
        }

        private string GetSavesSystem(string System)
        {
            switch (System)
            {
                case "gb":
                case "gameboy":
                    return "gb";
                case "gbc":
                case "gameboycolor":
                    return "gbc";
                case "gamegear":
                case "gg":
                    return "gg";
                case "mastersystem":
                    return "sms";
                case "megadrive":
                case "genesis":
                    return "md";
                case "nes":
                case "famicom":
                    return "nes";
                case "segacd":
                case "megacd":
                    return "scd";
                case "sega32x":
                case "mega32x":
                    return "32x";
                case "snes":
                case "superfamicom":
                case "sfamicom":
                    return "sfc";
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

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }
    }
}
