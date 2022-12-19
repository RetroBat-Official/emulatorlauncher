using System;
using System.Collections.Generic;
using System.Linq;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class RyujinxGenerator : Generator
    {
        private void CreateControllerConfiguration(DynamicJson json)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            //clear existing input_config section to avoid the same controller mapped to different players because of past mapping
            json.Remove("input_config");

            //create new input_config section
            var input_configs = new List<DynamicJson>();

            //loop controllers
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(json, controller, input_configs);
        }

        /// <summary>
        /// Configure Input routing between gamepad and keyboard
        /// </summary>
        /// <param name="json"></param>
        /// <param name="c"></param>
        /// <param name="input_configs"></param>
        private void ConfigureInput(DynamicJson json, Controller c, List<DynamicJson> input_configs)
        {

            if (c == null || c.Config == null)
                return;

            if (c.IsKeyboard)
                ConfigureKeyboard(json, c.Config, input_configs);
            else
                ConfigureJoystick(json, c, c.PlayerIndex, input_configs);
        }

        /// <summary>
        /// Keyboard
        /// </summary>
        /// <param name="json"></param>
        /// <param name="keyboard"></param>
        /// <param name="input_configs"></param>
        private void ConfigureKeyboard(DynamicJson json, InputConfig keyboard, List<DynamicJson> input_configs)
        {
            if (keyboard == null)
                return;

            //Define action for keyboard mapping
            Action<DynamicJson, string, InputKey> WriteKeyboardMapping = (v, w, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    v[w] = SdlToKeyCode(a.Id).ToString();
                }
                else
                    v[w] = "Unbound";
            };

            var input_config = new DynamicJson();
            var left_joycon_stick = new DynamicJson();
            WriteKeyboardMapping(left_joycon_stick, "stick_up", InputKey.leftanalogup);
            WriteKeyboardMapping(left_joycon_stick, "stick_down", InputKey.leftanalogdown);
            WriteKeyboardMapping(left_joycon_stick, "stick_left", InputKey.leftanalogleft);
            WriteKeyboardMapping(left_joycon_stick, "stick_right", InputKey.leftanalogright);
            WriteKeyboardMapping(left_joycon_stick, "stick_button", InputKey.r3);
            input_config.SetObject("left_joycon_stick", left_joycon_stick);

            var right_joycon_stick = new DynamicJson();
            WriteKeyboardMapping(right_joycon_stick, "stick_up", InputKey.rightanalogup);
            WriteKeyboardMapping(right_joycon_stick, "stick_down", InputKey.rightanalogdown);
            WriteKeyboardMapping(right_joycon_stick, "stick_left", InputKey.rightanalogleft);
            WriteKeyboardMapping(right_joycon_stick, "stick_right", InputKey.rightanalogright);
            WriteKeyboardMapping(right_joycon_stick, "stick_button", InputKey.l3);
            input_config.SetObject("right_joycon_stick", right_joycon_stick);

            var left_joycon = new DynamicJson();
            WriteKeyboardMapping(left_joycon, "button_minus", InputKey.select);
            WriteKeyboardMapping(left_joycon, "button_l", InputKey.pageup);
            WriteKeyboardMapping(left_joycon, "button_zl", InputKey.l2);
            left_joycon["button_sl"] = "Unbound";
            left_joycon["button_sr"] = "Unbound";
            WriteKeyboardMapping(left_joycon, "dpad_up", InputKey.up);
            WriteKeyboardMapping(left_joycon, "dpad_down", InputKey.down);
            WriteKeyboardMapping(left_joycon, "dpad_left", InputKey.left);
            WriteKeyboardMapping(left_joycon, "dpad_right", InputKey.right);
            input_config.SetObject("left_joycon", left_joycon);

            var right_joycon = new DynamicJson();
            WriteKeyboardMapping(right_joycon, "button_plus", InputKey.start);
            WriteKeyboardMapping(right_joycon, "button_r", InputKey.pagedown);
            WriteKeyboardMapping(right_joycon, "button_zr", InputKey.r2);
            right_joycon["button_sl"] = "Unbound";
            right_joycon["button_sr"] = "Unbound";
            WriteKeyboardMapping(right_joycon, "button_x", InputKey.x);
            WriteKeyboardMapping(right_joycon, "button_b", InputKey.b);
            WriteKeyboardMapping(right_joycon, "button_y", InputKey.y);
            WriteKeyboardMapping(right_joycon, "button_a", InputKey.a);
            input_config.SetObject("right_joycon", right_joycon);

            input_config["version"] = "1";
            input_config["backend"] = "WindowKeyboard";
            input_config["id"] = "\"" + "0" + "\"";
            input_config["controller_type"] = "ProController";
            input_config["player_index"] = "Player1";

            input_configs.Add(input_config);
            json.SetObject("input_config", input_configs);
        }

        /// <summary>
        /// Gamepad configuration
        /// </summary>
        /// <param name="json"></param>
        /// <param name="c"></param>
        /// <param name="playerIndex"></param>
        /// <param name="input_configs"></param>
        private void ConfigureJoystick(DynamicJson json, Controller c, int playerIndex, List<DynamicJson> input_configs)
        {
            if (c == null)
                return;

            InputConfig joy = c.Config;
            if (joy == null)
                return;

            //Define tech (SDL or XInput)
            string tech = c.IsXInputDevice ? "XInput" : "SDL";

            //Get SDL controller index
            int index = c.DeviceIndex;
            
            //Build input_config section
            var input_config = new DynamicJson();
            
            //left joycon section
            var left_joycon_stick = new DynamicJson();
            left_joycon_stick["joystick"] = "Left";
            left_joycon_stick["invert_stick_x"] = "false";
            left_joycon_stick["invert_stick_y"] = "false";
            left_joycon_stick["rotate90_cw"] = "false";
            left_joycon_stick["stick_button"] = GetInputKeyName(c, InputKey.l3, tech);
            input_config.SetObject("left_joycon_stick", left_joycon_stick);

            //right joycon section
            var right_joycon_stick = new DynamicJson();
            right_joycon_stick["joystick"] = "Right";
            right_joycon_stick["invert_stick_x"] = "false";
            right_joycon_stick["invert_stick_y"] = "false";
            right_joycon_stick["rotate90_cw"] = "false";
            right_joycon_stick["stick_button"] = GetInputKeyName(c, InputKey.r3, tech);
            input_config.SetObject("right_joycon_stick", right_joycon_stick);

            input_config["deadzone_left"] = "0.1";
            input_config["deadzone_right"] = "0.1";
            input_config["range_left"] = "1";
            input_config["range_right"] = "1";
            input_config["trigger_threshold"] = "0.5";

            //motion - can be enabled in features
            var motion = new DynamicJson();
            motion["motion_backend"] = "GamepadDriver";
            motion["sensitivity"] = "100";
            motion["gyro_deadzone"] = "1";
            
            if (Program.SystemConfig.isOptSet("ryujinx_enable_motion") && Program.SystemConfig.getOptBoolean("ryujinx_enable_motion") && tech != "XInput")
                motion["enable_motion"] = "true";
            else
                motion["enable_motion"] = "false";
            input_config.SetObject("motion", motion);

            //rumble - can be enabled in features
            var rumble = new DynamicJson();
            rumble["strong_rumble"] = "1";
            rumble["weak_rumble"] = "1";

            if (Program.SystemConfig.isOptSet("ryujinx_enable_rumble") && Program.SystemConfig.getOptBoolean("ryujinx_enable_rumble"))
                rumble["enable_rumble"] = "true";
            else
                rumble["enable_rumble"] = "false";

            input_config.SetObject("rumble", rumble);

            //left joycon buttons mapping
            var left_joycon = new DynamicJson();
            left_joycon["button_minus"] = GetInputKeyName(c, InputKey.select, tech);
            left_joycon["button_l"] = GetInputKeyName(c, InputKey.pageup, tech);
            left_joycon["button_zl"] = GetInputKeyName(c, InputKey.l2, tech);
            left_joycon["button_sl"] = "Unbound";
            left_joycon["button_sr"] = "Unbound";
            left_joycon["dpad_up"] = GetInputKeyName(c, InputKey.up, tech);
            left_joycon["dpad_down"] = GetInputKeyName(c, InputKey.down, tech);
            left_joycon["dpad_left"] = GetInputKeyName(c, InputKey.left, tech);
            left_joycon["dpad_right"] = GetInputKeyName(c, InputKey.right, tech);
            input_config.SetObject("left_joycon", left_joycon);

            //right joycon buttons mapping
            var right_joycon = new DynamicJson();
            right_joycon["button_plus"] = GetInputKeyName(c, InputKey.start, tech);
            right_joycon["button_r"] = GetInputKeyName(c, InputKey.pagedown, tech);
            right_joycon["button_zr"] = GetInputKeyName(c, InputKey.r2, tech);
            right_joycon["button_sl"] = "Unbound";
            right_joycon["button_sr"] = "Unbound";
            right_joycon["button_x"] = GetInputKeyName(c, InputKey.x, tech);
            right_joycon["button_b"] = GetInputKeyName(c, InputKey.b, tech);
            right_joycon["button_y"] = GetInputKeyName(c, InputKey.y, tech);
            right_joycon["button_a"] = GetInputKeyName(c, InputKey.a, tech);
            input_config.SetObject("right_joycon", right_joycon);

            //player identification part
            //get guid in system.guid format
            string guid = c.GetSdlGuid(SdlVersion.SDL2_26);
            var newguid = SdlJoystickGuidManager.FromSdlGuidString(guid);

            input_config["version"] = "1";
            input_config["backend"] = "GamepadSDL2";
            input_config["id"] = index + "-" + newguid.ToString();
            input_config["controller_type"] = "ProController";
            input_config["player_index"] = "Player" + playerIndex;

            //add section to file
            input_configs.Add(input_config);
            json.SetObject("input_config", input_configs);
        }

        private static string GetInputKeyName(Controller c, InputKey key, string tech)
        {
            Int64 pid = -1;

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return "B";
                        case 1: return "A";
                        case 2: return "X";
                        case 3: return "Y";
                        case 4: return tech == "XInput" ? "LeftShoulder" : "Minus";
                        case 5: return "RightShoulder";
                        case 6: return tech == "XInput" ? "Minus" : "Plus";
                        case 7: return tech == "XInput" ? "Plus" : "LeftStick";
                        case 8: return tech == "XInput" ? "LeftStick" : "RightStick";
                        case 9: return tech == "XInput" ? "RightStick" : "LeftShoulder";
                        case 10: return "RightShoulder";
                        case 11: return "DPadUp";
                        case 12: return "DPadDown";
                        case 13: return "DPadLeft";
                        case 14: return "DPadRight";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "DPadUp";
                        case 2: return "DPadRight";
                        case 4: return "DPadDown";
                        case 8: return "DPadLeft";
                    }
                }

                //No need to treat all directions from sticks as Ryujinx only needs "Left" and "Right" values
                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 4: return "LeftTrigger";
                        case 5: return "RightTrigger";
                    }
                }
            }
            return "Unbound";
        }

        private static string SdlToKeyCode(long sdlCode)
        {
            
            //The following list of keys has been verified, ryujinx will not allow wrong string so do not add a key until the description has been tested in the emulator first
            switch (sdlCode)
            {
                case 0x0D: return "Enter";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x20: return "Space";
                case 0x2B: return "Plus";
                case 0x2C: return "Comma";
                case 0x2D: return "Minus";
                case 0x2E: return "Period";
                case 0x2F: return "Slash";
                case 0x30: return "Number0";
                case 0x31: return "Number1";
                case 0x32: return "Number2";
                case 0x33: return "Number3";
                case 0x34: return "Number4";
                case 0x35: return "Number5";
                case 0x36: return "Number6";
                case 0x37: return "Number7";
                case 0x38: return "Number8";
                case 0x39: return "Number9";
                case 0x3B: return "Semicolon";
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
                case 0x7F: return "Delete";
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
                case 0x40000047: return "ScrollLock";
                case 0x40000048: return "Pause";
                case 0x40000049: return "Insert";
                case 0x4000004A: return "Home";
                case 0x4000004B: return "PageUp";
                case 0x4000004D: return "End";
                case 0x4000004E: return "PageDown";
                case 0x4000004F: return "Right";
                case 0x40000050: return "Left";
                case 0x40000051: return "Down";
                case 0x40000052: return "Up";
                case 0x40000053: return "NumLock";
                case 0x40000054: return "KeypadDivide";
                case 0x40000055: return "KeypadMultiply*";
                case 0x40000056: return "KeypadSubtract";
                case 0x40000057: return "KeypadAdd";
                case 0x40000058: return "Enter";
                case 0x40000059: return "Keypad1";
                case 0x4000005A: return "Keypad2";
                case 0x4000005B: return "Keypad3";
                case 0x4000005C: return "Keypad4";
                case 0x4000005D: return "Keypad5";
                case 0x4000005E: return "Keypad6";
                case 0x4000005F: return "Keypad7";
                case 0x40000060: return "Keypad8";
                case 0x40000061: return "Keypad9";
                case 0x40000062: return "Keypad0";
                case 0x40000063: return "KeypadDecimal";
                case 0x40000085: return "KeypadDecimal";
                case 0x400000E0: return "ControlLeft";
                case 0x400000E1: return "ShiftLeft";
                case 0x400000E2: return "AltLeft";
                case 0x400000E4: return "ControlRight";
                case 0x400000E5: return "ShiftRight";
            }
            return "Unbound";
        }
    }
}