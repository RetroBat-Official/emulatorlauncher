using EmulatorLauncher.Common;
using System.IO;

namespace EmulatorLauncher
{
    class BigPemuSaveStatesMonitor : SaveStatesWatcher
    {
        private string _shotPath;

        public BigPemuSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string screenshotPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            _shotPath = screenshotPath;
        }

        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                if (File.Exists(_shotPath))
                    File.Copy(_shotPath, destScreenShot, true);
            }
            catch { }
        }
    }
}
