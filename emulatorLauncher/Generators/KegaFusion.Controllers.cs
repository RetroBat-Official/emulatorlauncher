using System.IO;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using System;

namespace EmulatorLauncher
{
    partial class KegaFusionGenerator : Generator
    {
        private void ConfigureControllers(IniFile ini, string system)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Kega-Fusion");

            int maxPad = 8;

            if (system == "mastersystem")
                maxPad = 2;

            var controllers = this.Controllers.Where(c => !c.IsKeyboard).ToList();
            
            if (this.Controllers.Count == 0)
                return;

            if (SystemConfig.isOptSet("kega_multitap") && !string.IsNullOrEmpty(SystemConfig["kega_multitap"]))
            {
                ini.WriteValue("", "MultiTapType", SystemConfig["kega_multitap"]);
            }

            else if (system != "mastersystem")
            {
                if (controllers.Count > 5)
                    ini.WriteValue("", "MultiTapType", "3");
                else if (controllers.Count > 2)
                    ini.WriteValue("", "MultiTapType", "1");
                else
                    ini.WriteValue("", "MultiTapType", "0");
            }

            // Cleanup
            foreach (var s in joyIndex)
            {
                ini.WriteValue("", "Joystick" + s.Value + "Using", "255");
                ini.WriteValue("", "Joystick" + s.Value + "Type", "0");
            }

            for (int i = 1; i < 3; i++)
            {
                ini.WriteValue("", "Joystick" + i.ToString() + "MSUsing", "255");
                ini.WriteValue("", "Joystick" + i.ToString() + "MSType", "0");
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(controller, ini, system);
        }

        private void ConfigureInput(Controller controller, IniFile ini, string system)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(controller.Config, ini, system);
            else
                ConfigureJoystick(controller, ini, system);
        }

        private void ConfigureJoystick(Controller ctrl, IniFile ini, string system)
        {
            if (ctrl == null)
                return;

            var ctrlrCfg = ctrl.Config;
            if (ctrlrCfg == null)
                return;

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid = ctrl.Guid.ToString();
            SdlToDirectInput controller = null;
            List<string> buttonMapping = new List<string>();
            string joy = joyIndex[ctrl.PlayerIndex.ToString()];
            int index = ctrl.DirectInput != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex;
            bool needMDActivationSwitch = false;
            bool md_pad = Program.SystemConfig["md_pad_kega"] == "1" || Program.SystemConfig["md_pad_kega"] == "2";

            if (_mdSystems.Contains(system))
            {
                string mdjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mdControllers.json");
                if (File.Exists(mdjson))
                {
                    try
                    {
                        var mdControllers = MegadriveController.LoadControllersFromJson(mdjson);

                        if (mdControllers != null)
                        {
                            MegadriveController mdGamepad = MegadriveController.GetMDController("kega-fusion", guid, mdControllers);

                            if (mdGamepad != null)
                            {
                                if (mdGamepad.ControllerInfo != null)
                                {
                                    if (mdGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                        needMDActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                    if (needMDActivationSwitch && !md_pad)
                                    {
                                        SimpleLogger.Instance.Info("[Controller] Specific MD mapping needs to be activated for this controller.");
                                        goto BypassMDControllers;
                                    }
                                }

                                SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + mdGamepad.Name);

                                if (mdGamepad.Mapping != null)
                                {
                                    string input = mdGamepad.Mapping["buttons"];

                                    if (Program.SystemConfig["md_pad_kega"] == "2" && mdGamepad.Mapping["buttons_anal"] != null)
                                        input = mdGamepad.Mapping["buttons_anal"];

                                    ini.WriteValue("", "Joystick" + joy + "Using", (index + 2).ToString());
                                    ini.WriteValue("", "Joystick" + joy + "Type", "2");
                                    ini.WriteValue("", "Player" + joy + "Buttons", input);

                                    SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());

                                    return;
                                }
                                else
                                    SimpleLogger.Instance.Info("[INFO] Missing mapping for Megadrive Gamepad, falling back to standard mapping.");
                            }
                            else
                                SimpleLogger.Instance.Info("[Controller] No specific mapping found for megadrive controller.");
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                    }
                    catch { }
                }
            }

            BypassMDControllers:

            guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

            try { controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid); }
            catch { }

            if (controller == null || controller.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt does not contain mapping for the controller " + guid + ". Controller mapping will not be available for player " + ctrl.PlayerIndex.ToString());
                return;
            }

