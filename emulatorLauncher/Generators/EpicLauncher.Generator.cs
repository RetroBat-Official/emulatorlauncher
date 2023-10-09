using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.Serialization;
using System.Threading;
using System.Security.Policy;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
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
                bool epicLauncherExists = Process.GetProcessesByName("EpicGamesLauncher").Any();

                KillExistingLauncherExes();

                Process.Start(path);

                var epicGame = GetLauncherExeProcess();
                if (epicGame != null)
                {
                    epicGame.WaitForExit();

                    if (!epicLauncherExists || (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam")))
                    {
                        foreach (var ui in Process.GetProcessesByName("EpicGamesLauncher"))
                        {
                            try { ui.Kill(); }
                            catch { }
                        }
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
