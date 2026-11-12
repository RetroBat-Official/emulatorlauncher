using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Launchers.Amazon;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    Parallel.ForEach(list, app =>
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(app.InstallDirectory) || !Directory.Exists(app.InstallDirectory))
                                return;

                            // Executable can be unknown (no fuel.json) : the game is still imported, without icon
                            var exeName = GetAmazonGameExecutable(app.InstallDirectory);
                            var installDir = Path.GetFullPath(app.InstallDirectory);
                            var exePath = string.IsNullOrEmpty(exeName) ? null : Path.Combine(installDir, exeName);

                            var game = new LauncherGameInfo()
                            {
                                Id = app.Id,
                                Name = app.ProductTitle,
                                InstallDirectory = installDir,
                                LauncherUrl = string.Format(GameLaunchUrl, app.Id),
                                ExecutableName = exeName,
                                Launcher = GameLauncherType.Amazon,
                                IconPath = exePath != null && File.Exists(exePath) ? exePath : null
                            };

                            lock (games)
                            {
                                games.Add(game);
                            }
                        }
                        catch (Exception ex)
                        {
                            // One game in error must not stop the import of the others
                            SimpleLogger.Instance.Warning("[Amazon] Unable to import " + app.ProductTitle + " : " + ex.Message);
                        }
                    });
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
            string gameInstallPath = null;

            using (var db = new SQLiteConnection("Data Source = " + GetDatabasePath()))
            {
                db.Open();

                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT installDirectory FROM DbSet WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", shorturl);

                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                        gameInstallPath = reader.GetString(0);
                }

                db.Close();
            }

            if (string.IsNullOrEmpty(gameInstallPath) || !Directory.Exists(gameInstallPath))
                throw new ApplicationException("There is a problem: the Game is not installed in Amazon Launcher");

            // Executable can be unknown (no fuel.json) : the game is still launched, without process name
            var exe = GetAmazonGameExecutable(gameInstallPath);
            if (string.IsNullOrEmpty(exe))
            {
                SimpleLogger.Instance.Warning("[Amazon] Game executable not found in fuel.json.");
                return null;
            }

            return Path.GetFileNameWithoutExtension(exe);
        }

        /// <summary>
        /// Get the game executable from the fuel.json file of the game folder, null if unknown
        /// </summary>
        private static string GetAmazonGameExecutable(string path)
        {
            try
            {
                string fuelFile = Path.Combine(path, "fuel.json");
                if (!File.Exists(fuelFile))
                    return null;

                var json = DynamicJson.Load(fuelFile);

                var jsonMain = json.GetObject("Main");
                if (jsonMain == null)
                    return null;

                string gameexe = jsonMain["Command"];
                if (!string.IsNullOrEmpty(gameexe))
                    return gameexe;
            }
            catch { }

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
