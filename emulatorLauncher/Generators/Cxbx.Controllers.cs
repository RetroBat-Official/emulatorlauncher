using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class CxbxGenerator : Generator
    {
        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            Dictionary<string, int> double_pads = new Dictionary<string, int>();

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
                ConfigureInput(ini, controller, double_pads); // ini has one section for each pad (from Pad1 to Pad4)
        }

        private void ConfigureInput(IniFile ini, Controller controller, Dictionary<string, int> double_pads)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller, controller.PlayerIndex);
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex, double_pads);
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
            if (SystemConfig.getOptBoolean("use_guns") && playerIndex == 1)
                padType = "2";

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
                ConfigureGun(ini, playerIndex);
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex, Dictionary<string, int> double_pads)
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
            int nsamepad;
            if (double_pads.ContainsKey(shortDeviceName))
                nsamepad = double_pads[shortDeviceName];
            else
                nsamepad = 0;

            double_pads[shortDeviceName] = nsamepad + 1;

            string deviceName = tech + "/" + nsamepad + "/" + controllerName;

            if (SystemConfig.getOptBoolean("use_guns") && playerIndex == 1)
                padType = "2";

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
                ConfigureGun(ini, playerIndex);
        }

        private static string GetInputKeyName(Controller c, InputKey key, bool isXinput)
        {
            Int64 pid;

            key = key.GetRevertedAxis(out bool revertAxis);

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
        private void ConfigureGun(IniFile ini, int playerIndex)
        {
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
            }

            bool invert = SystemConfig.getOptBoolean("gun_invert");

            // DolphinBar Wiimote : the D-pad already sends the arrow keys (gun stick). Cxbx allows only one key per button,
            // so Start goes on button 1 (mode 1) or + (mode 2) and SE/BA on button 2 (mode 1 only, mode 2 sends volume keys)
            bool dolphinBar = guns.Any(g => g.Type == RawLighGunType.MayFlashWiimote);
            bool wiimoteNormal = SystemConfig["WiimoteMode"] == "normal";

            string startKey = "1";
            string backKey = "5";

            if (dolphinBar)
            {
                startKey = wiimoteNormal ? "PRIOR" : "RETURN";
                if (wiimoteNormal)
                    backKey = "NEXT";
            }

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
            ini.WriteValue(profileSection, "START", startKey);
            ini.WriteValue(profileSection, "SE/BA", backKey);
            ini.WriteValue(profileSection, "Trigger", invert ? "Click 1" : "Click 0");
            ini.WriteValue(profileSection, "Grip", invert ? "Click 0" : "Click 1");
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
    }
}
