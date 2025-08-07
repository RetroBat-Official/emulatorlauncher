using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using Microsoft.Win32;
using EmulatorLauncher.Common.Launchers.Gog;
using Newtonsoft.Json;
using System.Linq;

namespace EmulatorLauncher.Common.Launchers
{
    public static class GogLibrary
    {
        static GogLibrary()
        {
            SQLiteInteropManager.InstallSQLiteInteropDll();
        }

        public static LauncherGameInfo[] GetInstalledGames()
        {
            var games = new List<LauncherGameInfo>();

            if (!IsInstalled)
                return games.ToArray();

            using (var db = new SQLiteConnection("Data Source = " + GetDatabasePath()))
            {
                db.Open();

                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT IB.productId, IB.installationPath, LD.title, LD.images FROM InstalledBaseProducts IB LEFT OUTER JOIN LimitedDetails as LD ON IB.productId = LD.productId; ";

                var reader = cmd.ExecuteReader();

                var list = reader.ReadObjects<GogInstalledGameInfo>();
                if (list != null)
                {
                    foreach (var app in list)
                    {                                         
                        if (!Directory.Exists(app.installationPath))
                            continue;

                        var exeInfo = GetExecutableInfo(app.productId, app.installationPath);
                        if (exeInfo == null || string.IsNullOrEmpty(exeInfo.Path))
                            continue;

                        // Store the actual executable info for monitoring
                        string actualExecutable = Path.Combine(app.installationPath, exeInfo.Path);
                        
                        var game = new LauncherGameInfo()
                        {
                            Id = app.productId.ToString(),
                            Name = app.title,
                            InstallDirectory = Path.GetFullPath(app.installationPath),
                            LauncherUrl = GetGogGalaxyPath(),   
                            ExecutableName = Path.GetFileName(actualExecutable),
                            Parameters = $"/command=runGame /gameId={app.productId}",
                            Launcher = GameLauncherType.Gog
                        };
                        
                        // Store the actual executable path for monitoring purposes
                        game.PreviewImageUrl = actualExecutable; // Temporary storage, will be used by launcher
                      
                        games.Add(game);
                    }
                }


                db.Close();
            }
            
            return games.ToArray();
        }

        public static bool IsInstalled { get { return File.Exists(GetDatabasePath()); } }

        static string GetGogGalaxyPath()
        {
            // First, try to find the path in the registry (most reliable method, with user-provided path)
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\GOG.com\\GalaxyClient\\paths"))
                {
                    if (key != null)
                    {
                        object path = key.GetValue("client");
                        if (path != null)
                        {
                            string clientPath = path.ToString();
                            if (File.Exists(clientPath))
                                return clientPath;
                        }
                    }
                }
            }
            catch {}

            // Fallback to searching Uninstall keys
            try
            {
                string uninstallKey = "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
                using (var key = Registry.LocalMachine.OpenSubKey(uninstallKey))
                {
                    if (key != null)
                    {
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            using (var subkey = key.OpenSubKey(subkeyName))
                            {
                                if (subkey != null && subkey.GetValue("DisplayName") as string == "GOG Galaxy")
                                {
                                    string installLocation = subkey.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrEmpty(installLocation))
                                    {
                                        string clientPath = Path.Combine(installLocation, "GalaxyClient.exe");
                                        if (File.Exists(clientPath))
                                            return clientPath;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback to common installation paths if registry fails
            string[] possiblePaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "GalaxyClient.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "GalaxyClient.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GOG.com", "Galaxy", "GalaxyClient.exe")
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Default fallback if nothing else works
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "GalaxyClient.exe");
        }

        static string GetDatabasePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string gogDB = Path.Combine(appData, "GOG.com", "Galaxy", "storage", "Galaxy-2.0.db");
            if (File.Exists(gogDB))
                return gogDB;

            return null;
        }

        public static string GetExecutablePathForGameId(string gameId)
        {
            if (!int.TryParse(gameId, out int productId))
                return null;

            if (!IsInstalled)
                return null;

            using (var db = new SQLiteConnection("Data Source = " + GetDatabasePath()))
            {
                db.Open();

                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT installationPath FROM InstalledBaseProducts WHERE productId = @productId";
                cmd.Parameters.AddWithValue("@productId", productId);

                var installationPath = cmd.ExecuteScalar() as string;
                if (string.IsNullOrEmpty(installationPath) || !Directory.Exists(installationPath))
                {
                    db.Close();
                    return null;
                }

                var exeInfo = GetExecutableInfo(productId, installationPath);
                db.Close();

                if (exeInfo == null || string.IsNullOrEmpty(exeInfo.Path))
                    return null;

                return Path.Combine(installationPath, exeInfo.Path);
            }
        }

        static GogPlayTask GetExecutableInfo(int productId, string installationPath)
        {
            string fn = Path.Combine(installationPath, "goggame-" + productId + ".info");
            if (!File.Exists(fn))
                return null;

            var product = JsonConvert.DeserializeObject<GogProduct>(File.ReadAllText(fn));
            if (product == null || product.PlayTasks == null)
                return null;

            return product.PlayTasks.Where(p => p.Category == "game").FirstOrDefault();
        }
    }
}

namespace EmulatorLauncher.Common.Launchers.Gog
{
    public class GogProduct
    {
        [JsonProperty("languages")]
        public List<string> Languages { get; set; }

        [JsonProperty("osBitness")]
        public List<string> OsBitness { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("gameId")]
        public string GameId { get; set; }

        [JsonProperty("rootGameId")]
        public string RootGameId { get; set; }

        [JsonProperty("playTasks")]
        public List<GogPlayTask> PlayTasks { get; set; }

        [JsonProperty("buildId")]
        public string BuildId { get; set; }
    }

    public class GogPlayTask
    {
        [JsonProperty("languages")]
        public List<string> Languages { get; set; }

        [JsonProperty("osBitness")]
        public List<string> OsBitness { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("isPrimary")]
        public bool IsPrimary { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
        
    }

    public class GogInstalledGameInfo
    {        
        public int productId { get; set; }
        public string installationPath { get; set; }
        public string title { get; set; }
        public string images { get; set; }        
    }
}
