using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System.Collections.Generic;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class Project64Generator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("project64");

            string exe = Path.Combine(path, "Project64.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            ConfigureProject64(system, path, rom, fullscreen);

            var commandArray = new List<string>();
            
            if (system == "n64dd" && Path.GetExtension(rom).ToLowerInvariant() != ".ndd")
            {
                string n64ddrom = rom + ".ndd";
                if (File.Exists(n64ddrom))
                {
                    commandArray.Add("--combo");
                    commandArray.Add("\"" + n64ddrom + "\"");
                }
                commandArray.Add("\"" + rom + "\"");
            }

            else if (system == "n64dd" && Path.GetExtension(rom).ToLowerInvariant() == ".ndd")
            {
                string romPath = Path.GetDirectoryName(rom);
                string n64rom = Path.Combine(romPath, Path.GetFileNameWithoutExtension(rom));
                if (File.Exists(n64rom))
                {
                    commandArray.Add("--combo");
                    commandArray.Add("\"" + rom + "\"");
                    commandArray.Add("\"" + n64rom + "\"");
                }
                else
                    commandArray.Add("\"" + rom + "\"");
            }

            else
                commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void ConfigureProject64(string system, string path, string rom, bool fullscreen)
        {
            string conf = Path.Combine(path, "Config", "Project64.cfg");

            using (var ini = IniFile.FromFile(conf))
            {
                ini.WriteValue("Settings", "Auto Full Screen", fullscreen ? "1" : "0");
                BindBoolIniFeature(ini, "Settings", "Enable Discord RPC", "discord", "1", "0");

                // N64DD bios paths
                string IPLJap = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "IPL_JAP.n64");
                if (File.Exists(IPLJap))
                    ini.WriteValue("Settings", "Disk IPL ROM Path", IPLJap);

                string IPLUSA = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "IPL_USA.n64");
                if (File.Exists(IPLUSA))
                    ini.WriteValue("Settings", "Disk IPL USA ROM Path", IPLUSA);

                string IPLDev = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "IPL_DEV.n64");
                if (File.Exists(IPLDev))
                    ini.WriteValue("Settings", "Disk IPL TOOL ROM Path", IPLDev);

                ini.WriteValue("Game Directory", "Directory", Path.GetDirectoryName(rom));
                ini.WriteValue("Game Directory", "Use Selected", "1");

                string screenshotsDir = Path.Combine(AppConfig.GetFullPath("screenshots"), "project64");
                if (!Directory.Exists(screenshotsDir))
                    try { Directory.CreateDirectory(screenshotsDir); } catch { }
                ini.WriteValue("Snap Shot Directory", "Directory", screenshotsDir + "\\");
                ini.WriteValue("Snap Shot Directory", "Use Selected", "1");

                string saveDir = Path.Combine(AppConfig.GetFullPath("saves"), system, "project64");
                if (!Directory.Exists(saveDir))
                    try { Directory.CreateDirectory(saveDir); } catch { }
                ini.WriteValue("Native Save Directory", "Directory", saveDir + "\\");
                ini.WriteValue("Native Save Directory", "Use Selected", "1");

                string stateDir = Path.Combine(AppConfig.GetFullPath("saves"), system, "project64", "sstates");
                if (!Directory.Exists(stateDir))
                    try { Directory.CreateDirectory(stateDir); } catch { }
                ini.WriteValue("Instant Save Directory", "Directory", stateDir + "\\");
                ini.WriteValue("Instant Save Directory", "Use Selected", "1");

                ConfigureControllers(ini);
            }
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
                return 0;
            }

            return ret;
        }
    }
}
