using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class DesmumeSaveStatesMonitor : SaveStatesWatcher
    {

        public DesmumeSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string screenshotPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
        }
    }
}
