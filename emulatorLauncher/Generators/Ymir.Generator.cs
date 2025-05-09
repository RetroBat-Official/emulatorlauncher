using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class YmirGenerator : Generator
    {
        public YmirGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("ymir");

            string exe = Path.Combine(path, "ymir-sdl3.exe");

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

            commandArray.Add("-d");
            commandArray.Add("\"" + rom + "\"");

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
            string iniFile = Path.Combine(path, "Ymir.toml");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    string backupRamPath = Path.Combine(AppConfig.GetFullPath("saves"), "saturn", "ymir", "state", "bup-int.bin");
                    ini.WriteValue("System", "InternalBackupRAMImagePath", "'" + backupRamPath + "'");
                    ini.WriteValue("System.IPL", "Override", "true");

                    string saturnBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), "saturn_bios.bin");

                    if (SystemConfig.isOptSet("ymir_force_bios") && !string.IsNullOrEmpty(SystemConfig["ymir_force_bios"]))
                    {
                        saturnBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), SystemConfig["ymir_force_bios"]);
                    }

                    ini.WriteValue("System.IPL", "Path", "'" + saturnBiosPath + "'");

                    //CreateControllerConfiguration(ini);

                    // Video
                    if (SystemConfig.isOptSet("ymir_ratio") && !string.IsNullOrEmpty(SystemConfig["ymir_ratio"]))
                    {
                        string ratio = SystemConfig["ymir_video"];

                        switch (ratio)
                        {
                            case "default":
                                ini.WriteValue("Video", "ForceAspectRatio", "false");
                                ini.WriteValue("Video", "ForcedAspect", "1.3333333333333333");
                                break;
                            case "43":
                                ini.WriteValue("Video", "ForceAspectRatio", "true");
                                ini.WriteValue("Video", "ForcedAspect", "1.3333333333333333");
                                break;
                            case "169":
                                ini.WriteValue("Video", "ForceAspectRatio", "true");
                                ini.WriteValue("Video", "ForcedAspect", "1.7777777777777777");
                                break;
                        }
                    }
                    else
                    {
                        ini.WriteValue("Video", "ForceAspectRatio", "false");
                        ini.WriteValue("Video", "ForcedAspect", "1.3333333333333333");
                    }

                    BindBoolIniFeature(ini, "Video", "ForceIntegerScaling", "integerscale", "true", "false");
                    
                    if (SystemConfig.isOptSet("ymir_videoformat") && !string.IsNullOrEmpty(SystemConfig["ymir_videoformat"]))
                        ini.WriteValue("System", "VideoStandard", "'" + SystemConfig["ymir_videoformat"] + "'");
                    else
                        ini.WriteValue("System", "VideoStandard", "'NTSC'");

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
    }
}
