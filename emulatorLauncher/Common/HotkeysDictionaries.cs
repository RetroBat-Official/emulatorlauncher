using System;
using System.Collections.Generic;
using System.Globalization;

namespace EmulatorLauncher
{
    public partial class Hotkeys
    {
        #region emulators dictionnaries
        private static readonly Dictionary<string, string> AresKeyEnum = new Dictionary<string, string>()
        {
            { "escape", "0x1/0/0" },
            { "f1", "0x1/0/1" },
            { "f2", "0x1/0/2" },
            { "f3", "0x1/0/3" },
            { "f4", "0x1/0/4" },
            { "f5", "0x1/0/5" },
            { "f6", "0x1/0/6" },
            { "f7", "0x1/0/7" },
            { "f8", "0x1/0/8" },
            { "f9", "0x1/0/9" },
            { "f10", "0x1/0/10" },
            { "f11", "0x1/0/11" },
            { "f12", "0x1/0/12" },
            { "printscreen", "0x1/0/13" },
            { "scrolllock", "0x1/0/14" },
            { "tilde", "0x1/0/15" },
            { "num1", "0x1/0/16" },
            { "num2", "0x1/0/17" },
            { "num3", "0x1/0/18" },
            { "num4", "0x1/0/19" },
            { "num5", "0x1/0/20" },
            { "num6", "0x1/0/21" },
            { "num7", "0x1/0/22" },
            { "num8", "0x1/0/23" },
            { "num9", "0x1/0/24" },
            { "num0", "0x1/0/25" },
            { "dash", "0x1/0/26" },
            { "equals", "0x1/0/27" },
            { "backspace", "0x1/0/28" },
            { "insert", "0x1/0/29" },
            { "del", "0x1/0/30" },
            { "home", "0x1/0/31" },
            { "end", "0x1/0/32" },
            { "pageup", "0x1/0/33" },
            { "pagedown", "0x1/0/34" },
            { "a", "0x1/0/35" },
            { "b", "0x1/0/36" },
            { "c", "0x1/0/37" },
            { "d", "0x1/0/38" },
            { "e", "0x1/0/39" },
            { "f", "0x1/0/40" },
            { "g", "0x1/0/41" },
            { "h", "0x1/0/42" },
            { "i", "0x1/0/43" },
            { "j", "0x1/0/44" },
            { "k", "0x1/0/45" },
            { "l", "0x1/0/46" },
            { "m", "0x1/0/47" },
            { "n", "0x1/0/48" },
            { "o", "0x1/0/49" },
            { "p", "0x1/0/50" },
            { "q", "0x1/0/51" },
            { "r", "0x1/0/52" },
            { "s", "0x1/0/53" },
            { "t", "0x1/0/54" },
            { "u", "0x1/0/55" },
            { "v", "0x1/0/56" },
            { "w", "0x1/0/57" },
            { "x", "0x1/0/58" },
            { "y", "0x1/0/59" },
            { "z", "0x1/0/60" },
            { "leftbracket", "0x1/0/61" },
            { "rightbracket", "0x1/0/62" },
            { "backslash", "0x1/0/63" },
            { "semicolon", "0x1/0/64" },
            { "apostrophe", "0x1/0/65" },
            { "comma", "0x1/0/66" },
            { "period", "0x1/0/67" },
            { "slash", "0x1/0/68" },
            { "keypad1", "0x1/0/69" },
            { "keypad2", "0x1/0/70" },
            { "keypad3", "0x1/0/71" },
            { "keypad4", "0x1/0/72" },
            { "keypad5", "0x1/0/73" },
            { "keypad6", "0x1/0/74" },
            { "keypad7", "0x1/0/75" },
            { "keypad8", "0x1/0/76" },
            { "keypad9", "0x1/0/77" },
            { "keypad0", "0x1/0/78" },
            { "point", "0x1/0/79" },
            { "enter", "0x1/0/80" },
            { "add", "0x1/0/81" },
            { "subtract", "0x1/0/82" },
            { "multiply", "0x1/0/83" },
            { "divide", "0x1/0/84" },
            { "capslock", "0x1/0/85" },
            { "up", "0x1/0/86" },
            { "down", "0x1/0/87" },
            { "left", "0x1/0/88" },
            { "right", "0x1/0/89" },
            { "tab", "0x1/0/90" },
            { "return", "0x1/0/91" },
            { "space", "0x1/0/92" },
            { "shift", "0x1/0/93" },
            { "rshift", "0x1/0/94" },
            { "ctrl", "0x1/0/95" },
            { "rctrl", "0x1/0/96" },
            { "alt", "0x1/0/977" },
            { "ralt", "0x1/0/98" },
        };

