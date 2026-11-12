using EmulatorLauncher.Common.Launchers.Steam;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steam_Library_Manager.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

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

            SimpleLogger.Instance.Info("[Steam] Found " + games.Count + " installed games.");
            return games.ToArray();
        }

        public static LauncherGameInfo[] GetAllGames(string retrobatPath, bool getUninstalled = false)
        {
            var ret = new List<LauncherGameInfo>();
            var allGames = new Dictionary<string, LauncherGameInfo>();
            var apiGames = new Dictionary<string, JToken>();
            var ownedGamesFromApi = new List<LauncherGameInfo>();

            if (getUninstalled)
            {
                // 1. Get API Key from file
                string apiKey = null;
                try
                {
                    if (!string.IsNullOrEmpty(retrobatPath))
                    {
                        string apiKeyPath = Path.Combine(retrobatPath, "user", "apikey", "steam.apikey");
                        if (File.Exists(apiKeyPath))
                        {
                            apiKey = File.ReadAllLines(apiKeyPath).FirstOrDefault().Trim();
                            if (string.IsNullOrEmpty(apiKey))
                                SimpleLogger.Instance.Warning("[Steam] steam.apikey file is empty.");
                            else
                                SimpleLogger.Instance.Info("[Steam] Found Steam API Key.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error("[Steam] Error reading steam.apikey file: " + ex.Message, ex);
                }

                string steamId64 = GetSteamId64();

                // 2. Call the API if the key exists
                if (!string.IsNullOrEmpty(apiKey))
                {

                    if (string.IsNullOrEmpty(steamId64))
                    {
                        SimpleLogger.Instance.Error("[Steam] Could not find user SteamID64. Cannot fetch game list from API.");
                    }
                    else
                    {
                        string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={apiKey}&steamid={steamId64}&format=json&include_appinfo=1&include_played_free_games=true&skip_unvetted_apps=false";

                        SimpleLogger.Instance.Info("[Steam] Calling Steam Web API: " + url.Replace(apiKey, "REDACTED"));

                        try
                        {
                            var webClient = new System.Net.WebClient();
                            webClient.Encoding = Encoding.UTF8;
                            string json = webClient.DownloadString(url);

                            var response = Newtonsoft.Json.Linq.JObject.Parse(json)["response"];
                            if (response != null && response.ToString() != "{}")
                            {
                                var responseGames = response["games"];
                                if (responseGames != null)
                                {
                                    SimpleLogger.Instance.Info("[Steam] Found " + responseGames.Count() + " games from Steam Web API.");
                                    Parallel.ForEach(responseGames, g =>
                                    {
                                        string appId = g["appid"]?.ToString();
                                        string name = g["name"]?.ToString();
                                        string iconURL = g["img_icon_url"]?.ToString();

                                        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name))
                                            return;

                                        string icoPath = Path.Combine(Path.GetTempPath(), $"{appId}.ico");
                                        string tempJpg = Path.Combine(Path.GetTempPath(), $"{appId}.jpg");
                                        string jpgUrl = $"http://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconURL}.jpg";

                                        // Download + convert
                                        try
                                        {
                                            if (DownloadImage(jpgUrl, tempJpg))
                                            {
                                                using (Bitmap bmp = new Bitmap(tempJpg))
                                                    SaveBitmapAsIcon(bmp, icoPath);
                                                File.Delete(jpgUrl);
                                            }
                                        }
                                        catch { }

                                        var gameToAdd = new LauncherGameInfo()
                                        {
                                            Id = appId,
                                            Name = name,
                                            LauncherUrl = string.Format(GameLaunchUrl, appId),
                                            PreviewImageUrl = string.Format(HeaderImageUrl, appId),
                                            Launcher = GameLauncherType.Steam,
                                            IconPath = File.Exists(icoPath) ? icoPath : null
                                        };

                                        lock (ownedGamesFromApi)
                                        {
                                            ownedGamesFromApi.Add(gameToAdd);
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.Instance.Info("[Steam] Steam API call failed. Using local data only. Error: " + ex.Message);
                        }
                    }
                }
                else
                {
                    SimpleLogger.Instance.Info("[Steam] API Key not found, trying public profile.");
                    try
                    {
                        if (!string.IsNullOrEmpty(steamId64))
                        {
                            string xmlUrl = $"https://steamcommunity.com/profiles/{steamId64}/games/?tab=all&xml=1";

                            SimpleLogger.Instance.Info("[Steam] Fetching games from public profile XML: " + xmlUrl);

                            XDocument doc;
                            using (var wc = new WebClient { Encoding = System.Text.Encoding.UTF8 })
                            {
                                string xml = wc.DownloadString(xmlUrl);
                                doc = XDocument.Parse(xml);
                            }

                            var xmlGames = doc.Descendants("game").ToList();
                            var allAppIds = xmlGames.Select(g => g.Element("appID")?.Value).Where(id => !string.IsNullOrEmpty(id));

                            var parallelOpts = new ParallelOptions { MaxDegreeOfParallelism = 8 };

                            Parallel.ForEach(xmlGames, parallelOpts, g =>
                            {
                                string appId = g.Element("appID")?.Value;
                                string name = g.Element("name")?.Value;
                                string iconURL = g.Element("img_icon_url")?.Value;

                                if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name))
                                    return;

                                string icoPath = Path.Combine(Path.GetTempPath(), $"{appId}.ico");
                                string tempJpg = Path.Combine(Path.GetTempPath(), $"{appId}.jpg");
                                string jpgUrl = $"http://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{iconURL}.jpg";

                                try
                                {
                                    if (!string.IsNullOrEmpty(iconURL) && DownloadImage(jpgUrl, tempJpg))
                                    {
                                        using (Bitmap bmp = new Bitmap(tempJpg))
                                            SaveBitmapAsIcon(bmp, icoPath);
                                        File.Delete(tempJpg);
                                    }
                                }
                                catch { }

                                var gameToAdd = new LauncherGameInfo()
                                {
                                    Id = appId,
                                    Name = name,
                                    LauncherUrl = string.Format(GameLaunchUrl, appId),
                                    PreviewImageUrl = string.Format(HeaderImageUrl, appId),
                                    Launcher = GameLauncherType.Steam,
                                    IconPath = File.Exists(icoPath) ? icoPath : null
                                };

                                lock (allGames) // protect shared dictionary + list
                                {
                                    if (!allGames.ContainsKey(appId))
                                    {
                                        ownedGamesFromApi.Add(gameToAdd);
                                        allGames.Add(appId, gameToAdd);
                                    }
                                }
                            });

                            SimpleLogger.Instance.Info("[Steam] Added " + ownedGamesFromApi.Count + " games from public profile XML.");
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Warning("[Steam] Failed to fetch games from public profile XML: " + ex.Message);
                    }
                }
            }

            // First, add installed games. They have more accurate information.
            var installedGames = GetInstalledGames();
            foreach (var game in installedGames)
            {
                if (!allGames.ContainsKey(game.Id))
                {
                    allGames.Add(game.Id, game);
                }
            }

            // Then, add all other owned games from the API list we fetched earlier.
            if (getUninstalled)
            {
                var nonInstalledGames = ownedGamesFromApi.Where(g => !allGames.ContainsKey(g.Id)).ToList();
                SimpleLogger.Instance.Info("[Steam] Found " + nonInstalledGames.Count + " non-installed games.");

                foreach (var game in nonInstalledGames)
                {
                    // The check is redundant here since we know they are not in allGames, but it's safe
                    if (!allGames.ContainsKey(game.Id))
                    {
                        allGames.Add(game.Id, game);
                    }
                }
            }

            return allGames.Values.ToArray();
        }

        private static void SaveBitmapAsIcon(Bitmap bmp, string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                using (Bitmap newBmp = new Bitmap(bmp, new Size(32, 32)))
                {
                    Icon icon = Icon.FromHandle(newBmp.GetHicon());

                    using (MemoryStream ms = new MemoryStream())
                    {
                        icon.Save(ms);
                        byte[] iconData = ms.ToArray();
                        fs.Write(iconData, 0, iconData.Length);
                    }
                }
            }
        }

        private static bool DownloadImage(string url, string filePath)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(url, filePath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }


        private static string GetSteamId64()
        {
            string steamPath = GetInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return null;

            string loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(loginUsersPath))
                return null;

            try
            {
                string vdfText = File.ReadAllText(loginUsersPath);
                var regex = new Regex("\"(\\d{17})\"\\s*\\{[^}]*\"MostRecent\"\\s*\"1\"", RegexOptions.IgnoreCase);
                var match = regex.Match(vdfText);

                if (match.Success)
                    return match.Groups[1].Value;

                // Fallback
                regex = new Regex("\"(\\d{17})\"");
                match = regex.Match(vdfText);
                if (match.Success)
                    return match.Groups[1].Value;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Steam] Error finding SteamID64: " + ex.Message, ex);
            }

            return null;
        }

        public static string GetSteamGameExecutableName(Uri uri, string steamdb, out string shorturl)
        {
            // Get Steam app ID from url
            shorturl = uri.AbsolutePath.Substring(1);
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

            string exe = null;

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

            // Then : Call method to get executable name from Steam vdf files
            if (exe == null)
                exe = FindExecutableName(steamAppId);

            if (string.IsNullOrEmpty(exe))
            {
                SimpleLogger.Instance.Info("[WARNING] Cannot find STEAM game executable in appinfo.vdf.");
                return null;
            }
            else
                SimpleLogger.Instance.Info("[STEAM] STEAM game executable found: " + exe);

            return Path.GetFileNameWithoutExtension(exe);
        }

        /// <summary>
        /// Find the executable of a Steam game in appinfo.vdf ('launch' section)
        /// </summary>
        /// <returns>Executable path relative to the game folder, null if not found</returns>
        static string FindExecutableName(int steamAppId, string installDir = null)
        {
            // Non-Steam shortcuts are not listed in appinfo.vdf
            if (steamAppId <= 0)
                return null;

            // Get Steam installation path in registry
            string path = GetInstallPath();
            if (string.IsNullOrEmpty(path))
                throw new ApplicationException("Can not find Steam installation folder in registry.");

            string appinfoPath = Path.Combine(path, "appcache", "appinfo.vdf");
            if (!File.Exists(appinfoPath))
            {
                SimpleLogger.Instance.Info("[WARNING] Missing file " + appinfoPath);
                return null;
            }

            // Game folder is used to check that the executable really belongs to the game
            if (string.IsNullOrEmpty(installDir))
                installDir = GetGameInstallDirectory(steamAppId);

            // Try to get executable by deserializing vdf file (only the requested app is read)
            try
            {
                var reader = new SteamAppInfoReader();
                reader.Read(appinfoPath, (uint)steamAppId);

                SimpleLogger.Instance.Info("[INFO] Reading Steam file 'appinfo.vdf'");

                var app = reader.Apps.FirstOrDefault(a => a.AppID == steamAppId);
                if (app == null)
                {
                    SimpleLogger.Instance.Info("[INFO] Game \"" + steamAppId + "\" not found in 'appinfo.vdf'");
                    return null;
                }

                SimpleLogger.Instance.Info("[INFO] Found Game \"" + steamAppId + "\" in 'appinfo.vdf'");

                string executable = SelectLaunchExecutable(app.Data, installDir);
                if (string.IsNullOrEmpty(executable))
                    SimpleLogger.Instance.Info("[WARNING] No Windows executable found in the launch options of the game.");
                else
                    SimpleLogger.Instance.Info("[INFO] Game executable " + executable + " found.");

                return executable;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Info("[WARNING] Impossible to read SteamAppInfo: " + ex.GetType().Name + " - " + ex.Message + (ex.InnerException != null ? " / " + ex.InnerException.Message : ""));
            }

            // Last resort, only when appinfo.vdf cannot be read (unknown file format)
            return FindExecutableNameAlternative(appinfoPath, steamAppId, installDir);
        }

        /// <summary>
        /// Select, in the 'launch' section of a game, the executable that Steam is the most likely to run on Windows
        /// </summary>
        static string SelectLaunchExecutable(ValveKeyValue.KVObject appData, string installDir)
        {
            string osArch = Environment.Is64BitOperatingSystem ? "64" : "32";
            var candidates = new List<KeyValuePair<int, string>>();

            foreach (var entry in appData.Traverse(d => d.Children).Where(d => d.Children.Any(c => c.Name == "executable")))
            {
                string executable = GetChildValue(entry, "executable");
                if (string.IsNullOrEmpty(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                var config = entry.Children.FirstOrDefault(c => c.Name == "config");
                string osList = GetChildValue(config, "oslist");
                string arch = GetChildValue(config, "osarch");
                string betaKey = GetChildValue(config, "betakey");
                string type = GetChildValue(entry, "type");

                // Entry dedicated to another OS
                if (!string.IsNullOrEmpty(osList) && osList.IndexOf("windows", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                executable = executable.Replace("/", "\\");

                // Lowest score is the best candidate
                int score = 0;

                if (string.IsNullOrEmpty(type) || type == "none")
                    score += 1;     // no type
                else if (type != "default")
                    score += 10;    // config tool, server, editor, vr...

                if (!string.IsNullOrEmpty(arch) && arch != osArch)
                    score += 20;    // other architecture

                if (!string.IsNullOrEmpty(betaKey))
                    score += 40;    // only available on a beta branch

                if (!string.IsNullOrEmpty(installDir) && !ExistsInGameFolder(installDir, executable))
                    score += 80;    // not present in the game folder

                SimpleLogger.Instance.Info("[STEAM] Launch option: " + executable + " (type: " + type + ", osarch: " + arch + ", betakey: " + betaKey + ", score: " + score + ")");
                candidates.Add(new KeyValuePair<int, string>(score, executable));
            }

            // OrderBy is stable : for an equal score, the order of appinfo.vdf is kept
            return candidates.OrderBy(c => c.Key).Select(c => c.Value).FirstOrDefault();
        }

        static string GetChildValue(ValveKeyValue.KVObject node, string name)
        {
            if (node == null)
                return null;

            return node.Children.Where(c => c.Name == name && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
        }

        static bool ExistsInGameFolder(string installDir, string executable)
        {
            try { return File.Exists(Path.Combine(installDir, executable)); }
            catch { return false; }
        }

        /// <summary>
        /// Raw search in appinfo.vdf : looks for the .exe names following the app ID, and keeps the first one that exists in the game folder
        /// </summary>
        static string FindExecutableNameAlternative(string appinfoPath, int steamAppId, string installDir)
        {
            // Without the game folder, the result cannot be checked
            if (string.IsNullOrEmpty(installDir))
            {
                SimpleLogger.Instance.Info("[WARNING] Game folder not found, cannot search executable with the alternative method.");
                return null;
            }

            try
            {
                SimpleLogger.Instance.Info("[INFO] Searching executable in Steam file 'appinfo.vdf' : alternative method");

                string appInfo = File.ReadAllText(appinfoPath);
                string appId = steamAppId.ToString();
                var tested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                int index = appInfo.IndexOf(appId, StringComparison.Ordinal);
                while (index != -1)
                {
                    // Launch options are stored shortly after the app ID : check the .exe names found in this area
                    int limit = Math.Min(appInfo.Length, index + 16384);
                    int exeIndex = appInfo.IndexOf(".exe", index, limit - index, StringComparison.OrdinalIgnoreCase);

                    while (exeIndex != -1)
                    {
                        int end = exeIndex + 4;

                        // Keep only the values ending with .exe
                        if (end < appInfo.Length && appInfo[end] == '\0')
                        {
                            int start = appInfo.LastIndexOf('\0', exeIndex) + 1;
                            string executable = appInfo.Substring(start, end - start).Replace("/", "\\");

                            if (tested.Add(executable) && ExistsInGameFolder(installDir, executable))
                            {
                                SimpleLogger.Instance.Info("[INFO] Game executable " + executable + " found (alternative method).");
                                return executable;
                            }
                        }

                        exeIndex = end < limit ? appInfo.IndexOf(".exe", end, limit - end, StringComparison.OrdinalIgnoreCase) : -1;
                    }

                    index = appInfo.IndexOf(appId, index + appId.Length, StringComparison.Ordinal);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Info("[WARNING] Impossible to find Steam executable name : " + ex.Message);
            }

            SimpleLogger.Instance.Info("[WARNING] No executable of the game folder found in 'appinfo.vdf' : consider .gameexe method.");
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
                catch { }
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
                LauncherUrl = string.Format(GameLaunchUrl, gameId) + "\"" + " -silent",
                PreviewImageUrl = string.Format(HeaderImageUrl, gameId),
                ExecutableName = FindExecutableName(gameId.ToInteger(), installDir),
                Launcher = GameLauncherType.Steam,
                IsInstalled = true
            };

            if (!string.IsNullOrEmpty(game.ExecutableName) && !string.IsNullOrEmpty(game.InstallDirectory))
            {
                // Executable is relative to the install folder (no extension when found with the alternative method)
                string exe = game.ExecutableName.Replace("/", "\\");
                if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    exe += ".exe";

                var iconFile = Path.Combine(game.InstallDirectory, exe);
                if (File.Exists(iconFile))
                    game.IconPath = iconFile;
            }

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
        /// Get the folder of an installed Steam game (from its appmanifest file)
        /// </summary>
        public static string GetGameInstallDirectory(int steamAppId)
        {
            try
            {
                var libraryfolders = new KeyValue();
                libraryfolders.ReadFileAsText(Path.Combine(GetInstallPath(), "config", "libraryfolders.vdf"));

                foreach (var folder in GetLibraryFolders(libraryfolders))
                {
                    string manifest = Path.Combine(folder, "steamapps", "appmanifest_" + steamAppId + ".acf");
                    if (!File.Exists(manifest))
                        continue;

                    var kv = new KeyValue();
                    using (var fs = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        kv.ReadAsText(fs);

                    string installDir = Path.Combine(folder, "steamapps", "common", kv["installDir"].Value);
                    if (Directory.Exists(installDir))
                        return installDir;
                }
            }
            catch { }

            return null;
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