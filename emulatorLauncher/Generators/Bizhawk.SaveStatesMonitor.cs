using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class BizhawkSaveStatesMonitor : SaveStatesWatcher
    {
        public BizhawkSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
        }
    }
}
