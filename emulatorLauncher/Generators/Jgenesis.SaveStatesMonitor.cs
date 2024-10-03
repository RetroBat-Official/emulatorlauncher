using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class JGenesisSaveStatesMonitor : SaveStatesWatcher
    {
        public JGenesisSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
        }
    }
}
