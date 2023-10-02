using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher
{
    class Mupen64SaveStatesMonitor : SaveStatesWatcher
    {
        public Mupen64SaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed) 
        { 

        }

        protected override string FilePattern { get { return "{{romfilename}}.st{{slot0}}"; } }
        protected override string ImagePattern { get { return "{{romfilename}}.st{{slot0}}.png"; } }

        protected override int FirstSlot { get { return 0; } }
        protected override int LastSlot { get { return 9; } }
    }
}
