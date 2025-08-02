using System;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using System.IO;
using System.Collections.Generic;

namespace EmulatorLauncher
{
    partial class YmirGenerator
    {
        /// <summary>
        /// Cf. N/A
        /// </summary>
        /// <param name="ini"></param>
        /*private void UpdateSdlControllersWithHints(IniFile ini)
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // UpdateSdlControllersWithHints(ini);

            // Cleanup
            for (int i = 1; i <= 2; i++)
            {
                ini.ClearSection("Input.Port" + i + ".ControlPadBinds");
                ini.ClearSection("Input.Port" + i + ".AnalogPadBinds");
                ini.ClearSection("Input.Port" + i);
            }
            
            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(2))
                ConfigureInput(ini, controller, controller.PlayerIndex);

            ConfigureHotkeys(ini);
        }

        private void ConfigureInput(IniFile ini, Controller controller, int playerindex)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, playerindex);
            else
                ConfigureJoystick(ini, controller, playerindex);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            bool invertBumpers = SystemConfig.getOptBoolean("saturn_invert_triggers");
            string inputSection = "Input.Port" + playerindex;
            

            string peripheral = "'ControlPad'";

            if (SystemConfig.isOptSet("ymir_peripheral") && !string.IsNullOrEmpty(SystemConfig["ymir_peripheral"]))
            {
                peripheral = SystemConfig["ymir_peripheral"];
                peripheral = "'" + peripheral + "'";
            }

            string inputMapSection = "Input.Port" + playerindex + ".ControlPadBinds";

            // Check if 3D pad setting
            bool analogdpad = false;
            if (peripheral == "'AnalogPad'")
            {
                inputMapSection = "Input.Port" + playerindex + ".AnalogPadBinds";
                analogdpad = true;
                invertBumpers = false;
            }

            ini.WriteValue(inputSection, "PeripheralType", peripheral);

            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;

            // Check if controller has a specific mapping
            string guid = (ctrl.Guid.ToString()).ToLowerInvariant();
            bool needSatActivationSwitch = false;
            bool sat_pad = Program.SystemConfig["saturn_pad_ymir"] == "1";

            string saturnjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "saturnControllers.json");
            if (File.Exists(saturnjson))
            {
                try
                {
                    var saturnControllers = MegadriveController.LoadControllersFromJson(saturnjson);

                    if (saturnControllers != null)
                    {
                        MegadriveController saturnGamepad = MegadriveController.GetMDController("ymir", guid, saturnControllers);

                        if (saturnGamepad != null)
                        {
                            if (saturnGamepad.ControllerInfo != null)
                            {
                                if (saturnGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needSatActivationSwitch = saturnGamepad.ControllerInfo["needActivationSwitch"] == "true";

                                inputMapSection = "Input.Port" + playerindex + ".ControlPadBinds";

                                if (saturnGamepad.ControllerInfo.ContainsKey("peripheral"))
                                {
                                    peripheral = saturnGamepad.ControllerInfo["peripheral"];
                                    ini.WriteValue(inputSection, "PeripheralType", "'" + peripheral + "'");
                                    inputMapSection = "Input.Port" + playerindex + "." + peripheral + "Binds";
                                }

                                if (needSatActivationSwitch && !sat_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific Saturn mapping needs to be activated for this controller.");
                                    goto BypassSATControllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + saturnGamepad.Name);

                            if (saturnGamepad.Mapping != null)
                            {
                                // write mapping here
                                foreach (var button in saturnGamepad.Mapping)
                                {
                                    string key = button.Key;
                                    string value = button.Value;

                                    if (value.Contains("_"))
                                    {
                                        var newValue = value.Split('_');
                                        ini.WriteValue(inputMapSection, key, "[ '" + newValue[0] + "@" + index + "', '" + newValue[1] + "@" + index + "' ]");
                                    }
                                    else
                                        ini.WriteValue(inputMapSection, key, "[ '" + value + "@" + index + "' ]");
                                }

                                SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());

                                return;
                            }
                            else
                                SimpleLogger.Instance.Info("[INFO] Missing mapping for Saturn Gamepad, falling back to standard mapping.");
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Saturn controller.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            BypassSATControllers:

            string padlayout = "standard";
            if (SystemConfig.isOptSet("saturn_padlayout") && !string.IsNullOrEmpty(SystemConfig["saturn_padlayout"]))
                padlayout = SystemConfig["saturn_padlayout"];

            if (padlayout == "lr_yz" || padlayout == "lr_xz")
            {
                ini.WriteValue(inputMapSection, "A", "[ 'GamepadX@" + index + "' ]");

                if (analogdpad)
                {
                    ini.WriteValue(inputMapSection, "AnalogL", "[ 'GamepadLeftTriggerButton@" + index + "' ]");
                    ini.WriteValue(inputMapSection, "AnalogR", "[ 'GamepadRightTriggerButton@" + index + "' ]");
                    ini.WriteValue(inputMapSection, "AnalogStick", "[ 'GamepadLeftStick@" + index + "' ]");
                }

                ini.WriteValue(inputMapSection, "B", "[ 'GamepadA@" + index + "' ]");
                ini.WriteValue(inputMapSection, "C", "[ 'GamepadB@" + index + "' ]");
            }
            
            else
            {
                ini.WriteValue(inputMapSection, "A", "[ 'GamepadA@" + index + "' ]");

                if (analogdpad)
                {
                    ini.WriteValue(inputMapSection, "AnalogL", "[ 'GamepadLeftTrigger@" + index + "' ]");
                    ini.WriteValue(inputMapSection, "AnalogR", "[ 'GamepadRightTrigger@" + index + "' ]");
                    ini.WriteValue(inputMapSection, "AnalogStick", "[ 'GamepadLeftStick@" + index + "' ]");
                }

                ini.WriteValue(inputMapSection, "B", "[ 'GamepadB@" + index + "' ]");
                ini.WriteValue(inputMapSection, "C", invertBumpers ? "[ 'GamepadRightTriggerButton@" + index + "' ]" : "[ 'GamepadRightBumper@" + index + "' ]");
            }

            if (!analogdpad)
                ini.WriteValue(inputMapSection, "DPad", "[ 'GamepadLeftStick@" + index + "' ]");
            else
                ini.WriteValue(inputMapSection, "DPad", "[ 'GamepadDPad@" + index + "' ]");

            ini.WriteValue(inputMapSection, "Down", "[ 'GamepadDpadDown@" + index + "' ]");
            ini.WriteValue(inputMapSection, "L", invertBumpers ? "[ 'GamepadLeftBumper@" + index + "' ]" : "[ 'GamepadLeftTriggerButton@" + index + "' ]");
            ini.WriteValue(inputMapSection, "Left", "[ 'GamepadDpadLeft@" + index + "' ]");
            ini.WriteValue(inputMapSection, "R", invertBumpers ? "[ 'GamepadRightBumper@" + index + "' ]" : "[ 'GamepadRightTriggerButton@" + index + "' ]");
            ini.WriteValue(inputMapSection, "Right", "[ 'GamepadDpadRight@" + index + "' ]");
            ini.WriteValue(inputMapSection, "Start", "[ 'GamepadStart@" + index + "' ]");
            
            if (analogdpad)
                ini.WriteValue(inputMapSection, "SwitchMode", "[ 'GamepadLeftThumb@" + index + "' ]");

            ini.WriteValue(inputMapSection, "Up", "[ 'GamepadDpadUp@" + index + "' ]");

            if (padlayout == "lr_yz")
            {
                ini.WriteValue(inputMapSection, "X", "[ 'GamepadY@" + index + "' ]");
                ini.WriteValue(inputMapSection, "Y", invertBumpers ? "[ 'GamepadLeftTriggerButton@" + index + "' ]" : "[ 'GamepadLeftBumper@" + index + "' ]");
                ini.WriteValue(inputMapSection, "Z", invertBumpers ? "[ 'GamepadRightTriggerButton@" + index + "' ]" : "[ 'GamepadRightBumper@" + index + "' ]");
            }

            else if (padlayout == "lr_xz")
            {
                ini.WriteValue(inputMapSection, "X", invertBumpers ? "[ 'GamepadLeftTriggerButton@" + index + "' ]" : "[ 'GamepadLeftBumper@" + index + "' ]");
                ini.WriteValue(inputMapSection, "Y", "[ 'GamepadY@" + index + "' ]");
                ini.WriteValue(inputMapSection, "Z", invertBumpers ? "[ 'GamepadRightTriggerButton@" + index + "' ]" : "[ 'GamepadRightBumper@" + index + "' ]");
            }

            else
            {
                ini.WriteValue(inputMapSection, "X", "[ 'GamepadX@" + index + "' ]");
                ini.WriteValue(inputMapSection, "Y", "[ 'GamepadY@" + index + "' ]");
                ini.WriteValue(inputMapSection, "Z", invertBumpers ? "[ 'GamepadLeftTriggerButton@" + index + "' ]" : "[ 'GamepadLeftBumper@" + index + "' ]");
            }

            string deadzone = "0.15";
            if (SystemConfig.isOptSet("ymir_deadzone") && !string.IsNullOrEmpty(SystemConfig["ymir_deadzone"]))
                deadzone = SystemConfig["ymir_deadzone"];

            string triggerDeadzone = "0.15";
            if (SystemConfig.isOptSet("ymir_trigger_deadzone") && !string.IsNullOrEmpty(SystemConfig["ymir_trigger_deadzone"]))
                triggerDeadzone = SystemConfig["ymir_trigger_deadzone"];

            ini.WriteValue("Input", "GamepadAnalogToDigitalSensitivity", deadzone);
            ini.WriteValue("Input", "GamepadLSDeadzone", triggerDeadzone);
            ini.WriteValue("Input", "GamepadRSDeadzone", triggerDeadzone);
        }

        private Dictionary<string, string> hotkeys = new Dictionary<string, string>()
        {
            { "OpenSettings", "F10" },
            { "PauseResume", "F6" },
            { "Rewind", "F4" },
            { "ToggleFullScreen", "Alt+Return" },
            { "TurboSpeed", "F5" }
        };

        private Dictionary<string, string> stateKeys = new Dictionary<string, string>()
        {
            { "QuickLoadState", "F3" },
            { "QuickSaveState", "F2" }
        };

        private void ConfigureHotkeys(IniFile ini)
        {
            foreach (var hk in hotkeys)
            {
                ini.WriteValue("Hotkeys", hk.Key, "[ '" + hk.Value + "' ]");
            }

            foreach (var hk in stateKeys)
            {
                ini.WriteValue("Hotkeys.SaveStates", hk.Key, "[ '" + hk.Value + "' ]");
            }
        }
    }
}
