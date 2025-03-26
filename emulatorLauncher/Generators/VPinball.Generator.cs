using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.VPinballLauncher;
using System.Xml;
using System.Xml.Linq;
using System.Drawing;
using Microsoft.Win32;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.PadToKeyboard;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using System.Drawing.Imaging;

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

            rom = this.TryUnZipGameIfNeeded(system, rom, true, false);
            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.vpx").FirstOrDefault();
                if (string.IsNullOrEmpty(rom))
                    return null;
            }

            _splash = ShowSplash(rom);

            ScreenResolution.SetHighDpiAware(exe);
            EnsureUltraDMDRegistered(path);
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

            ScreenRes sr = ScreenRes.Load(Path.GetDirectoryName(rom));
            if (sr != null)
            {
                sr.ScreenResX = resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width;
                sr.ScreenResY = resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height;

                Screen secondary = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
                if (secondary != null)
                {
                    sr.Screen2ResX = secondary.Bounds.Width;
                    sr.Screen2ResY = secondary.Bounds.Height;
                }
                sr.Monitor = Screen.AllScreens.Length == 1 ? 1 : 2;

                sr.Save();
            }

            SetupOptions(path, romPath, resolution);
            SetupB2STableSettings(path);

            var commands = new List<string>();

            if (_version >= new Version(10, 7, 0, 0))
                commands.Add("-ExtMinimized");

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

        private void SetupOptions(string path, string romPath, ScreenResolution resolution)
        {
            if (_version >= new Version(10, 8, 0, 0))
                SetupOptionsIniFile(path, resolution);
            else
                SetupOptionsRegistry(resolution);

            SetupVPinMameOptions(path, romPath);
            SetupDmdDevice(path);
        }

        private void SetupOptionsIniFile(string path, ScreenResolution resolution)
        {
            string iniFile = Path.Combine(path, "VPinballX.ini");

            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                SimpleLogger.Instance.Info("[Generator] Writing config to VPinballX.ini file.");

                if (SystemConfig.isOptSet("enableb2s") && !SystemConfig.getOptBoolean("enableb2s"))
                    ini.WriteValue("Controller", "ForceDisableB2S", "1");
                else
                    ini.WriteValue("Controller", "ForceDisableB2S", "0");

                BindIniFeature(ini, "Controller", "DOFContactors", "vpdof_contractors", "2");
                BindIniFeature(ini, "Controller", "DOFKnocker", "vpdof_knocker", "2");
                BindIniFeature(ini, "Controller", "DOFChimes", "vpdof_chimes", "2");
                BindIniFeature(ini, "Controller", "DOFBell", "vpdof_bell", "2");
                BindIniFeature(ini, "Controller", "DOFGear", "vpdof_gear", "2");
                BindIniFeature(ini, "Controller", "DOFShaker", "vpdof_shaker", "2");
                BindIniFeature(ini, "Controller", "DOFFlippers", "vpdof_flippers", "2");
                BindIniFeature(ini, "Controller", "DOFTargets", "vpdof_targets", "2");
                BindIniFeature(ini, "Controller", "DOFDropTargets", "vpdof_droptargets", "2");

                ini.WriteValue("Player", "DisableESC", "1");

                // Get monitor index 
                int monitorIndex = SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]) ? SystemConfig["MonitorIndex"].ToInteger() - 1 : 0;
                if (monitorIndex >= Screen.AllScreens.Length)
                    monitorIndex = 0;

                // Get bounds based on screen or resolution
                Size bounds = resolution == null ? Screen.PrimaryScreen.Bounds.Size : new Size(resolution.Width, resolution.Height);
                if (monitorIndex != 0 && resolution == null && monitorIndex < Screen.AllScreens.Length)
                    bounds = Screen.AllScreens[monitorIndex].Bounds.Size;

                // Resolution and fullscreen
                ini.WriteValue("Player", "Width", bounds.Width.ToString());
                ini.WriteValue("Player", "Height", bounds.Height.ToString());
                ini.WriteValue("Player", "FullScreen", "0"); // resolution == null ? "0" : "1" -> Let desktop resolution handle
                ini.WriteValue("Player", "Display", monitorIndex.ToString());

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
                
                ini.WriteValue("Player", "BGSet", SystemConfig.getOptBoolean("arcademode") ? "1" : "0");

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

                // Controls
                if (SystemConfig.isOptSet("vp_inputdriver") && SystemConfig["vp_inputdriver"] == "pad2key")
                {
                    string sourcep2kFile = Path.Combine(AppConfig.GetFullPath("system"), "padtokey", "vpinball.keys.old");
                    string targetp2kFile = Path.Combine(AppConfig.GetFullPath("system"), "padtokey", "vpinball.keys");

                    if (!File.Exists(targetp2kFile) && File.Exists(sourcep2kFile))
                        try { File.Copy(sourcep2kFile, targetp2kFile); } catch { }
                }
                else
                    SetupVPinballControls(ini);

                ini.Save();
            }
        }

        private static void SetupVPinMameOptions(string path, string romPath)
        {
            var softwareKey = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (softwareKey == null)
                return;
            
            var visualPinMame = softwareKey.CreateSubKey("Freeware").CreateSubKey("Visual PinMame");
            if (visualPinMame != null)
            {
                SimpleLogger.Instance.Info("[Generator] Writing VPinMame config to Registry.");

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

                // default key
                if (defaultKey != null)
                {
                    SetOption(defaultKey, "cheat", 1);

                    if (Program.SystemConfig.getOptBoolean("vpmame_dmd"))
                    {
                        SetOption(defaultKey, "showpindmd", 1);
                        SetOption(defaultKey, "showwindmd", 0);
                    }
                    else
                    {
                        SetOption(defaultKey, "showpindmd", 0);
                        SetOption(defaultKey, "showwindmd", 1);
                    }

                    BindBoolRegistryFeature(defaultKey, "cabinet_mode", "vpmame_cabinet", 1, 0, true);
                    BindBoolRegistryFeature(defaultKey, "dmd_colorize", "vpmame_colordmd", 1, 0, false);

                    defaultKey.Close();
                }

                // per rom config
                if (romPath != null)
                {
                    string[] romList = Directory.GetFiles(romPath, "*.zip").Select(r => Path.GetFileNameWithoutExtension(r)).Distinct().ToArray();
                    foreach (var rom in romList)
                    {
                        var romKey = visualPinMame.OpenSubKey(rom, true);

                        if (romKey == null)
                            romKey = visualPinMame.CreateSubKey(rom);

                        SetOption(romKey, "cheat", 1);

                        if (Program.SystemConfig.getOptBoolean("vpmame_dmd"))
                        {
                            SetOption(romKey, "showpindmd", 1);
                            SetOption(romKey, "showwindmd", 0);
                        }
                        else
                        {
                            SetOption(romKey, "showpindmd", 0);
                            SetOption(romKey, "showwindmd", 1);
                        }

                        BindBoolRegistryFeature(romKey, "cabinet_mode", "vpmame_cabinet", 1, 0, true);
                        BindBoolRegistryFeature(romKey, "dmd_colorize", "vpmame_colordmd", 1, 0, false);

                        romKey.Close();
                    }
                }
            }

            softwareKey.Close();
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
                catch {}
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            try
            {
                var px = Process.Start(path);

                using (var kb = new KeyboardManager(() => KillProcess(px)))
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
                        backGlass.Kill();

                    Process[] ultraDMDs = Process.GetProcessesByName("UltraDMD");
                    foreach (Process ultraDMD in ultraDMDs)
                        ultraDMD.Kill();

                    Process[] pupDisplays = Process.GetProcessesByName("PinUpDisplay");
                    foreach (Process pupDisplay in pupDisplays)
                        pupDisplay.Kill();

                    Process[] pupPlayers = Process.GetProcessesByName("PinUpPlayer");
                    foreach (Process pupPlayer in pupPlayers)
                        pupPlayer.Kill();
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

            return -1;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _processName, InputKey.hotkey | InputKey.r3, () => SaveScreenshot());
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
