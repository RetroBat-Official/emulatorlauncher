using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    class Xm6proGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public Xm6proGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("xm6pro");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "XM6.exe");
            if (!File.Exists(exe))
                return null;

            // XM6 loads its ROM files from its own folder: reuse the X68000 BIOS files shared with px68k when missing
            CopyBiosFiles(path);

            List<string> disks = GetDiskFiles(system, rom);

            bool fullscreen = ShouldRunFullscreen();

            if (SystemConfig.isOptSet("68k_stretch") && SystemConfig["68k_stretch"] == "true")
                SystemConfig["bezel"] = "none";

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            SetupConfiguration(path, fullscreen, disks);

            // XM6 loads the first file in floppy drive 0 and the second one in floppy drive 1
            string arguments = string.Join(" ", disks.Take(2).Select(d => "\"" + d + "\""));

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = arguments,
            };
        }

        // Floppy disk image formats handled by XM6 (.hdf is a SASI hard disk image, never loaded in a floppy drive)
        private static readonly string[] _floppyExtensions = new string[] { ".dim", ".img", ".d88", ".88d", ".hdm", ".dup", ".2hd", ".xdf" };

        /// <summary>
        /// Returns the disk images to load, in drive order: .zip is opened natively by XM6, .7z is extracted, .m3u lists the disks.
        /// </summary>
        private List<string> GetDiskFiles(string system, string rom)
        {
            var disks = new List<string>();
            string ext = Path.GetExtension(rom).ToLowerInvariant();

            if (ext == ".7z")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    var files = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories)
                        .OrderBy(f => f, StringComparer.InvariantCultureIgnoreCase)
                        .ToList();

                    disks.AddRange(files.Where(f => _floppyExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));

                    // Archive containing a hard disk image only
                    if (disks.Count == 0)
                    {
                        string hardDisk = files.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() == ".hdf");
                        if (hardDisk != null)
                            disks.Add(hardDisk);
                    }

                    if (disks.Count > 0)
                        ValidateUncompressedGame(); // Games write to their disks (saves): offer to keep the extracted files
                }
            }
            else if (ext == ".m3u")
            {
                string romDir = Path.GetDirectoryName(rom);

                foreach (string line in File.ReadAllLines(rom))
                {
                    string entry = line.Trim();
                    if (string.IsNullOrEmpty(entry) || entry.StartsWith("#"))
                        continue;

                    string disk = Path.IsPathRooted(entry) ? entry : Path.Combine(romDir, entry);
                    if (File.Exists(disk))
                        disks.Add(Path.GetFullPath(disk));
                }
            }

            if (disks.Count == 0)
                disks.Add(rom);

            return disks;
        }

        private static readonly string[] _biosFiles = new string[] { "IPLROM.DAT", "CGROM.DAT", "IPLROMXV.DAT", "IPLROMCO.DAT", "IPLROM30.DAT" };

        private void CopyBiosFiles(string emulatorPath)
        {
            string biosRoot = AppConfig.GetFullPath("bios");
            if (string.IsNullOrEmpty(biosRoot))
                return;

            string biosPath = Path.Combine(biosRoot, "keropi");
            if (!Directory.Exists(biosPath))
                return;

            foreach (string biosFile in _biosFiles)
            {
                string source = Path.Combine(biosPath, biosFile);
                string target = Path.Combine(emulatorPath, biosFile);

                if (File.Exists(source) && !File.Exists(target))
                {
                    SimpleLogger.Instance.Info("[Generator] Copying " + biosFile + " from bios\\keropi to xm6pro folder.");
                    FileTools.TryCopyFile(source, target, false);
                }
            }
        }

        private void SetupConfiguration(string path, bool fullscreen, List<string> disks)
        {
            string iniFile = Path.Combine(path, "XM6.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    ini.Remove("SASI", "File0");
                    ini.WriteValue("Window", "Full", fullscreen ? "1" : "0");
                    ini.WriteValue("Resume", "Screen", "1"); // Required for [Window] Full to be applied at startup

                    // Floppy disks are passed on the command line (drive 0, drive 1): disable floppy resume so previous disks are not reinserted
                    ini.WriteValue("Resume", "FD", "0");

                    // Recent files are rebuilt at each launch (XM6 appends every loaded disk, so the lists grow endlessly)
                    ini.ClearSection("MRU0");
                    ini.ClearSection("MRU1");

                    // More than 2 disks: list the remaining ones in drive 1 recent files, to swap them from the Floppy Drive #1 menu
                    for (int i = 2; i < disks.Count; i++)
                        ini.WriteValue("MRU1", "File" + (i - 2), disks[i]);

                    ini.WriteValue("Basic", "AutoMemSw", "1");

                    // Full screen scaling: Stretch enables scaling (integer x2), Maximum stretches to the whole screen,
                    // Rescale corrects the aspect ratio of some non-standard video modes (no effect on standard modes)
                    bool stretchToScreen = SystemConfig.getOptBoolean("68k_stretch");
                    ini.WriteValue("Display", "FullScreenStretch", "1");
                    ini.WriteValue("Display", "FullScreenRescale", "1");
                    ini.WriteValue("Display", "FullScreenMaximum", stretchToScreen ? "1" : "0");

                    BindBoolIniFeature(ini, "Display", "Scanlines", "68k_scanlines", "1", "0"); // Works with few games
                    BindBoolIniFeature(ini, "Display", "Smoothing", "68k_smooth", "1", "0");

                    // Status bar (window) and info bar (full screen): floppy names, drive lamps, speed
                    string showInfo = SystemConfig.getOptBoolean("68k_statusbar") ? "1" : "0";
                    ini.WriteValue("Window", "StatusBar", showInfo);
                    ini.WriteValue("Display", "InfoFilesF", showInfo);
                    ini.WriteValue("Display", "InfoLampsF", showInfo);
                    ini.WriteValue("Display", "InfoSpeedF", showInfo);

                    BindBoolIniFeature(ini, "Misc", "FloppySpeed", "68k_floppy", "1", "0"); // Improves loading

                    // MIDI board: only plug it when an output device is selected, otherwise games detecting the board send their music to no device
                    string midiDevice = SystemConfig.GetValueOrDefault("68k_midi_index", "0");
                    ini.WriteValue("MIDI", "ID", midiDevice != "0" ? "1" : "0");
                    ini.WriteValue("MIDI", "OutDevice", midiDevice); // MIDI output device index, as listed in the emulator
                    BindIniFeature(ini, "MIDI", "IntLevel", "68k_midi_interrupt", "0"); // MIDI interrupt level

                    BindIniFeature(ini, "Basic", "Clock", "68k_clock", "0");
                    BindIniFeature(ini, "Basic", "Memory", "68k_ram", "3");

                    ConfigureControllers(ini);

                    ini.Save();
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Unable to write XM6.ini: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// XM6 reads PC controllers through DirectInput: Device A (Device1) and Device B (Device2) hold the DirectInput game controller index + 1 (0 = none).
        /// ButtonDn keys: D = device slot (1 = A, 2 = B), n = physical button (1-C), value = (X68000 port - 1) << 16 | X68000 button (1 = A, 2 = B, 3 = A+B, 4-8, 0 = none).
        /// Player 1 controller goes to device A / port 1, player 2 controller to device B / port 2.
        /// </summary>
        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            for (int slot = 1; slot <= 2; slot++)
            {
                var ctrl = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == slot && c.Config != null && !c.IsKeyboard);
                var dinput = ctrl != null ? ctrl.DirectInput : null;

                int device = dinput != null ? dinput.DeviceIndex + 1 : 0;
                ini.WriteValue("Joystick", "Device" + slot, device.ToString());

                if (ctrl != null)
                    SimpleLogger.Instance.Info("[Generator] XM6 device " + (char)('A' + slot - 1) + " : " + ctrl.Name + (dinput != null ? " (DirectInput index " + dinput.DeviceIndex + ")" : " (not found in DirectInput)"));

                // Physical buttons 1-8 drive X68000 buttons 1-8 of the same port (XM6 defaults), buttons 9-12 unused
                for (int button = 1; button <= 12; button++)
                {
                    int value = button <= 8 ? ((slot - 1) << 16) | button : 0;
                    ini.WriteValue("Joystick", "Button" + slot + button.ToString("X"), value.ToString());
                }
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
