using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;
using System.Globalization;

namespace emulatorLauncher
{
    partial class OpenBorGenerator : Generator
    {
        private void SetupControllers(ConfigFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!Controllers.Any())
                return;

            bool hasKeyb = false;

            var controllers = Controllers.OrderBy(i => i.PlayerIndex).ToArray();

            for (int idx = 0; idx < 4; idx++)
            {
                ResetController(ini, idx);

                var ctl = idx < controllers.Length ? controllers[idx] : null;
                if (ctl == null || ctl.Config == null)
                {
                    if (!hasKeyb)
                    {
                        ConfigureDefaultKeyboard(ini, idx);
                        hasKeyb = true;
                    }

                    continue;
                }

                if (ctl.Config.Type == "keyboard")
                {
                    hasKeyb = true;
                    ConfigureKeyboard(ini, idx, ctl);
                    continue;
                }

                ConfigureJoystick(ini, idx, ctl);
            }
        }

        private void ResetController(ConfigFile ini, int idx)
        {
            for (int i = 0; i < 17; i++)
                ini["keys." + idx + "." + i] = "0";
        }

        private void ConfigureJoystick(ConfigFile ini, int idx, Controller c)
        {
            ini["keys." + idx + ".0"] = JoystickValue(InputKey.up, c).ToString();    // PADUP
            ini["keys." + idx + ".1"] = JoystickValue(InputKey.down, c).ToString();  // PADDOWN
            ini["keys." + idx + ".2"] = JoystickValue(InputKey.left, c).ToString();  // PADLEFT
            ini["keys." + idx + ".3"] = JoystickValue(InputKey.right, c).ToString(); // PADRIGHT
            ini["keys." + idx + ".4"] = JoystickValue(InputKey.y, c).ToString(); // ATTACK
            ini["keys." + idx + ".5"] = JoystickValue(InputKey.b, c).ToString(); // ATTACK 2
            ini["keys." + idx + ".6"] = JoystickValue(InputKey.pagedown, c).ToString(); // ATTACK 3
            ini["keys." + idx + ".7"] = JoystickValue(InputKey.pageup, c).ToString(); // ATTACK4
            ini["keys." + idx + ".8"] = JoystickValue(InputKey.a, c).ToString(); // JUMP
            ini["keys." + idx + ".9"] = JoystickValue(InputKey.x, c).ToString(); // SPECIAL
            ini["keys." + idx + ".10"] = JoystickValue(InputKey.start, c).ToString(); // START
            ini["keys." + idx + ".11"] = "0"; // SCREENSHOT

            // EXIT
            if (Program.EnableHotKeyStart)
                ini["keys." + idx + ".12"] = JoystickValue(InputKey.hotkey, c).ToString(); // esc
            else
                ini["keys." + idx + ".12"] = "0";

            ini["keys." + idx + ".13"] = JoystickValue(InputKey.joystick1up, c).ToString(); // AXISUP
            ini["keys." + idx + ".14"] = JoystickValue(InputKey.joystick1up, c, true).ToString(); // AXISDOWN
            ini["keys." + idx + ".15"] = JoystickValue(InputKey.joystick1left, c).ToString(); // AXISLEFT
            ini["keys." + idx + ".16"] = JoystickValue(InputKey.joystick1left, c, true).ToString(); // AXISRIGHT
        }

        private void ConfigureDefaultKeyboard(ConfigFile ini, int idx)
        {
            ini["keys." + idx + ".0"] = "82";
            ini["keys." + idx + ".1"] = "81";
            ini["keys." + idx + ".2"] = "80";
            ini["keys." + idx + ".3"] = "79";
            ini["keys." + idx + ".4"] = "4";
            ini["keys." + idx + ".5"] = "22";
            ini["keys." + idx + ".6"] = "29";
            ini["keys." + idx + ".7"] = "27";
            ini["keys." + idx + ".8"] = "7";
            ini["keys." + idx + ".9"] = "9";
            ini["keys." + idx + ".10"] = "40";
            ini["keys." + idx + ".11"] = "69";
            ini["keys." + idx + ".12"] = "41"; // Esc            
        }

