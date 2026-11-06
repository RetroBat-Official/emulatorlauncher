using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class LinuxloaderGenerator : Generator
    {
        private readonly Dictionary<int, string> _padGuids = new Dictionary<int, string>();

        static readonly string[] mappingPaths =
        {
            "{userpath}\\inputmapping\\linuxloader.yml",
            "{systempath}\\resources\\inputmapping\\linuxloader.yml"
        };

        // Sections of controls.ini mapped from linuxloader.yml (yml container = section name in lower case)
        private static readonly string[] _padSections = { "Digital", "Driving", "Flying" };

        // Keyboard bindings, also the list of the controls written in each section (a control without binding is emptied)
        private static readonly Dictionary<string, Dictionary<string, string>> _keyboardP1 = new Dictionary<string, Dictionary<string, string>>()
        {
            { "Digital", new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "P1_Up", "KEY_Up" }, { "P1_Down", "KEY_Down" }, { "P1_Left", "KEY_Left" }, { "P1_Right", "KEY_Right" },
                    { "P1_Button1", "KEY_Left Ctrl" }, { "P1_Button2", "KEY_Left Alt" }, { "P1_Button3", "KEY_Space" }, { "P1_Card1Insert", "KEY_F7" }
                }
            },
            { "Driving", new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "P1_Steer_Left", "KEY_Left" }, { "P1_Steer_Right", "KEY_Right" }, { "P1_Gas_Digital", "KEY_Up" }, { "P1_Brake_Digital", "KEY_Down" },
                    { "ViewChange", "KEY_Left Shift" }, { "Boost", "KEY_Left Ctrl" }, { "BoostRight", "KEY_Left Alt" },
                    { "GearUp", "KEY_X" }, { "GearDown", "KEY_Z" }, { "MusicChange", "KEY_Space" },
                    { "Up", "KEY_R" }, { "Down", "KEY_F" }, { "Left", "KEY_D" }, { "Right", "KEY_G" }, { "CardInsert", "KEY_F7" },
                    { "P1_Steer", "" }, { "P1_Gas", "" }, { "P1_Brake", "" }
                }
            },
            { "Flying", new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { "Flying_Left", "KEY_Left" }, { "Flying_Right", "KEY_Right" }, { "Flying_Up", "KEY_Up" }, { "Flying_Down", "KEY_Down" },
                    { "Throttle_Accelerate", "KEY_X" }, { "Throttle_Slowdown", "KEY_Z" },
                    { "GunTrigger", "KEY_Left Ctrl" }, { "MissileTrigger", "KEY_Left Alt" }, { "ClimaxSwitch", "KEY_Left Shift" },
                    { "Flying_X", "" }, { "Flying_Y", "" }, { "Throttle", "" }
                }
            },
        };

        // Player 2 only plays digital games
        private static readonly Dictionary<string, string> _keyboardDigitalP2 = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "P2_Up", "KEY_R" }, { "P2_Down", "KEY_F" }, { "P2_Left", "KEY_D" }, { "P2_Right", "KEY_G" },
            { "P2_Button1", "KEY_A" }, { "P2_Button2", "KEY_S" }, { "P2_Button3", "KEY_Q" }, { "P2_Card2Insert", "KEY_F8" }
        };

        // Analog controls : a stick or a trigger is bound as a whole axis
        private static readonly HashSet<string> _analogControls = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            "P1_Steer", "P1_Gas", "P1_Brake", "Flying_X", "Flying_Y", "Throttle"
        };

        private class PadInput
        {
            public string Gamepad;  // SDL gamepad input (GCx_)
            public string DbKey;    // gamecontrollerdb key, for joysticks (JOYx_)
            public bool IsAxis;
            public int Direction;   // -1 / 1 : half axis used as a button

            public PadInput(string gamepad, string dbKey, bool isAxis = false, int direction = 0)
            {
                Gamepad = gamepad;
                DbKey = dbKey;
                IsAxis = isAxis;
                Direction = direction;
            }
        }

        // Controller positions of linuxloader.yml
        private static readonly Dictionary<string, PadInput> _padInputs = new Dictionary<string, PadInput>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "south", new PadInput("BUTTON_A", "a") },
            { "east", new PadInput("BUTTON_B", "b") },
            { "west", new PadInput("BUTTON_X", "x") },
            { "north", new PadInput("BUTTON_Y", "y") },
            { "l1", new PadInput("BUTTON_LEFTSHOULDER", "leftshoulder") },
            { "r1", new PadInput("BUTTON_RIGHTSHOULDER", "rightshoulder") },
            { "l2", new PadInput("AXIS_LEFTTRIGGER", "lefttrigger", true) },
            { "r2", new PadInput("AXIS_RIGHTTRIGGER", "righttrigger", true) },
            { "l3", new PadInput("BUTTON_LEFTSTICK", "leftstick") },
            { "r3", new PadInput("BUTTON_RIGHTSTICK", "rightstick") },
            { "select", new PadInput("BUTTON_BACK", "back") },
            { "start", new PadInput("BUTTON_START", "start") },
            { "guide", new PadInput("BUTTON_GUIDE", "guide") },
            { "up", new PadInput("BUTTON_DPUP", "dpup") },
            { "down", new PadInput("BUTTON_DPDOWN", "dpdown") },
            { "left", new PadInput("BUTTON_DPLEFT", "dpleft") },
            { "right", new PadInput("BUTTON_DPRIGHT", "dpright") },
            { "leftx", new PadInput("AXIS_LEFTX", "leftx", true) },
            { "lefty", new PadInput("AXIS_LEFTY", "lefty", true) },
            { "rightx", new PadInput("AXIS_RIGHTX", "rightx", true) },
            { "righty", new PadInput("AXIS_RIGHTY", "righty", true) },
            { "leftx-left", new PadInput("AXIS_LEFTX", "leftx", true, -1) },
            { "leftx-right", new PadInput("AXIS_LEFTX", "leftx", true, 1) },
            { "lefty-up", new PadInput("AXIS_LEFTY", "lefty", true, -1) },
            { "lefty-down", new PadInput("AXIS_LEFTY", "lefty", true, 1) },
            { "rightx-left", new PadInput("AXIS_RIGHTX", "rightx", true, -1) },
            { "rightx-right", new PadInput("AXIS_RIGHTX", "rightx", true, 1) },
            { "righty-up", new PadInput("AXIS_RIGHTY", "righty", true, -1) },
            { "righty-down", new PadInput("AXIS_RIGHTY", "righty", true, 1) },
        };

        private void CreateControllerConfiguration(string cfgPath, string gamePath)
        {
            try
            {
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_HIDAPI", "1", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "1", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_ENHANCED_REPORTS", "0", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_HIDAPI_COMBINE_JOY_CONS", "1", EnvironmentVariableTarget.Process);
            }
            catch { }

            bool autoConfig = !(Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1");

            if (autoConfig)
            {
                SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Linuxloader");
                string gameCtrlFile = Path.Combine(gamePath, "controls.ini");
                if (File.Exists(gameCtrlFile))
                {
                    AddFileForRestoration(gameCtrlFile);
                    try { File.Delete(gameCtrlFile); }
                    catch { }
                }
            }
            else
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");

            var pads = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.PlayerIndex).Take(2).ToList();
            bool samePad = pads.Count > 1 && pads[0].Guid.ToString() == pads[1].Guid.ToString();

            using (var ini = new IniFile(cfgPath, IniOptions.UseSpaces))
            {
                if (autoConfig)
                {
                    foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(2))
                        ConfigureInput(ini, controller, samePad);

                    ConfigureWheel(ini);

                    // Purge : avoid P3_GUID/P4_GUID added by the loader to stay
                    for (int p = 1; p <= 4; p++)
                        ini.Remove("ControllerGUIDs", "P" + p + "_GUID");

                    foreach (var kv in _padGuids)
                        ini.WriteValue("ControllerGUIDs", "P" + kv.Key + "_GUID", kv.Value);
                }

                ConfigureLindberghGuns(ini, "lindbergh", !autoConfig);

                // Gun axis auto-centering (gamepad aiming)
                string centering = SystemConfig.getOptBoolean("ll_gun_centering") ? "1" : "0";
                ini.WriteValue("Config", "GunX_Centering", centering);
                ini.WriteValue("Config", "GunY_Centering", centering);

                EnsureMahjongSection(ini);

                ini.Save();
            }
        }

        private void ConfigureInput(IniFile ini, Controller controller, bool samePad)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard && controller.PlayerIndex == 1)
                ConfigureKeyboard(ini, controller.Config, controller.PlayerIndex);
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex, samePad);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            // Common section
            ini.WriteValue("Common", "Test", "KEY_0");
            ini.WriteValue("Common", "ExitGame", "KEY_Escape");
            ini.WriteValue("Common", "P1_Coin", "KEY_5");
            ini.WriteValue("Common", "P2_Coin", "KEY_6");
            ini.WriteValue("Common", "P1_Start", "KEY_1");
            ini.WriteValue("Common", "P2_Start", "KEY_2");
            ini.WriteValue("Common", "P1_Service", "KEY_9");

            foreach (string section in _padSections)
                WritePadSection(ini, section, _keyboardP1[section], 1, null, null, false);

            WritePadSection(ini, "Digital", _keyboardDigitalP2, 2, null, null, false);

            WriteShootingSection(ini);
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex, bool samePad)
        {
            if (ctrl == null)
                return;

            bool testService = SystemConfig.getOptBoolean("ll_testmode");
            bool isXinput = ctrl.IsXInputDevice;

            int cIndex = playerindex - 1;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            var guid = ctrl.Guid;
            if (ctrl.SdlWrappedTechID == SdlWrappedTechId.RawInput && ctrl.XInput != null)
                guid = guid.ToXInputGuid(ctrl.XInput.SubType);

            string guidString = RemoveGuidCRC(guid.ToString()).ToLowerInvariant();

            Sdl3GameController sdl3Controller = ctrl.Sdl3Controller;

            if (sdl3Controller != null)
            {
                if (!string.IsNullOrEmpty(sdl3Controller.GuidString) && sdl3Controller.GuidString != new string('0', 32))
                {
                    var cleaned = RemoveGuidCRC(sdl3Controller.GuidString);
                    if (!string.IsNullOrEmpty(cleaned))
                        guidString = cleaned.ToLowerInvariant();
                }
            }

            if (samePad)
            {
                if (sdl3Controller != null)
                    cIndex = sdl3Controller.EnumerationIndex;
                else
                {
                    var sortedControllers = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.DirectInput?.DeviceIndex ?? i.DeviceIndex).ToList();
                    cIndex = sortedControllers.IndexOf(ctrl);
                }
            }

            // GUID pinning keeps the real pad order, only the binding index is swapped
            int guidSlot = cIndex;

            if (SystemConfig.getOptBoolean("ll_reversepadindex"))
            {
                if (cIndex == 0)
                    cIndex = 1;
                else if (cIndex == 1)
                    cIndex = 0;
            }

            // Gamepads use the SDL gamepad names (GCx_), other joysticks their raw inputs (JOYx_) read from gamecontrollerdb.txt
            bool standardJoy = sdl3Controller != null && !sdl3Controller.IsGamepad;
            string gcIndex = (standardJoy ? "JOY" : "GC") + cIndex + "_";

            SdlToDirectInput dinputController = null;
            if (standardJoy)
            {
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
                string dguid = guid.ToString().Substring(0, 24) + "00000000";

                if (File.Exists(gamecontrollerDB))
                    dinputController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, dguid);
                else
                    SimpleLogger.Instance.Warning("[CONTROLS] gamecontrollerdb.txt not found, mapping won't be available");

                if (dinputController == null || dinputController.ButtonMappings == null)
                {
                    SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt does not contain mapping for the controller " + guid + ". Only keyboard is mapped for P" + playerindex + ".");
                    dinputController = null;
                }
            }

            bool padMapped = !standardJoy || dinputController != null;

            // Deadzones : loader internal defaults when not set (8000 for sticks, 500 for pedals)
            string steerDeadzone = SystemConfig.isOptSet("ll_steer_deadzone") && !string.IsNullOrEmpty(SystemConfig["ll_steer_deadzone"]) ? SystemConfig["ll_steer_deadzone"] : "8000";
            ini.WriteValue("Config", "Steer_DeadZone", steerDeadzone);

            string gasBrakeDeadzone = SystemConfig.isOptSet("ll_gasbrake_deadzone") && !string.IsNullOrEmpty(SystemConfig["ll_gasbrake_deadzone"]) ? SystemConfig["ll_gasbrake_deadzone"] : "500";
            ini.WriteValue("Config", "Gas_DeadZone", gasBrakeDeadzone);
            ini.WriteValue("Config", "Brake_DeadZone", gasBrakeDeadzone);

            string flyingDeadzone = SystemConfig.isOptSet("ll_flying_deadzone") && !string.IsNullOrEmpty(SystemConfig["ll_flying_deadzone"]) ? SystemConfig["ll_flying_deadzone"] : "8000";
            ini.WriteValue("Config", "FLYING_X_DeadZone", flyingDeadzone);
            ini.WriteValue("Config", "FLYING_Y_DeadZone", flyingDeadzone);
            ini.WriteValue("Config", "Throttle_DeadZone", flyingDeadzone);

            // Common section : RetroBat standard (select = coin, start = start, R3 / L3 = test / service in test mode)
            string start = padMapped ? GetPadInput("start", null, false, gcIndex, dinputController, isXinput) : null;
            string select = padMapped ? GetPadInput("select", null, false, gcIndex, dinputController, isXinput) : null;

            if (playerindex == 1)
            {
                string test = testService && padMapped ? GetPadInput("r3", null, false, gcIndex, dinputController, isXinput) : null;
                string service = testService && padMapped ? GetPadInput("l3", null, false, gcIndex, dinputController, isXinput) : null;

                ini.WriteValue("Common", "Test", JoinInputs("KEY_0", test));
                ini.WriteValue("Common", "ExitGame", JoinInputs("KEY_Escape", start != null && select != null ? start + " + " + select : null));
                ini.WriteValue("Common", "P1_Coin", JoinInputs("KEY_5", select));
                ini.WriteValue("Common", "P2_Coin", "KEY_6");
                ini.WriteValue("Common", "P1_Start", JoinInputs("KEY_1", start));
                ini.WriteValue("Common", "P2_Start", "KEY_2");
                ini.WriteValue("Common", "P1_Service", JoinInputs("KEY_9", service));

                foreach (string section in _padSections)
                    WritePadSection(ini, section, _keyboardP1[section], 1, gcIndex, dinputController, padMapped, isXinput);

                // Player 2 keyboard only, completed below if a second controller is plugged
                WritePadSection(ini, "Digital", _keyboardDigitalP2, 2, null, null, false);

                WriteShootingSection(ini);
            }
            else if (playerindex == 2)
            {
                ini.WriteValue("Common", "P2_Coin", JoinInputs("KEY_6", select));
                ini.WriteValue("Common", "P2_Start", JoinInputs("KEY_2", start));

                WritePadSection(ini, "Digital", _keyboardDigitalP2, 2, gcIndex, dinputController, padMapped, isXinput);
            }

            if (sdl3Controller != null && !string.IsNullOrEmpty(guidString))
            {
                _padGuids[guidSlot + 1] = guidString;
                _padSlots[ctrl] = guidSlot;
            }
            else
                SimpleLogger.Instance.Warning("[CONTROLS] No SDL3 GUID for P" + playerindex + " - GUID pinning unavailable for this pad, other players are unaffected.");
        }

        /// <summary>
        /// Writes all the controls of a section : keyboard first, then the controller inputs of linuxloader.yml.
        /// The yml values are the player 1 controls, converted for player 2 (P1_ to P2_, Card1Insert to Card2Insert).
        /// </summary>
        private void WritePadSection(IniFile ini, string section, Dictionary<string, string> keyboard, int player, string gcIndex, SdlToDirectInput dinput, bool withPad, bool isXinput = false)
        {
            var bindings = keyboard.ToDictionary(k => k.Key, k => new List<string>(), StringComparer.InvariantCultureIgnoreCase);
            foreach (var kv in keyboard.Where(k => !string.IsNullOrEmpty(k.Value)))
                bindings[kv.Key].Add(kv.Value);

            if (withPad)
            {
                foreach (var entry in GetYmlMapping(section))
                {
                    foreach (string value in entry.Value)
                    {
                        // Optional axis mode : "Throttle:inverted"
                        string[] parts = value.Split(':');
                        string control = parts[0].Trim();
                        string mode = parts.Length > 1 ? parts[1].Trim() : null;

                        if (player == 2)
                            control = control.Replace("P1_", "P2_").Replace("Card1Insert", "Card2Insert");

                        List<string> list;
                        if (!bindings.TryGetValue(control, out list))
                        {
                            SimpleLogger.Instance.Warning("[CONTROLS] Unknown control '" + control + "' in linuxloader.yml [" + section.ToLowerInvariant() + "].");
                            continue;
                        }

                        string input = GetPadInput(entry.Key, mode, _analogControls.Contains(control), gcIndex, dinput, isXinput);
                        if (!string.IsNullOrEmpty(input) && !list.Contains(input))
                            list.Add(input);
                    }
                }
            }

            foreach (var kv in bindings)
                ini.WriteValue(section, kv.Key, string.Join(", ", kv.Value));
        }

        /// <summary>
        /// Converts a controller position of linuxloader.yml to a linuxloader input (GCx_ for gamepads, JOYx_ through gamecontrollerdb for joysticks).
        /// A trigger or a stick used by a button control is bound as a half axis.
        /// </summary>
        private string GetPadInput(string position, string mode, bool analogControl, string gcIndex, SdlToDirectInput dinput, bool isXinput)
        {
            PadInput input;
            if (!_padInputs.TryGetValue(position, out input))
            {
                SimpleLogger.Instance.Warning("[CONTROLS] Unknown controller position '" + position + "' in linuxloader.yml.");
                return null;
            }

            int direction = input.Direction;
            if (input.IsAxis && direction == 0 && !analogControl && string.IsNullOrEmpty(mode))
                direction = 1;

            string ret;
            if (dinput != null)
                ret = GetDinputMapping(gcIndex, dinput, input.DbKey, isXinput, direction);
            else
            {
                ret = gcIndex + input.Gamepad;
                if (direction > 0)
                    ret += "_POSITIVE";
                else if (direction < 0)
                    ret += "_NEGATIVE";
            }

            if (string.IsNullOrEmpty(ret) || ret == "Unassigned")
                return null;

            // Whole axis modes
            if (input.IsAxis && direction == 0 && !string.IsNullOrEmpty(mode))
            {
                switch (mode.ToLowerInvariant())
                {
                    case "inverted": ret += "_INVERTED"; break;
                    case "positive_half": ret += "_POSITIVE_HALF"; break;
                    case "negative_half": ret += "_NEGATIVE_HALF"; break;
                }
            }

            return ret;
        }

        private Dictionary<string, Dictionary<string, List<string>>> _ymlMapping;

        /// <summary>
        /// Container of linuxloader.yml for a section : <section>_<layout>, then <section>.
        /// Key : controller position. Value : controls of player 1, separated by commas.
        /// </summary>
        private Dictionary<string, List<string>> GetYmlMapping(string section)
        {
            if (_ymlMapping == null)
                _ymlMapping = LoadYmlMapping();

            Dictionary<string, List<string>> ret;
            if (_ymlMapping.TryGetValue(section.ToLowerInvariant(), out ret))
                return ret;

            return new Dictionary<string, List<string>>();
        }

        private Dictionary<string, Dictionary<string, List<string>>> LoadYmlMapping()
        {
            var ret = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.InvariantCultureIgnoreCase);

            string ymlFile = Controller.GetSystemYmlMappingFile("linuxloader", "", "lindbergh", mappingPaths);
            if (string.IsNullOrEmpty(ymlFile))
            {
                SimpleLogger.Instance.Warning("[CONTROLS] linuxloader.yml not found, only the keyboard is mapped.");
                return ret;
            }

            YmlFile yml;
            try { yml = YmlFile.Load(ymlFile); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[CONTROLS] Unable to read " + ymlFile + " : " + ex.Message);
                return ret;
            }

            string layout = SystemConfig.isOptSet("controller_layout") ? SystemConfig["controller_layout"] : null;
            if (layout == "default")
                layout = null;

            foreach (string section in _padSections)
            {
                string baseName = section.ToLowerInvariant();

                foreach (string name in string.IsNullOrEmpty(layout) ? new[] { baseName } : new[] { baseName + "_" + layout, baseName })
                {
                    var container = yml.Elements.FirstOrDefault(e => e.Name == name) as YmlContainer;
                    if (container == null)
                        continue;

                    // A controller position can drive several controls : "left: P1_Steer_Left, Left"
                    ret[baseName] = container.Elements.OfType<YmlElement>()
                        .Where(e => !string.IsNullOrEmpty(e.Name) && !string.IsNullOrEmpty(e.Value))
                        .ToDictionary(e => e.Name, e => e.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList(), StringComparer.InvariantCultureIgnoreCase);

                    SimpleLogger.Instance.Info("[CONTROLS] Controls '" + name + "' loaded from " + ymlFile);
                    break;
                }
            }

            return ret;
        }

        private static string JoinInputs(string key, string pad)
        {
            return string.IsNullOrEmpty(pad) ? key : key + ", " + pad;
        }

        /// <summary>
        /// Shooting section : mouse / lightgun (overwritten by the gun options when guns are used).
        /// </summary>
        private void WriteShootingSection(IniFile ini)
        {
            WriteGunAxes(ini);

            ini.WriteValue("Shooting", "P1_Trigger", "MOUSE_LEFT_BUTTON");
            ini.WriteValue("Shooting", "P1_Reload", "MOUSE_RIGHT_BUTTON");
            ini.WriteValue("Shooting", "P1_GunButton", "MOUSE_MIDDLE_BUTTON");
            ini.WriteValue("Shooting", "P1_ActionButton", "KEY_R");
            ini.WriteValue("Shooting", "P1_PedalLeft", "KEY_Left");
            ini.WriteValue("Shooting", "P1_PedalRight", "KEY_Right");
        }

        private string GetDinputMapping(string index, SdlToDirectInput c, string buttonkey, bool isxinput, int plus = 0)
        {
            if (c == null || !c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "";
            }

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("-a"))
                plus = -1;

            if (button.StartsWith("+a"))
                plus = 1;

            if (isxinput)
            {
                if (button == "a5")
                    return index + "AXIS_2_NEGATIVE";
            }

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());
                return index + "BUTTON_" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return index + "HAT0_UP";
                    case 2:
                        return index + "HAT0_RIGHT";
                    case 4:
                        return index + "HAT0_DOWN";
                    case 8:
                        return index + "HAT0_LEFT";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.StartsWith("a") ? button.Substring(1).ToInteger() : button.Substring(2).ToInteger();

                if (plus == 1) return index + "AXIS_" + axisID + "_POSITIVE";
                else if (plus == -1) return index + "AXIS_" + axisID + "_NEGATIVE";
                else return index + "AXIS_" + axisID;
            }

            return "Unassigned";
        }

        public static string RemoveGuidCRC(string guid)
        {
            if (guid == null || guid.Length < 8)
                return guid;

            return guid.Substring(0, 4) + "0000" + guid.Substring(8);
        }

        /// <summary>
        /// P1 gun axes (mouse), optionally inverted.
        /// </summary>
        private void WriteGunAxes(IniFile ini)
        {
            string invertAxis = SystemConfig.isOptSet("ll_gunaxis_invert") && !string.IsNullOrEmpty(SystemConfig["ll_gunaxis_invert"]) ? SystemConfig["ll_gunaxis_invert"].ToLowerInvariant() : "none";
            bool invertX = invertAxis == "x" || invertAxis == "both";
            bool invertY = invertAxis == "y" || invertAxis == "both";

            ini.WriteValue("Shooting", "P1_GunX", invertX ? "MOUSE_AXIS_X_INVERTED" : "MOUSE_AXIS_X");
            ini.WriteValue("Shooting", "P1_GunY", invertY ? "MOUSE_AXIS_Y_INVERTED" : "MOUSE_AXIS_Y");
        }

        private void EnsureMahjongSection(IniFile ini)
        {
            if (ini.EnumerateKeys("Mahjong").Length > 0)
                return;

            SimpleLogger.Instance.Info("[CONTROLS] Adding missing [Mahjong] section (keyboard defaults).");

            ini.WriteValue("Mahjong", "ButtonA", "KEY_Y");
            ini.WriteValue("Mahjong", "ButtonB", "KEY_U");
            ini.WriteValue("Mahjong", "ButtonC", "KEY_I");
            ini.WriteValue("Mahjong", "ButtonD", "KEY_O");
            ini.WriteValue("Mahjong", "ButtonE", "KEY_G");
            ini.WriteValue("Mahjong", "ButtonF", "KEY_H");
            ini.WriteValue("Mahjong", "ButtonG", "KEY_J");
            ini.WriteValue("Mahjong", "ButtonH", "KEY_K");
            ini.WriteValue("Mahjong", "ButtonI", "KEY_L");
            ini.WriteValue("Mahjong", "ButtonJ", "KEY_V");
            ini.WriteValue("Mahjong", "ButtonK", "KEY_B");
            ini.WriteValue("Mahjong", "ButtonL", "KEY_N");
            ini.WriteValue("Mahjong", "ButtonM", "KEY_M");
            ini.WriteValue("Mahjong", "ButtonN", "KEY_Comma");
            ini.WriteValue("Mahjong", "ButtonChi", "KEY_F1");
            ini.WriteValue("Mahjong", "ButtonPon", "KEY_F2");
            ini.WriteValue("Mahjong", "ButtonKan", "KEY_F3");
            ini.WriteValue("Mahjong", "ButtonReach", "KEY_F4");
            ini.WriteValue("Mahjong", "ButtonAgari", "KEY_F5");
            ini.WriteValue("Mahjong", "ButtonCancel", "KEY_F6");
            ini.WriteValue("Mahjong", "CardInsert", "KEY_F7");
        }
    }
}