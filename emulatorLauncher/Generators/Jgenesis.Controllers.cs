using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private void SetupControllers(IniFile ini, string jgenSystem)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (jgenSystem == "sega_cd")
                jgenSystem = "genesis";

            int maxPad = 1;
            if (systemMaxPad.ContainsKey(jgenSystem))
                maxPad = systemMaxPad[jgenSystem];

            CleanupKBSections(ini, jgenSystem);
            WriteKBHotkeys(ini);

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(ini, controller, controller.PlayerIndex, jgenSystem);

            if (Controllers.Count > 0 && Controllers.Count < maxPad)
                CleanupJoySections(ini, Controllers.Count, maxPad, jgenSystem);
        }

        private void ConfigureInput(IniFile ini, Controller controller, int playerIndex, string jgenSystem)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, playerIndex, jgenSystem);
            else
                ConfigureJoystick(ini,controller, playerIndex, jgenSystem);
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex, string jgenSystem)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            if (jgenSystem != "game_boy")
                ini.ClearSection("inputs." + jgenSystem + "_joystick.p" + playerIndex);
            else
                ini.ClearSection("inputs." + jgenSystem + "_joystick");

            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;
            string name = ctrl.SdlController != null ? ctrl.SdlController.Name : ctrl.Name;
            bool isXInput = ctrl.IsXInputDevice;
            bool revertButtons = SystemConfig.isOptSet("jgen_revertbuttons") && SystemConfig.getOptBoolean("jgen_revertbuttons");

            Dictionary<string, InputKey> mapping = null;

            switch (jgenSystem)
            {
                case "game_boy":
                    mapping = gbMapping;
                    break;
                case "genesis":
                    mapping = mdMapping;
                    break;
                case "nes":
                    mapping = nesMapping;
                    break;
                case "smsgg":
                    mapping = smsMapping;
                    break;
                case "snes":
                    mapping = snesMapping;
                    break;
            }

            if (mapping == null)
                return;

            foreach (var kv in mapping)
            {
                string iniSection = "inputs." + jgenSystem + "_joystick.p" + playerIndex + "." + kv.Key;
                if (jgenSystem == "game_boy")
                    iniSection = "inputs.gb_joystick." + kv.Key;

                string inputType = GetInputType(ctrl, kv.Value);
                if (inputType == null)
                    continue;

                var inputInfo = GetInputInfo(ctrl, kv.Value);
                if (inputInfo.Count == 0)
                    continue;

                string idx = inputInfo.Keys.First();
                switch (inputType)
                {
                    case "Button":
                        ini.WriteValue(iniSection, "name", "\"" + name + "\"");
                        ini.WriteValue(iniSection, "idx", index.ToString());
                        ini.WriteValue(iniSection, "type", "\"" + inputType + "\"");
                        ini.WriteValue(iniSection, idx, inputInfo[idx]);
                        break;
                    case "Axis":
                        ini.WriteValue(iniSection, "name", "\"" + name + "\"");
                        ini.WriteValue(iniSection, "idx", index.ToString());
                        ini.WriteValue(iniSection, "type", "\"" + inputType + "\"");
                        ini.WriteValue(iniSection, "axis_idx", idx);
                        ini.WriteValue(iniSection, "direction", "\"" + inputInfo[idx] + "\"");
                        break;
                    case "Hat":
                        ini.WriteValue(iniSection, "name", "\"" + name + "\"");
                        ini.WriteValue(iniSection, "idx", index.ToString());
                        ini.WriteValue(iniSection, "type", "\"" + inputType + "\"");
                        ini.WriteValue(iniSection, "hat_idx", idx);
                        ini.WriteValue(iniSection, "direction", "\"" + inputInfo[idx] + "\"");
                        break;
                }
            }

            if (playerIndex == 1)
                ConfigureDefaultKeyboard(ini, jgenSystem);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerIndex, string jgenSystem)
        {
            if (keyboard == null)
                return;

            Action<string, string, InputKey> WriteKeyboardMapping = (v, w, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                    ini.WriteValue(v, w, "\"" + SdlToKeyCode(a.Id) + "\"");
            };

            string section = inputKbSection[jgenSystem];

            if (jgenSystem != "game_boy")
                section += "p" + playerIndex;

            Dictionary<string, InputKey> mapping = null;

            switch (jgenSystem)
            {
                case "game_boy":
                    mapping = gbMapping;
                    break;
                case "genesis":
                    mapping = mdMapping;
                    break;
                case "nes":
                    mapping = nesMapping;
                    break;
                case "smsgg":
                    mapping = smsMapping;
                    break;
                case "snes":
                    mapping = snesMapping;
                    break;
            }

            if (mapping == null)
                return;

            if (jgenSystem == "smsgg")
            {
                WriteKeyboardMapping("inputs.smsgg_keyboard", "pause", InputKey.start);
            }

            foreach (var kv in mapping)
                WriteKeyboardMapping(section, kv.Key, kv.Value);
        }

        private void ConfigureDefaultKeyboard(IniFile ini, string jgenSystem)
        {
            string section = inputKbSection[jgenSystem];
            int playerIndex = 1;
            
            if (jgenSystem != "game_boy")
                section += "p" + playerIndex;

            Dictionary<string, InputKey> mapping = null;

            switch (jgenSystem)
            {
                case "game_boy":
                    mapping = gbMapping;
                    break;
                case "genesis":
                    mapping = mdMapping;
                    break;
                case "nes":
                    mapping = nesMapping;
                    break;
                case "smsgg":
                    mapping = smsMapping;
                    break;
                case "snes":
                    mapping = snesMapping;
                    break;
            }

            if (mapping == null)
                return;

            if (jgenSystem == "smsgg")
            {
                ini.WriteValue("inputs.smsgg_keyboard", "pause", "\"Return\"");
            }

            foreach (var kv in mapping)
            {
                ini.WriteValue(section, kv.Key, "\"" + defaultKB[kv.Value] + "\"");
            }
        }

        private void WriteKBHotkeys(IniFile ini)
        {
            string iniSection = "inputs.hotkeys";

            ini.WriteValue(iniSection, "quit", "\"Escape\"");
            ini.WriteValue(iniSection, "toggle_fullscreen", "\"Tab\"");
            ini.WriteValue(iniSection, "save_state", "\"F1\"");
            ini.WriteValue(iniSection, "load_state", "\"F2\"");
            ini.WriteValue(iniSection, "soft_reset", "\"F3\"");
            ini.WriteValue(iniSection, "hard_reset", "\"F4\"");
            ini.WriteValue(iniSection, "pause", "\"F6\"");
            ini.WriteValue(iniSection, "step_frame", "\"F11\"");
            ini.WriteValue(iniSection, "fast_forward", "\"F9\"");
            ini.WriteValue(iniSection, "rewind", "\"F8\"");
            ini.WriteValue(iniSection, "open_debugger", "\"F12\"");
        }

        private void CleanupKBSections(IniFile ini, string jgenSystem)
        {
            int maxPad = systemMaxPad.ContainsKey(jgenSystem) ? systemMaxPad[jgenSystem] : 1;

            if (maxPad == 1)
                return;

            string kbSection = inputKbSection[jgenSystem];
            Dictionary<string, InputKey> mapping = null;

            switch (jgenSystem)
            {
                case "game_boy":
                    mapping = gbMapping;
                    break;
                case "genesis":
                    mapping = mdMapping;
                    break;
                case "nes":
                    mapping = nesMapping;
                    break;
                case "smsgg":
                    mapping = smsMapping;
                    break;
                case "snes":
                    mapping = snesMapping;
                    break;
            }

            if (mapping != null)
            {
                for (int i = 2; i <= maxPad; i++)
                {
                    if (jgenSystem != "game_boy")
                        kbSection += "p" + i;
                    
                    foreach (var kv in mapping)
                        ini.Remove(kbSection, kv.Key);
                }
            }
        }

        private void CleanupJoySections(IniFile ini, int count, int maxPad, string jgenSystem)
        {
            Dictionary<string, InputKey> mapping = null;

            switch (jgenSystem)
            {
                case "game_boy":
                    mapping = gbMapping;
                    break;
                case "genesis":
                    mapping = mdMapping;
                    break;
                case "nes":
                    mapping = nesMapping;
                    break;
                case "smsgg":
                    mapping = smsMapping;
                    break;
                case "snes":
                    mapping = snesMapping;
                    break;
            }

            if (mapping == null)
                return;

            for (int i = count + 1; i <= maxPad; i++)
            {
                foreach (var kv in mapping)
                {
                    if (jgenSystem == "game_boy")
                        continue;
                    else
                        ini.ClearSection("inputs." + jgenSystem + "_joystick.p" + i + "." + kv.Key);
                }

                if (jgenSystem == "game_boy")
                    ini.WriteValue("inputs.gb_joystick", null, null);
                else
                    ini.WriteValue("inputs." + jgenSystem + "_joystick.p" + i, null, null);
            } 
        }

        static readonly Dictionary<string, int> systemMaxPad = new Dictionary<string, int>()
        {
            { "smsgg", 2 },
            { "genesis", 2 },
            { "game_boy", 1 },
            { "nes", 2 },
            { "snes", 2 }
        };

        static readonly Dictionary<string, string> inputKbSection = new Dictionary<string, string>()
        {
            { "smsgg", "inputs.smsgg_keyboard." },
            { "genesis", "inputs.genesis_keyboard." },
            { "game_boy", "inputs.gb_keyboard" },
            { "nes", "inputs.nes_keyboard." },
            { "snes", "inputs.snes_keyboard." }
        };

        static readonly Dictionary<string, InputKey> gbMapping = new Dictionary<string, InputKey>()
        {
            { "up", InputKey.up },
            { "left", InputKey.left },
            { "right", InputKey.right },
            { "down", InputKey.down },
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "start", InputKey.start },
            { "select", InputKey.select }
        };

        static readonly Dictionary<string, InputKey> mdMapping = new Dictionary<string, InputKey>()
        {
            { "up", InputKey.up },
            { "left", InputKey.left },
            { "right", InputKey.right },
            { "down", InputKey.down },
            { "a", InputKey.y },
            { "b", InputKey.a },
            { "c", InputKey.b },
            { "x", InputKey.pageup },
            { "y", InputKey.x },
            { "z", InputKey.pagedown },
            { "start", InputKey.start },
            { "mode", InputKey.select }
        };

        static readonly Dictionary<string, InputKey> nesMapping = new Dictionary<string, InputKey>()
        {
            { "up", InputKey.up },
            { "left", InputKey.left },
            { "right", InputKey.right },
            { "down", InputKey.down },
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "start", InputKey.start },
            { "select", InputKey.select }
        };

        static readonly Dictionary<string, InputKey> smsMapping = new Dictionary<string, InputKey>()
        {
            { "up", InputKey.up },
            { "left", InputKey.left },
            { "right", InputKey.right },
            { "down", InputKey.down },
            { "button1", InputKey.a },
            { "button2", InputKey.b },
        };

        static readonly Dictionary<string, InputKey> snesMapping = new Dictionary<string, InputKey>()
        {
            { "up", InputKey.up },
            { "left", InputKey.left },
            { "right", InputKey.right },
            { "down", InputKey.down },
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "x", InputKey.x },
            { "y", InputKey.y },
            { "l", InputKey.pageup },
            { "r", InputKey.pagedown },
            { "start", InputKey.start },
            { "select", InputKey.select }
        };

        static readonly Dictionary<InputKey, string> defaultKB = new Dictionary<InputKey, string>()
        {
            { InputKey.up, "Up" },
            { InputKey.left, "Left" },
            { InputKey.right, "Right" },
            { InputKey.down, "Down" },
            { InputKey.b, "S" },
            { InputKey.a, "A" },
            { InputKey.x, "Q" },
            { InputKey.y, "W" },
            { InputKey.pageup, "D" },
            { InputKey.pagedown, "C" },
            { InputKey.start, "Return" },
            { InputKey.select, "Right Shift" }
        };

        private static Dictionary<string, string> GetInputInfo(Controller c, InputKey key)
        {
            Int64 pid;
            Int64 pvalue;

            Dictionary<string, string> info = new Dictionary<string, string>();

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    info["button_idx"] = pid.ToString();
                    return info;
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    pvalue = input.Value;
                    info[pid.ToString()] = pvalue > 0 ? "Positive" : "Negative";
                    return info;
                }

                if (input.Type == "hat")
                {
                    pid = input.Id;
                    pvalue = input.Value;
                    switch (pvalue)
                    {
                        case 1:
                            info[pid.ToString()] = "Up";
                            return info;
                        case 2:
                            info[pid.ToString()] = "Right";
                            return info;
                        case 4:
                            info[pid.ToString()] = "Down";
                            return info;
                        case 8:
                            info[pid.ToString()] = "Left";
                            return info;
                    }
                }
            }
            return info;
        }

        private static string GetInputType(Controller c, InputKey key)
        {
            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                    return "Button";

                if (input.Type == "axis")
                    return "Axis";

                if (input.Type == "hat")
                    return "Hat";
            }
            return null;
        }

        private static string SdlToKeyCode(long sdlCode)
        {

            //The following list of keys has been verified, ryujinx will not allow wrong string so do not add a key until the description has been tested in the emulator first
            switch (sdlCode)
            {
                case 0x0D: return "Return";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x1B: return "Escape";
                case 0x20: return "Space";
                case 0x21: return "!";
                case 0x24: return "$";
                case 0x28: return "(";
                case 0x29: return ")";
                case 0x2B: return "+";
                case 0x2C: return ",";
                case 0x2D: return "-";
                case 0x2E: return ".";
                case 0x2F: return "/";
                case 0x30: return "0";
                case 0x31: return "1";
                case 0x32: return "2";
                case 0x33: return "3";
                case 0x34: return "4";
                case 0x35: return "5";
                case 0x36: return "6";
                case 0x37: return "7";
                case 0x38: return "8";
                case 0x39: return "9";
                case 0x3B: return ";";
                case 0x3C: return "<";
                case 0x3D: return "=";
                case 0x3F: return ">";
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
                case 0x40000039: return "CapsLock";
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
                case 0x40000053: return "Numlock";
                case 0x40000054: return "Keypad /";
                case 0x40000055: return "Keypad *";
                case 0x40000056: return "Keypad -";
                case 0x40000057: return "Keypad +";
                case 0x40000058: return "Keypad Enter";
                case 0x40000059: return "Keypad 1";
                case 0x4000005A: return "Keypad 2";
                case 0x4000005B: return "Keypad 3";
                case 0x4000005C: return "Keypad 4";
                case 0x4000005D: return "Keypad 5";
                case 0x4000005E: return "Keypad 6";
                case 0x4000005F: return "Keypad 7";
                case 0x40000060: return "Keypad 8";
                case 0x40000061: return "Keypad 9";
                case 0x40000062: return "Keypad 0";
                case 0x40000063: return "Keypad .";
                case 0x40000085: return "Keypad .";
                case 0x400000E0: return "Left Ctrl";
                case 0x400000E1: return "Left Shift";
                case 0x400000E2: return "Left Alt";
                case 0x400000E4: return "Right Ctrl";
                case 0x400000E5: return "Right Shift";
            }
            return "";
        }
    }
}
