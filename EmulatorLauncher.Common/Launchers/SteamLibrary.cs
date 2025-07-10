using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Win32;
using Steam_Library_Manager.Framework;
using EmulatorLauncher.Common.Launchers.Steam;
using Newtonsoft.Json;

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
        
        public static string GetSteamGameExecutableName(Uri uri, string steamdb)
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

            // First : Method to get steam executable name from RetroBat database file first
            if (File.Exists(steamdb))
            {
                string json = File.ReadAllText(steamdb);
                string steamApp = steamAppId.ToString();
                var appMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                if (appMap != null && appMap.TryGetValue(steamApp, out string exeName))
                {
                    SimpleLogger.Instance.Info("[STEAM] STEAM game executable found in json file: " + exeName);
                    return exeName;
                }
            }

            SimpleLogger.Instance.Info("[WARNING] Cannot find STEAM game executable in steamexecutables.json. Use the .gameexe method or registry monitoring will be used.");
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
