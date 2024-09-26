using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class FbneoGenerator : Generator
    {
        public FbneoGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private string _romName;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("fbneo");

            string exe = Path.Combine(path, "fbneo64.exe");
            if (!File.Exists(exe))
                return null;

            _romName = Path.GetFileNameWithoutExtension(rom);
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //Applying bezels
            if (fullscreen)
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            _resolution = resolution;

            // Configure cfg file
            SetupConfig(path, rom);
            CreateControllerConfiguration(path, rom, system);

            // Command line arguments
            List<string> commandArray = new List<string>
            {
                _romName
            };

            if (!fullscreen)
                commandArray.Add("-w");

            string args = string.Join(" ", commandArray);
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfig(string path, string rom)
        {          
            string configFolder = Path.Combine(path, "config");
            if (!Directory.Exists(configFolder)) try { Directory.CreateDirectory(configFolder); }
                catch { }

            string iniFile = Path.Combine(configFolder, "fbneo64.ini");
            if (!File.Exists(iniFile))
            {
                string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "fbneo", "config", "fbneo64.ini");

                if (File.Exists(templateFile))
                    try { File.Copy(templateFile, iniFile); } catch { }
            }

            if (File.Exists(iniFile))
            {
                var cfg = FbneoConfigFile.FromFile(iniFile);

                // Write paths
                cfg["szAppRomPaths[0]"] = Path.GetDirectoryName(rom) + "\\";
                cfg["szAppRomPaths[1]"] = Path.Combine(AppConfig.GetFullPath("bios")) + "\\";

                string fbneoBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), "fbneo");
                if (!Directory.Exists(fbneoBiosPath)) try { Directory.CreateDirectory(fbneoBiosPath); }
                    catch { }
                cfg["szAppRomPaths[2]"] = fbneoBiosPath + "\\";
                cfg["szAppHiscorePath"] = fbneoBiosPath + "\\";

                string cheatsPath = Path.Combine(fbneoBiosPath, "cheats");
                if (!Directory.Exists(cheatsPath)) try { Directory.CreateDirectory(cheatsPath); }
                    catch { }
                cfg["szAppCheatsPath"] = cheatsPath + "\\";

                string samplePath = Path.Combine(AppConfig.GetFullPath("bios"), "fbneo", "samples");
                if (!Directory.Exists(samplePath)) try { Directory.CreateDirectory(samplePath); }
                    catch { }
                cfg["szAppSamplesPath"] = samplePath + "\\";

                string eepromPath = Path.Combine(AppConfig.GetFullPath("saves"), "fbneo");
                if (!Directory.Exists(eepromPath)) try { Directory.CreateDirectory(eepromPath); }
                    catch { }
                cfg["szAppEEPROMPath"] = eepromPath + "\\";

                string blendPath = Path.Combine(eepromPath, "blend");
                if (!Directory.Exists(blendPath)) try { Directory.CreateDirectory(blendPath); }
                    catch { }
                cfg["szAppBlendPath"] = blendPath + "\\";

                // Video driver
                if (SystemConfig.isOptSet("fbneo_renderer") && !string.IsNullOrEmpty(SystemConfig["fbneo_renderer"]))
                    cfg["nVidSelect"] = SystemConfig["fbneo_renderer"];
                else
                    cfg["nVidSelect"] = "3";

                // Monitor indexes
                if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                {
                    string emuMonitor = "\\\\" + ".\\" + "DISPLAY" + SystemConfig["MonitorIndex"];
                    cfg["HorScreen"] = emuMonitor;
                }
                else
                    cfg["HorScreen"] = "\\\\" + ".\\" + "DISPLAY1";

                if (SystemConfig.isOptSet("VerticalMonitorIndex") && !string.IsNullOrEmpty(SystemConfig["VerticalMonitorIndex"]))
                {
                    string emuMonitorV = "\\\\" + ".\\" + "DISPLAY" + SystemConfig["VerticalMonitorIndex"];
                    cfg["VerScreen"] = emuMonitorV;
                }
                else if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                {
                    string emuMonitorV = "\\\\" + ".\\" + "DISPLAY" + SystemConfig["MonitorIndex"];
                    cfg["VerScreen"] = emuMonitorV;
                }
                else
                    cfg["VerScreen"] = "\\\\" + ".\\" + "DISPLAY1";

                // Scanlines
                if (SystemConfig.isOptSet("fbneo_scanlines") && SystemConfig["fbneo_scanlines"] != "0")
                {
                    cfg["bVidScanlines"] = "1";
                    if (SystemConfig["fbneo_scanlines"] == "max")
                        cfg["nVidScanIntensity"] = "0";
                    else
                        cfg["nVidScanIntensity"] = SystemConfig["fbneo_scanlines"];
                }
                else
                    cfg["bVidScanlines"] = "0";

                // Audio driver
                if (SystemConfig.isOptSet("fbneo_audiodriver") && !string.IsNullOrEmpty(SystemConfig["fbneo_audiodriver"]))
                    cfg["nAudSelect"] = SystemConfig["fbneo_audiodriver"];
                else
                    cfg["nAudSelect"] = "0";

                BindBoolFeature(cfg, "bForce60Hz", "fbneo_force60hz", "1", "0");
                BindBoolFeature(cfg, "bRunAhead", "fbneo_runahead", "1", "0");

                cfg.Save();
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
                ReshadeManager.UninstallReshader(ReshadeBezelType.d3d9, path.WorkingDirectory);
                return 0;
            }
            ReshadeManager.UninstallReshader(ReshadeBezelType.d3d9, path.WorkingDirectory);
            return ret;
        }
    }
    
    class FbneoConfigFile
    {
        private string _fileName;
        private List<string> _lines;

        public static FbneoConfigFile FromFile(string file)
        {
            var ret = new FbneoConfigFile
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

                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != '/' && l.StartsWith(key + " "));
                if (idx >= 0)
                {
                    _lines.RemoveAt(idx);

                    if (!string.IsNullOrEmpty(value))
                        _lines.Insert(idx, key + " " + value);
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    _lines.Add(key + " " + value);
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