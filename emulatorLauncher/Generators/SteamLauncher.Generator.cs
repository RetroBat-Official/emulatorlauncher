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

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class SteamGameLauncher : GameLauncher
        {
            public SteamGameLauncher(Uri uri)
            {
                LauncherExe = GetSteamGameExecutableName(uri);
            }

            private string GetSteamGameExecutableName(Uri uri)
            {
                string shorturl = uri.AbsolutePath.Substring(1);
                int steamAppId = shorturl.ToInteger();

                SteamGame game = SteamAppInfoReader.FindGameInformations(steamAppId);

                if (game == null || game.Executable == null)
                {
                    SimpleLogger.Instance.Info("[WARNING] Cannot find STEAM game executable");
                    return null;
                }

                else
                    return Path.GetFileNameWithoutExtension(game.Executable);
            }

            public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
            {
                bool uiExists = Process.GetProcessesByName("steam").Any();

                KillExistingLauncherExes();

                Process.Start(path);

                var steam = GetLauncherExeProcess();

                if (steam != null)
                {
                    steam.WaitForExit();

                    if (!uiExists || (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam")))
                    {
                        foreach (var ui in Process.GetProcessesByName("steam"))
                        {
                            try { ui.Kill(); }
                            catch { }
                        }
                    }
                }
                return 0;
            }
        }
    }
}
