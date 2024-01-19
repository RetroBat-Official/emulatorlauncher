using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class PpssppSaveStatesMonitor : SaveStatesWatcher
    {
        public PpssppSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath) 
        { 
        }
    }
}
