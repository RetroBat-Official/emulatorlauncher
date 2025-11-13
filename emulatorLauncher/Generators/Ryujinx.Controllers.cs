using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using Newtonsoft.Json.Linq;

namespace EmulatorLauncher
{
    partial class RyujinxGenerator : Generator
    {
        private List<Sdl3GameController> _sdl3Controllers = new List<Sdl3GameController>();

        /// <summary>
        /// cf. https://github.com/Ryujinx/Ryujinx/blob/master/src/Ryujinx.SDL2.Common/SDL2Driver.cs#L56
        /// </summary>
        private void UpdateSdlControllersWithHints()
        {
            string dllPath = Path.Combine(_emulatorPath, "SDL2.dll");
            _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(dllPath);

            if (Program.Controllers.Count(c => !c.IsKeyboard) == 0)
                return;

            var hints = new List<string>
            {
                "SDL_HINT_JOYSTICK_HIDAPI_SWITCH_HOME_LED = 0",
                "SDL_HINT_JOYSTICK_HIDAPI_JOY_CONS = 1",
                "SDL_HINT_JOYSTICK_HIDAPI_COMBINE_JOY_CONS = 1"
            };

            if (SystemConfig.getOptBoolean("ps_controller_enhanced"))
            {
                hints.Add("SDL_HINT_JOYSTICK_HIDAPI_PS4_RUMBLE = 1");
                hints.Add("SDL_HINT_JOYSTICK_HIDAPI_PS5_RUMBLE = 1");
            }

            _sdlMapping = SdlDllControllersMapping.FromDll(dllPath, string.Join(",", hints));
            if (_sdlMapping == null)
            {
                SdlGameController.ReloadWithHints(string.Join(",", hints));
                Program.Controllers.ForEach(c => c.ResetSdlController());
            }
        }

        private SdlDllControllersMapping _sdlMapping;

        private void CreateControllerConfiguration(dynamic json)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Ryujinx");

            UpdateSdlControllersWithHints();

            // Check SDL3 dll Get list of SDL3 controllers
            bool sdl3 = Controller.CheckSDL3dll();

            if (sdl3 && Sdl3GameController.ListJoysticks(out List<Sdl3GameController> Sdl3Controllers))
                _sdl3Controllers = Sdl3Controllers;

            //clear existing input_config section to avoid the same controller mapped to different players because of past mapping
            json.input_config = new Newtonsoft.Json.Linq.JArray();

            //create new input_config section
            List<object> input_configs = new List<object>();

            int maxPad = 8;
            if (SystemConfig.isOptSet("ryujinx_maxcontrollers") && !string.IsNullOrEmpty(SystemConfig["ryujinx_maxcontrollers"]))
                maxPad = SystemConfig["ryujinx_maxcontrollers"].ToInteger();

            //loop controllers
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(json, controller, input_configs);

            json.input_config = JArray.FromObject(input_configs);
        }

