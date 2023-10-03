using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace emulatorLauncher
{
    class Pcsx2SaveStatesMonitor : SaveStatesWatcher
    {
        public Pcsx2SaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath) 
        { 
        }

        protected override string FilePattern { get { return "{{romfilename}}.{{slot2d}}.p2s"; } }
        protected override string ImagePattern { get { return "{{romfilename}}.{{slot2d}}.p2s.png"; } }

        protected override int FirstSlot { get { return 1; } }
        protected override int LastSlot { get { return 10; } }

        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {            
            try
            {
                using (var arch = ZipArchive.OpenRead(saveState))
                {
                    var sshot = arch.Entries.FirstOrDefault(en => en.Filename == "Screenshot.png");
                    if (sshot != null)
                    {
                        sshot.Extract(EmulatorPath);

                        FileTools.TryDeleteFile(destScreenShot);
                        File.Move(Path.Combine(EmulatorPath, "Screenshot.png"), destScreenShot);
                    }
                }
            }
            catch { }
        }
    }
}
