using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    class exoDOSGenerator : Generator
    {
        public exoDOSGenerator()
        {
            DependsOnDesktopResolution = true;
        }
        private string _system;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            _system = system;

            ConfigureExo(system, rom);

            return new ProcessStartInfo()
            {
                FileName = rom
            };
        }

        private void ConfigureExo(string system, string rom)
        {
            string path;
            switch (system)
            {
                case "exodos":
                    path = Program.SystemConfig["exodosPath"];
                    break;
                case "exowin3x":
                    path = Program.SystemConfig["exowin3xPath"];
                    break;
                case "exowin9x":
                    path = Program.SystemConfig["exowin9xPath"];
                    break;
                default:
                    return;
            }

            if (string.IsNullOrEmpty(path))
                return;
            else
                path = Path.GetDirectoryName(path);

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            string gameRes = SystemConfig.isOptSet("exo_resolution") && !string.IsNullOrEmpty(SystemConfig["exo_resolution"])? SystemConfig["exo_resolution"] : "medium";
            bool aspectRatio = SystemConfig.getOptBoolean("exo_aspect_ratio") || !SystemConfig.isOptSet("exo_aspect_ratio");

            string emulatorPath = Path.Combine(path, "eXo", "emulators");
            string utilPath = Path.Combine(path, "eXo", "util");

            try
            {
                string linkTarget = FileTools.GetShortcutTargetwsh(rom);

                string romPath = null;
                if (linkTarget != null)
                {
                    romPath = Path.GetDirectoryName(linkTarget);
                }

                var iniFiles = new List<string>();
                
                if (system == "exodos")
                {
                    iniFiles.Add(Path.Combine(emulatorPath, "dosbox", "options.conf"));

                    if (!string.IsNullOrEmpty(romPath))
                    {
                        iniFiles.Add(Path.Combine(romPath, "dosbox.conf"));
                    }
                }

                if (system == "exowin9x")
                {
                    iniFiles.Add(Path.Combine(emulatorPath, "dosbox", "options.conf"));
                    iniFiles.Add(Path.Combine(emulatorPath, "dosbox", "options9x.conf"));
                    
                    if (!string.IsNullOrEmpty(romPath))
                    {
                        iniFiles.Add(Path.Combine(romPath, "Play.conf"));
                    }
                }

                if (system == "exowin3x")
                {
                    iniFiles.Add(Path.Combine(emulatorPath, "dosbox", "options.conf"));

                    if (!string.IsNullOrEmpty(romPath))
                    {
                        iniFiles.Add(Path.Combine(romPath, "dosbox.conf"));
                        iniFiles.Add(Path.Combine(romPath, "dosbox2.conf"));
                    }
                }

                foreach (var file in iniFiles)
                {
                    if (File.Exists(file))
                        ApplyIniSettings(file, gameRes, aspectRatio, fullscreen);
                }

                UpdateSelFiles(utilPath, gameRes, aspectRatio, fullscreen);
            }
            catch { }
        }

        private void ApplyIniSettings(string file, string gameRes, bool aspectRatio, bool fullscreen)
        {
            try
            {
                using (var ini = new IniFile(file))
                {
                    string resolution;
                    switch (gameRes)
                    {
                        case "small":
                            resolution = "640x480";
                            break;
                        case "large":
                            resolution = "2560x1920";
                            break;
                        case "medium":
                        default:
                            resolution = "1280x960";
                            break;
                    }

                    ini.WriteValue("sdl", "windowresolution", resolution);
                    ini.WriteValue("render", "aspect", aspectRatio ? "true" : "false");
                    ini.WriteValue("sdl", "fullscreen", fullscreen ? "true" : "false");
                }
            }
            catch { }
        }

        private void UpdateSelFiles(string utilPath, string gameRes, bool aspectRatio, bool fullscreen)
        {
            // Resolution
            string resFile;
            if (gameRes == "small")
                resFile = "SML.SEL";
            else if (gameRes == "large")
                resFile = "LRG.SEL";
            else
                resFile = "MED.SEL";

            SetExclusiveFile(utilPath, resFile, new string[] { "SML.SEL", "MED.SEL", "LRG.SEL" });

            // Aspect ratio
            string aspectFile = aspectRatio ? "AYES.SEL" : "ANO.SEL";
            SetExclusiveFile(utilPath, aspectFile, new string[] { "AYES.SEL", "ANO.SEL" });

            // Fullscreen
            string screenFile = fullscreen ? "FULL.SEL" : "WIN.SEL";
            SetExclusiveFile(utilPath, screenFile, new string[] { "FULL.SEL", "WIN.SEL" });
        }

        private void SetExclusiveFile(string path, string keep, string[] allFiles)
        {
            try
            {
                foreach (var file in allFiles)
                {
                    string fullPath = Path.Combine(path, file);

                    if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!File.Exists(fullPath))
                            File.WriteAllText(fullPath, "");
                    }
                    else
                    {
                        if (File.Exists(fullPath))
                            try { File.Delete(fullPath); } catch { }
                    }
                }
            }
            catch
            { }
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            PadToKey.AddOrUpdateKeyMapping(mapping, "86Box", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "PCBox", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "DOSBox", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox_gamelink", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox_x64", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox_noopt", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox_debug", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-debug", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox__", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox_with_debugger", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "DOSBox_debug", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "scummvm", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_MinGWx64_SDL1", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_MinGWx64_SDL2", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_MinGWx86_SDL1", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_MinGWx86_SDL2", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_x64_SDL1", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_x64_SDL2", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_x86_SDL1", InputKey.hotkey | InputKey.start, "(%{KILL})");
            PadToKey.AddOrUpdateKeyMapping(mapping, "dosbox-x_x86_SDL2", InputKey.hotkey | InputKey.start, "(%{KILL})");

            return mapping;
        }

        public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
        {
            KillRunningEmulators();

            var process = Process.Start(path);
            //Job.Current.AddProcess(process);

            int maxRetries = 10;
            Process emulator = null;

            for (int i = 0; i < maxRetries; i++)
            {
                emulator = GetFirstRunningEmulator();

                if (emulator != null)
                {
                    Job.Current.AddProcess(emulator);
                    break;
                }

                Thread.Sleep(3000);

                if (i == maxRetries - 1)
                    return 0;
            }

            try
            {
                if (emulator.ProcessName != null)
                {
                    SimpleLogger.Instance.Info($"[EMULATOR] Process {emulator.ProcessName} found, waiting for it to exit.");
                }
                emulator.WaitForExit();
            }
            catch { }

            return 0;
        }

        Process GetFirstRunningEmulator()
        {
            return Process.GetProcesses()
                .FirstOrDefault(p =>
                {
                    try
                    {
                        var name = p.ProcessName;

                        return name.Equals("86Box", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("PCBox", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("scummvm", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        void KillRunningEmulators()
        {
            SimpleLogger.Instance.Info("[EMULATOR] Checking for running ExoDOS emulators and closing them.");
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var name = p.ProcessName;

                    if (name.Equals("86Box", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("PCBox", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("scummvm", StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill();
                        p.WaitForExit();
                    }
                }
                catch
                { }
            }
        }

        public static void UpdateDOSGames()
        {
            try
            {
                string exoDOSPath = Path.GetDirectoryName(Program.SystemConfig["exodosPath"]);
                if (!Directory.Exists(exoDOSPath))
                {
                    SimpleLogger.Instance.Error("[ExoDOS] Invalid ExoDOS path.");
                    return;
                }

                CleanExoDOSScripts(exoDOSPath);

                string baseInstalledGamesPath = Path.Combine(exoDOSPath, "eXo", "eXoDOS");

                if (!Directory.Exists(baseInstalledGamesPath))
                    return;

                var games = GetGames(baseInstalledGamesPath, "!dos");

                if (games.Count == 0)
                    return;

                CreateShortcuts(games, "exodos");
            }
            catch { }
        }

        public static void UpdateWin3xGames()
        {
            try
            {
                string exoPath = Path.GetDirectoryName(Program.SystemConfig["exowin3xPath"]);
                if (!Directory.Exists(exoPath))
                {
                    SimpleLogger.Instance.Error("[ExoWin3x] Invalid ExoWin3x path.");
                    return;
                }

                CleanExoDOSScripts(exoPath);

                string baseInstalledGamesPath = Path.Combine(exoPath, "eXo", "eXoWin3x");

                if (!Directory.Exists(baseInstalledGamesPath))
                    return;

                var games = GetGames(baseInstalledGamesPath, "!win3x");

                if (games.Count == 0)
                    return;

                CreateShortcuts(games, "exowin3x");
            }
            catch { }
        }

        public static void UpdateWin9xGames()
        {
            try
            {
                string exoPath = Path.GetDirectoryName(Program.SystemConfig["exowin9xPath"]);
                if (!Directory.Exists(exoPath))
                {
                    SimpleLogger.Instance.Error("[ExoWin9x] Invalid ExoWin9x path.");
                    return;
                }

                CleanExoDOSScripts(exoPath);

                string baseInstalledGamesPath = Path.Combine(exoPath, "eXo", "eXoWin9x");

                if (!Directory.Exists(baseInstalledGamesPath))
                    return;

                var games = GetGames(baseInstalledGamesPath, "!win9x");

                if (games.Count == 0)
                    return;

                CreateShortcuts(games, "exowin9x");
            }
            catch { }
        }

        private static List<ExoDosGame> GetGames(string baseInstalledGamesPath, string exoType)
        {
            var games = new List<ExoDosGame>();

            List<string> validFolders = new List<string>();

            var folders = Directory.EnumerateDirectories(baseInstalledGamesPath)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !name.StartsWith("!");
                }).ToList();

            validFolders = folders.Where(d => !(Path.GetFileName(d).Length == 4 && Path.GetFileName(d).All(char.IsDigit))).ToList();

            foreach (var folder in folders)
            {
                string folderName = Path.GetFileName(folder);
                if (folderName.Length == 4 && folderName.All(char.IsDigit))
                {
                    var subfolders = Directory.EnumerateDirectories(folder)
                    .Where(d =>
                    {
                        var name = Path.GetFileName(d);
                        return !name.StartsWith("!");
                    }).ToList();

                    validFolders.AddRange(subfolders);
                }
            }

            foreach (var dir in validFolders)
            {
                string dirName = Path.GetFileName(dir);
                string yearPath = Path.GetDirectoryName(dir);
                string year = Path.GetFileName(yearPath);

                var gameBatPath = exoType == "!win9x" ? Path.Combine(baseInstalledGamesPath, exoType, year, dirName) : Path.Combine(baseInstalledGamesPath, exoType, dirName);

                if (!Directory.Exists(gameBatPath))
                    continue;

                var batFile = Directory.EnumerateFiles(gameBatPath, "*.bat", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => !string.Equals(Path.GetFileName(f), "install.bat", StringComparison.OrdinalIgnoreCase));

                if (batFile == null)
                    continue;

                games.Add(new ExoDosGame
                {
                    Name = Path.GetFileNameWithoutExtension(batFile),
                    BatPath = batFile,
                    WorkingDirectory = gameBatPath
                });
            }

            return games;
        }

        private static void CreateShortcuts(List<ExoDosGame> games, string romFolder)
        {
            string targetFolder = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "roms", romFolder);
            if (!Directory.Exists(targetFolder))
                try { Directory.CreateDirectory(targetFolder); } catch { }

            if (!Directory.Exists(targetFolder))
            {
                SimpleLogger.Instance.Error($"[{romFolder}] Unable to create shortcuts folder: {targetFolder}");
                return;
            }

            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));

            foreach (var game in games)
            {
                try
                {
                    dynamic shortcut = shell.CreateShortcut(
                        Path.Combine(targetFolder, game.Name + ".lnk"));

                    shortcut.TargetPath = game.BatPath;
                    shortcut.WorkingDirectory = game.WorkingDirectory;
                    shortcut.Save();

                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }
                catch { }
            }

            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        class ExoDosGame
        {
            public string Name { get; set; }
            public string BatPath { get; set; }
            public string WorkingDirectory { get; set; }
        }

        private static void CleanExoDOSScripts(string exoDOSPath)
        {
            string exoDosScriptsPath = Path.Combine(exoDOSPath, "eXo", "util");
            if (!Directory.Exists(exoDosScriptsPath))
                return;

            var ipScript = Path.Combine(exoDosScriptsPath, "ip.bat");
            if (File.Exists(ipScript))
            {
                var content = File.ReadAllText(ipScript);

                // Add -UseBasicParsing to any Invoke-WebRequest missing it
                content = Regex.Replace(
                    content,
                    @"(Invoke-WebRequest\b(?![^)]*-UseBasicParsing)[^)]*)\)",
                    "$1 -UseBasicParsing)",
                    RegexOptions.IgnoreCase
                );

                File.WriteAllText(ipScript, content);
            }
        }
    }
}
