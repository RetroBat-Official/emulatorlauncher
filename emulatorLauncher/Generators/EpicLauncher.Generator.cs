using System;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common.Launchers;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class EpicGameLauncher : GameLauncher
        {
            public EpicGameLauncher(Uri uri)
            {
                LauncherExe = EpicLibrary.GetEpicGameExecutableName(uri);
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                bool epicLauncherExists = Process.GetProcessesByName("EpicGamesLauncher").Any();
                SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);
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
        }
    }
}
