using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.EmulationStation;
using System;
using System.Security.Cryptography;

namespace EmulatorLauncher
{
    partial class VPinballGenerator : Generator
    {
        private void SetupVPinballControls(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            Controller controller = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

            if (controller == null)
                return;

            if (!controller.IsKeyboard)
                WriteControllerconfig(ini, controller);

            WriteKBconfig(ini);
        }

        private void WriteKBconfig(IniFile ini)
        {
            ini.WriteValue("Player", "LFlipKey", "42");
            ini.WriteValue("Player", "RFlipKey", "54");
            ini.WriteValue("Player", "StagedLFlipKey", "219");
            ini.WriteValue("Player", "StagedRFlipKey", "184");
            ini.WriteValue("Player", "LTiltKey", "44");
            ini.WriteValue("Player", "RTiltKey", "53");
            ini.WriteValue("Player", "CTiltKey", "57");
            ini.WriteValue("Player", "PlungerKey", "28");
            ini.WriteValue("Player", "FrameCount", "87");
            ini.WriteValue("Player", "DebugBalls", "24");
            ini.WriteValue("Player", "Debugger", "32");
            ini.WriteValue("Player", "AddCreditKey", "6");
            ini.WriteValue("Player", "AddCreditKey2", "5");
            ini.WriteValue("Player", "StartGameKey", "2");
            ini.WriteValue("Player", "MechTilt", "20");
            ini.WriteValue("Player", "RMagnaSave", "157");
            ini.WriteValue("Player", "LMagnaSave", "29");
            ini.WriteValue("Player", "ExitGameKey", "16");
            ini.WriteValue("Player", "VolumeUp", "13");
            ini.WriteValue("Player", "VolumeDown", "12");
            ini.WriteValue("Player", "LockbarKey", "56");
            ini.WriteValue("Player", "PauseKey", "25");
            ini.WriteValue("Player", "TweakKey", "88");
            ini.WriteValue("Player", "JoyCustom1Key", "200");
            ini.WriteValue("Player", "JoyCustom2Key", "208");
            ini.WriteValue("Player", "JoyCustom3Key", "203");
            ini.WriteValue("Player", "JoyCustom4Key", "205");
        }

