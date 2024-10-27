using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
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
        private string _exename = null;
        private bool _isGameExePath;
        private bool _steamRun = false;
        private bool _exeFile;
        private bool _nonSteam = false;

        private GameLauncher _gameLauncher;

        static readonly Dictionary<string, Func<Uri, GameLauncher>> launchers = new Dictionary<string, Func<Uri, GameLauncher>>()
        {
            { "com.epicgames.launcher", (uri) => new EpicGameLauncher(uri) },
            { "steam", (uri) => new SteamGameLauncher(uri) },
            { "amazon-games", (uri) => new AmazonGameLauncher(uri) }
        };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            rom = this.TryUnZipGameIfNeeded(system, rom);

            _systemName = system.ToLowerInvariant();

            string path = Path.GetDirectoryName(rom);
            string arguments = null;
            _isGameExePath = false;
            _exeFile = false;
            string extension = Path.GetExtension(rom);

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (extension == ".lnk")
            {
                SimpleLogger.Instance.Info("[INFO] link file, searching for target.");
                string target = FileTools.GetShortcutTarget(rom);

                string executableFile = Path.Combine(Path.GetDirectoryName(rom), Path.GetFileNameWithoutExtension(rom) + ".gameexe");

                if (target != "" && target != null)
                {
                    _isGameExePath = File.Exists(target);
                    
                    if (_isGameExePath)
                        SimpleLogger.Instance.Info("[INFO] Link target file found.");
                    
                    // executable process to monitor might be different from the target - user can specify true process executable in a .gameexe file
                    _exeFile = GetProcessFromFile(rom);
                }

                // if the target is not found in the link, see if a .gameexe file or a .uwp file exists
                else
                {
                    // First case : use has directly specified the executable name in a .gameexe file
                    _exeFile = GetProcessFromFile(rom);

                    // Second case : user has specified the UWP app name in a .uwp file
                    string uwpexecutableFile = Path.Combine(Path.GetDirectoryName(rom), Path.GetFileNameWithoutExtension(rom) + ".uwp");
                    if (File.Exists(uwpexecutableFile) && !_exeFile)
                    {
                        var romLines = File.ReadAllLines(uwpexecutableFile);
                        if (romLines.Length > 0)
                        {
                            string uwpAppName = romLines[0];
                            int line = -1;
                            var fileStream = GetStoreAppVersion(uwpAppName);

                            if (fileStream != null && fileStream != "")
                            {
                                string[] lines = fileStream.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                                int m;
                                for (m = 0; m < lines.Count(); m++)
                                {
                                    if (lines[m].Contains("InstallLocation"))
                                    {
                                        line = m + 2;
                                    }
                                }
                                string installLocation = lines[line];

                                if (Directory.Exists(installLocation))
                                {
                                    string appManifest = Path.Combine(installLocation, "AppxManifest.xml");

                                    if (File.Exists(appManifest))
                                    {
                                        XDocument doc = XDocument.Load(appManifest);
                                        XElement applicationElement = doc.Descendants().Where(x => x.Name.LocalName == "Application").FirstOrDefault();
                                        if (applicationElement != null)
                                        {
                                            string exePath = applicationElement.Attribute("Executable").Value;
                                            if (exePath != null)
                                            {
                                                _exename = Path.GetFileNameWithoutExtension(exePath);
                                                _exeFile = true;
                                                SimpleLogger.Instance.Info("[INFO] Executable name found for UWP app: " + _exename);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    else if (!_exeFile)
                        SimpleLogger.Instance.Info("[INFO] Impossible to find executable name, using rom file name.");
                }

                if (_isGameExePath)
                {
                    rom = target;
                    path = Path.GetDirectoryName(target);

                    SimpleLogger.Instance.Info("[INFO] New ROM : " + rom);
                }
            }

            // Define if shortcut is an EpicGame or Steam shortcut
            else if (extension == ".url")
            {
                // executable process to monitor might be different from the target - user can specify true process executable in a .gameexe file
                _exeFile = GetProcessFromFile(rom);

                if (!_exeFile)
                {
                    try
                    {
                        var uri = new Uri(IniFile.FromFile(rom).GetValue("InternetShortcut", "URL"));

                        if (launchers.TryGetValue(uri.Scheme, out Func<Uri, GameLauncher> gameLauncherInstanceBuilder))
                            _gameLauncher = gameLauncherInstanceBuilder(uri);
                    }
                    catch (Exception ex)
                    {
                        SetCustomError(ex.Message);
                        SimpleLogger.Instance.Error("[ExeLauncherGenerator] " + ex.Message, ex);
                        return null;
                    }
                }

                // Run Steam games via their shortcuts and not just run the .url file
                var urlLines = File.ReadAllLines(rom);

                if (urlLines.Length > 0)
                {
                    // Get URL to run and add -silent argument
                    if (urlLines.Any(l => l.StartsWith("URL=steam://rungameid")))
                    {
                        string urlline = urlLines.FirstOrDefault(l => l.StartsWith("URL=steam"));
                        if (!string.IsNullOrEmpty(urlline) && !urlline.Contains("-silent"))
                        {
                            for (int i = 0; i < urlLines.Length; i++)
                            {
                                if (urlLines[i].StartsWith("URL="))
                                {
                                    string temp = urlLines[i];
                                    urlLines[i] = urlLines[i] + "\"" + " -silent";
                                }
                            }
                            try
                            {
                                File.WriteAllLines(rom, urlLines);
                            }
                            catch { }

                            _steamRun = true;
                        }
                    }

                    // Get executable name from icon path
                    if (string.IsNullOrEmpty(_exename) && urlLines.Any(l => l.StartsWith("IconFile")))
                    {
                        string iconline = urlLines.FirstOrDefault(l => l.StartsWith("IconFile"));
                        if (iconline.EndsWith(".exe"))
                        {
                            string iconPath = iconline.Substring(9, iconline.Length - 9);
                            _exename = Path.GetFileNameWithoutExtension(iconPath);
                            if (!string.IsNullOrEmpty(_exename))
                            {
                                _nonSteam = true;
                                SimpleLogger.Instance.Info("[STEAM] Found name of executable from icon info: " + _exename);
                            }
                        }
                    }
                }
            }

            else if (extension == ".game")
            {
                string linkTarget = null;
                string [] lines = File.ReadAllLines(rom);

                if (lines.Length == 0)
                    throw new Exception("No path specified in .game file.");
                else
                    linkTarget = lines[0];

                if (!File.Exists(linkTarget))
                    throw new Exception("Target file " + linkTarget + " does not exist.");

                _isGameExePath = File.Exists(linkTarget);
                
                if (_isGameExePath)
                {
                    rom = linkTarget;
                    path = Path.GetDirectoryName(linkTarget);
                }
            }

            if (Directory.Exists(rom)) // If rom is a directory ( .pc .win .windows, .wine )
            {
                path = rom;

                if (File.Exists(Path.Combine(rom, "autorun.cmd")))
                    rom = Path.Combine(rom, "autorun.cmd");
                else if (File.Exists(Path.Combine(rom, "autorun.bat")))
                    rom = Path.Combine(rom, "autorun.bat");
                else if (File.Exists(Path.Combine(rom, "autoexec.cmd")))
                    rom = Path.Combine(rom, "autoexec.cmd");
                else if (File.Exists(Path.Combine(rom, "autoexec.bat")))
                    rom = Path.Combine(rom, "autoexec.bat");
                else
                    rom = Directory.GetFiles(path, "*.exe").FirstOrDefault();

                if (Path.GetFileName(rom) == "autorun.cmd")
                {
                    var wineCmd = File.ReadAllLines(rom);
                    if (wineCmd == null || wineCmd.Length == 0)
                        throw new Exception("autorun.cmd is empty");

                    var dir = wineCmd.Where(l => l.StartsWith("DIR=")).Select(l => l.Substring(4)).FirstOrDefault();

                    var wineCommand = wineCmd.Where(l => l.StartsWith("CMD=")).Select(l => l.Substring(4)).FirstOrDefault();
                    if (string.IsNullOrEmpty(wineCommand) && wineCmd.Length > 0)
                        wineCommand = wineCmd.FirstOrDefault();

                    var args = wineCommand.SplitCommandLine();
                    if (args.Length > 0)
                    {
                        string exe = string.IsNullOrEmpty(dir) ? Path.Combine(path, args[0]) : Path.Combine(path, dir.Replace("/", "\\"), args[0]);
                        if (File.Exists(exe))
                        {
                            rom = exe;

                            if (!string.IsNullOrEmpty(dir))
                            {
                                string customDir = Path.Combine(path, dir);
                                path = Directory.Exists(customDir) ? customDir : Path.GetDirectoryName(rom);
                            }
                            else
                                path = Path.GetDirectoryName(rom);

                            if (args.Length > 1)
                                arguments = string.Join(" ", args.Skip(1).ToArray());
                        }
                        else
                            throw new Exception("Invalid autorun.cmd executable");
                    }
                    else
                        throw new Exception("Invalid autorun.cmd command");
                }
            }

            if (!File.Exists(rom) && !_steamRun)
                return null;

            if (Path.GetExtension(rom).ToLower() == ".m3u")
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

            string ext = Path.GetExtension(rom).ToLower();
            
            if (ext == ".bat" || ext == ".cmd")
            {
                ret.WindowStyle = ProcessWindowStyle.Hidden;
                ret.UseShellExecute = true;
            }
            else if (string.IsNullOrEmpty(_exename) && _gameLauncher == null)
            {
                _exename = Path.GetFileNameWithoutExtension(rom);
                SimpleLogger.Instance.Info("[INFO] Executable name : " + _exename);
            }

            // If game was uncompressed, say we are going to launch, so the deletion will not be silent
            ValidateUncompressedGame();

            return ret;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_isGameExePath || _exeFile)
                return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");

            else if (_gameLauncher != null) 
                return _gameLauncher.SetupCustomPadToKeyMapping(mapping);

            else if (_systemName != "mugen" || _systemName != "ikemen" || string.IsNullOrEmpty(_exename))
                return mapping;

            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

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

                if (!string.IsNullOrEmpty(ini.GetValue("Config", "GameWidth")))
                {
                    ini.WriteValue("Config", "GameWidth", resolution.Width.ToString());
                    ini.WriteValue("Config", "GameHeight", resolution.Height.ToString());
                }

                if (SystemConfig["resolution"] == "480p")
                {
                    ini.WriteValue("Config", "GameWidth", "640");
                    ini.WriteValue("Config", "GameHeight", "480");
                }
                else if (SystemConfig["resolution"] == "720p")
                {
                    ini.WriteValue("Config", "GameWidth", "960");
                    ini.WriteValue("Config", "GameHeight", "720");
                }
                else if (SystemConfig["resolution"] == "960p")
                {
                    ini.WriteValue("Config", "GameWidth", "1280");
                    ini.WriteValue("Config", "GameHeight", "960");
                }
                else
                {
                    ini.WriteValue("Config", "GameWidth", resolution.Width.ToString());
                    ini.WriteValue("Config", "GameHeight", resolution.Height.ToString());
                }

                //ini.WriteValue("Video", "Width", resolution.Width.ToString());
                //ini.WriteValue("Video", "Height", resolution.Height.ToString());

                BindBoolIniFeatureOn(ini, "Video", "VRetrace", "VRetrace", "1", "0");
                ini.WriteValue("Video", "FullScreen", fullscreen ? "1" : "0");

            }
        }

        private void UpdateIkemenConfig(string path, string system, string rom, bool fullscreen, ScreenResolution resolution, string emulator)
        {
            if (_systemName != "ikemen")
                return;

            var json = DynamicJson.Load(Path.Combine(path, "save", "config.json"));

            ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator);

            if (resolution == null)
                resolution = ScreenResolution.CurrentResolution;

            json["FirstRun"] = "false";           
            json["Fullscreen"] = fullscreen ? "true" : "false";

            if (SystemConfig["resolution"] == "240p")
            {
                json["GameWidth"] = "320";
                json["GameHeight"] = "240";
            }
            else if (SystemConfig["resolution"] == "480p")
            {
                json["GameWidth"] = "640";
                json["GameHeight"] = "480";
            }
            else if (SystemConfig["resolution"] == "720p")
            {
                json["GameWidth"] = "1280";
                json["GameHeight"] = "720";
            }
            else if (SystemConfig["resolution"] == "960p")
            {
                json["GameWidth"] = "1280";
                json["GameHeight"] = "960";
            }
            else if (SystemConfig["resolution"] == "1080p")
            {
                json["GameWidth"] = "1920";
                json["GameHeight"] = "1080";
            }
            else
            {
                json["GameWidth"] = resolution.Width.ToString();
                json["GameHeight"] = resolution.Height.ToString();
            }

            BindBoolFeatureOn(json, "VRetrace", "VRetrace", "1", "0");

            json.Save();
        }

        private bool GetProcessFromFile(string rom)
        {
            string executableFile = Path.Combine(Path.GetDirectoryName(rom), Path.GetFileNameWithoutExtension(rom) + ".gameexe");

            if (!File.Exists(executableFile))
                return false;

            var lines = File.ReadAllLines(executableFile);
            if (lines.Length < 1)
                return false;
            else
            {
                _exename = lines[0].ToString();
                SimpleLogger.Instance.Info("[INFO] Executable name specified in .gameexe file: " + _exename);
                return true;
            }
        }

        static string GetStoreAppVersion(string appName)
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

        string[] launcherPprocessNames = { "Amazon Games UI", "EADesktop", "EpicGamesLauncher", "steam" };

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_isGameExePath || _exeFile || _nonSteam)
            {
                Dictionary<string, bool> launcherProcessStatusBefore = new Dictionary<string, bool>();
                Dictionary<string, bool> launcherProcessStatusAfter = new Dictionary<string, bool>();
                
                foreach (string processName in launcherPprocessNames)
                {
                    bool uiExists = Process.GetProcessesByName(processName).Any();

                    if (uiExists)
                        launcherProcessStatusBefore.Add(processName, true);
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
                    SimpleLogger.Instance.Info("Process : " + _exename + " not running");
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
                    game.WaitForExit();
                }

                foreach (var processName in launcherProcessStatusAfter)
                {
                    if (!launcherProcessStatusBefore.ContainsKey(processName.Key) || Program.SystemConfig.getOptBoolean("killsteam"))
                    {
                        foreach (var ui in Process.GetProcessesByName(processName.Key))
                        {
                            try
                            {
                                SimpleLogger.Instance.Info("[INFO] Killing process " + processName.Key);
                                ui.Kill();
                            }
                            catch { }
                        }
                    }
                }

                return 0;
            }

            else if (_systemName == "windows" || _gameLauncher != null)
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

                    base.RunAndWait(path);
                }
            }

            else
                base.RunAndWait(path);

            return 0;
        }

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
