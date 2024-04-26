using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using System.Collections.Generic;
using EmulatorLauncher.Common.Joysticks;
using System;

namespace EmulatorLauncher
{
    partial class ZincGenerator : Generator
    {
        private void ConfigureControllers(string iniFile, string path)
        {
            if (Program.SystemConfig.isOptSet("zinc_controller_config") && (Program.SystemConfig["zinc_controller_config"] == "none" || Program.SystemConfig["zinc_controller_config"] == "predefined"))
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
                    ConfigureInput(controller, ini);
            }
        }

        private void ConfigureInput(Controller controller, IniFile ini)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(controller, ini);

        }

        private void ConfigureJoystick(Controller ctrl, IniFile ini)
        {
            if (ctrl == null)
                return;

            var ctrlrCfg = ctrl.Config;
            if (ctrlrCfg == null)
                return;

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid1 = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput controller = null;

            SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

            try { controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1); }
            catch { }
            
            int index;
            if (ctrl.DirectInput != null)
                index = ctrl.DirectInput.DeviceIndex + 1;
            else
                index = ctrl.DeviceIndex + 1;

            string playerSection = "player" + ctrl.PlayerIndex.ToString();

            string joy = "j" + index.ToString();

            if (controller != null)
            {
                if (ctrl.PlayerIndex == 1)
                {
                    // all section
                    ini.WriteValue("all", "test", GetDInputKeyName(ctrl, controller, InputKey.r3, joy));
                    ini.WriteValue("all", "services", GetDInputKeyName(ctrl, controller, InputKey.l3, joy));
                }

                // player section
                ini.WriteValue(playerSection, "coin", GetDInputKeyName(ctrl, controller, InputKey.select, joy));
                ini.WriteValue(playerSection, "start", GetDInputKeyName(ctrl, controller, InputKey.start, joy));

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
                    ini.WriteValue(playerSection, "btn1", GetDInputKeyName(ctrl, controller, InputKey.y, joy));  // weak punch
                    ini.WriteValue(playerSection, "btn2", GetDInputKeyName(ctrl, controller, InputKey.x, joy));  // middle punch
                    ini.WriteValue(playerSection, "btn3", GetDInputKeyName(ctrl, controller, InputKey.pageup, joy));  // fierce punch
                    ini.WriteValue(playerSection, "btn4", GetDInputKeyName(ctrl, controller, InputKey.a, joy));  // weak kick
                    ini.WriteValue(playerSection, "btn5", GetDInputKeyName(ctrl, controller, InputKey.b, joy));  // middle kick
                    ini.WriteValue(playerSection, "btn6", GetDInputKeyName(ctrl, controller, InputKey.pagedown, joy));  // fierce kick
                }
                else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "edge")
                {
                    ini.WriteValue(playerSection, "btn1", GetDInputKeyName(ctrl, controller, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn2", GetDInputKeyName(ctrl, controller, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn3", GetDInputKeyName(ctrl, controller, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn4", GetDInputKeyName(ctrl, controller, InputKey.b, joy));
                    ini.WriteValue(playerSection, "btn9", GetDInputKeyName(ctrl, controller, InputKey.y, joy));            // left punch
                    ini.WriteValue(playerSection, "btn10", GetDInputKeyName(ctrl, controller, InputKey.x, joy));           // right punch
                    ini.WriteValue(playerSection, "btn11", GetDInputKeyName(ctrl, controller, InputKey.a, joy));           // kick
                    ini.WriteValue(playerSection, "btn12", GetDInputKeyName(ctrl, controller, InputKey.b, joy));           // block
                }
                else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "tekken")
                {
                    ini.WriteValue(playerSection, "btn1", GetDInputKeyName(ctrl, controller, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn2", GetDInputKeyName(ctrl, controller, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn4", GetDInputKeyName(ctrl, controller, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn5", GetDInputKeyName(ctrl, controller, InputKey.b, joy));
                    ini.WriteValue(playerSection, "btn9", GetDInputKeyName(ctrl, controller, InputKey.y, joy));            // left punch
                    ini.WriteValue(playerSection, "btn10", GetDInputKeyName(ctrl, controller, InputKey.x, joy));           // right punch
                    ini.WriteValue(playerSection, "btn12", GetDInputKeyName(ctrl, controller, InputKey.a, joy));           // left kick
                    ini.WriteValue(playerSection, "btn13", GetDInputKeyName(ctrl, controller, InputKey.b, joy));           // right kick
                }
                else
                {
                    ini.WriteValue(playerSection, "btn1", GetDInputKeyName(ctrl, controller, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn2", GetDInputKeyName(ctrl, controller, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn3", GetDInputKeyName(ctrl, controller, InputKey.pageup, joy));
                    ini.WriteValue(playerSection, "btn4", GetDInputKeyName(ctrl, controller, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn5", GetDInputKeyName(ctrl, controller, InputKey.b, joy));
                    ini.WriteValue(playerSection, "btn6", GetDInputKeyName(ctrl, controller, InputKey.pagedown, joy));
                    ini.WriteValue(playerSection, "btn9", GetDInputKeyName(ctrl, controller, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn10", GetDInputKeyName(ctrl, controller, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn11", GetDInputKeyName(ctrl, controller, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn12", GetDInputKeyName(ctrl, controller, InputKey.b, joy));
                }
            }

            else
            {
                if (ctrl.PlayerIndex == 1)
                {
                    // all section
                    ini.WriteValue("all", "test", GetInputKeyName(ctrl, InputKey.r3, joy));
                    ini.WriteValue("all", "services", GetInputKeyName(ctrl, InputKey.l3, joy));
                }

                // player section
                ini.WriteValue(playerSection, "coin", GetInputKeyName(ctrl, InputKey.select, joy));
                ini.WriteValue(playerSection, "start", GetInputKeyName(ctrl, InputKey.start, joy));

                if (!SystemConfig.isOptSet("zinc_digital") || SystemConfig["zinc_digital"] != "1")
                {
                    ini.WriteValue(playerSection, "right", GetInputKeyName(ctrl, InputKey.right, joy));
                    ini.WriteValue(playerSection, "left", GetInputKeyName(ctrl, InputKey.left, joy));
                    ini.WriteValue(playerSection, "down", GetInputKeyName(ctrl, InputKey.down, joy));
                    ini.WriteValue(playerSection, "up", GetInputKeyName(ctrl, InputKey.up, joy));
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
                    ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, InputKey.y, joy));  // weak punch
                    ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, InputKey.x, joy));  // middle punch
                    ini.WriteValue(playerSection, "btn3", GetInputKeyName(ctrl, InputKey.pageup, joy));  // fierce punch
                    ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, InputKey.a, joy));  // weak kick
                    ini.WriteValue(playerSection, "btn5", GetInputKeyName(ctrl, InputKey.b, joy));  // middle kick
                    ini.WriteValue(playerSection, "btn6", GetInputKeyName(ctrl, InputKey.pagedown, joy));  // fierce kick
                }
                else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "edge")
                {
                    ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn3", GetInputKeyName(ctrl, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, InputKey.b, joy));
                    ini.WriteValue(playerSection, "btn9", GetInputKeyName(ctrl, InputKey.y, joy));            // left punch
                    ini.WriteValue(playerSection, "btn10", GetInputKeyName(ctrl, InputKey.x, joy));           // right punch
                    ini.WriteValue(playerSection, "btn11", GetInputKeyName(ctrl, InputKey.a, joy));           // kick
                    ini.WriteValue(playerSection, "btn12", GetInputKeyName(ctrl, InputKey.b, joy));           // block
                }
                else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "tekken")
                {
                    ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn5", GetInputKeyName(ctrl, InputKey.b, joy));
                    ini.WriteValue(playerSection, "btn9", GetInputKeyName(ctrl, InputKey.y, joy));            // left punch
                    ini.WriteValue(playerSection, "btn10", GetInputKeyName(ctrl, InputKey.x, joy));           // right punch
                    ini.WriteValue(playerSection, "btn12", GetInputKeyName(ctrl, InputKey.a, joy));           // left kick
                    ini.WriteValue(playerSection, "btn13", GetInputKeyName(ctrl, InputKey.b, joy));           // right kick
                }
                else
                {
                    ini.WriteValue(playerSection, "btn1", GetInputKeyName(ctrl, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn2", GetInputKeyName(ctrl, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn3", GetInputKeyName(ctrl, InputKey.pageup, joy));
                    ini.WriteValue(playerSection, "btn4", GetInputKeyName(ctrl, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn5", GetInputKeyName(ctrl, InputKey.b, joy));
                    ini.WriteValue(playerSection, "btn6", GetInputKeyName(ctrl, InputKey.pagedown, joy));
                    ini.WriteValue(playerSection, "btn9", GetInputKeyName(ctrl, InputKey.y, joy));
                    ini.WriteValue(playerSection, "btn10", GetInputKeyName(ctrl, InputKey.x, joy));
                    ini.WriteValue(playerSection, "btn11", GetInputKeyName(ctrl, InputKey.a, joy));
                    ini.WriteValue(playerSection, "btn12", GetInputKeyName(ctrl, InputKey.b, joy));
                }
            }

            foreach (var value in ini.EnumerateValues(playerSection))
            {
                if (value.Value == "null")
                    ini.Remove(playerSection, value.Key);
            }
        }

        private static string GetDInputKeyName(Controller c, SdlToDirectInput ctrl, InputKey key, string joy)
        {
            if (c.Config[key] == null)
                return "null";

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

        private static string GetInputKeyName(Controller c, InputKey key, string joy)
        {
            Int64 pid;

            var input = c.GetDirectInputMapping(key);
            if (input == null)
                return "null";

            long nb = input.Id + 1;

            if (input.Type == "button")
                return (joy + "b" + nb.ToString());

            if (input.Type == "hat")
            {
                pid = input.Value;
                switch (pid)
                {
                    case 1: return (joy + "up");
                    case 2: return (joy + "right");
                    case 4: return (joy + "down");
                    case 8: return (joy + "left");
                }
            }
            return "null";
        }
    }
}
