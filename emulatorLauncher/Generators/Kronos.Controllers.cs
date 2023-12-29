using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Globalization;
using EmulatorLauncher.Common.Joysticks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace EmulatorLauncher
{
    partial class KronosGenerator
    {
        /// <summary>
        /// Cf. https://github.com/PCSX2/pcsx2/blob/master/pcsx2/Frontend/SDLInputSource.cpp#L211
        /// </summary>
        /// <param name="pcsx2ini"></param>
        private void UpdateSdlControllersWithHints(IniFile ini)
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private void CreateControllerConfiguration(string path, IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // UpdateSdlControllersWithHints(ini);

            if (SystemConfig.isOptSet("kronos_multitap") && !string.IsNullOrEmpty(SystemConfig["kronos_multitap"]))
                _multitap = SystemConfig["kronos_multitap"];
            else if (Controllers.Count > 7)
                _multitap = "both";
            else if (Controllers.Count > 2)
                _multitap = "2";
            else
                _multitap = "0";

            // clear existing pad sections of ini file
            for (int i = 1; i < 7; i++)
            {
                string port1Type = "Input\\Port\\1\\Id\\" + i.ToString() + "\\Type";
                string port2Type = "Input\\Port\\2\\Id\\" + i.ToString() + "\\Type";

                ini.WriteValue("1.0", port1Type, "0");
                ini.WriteValue("1.0", port2Type, "0");
            }

            ini.WriteValue("1.0", "Input\\PerCore", "3");

            /* Configure hotkeys
            ResetHotkeysToDefault(ini);*/

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


        private void ConfigureJoystick(IniFile ini, Controller controller, int playerindex)
        {
            return;     // TODO
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            string port = GetControllerPort(playerindex);
            string controllerId = GetControllerId(playerindex, port);

            ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Type", "2");

            bool azerty = false;
            List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                azerty = true;

            if (azerty)
            {
                foreach (KeyValuePair<string, string> pair in AzertyLayout)
                {
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\2" + "\\Key\\" + pair.Key, pair.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> pair in keyboardMapping)
                {
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\2" + "\\Key\\" + pair.Key, pair.Value);
                }
            }
        }

        static Dictionary<string, string> keyboardMapping = new Dictionary<string, string>
        {
            { "0", "16777235" },            // up
            { "1", "16777236" },            // Right
            { "2", "16777237" },            // Down
            { "3", "16777234" },            // Left
            { "4", "69" },                  // Right trigger - E
            { "5", "81" },                  // Left trigger - Q
            { "6", "16777220" },            // Start - enter
            { "7", "90" },                  // Z
            { "8", "88" },                  // X
            { "9", "67" },                  // C
            { "10", "65" },                 // A
            { "11", "83" },                 // S
            { "12", "68" },                 // D
        };

        static Dictionary<string, string> AzertyLayout = new Dictionary<string, string>
        {
            { "0", "16777235" },            // up
            { "1", "16777236" },            // Right
            { "2", "16777237" },            // Down
            { "3", "16777234" },            // Left
            { "4", "69" },                  // Right trigger - E
            { "5", "65" },                  // Left trigger - A
            { "6", "16777220" },            // Start - enter
            { "7", "87" },                  // W
            { "8", "88" },                  // X
            { "9", "67" },                  // C
            { "10", "81" },                 // Q
            { "11", "83" },                 // S
            { "12", "68" },                 // D
        };

        private string GetControllerPort(int playerindex)
        {
            if (_multitap == "both")
                return multitap_ports[playerindex];

            if (_multitap == "1")
                return multitap_port1[playerindex];

            if (_multitap == "2")
                return multitap_port2[playerindex];

            if (playerindex == 1)
                return "1";

            if (playerindex == 2)
                return "2";

            return "1";
        }

        private string GetControllerId(int playerindex, string port)
        {
            if (playerindex < 13)
                return multitap_id[playerindex];

            return "1";
        }

        static Dictionary<int, string> multitap_port1 = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "2" },
            { 3, "1" },
            { 4, "1" },
            { 5, "1" },
            { 6, "1" },
            { 7, "1" },
        };

        static Dictionary<int, string> multitap_port2 = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "2" },
            { 3, "2" },
            { 4, "2" },
            { 5, "2" },
            { 6, "2" },
            { 7, "2" },
        };

        static Dictionary<int, string> multitap_ports = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "2" },
            { 3, "1" },
            { 4, "1" },
            { 5, "1" },
            { 6, "1" },
            { 7, "1" },
            { 8, "2" },
            { 9, "2" },
            { 10, "2" },
            { 11, "2" },
            { 12, "2" },
        };

        static Dictionary<int, string> multitap_id = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "1" },
            { 3, "2" },
            { 4, "3" },
            { 5, "4" },
            { 6, "5" },
            { 7, "6" },
            { 8, "2" },
            { 9, "3" },
            { 10, "4" },
            { 11, "5" },
            { 12, "6" },
        };
    }
}
