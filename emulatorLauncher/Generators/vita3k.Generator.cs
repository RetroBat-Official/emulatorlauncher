using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static EmulatorLauncher.Common.KeyboardInterceptor;

namespace EmulatorLauncher
{
    class Vita3kGenerator : Generator
    {
        private string _prefPath = "";
        private bool _fullscreen = true;
        Process _vita3kProcess = null;

        // Game installed by Vita3K at launch (first launch of a package), used to display the installation progress
        private string _installName = null;
        private string _installPath = null;
        private long _installSize = 0;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("vita3k");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "Vita3K.exe");
            if (!File.Exists(exe))
                return null;

            _fullscreen = ShouldRunFullscreen();

            // Vita3K portable mode: enabled by a 'portable' folder next to Vita3K.exe (config.yml, gui-configs and Vita FS 'fs' inside it)
            // Empty folder: remove it to use RetroBat paths. Folder with content: use it, pref-path is then forced to 'portable\fs' by Vita3K
            string configPath = path;
            string portablePath = Path.Combine(path, "portable");

            if (Directory.Exists(portablePath))
            {
                bool portableEmpty = false;
                try { portableEmpty = !Directory.EnumerateFileSystemEntries(portablePath).Any(); }
                catch { }

                if (portableEmpty)
                {
                    try
                    {
                        Directory.Delete(portablePath, false);
                        SimpleLogger.Instance.Info("[Generator] Empty Vita3K 'portable' folder removed.");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Unable to remove empty Vita3K 'portable' folder: " + ex.Message);
                    }
                }

                if (Directory.Exists(portablePath))
                {
                    configPath = portablePath;
                    _prefPath = Path.Combine(portablePath, "fs");
                    SimpleLogger.Instance.Info("[Generator] Vita3K portable mode detected, using '" + portablePath + "'.");

                    if (SystemConfig.isOptSet("vita3k_pref_path") && !string.IsNullOrEmpty(SystemConfig["vita3k_pref_path"]))
                        SimpleLogger.Instance.Warning("[WARNING] Vita3K portable mode: 'vita3k_pref_path' option is ignored by the emulator.");
                }
            }

            if (configPath == path && !GetVita3kPrefPath(path))
                _prefPath = Path.Combine(AppConfig.GetFullPath("saves"), "psvita", "vita3k");

            // Vita3K exits silently at startup when the pref-path folder does not exist
            try
            {
                if (!Directory.Exists(_prefPath))
                    Directory.CreateDirectory(_prefPath);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Unable to create Vita3K pref-path '" + _prefPath + "': " + ex.Message);
            }

            SimpleLogger.Instance.Info("[Generator] Setting '" + _prefPath + "' as content path for the emulator");

            CheckFirmware();

            /*
             * .m3u    : title ID of a game already installed in Vita3K
             * .psvita : game folder, installed the first time it is launched
             * .vpk    : game package, installed the first time it is launched
             */
            string ext = Path.GetExtension(rom).ToLowerInvariant();
            bool isPackage = ext == ".vpk" || ext == ".psvita";

            string gameID = GetGameTitleId(rom, ext);
            if (string.IsNullOrEmpty(gameID))
                SimpleLogger.Instance.Warning("[WARNING] No game title ID found for '" + rom + "'.");
            else
                SimpleLogger.Instance.Info("[Generator] Game title ID: " + gameID);

            bool installed = !string.IsNullOrEmpty(gameID) && Directory.Exists(Path.Combine(_prefPath, "ux0", "app", gameID));

            // Vita3K can only install folders and packages: .m3u and .psvita files need the game to be already installed
            if (!installed && (ext == ".m3u" || (ext == ".psvita" && !Directory.Exists(rom))))
                throw new ApplicationException("PS Vita game " + (gameID ?? Path.GetFileName(rom)) + " is not installed in Vita3K.");

            // Vita3K installs packages before showing any window: prepare a progress window for RunAndWait
            if (!installed && isPackage)
            {
                _installName = Path.GetFileNameWithoutExtension(rom.TrimEnd('\\', '/'));
                _installPath = string.IsNullOrEmpty(gameID) ? null : Path.Combine(_prefPath, "ux0", "app", gameID);
                _installSize = _installPath == null ? 0 : GetContentSize(rom);
                SimpleLogger.Instance.Info("[Generator] Vita3K will install '" + _installName + "' (" + _installSize + " bytes) before booting it.");
            }

