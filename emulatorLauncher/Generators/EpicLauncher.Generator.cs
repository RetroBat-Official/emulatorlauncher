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

                var modSdkMetadataDir = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Epic Games\\EOS", "ModSdkMetadataDir", null);
                if (modSdkMetadataDir != null)
                {
                    string manifestPath = modSdkMetadataDir.ToString();

                    string gameExecutable = null;

                    if (Directory.Exists(manifestPath))
                    {
                        foreach (var file in Directory.EnumerateFiles(manifestPath, "*.item"))
                        {
                            var item = JsonSerializer.DeserializeString<EpicGame>(File.ReadAllText(file));
                            if (shorturl.Equals(item.CatalogNamespace))
                            {
                                gameExecutable = item.LaunchExecutable;
                                break;
                            }
                        }
                    }

                    if (gameExecutable == null)
                        throw new ApplicationException("There is a problem: The Game is not installed");

                    return Path.GetFileNameWithoutExtension(gameExecutable);
                }

                throw new ApplicationException("There is a problem: Epic Launcher is not installed");
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                KillExistingLauncherExes();

                Process.Start(path);

                var epicGame = GetLauncherExeProcess();
                if (epicGame != null)
                {
                    epicGame.WaitForExit();

                    if (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam"))
                    {
                        var epicLauncher = Process.GetProcessesByName("EpicGamesLauncher").OrderBy(p => p.StartTime).FirstOrDefault();
                        if (epicLauncher != null)
                            epicLauncher.Kill();
                    }
                }

                return 0;
            }

            [DataContract]
            public class EpicGame
            {
                [DataMember]
                public string CatalogNamespace { get; set; }

                [DataMember]
                public string LaunchExecutable { get; set; }
            }
        }
    }
}
