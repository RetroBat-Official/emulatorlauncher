using EmulatorLauncher.Common;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
        private string _ipsOriginalFile = null;
        private string _ipsIniFile = null;
        private bool _restoreIpsFile = false;
        private bool _pad2Keyoverride = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("fbneo");

            string exe = Path.Combine(path, "fbneo64.exe");
            if (!File.Exists(exe))
                return null;

            _romName = Path.GetFileNameWithoutExtension(rom);
            bool fullscreen = ShouldRunFullscreen();

            // Applying bezels : ReShade only hooks DirectX9 (3, 4) and Direct3D 11 (5) renderers
            string renderer = SystemConfig.isOptSet("fbneo_renderer") && !string.IsNullOrEmpty(SystemConfig["fbneo_renderer"]) ? SystemConfig["fbneo_renderer"] : "3";
            if (fullscreen)
            {
                bool reshade = false;

                if (renderer == "3" || renderer == "4")
                    reshade = ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x64, system, rom, path, resolution, emulator);
                else if (renderer == "5")
                    reshade = ReshadeManager.Setup(ReshadeBezelType.dxgi, ReshadePlatform.x64, system, rom, path, resolution, emulator);

                if (!reshade)
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            _resolution = resolution;

            // Input plugin must be known before writing the main config
            _useXInput = UseXInputPlugin();

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

            if (SystemConfig.getOptBoolean("fbneo_ips"))
            {
                commandArray.Add("-ips");

                if (SystemConfig.isOptSet("fbneo_patch") && !string.IsNullOrEmpty(SystemConfig["fbneo_patch"]))
                {
                    string ipsFile = SystemConfig["fbneo_patch"];
                    if (File.Exists(ipsFile))
                    {
                        // FBNeo only strips double quotes from the -ips argument
                        string ipsFileName = Path.GetFileName(ipsFile);
                        commandArray.Add("\"" + ipsFileName + "\"");
                    }

                    _ipsIniFile = Path.Combine(path, "config", "ips", _romName + ".ini");

                    if (File.Exists(_ipsIniFile))
                    {
                        _ipsOriginalFile = File.ReadAllText(_ipsIniFile);
                        _restoreIpsFile = true;
                    }
                }
            }

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
                cfg["bSkipStartupCheck"] = "1";

                // Write paths
                cfg["szAppRomPaths[0]"] = Path.GetDirectoryName(rom) + "\\";
                cfg["szAppRomPaths[1]"] = Path.Combine(AppConfig.GetFullPath("bios")) + "\\";

                string fbneoBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), "fbneo");
                if (!Directory.Exists(fbneoBiosPath)) try { Directory.CreateDirectory(fbneoBiosPath); }
                    catch { }
                cfg["szAppRomPaths[2]"] = fbneoBiosPath + "\\";

                string cheatsPath = Path.Combine(fbneoBiosPath, "cheats");
                if (!Directory.Exists(cheatsPath)) try { Directory.CreateDirectory(cheatsPath); }
                    catch { }
                cfg["szAppCheatsPath"] = cheatsPath + "\\";

                string samplePath = Path.Combine(AppConfig.GetFullPath("bios"), "fbneo", "samples");
                if (!Directory.Exists(samplePath)) try { Directory.CreateDirectory(samplePath); }
                    catch { }
                cfg["szAppSamplesPath"] = samplePath + "\\";

                string eepromPath = Path.Combine(AppConfig.GetFullPath("saves"), "fbneo", "fbneo");
                if (!Directory.Exists(eepromPath)) try { Directory.CreateDirectory(eepromPath); }
                    catch { }
                cfg["szAppEEPROMPath"] = eepromPath + "\\";
                cfg["szAppHiscorePath"] = eepromPath + "\\";

                string blendPath = Path.Combine(eepromPath, "blend");
                if (!Directory.Exists(blendPath)) try { Directory.CreateDirectory(blendPath); }
                    catch { }
                cfg["szAppBlendPath"] = blendPath + "\\";

                // Video driver
                if (SystemConfig.isOptSet("fbneo_renderer") && !string.IsNullOrEmpty(SystemConfig["fbneo_renderer"]))
                    cfg["nVidSelect"] = SystemConfig["fbneo_renderer"];
                else
                    cfg["nVidSelect"] = "3";

                // Monitor indexes : FBNeo expects a Windows adapter name, not an index.
                if (Program.IsMonitorIndexSet)
                    cfg["HorScreen"] = Program.TargetScreen.DeviceName;

                if (SystemConfig.isOptSet("VerticalMonitorIndex") && !string.IsNullOrEmpty(SystemConfig["VerticalMonitorIndex"]))
                {
                    var vScreen = Displays.FromIndex(SystemConfig["VerticalMonitorIndex"].ToInteger(), MonitorOrder.SdlLike);
                    cfg["VerScreen"] = (vScreen ?? Program.TargetScreen).DeviceName;
                }
                else if (Program.IsMonitorIndexSet)
                    cfg["VerScreen"] = Program.TargetScreen.DeviceName;

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

                cfg["bSaveInputs"] = "1";

                // Input plugin must match the codes written by the controller configuration (0 = DirectInput, 1 = XInput + DirectInput)
                if (!SystemConfig.getOptBoolean("disableautocontrollers"))
                    cfg["nInputSelect"] = _useXInput ? "1" : "0";

                BindBoolFeature(cfg, "bForce60Hz", "fbneo_force60hz", "1", "0");
                BindBoolFeature(cfg, "bVidIntegerScale", "integerscale", "1", "0");
                BindBoolFeature(cfg, "bRunAhead", "fbneo_runahead", "1", "0");
                BindBoolFeatureOn(cfg, "EnableHiscores", "fbneo_hiscore", "1", "0");

                SetupHotkeys(cfg);

                cfg.Save();
            }
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));

            return mapping;
        }

        // RetroBat keyboard hotkeys, FBNeo format 0xMMVV (MM = modifiers, VV = Windows virtual-key code)
        private void SetupHotkeys(FbneoConfigFile cfg)
        {
            var hotkeys = new Dictionary<string, string>()
            {
                { "ToggleMenu", "0x0070" },          // F1
                { "FullScreen", "0x0046" },          // F
                { "FastForward", "0x004C" },         // L
                { "Rewind", "0x0008" },              // Backspace
                { "FastForwardToggle", "0x0020" },   // Space
                { "SaveSnapshot", "0x0077" },        // F8
                { "StateSaveSlot", "0x0071" },       // F2
                { "StateLoadSlot", "0x0073" },       // F4
                { "StatePrevSlot", "0x0075" },       // F6
                { "StateNextSlot", "0x0076" },       // F7
                { "Pause", "0x0050" },               // P
                { "ExitFullScreen", "0x001B" }       // Escape
            };

            // Overrides from kb_hotkeys.yml (also generates the matching padToKey.xml)
            if (Hotkeys.GetHotKeysFromFile("fbneo", "", out Dictionary<string, HotkeyResult> customHotkeys))
            {
                foreach (var h in customHotkeys)
                    hotkeys[h.Value.EmulatorKey] = h.Value.EmulatorValue;

                _pad2Keyoverride = true;
            }

            // Free FBNeo default keys now used by RetroBat hotkeys
            foreach (var def in fbneoDefaultHotkeys)
            {
                if (!hotkeys.ContainsKey(def.Key) && hotkeys.Values.Contains(def.Value))
                    hotkeys[def.Key] = "0x0000";
            }

            foreach (var hk in hotkeys)
                cfg["Hotkey " + hk.Key] = hk.Value + " 0x0000";
        }

        // FBNeo default hotkeys without modifier (hotkeys.cpp)
        private static readonly Dictionary<string, string> fbneoDefaultHotkeys = new Dictionary<string, string>()
        {
            { "LoadGame", "0x0075" },        // F6
            { "Pause", "0x0013" },           // Pause
            { "FastForward", "0x0070" },     // F1
            { "Rewind", "0x0021" },          // Page up
            { "MapInputs", "0x0074" },       // F5
            { "StateLoadSlot", "0x0078" },   // F9
            { "StateSaveSlot", "0x0079" },   // F10
            { "StatePrevSlot", "0x0077" },   // F8
            { "StateNextSlot", "0x007A" },   // F11
            { "MemcardToggle", "0x0076" },   // F7
            { "ExitFullScreen", "0x001B" },  // Escape
            { "ToggleFPS", "0x0008" },       // Backspace
            { "SaveSnapshot", "0x007B" }     // F12
        };

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.d3d9, path.WorkingDirectory);
            ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, path.WorkingDirectory);

            if (ret == 1)
                return 0;

            return ret;
        }

        public override void Cleanup()
        {
            if (_restoreIpsFile)
            {
                try { File.WriteAllText(_ipsIniFile, _ipsOriginalFile); }
                catch { }
            }

            base.Cleanup();
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