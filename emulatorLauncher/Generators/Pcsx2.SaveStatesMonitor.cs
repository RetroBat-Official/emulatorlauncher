using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    class Pcsx2SaveStatesMonitor : SaveStatesWatcher
    {
        public Pcsx2SaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath) 
        { 
        }

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
