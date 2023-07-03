using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using Microsoft.Win32;
using System.Runtime.Serialization;
using System.Threading;

namespace emulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class EpicGameLauncher : GameLauncher
        {
            public EpicGameLauncher(Uri uri)
            {
                string url = uri.ToString();

                _LauncherExeName = GetEpicGameExecutableName(url);
            }
            private string GetEpicGameExecutableName(string url)
            {
                string toRemove = "com.epicgames.launcher://apps/";
                string shorturl = url.ToString().Replace(toRemove, "");

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
                                    gameExecutable = games.Where(i => shorturl.StartsWith(i.CatalogNamespace)).Select(i => i.LaunchExecutable).FirstOrDefault();

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
                using (var frm = new System.Windows.Forms.Form())
                {
                    // Some games fail to allocate DirectX surface if EmulationStation is showing fullscren : pop an invisible window between ES & the game solves the problem
                    frm.ShowInTaskbar = false;
                    frm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
                    frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                    frm.Opacity = 0;
                    frm.Show();

                    System.Windows.Forms.Application.DoEvents();

                    Process process = Process.Start(path);

                    int i = 1;
                    Process[] game = Process.GetProcessesByName(_LauncherExeName);

                    while (i <= 5 && game.Length == 0)
                    {
                        game = Process.GetProcessesByName(_LauncherExeName);
                        Thread.Sleep(4000);
                        i++;
                    }
                    Process[] epic = Process.GetProcessesByName("EpicGamesLauncher");

                    if (game.Length == 0)
                        return 0;
                    else
                    {
                        Process epicGame = game.OrderBy(p => p.StartTime).FirstOrDefault();
                        Process epicLauncher = null;

                        if (epic.Length > 0)
                            epicLauncher = epic.OrderBy(p => p.StartTime).FirstOrDefault();

                        epicGame.WaitForExit();

                        if (Program.SystemConfig.isOptSet("notkillsteam") && Program.SystemConfig.getOptBoolean("notkillsteam"))
                            return 0;
                        else if (epicLauncher != null)
                            epicLauncher.Kill();
                    }
                    return 0;
                }
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
