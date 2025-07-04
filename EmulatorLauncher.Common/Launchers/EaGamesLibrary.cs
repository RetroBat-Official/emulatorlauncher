using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Xml.Linq;

namespace EmulatorLauncher.Common.Launchers
{
    public static class EaGamesLibrary
    {
        public static LauncherGameInfo[] GetInstalledGames()
        {
            var games = new List<LauncherGameInfo>();

            var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Ea Games", false);
            if (key != null)
            {
                foreach (var subkeyName in key.GetSubKeyNames())
                {
                    var subKey = key.OpenSubKey(subkeyName, false);
                    if (subKey == null)
                        continue;

                    string displayName = subKey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrEmpty(displayName))
                        continue;

                    string installDir = subKey.GetValue("Install Dir")?.ToString();
                    if (string.IsNullOrEmpty(installDir))
                        continue;

                    string xmlPath = Path.Combine(installDir, "__Installer/installerdata.xml");
                    if (!File.Exists(xmlPath))
                        continue;

                    var doc = XDocument.Load(xmlPath);
                    var contentId = doc.Element("DiPManifest")?.Elements("contentIDs")?.Select(c => c.Element("contentID"))?.Select(c => c.Value)?.FirstOrDefault();

                    var launcher = doc
                        .Element("DiPManifest")?
                        .Elements("runtime")?
                        .Elements("launcher")?
                        .Where(c => c.Element("trial")?.Value != "1");

                    var filepath = launcher.Select(c => c.Element("filePath"))?.Select(c => c.Value)?.FirstOrDefault();
                    if (string.IsNullOrEmpty(filepath))
                        continue;

                    filepath = System.Text.RegularExpressions.Regex.Replace(filepath, @"\[[^\[\]]*\]", installDir);

                    var parameters = launcher.Select(c => c.Element("parameters"))?.Select(c => c.Value)?.FirstOrDefault();

                    var game = new LauncherGameInfo()
                    {
                        Id = contentId,
                        Name = displayName,
                        InstallDirectory = installDir,
                        LauncherUrl = filepath,
                        Parameters = parameters,
                        ExecutableName = Path.GetFileName(filepath),
                        Launcher = GameLauncherType.EaGames
                    };

                    games.Add(game);
                }

                key.Close();
            }
            return games.ToArray();
        }
    }
}
