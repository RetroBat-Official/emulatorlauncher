using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class Pcsx2Generator : Generator
    {
        private string path;
        private string romName;

        private const string savDirName = "tmp";

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, string gameResolution)
        {
            path = AppConfig.GetFullPath("pcsx2");

            string exe = Path.Combine(path, "pcsx2.exe");
            if (!File.Exists(exe))
                return null;

            romName = Path.GetFileNameWithoutExtension(rom);

            RestoreIni(path, null, "GSdx.ini", true);
            RestoreIni(path, null, "PCSX2_vm.ini", true);

            SaveIni(path, romName, "GSdx.ini");
            SaveIni(path, romName, "PCSX2_vm.ini");

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "--portable --fullscreen --nogui \"" + rom + "\"",            
            };
        }

        public override void Cleanup()
        {
            RestoreIni(path, romName, "GSdx.ini");
            RestoreIni(path, romName, "PCSX2_vm.ini");

            try
            {
                string savDir = Path.Combine(path, "inis", savDirName);
                if (Directory.Exists(savDir))
                    Directory.Delete(savDir);
            }
            catch { }
        }

        static void SaveIni(string path, string romName, string iniName)
        {
            string ini = Path.Combine(path, "inis", romName, iniName);
            if (!File.Exists(ini))
                return;

            string originalIni = Path.Combine(path, "inis", iniName);
            if (File.Exists(originalIni))
            {
                string savDir = Path.Combine(path, "inis", savDirName);
                if (!Directory.Exists(savDir))
                    Directory.CreateDirectory(savDir);

                string savIni = Path.Combine(path, "inis", savDirName, iniName);

                try { File.Copy(originalIni, savIni, true); }
                catch { return; }
            }

            try { File.Copy(ini, originalIni, true); }
            catch { }

        }

        static void RestoreIni(string path, string romName, string iniName, bool force = false)
        {
            if (string.IsNullOrEmpty(romName))
                return;

            if (!force)
            {
                string ini = Path.Combine(path, "inis", romName, iniName);
                if (!File.Exists(ini))
                    return;
            }

            string originalIni = Path.Combine(path, "inis", iniName);
            if (File.Exists(originalIni))
            {
                string savIni = Path.Combine(path, "inis", savDirName, iniName);
                if (File.Exists(savIni))
                {
                    try { File.Move(savIni, originalIni); }
                    catch { }

                    try { File.Delete(savIni); }
                    catch { }

                }
            }
        }
    }
}
