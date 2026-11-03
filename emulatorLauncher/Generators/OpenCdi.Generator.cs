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

        private static readonly string[] BiosNames = { "cdi220b.rom", "cdi220.rom", "cdi200.rom", "cdi.rom", "cdi490a.rom" };

        // Digital Video Cartridge ROM names
        private static readonly string[] DvcNames = { "vmpega.rom", "impega.rom" };

        // BIOS files that can be taken from the MAME sets
        private static readonly Dictionary<string, string> MameBiosFiles = new Dictionary<string, string>()
        {
            { "cdi220b.rom", "279683ca" },
            { "cdi200.rom", "40c4e6b9" },
        };

        private static readonly string[] SupportedExtensions = { ".chd", ".cue", ".iso", ".bin" };

        private const int NvramSize = 8192;
        private static readonly byte[] NvramDefaultClock = { 0x00, 0x00, 0x00, 0x12, 0x01, 0x01, 0x01, 0x95 };

        // Win32 menu command of OpenCDi 'Quick Load State (Slot 1)' (src/gui.rs)
        private const int WM_COMMAND = 0x0111;
        private const int ID_STATE_QUICK_LOAD = 1071;

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _fullscreen;
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

            string ext = Path.GetExtension(rom).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
                throw new ApplicationException("OpenCDi only supports .chd, .cue, .iso and .bin disc images.");

            _fullscreen = ShouldRunFullscreen();
            _resolution = resolution;

            if (_fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            string savesPath = Path.Combine(PathHelper.ResolvePath(AppConfig.GetFullPath("saves")), system, "opencdi");
            try { Directory.CreateDirectory(savesPath); }
            catch { }

            SetupSaveStates(system, rom, path);

            bool dvcEnabled = !SystemConfig.isOptSet("opencdi_dvc") || SystemConfig.getOptBoolean("opencdi_dvc");

            string biosFile = GetBiosFile(path);
            string dvcFile = dvcEnabled ? GetDvcFile(path) : null;
            string nvramFile = GetNvramFile(path, savesPath, rom);

            // The disc must stay the first argument : OpenCDi takes the first argument not starting with '--' as the disc,
            var commandArray = new List<string>
            {
                "\"" + rom + "\""
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
            commandArray.Add("\"" + nvramFile + "\"");

            if (SystemConfig["opencdi_pointer"] == "mouse")
                commandArray.Add("--mouse");

            return new ProcessStartInfo()
            {
                FileName = exe,
                // savestates\, opencdi_controls.ini, opencdi_recent.txt and the BMP screenshots are written in the working directory
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
            };
        }

        #region Save states
        /// <summary>
        /// OpenCDi saves in emulators\opencdi\savestates: the watcher copies each new state to the RetroBat folder as the next slot.
        /// A state chosen in EmulationStation is copied to slot 1 and loaded once the emulation runs (no command line option).
        /// </summary>
        private void SetupSaveStates(string system, string rom, string path)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported("opencdi"))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, "opencdi", "opencdi");
            string emulatorPath = Path.Combine(path, "savestates");
            string icon = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png");

            // Copied before the watcher starts, so it is not imported back as a new state
            string stateFile = SystemConfig["state_file"];
            if (!string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
            {
                try
                {
                    FileTools.TryCreateDirectory(emulatorPath);
                    File.Copy(stateFile, OpenCdiSaveStatesMonitor.GetEmulatorStateFile(emulatorPath, rom, 1), true);
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

        #region BIOS / DVC / NVRAM
        /// <summary>
        /// System BIOS passed with --rom : bios\cdi, bios, then the MAME sets (cdimono1.zip, cdibios.zip) extracted to emulators\opencdi\bios.
        /// Returns null when the BIOS is only in the emulator folder (OpenCDi finds it by itself).
        /// </summary>
        private string GetBiosFile(string path)
        {
            string biosPath = AppConfig.GetFullPath("bios");
            string targetPath = Path.Combine(path, "bios");

            if (!string.IsNullOrEmpty(biosPath))
            {
                string bios = FindLooseFile(new[] { Path.Combine(biosPath, "cdi"), biosPath }, BiosNames);
                if (bios != null)
                    return bios;

                var zipFiles = new string[]
                {
                    Path.Combine(biosPath, "cdimono1.zip"),
                    Path.Combine(biosPath, "cdibios.zip"),
                    Path.Combine(biosPath, "same_cdi", "bios", "cdimono1.zip"),
                    Path.Combine(biosPath, "same_cdi", "bios", "cdibios.zip")
                };

                bios = ExtractFromMameSets(zipFiles, targetPath);
                if (bios != null)
                    return bios;
            }

            // ROM placed by the user in emulators\opencdi\bios or next to opencdi.exe
            if (BiosNames.Any(n => File.Exists(Path.Combine(targetPath, n)) || File.Exists(Path.Combine(path, n))))
                return null;

            throw new ApplicationException("Missing CD-i BIOS : place 'cdi220b.rom' in 'bios\\cdi' or 'cdimono1.zip' in the RetroBat bios folder.");
        }

        /// <summary>
        /// Digital Video Cartridge ROM passed with --dvc (only needed by the FMV games and Video CDs).
        /// </summary>
        private string GetDvcFile(string path)
        {
            string biosPath = AppConfig.GetFullPath("bios");
            if (!string.IsNullOrEmpty(biosPath))
            {
                string dvc = FindLooseFile(new[] { Path.Combine(biosPath, "cdi"), biosPath }, DvcNames);
                if (dvc != null)
                    return dvc;
            }

            bool inEmulatorFolder = DvcNames.Any(n =>
                File.Exists(Path.Combine(path, "dvc", n)) || File.Exists(Path.Combine(path, "bios", n)) || File.Exists(Path.Combine(path, n)));

            if (!inEmulatorFolder)
                SimpleLogger.Instance.Warning("[OpenCdiGenerator] DVC ROM (vmpega.rom) not found in 'bios\\cdi' : FMV games and Video CDs will not work.");

            return null;
        }

        /// <summary>
        /// First file found, names being in OpenCDi priority order.
        /// </summary>
        private static string FindLooseFile(string[] folders, string[] names)
        {
            foreach (string name in names)
            {
                foreach (string folder in folders)
                {
                    string file = Path.Combine(folder, name);
                    if (File.Exists(file))
                        return file;
                }
            }

            return null;
        }

        /// <summary>
        /// Extract the system BIOS from the MAME CD-i sets, checking the CRC of the entry.
        /// </summary>
        private static string ExtractFromMameSets(string[] zipFiles, string targetPath)
        {
            foreach (var bios in MameBiosFiles)
            {
                string target = Path.Combine(targetPath, bios.Key);
                if (File.Exists(target) && new FileInfo(target).Length == 0x80000)
                    return target;

                foreach (string zipFile in zipFiles)
                {
                    if (!File.Exists(zipFile))
                        continue;

                    try
                    {
                        using (var archive = Zip.Open(zipFile))
                        {
                            if (archive == null)
                                continue;

                            var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory &&
                                string.Equals(Path.GetFileName(e.Filename), bios.Key, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(e.HexCrc, bios.Value, StringComparison.OrdinalIgnoreCase));

                            if (entry == null)
                                continue;

                            entry.Extract(targetPath);

                            if (File.Exists(target))
                            {
                                SimpleLogger.Instance.Info("[OpenCdiGenerator] Extracted " + bios.Key + " from " + zipFile);
                                return target;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Warning("[OpenCdiGenerator] Unable to extract " + bios.Key + " from " + zipFile + " : " + ex.Message);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// NVRAM file passed with --nvram : one file per game (default) or one file shared by all the games, as on a real player.
        /// </summary>
        private string GetNvramFile(string path, string savesPath, string rom)
        {
            string nvramFile;

            if (SystemConfig["opencdi_nvram"] == "shared")
                nvramFile = Path.Combine(savesPath, "nvram_mk48t08.bin");
            else
                nvramFile = Path.Combine(savesPath, "nvram", Path.GetFileNameWithoutExtension(rom) + ".bin");

            if (File.Exists(nvramFile))
                return nvramFile;

            try
            {
                string dir = Path.GetDirectoryName(nvramFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Shared NVRAM : keep the saves made before RetroBat handled the NVRAM (file of the emulator folder)
                string legacyNvram = Path.Combine(path, "bios", "nvram_mk48t08.bin");
                if (SystemConfig["opencdi_nvram"] == "shared" && File.Exists(legacyNvram))
                {
                    File.Copy(legacyNvram, nvramFile);
                    return nvramFile;
                }

                // OpenCDi only uses the --nvram path when the file exists: create a blank NVRAM, same content as OpenCDi's
                var blank = new byte[NvramSize];
                Array.Copy(NvramDefaultClock, 0, blank, NvramSize - NvramDefaultClock.Length, NvramDefaultClock.Length);
                File.WriteAllBytes(nvramFile, blank);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[OpenCdiGenerator] Unable to create NVRAM file " + nvramFile + " : " + ex.Message);
            }

            return nvramFile;
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

            bool windowReady = !_fullscreen && !_loadState;

            try
            {
                while (!process.WaitForExit(50))
                {
                    if (!windowReady)
                    {
                        // The title shows the speed counters ('|') once the emulation runs : the Win32 menu is attached at this point,
                        // and the BIOS error message box (title without '|') is never covered
                        IntPtr hWnd = User32.FindHwnds(process.Id).FirstOrDefault(h =>
                        {
                            string title = User32.GetWindowText(h);
                            return title != null && title.StartsWith("OpenCDi") && title.Contains("|");
                        });

                        if (hWnd != IntPtr.Zero)
                        {
                            windowReady = true;

                            if (_fullscreen)
                                bezel = SetupFullscreenWindow(hWnd);

                            if (_loadState)
                                User32.SendMessage(hWnd, WM_COMMAND, ID_STATE_QUICK_LOAD, 0);
                        }
                    }

                    Application.DoEvents();
                }

                // Exit code 1 : BIOS or disc error, already reported by OpenCDi in a message box
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
        /// Borderless window without the menu bar, covering the screen or fitted into the bezel hole.
        /// OpenCDi own fullscreen (F11, winit borderless) keeps the menu bar and always uses the current screen.
        /// </summary>
        private FakeBezelFrm SetupFullscreenWindow(IntPtr hWnd)
        {
            FakeBezelFrm bezel = null;

            // Without MonitorIndex, keep the screen chosen by the emulator
            var screen = Program.IsMonitorIndexSet ? Program.TargetScreen : Screen.FromHandle(hWnd);
            var bounds = screen.Bounds;

            int posX = bounds.Left;
            int posY = bounds.Top;
            int resX = _resolution == null ? bounds.Width : _resolution.Width;
            int resY = _resolution == null ? bounds.Height : _resolution.Height;

            // Remove the menu bar and the window borders
            User32.SetMenu(hWnd, IntPtr.Zero);

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
