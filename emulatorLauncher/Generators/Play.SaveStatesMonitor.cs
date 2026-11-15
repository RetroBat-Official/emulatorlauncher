using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    // Play! writes "Play Data Files\states\<executable>.st<slot>.zip" directly (no rename)
    class PlaySaveStatesMonitor : SaveStatesWatcher
    {
        public PlaySaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
        }
    }
}