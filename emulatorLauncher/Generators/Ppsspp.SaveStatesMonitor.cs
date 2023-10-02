using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace emulatorLauncher
{
    class PpssppSaveStatesMonitor : SaveStatesWatcher
    {
        public PpssppSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath) 
        { 
        }

        protected override string FilePattern { get { return "{{romfilename}}_{{slot0}}.ppst"; } }
        protected override string ImagePattern { get { return "{{romfilename}}_{{slot0}}.jpg"; } }

        protected override int FirstSlot { get { return 0; } }
        protected override int LastSlot { get { return 4; } }
    }
}
