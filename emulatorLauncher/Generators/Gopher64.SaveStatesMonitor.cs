using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class Gopher64SaveStatesMonitor : SaveStatesWatcher
    {
        public Gopher64SaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            
        }
    }
}
