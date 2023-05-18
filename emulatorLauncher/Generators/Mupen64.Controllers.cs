using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class Mupen64Generator
    {
        /// <summary>
        /// Cf. https://github.com/Rosalie241/RMG/tree/master/Source/RMG-Input/Utilities
        /// </summary>
        /// <param name="mupen64plus.cfg"></param>
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // UpdateSdlControllersWithHints();     // No hints found in emulator code

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(controller, ini);
        }

        private void ConfigureInput(Controller controller, IniFile ini)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(controller, ini);
            else
                ConfigureJoystick(controller, ini);
        }

        private void ConfigureJoystick(Controller controller, IniFile ini)
        {
            if (controller == null)
                return;

            var cfg = controller.Config;
            if (cfg == null)
                return;
        }

        private static void ConfigureKeyboard(Controller controller, IniFile ini)
        {
            if (controller == null)
                return;

            InputConfig keyboard = controller.Config;
            if (keyboard == null)
                return;
        }
    }
}
