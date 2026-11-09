using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    class OpenCdiGenerator : Generator
    {
        public OpenCdiGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        // System BIOS files, in the order OpenCDi looks for them (zip sets are read by OpenCDi)
        private static readonly string[] BiosNames = { "cdi220b.rom", "cdi200.rom", "cdimono1.zip", "cdibios.zip", "cdi.rom", "cdi220.rom", "cdi490a.rom" };

        // CRC32 of the Mono-I system ROMs in the MAME sets (cdi220b.rom, cdi200.rom)
        private static readonly string[] MonoOneBiosCrcs = { "279683ca", "40c4e6b9" };

        // Digital Video Cartridge ROMs, loose or in a zip set (vmpega.rom)
        private static readonly string[] DvcNames = { "vmpega.rom", "impega.rom" };
        private static readonly string[] DvcZipNames = { "cdimono1.zip", "cdibios.zip" };
        private const string VmpegaCrc = "db264e8b";

        private static readonly string[] SupportedExtensions = { ".chd", ".cue", ".iso", ".bin" };

        // Win32 menu command of OpenCDi 'Quick Load State (Slot 1)' (src/gui.rs)
        private const int WM_COMMAND = 0x0111;
        private const int ID_STATE_QUICK_LOAD = 1071;

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _placeWindow;
        private SaveStatesWatcher _saveStatesWatcher;
        private bool _loadState;

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("opencdi");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "opencdi.exe");
            if (!File.Exists(exe))
                return null;

            // .m3u : OpenCDi boots one disc, the next ones are inserted in game with Ctrl+I
            string disc = rom;
            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
            {
                string first = File.ReadAllLines(rom).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("#"));
                if (string.IsNullOrEmpty(first))
                    throw new ApplicationException("The .m3u file does not list any disc.");

                disc = Path.IsPathRooted(first) ? first : Path.Combine(Path.GetDirectoryName(rom), first);
            }

            string ext = Path.GetExtension(disc).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
                throw new ApplicationException("OpenCDi only supports .chd, .cue, .iso and .bin disc images.");

            bool fullscreen = ShouldRunFullscreen();
            _resolution = resolution;

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            // OpenCDi fullscreen covers the whole screen and its monitor index doesn't follow the RetroBat order:
            // with a bezel or a chosen screen, EmulatorLauncher places the window itself
            _placeWindow = fullscreen && (_bezelFileInfo != null || Program.IsMonitorIndexSet);

            string savesPath = Path.Combine(PathHelper.ResolvePath(AppConfig.GetFullPath("saves")), system, "opencdi");
            FileTools.TryCreateDirectory(savesPath);

            SetupSaveStates(system, rom, disc, path);

            bool dvcEnabled = !SystemConfig.isOptSet("opencdi_dvc") || SystemConfig.getOptBoolean("opencdi_dvc");
            bool mouse = SystemConfig["opencdi_pointer"] == "mouse";

            string biosFile = GetBiosFile(path);
            string dvcFile = dvcEnabled ? GetDvcFile(path) : null;

            // The pointer device is saved in opencdi_controls.ini (F9) and used when --mouse is not given
            SetInputDevice(path, mouse);

            var commandArray = new List<string>
            {
                "\"" + disc + "\""
            };

            if (!string.IsNullOrEmpty(biosFile))
            {
                commandArray.Add("--rom");
                commandArray.Add("\"" + biosFile + "\"");
            }

            if (!dvcEnabled)
                commandArray.Add("--no-dvc");
            else if (!string.IsNullOrEmpty(dvcFile))
            {
                commandArray.Add("--dvc");
                commandArray.Add("\"" + dvcFile + "\"");
            }

            commandArray.Add("--nvram");
            if (SystemConfig["opencdi_nvram"] == "shared")
                commandArray.Add("\"" + Path.Combine(savesPath, "nvram_mk48t08.bin") + "\"");
            else
                commandArray.Add("\"" + Path.Combine(savesPath, "nvram", Path.GetFileNameWithoutExtension(rom) + ".bin") + "\"");

            string screenshotPath = AppConfig.GetFullPath("screenshots");
            if (!string.IsNullOrEmpty(screenshotPath))
            {
                commandArray.Add("--screenshot-dir");
                commandArray.Add("\"" + screenshotPath + "\"");
            }

            if (_placeWindow)
                commandArray.Add("--no-menu");
            else if (fullscreen)
                commandArray.Add("--fullscreen");

            if (mouse)
                commandArray.Add("--mouse");

            // Slider value in percent (10 to 200), OpenCDi reads values above 5 as a percentage
            if (SystemConfig.isOptSet("opencdi_mouse_sensitivity") && !string.IsNullOrEmpty(SystemConfig["opencdi_mouse_sensitivity"]))
            {
                commandArray.Add("--mouse-sensitivity");
                commandArray.Add(SystemConfig["opencdi_mouse_sensitivity"].ToIntegerString());
            }

            if (SystemConfig.isOptSet("opencdi_video_levels") && !string.IsNullOrEmpty(SystemConfig["opencdi_video_levels"]))
            {
                commandArray.Add("--video-levels");
                commandArray.Add(SystemConfig["opencdi_video_levels"]);
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                // opencdi.ini, opencdi_controls.ini and opencdi_recent.txt are written in the working directory
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
            };
        }

        /// <summary>
        /// Writes input_device=Gamepad|Mouse in opencdi_controls.ini, keeping the other keys.
        /// </summary>
        private static void SetInputDevice(string path, bool mouse)
        {
            string iniFile = Path.Combine(path, "opencdi_controls.ini");
            string line = "input_device=" + (mouse ? "Mouse" : "Gamepad");

            try
            {
                var lines = File.Exists(iniFile) ? File.ReadAllLines(iniFile).ToList() : new List<string>();

                int index = lines.FindIndex(l => l.Contains("=") && l.Split('=')[0].Trim().Equals("input_device", StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                    lines[index] = line;
                else
                    lines.Insert(0, line);

                File.WriteAllLines(iniFile, lines);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[OpenCdiGenerator] Unable to write " + iniFile + " : " + ex.Message);
            }
        }

        #region Save states
        /// <summary>
        /// OpenCDi saves in emulators\opencdi\states: the watcher copies each new state to the RetroBat folder as the next slot.
        /// A state chosen in EmulationStation is copied to slot 1 and loaded once the emulation runs (no command line option).
        /// </summary>
        private void SetupSaveStates(string system, string rom, string disc, string path)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported("opencdi"))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, "opencdi", "opencdi");
            string emulatorPath = Path.Combine(path, "states");
            string icon = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png");

            // Copied before the watcher starts, so it is not imported back as a new state
            string stateFile = SystemConfig["state_file"];
            if (!string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
            {
                try
                {
                    FileTools.TryCreateDirectory(emulatorPath);
                    File.Copy(stateFile, OpenCdiSaveStatesMonitor.GetEmulatorStateFile(emulatorPath, disc, 1), true);
                    _loadState = true;
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[OpenCdiGenerator] Unable to prepare save state " + stateFile + " : " + ex.Message);
                }
            }

            _saveStatesWatcher = new OpenCdiSaveStatesMonitor(rom, emulatorPath, localPath, icon);
            SimpleLogger.Instance.Info("[INFO] SavesStatesWatcher enabled.");
        }

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }
        #endregion

        #region BIOS / DVC
        // Same places as the other CD-i emulators: bios (loose ROMs, MAME set) and the SAME_CDI set
        private static string[] GetBiosFolders()
        {
            string biosPath = Program.AppConfig.GetFullPath("bios");
            if (string.IsNullOrEmpty(biosPath))
                return new string[0];

            return new[] { biosPath, Path.Combine(biosPath, "same_cdi", "bios") };
        }

        /// <summary>
        /// System BIOS passed with --rom (a .rom file or a zip set). Returns null when the BIOS is only in the emulator folder.
        /// </summary>
        private static string GetBiosFile(string path)
        {
            var folders = GetBiosFolders();

            foreach (string name in BiosNames)
            {
                foreach (string folder in folders)
                {
                    string file = Path.Combine(folder, name);
                    if (!File.Exists(file))
                        continue;

                    // A zip is only used when it holds a Mono-I system ROM, otherwise OpenCDi would quit without a message
                    if (name.EndsWith(".zip") && !ZipContains(file, MonoOneBiosCrcs, 0x80000))
                        continue;

                    return file;
                }
            }

            // ROM placed by the user in emulators\opencdi\bios or next to opencdi.exe
            if (BiosNames.Any(n => File.Exists(Path.Combine(path, "bios", n)) || File.Exists(Path.Combine(path, n))))
                return null;

            throw new ApplicationException("Missing CD-i BIOS : place 'cdimono1.zip' or 'cdi220b.rom' in the RetroBat bios folder.");
        }

        /// <summary>
        /// Digital Video Cartridge ROM passed with --dvc (a .rom file or a zip set), only needed by the FMV games and Video CDs.
        /// </summary>
        private static string GetDvcFile(string path)
        {
            var folders = GetBiosFolders();

            foreach (string name in DvcNames)
            {
                foreach (string folder in folders)
                {
                    string file = Path.Combine(folder, name);
                    if (File.Exists(file))
                        return file;
                }
            }

            foreach (string name in DvcZipNames)
            {
                foreach (string folder in folders)
                {
                    string file = Path.Combine(folder, name);
                    if (File.Exists(file) && ZipContains(file, new[] { VmpegaCrc }, 0x40000))
                        return file;
                }
            }

            bool inEmulatorFolder = DvcNames.Any(n =>
                File.Exists(Path.Combine(path, "dvc", n)) || File.Exists(Path.Combine(path, "bios", n)) || File.Exists(Path.Combine(path, n)));

            if (!inEmulatorFolder)
                SimpleLogger.Instance.Warning("[OpenCdiGenerator] DVC ROM (vmpega.rom) not found in the bios folder : FMV games and Video CDs will not work.");

            return null;
        }

        private static bool ZipContains(string zipFile, string[] crcs, long size)
        {
            try
            {
                return Zip.ListEntries(zipFile).Any(e => !e.IsDirectory && e.Length == size && crcs.Contains(e.HexCrc, StringComparer.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Window
        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            var process = StartEmulator(path);
            if (process == null)
                return ExitCode == ExitCodes.CustomError ? -1 : 0;

            Job.Current.AddProcess(process);

            bool windowReady = !_placeWindow && !_loadState;

            try
            {
                while (!process.WaitForExit(50))
                {
                    if (!windowReady)
                    {
                        // The title shows the speed counters ('|') once the emulation runs
                        IntPtr hWnd = User32.FindHwnds(process.Id).FirstOrDefault(h =>
                        {
                            string title = User32.GetWindowText(h);
                            return title != null && title.StartsWith("OpenCDi") && title.Contains("|");
                        });

                        if (hWnd != IntPtr.Zero)
                        {
                            windowReady = true;

                            if (_placeWindow)
                                bezel = SetupWindow(hWnd);

                            if (_loadState)
                                User32.SendMessage(hWnd, WM_COMMAND, ID_STATE_QUICK_LOAD, 0);
                        }
                    }

                    Application.DoEvents();
                }

                // Exit code 1 : BIOS or disc error, reported by OpenCDi
                int exitCode = ReportExitCode(process, path);
                return exitCode == 1 ? 0 : exitCode;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[OpenCdiGenerator] Error while waiting for the emulator : " + ex.Message, ex);
            }
            finally
            {
                bezel?.Dispose();
            }

            return -1;
        }

        /// <summary>
        /// Borderless window (started with --no-menu) covering the target screen or fitted into the bezel hole.
        /// </summary>
        private FakeBezelFrm SetupWindow(IntPtr hWnd)
        {
            FakeBezelFrm bezel = null;

            // Without MonitorIndex, keep the screen chosen by the emulator
            var screen = Program.IsMonitorIndexSet ? Program.TargetScreen : Screen.FromHandle(hWnd);
            var bounds = screen.Bounds;

            int posX = bounds.Left;
            int posY = bounds.Top;
            int resX = _resolution == null ? bounds.Width : _resolution.Width;
            int resY = _resolution == null ? bounds.Height : _resolution.Height;

            var style = User32.GetWindowStyle(hWnd);
            style &= ~WS.CAPTION;
            style &= ~WS.THICKFRAME;
            style &= ~WS.MAXIMIZEBOX;
            style &= ~WS.MINIMIZEBOX;
            style &= ~WS.SYSMENU;
            User32.SetWindowStyle(hWnd, style);

            if (_bezelFileInfo != null)
            {
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution, true, screen);

                // Fit the picture into the bezel hole (OpenCDi letterboxes its 768x560 frame in the window)
                if (bezel != null && bezel.ViewPort.Width > 0 && bezel.ViewPort.Height > 0)
                {
                    posX = bezel.ViewPort.Left;
                    posY = bezel.ViewPort.Top;
                    resX = bezel.ViewPort.Width;
                    resY = bezel.ViewPort.Height;
                }
            }

            User32.SetWindowPos(hWnd, IntPtr.Zero, posX, posY, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);

            // OpenCDi ignores the inputs when its window does not have the focus (the bezel forms take it when shown)
            User32.ForceForegroundWindow(hWnd);

            return bezel;
        }
        #endregion
    }
}
