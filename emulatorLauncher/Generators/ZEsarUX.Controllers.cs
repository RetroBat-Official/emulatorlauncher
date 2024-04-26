using System.Linq;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class ZEsarUXGenerator : Generator
    {
        private void CreateControllerConfiguration(ZEsarUXConfigFile cfg)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(1))
                ConfigureJoystick(controller, cfg);
        }

        private void ConfigureJoystick(Controller controller, ZEsarUXConfigFile cfg)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            int index = controller.DirectInput != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex;
            string guid = (controller.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput dinputCtrl = null;
            bool isxinput = false;
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            
            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                return;
            }

            if (gamecontrollerDB != null)
            {
                SimpleLogger.Instance.Info("[INFO] Fetching gamecontrollerdb.txt file with guid : " + guid);
                dinputCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

                if (dinputCtrl == null)
                {
                    SimpleLogger.Instance.Info("[INFO] No controller found in gamecontrollerdb.txt file for guid : " + guid);
                    return;
                }
            }

            if (controller.IsXInputDevice)
                isxinput = true;

            cfg["--realjoystickindex"] = index.ToString();
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.up, dinputCtrl, isxinput) + "\""] = "Up";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.down, dinputCtrl, isxinput) + "\""] = "Down";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.left, dinputCtrl, isxinput) + "\""] = "Left";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.right, dinputCtrl, isxinput) + "\""] = "Right";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.a, dinputCtrl, isxinput) + "\""] = "Fire";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.b, dinputCtrl, isxinput) + "\""] = "Aux1";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.x, dinputCtrl, isxinput) + "\""] = "Osdkeyboard";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.start, dinputCtrl, isxinput) + "\""] = "Enter";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.select, dinputCtrl, isxinput) + "\""] = "EscMenu";
        }

        private static string GetInputKeyName(Controller c, InputKey key, SdlToDirectInput dInputCtrl, bool isXinput)
        {
            key = key.GetRevertedAxis(out bool revertAxis);

            string esName = (c.Config[key].Name).ToString();

            if (esName == null || !esToDinput.ContainsKey(esName))
                return "";

            string dinputName = esToDinput[esName];
            if (dinputName == null)
                return "";

            if (!dInputCtrl.ButtonMappings.ContainsKey(dinputName))
                return "";

            string button = dInputCtrl.ButtonMappings[dinputName];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1: return "-1";
                    case 2: return "+0";
                    case 4: return "+1";
                    case 8: return "-0";
                };
            }

            else if (button.StartsWith("b"))
            {
                string buttonID = button.Substring(1);
                return buttonID;
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                string axisID = button.Substring(1);

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2);

                if (isXinput)
                {
                    if (axisID == "2")
                        return "+2";
                    if (axisID == "5")
                        return "-2";
                }

                bool trigger = triggerList.Contains(dinputName);

                if (trigger || revertAxis)
                    return "+" + axisID;
                else
                    return "-" + axisID;
            }

            return "";
        }

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

        private static readonly List<string> triggerList = new List<string>() { "righttrigger", "lefttrigger" };
    }
}