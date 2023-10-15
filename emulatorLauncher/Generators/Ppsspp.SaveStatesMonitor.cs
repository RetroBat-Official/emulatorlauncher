using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
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
