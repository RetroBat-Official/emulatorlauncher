using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    partial class KeishoGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private string _path;
        private string _romSet;     // Set selected with --game, null when the archive name is not a set of games.xml
        private string _family;     // Parent set (or the set itself), used to search the controller mappings
        private SaveStatesWatcher _saveStatesWatcher;
        private bool _pad2Keyoverride = false;
        private bool _keyboardGears;  // The game shifts gears with F1..F5 on the keyboard (games.xml gearbox / shift_buttons)

        // Catalogs shipped in the Keisho 'lang' folder ("en" is the built-in language)
        private static readonly string[] _keishoLanguages = new string[] { "ar", "bg", "bn", "ca", "cs", "da", "de", "el", "es", "et", "fa", "fi", "fr",
            "he", "hi", "hr", "hu", "id", "it", "ja", "ko", "lt", "lv", "ms", "nb", "nl", "pl", "pt", "pt_BR", "ro", "ru", "sk", "sl", "sr", "sv",
            "ta", "th", "tr", "uk", "ur", "vi", "zh_CN", "zh_TW" };

        public KeishoGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("keisho");
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "keisho.exe");
            if (!File.Exists(exe))
                return null;

            Sdl3GameController.SetEnumerationHints(Sdl3GameController.Sdl3HintProfile.NoHidapi);

            _path = path;

            // Archives are identified by CRC and a merged archive can hold several revisions: select the set named like the archive
            ReadGameDatabase(path, Path.GetFileNameWithoutExtension(rom));

            bool fullscreen = ShouldRunFullscreen() && !SystemConfig.getOptBoolean("disable_fullscreen");

            // Keisho draws its bezel itself, in fullscreen and 4:3 only
            if (fullscreen && GetAspectMode() == "4:3")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            SetupConfiguration(path, system, fullscreen);
            SetupSaveStates(system, rom);

            var commandArray = new List<string>
            {
                "--config",
                "\"" + path + "\""
            };

            if (fullscreen)
                commandArray.Add("--fullscreen");

            if (!string.IsNullOrEmpty(_romSet))
            {
                commandArray.Add("--game");
                commandArray.Add(_romSet);
            }

            // Save state selected in EmulationStation
            string stateFile = SystemConfig["state_file"];
            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
            {
                commandArray.Add("--load-state");
                commandArray.Add("\"" + stateFile + "\"");
            }

            commandArray.Add("\"" + rom + "\"");

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
            };
        }

        /// <summary>
        /// Reads games.xml (next to keisho.exe) to find the set named like the archive and its parent (game family).
        /// </summary>
        private void ReadGameDatabase(string path, string romName)
        {
            _family = romName;

            string dbFile = Path.Combine(path, "games.xml");
            if (!File.Exists(dbFile))
            {
                SimpleLogger.Instance.Warning("[Keisho] games.xml not found in " + path);
                return;
            }

            try
            {
                // The comments of games.xml contain "--", rejected by XmlReader: they are removed before parsing
                string xml = Regex.Replace(File.ReadAllText(dbFile), "<!--.*?-->", "", RegexOptions.Singleline);

                var game = XDocument.Parse(xml).Descendants("game")
                    .FirstOrDefault(g => romName.Equals((string)g.Attribute("name"), StringComparison.InvariantCultureIgnoreCase));

                if (game == null)
                {
                    SimpleLogger.Instance.Info("[Keisho] '" + romName + "' is not a set name of games.xml, the set is identified by the emulator from the archive content.");
                    return;
                }

                _romSet = (string)game.Attribute("name");
                string parent = (string)game.Attribute("parent");
                _family = string.IsNullOrEmpty(parent) ? _romSet : parent;

                // Clones inherit the gear flags of their parent
                var parentGame = string.IsNullOrEmpty(parent) ? null : game.Parent.Elements("game").FirstOrDefault(g => parent.Equals((string)g.Attribute("name"), StringComparison.InvariantCultureIgnoreCase));
                _keyboardGears = new[] { game, parentGame }.Where(g => g != null)
                    .Any(g => (string)g.Attribute("gearbox") == "true" || (string)g.Attribute("shift_buttons") == "true");

                SimpleLogger.Instance.Info("[Keisho] Set '" + _romSet + "', family '" + _family + "'.");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Keisho] Unable to read games.xml : " + ex.Message);
            }
        }

        private string GetAspectMode()
        {
            return SystemConfig.GetValueOrDefault("keisho_ratio", "4:3");
        }

        private void SetupConfiguration(string path, string system, bool fullscreen)
        {
            string iniFile = Path.Combine(path, "keisho.ini");

            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues))
            {
                // Paths: everything stays in the RetroBat folders
                string nvramPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "keisho");
                FileTools.TryCreateDirectory(nvramPath);
                ini.WriteValue("paths", "nvram_dir", nvramPath);

                string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "keisho");
                FileTools.TryCreateDirectory(screenshotsPath);
                ini.WriteValue("paths", "screenshot_dir", screenshotsPath);

                // No rom_dir: without a game picker to return to, Escape quits the emulator
                ini.Remove("paths", "rom_dir");
                ini.Remove("paths", "model1_rom_dir");
                ini.Remove("paths", "model3_rom_dir");

                // Front-end behavior
                ini.WriteValue("video", "fullscreen", fullscreen ? "true" : "false");
                ini.WriteValue("video", "throttle", "true");
                ini.WriteValue("paths", "scrape_artwork", "false");
                ini.WriteValue("system", "language", GetKeishoLanguage());

                // Music volume of the games with an MPEG music board (Daytona USA, Sega Rally...)
                BindIniFeatureSlider(ini, "audio", "music_volume", "keisho_musicvolume", "100");

                // RetroBat manages the controller hotkeys (Guide + L3/R3 for Service/Test stay active, L3/R3 alone are sent by padtokey)
                ini.WriteValue("gamepad", "pad_shortcuts", "false");

                // Video
                BindIniFeature(ini, "video", "graphics_backend", "keisho_renderer", "opengl");
                BindIniFeature(ini, "video", "render_scale", "keisho_resolution", "1");
                BindBoolIniFeature(ini, "video", "vsync", "keisho_vsync", "true", "false");
                BindIniFeature(ini, "video", "scaling_method", "keisho_scaling", "sharp");
                ini.WriteValue("video", "aspect_mode", GetAspectMode());
                BindIniFeatureSlider(ini, "video", "brightness", "keisho_brightness", "50");
                BindBoolIniFeature(ini, "video", "crt_enabled", "keisho_crt", "true", "false");

                if (SystemConfig.isOptSet("keisho_anisotropy") && !string.IsNullOrEmpty(SystemConfig["keisho_anisotropy"]))
                {
                    ini.WriteValue("video", "texture_filter", "anisotropic");
                    ini.WriteValue("video", "anisotropy", SystemConfig["keisho_anisotropy"]);
                }
                else
                    ini.WriteValue("video", "texture_filter", "faithful");

                BindIniFeature(ini, "video", "upscale_2d", "keisho_upscale2d", "faithful");
                BindIniFeature(ini, "video", "translucency", "keisho_translucency", "stipple");
                BindBoolIniFeature(ini, "video", "extended_draw_distance", "keisho_draw_distance", "true", "false");

                SetupBezel(ini, path);

                // On-screen information
                string perf = SystemConfig["performance_overlay"];
                ini.WriteValue("video", "show_fps", (perf == "simple" || perf == "detailed") ? "true" : "false");
                BindBoolIniFeatureOn(ini, "video", "show_notifications", "keisho_notifications", "true", "false");

                // Emulation
                ini.WriteValue("system", "rewind", SystemConfig.getOptBoolean("rewind") ? "true" : "false");

                // Lamps and drive board outputs for MAMEHooker-like tools
                BindBoolIniFeature(ini, "network", "outputs_windows", "keisho_outputs", "true", "false");

                // Hotkeys, Light guns, wheel and gamepads
                WriteKeyboardHotkeys(ini);
                SetupGuns(ini);
                SetupWheel(ini);
                CreateControllerConfiguration(ini);

                ini.Save();
            }
        }

        /// <summary>
        /// Keisho searches <set>.png, <parent>.png then default.png in bezel_dir: the RetroBat bezel is copied as default.png
        /// to a folder used only by RetroBat, so that the user bezels folder of the emulator is never changed.
        /// </summary>
        private void SetupBezel(IniFile ini, string path)
        {
            string bezelPath = Path.Combine(path, "retrobat_bezel");
            string bezelFile = Path.Combine(bezelPath, "default.png");

            if (_bezelFileInfo != null && !string.IsNullOrEmpty(_bezelFileInfo.PngFile) && File.Exists(_bezelFileInfo.PngFile))
            {
                try
                {
                    FileTools.TryCreateDirectory(bezelPath);
                    File.Copy(_bezelFileInfo.PngFile, bezelFile, true);

                    ini.WriteValue("video", "bezel", "true");
                    ini.WriteValue("paths", "bezel_dir", bezelPath);
                    return;
                }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[Keisho] Unable to copy the bezel : " + ex.Message); }
            }

            ini.WriteValue("video", "bezel", "false");
            ini.Remove("paths", "bezel_dir");

            if (File.Exists(bezelFile))
                try { File.Delete(bezelFile); } catch { }
        }

        /// <summary>
        /// Keisho saves in <nvram_dir>\states: the watcher copies each new state to the RetroBat folder as the next slot.
        /// A state chosen in EmulationStation is loaded with --load-state, the emulator folder is never changed.
        /// </summary>
        private void SetupSaveStates(string system, string rom)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported("keisho"))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, "keisho", "keisho");
            string emulatorPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "keisho", "states");
            string icon = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png");

            _saveStatesWatcher = new KeishoSaveStatesMonitor(rom, emulatorPath, localPath, icon);
            SimpleLogger.Instance.Info("[INFO] SavesStatesWatcher enabled.");
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
                PadToKey.AddOrUpdateKeyMapping(mapping, "keisho", InputKey.hotkey | InputKey.start, "(%{CLOSE})");
            }
            else if (_keyboardGears && mapping != null && mapping["keisho"] != null)
            {
                // F1 / F2 / F4 shift gears in these games: the controller hotkeys send the Keisho keys written in [system]
                foreach (var input in mapping["keisho"].Input)
                {
                    if (input.Code == "KEY_F1")
                        input.Code = "KEY_F10";
                    else if (input.Code == "KEY_F2")
                        input.Code = "KEY_F6";
                    else if (input.Code == "KEY_F4")
                        input.Code = "KEY_F7";
                }
            }

            return AddOperatorButtons(mapping);
        }

        /// <summary>
        /// Test / Service on R3 / L3 alone, as on the other arcade systems. Keisho has no pad binding for them (Guide chord only),
        /// so the controller sends the Keisho keys, read by scancode : 0 = Test, 9 = Service.
        /// </summary>
        private static PadToKey AddOperatorButtons(PadToKey mapping)
        {
            foreach (var button in new[] { new { Key = InputKey.r3, Code = "KEY_0" }, new { Key = InputKey.l3, Code = "KEY_9" } })
            {
                mapping = PadToKey.AddOrUpdateKeyMapping(mapping, "keisho", button.Key, (string)null);

                var input = mapping?["keisho"]?[button.Key];
                if (input != null)
                    input.Code = button.Code;
            }

            return mapping;
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

        private string GetKeishoLanguage()
        {
            // Full code first (pt_BR, zh_TW), then the language alone
            string full = SystemConfig["Language"];
            if (!string.IsNullOrEmpty(full))
            {
                string exact = _keishoLanguages.FirstOrDefault(l => l.Equals(full, StringComparison.InvariantCultureIgnoreCase));
                if (exact != null)
                    return exact;
            }

            string lang = GetCurrentLanguage();
            if (lang == "jp")
                lang = "ja";
            else if (lang == "zh")
                return "zh_CN";
            else if (lang == "no")
                lang = "nb";

            return _keishoLanguages.FirstOrDefault(l => l == lang) ?? "en";
        }
    }
}