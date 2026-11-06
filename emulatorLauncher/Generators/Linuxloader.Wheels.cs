using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class LinuxloaderGenerator
    {
        static readonly string[] wheelMappingPaths =
        {
            "{userpath}\\inputmapping\\linuxloader_wheels.yml",
            "{systempath}\\resources\\inputmapping\\wheels\\linuxloader_wheels.yml"
        };

        // linuxloader_wheels.yml controls written in [Common] (Test and Service only in test mode)
        private static readonly Dictionary<string, string> _wheelCommonControls = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "Start", "P1_Start" }, { "Coin", "P1_Coin" }, { "Test", "Test" }, { "Service", "P1_Service" }
        };

        // Loader slot (0 = P1)
        private readonly Dictionary<Controller, int> _padSlots = new Dictionary<Controller, int>();

        /// <summary>
        /// Adds the wheel to the [Driving] controls of player 1 (the keyboard and controller bindings are kept).
        /// SDL opens wheels with its DirectInput driver : buttons and POVs keep their DirectInput number,
        /// axes are numbered in the X, Y, Z, Rx, Ry, Rz, Slider order without gap.
        /// </summary>
        private void ConfigureWheel(IniFile ini)
        {
            if (!SystemConfig.getOptBoolean("use_wheel"))
                return;

            var wheels = Wheel.GetConnectedWheels(Controllers);
            if (wheels.Count < 1)
            {
                SimpleLogger.Instance.Info("[WHEELS] No usable wheel found.");
                return;
            }

            var ctrl = Controllers.FirstOrDefault(c => !c.IsKeyboard && c.DeviceIndex == wheels[0].ControllerIndex);
            if (ctrl == null)
                return;

            string wheelType = wheels[0].Type.ToString();
            SimpleLogger.Instance.Info("[WHEELS] Wheel " + wheels[0].Name + " (" + wheelType + ")");

            var sdl3 = ctrl.Sdl3Controller;
            if (sdl3 == null || string.IsNullOrEmpty(sdl3.GuidString) || sdl3.GuidString == new string('0', 32))
            {
                SimpleLogger.Instance.Warning("[WHEELS] No SDL3 device found for " + ctrl.Name + ", the wheel is not mapped.");
                return;
            }

            // XInput wheels are SDL gamepads : already mapped by linuxloader.yml (steering = left stick, pedals = triggers)
            if (sdl3.IsGamepad)
            {
                SimpleLogger.Instance.Info("[WHEELS] " + ctrl.Name + " is seen as a gamepad by SDL, the controller mapping is used.");
                return;
            }

            // Loader slot : the one of the wheel if it is already configured (P1 / P2), else the next free one
            int slot;
            if (!_padSlots.TryGetValue(ctrl, out slot))
            {
                slot = _padGuids.Count > 0 ? _padGuids.Keys.Max() : 0;
                if (slot >= 4)
                {
                    SimpleLogger.Instance.Warning("[WHEELS] No free player slot for the wheel.");
                    return;
                }

                _padGuids[slot + 1] = RemoveGuidCRC(sdl3.GuidString).ToLowerInvariant();
            }

            var mapping = LoadWheelMapping(wheelType);
            if (mapping == null)
                return;

            // A wheel needs almost no steering deadzone (8000 is the loader default for sticks)
            if (!SystemConfig.isOptSet("ll_steer_deadzone") || string.IsNullOrEmpty(SystemConfig["ll_steer_deadzone"]))
                ini.WriteValue("Config", "Steer_DeadZone", "500");

            string joy = "JOY" + slot + "_";
            int[] axes = Wheel.GetSdlAxisIndexes(ctrl);
            bool testService = SystemConfig.getOptBoolean("ll_testmode");

            // Gas and brake on the same axis cannot be split by the loader
            string gas, brake;
            mapping.TryGetValue("P1_Gas", out gas);
            mapping.TryGetValue("P1_Brake", out brake);
            if (!string.IsNullOrEmpty(gas) && AxisName(gas) == AxisName(brake))
            {
                SimpleLogger.Instance.Warning("[WHEELS] Gas and brake share the same axis, the pedals are not mapped.");
                mapping.Remove("P1_Gas");
                mapping.Remove("P1_Brake");
            }

            foreach (var kv in mapping)
            {
                string section = "Driving";
                string control = kv.Key;

                string commonControl;
                if (_wheelCommonControls.TryGetValue(control, out commonControl))
                {
                    if ((control == "Test" || control == "Service") && !testService)
                        continue;

                    section = "Common";
                    control = commonControl;
                }
                else if (!_keyboardP1["Driving"].ContainsKey(control))
                {
                    SimpleLogger.Instance.Warning("[WHEELS] Unknown control '" + control + "' in linuxloader_wheels.yml.");
                    continue;
                }

                foreach (string value in kv.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0))
                {
                    string input = ConvertWheelInput(value, joy, axes, control);
                    if (!string.IsNullOrEmpty(input))
                        AppendInput(ini, section, control, input);
                }
            }

            SimpleLogger.Instance.Info("[WHEELS] Wheel mapped as JOY" + slot + ".");
        }

        /// <summary>
        /// Container of linuxloader_wheels.yml : wheel type, else default. Key : linuxloader control, value : DirectInput inputs.
        /// </summary>
        private Dictionary<string, string> LoadWheelMapping(string wheelType)
        {
            string ymlFile = Controller.GetSystemYmlMappingFile("linuxloader", "", "lindbergh", wheelMappingPaths);
            if (ymlFile == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] linuxloader_wheels.yml not found, the wheel is not mapped.");
                return null;
            }

            try
            {
                var yml = YmlFile.Load(ymlFile);
                var container = yml.Elements.FirstOrDefault(e => e.Name == wheelType) as YmlContainer
                             ?? yml.Elements.FirstOrDefault(e => e.Name == "default") as YmlContainer;

                if (container == null)
                {
                    SimpleLogger.Instance.Info("[WHEELS] No mapping for " + wheelType + " in " + ymlFile);
                    return null;
                }

                SimpleLogger.Instance.Info("[WHEELS] Wheel controls '" + container.Name + "' loaded from " + ymlFile);

                return container.Elements.OfType<YmlElement>()
                    .Where(e => !string.IsNullOrEmpty(e.Name) && !string.IsNullOrEmpty(e.Value))
                    .ToDictionary(e => e.Name, e => e.Value.Trim(), StringComparer.InvariantCultureIgnoreCase);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WHEELS] Unable to read " + ymlFile + " : " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// DirectInput name (from 1) to a linuxloader joystick input : BUTTON5 = JOYx_BUTTON_4, POV1_UP = JOYx_HAT0_UP,
        /// XAXIS_NEG = JOYx_AXIS_0 (whole axis for the steering and the pedals, half axis for a button control).
        /// </summary>
        private static string ConvertWheelInput(string dinputName, string joy, int[] axes, string control)
        {
            string name = dinputName.ToUpperInvariant();

            if (name.StartsWith("BUTTON"))
            {
                int button = name.Substring(6).ToInteger();
                return button > 0 && button <= 32 ? joy + "BUTTON_" + (button - 1) : null;
            }

            if (name.StartsWith("POV"))
            {
                int sep = name.IndexOf('_');
                if (sep < 4)
                    return null;

                int hat = name.Substring(3, sep - 3).ToInteger();
                string dir = name.Substring(sep + 1);
                return hat > 0 && hat <= 4 && (dir == "UP" || dir == "DOWN" || dir == "LEFT" || dir == "RIGHT") ? joy + "HAT" + (hat - 1) + "_" + dir : null;
            }

            int index = Wheel.GetSdlAxisIndex(name, axes);
            if (index < 0)
                return null;

            bool negative = name.EndsWith("_NEG");
            string axis = joy + "AXIS_" + index;

            // Steering : turned over when its left is on the positive side
            if (control == "P1_Steer")
                return negative ? axis : axis + "_INVERTED";

            // Pedals : the loader reads them from the negative end, turned over when pressed toward the negative side
            if (control == "P1_Gas" || control == "P1_Brake")
                return negative ? axis + "_INVERTED" : axis;

            return axis + (negative ? "_NEGATIVE" : "_POSITIVE");
        }

        private static string AxisName(string dinputName)
        {
            if (string.IsNullOrEmpty(dinputName))
                return null;

            string name = dinputName.Split(',')[0].Trim().ToUpperInvariant();
            foreach (string s in new[] { "_POS", "_NEG", "_INV" })
            {
                if (name.EndsWith(s))
                    return name.Substring(0, name.Length - s.Length);
            }
            return name;
        }
    }
}
