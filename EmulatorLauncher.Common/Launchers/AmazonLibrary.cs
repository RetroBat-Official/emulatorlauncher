using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using EmulatorLauncher.Common.FileFormats;
using System.Data.Common;
using EmulatorLauncher.Common.Launchers.Amazon;

namespace EmulatorLauncher.Common.Launchers
{
    public static class AmazonLibrary
    {
        static AmazonLibrary()
        {
            SQLiteInteropManager.InstallSQLiteInteropDll();
        }

        const string GameLaunchUrl = @"amazon-games://play/{0}";

        public static LauncherGameInfo[] GetInstalledGames()
        {
            var games = new List<LauncherGameInfo>();

            if (!IsInstalled)
                return games.ToArray();

            using (var db = new SQLiteConnection("Data Source = " + GetDatabasePath()))
            {
                db.Open();

                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT * FROM DbSet WHERE Installed = 1;";

                var reader = cmd.ExecuteReader();

                var list = reader.ReadObjects<AmazonInstalledGameInfo>();
                if (list != null)
                {
                    foreach (var app in list)
                    {
                        if (!Directory.Exists(app.InstallDirectory))
                            continue;

                        var game = new LauncherGameInfo()
                        {
                            Id = app.Id,
                            Name = app.ProductTitle,
                            InstallDirectory = Path.GetFullPath(app.InstallDirectory),
                            LauncherUrl = string.Format(GameLaunchUrl, app.Id),   
                            ExecutableName = GetAmazonGameExecutable(app.InstallDirectory),
                            Launcher = GameLauncherType.Amazon
                        };

                        games.Add(game);
                    }
                }


                db.Close();
            }
            
            return games.ToArray();
        }

        public static bool IsInstalled
        {
            get
            {
                return File.Exists(GetDatabasePath());
            }
        }

        static string GetDatabasePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string amazonDB = Path.Combine(appData, "Amazon Games", "Data", "Games", "Sql", "GameInstallInfo.sqlite");
            if (File.Exists(amazonDB))
                return amazonDB;

            return null;
        }

        public static string GetAmazonGameExecutableName(Uri uri)
        {
            if (!IsInstalled)
                return null;

            string shorturl = uri.AbsolutePath.Substring(1);

            string amazonDB = GetDatabasePath();
            if (File.Exists(amazonDB))
            {
                string gameInstallPath = null;

                using (var db = new SQLiteConnection("Data Source = " + amazonDB))
                {
                    db.Open();

                    var cmd = db.CreateCommand();
                    cmd.CommandText = "SELECT installDirectory FROM DbSet where Id = '" + shorturl + "'";

                    var reader = cmd.ExecuteReader();

                    if (!reader.HasRows)
                    {
                        db.Close();
                        throw new ApplicationException("There is a problem: the Game is not installed in Amazon Launcher");
                    }

                    while (reader.Read())
                        gameInstallPath = reader.GetString(0);

                    db.Close();
                }

                var exe = GetAmazonGameExecutable(gameInstallPath);
                if (string.IsNullOrEmpty(exe))
                    throw new ApplicationException("There is a problem: Game is not installed");

                return Path.GetFileNameWithoutExtension(exe);
            }

            throw new ApplicationException("There is a problem: Amazon Launcher is not installed or the Game is not installed");
        }

        private static string GetAmazonGameExecutable(string path)
        {
            string fuelFile = Path.Combine(path, "fuel.json");
            string gameexe = null;

            if (!File.Exists(fuelFile))
                throw new ApplicationException("There is a problem: game executable cannot be found");

            var json = DynamicJson.Load(fuelFile);

            var jsonMain = json.GetObject("Main");
            if (jsonMain == null)
                return null;

            gameexe = jsonMain["Command"];

            if (!string.IsNullOrEmpty(gameexe))
                return gameexe;

            return null;
        }

    }
}

namespace EmulatorLauncher.Common.Launchers.Amazon
{
    public class AmazonInstalledGameInfo
    {
        public string Id { get; set; }
        public string InstallDirectory { get; set; }
        public int Installed { get; set; }
        public string ProductTitle { get; set; }
        public string ProductAsin { get; set; }
        public string ManifestSignature { get; set; }
        public string ManifestSignatureKeyId { get; set; }
    }
}
