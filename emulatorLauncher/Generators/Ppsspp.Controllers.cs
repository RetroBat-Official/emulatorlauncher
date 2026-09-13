using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Globalization;
using EmulatorLauncher.Common.Joysticks;
using System;

namespace EmulatorLauncher
{
    partial class PpssppGenerator
    {
        private enum PpssppPadType
        {
            XInput,             // DEVICE_ID_XINPUT_0 + slot, handled by Windows/XinputDevice.cpp
            HidPlayStation,     // DEVICE_ID_PAD_0, handled by Windows/Hid/HidInputDevice.cpp
            HidSwitch,          // DEVICE_ID_PAD_0, same layer, digital triggers
            DirectInput         // DEVICE_ID_PAD_0 + index, handled by Windows/DinputDevice.cpp
        }

        private const int PPSSPP_DEVICE_ID_KEYBOARD = 1;
        private const int PPSSPP_DEVICE_ID_PAD_0 = 10;
        private const int PPSSPP_DEVICE_ID_XINPUT_0 = 20;
        private const int PPSSPP_MAX_PADS = 4;

        private void CreateControllerConfiguration(string memPath)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[CONTROLS] Creating controller configuration for PPSSPP");

            string iniFile = Path.Combine(memPath, "SYSTEM", "controls.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    ini.ClearSection("ControlMapping");

                    // PPSSPP accepts several comma separated bindings per entry, so every binding is
                    // collected here first. Each pad then contributes its own device id and its own
                    // keycodes, instead of one hardcoded set being reused for all of them.
                    var mappings = new Dictionary<string, List<string>>();

                    var keyboard = Program.Controllers.FirstOrDefault(c => c.IsKeyboard && c.Config != null);
                    if (keyboard != null)
                        AddKeyboardMappings(mappings, keyboard.Config);

                    AddKeyboardHotkeys(mappings);

                    var usedDeviceIds = new List<int>();

                    var pads = Program.Controllers
                        .Where(c => !c.IsKeyboard && c.Config != null)
                        .OrderBy(c => c.PlayerIndex)
                        .Take(PPSSPP_MAX_PADS);

                    foreach (var pad in pads)
                        AddPadMappings(mappings, pad, usedDeviceIds);

                    foreach (var mapping in mappings)
                        ini.WriteValue("ControlMapping", mapping.Key, string.Join(",", mapping.Value.ToArray()));
                }
            }
            catch { }
        }

        private static void AddMapping(Dictionary<string, List<string>> mappings, string pspButton, string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return;

            List<string> bindings;
            if (!mappings.TryGetValue(pspButton, out bindings))
            {
                bindings = new List<string>();
                mappings[pspButton] = bindings;
            }

            if (!bindings.Contains(binding))
                bindings.Add(binding);
        }

        private void AddPadMappings(Dictionary<string, List<string>> mappings, Controller controller, List<int> usedDeviceIds)
        {
            PpssppPadType padType;
            int deviceId = GetPpssppDeviceId(controller, out padType);

            if (deviceId < 0)
            {
                SimpleLogger.Instance.Warning("[WARNING] Cannot resolve a PPSSPP device index for " + controller.DevicePath + ", controller skipped.");
                return;
            }

            // Manual override, kept as an escape hatch. It only applies to player 1.
            if (controller.PlayerIndex == 1 && SystemConfig.isOptSet("ppsspp_forceindex") && !string.IsNullOrEmpty(SystemConfig["ppsspp_forceindex"]))
            {
                int baseId = (padType == PpssppPadType.XInput) ? PPSSPP_DEVICE_ID_XINPUT_0 : PPSSPP_DEVICE_ID_PAD_0;
                deviceId = baseId + SystemConfig["ppsspp_forceindex"].ToInteger();
            }

            // A HID pad and the first DirectInput pad both report as DEVICE_ID_PAD_0 in PPSSPP and
            // cannot be told apart in controls.ini. The lowest player index keeps the slot.
            if (usedDeviceIds.Contains(deviceId))
            {
                SimpleLogger.Instance.Warning("[WARNING] PPSSPP device id " + deviceId + " is already assigned, player " + controller.PlayerIndex + " skipped.");
                return;
            }

            usedDeviceIds.Add(deviceId);

            SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + " : " + controller.DevicePath + " -> PPSSPP device " + deviceId + " (" + padType + ")");

            string prefix = deviceId.ToString() + "-";

            foreach (var entry in pspMapping)
            {
                string code = GetPadKeyCode(controller, padType, entry.Value);
                if (string.IsNullOrEmpty(code))
                    continue;

                AddMapping(mappings, entry.Key, prefix + code);
            }

            AddPadHotkeys(mappings, controller, padType, prefix);
        }

        private void AddPadHotkeys(Dictionary<string, List<string>> mappings, Controller controller, PpssppPadType padType, string prefix)
        {
            // Every combo is built on SELECT. Without it there is no safe modifier to use.
            string selectCode = GetPadKeyCode(controller, padType, InputKey.select);
            if (string.IsNullOrEmpty(selectCode))
            {
                SimpleLogger.Instance.Warning("[WARNING] No SELECT button found for player " + controller.PlayerIndex + ", pad hotkeys skipped.");
                return;
            }

            string modifier = prefix + selectCode + ":";

            var hotkeys = new Dictionary<string, InputKey>();

            Dictionary<string, string> padHKDic;
            if (Hotkeys.GetPadHKFromFile("ppsspp", "", out padHKDic) && padHKDic.Count > 0)
            {
                foreach (var hotkey in padHKDic)
                {
                    InputKey key;
                    if (hotkeyInputNames.TryGetValue(hotkey.Value, out key))
                        hotkeys[hotkey.Key] = key;
                }
            }
            else
                hotkeys = defaultPadHotkeys;

            foreach (var hotkey in hotkeys)
            {
                string code = GetPadKeyCode(controller, padType, hotkey.Value);
                if (string.IsNullOrEmpty(code))
                    continue;

                AddMapping(mappings, hotkey.Key, modifier + prefix + code);
            }
        }

        /// <summary>
        /// Resolves the device id PPSSPP will use for this controller on Windows.
        /// Returns -1 when it cannot be determined.
        /// </summary>
        private static int GetPpssppDeviceId(Controller controller, out PpssppPadType padType)
        {
            padType = PpssppPadType.DirectInput;

            // XInput pads are polled by XinputDevice on their own slot, this one is exact.
            if (controller.IsXInputDevice && controller.XInput != null)
            {
                padType = PpssppPadType.XInput;
                return PPSSPP_DEVICE_ID_XINPUT_0 + controller.XInput.DeviceIndex;
            }

            int vendorId = (int)controller.VendorID;
            int productId = (int)controller.ProductID;

            // Pads handled by the native HID layer always land on DEVICE_ID_PAD_0 : HidInputDevice
            // keeps pad_ at 0 and only ever opens one controller.
            if (IsHidSupportedDevice(vendorId, productId))
            {
                padType = (vendorId == NINTENDO_VENDOR_ID) ? PpssppPadType.HidSwitch : PpssppPadType.HidPlayStation;
                return PPSSPP_DEVICE_ID_PAD_0;
            }

            // Everything else goes through DirectInput. PPSSPP numbers those pads by their rank in
            // the DirectInput enumeration, after removing XInput pads and HID handled pads, so the
            // EmulationStation player index cannot be used here.
            var directInput = controller.DirectInput;
            if (directInput == null)
                return -1;

            var dinputPads = DirectInputInfo.Controllers
                .Where(d => !d.IsXInput && !IsHidSupportedDevice(d.VendorId, d.ProductId))
                .OrderBy(d => d.DeviceIndex)
                .ToList();

            int rank = dinputPads.FindIndex(d => d.InstanceGuid == directInput.InstanceGuid);
            if (rank < 0)
                return -1;

            return PPSSPP_DEVICE_ID_PAD_0 + rank;
        }

        /// <summary>
        /// Returns the PPSSPP keycode this controller emits for the given input, or null when the
        /// input is not available on that controller.
        /// </summary>
        private static string GetPadKeyCode(Controller controller, PpssppPadType padType, InputKey key)
        {
            string code;

            switch (padType)
            {
                case PpssppPadType.XInput:
                    return xinputKeyCodes.TryGetValue(key, out code) ? code : null;

                case PpssppPadType.HidSwitch:
                    // Switch Pro reports its triggers as digital buttons, PlayStation pads as analog axes.
                    if (key == InputKey.l2)
                        return "104";
                    if (key == InputKey.r2)
                        return "105";
                    return hidKeyCodes.TryGetValue(key, out code) ? code : null;

                case PpssppPadType.HidPlayStation:
                    return hidKeyCodes.TryGetValue(key, out code) ? code : null;

                default:
                    // DirectInput exposes raw button indexes, so read them from es_input.cfg instead
                    // of assuming a layout. NKCODE_BUTTON_1 is 188 and buttons are contiguous.
                    return GetInputKeyName(controller, key);
            }
        }

        private void AddKeyboardMappings(Dictionary<string, List<string>> mappings, InputConfig keyboard)
        {
            if (keyboard == null)
                return;

            string prefix = PPSSPP_DEVICE_ID_KEYBOARD.ToString() + "-";

            foreach (var input in pspMapping)
            {
                var a = keyboard[input.Value];
                if (a == null)
                    continue;

                int id = (int)a.Id;

                List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
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

                SDL.SDL_Keycode code = (SDL.SDL_Keycode)id;

                NKCODE nkCode;
                if (input_config_key_map.TryGetValue(code, out nkCode))
                    AddMapping(mappings, input.Key, prefix + (int)nkCode);
            }
        }

        private void AddKeyboardHotkeys(Dictionary<string, List<string>> mappings)
        {
            string prefix = PPSSPP_DEVICE_ID_KEYBOARD.ToString() + "-";

            Dictionary<string, HotkeyResult> hotkeys;
            if (Hotkeys.GetHotKeysFromFile("ppsspp", "", out hotkeys))
            {
                foreach (var hotkey in hotkeys)
                    AddMapping(mappings, hotkey.Value.EmulatorKey, prefix + hotkey.Value.EmulatorValue);

                return;
            }

            AddMapping(mappings, "Rewind", prefix + "67");              // Backspace
            AddMapping(mappings, "Fast-forward", prefix + "40");        // L
            AddMapping(mappings, "Load State", prefix + "134");         // F4
            AddMapping(mappings, "Save State", prefix + "132");         // F2
            AddMapping(mappings, "Pause", prefix + "131");              // F1
            AddMapping(mappings, "Pause (no menu)", prefix + "44");     // P
            AddMapping(mappings, "Screenshot", prefix + "138");         // F8
            AddMapping(mappings, "Previous Slot", prefix + "136");      // F6
            AddMapping(mappings, "Next Slot", prefix + "137");          // F7
            AddMapping(mappings, "Frame Advance", prefix + "39");       // K
            AddMapping(mappings, "Toggle Fullscreen", prefix + "34");   // F
            AddMapping(mappings, "Exit App", prefix + "111");           // Escape
        }

        /*private static int GetInputCode(string key, SdlToDirectInput ctrl, int direction = -1)
        {
            if (ctrl.ButtonMappings[key] == null)
                return 0;

            string button = ctrl.ButtonMappings[key];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1: return 19;
                    case 2: return 22;
                    case 4: return 20;
                    case 8: return 21;
                };
            }

            else if (button.StartsWith("b"))
            {
                int buttonID = button.Substring(1).ToInteger();
                return 188 + buttonID;
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                switch (axisID)
                {
                    case 0:
                        if (direction == 1) return 4000;
                        else return 4001;
                    case 1:
                        if (direction == 1) return 4002;
                        else return 4003;
                    case 2:
                        if (direction == 1) return 4022;
                        else return 4023;
                    case 3:
                        if (direction == 1) return 4024;
                        else return 4025;
                    case 4:
                        if (direction == 1) return 4026;
                        else return 4027;
                    case 5:
                        if (direction == 1) return 4028;
                        else return 4029;
                };
            }

            return 0;
        }*/

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            long pid;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    return (188 + pid).ToString();
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if (revertAxis) return "4000";
                            else return "4001";
                        case 1:
                            if (revertAxis) return "4002";
                            else return "4003";
                        case 2:
                            if (revertAxis) return "4022";
                            else return "4023";
                        case 3:
                            if (revertAxis) return "4024";
                            else return "4025";
                        case 4:
                            if (revertAxis) return "4026";
                            else return "4027";
                        case 5:
                            if (revertAxis) return "4028";
                            else return "4029";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "19";
                        case 2: return "22";
                        case 4: return "20";
                        case 8: return "21";
                    }
                }
            }
            return null;
        }

        private const int NINTENDO_VENDOR_ID = 0x057E;

        // Mirrors g_psInfos in Windows/Hid/HidInputDevice.cpp (PPSSPP 1.20.4).
        // These pads are handled by the native HID layer AND removed from the DirectInput
        // enumeration, so they must be excluded when computing DirectInput ranks.
        // Keep in sync when upgrading PPSSPP.
        static readonly List<KeyValuePair<int, int>> hidSupportedDevices = new List<KeyValuePair<int, int>>()
        {
            new KeyValuePair<int, int>(0x054C, 0x05C4),     // DualShock 4 v1
            new KeyValuePair<int, int>(0x054C, 0x09CC),     // DualShock 4 v2
            new KeyValuePair<int, int>(0x054C, 0x0CE6),     // DualSense
            new KeyValuePair<int, int>(0x054C, 0x0DF2),     // DualSense Edge
            new KeyValuePair<int, int>(0x054C, 0x0CDA),     // PlayStation Classic
            new KeyValuePair<int, int>(0x057E, 0x2009),     // Switch Pro
        };

        private static bool IsHidSupportedDevice(int vendorId, int productId)
        {
            foreach (var device in hidSupportedDevices)
            {
                if (device.Key == vendorId && device.Value == productId)
                    return true;
            }
            return false;
        }

        // Windows/XinputDevice.cpp : standard Android style keycodes.
        static readonly Dictionary<InputKey, string> xinputKeyCodes = new Dictionary<InputKey, string>
        {
            { InputKey.up,              "19"   },      // NKCODE_DPAD_UP
            { InputKey.down,            "20"   },      // NKCODE_DPAD_DOWN
            { InputKey.left,            "21"   },      // NKCODE_DPAD_LEFT
            { InputKey.right,           "22"   },      // NKCODE_DPAD_RIGHT
            { InputKey.a,               "96"   },      // NKCODE_BUTTON_A, south
            { InputKey.b,               "97"   },      // NKCODE_BUTTON_B, east
            { InputKey.y,               "99"   },      // NKCODE_BUTTON_X, west
            { InputKey.x,               "100"  },      // NKCODE_BUTTON_Y, north
            { InputKey.pageup,          "102"  },      // NKCODE_BUTTON_L1
            { InputKey.pagedown,        "103"  },      // NKCODE_BUTTON_R1
            { InputKey.l3,              "106"  },      // NKCODE_BUTTON_THUMBL
            { InputKey.r3,              "107"  },      // NKCODE_BUTTON_THUMBR
            { InputKey.start,           "108"  },      // NKCODE_BUTTON_START
            { InputKey.select,          "109"  },      // NKCODE_BUTTON_SELECT
            { InputKey.l2,              "4034" },      // JOYSTICK_AXIS_LTRIGGER, positive
            { InputKey.r2,              "4036" },      // JOYSTICK_AXIS_RTRIGGER, positive
            { InputKey.joystick1up,     "4002" },      // JOYSTICK_AXIS_Y, positive is up on XInput
            { InputKey.joystick1down,   "4003" },
            { InputKey.joystick1left,   "4001" },
            { InputKey.joystick1right,  "4000" },
        };

        // Windows/Hid/DualShock.cpp and Windows/Hid/SwitchPro.cpp produce the same keycodes for
        // every button mapped here, only the triggers differ (handled in GetPadKeyCode).
        static readonly Dictionary<InputKey, string> hidKeyCodes = new Dictionary<InputKey, string>
        {
            { InputKey.up,              "19"   },      // NKCODE_DPAD_UP
            { InputKey.down,            "20"   },      // NKCODE_DPAD_DOWN
            { InputKey.left,            "21"   },      // NKCODE_DPAD_LEFT
            { InputKey.right,           "22"   },      // NKCODE_DPAD_RIGHT
            { InputKey.x,               "188"  },      // NKCODE_BUTTON_1, north
            { InputKey.a,               "189"  },      // NKCODE_BUTTON_2, south
            { InputKey.b,               "190"  },      // NKCODE_BUTTON_3, east
            { InputKey.y,               "191"  },      // NKCODE_BUTTON_4, west
            { InputKey.pageup,          "194"  },      // NKCODE_BUTTON_7
            { InputKey.pagedown,        "195"  },      // NKCODE_BUTTON_8
            { InputKey.select,          "196"  },      // NKCODE_BUTTON_9
            { InputKey.start,           "197"  },      // NKCODE_BUTTON_10
            { InputKey.l3,              "106"  },      // NKCODE_BUTTON_THUMBL
            { InputKey.r3,              "107"  },      // NKCODE_BUTTON_THUMBR
            { InputKey.l2,              "4034" },      // JOYSTICK_AXIS_LTRIGGER, positive
            { InputKey.r2,              "4036" },      // JOYSTICK_AXIS_RTRIGGER, positive
            { InputKey.joystick1up,     "4003" },      // JOYSTICK_AXIS_Y, negative is up on HID
            { InputKey.joystick1down,   "4002" },
            { InputKey.joystick1left,   "4001" },
            { InputKey.joystick1right,  "4000" },
        };

        // Default pad hotkeys, all combined with SELECT.
        static readonly Dictionary<string, InputKey> defaultPadHotkeys = new Dictionary<string, InputKey>
        {
            { "Exit App",           InputKey.start   },
            { "Rewind",             InputKey.left    },
            { "Fast-forward",       InputKey.right   },
            { "Load State",         InputKey.x       },
            { "Save State",         InputKey.y       },
            { "Pause",              InputKey.a       },
            { "Screenshot",         InputKey.r3      },
            { "Pause (no menu)",    InputKey.l3      },
            { "Previous Slot",      InputKey.down    },
            { "Next Slot",          InputKey.up      },
        };

        // Names used in the pad hotkey file.
        static readonly Dictionary<string, InputKey> hotkeyInputNames = new Dictionary<string, InputKey>
        {
            { "up",         InputKey.up       },
            { "down",       InputKey.down     },
            { "left",       InputKey.left     },
            { "right",      InputKey.right    },
            { "a",          InputKey.a        },
            { "b",          InputKey.b        },
            { "x",          InputKey.x        },
            { "y",          InputKey.y        },
            { "start",      InputKey.start    },
            { "select",     InputKey.select   },
            { "pageup",     InputKey.pageup   },
            { "pagedown",   InputKey.pagedown },
            { "l2",         InputKey.l2       },
            { "r2",         InputKey.r2       },
            { "l3",         InputKey.l3       },
            { "r3",         InputKey.r3       },
        };


        static readonly Dictionary<string, InputKey> pspMapping = new Dictionary<string, InputKey>
        {
            { "Up", InputKey.up },
            { "Down", InputKey.down },
            { "Left", InputKey.left },
            { "Right", InputKey.right },
            { "Circle", InputKey.b },
            { "Cross", InputKey.a },
            { "Square", InputKey.y },
            { "Triangle", InputKey.x },
            { "Start", InputKey.start },
            { "Select", InputKey.select },
            { "L", InputKey.pageup },
            { "R", InputKey.pagedown },
            { "An.Up", InputKey.joystick1up },
            { "An.Down", InputKey.joystick1down },
            { "An.Left", InputKey.joystick1left },
            { "An.Right", InputKey.joystick1right }
        };

        enum NKCODE : int
        {
            BUTTON_CROSS = 23, // trackpad or X button(Xperia Play) is pressed
            BUTTON_CROSS_PS3 = 96, // PS3 X button is pressed
            BUTTON_CIRCLE = 1004, // Special custom keycode generated from 'O' button by our java code. Or 'O' button if Alt is pressed (TODO)
            BUTTON_CIRCLE_PS3 = 97, // PS3 O button is pressed
            BUTTON_SQUARE = 99, // Square button(Xperia Play) is pressed
            BUTTON_TRIANGLE = 100, // 'Triangle button(Xperia Play) is pressed
            UNKNOWN = 0,
            SOFT_LEFT = 1,
            SOFT_RIGHT = 2,
            HOME = 3,
            BACK = 4,
            CALL = 5,
            ENDCALL = 6,
            KEY_0 = 7,
            KEY_1 = 8,
            KEY_2 = 9,
            KEY_3 = 10,
            KEY_4 = 11,
            KEY_5 = 12,
            KEY_6 = 13,
            KEY_7 = 14,
            KEY_8 = 15,
            KEY_9 = 16,
            STAR = 17,
            POUND = 18,
            DPAD_UP = 19,
            DPAD_DOWN = 20,
            DPAD_LEFT = 21,
            DPAD_RIGHT = 22,
            DPAD_CENTER = 23,
            VOLUME_UP = 24,
            VOLUME_DOWN = 25,
            POWER = 26,
            CAMERA = 27,
            CLEAR = 28,
            A = 29,
            B = 30,
            C = 31,
            D = 32,
            E = 33,
            F = 34,
            G = 35,
            H = 36,
            I = 37,
            J = 38,
            K = 39,
            L = 40,
            M = 41,
            N = 42,
            O = 43,
            P = 44,
            Q = 45,
            R = 46,
            S = 47,
            T = 48,
            U = 49,
            V = 50,
            W = 51,
            X = 52,
            Y = 53,
            Z = 54,
            COMMA = 55,
            PERIOD = 56,
            ALT_LEFT = 57,
            ALT_RIGHT = 58,
            SHIFT_LEFT = 59,
            SHIFT_RIGHT = 60,
            TAB = 61,
            SPACE = 62,
            SYM = 63,
            EXPLORER = 64,
            ENVELOPE = 65,
            ENTER = 66,
            DEL = 67,
            GRAVE = 68,
            MINUS = 69,
            EQUALS = 70,
            LEFT_BRACKET = 71,
            RIGHT_BRACKET = 72,
            BACKSLASH = 73,
            SEMICOLON = 74,
            APOSTROPHE = 75,
            SLASH = 76,
            AT = 77,
            NUM = 78,
            HEADSETHOOK = 79,
            FOCUS = 80,
            PLUS = 81,
            MENU = 82,
            NOTIFICATION = 83,
            SEARCH = 84,
            MEDIA_PLAY_PAUSE = 85,
            MEDIA_STOP = 86,
            MEDIA_NEXT = 87,
            MEDIA_PREVIOUS = 88,
            MEDIA_REWIND = 89,
            MEDIA_FAST_FORWARD = 90,
            MUTE = 91,
            PAGE_UP = 92,
            PAGE_DOWN = 93,
            PICTSYMBOLS = 94,
            SWITCH_CHARSET = 95,
            BUTTON_A = 96,
            BUTTON_B = 97,
            BUTTON_C = 98,
            BUTTON_X = 99,
            BUTTON_Y = 100,
            BUTTON_Z = 101,
            BUTTON_L1 = 102,
            BUTTON_R1 = 103,
            BUTTON_L2 = 104,
            BUTTON_R2 = 105,
            BUTTON_THUMBL = 106,
            BUTTON_THUMBR = 107,
            BUTTON_START = 108,
            BUTTON_SELECT = 109,
            BUTTON_MODE = 110,
            ESCAPE = 111,
            FORWARD_DEL = 112,
            CTRL_LEFT = 113,
            CTRL_RIGHT = 114,
            CAPS_LOCK = 115,
            SCROLL_LOCK = 116,
            META_LEFT = 117,
            META_RIGHT = 118,
            FUNCTION = 119,
            SYSRQ = 120,
            BREAK = 121,
            MOVE_HOME = 122,
            MOVE_END = 123,
            INSERT = 124,
            FORWARD = 125,
            MEDIA_PLAY = 126,
            MEDIA_PAUSE = 127,
            MEDIA_CLOSE = 128,
            MEDIA_EJECT = 129,
            MEDIA_RECORD = 130,
            F1 = 131,
            F2 = 132,
            F3 = 133,
            F4 = 134,
            F5 = 135,
            F6 = 136,
            F7 = 137,
            F8 = 138,
            F9 = 139,
            F10 = 140,
            F11 = 141,
            F12 = 142,
            NUM_LOCK = 143,
            NUMPAD_0 = 144,
            NUMPAD_1 = 145,
            NUMPAD_2 = 146,
            NUMPAD_3 = 147,
            NUMPAD_4 = 148,
            NUMPAD_5 = 149,
            NUMPAD_6 = 150,
            NUMPAD_7 = 151,
            NUMPAD_8 = 152,
            NUMPAD_9 = 153,
            NUMPAD_DIVIDE = 154,
            NUMPAD_MULTIPLY = 155,
            NUMPAD_SUBTRACT = 156,
            NUMPAD_ADD = 157,
            NUMPAD_DOT = 158,
            NUMPAD_COMMA = 159,
            NUMPAD_ENTER = 160,
            NUMPAD_EQUALS = 161,
            NUMPAD_LEFT_PAREN = 162,
            NUMPAD_RIGHT_PAREN = 163,
            VOLUME_MUTE = 164,
            INFO = 165,
            CHANNEL_UP = 166,
            CHANNEL_DOWN = 167,
            ZOOM_IN = 168,
            ZOOM_OUT = 169,
            TV = 170,
            WINDOW = 171,
            GUIDE = 172,
            DVR = 173,
            BOOKMARK = 174,
            CAPTIONS = 175,
            SETTINGS = 176,
            TV_POWER = 177,
            TV_INPUT = 178,
            STB_POWER = 179,
            STB_INPUT = 180,
            AVR_POWER = 181,
            AVR_INPUT = 182,
            PROG_RED = 183,
            PROG_GREEN = 184,
            PROG_YELLOW = 185,
            PROG_BLUE = 186,
            APP_SWITCH = 187,
            BUTTON_1 = 188,
            BUTTON_2 = 189,
            BUTTON_3 = 190,
            BUTTON_4 = 191,
            BUTTON_5 = 192,
            BUTTON_6 = 193,
            BUTTON_7 = 194,
            BUTTON_8 = 195,
            BUTTON_9 = 196,
            BUTTON_10 = 197,
            BUTTON_11 = 198,
            BUTTON_12 = 199,
            BUTTON_13 = 200,
            BUTTON_14 = 201,
            BUTTON_15 = 202,
            BUTTON_16 = 203,
            LANGUAGE_SWITCH = 204,
            MANNER_MODE = 205,
            KEY_3D_MODE = 206,
            CONTACTS = 207,
            CALENDAR = 208,
            MUSIC = 209,
            CALCULATOR = 210,
            ZENKAKU_HANKAKU = 211,
            EISU = 212,
            MUHENKAN = 213,
            HENKAN = 214,
            KATAKANA_HIRAGANA = 215,
            YEN = 216,
            RO = 217,
            KANA = 218,
            ASSIST = 219,
        }

        /*static readonly Dictionary<InputKey, NKCODE> dualSenseToNKCode = new Dictionary<InputKey, NKCODE>
        {
            { InputKey.b,  NKCODE.BUTTON_3 }, // EAST
            { InputKey.a,  NKCODE.BUTTON_2 }, // SOUTH
            { InputKey.y,  NKCODE.BUTTON_1 }, // WEST
            { InputKey.x,  NKCODE.BUTTON_4 }, // NORTH
            { InputKey.select,  NKCODE.BUTTON_9 }, // SELECT
            { InputKey.start,  NKCODE.BUTTON_10 }, // START
            { InputKey.pageup,  NKCODE.BUTTON_5 }, // L
            { InputKey.pagedown,  NKCODE.BUTTON_6 }, // R
            { InputKey.up,  NKCODE.DPAD_UP }, 
            { InputKey.down,  NKCODE.DPAD_DOWN }, 
            { InputKey.left,  NKCODE.DPAD_LEFT }, 
            { InputKey.right,  NKCODE.DPAD_RIGHT }
        };*/

        /*static readonly Dictionary<InputKey, int> dualSenseJoy = new Dictionary<InputKey, int>
        {
            { InputKey.joystick1up,  4003 },
            { InputKey.joystick1down,  4002 },
            { InputKey.joystick1left,  4001 },
            { InputKey.joystick1right,  4000 },
        };

        static readonly Dictionary<InputKey, NKCODE> xInputToNKCode = new Dictionary<InputKey, NKCODE>
        {
            { InputKey.b,  NKCODE.BUTTON_B }, // EAST
            { InputKey.a,  NKCODE.BUTTON_A }, // SOUTH
            { InputKey.y,  NKCODE.BUTTON_X }, // WEST
            { InputKey.x,  NKCODE.BUTTON_Y }, // NORTH
            { InputKey.select,  NKCODE.BUTTON_SELECT }, // SELECT
            { InputKey.start,  NKCODE.BUTTON_START }, // START
            { InputKey.pageup,  NKCODE.BUTTON_L1 }, // L
            { InputKey.pagedown,  NKCODE.BUTTON_R1 }, // R
            { InputKey.up,  NKCODE.DPAD_UP },
            { InputKey.down,  NKCODE.DPAD_DOWN },
            { InputKey.left,  NKCODE.DPAD_LEFT },
            { InputKey.right,  NKCODE.DPAD_RIGHT }
        };

        static readonly Dictionary<InputKey, int> xInputJoy = new Dictionary<InputKey, int>
        {
            { InputKey.joystick1up,  4002 },
            { InputKey.joystick1down,  4003 },
            { InputKey.joystick1left,  4001 },
            { InputKey.joystick1right,  4000 },
        };*/

        static readonly Dictionary<SDL.SDL_Keycode, NKCODE> input_config_key_map = new Dictionary<SDL.SDL_Keycode, NKCODE>()
        {
           { SDL.SDL_Keycode.SDLK_BACKSPACE, NKCODE.DEL },
           { SDL.SDL_Keycode.SDLK_TAB, NKCODE.TAB },
           { SDL.SDL_Keycode.SDLK_CLEAR, NKCODE.CLEAR },
           { SDL.SDL_Keycode.SDLK_RETURN, NKCODE.ENTER },
           { SDL.SDL_Keycode.SDLK_ESCAPE, NKCODE.ESCAPE },
           { SDL.SDL_Keycode.SDLK_SPACE, NKCODE.SPACE },
           { SDL.SDL_Keycode.SDLK_PLUS, NKCODE.PLUS },
           { SDL.SDL_Keycode.SDLK_COMMA, NKCODE.COMMA },
           { SDL.SDL_Keycode.SDLK_MINUS, NKCODE.MINUS },
           { SDL.SDL_Keycode.SDLK_PERIOD, NKCODE.PERIOD },
           { SDL.SDL_Keycode.SDLK_SLASH, NKCODE.SLASH },
           { SDL.SDL_Keycode.SDLK_0, NKCODE.KEY_0 },
           { SDL.SDL_Keycode.SDLK_1, NKCODE.KEY_1 },
           { SDL.SDL_Keycode.SDLK_2, NKCODE.KEY_2 },
           { SDL.SDL_Keycode.SDLK_3, NKCODE.KEY_3 },
           { SDL.SDL_Keycode.SDLK_4, NKCODE.KEY_4 },
           { SDL.SDL_Keycode.SDLK_5, NKCODE.KEY_5 },
           { SDL.SDL_Keycode.SDLK_6, NKCODE.KEY_6 },
           { SDL.SDL_Keycode.SDLK_7, NKCODE.KEY_7 },
           { SDL.SDL_Keycode.SDLK_8, NKCODE.KEY_8 },
           { SDL.SDL_Keycode.SDLK_9, NKCODE.KEY_9 },
           { SDL.SDL_Keycode.SDLK_SEMICOLON, NKCODE.SEMICOLON },
           { SDL.SDL_Keycode.SDLK_EQUALS, NKCODE.EQUALS },
           { SDL.SDL_Keycode.SDLK_AT, NKCODE.AT },
           { SDL.SDL_Keycode.SDLK_LEFTBRACKET, NKCODE.LEFT_BRACKET },
           { SDL.SDL_Keycode.SDLK_BACKSLASH, NKCODE.BACKSLASH },
           { SDL.SDL_Keycode.SDLK_RIGHTBRACKET, NKCODE.RIGHT_BRACKET },
           { SDL.SDL_Keycode.SDLK_a, NKCODE.A },
           { SDL.SDL_Keycode.SDLK_b, NKCODE.B },
           { SDL.SDL_Keycode.SDLK_c, NKCODE.C },
           { SDL.SDL_Keycode.SDLK_d, NKCODE.D },
           { SDL.SDL_Keycode.SDLK_e, NKCODE.E },
           { SDL.SDL_Keycode.SDLK_f, NKCODE.F },
           { SDL.SDL_Keycode.SDLK_g, NKCODE.G },
           { SDL.SDL_Keycode.SDLK_h, NKCODE.H },
           { SDL.SDL_Keycode.SDLK_i, NKCODE.I },
           { SDL.SDL_Keycode.SDLK_j, NKCODE.J },
           { SDL.SDL_Keycode.SDLK_k, NKCODE.K },
           { SDL.SDL_Keycode.SDLK_l, NKCODE.L },
           { SDL.SDL_Keycode.SDLK_m, NKCODE.M },
           { SDL.SDL_Keycode.SDLK_n, NKCODE.N },
           { SDL.SDL_Keycode.SDLK_o, NKCODE.O },
           { SDL.SDL_Keycode.SDLK_p, NKCODE.P },
           { SDL.SDL_Keycode.SDLK_q, NKCODE.Q },
           { SDL.SDL_Keycode.SDLK_r, NKCODE.R },
           { SDL.SDL_Keycode.SDLK_s, NKCODE.S },
           { SDL.SDL_Keycode.SDLK_t, NKCODE.T },
           { SDL.SDL_Keycode.SDLK_u, NKCODE.U },
           { SDL.SDL_Keycode.SDLK_v, NKCODE.V },
           { SDL.SDL_Keycode.SDLK_w, NKCODE.W },
           { SDL.SDL_Keycode.SDLK_x, NKCODE.X },
           { SDL.SDL_Keycode.SDLK_y, NKCODE.Y },
           { SDL.SDL_Keycode.SDLK_z, NKCODE.Z },
           { SDL.SDL_Keycode.SDLK_DELETE, NKCODE.FORWARD_DEL },
           { SDL.SDL_Keycode.SDLK_KP_0, NKCODE.NUMPAD_0 },
           { SDL.SDL_Keycode.SDLK_KP_1, NKCODE.NUMPAD_1 },
           { SDL.SDL_Keycode.SDLK_KP_2, NKCODE.NUMPAD_2 },
           { SDL.SDL_Keycode.SDLK_KP_3, NKCODE.NUMPAD_3 },
           { SDL.SDL_Keycode.SDLK_KP_4, NKCODE.NUMPAD_4 },
           { SDL.SDL_Keycode.SDLK_KP_5, NKCODE.NUMPAD_5 },
           { SDL.SDL_Keycode.SDLK_KP_6, NKCODE.NUMPAD_6 },
           { SDL.SDL_Keycode.SDLK_KP_7, NKCODE.NUMPAD_7 },
           { SDL.SDL_Keycode.SDLK_KP_8, NKCODE.NUMPAD_8 },
           { SDL.SDL_Keycode.SDLK_KP_9, NKCODE.NUMPAD_9 },
           { SDL.SDL_Keycode.SDLK_KP_PERIOD, NKCODE.NUMPAD_DOT },
           { SDL.SDL_Keycode.SDLK_KP_DIVIDE, NKCODE.NUMPAD_DIVIDE },
           { SDL.SDL_Keycode.SDLK_KP_MULTIPLY, NKCODE.NUMPAD_MULTIPLY },
           { SDL.SDL_Keycode.SDLK_KP_MINUS, NKCODE.NUMPAD_SUBTRACT },
           { SDL.SDL_Keycode.SDLK_KP_PLUS, NKCODE.NUMPAD_ADD },
           { SDL.SDL_Keycode.SDLK_KP_ENTER, NKCODE.NUMPAD_ENTER },
           { SDL.SDL_Keycode.SDLK_KP_EQUALS, NKCODE.NUMPAD_EQUALS },
           { SDL.SDL_Keycode.SDLK_UP, NKCODE.DPAD_UP },
           { SDL.SDL_Keycode.SDLK_DOWN, NKCODE.DPAD_DOWN },
           { SDL.SDL_Keycode.SDLK_RIGHT, NKCODE.DPAD_RIGHT },
           { SDL.SDL_Keycode.SDLK_LEFT, NKCODE.DPAD_LEFT },
           { SDL.SDL_Keycode.SDLK_INSERT, NKCODE.INSERT },
           { SDL.SDL_Keycode.SDLK_HOME, NKCODE.HOME },
           { SDL.SDL_Keycode.SDLK_END, NKCODE.MOVE_END },
           { SDL.SDL_Keycode.SDLK_PAGEUP, NKCODE.PAGE_UP },
           { SDL.SDL_Keycode.SDLK_PAGEDOWN, NKCODE.PAGE_DOWN },
           { SDL.SDL_Keycode.SDLK_F1, NKCODE.F1 },
           { SDL.SDL_Keycode.SDLK_F2, NKCODE.F2 },
           { SDL.SDL_Keycode.SDLK_F3, NKCODE.F3 },
           { SDL.SDL_Keycode.SDLK_F4, NKCODE.F4 },
           { SDL.SDL_Keycode.SDLK_F5, NKCODE.F5 },
           { SDL.SDL_Keycode.SDLK_F6, NKCODE.F6 },
           { SDL.SDL_Keycode.SDLK_F7, NKCODE.F7 },
           { SDL.SDL_Keycode.SDLK_F8, NKCODE.F8 },
           { SDL.SDL_Keycode.SDLK_F9, NKCODE.F9 },
           { SDL.SDL_Keycode.SDLK_F10, NKCODE.F10 },
           { SDL.SDL_Keycode.SDLK_F11, NKCODE.F11 },
           { SDL.SDL_Keycode.SDLK_F12, NKCODE.F12 },
           { SDL.SDL_Keycode.SDLK_NUMLOCKCLEAR, NKCODE.NUM_LOCK },
           { SDL.SDL_Keycode.SDLK_CAPSLOCK, NKCODE.CAPS_LOCK },
           { SDL.SDL_Keycode.SDLK_SCROLLLOCK, NKCODE.SCROLL_LOCK },
           { SDL.SDL_Keycode.SDLK_RSHIFT, NKCODE.SHIFT_RIGHT },
           { SDL.SDL_Keycode.SDLK_LSHIFT, NKCODE.SHIFT_LEFT },
           { SDL.SDL_Keycode.SDLK_RCTRL, NKCODE.CTRL_RIGHT },
           { SDL.SDL_Keycode.SDLK_LCTRL, NKCODE.CTRL_LEFT },
           { SDL.SDL_Keycode.SDLK_RALT, NKCODE.ALT_RIGHT },
           { SDL.SDL_Keycode.SDLK_LALT, NKCODE.ALT_RIGHT },
           { SDL.SDL_Keycode.SDLK_MODE, NKCODE.BUTTON_MODE },
           { SDL.SDL_Keycode.SDLK_SYSREQ, NKCODE.SYSRQ },                                                                   
           { SDL.SDL_Keycode.SDLK_PAUSE, NKCODE.BREAK },
           { SDL.SDL_Keycode.SDLK_MENU, NKCODE.MENU },
           { SDL.SDL_Keycode.SDLK_POWER, NKCODE.POWER },
        };
    }
}
