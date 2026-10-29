using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using EmulatorLauncher.VPinballLauncher;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    partial class VPinballGenerator : Generator
    {
        public VPinballGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private LoadingForm _splash;
        private Version _version;
        private string _processName;
        private string _exe;
        private string _gamePath;
        private bool _remapexit = false;
        private bool _exclusiveFullscreen = false;
        private ScreenResolution _rotationMode;                  // rotation without video mode, restored at the end of RunAndWait
        private ScreenResolution _appliedResolution;             // video mode applied in Generate (owned and disposed by Program)
        private ScreenResolution.LayoutSnapshot _displayLayout;  // screen positions before any change, restored in Cleanup
        private bool _displayChanged = false;
        private VPinballScreenLayout _layout;                    // 10.8+ only, null for older versions

        private const string DisabledResSuffix = ".retrobat";
        private string _tableResFile;
        private bool _tableResDisabled = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("vpinball");
            if (path == null)
                return null;

            string exe = Path.Combine(path, Environment.Is64BitOperatingSystem ? "VPinballX64.exe" : "VPinballX.exe");
            if (!File.Exists(exe) && Environment.Is64BitOperatingSystem)
                exe = Path.Combine(path, "VPinballX.exe");
            if (!File.Exists(exe))
                return null;

            _exe = exe;
            _processName = Path.GetFileNameWithoutExtension(exe);
            _version = new Version(10, 0, 0, 0);
            _gamePath = Path.GetDirectoryName(rom);

            // Get version from executable
            var versionInfo = FileVersionInfo.GetVersionInfo(exe);
            string versionString = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
            Version.TryParse(versionString, out _version);

            _exclusiveFullscreen = _version >= new Version(10, 8, 0, 0) && resolution != null && SystemConfig.getOptBoolean("vp_exclusivefullscreen");
            if (_exclusiveFullscreen)
                DependsOnDesktopResolution = false;

            rom = this.TryUnZipGameIfNeeded(system, rom, true, false);
            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.vpx").FirstOrDefault();
                if (string.IsNullOrEmpty(rom))
                    return null;
            }

            SetupTableResFile(rom);

            // 10.8+ : video mode and rotation are applied now, so that the screen layout is computed on the final geometry
            if (_version >= new Version(10, 8, 0, 0))
            {
                ApplyDisplayMode(resolution);
                _layout = VPinballScreenLayout.Build();
            }

            _splash = ShowSplash(rom);

            ScreenResolution.SetHighDpiAware(exe);
            EnsureDMDRegistered(path);
            EnsureBackglassServerRegistered(path);
            EnsureVPinMameRegistered(path);
            EnsurePinupPlayerRegistered(path);
            EnsurePinupDOFRegistered(path);
            EnsurePupServerRegistered(path);
            EnsurePupDMDControlRegistered(path);

            string romPath = Path.Combine(Path.GetDirectoryName(rom), "roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(Path.GetDirectoryName(rom), ".roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(AppConfig.GetFullPath("roms"), "vpinball", "roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(AppConfig.GetFullPath("roms"), "vpinball", ".roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(AppConfig.GetFullPath("vpinball"), "VPinMAME", "roms");
            if (!Directory.Exists(romPath))
            {
                romPath = Path.Combine(AppConfig.GetFullPath("roms"), "vpinball", "roms");
                try { Directory.CreateDirectory(romPath); } catch { SimpleLogger.Instance.Error("[ERROR] Missing roms subfolder in roms\vpinball folder."); }
            }

            SimpleLogger.Instance.Info("[INFO] using rompath: " + romPath);


            if (!SystemConfig.getOptBoolean("noscreenres"))
            {
                ScreenRes sr = ScreenRes.Load(Path.GetDirectoryName(rom));

                if (sr != null)
                {
                    if (_layout != null && !_layout.IsManual)
                        SetupScreenResFromLayout(sr);
                    else
                        SetupScreenResManual(sr, resolution);

                    if (SystemConfig.isOptSet("FlipLedDisplay") && SystemConfig.getOptBoolean("FlipLedDisplay"))
                        sr.DmdFlipY = 1;
                    else
                        sr.DmdFlipY = 0;

                    if (SystemConfig.isOptSet("Screen2posXStart") && !string.IsNullOrEmpty(SystemConfig["Screen2posXStart"]) && SystemConfig.isOptSet("Screen2posYStart") && !string.IsNullOrEmpty(SystemConfig["Screen2posYStart"]))
                    {
                        sr.Screen2posXStart = SystemConfig["Screen2posXStart"].ToInteger();
                        sr.Screen2posYStart = SystemConfig["Screen2posYStart"].ToInteger();
                    }

                    if (SystemConfig.isOptSet("Screen2ResXStart") && !string.IsNullOrEmpty(SystemConfig["Screen2ResXStart"]) && SystemConfig.isOptSet("Screen2ResYStart") && !string.IsNullOrEmpty(SystemConfig["Screen2ResYStart"]))
                    {
                        sr.Screen2ResXStart = SystemConfig["Screen2ResXStart"].ToInteger();
                        sr.Screen2ResYStart = SystemConfig["Screen2ResYStart"].ToInteger();
                    }

                    if (SystemConfig.isOptSet("FramePath") && !string.IsNullOrEmpty(SystemConfig["FramePath"]))
                        sr.FramePath = SystemConfig["FramePath"].Replace('/', '\\');
                    else
                        sr.FramePath = "#";

                    sr.Save();
                }
            }
            else
                SimpleLogger.Instance.Info("[Generator] noscreenres set - ScreenRes.txt left untouched.");

            SetupOptions(path, romPath, rom, resolution);
            SetupB2STableSettings(path);

            var commands = new List<string>();

            if (_version >= new Version(10, 7, 0, 0))
                commands.Add("-ExtMinimized");

            if (SystemConfig.isOptSet("vp_minimized") && SystemConfig.getOptBoolean("vp_minimized"))
                commands.Add("-minimized");

            if (_version >= new Version(10, 8, 0, 0))
            {
                commands.Add("-Ini");
                commands.Add(Path.Combine(path, "VPinballX.ini"));

            }
            commands.Add("-play");
            commands.Add(rom);

            string args = string.Join(" ", commands.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WindowStyle = _splash != null ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
                UseShellExecute = true
            };
        }

        private static LoadingForm ShowSplash(string rom)
        {
            if (rom == null)
                return null;

            string fn = Path.ChangeExtension(rom, ".directb2s");

            var data = DirectB2sData.FromFile(fn);
            if (data != null)
            {
                int last = Environment.TickCount;
                int index = 0;

                var frm = new LoadingForm
                {
                    Image = data.RenderBackglass(index)
                };
                frm.Timer += (a, b) =>
                {
                    int now = Environment.TickCount;
                    if (now - last > 1000)
                    {
                        index++;
                        if (index >= 4)
                            index = 0;

                        frm.Image = data.RenderBackglass(index);
                        frm.Invalidate();
                        last = now;
                    }

                };
                frm.Show();

                return frm;
            }

            return null;
        }

        private void SetupOptions(string path, string romPath, string vpxFile, ScreenResolution resolution)
        {
            if (_version >= new Version(10, 8, 0, 0))
                SetupOptionsIniFile(path, resolution);
            else
                SetupOptionsRegistry(resolution);

            SetupVPinMameOptions(path, romPath, vpxFile);
            SetupDmdDevice(path);
        }

        private void SetupOptionsIniFile(string path, ScreenResolution resolution)
        {
            string iniFile = Path.Combine(path, "VPinballX.ini");

            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                SimpleLogger.Instance.Info("[Generator] Writing config to VPinballX.ini file.");

                // Backglass : forced by ENABLE BACKGLASS, else follows the screen layout (screen setup not set : enabled as before)
                bool b2s;
                if (SystemConfig.isOptSet("enableb2s") && !string.IsNullOrEmpty(SystemConfig["enableb2s"]))
                    b2s = SystemConfig.getOptBoolean("enableb2s");
                else
                    b2s = _layout == null || _layout.IsManual || _layout.HasBackglass;

                ini.WriteValue("Controller", "ForceDisableB2S", b2s ? "0" : "1");

                BindIniFeature(ini, "Controller", "DOFContactors", "vpdof_contractors", "2");
                BindIniFeature(ini, "Controller", "DOFKnocker", "vpdof_knocker", "2");
                BindIniFeature(ini, "Controller", "DOFChimes", "vpdof_chimes", "2");
                BindIniFeature(ini, "Controller", "DOFBell", "vpdof_bell", "2");
                BindIniFeature(ini, "Controller", "DOFGear", "vpdof_gear", "2");
                BindIniFeature(ini, "Controller", "DOFShaker", "vpdof_shaker", "2");
                BindIniFeature(ini, "Controller", "DOFFlippers", "vpdof_flippers", "2");
                BindIniFeature(ini, "Controller", "DOFTargets", "vpdof_targets", "2");
                BindIniFeature(ini, "Controller", "DOFDropTargets", "vpdof_droptargets", "2");
                BindBoolIniFeature(ini, "Player", "PBWEnabled", "vp_analognudge", "1", "0");

                ini.WriteValue("Player", "DisableESC", "1");

                // Target screen : only when the user chose one (MonitorIndex is 0-based, SDL order)
                Screen vpScreen = Program.IsMonitorIndexSet ? Program.TargetScreen : null;

                // Playfield size : screen holding the video mode, else target screen, else primary (same rules as before).
                // Live geometry : the video mode and the rotation are already applied
                Screen playfieldScreen = resolution != null && !_exclusiveFullscreen ? Displays.FromDeviceName(resolution.DeviceName) : (vpScreen ?? Screen.PrimaryScreen);
                Rectangle playfieldBounds = VPinballScreenLayout.GetBounds(playfieldScreen);
                if (playfieldBounds.IsEmpty)
                    playfieldBounds = resolution == null ? (vpScreen ?? Screen.PrimaryScreen).Bounds : new Rectangle(0, 0, resolution.Width, resolution.Height);

                Size bounds = playfieldBounds.Size;

                if (_exclusiveFullscreen)
                {
                    // VPX switches the video mode itself : selected mode, oriented like the (possibly rotated) desktop
                    bounds = new Size(resolution.Width, resolution.Height);
                    if ((bounds.Height > bounds.Width) != (playfieldBounds.Height > playfieldBounds.Width))
                        bounds = new Size(bounds.Height, bounds.Width);
                }

                // Resolution and fullscreen
                ini.WriteValue("Player", "Width", bounds.Width.ToString());
                ini.WriteValue("Player", "Height", bounds.Height.ToString());

                if (_exclusiveFullscreen)
                {
                    // VPX switches to the selected video mode itself, desktop resolution is left untouched
                    ini.WriteValue("Player", "FullScreen", "1");
                    ini.WriteValue("Player", "RefreshRate", resolution.DisplayFrequency.ToString());
                }
                else
                    ini.WriteValue("Player", "FullScreen", "0");

                // VPX display index follows the adapter enumeration order. Not set : keep VPX's own choice
                if (vpScreen != null)
                    ini.WriteValue("Player", "Display", Math.Max(0, Displays.IndexOf(vpScreen, MonitorOrder.EnumDisplayMonitors)).ToString());

                // Vertical sync
                if (SystemConfig.isOptSet("vp_vsync") && !string.IsNullOrEmpty(SystemConfig["vp_vsync"]))
                    ini.WriteValue("Player", "SyncMode", SystemConfig["vp_vsync"]);
                else
                    ini.WriteValue("Player", "SyncMode", "3");

                // Video options
                if (SystemConfig.isOptSet("vp_ambient_occlusion") && SystemConfig["vp_ambient_occlusion"] == "dynamic")
                {
                    ini.WriteValue("Player", "DisableAO", "0");
                    ini.WriteValue("Player", "DynamicAO", "1");
                }
                else
                {
                    ini.WriteValue("Player", "DisableAO", SystemConfig["vp_ambient_occlusion"] == "0" ? "1" : "0");
                    ini.WriteValue("Player", "DynamicAO", "0");
                }

                ini.WriteValue("Player", "AAFactor", SystemConfig.GetValueOrDefault("vp_supersampling", "1.000000"));
                ini.WriteValue("Player", "FXAA", SystemConfig.GetValueOrDefault("vp_antialiasing", "0"));
                ini.WriteValue("Player", "Sharpen", SystemConfig.GetValueOrDefault("vp_sharpen", "0"));

                // View setup : 0 = desktop, 1 = cabinet, 2 = full single screen (FSS). AUTO follows the screen layout
                if (SystemConfig.isOptSet("vp_viewmode") && !string.IsNullOrEmpty(SystemConfig["vp_viewmode"]))
                    ini.WriteValue("Player", "BGSet", SystemConfig["vp_viewmode"]);
                else
                    ini.WriteValue("Player", "BGSet", _layout != null && !_layout.IsManual ? _layout.DefaultViewMode.ToString() : "0");

                bool aniFilter = !SystemConfig.isOptSet("vp_anisotropic_filtering") || SystemConfig.getOptBoolean("vp_anisotropic_filtering");
                ini.WriteValue("Player", "ForceAnisotropicFiltering", aniFilter ? "1" : "0");
                ini.WriteValue("Player", "UseNVidiaAPI", SystemConfig.getOptBoolean("vp_nvidia") ? "1" : "0");
                ini.WriteValue("Player", "SoftwareVertexProcessing", SystemConfig.getOptBoolean("vp_vertex") ? "1" : "0");

                // level of details
                if (SystemConfig.isOptSet("vp_details") && !string.IsNullOrEmpty(SystemConfig["vp_details"]))
                    ini.WriteValue("Player", "AlphaRampAccuracy", SystemConfig["vp_details"].ToIntegerString());
                else
                    ini.WriteValue("Player", "AlphaRampAccuracy", "10");

                // Audio
                ini.WriteValue("Player", "PlayMusic", SystemConfig.getOptBoolean("vp_music_off") ? "0" : "1");
                BindIniFeature(ini, "Player", "Sound3D", "vp_audiochannels", "0");

                // Lighting
                if (SystemConfig.isOptSet("vp_lighting") && !string.IsNullOrEmpty(SystemConfig["vp_lighting"]))
                {
                    string lighting = SystemConfig["vp_lighting"];
                    ini.WriteValue("Player", "EmissionScale", lighting);
                    ini.WriteValue("TableOverride", "OverrideEmissionScale", "1");
                }
                else
                {
                    ini.WriteValue("Player", "EmissionScale", "0.500000");
                    ini.WriteValue("TableOverride", "OverrideEmissionScale", "1");
                }

                // Controls
                ini.WriteValue("Player", "TweakKey", "68");
                if (SystemConfig.isOptSet("vp_inputdriver") && SystemConfig["vp_inputdriver"] == "pad2key")
                {
                    string sourcep2kFile = Path.Combine(AppConfig.GetFullPath("system"), "padtokey", "vpinball.keys.old");
                    string targetp2kFile = Path.Combine(AppConfig.GetFullPath("system"), "padtokey", "vpinball.keys");

                    if (!File.Exists(targetp2kFile) && File.Exists(sourcep2kFile))
                        try { File.Copy(sourcep2kFile, targetp2kFile); } catch { }
                }
                else
                    SetupVPinballControls(ini);

                if (SystemConfig.getOptBoolean("vp_useexit"))
                {
                    ini.WriteValue("Player", "ExitGameKey", "45");
                    _remapexit = true;
                }

                ini.Save();
            }
        }

        private void SetupVPinMameOptions(string path, string romPath, string vpxFile)
        {
            var softwareKey = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (softwareKey == null)
                return;

            var visualPinMame = softwareKey.CreateSubKey("Freeware").CreateSubKey("Visual PinMame");
            if (visualPinMame != null)
            {
                SimpleLogger.Instance.Info("[Generator] Writing VPinMame config to Registry.");

                // PinMAME may not handle windows at negative coordinates (screen on the left of / above the primary screen)
                if (_layout != null && !_layout.IsManual && _layout.DmdLocation != VPinballDmdLocation.None && (_layout.DmdWindow.X < 0 || _layout.DmdWindow.Y < 0))
                    SimpleLogger.Instance.Warning("[Generator] PinMAME DMD window at negative position (" + _layout.DmdWindow.X + "," + _layout.DmdWindow.Y + ") : if it shows up at the wrong place, move the DMD screen to the right of / below the primary screen in Windows, or use the external DMD (VPINMAME > DMD = EXTERNAL).");

                DisableVPinMameLicenceDialogs(romPath, visualPinMame);

                var globalKey = visualPinMame.CreateSubKey("globals");
                var defaultKey = visualPinMame.CreateSubKey("default");

                // global key
                if (globalKey != null)
                {
                    string vPinMamePath = Path.Combine(path, "VPinMAME");

                    SetOption(globalKey, "rompath", string.IsNullOrEmpty(romPath) ? Path.Combine(vPinMamePath, "roms") : romPath);

                    SetOption(globalKey, "artwork_directory", Path.Combine(vPinMamePath, "artwork"));
                    SetOption(globalKey, "cfg_directory", Path.Combine(vPinMamePath, "cfg"));
                    SetOption(globalKey, "cheat_file", Path.Combine(vPinMamePath, "cheat.dat"));
                    SetOption(globalKey, "cpu_affinity_mask", 0);
                    SetOption(globalKey, "diff_directory", Path.Combine(vPinMamePath, "diff"));
                    SetOption(globalKey, "hiscore_directory", Path.Combine(vPinMamePath, "hi"));
                    SetOption(globalKey, "history_file", Path.Combine(vPinMamePath, "history.dat"));
                    SetOption(globalKey, "input_directory", Path.Combine(vPinMamePath, "inp"));
                    SetOption(globalKey, "joystick", 0);
                    SetOption(globalKey, "low_latency_throttle", 0);
                    SetOption(globalKey, "mameinfo_file", Path.Combine(vPinMamePath, "mameinfo.dat"));
                    SetOption(globalKey, "memcard_directory", Path.Combine(vPinMamePath, "memcard"));
                    SetOption(globalKey, "mouse", 0);
                    SetOption(globalKey, "nvram_directory", Path.Combine(vPinMamePath, "nvram"));
                    SetOption(globalKey, "samplepath", Path.Combine(vPinMamePath, "samples"));
                    SetOption(globalKey, "screen", "");
                    SetOption(globalKey, "snapshot_directory", Path.Combine(vPinMamePath, "snap"));
                    SetOption(globalKey, "state_directory", Path.Combine(vPinMamePath, "sta"));
                    SetOption(globalKey, "steadykey", 1);
                    SetOption(globalKey, "wave_directory", Path.Combine(vPinMamePath, "wave"));
                    SetOption(globalKey, "window", 1);

                    globalKey.Close();
                }

                // default key: template used by VPinMAME for roms that do not have their own key yet
                if (defaultKey != null)
                {
                    SetupVPinMameRomKey(defaultKey);
                    defaultKey.Close();
                }

                // per rom config: only the rom used by the launched table, to preserve user tweaks on other roms
                string romName = VpxRomNameReader.GetRomName(vpxFile, romPath);
                if (!string.IsNullOrEmpty(romName))
                {
                    SimpleLogger.Instance.Info("[Generator] VPinMAME rom name read from table: " + romName);

                    // CreateSubKey opens the key if it already exists
                    using (var romKey = visualPinMame.CreateSubKey(romName))
                    {
                        if (romKey != null)
                            SetupVPinMameRomKey(romKey);
                    }
                }

                else if (!string.IsNullOrEmpty(romPath) && Directory.Exists(romPath))
                {
                    // Fallback: rom name could not be read from the table, update every rom key
                    SimpleLogger.Instance.Info("[Generator] VPinMAME rom name not found in table, updating all rom keys.");

                    string[] romList = Directory.GetFiles(romPath, "*.zip").Select(r => Path.GetFileNameWithoutExtension(r)).Distinct().ToArray();
                    foreach (var rom in romList)
                    {
                        using (var romKey = visualPinMame.CreateSubKey(rom))
                        {
                            if (romKey != null)
                                SetupVPinMameRomKey(romKey);
                        }
                    }
                }

                visualPinMame.Close();
            }

            softwareKey.Close();
        }

        // Per-rom VPinMAME settings, shared by the 'default' key and the rom keys
        private void SetupVPinMameRomKey(RegistryKey key)
        {
            SetOption(key, "cheat", 1);

            if (Program.SystemConfig.getOptBoolean("vpmame_dmd"))
            {
                SetOption(key, "showpindmd", 1);
                SetOption(key, "showwindmd", 0);
            }
            else if (Program.SystemConfig["vpmame_dmd"] == "none")
            {
                SetOption(key, "showpindmd", 0);
                SetOption(key, "showwindmd", 0);
            }
            else
            {
                SetOption(key, "showpindmd", 0);
                SetOption(key, "showwindmd", 1);
            }

            if (Program.SystemConfig.isOptSet("vpmame_soundmode") && !string.IsNullOrEmpty(Program.SystemConfig["vpmame_soundmode"]))
                SetOption(key, "sound_mode", Program.SystemConfig["vpmame_soundmode"].ToInteger());
            else
                SetOption(key, "sound_mode", 0);

            if (Program.SystemConfig.isOptSet("vpmame_dmdsize") && !string.IsNullOrEmpty(Program.SystemConfig["vpmame_dmdsize"]))
                SetOption(key, "dmd_doublesize", Program.SystemConfig["vpmame_dmdsize"].ToInteger());
            else
                SetOption(key, "dmd_doublesize", 0);

            BindBoolRegistryFeature(key, "cabinet_mode", "vpmame_cabinet", 1, 0, true);
            BindBoolRegistryFeature(key, "dmd_colorize", "vpmame_colordmd", 1, 0, false);

            // DMD window from the screen layout (screen setup not set : left untouched)
            if (_layout != null && !_layout.IsManual)
            {
                if (_layout.DmdLocation == VPinballDmdLocation.None)
                {
                    // Hardware DMD only : external DMD driver (ZeDMD, PIN2DMD...) without the PinMAME window
                    SetOption(key, "showpindmd", 1);
                    SetOption(key, "showwindmd", 0);
                }
                else
                {
                    Rectangle dmd = _layout.DmdWindow;
                    SetOption(key, "dmd_pos_x", dmd.X);
                    SetOption(key, "dmd_pos_y", dmd.Y);
                    SetOption(key, "dmd_width", dmd.Width);
                    SetOption(key, "dmd_height", dmd.Height);
                }
            }
        }

        private void SetupDmdDevice(string path)
        {
            string iniFile = Path.Combine(path, "VPinMAME", "DmdDevice.ini");

            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                BindBoolIniFeatureOn(ini, "virtualdmd", "enabled", "vpmame_virtualdmd", "true", "false");
                BindBoolIniFeature(ini, "zedmd", "enabled", "vpmame_zedmd", "true", "false");
                BindBoolIniFeature(ini, "zedmdhd", "enabled", "vpmame_zedmdhd", "true", "false");
                BindBoolIniFeature(ini, "pin2dmd", "enabled", "vpmame_pin2dmd", "true", "false");

                // Virtual DMD position from the screen layout (screen setup not set : left untouched)
                if (_layout != null && !_layout.IsManual)
                {
                    if (_layout.DmdLocation == VPinballDmdLocation.None)
                    {
                        // Hardware DMD only : no window on screen
                        ini.WriteValue("virtualdmd", "enabled", "false");
                    }
                    else
                    {
                        Rectangle dmd = _layout.DmdWindow;

                        // Position written here, not read from the VPinMAME registry
                        ini.WriteValue("virtualdmd", "useregistry", "false");
                        ini.WriteValue("virtualdmd", "left", dmd.X.ToString());
                        ini.WriteValue("virtualdmd", "top", dmd.Y.ToString());
                        ini.WriteValue("virtualdmd", "width", dmd.Width.ToString());
                        ini.WriteValue("virtualdmd", "height", dmd.Height.ToString());

                        // On the playfield screen, the DMD must stay over the fullscreen VPX window
                        if (_layout.DmdLocation == VPinballDmdLocation.Playfield)
                            ini.WriteValue("virtualdmd", "stayontop", "true");
                    }
                }

                ini.Save();
            }
        }

        private void SetupB2STableSettings(string path)
        {
            string b2STableSettingsPath = Path.Combine(path, "BackglassServer", "B2STableSettings.xml");
            string b2STableSettingsPathRom = Path.Combine(_gamePath, "B2STableSettings.xml");

            if (!File.Exists(b2STableSettingsPathRom))
            {
                try
                {
                    XDocument xmlDoc = new XDocument(
                    new XElement("B2STableSettings",
                    new XElement("ArePluginsOn", 1),
                    new XElement("DefaultStartMode", 2),
                    new XElement("DisableFuzzyMatching", 1),
                    new XElement("HideGrill", SystemConfig.getOptBoolean("vpbg_hidegrill") ? 1 : 0),
                    new XElement("LogPath", ""),  // Empty value
                    new XElement("IsLampsStateLogOn", 0),
                    new XElement("IsSolenoidsStateLogOn", 0),
                    new XElement("IsGIStringsStateLogOn", 0),
                    new XElement("IsLEDsStateLogOn", 0),
                    new XElement("IsPaintingLogOn", 0),
                    new XElement("IsStatisticsBackglassOn", 0),
                    new XElement("FormToFront", 1),
                    new XElement("ShowStartupError", 0),
                    new XElement("ScreenshotPath", ""),  // Empty value
                    new XElement("ScreenshotFileType", 0)
                    ));

                    xmlDoc.Save(b2STableSettingsPathRom);
                    xmlDoc.Save(b2STableSettingsPath);
                }
                catch { }
            }
            else
            {
                try
                {
                    XDocument xmlDoc = XDocument.Load(b2STableSettingsPathRom);
                    XElement root = xmlDoc.Element("B2STableSettings");

                    if (root != null)
                    {
                        // Plugins
                        XElement element = root.Element("ArePluginsOn");

                        if (element != null)
                            element.Value = "1";
                        else
                            root.Add(new XElement("ArePluginsOn", "1"));

                        // Hide grill
                        XElement hidegrill = root.Element("HideGrill");

                        if (hidegrill != null)
                            hidegrill.Value = SystemConfig.getOptBoolean("vpbg_hidegrill") ? "1" : "0";
                        else
                            root.Add(new XElement("HideGrill", SystemConfig.getOptBoolean("vpbg_hidegrill") ? "1" : "0"));

                        xmlDoc.Save(b2STableSettingsPathRom);
                        xmlDoc.Save(b2STableSettingsPath);
                    }
                    else
                        SimpleLogger.Instance.Warning("[WARNING] File B2STableSettings.xml is corrupted.");
                }
                catch { }
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            try
            {
                // Desktop geometry changed in Generate : move the cursor out of the playfield again (Screen bounds may be cached, use live metrics)
                if (_displayChanged && !SystemConfig.getOptBoolean("use_guns"))
                {
                    Size primary = SystemInformation.PrimaryMonitorSize;
                    Cursor.Position = new Point(primary.Width - 1, primary.Height / 2);
                }

                var px = Process.Start(path);
                Job.Current.AddProcess(px);

                using (var kb = new KeyboardManager(() => GracefulKill(px)))
                {
                    kb.RegisterKeyboardAction(() => SaveScreenshot(), (vkCode, scanCode) => vkCode == 44 && scanCode == 55);

                    while (!px.HasExited)
                    {
                        if (px.WaitForExit(10))
                            break;

                        Application.DoEvents();
                    }
                }

                try
                {
                    Process[] backGlasses = Process.GetProcessesByName("B2SBackglassServerEXE");
                    foreach (Process backGlass in backGlasses)
                        GracefulKill(backGlass);

                    Process[] ultraDMDs = Process.GetProcessesByName("UltraDMD");
                    foreach (Process ultraDMD in ultraDMDs)
                        GracefulKill(ultraDMD);

                    Process[] pupDisplays = Process.GetProcessesByName("PinUpDisplay");
                    foreach (Process pupDisplay in pupDisplays)
                        GracefulKill(pupDisplay);

                    Process[] pupDisplayXs = Process.GetProcessesByName("PinUpDisplayX");
                    foreach (Process pupDisplayX in pupDisplayXs)
                        GracefulKill(pupDisplayX);

                    Process[] pupPlayers = Process.GetProcessesByName("PinUpPlayer");
                    foreach (Process pupPlayer in pupPlayers)
                        GracefulKill(pupPlayer);
                }
                catch { }

                int exitCode = px.ExitCode;

                // vpinball always returns -1 when exiting
                if (exitCode == -1)
                    return 0;

                return exitCode;
            }
            catch
            {

            }
            finally
            {
                // Rotation without video mode : restore as soon as VPX and its companions are closed
                RestoreRotationMode();
            }

            return -1;
        }

        static void GracefulKill(Process p, int waitMs = 2000)
        {
            try
            {
                if (p.HasExited) return;
                if (!p.CloseMainWindow() || !p.WaitForExit(waitMs))
                    KillProcess(p);
            }
            catch { }
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_remapexit)
                mapping = PadToKey.AddOrUpdateKeyMapping(mapping, _processName, InputKey.hotkey | InputKey.start, null, action: () => ExitVPinball());
            else
                mapping = PadToKey.AddOrUpdateKeyMapping(mapping, _processName, InputKey.hotkey | InputKey.start, "(%{CLOSE})");

            return PadToKey.AddOrUpdateKeyMapping(mapping, _processName, InputKey.hotkey | InputKey.r3, () => SaveScreenshot());
        }

        private void ExitVPinball()
        {
            uint foregroundPid;
            User32.GetWindowThreadProcessId(User32.GetForegroundWindow(), out foregroundPid);

            var vpx = Process.GetProcessesByName(_processName);
            try
            {
                if (vpx.Any(p => p.Id == foregroundPid))
                {
                    SendKeys.SendWait("x");
                    return;
                }

                foreach (var p in vpx)
                {
                    try { p.CloseMainWindow(); }
                    catch { }
                }
            }
            finally
            {
                foreach (var p in vpx)
                    p.Dispose();
            }
        }

        private static void SaveScreenshot()
        {
            if (!ScreenCapture.AddScreenCaptureToGameList(Program.SystemConfig["system"], Program.SystemConfig["rom"]))
            {
                string path = Program.AppConfig.GetFullPath("screenshots");
                if (!Directory.Exists(path))
                    return;

                int index = 0;
                string fn;

                do
                {
                    fn = Path.Combine(path, Path.GetFileNameWithoutExtension(Program.SystemConfig["rom"]) + (index == 0 ? "" : "_" + index.ToString()) + ".jpg");
                    index++;
                }
                while (File.Exists(fn));

                ScreenCapture.CaptureScreen(fn);
            }
        }

        private static void KillProcess(Process px)
        {
            try { px.Kill(); }
            catch { }
        }

        public override void Cleanup()
        {
            // Display restore order : playfield mode first, then the position of the other screens.
            // Normal exit : already done by Program (video mode) and RunAndWait (rotation), these calls do nothing.
            // Exception in Generate : Program calls Cleanup before disposing its ScreenResolution, the mode is restored here first.
            if (_appliedResolution != null)
            {
                _appliedResolution.Dispose();
                _appliedResolution = null;
            }

            RestoreRotationMode();
            RestoreDisplayLayout();

            // B2S server has been closed in RunAndWait, the .res file is no longer in use
            RestoreTableResFile();

            if (_splash != null)
            {
                _splash.Dispose();
                _splash = null;
            }

            string backupp2kFile = Path.Combine(AppConfig.GetFullPath("system"), "padtokey", "vpinball.keys.old");
            string p2kFile = Path.Combine(AppConfig.GetFullPath("system"), "padtokey", "vpinball.keys");

            if (File.Exists(p2kFile))
            {
                if (File.Exists(backupp2kFile))
                    try { File.Delete(backupp2kFile); } catch { }

                try { File.Move(p2kFile, backupp2kFile); } catch { }
            }

            base.Cleanup();
        }

        /// <summary>
        /// IGNORE TABLE .RES FILE : the table specific B2S settings file is renamed during the game and restored on exit.
        /// A file left renamed by a previous crash is always restored first, whatever the option.
        /// </summary>
        private void SetupTableResFile(string rom)
        {
            if (string.IsNullOrEmpty(rom))
                return;

            _tableResFile = Path.ChangeExtension(rom, ".res");
            string disabled = _tableResFile + DisabledResSuffix;

            // Crash recovery
            if (File.Exists(disabled))
            {
                if (File.Exists(_tableResFile))
                {
                    // The user created a new .res file meanwhile : never overwrite or delete any of them
                    SimpleLogger.Instance.Warning("[Generator] Both " + _tableResFile + " and " + disabled + " exist, table .res file left untouched.");
                    return;
                }

                try
                {
                    File.Move(disabled, _tableResFile);
                    SimpleLogger.Instance.Info("[Generator] Restored table .res file left disabled by a previous session : " + _tableResFile);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[Generator] Unable to restore " + disabled + " : " + ex.Message);
                    return;
                }
            }

            if (!SystemConfig.getOptBoolean("vp_ignoreres") || !File.Exists(_tableResFile))
                return;

            try
            {
                File.Move(_tableResFile, disabled);
                _tableResDisabled = true;
                SimpleLogger.Instance.Info("[Generator] Table .res file ignored for this game : " + _tableResFile);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Unable to disable " + _tableResFile + " : " + ex.Message);
            }
        }

        private void RestoreTableResFile()
        {
            if (!_tableResDisabled)
                return;

            _tableResDisabled = false;

            string disabled = _tableResFile + DisabledResSuffix;

            try
            {
                if (File.Exists(disabled) && !File.Exists(_tableResFile))
                {
                    File.Move(disabled, _tableResFile);
                    SimpleLogger.Instance.Info("[Generator] Table .res file restored : " + _tableResFile);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Unable to restore " + disabled + " : " + ex.Message);
            }
        }

        private ScreenOrientation? GetRequestedRotation()
        {
            switch (SystemConfig["vp_rotatescreen"])
            {
                case "90": return ScreenOrientation.Angle90;
                case "180": return ScreenOrientation.Angle180;
                case "270": return ScreenOrientation.Angle270;
            }

            return null;
        }

        /// <summary>
        /// 10.8+ : applies the video mode and the playfield rotation before the configuration files are written.
        /// Video mode (with or without rotation) : applied on the ScreenResolution owned by Program, restored when Program disposes it.
        /// Rotation only (no video mode, or exclusive fullscreen) : own ScreenResolution, restored in RunAndWait / Cleanup.
        /// The position of every screen is captured first, and restored in Cleanup.
        /// </summary>
        private void ApplyDisplayMode(ScreenResolution resolution)
        {
            ScreenOrientation? rotation = GetRequestedRotation();
            Screen playfield = VPinballScreenLayout.GetPlayfieldScreen();

            // Before any change (also needed for exclusive fullscreen : VPX changes the mode itself)
            _displayLayout = ScreenResolution.LayoutSnapshot.Capture();

            if (resolution != null && !_exclusiveFullscreen)
            {
                if (rotation.HasValue && playfield != null)
                {
                    // Video mode and rotation in a single change, on the playfield screen
                    resolution.SetTargetScreen(playfield);
                    resolution.Orientation = rotation;
                }

                resolution.Apply();
                _appliedResolution = resolution;
                _displayChanged = resolution.HasChanged;

                // Already applied : Program must not apply it again after Generate
                DependsOnDesktopResolution = false;
                return;
            }

            if (!rotation.HasValue || playfield == null)
                return;

            _rotationMode = ScreenResolution.GetCurrentResolution(playfield.DeviceName);
            if (_rotationMode == null)
            {
                SimpleLogger.Instance.Warning("[Generator] Unable to read the current mode of " + playfield.DeviceName + ", screen not rotated.");
                return;
            }

            _rotationMode.Orientation = rotation;
            _rotationMode.Apply();
            _displayChanged = _rotationMode.HasChanged;
        }

        private void RestoreRotationMode()
        {
            if (_rotationMode != null)
            {
                _rotationMode.Dispose();
                _rotationMode = null;
            }
        }

        /// <summary>Puts back the screens moved by Windows. Must run after the playfield mode has been restored.</summary>
        private void RestoreDisplayLayout()
        {
            if (_displayLayout == null)
                return;

            var snapshot = _displayLayout;
            _displayLayout = null;

            if (_displayChanged || _exclusiveFullscreen)
                snapshot.Restore();
        }

        /// <summary>
        /// ScreenRes.txt from the computed screen layout.
        /// The backglass screen is given by its X coordinate (@x) : independent of the Windows / B2S screen numbering.
        /// Backglass position is relative to the selected screen, DMD position is relative to the backglass window.
        /// </summary>
        private void SetupScreenResFromLayout(ScreenRes sr)
        {
            var layout = _layout;

            sr.ScreenResX = layout.Playfield.Width;
            sr.ScreenResY = layout.Playfield.Height;

            // No backglass screen : B2S is disabled in VPinballX.ini, values matching the playfield screen are written
            Rectangle screen = layout.HasBackglass ? layout.BackglassScreenBounds : layout.Playfield;
            Rectangle backglass = layout.HasBackglass ? layout.Backglass : layout.Playfield;

            sr.Monitor = "@" + screen.X;
            sr.Screen2ResX = backglass.Width;
            sr.Screen2ResY = backglass.Height;

            Point backglassPos = VPinballScreenLayout.RelativeTo(backglass, screen);
            sr.Screen2posX = backglassPos.X;
            sr.Screen2posY = backglassPos.Y;

            // B2S DMD area (B2S DMD / full DMD), relative to the backglass window
            if (layout.DmdLocation == VPinballDmdLocation.None)
            {
                sr.DmdResX = 0;
                sr.DmdResY = 0;
                sr.DmdPosX = 0;
                sr.DmdPosY = 0;
            }
            else
            {
                Point dmdPos = VPinballScreenLayout.RelativeTo(layout.DmdArea, backglass);
                sr.DmdResX = layout.DmdArea.Width;
                sr.DmdResY = layout.DmdArea.Height;
                sr.DmdPosX = dmdPos.X;
                sr.DmdPosY = dmdPos.Y;
            }
        }

        /// <summary>Screen setup not set (or VPX before 10.8) : SCREENRES values, defaults as before.</summary>
        private void SetupScreenResManual(ScreenRes sr, ScreenResolution resolution)
        {
            // Legacy default : video mode, else primary screen
            Size playfield = resolution == null ? Screen.PrimaryScreen.Bounds.Size : new Size(resolution.Width, resolution.Height);

            // Screen rotation (10.8+) : live size of the rotated playfield screen
            if (_version >= new Version(10, 8, 0, 0) && GetRequestedRotation().HasValue)
            {
                Rectangle live = VPinballScreenLayout.GetBounds(VPinballScreenLayout.GetPlayfieldScreen());
                if (!live.IsEmpty)
                    playfield = live.Size;
            }

            if (SystemConfig.isOptSet("ScreenResX") && !string.IsNullOrEmpty(SystemConfig["ScreenResX"]))
                sr.ScreenResX = SystemConfig["ScreenResX"].ToInteger();
            else
                sr.ScreenResX = playfield.Width;

            if (SystemConfig.isOptSet("ScreenResY") && !string.IsNullOrEmpty(SystemConfig["ScreenResY"]))
                sr.ScreenResY = SystemConfig["ScreenResY"].ToInteger();
            else
                sr.ScreenResY = playfield.Height;

            if (SystemConfig.isOptSet("Screen2ResX") && !string.IsNullOrEmpty(SystemConfig["Screen2ResX"]) && SystemConfig.isOptSet("Screen2ResY") && !string.IsNullOrEmpty(SystemConfig["Screen2ResY"]))
            {
                sr.Screen2ResX = SystemConfig["Screen2ResX"].ToInteger();
                sr.Screen2ResY = SystemConfig["Screen2ResY"].ToInteger();
            }
            else
            {
                Screen secondary = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
                if (secondary != null)
                {
                    sr.Screen2ResX = secondary.Bounds.Width;
                    sr.Screen2ResY = secondary.Bounds.Height;
                }
            }

            if (SystemConfig.isOptSet("Monitor") && !string.IsNullOrEmpty(SystemConfig["Monitor"]))
                sr.Monitor = SystemConfig["Monitor"].ToInteger();
            else
                sr.Monitor = Screen.AllScreens.Length == 1 ? 1 : 2;

            if (SystemConfig.isOptSet("Screen2posX") && !string.IsNullOrEmpty(SystemConfig["Screen2posX"]) && SystemConfig.isOptSet("Screen2posY") && !string.IsNullOrEmpty(SystemConfig["Screen2posY"]))
            {
                sr.Screen2posX = SystemConfig["Screen2posX"].ToInteger();
                sr.Screen2posY = SystemConfig["Screen2posY"].ToInteger();
            }

            if (SystemConfig.isOptSet("DmdResX") && !string.IsNullOrEmpty(SystemConfig["DmdResX"]) && SystemConfig.isOptSet("DmdResY") && !string.IsNullOrEmpty(SystemConfig["DmdResY"]))
            {
                sr.DmdResX = SystemConfig["DmdResX"].ToInteger();
                sr.DmdResY = SystemConfig["DmdResY"].ToInteger();
            }

            if (SystemConfig.isOptSet("DmdposX") && !string.IsNullOrEmpty(SystemConfig["DmdposX"]) && SystemConfig.isOptSet("DmdposY") && !string.IsNullOrEmpty(SystemConfig["DmdposY"]))
            {
                sr.DmdPosX = SystemConfig["DmdposX"].ToInteger();
                sr.DmdPosY = SystemConfig["DmdposY"].ToInteger();
            }
        }

        class DirectB2sData
        {
            public static DirectB2sData FromFile(string file)
            {
                if (!File.Exists(file))
                    return null;


                XmlDocument document = new XmlDocument();
                document.Load(file);

                XmlElement element = (XmlElement)document.SelectSingleNode("DirectB2SData");
                if (element == null)
                    return null;

                var bulbs = new List<Bulb>();

                foreach (XmlElement bulb in element.SelectNodes("Illumination/Bulb"))
                {
                    if (!bulb.HasAttribute("Parent") || bulb.GetAttribute("Parent") != "Backglass")
                        continue;

                    try
                    {
                        Bulb item = new Bulb
                        {
                            ID = bulb.GetAttribute("ID").ToInteger(),
                            LightColor = bulb.GetAttribute("LightColor"),
                            LocX = bulb.GetAttribute("LocX").ToInteger(),
                            LocY = bulb.GetAttribute("LocY").ToInteger(),
                            Width = bulb.GetAttribute("Width").ToInteger(),
                            Height = bulb.GetAttribute("Height").ToInteger(),
                            Visible = bulb.GetAttribute("Visible") == "1",
                            IsImageSnippit = bulb.GetAttribute("IsImageSnippit") == "1",
                            Image = Misc.Base64ToImage(bulb.GetAttribute("Image"))
                        };

                        if (item.Visible && item.Image != null)
                            bulbs.Add(item);
                    }
                    catch { }
                }

                XmlElement element13 = (XmlElement)element.SelectSingleNode("Images/BackglassImage");
                if (element13 != null)
                {
                    try
                    {
                        var image = Misc.Base64ToImage(element13.Attributes["Value"].InnerText);
                        if (image != null)
                        {
                            return new DirectB2sData()
                            {
                                Bulbs = bulbs.ToArray(),
                                Image = image,
                            };
                        }
                    }
                    catch { }
                }

                return null;
            }

            public Image RenderBackglass(int index = 0)
            {
                var bitmap = new Bitmap(Image);

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    foreach (var bulb in Bulbs)
                    {
                        if (bulb.IsImageSnippit)
                            continue;

                        if (index == 0 && (bulb.ID & 1) == 0)
                            continue;

                        if (index == 1 && (bulb.ID & 1) == 1)
                            continue;

                        if (index == 3)
                            continue;

                        Color lightColor = Color.White;
                        var split = bulb.LightColor.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        if (split.Length == 3)
                            lightColor = Color.FromArgb(split[0].ToInteger(), split[1].ToInteger(), split[2].ToInteger());

                        using (ImageAttributes imageAttrs = new ImageAttributes())
                        {
                            var colorMatrix = new ColorMatrix(new float[][]
                                    {
                                        new float[] { lightColor.R / 255f, 0, 0, 0, 0 },
                                        new float[] { 0, lightColor.G / 255f, 0, 0, 0 },
                                        new float[] { 0, 0, lightColor.B / 255f, 0, 0 },
                                        new float[] { 0, 0, 0, 1, 0 },
                                        new float[] { 0, 0, 0, 0, 1 }
                                    });

                            imageAttrs.SetColorMatrix(colorMatrix);

                            Rectangle dest = new Rectangle(bulb.LocX, bulb.LocY, bulb.Width, bulb.Height);
                            g.DrawImage(bulb.Image, dest, 0, 0, bulb.Image.Width, bulb.Image.Height, GraphicsUnit.Pixel, imageAttrs, null, IntPtr.Zero);
                        }
                    }
                }

                return bitmap;
            }

            public Bulb[] Bulbs { get; private set; }
            public Image Image { get; private set; }

            public class Bulb
            {
                public int ID { get; set; }
                public string LightColor { get; set; }
                public int LocX { get; set; }
                public int LocY { get; set; }
                public int Width { get; set; }
                public int Height { get; set; }
                public bool Visible { get; set; }
                public bool IsImageSnippit { get; set; }
                public Image Image { get; set; }
            }
        }
    }
}
