using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class OdinGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private string _path;
        private string _game;
        private string _section;    // Game of the odin.ini sections : the parent for a version
        private bool _pad2Keyoverride = false;
        private string _statesPath;
        private SaveStatesWatcher _saveStatesWatcher;

        // Games with an optional sound board, Gaelco games with a woofer ([sound.<game>] mode)
        private static readonly string[] _optionSoundGames = { "pharrier", "sgnascar", "swracer", "atvtrack", "gfootbal", "smashdrv" };

        public OdinGenerator()
        {
            DependsOnDesktopResolution = true;
        }
        protected override bool UseGenericScreenPlacement { get { return true; } }

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "odin.exe"))
                return null;

            _path = path;

            // Odin boots MAME sets, identified by their name (airtrix, braveff...)
            _game = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();

            // A version of a game (airtrixo, smashdrvb...) shares the odin.ini sections of its parent
            _section = _parents.TryGetValue(_game, out string parent) ? parent : _game;

            bool fullscreen = ShouldRunFullscreen() && !SystemConfig.getOptBoolean("disable_fullscreen");

            if (fullscreen && !SystemConfig.getOptBoolean("odin_emulator_bezels"))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            SetupSettings(path, system, fullscreen);
            SetupSaveStates(system, rom);

            // Odin looks for the shared sets of the Hikaru games (hikaru, mie...) in the BIOS folder, then in the games folder (Gaelco games need none)
            var commandArray = new List<string>
            {
                "-R", "\"" + Path.GetDirectoryName(rom) + "\"",
                "-B", "\"" + AppConfig.GetFullPath("bios") + "\"",
                "-r", _game
            };

            if (fullscreen)
                commandArray.Add("-f");

            // State chosen in EmulationStation
            string stateFile = SystemConfig["state_file"];
            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
            {
                commandArray.Add("-l");
                commandArray.Add("\"" + Path.GetFullPath(stateFile) + "\"");
            }

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
                // odin.exe is a console program which frees its console: no console window
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // No blocking error box when the board stops, the error is read from odin.log
            ret.EnvironmentVariables["ODIN_STOP_BOX"] = "0";

            return ret;
        }

        /// <summary>
        /// odin.ini beside odin.exe makes Odin portable: settings, saves, states and log are kept in its folder.
        /// Odin writes every key it knows as a comment at its default value, the generator writes the keys it manages.
        /// </summary>
        private void SetupSettings(string path, string system, bool fullscreen)
        {
            string iniFile = Path.Combine(path, "odin.ini");

            // KeepEmptyValues : an empty control line leaves the control without input
            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues))
            {
                // Paths
                string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "odin");
                FileTools.TryCreateDirectory(savesPath);
                ini.WriteValue("paths", "saves", savesPath);

                // States of F2 / F4 (one per game), copied to the RetroBat slots by OdinSaveStatesMonitor
                _statesPath = Path.Combine(savesPath, "states");
                FileTools.TryCreateDirectory(_statesPath);
                ini.WriteValue("paths", "states", _statesPath);

                // odin.log stays in the emulator folder, it is read on exit to report launch errors
                ini.Remove("paths", "log");

                // Display
                ini.WriteValue("display", "fullscreen", fullscreen ? "on" : "off");
                BindIniFeature(ini, "display", "api", "odin_renderer", "opengl");
                BindIniFeature(ini, "display", "scale", "odin_scale", "1");
                BindIniFeature(ini, "display", "anisotropy", "odin_anisotropy", "1");
                BindIniFeature(ini, "display", "upscale_mode", "odin_upscale_mode", "raw");
                BindIniFeature(ini, "display", "vsync", "odin_vsync", "off");
                BindIniFeature(ini, "display", "filter", "odin_crt", "none");
                ini.WriteValue("display", "unthrottled", "off");

                SetupBezel(ini);

                // The sections of a version itself (written by an older RetroBat) would be read after the parent's ones
                if (_section != _game)
                {
                    foreach (string kind in new[] { "controls.", "players.", "sound." })
                        ini.ClearSection(kind + _game);
                }

                // Sound : mode of the games with an optional sound board ([sound.<game>] in OPTIONS), Odin default when not set
                string soundSection = "sound." + _section;
                if (_optionSoundGames.Contains(_section) && SystemConfig.isOptSet("odin_soundmode") && !string.IsNullOrEmpty(SystemConfig["odin_soundmode"]))
                    ini.WriteValue(soundSection, "mode", SystemConfig["odin_soundmode"]);
                else
                    ini.Remove(soundSection, "mode");

                // Light guns, wheel and controllers
                SetupGuns(ini);
                SetupWheelOptions(ini);
                CreateControllerConfiguration(ini);
                BindIniFeatureSlider(ini, "gamepads", "dead_zone", "odin_deadzone", "3");

                // The pad shakes as the cabinet (vibration device, drive board, nozzle), on by default
                if (SystemConfig.isOptSet("odin_rumble") && !SystemConfig.getOptBoolean("odin_rumble"))
                    ini.WriteValue("players." + _section, "vibration", "off");
                else
                    ini.Remove("players." + _section, "vibration");

                // Lamps, nozzle vibration, drive board : MAME network outputs (port 8000) for Hook Of The Reaper, QMamehook...
                BindBoolIniFeature(ini, "outputs", "network", "odin_outputs", "on", "off");

                // Hotkeys
                WriteHotkeys(ini);

                ini.Save();
            }
        }

        /// <summary>
        /// Odin draws a bezel picture (<game>_bezel.png) from its artwork folder and places the game in its hole (<game>_bezel.info, Batocera format).
        /// RetroBat decorations use the same format : they are copied to a work folder given to Odin as its artwork folder.
        /// USE EMULATOR BEZELS keeps the artwork folder of Odin (Odin-Artwork pack).
        /// </summary>
        private void SetupBezel(IniFile ini)
        {
            // The artwork folder of Odin, beside odin.exe, is used when the key is empty
            ini.Remove("paths", "artwork");

            if (SystemConfig.getOptBoolean("odin_emulator_bezels"))
            {
                ini.WriteValue("display", "bezel", "image");
                return;
            }

            string bezelPath = Path.Combine(Path.GetTempPath(), "odin-bezel");

            // Odin also looks for the pictures of the parent set: no file is kept from a previous game
            try
            {
                if (Directory.Exists(bezelPath))
                    Directory.Delete(bezelPath, true);
            }
            catch { }

            if (_bezelFileInfo == null || string.IsNullOrEmpty(_bezelFileInfo.PngFile) || !File.Exists(_bezelFileInfo.PngFile))
            {
                ini.WriteValue("display", "bezel", "off");
                return;
            }

            try
            {
                Directory.CreateDirectory(bezelPath);
                File.Copy(_bezelFileInfo.PngFile, Path.Combine(bezelPath, _game + "_bezel.png"), true);

                if (!string.IsNullOrEmpty(_bezelFileInfo.InfoFile) && File.Exists(_bezelFileInfo.InfoFile))
                    File.Copy(_bezelFileInfo.InfoFile, Path.Combine(bezelPath, _game + "_bezel.info"), true);

                ini.WriteValue("paths", "artwork", bezelPath);
                ini.WriteValue("display", "bezel", "image");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Odin] Unable to copy the bezel : " + ex.Message);
                ini.WriteValue("display", "bezel", "off");
            }
        }

        #region Hotkeys
        // Odin gamepad shortcuts (Guide alone or Guide + button), kept besides the RetroBat hotkeys (Select, es_padtokey.cfg).
        // Card (Guide + Y) is not kept : with Guide as RetroBat hotkey, Y saves the state.
        private static readonly Dictionary<string, string> _hotkeyPadInputs = new Dictionary<string, string>()
        {
            { "menu", "pad:guide" },
            { "pause", "pad:guide+back" },
            { "save_state", "pad:guide+rightshoulder" },
            { "load_state", "pad:guide+leftshoulder" },
            { "quit", "pad:guide+start" },
        };

        private void WriteHotkeys(IniFile ini)
        {
            // RetroBat defaults, the keys sent by the controller hotkeys of es_padtokey.cfg (RetroBat hotkey : Select). Escape quits as in the other emulators.
            var keys = new Dictionary<string, string>()
            {
                { "menu", "F10" },
                { "pause", "P" },
                { "save_state", "F2" },
                { "load_state", "F4" },
                { "reset", "Ctrl+R" },
                { "fullscreen", "F11, Alt+Return" },
                { "card", "F6" },
                { "light_gun", "F7" },
                { "quit", "Escape, Ctrl+Q" },
            };

            // User keyboard hotkeys from odin_kb_hotkeys.yml / kb_hotkeys.yml, the controller hotkeys follow them (padToKey.xml)
            if (Hotkeys.GetHotKeysFromFile("odin", "", out Dictionary<string, HotkeyResult> custoHotkeys) && custoHotkeys.Count > 0)
            {
                foreach (var hotkey in custoHotkeys.Values)
                {
                    if (!string.IsNullOrEmpty(hotkey.EmulatorValue))
                        keys[hotkey.EmulatorKey] = hotkey.EmulatorValue;
                }

                _pad2Keyoverride = true;
            }

            foreach (var key in keys)
            {
                string value = key.Value;
                if (_hotkeyPadInputs.TryGetValue(key.Key, out string pad))
                    value += ", " + pad;

                ini.WriteValue("hotkeys", key.Key, value);
            }

            // Close game goes back to the Odin library : disabled, quit is used instead
            ini.WriteValue("hotkeys", "close_game", "none");
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
                PadToKey.AddOrUpdateKeyMapping(mapping, "odin", InputKey.hotkey | InputKey.start, "(%{CLOSE})");
            }

            return mapping;
        }
        #endregion

        /// <summary>
        /// Odin saves <set>.odinstate in [paths] states : the watcher copies each new state to the RetroBat folder as the next slot.
        /// A state chosen in EmulationStation is loaded with -l, the emulator folder is never changed.
        /// </summary>
        private void SetupSaveStates(string system, string rom)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported("odin"))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, "odin", "odin");
            string icon = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png");

            _saveStatesWatcher = new OdinSaveStatesMonitor(rom, _statesPath, localPath, icon);
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
            int ret = base.RunAndWait(path);

            if (RawExitCode != 0 && ExitCode != ExitCodes.CustomError)
            {
                string error = GetLaunchError();
                if (!string.IsNullOrEmpty(error))
                {
                    SimpleLogger.Instance.Error("[Odin] " + error);
                    SetCustomError("Odin : " + error);
                }
            }

            return ret;
        }

        /// <summary>
        /// Launch error written by Odin in odin.log (main.cpp : load_or_tell, play) : missing files first, else the last error.
        /// </summary>
        private string GetLaunchError()
        {
            try
            {
                string logFile = Path.Combine(_path, "odin.log");
                if (!File.Exists(logFile))
                    return null;

                var errors = File.ReadAllLines(logFile)
                    .Where(l => l.StartsWith("ERROR: "))
                    .Select(l => l.Substring(7).Trim())
                    .ToList();

                if (errors.Count == 0)
                    return null;

                // "'<game>': <file> (<size> bytes, CRC <crc>) is missing; MAME keeps it in <set>.zip"
                var missing = errors.Where(e => e.Contains(" is missing; ")).ToList();
                if (missing.Count == 1)
                    return missing[0];

                if (missing.Count > 1)
                {
                    // "missing files : 93c46.ic115 (hikaru.zip), x76f100.ic85 (hikaru.zip)"
                    var files = missing.Select(e =>
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(e, @"^'[^']+': (.+?) \(.*MAME keeps it in (.+)$");
                        return m.Success ? m.Groups[1].Value + " (" + m.Groups[2].Value + ")" : e;
                    });

                    return "missing files : " + string.Join(", ", files);
                }

                return errors.Last();
            }
            catch { }

            return null;
        }
    }
}