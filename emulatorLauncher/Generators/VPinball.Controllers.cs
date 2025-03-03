using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class VPinballGenerator : Generator
    {
        private void SetupVPinballControls(IniFile ini)
        {
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
                return;

            Controller controller = null;
            bool isXinput = false;
            string LRAxis = "1";
            string UDAxis = "2";
            string PlungerAxis = "4";

            if (Controllers != null && Controllers.Count > 0)
            {
                controller = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                
                if (controller != null && controller.IsXInputDevice)
                    isXinput = true;
                else if (controller != null)
                {
                    SdlToDirectInput dinputController = getDInputController(controller);
                    if (dinputController != null)
                    {
                        if (dinputController.ButtonMappings.ContainsKey("leftx"))
                            LRAxis = getDinputID(dinputController.ButtonMappings, "leftx");
                        if (dinputController.ButtonMappings.ContainsKey("lefty"))
                            UDAxis = getDinputID(dinputController.ButtonMappings, "lefty");
                        if (dinputController.ButtonMappings.ContainsKey("righty"))
                            PlungerAxis = getDinputID(dinputController.ButtonMappings, "righty");
                    }
                    if (LRAxis == null)
                        LRAxis = "1";
                    if (UDAxis == null)
                        UDAxis = "2";
                    if (PlungerAxis == null)
                        PlungerAxis = "4";
                }
            }

            if (SystemConfig.isOptSet("vp_inputdriver") && !string.IsNullOrEmpty(SystemConfig["vp_inputdriver"]))
                ini.WriteValue("Player", "InputApi", SystemConfig["vp_inputdriver"]);
            else
                ini.WriteValue("Player", "InputApi", isXinput ? "1" : "0");

            ini.WriteValue("Player", "LRAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? "0" : LRAxis);
            ini.WriteValue("Player", "UDAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? "0" : UDAxis);
            ini.WriteValue("Player", "PlungerAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? "0" : PlungerAxis);
            ini.WriteValue("Player", "ReversePlungerAxis", "1");
            BindIniFeatureSlider(ini, "Player", "DeadZone", "joy_deadzone", "15");

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

        private SdlToDirectInput getDInputController(Controller ctrl)
        {
            string gamecontrollerDB = Path.Combine(Program.AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            if (!File.Exists(gamecontrollerDB))
                return null;

            string guid = (ctrl.Guid.ToString()).ToLowerInvariant();
            if (string.IsNullOrEmpty(guid))
                return null;

            SdlToDirectInput dinputController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);
            if (dinputController == null)
                return null;
            else
                return dinputController;
        }

        private string getDinputID(Dictionary<string, string> mapping, string key)
        {
            if (!mapping.ContainsKey(key))
                return null;

            string button = mapping[key];

            if (button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(2).ToInteger();
                axisID++;
                return axisID.ToString();
            }
            else if (button.StartsWith("a"))
            {
                int axisID = button.Substring(1).ToInteger();
                axisID++;
                return axisID.ToString();
            }

            return null;
        }
    }
}
