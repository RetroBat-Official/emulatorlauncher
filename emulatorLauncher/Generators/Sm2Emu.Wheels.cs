using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Sm2EmuGenerator
    {
        // sm2emu_wheels.yml controls to sm2-emu settings (config.cpp, wheel_button_*)
        private static readonly Dictionary<string, string> _sm2WheelRoles = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "Start", "wheel_button_start" }, { "Coin", "wheel_button_coin" },
            { "Button1", "wheel_button_1" }, { "Button2", "wheel_button_2" }, { "Button3", "wheel_button_3" }, { "Button4", "wheel_button_4" },
            { "GearUp", "wheel_button_gear_up" }, { "GearDown", "wheel_button_gear_down" },
            { "Test", "wheel_button_test" }, { "Service", "wheel_button_service" }, { "Menu", "wheel_button_menu" },
            { "Gear1", "wheel_button_gear_1" }, { "Gear2", "wheel_button_gear_2" }, { "Gear3", "wheel_button_gear_3" }, { "Gear4", "wheel_button_gear_4" },
            { "GearNeutral", "wheel_button_gear_neutral" }
        };

        private static readonly string[] _sm2ShifterRoles = new string[] { "Gear1", "Gear2", "Gear3", "Gear4", "GearNeutral" };

        static readonly string[] wheelMappingPaths =
        {
            "{userpath}\\inputmapping\\sm2emu_wheels.yml",
            "{systempath}\\resources\\inputmapping\\wheels\\sm2emu_wheels.yml"
        };

        /// <summary>
        /// sm2-emu uses one wheel (the first SDL3 joystick typed as a wheel). Only the buttons are written,
        /// the steering and pedal axes are detected by the emulator.
        /// </summary>
        private void SetupWheel(IniFile ini)
        {
            if (!SystemConfig.getOptBoolean("use_wheel"))
                return;

            BindBoolIniFeatureOn(ini, "", "wheel_ffb", "sm2emu_wheel_ffb", "true", "false");

            var wheels = Wheel.GetConnectedWheels(Controllers);
            if (wheels.Count < 1)
            {
                SimpleLogger.Instance.Info("[WHEELS] No wheel found.");
                return;
            }

            string wheelType = wheels[0].Type.ToString();
            SimpleLogger.Instance.Info("[WHEELS] Wheel type identified : " + wheelType);

            string ymlFile = Controller.GetSystemYmlMappingFile("sm2-emu", "", "model2", wheelMappingPaths);
            if (ymlFile == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] sm2emu_wheels.yml not found, the sm2-emu wheel buttons are kept.");
                return;
            }

            YmlContainer wheelMapping = null;
            try
            {
                var yml = YmlFile.Load(ymlFile);
                wheelMapping = yml?.Elements.FirstOrDefault(e => e.Name == wheelType) as YmlContainer;
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("[WHEELS] Unable to read " + ymlFile + " : " + ex.Message); }

            if (wheelMapping == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] No mapping for " + wheelType + " in " + ymlFile + ", the sm2-emu wheel buttons are kept.");
                return;
            }

            var buttons = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var element in wheelMapping.Elements.OfType<YmlElement>())
            {
                int button;
                if (_sm2WheelRoles.ContainsKey(element.Name) && int.TryParse(element.Value, out button))
                    buttons[element.Name] = button;
            }

            bool noShifter = SystemConfig.getOptBoolean("wheel_nogearstick");

            // Every control is written, so that a control missing in the container does not keep a button of another wheel
            foreach (var role in _sm2WheelRoles)
            {
                int button;
                if (!buttons.TryGetValue(role.Key, out button) || (noShifter && _sm2ShifterRoles.Contains(role.Key)))
                    button = -1;

                ini.WriteValue("", role.Value, button.ToString());
            }

            SimpleLogger.Instance.Info("[WHEELS] Wheel buttons for " + wheelType + " loaded from " + ymlFile);
        }
    }
}