        private static readonly Dictionary<string, string> sdlKeyCodeEnum = new Dictionary<string, string>()
        {
            { "escape", "0x1B" },
            { "f1", "0x4000003A" },
            { "f2", "0x4000003B" },
            { "f3", "0x4000003C" },
            { "f4", "0x4000003D" },
            { "f5", "0x4000003E" },
            { "f6", "0x4000003F" },
            { "f7", "0x40000040" },
            { "f8", "0x40000041" },
            { "f9", "0x40000042" },
            { "f10", "0x40000043" },
            { "f11", "0x40000044" },
            { "f12", "0x40000045" },
            { "printscreen", "0x40000046" },
            { "scrolllock", "0x40000047" },
            { "tilde", "0x60" },
            { "num1", "0x31" },
            { "num2", "0x32" },
            { "num3", "0x33" },
            { "num4", "0x34" },
            { "num5", "0x35" },
            { "num6", "0x36" },
            { "num7", "0x37" },
            { "num8", "0x38" },
            { "num9", "0x39" },
            { "num0", "0x30" },
            { "dash", "0x2D" }, // USE MINUS
            { "equals", "0x3D" },
            { "backspace", "0x08" },
            { "insert", "0x40000049" },
            { "del", "0x7F" },
            { "home", "0x4000004A" },
            { "end", "0x4000004D" },
            { "pageup", "0x4000004B" },
            { "pagedown", "0x4000004E" },
            { "a", "0x61" },
            { "b", "0x62" },
            { "c", "0x63" },
            { "d", "0x64" },
            { "e", "0x65" },
            { "f", "0x66" },
            { "g", "0x67" },
            { "h", "0x68" },
            { "i", "0x69" },
            { "j", "0x6A" },
            { "k", "0x6B" },
            { "l", "0x6C" },
            { "m", "0x6D" },
            { "n", "0x6E" },
            { "o", "0x6F" },
            { "p", "0x70" },
            { "q", "0x71" },
            { "r", "0x72" },
            { "s", "0x73" },
            { "t", "0x74" },
            { "u", "0x75" },
            { "v", "0x76" },
            { "w", "0x77" },
            { "x", "0x78" },
            { "y", "0x79" },
            { "z", "0x7A" },
            { "leftbracket", "0x5B" },
            { "rightbracket", "0x5D" },
            { "backslash", "0x5C" },
            { "semicolon", "0x3B" },
            { "apostrophe", "0x27" },
            { "comma", "0x2C" },
            { "period", "0x2E" },
            { "slash", "0x2F" },
            { "keypad1", "0x40000059" },
            { "keypad2", "0x4000005A" },
            { "keypad3", "0x4000005B" },
            { "keypad4", "0x4000005C" },
            { "keypad5", "0x4000005D" },
            { "keypad6", "0x4000005E" },
            { "keypad7", "0x4000005F" },
            { "keypad8", "0x40000060" },
            { "keypad9", "0x40000061" },
            { "keypad0", "0x40000062" },
            { "point", "0x40000063" },
            { "enter", "0x40000058" },
            { "add", "0x40000057" },
            { "subtract", "0x40000056" },
            { "multiply", "0x40000055" },
            { "divide", "0x40000054" },
            { "capslock", "0x40000039" },
            { "up", "0x40000052" },
            { "down", "0x40000051" },
            { "left", "0x40000050" },
            { "right", "0x4000004F" },
            { "tab", "0x09" },
            { "return", "0x0D" },
            { "space", "0x20" },
            { "shift", "0x400000E1" },
            { "rshift", "0x400000E5" },
            { "ctrl", "0x400000E0" },
            { "rctrl", "0x400000E4" },
            { "alt", "0x400000E2" },
            { "ralt", "0x400000E6" },
        };

        private static Dictionary<string, string> padHotkey = new Dictionary<string, string>()
        {
            { "input_menu_toggle", "a" },
            { "input_toggle_fullscreen", "a" },
            { "input_hold_fast_forward", "right" },
            { "input_exit_emulator", "start" },
            { "input_pause_toggle", "b" },
            { "input_load_state", "x" },
            { "input_save_state", "y" },
            { "disk_eject_toggle", "l1" },
            { "input_ai_service", "r1" },
            { "input_disk_prev", "l2" },
            { "input_disk_next", "r2" },
            { "input_screenshot", "r3" },
            { "input_state_slot_decrease", "down" },
            { "input_state_slot_increase", "up" },
            { "input_rewind", "left" },
        };

        private static readonly Dictionary<string, string> raStringToPad2KeyString = new Dictionary<string, string>()
        {
            { "ESCAPE", "ESC" },
        };
        #endregion

        #region classes
        internal class EmulatorHotkey
        {
            public string Emulator { get; set; }
            public EmulatorHotkeyInfo[] EmulatorHotkeys { get; set; }

            public EmulatorHotkey(string emulator, EmulatorHotkeyInfo[] emulatorHotkeys)
            {
                Emulator = emulator;
                EmulatorHotkeys = emulatorHotkeys;
            }
        }

        internal class EmulatorHotkeyInfo
        {
            public string RetroArchHK { get; set; }
            public string EmulatorHK { get; set; }
            public string DefaultValue { get; set; }

