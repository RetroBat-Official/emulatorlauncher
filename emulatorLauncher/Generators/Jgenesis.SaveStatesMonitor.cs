using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class JGenesisSaveStatesMonitor : SaveStatesWatcher
    {
        private string _shotPath;

        public JGenesisSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string screenshotPath)
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
