using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class RaineGenerator : Generator
    {
        private string _path;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {

            _path = AppConfig.GetFullPath("raine");
            _resolution = resolution;

            string exe = Path.Combine(_path, "raine.exe");
            if (!File.Exists(exe) || !Environment.Is64BitOperatingSystem)
                exe = Path.Combine(_path, "raine32.exe");

            if (!File.Exists(exe))
                return null;

            string romPath = Path.GetDirectoryName(rom);

            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                rom = Path.GetFileNameWithoutExtension(rom);
            }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, _path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            SetupSettings(fullscreen, romPath);

            var commandArray = new List<string>
            {
                "-n",
                "-nb"
            };

            if (fullscreen)
            {
                commandArray.Add("-fs");
                commandArray.Add("1");
            }
            else
            {
                commandArray.Add("-fs");
                commandArray.Add("0");
            }

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args,
            };
        }

        private void SetupSettings(bool fullscreen, string romPath)
        {
            string iniFile = Path.Combine(_path, "config", "raine32_sdl.cfg");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
                {
                    Uri relRoot = new Uri(_path, UriKind.Absolute);

                    string biosPath = AppConfig.GetFullPath("bios") + "\\";
                    romPath += "\\";

                    if (!string.IsNullOrEmpty(biosPath))
                    {
                        ini.WriteValue("Directories", "rom_dir_1", biosPath);
                        ini.WriteValue("neocd", "neocd_bios", Path.Combine(biosPath, "neocdz.zip"));
                        ini.WriteValue("Directories", "emudx", Path.Combine(_path, "emudx") + "\\");
                        ini.WriteValue("Directories", "artwork", Path.Combine(_path, "artwork") + "\\"); ;
                    }

                    if (!string.IsNullOrEmpty(romPath))
                    {
                        ini.WriteValue("Directories", "rom_dir_0", romPath);
                        ini.WriteValue("neocd", "neocd_dir", Path.Combine(AppConfig.GetFullPath("roms"), "neogeocd") + "\\");
                    }

                    string sshotPath = AppConfig.GetFullPath("screenshots");
                    if (!string.IsNullOrEmpty(sshotPath))
                        ini.WriteValue("Directories", "ScreenShots", Path.Combine(sshotPath, "raine") + "\\");

                    /*
                    if (SystemConfig.isOptSet("raine_shader") && !string.IsNullOrEmpty(SystemConfig["raine_shader"]))
                        ini.WriteValue("Display", "ogl_shader", _path + "\\" + SystemConfig["raine_shader"]);
                    else
                        ini.WriteValue("Display", "ogl_shader", "None");
                    */

                    BindBoolIniFeatureOn(ini, "General", "LimitSpeed", "raine_throttle", "1", "0");
                    BindIniFeatureSlider(ini, "General", "frame_skip", "raine_frame_skip", "0");
                    BindBoolIniFeature(ini, "General", "ShowFPS", "raine_showfps", "1", "0");

                    ini.WriteValue("Display", "video_mode", "0");
                    ini.WriteValue("Display", "ogl_render", "1");
                    ini.WriteValue("Display", "fullscreen", fullscreen ? "1" : "0");
                    ini.WriteValue("Display", "use_bld", "1");
                    BindIniFeature(ini, "Display", "fix_aspect_ratio", "raine_ratio", "1");
                    BindIniFeature(ini, "Display", "rotate", "raine_rotate", "0");
                    BindIniFeature(ini, "Display", "integer_scaling", "integerscale", "0");
                    BindIniFeature(ini, "Display", "ogl_render", "raine_render", "0");
                    BindIniFeature(ini, "Display", "ogl_filter", "raine_filter", "0");
                    BindIniFeature(ini, "Display", "ogl_dbuf", "raine_doublebuffer", "0");
                    BindBoolIniFeatureOn(ini, "Display", "keep_ratio", "raine_keep_ratio", "1", "0");

                    BindIniFeature(ini, "Sound", "sample_rate", "raine_sample_rate", "44100");

                    BindIniFeature(ini, "neogeo", "bios", "raine_bios", "25");
                    ini.WriteValue("neogeo", "shared_saveram", "0");

                    BindIniFeatureSlider(ini, "neocd", "music_volume", "raine_music_volume", "70");
                    BindIniFeatureSlider(ini, "neocd", "sfx_volume", "raine_sfx_volume", "70");
                    BindBoolIniFeatureOn(ini, "neocd", "allowed_speed_hacks", "raine_speed_hack", "1", "0");
                    BindIniFeature(ini, "neocd", "cdrom_speed", "raine_cd_speed", "8");

                    BindBoolIniFeatureOn(ini, "emulator_joy_config", "hat_for_moves", "raine_hat_for_moves", "1", "0");

                    CreateControllerConfiguration(ini);
                }
            }
            catch { }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
            {
                ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);
                return 0;
            }
            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);
            return ret;
        }
    }
}
