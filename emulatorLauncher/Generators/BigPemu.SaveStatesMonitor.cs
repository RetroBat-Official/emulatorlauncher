using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class BigPemuSaveStatesMonitor : SaveStatesWatcher
    {
        public BigPemuSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        { 
        }
    }
}
