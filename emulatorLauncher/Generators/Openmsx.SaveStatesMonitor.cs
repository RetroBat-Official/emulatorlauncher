using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class OpenmsxSaveStatesMonitor : SaveStatesWatcher
    {
        public OpenmsxSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
             : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        { 
        }

        public static bool GetEmulatorStateName(string statePath, string rom, int slot, out string name)
        {
            name = null;
            string txtFile = Path.Combine(statePath, Path.GetFileNameWithoutExtension(rom) + ".txt");

            if (File.Exists(txtFile))
            {
                var txtLines = File.ReadAllLines(txtFile);
                if (txtLines.Length > 0)
                {
                    name = txtLines[0] + "_" + slot;
                    return true;
                }
            }
            return false;
        }
    }
}
