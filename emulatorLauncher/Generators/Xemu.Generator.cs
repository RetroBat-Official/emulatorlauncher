using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class XEmuGenerator : Generator
    {
        public XEmuGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        protected override bool IsScreenPlacementWindow(IntPtr hWnd)
        {
            // SDL default window class : excludes message boxes (#32770) and other helper windows
            return User32.GetClassName(hWnd) == "SDL_app" && base.IsScreenPlacementWindow(hWnd);
        }

        private SdlVersion _sdlVersion = SdlVersion.SDL2_0_X;
        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private Rectangle _windowRect = Rectangle.Empty;
        private bool _chihiro;
        private string _chihiroEeprom;
        private string _chihiroEepromSave;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
                return null;

            _chihiro = (emulator == "xemu-chihiro" || system == "chihiro");

            string exe = Path.Combine(path, "xemu.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "xemuw.exe");
            
            if (!File.Exists(exe))
                return null;

            bool fullscreen = ShouldRunFullscreen();

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            // Extract SDL2 version info
            SimpleLogger.Instance.Info("[Generator] Getting SDL version from: " + exe);
            _sdlVersion = SdlJoystickGuidManager.GetSdlVersionFromStaticBinary(exe, SdlVersion.SDL2_0_X);

            try
            {
                // Define Paths
                string eepromPath = null;
                string hddPath = null;
                string bootRom = null;

                if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig.GetFullPath("saves")))
                {
                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }

                    if (!_chihiro)
                    {
                        // Copy eeprom file from resources if file does not exist yet
                        if (!File.Exists(Path.Combine(savePath, "eeprom.bin")))
                        {
                            SimpleLogger.Instance.Info("[Generator] eeprom.bin not found, copying from template.");
                            File.WriteAllBytes(Path.Combine(savePath, "eeprom.bin"), Properties.Resources.eeprom);
                        }

                        // Unzip and Copy hdd image file from resources if file does not exist yet
                        if (!File.Exists(Path.Combine(savePath, "xbox_hdd.qcow2")))
                        {
                            SimpleLogger.Instance.Info("[Generator] xbox_hdd.qcow2 not found, copying from template.");
                            string zipFile = Path.Combine(savePath, "xbox_hdd.qcow2.zip");
                            File.WriteAllBytes(zipFile, Properties.Resources.xbox_hdd_qcow2);

                            Zip.Extract(zipFile, savePath);
                            File.Delete(zipFile);
                        }

                        if (File.Exists(Path.Combine(savePath, "eeprom.bin")))
                            eepromPath = Path.Combine(savePath, "eeprom.bin");

                        if (File.Exists(Path.Combine(savePath, "xbox_hdd.qcow2")))
                            hddPath = Path.Combine(savePath, "xbox_hdd.qcow2");
                    }
                }

                if (!_chihiro && !string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig.GetFullPath("bios")))
                {
                    if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "mcpx_1.0.bin")))
                        bootRom = Path.Combine(AppConfig.GetFullPath("bios"), "mcpx_1.0.bin");
                }

                // Settings
                SetupTOMLConfiguration(path, system, eepromPath, hddPath, bootRom);
            }
            catch { }

            if (_chihiro)
                SetupChihiroEeprom(path);

            // Command line arguments
            List<string> commandArray = new List<string>();

            if (IsEmulationStationWindowed(out Rectangle emulationStationBounds, true) && !SystemConfig.getOptBoolean("forcefullscreen"))
            {
                _windowRect = emulationStationBounds;
                _bezelFileInfo = null;
            }
            else if (fullscreen)
                commandArray.Add("-full-screen");

            commandArray.Add("-dvd_path");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            // Disable bezel if is widescreen
            if (!SystemConfig.isOptSet("bezel") && SystemConfig["scale"] == "stretch")
            {
                SystemConfig["forceNoBezel"] = "1";
                ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator);
            }

            // Launch emulator
            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        /// <summary>
        /// Configure emulator, write to .toml file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="eepromPath"></param>
        /// <param name="hddPath"></param>
        /// <param name="bootRom"></param>
        private void SetupTOMLConfiguration(string path, string system, string eepromPath, string hddPath, string bootRom)
        {
            using (IniTomlFile ini = new IniTomlFile(Path.Combine(path, "xemu.toml"), IniTomlOptions.KeepEmptyLines | IniTomlOptions.UseSpaces))
            {
                SimpleLogger.Instance.Info("[Generator] Writing settings to 'xemu.toml' file.");

                // Force settings
                ini.WriteValue("general", "show_welcome", "false");
                ini.WriteValue("general.updates", "check", "false");

                // Skip Boot anim
                BindBoolIniFeatureAuto(ini, "general", "skip_boot_anim", "show_boot", "true", "false", "true");
                BindBoolIniFeature(ini, "display.ui", "show_notifications", "xemu_notifications", "true", "false");
                BindBoolIniFeatureOn(ini, "perf", "cache_shaders", "xemu_cache_shaders", "true", "false");

                // Controllers
                if (!SystemConfig.getOptBoolean("disableautocontrollers"))
                {
                    for (int i = 0; i < 16; i++)
                        ini.Remove("input.bindings", "port" + i);

                    var inputArray = new List<object>();

                    int port = 1;

                    foreach (var ctl in Controllers.Where(c => !c.IsKeyboard && c.Config != null))
                    {
                        if (port > 4)
                            break;

                        string guid = ctl.GetSdlGuid(_sdlVersion, true).ToLowerInvariant();
                        string identity = GetXemuControllerIdentity(ctl);

                        ini.WriteValue("input.bindings", "port" + port, "'" + identity + "'");
                        if (port == 1)
                            _chihiroPad = ctl;

                        // The mapping table is keyed by model, never by port: always the
                        // bare GUID here, even when the port binding carries a path.
                        inputArray.Add(new Dictionary<string, string> { { "gamepad_id", guid } });

                        SimpleLogger.Instance.Info("[Generator] xemu port " + port + " = " + ctl.ToShortString() + " -> " + identity);
                        port++;
                    }

                    var keyboard = Controllers.FirstOrDefault(c => c.IsKeyboard);
                    if (keyboard != null && port <= 4)
                    {
                        ini.WriteValue("input.bindings", "port" + port, "'keyboard'");
                        inputArray.Add(new Dictionary<string, string> { { "gamepad_id", "keyboard" } });
                    }

                    ini.SetArray("input", "gamepad_mappings", inputArray);
                }

                // Renderer
                if (SystemConfig.isOptSet("xemu_renderer") && !string.IsNullOrEmpty(SystemConfig["xemu_renderer"]))
                    ini.WriteValue("display", "renderer", "'" + SystemConfig["xemu_renderer"] + "'");
                else if (Features.IsSupported("xemu_renderer"))
                    ini.Remove("display", "renderer");

                // Resolution
                BindIniFeature(ini, "display.quality", "surface_scale", "render_scale", "1");

                // Aspect Ratio and scaling
                if (SystemConfig.isOptSet("xemu_scale") && !string.IsNullOrEmpty(SystemConfig["xemu_scale"]))
                    ini.WriteValue("display.ui", "fit", "'" + SystemConfig["xemu_scale"] + "'");                
                else if (Features.IsSupported("xemu_scale"))
                    ini.WriteValue("display.ui", "fit", "'scale'");

                if (SystemConfig.isOptSet("xemu_ratio") && !string.IsNullOrEmpty(SystemConfig["xemu_ratio"]))
                    ini.WriteValue("display.ui", "aspect_ratio", "'" + SystemConfig["xemu_ratio"] + "'");
                else
                    ini.Remove("display.ui", "aspect_ratio");

                // Menu Bar
                if (SystemConfig.isOptSet("menubar") && SystemConfig.getOptBoolean("menubar"))
                    ini.WriteValue("display.ui", "show_menubar", "true");
                else if (Features.IsSupported("menubar"))
                    ini.WriteValue("display.ui", "show_menubar", "false");


                // sys options
                if (!_chihiro)
                {
                    if (SystemConfig.isOptSet("system_memory") && !string.IsNullOrEmpty(SystemConfig["system_memory"]))
                        ini.WriteValue("sys", "mem_limit", "'" + SystemConfig["system_memory"] + "'");
                    else
                        ini.WriteValue("sys", "mem_limit", "'128'");
                }

                if (SystemConfig.isOptSet("xemu_avpack") && !string.IsNullOrEmpty(SystemConfig["xemu_avpack"]))
                    ini.WriteValue("sys", "avpack", "'" + SystemConfig["xemu_avpack"] + "'");
                else
                    ini.WriteValue("sys", "avpack", "'HDTV'");

                // Vsync
                BindBoolIniFeatureOn(ini, "display.window", "vsync", "vsync", "true", "false");

                //¨Paths
                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "xemu");
                if (Directory.Exists(screenshotPath))
                    ini.WriteValue("general", "screenshot_dir", "'" + screenshotPath + "'");

                if (!string.IsNullOrEmpty(eepromPath))
                    ini.WriteValue("sys.files", "eeprom_path", "'" + eepromPath + "'");

                if (!string.IsNullOrEmpty(hddPath))
                    ini.WriteValue("sys.files", "hdd_path", "'" + hddPath + "'");

                string flashromPath = Path.Combine(AppConfig.GetFullPath("bios"));
                if (!_chihiro)
                {
                    if (SystemConfig.isOptSet("xemu_flashrom") && !string.IsNullOrEmpty(SystemConfig["xemu_flashrom"]))
                        ini.WriteValue("sys.files", "flashrom_path", "'" + Path.Combine(flashromPath, SystemConfig["xemu_flashrom"]) + "'");
                    else
                        ini.WriteValue("sys.files", "flashrom_path", "'" + Path.Combine(flashromPath, "Complex_4627.bin") + "'");
                }

                // CHIHIRO: the fork reads its own BIOS from chihiro.roms.bios_path
                if (_chihiro)
                    SetupChihiroConfiguration(ini);

                if (!string.IsNullOrEmpty(bootRom))
                    ini.WriteValue("sys.files", "bootrom_path", "'" + bootRom + "'");

                //audio
                BindBoolIniFeature(ini, "audio", "use_dsp", "xemu_dsp", "true", "false");

                ini.Save();
            }

            // Write xbox bios settings in eeprom.bin file
            WriteXboxEEPROM(eepromPath);
        }

        /// <summary>
        /// Get XBOX language to write to eeprom, value from features or default language of ES.
        /// </summary>
        private int GetXboxLangFromEnvironment()
        {
            SimpleLogger.Instance.Info("[Generator] Getting Language from RetroBat language.");

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

            SimpleLogger.Instance.Info("[Generator] Writing language in XBOX eeprom.");

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

            uint UserSectionChecksum = ~ChecksumCalculate(bytes, 0x64, 0x5C);

            byte[] userchecksum = BitConverter.GetBytes(UserSectionChecksum);
            for (int i = 0; i < userchecksum.Length; i++)
                bytes[96 + i] = userchecksum[i];

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

        /// <summary>
        /// Chihiro settings (Tovarichtch fork). The fork identifies the cabinet
        /// from the game itself - card reader, drive board, monitor type and
        /// input profile - so no game profile is forced here.
        /// </summary>
        private void SetupChihiroConfiguration(IniTomlFile ini)
        {
            string biosPath = AppConfig.GetFullPath("bios");

            string chihiroBios = Path.Combine(biosPath, "chihiro", "chihiro_xbox_bios.bin");
            if (!File.Exists(chihiroBios))
                chihiroBios = Path.Combine(biosPath, "chihiro_xbox_bios.bin");

            if (File.Exists(chihiroBios))
            {
                ini.WriteValue("chihiro.roms", "bios_path", "'" + chihiroBios + "'");

                ini.Remove("chihiro.roms", "mediaboard_path");
                ini.Remove("chihiro.roms", "ic10_path");
                ini.Remove("chihiro.roms", "ic11_path");
                ini.Remove("chihiro.roms", "pc20_path");

                string biosDir = Path.GetDirectoryName(chihiroBios);

                foreach (var f in new[] { "ic10_g24lc64.bin", "ic11_24lc024.bin", "pc20_g24lc64.bin" })
                {
                    if (!File.Exists(Path.Combine(biosDir, f)))
                        SimpleLogger.Instance.Warning("[Generator] Chihiro file missing next to the BIOS: " + f);
                }

                if (!File.Exists(Path.Combine(biosDir, "fpr21042_m29w160et.bin")) &&
                    !File.Exists(Path.Combine(biosDir, "fpr-23887_29lv160te.ic4")) &&
                    !File.Exists(Path.Combine(biosDir, "fpr-23887.bin")))
                    SimpleLogger.Instance.Warning("[Generator] Chihiro media board flash missing next to the BIOS.");
            }
            else
                SimpleLogger.Instance.Warning("[Generator] chihiro_xbox_bios.bin not found, Chihiro will not boot.");

            string savePath = Path.Combine(AppConfig.GetFullPath("saves"), "chihiro");
            if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); } catch { }
            if (Directory.Exists(savePath))
                ini.WriteValue("chihiro.roms", "snapshot_store_path", "'" + Path.Combine(savePath, "chihiro_snapshots.qcow2") + "'");

            // Cabinet settings. Enum values are quoted strings in the toml.
            BindBoolIniFeature(ini, "chihiro.settings", "freeplay", "chihiro_freeplay", "true", "false");
            BindBoolIniFeature(ini, "chihiro.settings", "lightgun_mode", "chihiro_lightgun_mode", "true", "false");

            if (SystemConfig.isOptSet("chihiro_region") && !string.IsNullOrEmpty(SystemConfig["chihiro_region"]))
                ini.WriteValue("chihiro.settings", "region", "'" + SystemConfig["chihiro_region"] + "'");
            else
                ini.WriteValue("chihiro.settings", "region", "'ex'");

            if (SystemConfig.isOptSet("chihiro_dimm_size") && !string.IsNullOrEmpty(SystemConfig["chihiro_dimm_size"]))
                ini.WriteValue("chihiro.settings", "dimm_size", "'" + SystemConfig["chihiro_dimm_size"] + "'");
            else
                ini.WriteValue("chihiro.settings", "dimm_size", "'auto'");

            ini.WriteValue("chihiro.settings", "board_type", "'auto'");

            // Drive board force feedback (OutRun 2, Maximum Tune)
            BindBoolIniFeature(ini, "chihiro.settings", "force_feedback", "chihiro_ffb", "true", "false");
            BindIniFeature(ini, "chihiro.settings", "ffb_strength", "chihiro_ffb_strength", "100");
            BindBoolIniFeature(ini, "chihiro.settings", "ffb_invert", "chihiro_ffb_invert", "true", "false");
            BindIniFeature(ini, "chihiro.settings", "wheel_rotation", "chihiro_wheel_rotation", "270");

            BindBoolIniFeatureOn(ini, "chihiro.settings", "wheel_autocenter", "chihiro_wheel_autocenter", "true", "false");
            BindIniFeature(ini, "chihiro.settings", "wheel_autocenter_strength", "chihiro_wheel_autocenter_strength", "50");

            ConfigureChihiroControllers(ini);
            ConfigureChihiroGuns(ini);
        }

        private void SetupChihiroEeprom(string path)
        {
            if (string.IsNullOrEmpty(AppConfig["saves"]) || !Directory.Exists(AppConfig.GetFullPath("saves")))
                return;

            string savePath = Path.Combine(AppConfig.GetFullPath("saves"), "chihiro");
            if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); } catch { }
            if (!Directory.Exists(savePath))
                return;

            _chihiroEeprom = Path.Combine(path, "chihiro_eeprom.bin");
            _chihiroEepromSave = Path.Combine(savePath, "chihiro_eeprom.bin");

            try
            {
                if (File.Exists(_chihiroEepromSave))
                {
                    if (!File.Exists(_chihiroEeprom) ||
                        File.GetLastWriteTimeUtc(_chihiroEepromSave) >= File.GetLastWriteTimeUtc(_chihiroEeprom))
                        File.Copy(_chihiroEepromSave, _chihiroEeprom, true);
                }
                else if (File.Exists(_chihiroEeprom))
                {
                    File.Copy(_chihiroEeprom, _chihiroEepromSave, true);
                }
            }
            catch { SimpleLogger.Instance.Warning("[Generator] Unable to restore chihiro_eeprom.bin."); }
        }

        private void SaveChihiroEeprom()
        {
            if (string.IsNullOrEmpty(_chihiroEeprom) || string.IsNullOrEmpty(_chihiroEepromSave))
                return;

            try
            {
                if (File.Exists(_chihiroEeprom))
                    File.Copy(_chihiroEeprom, _chihiroEepromSave, true);
            }
            catch { SimpleLogger.Instance.Warning("[Generator] Unable to save chihiro_eeprom.bin."); }
        }

        private string GetXemuControllerIdentity(Controller ctl)
        {
            string guid = ctl.GetSdlGuid(_sdlVersion, true).ToLowerInvariant();

            bool hasTwin = Controllers.Any(c => c != ctl && !c.IsKeyboard && c.Config != null &&
                                                c.GetSdlGuid(_sdlVersion, true).ToLowerInvariant() == guid);

            if (hasTwin && !string.IsNullOrEmpty(ctl.DevicePath))
                return guid + "#" + ctl.DevicePath;

            return guid;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = 0;

            if (_windowRect.IsEmpty)
                ret = base.RunAndWait(path);
            else
            {
                var process = Process.Start(path);
                Job.Current.AddProcess(process);

                while (process != null)
                {
                    try
                    {
                        var hWnd = process.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            User32.SetWindowPos(hWnd, IntPtr.Zero, _windowRect.Left, _windowRect.Top, _windowRect.Width, _windowRect.Height, SWP.NOZORDER);
                            break;
                        }
                    }
                    catch { }

                    if (process.WaitForExit(1))
                    {
                        try { ret = process.ExitCode; }
                        catch { }
                        process = null;
                        break;
                    }

                }

                if (process != null)
                {
                    process.WaitForExit();
                    try { ret = process.ExitCode; }
                    catch { }
                }
            }

            if (_chihiro)
                SaveChihiroEeprom();

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
        }

        public static readonly Dictionary<string, int> xemuScancodes = new Dictionary<string, int>
        {
            // --- Letters ---
            { "A", 4 }, { "B", 5 }, { "C", 6 }, { "D", 7 }, { "E", 8 }, { "F", 9 },
            { "G", 10 }, { "H", 11 }, { "I", 12 }, { "J", 13 }, { "K", 14 }, { "L", 15 },
            { "M", 16 }, { "N", 17 }, { "O", 18 }, { "P", 19 }, { "Q", 20 }, { "R", 21 },
            { "S", 22 }, { "T", 23 }, { "U", 24 }, { "V", 25 }, { "W", 26 }, { "X", 27 },
            { "Y", 28 }, { "Z", 29 },

            // --- Numbers (Top Row) ---
            { "1", 30 }, { "2", 31 }, { "3", 32 }, { "4", 33 }, { "5", 34 },
            { "6", 35 }, { "7", 36 }, { "8", 37 }, { "9", 38 }, { "0", 39 },

            // --- Control Keys ---
            { "Enter", 40 },
            { "Escape", 41 },
            { "Backspace", 42 },
            { "Tab", 43 },
            { "Space", 44 },
            { "Minus", 45 },
            { "Equals", 46 },
            { "LeftBracket", 47 },
            { "RightBracket", 48 },
            { "Backslash", 49 },
            { "Semicolon", 51 },
            { "Apostrophe", 52 },
            { "Grave", 53 },
            { "Comma", 54 },
            { "Period", 55 },
            { "Slash", 56 },
            { "CapsLock", 57 },

            // --- Function Keys ---
            { "F1", 58 }, { "F2", 59 }, { "F3", 60 }, { "F4", 61 }, { "F5", 62 }, { "F6", 63 },
            { "F7", 64 }, { "F8", 65 }, { "F9", 66 }, { "F10", 67 }, { "F11", 68 }, { "F12", 69 },

            // --- System & Navigation Keys ---
            { "PrintScreen", 70 },
            { "ScrollLock", 71 },
            { "Pause", 72 },
            { "Insert", 73 },
            { "Home", 74 },
            { "PageUp", 75 },
            { "Delete", 76 },
            { "End", 77 },
            { "PageDown", 78 },

            // --- Arrow Keys ---
            { "ArrowRight", 79 },
            { "ArrowLeft", 80 },
            { "ArrowDown", 81 },
            { "ArrowUp", 82 },

            // --- Keypad ---
            { "NumLock", 83 },
            { "KeypadDivide", 84 },
            { "KeypadMultiply", 85 },
            { "KeypadMinus", 86 },
            { "KeypadPlus", 87 },
            { "KeypadEnter", 88 },
            { "Keypad1", 89 }, { "Keypad2", 90 }, { "Keypad3", 91 },
            { "Keypad4", 92 }, { "Keypad5", 93 }, { "Keypad6", 94 },
            { "Keypad7", 95 }, { "Keypad8", 96 }, { "Keypad9", 97 },
            { "Keypad0", 98 },
            { "KeypadPeriod", 99 },

            // --- Modifiers ---
            { "LeftCtrl", 224 },
            { "LeftShift", 225 },
            { "LeftAlt", 226 },
            { "LeftWindows", 227 },
            { "RightCtrl", 228 },
            { "RightShift", 229 },
            { "RightAlt", 230 },
            { "RightWindows", 231 },

            // --- Virtual mouse ---
            { "MouseLeft", 1001 },
            { "MouseMiddle", 1002 },
            { "MouseRight", 1003 },
            { "MouseX1", 1004 },
            { "MouseX2", 1005 }
        };
    }
}
