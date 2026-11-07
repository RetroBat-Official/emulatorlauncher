using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class OdinGenerator
    {
        /// <summary>
        /// Odin controls driven by the wheel in a game (odin-games.json), null when the game has none.
        /// </summary>
        private class WheelControls
        {
            public string Steering, Gas, Brake, GearUp, GearDown, View, Start;
            public bool BrakeIsButton;
        }

        // Games played with a wheel (parent sets, the versions share their controls)
        private static readonly Dictionary<string, WheelControls> _wheelControls = new Dictionary<string, WheelControls>()
        {
            { "sgnascar", new WheelControls { Steering = "steering_wheel", Gas = "gas_pedal", Brake = "brake_pedal", GearUp = "gear_up", GearDown = "gear_down", View = "view_change", Start = "start" } },
            { "atvtrack", new WheelControls { Steering = "handlebar", Gas = "throttle", Brake = "right_brake", BrakeIsButton = true, GearUp = "right_brake", GearDown = "left_brake", Start = "start" } },
            { "smashdrv", new WheelControls { Steering = "wheel", Gas = "gas_pedal", Brake = "brake_pedal", Start = "horn" } },
        };

        // The Model 3 wheel mapping (DirectInput names) has the same controls as NASCAR Arcade
        static readonly string[] wheelMappingPaths =
        {
            "{userpath}\\inputmapping\\odin_wheels.yml",
            "{systempath}\\resources\\inputmapping\\wheels\\odin_wheels.yml",
            "{userpath}\\inputmapping\\model3_wheels.yml",
            "{systempath}\\resources\\inputmapping\\wheels\\model3_wheels.yml"
        };

        private Controller _wheelController;
        private bool _wheelSearched;

        /// <summary>
        /// Controller of the first usable wheel (priority order of Wheel.GetConnectedWheels), for the games played with a wheel.
        /// </summary>
        private Controller GetWheelController()
        {
            if (_wheelSearched)
                return _wheelController;

            _wheelSearched = true;

            if (!_wheelControls.ContainsKey(_section) || !SystemConfig.getOptBoolean("use_wheel"))
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
        /// Wheel options of [players.<game>] : force feedback strength and rotation, Odin defaults when not set.
        /// </summary>
        private void SetupWheelOptions(IniFile ini)
        {
            string section = "players." + _section;

            if (!_wheelControls.ContainsKey(_section))
                return;

            BindIniFeature(ini, section, "wheel_force", "odin_wheel_force", "100");
            BindIniFeature(ini, section, "wheel_rotation", "odin_wheel_rotation", "270");
        }

        /// <summary>
        /// The wheel is player 1 : joy1 by its SDL3 GUID, device1 = joystick (the wheel is the device that turns).
        /// </summary>
        private void WriteWheelDevice(IniFile ini, string section, Controller wheel)
        {
            ini.Remove(section, "p1");
            ini.Remove(section, "joy1");
            ini.Remove(section, "p1_path");
            ini.Remove(section, "joy1_path");

            var sdl3 = wheel.Sdl3Controller;
            if (sdl3 != null && !string.IsNullOrEmpty(sdl3.GuidString) && sdl3.GuidString != new string('0', 32))
            {
                ini.WriteValue(section, "joy1", sdl3.GuidString + " " + (string.IsNullOrEmpty(sdl3.Name) ? wheel.Name : sdl3.Name));

                if (!string.IsNullOrEmpty(sdl3.Path))
                    ini.WriteValue(section, "joy1_path", sdl3.Path);
            }
            else
                SimpleLogger.Instance.Warning("[WHEELS] No SDL3 device found for " + wheel.Name + ", the wheel is not pinned.");

            ini.WriteValue(section, "device1", "joystick");
        }

        /// <summary>
        /// Wheel controls from odin_wheels.yml / model3_wheels.yml (container of the wheel type, else default), on the controls of the game.
        /// SDL opens wheels with its DirectInput driver : buttons and POVs keep their DirectInput number, axes are numbered
        /// in the X, Y, Z, Rx, Ry, Rz, Slider order without gap. Without mapping, Odin steers with the first axis (joy:axis0).
        /// </summary>
        private void AddWheelLines(Dictionary<string, List<string>> lines, Controller wheel)
        {
            var controls = _wheelControls[_section];
            var wheels = Wheel.GetConnectedWheels(Controllers);
            string wheelType = wheels.Count > 0 ? wheels[0].Type.ToString() : "default";

            string ymlFile = Controller.GetSystemYmlMappingFile("odin", "", "hikaru", wheelMappingPaths);
            YmlContainer wheelMapping = null;

            if (ymlFile != null)
            {
                try
                {
                    var yml = YmlFile.Load(ymlFile);
                    wheelMapping = yml?.Elements.FirstOrDefault(e => e.Name == wheelType) as YmlContainer
                                ?? yml?.Elements.FirstOrDefault(e => e.Name == "default") as YmlContainer;
                }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[WHEELS] Unable to read " + ymlFile + " : " + ex.Message); }
            }

            if (wheelMapping == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] No mapping for " + wheelType + ", Odin wheel defaults are used.");
                AddLineInput(lines, controls.Steering, "joy:axis0");
                return;
            }

            var buttons = wheelMapping.Elements.OfType<YmlElement>()
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .ToDictionary(e => e.Name, e => (e.Value ?? "").Trim(), StringComparer.InvariantCultureIgnoreCase);

            int[] axes = Wheel.GetSdlAxisIndexes(wheel);

            // Steering : left on the negative side of the axis, else turned over
            string steering = ConvertWheelAxis(GetWheelValue(buttons, "InputSteeringLeft"), axes, false);
            AddLineInput(lines, controls.Steering, steering ?? "joy:axis0");

            // Pedals : whole axis, Odin reads the side from where the pedal rests (:invert picks the low half when both pedals share one axis)
            AddWheelInput(lines, controls.Gas, ConvertWheelAxis(GetWheelValue(buttons, "InputAccelerator"), axes, true));

            // A brake button of the game (ATV Track) is pressed from half the pedal's way
            AddWheelInput(lines, controls.Brake, ConvertWheelAxis(GetWheelValue(buttons, "InputBrake"), axes, true, controls.BrakeIsButton));

            // Sequential gears (paddles), the buttons of a separate gear stick ("Shifter_") are not read
            AddWheelInput(lines, controls.GearUp, ConvertWheelButton(GetWheelValue(buttons, "InputGearShiftUp")));
            AddWheelInput(lines, controls.GearDown, ConvertWheelButton(GetWheelValue(buttons, "InputGearShiftDown")));
            AddWheelInput(lines, controls.View, ConvertWheelButton(GetWheelValue(buttons, "InputViewChange")));
            AddWheelInput(lines, controls.Start, ConvertWheelButton(GetWheelValue(buttons, "Start")));
            AddWheelInput(lines, "coin1", ConvertWheelButton(GetWheelValue(buttons, "Coin")));

            SimpleLogger.Instance.Info("[WHEELS] Wheel controls for " + wheelType + " loaded from " + ymlFile);
        }

        private static void AddWheelInput(Dictionary<string, List<string>> lines, string id, string input)
        {
            // id is null when the game has no such control
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(input))
                AddLineInput(lines, id, input);
        }

        private static string GetWheelValue(Dictionary<string, string> buttons, string key)
        {
            string value;
            return buttons.TryGetValue(key, out value) && value != null && value != "nul" ? value : "";
        }

        /// <summary>
        /// BUTTON10, POV1_UP (DirectInput names, from 1) to joy:button9, joy:hat0_up.
        /// </summary>
        private static string ConvertWheelButton(string dinputName)
        {
            if (string.IsNullOrEmpty(dinputName))
                return null;

            string name = dinputName.Trim().ToUpperInvariant();

            if (name.StartsWith("BUTTON"))
            {
                int button = name.Substring(6).ToInteger();
                return button > 0 ? "joy:button" + (button - 1) : null;
            }

            if (name.StartsWith("POV"))
            {
                int sep = name.IndexOf('_');
                if (sep < 4)
                    return null;

                int hat = name.Substring(3, sep - 3).ToInteger();
                string dir = name.Substring(sep + 1).ToLowerInvariant();
                return hat > 0 && (dir == "up" || dir == "down" || dir == "left" || dir == "right") ? "joy:hat" + (hat - 1) + "_" + dir : null;
            }

            return null;
        }

        /// <summary>
        /// XAXIS_NEG, RZAXIS_POS (DirectInput names) to a whole SDL axis : joy:axis<n>, turned over (:invert) when the wanted side is negative.
        /// pedal : the side given is where the pedal is pressed. Otherwise the side given is the left of the steering.
        /// half : a pedal pressing a button of the game, from half its way (Odin reads no whole axis on a button).
        /// </summary>
        private static string ConvertWheelAxis(string dinputName, int[] axes, bool pedal, bool half = false)
        {
            if (string.IsNullOrEmpty(dinputName))
                return null;

            int index = Wheel.GetSdlAxisIndex(dinputName, axes);
            if (index < 0)
                return null;

            string axis = "axis" + index;
            bool negative = dinputName.Trim().EndsWith("_NEG", StringComparison.InvariantCultureIgnoreCase);

            if (half)
                return "joy:" + (negative ? "-" : "+") + axis;

            // Pedal pressed toward the negative side, or steering with its left on the positive side : turned over
            bool invert = pedal ? negative : !negative;
            return "joy:" + axis + (invert ? ":invert" : "");
        }
    }
}