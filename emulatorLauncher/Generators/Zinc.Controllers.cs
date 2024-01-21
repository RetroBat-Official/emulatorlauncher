using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using static SdlToDirectInput;
using EmulatorLauncher.Common;
using System.Collections.Generic;

namespace EmulatorLauncher
{
    partial class ZincGenerator : Generator
    {
        private void ConfigureControllers(string iniFile, string path)
        {
            if (!Program.SystemConfig.isOptSet("zinc_controller_config") || Program.SystemConfig["zinc_controller_config"] != "autoconfig")
                return;

            using (var ini = IniFile.FromFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                string outputFile = Path.Combine(path, "wberror.txt");
                ini.WriteValue("General", "output", outputFile);
                ini.WriteValue("General", "NOERROR", "1");

                ini.ClearSection("all");
                ini.ClearSection("player1");
                ini.ClearSection("player2");

                foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(2))
                    ConfigureInput(controller, ini, path);
            }
        }

        private void ConfigureInput(Controller controller, IniFile ini, string path)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(controller, ini, path);

        }

        private void ConfigureJoystick(Controller ctrl, IniFile ini, string path)
        {
            if (ctrl == null)
                return;

            var ctrlrCfg = ctrl.Config;
            if (ctrlrCfg == null)
                return;

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            
            string guid1 = (ctrl.Guid.ToString()).Substring(0, 27) + "00000";
            var controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1);

            SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

            int index;
            if (ctrl.DirectInput != null)
                index = ctrl.DirectInput.DeviceIndex + 1;
            else
                index = ctrl.DeviceIndex + 1;

            string playerSection = "player" + ctrl.PlayerIndex.ToString();

            string joy = "j" + index.ToString();

            if (ctrl.PlayerIndex == 1)
            {
                // all section
                ini.WriteValue("all", "test", GetInputKeyName(ctrl, controller, InputKey.r3, joy));
                ini.WriteValue("all", "services", GetInputKeyName(ctrl, controller, InputKey.leftthumb, joy));
            }

            // player section
            ini.WriteValue(playerSection, "coin", GetInputKeyName(ctrl, controller, InputKey.select, joy));
            ini.WriteValue(playerSection, "start", GetInputKeyName(ctrl, controller, InputKey.start, joy));
            
            if (!SystemConfig.isOptSet("zinc_digital") || SystemConfig["zinc_digital"] != "1")
            {
                ini.WriteValue(playerSection, "right", joy + "right");
                ini.WriteValue(playerSection, "left", joy + "left");
                ini.WriteValue(playerSection, "down", joy + "down");
                ini.WriteValue(playerSection, "up", joy + "up");
            }
            else if (ctrl.PlayerIndex == 1)
            {
                ini.WriteValue(playerSection, "right", "kCD");
                ini.WriteValue(playerSection, "left", "kCB");
                ini.WriteValue(playerSection, "down", "kD0");
                ini.WriteValue(playerSection, "up", "kC8");
            }
            else if (ctrl.PlayerIndex == 2)
            {
                ini.WriteValue(playerSection, "right", "k22");
                ini.WriteValue(playerSection, "left", "k20");
                ini.WriteValue(playerSection, "down", "k21");
                ini.WriteValue(playerSection, "up", "k13");
            }

            if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "sf")
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, controller, InputKey.y, joy));  // weak punch
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, controller, InputKey.x, joy));  // middle punch
                ini.WriteValue(playerSection, "btn3", GetInputKeyName(ctrl, controller, InputKey.pageup, joy));  // fierce punch
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, controller, InputKey.a, joy));  // weak kick
                ini.WriteValue(playerSection, "btn5", GetInputKeyName(ctrl, controller, InputKey.b, joy));  // middle kick
                ini.WriteValue(playerSection, "btn6", GetInputKeyName(ctrl, controller, InputKey.pagedown, joy));  // fierce kick
            }
            else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "edge")
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn3", GetInputKeyName(ctrl, controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, controller, InputKey.b, joy));
                ini.WriteValue(playerSection, "btn9", GetInputKeyName(ctrl, controller, InputKey.y, joy));            // left punch
                ini.WriteValue(playerSection, "btn10", GetInputKeyName(ctrl, controller, InputKey.x, joy));           // right punch
                ini.WriteValue(playerSection, "btn11", GetInputKeyName(ctrl, controller, InputKey.a, joy));           // kick
                ini.WriteValue(playerSection, "btn12", GetInputKeyName(ctrl, controller, InputKey.b, joy));           // block
            }
            else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "tekken")
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn5", GetInputKeyName(ctrl, controller, InputKey.b, joy));
                ini.WriteValue(playerSection, "btn9", GetInputKeyName(ctrl, controller, InputKey.y, joy));            // left punch
                ini.WriteValue(playerSection, "btn10", GetInputKeyName(ctrl, controller, InputKey.x, joy));           // right punch
                ini.WriteValue(playerSection, "btn12", GetInputKeyName(ctrl, controller, InputKey.a, joy));           // left kick
                ini.WriteValue(playerSection, "btn13", GetInputKeyName(ctrl, controller, InputKey.b, joy));           // right kick
            }
            else
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn3", GetInputKeyName(ctrl, controller, InputKey.pageup, joy));
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn5", GetInputKeyName(ctrl, controller, InputKey.b, joy));
                ini.WriteValue(playerSection, "btn6", GetInputKeyName(ctrl, controller, InputKey.pagedown, joy));
                ini.WriteValue(playerSection, "btn9", GetInputKeyName(ctrl, controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn10", GetInputKeyName(ctrl, controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn11", GetInputKeyName(ctrl, controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn12", GetInputKeyName(ctrl, controller, InputKey.b, joy));
            }
        }

        private static string GetInputKeyName(Controller c, SdlToDirectInput ctrl, InputKey key, string joy)
        {
            string esName = (c.Config[key].Name).ToString();

            if (esName == null || !esToDinput.ContainsKey(esName))
                return "null";
            
            string dinputName = esToDinput[esName];
            if (dinputName == null)
                return "null";

            if (!ctrl.ButtonMappings.ContainsKey(dinputName))
                return "null";

            string button = ctrl.ButtonMappings[dinputName];

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger()) + 1;
                return joy + "b" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        if (c.PlayerIndex == 1)
                            return "kC8";
                        else if (c.PlayerIndex == 2)
                            return "k13";
                        break;
                    case 2:
                        if (c.PlayerIndex == 1)
                            return "kCD";
                        else if (c.PlayerIndex == 2)
                            return "k22";
                        break;
                    case 4:
                        if (c.PlayerIndex == 1)
                            return "kD0";
                        else if (c.PlayerIndex == 2)
                            return "k21";
                        break;
                    case 8:
                        if (c.PlayerIndex == 1)
                            return "kCB";
                        else if (c.PlayerIndex == 2)
                            return "k20";
                        break;
                };
            }

            return "null";
        }

        private static Dictionary<string, string> esToDinput = new Dictionary<string, string>()
        {
            { "a", "a" },
            { "b", "b" },
            { "x", "y" },
            { "y", "x" },
            { "select", "back" },
            { "start", "start" },
            { "joystick1left", "leftx" },
            { "joystick1up", "lefty" },
            { "joystick2left", "rightx" },
            { "joystick2up", "righty" },
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
            { "leftthumb", "leftstick" },
            { "rightthumb", "rightstick" },
            { "l1", "leftshoulder" },
            { "r1", "rightshoulder" },
        };
    }
}
