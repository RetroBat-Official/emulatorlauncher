using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using Microsoft.Win32;
using System.Runtime.Serialization;
using System.Threading;
using System.Security.Policy;

namespace emulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class EpicGameLauncher : GameLauncher
        {
            public EpicGameLauncher(Uri uri)
            {
                LauncherExe = GetEpicGameExecutableName(uri);
            }

            private string GetEpicGameExecutableName(Uri uri)
            {
                string shorturl = uri.LocalPath.ExtractString("/", ":");

                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Epic Games\\EOS"))
                    {
                        if (key != null)
                        {
                            Object o = key.GetValue("ModSdkMetadataDir");
                            if (o != null)
                            {
                                string manifestPath = o.ToString();

                                List<EpicGames> games = new List<EpicGames>();

                                foreach (var file in Directory.EnumerateFiles(manifestPath, "*.item"))
                                {
                                    var rr = JsonSerializer.DeserializeString<EpicGames>(File.ReadAllText(file));
                                    if (rr != null)
                                        games.Add(rr);
                                }

                                string gameExecutable = null;

                                if (games.Count > 0)
                                    gameExecutable = games.Where(i => i.CatalogNamespace.Equals(shorturl)).Select(i => i.LaunchExecutable).FirstOrDefault();

                                if (gameExecutable != null)
                                    return Path.GetFileNameWithoutExtension(gameExecutable);
                            }
                        }
                    }
                }
                catch
                {
                    throw new ApplicationException("There is a problem: Epic Launcher is not installed or the Game is not installed");
                }

                return null;
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                Process process = Process.Start(path);

                int i = 1;
                Process[] game = Process.GetProcessesByName(LauncherExe);

                while (i <= 5 && game.Length == 0)
                {
                    game = Process.GetProcessesByName(LauncherExe);
                    Thread.Sleep(6000);
                    i++;
                }

                Process[] epic = Process.GetProcessesByName("EpicGamesLauncher");

                if (game.Length == 0)
                    return 0;

                Process epicGame = game.OrderBy(p => p.StartTime).FirstOrDefault();
                Process epicLauncher = null;

                if (epic.Length > 0)
                    epicLauncher = epic.OrderBy(p => p.StartTime).FirstOrDefault();

                epicGame.WaitForExit();

                if (Program.SystemConfig.isOptSet("notkillsteam") && Program.SystemConfig.getOptBoolean("notkillsteam"))
                    return 0;

                if (epicLauncher != null)
                    epicLauncher.Kill();

                return 0;
            }

            [DataContract]
            public class EpicGames
            {
                [DataMember]
                public string CatalogNamespace { get; set; }

                [DataMember]
                public string LaunchExecutable { get; set; }
            }
        }
    }
}
