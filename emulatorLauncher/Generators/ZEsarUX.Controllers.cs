using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Windows.Input;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class ZEsarUXGenerator : Generator
    {
        private void CreateControllerConfiguration(ZEsarUXConfigFile cfg)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(1))
                ConfigureInput(controller, cfg);
        }

        private void ConfigureInput(Controller controller, ZEsarUXConfigFile cfg)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard && this.Controllers.Count(i => !i.IsKeyboard) == 0)
                return;
            else
                ConfigureJoystick(controller, cfg);
        }

        private void ConfigureJoystick(Controller controller, ZEsarUXConfigFile cfg)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            int index = controller.DeviceIndex;

            if (index < 0)
                return;

            string tech = "xinput";
            if (!controller.IsXInputDevice)
                tech = "sdl";
            if (controller.VendorID == USB_VENDOR.SONY)
                tech = "dualshock";
            if (controller.VendorID == USB_VENDOR.NINTENDO)
                tech = "nintendo";

            cfg["--realjoystickindex"] = index.ToString();
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.up, tech) + "\""] = "Up";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.down, tech) + "\""] = "Down";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.left, tech) + "\""] = "Left";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.right, tech) + "\""] = "Right";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.a, tech) + "\""] = "Fire";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.b, tech) + "\""] = "Aux1";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.x, tech) + "\""] = "Osdkeyboard";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.start, tech) + "\""] = "Enter";
            cfg["--joystickevent \"" + GetInputKeyName(controller, InputKey.select, tech) + "\""] = "EscMenu";
        }

        private static string GetInputKeyName(Controller c, InputKey key, string tech)
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
                        case 0: 
                            if (tech == "dualshock") return "1";
                            else if (tech == "nintendo") return "1";
                            else return pid.ToString();
                        case 1:
                            if (tech == "dualshock") return "2";
                            else if (tech == "nintendo") return "0";
                            else return pid.ToString();
                        case 2:
                            if (tech == "dualshock") return "0";
                            else if (tech == "nintendo") return "3";
                            else return pid.ToString();
                        case 3:
                            if (tech == "dualshock") return "3";
                            else if (tech == "nintendo") return "2";
                            else return pid.ToString();
                        case 4: return tech == "xinput" ? pid.ToString() : "8";
                        case 5: return tech == "xinput" ? pid.ToString() : "12";
                        case 6: return tech == "xinput" ? pid.ToString() : "9";
                        case 7: return tech == "xinput" ? pid.ToString() : "10";
                        case 8: return tech == "xinput" ? pid.ToString() : "11";
                        case 9: return tech == "xinput" ? pid.ToString() : "4";
                        case 10: return tech == "xinput" ? pid.ToString() : "5";
                        case 11: return "-1";
                        case 12: return "+1";
                        case 13: return "-0";
                        case 14: return "+0";
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+0";
                            else return "-0";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+1";
                            else return "-1";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            {
                                if (tech == "xinput") return "+4";
                                else return "+2";
                            }
                            else
                            {
                                if (tech == "xinput") return "-4";
                                else return "-2";
                            }
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+3";
                            else return "-3";
                        case 4:
                            if (tech == "xinput") return "+2";
                            else if (tech == "dualshock") return "+5";
                            else return "6";
                        case 5:
                            if (tech == "xinput") return "-2";
                            else if (tech == "dualshock") return "+4";
                            else return "7";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "-1";
                        case 2: return "+0";
                        case 4: return "+1";
                        case 8: return "-0";
                    }
                }
            }
            return "";
        }
    }
}