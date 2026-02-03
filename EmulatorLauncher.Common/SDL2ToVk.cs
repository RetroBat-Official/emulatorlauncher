using System;

namespace EmulatorLauncher.Common
{
    public static class SDL2ToVk
    {
        // SDL2 constants
        private const int SDLK_SCANCODE_MASK = unchecked((int)0x40000000);

        public static int SdlKeycodeToVk(int sdlKeycode)
        {
            // 1) SDL scancode wrapped in keycode: 0x40000000 | scancode
            if ((sdlKeycode & SDLK_SCANCODE_MASK) != 0)
            {
                int sc = sdlKeycode & 0x1FF; // SDL scancode id

                int vk = SdlScancodeToVk(sc);
                if (vk != 0) return vk;

                return 0;
            }

            // 2) ASCII-ish keycodes
            // letters: SDL uses lowercase
            if (sdlKeycode >= 'a' && sdlKeycode <= 'z')
                return Char.ToUpperInvariant((char)sdlKeycode); // VK_A..VK_Z

            // digits
            if (sdlKeycode >= '0' && sdlKeycode <= '9')
                return sdlKeycode; // VK_0..VK_9

            // common punctuation from keycode (when SDL gives ASCII)
            switch (sdlKeycode)
            {
                case ' ': return 0x20; // VK_SPACE
                case '\r': return 0x0D; // VK_RETURN
                case '\t': return 0x09; // VK_TAB
                case '\b': return 0x08; // VK_BACK
                case 0x1B: return 0x1B; // VK_ESCAPE

                case ',': return 0xBC; // VK_OEM_COMMA
                case '.': return 0xBE; // VK_OEM_PERIOD
                case ';': return 0xBA; // VK_OEM_1
                case '/': return 0xBF; // VK_OEM_2
                case '`': return 0xC0; // VK_OEM_3
                case '[': return 0xDB; // VK_OEM_4
                case '\\': return 0xDC; // VK_OEM_5
                case ']': return 0xDD; // VK_OEM_6
                case '\'': return 0xDE; // VK_OEM_7
                case '-': return 0xBD; // VK_OEM_MINUS
                case '=': return 0xBB; // VK_OEM_PLUS
            }

            return 0;
        }

