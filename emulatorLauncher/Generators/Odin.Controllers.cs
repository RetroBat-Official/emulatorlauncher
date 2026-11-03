using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class OdinGenerator
    {
        static readonly string[] mappingPaths =
        {
            "{userpath}\\inputmapping\\odin.yml",
            "{systempath}\\resources\\inputmapping\\odin.yml"
        };

        // Controller positions of odin.yml to SDL gamepad inputs (OPTIONS, INPUTS)
        private static readonly Dictionary<string, string> _padInputs = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "south", "a" }, { "east", "b" }, { "west", "x" }, { "north", "y" },
            { "l1", "leftshoulder" }, { "r1", "rightshoulder" }, { "l2", "lefttrigger" }, { "r2", "righttrigger" },
            { "l3", "leftstick" }, { "r3", "rightstick" }, { "select", "back" }, { "start", "start" },
            { "up", "dpup" }, { "down", "dpdown" }, { "left", "dpleft" }, { "right", "dpright" },
            { "leftx", "leftx" }, { "lefty", "lefty" }, { "rightx", "rightx" }, { "righty", "righty" },
            { "leftx-left", "-leftx" }, { "leftx-right", "+leftx" }, { "lefty-up", "-lefty" }, { "lefty-down", "+lefty" },
            { "rightx-left", "-rightx" }, { "rightx-right", "+rightx" }, { "righty-up", "-righty" }, { "righty-down", "+righty" }
        };

        // Controller positions to EmulationStation inputs for joysticks (DirectInput numbering, same as the SDL3 DirectInput driver).
        // The bool asks for the opposite side of the configured direction (right / down halves of the sticks).
        // GetDirectInputMapping reads gamecontrollerdb.txt with the SDL names (x = west, y = north).
        private static readonly Dictionary<string, KeyValuePair<InputKey, bool>> _joyInputs = new Dictionary<string, KeyValuePair<InputKey, bool>>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "south", Pos(InputKey.a) }, { "east", Pos(InputKey.b) }, { "west", Pos(InputKey.x) }, { "north", Pos(InputKey.y) },
            { "l1", Pos(InputKey.pageup) }, { "r1", Pos(InputKey.pagedown) }, { "l2", Pos(InputKey.l2) }, { "r2", Pos(InputKey.r2) },
            { "l3", Pos(InputKey.l3) }, { "r3", Pos(InputKey.r3) }, { "select", Pos(InputKey.select) }, { "start", Pos(InputKey.start) },
            { "up", Pos(InputKey.up) }, { "down", Pos(InputKey.down) }, { "left", Pos(InputKey.left) }, { "right", Pos(InputKey.right) },
            { "leftx", Pos(InputKey.joystick1left) }, { "lefty", Pos(InputKey.joystick1up) }, { "rightx", Pos(InputKey.joystick2left) }, { "righty", Pos(InputKey.joystick2up) },
            { "leftx-left", Pos(InputKey.joystick1left) }, { "leftx-right", Pos(InputKey.joystick1left, true) },
            { "lefty-up", Pos(InputKey.joystick1up) }, { "lefty-down", Pos(InputKey.joystick1up, true) },
            { "rightx-left", Pos(InputKey.joystick2left) }, { "rightx-right", Pos(InputKey.joystick2left, true) },
            { "righty-up", Pos(InputKey.joystick2up) }, { "righty-down", Pos(InputKey.joystick2up, true) }
        };

        private static KeyValuePair<InputKey, bool> Pos(InputKey key, bool opposite = false)
        {
            return new KeyValuePair<InputKey, bool>(key, opposite);
        }

        // Whole stick axes (analog controls : yaw, levers, sight, steering)
        private static readonly string[] _fullAxes = { "leftx", "lefty", "rightx", "righty" };

        /// <summary>
        /// Keyboard and mouse inputs of each control, kept on the lines written by RetroBat (a line replaces all the inputs of a control).
        /// Odin defaults of OPTIONS, with the RetroBat arcade keys : coins 5-6, starts 1-2, Test 9, Service 0.
        /// </summary>
        private static readonly Dictionary<string, string> _keyboardInputs = new Dictionary<string, string>()
        {
            // Every game
            { "coin1", "key:5, key:Keypad 5" }, { "coin2", "key:6, key:Keypad 6" }, { "test", "key:0, key:Keypad 0" }, { "1p_service", "key:9, key:Keypad 9" },
            { "start", "key:1, key:Keypad 1" },
            // airtrix, airtrixo
            { "left", "key:X" }, { "right", "key:C" }, { "up", "key:Up" }, { "down", "key:Down" }, { "yaw", "" }, { "roll", "" },
            // braveff
            { "1p_water", "key:Left Ctrl, mouse:left" }, { "1p_fog", "key:Left Alt, mouse:right" }, { "1p_start", "key:1, key:Keypad 1, mouse:middle" },
            { "1p_sight_x", "mouse:x" }, { "1p_sight_y", "mouse:y" },
            { "2p_water", "key:A, mouse:left" }, { "2p_fog", "key:S, mouse:right" }, { "2p_start", "key:2, key:Keypad 2, mouse:middle" },
            { "2p_sight_x", "mouse:x" }, { "2p_sight_y", "mouse:y" },
            // pharrier
            { "bullet", "key:Left Ctrl" }, { "missile", "key:Left Alt" }, { "bomb", "key:Left Shift" },
            { "vr_l", "key:X" }, { "vr_r", "key:C" }, { "stick_h", "" }, { "stick_v", "" },
            // swracer
            { "boost", "key:C" }, { "brake", "key:X" }, { "left_button", "key:Left" }, { "right_button", "key:Right" },
            { "left_lever", "" }, { "right_lever", "" },
            // sgnascar, sgnascaro
            { "steering_wheel", "" }, { "gas_pedal", "key:Up" }, { "brake_pedal", "key:Down" },
            { "gear_down", "key:X" }, { "gear_up", "key:C" }, { "view_change", "key:Z" },
            // von4
            { "left_lever_up", "key:E" }, { "left_lever_down", "key:D" }, { "left_lever_left", "key:S" }, { "left_lever_right", "key:F" },
            { "left_trigger", "key:Left Ctrl" }, { "left_turbo", "key:Left Alt" },
            { "right_lever_up", "key:I" }, { "right_lever_down", "key:K" }, { "right_lever_left", "key:J" }, { "right_lever_right", "key:L" },
            { "right_trigger", "key:Space" }, { "right_turbo", "key:Left Shift" }, { "pib", "key:Z" },
        };

        private enum OdinDevice { None, Keyboard, Pad, Joystick }

        /// <summary>
        /// [players.<game>] : the gamepad (p1, p2) or joystick (joy1, joy2) of each player by its SDL3 GUID, and the device it plays with.
        /// [controls.<game>] : rewritten at each launch from odin.yml, by controller position for gamepads, by DirectInput number for joysticks.
        /// The wheel of NASCAR Arcade is player 1, from the wheel mapping (Odin.Wheels.cs).
        /// </summary>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Odin");

            string playersSection = "players." + _game;
            string controlsSection = "controls." + _game;
            ini.ClearSection(controlsSection);

            var mapping = LoadYmlMapping();
            var lines = new Dictionary<string, List<string>>();
            var wheel = GetWheelController();

            // Only Brave Firefighters has two players on one cabinet, the other games are linked cabinets
            int players = _game == "braveff" ? 2 : 1;

            for (int player = 1; player <= 2; player++)
            {
                if (player == 1 && wheel != null)
                {
                    WriteWheelDevice(ini, playersSection, wheel);
                    AddWheelLines(lines, wheel);
                    continue;
                }

                var ctrl = player <= players ? Controllers.FirstOrDefault(c => c.PlayerIndex == player && c.Config != null) : null;
                var device = WritePlayerDevice(ini, playersSection, player, ctrl);

                if (player > players)
                    continue;

                foreach (var map in mapping)
                {
                    foreach (string control in map.Value)
                    {
                        string id = GetPlayerControl(control, player);
                        if (id == null)
                            continue;

                        string input = null;
                        if (device == OdinDevice.Pad)
                            input = GetPadInput(map.Key);
                        else if (device == OdinDevice.Joystick)
                            input = GetJoyInput(ctrl, map.Key);

                        if (!lines.ContainsKey(id))
                            lines[id] = new List<string>();

                        if (!string.IsNullOrEmpty(input) && !lines[id].Contains(input))
                            lines[id].Add(input);
                    }
                }
            }

            // Test and Service keep their RetroBat keys without controller (R3 / L3 are set by the yml)
            if (!lines.ContainsKey("test"))
                lines["test"] = new List<string>();
            if (!lines.ContainsKey("1p_service"))
                lines["1p_service"] = new List<string>();

            // Light guns : coin and start of the Wiimotes, the guns aim and fire with the mouse inputs of the lines
            if (_guns.Any(g => g.Type == RawLighGunType.MayFlashWiimote))
            {
                foreach (string id in new[] { "1p_start", "2p_start", "coin1", "coin2" })
                {
                    if (!lines.ContainsKey(id))
                        lines[id] = new List<string>();
                }
            }

            foreach (var line in lines)
            {
                var inputs = new List<string>();

                string keys;
                _keyboardInputs.TryGetValue(line.Key, out keys);
                keys = GetGunInputs(line.Key, keys);

                if (!string.IsNullOrEmpty(keys))
                    inputs.Add(keys);

                inputs.AddRange(line.Value);

                // An empty line would leave the control without any input
                if (inputs.Count > 0)
                    ini.WriteValue(controlsSection, line.Key, string.Join(", ", inputs));
            }

            // No service for player 2 without controller (Odin default : Guide + L3)
            if (!lines.ContainsKey("2p_service"))
                ini.WriteValue(controlsSection, "2p_service", "");

            WriteGunKeyboardLines(ini, controlsSection);
        }

        private static void AddLineInput(Dictionary<string, List<string>> lines, string id, string input)
        {
            if (!lines.ContainsKey(id))
                lines[id] = new List<string>();

            if (!lines[id].Contains(input))
                lines[id].Add(input);
        }

        /// <summary>
        /// Control id of a player : yml values are written for player 1 (1p_...), coin and service are per player.
        /// </summary>
        private static string GetPlayerControl(string control, int player)
        {
            if (control == "coin")
                return "coin" + player;

            if (control == "service")
                return player + "p_service";

            if (player == 1)
                return control;

            return control.StartsWith("1p_") ? "2p_" + control.Substring(3) : null;
        }

        private OdinDevice WritePlayerDevice(IniFile ini, string section, int player, Controller ctrl)
        {
            string p = player.ToString();

            ini.Remove(section, "p" + p);
            ini.Remove(section, "joy" + p);
            ini.Remove(section, "device" + p);

            if (ctrl == null)
                return OdinDevice.None;

            if (ctrl.IsKeyboard)
            {
                ini.WriteValue(section, "device" + p, "keyboard");
                return OdinDevice.Keyboard;
            }

            var sdl3 = ctrl.Sdl3Controller;
            bool isPad;
            string guid = null;
            string name = ctrl.Name;

            if (sdl3 != null)
            {
                isPad = sdl3.IsGamepad;
                guid = sdl3.GuidString;
                if (!string.IsNullOrEmpty(sdl3.Name))
                    name = sdl3.Name;
            }
            else
            {
                // SDL3.dll missing or device not found : no GUID, Odin takes the first free device
                isPad = ctrl.IsXInputDevice || ctrl.SdlController != null;
                SimpleLogger.Instance.Warning("[Odin] No SDL3 device found for " + ctrl.Name + ", player " + p + " is not pinned.");
            }

            // Two identical gamepads share their GUID : Odin seats them in its SDL3 order, not the RetroBat one
            if (sdl3 != null && Controllers.Any(c => c != ctrl && c.Sdl3Controller != null && c.Sdl3Controller.GuidString == guid))
                SimpleLogger.Instance.Warning("[Odin] Several devices share the GUID " + guid + " : player order follows the SDL3 order.");

            if (!string.IsNullOrEmpty(guid) && guid != new string('0', 32))
                ini.WriteValue(section, (isPad ? "p" : "joy") + p, guid + " " + name);

            ini.WriteValue(section, "device" + p, isPad ? "pad" : "joystick");

            SimpleLogger.Instance.Info("[Odin] Player " + p + " : " + name + " (" + (isPad ? "gamepad" : "joystick") + ")");
            return isPad ? OdinDevice.Pad : OdinDevice.Joystick;
        }

        private static string GetPadInput(string position)
        {
            string ret;
            return _padInputs.TryGetValue(position, out ret) ? "pad:" + ret : null;
        }

        /// <summary>
        /// Joystick input of a controller position : DirectInput number from es_input.cfg and gamecontrollerdb.txt (GetDirectInputMapping).
        /// </summary>
        private static string GetJoyInput(Controller ctrl, string position)
        {
            KeyValuePair<InputKey, bool> pos;
            if (ctrl == null || ctrl.Config == null || !_joyInputs.TryGetValue(position, out pos))
                return null;

            var input = ctrl.GetDirectInputMapping(pos.Key);
            if (input == null)
                return null;

            // Without gamecontrollerdb.txt entry, the es_input.cfg names are used (SNES naming : x = north, y = west)
            if ((ctrl.dinputCtrl == null || ctrl.dinputCtrl.ButtonMappings == null) && (pos.Key == InputKey.x || pos.Key == InputKey.y))
            {
                input = ctrl.GetDirectInputMapping(pos.Key == InputKey.x ? InputKey.y : InputKey.x);
                if (input == null)
                    return null;
            }

            switch (input.Type)
            {
                case "button":
                    return "joy:button" + input.Id;

                case "hat":
                    long dir = input.Value;
                    if (pos.Value)
                        dir = dir == 1 ? 4 : dir == 4 ? 1 : dir == 2 ? 8 : 2;

                    string way = dir == 1 ? "_up" : dir == 2 ? "_right" : dir == 4 ? "_down" : dir == 8 ? "_left" : null;
                    return way == null ? null : "joy:hat" + input.Id + way;

                case "axis":
                    // Left and up are configured as the negative side of the axis
                    if (_fullAxes.Contains(position, StringComparer.InvariantCultureIgnoreCase))
                        return "joy:axis" + input.Id + (input.Value > 0 ? ":invert" : "");

                    bool positive = input.Value > 0;
                    if (pos.Value)
                        positive = !positive;
                    return "joy:" + (positive ? "+" : "-") + "axis" + input.Id;
            }

            return null;
        }

        /// <summary>
        /// Container of odin.yml : <set>_<layout>, <set>, <parent>_<layout>, <parent>, default_<layout>, default.
        /// Key : controller position. Value : Odin controls of player 1, separated by commas (coin and service are per player).
        /// </summary>
        private Dictionary<string, List<string>> LoadYmlMapping()
        {
            var ret = new Dictionary<string, List<string>>(StringComparer.InvariantCultureIgnoreCase);

            string ymlFile = Controller.GetSystemYmlMappingFile("odin", "", "odin", mappingPaths);
            if (string.IsNullOrEmpty(ymlFile))
            {
                SimpleLogger.Instance.Warning("[Odin] odin.yml not found, Odin default controls are used.");
                return ret;
            }

            YmlFile yml;
            try { yml = YmlFile.Load(ymlFile); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Odin] Unable to read " + ymlFile + " : " + ex.Message);
                return ret;
            }

            string layout = SystemConfig["controller_layout"];
            if (layout == "classic8")
                layout = "6alternative";

            var bases = new List<string> { _game };
            string parent;
            if (_parents.TryGetValue(_game, out parent))
                bases.Add(parent);
            bases.Add("default");

            foreach (string b in bases)
            {
                foreach (string name in string.IsNullOrEmpty(layout) ? new[] { b } : new[] { b + "_" + layout, b })
                {
                    var container = yml.Elements.FirstOrDefault(e => e.Name == name) as YmlContainer;
                    if (container == null)
                        continue;

                    foreach (var element in container.Elements.OfType<YmlElement>())
                    {
                        if (string.IsNullOrEmpty(element.Name) || string.IsNullOrEmpty(element.Value) || !_padInputs.ContainsKey(element.Name))
                            continue;

                        // A controller position can drive several controls : "r2: gas_pedal, view_change"
                        ret[element.Name] = element.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
                    }

                    SimpleLogger.Instance.Info("[Odin] Controls '" + name + "' loaded from " + ymlFile);
                    return ret;
                }
            }

            return ret;
        }

        // Versions of a game using the controls of their parent (odin-games.json)
        private static readonly Dictionary<string, string> _parents = new Dictionary<string, string>()
        {
            { "airtrixo", "airtrix" },
            { "sgnascaro", "sgnascar" },
        };
    }
}