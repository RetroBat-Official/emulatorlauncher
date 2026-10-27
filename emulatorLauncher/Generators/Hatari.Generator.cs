using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class HatariGenerator : Generator
    {
        public HatariGenerator()
        {
            // Hatari keeps the desktop resolution in fullscreen (bKeepResolution), so the videomode feature must be applied by EmulatorLauncher
            DependsOnDesktopResolution = true;
        }
        protected override bool UseGenericScreenPlacement { get { return true; } }
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _machineType = "st";

        // Floppy formats opened by Hatari (zip and gz archives are opened natively)
        private static readonly string[] _diskExtensions = new string[] { ".st", ".msa", ".stx", ".dim", ".ipf", ".ctr", ".raw", ".zip", ".gz" };

        // Hard disk image extensions, same convention as the hatariB libretro core (m3u playlists are shared between both emulators)
        private static readonly string[] _acsiExtensions = new string[] { ".acsi", ".ahd", ".vhd" };
        private static readonly string[] _scsiExtensions = new string[] { ".scsi", ".shd" };
        private static readonly string[] _ideExtensions = new string[] { ".ide" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("hatari");

            string exe = Path.Combine(path, "hatari.exe");
            if (!File.Exists(exe))
                return null;

            string romName = Path.GetFileNameWithoutExtension(rom);
            string romPath = Path.GetDirectoryName(rom);
            string cfgFile = Path.Combine(path, "hatari.cfg");

            // Hatari cannot open 7z archives : extract them first
            string romExt = Path.GetExtension(rom).ToLowerInvariant();
            if (romExt == ".7z" || romExt == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    var extractedFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories);

                    // An m3u inside the archive has priority, otherwise first disk image
                    string extractedRom = extractedFiles.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() == ".m3u");
                    if (extractedRom == null)
                        extractedRom = extractedFiles
                            .Where(f => _diskExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();

                    if (extractedRom != null)
                    {
                        rom = extractedRom;
                        ValidateUncompressedGame();
                    }
                }
            }

            // Floppies, hard disks and m3u directives (#AUTO, #RES, #BOOTA, #BOOTB)
            var media = new HatariMedia();
            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
                ParseM3u(rom, media);
            else
                media.AddFile(rom);

            if (media.IsEmpty)
                throw new ApplicationException("No valid disk image found in " + Path.GetFileName(rom));

            // Optional <game>.autorun file next to the game : Atari path of the program to start (e.g. A:\GAME.PRG or C:\DIR\GAME.PRG)
            if (string.IsNullOrEmpty(media.AutoRun))
            {
                string searchAuto = Path.Combine(romPath, romName + ".autorun");
                if (File.Exists(searchAuto))
                    media.AutoRun = File.ReadAllLines(searchAuto).Select(l => l.Trim()).FirstOrDefault(l => !string.IsNullOrEmpty(l));
            }

            bool fullscreen = ShouldRunFullscreen();

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            // Per game memory snapshots
            string savesDir = Path.Combine(AppConfig.GetFullPath("saves"), "atarist", "hatari");
            if (!Directory.Exists(savesDir))
                try { Directory.CreateDirectory(savesDir); } catch { }

            string saveStateFile = Path.Combine(savesDir, romName + ".sav");
            string autoSaveFile = Path.Combine(savesDir, romName + ".auto.sav");

            // Hatari restores the autosave snapshot only when no disk/machine option is passed on the command line
            bool restoreAutoSave = SystemConfig.getOptBoolean("hatari_autosave") && File.Exists(autoSaveFile);

            SetupHatari(cfgFile, path, media, saveStateFile, autoSaveFile, fullscreen);

            var commandArray = new List<string>
            {
                "-c",
                "\"" + cfgFile + "\""
            };

            if (!restoreAutoSave)
            {
                // Command line machine selection also resets CPU level, MMU, FPU and address space to values matching the machine
                commandArray.Add("--machine");
                commandArray.Add(_machineType);

                // Must come after --machine, which resets the CPU clock
                commandArray.Add("--cpuclock");
                commandArray.Add(GetCpuClock());

                if (media.GemdosDir != null)
                {
                    commandArray.Add("-d");
                    commandArray.Add("\"" + media.GemdosDir + "\"");
                }

                if (media.Acsi != null)
                {
                    commandArray.Add("--acsi");
                    commandArray.Add("\"0=" + media.Acsi + "\"");
                }

                if (media.Scsi != null)
                {
                    commandArray.Add("--scsi");
                    commandArray.Add("\"0=" + media.Scsi + "\"");
                }

                if (media.Ide != null)
                {
                    commandArray.Add("--ide-master");
                    commandArray.Add("\"" + media.Ide + "\"");
                }

                string diskA = media.GetDriveDisk(0);
                string diskB = media.GetDriveDisk(1);

                if (diskA != null)
                {
                    commandArray.Add("--disk-a");
                    commandArray.Add("\"" + diskA + "\"");
                }

                if (diskB != null && diskB != diskA)
                {
                    commandArray.Add("--disk-b");
                    commandArray.Add("\"" + diskB + "\"");
                }

                if (!string.IsNullOrEmpty(media.AutoRun))
                {
                    commandArray.Add("--auto");
                    commandArray.Add("\"" + media.AutoRun + "\"");
                }

                if (!string.IsNullOrEmpty(media.TosRes))
                {
                    commandArray.Add("--tos-res");
                    commandArray.Add(media.TosRes);
                }
            }
            else
                SimpleLogger.Instance.Info("[Generator] Hatari: restoring autosave snapshot " + autoSaveFile);

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        /// <summary>
        /// Reads an m3u playlist, hatariB format : one image per line (floppies first, then hard disks),
        /// #AUTO:path, #RES:low|med|high, #BOOTA:n and #BOOTB:n (1 = first image of the list, 0 = empty drive)
        /// </summary>
        private static void ParseM3u(string m3uFile, HatariMedia media)
        {
            string m3uDir = Path.GetDirectoryName(m3uFile);

            foreach (var line in File.ReadAllLines(m3uFile))
            {
                string entry = line.Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                if (entry.StartsWith("#"))
                {
                    if (entry.StartsWith("#AUTO:", StringComparison.OrdinalIgnoreCase))
                        media.AutoRun = entry.Substring(6).Trim();
                    else if (entry.StartsWith("#RES:", StringComparison.OrdinalIgnoreCase))
                        media.TosRes = entry.Substring(5).Trim().ToLowerInvariant();
                    else if (entry.StartsWith("#BOOTA:", StringComparison.OrdinalIgnoreCase))
                        media.BootIndex[0] = ParseBootIndex(entry.Substring(7));
                    else if (entry.StartsWith("#BOOTB:", StringComparison.OrdinalIgnoreCase))
                        media.BootIndex[1] = ParseBootIndex(entry.Substring(7));

                    continue;
                }

                try
                {
                    entry = entry.Replace('/', '\\');
                    string filePath = Path.IsPathRooted(entry) ? entry : Path.GetFullPath(Path.Combine(m3uDir, entry));

                    if (File.Exists(filePath))
                        media.AddFile(filePath);
                    else
                        SimpleLogger.Instance.Warning("[Generator] Hatari: file not found in m3u : " + filePath);
                }
                catch { }
            }
        }

        private static int ParseBootIndex(string value)
        {
            int index;
            if (!int.TryParse(value.Trim(), out index) || index < 0)
                return 0;   // empty or invalid value = empty drive

            return index;
        }

        private class HatariMedia
        {
            public List<string> Images = new List<string>();     // every m3u entry, for #BOOTA / #BOOTB numbering
            public List<string> Floppies = new List<string>();
            public string Acsi;
            public string Scsi;
            public string Ide;
            public string GemdosDir;
            public string AutoRun;
            public string TosRes;
            public int[] BootIndex = new int[] { -1, -1 };        // -1 = automatic

            public bool IsEmpty { get { return Floppies.Count == 0 && Acsi == null && Scsi == null && Ide == null && GemdosDir == null; } }
            public bool HasHardDisk { get { return Acsi != null || Scsi != null || Ide != null || GemdosDir != null; } }
            // Hard disk images (GEMDOS folders excluded : they are emulated by Hatari and work with every TOS)
            public bool HasHardDiskImage { get { return Acsi != null || Scsi != null || Ide != null; } }

            public void AddFile(string file)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                Images.Add(file);

                if (_acsiExtensions.Contains(ext))
                    Acsi = Acsi ?? file;
                else if (_scsiExtensions.Contains(ext))
                    Scsi = Scsi ?? file;
                else if (_ideExtensions.Contains(ext))
                    Ide = Ide ?? file;
                else if (ext == ".gemdos" && Directory.Exists(file))
                    GemdosDir = GemdosDir ?? file;      // RetroBat convention : folder named <game>.gemdos
                else if (ext == ".gem" || ext == ".gemdos")
                {
                    // hatariB convention : dummy file next to a folder with the same name
                    string dir = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
                    if (Directory.Exists(dir))
                        GemdosDir = GemdosDir ?? dir;
                    else
                        SimpleLogger.Instance.Warning("[Generator] Hatari: GEMDOS folder not found : " + dir);
                }
                else
                    Floppies.Add(file);
            }

            /// <summary>Disk inserted at boot in drive 0 (A) or 1 (B), null = empty drive</summary>
            public string GetDriveDisk(int drive)
            {
                int boot = BootIndex[drive];
                if (boot == 0)
                    return null;

                if (boot > 0)
                {
                    if (boot > Images.Count)
                        return null;

                    string image = Images[boot - 1];
                    return Floppies.Contains(image) ? image : null;
                }

                // Automatic : first floppy in drive A, second one in drive B
                return Floppies.Count > drive ? Floppies[drive] : null;
            }
        }

        private string GetCpuClock()
        {
            if (SystemConfig.isOptSet("hatari_frequency") && !string.IsNullOrEmpty(SystemConfig["hatari_frequency"]))
                return SystemConfig["hatari_frequency"];

            return _machineType == "megaste" ? "16" : "8";
        }

        /// <summary>
        /// Reads the TOS version from the ROM header, as Hatari does (word at offset 2, EmuTOS identified by 'ETOS' at offset 0x2C).
        /// Returns 0 if unknown.
        /// </summary>
        internal static int GetTosVersion(string tosFile, out bool isEmuTos)
        {
            isEmuTos = false;

            try
            {
                byte[] header = new byte[0x40];
                using (var fs = File.OpenRead(tosFile))
                {
                    if (fs.Read(header, 0, header.Length) < header.Length)
                        return 0;
                }

                // RAM TOS images start with a loader : version unknown
                if (header[0] == 0x46 && header[1] == 0xFC && header[2] == 0x27 && header[3] == 0x00)
                    return 0;

                isEmuTos = header[0x2C] == 'E' && header[0x2D] == 'T' && header[0x2E] == 'O' && header[0x2F] == 'S';
                return (header[2] << 8) | header[3];
            }
            catch { return 0; }
        }

        private void SetupHatari(string cfgFile, string path, HatariMedia media, string saveStateFile, string autoSaveFile, bool fullscreen)
        {
            using (var ini = IniFile.FromFile(cfgFile, IniOptions.UseSpaces))
            {
                // LOG
                ini.WriteValue("Log", "bConfirmQuit", "FALSE");
                // Only fatal errors open a blocking dialog (Hatari silently fixes TOS / machine mismatches)
                ini.WriteValue("Log", "nAlertDlgLogLevel", "0");
                // Log file next to the emulator, overwritten at each launch (stderr is not visible on Windows)
                // nTextLogLevel is left untouched : default INFO, can be set to 5 (DEBUG) manually in hatari.cfg
                ini.WriteValue("Log", "sLogFileName", Path.Combine(path, "hatari.log"));

                // SCREEN
                ini.WriteValue("Screen", "bFullScreen", fullscreen ? "TRUE" : "FALSE");
                ini.WriteValue("Screen", "bKeepResolution", "TRUE");
                ini.WriteValue("Screen", "bShowStatusbar", "FALSE");
                ini.WriteValue("Screen", "bShowDriveLed", "FALSE");

                BindBoolIniFeatureOn(ini, "Screen", "bUseVsync", "hatari_vsync", "TRUE", "FALSE");
                BindIniFeature(ini, "Screen", "nMonitorType", "hatari_monitor", "1");

                string screenshotsDir = AppConfig.GetFullPath("screenshots");
                if (!string.IsNullOrEmpty(screenshotsDir) && Directory.Exists(screenshotsDir))
                    ini.WriteValue("Screen", "szScreenShotDir", screenshotsDir);

                // MEMORY SNAPSHOTS
                BindBoolIniFeature(ini, "Memory", "bAutoSave", "hatari_autosave", "TRUE", "FALSE");
                ini.WriteValue("Memory", "szMemoryCaptureFileName", saveStateFile);
                ini.WriteValue("Memory", "szAutoSaveFileName", autoSaveFile);

                // FLOPPY
                // Disks are also written in the config file : used when the autosave snapshot is restored (no disk on the command line)
                string diskA = media.GetDriveDisk(0);
                string diskB = media.GetDriveDisk(1);

                // Disk B is inserted explicitly when needed, automatic insertion would conflict with #BOOTB
                ini.WriteValue("Floppy", "bAutoInsertDiskB", media.Floppies.Count > 1 ? "FALSE" : "TRUE");
                ini.WriteValue("Floppy", "szDiskAZipPath", "");
                ini.WriteValue("Floppy", "szDiskBZipPath", "");
                ini.WriteValue("Floppy", "szDiskAFileName", diskA ?? "");
                ini.WriteValue("Floppy", "szDiskBFileName", diskB != null && diskB != diskA ? diskB : "");
                ini.WriteValue("Floppy", "EnableDriveA", "TRUE");
                ini.WriteValue("Floppy", "EnableDriveB", "TRUE");

                // Fast floppy speeds up loading but can break some copy protections (STX images)
                BindBoolIniFeature(ini, "Floppy", "FastFloppy", "hatari_fastfloppy", "TRUE", "FALSE");

                // File selector opens in the game folder (disk swap for games with more than 2 disks)
                string firstImage = media.Images.FirstOrDefault();
                string diskDir = firstImage != null ? Path.GetDirectoryName(firstImage) : null;
                ini.WriteValue("Floppy", "szDiskImageDirectory", diskDir != null && Directory.Exists(diskDir) ? diskDir : Path.Combine(AppConfig.GetFullPath("roms"), "atarist"));

                // HARD DISKS (also passed on command line, kept here for autosave restore)
                // Boot from hard disk only when there is no floppy to boot from
                bool bootFromHardDisk = media.HasHardDisk && diskA == null;

                ini.WriteValue("HardDisk", "szHardDiskDirectory", media.GemdosDir ?? path);
                ini.WriteValue("HardDisk", "bUseHardDiskDirectory", media.GemdosDir != null ? "TRUE" : "FALSE");
                ini.WriteValue("HardDisk", "bBootFromHardDisk", bootFromHardDisk ? "TRUE" : "FALSE");

                ini.WriteValue("ACSI", "bUseDevice0", media.Acsi != null ? "TRUE" : "FALSE");
                if (media.Acsi != null)
                    ini.WriteValue("ACSI", "sDeviceFile0", media.Acsi);

                ini.WriteValue("SCSI", "bUseDevice0", media.Scsi != null ? "TRUE" : "FALSE");
                if (media.Scsi != null)
                    ini.WriteValue("SCSI", "sDeviceFile0", media.Scsi);

                ini.WriteValue("IDE", "bUseDevice0", media.Ide != null ? "TRUE" : "FALSE");
                if (media.Ide != null)
                    ini.WriteValue("IDE", "sDeviceFile0", media.Ide);

                // ROM
                string tosDir = Path.Combine(AppConfig.GetFullPath("bios"), "hatari", "tos");
                string tosfile = Path.Combine(AppConfig.GetFullPath("bios"), "tos.img");
                string emutos = Path.Combine(tosDir, "emutos.img");

                if (SystemConfig.isOptSet("hatari_tos") && !string.IsNullOrEmpty(SystemConfig["hatari_tos"]) && SystemConfig["hatari_tos"] != "default")
                    tosfile = Path.Combine(tosDir, SystemConfig["hatari_tos"]);
                else if (media.HasHardDiskImage && File.Exists(emutos))
                {
                    // Original TOS needs a hard disk driver inside the image, EmuTOS reads ACSI / SCSI / IDE disks natively
                    tosfile = emutos;
                    SimpleLogger.Instance.Info("[Generator] Hatari: hard disk image detected, using EmuTOS.");
                }
                else
                {
                    // No tos.img : fall back on EmuTOS, which runs on every machine
                    if (!File.Exists(tosfile) && File.Exists(emutos))
                        tosfile = emutos;

                    // Autostart (#AUTO, .autorun) and GEMDOS hard disk emulation require TOS 1.04 or later
                    if ((media.GemdosDir != null || !string.IsNullOrEmpty(media.AutoRun)) && File.Exists(tosfile))
                    {
                        bool isEmuTos;
                        int tosVersion = GetTosVersion(tosfile, out isEmuTos);

                        if (!isEmuTos && tosVersion > 0 && tosVersion < 0x0104)
                        {
                            string newTos = new string[] { "tos104.img", "tos206.img", "emutos.img" }
                                .Select(t => Path.Combine(tosDir, t))
                                .FirstOrDefault(t => File.Exists(t));

                            if (newTos != null)
                            {
                                SimpleLogger.Instance.Info("[Generator] Hatari: TOS " + (tosVersion >> 8) + "." + (tosVersion & 0xFF).ToString("X2") + " does not support autostart / GEMDOS hard disk, using " + Path.GetFileName(newTos));
                                tosfile = newTos;
                            }
                        }
                    }
                }

                string tosName = Path.GetFileName(tosfile);

                if (!File.Exists(tosfile))
                    throw new ApplicationException("Missing TOS file " + tosName + " in 'bios' folder.");

                ini.WriteValue("ROM", "szTosImageFileName", tosfile);

                // SYSTEM
                // Machine type : user choice, otherwise STE when the TOS or the Jaguar joypads require it, otherwise ST
                _machineType = "st";
                if (SystemConfig.isOptSet("hatari_machine") && !string.IsNullOrEmpty(SystemConfig["hatari_machine"]))
                    _machineType = SystemConfig["hatari_machine"];
                else if (tosName.StartsWith("tos106", StringComparison.OrdinalIgnoreCase) || tosName.StartsWith("tos162", StringComparison.OrdinalIgnoreCase))
                    _machineType = "ste";
                else if (SystemConfig.getOptBoolean("hatari_joypad"))
                    _machineType = "ste";

                // Machine type and default values (also passed on command line, see Generate)
                switch (_machineType)
                {
                    case "megast":
                        ini.WriteValue("System", "nModelType", "1");
                        ini.WriteValue("Memory", "nMemorySize", "2048");
                        break;
                    case "ste":
                        ini.WriteValue("System", "nModelType", "2");
                        ini.WriteValue("Memory", "nMemorySize", "4096");
                        break;
                    case "megaste":
                        ini.WriteValue("System", "nModelType", "3");
                        ini.WriteValue("Memory", "nMemorySize", "4096");
                        break;
                    default:
                        _machineType = "st";
                        ini.WriteValue("System", "nModelType", "0");
                        ini.WriteValue("Memory", "nMemorySize", "1024");
                        break;
                }

                ini.WriteValue("System", "nCpuFreq", GetCpuClock());

                // Overwrite values if feature is set
                if (SystemConfig.isOptSet("hatari_memory") && !string.IsNullOrEmpty(SystemConfig["hatari_memory"]))
                    ini.WriteValue("Memory", "nMemorySize", SystemConfig["hatari_memory"]);

                // other system options
                BindBoolIniFeature(ini, "System", "bFastBoot", "hatari_fastboot", "TRUE", "FALSE");

                // VIDEO
                string recordsDir = AppConfig.GetFullPath("records");
                if (!string.IsNullOrEmpty(recordsDir))
                {
                    string recordingDir = Path.Combine(recordsDir, "output", "hatari");
                    if (!Directory.Exists(recordingDir))
                        try { Directory.CreateDirectory(recordingDir); } catch { }

                    ini.WriteValue("Video", "AviRecordFile", Path.Combine(recordingDir, "hatari.avi"));
                }

                // HOTKEYS
                // Atari ST keyboard has F1 to F10 : keys not existing on the ST (F11, F12, Pause) are preferred
                // F2, F4 and F8 follow RetroBat conventions (save state, load state, screenshot)
                ini.WriteValue("KeyShortcutsWithoutMod", "kOptions", "F12");
                ini.WriteValue("KeyShortcutsWithoutMod", "kFullScreen", "F11");
                ini.WriteValue("KeyShortcutsWithoutMod", "kPause", "Pause");

                if (SystemConfig["hatari_hotkeys"] == "atari")
                {
                    // Function keys left to the Atari, snapshots available with AltGr + K / L / G
                    ini.WriteValue("KeyShortcutsWithoutMod", "kScreenShot", "");
                    ini.WriteValue("KeyShortcutsWithoutMod", "kLoadMem", "");
                    ini.WriteValue("KeyShortcutsWithoutMod", "kSaveMem", "");
                }
                else
                {
                    ini.WriteValue("KeyShortcutsWithoutMod", "kScreenShot", "F8");
                    ini.WriteValue("KeyShortcutsWithoutMod", "kLoadMem", "F4");
                    ini.WriteValue("KeyShortcutsWithoutMod", "kSaveMem", "F2");
                }

                ini.WriteValue("KeyShortcutsWithMod", "kScreenShot", "G");
                ini.WriteValue("KeyShortcutsWithMod", "kLoadMem", "L");
                ini.WriteValue("KeyShortcutsWithMod", "kSaveMem", "K");
                // Quit with AltGr + Q (Alt+F4 is disabled by Hatari, Escape is an Atari key)
                ini.WriteValue("KeyShortcutsWithMod", "kQuit", "Q");

                SetupJoysticks(ini);

                ini.Save();
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}