            List<string> commandArray = new List<string>();

            // -F is ignored by Qt builds (fullscreen is driven by boot-apps-full-screen), kept for older builds
            // -w: do not let Vita3K rewrite config.yml at startup
            // -f -c: load the config.yml file generated below
            if (_fullscreen)
                commandArray.Add("-F");

            commandArray.Add("-w");
            commandArray.Add("-f");

            string configfile = Path.Combine(configPath, "config.yml");
            commandArray.Add("-c \"" + configfile + "\"");

            // Installed game: boot it with its title ID
            // Not installed: content path as positional argument, Vita3K installs the game then boots it
            if (installed)
                commandArray.Add("-r " + gameID);
            else if (isPackage)
                commandArray.Add("\"" + rom.TrimEnd('\\', '/') + "\"");

            string args = string.Join(" ", commandArray);

            SetupConfiguration(configfile);
            SetupGUIConfiguration(configPath);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                UseShellExecute = false,
            };
        }

        //Configure config.yml file
        private void SetupConfiguration(string configPpath)
        {
            // Missing keys are set to their default value by Vita3K: config.yml can be created from scratch, no template needed
            if (!File.Exists(configPpath))
                SimpleLogger.Instance.Info("[Generator] Vita3K config.yml not found, creating a new one.");

            var yml = YmlFile.Load(Path.Combine(configPpath));

            //write pref-path with emulator path
            yml["pref-path"] = _prefPath;

            //First tackle the GUI stuff
            yml["initial-setup"] = "true";
            yml["user-auto-connect"] = "true";
            yml["show-welcome"] = "false";
            yml["boot-apps-full-screen"] = _fullscreen ? "true" : "false";

            // Discord
            if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                yml["discord-rich-presence"] = "true";
            else
                yml["discord-rich-presence"] = "false";

            //System language
            BindFeature(yml, "sys-lang", "psvita_language", GetDefaultvitaLanguage());
            
            // Confirm button (1 = Cross, 0 = Circle) and PlayStation TV mode (local multiplayer)
            BindFeature(yml, "sys-button", "vita3k_enter_button", "1");
            BindBoolFeature(yml, "pstv-mode", "vita3k_pstv_mode", "true", "false");

            //Then the emulator options
            BindFeature(yml, "backend-renderer", "backend-renderer", "Vulkan");
            BindFeature(yml, "resolution-multiplier", "resolution_multiplier", "1.00");
            BindBoolFeature(yml, "disable-surface-sync", "disable_surfacesync", "true", "false");
            BindFeature(yml, "screen-filter", "vita_screenfilter", "Bilinear");
            BindBoolFeatureOn(yml, "v-sync", "vita_vsync", "true", "false");
            BindFeature(yml, "anisotropic-filtering", "anisotropic-filtering", "1");
            BindBoolFeatureOn(yml, "cpu-opt", "cpu_opt", "true", "false");
            BindBoolFeatureOn(yml, "async-pipeline-compilation", "async_pipeline_compilation", "true", "false");
            BindBoolFeatureOn(yml, "shader-cache", "shader_cache", "true", "false");
            BindBoolFeatureOn(yml, "texture-cache", "texture_cache", "true", "false");
            BindFeature(yml, "high-accuracy", "vita3k_high_accuracy", "true");
            BindBoolFeature(yml, "fps-hack", "vita3k_fpshack", "true", "false");
            BindBoolFeature(yml, "show-compile-shaders", "vita3k_showShaderCompile", "true", "false");
            yml["check-for-updates-mode"] = "0";

            // The missing firmware dialog blocks the launch (default button = Cancel): firmware is checked in the generator log instead
            yml["warn-missing-firmware"] = "false";

            //Performance overlay options
            if (SystemConfig.isOptSet("performance-overlay") && SystemConfig["performance-overlay"] != "false")
            {
                yml["performance-overlay"] = "true";
                yml["performance-overlay-detail"] = SystemConfig["performance-overlay"];
            }
            else
                yml["performance-overlay"] = "false";

            // Remove misspelled key written by previous versions of the generator
            yml.Remove("perfomance-overlay-detail");

            // LLE modules: Vita3K automatic mode already loads libhttp, libscemp4 and the other known modules
            // "0" = automatic mode (list cleared), not set = keep the settings made in the Vita3K interface
            if (SystemConfig.isOptSet("modules") && SystemConfig["modules"] == "0")
            {
                yml["modules-mode"] = "0";
                var lleModules = yml.GetOrCreateContainer("lle-modules");
                lleModules.Elements.Clear();
            }

            // Custom textures
            if (SystemConfig.getOptBoolean("vita_custom_textures"))
                yml["import-textures"] = "true";
            else
                yml["import-textures"] = "false";

            // Controls
            var buttonMap = yml.GetOrCreateContainer("controller-binds");
            buttonMap.Elements.Clear();
            
            /*var c1 = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            buttonMap.Elements.Add(new YmlElement() { Value = "- 0" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 1" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 2" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 3" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 4" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 5" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 6" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 7" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 8" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 9" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 10" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 11" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 12" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 13" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 14" });*/

            //save config file
            yml.Save();
        }

        private void SetupGUIConfiguration(string path)
        {
            string guiSettingsPath = Path.Combine(path, "gui-configs");
            if (!Directory.Exists(guiSettingsPath))
            {
                try
                {
                    Directory.CreateDirectory(guiSettingsPath);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error("Error creating gui-configs directory: " + ex.Message);
                    return;
                }
            }

            string guiConfigFile = Path.Combine(guiSettingsPath, "CurrentSettings.ini");

            using (var ini = new IniFile(guiConfigFile))
            {
                // Disable Qt dialogs that would block the launch or the exit (no gamepad navigation in Qt dialogs)
                ini.WriteValue("MainWindow", "confirmExitApp", "false");
                ini.WriteValue("MainWindow", "warnAdminPrivileges", "false");
            }
        }

        /// <summary>
        /// UI - console language (SceSystemParamLang)
        /// Japanese = 0, English (US) = 1, French = 2, Spanish = 3, German = 4, Italian = 5, Dutch = 6, Portuguese (PT) = 7,
        /// Russian = 8, Korean = 9, Chinese (Traditional) = 10, Chinese (Simplified) = 11, Finnish = 12, Swedish = 13,
        /// Danish = 14, Norwegian = 15, Polish = 16, Portuguese (BR) = 17, English (GB) = 18, Turkish = 19
        /// </summary>
        /// <returns></returns>
        private string GetDefaultvitaLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "jp", "0" },
                { "ja", "0" },
                { "en", "1" },
                { "fr", "2" },
                { "es", "3" },
                { "de", "4" },
                { "it", "5" },
                { "nl", "6" },
                { "pt", "7" },
                { "ru", "8" },
                { "ko", "9" },
                { "zh", "11" },
                { "fi", "12" },
                { "sv", "13" },
                { "da", "14" },
                { "nn", "15" },
                { "nb", "15" },
                { "no", "15" },
                { "pl", "16" },
                { "tr", "19" }
            };

            // Special cases
            if (SystemConfig["Language"] == "zh_TW")
                return "10";
            if (SystemConfig["Language"] == "zh_CN")
                return "11";
            if (SystemConfig["Language"] == "pt_BR")
                return "17";
            if (SystemConfig["Language"] == "en_GB")
                return "18";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }

            return "1";
        }

        private bool GetVita3kPrefPath(string path)
        {
            if (SystemConfig.isOptSet("vita3k_pref_path") && !string.IsNullOrEmpty(SystemConfig["vita3k_pref_path"]))
            {
                _prefPath = SystemConfig["vita3k_pref_path"].Replace("/", "\\");
                return true;
            }
            else
                return false;
        }

        #region Vita3K windows
        private const int WM_CLOSE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Close Vita3K through its Qt main window (stops the running game and exits the process), kill it if it does not exit.
        /// Closing only the game window would leave the Vita3K interface open.
        /// </summary>
        private static void CloseVita3k(Process p)
        {
            try
            {
                if (p == null || p.HasExited)
                    return;

                IntPtr mainWindow = GetVita3kWindows(p.Id).Where(w => IsVita3kMainWindowTitle(w.Value)).Select(w => w.Key).FirstOrDefault();
                if (mainWindow != IntPtr.Zero)
                    PostMessage(mainWindow, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                else
                    p.CloseMainWindow();

                if (p.WaitForExit(5000))
                    return;

                SimpleLogger.Instance.Warning("[Generator] Vita3K did not exit in time, killing process.");
                p.Kill();
            }
            catch { }
        }

        /// <summary>
        /// Visible top-level windows of the Vita3K process with their title
        /// </summary>
        private static List<KeyValuePair<IntPtr, string>> GetVita3kWindows(int processId)
        {
            try
            {
                return User32.FindHwnds(processId)
                    .Select(hWnd => new KeyValuePair<IntPtr, string>(hWnd, User32.GetWindowText(hWnd)))
                    .ToList();
            }
            catch
            {
                return new List<KeyValuePair<IntPtr, string>>();
            }
        }

        // Main window title is "Vita3K <version>"
        private static bool IsVita3kMainWindowTitle(string title)
        {
            return !string.IsNullOrEmpty(title) && title.StartsWith("Vita3K", StringComparison.OrdinalIgnoreCase) && !title.Contains(" | ");
        }

        // Game window title is "<game title> (<title ID>) | <status or renderer, fps...>"
        private static bool IsVita3kGameWindowTitle(string title)
        {
            return !string.IsNullOrEmpty(title) && title.Contains(" | ") && !title.StartsWith("Vita3K", StringComparison.OrdinalIgnoreCase);
        }
        #endregion

        #region Game and firmware detection
        /// <summary>
        /// Log missing firmware components (Vita3K checks vs0 and sa0 the same way: folder exists and is not empty)
        /// </summary>
        private void CheckFirmware()
        {
            try
            {
                foreach (var folder in new string[] { "vs0", "sa0" })
                {
                    string fwPath = Path.Combine(_prefPath, folder);
                    if (!Directory.Exists(fwPath) || !Directory.EnumerateFileSystemEntries(fwPath).Any())
                        SimpleLogger.Instance.Warning("[WARNING] Vita3K firmware component '" + folder + "' is not installed in '" + _prefPath + "', some games may not boot or display text correctly.");
                }
            }
            catch { }
        }

        /// <summary>
        /// Get the title ID of the game (e.g. PCSE00000)
        /// .m3u: first line of the file, folder: sce_sys\param.sfo, .vpk: [ID] in file name, then sce_sys/param.sfo inside the package
        /// </summary>
        private static string GetGameTitleId(string rom, string ext)
        {
            string titleId = null;

            try
            {
                if (ext == ".m3u")
                {
                    titleId = File.ReadAllLines(rom)
                        .Select(l => l.Trim())
                        .FirstOrDefault(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"));
                }
                else if (Directory.Exists(rom))
                {
                    string sfo = Path.Combine(rom, "sce_sys", "param.sfo");
                    if (File.Exists(sfo))
                        titleId = GetTitleIdFromSfo(File.ReadAllBytes(sfo));
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Unable to read title ID from '" + rom + "': " + ex.Message);
            }

            // Title ID between brackets in the file name, e.g. "Game [PCSE00000].vpk"
            if (string.IsNullOrEmpty(titleId))
            {
                var match = Regex.Match(Path.GetFileName(rom.TrimEnd('\\', '/')), @"\[([A-Za-z0-9]{9})\]");
                if (match.Success)
                    titleId = match.Groups[1].Value;
            }

            // Last resort for packages: read sce_sys/param.sfo inside the archive
            if (string.IsNullOrEmpty(titleId) && ext == ".vpk" && File.Exists(rom))
                titleId = GetTitleIdFromArchive(rom);

            return string.IsNullOrEmpty(titleId) ? null : titleId.Trim().ToUpperInvariant();
        }

        private static string GetTitleIdFromArchive(string archive)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "retrobat_vita3k_sfo");

            try
            {
                using (var zip = Zip.Open(archive))
                {
                    if (zip == null)
                        return null;

                    // Shortest path = param.sfo of the game itself, not of an embedded content
                    var entry = zip.Entries
                        .Where(e => !e.IsDirectory && e.Filename.Replace('\\', '/').EndsWith("sce_sys/param.sfo", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(e => e.Filename.Length)
                        .FirstOrDefault();

                    if (entry == null)
                        return null;

                    entry.Extract(tempDir);

                    string sfoFile = Path.Combine(tempDir, entry.Filename.Replace('/', '\\'));
                    if (File.Exists(sfoFile))
                        return GetTitleIdFromSfo(File.ReadAllBytes(sfoFile));
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Unable to read param.sfo from '" + archive + "': " + ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Read TITLE_ID from a PARAM.SFO buffer
        /// Header: magic "\0PSF", version, key table offset, data table offset, entry count
        /// Index entry (16 bytes): key offset (u16), data format (u16), data length (u32), data max length (u32), data offset (u32)
        /// </summary>
        private static string GetTitleIdFromSfo(byte[] data)
        {
            if (data == null || data.Length < 0x14 || data[0] != 0 || data[1] != 'P' || data[2] != 'S' || data[3] != 'F')
                return null;

            int keyTable = BitConverter.ToInt32(data, 0x08);
            int dataTable = BitConverter.ToInt32(data, 0x0C);
            int count = BitConverter.ToInt32(data, 0x10);

            for (int i = 0; i < count; i++)
            {
                int entry = 0x14 + i * 0x10;
                if (entry + 0x10 > data.Length)
                    break;

                int keyOffset = keyTable + BitConverter.ToUInt16(data, entry);
                int dataLength = BitConverter.ToInt32(data, entry + 0x04);
                int dataOffset = dataTable + BitConverter.ToInt32(data, entry + 0x0C);

                if (keyOffset < 0 || keyOffset >= data.Length)
                    continue;

                int keyEnd = Array.IndexOf(data, (byte)0, keyOffset);
                if (keyEnd < 0)
                    continue;

                if (Encoding.ASCII.GetString(data, keyOffset, keyEnd - keyOffset) != "TITLE_ID")
                    continue;

                if (dataOffset < 0 || dataLength < 0 || dataOffset + dataLength > data.Length)
                    return null;

                return Encoding.UTF8.GetString(data, dataOffset, dataLength).TrimEnd('\0').Trim();
            }

            return null;
        }

        /// <summary>
        /// Size of the content to install: uncompressed size of the package, or size of the folder
        /// </summary>
        private static long GetContentSize(string rom)
        {
            try
            {
                if (Directory.Exists(rom))
                    return new DirectoryInfo(rom).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

                // Zip.ListEntries only accepts .zip/.7z/.rar extensions: open the .vpk (zip archive) directly
                if (File.Exists(rom))
                {
                    using (var zip = Zip.Open(rom))
                        return zip == null ? 0 : zip.Entries.Where(e => !e.IsDirectory).Sum(e => e.Length);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Unable to get size of '" + rom + "': " + ex.Message);
            }

            return 0;
        }

        /// <summary>
        /// Installation progress (0-99), from the size already written in ux0\app\[title ID]
        /// </summary>
        private int GetInstallProgress()
        {
            if (_installSize <= 0 || string.IsNullOrEmpty(_installPath) || !Directory.Exists(_installPath))
                return 0;

            try
            {
                // new FileInfo(path).Length reads the current size, the size listed by the directory enumeration is only updated when the file is closed
                long written = Directory.EnumerateFiles(_installPath, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
                return (int)Math.Min(99, written * 100 / _installSize);
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "vita3k", InputKey.hotkey | InputKey.start,
                action: () =>
                {
                    var p = Process.GetProcessesByName("Vita3K").FirstOrDefault();
                    if (p != null)
                        CloseVita3k(p);
                });
        }

        /// <summary>
        /// Installation progress window, run on its own UI thread so that it stays responsive while RunAndWait waits for Vita3K
        /// </summary>
        private class InstallProgressWindow : IDisposable
        {
            private readonly System.Threading.Thread _thread;
            private readonly System.Threading.ManualResetEvent _ready = new System.Threading.ManualResetEvent(false);
            private InstallerFrm _frm;

            public InstallProgressWindow(string label, bool indeterminate, Func<int> getProgress)
            {
                _thread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        _frm = new InstallerFrm();
                        _frm.ShowProgress(label, indeterminate);

                        using (var timer = new System.Windows.Forms.Timer() { Interval = 1000 })
                        {
                            timer.Tick += (s, e) => _frm.SetProgress(getProgress());
                            timer.Start();

                            _ready.Set();
                            Application.Run(_frm);
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Unable to show installation progress: " + ex.Message);
                    }
                    finally
                    {
                        _ready.Set();
                    }
                });

                _thread.SetApartmentState(System.Threading.ApartmentState.STA);
                _thread.IsBackground = true;
                _thread.Start();
                _ready.WaitOne(5000);
            }

            public void Dispose()
            {
                try
                {
                    var frm = _frm;
                    if (frm != null && !frm.IsDisposed && frm.IsHandleCreated)
                        frm.BeginInvoke(new Action(frm.Close));
                }
                catch { }

                _thread.Join(2000);
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            try
            {
                var px = Process.Start(path);

                if (px == null)
                    return 0;

                using (var escHook = new KeyboardInterceptor(px, new KeyTrigger(Keys.Escape)))
                {
                    // Vita3K (Qt) keeps its main window open when the game window is closed (in-game exit, boot error...)
                    // Watch the game window and close the emulator once it is gone, to return to the frontend
                    bool gameWindowSeen = false;
                    int gameWindowMissingTicks = 0;

                    // First launch of a package: Vita3K installs it without any window, show the progress meanwhile
                    InstallProgressWindow installProgress = null;
                    if (!string.IsNullOrEmpty(_installName))
                        installProgress = new InstallProgressWindow(Properties.Resources.Installing + "\r\n" + _installName, _installSize <= 0, GetInstallProgress);

                    while (!px.WaitForExit(500))
                    {
                        var windows = GetVita3kWindows(px.Id);

                        // A Vita3K window is shown once the installation is over
                        if (installProgress != null && windows.Any(w => !string.IsNullOrEmpty(w.Value)))
                        {
                            installProgress.Dispose();
                            installProgress = null;
                        }

                        IntPtr mainWindow = windows.Where(w => IsVita3kMainWindowTitle(w.Value)).Select(w => w.Key).FirstOrDefault();
                        if (mainWindow != IntPtr.Zero)
                            escHook.TargetHwnd = mainWindow;

                        if (windows.Any(w => IsVita3kGameWindowTitle(w.Value)))
                        {
                            gameWindowSeen = true;
                            gameWindowMissingTicks = 0;
                        }
                        else if (gameWindowSeen && mainWindow != IntPtr.Zero && windows.Count(w => !string.IsNullOrEmpty(w.Value)) == 1)
                        {
                            // Only the main window is left (no error dialog to read): close the emulator after ~3 seconds
                            gameWindowMissingTicks++;
                            if (gameWindowMissingTicks >= 6)
                            {
                                SimpleLogger.Instance.Info("[Generator] Vita3K game window closed, exiting emulator.");
                                CloseVita3k(px);
                                break;
                            }
                        }
                        else
                            gameWindowMissingTicks = 0;
                    }

                    if (installProgress != null)
                    {
                        installProgress.Dispose();
                        installProgress = null;
                    }

                    px.WaitForExit();
                    SimpleLogger.Instance.Info("[Generator] Process exited with code " + px.ExitCode);
                    int exitCode = px.ExitCode;

                    foreach (var p in Process.GetProcessesByName("Vita3K").Where(p => !p.HasExited))
                        try { p.CloseMainWindow(); } catch { }

                    foreach (var p in Process.GetProcessesByName("Vita3K").Where(p => !p.HasExited))
                        try { p.WaitForExit(2000); } catch { }

                    foreach (var p in Process.GetProcessesByName("Vita3K").Where(p => !p.HasExited))
                        try { if (!p.HasExited) p.Kill(); } catch { }

                    return 0;
                }
            }
            catch { }

            return 0;
        }
    }
}
