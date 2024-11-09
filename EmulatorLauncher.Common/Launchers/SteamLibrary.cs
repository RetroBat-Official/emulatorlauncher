using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Win32;
using Steam_Library_Manager.Framework;
using EmulatorLauncher.Common.Launchers.Steam;

namespace EmulatorLauncher.Common.Launchers
{
    public static class SteamLibrary
    {
        // https://cdn.cloudflare.steamstatic.com/steam/apps/1515950/header.jpg

        const string GameLaunchUrl = @"steam://rungameid/{0}";
        const string HeaderImageUrl = @"https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/header.jpg";

        public static LauncherGameInfo[] GetInstalledGames()
        {
            var games = new List<LauncherGameInfo>();

            string libraryfoldersPath = Path.Combine(GetInstallPath(), "config", "libraryfolders.vdf");

            try
            {
                var libraryfolders = new KeyValue();
                libraryfolders.ReadFileAsText(libraryfoldersPath);

                var folders = GetLibraryFolders(libraryfolders);

                foreach (var folder in folders)
                {
                    var libFolder = Path.Combine(folder, "steamapps");
                    if (Directory.Exists(libFolder))
                    {
                        foreach(var game in GetInstalledGamesFromFolder(libFolder))
                        {
                            if (game.Id == "228980")
                                continue;

                            games.Add(game);
                        }
                    }
                }
            }
            catch { }

            return games.ToArray();
        }
        
        public static string GetSteamGameExecutableName(Uri uri)
        {
            // Get Steam app ID from url
            string shorturl = uri.AbsolutePath.Substring(1);
            int endurl = shorturl.IndexOf("%");
            if (endurl == -1) // If there's no space, get until the end of the string
                endurl = shorturl.Length;
            shorturl = shorturl.Substring(0, endurl);
            
            if (!string.IsNullOrEmpty(shorturl))
                SimpleLogger.Instance.Info("[INFO] STEAM appID: " + shorturl);
            
            int steamAppId = shorturl.ToInteger();
            ulong SteamAppIdLong = shorturl.ToUlong();

            // If app ID is too long, it's a non-Steam game : return
            if (steamAppId == 0 && SteamAppIdLong == 0)
            {
                SimpleLogger.Instance.Info("[STEAM] Non-Steam game detected.");
                return null;
            }

            // Call method to get executable name from Steam vdf files
            string exe = FindExecutableName(steamAppId, SteamAppIdLong);

            if (string.IsNullOrEmpty(exe))
            {
                SimpleLogger.Instance.Info("[WARNING] Cannot find STEAM game executable in appinfo.vdf.");
                return null;
            }
            else
                SimpleLogger.Instance.Info("[STEAM] STEAM game executable found: " + exe);

            return Path.GetFileNameWithoutExtension(exe);
        }

