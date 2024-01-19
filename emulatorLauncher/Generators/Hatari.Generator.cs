using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
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

            string romName = Path.GetFileNameWithoutExtension(rom);
            string romPath = Path.GetDirectoryName(rom);
            string cfgFile = Path.Combine(path, "hatari.cfg");
            string disk = "--disk-a";
            bool diskImage = false;

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

            var commandArray = new List<string>();
            commandArray.Add("-c");
            commandArray.Add("\"" + cfgFile + "\"");
            
            if (Path.GetExtension(rom).ToLower() == ".gemdos")
            {
                commandArray.Add("-d");
                commandArray.Add("\"" + rom + "\"");
                diskImage = true;

                string searchAuto = Path.Combine(romPath, romName + ".autorun");
                if (File.Exists(searchAuto))
                {
                    string[] autoRun = File.ReadAllLines(searchAuto);
                    
                    if (autoRun.Length > 0)
                    {
                        string autorunCmd = autoRun[0];
                        commandArray.Add("--auto");
                        commandArray.Add("\"" + autoRun[0] + "\"");
                    }
                }
            }

            else
            {
                commandArray.Add(disk);
                commandArray.Add("\"" + rom + "\"");
            }

            string args = string.Join(" ", commandArray);

            SetupHatari(cfgFile, rom, path, diskImage);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupHatari(string cfgFile, string rom, string path, bool diskImage = false)
        {
            using (var ini = IniFile.FromFile(cfgFile, IniOptions.UseSpaces))
            {
                string machineType = "st";
                if (SystemConfig.isOptSet("hatari_machine") && !string.IsNullOrEmpty(SystemConfig["hatari_machine"]))
                    machineType = SystemConfig["hatari_machine"];

                ini.WriteValue("Log", "bConfirmQuit", "FALSE");

                // SCREEN
                bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

                if (fullscreen)
                    ini.WriteValue("Screen", "bFullScreen", "TRUE");
                else
                    ini.WriteValue("Screen", "bFullScreen", "FALSE");

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
                ini.AppendValue("Floppy", "szDiskAFileName", "");
                ini.AppendValue("Floppy", "szDiskBFileName", "");

                //add feature to select disk drive & insert blank disk
                ini.WriteValue("Floppy", "EnableDriveA", "TRUE");
                ini.WriteValue("Floppy", "EnableDriveB", "TRUE");
                ini.WriteValue("Floppy", "szDiskImageDirectory", Path.Combine(AppConfig.GetFullPath("roms"), "atarist"));

                // HARDDISK
                if (!diskImage)
                {
                    ini.WriteValue("HardDisk", "szHardDiskDirectory", path);
                    ini.WriteValue("HardDisk", "bBootFromHardDisk", "FALSE");
                    ini.WriteValue("HardDisk", "bUseHardDiskDirectory", "FALSE");
                }
                else
                {
                    ini.WriteValue("HardDisk", "szHardDiskDirectory", rom);
                    ini.WriteValue("HardDisk", "bBootFromHardDisk", "TRUE");
                    ini.WriteValue("HardDisk", "bUseHardDiskDirectory", "TRUE");
                }

                // ROM
                string tosfile = Path.Combine(AppConfig.GetFullPath("bios"), "tos.img");

                if (SystemConfig.isOptSet("hatari_tos") && !string.IsNullOrEmpty(SystemConfig["hatari_tos"]))
                    tosfile = Path.Combine(AppConfig.GetFullPath("bios"), "hatari", "tos", SystemConfig["hatari_tos"]);

                string tosName = Path.GetFileName(tosfile);

                if (!File.Exists(tosfile))
                    throw new ApplicationException("Missing TOS file " + tosName + " in 'bios' folder.");

                ini.WriteValue("ROM", "szTosImageFileName", tosfile);

                // SYSTEM
                // machine type and default values
                switch (machineType)
                {
                    case "st":
                        ini.WriteValue("System", "nModelType", "0");
                        ini.WriteValue("System", "nCpuFreq", "8");
                        ini.WriteValue("Memory", "nMemorySize", "1024");
                        break;
                    case "megast":
                        ini.WriteValue("System", "nModelType", "1");
                        ini.WriteValue("System", "nCpuFreq", "8");
                        ini.WriteValue("Memory", "nMemorySize", "2048");
                        break;
                    case "ste":
                        ini.WriteValue("System", "nModelType", "2");
                        ini.WriteValue("System", "nCpuFreq", "8");
                        ini.WriteValue("Memory", "nMemorySize", "4096");
                        break;
                    case "megaste":
                        ini.WriteValue("System", "nModelType", "3");
                        ini.WriteValue("System", "nCpuFreq", "16");
                        ini.WriteValue("Memory", "nMemorySize", "4096");
                        break;
                }
                
                // Overwrite values if feature is set
                if (SystemConfig.isOptSet("hatari_frequency") && !string.IsNullOrEmpty(SystemConfig["hatari_frequency"]))
                    ini.WriteValue("System", "nCpuFreq", SystemConfig["hatari_frequency"]);
                if (SystemConfig.isOptSet("hatari_memory") && !string.IsNullOrEmpty(SystemConfig["hatari_memory"]))
                    ini.WriteValue("Memory", "nMemorySize", SystemConfig["hatari_memory"]);

                // other system options
                BindBoolIniFeature(ini, "System", "bFastBoot", "hatari_fastboot", "TRUE", "FALSE");

                // VIDEO
                string recording = Path.Combine(AppConfig.GetFullPath("records"), "output", "hatari", "hatari.avi");
                ini.WriteValue("Video", "AviRecordFile", recording);

                SetupJoysticks(ini);

                ini.Save();
            }
        }

        private void SetupJoysticks(IniFile ini)
        {
            // First initialize all joysticks to disabled
            for (int i = 0; i < 6; i++)
            {
                ini.WriteValue("Joystick" + i, "nJoystickMode", "0");
                ini.WriteValue("Joystick" + i, "nJoyId", "-1");
            }

            if (Program.Controllers.Count == 0)
                return;
            
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigureInput(ini, controller, controller.PlayerIndex);
        }

        private void ConfigureInput(IniFile ini, Controller c, int playerIndex)
        {
            // First define section to use
            // Joystick1 = standard port for controller
            // Joystick0 = second port for multiplayer games
            // Joystick2/3 = ports used for Jaguar Joysticks (STE only)
            // Joystick4/5 = joysticks plugged in parallel port with adapter

            string joySection = "Joystick1";
            if (playerIndex == 2)
                joySection = "Joystick0";
            if (playerIndex == 3)
                joySection = "Joystick4";
            if (playerIndex == 4)
                joySection = "Joystick5";
            if (playerIndex > 4)
                return;

            if (SystemConfig.isOptSet("hatari_joypad") && SystemConfig.getOptBoolean("hatari_joypad") && (SystemConfig["hatari_machine"] == "ste" || SystemConfig["hatari_machine"] == "megaste"))
            {
                if (playerIndex == 1)
                    joySection = "Joystick2";
                else if (playerIndex == 2)
                    joySection = "Joystick3";
            }

            if (c.IsKeyboard)
            {
                ini.WriteValue(joySection, "nJoystickMode", "1");
                ini.WriteValue(joySection, "nJoyId", "0");
                ini.WriteValue(joySection, "kUp", "Up");
                ini.WriteValue(joySection, "kDown", "Down");
                ini.WriteValue(joySection, "kLeft", "Left");
                ini.WriteValue(joySection, "kRight", "Right");
                ini.WriteValue(joySection, "kFire", "X");
            }

            int index = c.DeviceIndex;

            ini.WriteValue(joySection, "nJoystickMode", "2");
            ini.WriteValue(joySection, "nJoyId", index.ToString());
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
