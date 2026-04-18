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
using System.Xml;
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
            bool fullscreen = ShouldRunFullscreen();

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

            bool fullscreen = ShouldRunFullscreen();
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
                using (var ini = new IniFile(file, IniOptions.UseSpaces | IniOptions.AllowRawLines))
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

            Process.Start(path);

            Thread.Sleep(3000);
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

            SimpleLogger.Instance.Info($"[EMULATOR] Process {emulator.ProcessName} ({emulator.Id}) found, waiting for it to exit.");

            int trackedId = emulator.Id;

            while (true)
            {
                var running = GetFirstRunningEmulator();

                if (running == null)
                    break;

                // Only log + re-track if it's a new process
                if (running.Id != trackedId)
                {
                    SimpleLogger.Instance.Info($"[EMULATOR] New process {running.ProcessName} ({running.Id}) detected, tracking it.");
                    Job.Current.AddProcess(running);
                    trackedId = running.Id;
                }

                try
                {
                    // Block until this specific process exits (no timeout)
                    running.WaitForExit();
                }
                catch { }

                // Brief pause before checking if a successor process spawned
                Thread.Sleep(1000);
            }

            SimpleLogger.Instance.Info("[EMULATOR] No emulator process found, exiting.");
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

        #region library
        public static void UpdateDOSGames()
        {
            try
            {
                string exoPath = Path.GetDirectoryName(Program.SystemConfig["exodosPath"]);
                if (!Directory.Exists(exoPath))
                {
                    SimpleLogger.Instance.Error("[ExoDOS] Invalid ExoDOS path.");
                    return;
                }

                CleanExoDOSScripts(exoPath);

                string baseInstalledGamesPath = Path.Combine(exoPath, "eXo", "eXoDOS");

                if (!Directory.Exists(baseInstalledGamesPath))
                    return;

                var games = GetGames(baseInstalledGamesPath, "!dos");

                if (games.Count == 0)
                    return;

                CreateShortcuts(games, "exodos");
                UpdateGamelist(games, "exodos", exoPath);
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
                UpdateGamelist(games, "exowin3x", exoPath);
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
                UpdateGamelist(games, "exowin9x", exoPath);
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

        private static void UpdateGamelist(List<ExoDosGame> games, string romFolder, string exoDOSPath)
        {
            string mediaSubFolder = "MS-DOS";
            if (romFolder == "exowin3x")
                mediaSubFolder = "Windows 3x";
            else if (romFolder == "exowin9x")
                mediaSubFolder = "Windows 9x";

            string xmlPath = Path.Combine(exoDOSPath, "xml", "all", "MS-DOS.xml");
            if (romFolder == "exowin3x")
                xmlPath = Path.Combine(exoDOSPath, "xml", "Windows 3x.xml");
            else if (romFolder == "exowin9x")
                xmlPath = Path.Combine(exoDOSPath, "xml", "Windows 9x.xml");

            var launchBoxIndex = BuildLaunchBoxIndex(xmlPath);
            var imageIndexFront = BuildImageIndex(Path.Combine(exoDOSPath, "Images", mediaSubFolder, "Box - Front"));
            var bannerIndex = BuildImageIndex(Path.Combine(exoDOSPath, "Images", mediaSubFolder, "Banner"));
            var fanartIndex = BuildImageIndex(Path.Combine(exoDOSPath, "Images", mediaSubFolder, "Fanart - Background"));
            var boxBackIndex = BuildImageIndex(Path.Combine(exoDOSPath, "Images", mediaSubFolder, "Box - Back"));
            var shotIndex = BuildImageIndex(Path.Combine(exoDOSPath, "Images", mediaSubFolder, "Screenshot - Game Title"));

            string romPath = Path.Combine(Program.AppConfig.GetFullPath("roms"), romFolder);
            string gamelistXml = Path.Combine(romPath, "gamelist.xml");

            XDocument doc = File.Exists(gamelistXml)
                ? XDocument.Load(gamelistXml)
                : new XDocument(
                    new XDeclaration("1.0", null, null),
                    new XElement("gameList")
                );

            if (doc.Root == null)
                doc.Add(new XElement("gameList"));

            foreach (ExoDosGame game in games)
            {
                string normalizedPath = game.Name.TrimStart('.', '/', '\\') + ".lnk";

                XElement existing = doc.Root
                    .Elements("game")
                    .FirstOrDefault(g => string.Equals(
                        g.Element("path") != null ? g.Element("path").Value.TrimStart('.', '/', '\\') : string.Empty,
                        normalizedPath,
                        StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new XElement("game",
                        new XElement("path", "./" + normalizedPath),
                        new XElement("name", CleanGameName(game.Name))
                    );

                    // Manuals
                    string manualPath = Path.Combine(exoDOSPath, "Manuals", mediaSubFolder, game.Name + ".pdf");
                    string targetManualPath = Path.Combine(romPath, "manuals");
                    if (!Directory.Exists(manualPath))
                        try { Directory.CreateDirectory(targetManualPath); } catch { }
                    string targetManualFile = Path.Combine(targetManualPath, game.Name + "-manual.pdf");

                    if (!File.Exists(targetManualFile) && File.Exists(manualPath))
                        try { File.Copy(manualPath, targetManualFile); } catch { }

                    if (File.Exists(targetManualFile))
                    {
                        XElement manual = new XElement("manual", "./manuals/" + game.Name + "-manual.pdf");
                        existing.Add(manual);
                    }

                    // Videos
                    string videoPath = Path.Combine(exoDOSPath, "Videos", mediaSubFolder, game.Name + ".mp4");
                    string targetVideoPath = Path.Combine(romPath, "videos");
                    if (!Directory.Exists(targetVideoPath))
                        try { Directory.CreateDirectory(targetVideoPath); } catch { }
                    string targetVideoFile = Path.Combine(targetVideoPath, game.Name + "-video.mp4");

                    if (!File.Exists(targetVideoFile) && File.Exists(videoPath))
                        try { File.Copy(videoPath, targetVideoFile); } catch { }

                    if (File.Exists(targetVideoFile))
                    {
                        XElement video = new XElement("video", "./videos/" + game.Name + "-video.mp4");
                        existing.Add(video);
                    }

                    string toSearch = Path.GetFileName(game.BatPath ?? "");
                    UpdateMetadata(existing, launchBoxIndex, toSearch, out string gameTitle);

                    if (!string.IsNullOrEmpty(gameTitle))
                    {
                        gameTitle = gameTitle.Replace(':', '_');
                        GetImagesFromLaunchBox(existing, imageIndexFront, gameTitle, romPath, game, "thumbnail", "-thumb");
                        GetImagesFromLaunchBox(existing, bannerIndex, gameTitle, romPath, game, "marquee", "-marquee");
                        GetImagesFromLaunchBox(existing, fanartIndex, gameTitle, romPath, game, "fanart", "-fanart");
                        GetImagesFromLaunchBox(existing, boxBackIndex, gameTitle, romPath, game, "boxback", "-boxback");
                        GetImagesFromLaunchBox(existing, shotIndex, gameTitle, romPath, game, "image", "-image");
                    }

                    doc.Root.Add(existing);
                }
            }

            doc.Save(gamelistXml);
        }

        private static void GetImagesFromLaunchBox(XElement existing, Dictionary<string, string> index, string gameTitle, string romPath, ExoDosGame game, string imageType, string suffix)
        {
            string imageFile = null;

            if (!index.TryGetValue(gameTitle + "-00", out imageFile))
                index.TryGetValue(gameTitle + "-01", out imageFile);

            // Priority 2: exact title match
            if (imageFile == null)
                index.TryGetValue(gameTitle, out imageFile);

            // Priority 3: starts with title
            if (imageFile == null)
                imageFile = index
                    .FirstOrDefault(kv => kv.Key.StartsWith(gameTitle, StringComparison.OrdinalIgnoreCase))
                    .Value;

            if (imageFile != null)
            {
                string targetImagePath = Path.Combine(romPath, "images");
                if (!Directory.Exists(targetImagePath))
                    Directory.CreateDirectory(targetImagePath);

                string ext = Path.GetExtension(imageFile);
                string targetFrontImageFile = Path.Combine(targetImagePath, game.Name + suffix + ext);
                if (!File.Exists(targetFrontImageFile))
                    try { File.Copy(imageFile, targetFrontImageFile); } catch { }

                if (File.Exists(targetFrontImageFile))
                    existing.SetElementValue(imageType, "./images/" + game.Name + suffix + ext);
            }
        }

        private static void UpdateMetadata(XElement existing, Dictionary<string, LaunchBoxGame> index, string toSearch, out string title)
        {
            title = null;

            LaunchBoxGame game;
            if (!index.TryGetValue(toSearch, out game))
                return;

            title = game.Title;

            if (!string.IsNullOrEmpty(game.Title))
                existing.SetElementValue("name", game.Title);
            if (!string.IsNullOrEmpty(game.Developer))
                existing.SetElementValue("developer", game.Developer);
            if (!string.IsNullOrEmpty(game.Publisher))
                existing.SetElementValue("publisher", game.Publisher);
            if (!string.IsNullOrEmpty(game.Genre))
                existing.SetElementValue("genre", game.Genre);
            if (!string.IsNullOrEmpty(game.Notes))
                existing.SetElementValue("desc", game.Notes);
            if (!string.IsNullOrEmpty(game.MaxPlayers))
                existing.SetElementValue("players", game.MaxPlayers);
            if (!string.IsNullOrEmpty(game.ReleaseDate))
                existing.SetElementValue("releasedate", game.ReleaseDate);
        }

        private static string CleanGameName(string name)
        {
            string cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"[\(\[].*?[\)\]]", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
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

        private static Dictionary<string, LaunchBoxGame> BuildLaunchBoxIndex(string xmlPath)
        {
            var index = new Dictionary<string, LaunchBoxGame>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(xmlPath))
                return index;

            using (var reader = XmlReader.Create(xmlPath))
            {
                LaunchBoxGame current = null;
                string activeElement = null;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "Game")
                                current = new LaunchBoxGame();
                            else if (current != null)
                                activeElement = reader.IsEmptyElement ? null : reader.Name;
                            break;

                        case XmlNodeType.Text:
                            if (current != null)
                            {
                                switch (activeElement)
                                {
                                    case "Title": current.Title = reader.Value; break;
                                    case "Developer": current.Developer = reader.Value; break;
                                    case "Publisher": current.Publisher = reader.Value; break;
                                    case "Notes": current.Notes = reader.Value; break;
                                    case "Genre": current.Genre = reader.Value; break;
                                    case "MaxPlayers": current.MaxPlayers = reader.Value; break;
                                    case "ApplicationPath": current.ApplicationPath = reader.Value; break;
                                    case "ReleaseDate":
                                        DateTime dt;
                                        if (DateTime.TryParse(reader.Value, out dt))
                                            current.ReleaseDate = dt.ToString("yyyyMMdd") + "T000000";
                                        break;
                                }
                            }
                            break;

                        case XmlNodeType.EndElement:
                            activeElement = null;
                            if (reader.Name == "Game" && current != null)
                            {
                                if (!string.IsNullOrEmpty(current.ApplicationPath))
                                {
                                    string key = Path.GetFileName(current.ApplicationPath);
                                    if (!index.ContainsKey(key))
                                        index[key] = current;
                                }
                                current = null;
                            }
                            break;
                    }
                }
            }

            return index;
        }

        private static Dictionary<string, string> BuildImageIndex(string imageFolder)
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(imageFolder))
                return index;

            foreach (var file in Directory.EnumerateFiles(imageFolder, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".png")
                    continue;

                string filename = Path.GetFileNameWithoutExtension(file);
                if (!index.ContainsKey(filename))
                    index[filename] = file;
            }

            return index;
        }

        public class LaunchBoxGame
        {
            public string Title { get; set; }
            public string Developer { get; set; }
            public string Publisher { get; set; }
            public string Notes { get; set; }
            public string MaxPlayers { get; set; }
            public string Genre { get; set; }
            public string ReleaseDate { get; set; } // formatted as 19921024T000000
            internal string ApplicationPath { get; set; } // used for matching only
        }

        public static bool FindGameByApplicationPath(string xmlFile, string applicationPathSuffix, out LaunchBoxGame game)
        {
            game = null;

            using (var reader = XmlReader.Create(xmlFile))
            {
                LaunchBoxGame current = null;
                string activeElement = null;

                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "Game")
                                current = new LaunchBoxGame();
                            else if (current != null)
                                activeElement = reader.Name;
                            break;

                        case XmlNodeType.Text:
                            if (current != null)
                            {
                                switch (activeElement)
                                {
                                    case "Title": current.Title = reader.Value; break;
                                    case "Developer": current.Developer = reader.Value; break;
                                    case "Publisher": current.Publisher = reader.Value; break;
                                    case "Notes": current.Notes = reader.Value; break;
                                    case "MaxPlayers": current.MaxPlayers = reader.Value; break;
                                    case "Genre": current.Genre = reader.Value; break;
                                    case "ReleaseDate":
                                        DateTime dt;
                                        if (DateTime.TryParse(reader.Value, out dt))
                                            current.ReleaseDate = dt.ToString("yyyyMMdd") + "T000000";
                                        break;
                                }
                            }
                            break;

                        case XmlNodeType.EndElement:
                            activeElement = null;
                            if (reader.Name == "Game" && current != null)
                            {
                                if (current.ApplicationPath != null && current.ApplicationPath.EndsWith(applicationPathSuffix, StringComparison.OrdinalIgnoreCase))
                                {
                                    game = current;
                                    return true;
                                }
                                current = null;
                            }
                            break;
                    }
                }
            }

            return false;
        }
        #endregion
    }
}