            if (system != "mastersystem")
            {
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.up));
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.down));
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.left));
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.right));

                if (SystemConfig.isOptSet("megadrive_control_layout") && SystemConfig["megadrive_control_layout"] == "lr_yz")
                {
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.y));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.a));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.b));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.start));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.x));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.pageup));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.pagedown));
                }
                else if (SystemConfig.isOptSet("megadrive_control_layout") && SystemConfig["megadrive_control_layout"] == "lr_zc")
                {
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.a));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.b));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.pagedown));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.start));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.y));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.x));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.pageup));
                }
                else
                {
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.y));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.a));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.b));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.start));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.pageup));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.x));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.pagedown));
                }

                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.select));

                buttonMapping.Add("8");
                buttonMapping.Add("9");
                buttonMapping.Add("10");
                buttonMapping.Add("11");

                string input = string.Join(",", buttonMapping);

                ini.WriteValue("", "Joystick" + joy + "Using", (index+2).ToString());
                ini.WriteValue("", "Joystick" + joy + "Type", "2");
                ini.WriteValue("", "Player" + joy + "Buttons", input);
            }

            else if (system == "mastersystem")
            {
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.up));
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.down));
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.left));
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.right));
                if (SystemConfig.getOptBoolean("rotate_buttons"))
                {
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.y));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.a));
                }
                else
                {
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.a));
                    buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.b));
                }
                buttonMapping.Add(GetInputCode(controller, ctrl, InputKey.start));
                buttonMapping.Add("0");

                string input = string.Join(",", buttonMapping);

                ini.WriteValue("", "Joystick" + joy + "MSUsing", (index+2).ToString());
                ini.WriteValue("", "Joystick" + joy + "MSType", "1");
                ini.WriteValue("", "Player" + joy + "MSButtons", input);
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private string GetInputCode(SdlToDirectInput ctrl, Controller c, InputKey key)
        {
            key = key.GetRevertedAxis(out bool revertAxis);
            bool isxinput = c.IsXInputDevice;

            string esName = (c.Config[key].Name).ToString();

            if (esName == null || !esToDinput.ContainsKey(esName))
                return "0";

            string dinputName = esToDinput[esName];
            if (dinputName == null)
                return "0";

            if (!ctrl.ButtonMappings.ContainsKey(dinputName))
                return "0";

            string button = ctrl.ButtonMappings[dinputName];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1: return "50";
                    case 2: return "49";
                    case 4: return "51";
                    case 8: return "48";
                };
            }

            else if (button.StartsWith("b"))
            {
                string buttonID = button.Substring(1);
                return buttonID;
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
                        if (revertAxis) return "33";
                        else return "32";
                    case 1:
                        if (revertAxis) return "35";
                        else return "34";
                    case 2:
                        if (revertAxis || isxinput) return "37";
                        else return "36";
                    case 3:
                        if (revertAxis) return "39";
                        else return "38";
                    case 4:
                        if (revertAxis) return "41";
                        else return "40";
                    case 5:
                        if (isxinput) return "36";
                        else if (revertAxis) return "43";
                        else return "42";
                };
            }

            return "0";
        }

        private static readonly Dictionary<string,string> joyIndex = new Dictionary<string,string>()
        {
            { "1", "1" },
            { "2", "2" },
            { "3", "1b" },
            { "4", "1c" },
            { "5", "1d" },
            { "6", "2b" },
            { "7", "2c" },
            { "8", "2d" },
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

        private void ConfigureKeyboard(InputConfig keyboard, IniFile ini, string system)
        {
            if (keyboard == null)
                return;

            List<string> buttonMapping = new List<string>();

            Action<InputKey> AddKeyboardMapping = (k) =>
            {
                var a = keyboard[k];
                if (a != null)
                    buttonMapping.Add(DirectInputInfo.SdlToDikCode(a.Id));
            };

            if (system != "mastersystem")
            {
                AddKeyboardMapping(InputKey.up);
                AddKeyboardMapping(InputKey.down);
                AddKeyboardMapping(InputKey.left);
                AddKeyboardMapping(InputKey.right);

                if (SystemConfig.isOptSet("megadrive_control_layout") && SystemConfig["megadrive_control_layout"] == "lr_yz")
                {
                    AddKeyboardMapping(InputKey.y);
                    AddKeyboardMapping(InputKey.a);
                    AddKeyboardMapping(InputKey.b);
                    AddKeyboardMapping(InputKey.start);
                    AddKeyboardMapping(InputKey.x);
                    AddKeyboardMapping(InputKey.pageup);
                    AddKeyboardMapping(InputKey.pagedown);
                }
                else if (SystemConfig.isOptSet("megadrive_control_layout") && SystemConfig["megadrive_control_layout"] == "lr_xz")
                {
                    AddKeyboardMapping(InputKey.y);
                    AddKeyboardMapping(InputKey.a);
                    AddKeyboardMapping(InputKey.b);
                    AddKeyboardMapping(InputKey.start);
                    AddKeyboardMapping(InputKey.pageup);
                    AddKeyboardMapping(InputKey.x);
                    AddKeyboardMapping(InputKey.pagedown);
                }
                else
                {
                    AddKeyboardMapping(InputKey.a);
                    AddKeyboardMapping(InputKey.b);
                    AddKeyboardMapping(InputKey.pagedown);
                    AddKeyboardMapping(InputKey.start);
                    AddKeyboardMapping(InputKey.y);
                    AddKeyboardMapping(InputKey.x);
                    AddKeyboardMapping(InputKey.pageup);
                }

                AddKeyboardMapping(InputKey.select);

                buttonMapping.Add("0");
                buttonMapping.Add("0");
                buttonMapping.Add("0");
                buttonMapping.Add("0");

                string input = string.Join(",", buttonMapping);

                ini.WriteValue("", "Joystick1Using", "0");
                ini.WriteValue("", "Joystick1Type", "2");
                ini.WriteValue("", "Player1Keys", input);
            }

            else if (system == "mastersystem")
            {
                AddKeyboardMapping(InputKey.up);
                AddKeyboardMapping(InputKey.down);
                AddKeyboardMapping(InputKey.left);
                AddKeyboardMapping(InputKey.right);
                AddKeyboardMapping(InputKey.a);
                AddKeyboardMapping(InputKey.b);
                AddKeyboardMapping(InputKey.start);
                buttonMapping.Add("0");

                string input = string.Join(",", buttonMapping);

                ini.WriteValue("", "Joystick1MSUsing", "0");
                ini.WriteValue("", "Joystick1MSType", "1");
                ini.WriteValue("", "Player1MSKeys", input);
            }
        }
    }
}