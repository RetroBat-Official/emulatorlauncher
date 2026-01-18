using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Project64Generator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _pad2Keyoverride = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("project64");

            string exe = Path.Combine(path, "Project64.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (fullscreen)
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            _resolution = resolution;

            ConfigureProject64(system, path, rom, fullscreen);
            ConfigureHotkeys(path);

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

        private void ConfigureHotkeys(string path)
        {
            string cfgFile = Path.Combine(path, "Config", "Project64.sc3");

            if (!File.Exists(cfgFile))
                try { File.WriteAllText(cfgFile, ""); } catch { }

            var lines = File.ReadAllLines(cfgFile);

            var actionIdsToReplace = new HashSet<int>
            {
                4007,4153,4154,4157,4159,4164,4165,4166,4167,4168,4169,
                4170,4171,4172,4173,4174,4175,4186,4187
            };

            string[] newLines =
            {
                "4007,27,0,0,0,6,1,0",      // ESC
                "4153,80,0,0,0,6,1,0",      // P
                "4154,119,0,0,0,6,1,0",     // F8
                "4157,115,0,0,0,6,1,0",     // F4
                "4159,113,0,0,0,6,1,0",     // F2
                "4175,70,0,0,0,6,1,0",      // F
                "4186,76,0,0,0,6,1,0",      // L
                "4187,8,0,0,0,6,1,0"        // Backspace
            };

            if (Hotkeys.GetHotKeysFromFile("project64", "", out Dictionary<string, HotkeyResult> hotkeys))
            {
                List<string> newLinesList = new List<string>();
                if (hotkeys.Count > 0)
                {
                    foreach (var h in hotkeys)
                    {
                        string lineToAdd = h.Value.EmulatorKey + "," + h.Value.EmulatorValue + ",0,0,0,6,1,0";
                        newLinesList.Add(lineToAdd);
                    }
                    newLines = newLinesList.ToArray();
                    _pad2Keyoverride = true;
                }
            }

            // Delete used hotkeys lines
            var newActionIds = new HashSet<int>(newLines.Select(l => int.Parse(l.Split(',')[0])));
            var newKeyCodes = new HashSet<int>(newLines.Select(l => int.Parse(l.Split(',')[1])));

            var filtered = lines.Where(line =>
            {
                var parts = line.Split(',');
                if (parts.Length < 2)
                    return true;

                if (!int.TryParse(parts[0], out int actionId))
                    return true;
                if (!int.TryParse(parts[1], out int keyCode))
                    return true;

                // Delete lines with already used actions
                if (newActionIds.Contains(actionId))
                    return false;

                // Delete lines with already used keys
                if (newKeyCodes.Contains(keyCode))
                    return false;

                return true;
            }).ToList();

            filtered.AddRange(newLines);
            var sorted = filtered.OrderBy(line =>
            {
                int comma = line.IndexOf(',');
                if (comma <= 0) return int.MaxValue;
                return int.TryParse(line.Substring(0, comma), out int id) ? id : int.MaxValue;
            }).ToArray();

            File.WriteAllLines(cfgFile, sorted);
        }

        private string BuildHotkeyLine(int actionId, int keyCode, bool ctrl, bool shift, bool alt)
        {
            return string.Join(",",
                actionId,
                keyCode,
                ctrl ? 1 : 0,
                shift ? 1 : 0,
                alt ? 1 : 0,
                6,  // Device = Keyboard
                1,  // Enabled
                0   // Always 0
            );
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
            }

            return mapping;
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
