using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Model3VulkanGenerator
    {
        // "sdlgamepad" : SDL gamepad API, standard button numbering, devices SDL does not know as gamepads are skipped by the emulator.
        // "sdl" : raw SDL joysticks (every device, native numbering), needed for wheels and arcade encoders.
        private bool _rawInputSystem = false;

        static readonly string[] mappingPaths =
        {
            "{userpath}\\inputmapping\\model3vulkan.yml",
            "{systempath}\\resources\\inputmapping\\model3vulkan.yml",
            "{userpath}\\inputmapping\\supermodel.yml",
            "{systempath}\\resources\\inputmapping\\supermodel.yml"
        };

        /// <summary>
        /// Controller positions used in the yml files (JOY_<position>) to the "sdlgamepad" bindings of Model3-Vulkan.
        /// BUTTON1 south, BUTTON2 east, BUTTON3 west, BUTTON4 north, BUTTON5 L1, BUTTON6 R1, BUTTON7 back, BUTTON8 start,
        /// BUTTON9 L3, BUTTON10 R3, BUTTON13 guide, ZAXIS left trigger, RZAXIS right trigger, POV1 d-pad.
        /// SDL3 buttons are positional : no swap is needed for Nintendo controllers.
        /// </summary>
        private static readonly Dictionary<string, string> _gamepadPositions = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "south", "BUTTON1" }, { "east", "BUTTON2" }, { "west", "BUTTON3" }, { "north", "BUTTON4" },
            { "leftshoulder", "BUTTON5" }, { "rightshoulder", "BUTTON6" }, { "l1", "BUTTON5" }, { "r1", "BUTTON6" },
            { "back", "BUTTON7" }, { "select", "BUTTON7" }, { "start", "BUTTON8" }, { "guide", "BUTTON13" },
            { "leftstick", "BUTTON9" }, { "rightstick", "BUTTON10" }, { "l3", "BUTTON9" }, { "r3", "BUTTON10" },
            { "lefttrigger", "ZAXIS_POS" }, { "righttrigger", "RZAXIS_POS" }, { "l2", "ZAXIS_POS" }, { "r2", "RZAXIS_POS" },
            { "dpup", "POV1_UP" }, { "dpdown", "POV1_DOWN" }, { "dpleft", "POV1_LEFT" }, { "dpright", "POV1_RIGHT" },
            { "up", "POV1_UP" }, { "down", "POV1_DOWN" }, { "left", "POV1_LEFT" }, { "right", "POV1_RIGHT" },
            { "leftx", "XAXIS" }, { "lefty", "YAXIS" }, { "rightx", "RXAXIS" }, { "righty", "RYAXIS" },
            { "leftx-left", "XAXIS_NEG" }, { "leftx-right", "XAXIS_POS" }, { "lefty-up", "YAXIS_NEG" }, { "lefty-down", "YAXIS_POS" },
            { "rightx-left", "RXAXIS_NEG" }, { "rightx-right", "RXAXIS_POS" }, { "righty-up", "RYAXIS_NEG" }, { "righty-down", "RYAXIS_POS" }
        };

        /// <summary>
        /// Controller positions to the EmulationStation inputs, for the raw "sdl" input system (native button numbering, read from es_input.cfg).
        /// The bool tells if the opposite direction of the configured one is wanted (right / down halves of the sticks).
        /// </summary>
        private static readonly Dictionary<string, KeyValuePair<InputKey, bool>> _rawPositions = new Dictionary<string, KeyValuePair<InputKey, bool>>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "south", Pos(InputKey.a) }, { "east", Pos(InputKey.b) }, { "west", Pos(InputKey.y) }, { "north", Pos(InputKey.x) },
            { "leftshoulder", Pos(InputKey.pageup) }, { "rightshoulder", Pos(InputKey.pagedown) }, { "l1", Pos(InputKey.pageup) }, { "r1", Pos(InputKey.pagedown) },
            { "back", Pos(InputKey.select) }, { "select", Pos(InputKey.select) }, { "start", Pos(InputKey.start) }, { "guide", Pos(InputKey.hotkey) },
            { "leftstick", Pos(InputKey.l3) }, { "rightstick", Pos(InputKey.r3) }, { "l3", Pos(InputKey.l3) }, { "r3", Pos(InputKey.r3) },
            { "lefttrigger", Pos(InputKey.l2) }, { "righttrigger", Pos(InputKey.r2) }, { "l2", Pos(InputKey.l2) }, { "r2", Pos(InputKey.r2) },
            { "dpup", Pos(InputKey.up) }, { "dpdown", Pos(InputKey.down) }, { "dpleft", Pos(InputKey.left) }, { "dpright", Pos(InputKey.right) },
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

        // Stick axes are read as a full axis (no half) for these positions
        private static readonly string[] _fullAxisPositions = new string[] { "leftx", "lefty", "rightx", "righty" };

        /// <summary>
        /// Writes every game input of the [ Global ] section : RetroBat default layout, then the game container of the yml file.
        /// Inputs not mapped are set to NONE, so that the emulator defaults (JOY1 / keyboard) never add a second device.
        /// </summary>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Model3-Vulkan");

            // Game sections ([ daytona2 ]...) written by -config-inputs would replace the RetroBat inputs
            foreach (var section in ini.EnumerateSections().Where(s => s != GlobalSection).ToList())
            {
                foreach (var key in ini.EnumerateKeys(section).Where(k => k.StartsWith("Input", StringComparison.InvariantCultureIgnoreCase)).ToList())
                    ini.Remove(section, key);
            }

            DefineGuns(ini);

            var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            var c2 = Controllers.FirstOrDefault(c => c.PlayerIndex == 2);

            // Wheel : player 1 uses the wheel, read as a raw joystick
            Controller wheel = GetWheelController();
            if (wheel != null)
            {
                c1 = wheel;
                if (c2 == wheel)
                    c2 = null;
            }

            Dictionary<string, string> mapping;

            if (c1 == null || c1.IsKeyboard)
            {
                mapping = GetKeyboardMapping();
                c1 = null;
            }
            else
            {
                mapping = GetDefaultPadMapping();

                var ymlMapping = LoadYmlMapping();
                if (ymlMapping != null)
                {
                    foreach (var entry in ymlMapping)
                        mapping[entry.Key] = entry.Value;
                }
            }

            if (c2 != null && c2.IsKeyboard)
                c2 = null;

            // Input system : the gamepad API when SDL knows the pads, otherwise the raw joysticks
            _rawInputSystem = wheel != null || !IsSdlGamepad(c1) || !IsSdlGamepad(c2);
            if (SystemConfig.isOptSet("m3v_inputdriver") && !string.IsNullOrEmpty(SystemConfig["m3v_inputdriver"]) && wheel == null)
                _rawInputSystem = SystemConfig["m3v_inputdriver"] == "sdl";

            ini.WriteValue(GlobalSection, "InputSystem", _rawInputSystem ? "sdl" : "sdlgamepad");
            foreach (var section in ini.EnumerateSections().Where(s => s != GlobalSection).ToList())
                ini.Remove(section, "InputSystem");

            int j1 = GetJoyNumber(c1, 1);
            int j2 = GetJoyNumber(c2, 2);

            SimpleLogger.Instance.Info("[INFO] Model3-Vulkan input system " + (_rawInputSystem ? "sdl" : "sdlgamepad") + ", player 1 : " + (c1 != null ? c1.Name + " (JOY" + j1 + ")" : "keyboard") + ", player 2 : " + (c2 != null ? c2.Name + " (JOY" + j2 + ")" : "none"));

            // Wheel axes and buttons from model3vulkan_wheels.yml / model3_wheels.yml
            if (wheel != null)
                ConfigureWheel(ini, mapping, wheel, j1);

            foreach (string key in inputValues)
            {
                string value;
                if (!mapping.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
                    value = "NONE";

                bool player2 = p2values.Contains(key);
                string resolved = ResolveMapping(key, value, player2 ? c2 : c1, player2 ? j2 : j1, player2);
                ini.WriteValue(GlobalSection, key, "\"" + resolved + "\"");
            }

            // Light guns
            ConfigureGuns(ini);

            // Deadzones - 5% by default
            string deadzone = SystemConfig.isOptSet("supermodel_joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["supermodel_joy_deadzone"]) ? SystemConfig["supermodel_joy_deadzone"].ToIntegerString() : "5";
            foreach (int joy in new int[] { j1, j2 }.Where(j => j > 0).Distinct())
            {
                foreach (string axis in new string[] { "X", "Y", "RX", "RY" })
                    ini.WriteValue(GlobalSection, "InputJoy" + joy + axis + "DeadZone", deadzone);
            }

            // Force feedback
            if (wheel != null)
                ini.WriteValue(GlobalSection, "ForceFeedback", SystemConfig.isOptSet("forceFeedback") && !SystemConfig.getOptBoolean("forceFeedback") ? "0" : "1");
            else
                ini.WriteValue(GlobalSection, "ForceFeedback", SystemConfig.getOptBoolean("forceFeedback") ? "1" : "0");
        }

        /// <summary>
        /// Converts the comma separated tokens of a mapping (yml syntax) to Model3-Vulkan bindings.
        /// KEY_xxx are kept, MOUSE_xxx use the mouse of the player gun, JOY_<position> use the player controller.
        /// </summary>
        private string ResolveMapping(string key, string value, Controller ctrl, int joyNumber, bool player2)
        {
            var result = new List<string>();

            foreach (string rawToken in value.Split(','))
            {
                string token = rawToken.Trim().Trim('"');
                if (token.Length == 0 || token.Equals("NONE", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (token.StartsWith("KEY", StringComparison.InvariantCultureIgnoreCase))
                {
                    result.Add(token);
                }
                else if (token.StartsWith("MOUSE", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Without a second gun, player 2 would aim with the same mouse as player 1
                    if (player2 && !_multigun)
                        continue;

                    int sep = token.IndexOf('_');
                    if (sep < 0)
                        continue;

                    result.Add((player2 ? _mouse2 : _mouse1) + token.Substring(sep));
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(token, @"^JOY[0-9]+_", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    result.Add(token.ToUpperInvariant());
                }
                else if (token.StartsWith("JOY", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (ctrl == null || joyNumber < 1)
                        continue;

                    int sep = token.IndexOf('_');
                    if (sep < 0)
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Wrong value '" + token + "' for " + key);
                        continue;
                    }

                    string part = token.Substring(sep + 1);
                    bool invert = part.EndsWith("_inv", StringComparison.InvariantCultureIgnoreCase);
                    if (invert)
                        part = part.Substring(0, part.Length - 4);

                    string binding = _rawInputSystem ? GetRawBinding(ctrl, part) : GetGamepadBinding(part);
                    if (binding == null)
                    {
                        if (!_gamepadPositions.ContainsKey(part))
                            binding = part.ToUpperInvariant();
                        else
                            continue;
                    }

                    // Inversion of a full axis (an axis already inverted in es_input.cfg goes back to normal)
                    if (invert && !binding.EndsWith("_POS") && !binding.EndsWith("_NEG"))
                        binding = binding.EndsWith("_INV") ? binding.Substring(0, binding.Length - 4) : binding + "_INV";

                    result.Add("JOY" + joyNumber + "_" + binding);
                }
                else
                    result.Add(token);
            }

            return result.Count > 0 ? string.Join(",", result.Distinct()) : "NONE";
        }

        private static string GetGamepadBinding(string position)
        {
            string ret;
            return _gamepadPositions.TryGetValue(position, out ret) ? ret : null;
        }

        /// <summary>
        /// Raw SDL joystick binding of a controller position, from es_input.cfg (SDL joystick numbering).
        /// </summary>
        private static string GetRawBinding(Controller ctrl, string position)
        {
            KeyValuePair<InputKey, bool> pos;
            if (ctrl == null || ctrl.Config == null || !_rawPositions.TryGetValue(position, out pos))
                return null;

            var input = ctrl.Config[pos.Key];
            if (input == null)
                return null;

            switch (input.Type)
            {
                case "button":
                    return "BUTTON" + (input.Id + 1);

                case "hat":
                    string dir = input.Value == 1 ? "UP" : input.Value == 2 ? "RIGHT" : input.Value == 4 ? "DOWN" : input.Value == 8 ? "LEFT" : null;
                    if (dir == null)
                        return null;
                    if (pos.Value)
                        dir = dir == "UP" ? "DOWN" : dir == "DOWN" ? "UP" : dir == "LEFT" ? "RIGHT" : "LEFT";
                    return "POV" + (input.Id + 1) + "_" + dir;

                case "axis":
                    string axis = "AXIS" + (input.Id + 1);
                    if (_fullAxisPositions.Contains(position, StringComparer.InvariantCultureIgnoreCase))
                        return input.Value > 0 ? axis + "_INV" : axis;     // left / up are configured as the negative side

                    bool positive = input.Value > 0;
                    if (pos.Value)
                        positive = !positive;
                    return axis + (positive ? "_POS" : "_NEG");
            }

            return null;
        }

        private int GetJoyNumber(Controller ctrl, int player)
        {
            string forced = player == 1 ? SystemConfig["model3_p1index"] : SystemConfig["model3_p2index"];
            if (player == 1 && _rawInputSystem && GetWheelController() != null && SystemConfig.isOptSet("wheel_index") && !string.IsNullOrEmpty(SystemConfig["wheel_index"]))
                forced = SystemConfig["wheel_index"];

            if (!string.IsNullOrEmpty(forced) && forced.ToInteger() > 0)
            {
                SimpleLogger.Instance.Info("[INFO] Forcing joystick index of player " + player + " to " + forced);
                return forced.ToInteger();
            }

            if (ctrl == null)
                return -1;

            var sdl3 = ctrl.Sdl3Controller;
            if (sdl3 != null)
            {
                if (_rawInputSystem)
                    return sdl3.EnumerationIndex + 1;

                return Sdl3GameController.GetControllers().Count(s => s.IsGamepad && s.EnumerationIndex < sdl3.EnumerationIndex) + 1;
            }

            // SDL3.dll missing : the SDL2 order is the closest one
            SimpleLogger.Instance.Warning("[WARNING] No SDL3 device found for " + ctrl.Name + ", using the SDL2 index.");
            return (ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex) + 1;
        }

        private static bool IsSdlGamepad(Controller ctrl)
        {
            if (ctrl == null || ctrl.IsKeyboard)
                return true;

            var sdl3 = ctrl.Sdl3Controller;
            return sdl3 == null || sdl3.IsGamepad;
        }

        /// <summary>
        /// Container of the yml file : <rom>_<layout>, <rom>, longest container starting the rom name (clones), default_<layout>, default.
        /// </summary>
        private Dictionary<string, string> LoadYmlMapping()
        {
            string ymlFile = Controller.GetSystemYmlMappingFile("model3-vulkan", "", "model3", mappingPaths);
            if (string.IsNullOrEmpty(ymlFile))
                return null;

            YmlFile yml;
            try { yml = YmlFile.Load(ymlFile); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Unable to read " + ymlFile + " : " + ex.Message);
                return null;
            }

            if (yml == null)
                return null;

            string layout = SystemConfig["controller_layout"];
            var containers = yml.Elements.OfType<YmlContainer>().ToList();

            Func<string, YmlContainer> find = name => containers.FirstOrDefault(c => c.Name == name);

            YmlContainer game = null;
            if (!string.IsNullOrEmpty(layout))
                game = find(_romName + "_" + layout);
            if (game == null)
                game = find(_romName);

            if (game == null)
            {
                // Clones : longest container name the rom name starts with (scud for scuda, vf3 for vf3a...)
                var parent = containers.Where(c => !c.Name.StartsWith("default") && _romName.StartsWith(c.Name)).OrderByDescending(c => c.Name.Length).FirstOrDefault();
                if (parent != null)
                {
                    if (!string.IsNullOrEmpty(layout))
                        game = find(parent.Name + "_" + layout);
                    if (game == null)
                        game = parent;
                }
            }

            if (game == null && !string.IsNullOrEmpty(layout))
                game = find("default_" + layout);
            if (game == null)
                game = find("default");

            if (game == null)
                return null;

            SimpleLogger.Instance.Info("[INPUT] Mapping '" + game.Name + "' loaded from " + ymlFile);

            var ret = new Dictionary<string, string>();
            foreach (var element in game.Elements.OfType<YmlElement>())
            {
                if (!string.IsNullOrEmpty(element.Name) && element.Value != null)
                    ret[element.Name] = element.Value;
            }

            return ret;
        }

        /// <summary>
        /// RetroBat gamepad layout (same as the supermodel emulator) : Start / Select = start / coin, L3 / R3 = service / test (option),
        /// keys 1-2 start, 5-6 coin, 9 test, 0 service.
        /// </summary>
        private Dictionary<string, string> GetDefaultPadMapping()
        {
            bool service = SystemConfig.getOptBoolean("m3_service");
            bool shoulders = SystemConfig.getOptBoolean("model3_racingshoulder");

            var map = new Dictionary<string, string>
            {
                { "InputStart1", "KEY_1,JOY_start" },
                { "InputCoin1", "KEY_5,JOY_select" },
                { "InputStart2", "KEY_2,JOY_start" },
                { "InputCoin2", "KEY_6,JOY_select" },
                { "InputServiceA", service ? "KEY_0,JOY_leftstick" : "KEY_0" },
                { "InputTestA", service ? "KEY_9,JOY_rightstick" : "KEY_9" },

                // 4-way digital joysticks
                { "InputJoyUp", "JOY_lefty-up,JOY_dpup" },
                { "InputJoyDown", "JOY_lefty-down,JOY_dpdown" },
                { "InputJoyLeft", "JOY_leftx-left,JOY_dpleft" },
                { "InputJoyRight", "JOY_leftx-right,JOY_dpright" },
                { "InputJoyUp2", "JOY_lefty-up,JOY_dpup" },
                { "InputJoyDown2", "JOY_lefty-down,JOY_dpdown" },
                { "InputJoyLeft2", "JOY_leftx-left,JOY_dpleft" },
                { "InputJoyRight2", "JOY_leftx-right,JOY_dpright" },

                // Fighting games (Virtua Fighter 3, Fighting Vipers 2)
                { "InputPunch", "JOY_west" },
                { "InputKick", "JOY_north" },
                { "InputGuard", "JOY_south" },
                { "InputEscape", "JOY_east" },
                { "InputPunch2", "JOY_west" },
                { "InputKick2", "JOY_north" },
                { "InputGuard2", "JOY_south" },
                { "InputEscape2", "JOY_east" },

                // Spikeout
                { "InputShift", "JOY_east,JOY_rightshoulder" },
                { "InputBeat", "JOY_south" },
                { "InputCharge", "JOY_west" },
                { "InputJump", "JOY_north" },

                // Virtua Striker
                { "InputShortPass", "JOY_west" },
                { "InputLongPass", "JOY_south" },
                { "InputShoot", "JOY_east" },
                { "InputShortPass2", "JOY_west" },
                { "InputLongPass2", "JOY_south" },
                { "InputShoot2", "JOY_east" },

                // Driving games
                { "InputSteering", "JOY_leftx" },
                { "InputAccelerator", shoulders ? "JOY_rightshoulder" : "JOY_righttrigger" },
                { "InputBrake", shoulders ? "JOY_leftshoulder" : "JOY_lefttrigger" },
                { "InputGearShiftUp", shoulders ? "JOY_dpup" : "JOY_rightshoulder" },
                { "InputGearShiftDown", shoulders ? "JOY_dpdown" : "JOY_leftshoulder" },
                { "InputGearShift1", shoulders ? "JOY_dpup" : "JOY_righty-up" },
                { "InputGearShift2", shoulders ? "JOY_dpdown" : "JOY_righty-down" },
                { "InputGearShift3", shoulders ? "JOY_dpleft" : "JOY_rightx-left" },
                { "InputGearShift4", shoulders ? "JOY_dpright" : "JOY_rightx-right" },
                { "InputVR1", "JOY_north" },
                { "InputVR2", "JOY_south" },
                { "InputVR3", "JOY_west" },
                { "InputVR4", "JOY_east" },
                { "InputViewChange", "JOY_north" },
                { "InputHandBrake", "JOY_south" },
                { "InputRearBrake", "JOY_south" },
                { "InputMusicSelect", "JOY_east" },

                // Virtual On
                { "InputTwinJoyTurnLeft", "JOY_rightx-left" },
                { "InputTwinJoyTurnRight", "JOY_rightx-right" },
                { "InputTwinJoyForward", "JOY_lefty-up" },
                { "InputTwinJoyReverse", "JOY_lefty-down" },
                { "InputTwinJoyStrafeLeft", "JOY_leftx-left" },
                { "InputTwinJoyStrafeRight", "JOY_leftx-right" },
                { "InputTwinJoyJump", "JOY_north" },
                { "InputTwinJoyCrouch", "JOY_south" },
                { "InputTwinJoyShot1", "JOY_lefttrigger" },
                { "InputTwinJoyShot2", "JOY_righttrigger" },
                { "InputTwinJoyTurbo1", "JOY_west,JOY_leftshoulder" },
                { "InputTwinJoyTurbo2", "JOY_east,JOY_rightshoulder" },

                // Analog joystick (Star Wars Trilogy)
                { "InputAnalogJoyX", "JOY_leftx_inv,MOUSE_XAXIS_INV" },
                { "InputAnalogJoyY", "JOY_lefty_inv,MOUSE_YAXIS" },
                { "InputAnalogJoyTrigger", "JOY_righttrigger,JOY_west,MOUSE_LEFT_BUTTON" },
                { "InputAnalogJoyEvent", "JOY_south,MOUSE_RIGHT_BUTTON" },

                // Light guns (The Lost World) and analog guns (Ocean Hunter, LA Machineguns) : mouse / light gun
                { "InputGunX", "MOUSE_XAXIS" },
                { "InputGunY", "MOUSE_YAXIS" },
                { "InputTrigger", "MOUSE_LEFT_BUTTON" },
                { "InputOffscreen", "MOUSE_RIGHT_BUTTON" },
                { "InputGunX2", "MOUSE_XAXIS" },
                { "InputGunY2", "MOUSE_YAXIS" },
                { "InputTrigger2", "MOUSE_LEFT_BUTTON" },
                { "InputOffscreen2", "MOUSE_RIGHT_BUTTON" },
                { "InputAnalogGunX", "MOUSE_XAXIS" },
                { "InputAnalogGunY", "MOUSE_YAXIS" },
                { "InputAnalogTriggerLeft", "MOUSE_LEFT_BUTTON" },
                { "InputAnalogTriggerRight", "MOUSE_RIGHT_BUTTON" },
                { "InputAnalogGunX2", "MOUSE_XAXIS" },
                { "InputAnalogGunY2", "MOUSE_YAXIS" },
                { "InputAnalogTriggerLeft2", "MOUSE_LEFT_BUTTON" },
                { "InputAnalogTriggerRight2", "MOUSE_RIGHT_BUTTON" },

                // Ski Champ
                { "InputSkiX", "JOY_leftx" },
                { "InputSkiY", "JOY_rightx" },
                { "InputSkiPollLeft", "JOY_lefttrigger" },
                { "InputSkiPollRight", "JOY_righttrigger" },
                { "InputSkiSelect1", "JOY_west" },
                { "InputSkiSelect2", "JOY_south" },
                { "InputSkiSelect3", "JOY_east" },

                // Magical Truck Adventure
                { "InputMagicalLever1", "JOY_lefty" },
                { "InputMagicalPedal1", "JOY_south" },
                { "InputMagicalLever2", "JOY_lefty" },
                { "InputMagicalPedal2", "JOY_south" },

                // Sega Bass Fishing / Get Bass
                { "InputFishingRodX", "JOY_leftx" },
                { "InputFishingRodY", "JOY_lefty" },
                { "InputFishingStickX", "JOY_rightx" },
                { "InputFishingStickY", "JOY_righty" },
                { "InputFishingReel", "JOY_righttrigger" },
                { "InputFishingCast", "JOY_south" },
                { "InputFishingSelect", "JOY_east" }
            };

            AddGunAutoTrigger(map);
            return map;
        }

        /// <summary>
        /// Keyboard layout when player 1 has no controller (same as the supermodel emulator).
        /// </summary>
        private Dictionary<string, string> GetKeyboardMapping()
        {
            var map = new Dictionary<string, string>
            {
                { "InputStart1", "KEY_1" }, { "InputStart2", "KEY_2" }, { "InputCoin1", "KEY_5" }, { "InputCoin2", "KEY_6" },
                { "InputServiceA", "KEY_0" }, { "InputTestA", "KEY_9" },
                { "InputJoyUp", "KEY_UP" }, { "InputJoyDown", "KEY_DOWN" }, { "InputJoyLeft", "KEY_LEFT" }, { "InputJoyRight", "KEY_RIGHT" },
                { "InputPunch", "KEY_A" }, { "InputKick", "KEY_S" }, { "InputGuard", "KEY_D" }, { "InputEscape", "KEY_F" },
                { "InputShift", "KEY_A" }, { "InputBeat", "KEY_S" }, { "InputCharge", "KEY_D" }, { "InputJump", "KEY_F" },
                { "InputShortPass", "KEY_A" }, { "InputLongPass", "KEY_S" }, { "InputShoot", "KEY_D" },
                { "InputSteeringLeft", "KEY_LEFT" }, { "InputSteeringRight", "KEY_RIGHT" },
                { "InputAccelerator", "KEY_UP" }, { "InputBrake", "KEY_DOWN" },
                { "InputGearShiftUp", "KEY_Y" }, { "InputGearShiftDown", "KEY_H" },
                { "InputGearShift1", "KEY_Q" }, { "InputGearShift2", "KEY_W" }, { "InputGearShift3", "KEY_E" }, { "InputGearShift4", "KEY_R" }, { "InputGearShiftN", "KEY_T" },
                { "InputVR1", "KEY_A" }, { "InputVR2", "KEY_S" }, { "InputVR3", "KEY_D" }, { "InputVR4", "KEY_F" },
                { "InputViewChange", "KEY_A" }, { "InputHandBrake", "KEY_S" }, { "InputRearBrake", "KEY_S" }, { "InputMusicSelect", "KEY_D" },
                { "InputTwinJoyTurnLeft", "KEY_Q" }, { "InputTwinJoyTurnRight", "KEY_W" }, { "InputTwinJoyForward", "KEY_UP" }, { "InputTwinJoyReverse", "KEY_DOWN" },
                { "InputTwinJoyStrafeLeft", "KEY_LEFT" }, { "InputTwinJoyStrafeRight", "KEY_RIGHT" }, { "InputTwinJoyJump", "KEY_E" }, { "InputTwinJoyCrouch", "KEY_R" },
                { "InputTwinJoyShot1", "KEY_A" }, { "InputTwinJoyShot2", "KEY_S" }, { "InputTwinJoyTurbo1", "KEY_Z" }, { "InputTwinJoyTurbo2", "KEY_X" },
                { "InputAnalogJoyLeft", "KEY_LEFT" }, { "InputAnalogJoyRight", "KEY_RIGHT" }, { "InputAnalogJoyUp", "KEY_UP" }, { "InputAnalogJoyDown", "KEY_DOWN" },
                { "InputAnalogJoyX", "MOUSE_XAXIS_INV" }, { "InputAnalogJoyY", "MOUSE_YAXIS" },
                { "InputAnalogJoyTrigger", "KEY_A,MOUSE_LEFT_BUTTON" }, { "InputAnalogJoyEvent", "KEY_S,MOUSE_RIGHT_BUTTON" }, { "InputAnalogJoyTrigger2", "KEY_D" },
                { "InputGunLeft", "KEY_LEFT" }, { "InputGunRight", "KEY_RIGHT" }, { "InputGunUp", "KEY_UP" }, { "InputGunDown", "KEY_DOWN" },
                { "InputGunX", "MOUSE_XAXIS" }, { "InputGunY", "MOUSE_YAXIS" }, { "InputTrigger", "KEY_A,MOUSE_LEFT_BUTTON" }, { "InputOffscreen", "KEY_S,MOUSE_RIGHT_BUTTON" },
                { "InputGunX2", "MOUSE_XAXIS" }, { "InputGunY2", "MOUSE_YAXIS" }, { "InputTrigger2", "MOUSE_LEFT_BUTTON" }, { "InputOffscreen2", "MOUSE_RIGHT_BUTTON" },
                { "InputAnalogGunLeft", "KEY_LEFT" }, { "InputAnalogGunRight", "KEY_RIGHT" }, { "InputAnalogGunUp", "KEY_UP" }, { "InputAnalogGunDown", "KEY_DOWN" },
                { "InputAnalogGunX", "MOUSE_XAXIS" }, { "InputAnalogGunY", "MOUSE_YAXIS" },
                { "InputAnalogTriggerLeft", "KEY_A,MOUSE_LEFT_BUTTON" }, { "InputAnalogTriggerRight", "KEY_S,MOUSE_RIGHT_BUTTON" },
                { "InputAnalogGunX2", "MOUSE_XAXIS" }, { "InputAnalogGunY2", "MOUSE_YAXIS" },
                { "InputAnalogTriggerLeft2", "MOUSE_LEFT_BUTTON" }, { "InputAnalogTriggerRight2", "MOUSE_RIGHT_BUTTON" },
                { "InputSkiLeft", "KEY_LEFT" }, { "InputSkiRight", "KEY_RIGHT" }, { "InputSkiUp", "KEY_UP" }, { "InputSkiDown", "KEY_DOWN" },
                { "InputSkiPollLeft", "KEY_A" }, { "InputSkiPollRight", "KEY_S" }, { "InputSkiSelect1", "KEY_Q" }, { "InputSkiSelect2", "KEY_W" }, { "InputSkiSelect3", "KEY_E" },
                { "InputMagicalLeverUp1", "KEY_UP" }, { "InputMagicalLeverDown1", "KEY_DOWN" }, { "InputMagicalPedal1", "KEY_A" }, { "InputMagicalPedal2", "KEY_S" },
                { "InputFishingRodLeft", "KEY_LEFT" }, { "InputFishingRodRight", "KEY_RIGHT" }, { "InputFishingRodUp", "KEY_UP" }, { "InputFishingRodDown", "KEY_DOWN" },
                { "InputFishingStickLeft", "KEY_A" }, { "InputFishingStickRight", "KEY_D" }, { "InputFishingStickUp", "KEY_W" }, { "InputFishingStickDown", "KEY_S" },
                { "InputFishingReel", "KEY_SPACE" }, { "InputFishingCast", "KEY_Z" }, { "InputFishingSelect", "KEY_X" }, { "InputFishingTension", "KEY_T" }
            };

            AddGunAutoTrigger(map);
            return map;
        }

        // Pointing off-screen reloads automatically when the trigger is pulled
        private static void AddGunAutoTrigger(Dictionary<string, string> map)
        {
            map["InputAutoTrigger"] = "1";
            map["InputAutoTrigger2"] = "1";
        }

        #region Hotkeys
        /// <summary>
        /// RetroBat keyboard hotkeys (keys sent by the controller hotkeys of es_padtokey.cfg) : F1 menu, F2 save, F4 load, F7 next slot, F8 screenshot, P pause.
        /// The Model3-Vulkan keys using F4 / F8 (HD textures, VRR cadence) are moved to Alt combinations.
        /// </summary>
        /*private void WriteKeyboardHotkeys(IniFile ini)
        {
            var hotkeys = new Dictionary<string, string>
            {
                { "InputUIExit", "KEY_ESCAPE" },
                { "InputUIToggleOverlay", "KEY_F1,KEY_TAB" },
                { "InputUISaveState", "KEY_F2" },
                { "InputUILoadState", "KEY_F4" },
                { "InputUIChangeSlot", "KEY_F7" },
                { "InputUIScreenShot", "KEY_F8" },
                { "InputUIPause", "KEY_P" },
                { "InputUIFullScreen", "KEY_ALT+KEY_RETURN" },
                { "InputUIToggleVrr", "KEY_ALT+KEY_V" },
                { "InputUIToggleHDTextures", "KEY_ALT+KEY_H" },
                { "InputUIReloadHDTextures", "KEY_F3" }
            };

            // User keyboard hotkeys from model3-vulkan_kb_hotkeys.yml / kb_hotkeys.yml, the controller hotkeys follow them (padToKey.xml)
            if (Hotkeys.GetHotKeysFromFile("model3-vulkan", "", out Dictionary<string, HotkeyResult> custoHotkeys, "Model3-Vulkan") && custoHotkeys.Count > 0)
            {
                foreach (var hotkey in custoHotkeys)
                    hotkeys[hotkey.Value.EmulatorKey] = hotkey.Value.EmulatorValue;

                _pad2Keyoverride = true;
            }

            foreach (var hotkey in hotkeys)
                ini.WriteValue(GlobalSection, hotkey.Key, "\"" + hotkey.Value + "\"");

            foreach (var section in ini.EnumerateSections().Where(s => s != GlobalSection).ToList())
            {
                foreach (var key in ini.EnumerateKeys(section).Where(k => k.StartsWith("InputUI", StringComparison.InvariantCultureIgnoreCase)).ToList())
                    ini.Remove(section, key);
            }
        }*/
        #endregion

        private static readonly List<string> inputValues = new List<string>
        {
            "InputStart1", "InputStart2", "InputCoin1", "InputCoin2", "InputServiceA", "InputServiceB", "InputTestA", "InputTestB",
            "InputJoyUp", "InputJoyDown", "InputJoyLeft", "InputJoyRight", "InputJoyUp2", "InputJoyDown2", "InputJoyLeft2", "InputJoyRight2",
            "InputPunch", "InputKick", "InputGuard", "InputEscape", "InputPunch2", "InputKick2", "InputGuard2", "InputEscape2",
            "InputShift", "InputBeat", "InputCharge", "InputJump",
            "InputShortPass", "InputLongPass", "InputShoot", "InputShortPass2", "InputLongPass2", "InputShoot2",
            "InputSteeringLeft", "InputSteeringRight", "InputSteering", "InputAccelerator", "InputBrake", "InputGearShiftUp", "InputGearShiftDown",
            "InputGearShift1", "InputGearShift2", "InputGearShift3", "InputGearShift4", "InputGearShiftN",
            "InputVR1", "InputVR2", "InputVR3", "InputVR4", "InputVRNext", "InputViewChange", "InputHandBrake", "InputRearBrake", "InputMusicSelect",
            "InputTwinJoyTurnLeft", "InputTwinJoyTurnRight", "InputTwinJoyForward", "InputTwinJoyReverse", "InputTwinJoyStrafeLeft", "InputTwinJoyStrafeRight",
            "InputTwinJoyJump", "InputTwinJoyCrouch", "InputTwinJoyLeft1", "InputTwinJoyLeft2", "InputTwinJoyRight1", "InputTwinJoyRight2",
            "InputTwinJoyUp1", "InputTwinJoyUp2", "InputTwinJoyDown1", "InputTwinJoyDown2", "InputTwinJoyShot1", "InputTwinJoyShot2", "InputTwinJoyTurbo1", "InputTwinJoyTurbo2",
            "InputAnalogJoyLeft", "InputAnalogJoyRight", "InputAnalogJoyUp", "InputAnalogJoyDown", "InputAnalogJoyX", "InputAnalogJoyY",
            "InputAnalogJoyTrigger", "InputAnalogJoyEvent", "InputAnalogJoyTrigger2", "InputAnalogJoyEvent2",
            "InputGunLeft", "InputGunRight", "InputGunUp", "InputGunDown", "InputGunX", "InputGunY", "InputTrigger", "InputOffscreen", "InputAutoTrigger",
            "InputGunLeft2", "InputGunRight2", "InputGunUp2", "InputGunDown2", "InputGunX2", "InputGunY2", "InputTrigger2", "InputOffscreen2", "InputAutoTrigger2",
            "InputAnalogGunLeft", "InputAnalogGunRight", "InputAnalogGunUp", "InputAnalogGunDown", "InputAnalogGunX", "InputAnalogGunY", "InputAnalogTriggerLeft", "InputAnalogTriggerRight",
            "InputAnalogGunLeft2", "InputAnalogGunRight2", "InputAnalogGunUp2", "InputAnalogGunDown2", "InputAnalogGunX2", "InputAnalogGunY2", "InputAnalogTriggerLeft2", "InputAnalogTriggerRight2",
            "InputSkiLeft", "InputSkiRight", "InputSkiUp", "InputSkiDown", "InputSkiX", "InputSkiY", "InputSkiPollLeft", "InputSkiPollRight", "InputSkiSelect1", "InputSkiSelect2", "InputSkiSelect3",
            "InputMagicalLeverUp1", "InputMagicalLeverDown1", "InputMagicalLeverUp2", "InputMagicalLeverDown2", "InputMagicalLever1", "InputMagicalLever2", "InputMagicalPedal1", "InputMagicalPedal2",
            "InputFishingRodLeft", "InputFishingRodRight", "InputFishingRodUp", "InputFishingRodDown", "InputFishingStickLeft", "InputFishingStickRight", "InputFishingStickUp", "InputFishingStickDown",
            "InputFishingRodX", "InputFishingRodY", "InputFishingStickX", "InputFishingStickY", "InputFishingReel", "InputFishingCast", "InputFishingSelect", "InputFishingTension"
        };

        // Inputs of player 2 (second controller, second gun)
        private static readonly List<string> p2values = new List<string>
        {
            "InputStart2", "InputCoin2", "InputServiceB", "InputTestB",
            "InputJoyUp2", "InputJoyDown2", "InputJoyLeft2", "InputJoyRight2",
            "InputPunch2", "InputKick2", "InputGuard2", "InputEscape2",
            "InputShortPass2", "InputLongPass2", "InputShoot2",
            "InputAnalogJoyTrigger2", "InputAnalogJoyEvent2",
            "InputGunLeft2", "InputGunRight2", "InputGunUp2", "InputGunDown2", "InputGunX2", "InputGunY2", "InputTrigger2", "InputOffscreen2", "InputAutoTrigger2",
            "InputAnalogGunLeft2", "InputAnalogGunRight2", "InputAnalogGunUp2", "InputAnalogGunDown2", "InputAnalogGunX2", "InputAnalogGunY2", "InputAnalogTriggerLeft2", "InputAnalogTriggerRight2",
            "InputMagicalLeverUp2", "InputMagicalLeverDown2", "InputMagicalLever2", "InputMagicalPedal2"
        };
    }
}
