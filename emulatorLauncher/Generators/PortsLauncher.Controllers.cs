using System.IO;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using System;
using System.Collections.Generic;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using Newtonsoft.Json.Linq;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        #region cgenius

        private void ConfigureCGeniusControls(IniFile ini)
        {
            if (_emulator != "cgenius")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // clear existing pad sections of ini file
            for (int i = 0; i < 4; i++)
            {
                ini.ClearSection("input" + i.ToString());
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
            {
                ConfigureCGeniusInput(ini, controller, controller.PlayerIndex - 1);
            }
        }

        private void ConfigureCGeniusInput(IniFile ini, Controller ctrl, int padIndex)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            if (ctrl.IsKeyboard)
            {
                ini.WriteValue("input" + padIndex, "Back", "Key 27 (Escape)");
                ini.WriteValue("input" + padIndex, "Camlead", "Key 99 (C)");
                ini.WriteValue("input" + padIndex, "Down", "Key 1073741905 (Down)");
                ini.WriteValue("input" + padIndex, "Fire", "Key 32 (Space)");
                ini.WriteValue("input" + padIndex, "Help", "Key 1073741882 (F1)");
                ini.WriteValue("input" + padIndex, "Jump", "Key 1073742048 (Left Ctrl)");
                ini.WriteValue("input" + padIndex, "Left", "Key 1073741904 (Left)");
                ini.WriteValue("input" + padIndex, "Lower-Left", "Key 1073741901 (End)");
                ini.WriteValue("input" + padIndex, "Lower-Right", "Key 1073741902 (PageDown)");
                ini.WriteValue("input" + padIndex, "Pogo", "Key 1073742050 (Left Alt)");
                ini.WriteValue("input" + padIndex, "Quickload", "Key 1073741890 (F9)");
                ini.WriteValue("input" + padIndex, "Quicksave", "Key 1073741887 (F6)");
                ini.WriteValue("input" + padIndex, "Right", "Key 1073741903 (Right)");
                ini.WriteValue("input" + padIndex, "Run", "Key 1073742049 (Left Shift)");
                ini.WriteValue("input" + padIndex, "Status", "Key 13 (Return)");
                ini.WriteValue("input" + padIndex, "Up", "Key 1073741906 (Up)");
                ini.WriteValue("input" + padIndex, "Upper-Left", "Key 1073741898 (Home)");
                ini.WriteValue("input" + padIndex, "Upper-Right", "Key 1073741899 (PageUp)");
            }
            else
            {
                if (ctrl == null)
                    return;

                InputConfig joy = ctrl.Config;
                if (joy == null)
                    return;

                string joyPad = "Joy" + (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index) + "-";

                foreach (var button in cgeniusMapping)
                {
                    if (padIndex != 0 && (button.Value == "Back" || button.Value == "Help"))
                        continue;

                    InputKey toSet = button.Key;
                    
                    if (SystemConfig.isOptSet("cgenius_analogPad") && SystemConfig.getOptBoolean("cgenius_analogPad"))
                    {
                        if (button.Key == InputKey.up)
                            toSet = InputKey.leftanalogdown;
                        else if (button.Key == InputKey.down)
                            toSet = InputKey.leftanalogdown;
                        else if (button.Key == InputKey.left)
                            toSet = InputKey.leftanalogleft;
                        else if (button.Key == InputKey.right)
                            toSet = InputKey.leftanalogright;
                    }

                    var input = ctrl.Config[toSet];
                    if (input != null)
                        ini.WriteValue("input" + padIndex, button.Value, joyPad + GetSDLInputName(ctrl, toSet, "cgenius"));
                    else
                        ini.WriteValue("input" + padIndex, button.Value, "Key 0 ()");
                }

                if (padIndex == 0)
                {
                    ini.WriteValue("input" + padIndex, "Quickload", "Key 1073741890 (F9)");
                    ini.WriteValue("input" + padIndex, "Quicksave", "Key 1073741887 (F6)");
                }
                else
                {
                    ini.WriteValue("input" + padIndex, "Quickload", "Key 0 ()");
                    ini.WriteValue("input" + padIndex, "Quicksave", "Key 0 ()");
                }
                ini.WriteValue("input" + padIndex, "Lower-Left", "Key 1073741901 (End)");
                ini.WriteValue("input" + padIndex, "Lower-Right", "Key 1073741902 (PageDown)");
                ini.WriteValue("input" + padIndex, "Upper-Left", "Key 1073741898 (Home)");
                ini.WriteValue("input" + padIndex, "Upper-Right", "Key 1073741899 (PageUp)");

                BindBoolIniFeature(ini, "input" + padIndex, "Analog", "cgenius_analogPad", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "TwoButtonFiring", "cgenius_TwoButtonFiring", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "SuperPogo", "cgenius_SuperPogo", "true", "false");
                BindBoolIniFeatureOn(ini, "input" + padIndex, "ImpossiblePogo", "cgenius_ImpossiblePogo", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "AutoFire", "cgenius_AutoFire", "true", "false");
            }
        }
        #endregion

        #region soh
        private void ConfigureSOHControls(JObject controllers)
        {
            if (_emulator != "soh")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            JObject deck;

            if (controllers["Deck"] == null)
            {
                deck = new JObject();
                controllers["Deck"] = deck;
            }
            else
                deck = (JObject)controllers["Deck"];
            
            // clear existing pad sections of ini file
            for (int i = 0; i < 4; i++)
                deck["Slot_" + i] = "Disconnected";

            int slotindex = 0;
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
            {
                // Do not configure controllers that do not have analog stick
                if (controller.Config[InputKey.rightanalogup] == null)
                {
                    SimpleLogger.Instance.Info("[CONTROLS] Ignoring controller " + controller.Guid.ToString() + " : no analog sticks.");
                    continue;
                }

                ConfigureSOHInput(controllers, deck, controller, slotindex);
                slotindex++;
            }
        }

        private void ConfigureSOHInput(JObject controllers, JObject deck, Controller ctrl, int slotindex)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            if (ctrl.IsKeyboard)
                return;

            if (ctrl.SdlController == null)
            {
                SimpleLogger.Instance.Info("[CONTROLS] Controller not known in SDL database, no configuration possible.");
                return;
            }

            SimpleLogger.Instance.Info("[CONTROLS] Configuring controller " + ctrl.Guid == null ? ctrl.DevicePath.ToString() : ctrl.Guid.ToString());

            JObject jsonCtrl;
            JObject ctrlSlot;
            JObject gyro;
            JObject mappings;
            JObject rumble;

            InputConfig joy = ctrl.Config;
            string guid = ctrl.GetSdlGuid(Common.Joysticks.SdlVersion.SDL2_30, true).ToLowerInvariant();

            SimpleLogger.Instance.Info("[CONTROLS] Configuring slot : " + slotindex.ToString());

            deck["Slot_" + slotindex] = guid;

            if (controllers[guid] == null)
            {
                jsonCtrl = new JObject();
                controllers[guid] = jsonCtrl;
            }
            else
                jsonCtrl = (JObject)controllers[guid];

            if (jsonCtrl["Slot_" + slotindex] == null)
            {
                ctrlSlot = new JObject();
                jsonCtrl["Slot_" + slotindex] = ctrlSlot;
            }
            else
                ctrlSlot = (JObject)jsonCtrl["Slot_" + slotindex];

            double deadzone = 15.0;
            if (SystemConfig.isOptSet("soh_deadzone") && !string.IsNullOrEmpty(SystemConfig["soh_deadzone"]))
                deadzone = SystemConfig["soh_deadzone"].ToDouble();

            // Set deadzones
            List<double> axisdeadzones = new List<double>();
            axisdeadzones.Add(deadzone);    // left stick
            axisdeadzones.Add(deadzone);    // left stick
            axisdeadzones.Add(deadzone);    // right stick
            axisdeadzones.Add(deadzone);    // right stick
            axisdeadzones.Add(deadzone);    // ?
            axisdeadzones.Add(deadzone);    // ?
            ctrlSlot["AxisDeadzones"] = JArray.FromObject(axisdeadzones);

            // Gyro
            if (ctrlSlot["Gyro"] == null)
            {
                gyro = new JObject();
                ctrlSlot["Gyro"] = gyro;
            }
            else
                gyro = (JObject)ctrlSlot["Gyro"];

            double gyroSensitivity = 1.0;
            if (SystemConfig.isOptSet("soh_gyroSensivity") && !string.IsNullOrEmpty(SystemConfig["soh_gyroSensivity"]))
                gyroSensitivity = (SystemConfig["soh_gyroSensivity"].ToDouble() / 100);

            gyro["Enabled"] = SystemConfig.getOptBoolean("soh_gyro") ? true : false;

            List<double> gyrodata = new List<double>();
            gyrodata.Add(0.0);                  // driftX
            gyrodata.Add(0.0);                  // driftY
            gyrodata.Add(gyroSensitivity);      // Sensitivity
            ctrlSlot["GyroData"] = JArray.FromObject(gyrodata);

            // Mappings
            SimpleLogger.Instance.Info("[CONTROLS] Re-creating mapping section for " + ctrl.Guid.ToString() + " and slot " + slotindex.ToString());
            ctrlSlot.Remove("Mappings");
            
            if (ctrlSlot["Mappings"] == null)
            {
                mappings = new JObject();
                ctrlSlot["Mappings"] = mappings;
            }
            else
                mappings = (JObject)ctrlSlot["Mappings"];

            // Special mapping for n64 style controllers
            string n64guid = ctrl.Guid.ToLowerInvariant();
            string n64json = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
            bool needActivationSwitch = false;
            bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");
            
            if (File.Exists(n64json))
            {
                try
                {
                    var n64Controllers = N64Controller.LoadControllersFromJson(n64json);

                    if (n64Controllers != null)
                    {
                        N64Controller n64Gamepad = N64Controller.GetN64Controller("soh", n64guid, n64Controllers);

                        if (n64Gamepad != null)
                        {
                            if (n64Gamepad.ControllerInfo != null)
                            {
                                if (n64Gamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needActivationSwitch = n64Gamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needActivationSwitch && !n64_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific n64 mapping needs to be activated for this controller.");
                                    goto BypassSPecialControllers;
                                }

                                SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + n64Gamepad.Name);

                                foreach (var button in n64Gamepad.Mapping)
                                    mappings[button.Key] = button.Value.ToInteger();

                                goto Rumble;
                            }
                        }
                    }
                }
                catch { }
            }

            BypassSPecialControllers:

            SimpleLogger.Instance.Info("[CONTROLS] Configuring mapping section");
            foreach (var button in sohMapping)
            {
                bool forceAxisPlus = false;
                InputKey key = button.Value;
                var input = ctrl.Config[key];

                if (input == null)
                    continue;

                if (input != null && input.Type == "axis" && input.Value > 0)
                    forceAxisPlus = true;
                
                string sdlID = GetSDLInputName(ctrl, key, "soh", forceAxisPlus);

                if (sdlID == null || sdlID == "")
                    continue;

                mappings[sdlID] = button.Key;
            }


            // Rumble
            Rumble:
            if (ctrlSlot["Rumble"] == null)
            {
                rumble = new JObject();
                ctrlSlot["Rumble"] = rumble;
            }
            else
                rumble = (JObject)ctrlSlot["Rumble"];

            rumble["Enabled"] = SystemConfig.getOptBoolean("soh_rumble") ? true : false;

            double rumbleStrength = 1.0;
            if (SystemConfig.isOptSet("soh_rumblestrength") && !string.IsNullOrEmpty(SystemConfig["soh_rumblestrength"]))
                rumbleStrength = (SystemConfig["soh_rumblestrength"].ToDouble() / 100);

            rumble["Strength"] = rumbleStrength;

            // Other
            ctrlSlot["UseStickDeadzoneForButtons"] = true;
            ctrlSlot["Version"] = 2;
        }
        #endregion

        #region sonic3air
        private void ConfigureSonic3airControls(string configFolder, DynamicJson settings)
        {
            if (_emulator != "sonic3air")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!Controllers.Any(c => !c.IsKeyboard))
                return;

            settings["PreferredGamepadPlayer1"] = string.Empty;
            settings["PreferredGamepadPlayer2"] = string.Empty;

            string inputSettingsFile = Path.Combine(configFolder, "settings_input.json");

            var inputJson = DynamicJson.Load(inputSettingsFile);

            var inputDevices = inputJson.GetOrCreateContainer("InputDevices");

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(2))
            {
                string deviceName = controller.Name;
                var device = inputDevices.GetOrCreateContainer(deviceName);
                bool isXinput = controller.IsXInputDevice;

                string[] deviceNames = new string[] { deviceName };
                device.SetObject("DeviceNames", deviceNames);

                string[] up = new string[] { GetSDLInputName(controller, InputKey.up, "sonic3air") };
                string[] down = new string[] { GetSDLInputName(controller, InputKey.down, "sonic3air") };
                string[] left = new string[] { GetSDLInputName(controller, InputKey.left, "sonic3air") };
                string[] right = new string[] { GetSDLInputName(controller, InputKey.right, "sonic3air") };
                string[] a = new string[] { GetSDLInputName(controller, InputKey.a, "sonic3air") };
                string[] b = new string[] { GetSDLInputName(controller, InputKey.b, "sonic3air") };
                string[] x = new string[] { GetSDLInputName(controller, InputKey.y, "sonic3air") };
                string[] y = new string[] { GetSDLInputName(controller, InputKey.x, "sonic3air") };
                string[] start = new string[] { GetSDLInputName(controller, InputKey.start, "sonic3air") };
                string[] back = new string[] { GetSDLInputName(controller, InputKey.select, "sonic3air") };
                string[] l = new string[] { GetSDLInputName(controller, InputKey.pageup, "sonic3air") };
                string[] r = new string[] { GetSDLInputName(controller, InputKey.pagedown, "sonic3air") };
                device.SetObject("Up", up);
                device.SetObject("Down", down);
                device.SetObject("Left", left);
                device.SetObject("Right", right);
                device.SetObject("A", a);
                device.SetObject("B", b);
                device.SetObject("X", x);
                device.SetObject("Y", y);
                device.SetObject("Start", start);
                device.SetObject("Back", back);
                device.SetObject("L", l);
                device.SetObject("R", r);

                if (controller.PlayerIndex == 1)
                    settings["PreferredGamepadPlayer1"] = deviceName;
                if (controller.PlayerIndex == 2)
                    settings["PreferredGamepadPlayer2"] = deviceName;
            }

            inputJson.Save();
        }
        #endregion

        #region general tools
        /// <summary>
        /// Method to retrieve SDL button information
        /// </summary>
        /// <param name="c">Controller</param>
        /// <param name="key">InputKey</param>
        /// <param name="port">Name of the port</param>
        /// <param name="forceAxisSignPositive">Use for triggers for example to force positive axis</param>
        /// <returns></returns>
        private static string GetSDLInputName(Controller c, InputKey key, string port = "default", bool forceAxisSignPositive = false)
        {
            Int64 pid;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    if (port == "cgenius")
                        return "B" + pid;
                    else if (port == "sonic3air")
                        return "Button" + pid;
                    else if (port == "soh" && c.IsXInputDevice)
                    {
                        if (sohXinputRemap.ContainsKey(pid))
                            return sohXinputRemap[pid].ToString();
                        else
                            return pid.ToString();
                    }
                    else
                        return pid.ToString();
                }

                else if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1:
                            if (port == "cgenius")
                                return "H1";
                            else if (port == "soh")
                                return "11";
                            else
                                return "Pov0";
                        case 2:
                            if (port == "cgenius")
                                return "H2";
                            else if (port == "soh")
                                return "14";
                            else
                                return "Pov1";
                        case 4:
                            if (port == "cgenius")
                                return "H4";
                            else if (port == "soh")
                                return "12";
                            else
                                return "Pov2";
                        case 8:
                            if (port == "cgenius")
                                return "H8";
                            else if (port == "soh")
                                return "13";
                            else
                                return "Pov3";
                    }
                }

                else if (input.Type == "axis")
                {
                    pid = input.Id;
                    if (port == "sonic3air")
                    {
                        switch (pid)
                        {
                            case 0:
                                if (revertAxis) return "Axis1";
                                else return "Axis0";
                            case 1:
                                if (revertAxis) return "Axis3";
                                else return "Axis2";
                            case 2:
                                if (revertAxis) return "Axis5";
                                else return "Axis4";
                            case 3:
                                if (revertAxis) return "Axis7";
                                else return "Axis8";
                            case 4: return "Axis9";
                            case 5: return "Axis11";
                        }
                    }
                    else if (port == "soh")
                    {
                        if (revertAxis || forceAxisSignPositive) return (512 + pid).ToString();
                        else return "-" + (512 + pid).ToString();
                    }
                    else
                    {
                        if (revertAxis) return "A" + pid + "-";
                        else return "A" + pid + "+";
                    }
                }
            }
            return "";
        }
        #endregion

        #region Dictionaries
        /// <summary>
        /// Dictionaries and mappings can be added below if necessary, keep alphabetical order and name it with port name
        /// </summary>
        private InputKeyMapping cgeniusMapping = new InputKeyMapping
        {
            { InputKey.select,      "Back" },
            { InputKey.pagedown,    "Camlead" },
            { InputKey.down,        "Down" },
            { InputKey.b,           "Fire" },
            { InputKey.start,       "Help" },
            { InputKey.a,           "Jump" },
            { InputKey.left,        "Left" },
            { InputKey.x,           "Pogo" },
            { InputKey.right,       "Right" },
            { InputKey.y,           "Run" },
            { InputKey.pageup,      "Status" },
            { InputKey.up,          "Up" }
        };

        private Dictionary<int, InputKey> sohMapping = new Dictionary<int, InputKey>()
        {
            { 32768,      InputKey.a },                     // A
            { 16384,      InputKey.y },                     // B
            { 32,         InputKey.pageup },                // L
            { 16,         InputKey.pagedown },              // R
            { 8192,       InputKey.l2 },                    // Z
            { 4096,       InputKey.start },                 // Start
            { 2048,       InputKey.up },                    // D-PAD
            { 1024,       InputKey.down },
            { 512,        InputKey.left },
            { 256,        InputKey.right },
            { 524288,     InputKey.leftanalogup },          // Analog stick
            { 262144,     InputKey.leftanalogdown },
            { 65536,      InputKey.leftanalogleft },
            { 131072,     InputKey.leftanalogright },
            //{ 1048576,    InputKey.rightanalogup },       // right analog (not used)
            //{ 2097152,    InputKey.rightanalogdown },
            //{ 4194304,    InputKey.rightanalogleft },
            //{ 8388608,    InputKey.rightanalogright },
            { 8,          InputKey.rightanalogup },         // C-Stick
            { 4,          InputKey.rightanalogdown },
            { 2,          InputKey.rightanalogleft },
            { 1,          InputKey.rightanalogright }
        };

        private static Dictionary<long, long> sohXinputRemap = new Dictionary<long, long>()
        {
            { 6, 4 },
            { 7, 6 },
            { 4, 9 },
            { 5, 10 },
            { 8, 7 },
            { 9, 8 },
            { 10, 5 }
        };
        #endregion
    }
}