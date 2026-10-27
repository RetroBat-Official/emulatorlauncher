using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmulatorLauncher
{
    partial class HatariGenerator : Generator
    {
        private void SetupJoysticks(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // First initialize all joysticks to disabled (nJoyId 0 : Hatari GUI reads the id even for disabled ports)
            for (int i = 0; i < 6; i++)
            {
                string section = "Joystick" + i;
                ini.WriteValue(section, "nJoystickMode", "0");
                ini.WriteValue(section, "nJoyId", "0");
                ini.WriteValue(section, "bEnableAutoFire", "FALSE");
                ini.WriteValue(section, "bEnableJumpOnFire2", "FALSE");
                ini.WriteValue(section, "nJoyBut1Index", "0");
                ini.WriteValue(section, "nJoyBut2Index", "1");
                ini.WriteValue(section, "nJoyBut3Index", "2");
            }

            if (this.Controllers.Count == 0)
                return;

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigureInput(ini, controller, controller.PlayerIndex);
        }

        private void ConfigureInput(IniFile ini, Controller c, int playerIndex)
        {
            if (c == null || playerIndex < 1 || playerIndex > 4)
                return;

            // Hatari sections :
            // Joystick1 = standard joystick port (player 1)
            // Joystick0 = mouse port (player 2)
            // Joystick2/3 = STE enhanced ports for Jaguar joypads (STE / Mega STE only)
            // Joystick4/5 = joysticks plugged in the parallel port with an adapter (4 player games)
            bool useJoypads = SystemConfig.getOptBoolean("hatari_joypad") && (_machineType == "ste" || _machineType == "megaste");

            string joySection;
            switch (playerIndex)
            {
                case 1: joySection = useJoypads ? "Joystick2" : "Joystick1"; break;
                case 2: joySection = useJoypads ? "Joystick3" : "Joystick0"; break;
                case 3: joySection = "Joystick4"; break;
                default: joySection = "Joystick5"; break;
            }

            if (c.IsKeyboard)
            {
                // Right Ctrl is Hatari default : Shift cannot be used, Hatari ignores the keyboard joystick while Shift is held
                ini.WriteValue(joySection, "nJoystickMode", "2");
                ini.WriteValue(joySection, "kUp", "Up");
                ini.WriteValue(joySection, "kDown", "Down");
                ini.WriteValue(joySection, "kLeft", "Left");
                ini.WriteValue(joySection, "kRight", "Right");
                ini.WriteValue(joySection, "kFire", "Right Ctrl");
            }
            else
            {
                // Hatari uses the SDL2 joystick index and only opens the first 6 joysticks
                int index = c.SdlController != null ? c.SdlController.Index : c.DeviceIndex;
                if (index < 0 || index > 5)
                {
                    SimpleLogger.Instance.Warning("[Generator] Hatari: SDL index " + index + " out of range for player " + playerIndex + ", controller not configured.");
                    return;
                }

                ini.WriteValue(joySection, "nJoystickMode", "1");
                ini.WriteValue(joySection, "nJoyId", index.ToString());

                // Hatari reads raw SDL joystick buttons : button 1 = fire, button 2 = space bar (or jump), button 3 = autofire
                if (c.Config != null)
                {
                    ini.WriteValue(joySection, "nJoyBut1Index", GetJoyButton(c, InputKey.a, 0).ToString());
                    ini.WriteValue(joySection, "nJoyBut2Index", GetJoyButton(c, InputKey.b, 1).ToString());
                    ini.WriteValue(joySection, "nJoyBut3Index", GetJoyButton(c, InputKey.y, 2).ToString());

                    // Hatari reads directions on axis 0/1 and hat 0 only
                    var up = c.Config[InputKey.up];
                    if (up != null && up.Type == "button")
                        SimpleLogger.Instance.Warning("[Generator] Hatari: d-pad of player " + playerIndex + " is exposed as buttons by SDL, use the left stick.");
                }

                if (SystemConfig["hatari_button2"] == "jump")
                    ini.WriteValue(joySection, "bEnableJumpOnFire2", "TRUE");
            }

            if (SystemConfig.getOptBoolean("hatari_autofire"))
                ini.WriteValue(joySection, "bEnableAutoFire", "TRUE");
        }

        private static int GetJoyButton(Controller c, InputKey key, int defaultValue)
        {
            var input = c.Config[key];
            if (input != null && input.Type == "button" && input.Id >= 0)
                return (int)input.Id;

            return defaultValue;
        }
    }
}
