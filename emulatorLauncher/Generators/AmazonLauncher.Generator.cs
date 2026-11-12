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
                return RunAndWaitForGame(path, "Amazon Games UI");
            }
        }
    }
}
