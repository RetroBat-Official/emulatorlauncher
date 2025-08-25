using EmulatorLauncher.Common.Launchers.Gog;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                    var galaxyClientPath = GetGalaxyClientPath();

                    Parallel.ForEach(list, app =>
                    {
                        if (!Directory.Exists(app.installationPath)) return;

                        var exeInfo = GetExecutableInfo(app.productId, app.installationPath);
                        if (exeInfo == null || string.IsNullOrEmpty(exeInfo.Path)) return;

                        var exePath = Path.Combine(app.installationPath, exeInfo.Path);
                        bool exeExists = File.Exists(exePath);

                        var game = new LauncherGameInfo()
                        {
                            Id = app.productId.ToString(),
                            Name = app.title,
                            InstallDirectory = exeExists ? exePath : Path.GetFullPath(app.installationPath),
                            LauncherUrl = galaxyClientPath,
                            ExecutableName = Path.GetFileName(galaxyClientPath),
                            Parameters = $"/command=runGame /gameId={app.productId}",
                            Launcher = GameLauncherType.Gog,
                            IconPath = exeExists ? exePath : null
                        };

                        lock (games)
                        {
                            games.Add(game);
                        }
                    });
                }

                db.Close();
            }
            
            return games.ToArray();
        }

        public static string GetGOGGameById(string productId)
        {
            using (var db = new SQLiteConnection("Data Source = " + GetDatabasePath()))
            {
                db.Open();

                var cmd = db.CreateCommand();
                cmd.CommandText = @"
                    SELECT IB.productId, IB.installationPath, LD.title, LD.images
                    FROM InstalledBaseProducts IB
                    LEFT OUTER JOIN LimitedDetails AS LD ON IB.productId = LD.productId
                    WHERE IB.productId = @productId;
                ";
                cmd.Parameters.AddWithValue("@productId", productId);

                var reader = cmd.ExecuteReader();
                var list = reader.ReadObjects<GogInstalledGameInfo>();

                if (list != null && list.Length > 0)
                {
                    var app = list.First();

                    if (Directory.Exists(app.installationPath))
                    {
                        var exeInfo = GetExecutableInfo(app.productId, app.installationPath);
                        if (exeInfo != null && !string.IsNullOrEmpty(exeInfo.Path))
                        {
                            db.Close();
                            return Path.GetFileNameWithoutExtension(exeInfo.Path);
                        }
                    }
                }

                db.Close();
            }
            return null;
        }

        private static string GetGalaxyClientPath()
        {
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient"))
            {
                if (key != null)
                {
                    var path = key.GetValue("path") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        var exePath = Path.Combine(path, "GalaxyClient.exe");
                        if (File.Exists(exePath))
                            return exePath;
                    }
                }
            }
            return @"C:\Program Files (x86)\GOG Galaxy\GalaxyClient.exe"; // fallback
        }

        public static bool IsInstalled { get { return File.Exists(GetDatabasePath()); } }

        static string GetDatabasePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string gogDB = Path.Combine(appData, "GOG.com", "Galaxy", "storage", "Galaxy-2.0.db");
            if (File.Exists(gogDB))
                return gogDB;

            return null;
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
