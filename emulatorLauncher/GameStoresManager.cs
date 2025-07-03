using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Launchers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EmulatorLauncher
{
    class GameStoresManager
    {
        public static void UpdateGames()
        {
            ImportStore("epic", EpicLibrary.GetInstalledGames);
            ImportStore("amazon", AmazonLibrary.GetInstalledGames);
            ImportStore("steam", SteamLibrary.GetInstalledGames);
        }

        private static void ImportStore(string name, Func<LauncherGameInfo[]> getInstalledGames)
        {
            try
            {
                var roms = Program.AppConfig.GetFullPath("roms");

                var dir = Path.Combine(roms, name);
                Directory.CreateDirectory(dir);

                var files = new HashSet<string>(Directory.GetFiles(dir, "*.url"));

                foreach (var game in getInstalledGames())
                {
                    string path = Path.Combine(dir, game.Name + ".url");
                    if (files.Contains(path))
                    {
                        files.Remove(path);
                        continue;
                    }

                    File.WriteAllText(path, "[InternetShortcut]\r\nURL=" + game.LauncherUrl);
                }

                foreach (var file in files)
                    FileTools.TryDeleteFile(file);
            }
            catch (Exception ex) { SimpleLogger.Instance.Error("[ImportStore] " + name + " : " + ex.Message, ex); }
        }

    }
}
