using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

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

        private void ConfigureJoystick(Controller controller, IniFile ini, string path)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            int index = 1;
            if (controller.DirectInput != null)
                index = controller.DirectInput.DeviceIndex + 1;

            string playerSection = "player" + controller.PlayerIndex.ToString();

            string joy = "j" + index.ToString();

            if (controller.PlayerIndex == 1)
            {
                // all section
                ini.WriteValue("all", "test", GetInputKeyName(controller, InputKey.r3, joy));
                ini.WriteValue("all", "services", GetInputKeyName(controller, InputKey.l3, joy));
            }

            // player section
            ini.WriteValue(playerSection, "coin", GetInputKeyName(controller, InputKey.select, joy));
            ini.WriteValue(playerSection, "start", GetInputKeyName(controller, InputKey.start, joy));
            
            if (!SystemConfig.isOptSet("zinc_digital") || SystemConfig["zinc_digital"] != "1")
            {
                ini.WriteValue(playerSection, "right", GetInputKeyName(controller, InputKey.right, joy));
                ini.WriteValue(playerSection, "left", GetInputKeyName(controller, InputKey.left, joy));
                ini.WriteValue(playerSection, "down", GetInputKeyName(controller, InputKey.down, joy));
                ini.WriteValue(playerSection, "up", GetInputKeyName(controller, InputKey.up, joy));
            }
            else if (controller.PlayerIndex == 1)
            {
                ini.WriteValue(playerSection, "right", "kCD");
                ini.WriteValue(playerSection, "left", "kCB");
                ini.WriteValue(playerSection, "down", "kD0");
                ini.WriteValue(playerSection, "up", "kC8");
            }
            else if (controller.PlayerIndex == 2)
            {
                ini.WriteValue(playerSection, "right", "k22");
                ini.WriteValue(playerSection, "left", "k20");
                ini.WriteValue(playerSection, "down", "k21");
                ini.WriteValue(playerSection, "up", "k13");
            }

            if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "sf")
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(controller, InputKey.y, joy));  // weak punch
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(controller, InputKey.x, joy));  // middle punch
                ini.WriteValue(playerSection, "btn3", GetInputKeyName(controller, InputKey.pageup, joy));  // fierce punch
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(controller, InputKey.a, joy));  // weak kick
                ini.WriteValue(playerSection, "btn5", GetInputKeyName(controller, InputKey.b, joy));  // middle kick
                ini.WriteValue(playerSection, "btn6", GetInputKeyName(controller, InputKey.pagedown, joy));  // fierce kick
            }
            else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "edge")
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn3", GetInputKeyName(controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(controller, InputKey.b, joy));
                ini.WriteValue(playerSection, "btn9", GetInputKeyName(controller, InputKey.y, joy));            // left punch
                ini.WriteValue(playerSection, "btn10", GetInputKeyName(controller, InputKey.x, joy));           // right punch
                ini.WriteValue(playerSection, "btn11", GetInputKeyName(controller, InputKey.a, joy));           // kick
                ini.WriteValue(playerSection, "btn12", GetInputKeyName(controller, InputKey.b, joy));           // block
            }
            else if (SystemConfig.isOptSet("zinc_control_scheme") && SystemConfig["zinc_control_scheme"] == "tekken")
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn5", GetInputKeyName(controller, InputKey.b, joy));
                ini.WriteValue(playerSection, "btn9", GetInputKeyName(controller, InputKey.y, joy));            // left punch
                ini.WriteValue(playerSection, "btn10", GetInputKeyName(controller, InputKey.x, joy));           // right punch
                ini.WriteValue(playerSection, "btn12", GetInputKeyName(controller, InputKey.a, joy));           // left kick
                ini.WriteValue(playerSection, "btn13", GetInputKeyName(controller, InputKey.b, joy));           // right kick
            }
            else
            {
                ini.WriteValue(playerSection, "btn1", GetInputKeyName(controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn2", GetInputKeyName(controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn3", GetInputKeyName(controller, InputKey.pageup, joy));
                ini.WriteValue(playerSection, "btn4", GetInputKeyName(controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn5", GetInputKeyName(controller, InputKey.b, joy));
                ini.WriteValue(playerSection, "btn6", GetInputKeyName(controller, InputKey.pagedown, joy));
                ini.WriteValue(playerSection, "btn9", GetInputKeyName(controller, InputKey.y, joy));
                ini.WriteValue(playerSection, "btn10", GetInputKeyName(controller, InputKey.x, joy));
                ini.WriteValue(playerSection, "btn11", GetInputKeyName(controller, InputKey.a, joy));
                ini.WriteValue(playerSection, "btn12", GetInputKeyName(controller, InputKey.b, joy));
            }
        }

        private static string GetInputKeyName(Controller c, InputKey key, string joy)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.GetDirectInputMapping(key);
            if (input == null)
                return "\"\"";

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
