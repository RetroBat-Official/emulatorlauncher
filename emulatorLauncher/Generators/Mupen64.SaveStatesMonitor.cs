using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class Mupen64SaveStatesMonitor : SaveStatesWatcher
    {
        public Mupen64SaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed) 
        { 

        }
    }
}
