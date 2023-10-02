using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace emulatorLauncher
{
    class DolphinSaveStatesMonitor : SaveStatesWatcher
    {
        public DolphinSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath) 
        { 
        }

        protected override string FilePattern { get { return "{{romfilename}}.s{{slot2d}}"; } }
        protected override string ImagePattern { get { return "{{romfilename}}.s{{slot2d}}.png"; } }

        protected override int FirstSlot { get { return 1; } }
        protected override int LastSlot { get { return 10; } }

        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                Thread.Sleep(350);

                var dt = new FileInfo(saveState).LastWriteTime;

                var shots = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(EmulatorPath), "ScreenShots", Path.GetFileNameWithoutExtension(saveState)));
                var shot = shots
                    .Where(s => Math.Abs((new FileInfo(s).LastWriteTime - dt).Seconds) < 3)
                    .OrderByDescending(s => new FileInfo(s).LastWriteTime)
                    .FirstOrDefault();

                if (File.Exists(shot))
                {
                    int cnt = 0;

                retry:
                    try
                    {
                        while (FileTools.IsFileLocked(shot))
                            Thread.Sleep(20);

                        File.Copy(shot, destScreenShot, true);
                        File.Delete(shot);
                    }
                    catch (IOException)
                    {
                        cnt++;
                        if (cnt < 3)
                        {
                            Thread.Sleep(200);
                            goto retry;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
