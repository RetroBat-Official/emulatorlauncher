using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Globalization;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using System.IO;

namespace EmulatorLauncher
{
    partial class YmirGenerator
    {
        /// <summary>
        /// Cf. N/A
        /// </summary>
        /// <param name="ini"></param>
        /*private void UpdateSdlControllersWithHints(IniFile ini)
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // UpdateSdlControllersWithHints(ini);

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(ini, controller, controller.PlayerIndex);
        }

        private void ConfigureInput(IniFile ini, Controller controller, int playerindex)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, playerindex);
            else
                ConfigureJoystick(ini, controller, playerindex);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;
        }

        private static string GetInputKeyName(Controller c, InputKey key, int index, bool trigger = false)
        {
            key = key.GetRevertedAxis(out bool revertAxis);
            Int64 pid;
            string ret = "";

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id + 1;
                    ret = ((index << 18) + pid).ToString();
                    return ret;
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;

                    if (trigger)
                    {
                        ret = ((index << 18) + 32768 + pid).ToString();
                    }
                    else
                    {
                        if (input.Value > 0 || revertAxis)
                            ret = ((index << 18) + 0x100000 + pid).ToString();
                        else
                            ret = ((index << 18) + 0x110000 + pid).ToString();
                    }
                    return ret;
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    ret = ((index << 18) + 0x200000 + (pid << 4)).ToString();
                    return ret;
                }
            }
            return ret;
        }
    }
}
