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
                return RunAndWaitForGame(path, "EpicGamesLauncher");
            }
        }
    }
}
