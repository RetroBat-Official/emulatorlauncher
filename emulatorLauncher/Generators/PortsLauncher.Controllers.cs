using System.IO;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using System;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        #region ports

        private void ConfigureCGeniusControls(IniFile ini)
        {
            if (_emulator != "cgenius")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // clear existing pad sections of ini file
            for (int i = 0; i < 4; i++)
            {
                ini.ClearSection("input" + i.ToString());
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
            {
                ConfigureInputCGenius(ini, controller, controller.PlayerIndex - 1);
            }
        }

        private void ConfigureInputCGenius(IniFile ini, Controller ctrl, int padIndex)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            if (ctrl.IsKeyboard)
            {
                ini.WriteValue("input" + padIndex, "Back", "Key 27 (Escape)");
                ini.WriteValue("input" + padIndex, "Camlead", "Key 99 (C)");
                ini.WriteValue("input" + padIndex, "Down", "Key 1073741905 (Down)");
                ini.WriteValue("input" + padIndex, "Fire", "Key 32 (Space)");
                ini.WriteValue("input" + padIndex, "Help", "Key 1073741882 (F1)");
                ini.WriteValue("input" + padIndex, "Jump", "Key 1073742048 (Left Ctrl)");
                ini.WriteValue("input" + padIndex, "Left", "Key 1073741904 (Left)");
                ini.WriteValue("input" + padIndex, "Lower-Left", "Key 1073741901 (End)");
                ini.WriteValue("input" + padIndex, "Lower-Right", "Key 1073741902 (PageDown)");
                ini.WriteValue("input" + padIndex, "Pogo", "Key 1073742050 (Left Alt)");
                ini.WriteValue("input" + padIndex, "Quickload", "Key 1073741890 (F9)");
                ini.WriteValue("input" + padIndex, "Quicksave", "Key 1073741887 (F6)");
                ini.WriteValue("input" + padIndex, "Right", "Key 1073741903 (Right)");
                ini.WriteValue("input" + padIndex, "Run", "Key 1073742049 (Left Shift)");
                ini.WriteValue("input" + padIndex, "Status", "Key 13 (Return)");
                ini.WriteValue("input" + padIndex, "Up", "Key 1073741906 (Up)");
                ini.WriteValue("input" + padIndex, "Upper-Left", "Key 1073741898 (Home)");
                ini.WriteValue("input" + padIndex, "Upper-Right", "Key 1073741899 (PageUp)");
            }
            else
            {
                if (ctrl == null)
                    return;

                InputConfig joy = ctrl.Config;
                if (joy == null)
                    return;

                string joyPad = "Joy" + (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index) + "-";

                foreach (var button in cgeniusMapping)
                {
                    if (padIndex != 0 && (button.Value == "Back" || button.Value == "Help"))
                        continue;

                    InputKey toSet = button.Key;
                    
                    if (SystemConfig.isOptSet("cgenius_analogPad") && SystemConfig.getOptBoolean("cgenius_analogPad"))
                    {
                        if (button.Key == InputKey.up)
                            toSet = InputKey.leftanalogdown;
                        else if (button.Key == InputKey.down)
                            toSet = InputKey.leftanalogdown;
                        else if (button.Key == InputKey.left)
                            toSet = InputKey.leftanalogleft;
                        else if (button.Key == InputKey.right)
                            toSet = InputKey.leftanalogright;
                    }

                    var input = ctrl.Config[toSet];
                    if (input != null)
                        ini.WriteValue("input" + padIndex, button.Value, joyPad + GetSDLInputName(ctrl, toSet, "cgenius"));
                    else
                        ini.WriteValue("input" + padIndex, button.Value, "Key 0 ()");
                }

                if (padIndex == 0)
                {
                    ini.WriteValue("input" + padIndex, "Quickload", "Key 1073741890 (F9)");
                    ini.WriteValue("input" + padIndex, "Quicksave", "Key 1073741887 (F6)");
                }
                else
                {
                    ini.WriteValue("input" + padIndex, "Quickload", "Key 0 ()");
                    ini.WriteValue("input" + padIndex, "Quicksave", "Key 0 ()");
                }
                ini.WriteValue("input" + padIndex, "Lower-Left", "Key 1073741901 (End)");
                ini.WriteValue("input" + padIndex, "Lower-Right", "Key 1073741902 (PageDown)");
                ini.WriteValue("input" + padIndex, "Upper-Left", "Key 1073741898 (Home)");
                ini.WriteValue("input" + padIndex, "Upper-Right", "Key 1073741899 (PageUp)");

                BindBoolIniFeature(ini, "input" + padIndex, "Analog", "cgenius_analogPad", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "TwoButtonFiring", "cgenius_TwoButtonFiring", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "SuperPogo", "cgenius_SuperPogo", "true", "false");
                BindBoolIniFeatureOn(ini, "input" + padIndex, "ImpossiblePogo", "cgenius_ImpossiblePogo", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "AutoFire", "cgenius_AutoFire", "true", "false");
            }
        }

        private void ConfigureSonic3airControls(string configFolder, DynamicJson settings)
        {
            if (_emulator != "sonic3air")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!Controllers.Any(c => !c.IsKeyboard))
                return;

            settings["PreferredGamepadPlayer1"] = string.Empty;
            settings["PreferredGamepadPlayer2"] = string.Empty;

            string inputSettingsFile = Path.Combine(configFolder, "settings_input.json");

            var inputJson = DynamicJson.Load(inputSettingsFile);

            var inputDevices = inputJson.GetOrCreateContainer("InputDevices");

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(2))
            {
                string deviceName = controller.Name;
                var device = inputDevices.GetOrCreateContainer(deviceName);
                bool isXinput = controller.IsXInputDevice;

                string[] deviceNames = new string[] { deviceName };
                device.SetObject("DeviceNames", deviceNames);

                string[] up = new string[] { GetSDLInputName(controller, InputKey.up, "sonic3air") };
                string[] down = new string[] { GetSDLInputName(controller, InputKey.down, "sonic3air") };
                string[] left = new string[] { GetSDLInputName(controller, InputKey.left, "sonic3air") };
                string[] right = new string[] { GetSDLInputName(controller, InputKey.right, "sonic3air") };
                string[] a = new string[] { GetSDLInputName(controller, InputKey.a, "sonic3air") };
                string[] b = new string[] { GetSDLInputName(controller, InputKey.b, "sonic3air") };
                string[] x = new string[] { GetSDLInputName(controller, InputKey.y, "sonic3air") };
                string[] y = new string[] { GetSDLInputName(controller, InputKey.x, "sonic3air") };
                string[] start = new string[] { GetSDLInputName(controller, InputKey.start, "sonic3air") };
                string[] back = new string[] { GetSDLInputName(controller, InputKey.select, "sonic3air") };
                string[] l = new string[] { GetSDLInputName(controller, InputKey.pageup, "sonic3air") };
                string[] r = new string[] { GetSDLInputName(controller, InputKey.pagedown, "sonic3air") };
                device.SetObject("Up", up);
                device.SetObject("Down", down);
                device.SetObject("Left", left);
                device.SetObject("Right", right);
                device.SetObject("A", a);
                device.SetObject("B", b);
                device.SetObject("X", x);
                device.SetObject("Y", y);
                device.SetObject("Start", start);
                device.SetObject("Back", back);
                device.SetObject("L", l);
                device.SetObject("R", r);

                if (controller.PlayerIndex == 1)
                    settings["PreferredGamepadPlayer1"] = deviceName;
                if (controller.PlayerIndex == 2)
                    settings["PreferredGamepadPlayer2"] = deviceName;
            }

            inputJson.Save();
        }
        #endregion

        #region general tools
        private static string GetSDLInputName(Controller c, InputKey key, string port = "default")
        {
            Int64 pid;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    if (port == "cgenius")
                        return "B" + pid;
                    else
                        return "Button" + pid;
                }

                else if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1:
                            if (port == "cgenius")
                                return "H1";
                            else
                                return "Pov0";
                        case 2:
                            if (port == "cgenius")
                                return "H2";
                            else
                                return "Pov1";
                        case 4:
                            if (port == "cgenius")
                                return "H4";
                            else
                                return "Pov2";
                        case 8:
                            if (port == "cgenius")
                                return "H8";
                            else
                                return "Pov3";
                    }
                }

                else if (input.Type == "axis")
                {
                    pid = input.Id;
                    if (port == "sonic3air")
                    {
                        switch (pid)
                        {
                            case 0:
                                if (revertAxis) return "Axis1";
                                else return "Axis0";
                            case 1:
                                if (revertAxis) return "Axis3";
                                else return "Axis2";
                            case 2:
                                if (revertAxis) return "Axis5";
                                else return "Axis4";
                            case 3:
                                if (revertAxis) return "Axis7";
                                else return "Axis8";
                            case 4: return "Axis9";
                            case 5: return "Axis11";
                        }
                    }
                    else
                    {
                        if (revertAxis) return "A" + pid + "-";
                        else return "A" + pid + "+";
                    }
                }
            }
            return "";
        }
        #endregion

        #region Dictionaries
        private InputKeyMapping cgeniusMapping = new InputKeyMapping
        {
            { InputKey.select,      "Back" },
            { InputKey.pagedown,    "Camlead" },
            { InputKey.down,        "Down" },
            { InputKey.b,           "Fire" },
            { InputKey.start,       "Help" },
            { InputKey.a,           "Jump" },
            { InputKey.left,        "Left" },
            { InputKey.x,           "Pogo" },
            { InputKey.right,       "Right" },
            { InputKey.y,           "Run" },
            { InputKey.pageup,      "Status" },
            { InputKey.up,          "Up" }
        };
        #endregion
    }
}