using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class JynxGenerator : Generator
    {
        public JynxGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _fullscreen;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("jynx");

            string exe = Path.Combine(path, "Jynx-Windows-64bit.exe");
            if (!File.Exists(exe))
                return null;

            string[] extensions = new string[] { ".bin", ".tap" };

            if (Path.GetExtension(rom).ToLower() == ".zip" || Path.GetExtension(rom).ToLower() == ".7z" || Path.GetExtension(rom).ToLower() == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories);
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            // Check BIOS files based on machine type
            string machineType = "0";
            if (SystemConfig.isOptSet("jynx_machine") && !string.IsNullOrEmpty(SystemConfig["jynx_machine"]))
                machineType = SystemConfig["jynx_machine"];

            if (machineType == "0")
            {
                string bios1 = Path.Combine(path, "lynx48-1.rom");
                string bios2 = Path.Combine(path, "lynx48-2.rom");
                if (!File.Exists(bios1) || !File.Exists(bios2))
                    throw new ApplicationException("Machine Lynx48k has missing BIOS file(s) in 'emulators\\jynx' folder.");
            }
            else
            {
                string bios1 = Path.Combine(path, "lynx96-1.rom");
                string bios2 = Path.Combine(path, "lynx96-2.rom");
                if (!File.Exists(bios1) || !File.Exists(bios2) || !File.Exists(bios2))
                    throw new ApplicationException("Machine Lynx96k has missing BIOS file(s) in 'emulators\\jynx' folder.");

                if (machineType == "3")
                {
                    string scorpionBios = Path.Combine(path, "lynx96-3-scorpion.rom");
                    if (!File.Exists(scorpionBios))
                        throw new ApplicationException("Machine Lynx96k - scorpion has missing BIOS file in 'emulators\\jynx' folder.");
                }
            }

            _fullscreen = (!IsEmulationStationWindowed() && !SystemConfig.getOptBoolean("jynx_fullscreen")) || SystemConfig.getOptBoolean("forcefullscreen");

            var commandArray = new List<string>
            {
                "--settings",
                "\".\\settings.txt\"",
                "--run",
                "\"" + rom + "\"",
                "--games"
            };

            string args = string.Join(" ", commandArray);

            SetupConfig(path, machineType);

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            _resolution = resolution;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfig(string path, string machineType)
        {
            string cfgFile = Path.Combine(path, "settings.txt");
            
            if (!File.Exists(cfgFile))
            {
                using (StreamWriter sw = File.CreateText(cfgFile))
                {
                    sw.WriteLine("FileVersion 4");
                    sw.WriteLine("MachineType 0");
                    sw.WriteLine("RenderStyle 1");
                    sw.WriteLine("SoundEnable 1");
                    sw.WriteLine("FullScreenEnable 1");
                    sw.WriteLine("CyclesPerTimeslice 70000");
                    sw.WriteLine("TapeSounds 0");
                    sw.WriteLine("RemExtensions 0");
                    sw.WriteLine("MaxSpeedCassette 1");
                    sw.WriteLine("MaxSpeedConsole 0");
                    sw.WriteLine("MaxSpeedAlways 0");
                    sw.WriteLine("ColourSet 0");
                }
            }

            var cfg = JynxConfigFile.FromFile(cfgFile);
            if (!_fullscreen)
                cfg["FullScreenEnable"] = "0";
            else
                cfg["FullScreenEnable"] = "1";

            cfg["MachineType"] = machineType;
            cfg["RenderStyle"] = "1";

            if (SystemConfig.isOptSet("jynx_colourset") && !string.IsNullOrEmpty(SystemConfig["jynx_colourset"]))
                cfg["ColourSet"] = SystemConfig["jynx_colourset"];
            else
                cfg["ColourSet"] = "0";

            cfg.Save();
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            return ret;
        }

        class JynxConfigFile
        {
            private string _fileName;
            private List<string> _lines;

            public static JynxConfigFile FromFile(string file)
            {
                var ret = new JynxConfigFile
                {
                    _fileName = file
                };

                try
                {
                    if (File.Exists(file))
                        ret._lines = File.ReadAllLines(file).ToList();
                }
                catch { }

                if (ret._lines == null)
                    ret._lines = new List<string>();

                return ret;
            }

            public string this[string key]
            {
                get
                {
                    int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
                    if (idx >= 0)
                    {
                        int split = _lines[idx].IndexOf(" ");
                        if (split >= 0)
                            return _lines[idx].Substring(split + 1).Trim();
                    }

                    return string.Empty;
                }
                set
                {
                    if (this[key] == value)
                        return;

                    int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
                    if (idx >= 0)
                    {
                        _lines.RemoveAt(idx);

                        if (!string.IsNullOrEmpty(value))
                            _lines.Insert(idx, key + " " + value);
                    }
                    else if (!string.IsNullOrEmpty(value))
                    {
                        _lines.Add(key + " " + value);
                        _lines.Add("");
                    }

                    IsDirty = true;
                }
            }

            public bool IsDirty { get; private set; }

            public void Save()
            {
                if (!IsDirty)
                    return;

                File.WriteAllLines(_fileName, _lines);
                IsDirty = false;
            }
        }
    }
}
