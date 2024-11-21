using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class SSFGenerator : Generator
    {
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for SSF");

            // clear existing Input section of ini file
            ini.ClearSection("Input");

            // Set controllers to use dinput
            ini.WriteValue("Program1", "UseXInput", "\"" + "0" + "\"");
            ini.WriteValue("Input", "EnableRapid", "\"0\"");
            ini.WriteValue("Input", "First", "\"1\"");

            // Multitap Management
            if (Controllers.Count > 7)
            {
                ini.WriteValue("Input", "PortFlag0", "\"1\"");
                ini.WriteValue("Input", "PortFlag1", "\"1\"");
            }
            else if (Controllers.Count > 2)
            {
                ini.WriteValue("Input", "PortFlag0", "\"1\"");
                ini.WriteValue("Input", "PortFlag1", "\"0\"");
            }
            else
            {
                ini.WriteValue("Input", "PortFlag0", "\"0\"");
                ini.WriteValue("Input", "PortFlag1", "\"0\"");
            }

            // Reset values
            for (int i=0; i<2; i++)
            {
                string rapidIndex = "Rapid" + i + "_";
                string variableIndex = "VariableRapid" + i + "_";
                string type = "PadType" + i + "_";
                string padMapping = "Pad" + i + "_";

                for (int j=0; j<6; j++)
                {
                    string rapidKey = rapidIndex + j;
                    string variableKey = variableIndex + j;
                    string typeKey = type + j;
                    string padMappingKey = padMapping + j + "_";
                    ini.WriteValue("Input", rapidKey, "\"0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0\"");
                    ini.WriteValue("Input", variableKey, "\"0\"");
                    ini.WriteValue("Input", typeKey, "\"5\"");

                    for (int k=0; k<4; k++)
                    {
                        string padMappingKeyToClean = padMappingKey + k;
                        ini.WriteValue("Input", padMappingKeyToClean, "\"0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0\"");
                    }
                }
            }

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(12))
                ConfigureInput(ini, controller);
        }

        private void ConfigureInput(IniFile ini, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex);
        }

        /// <summary>
        /// Gamepad configuration
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="ctrl"></param>
        /// <param name="playerIndex"></param>
        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            //Get controller index
            int index = ctrl.DirectInput != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex;

            // Retrieve ini pad index   (for now only control pad is available)
            string padNumber = "Pad" + ssfPadOrder[playerIndex];
            string padTypeValue = "0";
            string padKey = padNumber + "_" + padTypeValue;

            string padType = "PadType" + ssfPadOrder[playerIndex];

            ini.WriteValue("Input", padType, "\"" + "0" + "\"");

            // Define variables to be used
            bool isXinput = ctrl.IsXInputDevice;
            bool invertTriggers = SystemConfig.getOptBoolean("saturn_invert_triggers");
            SdlToDirectInput dinputController;

            // Find controllerMapping in Gamecontrollerdb.txt file
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid = (ctrl.Guid.ToString()).ToLowerInvariant();

            // Special mapping for saturn-like controllers in json file
            bool needSatActivationSwitch = false;
            bool sat_pad = Program.SystemConfig["saturn_pad_ssf"] == "1" || Program.SystemConfig["saturn_pad_ssf"] == "2";

            string saturnjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "saturnControllers.json");
            if (File.Exists(saturnjson))
            {
                try
                {
                    var saturnControllers = MegadriveController.LoadControllersFromJson(saturnjson);

                    if (saturnControllers != null)
                    {
                        MegadriveController saturnGamepad = MegadriveController.GetMDController("ssf", guid, saturnControllers);

                        if (saturnGamepad != null)
                        {
                            if (saturnGamepad.ControllerInfo != null)
                            {
                                if (saturnGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needSatActivationSwitch = saturnGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needSatActivationSwitch && !sat_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific Saturn mapping needs to be activated for this controller.");
                                    goto BypassSATControllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + saturnGamepad.Name);

                            if (saturnGamepad.Mapping != null)
                            {
                                string input = saturnGamepad.Mapping["buttons"];
                                if (Program.SystemConfig["saturn_pad_ssf"] == "2" && saturnGamepad.Mapping["buttons_anal"] != null)
                                    input = saturnGamepad.Mapping["buttons_anal"];
                                
                                ini.WriteValue("Input", padKey, "\"" + input + "\"");

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

            guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available");
                return;
            }
            SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file found in tools folder. Searching for controller " + guid);

            dinputController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

            if (dinputController == null)
            {
                SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt does not contain mapping for the controller " + guid + ". Controller mapping will not be available");
                return;
            }

            //Write button mapping in ini file
            // dpadStart = 34822;
            // buttonStart = 0;
            // axisStart = 32769;
            // Increment by player : 65536

            List<string> buttonMapping = new List<string>();
            if (SystemConfig.getOptBoolean("ssf_use_analog"))
            {
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.joystick1up, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.joystick1down, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.joystick1left, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.joystick1right, dinputController, isXinput)) + (index * 65536)).ToString());
            }

            else
            {
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.up, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.down, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.left, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.right, dinputController, isXinput)) + (index * 65536)).ToString());
            }

            if (SystemConfig.isOptSet("saturn_padlayout") && SystemConfig["saturn_padlayout"] == "lr_yz")
            {
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.y, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.a, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.b, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.x, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.l2 : InputKey.pageup, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.r2 : InputKey.pagedown, dinputController, isXinput)) + (index * 65536)).ToString());
            }

            else if (SystemConfig.isOptSet("saturn_padlayout") && SystemConfig["saturn_padlayout"] == "lr_xz")
            {
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.y, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.a, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.b, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.l2 : InputKey.pageup, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.x, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.r2 : InputKey.pagedown, dinputController, isXinput)) + (index * 65536)).ToString());
            }

            else
            {
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.a, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.b, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.r2 : InputKey.pagedown, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.y, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.x, dinputController, isXinput)) + (index * 65536)).ToString());
                buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.l2 : InputKey.pageup, dinputController, isXinput)) + (index * 65536)).ToString());
            }

            buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.pageup : InputKey.l2, dinputController, isXinput, true)) + (index * 65536)).ToString());
            buttonMapping.Add("1/" + ((GetInputCode(ctrl, invertTriggers ? InputKey.pagedown : InputKey.r2, dinputController, isXinput, true)) + (index * 65536)).ToString());
            buttonMapping.Add("1/" + ((GetInputCode(ctrl, InputKey.start, dinputController, isXinput)) + (index * 65536)).ToString());

            buttonMapping.Add("0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0");

            string mapping = string.Join("/", buttonMapping);

            ini.WriteValue("Input", padKey, "\"" + mapping + "\"");

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private static int GetInputCode(Controller c, InputKey key, SdlToDirectInput ctrl, bool isXinput = false, bool trigger = false)
        {
            key = key.GetRevertedAxis(out bool revertAxis);

            string esName = (c.Config[key].Name).ToString();

            if (esName == null || !esToDinput.ContainsKey(esName))
                return 0;

            string dinputName = esToDinput[esName];
            if (dinputName == null)
                return 0;

            if (!ctrl.ButtonMappings.ContainsKey(dinputName))
                return 0;

            string button = ctrl.ButtonMappings[dinputName];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1: return 34822;
                    case 2: return 34825;
                    case 4: return 34823;
                    case 8: return 34824;
                };
            }

            else if (button.StartsWith("b"))
            {
                int buttonID = button.Substring(1).ToInteger();
                return buttonID * 256;
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                switch (axisID)
                {
                    case 0:
                        if (revertAxis || trigger) return 32769 + 1;
                        else return 32769;
                    case 1:
                        if (revertAxis || trigger) return 33025 + 1;
                        else return 33025;
                    case 2:
                        if (isXinput || revertAxis || trigger) return 33281 + 1;
                        else return 33281;
                    case 3:
                        if (revertAxis || trigger) return 33537 + 1;
                        else return 33537;
                    case 4:
                        if (revertAxis || trigger) return 33793 + 1;
                        else return 33793;
                    case 5:
                        if (isXinput) return 33281;
                        else if (revertAxis || trigger) return 34049 + 1;
                        else return 34049;
                };
            }

            return 0;
        }

        private static readonly Dictionary<int, string> ssfPadOrder = new Dictionary<int, string>()
        {
            { 1, "0_0" },
            { 2, "1_0" },
            { 3, "0_1" },
            { 4, "0_2" },
            { 5, "0_3" },
            { 6, "0_4" },
            { 7, "0_5" },
            { 8, "1_1" },
            { 9, "1_2" },
            { 10, "1_3" },
            { 11, "1_4" },
            { 12, "1_5" },
        };

        private static readonly Dictionary<string, string> esToDinput = new Dictionary<string, string>()
        {
            { "a", "a" },
            { "b", "b" },
            { "x", "y" },
            { "y", "x" },
            { "select", "back" },
            { "start", "start" },
            { "joystick1left", "leftx" },
            { "leftanalogleft", "leftx" },
            { "joystick1up", "lefty" },
            { "leftanalogup", "lefty" },
            { "joystick2left", "rightx" },
            { "rightanalogleft", "rightx" },
            { "joystick2up", "righty" },
            { "rightanalogup", "righty" },
            { "up", "dpup" },
            { "down", "dpdown" },
            { "left", "dpleft" },
            { "right", "dpright" },
            { "l2", "lefttrigger" },
            { "l3", "leftstick" },
            { "pagedown", "rightshoulder" },
            { "pageup", "leftshoulder" },
            { "r2", "righttrigger" },
            { "r3", "rightstick" },
            { "leftthumb", "lefttrigger" },
            { "rightthumb", "righttrigger" },
            { "l1", "leftshoulder" },
            { "r1", "rightshoulder" },
            { "lefttrigger", "leftstick" },
            { "righttrigger", "rightstick" },
        };
    }
}