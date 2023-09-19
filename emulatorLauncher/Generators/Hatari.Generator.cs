using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class HatariGenerator : Generator
    {
        public HatariGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("hatari");

            string exe = Path.Combine(path, "hatari.exe");
            if (!File.Exists(exe))
                return null;

            string cfgFile = Path.Combine(path, "hatari.cfg");

            SetupHatari(cfgFile, rom, path);

            var commandArray = new List<string>();
            commandArray.Add("-c");
            commandArray.Add("\"" + cfgFile + "\"");
            commandArray.Add("--disk-a");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupHatari(string cfgFile, string rom, string path)
        {
            using (var ini = IniFile.FromFile(cfgFile, IniOptions.UseSpaces))
            {
                ini.WriteValue("Log", "bConfirmQuit", "FALSE");
                
                // SCREEN
                ini.WriteValue("Screen", "bFullScreen", "TRUE");
                ini.WriteValue("Screen", "bKeepResolution", "TRUE");
                ini.WriteValue("Screen", "bShowStatusbar", "FALSE");
                ini.WriteValue("Screen", "bShowDriveLed", "FALSE");

                // Add resolution setting from videomode
                BindBoolIniFeature(ini, "Screen", "bUseVsync", "hatari_vsync", "FALSE", "TRUE");

                // MEMORY
                string savesFile = Path.Combine(AppConfig.GetFullPath("saves"), "atarist", "hatari", "hatari.sav");
                string autoSaveFile = Path.Combine(AppConfig.GetFullPath("saves"), "atarist", "hatari", "auto.sav");
                
                //add memory size feature

                BindBoolIniFeature(ini, "Memory", "bAutoSave", "hatari_autosave", "TRUE", "FALSE");

                ini.WriteValue("Memory", "szMemoryCaptureFileName", savesFile);
                ini.WriteValue("Memory", "szAutoSaveFileName", autoSaveFile);

                // FLOPPY
                ini.WriteValue("Floppy", "bAutoInsertDiskB", "TRUE");
                
                //add feature to select disk drive & insert blank disk
                ini.WriteValue("Floppy", "EnableDriveA", "TRUE");
                ini.WriteValue("Floppy", "EnableDriveB", "TRUE");
                ini.WriteValue("Floppy", "szDiskAFileName", rom);
                ini.WriteValue("Floppy", "szDiskImageDirectory", Path.Combine(AppConfig.GetFullPath("roms"), "atarist"));

                // HARDDISK
                ini.WriteValue("HardDisk", "szHardDiskDirectory", path);

                // ROM

                //add feature to select tos
                string tosfile = Path.Combine(AppConfig.GetFullPath("bios"), "tos.img");
                ini.WriteValue("ROM", "szTosImageFileName", tosfile);

                // SYSTEM

                //add feature for model type, cpu, fastboot...

                // VIDEO
                string recording = Path.Combine(AppConfig.GetFullPath("records"), "output", "hatari", "hatari.avi");
                ini.WriteValue("Video", "AviRecordFile", recording);

                ini.Save();
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
