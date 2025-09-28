﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class CxbxGenerator : Generator
    {
        public CxbxGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        #region XboxIsoVfs management
        private string MountIso(string rom)
        {
            if (Path.GetExtension(rom).ToLowerInvariant() != ".iso")
                return rom;
                                
            string xboxIsoVfsPath = Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "xbox-iso-vfs.exe");
            if (!File.Exists(xboxIsoVfsPath))
                throw new ApplicationException("xbox-iso-vfs is required and is not installed");

            // Check dokan is installed
            string dokan = Environment.GetEnvironmentVariable("DokanLibrary2");
            if (!Directory.Exists(dokan))
            {
                MountFile.ShowDownloadDokanPage();
                throw new ApplicationException("Dokan 2 is required and is not installed");
            }

            dokan = Path.Combine(dokan, "dokan2.dll");
            if (!File.Exists(dokan))
            {
                MountFile.ShowDownloadDokanPage();
                throw new ApplicationException("Dokan 2 is required and is not installed");
            }

            var drive = FileTools.FindFreeDriveLetter() ?? throw new ApplicationException("Unable to find a free drive letter to mount");

            var xboxIsoVfs = Process.Start(new ProcessStartInfo()
            {
                FileName = xboxIsoVfsPath,
                WorkingDirectory = Path.GetDirectoryName(rom),
                Arguments = "\"" + rom + "\" " + drive,
                UseShellExecute = false,    
                CreateNoWindow = true
            });

            int time = Environment.TickCount;
            int elapsed = 0;

            while (elapsed < 5000)
            {
                if (xboxIsoVfs.WaitForExit(10))
                    return rom;

                if (Directory.Exists(drive))
                {
                    Job.Current.AddProcess(xboxIsoVfs);
                    return drive;
                }

                int newTime = Environment.TickCount;
                elapsed = time - newTime;
                time = newTime;
            }

            try { xboxIsoVfs.Kill(); }
            catch { }

            return rom;
        }

        #endregion

        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private bool _isUsingCxBxLoader;
        private bool _chihiro = false;
        private bool _chihiroDS = false;
        private bool _demulshooter = false;
        private string _romName;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = null;

            if ((core != null && core == "chihiro") || (emulator != null && emulator == "chihiro"))
            {
                path = AppConfig.GetFullPath("chihiro");
                _chihiro = true;
            }
            else if ((core != null && core == "chihiro-gun") || (emulator != null && emulator == "chihiro-gun"))
            {
                path = AppConfig.GetFullPath("chihiro-ds");
                _chihiro = true;
                _chihiroDS = true;
            }

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-reloaded");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-r");

            if (string.IsNullOrEmpty(path))
                return null;

            _isUsingCxBxLoader = true;

            string exe = Path.Combine(path, "cxbxr-ldr.exe");

            if (!File.Exists(exe))
            {
                _isUsingCxBxLoader = false;
                exe = Path.Combine(path, "cxbx.exe");
            }
            
            if (!File.Exists(exe))
                return null;

            rom = MountIso(rom);

            _resolution = resolution;
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            
            // If rom is a directory
            if (Directory.Exists(rom))
            {
                var xbeFiles = Directory.GetFiles(rom, "*.xbe");

                var xbe = xbeFiles.FirstOrDefault(r => Path.GetFileNameWithoutExtension(r).ToLowerInvariant() == "default");
                if (string.IsNullOrEmpty(xbe))
                    xbe = xbeFiles.FirstOrDefault();

                if (string.IsNullOrEmpty(xbe))
                    throw new ApplicationException("Unable to find XBE file");

                rom = xbe;
            }

            _romName = Path.GetFileNameWithoutExtension(rom);

            //Configuration of .ini file
            using (var ini = IniFile.FromFile(Path.Combine(path, "settings.ini"), IniOptions.KeepEmptyValues | IniOptions.AllowDuplicateValues | IniOptions.UseSpaces))
            {
                var guns = RawLightgun.GetRawLightguns();
                var res = resolution ?? ScreenResolution.CurrentResolution;

                //Fulscreen Management
                if (_isUsingCxBxLoader && !_chihiro)
                    ini.WriteValue("video", "FullScreen", "false");
                else
                {
                    string videoResolution = res.Width + " x " + res.Height + " 32bit x8r8g8b8 (" + (res.DisplayFrequency <= 0 ? 60 : res.DisplayFrequency).ToString() + " hz)";
                    ini.WriteValue("video", "VideoResolution", videoResolution);
                    ini.WriteValue("video", "FullScreen", fullscreen ? "true" : "false");
                }

                //Vsync
                BindBoolIniFeatureOn(ini, "video", "VSync", "VSync", "true", "false");

                //Aspect ratio : keep original or stretch
                if (Features.IsSupported("ratio") && SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "stretch")
                {
                    ini.WriteValue("video", "MaintainAspect", "false");

                    if (!SystemConfig.getOptBoolean("use_guns") || !guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
                            _bezelFileInfo = null;
                }
                else
                    ini.WriteValue("video", "MaintainAspect", "true");

                //Internal resolution
                BindIniFeature(ini, "video", "RenderResolution", "internalresolution", "1");

                //XBE signature
                BindBoolIniFeature(ini, "gui", "IgnoreInvalidXbeSig", "xbeSignature", "true", "false");

                //hacks
                BindBoolIniFeature(ini, "hack", "DisablePixelShaders", "disablePixelShaders", "true", "false");
                BindBoolIniFeature(ini, "hack", "UseAllCores", "useallcores", "true", "false");
                BindBoolIniFeature(ini, "hack", "SkipRdtscPatching", "rdtscPatching", "true", "false");

                ConfigureControllers(ini);
            }

            // If DemulShooter is enabled, configure it
            if (_chihiro && SystemConfig.getOptBoolean("use_demulshooter") && _demulshooter != true)
            {
                var guns = RawLightgun.GetRawLightguns();
                _demulshooter = true;
                SimpleLogger.Instance.Info("[INFO] Configuring DemulShooter");
                var gun1 = guns.Length > 0 ? guns[0] : null;
                var gun2 = guns.Length > 1 ? guns[1] : null;

                if (_chihiroDS)
                    Demulshooter.StartDemulshooter("chihiro-ds", "chihiro", _romName, gun1, gun2);
                else
                    Demulshooter.StartDemulshooter("chihiro", "chihiro", _romName, gun1, gun2);
            }

            if (system == "xbox")
            {
                string eeprom = Path.Combine(AppConfig.GetFullPath("cxbx-reloaded"), "EEPROM.bin");
                if (!File.Exists(eeprom))
                    File.WriteAllBytes(eeprom, Properties.Resources.eeprom);

                WriteXboxEEPROM(eeprom);
            }
            

            if (_isUsingCxBxLoader)
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    WindowStyle = ProcessWindowStyle.Maximized,
                    Arguments = "/load \"" + rom + "\"",
                };
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }

        /// <summary>
        /// Get XBOX language to write to eeprom, value from features or default language of ES.
        /// </summary>
        private int GetXboxLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, int>()
            {
                { "en", 1 },
                { "jp", 2 },
                { "ja", 2 },
                { "de", 3 },
                { "fr", 4 },
                { "es", 5 },
                { "it", 6 },
                { "ko", 7 },
                { "zh", 8 },
                { "pt", 9 }
            };

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out int ret))
                    return ret;
            }

            return 1;
        }

        /// <summary>
        /// Write data to XboX eeprom (language).
        /// </summary>
        /// <param name="path"></param>
        private void WriteXboxEEPROM(string path)
        {
            if (!File.Exists(path))
                return;

            int langId;

            if (SystemConfig.isOptSet("xbox_language") && !string.IsNullOrEmpty(SystemConfig["xbox_language"]))
                langId = SystemConfig["xbox_language"].ToInteger();
            else
                langId = GetXboxLangFromEnvironment();

            // Read eeprom file
            byte[] bytes = File.ReadAllBytes(path);

            var toSet = new byte[] { (byte)langId };
            for (int i = 0; i < toSet.Length; i++)
                bytes[144] = toSet[i];

            if (SystemConfig.isOptSet("xbox_videostandard") && !string.IsNullOrEmpty(SystemConfig["xbox_videostandard"]))
            {
                VideoStandard vs = (VideoStandard)Enum.Parse(typeof(VideoStandard), SystemConfig["xbox_videostandard"]);
                byte[] vsBytes = BitConverter.GetBytes((uint)vs);
                Array.Copy(vsBytes, 0, bytes, 0x58, 4);
            }

            uint UserSectionChecksum = ~ChecksumCalculate(bytes, 0x64, 0x5C);

            uint userSum = ~ChecksumCalculate(bytes, 0x64, 0x5C);
            Array.Copy(BitConverter.GetBytes(userSum), 0, bytes, 0x60, 4);

            uint factorySum = ~ChecksumCalculate(bytes, 0x34, 0x2C);
            Array.Copy(BitConverter.GetBytes(factorySum), 0, bytes, 0x30, 4);

            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Calculates the EEPROM data checksum of specified offset and size.
        /// Original code by Ernegien (https://github.com/Ernegien/XboxEepromEditor)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        private static uint ChecksumCalculate(byte[] data, int offset, int size)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (size % sizeof(uint) > 0)
                throw new ArgumentException("Size must be a multiple of four.", "size");

            if (offset + size > data.Length)
                throw new ArgumentOutOfRangeException();

            // high and low parts of the internal checksum
            uint high = 0, low = 0;

            for (int i = 0; i < size / sizeof(uint); i++)
            {
                uint val = BitConverter.ToUInt32(data, offset + i * sizeof(uint));
                ulong sum = ((ulong)high << 32) | low;

                high = (uint)((sum + val) >> 32);
                low += val;
            }

            return high + low;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (!_isUsingCxBxLoader)
                return base.RunAndWait(path);

            FakeBezelFrm bezel = null;

            var process = Process.Start(path);

            if (_chihiroDS)
            {
                process.WaitForExit();

                while (true)
                {
                    var runningProcesses = Process.GetProcessesByName("cxbxr-ldr");
                    if (runningProcesses.Length == 0)
                    {
                        System.Threading.Thread.Sleep(1000);
                        runningProcesses = Process.GetProcessesByName("cxbxr-ldr");
                        if (runningProcesses.Length == 0)
                        {
                            break;
                        }
                    }

                    foreach (var proc in runningProcesses)
                    {
                        try
                        {
                            var hWnd = User32.FindHwnd(process.Id);
                            if (hWnd == IntPtr.Zero)
                                continue;

                            var name = User32.GetClassName(hWnd);
                            if (name != null && name.Contains("CxbxRender"))
                            {
                                var style = User32.GetWindowStyle(hWnd);
                                if (style.HasFlag(WS.CAPTION))
                                {
                                    int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                                    int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                                    style &= ~WS.CAPTION;
                                    style &= ~WS.THICKFRAME;
                                    style &= ~WS.MAXIMIZEBOX;
                                    style &= ~WS.MINIMIZEBOX;
                                    style &= ~WS.OVERLAPPED;
                                    style &= ~WS.SYSMENU;
                                    User32.SetWindowStyle(hWnd, style);

                                    User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);

                                    if (_bezelFileInfo != null)
                                        bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
                                }

                                break;
                            }
                        }
                        catch { }

                        proc?.WaitForExit();
                    }
                    System.Threading.Thread.Sleep(500);

                    bezel?.Dispose();
                }

                return 0;
            }

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;

                var name = User32.GetClassName(hWnd);
                if (name != null && name.Contains("CxbxRender"))
                {
                    var style = User32.GetWindowStyle(hWnd);
                    if (style.HasFlag(WS.CAPTION))
                    {
                        int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                        int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                        style &= ~WS.CAPTION;
                        style &= ~WS.THICKFRAME;
                        style &= ~WS.MAXIMIZEBOX;
                        style &= ~WS.MINIMIZEBOX;
                        style &= ~WS.OVERLAPPED;
                        style &= ~WS.SYSMENU;
                        User32.SetWindowStyle(hWnd, style);

                        User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);

                        if (_bezelFileInfo != null)
                            bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
                    }

                    break;
                }
            }

            process?.WaitForExit();

            bezel?.Dispose();

            if (process != null)
            {
                try { return process.ExitCode; }
                catch { }
            }

            return -1;
        }

        public override void Cleanup()
        {
            if (_demulshooter)
                Demulshooter.KillDemulShooter();

            if (_sindenSoft)
                Guns.KillSindenSoftware();

            base.Cleanup();
        }
    }

    public enum VideoStandard
    {
        Unknown = 0,
        NtscM = 0x00400100,
        NtscJ = 0x00400200,
        PalI = 0x00800300,
        PalM = 0x00400400
    }
}
