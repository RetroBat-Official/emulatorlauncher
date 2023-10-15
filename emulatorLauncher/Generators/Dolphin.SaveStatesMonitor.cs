using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class DolphinSaveStatesMonitor : SaveStatesWatcher
    {
        public DolphinSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath) 
        { 
        }

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
