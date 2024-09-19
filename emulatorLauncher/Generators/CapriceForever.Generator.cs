using System;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class CapriceForeverGenerator : Generator
    {
        public CapriceForeverGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private bool _xAxis = false;
        private bool _yAxis = false;
        private bool _zAxis = false;
        private bool _rxAxis = false;
        private bool _ryAxis = false;
        private bool _rzAxis = false;
        private bool _gx4000;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "Caprice64.exe");
            if (!File.Exists(exe))
                return null;

            _gx4000 = system == "gx4000";

            string[] extensions = new string[] { ".m3u", ".dsk", ".tap", ".cpr" };
            if (Path.GetExtension(rom).ToLower() == ".zip" || Path.GetExtension(rom).ToLower() == ".7z")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            string driveADisk = rom;
            string driveBDisk;
            string romType = "disk";

            if (Path.GetExtension(rom).ToLower() == ".tap")
                romType = "tape";

            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                var rawLines = File.ReadAllLines(rom);
                var lines = rawLines.Where(line => !line.TrimStart().StartsWith("#")).ToArray();
                if (lines.Length == 0)
                {
                    SimpleLogger.Instance.Error("M3U file is empty: " + rom);
                    return null;
                }
                if (lines.Length > 1)
                {
                    driveADisk = Path.Combine(Path.GetDirectoryName(rom), lines[0].Trim());
                    driveBDisk = Path.Combine(Path.GetDirectoryName(rom), lines[1].Trim());
                }
                else
                {
                    driveADisk = Path.Combine(Path.GetDirectoryName(rom), lines[0].Trim());
                    driveBDisk = null;
                }

                if (!File.Exists(driveADisk))
                {
                    SimpleLogger.Instance.Error("M3U file is invalid: " + rom);
                    return null;
                }
            }
            else
                driveBDisk = null;

            if (Path.GetExtension(rom).ToLower() == ".cpr")
                romType = "cart";

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfig(path, system, romType, driveADisk, driveBDisk, rom);
            SetupDevice(path, rom, romType);

            string drive = "/DriveA=";
            if (SystemConfig.getOptBoolean("caprice_driveB"))
                drive = "/DriveB=";

            if (romType == "cart")
                drive = "/Cartridge=";

            List<string> commandArray = new List<string>();
            
            if (driveBDisk != null)
                commandArray.Add("/DriveA=" + "\"" + driveADisk + "\" /DriveB=\"" + driveBDisk + "\"");
            else
                commandArray.Add(drive + "\"" + driveADisk + "\"");

            // Keyboard as joystick
            if (SystemConfig.getOptBoolean("caprice_keyboardasjoystick"))
                commandArray.Add("/KeyboardAsJoystick");

            if (fullscreen)
                commandArray.Add("/fullscreen");

            // autorun file management
            string autorunFile = rom.Replace(Path.GetExtension(rom), ".autorun");

            if (File.Exists(autorunFile) && !_gx4000)
            {
                var lines = File.ReadAllLines(autorunFile);

                if (lines.Length > 0)
                {
                    commandArray.Add("/Command=\"" + lines[0].Replace("\"", "\"\"") + "\"");
                }
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfig(string path, string system, string romType, string driveADisk, string driveBDisk, string rom)
        {
            string iniFile = Path.Combine(path, "Caprice.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues))
                {
                    if (SystemConfig.isOptSet("caprice_renderer") && !string.IsNullOrEmpty(SystemConfig["caprice_renderer"]))
                    {
                        string renderer = SystemConfig["caprice_renderer"];
                        switch (renderer)
                        {
                            case "directx":
                                ini.WriteValue("MainForm", "UseOpenGL", "0");
                                ini.WriteValue("MainForm", "ForceGDI", "0");
                                break;
                            case "opengl":
                                ini.WriteValue("MainForm", "UseOpenGL", "1");
                                ini.WriteValue("MainForm", "ForceGDI", "0");
                                break;
                            case "gdi":
                                ini.WriteValue("MainForm", "UseOpenGL", "0");
                                ini.WriteValue("MainForm", "ForceGDI", "1");
                                break;
                        }
                    }
                    else
                    {
                        ini.WriteValue("MainForm", "UseOpenGL", "0");
                        ini.WriteValue("MainForm", "ForceGDI", "0");
                    }
                    
                    BindBoolIniFeature(ini, "MainForm", "DrawScanlines", "caprice_scanlines", "1", "0");
                    ini.WriteValue("MainForm", "ScanlinesDirectory", "Scanlines\\");
                    ini.WriteValue("MainForm", "Fullscreen", "0");
                    ini.WriteValue("MainForm", "KeepFullscreen", "0");
                    ini.WriteValue("MainForm", "SettingsDirectory", "Settings\\");
                    BindBoolIniFeature(ini, "MainForm", "FullscreenMenu", "caprice_gui", "1", "0");
                    ini.WriteValue("MainForm", "ControlPanelVisible", "1");

                    // Drives and autostart
                    ini.WriteValue("Drives", "DriveADiskFilename", romType == "cart" ? null : driveADisk);
                    if (driveBDisk != null)
                        ini.WriteValue("Drives", "DriveBDiskFilename", romType == "cart" ? null : driveBDisk);
                    else
                        ini.WriteValue("Drives", "DriveBDiskFilename", null);

                    ini.WriteValue("Drives", "AutoStartEnable", "1");
                    ini.WriteValue("Drives", "DiscDirectory", Path.GetDirectoryName(driveADisk));

                    if (romType == "tape")
                    {
                        ini.WriteValue("Tape", "TapeFilename", driveADisk);
                        ini.WriteValue("Tape", "AutoPlayEnable", "1");
                        ini.WriteValue("Tape", "TapeDirectory", Path.GetDirectoryName(driveADisk));
                    }

                    BindBoolIniFeatureOn(ini, "Emulator", "FastLoading", "caprice_fastload", "1", "0");
                    BindBoolIniFeature(ini, "Emulator", "TurboFullSpeed", "caprice_turbo", "1", "0");
                    BindBoolIniFeature(ini, "Emulator", "ColorMonitor", "caprice_monochrome", "0", "1");

                    ini.WriteValue("Emulator", "DeviceSettings", null);

                    ini.WriteValue("Inputs", "ProfileDirectory", "Profiles\\");

                    bool controllerConfig = true;
                    if (SystemConfig.isOptSet("caprice_gamepadprofile") && !string.IsNullOrEmpty(SystemConfig["caprice_gamepadprofile"]))
                    {
                        string padProfile = SystemConfig["caprice_gamepadprofile"];

                        switch(padProfile)
                        {
                            case "retrobat":
                                controllerConfig = true;
                                break;
                            case "per_game":
                                controllerConfig = false;
                                string romName = Path.GetFileNameWithoutExtension(rom);
                                ini.WriteValue("Inputs", "DefaultProfileFilename", "romName.prfl");
                                break;
                            case "custom":
                                controllerConfig = false;
                                ini.WriteValue("Inputs", "DefaultProfileFilename", "custom.prfl");
                                break;
                        }
                    }
                    else
                        ini.WriteValue("Inputs", "DefaultProfileFilename", "Default.prfl");

                    string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "capriceforever");
                    if (!Directory.Exists(screenshotPath))
                        try { Directory.CreateDirectory(screenshotPath); } catch { }

                    string statePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "capriceforever");
                    if (!Directory.Exists(statePath))
                        try { Directory.CreateDirectory(statePath); } catch { }

                    string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "capriceforever");
                    if (!Directory.Exists(cheatsPath))
                        try { Directory.CreateDirectory(cheatsPath); } catch { }

                    ini.WriteValue("Snapshot", "SnapshotDirectory", statePath);
                    ini.WriteValue("Screenshot", "ScreenshotDirectory", screenshotPath);
                    ini.WriteValue("CheatScripts", "CheatScriptsDirectory", cheatsPath);

                    // Cartridges (gx4000)
                    string cartFolder = Path.GetFullPath(rom).Replace(Path.GetFileName(rom), "");
                    ini.WriteValue("Roms", "CartridgesDirectory", cartFolder);

                    // Keyboard as joystick
                    ini.WriteValue("Joystick Keyboard", "JoystickKey129", "38");
                    ini.WriteValue("Joystick Keyboard", "JoystickKey130", "40");
                    ini.WriteValue("Joystick Keyboard", "JoystickKey131", "37");
                    ini.WriteValue("Joystick Keyboard", "JoystickKey132", "39");
                    ini.WriteValue("Joystick Keyboard", "JoystickKey133", "18");    // Fire 1: ALT
                    ini.WriteValue("Joystick Keyboard", "JoystickKey134", "17");    // Fire 2: CTRL

                    if (controllerConfig)
                        ConfigureControllers(ini, path);
                }
            }
            catch { }
        }

        private void SetupDevice(string path, string rom, string romType)
        {
            string iniFile = Path.Combine(path, "Device.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues))
                {
                    BindIniFeature(ini, "Emulator", "Device", "caprice_device", _gx4000? "PLUS_6128_UK" : "CPC_6128_UK");
                    BindIniFeature(ini, "Emulator", "Brand", "caprice_brand", "Amstrad");
                    BindIniFeature(ini, "Emulator", "CRTCType", "caprice_crtc", "0");
                    
                    if (_gx4000)
                        ini.WriteValue("Emulator", "CRTCType", "3");

                    ini.WriteValue("Emulator", "FloppyDrive", "1");
                    BindBoolIniFeature(ini, "Emulator", "256kMemoryExtension", "caprice_256k", "1", "0");

                    if (SystemConfig.getOptBoolean("caprice_256ksilicon"))
                    {
                        ini.WriteValue("Emulator", "256kSiliconDisc", "1");
                        ini.WriteValue("Roms", "UpperROM1", "Default Silicon Disc 1.3");
                        ini.WriteValue("Emulator", "4MMemoryExtension", "0");
                    }
                    else
                    {
                        ini.WriteValue("Emulator", "256kSiliconDisc", "0");
                        ini.WriteValue("Roms", "UpperROM1", null);
                    }

                    if (SystemConfig.getOptBoolean("caprice_4m"))
                    {
                        ini.WriteValue("Emulator", "256kMemoryExtension", "0");
                        ini.WriteValue("Emulator", "256kSiliconDisc", "0");
                        ini.WriteValue("Emulator", "4MMemoryExtension", "1");
                        ini.WriteValue("Roms", "UpperROM1", null);
                    }
                    else
                    {
                        ini.WriteValue("Emulator", "4MMemoryExtension", "0");
                    }

                    BindBoolIniFeature(ini, "Emulator", "PlayCity", "caprice_playcity", "1", "0");
                    BindBoolIniFeature(ini, "Emulator", "Digiblaster", "caprice_digiblaster", "1", "0");
                    BindBoolIniFeature(ini, "Emulator", "Multiface2", "caprice_multiface2", "1", "0");

                    if (SystemConfig.isOptSet("caprice_addspeech") && !string.IsNullOrEmpty(SystemConfig["caprice_addspeech"]))
                    {
                        string speech = SystemConfig["caprice_addspeech"];

                        switch (speech)
                        {
                            case "tmpi":
                                ini.WriteValue("Emulator", "TechniMusique", "1");
                                ini.WriteValue("Emulator", "SSA1", "0");
                                ini.WriteValue("Emulator", "DKTronicsSpeech", "0");
                                ini.WriteValue("Roms", "UpperROM1", "Empty");
                                break;
                            case "ssa1":
                                ini.WriteValue("Emulator", "TechniMusique", "0");
                                ini.WriteValue("Emulator", "SSA1", "1");
                                ini.WriteValue("Emulator", "DKTronicsSpeech", "0");
                                ini.WriteValue("Roms", "UpperROM1", "Empty");
                                break;
                            case "dktronics":
                                ini.WriteValue("Emulator", "TechniMusique", "0");
                                ini.WriteValue("Emulator", "SSA1", "0");
                                ini.WriteValue("Emulator", "DKTronicsSpeech", "1");
                                ini.WriteValue("Emulator", "DKTronicsSpeechRom", "1");
                                ini.WriteValue("Roms", "UpperROM1", "Default DK'Tronics Speech");
                                break;
                            case "none":
                                ini.WriteValue("Emulator", "TechniMusique", "0");
                                ini.WriteValue("Emulator", "SSA1", "0");
                                ini.WriteValue("Emulator", "DKTronicsSpeech", "0");
                                ini.WriteValue("Emulator", "DKTronicsSpeechRom", "0");
                                ini.WriteValue("Roms", "UpperROM1", "Empty");
                                break;
                        }
                    }
                    else
                    {
                        ini.WriteValue("Emulator", "SSA1", "0");
                        ini.WriteValue("Emulator", "DKTronicsSpeech", "0");
                        ini.WriteValue("Emulator", "DKTronicsSpeechRom", "0");
                        ini.WriteValue("Roms", "UpperROM1", "Empty");
                    }

                    if (SystemConfig.isOptSet("caprice_xmem") && !string.IsNullOrEmpty(SystemConfig["caprice_xmem"]))
                    {
                        string xmem = SystemConfig["caprice_xmem"];

                        switch (xmem)
                        {
                            case "xmem":
                                ini.WriteValue("Emulator", "XMem", "1");
                                ini.WriteValue("Emulator", "XMEMBoot", "0");
                                break;
                            case "xmem_boot":
                                ini.WriteValue("Emulator", "XMem", "1");
                                ini.WriteValue("Emulator", "XMEMBoot", "1");
                                break;
                            case "no":
                                ini.WriteValue("Emulator", "XMem", "0");
                                ini.WriteValue("Emulator", "XMEMBoot", "0");
                                break;
                        }   
                    }
                    else
                    {
                        ini.WriteValue("Emulator", "XMem", "0");
                        ini.WriteValue("Emulator", "XMEMBoot", "0");
                    }

                    BindBoolIniFeature(ini, "Emulator", "XMass", "caprice_xmass", "1", "0");
                    BindBoolIniFeature(ini, "Emulator", "Albireo", "caprice_albireo", "1", "0");

                    // Storage
                    ini.WriteValue("Storage", "XMASSDirectory", "Roms\\X-Mass\\");
                    ini.WriteValue("Storage", "XMassFilename", "New X-Mass.xmass");
                    ini.WriteValue("Storage", "SharedDirectory", "Share\\");

                    // Roms
                    ini.WriteValue("Roms", "RomsDirectory", "Roms\\");
                    ini.WriteValue("Roms", "Multiface2Directory", "Roms\\Multiface 2\\");
                    ini.WriteValue("Roms", "XMEMDirectory", "Roms\\X-Mem\\");
                    ini.WriteValue("Roms", "XMemROM", "New X-Mem.xmem");
                    ini.WriteValue("Roms", "LowerROM", _gx4000 ? "Empty" : null);
                    ini.WriteValue("Roms", "CartridgeFilename", romType == "cart" ? Path.GetFileName(rom) : null);
                    ini.WriteValue("Roms", "UpperROM0", _gx4000 ? "Empty" : null);
                    ini.WriteValue("Roms", "UpperROM7", _gx4000 ? "Empty" : "Default AMSDOS");
                }
            }
            catch { }
        }
    }
}
