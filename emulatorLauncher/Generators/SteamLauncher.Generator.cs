using System;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common.Launchers;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class SteamGameLauncher : GameLauncher
        {
            public SteamGameLauncher(Uri uri)
            {
                // Call method to get Steam executable
                LauncherExe = SteamLibrary.GetSteamGameExecutableName(uri);
            }

            public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
            {
                // Check if steam is already running
                bool uiExists = Process.GetProcessesByName("steam").Any();
                SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);
                
                // Kill game if already running
                KillExistingLauncherExes();

                // Start game
                Process.Start(path);

                // Get running game process (30 seconds delay 30x1000)
                var steamGame = GetLauncherExeProcess();

                if (steamGame != null)
                {
                    steamGame.WaitForExit();

                    // Kill steam if it was not running previously or if option is set in RetroBat
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
