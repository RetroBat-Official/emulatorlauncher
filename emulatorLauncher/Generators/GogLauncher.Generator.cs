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
            public GogGameLauncher(Uri uri)
            {
                // Call method to get Gog executable
                LauncherExe = GogLibrary.GetGOGGameById(uri.AbsolutePath);
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                return RunAndWaitForGame(path, "GalaxyClient");
            }
        }
    }
}
