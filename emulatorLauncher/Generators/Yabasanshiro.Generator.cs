using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class YabasanshiroGenerator : Generator
    {
        public YabasanshiroGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private string _multitap;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("yabasanshiro");

            string exe = Path.Combine(path, "yabasanshiro.exe");

            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            bool startBios = SystemConfig.getOptBoolean("saturn_startbios");

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            
            _resolution = resolution;

            // Manage .m3u files
            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                var fromm3u = MultiDiskImageFile.FromFile(rom);

                if (fromm3u.Files.Length == 0)
                    throw new ApplicationException("m3u file does not contain any game file.");

                else if (fromm3u.Files.Length == 1)
                    rom = fromm3u.Files[0];

                else
                {
                    if (SystemConfig.isOptSet("saturn_discnumber") && !string.IsNullOrEmpty(SystemConfig["saturn_discnumber"]))
                    {
                        int discNumber = SystemConfig["saturn_discnumber"].ToInteger();
                        if (discNumber >= 0 && discNumber <= fromm3u.Files.Length)
                            rom = fromm3u.Files[discNumber];
                        else
                            rom = fromm3u.Files[0];
                    }
                    else
                        rom = fromm3u.Files[0];
                }

                if (!File.Exists(rom))
                    throw new ApplicationException("File '" + rom + "' does not exist");
            }

            SetupConfig(path, system, exe, rom, resolution, fullscreen);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-f");

            commandArray.Add("-a");                 // autostart

            if (!startBios)
            {
                commandArray.Add("-i");
                commandArray.Add("\"" + rom + "\"");
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private void SetupConfig(string path, string system, string exe, string rom, ScreenResolution resolution, bool fullscreen = true)
        {
            string iniFile = Path.Combine(path, "yabause.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues))
                {
                    // Inject path loop
                    Dictionary<string, string> userPath = new Dictionary<string, string>
                    {
                        { "General\\SaveStates", (Path.Combine(AppConfig.GetFullPath("saves"), system, "yabasanshiro", "sstates")) },
                    };
                    
                    foreach (KeyValuePair<string, string> pair in userPath)
                    {
                        if (!Directory.Exists(pair.Value)) try { Directory.CreateDirectory(pair.Value); }
                            catch { }
                        if (!string.IsNullOrEmpty(pair.Value) && Directory.Exists(pair.Value))
                            ini.WriteValue("0.9.11", pair.Key, pair.Value.Replace("\\", "/"));
                    }

                    string bkram = Path.Combine(AppConfig.GetFullPath("saves"), system, "yabasanshiro", "bkram.bin");

                    ini.WriteValue("0.9.11", "Memory\\Path", bkram.Replace("\\", "/"));

                    string bios = Path.Combine(AppConfig.GetFullPath("bios"), "saturn_bios.bin");
                    if (File.Exists(bios))
                        ini.WriteValue("0.9.11", "General\\Bios", bios.Replace("\\", "/"));

                    // disable fullscreen if windowed mode
                    if (!fullscreen)
                        ini.WriteValue("0.9.11", "Video\\Fullscreen", "false");

                    // Features
                    ini.WriteValue("0.9.11", "General\\CdRom", "1");
                    ini.WriteValue("0.9.11", "General\\CdRomISO", null);
                    ini.WriteValue("0.9.11", "General\\UseSh2Cache", "true");
                    ini.WriteValue("0.9.11", "Advanced\\SH2Interpreter", "3");
                    ini.WriteValue("0.9.11", "Cartridge\\Type", "6");
                    BindIniFeature(ini, "0.9.11", "Video\\VideoCore", "yaba_videocore", "1");
                    BindBoolIniFeature(ini, "0.9.11", "General\\EnableEmulatedBios", "yabasanshiro_force_hle_bios", "true", "false");
                    BindBoolIniFeature(ini, "0.9.11", "General\\ShowFPS", "yaba_fps", "true", "false");
                    BindBoolIniFeature(ini, "0.9.11", "General\\EnableFrameSkipLimiter", "yaba_frameskip", "true", "false");
                    BindIniFeature(ini, "0.9.11", "Video\\AspectRatio", "yaba_ratio", "0");
                    BindIniFeature(ini, "0.9.11", "Video\\resolution_mode", "yaba_resolution", "0");
                    BindIniFeature(ini, "0.9.11", "Video\\filter_type", "yaba_filtering", "0");
                    BindIniFeature(ini, "0.9.11", "Sound\\SoundCore", "yaba_audiocore", "1");

                    CreateControllerConfiguration(ini);
                    //ConfigureGun(path, ini);

                    ini.Save();
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
                return 0;

            return ret;
        }

        /*private void ConfigureGun(string path, IniFile ini)
        {
            if (!SystemConfig.isOptSet("use_guns") || string.IsNullOrEmpty(SystemConfig["use_guns"]) || !SystemConfig.getOptBoolean("use_guns"))
                return;

            string gunport = "2";
            if (SystemConfig.isOptSet("yaba_gunport") && !string.IsNullOrEmpty(SystemConfig["yaba_gunport"]))
                gunport = SystemConfig["yaba_gunport"];

            bool gunInvert = SystemConfig.getOptBoolean("gun_invert");

            ini.WriteValue("0.9.11", "Input\\Port\\" + gunport + "\\Id\\1\\Type", "37");
            ini.WriteValue("0.9.11", "Input\\Port\\" + gunport + "\\Id\\1\\Controller\\37\\Key\\25", gunInvert ? "2147483650" : "2147483649");
            ini.WriteValue("0.9.11", "Input\\Port\\" + gunport + "\\Id\\1\\Controller\\37\\Key\\27", gunInvert ? "2147483649" : "2147483650");
            ini.WriteValue("0.9.11", "Input\\GunMouseSensitivity", "100");
        }*/
    }
}
