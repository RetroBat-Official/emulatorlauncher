using System;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common.Launchers;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class LocalFileGameLauncher : GameLauncher
        {
            public LocalFileGameLauncher(Uri uri)
            {
                LauncherExe = uri.LocalPath;
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                try
                {
                    var process = Process.Start(LauncherExe);
                    Job.Current.AddProcess(process);
                    process.WaitForExit();
                    return 0;
                }
                catch { }

                return -1;
            }
        }

        class AmazonGameLauncher : GameLauncher
        {
            public AmazonGameLauncher(Uri uri)
            {
                LauncherExe = AmazonLibrary.GetAmazonGameExecutableName(uri);
            }
           
            public override int RunAndWait(ProcessStartInfo path)
            {
                bool uiExists = Process.GetProcessesByName("Amazon Games UI").Any();
                SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);
                KillExistingLauncherExes();

                Process.Start(path);

                var amazonGame = GetLauncherExeProcess();
                if (amazonGame != null)
                {
                    amazonGame.WaitForExit();

                    if ((!uiExists && Program.SystemConfig["killsteam"] != "0") || (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam")))
                    {
                        foreach (var ui in Process.GetProcessesByName("Amazon Games UI"))
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
