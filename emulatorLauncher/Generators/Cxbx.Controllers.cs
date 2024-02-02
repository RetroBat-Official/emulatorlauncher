using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class CxbxGenerator : Generator
    {
        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            Dictionary<string, int> double_pads = new Dictionary<string, int>();
            int nsamepad = 0;

            // clear existing pad sections of ini file
            for (int i = 0; i < 4; i++)
            {
                string portSection = "input-port-" + i;
                ini.WriteValue(portSection, "Type", "-1");
                ini.WriteValue(portSection, "DeviceName", "");
                ini.WriteValue(portSection, "ProfileName", "\"\"\"");
                ini.WriteValue(portSection, "TopSlot", "-1");
                ini.WriteValue(portSection, "BottomSlot", "-1");
            }

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigureInput(ini, controller, double_pads, nsamepad); // ini has one section for each pad (from Pad1 to Pad4)
        }

        private void ConfigureInput(IniFile ini, Controller controller, Dictionary<string, int> double_pads, int nsamepad)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller, controller.PlayerIndex);
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex, double_pads, nsamepad);
        }

        /// <summary>
        /// Keyboard
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="keyboard"></param>
        /// <param name="padNumber"></param>
        private void ConfigureKeyboard(IniFile ini, Controller ctrl, int playerIndex)
        {
            if (ctrl == null)
                return;

            InputConfig keyboard = ctrl.Config;
            if (keyboard == null)
                return;

            string padType = "1";
            if (SystemConfig.isOptSet("cxbx_controller" + playerIndex) || !string.IsNullOrEmpty(SystemConfig["cxbx_controller" + playerIndex]))
                padType = SystemConfig["cxbx_controller" + playerIndex];
            
            string profileSection = "input-profile-" + (playerIndex - 1);
            ini.ClearSection(profileSection);

            if (padType != "2")
            {
                // Write profile mapping section
                string inputSection = "input-port-" + (playerIndex - 1);
                ini.WriteValue(inputSection, "Type", padType);
                ini.WriteValue(inputSection, "DeviceName", "DInput/0/KeyboardMouse");
                ini.WriteValue(inputSection, "ProfileName", "Keyboard" + playerIndex);
                ini.WriteValue(inputSection, "TopSlot", "-1");
                ini.WriteValue(inputSection, "BottomSlot", "-1");

                ini.WriteValue(profileSection, "Type", padType);
                ini.WriteValue(profileSection, "ProfileName", "Keyboard" + playerIndex);
                ini.WriteValue(profileSection, "DeviceName", "DInput/0/KeyboardMouse");
                ini.WriteValue(profileSection, "D Pad Up", "UP");
                ini.WriteValue(profileSection, "D Pad Down", "DOWN");
                ini.WriteValue(profileSection, "D Pad Left", "LEFT");
                ini.WriteValue(profileSection, "D Pad Right", "RIGHT");
                ini.WriteValue(profileSection, "Start", "RETURN");
                ini.WriteValue(profileSection, "Back", "SPACE");
                ini.WriteValue(profileSection, "L Thumb", "B");
                ini.WriteValue(profileSection, "R Thumb", "M");
                ini.WriteValue(profileSection, "A", "S");
                ini.WriteValue(profileSection, "B", "D");
                ini.WriteValue(profileSection, "X", "W");
                ini.WriteValue(profileSection, "Y", "E");
                ini.WriteValue(profileSection, "Black", "C");
                ini.WriteValue(profileSection, "White", "X");
                ini.WriteValue(profileSection, "L Trigger", "Q");
                ini.WriteValue(profileSection, "R Trigger", "R");
                ini.WriteValue(profileSection, "Left Axis X+", "H");
                ini.WriteValue(profileSection, "Left Axis X-", "F");
                ini.WriteValue(profileSection, "Left Axis Y+", "T");
                ini.WriteValue(profileSection, "Left Axis Y-", "G");
                ini.WriteValue(profileSection, "Right Axis X+", "L");
                ini.WriteValue(profileSection, "Right Axis X-", "J");
                ini.WriteValue(profileSection, "Right Axis Y+", "I");
                ini.WriteValue(profileSection, "Right Axis Y-", "K");
                ini.WriteValue(profileSection, "Motor", "");
            }

            else if (padType == "2" && playerIndex == 1)
                ConfigureGun(ini, ctrl, playerIndex);
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex, Dictionary<string, int> double_pads, int nsamepad)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            //Define tech (SDL or XInput)
            bool isXinput = ctrl.IsXInputDevice;
            string tech = isXinput ? "XInput" : "SDL";
            int padIndex = isXinput ? ctrl.XInput.DeviceIndex : (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index);
            string controllerName = isXinput ? "Gamepad" : ctrl.Name;
            string shortDeviceName = tech + controllerName;
            string padType = "1";

            if (double_pads.ContainsKey(shortDeviceName))
                nsamepad = double_pads[shortDeviceName];
            else
                nsamepad = 0;

            double_pads[shortDeviceName] = nsamepad + 1;

            string deviceName = tech + "/" + nsamepad + "/" + controllerName;

            if (SystemConfig.isOptSet("cxbx_controller" + playerIndex) || !string.IsNullOrEmpty(SystemConfig["cxbx_controller" + playerIndex]))
                padType = SystemConfig["cxbx_controller" + playerIndex];

            string profileSection = "input-profile-" + (playerIndex - 1);
            ini.ClearSection(profileSection);

            if (padType != "2")
            {
                // Write profile mapping section
                string inputSection = "input-port-" + (playerIndex - 1);
                ini.WriteValue(inputSection, "Type", padType);
                ini.WriteValue(inputSection, "DeviceName", deviceName);
                ini.WriteValue(inputSection, "ProfileName", "\"" + ctrl.Name + padIndex + "\"");
                ini.WriteValue(inputSection, "TopSlot", "-1");
                ini.WriteValue(inputSection, "BottomSlot", "-1");

                ini.WriteValue(profileSection, "Type", padType);
                ini.WriteValue(profileSection, "ProfileName", "\"" + ctrl.Name + padIndex + "\"");
                ini.WriteValue(profileSection, "DeviceName", deviceName);
                ini.WriteValue(profileSection, "D Pad Up", GetInputKeyName(ctrl, InputKey.up, isXinput));
                ini.WriteValue(profileSection, "D Pad Down", GetInputKeyName(ctrl, InputKey.down, isXinput));
                ini.WriteValue(profileSection, "D Pad Left", GetInputKeyName(ctrl, InputKey.left, isXinput));
                ini.WriteValue(profileSection, "D Pad Right", GetInputKeyName(ctrl, InputKey.right, isXinput));
                ini.WriteValue(profileSection, "Start", GetInputKeyName(ctrl, InputKey.start, isXinput));
                ini.WriteValue(profileSection, "Back", GetInputKeyName(ctrl, InputKey.select, isXinput));
                ini.WriteValue(profileSection, "L Thumb", GetInputKeyName(ctrl, InputKey.l3, isXinput));
                ini.WriteValue(profileSection, "R Thumb", GetInputKeyName(ctrl, InputKey.r3, isXinput));
                ini.WriteValue(profileSection, "A", GetInputKeyName(ctrl, InputKey.a, isXinput));
                ini.WriteValue(profileSection, "B", GetInputKeyName(ctrl, InputKey.b, isXinput));
                ini.WriteValue(profileSection, "X", GetInputKeyName(ctrl, InputKey.y, isXinput));
                ini.WriteValue(profileSection, "Y", GetInputKeyName(ctrl, InputKey.x, isXinput));
                ini.WriteValue(profileSection, "Black", GetInputKeyName(ctrl, InputKey.pageup, isXinput));
                ini.WriteValue(profileSection, "White", GetInputKeyName(ctrl, InputKey.pagedown, isXinput));
                ini.WriteValue(profileSection, "L Trigger", GetInputKeyName(ctrl, InputKey.l2, isXinput));
                ini.WriteValue(profileSection, "R Trigger", GetInputKeyName(ctrl, InputKey.r2, isXinput));
                ini.WriteValue(profileSection, "Left Axis X+", GetInputKeyName(ctrl, InputKey.leftanalogright, isXinput));
                ini.WriteValue(profileSection, "Left Axis X-", GetInputKeyName(ctrl, InputKey.leftanalogleft, isXinput));
                ini.WriteValue(profileSection, "Left Axis Y+", GetInputKeyName(ctrl, InputKey.leftanalogup, isXinput));
                ini.WriteValue(profileSection, "Left Axis Y-", GetInputKeyName(ctrl, InputKey.leftanalogdown, isXinput));
                ini.WriteValue(profileSection, "Right Axis X+", GetInputKeyName(ctrl, InputKey.rightanalogright, isXinput));
                ini.WriteValue(profileSection, "Right Axis X-", GetInputKeyName(ctrl, InputKey.rightanalogleft, isXinput));
                ini.WriteValue(profileSection, "Right Axis Y+", GetInputKeyName(ctrl, InputKey.rightanalogup, isXinput));
                ini.WriteValue(profileSection, "Right Axis Y-", GetInputKeyName(ctrl, InputKey.rightanalogdown, isXinput));

                if (SystemConfig.isOptSet("cxbx_rumble") && SystemConfig.getOptBoolean("cxbx_rumble"))
                    ini.WriteValue(profileSection, "Motor", "LeftRight");
                else
                    ini.WriteValue(profileSection, "Motor", "");
            }

            else if (padType == "2" && playerIndex == 1)
                ConfigureGun(ini, ctrl, playerIndex);
        }

        private static string GetInputKeyName(Controller c, InputKey key, bool isXinput)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return isXinput ? "Button A" : "Button " + pid;
                        case 1: return isXinput ? "Button B" : "Button " + pid;
                        case 2: return isXinput ? "Button X" : "Button " + pid;
                        case 3: return isXinput ? "Button Y" : "Button " + pid;
                        case 4: return isXinput ? "Shoulder L" : "Button " + pid;
                        case 5: return isXinput ? "Shoulder R" : "Button " + pid;
                        case 6: return isXinput ? "Back" : "Button " + pid;
                        case 7: return isXinput ? "Start" : "Button " + pid;
                        case 8: return isXinput ? "Thumb L" : "Button " + pid;
                        case 9: return isXinput ? "Thumb R" : "Button " + pid;
                        case 10: return isXinput ? "Guide" : "Button " + pid;
                        case 11: return isXinput ? "DPadUp" : "Button " + pid;
                        case 12: return isXinput ? "DPadDown" : "Button " + pid;
                        case 13: return isXinput ? "DPadLeft" : "Button " + pid;
                        case 14: return isXinput ? "DPadRight" : "Button " + pid;
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return isXinput ? "Left X+" : "Axis 0+";
                            else return isXinput ? "Left X-" : "Axis 0-";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return isXinput ? "Left Y-" : "Axis 1+";
                            else return isXinput ? "Left Y+" : "Axis 1-";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return isXinput ? "Right X+" : "Axis 2+";
                            else return isXinput ? "Right X-" : "Axis 2-";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return isXinput ? "Right Y-" : "Axis 3+";
                            else return isXinput ? "Right Y+" : "Axis 3-";
                        case 4: return isXinput ? "Trigger L" : "Axis 4+";
                        case 5: return isXinput ? "Trigger R" : "Axis 5+";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "Pad N";
                        case 2: return "Pad E";
                        case 4: return "Pad S";
                        case 8: return "Pad W";
                    }
                }
            }
            return "";
        }

        // Configure ems topgun, only for player 1 and hardmapping to keyboard/mouse
        private void ConfigureGun(IniFile ini, Controller ctrl, int playerIndex)
        {
            string profileSection = "input-profile-" + (playerIndex - 1);
            string inputSection = "input-port-" + (playerIndex - 1);
            
            ini.WriteValue(inputSection, "Type", "2");
            ini.WriteValue(inputSection, "DeviceName", "DInput/0/KeyboardMouse");
            ini.WriteValue(inputSection, "ProfileName", "Gun" + playerIndex);
            ini.WriteValue(inputSection, "TopSlot", "-1");
            ini.WriteValue(inputSection, "BottomSlot", "-1");

            ini.WriteValue(profileSection, "Type", "2");
            ini.WriteValue(profileSection, "ProfileName", "Gun" + playerIndex);
            ini.WriteValue(profileSection, "DeviceName", "DInput/0/KeyboardMouse");
            ini.WriteValue(profileSection, "Stick Up", "UP");
            ini.WriteValue(profileSection, "Stick Down", "DOWN");
            ini.WriteValue(profileSection, "Stick Left", "LEFT");
            ini.WriteValue(profileSection, "Stick Right", "RIGHT");
            ini.WriteValue(profileSection, "START", "RETURN");
            ini.WriteValue(profileSection, "SE/BA", "SPACE");
            ini.WriteValue(profileSection, "Trigger", "Click 0");
            ini.WriteValue(profileSection, "Grip", "Click 1");
            ini.WriteValue(profileSection, "A", "S");
            ini.WriteValue(profileSection, "B", "D");
            ini.WriteValue(profileSection, "Aim X+", "Cursor X+");
            ini.WriteValue(profileSection, "Aim X-", "Cursor X-");
            ini.WriteValue(profileSection, "Aim Y+", "Cursor Y+");
            ini.WriteValue(profileSection, "Aim Y-", "Cursor Y-");
            ini.WriteValue(profileSection, "Turbo Left", "");
            ini.WriteValue(profileSection, "Turbo Right", "");
            ini.WriteValue(profileSection, "Laser", "C");
        }

        private static string SdlToKeyCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x0D: return "RETURN";
                case 0x00: return "";
                case 0x08: return "BACKSPACE";
                case 0x09: return "TAB";
                case 0x1B: return "ESCAPE";
                case 0x20: return "SPACE";
                case 0x21: return "EXCLAIM";
                case 0x22: return "QUOTEDBL";
                case 0x23: return "HASH";
                case 0x24: return "DOLLAR";
                case 0x25: return "PERCENT";
                case 0x26: return "AMPERSAND";
                case 0x27: return "QUOTE";
                case 0x28: return "LEFTPAREN";
                case 0x29: return "RIGHTPAREN";
                case 0x2A: return "ASTERISK";
                case 0x2B: return "PLUS";
                case 0x2C: return "COMMA";
                case 0x2D: return "MINUS";
                case 0x2E: return "PERIOD";
                case 0x2F: return "SLASH";
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
                case 0x3A: return "COLON";
                case 0x3B: return "SEMICOLON";
                case 0x3C: return "LESS";
                case 0x3D: return "EQUALS";
                case 0x3E: return "GREATER";
                case 0x3F: return "QUESTION";
                case 0x40: return "AT";
                case 0x5B: return "LBRACKET";
                case 0x5C: return "BACKSLASH";
                case 0x5D: return "RBRACKET";
                case 0x5E: return "CARET";
                case 0x5F: return "UNDERSCORE";
                case 0x60: return "BACKQUOTE";
                case 0x61: return "A";
                case 0x62: return "B";
                case 0x63: return "C";
                case 0x64: return "D";
                case 0x65: return "E";
                case 0x66: return "F";
                case 0x67: return "G";
                case 0x68: return "H";
                case 0x69: return "I";
                case 0x6A: return "J";
                case 0x6B: return "K";
                case 0x6C: return "L";
                case 0x6D: return "M";
                case 0x6E: return "N";
                case 0x6F: return "O";
                case 0x70: return "P";
                case 0x71: return "Q";
                case 0x72: return "R";
                case 0x73: return "S";
                case 0x74: return "T";
                case 0x75: return "U";
                case 0x76: return "V";
                case 0x77: return "W";
                case 0x78: return "X";
                case 0x79: return "Y";
                case 0x7A: return "Z";
                case 0x7F: return "DELETE";
                case 0x40000039: return "CAPITAL";
                case 0x4000003A: return "F1";
                case 0x4000003B: return "F2";
                case 0x4000003C: return "F3";
                case 0x4000003D: return "F4";
                case 0x4000003E: return "F5";
                case 0x4000003F: return "F6";
                case 0x40000040: return "F7";
                case 0x40000041: return "F8";
                case 0x40000042: return "F9";
                case 0x40000043: return "F10";
                case 0x40000044: return "F11";
                case 0x40000045: return "F12";
                case 0x40000046: return "SYSRQ";
                case 0x40000047: return "SCROLLLOCK";
                case 0x40000048: return "PAUSE";
                case 0x40000049: return "INSERT";
                case 0x4000004A: return "HOME";
                case 0x4000004B: return "PRIOR";
                case 0x4000004D: return "END";
                case 0x4000004E: return "NEXT";
                case 0x4000004F: return "RIGHT";
                case 0x40000050: return "LEFT";
                case 0x40000051: return "DOWN";
                case 0x40000052: return "UP";
                case 0x40000053: return "NUMLOCK";
                case 0x40000054: return "DIVIDE";
                case 0x40000055: return "MULTIPLY";
                case 0x40000056: return "SUBTRACT";
                case 0x40000057: return "ADD";
                case 0x40000058: return "NUMPADENTER";
                case 0x40000059: return "NUMPAD1";
                case 0x4000005A: return "NUMPAD2";
                case 0x4000005B: return "NUMPAD3";
                case 0x4000005C: return "NUMPAD4";
                case 0x4000005D: return "NUMPAD5";
                case 0x4000005E: return "NUMPAD6";
                case 0x4000005F: return "NUMPAD7";
                case 0x40000060: return "NUMPAD8";
                case 0x40000061: return "NUMPAD9";
                case 0x40000062: return "NUMPAD0";
                case 0x40000063: return "DECIMAL";
                case 0x40000067: return "NUMPADEQUALS";
                case 0x40000068: return "F13";
                case 0x40000069: return "F14";
                case 0x4000006A: return "F15";
                case 0x4000006B: return "F16";
                case 0x4000006C: return "F17";
                case 0x4000006D: return "F18";
                case 0x4000006E: return "F19";
                case 0x4000006F: return "F20";
                case 0x40000070: return "F21";
                case 0x40000071: return "F22";
                case 0x40000072: return "F23";
                case 0x40000073: return "F24";
                case 0x40000074: return "EXECUTE";
                case 0x40000075: return "HELP";
                case 0x40000076: return "MENU";
                case 0x40000077: return "SELECT";
                case 0x40000078: return "STOP";
                case 0x40000079: return "AGAIN";
                case 0x4000007A: return "UNDO";
                case 0x4000007B: return "CUT";
                case 0x4000007C: return "COPY";
                case 0x4000007D: return "PASTE";
                case 0x4000007E: return "MENU";
                case 0x4000007F: return "MUTE";
                case 0x40000080: return "VOLUMEUP";
                case 0x40000081: return "VOLUMEDOWN";
                case 0x40000085: return "DECIMAL";
                case 0x400000E0: return "LCONTROL";
                case 0x400000E1: return "LSHIFT";
                case 0x400000E2: return "LMENU";
                case 0x400000E4: return "RCONTROL";
                case 0x400000E5: return "RSHIFT";
                case 0x400000E6: return "RALT";
                case 0x400000E7: return "RGUI";
                case 0x40000101: return "MODE";
                case 0x40000102: return "AUDIONEXT";
                case 0x40000103: return "AUDIOPREV";
                case 0x40000105: return "AUDIOPLAY";
            }
            return "";
        }
    }
}
