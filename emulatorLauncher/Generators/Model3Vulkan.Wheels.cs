using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Model3VulkanGenerator
    {
        static readonly string[] wheelMappingPaths =
        {
            "{userpath}\\inputmapping\\model3vulkan_wheels.yml",
            "{systempath}\\resources\\inputmapping\\wheels\\model3vulkan_wheels.yml",
            "{userpath}\\inputmapping\\model3_wheels.yml",
            "{systempath}\\resources\\inputmapping\\wheels\\model3_wheels.yml"
        };

        // Wheel inputs of model3_wheels.yml (DirectInput names) written for player 1
        private static readonly string[] _wheelInputs = new string[]
        {
            "InputSteeringLeft", "InputSteeringRight", "InputSteering", "InputAccelerator", "InputBrake",
            "InputVR1", "InputVR2", "InputVR3", "InputVR4", "InputViewChange", "InputHandBrake", "InputRearBrake", "InputMusicSelect"
        };

        private Controller _wheelController;
        private bool _wheelSearched;

        /// <summary>
        /// Controller of the first usable wheel (priority order of Wheel.GetConnectedWheels), when the wheel option is on.
        /// </summary>
        private Controller GetWheelController()
        {
            if (_wheelSearched)
                return _wheelController;

            _wheelSearched = true;

            if (!SystemConfig.getOptBoolean("use_wheel"))
                return null;

            var wheels = Wheel.GetConnectedWheels(Controllers);
            if (wheels.Count == 0)
            {
                SimpleLogger.Instance.Info("[WHEELS] No usable wheel found.");
                return null;
            }

            _wheelController = Controllers.FirstOrDefault(c => !c.IsKeyboard && c.DeviceIndex == wheels[0].ControllerIndex);
            if (_wheelController != null)
                SimpleLogger.Instance.Info("[WHEELS] Wheel " + wheels[0].Name + " (" + wheels[0].Type + ")");

            return _wheelController;
        }

        /// <summary>
        /// The wheel is read as a raw SDL3 joystick. SDL opens wheels with its DirectInput driver and numbers the axes present on the device
        /// in the X, Y, Z, Rx, Ry, Rz, Slider order without gap : the DirectInput names of the yml are converted to AXISn with the device objects.
        /// </summary>
        private void ConfigureWheel(IniFile ini, Dictionary<string, string> mapping, Controller wheel, int joyNumber)
        {
            var wheels = Wheel.GetConnectedWheels(Controllers);
            string wheelType = wheels.Count > 0 ? wheels[0].Type.ToString() : "default";

            string ymlFile = Controller.GetSystemYmlMappingFile("model3-vulkan", "", "model3", wheelMappingPaths);
            if (ymlFile == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] model3_wheels.yml not found.");
                return;
            }

            YmlContainer wheelMapping = null;
            try
            {
                var yml = YmlFile.Load(ymlFile);
                wheelMapping = yml?.Elements.FirstOrDefault(e => e.Name == wheelType) as YmlContainer
                            ?? yml?.Elements.FirstOrDefault(e => e.Name == "default") as YmlContainer;
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("[WHEELS] Unable to read " + ymlFile + " : " + ex.Message); }

            if (wheelMapping == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] No mapping for " + wheelType + " in " + ymlFile);
                return;
            }

            var buttons = wheelMapping.Elements.OfType<YmlElement>()
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .ToDictionary(e => e.Name, e => (e.Value ?? "").Trim(), StringComparer.InvariantCultureIgnoreCase);

            int[] axes = Wheel.GetSdlAxisIndexes(wheel);

            // Gear stick on another device (some Thrustmaster) : FORCE GEARSTICK DEVICE ID is the SDL3 joystick number, starting from 0
            int shifterNumber = -1;
            if (SystemConfig.isOptSet("gearstick_deviceid") && !string.IsNullOrEmpty(SystemConfig["gearstick_deviceid"]))
                shifterNumber = SystemConfig["gearstick_deviceid"].ToInteger() + 1;
            else if (GetWheelValue(buttons, "DeportedShifter").Equals("true", StringComparison.InvariantCultureIgnoreCase))
                SimpleLogger.Instance.Warning("[WHEELS] The gear stick of " + wheelType + " is a separate device : set FORCE GEARSTICK DEVICE ID, the wheel buttons are used meanwhile.");

            Func<string, int, string> convert = (dinputName, number) =>
            {
                string sdl = ConvertDirectInputBinding(dinputName, axes);
                return sdl == null ? null : "JOY" + number + "_" + sdl;
            };

            // Start / coin / test / service : wheel buttons, keyboard keys kept
            bool service = SystemConfig.getOptBoolean("m3_service");
            SetWheelInput(mapping, "InputStart1", "KEY_1", convert(GetWheelValue(buttons, "Start"), joyNumber));
            SetWheelInput(mapping, "InputCoin1", "KEY_5", convert(GetWheelValue(buttons, "Coin"), joyNumber));
            SetWheelInput(mapping, "InputServiceA", "KEY_0", service ? convert(GetWheelValue(buttons, "Service"), joyNumber) : null);
            SetWheelInput(mapping, "InputTestA", "KEY_9", service ? convert(GetWheelValue(buttons, "Test"), joyNumber) : null);

            foreach (string input in _wheelInputs)
                SetWheelInput(mapping, input, null, convert(GetWheelValue(buttons, input), joyNumber));

            // Sequential gears, "Shifter_" values are buttons of the gear stick device
            foreach (string input in new string[] { "InputGearShiftUp", "InputGearShiftDown" })
            {
                string value = GetWheelValue(buttons, input);
                if (value.StartsWith("Shifter_", StringComparison.InvariantCultureIgnoreCase))
                    SetWheelInput(mapping, input, null, shifterNumber > 0 ? convert(value.Substring(8), shifterNumber) : null);
                else
                    SetWheelInput(mapping, input, null, convert(value, joyNumber));
            }

            // 4-speed gearbox : H-shifter (StickGearX) or the wheel buttons (GearX) with DISABLE GEAR STICK
            bool noGearStick = SystemConfig.getOptBoolean("wheel_nogearstick");
            string[] gears = new string[] { "1", "2", "3", "4", "N" };
            foreach (string gear in gears)
            {
                string value = GetWheelValue(buttons, noGearStick ? "Gear" + gear : "StickGear" + gear);
                int number = !noGearStick && shifterNumber > 0 ? shifterNumber : joyNumber;
                SetWheelInput(mapping, "InputGearShift" + gear, null, convert(value, number));
            }
        }

        private static string GetWheelValue(Dictionary<string, string> buttons, string key)
        {
            string value;
            return buttons.TryGetValue(key, out value) && value != null && value != "nul" ? value : "";
        }

        private static void SetWheelInput(Dictionary<string, string> mapping, string input, string key, string binding)
        {
            var values = new List<string>();
            if (!string.IsNullOrEmpty(key))
                values.Add(key);
            if (!string.IsNullOrEmpty(binding))
                values.Add(binding);

            mapping[input] = values.Count > 0 ? string.Join(",", values) : "NONE";
        }

        /// <summary>
        /// XAXIS_NEG, RZAXIS_INV, SLIDER1, BUTTON5, POV1_UP (DirectInput names) to the SDL raw joystick binding.
        /// Buttons and POVs keep their number (SDL numbers them in the DirectInput order), axes get the SDL index (AXISn).
        /// </summary>
        private static string ConvertDirectInputBinding(string dinputName, int[] axes)
        {
            if (string.IsNullOrEmpty(dinputName))
                return null;

            string name = dinputName.Trim().ToUpperInvariant();
            if (name.StartsWith("BUTTON") || name.StartsWith("POV"))
                return name;

            string suffix = "";
            foreach (string s in new string[] { "_POS", "_NEG", "_INV" })
            {
                if (name.EndsWith(s))
                {
                    suffix = s;
                    break;
                }
            }

            int index = Wheel.GetSdlAxisIndex(name, axes);
            return index < 0 ? null : "AXIS" + (index + 1) + suffix;
        }
    }
}
