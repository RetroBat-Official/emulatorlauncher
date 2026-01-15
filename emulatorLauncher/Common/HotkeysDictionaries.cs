using EmulatorLauncher.Common;
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

        private static readonly Dictionary<string, string> bizhawkKeys = new Dictionary<string, string>()
        {
            { "a", "A" },
            { "b", "B" },
            { "c", "C" },
            { "d", "D" },
            { "e", "E" },
            { "f", "F" },
            { "g", "G" },
            { "h", "H" },
            { "i", "I" },
            { "j", "J" },
            { "k", "K" },
            { "l", "L" },
            { "m", "M" },
            { "n", "N" },
            { "o", "O" },
            { "p", "P" },
            { "q", "Q" },
            { "r", "R" },
            { "s", "S" },
            { "t", "T" },
            { "u", "U" },
            { "v", "V" },
            { "w", "W" },
            { "x", "X" },
            { "y", "Y" },
            { "z", "Z" },
            { "num1", "Number1" },
            { "num2", "Number2" },
            { "num3", "Number3" },
            { "num4", "Number4" },
            { "num5", "Number5" },
            { "num6", "Number6" },
            { "num7", "Number7" },
            { "num8", "Number8" },
            { "num9", "Number9" },
            { "num0", "Number0" },
            { "return", "Enter" },
            { "escape", "Escape" },
            { "backspace", "Backspace" },
            { "tab", "Tab" },
            { "space", "Space" },
            { "minus", "Minus" },
            { "equals", "Equals" },
            { "leftbracket", "LeftBracket" },
            { "rightbracket", "RightBracket" },
            { "backslash", "Backslash" },
            { "semicolon", "Semicolon" },
            { "quote", "Apostrophe" },
            { "backquote", "Backtick" },
            { "comma", "Comma" },
            { "period", "Period" },
            { "slash", "Slash" },
            { "capslock", "CapsLock" },
            { "f1", "F1" },
            { "f2", "F2" },
            { "f3", "F3" },
            { "f4", "F4" },
            { "f5", "F5" },
            { "f6", "F6" },
            { "f7", "F7" },
            { "f8", "F8" },
            { "f9", "F9" },
            { "f10", "F10" },
            { "f11", "F11" },
            { "f12", "F12" },
            { "printscreen", "PrintScreen" },
            { "scrolllock", "ScrollLock" },
            { "pause", "Pause" },
            { "insert", "Insert" },
            { "home", "Home" },
            { "pageup", "PageUp" },
            { "delete", "Delete" },
            { "end", "End" },
            { "pagedown", "PageDown" },
            { "right", "Right" },
            { "left", "Left" },
            { "down", "Down" },
            { "up", "Up" },
            { "numlockclear", "NumLock" },
            { "divide", "KeypadDivide" },
            { "multiply", "KeypadMultiply" },
            { "subtract", "KeypadSubtract" },
            { "add", "KeypadAdd" },
            { "enter", "KeypadEnter" },
            { "keypad1", "Keypad1" },
            { "keypad2", "Keypad2" },
            { "keypad3", "Keypad3" },
            { "keypad4", "Keypad4" },
            { "keypad5", "Keypad5" },
            { "keypad6", "Keypad6" },
            { "keypad7", "Keypad7" },
            { "keypad8", "Keypad8" },
            { "keypad9", "Keypad9" },
            { "keypad10", "Keypad10" },
            { "keypad11", "Keypad11" },
            { "keypad12", "Keypad12" },
            { "keypad0", "Keypad0" },
            { "point", "KeypadDecimal" },
            { "f13", "F13" },
            { "f14", "F14" },
            { "f15", "F15" },
            { "f16", "F16" },
            { "f17", "F17" },
            { "f18", "F18" },
            { "f19", "F19" },
            { "f20", "F20" },
            { "f21", "F21" },
            { "f22", "F22" },
            { "f23", "F23" },
            { "f24", "F24" },
            { "ctrl", "LeftCtrl" },
            { "shift", "LeftShift" },
            { "alt", "LeftAlt" },
            { "rctrl", "RightCtrl" },
            { "rshift", "RightShift" },
            { "ralt", "RightAlt" }
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

        private static readonly Dictionary<string, string> sdlKeycodeToHID = new Dictionary<string, string>()
        {
            { "a", "4" },
            { "b", "5" },
            { "c", "6" },
            { "d", "7" },
            { "e", "8" },
            { "f", "9" },
            { "g", "10" },
            { "h", "11" },
            { "i", "12" },
            { "j", "13" },
            { "k", "14" },
            { "l", "15" },
            { "m", "16" },
            { "n", "17" },
            { "o", "18" },
            { "p", "19" },
            { "q", "20" },
            { "r", "21" },
            { "s", "22" },
            { "t", "23" },
            { "u", "24" },
            { "v", "25" },
            { "w", "26" },
            { "x", "27" },
            { "y", "28" },
            { "z", "29" },
            { "num1", "30" },
            { "num2", "31" },
            { "num3", "32" },
            { "num4", "33" },
            { "num5", "34" },
            { "num6", "35" },
            { "num7", "36" },
            { "num8", "37" },
            { "num9", "38" },
            { "num0", "39" },
            { "return", "40" },
            { "escape", "41" },
            { "backspace", "42" },
            { "tab", "43" },
            { "space", "44" },
            { "minus", "45" },
            { "equals", "46" },
            { "leftbracket", "47" },
            { "rightbracket", "48" },
            { "backslash", "49" },
            { "semicolon", "51" },
            { "quote", "52" },
            { "backquote", "53" },
            { "comma", "54" },
            { "period", "55" },
            { "slash", "56" },
            { "capslock", "57" },
            { "f1", "58" },
            { "f2", "59" },
            { "f3", "60" },
            { "f4", "61" },
            { "f5", "62" },
            { "f6", "63" },
            { "f7", "64" },
            { "f8", "65" },
            { "f9", "66" },
            { "f10", "67" },
            { "f11", "68" },
            { "f12", "69" },
            { "printscreen", "70" },
            { "scrolllock", "71" },
            { "pause", "72" },
            { "insert", "73" },
            { "home", "74" },
            { "pageup", "75" },
            { "delete", "76" },
            { "end", "77" },
            { "pagedown", "78" },
            { "right", "79" },
            { "left", "80" },
            { "down", "81" },
            { "up", "82" },
            { "numlockclear", "83" },
            { "divide", "84" },
            { "multiply", "85" },
            { "subtract", "86" },
            { "add", "87" },
            { "enter", "88" },
            { "keypad1", "89" },
            { "keypad2", "90" },
            { "keypad3", "91" },
            { "keypad4", "92" },
            { "keypad5", "93" },
            { "keypad6", "94" },
            { "keypad7", "95" },
            { "keypad8", "96" },
            { "keypad9", "97" },
            { "keypad0", "98" },
            { "point", "99" },
            { "f13", "104" },
            { "f14", "105" },
            { "f15", "106" },
            { "f16", "107" },
            { "f17", "108" },
            { "f18", "109" },
            { "f19", "110" },
            { "f20", "111" },
            { "f21", "112" },
            { "f22", "113" },
            { "f23", "114" },
            { "f24", "115" },
            { "ctrl", "224" },
            { "shift", "225" },
            { "alt", "226" },
            { "rctrl", "228" },
            { "rshift", "229" },
            { "ralt", "230" }
        };

        private static Dictionary<string, string> padHotkey = new Dictionary<string, string>()
        {
            { "input_menu_toggle", "a" },
            { "input_toggle_fullscreen", "a" },
            { "input_hold_fast_forward", "right" },
            { "input_exit_emulator", "start" },
            //{ "input_pause_toggle", "b" },
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

        private static readonly Dictionary<string, string> emulatorAppName = new Dictionary<string, string>()
        {
            { "ares", "ares" },
            { "bigpemu", "BigPEmu" },
            { "bizhawk", "EmuHawk" },
            { "flycast", "flycast" },
            { "retroarch", "retroarch" },
        };

        private static readonly Dictionary<string, Dictionary<string,string>> EmulatorDic = new Dictionary<string, Dictionary<string, string>>()
        {
            { "ares", AresKeyEnum },
            { "bigpemu", sdlKeyCodeEnum },
            { "bizhawk", bizhawkKeys },
            { "flycast", sdlKeycodeToHID }
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
                    new EmulatorHotkeyInfo("input_exit_emulator", "QuitEmulator", "0x1/0/0;;")                 // escape
                }),

            new EmulatorHotkey("bigpemu", new EmulatorHotkeyInfo[]
                {
                    new EmulatorHotkeyInfo("input_menu_toggle", "menu", "58_59"),                 // F1
                    new EmulatorHotkeyInfo("input_hold_fast_forward", "ff", "15_38"),             // L
                    new EmulatorHotkeyInfo("input_rewind", "rewind", "42_14"),                    // backspace
                    new EmulatorHotkeyInfo("input_save_state", "savestate", "59_60"),             // F2
                    new EmulatorHotkeyInfo("input_load_state", "loadstate", "61_62"),             // F4
                    new EmulatorHotkeyInfo("input_screenshot", "screenshot", "65_66")            // F8
                }),

            new EmulatorHotkey("bizhawk", new EmulatorHotkeyInfo[]
                {
                    new EmulatorHotkeyInfo("input_pause_toggle", "Pause", "P"),                   // P
                    new EmulatorHotkeyInfo("input_hold_fast_forward", "Fast Forward", "L"),       // L
                    new EmulatorHotkeyInfo("input_rewind", "Rewind", "Backspace"),                // backspace
                    new EmulatorHotkeyInfo("input_save_state", "Quick Save", "F2"),               // F2
                    new EmulatorHotkeyInfo("input_load_state", "Quick Load", "F4"),               // F4
                    new EmulatorHotkeyInfo("input_screenshot", "Screenshot", "F8"),               // F8
                    new EmulatorHotkeyInfo("input_state_slot_decrease", "Previous Slot", "F6"),   // F6
                    new EmulatorHotkeyInfo("input_state_slot_increase", "Next Slot", "F7"),       // F7
                    new EmulatorHotkeyInfo("input_exit_emulator", "Exit Program", "Escape"),      // Escape
                    new EmulatorHotkeyInfo("input_toggle_fullscreen", "Full Screen", "F")         // F
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
                    new EmulatorHotkeyInfo("input_shader_prev", "input_shader_prev", "n")
                }),

            new EmulatorHotkey("flycast", new EmulatorHotkeyInfo[]
                {
                    new EmulatorHotkeyInfo("input_menu_toggle", "btn_menu", "58"),
                    new EmulatorHotkeyInfo("input_hold_fast_forward", "btn_fforward", "15"),
                    new EmulatorHotkeyInfo("input_screenshot", "btn_screenshot", "65"),
                    new EmulatorHotkeyInfo("input_save_state", "btn_quick_save", "59"),
                    new EmulatorHotkeyInfo("input_load_state", "btn_jump_state", "61"),
                    new EmulatorHotkeyInfo("input_exit_emulator", "btn_escape", "41")
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