        private void WriteControllerconfig(IniFile ini, Controller controller)
        {
            SdlToDirectInput dinputController = getDInputController(controller);

            bool isXinput = controller.IsXInputDevice;

            string inputApi = isXinput ? "1" : "0";
            if (SystemConfig.isOptSet("vp_inputdriver") && !string.IsNullOrEmpty(SystemConfig["vp_inputdriver"]))
                inputApi = SystemConfig["vp_inputdriver"];

            ini.WriteValue("Player", "InputApi", inputApi);

            if (inputApi == "1")    // XINPUT case
            {
                // Axis
                if (SystemConfig.getOptBoolean("nouse_joyaxis"))
                {
                    ini.WriteValue("Player", "PlungerAxis", "0");
                    ini.WriteValue("Player", "LRAxis", "0");
                    ini.WriteValue("Player", "UDAxis", "0");
                }
                else
                {
                    ini.WriteValue("Player", "PlungerAxis", "6");           // R2
                    ini.WriteValue("Player", "ReversePlungerAxis", "0");
                    ini.WriteValue("Player", "LRAxis", "1");                // Left stick horizontal
                    ini.WriteValue("Player", "LRAxisFlip", "0");
                    ini.WriteValue("Player", "UDAxis", "2");                // Left stick vertical
                    ini.WriteValue("Player", "UDAxisFlip", "0");
                }

                ini.WriteValue("Player", "JoyCTiltKey", "13");          // up
                ini.WriteValue("Player", "JoyLTiltKey", "11");          // left
                ini.WriteValue("Player", "JoyRTiltKey", "12");          // right
                ini.WriteValue("Player", "JoyMechTiltKey", "14");       // down
                ini.WriteValue("Player", "JoyAddCreditKey", "7");       // select
                ini.WriteValue("Player", "JoyStartGameKey", "8");       // start
                ini.WriteValue("Player", "JoyPlungerKey", "1");         // SOUTH
                ini.WriteValue("Player", "JoyPauseKey", "2");           // EAST
                ini.WriteValue("Player", "JoyLFlipKey", "5");           // L1
                ini.WriteValue("Player", "JoyRFlipKey", "6");           // R1
            }
            
            else if (dinputController != null && dinputController.ButtonMappings.Count > 0)
            {
                if (SystemConfig.getOptBoolean("nouse_joyaxis"))
                {
                    ini.WriteValue("Player", "PlungerAxis", "0");
                    ini.WriteValue("Player", "LRAxis", "0");
                    ini.WriteValue("Player", "UDAxis", "0");
                }
                else
                {
                    string plungerAxis = getDinputID(dinputController.ButtonMappings, "righttrigger", isXinput);
                    if (plungerAxis.Split('_').Length > 1)
                    {
                        ini.WriteValue("Player", "PlungerAxis", plungerAxis.Split('_')[0]);
                        ini.WriteValue("Player", "ReversePlungerAxis", plungerAxis.Split('_')[1]);
                    }
                    else
                    {
                        ini.WriteValue("Player", "PlungerAxis", plungerAxis);
                        ini.WriteValue("Player", "ReversePlungerAxis", isXinput ? "1" : "0");
                    }

                    ini.WriteValue("Player", "LRAxis", getDinputID(dinputController.ButtonMappings, "leftx", isXinput)); // Left stick horizontal
                    ini.WriteValue("Player", "LRAxisFlip", "0");
                    ini.WriteValue("Player", "UDAxis", getDinputID(dinputController.ButtonMappings, "lefty", isXinput)); // Left stick vertical
                    ini.WriteValue("Player", "UDAxisFlip", "0");
                }
                ini.WriteValue("Player", "JoyCTiltKey", getDinputID(dinputController.ButtonMappings, "buttonup", isXinput));        // up
                ini.WriteValue("Player", "JoyLTiltKey", getDinputID(dinputController.ButtonMappings, "buttonleft", isXinput));      // left
                ini.WriteValue("Player", "JoyRTiltKey", getDinputID(dinputController.ButtonMappings, "buttonright", isXinput));     // right
                ini.WriteValue("Player", "JoyMechTiltKey", getDinputID(dinputController.ButtonMappings, "buttondown", isXinput));   // down
                ini.WriteValue("Player", "JoyAddCreditKey", getDinputID(dinputController.ButtonMappings, "back", isXinput));        // select
                ini.WriteValue("Player", "JoyStartGameKey", getDinputID(dinputController.ButtonMappings, "start", isXinput));       // start
                ini.WriteValue("Player", "JoyPlungerKey", getDinputID(dinputController.ButtonMappings, "a", isXinput));             // SOUTH
                ini.WriteValue("Player", "JoyPauseKey", getDinputID(dinputController.ButtonMappings, "b", isXinput));               // EAST
                ini.WriteValue("Player", "JoyLFlipKey", getDinputID(dinputController.ButtonMappings, "leftshoulder", isXinput));    // L1
                ini.WriteValue("Player", "JoyRFlipKey", getDinputID(dinputController.ButtonMappings, "rightshoulder", isXinput));   // R1
            }
            
            else
            {
                if (SystemConfig.getOptBoolean("nouse_joyaxis"))
                {
                    ini.WriteValue("Player", "PlungerAxis", "0");
                    ini.WriteValue("Player", "LRAxis", "0");
                    ini.WriteValue("Player", "UDAxis", "0");
                }
                else
                {
                    string plungerAxis = getButtonID(controller, InputKey.r2, isXinput);
                    if (plungerAxis.Split('_').Length > 1)
                    {
                        ini.WriteValue("Player", "PlungerAxis", plungerAxis.Split('_')[0]);
                        ini.WriteValue("Player", "ReversePlungerAxis", plungerAxis.Split('_')[1]);
                    }
                    else
                    {
                        ini.WriteValue("Player", "PlungerAxis", plungerAxis);
                        ini.WriteValue("Player", "ReversePlungerAxis", isXinput ? "1" : "0");
                    }

                    ini.WriteValue("Player", "LRAxis", getButtonID(controller, InputKey.leftanalogleft, isXinput));
                    ini.WriteValue("Player", "LRAxisFlip", "0");
                    ini.WriteValue("Player", "UDAxis", getButtonID(controller, InputKey.leftanalogup, isXinput));
                    ini.WriteValue("Player", "UDAxisFlip", "0");
                }
                ini.WriteValue("Player", "JoyCTiltKey", getButtonID(controller, InputKey.up, isXinput));
                ini.WriteValue("Player", "JoyLTiltKey", getButtonID(controller, InputKey.left, isXinput));
                ini.WriteValue("Player", "JoyRTiltKey", getButtonID(controller, InputKey.right, isXinput));
                ini.WriteValue("Player", "JoyMechTiltKey", getButtonID(controller, InputKey.down, isXinput));
                ini.WriteValue("Player", "JoyAddCreditKey", getButtonID(controller, InputKey.select, isXinput));
                ini.WriteValue("Player", "JoyStartGameKey", getButtonID(controller, InputKey.start, isXinput));
                ini.WriteValue("Player", "JoyPlungerKey", getButtonID(controller, InputKey.a, isXinput));
                ini.WriteValue("Player", "JoyPauseKey", getButtonID(controller, InputKey.b, isXinput));
                ini.WriteValue("Player", "JoyLFlipKey", getButtonID(controller, InputKey.pageup, isXinput));
                ini.WriteValue("Player", "JoyRFlipKey", getButtonID(controller, InputKey.pagedown, isXinput));
            }

            BindIniFeatureSlider(ini, "Player", "DeadZone", "joy_deadzone", "15");
            BindIniFeature(ini, "Player", "RumbleMode", "vp_rumble", "3");
        }

