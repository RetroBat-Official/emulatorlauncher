using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System.Collections.Generic;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using EmulatorLauncher.Common.Joysticks;
using System.Reflection;

namespace EmulatorLauncher
{
    partial class Project64Generator : Generator
    {
        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            if (!this.Controllers.Any(c => !c.IsKeyboard))
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Project64");

            for (int i = 1; i < 5; i++)
            {
                string iniSection = "Input-Controller " + i;
                ini.ClearSection(iniSection);
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigureInput(controller, ini);
        }

        private void ConfigureInput(Controller controller, IniFile ini)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(controller, ini, controller.PlayerIndex);
        }

        private void ConfigureJoystick(Controller ctrl, IniFile ini, int playerIndex)
        {
            if (ctrl == null)
                return;

            if (ctrl.Config == null)
                return;

            // Initializing controller information
            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput controller;
            string instanceGuid = "{" + ctrl.DirectInput.InstanceGuid.ToString().ToUpperInvariant() + "}";
            bool isxinput = ctrl.IsXInputDevice;
            string iniSection = "Input-Controller " + playerIndex;
            bool zAsRightTrigger = SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_stick");
            bool xboxLayout = SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && SystemConfig["mupen64_inputprofile" + playerIndex] == "xbox";

            // Looking for gamecontrollerdb.txt file
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

            // Fetching controller mapping from gamecontrollerdb.txt file
            controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

            if (controller == null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerIndex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid);
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerIndex + ": " + guid + " found in gamecontrollerDB file.");

            if (controller.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return;
            }