        static string FindExecutableName(int steamAppId, ulong SteamAppIdLong = 1999999999999999999)
        {
            // Get Steam installation path in registry
            string path = GetInstallPath();
            if (string.IsNullOrEmpty(path))
                throw new ApplicationException("Can not find Steam installation folder in registry.");
            
            string appinfoPath = Path.Combine(path, "appcache", "appinfo.vdf");

            if (!File.Exists(appinfoPath))
                SimpleLogger.Instance.Info("[WARNING] Missing file " + appinfoPath);

            // Try to get executable by deserializing vdf file
            // Broken since july 2024 - returns error
            try
            {
                var reader = new SteamAppInfoReader();
                reader.Read(appinfoPath);

                SimpleLogger.Instance.Info("[INFO] Reading Steam file 'appinfo.vdf'");

                var app = reader.Apps.FirstOrDefault(a => a.AppID == steamAppId);
                if (app == null || steamAppId == 0)
                    app = reader.Apps.FirstOrDefault(a => a.AppID == SteamAppIdLong);
                if (app == null)
                    return null;

                SimpleLogger.Instance.Info("[INFO] Found Game \"" + steamAppId + "\" in 'appinfo.vdf'");

                string executable;

                var executables = app.Data.Traverse(d => d.Children).Where(d => d.Children.Any(c => c.Name == "executable")).ToArray();
                foreach (var exe in executables)
                {
                    var config = exe.Children.Where(c => c.Name == "config").SelectMany(c => c.Children).Where(c => c.Name == "oslist" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                    if ("windows".Equals(config))
                    {
                        var type = exe.Children.Where(c => c.Name == "type" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (type != "default")
                            SimpleLogger.Instance.Info("[INFO] No default 'type' found, using first executable found.");

                        executable = exe.Children.Where(c => c.Name == "executable" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (!string.IsNullOrEmpty(executable))
                        {
                            SimpleLogger.Instance.Info("[INFO] Game executable " + executable + " found.");
                            return executable;
                        }
                    }
                    else
                    {
                        SimpleLogger.Instance.Info("[INFO] No 'windows' specific config found, using first executable found.");

                        var type = exe.Children.Where(c => c.Name == "type" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (type != "default")
                            SimpleLogger.Instance.Info("[INFO] No default 'type' found, using first executable found.");

                        executable = exe.Children.Where(c => c.Name == "executable" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (!string.IsNullOrEmpty(executable))
                        {
                            SimpleLogger.Instance.Info("[INFO] Game executable " + executable + " found.");
                            return executable;
                        }
                    }

                    SimpleLogger.Instance.Info("[WARNING] No game executable found, cannot put ES in Wait-mode.");
                }
            }
            catch
            {
                SimpleLogger.Instance.Info("[WARNING] Impossible to read SteamAppInfo.");
            }

            // Try brutal method to look for the app ID and retrieve the first .exe that follows in the vdf file
            try
            {
                SimpleLogger.Instance.Info("[INFO] Searching executable in Steam file 'appinfo.vdf' : alternative method");
                string appInfo = File.ReadAllText(appinfoPath);
                int index = appInfo.IndexOf(steamAppId.ToString());
                if (steamAppId == 0)
                    index = appInfo.IndexOf(SteamAppIdLong.ToString());

                if (index != -1)
                {
                    SimpleLogger.Instance.Info("[INFO] Found Game \"" + steamAppId + "\" in 'appinfo.vdf'");
                    string substringToSearch = appInfo.Substring(index);

                    int exeIndex = substringToSearch.IndexOf(".exe");

                    if (exeIndex != -1)
                    {
                        int actualExeIndex = index + exeIndex;

                        // Restrict the search to the substring between the app ID and the .exe
                        int sectionStart = index;
                        int sectionEnd = actualExeIndex;
                        if (sectionEnd != -1)
                        {
                            string restrictedContent = appInfo.Substring(sectionStart, sectionEnd - sectionStart);

                            if (restrictedContent != null)
                            {
                                int nullIndex = restrictedContent.LastIndexOf('\0');
                                if (nullIndex != -1)
                                {
                                    string steamExeName = restrictedContent.Substring(nullIndex + 1);

                                    if (steamExeName != null)
                                    {
                                        SimpleLogger.Instance.Info("[INFO] Game executable " + steamExeName + " found (alternative method).");
                                        return steamExeName;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch 
            {
                SimpleLogger.Instance.Info("[WARNING] Impossible to find Steam executable name : consider .gameexe method.");
            }

            return null;
        }

        static List<LauncherGameInfo> GetInstalledGamesFromFolder(string path)
        {
            var games = new List<LauncherGameInfo>();

            foreach (var file in Directory.GetFiles(path, @"appmanifest*"))
            {
                if (file.EndsWith("tmp", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var game = GetInstalledGameFromFile(Path.Combine(path, file));
                    if (game == null)
                        continue;
                   
                    if (string.IsNullOrEmpty(game.InstallDirectory) || game.InstallDirectory.Contains(@"steamapps\music"))
                        continue;

                    games.Add(game);
                }
                catch (Exception ex)
                {

                }
            }

            return games;
        }

        static LauncherGameInfo GetInstalledGameFromFile(string path)
        {
            var kv = new KeyValue();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                kv.ReadAsText(fs);

            SteamAppStateFlags appState;
            if (!string.IsNullOrEmpty(kv["StateFlags"].Value) && Enum.TryParse<SteamAppStateFlags>(kv["StateFlags"].Value, out appState))
            {
                if (!appState.HasFlag(SteamAppStateFlags.FullyInstalled))
                    return null;
            }
            else
                return null;

            var name = string.Empty;
            if (string.IsNullOrEmpty(kv["name"].Value))
            {
                if (kv["UserConfig"]["name"].Value != null)
                {
                    name = kv["UserConfig"]["name"].Value; //  StringExtensions.NormalizeGameName();
                }
            }
            else
                name = kv["name"].Value; // StringExtensions.NormalizeGameName();

            var gameId = kv["appID"].Value;
            if (gameId == "228980")
                return null;

            var installDir = Path.Combine((new FileInfo(path)).Directory.FullName, "common", kv["installDir"].Value);
            if (!Directory.Exists(installDir))
            {
                installDir = Path.Combine((new FileInfo(path)).Directory.FullName, "music", kv["installDir"].Value);
                if (!Directory.Exists(installDir))
                {
                    installDir = string.Empty;
                }
            }

            var game = new LauncherGameInfo()
            {
                Id = gameId,
                Name = name,
                InstallDirectory = installDir,
                LauncherUrl = string.Format(GameLaunchUrl, gameId),
                PreviewImageUrl = string.Format(HeaderImageUrl, gameId),
                ExecutableName = FindExecutableName(gameId.ToInteger()),
                Launcher = GameLauncherType.Steam
            };

            return game;
        }

        static List<string> GetLibraryFolders(KeyValue foldersData)
        {
            var dbs = new List<string>();
            foreach (var child in foldersData.Children)
            {
                int val;
                if (int.TryParse(child.Name, out val))
                {
                    if (!string.IsNullOrEmpty(child.Value) && Directory.Exists(child.Value))
                        dbs.Add(child.Value);
                    else if (child.Children != null && child.Children.Count > 0)
                    {
                        var path = child.Children.FirstOrDefault(a => a.Name != null && a.Name.Equals("path", StringComparison.OrdinalIgnoreCase) == true);
                        if (!string.IsNullOrEmpty(path.Value) && Directory.Exists(path.Value))
                            dbs.Add(path.Value);
                    }
                }
            }

            return dbs;
        }

        /// <summary>
        /// Get Steam installation path in registry
        /// </summary>
        /// <returns>Steam install path</returns>
        public static string GetInstallPath()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam"))
                {
                    if (key != null)
                    {
                        var o = key.GetValue("InstallPath");
                        if (o != null)
                            return o as string;
                    }
                }
            }
            catch { }

            return null;
        }
    }

}


namespace EmulatorLauncher.Common.Launchers.Steam
{
    [Flags]
    public enum SteamAppStateFlags
    {
        Invalid = 0,
        Uninstalled = 1,
        UpdateRequired = 2,
        FullyInstalled = 4,
        Encrypted = 8,
        Locked = 16,
        FilesMissing = 32,
        AppRunning = 64,
        FilesCorrupt = 128,
        UpdateRunning = 256,
        UpdatePaused = 512,
        UpdateStarted = 1024,
        Uninstalling = 2048,
        BackupRunning = 4096,
        Reconfiguring = 65536,
        Validating = 131072,
        AddingFiles = 262144,
        Preallocating = 524288,
        Downloading = 1048576,
        Staging = 2097152,
        Committing = 4194304,
        UpdateStopping = 8388608
    }
}