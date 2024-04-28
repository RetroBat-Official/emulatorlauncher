using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class RedreamGenerator : Generator
    {
        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // Clear existing ports

            for (int i = 0; i < 4; i++)
                ini.WriteValue("", "port" + i, "dev:1,desc:disabled,type:controller");

            if (SystemConfig.isOptSet("redream_controller_autoconfig") && SystemConfig.getOptBoolean("redream_controller_autoconfig"))
            {
                for (int i = 0; i < 4; i++)
                    ini.WriteValue("", "port" + i, "dev:0,desc:auto,type:controller");
            }
            else
            {
                foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                    ConfigureInput(ini, controller, controller.PlayerIndex);
            }
        }

        private void ConfigureInput(IniFile ini, Controller controller, int playerIndex)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, playerIndex);
            else
                ConfigureJoystick(ini, controller, playerIndex);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerIndex)
        {
            if (keyboard == null)
                return;

            string portNr = "port" + (playerIndex - 1);
            string profileNr = "profile" + (playerIndex - 1);

            ini.WriteValue("", portNr, "dev:2,desc:keyboard,type:controller");

            var profileList = new List<string>
            {
                "name:keyboard0,type:controller,deadzone:12,crosshair:1"
            };

            Action<string, InputKey> WriteKeyboardMapping = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                    profileList.Add(v + ":" + SdlToKeyCode(a.Id));
            };

            WriteKeyboardMapping("a", InputKey.a);
            WriteKeyboardMapping("b", InputKey.b);
            WriteKeyboardMapping("x", InputKey.y);
            WriteKeyboardMapping("y", InputKey.x);
            WriteKeyboardMapping("start", InputKey.start);
            WriteKeyboardMapping("dpad_up", InputKey.up);
            WriteKeyboardMapping("dpad_down", InputKey.down);
            WriteKeyboardMapping("dpad_left", InputKey.left);
            WriteKeyboardMapping("dpad_right", InputKey.right);
            WriteKeyboardMapping("ljoy_up", InputKey.leftanalogup);
            WriteKeyboardMapping("ljoy_down", InputKey.leftanalogdown);
            WriteKeyboardMapping("ljoy_left", InputKey.leftanalogleft);
            WriteKeyboardMapping("ljoy_right", InputKey.leftanalogright);
            WriteKeyboardMapping("ltrig", InputKey.pageup);
            WriteKeyboardMapping("rtrig", InputKey.pagedown);
            WriteKeyboardMapping("menu", InputKey.select);

            string profile = string.Join(",", profileList);

            ini.WriteValue("", profileNr, profile);
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            string portNr = "port" + (playerIndex - 1);
            string profileNr = "profile" + (playerIndex - 1);

            string tech = "xinput";
            if (ctrl.VendorID == USB_VENDOR.NINTENDO)
                tech = "nintendo";
            else if (!ctrl.IsXInputDevice)
                tech = "sdl";

            int index = 0;
            if (ctrl.DirectInput != null)
                index = ctrl.DeviceIndex + 4;

            string guid = ctrl.Guid.ToLowerInvariant();

            if (guid.EndsWith("6803"))
                guid = guid.Substring(0, guid.Length - 4) + "6800";
            else if (guid.EndsWith("7200"))
                guid = guid.Substring(0, guid.Length - 4) + "7801";

            ini.WriteValue("", portNr, "dev:" + index + ",desc:" + guid + ",type:controller");

            var profileList = new List<string>
            {
                "name:" + guid + ",type:controller,deadzone:12,crosshair:1",
                "a:" + GetInputKeyName(ctrl, InputKey.a, tech),
                "b:" + GetInputKeyName(ctrl, InputKey.b, tech),
                "x:" + GetInputKeyName(ctrl, InputKey.y, tech),
                "y:" + GetInputKeyName(ctrl, InputKey.x, tech),
                "start:" + GetInputKeyName(ctrl, InputKey.start, tech),
                "dpad_up:" + GetInputKeyName(ctrl, InputKey.up, tech),
                "dpad_down:" + GetInputKeyName(ctrl, InputKey.down, tech),
                "dpad_left:" + GetInputKeyName(ctrl, InputKey.left, tech),
                "dpad_right:" + GetInputKeyName(ctrl, InputKey.right, tech),
                "ljoy_up:" + GetInputKeyName(ctrl, InputKey.leftanalogup, tech),
                "ljoy_down:" + GetInputKeyName(ctrl, InputKey.leftanalogdown, tech),
                "ljoy_left:" + GetInputKeyName(ctrl, InputKey.leftanalogleft, tech),
                "ljoy_right:" + GetInputKeyName(ctrl, InputKey.leftanalogright, tech)
            };

            if (SystemConfig.isOptSet("redream_use_digital_triggers") && SystemConfig.getOptBoolean("redream_use_digital_triggers"))
            {
                profileList.Add("ltrig:" + GetInputKeyName(ctrl, InputKey.pageup, tech));
                profileList.Add("rtrig:" + GetInputKeyName(ctrl, InputKey.pagedown, tech));
            }
            else
            {
                profileList.Add("ltrig:" + GetInputKeyName(ctrl, InputKey.l2, tech));
                profileList.Add("rtrig:" + GetInputKeyName(ctrl, InputKey.r2, tech));
            }

            profileList.Add("menu:" + GetInputKeyName(ctrl, InputKey.select, tech));

            string profile = string.Join(",", profileList);
            ini.WriteValue("", profileNr, profile);
        }

        private static string GetInputKeyName(Controller c, InputKey key, string tech)
        {
            Int64 pid;
            bool isXinput = tech == "xinput";

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];

            if (input != null)
            {
                if (input.Type == "button")
                {
                    return "joy" + input.Id;
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+axis0";
                            else return "-axis0";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+axis1";
                            else return "-axis1";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return isXinput ? "+axis3" : "+axis2";
                            else return isXinput ? "-axis3" : "-axis2";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return isXinput ? "+axis4" : "+axis3";
                            else return isXinput ? "-axis4" : "-axis3";
                        case 4: return isXinput ? "+axis2" : "+axis4";
                        case 5: return "+axis5";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "hat0";
                        case 2: return "hat3";
                        case 4: return "hat1";
                        case 8: return "hat2";
                    }
                }
            }

            return "\"\"";
        }

        private static string SdlToKeyCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x0D: return "return";
                case 0x08: return "backspace";
                case 0x09: return "tab";
                case 0x1B: return "escape";
                case 0x20: return "space";
                case 0x30: return "0";
                case 0x31: return "1";
                case 0x32: return "2";
                case 0x33: return "3";
                case 0x34: return "4";
                case 0x35: return "5";
                case 0x36: return "6";
                case 0x37: return "7";
                case 0x38: return "8";
                case 0x39: return "9";
                case 0x3D: return "=";
                case 0x61: return "a";
                case 0x62: return "b";
                case 0x63: return "c";
                case 0x64: return "d";
                case 0x65: return "e";
                case 0x66: return "f";
                case 0x67: return "g";
                case 0x68: return "h";
                case 0x69: return "i";
                case 0x6A: return "j";
                case 0x6B: return "k";
                case 0x6C: return "l";
                case 0x6D: return "m";
                case 0x6E: return "n";
                case 0x6F: return "o";
                case 0x70: return "p";
                case 0x71: return "q";
                case 0x72: return "r";
                case 0x73: return "s";
                case 0x74: return "t";
                case 0x75: return "u";
                case 0x76: return "v";
                case 0x77: return "w";
                case 0x78: return "x";
                case 0x79: return "y";
                case 0x7A: return "z";
                case 0x7F: return "delete";
                case 0x40000039: return "capslock";
                case 0x4000003A: return "f1";
                case 0x4000003B: return "f2";
                case 0x4000003C: return "f3";
                case 0x4000003D: return "f4";
                case 0x4000003E: return "f5";
                case 0x4000003F: return "f6";
                case 0x40000040: return "f7";
                case 0x40000041: return "f8";
                case 0x40000042: return "f9";
                case 0x40000043: return "f10";
                case 0x40000044: return "f11";
                case 0x40000045: return "f12";
                case 0x40000047: return "scrolllock";
                case 0x40000049: return "insert";
                case 0x4000004A: return "home";
                case 0x4000004B: return "pageup";
                case 0x4000004D: return "end";
                case 0x4000004E: return "pagedown";
                case 0x4000004F: return "right";
                case 0x40000050: return "left";
                case 0x40000051: return "down";
                case 0x40000052: return "up";
                case 0x40000053: return "numlock";
                case 0x40000054: return "kp_divide";
                case 0x40000055: return "kp_multiply";
                case 0x40000056: return "kp_minus";
                case 0x40000057: return "kp_plus";
                case 0x40000058: return "kp_enter";
                case 0x40000059: return "kp_1";
                case 0x4000005A: return "kp_2";
                case 0x4000005B: return "kp_3";
                case 0x4000005C: return "kp_4";
                case 0x4000005D: return "kp_5";
                case 0x4000005E: return "kp_6";
                case 0x4000005F: return "kp_7";
                case 0x40000060: return "kp_8";
                case 0x40000061: return "kp_9";
                case 0x40000062: return "kp_0";
                case 0x40000063: return "kp_period";
                case 0x400000E0: return "lctrl";
                case 0x400000E1: return "lshift";
                case 0x400000E2: return "lalt";
                case 0x400000E4: return "rctrl";
                case 0x400000E5: return "rshift";
                case 0x400000E6: return "ralt";
            }
            return "none";
        }
    }
}
