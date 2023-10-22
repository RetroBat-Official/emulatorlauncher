using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Windows.Input;
using System.Globalization;

namespace EmulatorLauncher
{
    partial class PpssppGenerator
    {
        // see. github.com/batocera-linux/batocera.linux/blob/master/package/batocera/core/batocera-configgen/configgen/configgen/generators/ppsspp/ppssppControllers.py
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string iniFile = Path.Combine(path, "memstick", "PSP", "SYSTEM", "controls.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    ini.ClearSection("ControlMapping");
                    var controller = Program.Controllers.FirstOrDefault();
                    GenerateControllerConfig(ini, controller);
                }
            }
            catch { }
        }

        private void GenerateControllerConfig(IniFile ini, Controller controller)
        {
            if (controller == null)
                return;

            int index = controller.DirectInput != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex;
            if (SystemConfig.isOptSet("ppsspp_forceindex") && !string.IsNullOrEmpty(SystemConfig["ppsspp_forceindex"]))
                index = SystemConfig["ppsspp_forceindex"].ToInteger();

            string xinputID = (20 + index).ToString() + "-";
            string sdlID = (10 + index).ToString() + "-";

            if (controller.IsKeyboard)
            {
                ConfigureKeyboard(ini, controller.Config);
                return;
            }
            else
            {
                foreach (var input in pspMapping)
                {
                    string button = input.Key;
                    var inputKey = input.Value;

                    if (button.StartsWith("An."))
                    {
                        string xTarget = xInputJoy[inputKey].ToString();
                        string sdlTarget = dualSenseJoy[inputKey].ToString();
                        ini.WriteValue("ControlMapping", button, xinputID + xTarget + "," + sdlID + sdlTarget);
                    }
                    else
                    {
                        var xnkCode = (int) xInputToNKCode[inputKey];
                        string xTarget = xnkCode.ToString();

                        var sdlnkCode = (int)dualSenseToNKCode[inputKey];
                        string sdlTarget = sdlnkCode.ToString();
                        
                        ini.WriteValue("ControlMapping", button, xinputID + xTarget + "," + sdlID + sdlTarget);
                    }
                }

                ini.WriteValue("ControlMapping", "Rewind", "1-131," + xinputID + "109:" + xinputID + "21," + sdlID + "196:" + sdlID + "21");            // SELECT + LEFT
                ini.WriteValue("ControlMapping", "Fast-forward", "1-132," + xinputID + "109:" + xinputID + "22, " + sdlID + "196:" + sdlID + "22");     // SELECT + RIGHT
                ini.WriteValue("ControlMapping", "Load State", "1-134," + xinputID + "109:" + xinputID + "100, " + sdlID + "196:" + sdlID + "191");     // SELECT + NORTH
                ini.WriteValue("ControlMapping", "Save State", "1-133," + xinputID + "109:" + xinputID + "99, " + sdlID + "196:" + sdlID + "188");      // SELECT + WEST
                ini.WriteValue("ControlMapping", "Pause", "1-140," + xinputID + "109:" + xinputID + "97, " + sdlID + "196:" + sdlID + "190");           // SELECT + EAST
                ini.WriteValue("ControlMapping", "Screenshot", "1-138," + xinputID + "109:" + xinputID + "105, " + sdlID + "196:" + sdlID + "195");     // SELECT + R2

                if (_saveStatesWatcher != null && _saveStatesWatcher.IncrementalMode)
                {
                    ini.WriteValue("ControlMapping", "Previous Slot", "");
                    ini.WriteValue("ControlMapping", "Next Slot", "");
                }
                else
                {
                    ini.WriteValue("ControlMapping", "Previous Slot", "1-135," + xinputID + "109:" + xinputID + "19," + sdlID + "196:" + sdlID + "19"); // SELECT + UP
                    ini.WriteValue("ControlMapping", "Next Slot", "1-136," + xinputID + "109:" + xinputID + "20," + sdlID + "196:" + sdlID + "20");     // SELECT + DOWN
                }
            }
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard)
        {
            if (keyboard == null)
                return;

            string deviceID = "1-";

            foreach(var input in pspMapping)
            {
                InputKey key = input.Value;
                var a = keyboard[key];

                if (a == null)
                    continue;

                int id = (int) a.Id;

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

                if (input_config_key_map.ContainsKey(code))
                {
                    var nkCode = input_config_key_map[code];
                    ini.WriteValue("ControlMapping", input.Key, deviceID + (int)nkCode);
                }
            }

            ini.WriteValue("ControlMapping", "Rewind", deviceID + "131");           //F1
            ini.WriteValue("ControlMapping", "Fast-forward", deviceID + "132");     //F2
            ini.WriteValue("ControlMapping", "Load State", deviceID + "134");       //F4
            ini.WriteValue("ControlMapping", "Save State", deviceID + "133");       //F3
            ini.WriteValue("ControlMapping", "Pause", deviceID + "140");            //F10
            ini.WriteValue("ControlMapping", "Screenshot", deviceID + "138");       //F8
            ini.WriteValue("ControlMapping", "Previous Slot", deviceID + "135");    //F5
            ini.WriteValue("ControlMapping", "Next Slot", deviceID + "136");        //F6
        }

        static Dictionary<string, InputKey> pspMapping = new Dictionary<string, InputKey>
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

        static Dictionary<InputKey, NKCODE> dualSenseToNKCode = new Dictionary<InputKey, NKCODE>
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
        };

        static Dictionary<InputKey, int> dualSenseJoy = new Dictionary<InputKey, int>
        {
            { InputKey.joystick1up,  4003 },
            { InputKey.joystick1down,  4002 },
            { InputKey.joystick1left,  4001 },
            { InputKey.joystick1right,  4000 },
        };

        static Dictionary<InputKey, NKCODE> xInputToNKCode = new Dictionary<InputKey, NKCODE>
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

        static Dictionary<InputKey, int> xInputJoy = new Dictionary<InputKey, int>
        {
            { InputKey.joystick1up,  4002 },
            { InputKey.joystick1down,  4003 },
            { InputKey.joystick1left,  4001 },
            { InputKey.joystick1right,  4000 },
        };

        static Dictionary<SDL.SDL_Keycode, NKCODE> input_config_key_map = new Dictionary<SDL.SDL_Keycode, NKCODE>()
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
