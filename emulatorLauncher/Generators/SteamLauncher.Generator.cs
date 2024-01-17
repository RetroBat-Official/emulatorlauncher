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
using EmulatorLauncher.Common.Launchers;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class SteamGameLauncher : GameLauncher
        {
            public SteamGameLauncher(Uri uri)
            {
                LauncherExe = SteamLibrary.GetSteamGameExecutableName(uri);
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