        /// <summary>
        /// SDL_Scancode (integer) -> Windows VK_*
        /// sc is the raw scancode number (SDL_SCANCODE_* enum value).
        /// </summary>
        private static int SdlScancodeToVk(int sc)
        {
            switch (sc)
            {
                // ========= Letters =========
                // SDL scancodes 4..29 correspond to A..Z
                case 4: return 'A';
                case 5: return 'B';
                case 6: return 'C';
                case 7: return 'D';
                case 8: return 'E';
                case 9: return 'F';
                case 10: return 'G';
                case 11: return 'H';
                case 12: return 'I';
                case 13: return 'J';
                case 14: return 'K';
                case 15: return 'L';
                case 16: return 'M';
                case 17: return 'N';
                case 18: return 'O';
                case 19: return 'P';
                case 20: return 'Q';
                case 21: return 'R';
                case 22: return 'S';
                case 23: return 'T';
                case 24: return 'U';
                case 25: return 'V';
                case 26: return 'W';
                case 27: return 'X';
                case 28: return 'Y';
                case 29: return 'Z';

                // ========= Numbers row =========
                case 30: return '1';
                case 31: return '2';
                case 32: return '3';
                case 33: return '4';
                case 34: return '5';
                case 35: return '6';
                case 36: return '7';
                case 37: return '8';
                case 38: return '9';
                case 39: return '0';

                // ========= Control / navigation =========
                case 40: return 0x0D; // RETURN
                case 41: return 0x1B; // ESCAPE
                case 42: return 0x08; // BACKSPACE
                case 43: return 0x09; // TAB
                case 44: return 0x20; // SPACE

                // ========= Punctuation row (layout dependent => OEM keys) =========
                case 45: return 0xBD; // -  VK_OEM_MINUS
                case 46: return 0xBB; // =  VK_OEM_PLUS
                case 47: return 0xDB; // [  VK_OEM_4
                case 48: return 0xDD; // ]  VK_OEM_6
                case 49: return 0xDC; // \  VK_OEM_5
                case 50: return 0xDC; // NonUS # and ~  (best effort)
                case 51: return 0xBA; // ;  VK_OEM_1
                case 52: return 0xDE; // '  VK_OEM_7
                case 53: return 0xC0; // `  VK_OEM_3
                case 54: return 0xBC; // ,  VK_OEM_COMMA
                case 55: return 0xBE; // .  VK_OEM_PERIOD
                case 56: return 0xBF; // /  VK_OEM_2

                // ========= Caps / function keys =========
                case 57: return 0x14; // CAPSLOCK

                case 58: return 0x70; // F1
                case 59: return 0x71; // F2
                case 60: return 0x72; // F3
                case 61: return 0x73; // F4
                case 62: return 0x74; // F5
                case 63: return 0x75; // F6
                case 64: return 0x76; // F7
                case 65: return 0x77; // F8
                case 66: return 0x78; // F9
                case 67: return 0x79; // F10
                case 68: return 0x7A; // F11
                case 69: return 0x7B; // F12

                // ========= Print / Scroll / Pause =========
                case 70: return 0x2C; // PRINTSCREEN
                case 71: return 0x91; // SCROLLLOCK
                case 72: return 0x13; // PAUSE

                // ========= Insert / Home / PgUp etc =========
                case 73: return 0x2D; // INSERT
                case 74: return 0x24; // HOME
                case 75: return 0x21; // PAGEUP
                case 76: return 0x2E; // DELETE
                case 77: return 0x23; // END
                case 78: return 0x22; // PAGEDOWN

                // ========= Arrows =========
                case 79: return 0x27; // RIGHT
                case 80: return 0x25; // LEFT
                case 81: return 0x28; // DOWN
                case 82: return 0x26; // UP

                // ========= NumLock + keypad =========
                case 83: return 0x90; // NUMLOCKCLEAR => VK_NUMLOCK

                case 84: return 0x6F; // KP_DIVIDE
                case 85: return 0x6A; // KP_MULTIPLY
                case 86: return 0x6D; // KP_MINUS
                case 87: return 0x6B; // KP_PLUS
                case 88: return 0x0D; // KP_ENTER => VK_RETURN (note: not VK_SEPARATOR)
                case 89: return 0x61; // KP_1
                case 90: return 0x62; // KP_2
                case 91: return 0x63; // KP_3
                case 92: return 0x64; // KP_4
                case 93: return 0x65; // KP_5
                case 94: return 0x66; // KP_6
                case 95: return 0x67; // KP_7
                case 96: return 0x68; // KP_8
                case 97: return 0x69; // KP_9
                case 98: return 0x60; // KP_0
                case 99: return 0x6E; // KP_PERIOD

                // ========= Modifiers =========
                case 224: return 0xA2; // LCTRL  VK_LCONTROL
                case 225: return 0xA0; // LSHIFT VK_LSHIFT
                case 226: return 0xA4; // LALT   VK_LMENU
                case 227: return 0x5B; // LGUI   VK_LWIN
                case 228: return 0xA3; // RCTRL  VK_RCONTROL
                case 229: return 0xA1; // RSHIFT VK_RSHIFT
                case 230: return 0xA5; // RALT   VK_RMENU
                case 231: return 0x5C; // RGUI   VK_RWIN

                // ========= Menu key =========
                case 118: return 0x5D; // APPLICATION / MENU

                // ========= Media keys (best effort) =========
                // SDL has many more; these are common VK mappings:
                case 127: return 0xAD; // MUTE
                case 128: return 0xAE; // VOLUMEUP
                case 129: return 0xAF; // VOLUMEDOWN

                default:
                    return 0;
            }
        }
    }
}
