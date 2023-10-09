using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private static void ConfigureControllers(GameProfile userProfile)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            bool xInput = Program.Controllers.All(c => c.Config != null && c.IsXInputDevice);

            var inputAPI = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Input API");
            if (inputAPI != null)
            {
                if (xInput && inputAPI.FieldOptions != null && inputAPI.FieldOptions.Any(f => f == "XInput"))
                    inputAPI.FieldValue = "XInput";
                else if (inputAPI.FieldOptions != null && inputAPI.FieldOptions.Any(f => f == "DirectInput"))
                    inputAPI.FieldValue = "DirectInput";
            }

            foreach (var c in Program.Controllers)
            {
                if (c.Config == null || c.Config.Type == "key")
                    continue;

                if (xInput)
                {
                    //            foreach (var btn in userProfile.JoystickButtons)
                    //                btn.XInputButton = null;

                    if (userProfile.EmulationProfile == "NamcoMachStorm")
                    {
                        ImportXInputButton(userProfile, c, InputKey.select, InputMapping.Service1);

                        ImportXInputButton(userProfile, c, InputKey.leftanalogup, InputMapping.Analog6);
                        ImportXInputButton(userProfile, c, InputKey.leftanalogleft, InputMapping.Analog4);
                        ImportXInputButton(userProfile, c, InputKey.r2, InputMapping.Analog2);

                        ImportXInputButton(userProfile, c, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp, InputMapping.P1RelativeUp);
                        ImportXInputButton(userProfile, c, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown, InputMapping.P1RelativeDown);

                        ImportXInputButton(userProfile, c, InputKey.a, InputMapping.JvsTwoP1Button1, InputMapping.P1Button1);
                        ImportXInputButton(userProfile, c, InputKey.b, InputMapping.ExtensionOne12);
                        ImportXInputButton(userProfile, c, InputKey.x, InputMapping.ExtensionOne11);
                    }
                    else
                    {
                        if (c.Config[InputKey.leftanalogleft] != null)
                        {
                            if (userProfile.HasAnyXInputButton(InputMapping.Analog0))
                            {
                                ImportXInputButton(userProfile, c, InputKey.left, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft, InputMapping.P1RelativeLeft);
                                ImportXInputButton(userProfile, c, InputKey.right, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight, InputMapping.P1RelativeRight);
                                ImportXInputButton(userProfile, c, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp, InputMapping.P1RelativeUp);
                                ImportXInputButton(userProfile, c, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown, InputMapping.P1RelativeDown);

                                // Wheel
                                ImportXInputButton(userProfile, c, InputKey.leftanalogleft, InputMapping.Analog0);
                                ImportXInputButton(userProfile, c, InputKey.leftanalogup, InputMapping.Analog1);

                                // Gas
                                ImportXInputButton(userProfile, c, InputKey.r2, InputMapping.Analog2);

                                // Brake
                                ImportXInputButton(userProfile, c, InputKey.l2, InputMapping.Analog4);
                            }
                            else
                            {
                                ImportXInputButton(userProfile, c, InputKey.leftanalogleft, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft, InputMapping.P1RelativeLeft);
                                ImportXInputButton(userProfile, c, InputKey.leftanalogright, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight, InputMapping.P1RelativeRight);
                                ImportXInputButton(userProfile, c, InputKey.leftanalogup, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp, InputMapping.P1RelativeUp);
                                ImportXInputButton(userProfile, c, InputKey.leftanalogdown, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown, InputMapping.P1RelativeDown);
                            }
                        }
                        else
                        {
                            ImportXInputButton(userProfile, c, InputKey.left, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft, InputMapping.P1RelativeLeft);
                            ImportXInputButton(userProfile, c, InputKey.right, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight, InputMapping.P1RelativeRight);
                            ImportXInputButton(userProfile, c, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp, InputMapping.P1RelativeUp);
                            ImportXInputButton(userProfile, c, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown, InputMapping.P1RelativeDown);
                        }

                        if (userProfile.HasAnyXInputButton(InputMapping.JvsTwoP1ButtonStart, InputMapping.P1ButtonStart, InputMapping.JvsTwoCoin1, InputMapping.Coin1))
                        {
                            ImportXInputButton(userProfile, c, InputKey.start, InputMapping.JvsTwoP1ButtonStart, InputMapping.P1ButtonStart);
                            ImportXInputButton(userProfile, c, InputKey.select, InputMapping.JvsTwoCoin1, InputMapping.Coin1);
                        }
                        else
                            ImportXInputButton(userProfile, c, InputKey.start, InputMapping.Service1, InputMapping.JvsTwoService1);

                        ImportXInputButton(userProfile, c, InputKey.a, InputMapping.JvsTwoP1Button1, InputMapping.P1Button1);
                        ImportXInputButton(userProfile, c, InputKey.b, InputMapping.JvsTwoP1Button2, InputMapping.P1Button2);
                        ImportXInputButton(userProfile, c, InputKey.x, InputMapping.JvsTwoP1Button3, InputMapping.P1Button3);
                        ImportXInputButton(userProfile, c, InputKey.y, InputMapping.JvsTwoP1Button4, InputMapping.P1Button4);

                        ImportXInputButton(userProfile, c, InputKey.pageup, InputMapping.JvsTwoP1Button5, InputMapping.P1Button5);
                        ImportXInputButton(userProfile, c, InputKey.pagedown, InputMapping.JvsTwoP1Button6, InputMapping.P1Button6);

                        if (userProfile.HasAnyXInputButton(InputMapping.ExtensionOne2) && !userProfile.HasAnyXInputButton(InputMapping.P1Button2))
                            ImportXInputButton(userProfile, c, InputKey.b, InputMapping.ExtensionOne2);
                        else
                            ImportXInputButton(userProfile, c, InputKey.b, InputMapping.JvsTwoP1Button2, InputMapping.P1Button2);
                    }
                }
                else
                {
                    //         foreach (var btn in userProfile.JoystickButtons)
                    //              btn.DirectInputButton = null;

                    if (c.Config[InputKey.leftanalogleft] != null)
                    {
                        ImportDirectInputButton(userProfile, c, InputKey.leftanalogup, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp, InputMapping.P1RelativeUp);
                        ImportDirectInputButton(userProfile, c, InputKey.leftanalogleft, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft, InputMapping.P1RelativeLeft);
                        ImportDirectInputButton(userProfile, c, InputKey.leftanalogdown, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown, InputMapping.P1RelativeDown);
                        ImportDirectInputButton(userProfile, c, InputKey.leftanalogright, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight, InputMapping.P1RelativeRight);
                    }
                    else
                    {
                        ImportDirectInputButton(userProfile, c, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp, InputMapping.P1RelativeUp);
                        ImportDirectInputButton(userProfile, c, InputKey.left, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft, InputMapping.P1RelativeLeft);
                        ImportDirectInputButton(userProfile, c, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown, InputMapping.P1RelativeDown);
                        ImportDirectInputButton(userProfile, c, InputKey.right, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight, InputMapping.P1RelativeRight);
                    }

                    ImportDirectInputButton(userProfile, c, InputKey.select, InputMapping.JvsTwoCoin1, InputMapping.Coin1);
                    ImportDirectInputButton(userProfile, c, InputKey.start, InputMapping.JvsTwoP1ButtonStart, InputMapping.P1ButtonStart);
                    ImportDirectInputButton(userProfile, c, InputKey.a, InputMapping.JvsTwoP1Button1, InputMapping.P1Button1);
                    ImportDirectInputButton(userProfile, c, InputKey.b, InputMapping.JvsTwoP1Button2, InputMapping.P1Button2);
                    ImportDirectInputButton(userProfile, c, InputKey.x, InputMapping.JvsTwoP1Button3, InputMapping.P1Button3);
                    ImportDirectInputButton(userProfile, c, InputKey.y, InputMapping.JvsTwoP1Button4, InputMapping.P1Button4);
                    ImportDirectInputButton(userProfile, c, InputKey.l3, InputMapping.JvsTwoP1Button5, InputMapping.P1Button5);
                    ImportDirectInputButton(userProfile, c, InputKey.r3, InputMapping.JvsTwoP1Button6, InputMapping.P1Button6);

                }

                break;
            }
        }

        private static void ImportXInputButton(GameProfile userProfile, Controller ctl, InputKey key, params InputMapping[] mapping)
        {
            InputConfig c = ctl.Config;

            var start = userProfile.JoystickButtons.FirstOrDefault(j => !j.HideWithXInput && mapping.Contains(j.InputMapping));

            bool reverseAxis = false;

            if (c[key] == null && key == InputKey.leftanalogdown)
            {
                reverseAxis = true;
                key = InputKey.leftanalogup;
            }
            if (c[key] == null && key == InputKey.leftanalogright)
            {
                reverseAxis = true;
                key = InputKey.leftanalogleft;
            }


            if (start != null && c[key] != null)
            {
                start.XInputButton = new XInputButton();

                if (c[key].Type == "axis")
                {
                    start.XInputButton.IsLeftThumbX = false;
                    start.XInputButton.IsRightThumbX = false;
                    start.XInputButton.IsLeftThumbY = false;
                    start.XInputButton.IsRightThumbY = false;
                    start.XInputButton.IsAxisMinus = false;
                    start.XInputButton.IsLeftTrigger = false;
                    start.XInputButton.IsRightTrigger = false;
                    start.XInputButton.XInputIndex = 0;
                    start.XInputButton.ButtonIndex = 0;
                    start.XInputButton.IsButton = false;
                    start.XInputButton.ButtonCode = 0;

                    start.BindNameXi = "Input Device 0 " + ctl.GetXInputMapping(key, reverseAxis).ToString();
                    start.BindName = "Input Device 0 " + ctl.GetXInputMapping(key, reverseAxis).ToString();

                    switch (ctl.GetXInputMapping(key, reverseAxis))
                    {
                        case XINPUTMAPPING.LEFTANALOG_LEFT:
                            start.XInputButton.IsLeftThumbX = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.LEFTANALOG_RIGHT:
                            start.XInputButton.IsLeftThumbX = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.LEFTANALOG_UP:
                            start.XInputButton.IsLeftThumbY = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.LEFTANALOG_DOWN:
                            start.XInputButton.IsLeftThumbY = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_LEFT:
                            start.XInputButton.IsRightThumbX = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_RIGHT:
                            start.XInputButton.IsRightThumbX = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_UP:
                            start.XInputButton.IsRightThumbY = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_DOWN:
                            start.XInputButton.IsRightThumbY = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.LEFTTRIGGER:
                            start.XInputButton.IsLeftTrigger = true;
                            break;
                        case XINPUTMAPPING.RIGHTTRIGGER:
                            start.XInputButton.IsRightTrigger = true;
                            break;

                    }
                }
                else if (c[key].Type == "button" || c[key].Type == "hat")
                {
                    var button = ctl.GetXInputButtonFlags(key);
                    if (button != XInputButtonFlags.NONE)
                    {
                        start.XInputButton.IsLeftThumbX = false;
                        start.XInputButton.IsRightThumbX = false;
                        start.XInputButton.IsLeftThumbY = false;
                        start.XInputButton.IsRightThumbY = false;
                        start.XInputButton.IsAxisMinus = false;
                        start.XInputButton.IsLeftTrigger = false;
                        start.XInputButton.IsRightTrigger = false;
                        start.XInputButton.XInputIndex = 0;
                        start.XInputButton.ButtonIndex = 0;
                        start.XInputButton.IsButton = true;
                        start.XInputButton.ButtonCode = (short)button;

                        start.BindNameXi = "Input Device 0 " + ctl.GetXInputMapping(key).ToString();
                        start.BindName = "Input Device 0 " + ctl.GetXInputMapping(key).ToString();

                    }
                }
            }
        }

        private static void ImportDirectInputButton(GameProfile userProfile, Controller ctrl, InputKey key, params InputMapping[] mapping)
        {
            var info = ctrl.DirectInput;
            if (info == null)
                return;

            var start = userProfile.JoystickButtons.FirstOrDefault(j => !j.HideWithDirectInput && mapping.Contains(j.InputMapping));
            if (start == null)
                return;

            bool reverseAxis;
            key = key.GetRevertedAxis(out reverseAxis);

            var input = ctrl.GetDirectInputMapping(key);
            if (input != null)
            {
                start.DirectInputButton = new JoystickButton();
                start.DirectInputButton.JoystickGuid = info.InstanceGuid;
                start.DirectInputButton.IsAxis = false;
                start.DirectInputButton.IsAxisMinus = false;
                start.DirectInputButton.IsFullAxis = false;
                start.DirectInputButton.IsReverseAxis = false;
                start.DirectInputButton.PovDirection = 0;
                start.DirectInputButton.IsReverseAxis = false;
                start.DirectInputButton.Button = 0;

                if (input.Type == "button")
                    start.DirectInputButton.Button = (int)input.Id + 48;
                else if (input.Type == "hat")
                {
                    start.DirectInputButton.Button = 32;
                    if (input.Value == 1) // Top
                        start.DirectInputButton.PovDirection = 0;
                    else if (input.Value == 4) // Down
                        start.DirectInputButton.PovDirection = 18000;
                    else if (input.Value == 8) // Left
                        start.DirectInputButton.PovDirection = 27000;
                    else if (input.Value == 2) // Right
                        start.DirectInputButton.PovDirection = 9000;
                }
                else if (input.Type == "axis")
                {
                    start.DirectInputButton.Button = (int)input.Id * 4;
                    start.DirectInputButton.IsAxis = true;
                    start.DirectInputButton.IsAxisMinus = reverseAxis ? input.Value > 0 : input.Value < 0;
                }
            }
        }
    }

    static class Exts
    {
        public static bool HasAnyXInputButton(this GameProfile pthi, params InputMapping[] lists)
        {
            return pthi.JoystickButtons.Any(j => !j.HideWithXInput && lists.Contains(j.InputMapping));
        }
    }
}
