using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Launchers;
using EmulatorLauncher.Common.Launchers.Epic;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        
        class GogGameLauncher : GameLauncher
        {
            private string _steamID;
            public GogGameLauncher(Uri uri)
            {
                // Call method to get Gog executable
                LauncherExe = GogLibrary.GetGOGGameById(uri.AbsolutePath);
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                bool uiExists = Process.GetProcessesByName("GalaxyClient").Any();
                SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);
                KillExistingLauncherExes();

                Process.Start(path);

                var gogGame = GetLauncherExeProcess();
                if (gogGame != null)
                {
                    Job.Current.AddProcess(gogGame);
                    SimpleLogger.Instance.Info("[INFO] Process found running: " + LauncherExe + " ,waiting to exit");
                    gogGame.WaitForExit();

                    if ((!uiExists && Program.SystemConfig["killsteam"] != "0") || (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam")))
                    {
                        foreach (var ui in Process.GetProcessesByName("GalaxyClient"))
                        {
                            try { ui.Kill(); }
                            catch { }
                            SimpleLogger.Instance.Info("[INFO] Killed GalaxyClient.");
                        }
                    }
                }

                return 0;
            }
        }
    }
}