        /// <summary>
        /// Configure Input routing between gamepad and keyboard
        /// </summary>
        /// <param name="json"></param>
        /// <param name="c"></param>
        /// <param name="input_configs"></param>
        private void ConfigureInput(dynamic json, Controller c, List<object> input_configs)
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
        private void ConfigureKeyboard(dynamic json, InputConfig keyboard, List<object> input_configs)
        {
            if (keyboard == null)
                return;

            string playerType = "ProController";

            if (SystemConfig.isOptSet("ryujinx_padtype1") && !string.IsNullOrEmpty(SystemConfig["ryujinx_padtype1"]))
                playerType = SystemConfig["ryujinx_padtype1"];

            bool handheld = playerType == "Handheld";

            var newInputConfig = new Dictionary<string, object>();

            newInputConfig["left_joycon_stick"] = new
            {
                stick_up = WriteKeyboardMapping(keyboard, InputKey.leftanalogup),
                stick_down = WriteKeyboardMapping(keyboard, InputKey.leftanalogdown),
                stick_left = WriteKeyboardMapping(keyboard, InputKey.leftanalogleft),
                stick_right = WriteKeyboardMapping(keyboard, InputKey.leftanalogright),
                stick_button = WriteKeyboardMapping(keyboard, InputKey.r3),
            };

            newInputConfig["right_joycon_stick"] = new
            {
                stick_up = WriteKeyboardMapping(keyboard, InputKey.rightanalogup),
                stick_down = WriteKeyboardMapping(keyboard, InputKey.rightanalogdown),
                stick_left = WriteKeyboardMapping(keyboard, InputKey.rightanalogleft),
                stick_right = WriteKeyboardMapping(keyboard, InputKey.rightanalogright),
                stick_button = WriteKeyboardMapping(keyboard, InputKey.l3),
            };

            if (playerType == "JoyconLeft")
            {
                newInputConfig["left_joycon"] = new
                {
                    button_minus = WriteKeyboardMapping(keyboard, InputKey.select),
                    button_l = "Unbound",
                    button_zl = "Unbound",
                    button_sl = WriteKeyboardMapping(keyboard, InputKey.pageup),
                    button_sr = WriteKeyboardMapping(keyboard, InputKey.pagedown),
                    dpad_up = WriteKeyboardMapping(keyboard, InputKey.up),
                    dpad_down = WriteKeyboardMapping(keyboard, InputKey.down),
                    dpad_left = WriteKeyboardMapping(keyboard, InputKey.left),
                    dpad_right = WriteKeyboardMapping(keyboard, InputKey.right),
                };
            }

            else
            {
                newInputConfig["left_joycon"] = new
                {
                    button_minus = WriteKeyboardMapping(keyboard, InputKey.select),
                    button_l = WriteKeyboardMapping(keyboard, InputKey.pageup),
                    button_zl = WriteKeyboardMapping(keyboard, InputKey.l2),
                    button_sl = "Unbound",
                    button_sr = "Unbound",
                    dpad_up = WriteKeyboardMapping(keyboard, InputKey.up),
                    dpad_down = WriteKeyboardMapping(keyboard, InputKey.down),
                    dpad_left = WriteKeyboardMapping(keyboard, InputKey.left),
                    dpad_right = WriteKeyboardMapping(keyboard, InputKey.right),
                };
            }

            if (playerType == "JoyconRight")
            {
                newInputConfig["right_joycon"] = new
                {
                    button_plus = WriteKeyboardMapping(keyboard, InputKey.start),
                    button_r = "Unbound",
                    button_zr = "Unbound",
                    button_sl = WriteKeyboardMapping(keyboard, InputKey.pageup),
                    button_sr = WriteKeyboardMapping(keyboard, InputKey.pagedown),
                    button_x = WriteKeyboardMapping(keyboard, InputKey.x),
                    button_b = WriteKeyboardMapping(keyboard, InputKey.b),
                    button_y = WriteKeyboardMapping(keyboard, InputKey.y),
                    button_a = WriteKeyboardMapping(keyboard, InputKey.a),
                };
            }

            else
            {
                newInputConfig["right_joycon"] = new
                {
                    button_plus = WriteKeyboardMapping(keyboard, InputKey.start),
                    button_r = WriteKeyboardMapping(keyboard, InputKey.pagedown),
                    button_zr = WriteKeyboardMapping(keyboard, InputKey.r2),
                    button_sl = "Unbound",
                    button_sr = "Unbound",
                    button_x = WriteKeyboardMapping(keyboard, InputKey.x),
                    button_b = WriteKeyboardMapping(keyboard, InputKey.b),
                    button_y = WriteKeyboardMapping(keyboard, InputKey.y),
                    button_a = WriteKeyboardMapping(keyboard, InputKey.a),
                };
            }

            newInputConfig["version"] = 1;
            newInputConfig["backend"] = "WindowKeyboard";
            newInputConfig["id"] = "0";
            newInputConfig["controller_type"] = playerType;
            newInputConfig["player_index"] = handheld ? "Handheld" : "Player1";

            input_configs.Add(Newtonsoft.Json.Linq.JObject.FromObject(newInputConfig));
        }

