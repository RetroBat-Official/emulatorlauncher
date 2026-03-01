using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        public ExeLauncherGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _systemName;
        private string _exename = null;         // variable used for EL to track the game process
        private bool _isGameExePath = false;    // true if the target of a link or the the .game file points to an existing executable
        private bool _steamRun = false;         // true when Steam URL is detected, to add -silent argument
        private bool _gameExeFile = false;      // true if the process name was specified in the .gameexe file, to avoid override of the process name
        private bool _nonSteam = false;         // true when Steam executable name was catched in icon file
        private bool _batfile = false;          // true if extension is .bet or .cmd
        private bool _batfileNoWait = false;    // true if executable is found in .bat file (and feature to search for exe in .bat is not disabled)

        private GameLauncher _gameLauncher;

        static readonly Dictionary<string, Func<Uri, GameLauncher>> launchers = new Dictionary<string, Func<Uri, GameLauncher>>()
        {
            { "file", (uri) => new LocalFileGameLauncher(uri) },
            { "com.epicgames.launcher", (uri) => new EpicGameLauncher(uri) },
            { "steam", (uri) => new SteamGameLauncher(uri) },
            { "amazon-games", (uri) => new AmazonGameLauncher(uri) },
            { "goggalaxy", (uri) => new GogGameLauncher(uri) },
        };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (Path.GetExtension(rom).ToLower() == ".wsquashfs")
                _gameExeFile = GetProcessFromFile(rom);

            _systemName = system.ToLowerInvariant();

            rom = this.TryUnZipGameIfNeeded(system, rom);
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            string path = Path.GetDirectoryName(rom);
            string arguments = null;
            string extension = Path.GetExtension(rom);

            // Manage link files
            if (extension == ".lnk")
            {
                var result = HandlelinkFile(rom);

                rom = result.Rom;
                path = result.WorkingDirectory;
                arguments = result.Arguments;
            }

            // Manage store games with .url extensions (Steam, Epic, ...)
            else if (extension == ".url")
            {
                var result = HandleUrlFile(rom);

                if (result.Error)
                    return null;

                _gameExeFile = result.GameExeFile;
                _gameLauncher = result.Launcher;
                _steamRun = result.SteamRun;
                _nonSteam = result.NonSteam;
                _exename = result.ExeName;
            }

            else if (extension == ".game" && File.Exists(rom))
            {
                var gameResult = HandleGameFile(rom, path);

                rom = gameResult.Rom;
                path = gameResult.WorkingDirectory;
                arguments = gameResult.Arguments;
                _isGameExePath = gameResult.IsGameExe;
            }

            // If rom is a folder
            if (Directory.Exists(rom))
            {
                var folderResult = HandleFolder(rom, ref arguments);

                rom = folderResult.Rom;
                path = folderResult.WorkingDirectory;
                _gameExeFile = folderResult.GameExeFile;
            }

            // At that point, return if rom file does not exist
            if (!File.Exists(rom) && !_steamRun)
                return null;

            // Manage .m3u extension
            if (extension == ".m3u")
            {
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(path, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            UpdateMugenConfig(path, fullscreen, resolution);
            UpdateIkemenConfig(path, system, rom, fullscreen, resolution, emulator);

            var ret = new ProcessStartInfo()
            {
                FileName = rom,
                WorkingDirectory = path
            };

            if (arguments != null)
                ret.Arguments = arguments;

            // If rom is a batch or command file, try to get executable inside the file
            string ext = Path.GetExtension(rom).ToLower();
            if (ext == ".bat" || ext == ".cmd")
            {
                if (GetExecutableName(rom, out string exeName, out string targetExe) && !_gameExeFile)
                {
                    if (File.Exists(targetExe))
                    {
                        ret.FileName = targetExe;
                        ret.WorkingDirectory = Path.GetDirectoryName(targetExe);
                    }
                    _exename = exeName;
                    _batfileNoWait = true;
                    SimpleLogger.Instance.Info("[INFO] Executable name found in batch file: " + _exename);
                }

                _batfile = true;
                //ret.WindowStyle = ProcessWindowStyle.Hidden;
                ret.UseShellExecute = true;
            }
            else if (string.IsNullOrEmpty(_exename) && _gameLauncher == null)
            {
                _exename = Path.GetFileNameWithoutExtension(rom);
                SimpleLogger.Instance.Info("[INFO] Executable name : " + _exename);
            }

            // If game was uncompressed, say we are going to launch, so the deletion will not be silent
            ValidateUncompressedGame();

            // Configure guns if needed
            ConfigureExeLauncherGuns(system, rom);

            return ret;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_isGameExePath || _gameExeFile)
                return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");

            else if (_gameLauncher != null) 
                return _gameLauncher.SetupCustomPadToKeyMapping(mapping);

            else if ((_systemName != "mugen" || _systemName != "ikemen") && string.IsNullOrEmpty(_exename))
                return mapping;

            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

        #region mugen_ikemen
        private void UpdateMugenConfig(string path, bool fullscreen, ScreenResolution resolution)
        {
            if (_systemName != "mugen")
                return;

            var cfg = Path.Combine(path, "data", "mugen.cfg");
            if (!File.Exists(cfg))
                return;

            if (resolution == null)
                resolution = ScreenResolution.CurrentResolution;

            using (var ini = IniFile.FromFile(cfg, IniOptions.UseSpaces | IniOptions.AllowDuplicateValues | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                var inisections = ini.EnumerateSections().Where(s => !s.StartsWith("Sound") && !s.StartsWith("Input") && !s.StartsWith("P1") && !s.StartsWith("P2"));

                foreach (var section in inisections)
                {
                    var height = ini.EnumerateKeys(section).Where(s => s.Contains("Height")).FirstOrDefault();
                    var width = ini.EnumerateKeys(section).Where(s => s.Contains("Width")).FirstOrDefault();

                    if (height != null)
                        ini.WriteValue(section, height, resolution.Height.ToString());
                    if (width != null)
                        ini.WriteValue(section, width, resolution.Width.ToString());

                    var vretrace = ini.EnumerateKeys(section).Where(s => s.Contains("VRetrace")).FirstOrDefault();
                    if (vretrace != null)
                        BindBoolIniFeatureOn(ini, section, vretrace, "VRetrace", "1", "0");

                    var fs = ini.EnumerateKeys(section).Where(s => s.Equals("FullScreen")).FirstOrDefault();
                    if (fs != null)
                        ini.WriteValue(section, fs, fullscreen ? "1" : "0");
                }
            }
        }

        private void UpdateIkemenConfig(string path, string system, string rom, bool fullscreen, ScreenResolution resolution, string emulator)
        {
            if (_systemName != "ikemen")
                return;

            ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator);

            string jsonConfFile = Path.Combine(path, "save", "config.json");
            string iniFile = Path.Combine(path, "save", "config.ini");
            
            if (!File.Exists(jsonConfFile) && !File.Exists(iniFile))
                return;

            string width = resolution == null ? ScreenResolution.CurrentResolution.Width.ToString() : resolution.Width.ToString();
            string height = resolution == null ? ScreenResolution.CurrentResolution.Height.ToString() : resolution.Height.ToString();

            if (SystemConfig.isOptSet("resolution") && !string.IsNullOrEmpty(SystemConfig["resolution"]) && SystemConfig["resolution"].Split('_').Length > 1)
            {
                width = SystemConfig["resolution"].Split('_')[0];
                height = SystemConfig["resolution"].Split('_')[1];
            }

            if (resolution == null)
                resolution = ScreenResolution.CurrentResolution;


            // Some games use config.json
            if (File.Exists(jsonConfFile))
            {
                var json = DynamicJson.Load(jsonConfFile);

                if (json.IsDefined("FirstRun"))
                    json["FirstRun"] = "false";

                if (json.IsDefined("Fullscreen"))
                    json["Fullscreen"] = fullscreen ? "true" : "false";

                if (json.IsDefined("Borderless"))
                    json["Borderless"] = SystemConfig.getOptBoolean("exclusivefs") ? "false" : "true";

                if (json.IsDefined("GameWidth"))
                {
                    if (SystemConfig.isOptSet(SystemConfig["resolution"]) && SystemConfig["resolution"] == "screen")
                    {
                        json["GameWidth"] = resolution.Width.ToString();
                        json["GameHeight"] = resolution.Height.ToString();
                    }
                    else if (SystemConfig.isOptSet(SystemConfig["resolution"]))
                    {
                        json["GameWidth"] = width;
                        json["GameHeight"] = height;
                    }
                }

                if (json.IsDefined("VRetrace"))
                    BindBoolFeatureOn(json, "VRetrace", "VRetrace", "1", "0");

                if (json.IsDefined("MSAA"))
                    BindBoolFeature(json, "MSAA", "ikemen_msaa", "true", "false");

                json.Save();

                return;
            }

            // Some games use config.ini
            if (File.Exists(iniFile))
            {
                using (var ini = IniFile.FromFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyLines))
                {
                    if (ini.KeyExists("Config", "FirstRun"))
                        ini.WriteValue("Config", "FirstRun", "0");

                    if (ini.KeyExists("Video", "Fullscreen"))
                        ini.WriteValue("Video", "Fullscreen", fullscreen ? "1" : "0");

                    if (ini.KeyExists("Video", "Borderless"))
                        ini.WriteValue("Video", "Borderless", SystemConfig.getOptBoolean("exclusivefs") ? "0" : "1");

                    if (ini.KeyExists("Video", "GameWidth"))
                    {
                        if (SystemConfig.isOptSet(SystemConfig["resolution"]) && SystemConfig["resolution"] == "screen")
                        {
                            ini.WriteValue("Video", "GameWidth", resolution.Width.ToString());
                            ini.WriteValue("Video", "GameHeight", resolution.Height.ToString());
                        }
                        else if (SystemConfig.isOptSet(SystemConfig["resolution"]))
                        {
                            ini.WriteValue("Video", "GameWidth", width);
                            ini.WriteValue("Video", "GameHeight", height);
                        }
                    }

                    if (ini.KeyExists("Video", "VSync"))
                        BindBoolIniFeatureOn(ini, "Video", "VSync", "VRetrace", "1", "0");

                    if (ini.KeyExists("Video", "MSAA"))
                        BindBoolIniFeature(ini, "Video", "MSAA", "ikemen_msaa", "1", "0");
                }
            }
        }
        #endregion

        readonly string[] launcherPprocessNames = { "Amazon Games UI", "EADesktop", "EpicGamesLauncher", "steam", "GalaxyClient" };

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_isGameExePath || _gameExeFile || _nonSteam)
            {
                Dictionary<string, bool> launcherProcessStatusBefore = new Dictionary<string, bool>();
                Dictionary<string, bool> launcherProcessStatusAfter = new Dictionary<string, bool>();
                
                foreach (string processName in launcherPprocessNames)
                {
                    bool uiExists = Process.GetProcessesByName(processName).Any();

                    if (uiExists)
                    {
                        SimpleLogger.Instance.Info("[INFO] Launcher: " + processName + " found running.");
                        launcherProcessStatusBefore.Add(processName, true);
                    }
                }

                int waitttime = 30;
                if (Program.SystemConfig.isOptSet("steam_wait") && !string.IsNullOrEmpty(Program.SystemConfig["steam_wait"]))
                    waitttime = Program.SystemConfig["steam_wait"].ToInteger();
                SimpleLogger.Instance.Info("[INFO] Starting process, waiting " + waitttime.ToString() + " seconds for the game to run before returning to Game List");

                Process process = Process.Start(path);
                
                SimpleLogger.Instance.Info("Process started : " + _exename);
                
                Thread.Sleep(4000);

                int i = 1;

                Process[] gamelist = Process.GetProcessesByName(_exename);

                while (i <= waitttime && gamelist.Length == 0)
                {
                    gamelist = Process.GetProcessesByName(_exename);
                    Thread.Sleep(1000);
                    i++;
                }

                if (gamelist.Length == 0)
                {
                    SimpleLogger.Instance.Info("Process : " + _exename + " not running.");

                    var gameProcess = FindGameProcessByWindowFocus();
                    if (gameProcess != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Game process '" + gameProcess.ProcessName + "' identified by window focus. Monitoring process.");
                        Job.Current.AddProcess(gameProcess);
                        gameProcess.WaitForExit();
                        SimpleLogger.Instance.Info("[INFO] Game process has exited.");

                        foreach (string processName in launcherPprocessNames)
                        {
                            bool uihasStarted = Process.GetProcessesByName(processName).Any();

                            if (uihasStarted)
                                launcherProcessStatusAfter.Add(processName, true);
                        }
                        KillLauncher(launcherProcessStatusAfter, launcherProcessStatusBefore);
                    }
                    else
                        SimpleLogger.Instance.Info("[INFO] All fallback methods failed. Unable to monitor game process.");

                    return 0;
                }

                else
                {
                    foreach (string processName in launcherPprocessNames)
                    {
                        bool uihasStarted = Process.GetProcessesByName(processName).Any();

                        if (uihasStarted)
                            launcherProcessStatusAfter.Add(processName, true);
                    }

                    SimpleLogger.Instance.Info("Process : " + _exename + " found, waiting to exit");
                    Process game = gamelist.OrderBy(p => p.StartTime).FirstOrDefault();

                    if (game != null)
                        Job.Current.AddProcess(game);
                    
                    game.WaitForExit();
                }

                KillLauncher(launcherProcessStatusAfter, launcherProcessStatusBefore);
                return 0;
            }

            else if ((!_batfile || _batfileNoWait) && (_systemName == "windows" || _gameLauncher != null))
            {
                using (var frm = new System.Windows.Forms.Form())
                {
                    // Some games fail to allocate DirectX surface if EmulationStation is showing fullscren : pop an invisible window between ES & the game solves the problem
                    frm.ShowInTaskbar = false;
                    frm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
                    frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                    frm.Opacity = 0;
                    frm.Show();

                    System.Windows.Forms.Application.DoEvents();

                    if (_gameLauncher != null)
                    {
                        path.UseShellExecute = true;
                        return _gameLauncher.RunAndWait(path);
                    }

                    else
                    {
                        int waitttime = 30;
                        if (Program.SystemConfig.isOptSet("steam_wait") && !string.IsNullOrEmpty(Program.SystemConfig["steam_wait"]))
                            waitttime = Program.SystemConfig["steam_wait"].ToInteger();
                        SimpleLogger.Instance.Info("[INFO] Starting process, waiting " + waitttime.ToString() + " seconds for the game to run before returning to Game List");

                        Process process = Process.Start(path);
                        
                        SimpleLogger.Instance.Info("Process started : " + _exename);

                        Thread.Sleep(4000);

                        int i = 1;

                        Process[] gamelist = Process.GetProcessesByName(_exename);

                        while (i <= waitttime && gamelist.Length == 0)
                        {
                            gamelist = Process.GetProcessesByName(_exename);
                            Thread.Sleep(1000);
                            i++;
                        }

                        if (gamelist.Length == 0)
                        {
                            SimpleLogger.Instance.Info("Process : " + _exename + " not running.");

                            var gameProcess = FindGameProcessByWindowFocus();
                            if (gameProcess != null)
                            {
                                SimpleLogger.Instance.Info("[INFO] Game process '" + gameProcess.ProcessName + "' identified by window focus. Monitoring process.");
                                Job.Current.AddProcess(gameProcess);
                                gameProcess.WaitForExit();
                                SimpleLogger.Instance.Info("[INFO] Game process has exited.");
                            }
                            else
                                SimpleLogger.Instance.Info("[INFO] All fallback methods failed. Unable to monitor game process.");

                            return 0;
                        }
                        else
                        {
                            SimpleLogger.Instance.Info("Process : " + _exename + " found, waiting to exit");
                            var jobToAdd = Process.GetProcessesByName(_exename).FirstOrDefault();
                            
                            if (jobToAdd != null)
                                Job.Current.AddProcess(jobToAdd);

                            while (Process.GetProcessesByName(_exename).Any())
                            {
                                Thread.Sleep(1000);
                            }
                        }
                        return 0;
                    }
                }
            }

            else
                base.RunAndWait(path);

            return 0;
        }

        private static Process FindGameProcessByWindowFocus()
        {
            SimpleLogger.Instance.Info("[INFO] Trying to find game process by window focus.");

            // Wait for initial window to appear (e.g., a launcher)
            System.Threading.Thread.Sleep(10000); // 10 seconds

            IntPtr hWnd = User32.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;

            User32.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return null;

            Process candidateProcess;
            try { candidateProcess = Process.GetProcessById((int)pid); }
            catch { return null; }

            SimpleLogger.Instance.Info("[INFO] Initial process candidate: " + candidateProcess.ProcessName);

            // Wait longer for the actual game to take over from the launcher
            System.Threading.Thread.Sleep(20000); // 20 more seconds

            hWnd = User32.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;

            User32.GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0) return null;

            Process finalProcess;
            try { finalProcess = Process.GetProcessById((int)pid); }
            catch { return null; }

            SimpleLogger.Instance.Info("[INFO] Final process candidate: " + finalProcess.ProcessName);

            // Check if the final process is fullscreen
            User32.GetWindowRect(hWnd, out RECT windowRect);

            int screenWidth = User32.GetSystemMetrics(User32.SM_CXSCREEN);
            int screenHeight = User32.GetSystemMetrics(User32.SM_CYSCREEN);

            bool isFullscreen = (windowRect.left == 0 && windowRect.top == 0 &&
                                 windowRect.right == screenWidth && windowRect.bottom == screenHeight);

            if (isFullscreen)
            {
                SimpleLogger.Instance.Info("[INFO] Final process '" + finalProcess.ProcessName + "' is fullscreen. Selecting it.");
                return finalProcess;
            }

            SimpleLogger.Instance.Info("[INFO] Final process is not fullscreen. Detection by focus failed.");
            return null;
        }

        private void KillLauncher(Dictionary<string, bool> launcherProcessAfter, Dictionary<string, bool> launcherProcessBefore)
        {
            foreach (var processName in launcherPprocessNames) // always kill launchers
            {
                if (Program.SystemConfig.getOptBoolean("killsteam"))
                {
                    foreach (var ui in Process.GetProcessesByName(processName))
                    {
                        try
                        {
                            SimpleLogger.Instance.Info("[INFO] Option set to always kill launchers, killing process " + processName);
                            ui.Kill();
                        }
                        catch { }
                    }
                }
                else if (SystemConfig.isOptSet("killsteam")) // do not kill launchers
                {
                    SimpleLogger.Instance.Info("[INFO] Option set to NOT kill launcher process.");
                    return;
                }
                else // kill launcher processes only if they were started with the game
                {
                    if (launcherProcessAfter.ContainsKey(processName) && !launcherProcessBefore.ContainsKey(processName))
                    {
                        foreach (var ui in Process.GetProcessesByName(processName))
                        {
                            try
                            {
                                SimpleLogger.Instance.Info("[INFO] Killing process " + processName);
                                ui.Kill();
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private bool GetExecutableName(string batFilePath, out string executableName, out string targetExe)
        {
            executableName = null;
            targetExe = null;

            if (SystemConfig.isOptSet("batexesearch") && !SystemConfig.getOptBoolean("batexesearch"))
                return false;

            var content = File.ReadAllText(batFilePath);

            var match = Regex.Match(content, @"(?:""([^""]+\.exe)""|([^\s]+\.exe))", RegexOptions.IgnoreCase);
            if (match.Success && !content.Contains("tasklist|findstr"))
            {
                var path = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (path != null)
                {
                    targetExe = Path.Combine(Path.GetDirectoryName(batFilePath), path);
                    executableName = Path.GetFileNameWithoutExtension(path);
                }
                return true;
            }
            return false;
        }

        // If .gameexe is used, the function to get the process name via launcher specific search is disabled
        private bool GetProcessFromFile(string rom)
        {
            string dir = Path.GetDirectoryName(rom);
            string file = Path.GetFileNameWithoutExtension(rom);

            if (string.IsNullOrEmpty(dir))
                dir = rom;

            if (string.IsNullOrEmpty(file))
                file = "default";

            string executableFile = Path.Combine(dir, file + ".gameexe");

            if (!File.Exists(executableFile))
                return false;

            var lines = File.ReadAllLines(executableFile);
            if (lines.Length < 1)
                return false;
            else
            {
                string line = FileTools.ReadFirstValidLine(executableFile);
                if (line.ToLowerInvariant().EndsWith(".exe"))
                    line = line.Substring(0, line.Length - 4);

                _exename = line;
                SimpleLogger.Instance.Info("[INFO] Executable name specified in .gameexe file: " + _exename);
                return true;
            }
        }

        #region LinkFiles
        private class LinkResolutionResult
        {
            public string Rom;
            public string WorkingDirectory;
            public string Arguments;
        }
        private LinkResolutionResult HandlelinkFile(string rom)
        {
            var result = new LinkResolutionResult
            {
                Rom = rom,
                WorkingDirectory = Path.GetDirectoryName(rom)
            };

            SimpleLogger.Instance.Info("[INFO] link file, searching for target.");
            string target = FileTools.GetShortcutTargetwsh(rom);
            result.Arguments = FileTools.GetShortcutArgswsh(rom);

            // GOG Galaxy special coase
            if (!string.IsNullOrEmpty(target))
            {
                string exeName = Path.GetFileNameWithoutExtension(target);
                
                if (exeName.Equals("GalaxyClient", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(result.Arguments ?? "", @"/gameId=(\d+)");
                    if (match.Success)
                    {
                        string gameId = match.Groups[1].Value;
                        var uri = new Uri($"goggalaxy:{gameId}");

                        if (launchers.TryGetValue(uri.Scheme, out Func<Uri, GameLauncher> gameLauncherInstanceBuilder))
                        {
                            _gameLauncher = gameLauncherInstanceBuilder(uri);
                            return result;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(target) && File.Exists(target))
            {
                _isGameExePath = true;
                _gameExeFile = GetProcessFromFile(rom);

                result.Rom = target;
                result.WorkingDirectory = Path.GetDirectoryName(target);

                SimpleLogger.Instance.Info("[INFO] Link target file found.");
                SimpleLogger.Instance.Info("[INFO] New ROM : " + result.Rom);

                return result;
            }

            // if the target is not found in the link, see if a .gameexe file or a .uwp file exists
            // First case : user has directly specified the executable name in a .gameexe file
            _gameExeFile = GetProcessFromFile(rom);

            // Second case : user has specified the UWP app name in a .uwp file
            if (!_gameExeFile)
                TryResolveUwpExecutable(rom);

            if (!_gameExeFile)
                SimpleLogger.Instance.Info("[INFO] Impossible to find executable name, using rom file name.");

            return result;
        }

        private void TryResolveUwpExecutable(string rom)
        {
            string uwpexecutableFile = Path.Combine(Path.GetDirectoryName(rom), Path.GetFileNameWithoutExtension(rom) + ".uwp");

            if (!File.Exists(uwpexecutableFile))
                return;

            var romLines = File.ReadAllLines(uwpexecutableFile);
            if (romLines.Length == 0)
                return;

            string uwpAppName = romLines[0];
            var fileStream = GetStoreAppInfo(uwpAppName);

            if (string.IsNullOrEmpty(fileStream))
                return;

            var installLocation = ExtractUWPInstallLocation(fileStream);
            if (string.IsNullOrEmpty(installLocation))
                return;

            string appManifest = Path.Combine(installLocation, "AppxManifest.xml");
            if (!File.Exists(appManifest))
                return;

            var doc = XDocument.Load(appManifest);
            var app = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Application");

            var exePath = app?.Attribute("Executable")?.Value;

            if (string.IsNullOrEmpty(exePath))
                return;

            _exename = Path.GetFileNameWithoutExtension(exePath);
            _gameExeFile = true;
            SimpleLogger.Instance.Info("[INFO] Executable name found for UWP app: " + _exename);
        }

        static string GetStoreAppInfo(string appName)
        {
            // PowerShell Process Start
            Process process = new Process();
            process.StartInfo.FileName = "powershell.exe";
            process.StartInfo.Arguments = $"-Command (Get-AppxPackage -Name {appName} | Select Installlocation)";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // Read Result
            string output = process.StandardOutput.ReadToEnd();

            // Process End
            process.WaitForExit();

            return output;
        }

        private string ExtractUWPInstallLocation(string fileStream)
        {
            if (string.IsNullOrEmpty(fileStream))
                return null;

            var lines = fileStream.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("InstallLocation"))
                {
                    int valueIndex = i + 2;

                    if (valueIndex < lines.Length)
                        return lines[valueIndex];

                    return null;
                }
            }

            return null;
        }
        #endregion

        #region urlFiles
        private class UrlResolutionResult
        {
            public bool GameExeFile;
            public GameLauncher Launcher;
            public bool SteamRun;
            public bool NonSteam;
            public string ExeName;
            public bool Error;
        }

        private UrlResolutionResult HandleUrlFile(string rom)
        {
            var result = new UrlResolutionResult();

            // executable process to monitor might be different from the target - user can specify true process executable in a .gameexe file
            result.GameExeFile = GetProcessFromFile(rom);

            if (!result.GameExeFile)
            {
                try
                {
                    var url = IniFile.FromFile(rom).GetValue("InternetShortcut", "URL");

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        var uri = new Uri(url);

                        if (launchers.TryGetValue(uri.Scheme, out var builder))
                            result.Launcher = builder(uri);
                    }
                }
                catch (Exception ex)
                {
                    SetCustomError(ex.Message);
                    SimpleLogger.Instance.Error("[ExeLauncherGenerator] " + ex.Message, ex);
                    result.Error = true;
                    return result;
                }
            }

            // Run Steam games via their shortcuts and not just run the .url file
            HandleSteamUrl(rom, result);
            
            return result;
        }

        private void HandleSteamUrl(string rom, UrlResolutionResult result)
        {
            var urlLines = File.ReadAllLines(rom);

            if (urlLines.Length == 0)
                return;

            // Get URL to run and add -silent argument
            var steamLineIndex = Array.FindIndex(urlLines, l => l.StartsWith("URL=steam://rungameid", StringComparison.OrdinalIgnoreCase));

            if (steamLineIndex >= 0)
            {
                result.SteamRun = true;

                if (!urlLines[steamLineIndex].Contains("-silent"))
                {
                    urlLines[steamLineIndex] += " -silent";
                    try { File.WriteAllLines(rom, urlLines); }
                    catch { }
                }
            }

            // Get executable name from icon path
            if (string.IsNullOrEmpty(result.ExeName))
            {
                var iconLine = urlLines.FirstOrDefault(l => l.StartsWith("IconFile", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(iconLine))
                {
                    var parts = iconLine.Split('=');
                    if (parts.Length == 2 && parts[1].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var iconPath = parts[1].Trim();
                        result.ExeName = Path.GetFileNameWithoutExtension(iconPath);

                        if (!string.IsNullOrEmpty(result.ExeName))
                        {
                            result.NonSteam = true;
                            SimpleLogger.Instance.Info("[STEAM] Found name of executable from icon info: " + result.ExeName);
                        }
                    }
                }
            }
        }
        #endregion

        #region dotGameFiles
        private class GameFileResolutionResult
        {
            public string Rom;
            public string WorkingDirectory;
            public string Arguments;
            public bool IsGameExe;
        }

        private GameFileResolutionResult HandleGameFile(string rom, string path)
        {
            var result = new GameFileResolutionResult
            {
                Rom = rom,
                WorkingDirectory = path
            };

            var lines = File.ReadAllLines(rom);

            if (lines.Length == 0)
                throw new Exception("No path specified in .game file.");

            string target = lines[0];

            if (target.StartsWith(".\\") || target.StartsWith("./"))
                target = Path.Combine(path, target.Substring(2));
            else if (target.StartsWith("\\") || target.StartsWith("/"))
                target = Path.Combine(path, target.Substring(1));

            if (lines.Length > 1)
                result.Arguments = string.Join(" ", lines.Skip(1));

            if (!File.Exists(target))
                throw new Exception("Target file " + target + " does not exist.");

            result.IsGameExe = File.Exists(target);

            if (result.IsGameExe)
            {
                _gameExeFile = GetProcessFromFile(rom);
                result.Rom = target;
                result.WorkingDirectory = Path.GetDirectoryName(target);
            }

            return result;
        }
        #endregion

        #region folderGames
        private class FolderResolutionResult
        {
            public string Rom;
            public string WorkingDirectory;
            public string Arguments;
            public bool GameExeFile;
        }

        private FolderResolutionResult HandleFolder(string folderPath, ref string arguments)
        {
            var result = new FolderResolutionResult
            {
                Rom = folderPath,
                WorkingDirectory = folderPath
            };

            if (!Directory.Exists(folderPath))
                return result;

            _gameExeFile = GetProcessFromFile(folderPath);

            string[] possibleAutoruns = new[]
            {
                "autorun.cmd",
                "autorun.bat",
                "autoexec.cmd",
                "autoexec.bat"
            };

            foreach (var file in possibleAutoruns)
            {
                string fullPath = Path.Combine(folderPath, file);
                if (File.Exists(fullPath))
                {
                    result.Rom = fullPath;
                    break;
                }
            }
            
            if (result.Rom == folderPath)
                result.Rom = Directory.GetFiles(folderPath, "*.exe")
                    .FirstOrDefault(f => !f.ToLowerInvariant().Contains("uninst"));

            result.WorkingDirectory = Path.GetDirectoryName(result.Rom);

            // Cas spécial autorun.cmd
            if (Path.GetFileName(result.Rom).Equals("autorun.cmd", StringComparison.OrdinalIgnoreCase))
                TryResolveAutorunCmd(ref result.Rom, ref result.WorkingDirectory, ref arguments);

            return result;
        }
        private void TryResolveAutorunCmd(ref string autorunPath, ref string path, ref string arguments)
        {
            path = Path.GetFullPath(path);

            var lines = File.ReadAllLines(autorunPath);
            if (lines.Length == 0)
                throw new Exception("autorun.cmd is empty");

            string dir = lines.FirstOrDefault(l => l.StartsWith("DIR="))?.Substring(4);
            string cmd = lines.FirstOrDefault(l => l.StartsWith("CMD="))?.Substring(4);

            if (string.IsNullOrEmpty(cmd) && lines.Length > 0)
                cmd = lines[0];

            var args = cmd.SplitCommandLine();
            if (args.Length == 0)
                throw new Exception("Invalid autorun.cmd command");

            string exe = string.IsNullOrEmpty(dir)
                ? Path.Combine(path, args[0])
                : Path.Combine(path, dir.Replace("/", "\\"), args[0]);

            if (!File.Exists(exe))
                throw new Exception("Invalid autorun.cmd executable");

            // Mise à jour du chemin et des arguments
            autorunPath = exe;
            path = string.IsNullOrEmpty(dir)
                ? Path.GetDirectoryName(exe)
                : Directory.Exists(Path.Combine(path, dir))
                    ? Path.Combine(path, dir.Replace("/", "\\"))
                    : Path.GetDirectoryName(exe);

            arguments = args.Length > 1
                ? string.Join(" ", args.Skip(1))
                : null;
        }

        #endregion
        abstract class GameLauncher
        {
            public string LauncherExe { get; protected set; }

            public abstract int RunAndWait(ProcessStartInfo path);

            public virtual PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
            {
                return PadToKey.AddOrUpdateKeyMapping(mapping, LauncherExe, InputKey.hotkey | InputKey.start, "(%{KILL})");
            }

            protected void KillExistingLauncherExes()
            {
                foreach (var px in Process.GetProcessesByName(LauncherExe))
                {
                    try { px.Kill(); }
                    catch { }
                }
            }

            protected Process GetLauncherExeProcess()
            {
                Process launcherprocess = null;

                int waitttime = 30;
                if (Program.SystemConfig.isOptSet("steam_wait") && !string.IsNullOrEmpty(Program.SystemConfig["steam_wait"]))
                    waitttime = Program.SystemConfig["steam_wait"].ToInteger();

                SimpleLogger.Instance.Info("[INFO] Starting process, waiting " + waitttime.ToString() + " seconds for the game to run before returning to Game List");

                for (int i = 0; i < waitttime; i++)
                {
                    launcherprocess = Process.GetProcessesByName(LauncherExe).FirstOrDefault();
                    if (launcherprocess != null)
                        break;

                    Thread.Sleep(1000);
                }

                return launcherprocess;
            }
        }
    }
}
