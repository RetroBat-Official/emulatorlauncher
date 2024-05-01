using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using System.Linq;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _destRom;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("jgenesis");

            string exe = Path.Combine(path, "jgenesis-gui.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                string romFile = null;
                var entries = Zip.ListEntries(rom).Where(e => !e.IsDirectory).Select(e => e.Filename).ToArray();

                if (entries.Length == 0)
                    return null;

                romFile = entries[0];

                if (romFile == null)
                    return null;

                _destRom = Path.Combine(Path.GetTempPath(), Path.GetFileName(romFile));

                Zip.Extract(rom, Path.GetTempPath());
                if (!File.Exists(_destRom))
                    return null;

                rom = _destRom;
            }

            // settings (toml configuration)
            SetupTomlConfiguration(path, system, rom, fullscreen);

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;
            

            // command line parameters
            var commandArray = new List<string>
            {
                "-f",
                "\"" + rom + "\""
            };

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupTomlConfiguration(string path, string system, string rom, bool fullscreen)
        {
            string settingsFile = Path.Combine(path, "jgenesis-config.toml");

            using (IniFile ini = new IniFile(settingsFile, IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
            {

                string jgenSystem = GetJgenesisSystem(system);
                if (jgenSystem == null)
                    return;

                ini.WriteValue("common", "launch_in_fullscreen", fullscreen ? "true" : "false");
                BindIniFeature(ini, "common", "wgpu_backend", "jgen_renderer", "Auto");
                BindIniFeature(ini, "common", "vsync_mode", "jgen_vsync", "Enabled");
                BindBoolIniFeature(ini, "common", "force_integer_height_scaling", "integerscale", "true", "false");
                BindIniFeature(ini, "common", "filter_mode", "jgen_filter", "Linear");
                BindIniFeature(ini, "common", "preprocess_shader", "jgen_shader", "None");

                ConfigureSMS(ini, jgenSystem);
                ConfigureGenesis(ini, jgenSystem);
                ConfigureNes(ini, jgenSystem);
                ConfigureSnes(ini, jgenSystem);
                ConfigureGameboy(ini, jgenSystem);

                SetupControllers(jgenSystem);

                // Save toml file
                ini.Save();
            }
        }

        private void ConfigureNes(IniFile ini, string system)
        {
            if (system != "nes")
                return;

            BindIniFeature(ini, "nes", "forced_timing_mode", "jgen_nes_timing", "Auto");
            BindIniFeature(ini, "nes", "aspect_ratio", "jgen_nes_ratio", "Ntsc");
            BindBoolIniFeature(ini, "nes", "remove_sprite_limit", "jgen_spritelimit", "true", "false");
            BindBoolIniFeature(ini, "nes", "pal_black_border", "jgen_nes_palborder", "true", "false");
            BindBoolIniFeature(ini, "nes", "audio_60hz_hack", "jgen_nes_audiohack", "false", "true");
        }

        private void ConfigureSMS(IniFile ini, string system)
        {
            if (system != "smsgg")
                return;

            BindBoolIniFeature(ini, "smsgg", "remove_sprite_limit", "jgen_spritelimit", "true", "false");
            BindIniFeature(ini, "smsgg", "sms_aspect_ratio", "jgen_sms_ratio", "Ntsc");
            BindIniFeature(ini, "smsgg", "gg_aspect_ratio", "jgen_gg_ratio", "GgLcd");
            BindIniFeature(ini, "smsgg", "sms_region", "jgen_sms_region", "International");
            BindIniFeature(ini, "smsgg", "sms_timing_mode", "jgen_sms_timing", "Ntsc");
            BindIniFeature(ini, "smsgg", "sms_model", "jgen_sms_model", "Sms2");
            BindBoolIniFeature(ini, "smsgg", "fm_sound_unit_enabled", "jgen_sms_fmchip", "false", "true");
        }

        private void ConfigureGenesis(IniFile ini, string system)
        {
            if (system != "genesis" && system != "sega_cd")
                return;

            BindIniFeature(ini, "genesis", "forced_region", "jgen_genesis_region", "Auto");
            BindIniFeature(ini, "genesis", "forced_timing_mode", "jgen_genesis_timing", "Auto");
            BindBoolIniFeature(ini, "genesis", "remove_sprite_limits", "jgen_spritelimit", "true", "false");
            BindIniFeature(ini, "genesis", "aspect_ratio", "jgen_genesis_ratio", "Ntsc");

            if (SystemConfig["jgen_genesis_pad"] == "3btn")
            {
                ini.WriteValue("inputs", "genesis_p1_type", "ThreeButton");
                ini.WriteValue("inputs", "genesis_p2_type", "ThreeButton");
            }
            else
            {
                ini.WriteValue("inputs", "genesis_p1_type", "SixButton");
                ini.WriteValue("inputs", "genesis_p2_type", "SixButton");
            }

            if (system == "sega_cd")
            {
                string regionbios = "bios_CD_U.bin";
                if (SystemConfig.isOptSet("jgen_genesis_region") && !string.IsNullOrEmpty(SystemConfig["jgen_genesis_region"]))
                    regionbios = SystemConfig["jgen_genesis_region"];

                string segaCdBios = Path.Combine(AppConfig.GetFullPath("bios"), regionbios);

                ini.WriteValue("sega_cd", "bios_path", "'" + segaCdBios + "'");
                BindBoolIniFeature(ini, "sega_cd", "enable_ram_cartridge", "jgen_segacd_ramcart", "false", "true");
                BindBoolIniFeature(ini, "sega_cd", "load_disc_into_ram", "jgen_segacd_loadtoram", "true", "false");
            }   
        }


        private void ConfigureGameboy(IniFile ini, string system)
        {
            if (system != "game_boy")
                return;

            BindIniFeature(ini, "game_boy", "gb_palette", "jgen_gb_palette", "GreenTint");
            BindIniFeature(ini, "game_boy", "aspect_ratio", "jgen_gb_ratio", "SquarePixels");
            BindBoolIniFeature(ini, "game_boy", "force_dmg_mode", "jgen_gb_dmg", "true", "false");
            BindBoolIniFeature(ini, "game_boy", "pretend_to_be_gba", "jgen_gb_gba", "true", "false");
            BindIniFeature(ini, "game_boy", "gbc_color_correction", "jgen_gb_colorcorrect", "GbcLcd");
            BindBoolIniFeature(ini, "game_boy", "audio_60hz_hack", "jgen_gb_60fps", "true", "false");
        }

        private void ConfigureSnes(IniFile ini, string system)
        {
            if (system != "snes")
                return;

            BindIniFeature(ini, "snes", "forced_timing_mode", "jgen_snes_timing", "Auto");
            BindIniFeature(ini, "snes", "aspect_ratio", "jgen_snes_ratio", "Ntsc");
            BindBoolIniFeature(ini, "snes", "audio_60hz_hack", "jgen_snes_audiohack", "false", "true");
            BindIniFeature(ini, "snes", "gsu_overclock_factor", "jgen_snes_superfx_overclock", "1");

            SetupGuns(ini, system);
        }

        private void SetupGuns(IniFile ini, string jgenSystem)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            if (jgenSystem == "snes")
            {
                ini.WriteValue("inputs", "snes_p2_type", "SuperScope");

                ini.WriteValue("inputs.snes_keyboard.super_scope", "fire", "MouseLeft");
                ini.WriteValue("inputs.snes_keyboard.super_scope", "cursor", "MouseRight");
                ini.WriteValue("inputs.snes_keyboard.super_scope", "pause", "MouseMiddle");
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
