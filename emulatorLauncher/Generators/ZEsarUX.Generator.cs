using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    partial class ZEsarUXGenerator : Generator
    {
        public ZEsarUXGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _destRom;
        private readonly List<string> extensions = new List<string>() { ".nex", ".pzx", ".rzx", ".sna", ".snx", ".sp", ".spg", ".tap", ".tzx", ".z80", ".zsf", ".zx", };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("zesarux");

            string exe = Path.Combine(path, "zesarux.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                var entries = Zip.ListEntries(rom).Where(e => !e.IsDirectory).Select(e => e.Filename).ToArray();

                bool stop = false;
                string romFile = null;

                foreach(var ext in extensions)
                {
                    if (stop)
                        break;

                    romFile = entries.Where(e => Path.GetExtension(e).ToLowerInvariant() == ext).FirstOrDefault();
                    if (!string.IsNullOrEmpty(romFile))
                        stop = true;
                }

                _destRom = Path.Combine(Path.GetTempPath(), Path.GetFileName(romFile));

                Zip.Extract(rom, Path.GetTempPath(), romFile);
                if (!File.Exists(_destRom))
                    return null;

                rom = _destRom;
            }

            // Configure cfg file
            string configFile = Path.Combine(path, ".zesaruxrc");
            SetupConfig(configFile, system);

            // Command line arguments
            List<string> commandArray = new List<string>();
            
            commandArray.Add("--configfile");
            commandArray.Add("\"" + configFile + "\"");
            commandArray.Add("--saveconf-on-exit");

            if (fullscreen)
                commandArray.Add("--fullscreen");

            commandArray.Add("--disable-xanniversary-logo");
            commandArray.Add("--nosplash");
            commandArray.Add("--stats-disable-check-updates");
            commandArray.Add("--disable-all-first-aid");
            commandArray.Add("--quickexit");
            commandArray.Add("--disabletooltips");
            commandArray.Add("--forceconfirmyes");
            commandArray.Add("--stats-disable-check-yesterday-users");
            commandArray.Add("--no-show-changelog");

            if (!SystemConfig.isOptSet("zesarux_showfps") || !SystemConfig.getOptBoolean("zesarux_showfps"))
                commandArray.Add("--no-fps");

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfig(string confgifile, string system)
        {
            var cfg = ZEsarUXConfigFile.FromFile(confgifile);

            if (!File.Exists(confgifile))
                try { File.Create(confgifile).Close(); } catch { }

            // Actions
            Action<string, string, string> BindZesarUXFeature = (featureName, settingName, defaultValue) =>
            {
                if (SystemConfig.isOptSet(featureName) && !string.IsNullOrEmpty(SystemConfig[featureName]))
                    cfg[settingName] = SystemConfig[featureName];
                else
                    cfg[settingName] = defaultValue;
            };

            Action<string, string, string, string> BindZesarUXBoolFeature = (featureName, settingName, trueValue, falseValue) =>
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else
                    cfg[settingName] = falseValue;
            };

            // General Settings
            BindZesarUXFeature("zesarux_machine", "--machine", "128k");

            if (SystemConfig.isOptSet("zesarux_joytype") && !string.IsNullOrEmpty(SystemConfig["zesarux_joytype"]))
                cfg["--joystickemulated"] = "\"" + SystemConfig["zesarux_joytype"] + "\"";
            else
                cfg["--joystickemulated"] = "Kempston";

            if (system != "zxspectrum")
                cfg.Remove("--realvideo");

            // controllers
            CreateControllerConfiguration(cfg);

            cfg.Save();
        }

        public override void Cleanup()
        {
            base.Cleanup();

            try
            {
                if (!string.IsNullOrEmpty(_destRom) && File.Exists(_destRom))
                    File.Delete(_destRom);
            }
            catch { }
        }
    }
    
    class ZEsarUXConfigFile
    {
        private string _fileName;
        private List<string> _lines;

        public static ZEsarUXConfigFile FromFile(string file)
        {
            var ret = new ZEsarUXConfigFile();
            ret._fileName = file;

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

        public void Remove(string key)
        {
            int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
            if (idx >= 0)
            {
                _lines.RemoveAt(idx);
                File.WriteAllLines(_fileName, _lines);
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