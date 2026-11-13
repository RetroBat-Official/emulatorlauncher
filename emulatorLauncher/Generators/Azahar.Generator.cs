using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
{
    partial class AzaharGenerator : Generator
    {
        public AzaharGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }
        private SdlVersion _sdlVersion = SdlVersion.SDL2_30;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private SaveStatesWatcher _saveStatesWatcher;
        private bool _loadState;
        private string _quickLoadShortcut = "F4";

        // Delay between the game window and the Quick Load shortcut, when a save state is launched from ES
        private const int QuickLoadDelay = 2000;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "azahar.exe"))
                return null;

            // Ensure user folder exists
            string userFolder = Path.Combine(path, "user");
            if (!Directory.Exists(userFolder)) try { Directory.CreateDirectory(userFolder); }
                catch { }

            bool fullscreen = ShouldRunFullscreen();

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            string sdl2 = Path.Combine(path, "SDL2.dll");
            if (File.Exists(sdl2))
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);

            // ES rom path, used to name the save states
            string esRom = rom;

            string[] extensions = new string[] { ".3ds", ".3dsx", ".elf", ".axf", ".cci", ".cxi", ".app", ".z3dsx", ".zcia", ".zcxi", ".zcci" };
            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip" || Path.GetExtension(rom).ToLowerInvariant() == ".7z" || Path.GetExtension(rom).ToLowerInvariant() == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            SetupConfigurationAzahar(path, rom, fullscreen);
            SetupSaveStates(system, emulator, core, path, esRom);

            if (fullscreen)
            {
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                _resolution = resolution;
                if (_bezelFileInfo != null && _bezelFileInfo.PngFile != null)
                    SimpleLogger.Instance.Info("[INFO] Bezel file selected : " + _bezelFileInfo.PngFile);
            }

            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
                rom = File.ReadAllText(rom);

            List<string> commandArray = new List<string>();
            if (fullscreen)
                commandArray.Add("-f");
            
            //commandArray.Add("-g");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfigurationAzahar(string path, string rom, bool fullscreen = true)
        {
            string userconfigPath = Path.Combine(path, "user", "config");
            if (!Directory.Exists(userconfigPath))
                try { Directory.CreateDirectory(userconfigPath); } catch {}

            string conf = Path.Combine(userconfigPath, "qt-config.ini");
            using (var ini = new IniFile(conf))
            {
                SimpleLogger.Instance.Info("[INFO] Writing Azahar configuration file: " + conf);

                ini.WriteValue("UI", "pauseWhenInBackground\\default", SystemConfig.getOptBoolean("nopauseonlostfocus") ? "true" : "false");
                ini.WriteValue("UI", "pauseWhenInBackground", SystemConfig.getOptBoolean("nopauseonlostfocus") ? "false" : "true");
                ini.WriteValue("UI", "saveStateWarning\\default", "false");
                ini.WriteValue("UI", "saveStateWarning", "false");

                // Define rom path
                string romPath = Path.GetDirectoryName(rom);

                if (!string.IsNullOrEmpty(romPath))
                {
                    ini.WriteValue("UI", "Paths\\gamedirs\\3\\path", romPath.Replace("\\", "/"));
                    ini.WriteValue("UI", "Paths\\gamedirs\\3\\deep_scan\\default", "false");
                    ini.WriteValue("UI", "Paths\\gamedirs\\3\\deep_scan", "true");
                }
                
                int gameDirsSize = ini.GetValue("UI", "Paths\\gamedirs\\size").ToInteger();
                if (gameDirsSize < 3)
                    ini.WriteValue("UI", "Paths\\gamedirs\\size", "3");
                
                // Write settings
                ini.WriteValue("Miscellaneous", "check_for_update_on_start\\default", "false");
                ini.WriteValue("Miscellaneous", "check_for_update_on_start", "false");

                if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                {
                    ini.WriteValue("UI", "enable_discord_presence\\default", "true");
                    ini.WriteValue("UI", "enable_discord_presence", "true");
                }
                else
                {
                    ini.WriteValue("UI", "enable_discord_presence\\default", "false");
                    ini.WriteValue("UI", "enable_discord_presence", "false");
                }

                ini.WriteValue("Data%20Storage", "use_custom_storage\\default", "false");
                ini.WriteValue("Data%20Storage", "use_custom_storage", "true");

                string emuNandPath = Path.Combine(AppConfig.GetFullPath("saves"), "3ds", "azahar", "nand");
                if (!Directory.Exists(emuNandPath)) try { Directory.CreateDirectory(emuNandPath); }
                    catch { }
                ini.WriteValue("Data%20Storage", "nand_directory\\default", "false");
                ini.WriteValue("Data%20Storage", "nand_directory", emuNandPath.Replace("\\", "/"));

                // Write nand settings (language)
                string nandPath = Path.Combine(emuNandPath, "data", "00000000000000000000000000000000", "sysdata", "00010017", "00000000", "config");
                if (File.Exists(nandPath))
                    Write3DSnand(nandPath);

                string sdmcPath = Path.Combine(AppConfig.GetFullPath("saves"), "3ds", "azahar", "sdmc");
                if (!Directory.Exists(sdmcPath)) try { Directory.CreateDirectory(sdmcPath); }
                    catch { }
                ini.WriteValue("Data%20Storage", "sdmc_directory\\default", "false");
                ini.WriteValue("Data%20Storage", "sdmc_directory", sdmcPath.Replace("\\", "/"));

                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "azahar");
                if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                    catch { }
                ini.WriteValue("UI", "Paths\\screenshotPath\\default", "false");
                ini.WriteValue("UI", "Paths\\screenshotPath", screenshotPath.Replace("\\", "/"));

                ini.WriteValue("UI", "fullscreen\\default", fullscreen ? "false" : "true");
                ini.WriteValue("UI", "fullscreen", fullscreen ? "true" : "false");

                ini.WriteValue("UI", "confirmClose\\default", "false");
                ini.WriteValue("UI", "confirmClose", "false");

                ini.WriteValue("UI", "firstStart\\default", "false");
                ini.WriteValue("UI", "firstStart", "false");

                ini.WriteValue("UI", "calloutFlags\\default", "false");
                ini.WriteValue("UI", "calloutFlags", "1");

                CreateControllerConfiguration(ini);

                if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                {
                    ini.WriteValue("Layout", "filter_mode\\default", "true");
                    ini.WriteValue("Layout", "filter_mode", "true");
                }
                else
                {
                    ini.WriteValue("Layout", "filter_mode\\default", "false");
                    ini.WriteValue("Layout", "filter_mode", "false");
                }

                if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                {
                    ini.WriteValue("Renderer", "use_vsync\\default", "false");
                    ini.WriteValue("Renderer", "use_vsync", "false");
                }
                else
                {
                    ini.WriteValue("Renderer", "use_vsync\\default", "true");
                    ini.WriteValue("Renderer", "use_vsync", "true");
                }

                if (SystemConfig.isOptSet("azahar_async_shader_compilation") && !SystemConfig.getOptBoolean("azahar_async_shader_compilation"))
                {
                    ini.WriteValue("Renderer", "async_shader_compilation\\default", "true");
                    ini.WriteValue("Renderer", "async_shader_compilation", "false");
                }
                else
                {
                    ini.WriteValue("Renderer", "async_shader_compilation\\default", "false");
                    ini.WriteValue("Renderer", "async_shader_compilation", "true");
                }

                if (Features.IsSupported("azahar_resolution_factor"))
                {
                    if (SystemConfig.isOptSet("azahar_resolution_factor"))
                    {
                        ini.WriteValue("Renderer", "resolution_factor\\default", SystemConfig["azahar_resolution_factor"] == "1" ? "true" : "false");
                        ini.WriteValue("Renderer", "resolution_factor", SystemConfig["azahar_resolution_factor"]);
                    }
                    else
                    {
                        ini.WriteValue("Renderer", "resolution_factor\\default", "true");
                        ini.WriteValue("Renderer", "resolution_factor", "1");
                    }
                }

                if (Features.IsSupported("azahar_texture_filter"))
                {
                    if (SystemConfig.isOptSet("azahar_texture_filter"))
                    {
                        ini.WriteValue("Renderer", "texture_filter\\default", "false");
                        ini.WriteValue("Renderer", "texture_filter", SystemConfig["azahar_texture_filter"]);
                    }
                    else
                    {
                        ini.WriteValue("Renderer", "texture_filter\\default", "true");
                        ini.WriteValue("Renderer", "texture_filter", "0");
                    }
                }

                if (Features.IsSupported("azahar_layout_option"))
                {
                    if (SystemConfig.isOptSet("azahar_layout_option"))
                    {
                        ini.WriteValue("Layout", "layout_option\\default", "false");
                        ini.WriteValue("Layout", "layout_option", SystemConfig["azahar_layout_option"]);
                        SimpleLogger.Instance.Info("[INFO] Setting layout option to : " + SystemConfig["azahar_layout_option"]);
                    }
                    else
                    {
                        ini.WriteValue("Layout", "layout_option\\default", "true");
                        ini.WriteValue("Layout", "layout_option", "0");
                    }
                }

                if (Features.IsSupported("azahar_swap_screen"))
                {
                    if (SystemConfig.isOptSet("azahar_swap_screen") && SystemConfig.getOptBoolean("azahar_swap_screen"))
                    {
                        ini.WriteValue("Layout", "swap_screen\\default", "false");
                        ini.WriteValue("Layout", "swap_screen", "true");
                    }
                    else
                    {
                        ini.WriteValue("Layout", "swap_screen\\default", "true");
                        ini.WriteValue("Layout", "swap_screen", "false");
                    }
                }

                // Audio
                BindQtIniFeature(ini, "Audio", "audio_emulation", "azahar_audio_emulation", "0");
                BindQtIniFeature(ini, "Audio", "output_type", "azahar_audio_api", "0");

                // Define console region
                if (SystemConfig.isOptSet("azahar_region_value") && !string.IsNullOrEmpty(SystemConfig["azahar_region_value"]) && SystemConfig["azahar_region_value"] != "-1")
                {
                    ini.WriteValue("System", "region_value\\default", "false");
                    ini.WriteValue("System", "region_value", SystemConfig["azahar_region_value"]);
                }
                else if (Features.IsSupported("azahar_region_value"))
                {
                    ini.WriteValue("System", "region_value\\default", "true");
                    ini.WriteValue("System", "region_value", "-1");
                }

                // Custom textures
                if (SystemConfig.isOptSet("azahar_custom_textures") && SystemConfig.getOptBoolean("azahar_custom_textures"))
                {
                    ini.WriteValue("Utility", "custom_textures\\default", "false");
                    ini.WriteValue("Utility", "custom_textures", "true");
                    SimpleLogger.Instance.Info("[INFO] Custom textures enabled.");
                }
                else if (Features.IsSupported("azahar_custom_textures"))
                {
                    ini.WriteValue("Utility", "custom_textures\\default", "true");
                    ini.WriteValue("Utility", "custom_textures", "false");
                }

                if (SystemConfig.isOptSet("azahar_PreloadTextures") && SystemConfig.getOptBoolean("azahar_PreloadTextures"))
                {
                    ini.WriteValue("Utility", "preload_textures\\default", "false");
                    ini.WriteValue("Utility", "preload_textures", "true");
                }
                else if (Features.IsSupported("azahar_PreloadTextures"))
                {
                    ini.WriteValue("Utility", "preload_textures\\default", "true");
                    ini.WriteValue("Utility", "preload_textures", "false");
                }

                // Renderer
                BindQtIniFeature(ini, "Renderer", "graphics_api", "graphics_api", "1");

                // Bezel adjustment for default_nocurve and default_curve
                List<string> bezelAdjust = new List<string>() { "default_curve", "default_curve_night", "default_nocurve", "default_nocurve_night" };
                if (bezelAdjust.Contains(SystemConfig["bezel"]))
                {
                    ini.WriteValue("Layout", "layout_option\\default", "false");
                    ini.WriteValue("Layout", "layout_option", "6");

                    decimal topPosX = 562m / 1920m;
                    decimal newTopPosX = (topPosX * (_resolution == null? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalTopPosx = (int)newTopPosX;

                    decimal topPosY = 66m / 1080m;
                    decimal newTopPosY = (topPosY * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalTopPosY = (int)newTopPosY;

                    decimal topWidth = 796m / 1920m;
                    decimal newTopWidth = (topWidth * (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalTopWidth = (int)newTopWidth;

                    decimal topHeight = 476m / 1080m;
                    decimal newTopHeight = (topHeight * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalTopHeight = (int)newTopHeight;

                    decimal bottomPosX = 648m / 1920m;
                    decimal newBottomPosX = (bottomPosX * (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalBottomPosx = (int)newBottomPosX;

                    decimal bottomPosY = 554m / 1080m;
                    decimal newBottomPosY = (bottomPosY * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalBottomPosY = (int)newBottomPosY;

                    decimal bottomWidth = 624m / 1920m;
                    decimal newBottomWidth = (bottomWidth * (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalBottomWidth = (int)newBottomWidth;

                    decimal bottomHeight = 466m / 1080m;
                    decimal newBottomHeight = (bottomHeight * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalBottomHeight = (int)newBottomHeight;

                    ini.WriteValue("Layout", "custom_top_x\\default", "false");
                    ini.WriteValue("Layout", "custom_top_x", finalTopPosx.ToString());

                    ini.WriteValue("Layout", "custom_top_y\\default", "false");
                    ini.WriteValue("Layout", "custom_top_y", finalTopPosY.ToString());

                    ini.WriteValue("Layout", "custom_top_width\\default", "false");
                    ini.WriteValue("Layout", "custom_top_width", finalTopWidth.ToString());

                    ini.WriteValue("Layout", "custom_top_height\\default", "false");
                    ini.WriteValue("Layout", "custom_top_height", finalTopHeight.ToString());

                    ini.WriteValue("Layout", "custom_bottom_x\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_x", finalBottomPosx.ToString());

                    ini.WriteValue("Layout", "custom_bottom_y\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_y", finalBottomPosY.ToString());

                    ini.WriteValue("Layout", "custom_bottom_width\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_width", finalBottomWidth.ToString());

                    ini.WriteValue("Layout", "custom_bottom_height\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_height", finalBottomHeight.ToString());
                }
            }
        }

        public static void Write3DSnand(string path, bool libretro = false)
        {
            if (!File.Exists(path))
                return;

            SimpleLogger.Instance.Info("[Generator] Writing to 3DS nand file.");

            int langId = 1;

            if (!libretro)
            {
                if (Program.SystemConfig.isOptSet("n3ds_language") && !string.IsNullOrEmpty(Program.SystemConfig["n3ds_language"]))
                    langId = Program.SystemConfig["n3ds_language"].ToInteger();
                else
                    langId = AzaharGenerator.Get3DSLangFromEnvironment();
            }
            else
            {
                if (Program.SystemConfig.isOptSet("azaharlr_language") && LibretroLanguages.ContainsKey(Program.SystemConfig["azaharlr_language"]))
                    langId = LibretroLanguages[Program.SystemConfig["azaharlr_language"]];
                else
                    langId = AzaharGenerator.Get3DSLangFromEnvironment();
            }

            // Read nand file
            byte[] bytes = File.ReadAllBytes(path);
            byte[] pattern = { 0x02, 0x00, 0x0A, 0x00 };

            int index = FindPattern(bytes, pattern);
            if (index == -1)
                return;

            int byteAfter = index + pattern.Length;
            if (byteAfter >= bytes.Length)
                return;

            bytes[byteAfter] = (byte)langId;

            File.WriteAllBytes(path, bytes);
        }

        static int FindPattern(byte[] data, byte[] pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }

        private static readonly Dictionary<string, int> LibretroLanguages = new Dictionary<string, int>()
        {
            { "Japanese", 0 },
            { "English", 1 },
            { "French", 2 },
            { "German", 3 },
            { "Italian", 4 },
            { "Spanish", 5 },
            { "Simplified Chinese", 6 },
            { "Korean", 7 },
            { "Dutch", 8 },
            { "Portuguese", 9 },
            { "Russian", 10 },
            { "Traditional Chinese", 11 }
        };

        private static int Get3DSLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, int>()  //OA = 10, OB = 11 (traditional chinese)
            {
                { "jp", 0 },
                { "ja", 0 },
                { "en", 1 },
                { "fr", 2 },
                { "de", 3 },
                { "it", 4 },
                { "es", 5 },
                { "zh", 6 },
                { "ko", 7 },
                { "nl", 8 },
                { "pt", 9 },
                { "ru", 10 },
            };

            SimpleLogger.Instance.Info("[Generator] Getting language from RetroBat language.");

            // Special case for Taiwanese which is zh_TW
            if (Program.SystemConfig["Language"] == "zh_TW")
                return 11;

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out int ret))
                    return ret;
            }

            return 1;
        }

        private void SetupSaveStates(string system, string emulator, string core, string path, string rom)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported(emulator))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
            string emulatorPath = Path.Combine(path, "user", "states");
            string icon = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png");

            // Copied to the quick save slot before the watcher starts, so it is not imported back as a new state
            string stateFile = SystemConfig["state_file"];
            if (!string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
            {
                string titleId = AzaharSaveStatesMonitor.GetTitleId(localPath, rom);
                if (titleId == null)
                    SimpleLogger.Instance.Warning("[AzaharGenerator] Unknown title id, unable to load save state " + stateFile);
                else
                {
                    try
                    {
                        FileTools.TryCreateDirectory(emulatorPath);
                        File.Copy(stateFile, AzaharSaveStatesMonitor.GetEmulatorStateFile(emulatorPath, titleId, 0), true);
                        _loadState = true;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Warning("[AzaharGenerator] Unable to prepare save state " + stateFile + " : " + ex.Message);
                    }
                }
            }

            _saveStatesWatcher = new AzaharSaveStatesMonitor(rom, emulatorPath, localPath, icon);
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

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            if (_loadState)
            {
                Keys quickLoadKey = QtKeyToKeys(_quickLoadShortcut);
                if (quickLoadKey == Keys.None)
                    SimpleLogger.Instance.Warning("[AzaharGenerator] Quick Load shortcut '" + _quickLoadShortcut + "' cannot be sent, load the save state manually.");
                else
                    StartQuickLoad(Path.GetFileNameWithoutExtension(path.FileName), quickLoadKey);
            }

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }

        /// <summary>
        /// Windows key of a Qt key name (single keys only, Keys.None otherwise)
        /// </summary>
        private static Keys QtKeyToKeys(string qtKey)
        {
            if (string.IsNullOrEmpty(qtKey) || qtKey.Contains("+"))
                return Keys.None;

            if (qtKey.Length == 1 && qtKey[0] >= '0' && qtKey[0] <= '9')
                return Keys.D0 + (qtKey[0] - '0');

            if (qtKey.Length == 1 && qtKey[0] >= 'A' && qtKey[0] <= 'Z')
                return (Keys)qtKey[0];

            switch (qtKey)
            {
                case "Esc": return Keys.Escape;
                case "Backspace": return Keys.Back;
                case "Ins": return Keys.Insert;
                case "Del": return Keys.Delete;
                case "PgUp": return Keys.PageUp;
                case "PgDown": return Keys.PageDown;
                case "Print": return Keys.PrintScreen;
                case "ScrollLock": return Keys.Scroll;
            }

            // F1-F12, Space, Tab, Return, Home, End, Pause, arrows, CapsLock : same name
            if (qtKey.Length > 1 && Enum.TryParse(qtKey, out Keys key))
                return key;

            return Keys.None;
        }

        /// <summary>
        /// Azahar cannot load a state from the command line: the Quick Load shortcut is sent once the game is loaded.
        /// </summary>
        private static void StartQuickLoad(string processName, Keys quickLoadKey)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    DateTime start = DateTime.UtcNow;

                    while (DateTime.UtcNow - start < TimeSpan.FromSeconds(60))
                    {
                        Thread.Sleep(250);

                        var process = Process.GetProcessesByName(processName).FirstOrDefault();
                        if (process == null)
                            continue;

                        // Title is "Azahar <version> | <game>" once the game is loaded (UpdateWindowTitle, src/citra_qt/citra_qt.cpp)
                        IntPtr hWnd = User32.FindHwnds(process.Id).FirstOrDefault(h =>
                        {
                            string title = User32.GetWindowText(h);
                            return title != null && title.StartsWith("Azahar") && title.Contains(" | ");
                        });

                        if (hWnd == IntPtr.Zero)
                            continue;

                        Thread.Sleep(QuickLoadDelay);

                        User32.SetForegroundWindow(hWnd);
                        SendKey.Send(quickLoadKey, true);
                        Thread.Sleep(50);
                        SendKey.Send(quickLoadKey, false);

                        SimpleLogger.Instance.Info("[AzaharGenerator] Quick Load sent to load the save state.");
                        return;
                    }

                    SimpleLogger.Instance.Warning("[AzaharGenerator] Game window not found, save state not loaded.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[AzaharGenerator] Unable to load save state : " + ex.Message);
                }
            });

            thread.IsBackground = true;
            thread.Start();
        }
    }
}