            public EmulatorHotkeyInfo(string retroArchHK, string emulatorHK, string defaultValue)
            {
                RetroArchHK = retroArchHK;
                EmulatorHK = emulatorHK;
                DefaultValue = defaultValue;
            }
        }
        #endregion

        #region emulators Hotkey Info
        private static readonly EmulatorHotkey[] EmulatorHotkeys = new EmulatorHotkey[]
        {
            new EmulatorHotkey("ares", new EmulatorHotkeyInfo[]
                {
                    new EmulatorHotkeyInfo("input_toggle_fullscreen", "ToggleFullscreen", "0x1/0/40;;"),        // f
                    new EmulatorHotkeyInfo("input_hold_fast_forward", "FastForward", "0x1/0/46;;"),             // l
                    new EmulatorHotkeyInfo("input_rewind", "Rewind", "0x1/0/28;;"),                             // backspace
                    new EmulatorHotkeyInfo("input_toggle_fast_forward", "ToggleFastForward", "0x1/0/92;;"),     // space
                    new EmulatorHotkeyInfo("input_frame_advance", "FrameAdvance", "0x1/0/45;;"),                // k
                    new EmulatorHotkeyInfo("input_screenshot", "CaptureScreenshot", "0x1/0/8;;"),               // F8
                    new EmulatorHotkeyInfo("input_save_state", "SaveState", "0x1/0/2;;"),                       // F2
                    new EmulatorHotkeyInfo("input_load_state", "LoadState", "0x1/0/4;;"),                       // F4
                    new EmulatorHotkeyInfo("input_state_slot_decrease", "DecrementStateSlot", "0x1/0/6;;"),     // F6
                    new EmulatorHotkeyInfo("input_state_slot_increase", "IncrementStateSlot", "0x1/0/7;;"),     // F7
                    new EmulatorHotkeyInfo("input_pause_toggle", "PauseEmulation", "0x1/0/50;;"),               // p
                    new EmulatorHotkeyInfo("input_exit_emulator", "QuitEmulator", "0x1/0/0;;"),                 // escape
                }),

            new EmulatorHotkey("bigpemu", new EmulatorHotkeyInfo[]
                {
                    new EmulatorHotkeyInfo("input_menu_toggle", "menu", "58_59"),                 // F1
                    new EmulatorHotkeyInfo("input_hold_fast_forward", "ff", "15_38"),             // L
                    new EmulatorHotkeyInfo("input_rewind", "rewind", "42_14"),                    // backspace
                    new EmulatorHotkeyInfo("input_save_state", "savestate", "59_60"),             // F2
                    new EmulatorHotkeyInfo("input_load_state", "loadstate", "61_62"),             // F4
                    new EmulatorHotkeyInfo("input_screenshot", "screenshot", "65_66"),            // F8
                }),

            new EmulatorHotkey("retroarch", new EmulatorHotkeyInfo[]
                {
                    new EmulatorHotkeyInfo("input_menu_toggle", "input_menu_toggle", "f1"),
                    new EmulatorHotkeyInfo("input_desktop_menu_toggle", "input_desktop_menu_toggle", "f5"),
                    new EmulatorHotkeyInfo("input_toggle_fullscreen", "input_toggle_fullscreen", "f"),
                    new EmulatorHotkeyInfo("input_hold_fast_forward", "input_hold_fast_forward", "l"),
                    new EmulatorHotkeyInfo("input_rewind", "input_rewind", "backspace"),
                    new EmulatorHotkeyInfo("input_toggle_fast_forward", "input_toggle_fast_forward", "space"),
                    new EmulatorHotkeyInfo("input_frame_advance", "input_frame_advance", "k"),
                    new EmulatorHotkeyInfo("input_screenshot", "input_screenshot", "f8"),
                    new EmulatorHotkeyInfo("input_save_state", "input_save_state", "f2"),
                    new EmulatorHotkeyInfo("input_load_state", "input_load_state", "f4"),
                    new EmulatorHotkeyInfo("input_state_slot_decrease", "input_state_slot_decrease", "f6"),
                    new EmulatorHotkeyInfo("input_state_slot_increase", "input_state_slot_increase", "f7"),
                    new EmulatorHotkeyInfo("input_pause_toggle", "input_pause_toggle", "p"),
                    new EmulatorHotkeyInfo("input_exit_emulator", "input_exit_emulator", "escape"),
                    new EmulatorHotkeyInfo("input_shader_next", "input_shader_next", "m"),
                    new EmulatorHotkeyInfo("input_shader_prev", "input_shader_prev", "n"),
                }),
        };
        #endregion

        #region public methods
        public static bool TryParseHexLong(string s, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s))
                return false;

            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            return long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
        #endregion
    }

    public class HotkeyResult
    {
        public string RetroArchValue { get; set; }
        public string EmulatorKey { get; set; }
        public string EmulatorValue { get; set; }
    };
}