        /// <summary>
        /// Gamepad configuration
        /// </summary>
        /// <param name="json"></param>
        /// <param name="c"></param>
        /// <param name="playerIndex"></param>
        /// <param name="input_configs"></param>
        private void ConfigureJoystick(dynamic json, Controller c, int playerIndex, List<object> input_configs)
        {
            if (c == null)
                return;

            InputConfig joy = c.Config;
            if (joy == null)
                return;

            string playerType = "ProController";
            string padType = "ryujinx_padtype" + playerIndex.ToString();

            if (SystemConfig.isOptSet(padType) && !string.IsNullOrEmpty(SystemConfig[padType]))
                playerType = SystemConfig[padType];

            bool handheld = playerType == "Handheld";

            // Define tech (SDL or XInput)
            string tech = c.IsXInputDevice ? "XInput" : "SDL";

            // Get controller index (index is equal to 0 and ++ for each repeated guid)
            int index = 0;
            var same_pad = this.Controllers.Where(i => i.Config != null && i.Guid == c.Guid && !i.IsKeyboard).OrderBy(j => j.DeviceIndex).ToList();
            if (same_pad.Count > 1)
                index = same_pad.IndexOf(c);

            //Build input_config section
            var newInputConfig = new Dictionary<string, object>();

            //left joycon section
            newInputConfig["left_joycon_stick"] = new
            {
                joystick = "Left",
                invert_stick_x = false,
                invert_stick_y = false,
                rotate90_cw = false,
                stick_button = GetInputKeyName(c, InputKey.l3, tech),
            };

            //right joycon section
            newInputConfig["right_joycon_stick"] = new
            {
                joystick = "Right",
                invert_stick_x = false,
                invert_stick_y = false,
                rotate90_cw = false,
                stick_button = GetInputKeyName(c, InputKey.r3, tech),
            };

            newInputConfig["deadzone_left"] = 0.1;
            newInputConfig["deadzone_right"] = 0.1;
            newInputConfig["range_left"] = 1;
            newInputConfig["range_right"] = 1;
            newInputConfig["trigger_threshold"] = 0.5;

            //motion - can be enabled in features
            if (Program.SystemConfig.getOptBoolean("ryujinx_enable_motion") && tech != "XInput")
            {
                newInputConfig["motion"] = new
                {
                    motion_backend = "GamepadDriver",
                    sensitivity = 100,
                    gyro_deadzone = 1,
                    enable_motion = true,
                };
            }
            else
            {
                newInputConfig["motion"] = new
                {
                    motion_backend = "GamepadDriver",
                    sensitivity = 100,
                    gyro_deadzone = 1,
                    enable_motion = false,
                };
            }

            if (Program.SystemConfig.getOptBoolean("ryujinx_enable_rumble"))
            {
                newInputConfig["rumble"] = new
                {
                    strong_rumble = 1,
                    weak_rumble = 1,
                    enable_rumble = true,
                };
            }
            else
            {
                newInputConfig["rumble"] = new
                {
                    strong_rumble = 1,
                    weak_rumble = 1,
                    enable_rumble = false,
                };
            }

            //leds
            newInputConfig["led"] = new
            {
                enable_led = false,
                turn_off_led = false,
                use_rainbow = false,
                led_color = 0,
            };

            //left joycon buttons mapping
            if (playerType == "JoyconLeft")
            {
                newInputConfig["left_joycon"] = new
                {
                    button_minus = GetInputKeyName(c, InputKey.select, tech),
                    button_l = "Unbound",
                    button_zl = "Unbound",
                    button_sl = GetInputKeyName(c, InputKey.pageup, tech),
                    button_sr = GetInputKeyName(c, InputKey.pagedown, tech),
                    dpad_up = GetInputKeyName(c, InputKey.up, tech),
                    dpad_down = GetInputKeyName(c, InputKey.down, tech),
                    dpad_left = GetInputKeyName(c, InputKey.left, tech),
                    dpad_right = GetInputKeyName(c, InputKey.right, tech),
                };
            }
            else
            {
                newInputConfig["left_joycon"] = new
                {
                    button_minus = GetInputKeyName(c, InputKey.select, tech),
                    button_l = GetInputKeyName(c, InputKey.pageup, tech),
                    button_zl = GetInputKeyName(c, InputKey.l2, tech),
                    button_sl = "Unbound",
                    button_sr = "Unbound",
                    dpad_up = GetInputKeyName(c, InputKey.up, tech),
                    dpad_down = GetInputKeyName(c, InputKey.down, tech),
                    dpad_left = GetInputKeyName(c, InputKey.left, tech),
                    dpad_right = GetInputKeyName(c, InputKey.right, tech),
                };
            }

            //right joycon buttons mapping
            Dictionary<string, object> right_joycon = new Dictionary<string, object>();
            right_joycon["button_plus"] = GetInputKeyName(c, InputKey.start, tech);

            if (playerType == "JoyconRight")
            {
                right_joycon["button_r"] = "Unbound";
                right_joycon["button_zr"] = "Unbound";
                right_joycon["button_sl"] = GetInputKeyName(c, InputKey.pageup, tech);
                right_joycon["button_sr"] = GetInputKeyName(c, InputKey.pagedown, tech);
            }
            else
            {
                right_joycon["button_r"] = GetInputKeyName(c, InputKey.pagedown, tech);
                right_joycon["button_zr"] = GetInputKeyName(c, InputKey.r2, tech);
                right_joycon["button_sl"] = "Unbound";
                right_joycon["button_sr"] = "Unbound";
            }

            // Invert button positions for XBOX controllers
            if (c.IsXInputDevice && Program.SystemConfig.getOptBoolean("ryujinx_gamepadbuttons"))
            {
                right_joycon["button_x"] = _sdl3 ? GetInputKeyName(c, InputKey.x, tech) : GetInputKeyName(c, InputKey.y, tech);
                right_joycon["button_b"] = _sdl3 ? GetInputKeyName(c, InputKey.b, tech) : GetInputKeyName(c, InputKey.a, tech);
                right_joycon["button_y"] = _sdl3 ? GetInputKeyName(c, InputKey.y, tech) : GetInputKeyName(c, InputKey.x, tech);
                right_joycon["button_a"] = _sdl3 ? GetInputKeyName(c, InputKey.a, tech) : GetInputKeyName(c, InputKey.b, tech);
            }
            else if (_sdl3 && c.VendorID != USB_VENDOR.NINTENDO)
            {
                right_joycon["button_x"] = GetInputKeyName(c, InputKey.y, tech);
                right_joycon["button_b"] = GetInputKeyName(c, InputKey.a, tech);
                right_joycon["button_y"] = GetInputKeyName(c, InputKey.x, tech);
                right_joycon["button_a"] = GetInputKeyName(c, InputKey.b, tech);
            }
            else
            {
                right_joycon["button_x"] = GetInputKeyName(c, InputKey.x, tech);
                right_joycon["button_b"] = GetInputKeyName(c, InputKey.b, tech);
                right_joycon["button_y"] = GetInputKeyName(c, InputKey.y, tech);
                right_joycon["button_a"] = GetInputKeyName(c, InputKey.a, tech);
            }

            newInputConfig["right_joycon"] = JObject.FromObject(right_joycon);

            // Player identification part
            // Get guid in system.guid format
            /*string guid = (_sdlVersion == SdlVersion.Unknown && c.SdlController.Guid != null) ? c.SdlController.Guid.ToString() : c.GetSdlGuid(_sdlVersion, true);

            if (_sdlMapping != null)
            {
                var sdlTrueGuid = _sdlMapping.GetControllerGuid(c.DevicePath);
                if (sdlTrueGuid != null)
                    guid = sdlTrueGuid.ToString();
            }*/

            string guid = c.Guid.ToString();
            if (SystemConfig.isOptSet("ryujinx_sdlguid") && SystemConfig.getOptBoolean("ryujinx_sdlguid"))
                guid = c.SdlController.Guid.ToString();

            if (guid == null)
            {
                SimpleLogger.Instance.Error("[ERROR] Controller " + c.DevicePath + " unable to get GUID.");
                return;
            }

            Sdl3GameController sdl3Controller = null;
            if (_sdl3 && _sdl3Controllers.Count > 0)
            {
                string cPath = c.DirectInput.DevicePath;
                
                if (c.IsXInputDevice)
                {
                    cPath = "xinput#" + c.XInput.DeviceIndex.ToString();
                    sdl3Controller = _sdl3Controllers.FirstOrDefault(j => j.Path.ToLowerInvariant() == cPath);
                }
                else
                {
                    sdl3Controller = _sdl3Controllers.FirstOrDefault(j => j.Path.ToLowerInvariant() == c.DirectInput.DevicePath);
                }
            }

            var newGuid = SdlJoystickGuidManager.FromSdlGuidString(guid);

            if (sdl3Controller != null && sdl3Controller.GuidString != null)
            {
                guid = sdl3Controller.GuidString;
                newGuid = SdlJoystickGuidManager.FromSdlGuidStringryujinx(guid);
            }

            string ryuGuidString = newGuid.ToString();

            string overrideGuidPath = Path.Combine(AppConfig.GetFullPath("tools"), "controllerinfo.yml");
            string overrideGuid = SdlJoystickGuid.GetGuidFromFile(overrideGuidPath, c.SdlController, c.Guid, "ryujinx");
            if (overrideGuid != null)
            {
                SimpleLogger.Instance.Info("[INFO] Controller GUID replaced from yml file : " + overrideGuid);
                ryuGuidString = overrideGuid;
            }

            newInputConfig["version"] = 1;
            newInputConfig["backend"] = _sdl3 ? "GamepadSDL3" : "GamepadSDL2";
            newInputConfig["id"] = (index + "-" + ryuGuidString).ToString();
            newInputConfig["controller_type"] = playerType;
            newInputConfig["player_index"] = handheld ? "Handheld" : "Player" + playerIndex;

            //add section to file
            input_configs.Add(Newtonsoft.Json.Linq.JObject.FromObject(newInputConfig));

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + c.DevicePath + " to player : " + c.PlayerIndex.ToString());
        }

        private static string GetInputKeyName(Controller c, InputKey key, string tech)
        {
            Int64 pid;

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
                        case 5: return tech == "SDL" ? "Guide" : "RightShoulder";
                        case 6: return tech == "XInput" ? "Minus" : "Plus";
                        case 7: return tech == "XInput" ? "Plus" : "LeftStick";
                        case 8: return tech == "XInput" ? "LeftStick" : "RightStick";
                        case 9: return tech == "XInput" ? "RightStick" : "LeftShoulder";
                        case 10: return tech == "XInput" ? "Guide" : "RightShoulder";
                        case 11: return "DpadUp";
                        case 12: return "DpadDown";
                        case 13: return "DpadLeft";
                        case 14: return "DpadRight";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "DpadUp";
                        case 2: return "DpadRight";
                        case 4: return "DpadDown";
                        case 8: return "DpadLeft";
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

        private static string WriteKeyboardMapping(InputConfig keyboard, InputKey k)
        {
            var a = keyboard[k];
            if (a != null)
            {
                return SdlToKeyCode(a.Id).ToString();
            }
            else
                return "Unbound";
        }

        private static string SdlToKeyCode(long sdlCode)
        {
            
            //The following list of keys has been verified, ryujinx will not allow wrong string so do not add a key until the description has been tested in the emulator first
            switch (sdlCode)
            {
                case 0x0D: return "Enter";
                case 0x08: return "BackSpace";
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
                case 0x40000055: return "KeypadMultiply";
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