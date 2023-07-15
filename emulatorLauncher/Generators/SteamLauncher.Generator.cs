using System;

namespace emulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class SteamGameLauncher : GameLauncher
        {
            public SteamGameLauncher(Uri uri)
            {
                throw new NotImplementedException("SteamGameLauncher is not implemented");
            }

            public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
            {
                throw new NotImplementedException();
            }
        }
    }
}