        private SdlToDirectInput getDInputController(Controller ctrl)
        {
            string gamecontrollerDB = Path.Combine(Program.AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            if (!File.Exists(gamecontrollerDB))
                return null;

            string guid = (ctrl.Guid.ToString()).ToLowerInvariant().Substring(0, 24) + "00000000";
            if (string.IsNullOrEmpty(guid))
                return null;

            SdlToDirectInput dinputController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);
            if (dinputController == null)
                return null;
            else
                return dinputController;
        }

        private string getDinputID(Dictionary<string, string> mapping, string key, bool isXinput)
        {
            if (!mapping.ContainsKey(key))
                return "0";

            bool inverted = false;
            if (isXinput && key == "righttrigger")
            {
                key = "lefttrigger";
                inverted = true;
            }

            string button = mapping[key];

            if (button.StartsWith("b"))
            {
                int buttonID = button.Substring(1).ToInteger();
                buttonID++;
                return buttonID.ToString();
            }
            else if (button.StartsWith("h"))
                return "0";

            else if (button.StartsWith("-a") || button.StartsWith("+a"))
            {
                if (button.StartsWith("-a"))
                    inverted = true;
                
                int axisID = button.Substring(2).ToInteger();
                axisID++;
                
                if (inverted)
                    return axisID.ToString() + "_1";

                return axisID.ToString();
            }
            else if (button.StartsWith("a"))
            {
                int axisID = button.Substring(1).ToInteger();
                axisID++;

                if (inverted)
                    return axisID.ToString() + "_1";

                return axisID.ToString();
            }

            return "0";
        }

        private string getButtonID(Controller c, InputKey key, bool isXinput)
        {
            if (c.Config == null)
                return "0";

            bool inverted = false;

            if (isXinput && key == InputKey.r2)
            {
                return "3_1";
            }

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    Int64 pid = input.Id;
                    return (pid + 1).ToString();
                }

                else if (input.Type == "hat")
                {
                    return "0";
                }

                else if (input.Type == "axis")
                {
                    Int64 pid = input.Id;
                    if (inverted)
                        return (pid + 1).ToString() + "_1";

                    return (pid + 1).ToString();
                }
            }

            return "0";
        }
    }
}