        private void ConfigureKeyboard(ConfigFile ini, int idx, Controller c)
        {
            ini["keys." + idx + ".0"] = KeyboardValue(InputKey.up, c).ToString();    // PADUP
            ini["keys." + idx + ".1"] = KeyboardValue(InputKey.down, c).ToString();  // PADDOWN
            ini["keys." + idx + ".2"] = KeyboardValue(InputKey.left, c).ToString();  // PADLEFT
            ini["keys." + idx + ".3"] = KeyboardValue(InputKey.right, c).ToString(); // PADRIGHT
            ini["keys." + idx + ".4"] = KeyboardValue(InputKey.b, c).ToString();     // ATTACK
            ini["keys." + idx + ".5"] = KeyboardValue(InputKey.x, c).ToString();     // ATTACK 2
            ini["keys." + idx + ".6"] = KeyboardValue(InputKey.pagedown, c).ToString(); // ATTACK 3
            ini["keys." + idx + ".7"] = KeyboardValue(InputKey.pageup, c).ToString(); // ATTACK4
            ini["keys." + idx + ".8"] = KeyboardValue(InputKey.a, c).ToString(); // JUMP
            ini["keys." + idx + ".9"] = KeyboardValue(InputKey.y, c).ToString();
            ini["keys." + idx + ".10"] = KeyboardValue(InputKey.start, c).ToString();
            ini["keys." + idx + ".11"] = "69"; // F12
            ini["keys." + idx + ".12"] = "41"; // Esc            
        }

        public int JoystickValue(InputKey key, Controller c, bool invertAxis = false)
        {
            var a = c.Config[key];
            if (a == null)
            {
                if (key == InputKey.hotkey)
                    a = c.Config[InputKey.hotkey];

                if (a == null)
                    return 0;
            }

            int nbButtons = c.NbButtons;
            int nbAxes = c.NbAxes;

            // With Custom Retrobat Build, Button & Axes count are hardcoded for easiest injection
            if (_isCustomRetrobatOpenBor)
            {
                nbButtons = 20;
                nbAxes = 8;
            }
            else if (c.IsXInputDevice)
            {
                nbButtons = 11;
                nbAxes = 6;                
            }

            int JOY_MAX_INPUTS = 64;

            int sdlIndex = c.SdlController != null ? c.SdlController.Index : c.DeviceIndex; // c.PlayerIndex - 1
            int value = 0;

            if (a.Type == "button")
                value = 1 + sdlIndex * JOY_MAX_INPUTS + (int)a.Id;
            else if (a.Type == "hat")
            {
                int hatfirst = 1 + sdlIndex * JOY_MAX_INPUTS + nbButtons + 2 * nbAxes + 4 * (int)a.Id;
                if (a.Value == 2) // SDL_HAT_RIGHT
                    hatfirst += 1;
                else if (a.Value == 4) // SDL_HAT_DOWN
                    hatfirst += 2;
                else if (a.Value == 8) // SDL_HAT_LEFT
                    hatfirst += 3;

                value = hatfirst;
            }
            else if (a.Type == "axis")
            {
                int axisfirst = 1 + sdlIndex * JOY_MAX_INPUTS + nbButtons + 2 * (int)a.Id;
                if ((invertAxis && a.Value < 0) || (!invertAxis && a.Value > 0)) axisfirst++;
                value = axisfirst;
            }

            if (c.Config.Type != "keyboard")
                value += 600;

            return value;
        }

        public int KeyboardValue(InputKey key, Controller c)
        {
            var a = c.Config[key];
            if (a == null)
                return 0;

            var azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };

            int id = (int)a.Id;
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
            {
                if (id == 'a')
                    id = 'q';
                else if (id == 'q')
                    id = 'a';
                else if (id == 'w')
                    id = 'z';
                else if (id == 'z')
                    id = 'w';
            }

            var mapped = SDL.SDL_default_keymap.Select(k => Convert.ToInt32(k)).ToList().IndexOf(id);
            if (mapped >= 0)
                return mapped;

            return 0;
        }
    }
}
