using System;
using System.Linq;
using System.Diagnostics;
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
