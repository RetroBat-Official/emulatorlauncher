using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace EmulatorLauncher
{
    partial class Sm2EmuGenerator
    {
        // Controller positions (yml keys) to SDL_GamepadButton values read by sm2-emu.
        // Triggers are "100 + SDL_GamepadAxis": the trigger pulled past half travel (LeftTrigger axis 4, RightTrigger axis 5).
        private static readonly Dictionary<string, int> _sdlPadButtons = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "south", 0 }, { "east", 1 }, { "west", 2 }, { "north", 3 }, { "select", 4 }, { "start", 6 },
            { "l3", 7 }, { "r3", 8 }, { "l1", 9 }, { "r1", 10 },
            { "up", 11 }, { "down", 12 }, { "left", 13 }, { "right", 14 },
            { "l2", 104 }, { "r2", 105 }
        };

        // yml values to sm2-emu pad roles (config.cpp, kPadRoleNames), in the emulator order
        private static readonly Dictionary<string, string> _sm2PadRoles = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "Button1", "button1" }, { "Button2", "button2" }, { "Button3", "button3" }, { "Button4", "button4" },
            { "Up", "up" }, { "Down", "down" }, { "Left", "left" }, { "Right", "right" },
            { "Start", "start" }, { "Coin", "coin" }, { "GearUp", "gear_up" }, { "GearDown", "gear_down" }
        };

        // Analog roles (config.cpp, kPadAxisNames): -1 keeps the sm2-emu default (aim/steering on the left stick,
        // accelerator on the right trigger, brake on the left trigger, Virtual On right lever on the right stick)
        private static readonly string[] _sm2AxisRoles = new string[] { "aim_x", "aim_y", "accel", "brake", "lever_x", "lever_y" };

        static readonly string[] mappingPaths =
        {
            "{userpath}\\inputmapping\\sm2emu.yml",
            "{systempath}\\resources\\inputmapping\\sm2emu.yml"
        };

        /// <summary>
        /// sm2-emu reads the gamepads through SDL3 by position (south/east/west/north...), the player 1 pad is the first one enumerated by SDL3.
        /// The same layout is written for both players in [gamepad], from sm2emu.yml (see GetButtonMapping).
        /// </summary>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for sm2-emu");

            string[] iniSections = { "video", "audio", "paths", "wheel", "lightgun", "network", "system", "gamepad", "ROOT" };

            // Per-game layouts saved from the sm2-emu menu ([<family>] sections) would replace the RetroBat mapping
            foreach (string section in ini.EnumerateSections())
            {
                if (string.IsNullOrEmpty(section) || iniSections.Contains(section))
                    continue;

                ini.ClearSection(section);
            }

            var roles = GetButtonMapping();

            // Assign pad to players
            var pad1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            var pad2 = Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
            UpdatePadIni(pad1, "pad1_device", ini);
            UpdatePadIni(pad2, "pad2_device", ini);

            for (int player = 1; player <= 2; player++)
            {
                string prefix = "pad" + player + "_";

                foreach (string role in _sm2PadRoles.Values)
                {
                    int button;
                    ini.WriteValue("gamepad", prefix + "button_" + role, roles.TryGetValue(role, out button) ? button.ToString() : "-1");
                }

                foreach (string axis in _sm2AxisRoles)
                {
                    ini.WriteValue("gamepad", prefix + "axis_" + axis, "-1");

                    bool isPedal = axis == "accel" || axis == "brake";
                    if (isPedal || axis == "aim_x" || axis == "aim_y")
                        ini.WriteValue("gamepad", prefix + "axis_" + axis + "_invert", "false");
                    if (isPedal)
                        ini.WriteValue("gamepad", prefix + "axis_" + axis + "_button", "-1");
                }
            }

            BindIniFeatureSlider(ini, "gamepad", "pad_stick_deadzone", "sm2emu_stickdeadzone", "5");
            BindBoolIniFeatureOn(ini, "gamepad", "pad_rumble", "sm2emu_pad_rumble", "true", "false");
            BindBoolIniFeature(ini, "wheel", "wheel_rumble", "sm2emu_wheel_rumble", "true", "false");
        }

        /// <summary>
        /// Returns the SDL button of each sm2-emu role.
        /// Directions, Start (start), Coin (select) and the gears (L1 down / R1 up) are always mapped, the yml can change them.
        /// Containers of sm2emu.yml are searched in this order (the first one found is used) :
        ///   with a CONTROLLER LAYOUT option : <set>_<layout>, <family>_<layout>, default_<layout>
        ///   then : <set>, <family>, default
        /// Without yml file, the RetroBat default (same as the m2emulator fighting games) is used.
        /// </summary>
        private Dictionary<string, int> GetButtonMapping()
        {
            var roles = new Dictionary<string, int>()
            {
                { "up", 11 }, { "down", 12 }, { "left", 13 }, { "right", 14 },
                { "start", 6 }, { "coin", 4 }, { "gear_up", 10 }, { "gear_down", 9 }
            };

            var physical = LoadYmlButtonMapping();
            if (physical == null)
            {
                physical = new Dictionary<string, List<string>>()
                {
                    { "west", new List<string> { "Button1" } },
                    { "north", new List<string> { "Button2" } },
                    { "south", new List<string> { "Button3" } },
                    { "east", new List<string> { "Button4" } },
                };
            }

            // A role is driven by one button only: the first one listed in the container wins
            var setByYml = new HashSet<string>();

            foreach (var entry in physical)
            {
                int button;
                if (!_sdlPadButtons.TryGetValue(entry.Key, out button))
                    continue;

                foreach (string value in entry.Value)
                {
                    string role;
                    if (!_sm2PadRoles.TryGetValue(value, out role))
                    {
                        SimpleLogger.Instance.Warning("[Sm2Emu] Unknown control '" + value + "' in sm2emu.yml");
                        continue;
                    }

                    if (setByYml.Contains(role))
                    {
                        SimpleLogger.Instance.Warning("[Sm2Emu] '" + value + "' is mapped twice in sm2emu.yml, '" + entry.Key + "' is ignored for this control");
                        continue;
                    }

                    roles[role] = button;
                    setByYml.Add(role);
                }
            }

            return roles;
        }

        private Dictionary<string, List<string>> LoadYmlButtonMapping()
        {
            string ymlFile = Controller.GetSystemYmlMappingFile("sm2-emu", "", "model2", mappingPaths);
            if (string.IsNullOrEmpty(ymlFile))
                return null;

            YmlFile yml;
            try { yml = YmlFile.Load(ymlFile); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Sm2Emu] Unable to read " + ymlFile + " : " + ex.Message);
                return null;
            }

            if (yml == null)
                return null;

            string controllerLayout = SystemConfig["controller_layout"];
            if (controllerLayout == "classic8")
                controllerLayout = "6alternative";

            var bases = new List<string>();
            if (!string.IsNullOrEmpty(_romSet))
                bases.Add(_romSet);
            if (!string.IsNullOrEmpty(_family))
                bases.Add(_family);
            bases.Add("default");
            bases = bases.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            // An arcade panel layout chosen by the user has priority over the gamepad layout of the game
            var searchNames = new List<string>();
            if (!string.IsNullOrEmpty(controllerLayout))
                searchNames.AddRange(bases.Select(b => b + "_" + controllerLayout));
            searchNames.AddRange(bases);

            foreach (string name in searchNames)
            {
                var container = yml.Elements.FirstOrDefault(e => e.Name == name) as YmlContainer;
                if (container == null)
                    continue;

                var ret = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);
                foreach (var element in container.Elements.OfType<YmlElement>())
                {
                    if (string.IsNullOrEmpty(element.Name) || string.IsNullOrEmpty(element.Value) || !_sdlPadButtons.ContainsKey(element.Name))
                        continue;

                    // A controller button can drive several controls : "Button1, Coin"
                    ret[element.Name] = element.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
                }

                SimpleLogger.Instance.Info("[Sm2Emu] Button mapping '" + name + "' loaded from " + ymlFile);
                return ret;
            }

            return null;
        }

        void UpdatePadIni(Controller pad, string iniKey, IniFile ini)
        {
            string path = GetPadPath(pad);

            if (path != null)
                ini.WriteValue("gamepad", iniKey, path);
            else
                ini.Remove("gamepad", iniKey);
        }

        string GetPadPath(Controller pad)
        {
            if (pad == null)
                return null;

            if (!pad.IsKeyboard)
            {
                string sdlPath = pad.Sdl3Controller?.Path ?? pad.SdlController?.Path.Replace("hid", "HID")
                                                                                .Replace("vid", "VID")
                                                                                .Replace("pid", "PID");
                if (!string.IsNullOrEmpty(sdlPath))
                    return sdlPath;
            }

            // Fallback on DevicePath (with formatting VID/PID/HID)
            return pad.DevicePath?.Replace("hid", "HID")
                                 .Replace("vid", "VID")
                                 .Replace("pid", "PID");
        }

        #region Hotkeys
        private void WriteKeyboardHotkeys(IniFile ini)
        {
            ini.WriteValue("system", "key_exit", "Tab");

            if (Hotkeys.GetHotKeysFromFile("sm2-emu", "", out Dictionary<string, HotkeyResult> custoHotkeys) && custoHotkeys.Count > 0)
            {
                foreach (var hotkey in custoHotkeys)
                    ini.WriteValue("system", hotkey.Value.EmulatorKey, hotkey.Value.EmulatorValue);

                _pad2Keyoverride = true;

                return;
            }

            // RetroBat defaults, sm2-emu
            ini.WriteValue("system", "key_quit", "Escape");
            ini.WriteValue("system", "key_overlay", "F1");
            ini.WriteValue("system", "key_fullscreen", "F");
            ini.WriteValue("system", "key_screenshot", "F8");
            ini.WriteValue("system", "key_pause", "P");
            ini.WriteValue("system", "key_quick_save", "F2");
            ini.WriteValue("system", "key_quick_load", "F4");
            ini.WriteValue("system", "key_fast_forward", "L");
            ini.WriteValue("system", "key_rewind", "Backspace");
        }
        #endregion
    }
}