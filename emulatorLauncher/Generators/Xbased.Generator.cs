using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class XbasedGenerator : Generator
    {
        public XbasedGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _path;
        private string _exe;
        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private XbasedScreenAnchor _screenAnchor;
        private SaveStatesWatcher _saveStatesWatcher;
        private bool _pad2Keyoverride = false;

        // Exit code of a run that goes on in a new process (state loaded, restart, title relaunch): bit 30 + new process id
        private const int RunAgainFlag = 0x40000000;

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out _path, out _exe, "xbased.exe"))
                return null;

            SetSdl3EmulatorVersion(_exe);

            _resolution = resolution;
            bool fullscreen = ShouldRunFullscreen();

            // Extracted games: only a file named default.xbe (or its folder) is read as a disc
            if (Path.GetExtension(rom).Equals(".xbe", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(rom).Equals("default.xbe", StringComparison.OrdinalIgnoreCase))
                SimpleLogger.Instance.Warning("[Generator] xbased only boots extracted games from a file named default.xbe : " + rom);

            SetupSettings(system);
            SetupSaveStates(system, rom);

            bool widescreen = SystemConfig.getOptBoolean("xbased_widescreen");
            string ratio = SystemConfig.GetValueOrDefault("xbased_ratio", "auto");

            // Fake bezels for a 4:3 picture only
            if (fullscreen && !widescreen && (ratio == "auto" || ratio == "4:3"))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            var commandArray = new List<string>();
            commandArray.Add("\"" + rom + "\"");
            commandArray.AddRange(GetCommandLineOptions(fullscreen, widescreen, ratio));

            // Save state selected in EmulationStation, loaded from the RetroBat folder (its blobs are there)
            string stateFile = SystemConfig["state_file"];
            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
                commandArray.Add("\"loadstate=" + stateFile + "\"");

            var ret = new ProcessStartInfo()
            {
                FileName = _exe,
                WorkingDirectory = _path,
                Arguments = string.Join(" ", commandArray),
                UseShellExecute = false,
            };

            // xbased opens its fullscreen frame on the screen of the window given in XBASED_DASH_HWND
            if (fullscreen && Program.IsMonitorIndexSet)
            {
                _screenAnchor = XbasedScreenAnchor.Create(Program.TargetScreen);
                if (_screenAnchor != null)
                {
                    ret.EnvironmentVariables["XBASED_DASH_HWND"] = _screenAnchor.Handle.ToInt64().ToString("x");
                    SimpleLogger.Instance.Info("[Generator] xbased screen anchor on " + Program.TargetScreen.DeviceName);
                }
            }

            return ret;
        }

        /// <summary>
        /// Run options: the command line comes after xbased.ini and the game's own ini, it always wins.
        /// </summary>
        private List<string> GetCommandLineOptions(bool fullscreen, bool widescreen, string ratio)
        {
            var options = new List<string>();

            options.Add("fullscreen=" + (fullscreen ? "1" : "0"));

            // Video
            options.Add("scale=" + SystemConfig.GetValueOrDefault("render_scale", "1"));
            options.Add("aspect=" + ratio);
            options.Add("vsync=" + (SystemConfig.getOptBoolean("vsync") || !SystemConfig.isOptSet("vsync") ? "1" : "0"));
            options.Add("fsrate=" + SystemConfig.GetValueOrDefault("xbased_fsrate", "desktop"));

            // Frame rate: original, up to 60 / 120 fps, or unlocked (held to the display's refresh rate)
            switch (SystemConfig["xbased_fps"])
            {
                case "60":
                case "120":
                    options.Add("uncap=1");
                    options.Add("fpscap=" + SystemConfig["xbased_fps"]);
                    break;
                case "unlocked":
                    options.Add("uncap=1");
                    options.Add("fpscap=0");
                    break;
                default:
                    options.Add("uncap=0");
                    break;
            }

            // Rendering
            options.Add("upscale=" + SystemConfig.GetValueOrDefault("xbased_upscale", "bilinear"));
            options.Add("fxaa=" + (SystemConfig.getOptBoolean("fxaa") ? "1" : "0"));
            options.Add("aniso=" + SystemConfig.GetValueOrDefault("anisotropic_filtering", "0"));
            options.Add("crt=" + SystemConfig.GetValueOrDefault("xbased_crt", "0"));
            options.Add("overscan=" + SystemConfig.GetValueOrDefault("xbased_overscan", "0"));
            options.Add("asyncpipe=" + (SystemConfig.getOptBoolean("xbased_asyncpipe") ? "1" : "0"));
            options.Add("prebuild=" + (SystemConfig.getOptBoolean("xbased_prebuild") ? "1" : "0"));
            options.Add("showfps=" + (SystemConfig.getOptBoolean("xbased_showfps") ? "1" : "0"));

            // Console settings
            options.Add("lang=" + GetXboxLanguage());
            options.Add("video=0x" + GetVideoFlags(widescreen).ToString("x"));
            options.Add("avpack=" + SystemConfig.GetValueOrDefault("xbased_avpack", "hdtv"));
            options.Add("pal60=" + SystemConfig.GetValueOrDefault("xbased_pal60", "1"));
            options.Add("ram=" + SystemConfig.GetValueOrDefault("system_memory", "64"));
            options.Add("devtime=" + (SystemConfig.getOptBoolean("xbased_fastdisc") ? "0" : "2"));
            options.Add("audio=" + SystemConfig.GetValueOrDefault("xbased_audio", "stereo"));

            return options;
        }

        /// <summary>
        /// Console video flags: widescreen 0x10000, 480p 0x80000, 720p 0x20000, 1080i 0x40000 (xbased default: 480p).
        /// </summary>
        private int GetVideoFlags(bool widescreen)
        {
            int flags;

            switch (SystemConfig["xbased_hdmodes"])
            {
                case "none":
                    flags = 0;
                    break;
                case "720p":
                    flags = 0x80000 | 0x20000;
                    break;
                case "1080i":
                    flags = 0x80000 | 0x20000 | 0x40000;
                    break;
                default:
                    flags = 0x80000;
                    break;
            }

            if (widescreen)
                flags |= 0x10000;

            return flags;
        }

        private int GetXboxLanguage()
        {
            if (SystemConfig.isOptSet("xbox_language") && !string.IsNullOrEmpty(SystemConfig["xbox_language"]))
                return SystemConfig["xbox_language"].ToInteger();

            var availableLanguages = new Dictionary<string, int>()
            {
                { "en", 1 }, { "jp", 2 }, { "ja", 2 }, { "de", 3 }, { "fr", 4 },
                { "es", 5 }, { "it", 6 }, { "ko", 7 }, { "zh", 8 }, { "pt", 9 }
            };

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang) && availableLanguages.TryGetValue(lang, out int ret))
                return ret;

            return 1;
        }

        /// <summary>
        /// xbased.ini keeps what xbased only reads from it: controllers, shortcuts, hard disk, interface.
        /// Also used by the dashboard when xbased is started from the RetroBat menu.
        /// </summary>
        private void SetupSettings(string system)
        {
            string iniFile = Path.Combine(_path, "xbased.ini");

            try
            {
                var ini = XbasedIniFile.Load(iniFile);

                // Updates come with RetroBat
                ini.Set("update.check", "0");

                // In-game menu in the RetroBat language
                string[] uiLanguages = { "en", "es", "fr", "de", "it", "pt", "ru", "pl", "ja", "zh", "ko" };
                string lang = GetCurrentLanguage();
                ini.Set("ui.lang", uiLanguages.Contains(lang) ? lang : "auto");

                // Hard disk (game saves) in the RetroBat saves folder
                string hddPath = GetHddPath(system);
                if (!string.IsNullOrEmpty(hddPath))
                    ini.Set("hdd", hddPath);

                // Game folder for the dashboard library
                string romsPath = Path.Combine(AppConfig.GetFullPath("roms"), system);
                if (Directory.Exists(romsPath))
                    ini.AddValue("gamedir", romsPath);

                ConfigureHotkeys(ini);
                ConfigureControllers(ini);

                ini.Save();
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Unable to write xbased.ini : " + ex.Message, ex);
            }
        }

        /// <summary>
        /// saves\xbox\xbased\hdd. A hard disk created by xbased in its own folder before is copied once (never moved).
        /// </summary>
        private string GetHddPath(string system)
        {
            if (string.IsNullOrEmpty(AppConfig["saves"]) || !Directory.Exists(AppConfig.GetFullPath("saves")))
                return null;

            string hddPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "xbased", "hdd");
            string emulatorHdd = Path.Combine(_path, "hdd");

            try
            {
                if (!Directory.Exists(hddPath) && Directory.Exists(emulatorHdd))
                {
                    SimpleLogger.Instance.Info("[Generator] Copying xbased hard disk to " + hddPath);
                    CopyDirectory(emulatorHdd, hddPath);
                }

                // xbased creates only the last folder of the path
                if (!Directory.Exists(hddPath))
                    Directory.CreateDirectory(hddPath);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Unable to prepare xbased hard disk : " + ex.Message);
                return null;
            }

            return hddPath;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);

            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }

        /// <summary>
        /// Keyboard shortcuts on the RetroBat keys (es_padtokey sends them for the Select hotkey combos).
        /// No controller shortcut: Guide is not used in RetroBat.
        /// </summary>
        private void ConfigureHotkeys(XbasedIniFile ini)
        {
            var keys = new Dictionary<string, string>()
            {
                { "menu", "F1" },
                { "save", "F2" },
                { "load", "F4" },
                { "turbo", "L" },
                { "pause", "P" },
                { "shot", "F8" },
                { "full", "F" },
            };

            // User keyboard hotkeys from xbased_kb_hotkeys.yml / kb_hotkeys.yml, the controller hotkeys follow them (padToKey.xml)
            if (Hotkeys.GetHotKeysFromFile("xbased", "", out Dictionary<string, HotkeyResult> custoHotkeys) && custoHotkeys.Count > 0)
            {
                foreach (var hotkey in custoHotkeys.Values)
                {
                    if (!string.IsNullOrEmpty(hotkey.EmulatorValue) && keys.ContainsKey(hotkey.EmulatorKey))
                        keys[hotkey.EmulatorKey] = hotkey.EmulatorValue;
                }

                _pad2Keyoverride = true;
            }

            foreach (var key in keys)
            {
                ini.Set("hk." + key.Key, key.Value);
                ini.Set("hkpad." + key.Key, "none");
            }
        }

        /// <summary>
        /// xbased saves in states\<TITLEID>.<version>: the watcher copies each new state (and its blobs) to the RetroBat folder as the next slot.
        /// </summary>
        private void SetupSaveStates(string system, string rom)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported("xbased"))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, "xbased", "xbased");
            string emulatorPath = Path.Combine(_path, "states");

            _saveStatesWatcher = new XbasedSaveStatesMonitor(rom, emulatorPath, localPath);
            SimpleLogger.Instance.Info("[INFO] SavesStatesWatcher enabled.");
        }

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
                PadToKey.AddOrUpdateKeyMapping(mapping, "xbased", InputKey.hotkey | InputKey.start, "(%{CLOSE})");
            }

            return mapping;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = 0;

            var process = StartEmulator(path);
            if (process == null)
            {
                bezel?.Dispose();
                _screenAnchor?.Dispose();
                return ExitCode == ExitCodes.CustomError ? -1 : 0;
            }

            try
            {
                Job.Current.AddProcess(process);

                while (process != null)
                {
                    process.WaitForExit();

                    int code = 0;
                    try { code = process.ExitCode; }
                    catch { }

                    // The run goes on in a new process (state loaded, restart, title relaunch): follow it
                    if ((code & unchecked((int)0xC0000000)) == RunAgainFlag)
                    {
                        int pid = code & 0x3FFFFFFF;
                        process.Dispose();
                        process = null;

                        try { process = Process.GetProcessById(pid); }
                        catch { }

                        SimpleLogger.Instance.Info("[Generator] xbased goes on in process " + pid + (process == null ? " (already gone)" : ""));
                        RawExitCode = 0;
                        continue;
                    }

                    ret = ReportExitCode(process, path);
                    process.Dispose();
                    process = null;
                }

                // The fullscreen frame (another xbased.exe process) closes itself within 3 seconds and restores the display mode
                WaitForFrame();
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Error while waiting for xbased : " + ex.Message, ex);
            }

            bezel?.Dispose();
            _screenAnchor?.Dispose();
            _screenAnchor = null;

            // TerminateProcess on quit: crash codes on exit are not errors
            if (ret == 1 || ret == -1 || ret == unchecked((int)0xc0000005) || ret == unchecked((int)0xC000013A))
                return 0;

            return ret;
        }

        private void WaitForFrame()
        {
            var limit = DateTime.Now.AddSeconds(5);

            while (DateTime.Now < limit)
            {
                var others = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_exe));
                bool running = others.Length > 0;

                foreach (var p in others)
                    p.Dispose();

                if (!running)
                    return;

                Thread.Sleep(100);
            }

            SimpleLogger.Instance.Warning("[Generator] xbased frame still running after 5 seconds.");
        }
    }

    /// <summary>
    /// xbased.ini: one option a line, "key=value" or a flag alone, '#' comments.
    /// </summary>
    class XbasedIniFile
    {
        private readonly string _path;
        private readonly List<string> _lines;
        private bool _dirty;

        private XbasedIniFile(string path, List<string> lines)
        {
            _path = path;
            _lines = lines;
        }

        public static XbasedIniFile Load(string path)
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string> { "# xbased options, one a line (the command line's come after and win)" };
            return new XbasedIniFile(path, lines);
        }

        private static string KeyOf(string line)
        {
            string l = line.Trim();
            if (l.Length == 0 || l.StartsWith("#") || l.StartsWith(";"))
                return null;

            int eq = l.IndexOf('=');
            return (eq < 0 ? l : l.Substring(0, eq)).Trim();
        }

        private static string ValueOf(string line)
        {
            int eq = line.IndexOf('=');
            return eq < 0 ? "1" : line.Substring(eq + 1).Trim();
        }

        /// <summary>
        /// Single value: every former line of the key is replaced by one.
        /// </summary>
        public void Set(string key, string value)
        {
            var current = _lines.Where(l => KeyOf(l) == key).ToList();
            if (current.Count == 1 && ValueOf(current[0]) == value)
                return;

            _lines.RemoveAll(l => KeyOf(l) == key);
            _lines.Add(key + "=" + value);
            _dirty = true;
        }

        /// <summary>
        /// Repeated key (gamedir, gamefile): the value is added if missing.
        /// </summary>
        public void AddValue(string key, string value)
        {
            if (_lines.Any(l => KeyOf(l) == key && string.Equals(ValueOf(l).TrimEnd('\\', '/'), value.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                return;

            _lines.Add(key + "=" + value);
            _dirty = true;
        }

        public void Remove(string key)
        {
            if (_lines.RemoveAll(l => KeyOf(l) == key) > 0)
                _dirty = true;
        }

        public void Save()
        {
            if (!_dirty)
                return;

            File.WriteAllLines(_path, _lines);
            _dirty = false;
        }
    }

    /// <summary>
    /// Invisible window on the target screen, kept with its own message loop while the game runs:
    /// xbased creates its fullscreen frame on the screen of this window and gives it the foreground back on exit.
    /// </summary>
    class XbasedScreenAnchor : IDisposable
    {
        private AnchorForm _form;
        public IntPtr Handle { get; private set; }

        public static XbasedScreenAnchor Create(Screen screen)
        {
            if (screen == null)
                return null;

            var anchor = new XbasedScreenAnchor();
            var ready = new ManualResetEvent(false);

            var thread = new Thread(() =>
            {
                try
                {
                    var bounds = screen.Bounds;
                    anchor._form = new AnchorForm();
                    anchor._form.Bounds = new Rectangle(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 1, 1);
                    anchor._form.HandleCreated += (s, e) => { anchor.Handle = anchor._form.Handle; ready.Set(); };
                    Application.Run(anchor._form);
                }
                catch { ready.Set(); }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            if (!ready.WaitOne(2000) || anchor.Handle == IntPtr.Zero)
            {
                SimpleLogger.Instance.Warning("[Generator] xbased screen anchor could not be created.");
                return null;
            }

            return anchor;
        }

        public void Dispose()
        {
            try
            {
                if (_form != null && !_form.IsDisposed && _form.IsHandleCreated)
                    _form.BeginInvoke(new Action(() => _form.Close()));
            }
            catch { }

            _form = null;
        }

        class AnchorForm : Form
        {
            public AnchorForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                Opacity = 0;
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x80 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                    return cp;
                }
            }
        }
    }
}
