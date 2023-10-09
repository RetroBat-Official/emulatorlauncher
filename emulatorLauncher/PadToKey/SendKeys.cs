using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace EmulatorLauncher.PadToKeyboard
{
    class SendKey
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        };

        [StructLayout(LayoutKind.Explicit)]
        struct MouseKeybdHardwareInputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(4)]
            public MOUSEINPUT mi;
            [FieldOffset(4)]
            public KEYBDINPUT ki;
            [FieldOffset(4)]
            public HARDWAREINPUT hi;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct KEYBDINPUT64
        {
            public ushort wVk;
            public short wScan;
            public uint dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct MOUSEINPUT64
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct HARDWAREINPUT64
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUT64
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(8)]
            public MOUSEINPUT64 mi;
            [FieldOffset(8)]
            public KEYBDINPUT64 ki;
            [FieldOffset(8)]
            public HARDWAREINPUT64 hi;
        }

        [DllImport("user32.dll")]
        private extern static void SendInput(int nInputs, ref INPUT pInputs, int cbsize);

        [DllImport("user32.dll", EntryPoint = "SendInput")]
        private extern static void SendInput64(int nInputs, ref INPUT64 pInputs, int cbsize);

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyA")]
        private extern static int MapVirtualKey(int wCode, int wMapType);

        const int INPUT_MOUSE = 0;
        const int INPUT_KEYBOARD = 1;

        const int KEYEVENTF_KEYDOWN = 0x0;
        const int KEYEVENTF_EXTENDEDKEY = 0x1;
        const int KEYEVENTF_KEYUP = 0x2;
        const int KEYEVENTF_UNICODE = 0x4;
        const int KEYEVENTF_SCANCODE = 0x8;

        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        const uint MOUSEEVENTF_MOVE = 0x0001;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        const uint MOUSEEVENTF_XDOWN = 0x0080;
        const uint MOUSEEVENTF_XUP = 0x0100;
        const uint MOUSEEVENTF_WHEEL = 0x0800;
        const uint MOUSEEVENTF_HWHEEL = 0x01000;

        public enum MouseInput : int
        {
            Click = 0x110,
            RClick = 0x111,
            MClick = 0x112
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        public static void MoveMouseBy(int dx, int dy)
        {
            mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, 0);
        }

        public static void SendMouseInput(MouseInput key, bool pressed)
        {
            Debug.WriteLine(key.ToString() + " " + (pressed ? "(DOWN)" : "(UP)"));

            switch (key)
            {
                case MouseInput.Click:
                    mouse_event(pressed ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    break;
                case MouseInput.RClick:
                    mouse_event(pressed ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    break;
                case MouseInput.MClick:
                    mouse_event(pressed ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                    break;
            }
        }

        public static void Send(Keys key, bool keyDown, bool isEXTEND = false)
        {
            Debug.WriteLine(key.ToString() + " " + (keyDown ? "(DOWN)" : "(UP)"));

            if (IntPtr.Size == 8)
            {
                INPUT64 inp = new INPUT64();

                inp.type = INPUT_KEYBOARD;
                inp.ki.wVk = (ushort)key;
                inp.ki.wScan = (short)MapVirtualKey(inp.ki.wVk, 0);
                inp.ki.dwFlags = (uint)(((isEXTEND) ? (KEYEVENTF_EXTENDEDKEY) : 0x0) | (keyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP));
                inp.ki.time = 0;
                inp.ki.dwExtraInfo = IntPtr.Zero;
                SendInput64(1, ref inp, Marshal.SizeOf(inp));
            }
            else
            {
                INPUT inp = new INPUT();

                inp.type = INPUT_KEYBOARD;
                inp.ki.wVk = (ushort)key;
                inp.ki.wScan = (ushort)MapVirtualKey(inp.ki.wVk, 0);
                inp.ki.dwFlags = (uint)(((isEXTEND) ? (KEYEVENTF_EXTENDEDKEY) : 0x0) | (keyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP));
                inp.ki.time = 0;
                inp.ki.dwExtraInfo = IntPtr.Zero;
                SendInput(1, ref inp, Marshal.SizeOf(inp));
            }
        }

        public static void SendScanCode(uint scanCode, bool keyDown, bool isEXTEND = false)
        {
            if ((scanCode & 0x0100) == 0x0100)
            {
                SendMouseInput((MouseInput)scanCode, keyDown);
                return;
            }

            Debug.WriteLine(scanCode.ToString() + " " + (keyDown ? "(DOWN)" : "(UP)"));

            if (IntPtr.Size == 8)
            {
                INPUT64 inp = new INPUT64();

                inp.type = INPUT_KEYBOARD;
                inp.ki.wScan = (short)(scanCode & 0xFF);

                inp.ki.dwFlags = (uint)(KEYEVENTF_SCANCODE | (keyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP));
                if ((scanCode & 0xE000) != 0)
                    inp.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;

                inp.ki.time = 0;
                inp.ki.dwExtraInfo = IntPtr.Zero;
                SendInput64(1, ref inp, Marshal.SizeOf(inp));
            }
            else
            {
                INPUT inp = new INPUT();

                inp.type = INPUT_KEYBOARD;
                inp.ki.wScan = (ushort)(scanCode & 0xFFFF);
                
                inp.ki.dwFlags = (uint)(KEYEVENTF_SCANCODE | (keyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP));
                if ((scanCode & 0xE000) != 0)
                    inp.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;

                inp.ki.time = 0;
                inp.ki.dwExtraInfo = IntPtr.Zero;
                SendInput(1, ref inp, Marshal.SizeOf(inp));
            }
        }
    }
    
    public enum ScanCode : uint
    {
        LBUTTON = 0,
        RBUTTON = 0,
        CANCEL = 70,
        MBUTTON = 0,
        XBUTTON1 = 0,
        XBUTTON2 = 0,
        BACK = 14,
        TAB = 15,
        CLEAR = 76,
        RETURN = 28,
        SHIFT = 42,
        CONTROL = 29,
        MENU = 56,
        PAUSE = 0,
        CAPITAL = 58,
        KANA = 0,
        HANGUL = 0,
        JUNJA = 0,
        FINAL = 0,
        HANJA = 0,
        KANJI = 0,
        ESCAPE = 1,
        CONVERT = 0,
        NONCONVERT = 0,
        ACCEPT = 0,
        MODECHANGE = 0,
        SPACE = 57,
        PRIOR = 73,
        NEXT = 81,
        END = 79,
        HOME = 71,
        LEFT = 75,
        UP = 72,
        RIGHT = 77,
        DOWN = 80,
        SELECT = 0,
        PRINT = 0,
        EXECUTE = 0,
        SNAPSHOT = 84,
        INSERT = 82,
        DELETE = 83,
        HELP = 99,
        KEY_0 = 11,
        KEY_1 = 2,
        KEY_2 = 3,
        KEY_3 = 4,
        KEY_4 = 5,
        KEY_5 = 6,
        KEY_6 = 7,
        KEY_7 = 8,
        KEY_8 = 9,
        KEY_9 = 10,
        KEY_A = 30,
        KEY_B = 48,
        KEY_C = 46,
        KEY_D = 32,
        KEY_E = 18,
        KEY_F = 33,
        KEY_G = 34,
        KEY_H = 35,
        KEY_I = 23,
        KEY_J = 36,
        KEY_K = 37,
        KEY_L = 38,
        KEY_M = 50,
        KEY_N = 49,
        KEY_O = 24,
        KEY_P = 25,
        KEY_Q = 16,
        KEY_R = 19,
        KEY_S = 31,
        KEY_T = 20,
        KEY_U = 22,
        KEY_V = 47,
        KEY_W = 17,
        KEY_X = 45,
        KEY_Y = 21,
        KEY_Z = 44,
        LWIN = 91,
        RWIN = 92,
        APPS = 93,
        SLEEP = 95,
        NUMPAD0 = 82,
        NUMPAD1 = 79,
        NUMPAD2 = 80,
        NUMPAD3 = 81,
        NUMPAD4 = 75,
        NUMPAD5 = 76,
        NUMPAD6 = 77,
        NUMPAD7 = 71,
        NUMPAD8 = 72,
        NUMPAD9 = 73,
        MULTIPLY = 55,
        ADD = 78,
        SEPARATOR = 0,
        SUBTRACT = 74,
        DECIMAL = 83,
        DIVIDE = 53,
        F1 = 59,
        F2 = 60,
        F3 = 61,
        F4 = 62,
        F5 = 63,
        F6 = 64,
        F7 = 65,
        F8 = 66,
        F9 = 67,
        F10 = 68,
        F11 = 87,
        F12 = 88,
        F13 = 100,
        F14 = 101,
        F15 = 102,
        F16 = 103,
        F17 = 104,
        F18 = 105,
        F19 = 106,
        F20 = 107,
        F21 = 108,
        F22 = 109,
        F23 = 110,
        F24 = 118,
        NUMLOCK = 69,
        SCROLL = 70,
        LSHIFT = 42,
        RSHIFT = 54,
        LCONTROL = 29,
        RCONTROL = 29,
        LMENU = 56,
        RMENU = 56,
        BROWSER_BACK = 106,
        BROWSER_FORWARD = 105,
        BROWSER_REFRESH = 103,
        BROWSER_STOP = 104,
        BROWSER_SEARCH = 101,
        BROWSER_FAVORITES = 102,
        BROWSER_HOME = 50,
        VOLUME_MUTE = 32,
        VOLUME_DOWN = 46,
        VOLUME_UP = 48,
        MEDIA_NEXT_TRACK = 25,
        MEDIA_PREV_TRACK = 16,
        MEDIA_STOP = 36,
        MEDIA_PLAY_PAUSE = 34,
        LAUNCH_MAIL = 108,
        LAUNCH_MEDIA_SELECT = 109,
        LAUNCH_APP1 = 107,
        LAUNCH_APP2 = 33,
        OEM_1 = 39,
        OEM_PLUS = 13,
        OEM_COMMA = 51,
        OEM_MINUS = 12,
        OEM_PERIOD = 52,
        OEM_2 = 53,
        OEM_3 = 41,
        OEM_4 = 26,
        OEM_5 = 43,
        OEM_6 = 27,
        OEM_7 = 40,
        OEM_8 = 0,
        OEM_102 = 86,
        PROCESSKEY = 0,
        PACKET = 0,
        ATTN = 0,
        CRSEL = 0,
        EXSEL = 0,
        EREOF = 93,
        PLAY = 0,
        ZOOM = 98,
        NONAME = 0,
        PA1 = 0,
        OEM_CLEAR = 0,
    }

    public enum LinuxScanCode : uint
    {
        KEY_ESC = 1,
        KEY_1 = 2,
        KEY_2 = 3,
        KEY_3 = 4,
        KEY_4 = 5,
        KEY_5 = 6,
        KEY_6 = 7,
        KEY_7 = 8,
        KEY_8 = 9,
        KEY_9 = 10,
        KEY_0 = 11,
        KEY_MINUS = 12,
        KEY_EQUAL = 13,
        KEY_BACKSPACE = 14,
        KEY_TAB = 15,
        KEY_Q = 0x10,
        KEY_W = 0x11,
        KEY_E = 0x12,
        KEY_R = 0x13,
        KEY_T = 0x14,
        KEY_Y = 0x15,
        KEY_U = 0x16,
        KEY_I = 0x17,
        KEY_O = 0x18,
        KEY_P = 0x19,
        KEY_LEFTBRACE = 0x1a,
        KEY_RIGHTBRACE = 0x1b,
        KEY_ENTER = 0x1c,
        KEY_LEFTCTL = 0x1D,
        KEY_LEFTCTRL = 0x1D,
        KEY_A = 0x1e,
        KEY_S = 0x1f,
        KEY_D = 0x20,
        KEY_F = 0x21,
        KEY_G = 0x22,
        KEY_H = 0x23,
        KEY_J = 0x24,
        KEY_K = 0x25,
        KEY_L = 0x26,
        KEY_SEMICOLON = 0x27,
        KEY_APOSTROPHE = 0x28,
        KEY_GRAVE = 0x29,
        KEY_LEFTSHIFT = 0x2a,
        KEY_BACKSLASH = 0x2b,
        KEY_Z = 0x2c,
        KEY_X = 0x2d,
        KEY_C = 0x2e,
        KEY_V = 0x2f,
        KEY_B = 0x30,
        KEY_N = 0x31,
        KEY_M = 0x32,
        KEY_COMMA = 0x33,
        KEY_DOT = 0x34,
        KEY_SLASH = 0x35,
        KEY_RIGHTSHIFT = 0x36,
        KEY_KPASTERISK = 0x37,
        KEY_LEFTALT = 0x38,
        KEY_SPACE = 0x39,
        KEY_CAPSLOCK = 0x3a,
        KEY_F1 = 0x3b,
        KEY_F2 = 0x3c,
        KEY_F3 = 0x3d,
        KEY_F4 = 0x3e,
        KEY_F5 = 0x3f,
        KEY_F6 = 0x40,
        KEY_F7 = 0x41,
        KEY_F8 = 0x42,
        KEY_F9 = 0x43,
        KEY_F10 = 0x44,
        KEY_NUMLOCK = 0x45,
        KEY_SCROLLLOCK = 0x46,
        KEY_KP7 = 71,
        KEY_KP8 = 72,
        KEY_KP9 = 73,
        KEY_KPMINUS = 74,
        KEY_KP4 = 75,
        KEY_KP5 = 76,
        KEY_KP6 = 77,
        KEY_KPPLUS = 78,
        KEY_KP1 = 79,
        KEY_KP2 = 80,
        KEY_KP3 = 81,
        KEY_KP0 = 83,
        KEY_KPDOT = 84,
        KEY_F11 = 0x57,
        KEY_F12 = 0x58,
        
        KEY_KPENTER = 0xE01C,
        KEY_RIGHTCTRL = 0xE01D,
        KEY_KPSLASH = 0xE035,
        KEY_RIGHTALT = 0xE038,
        KEY_HOME = 0xE047,
        KEY_UP = 0xE048,
        KEY_PAGEUP = 0xE049,
        KEY_LEFT = 0xE04B,
        KEY_RIGHT = 0xE04D,
        KEY_END = 0xE04F,
        KEY_DOWN = 0xE050,
        KEY_PAGEDOWN = 0xE051,
        KEY_INSERT = 0xE052,
        KEY_DELETE = 0xE053,
        KEY_PAUSE = 0xE046,
        KEY_PRINT = 0xE037,
        KEY_MENU = 0xE05D, // 	sc_application = 0xE05D,

        // Mouse clic
        BTN_LEFT		= 0x110,
        BTN_RIGHT		= 0x111,
        BTN_MIDDLE		= 0x112,
    }
}