            if (SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl"))
            {
                ini.WriteValue(iniSection, "AnalogDown", instanceGuid + " " + GetDinputMapping(controller, "lefty", isxinput, 1));
                ini.WriteValue(iniSection, "AnalogLeft", instanceGuid + " " + GetDinputMapping(controller, "leftx", isxinput, -1));
                ini.WriteValue(iniSection, "AnalogRight", instanceGuid + " " + GetDinputMapping(controller, "leftx", isxinput, 1));
                ini.WriteValue(iniSection, "AnalogUp", instanceGuid + " " + GetDinputMapping(controller, "lefty", isxinput, -1));

                if (zAsRightTrigger)
                {
                    ini.WriteValue(iniSection, "ButtonA", instanceGuid + " " + GetDinputMapping(controller, "leftshoulder", isxinput, -1));
                    ini.WriteValue(iniSection, "ButtonB", instanceGuid + " " + GetDinputMapping(controller, "lefttrigger", isxinput, -1));
                    ini.WriteValue(iniSection, "ButtonR", instanceGuid + " " + GetDinputMapping(controller, "rightshoulder", isxinput, -1));
                    ini.WriteValue(iniSection, "ButtonZ", instanceGuid + " " + GetDinputMapping(controller, "righttrigger", isxinput, -1));
                }
                else
                {
                    ini.WriteValue(iniSection, "ButtonA", instanceGuid + " " + GetDinputMapping(controller, "rightshoulder", isxinput, -1));
                    ini.WriteValue(iniSection, "ButtonB", instanceGuid + " " + GetDinputMapping(controller, "righttrigger", isxinput, -1));
                    ini.WriteValue(iniSection, "ButtonR", instanceGuid + " " + GetDinputMapping(controller, "leftshoulder", isxinput, -1));
                    ini.WriteValue(iniSection, "ButtonZ", instanceGuid + " " + GetDinputMapping(controller, "lefttrigger", isxinput, -1));
                }

                ini.WriteValue(iniSection, "ButtonL", instanceGuid + " " + GetDinputMapping(controller, "back", isxinput));
                ini.WriteValue(iniSection, "ButtonStart", instanceGuid + " " + GetDinputMapping(controller, "start", isxinput));

                ini.WriteValue(iniSection, "CButtonDown", instanceGuid + " " + GetDinputMapping(controller, "a", isxinput, 1));
                ini.WriteValue(iniSection, "CButtonLeft", instanceGuid + " " + GetDinputMapping(controller, "x", isxinput, -1));
                ini.WriteValue(iniSection, "CButtonRight", instanceGuid + " " + GetDinputMapping(controller, "b", isxinput, 1));
                ini.WriteValue(iniSection, "CButtonUp", instanceGuid + " " + GetDinputMapping(controller, "y", isxinput, -1));

                ini.WriteValue(iniSection, "DPadDown", instanceGuid + " " + GetDinputMapping(controller, "dpdown", isxinput, 1));
                ini.WriteValue(iniSection, "DPadLeft", instanceGuid + " " + GetDinputMapping(controller, "dpleft", isxinput, -1));
                ini.WriteValue(iniSection, "DPadRight", instanceGuid + " " + GetDinputMapping(controller, "dpright", isxinput, 1));
                ini.WriteValue(iniSection, "DPadUp", instanceGuid + " " + GetDinputMapping(controller, "dpup", isxinput, 1));
            }

            else
            {
                ini.WriteValue(iniSection, "AnalogDown", instanceGuid + " " + GetDinputMapping(controller, "lefty", isxinput, 1));
                ini.WriteValue(iniSection, "AnalogLeft", instanceGuid + " " + GetDinputMapping(controller, "leftx", isxinput, -1));
                ini.WriteValue(iniSection, "AnalogRight", instanceGuid + " " + GetDinputMapping(controller, "leftx", isxinput, 1));
                ini.WriteValue(iniSection, "AnalogUp", instanceGuid + " " + GetDinputMapping(controller, "lefty", isxinput, -1));

                if (xboxLayout)
                {
                    ini.WriteValue(iniSection, "ButtonA", instanceGuid + " " + GetDinputMapping(controller, "a", isxinput));
                    ini.WriteValue(iniSection, "ButtonB", instanceGuid + " " + GetDinputMapping(controller, "b", isxinput));
                }
                else
                {
                    ini.WriteValue(iniSection, "ButtonA", instanceGuid + " " + GetDinputMapping(controller, "a", isxinput));
                    ini.WriteValue(iniSection, "ButtonB", instanceGuid + " " + GetDinputMapping(controller, "x", isxinput));
                }

                ini.WriteValue(iniSection, "ButtonL", instanceGuid + " " + GetDinputMapping(controller, "leftshoulder", isxinput, -1));
                ini.WriteValue(iniSection, "ButtonR", instanceGuid + " " + GetDinputMapping(controller, "rightshoulder", isxinput, -1));
                ini.WriteValue(iniSection, "ButtonStart", instanceGuid + " " + GetDinputMapping(controller, "start", isxinput));
                
                if (zAsRightTrigger)
                    ini.WriteValue(iniSection, "ButtonZ", instanceGuid + " " + GetDinputMapping(controller, "righttrigger", isxinput, -1));
                else
                    ini.WriteValue(iniSection, "ButtonZ", instanceGuid + " " + GetDinputMapping(controller, "lefttrigger", isxinput, -1));

                ini.WriteValue(iniSection, "CButtonDown", instanceGuid + " " + GetDinputMapping(controller, "righty", isxinput, 1));
                ini.WriteValue(iniSection, "CButtonLeft", instanceGuid + " " + GetDinputMapping(controller, "rightx", isxinput, -1));
                ini.WriteValue(iniSection, "CButtonRight", instanceGuid + " " + GetDinputMapping(controller, "rightx", isxinput, 1));
                ini.WriteValue(iniSection, "CButtonUp", instanceGuid + " " + GetDinputMapping(controller, "righty", isxinput, -1));

                ini.WriteValue(iniSection, "DPadDown", instanceGuid + " " + GetDinputMapping(controller, "dpdown", isxinput, 1));
                ini.WriteValue(iniSection, "DPadLeft", instanceGuid + " " + GetDinputMapping(controller, "dpleft", isxinput, -1));
                ini.WriteValue(iniSection, "DPadRight", instanceGuid + " " + GetDinputMapping(controller, "dpright", isxinput, 1));
                ini.WriteValue(iniSection, "DPadUp", instanceGuid + " " + GetDinputMapping(controller, "dpup", isxinput, 1));
            }

            if (playerIndex > 1)
                ini.WriteValue(iniSection, "Present", "1");

            BindIniFeatureSlider(ini, iniSection, "Deadzone", "p64_deadzone", "25");
        }

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey, bool isxinput, int plus = -1)
        {
            if (c == null)
                return "";

            if (!c.ButtonMappings.ContainsKey(buttonkey))
                return "";

            string button = c.ButtonMappings[buttonkey];
            
            if (button.StartsWith("-a"))
                plus = -1;

            if (button.StartsWith("+a"))
                plus = 1;

            if (isxinput)
            {
                if (button == "a5")
                {
                    button = "a2";
                    plus = 1;
                }
                else if (button == "a2")
                    plus = -1;
            }

            if (button.StartsWith("b"))
            {
                string buttonID = button.Substring(1);
                string formattedID = buttonID.PadLeft(2, '0');
                return formattedID + " 0 1";
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "08 0 3";
                    case 2:
                        return "08 1 3";
                    case 4:
                        return "08 2 3";
                    case 8:
                        return "08 3 3";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                string axisID = button.Substring(1);
                string formattedAxisID = axisID.PadLeft(2, '0');

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2);

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1);

                if (plus == 1) return formattedAxisID + " 1 2";
                else return formattedAxisID + " 0 2";
            }

            return "";
        }
    }
}
