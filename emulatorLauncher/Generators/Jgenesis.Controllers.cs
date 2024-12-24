using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private List<string> noPlayerSystem = new List<string> { "game_boy" };
        private void SetupControllers(IniFileJGenesis ini, string jgenSystem)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for JGenesis");

            if (jgenSystem == "sega_cd" || jgenSystem == "sega_32x")
                jgenSystem = "genesis";

            int maxPad = 1;
            if (systemMaxPad.ContainsKey(jgenSystem))
                maxPad = systemMaxPad[jgenSystem];

            CleanupControls(ini, jgenSystem);
            WriteKBHotkeys(ini);

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(ini, controller, controller.PlayerIndex, jgenSystem);
        }

        private void ConfigureInput(IniFileJGenesis ini, Controller controller, int playerIndex, string jgenSystem)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, playerIndex, jgenSystem);
            else
                ConfigureJoystick(ini,controller, playerIndex, jgenSystem);
        }

        private void ConfigureJoystick(IniFileJGenesis ini, Controller ctrl, int playerIndex, string jgenSystem)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;
            string name = ctrl.SdlController != null ? ctrl.SdlController.Name : ctrl.Name;
            bool isXInput = ctrl.IsXInputDevice;
            bool revertButtons = SystemConfig.isOptSet("jgen_revertbuttons") && SystemConfig.getOptBoolean("jgen_revertbuttons");
            string guid = ctrl.Guid.ToString();

            // Manage specific megadrive controllers from json file
            bool needMDActivationSwitch = false;
            bool mapping2 = false;
            bool md_pad = Program.SystemConfig.getOptBoolean("md_pad");
            if (_mdSystems.Contains(jgenSystem))
            {
                string mdjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mdControllers.json");
                try
                {
                    var mdControllers = MegadriveController.LoadControllersFromJson(mdjson);

                    if (mdControllers != null)
                    {
                        MegadriveController mdGamepad = MegadriveController.GetMDController("jgenesis", guid, mdControllers);

                        if (mdGamepad != null)
                        {
                            if (mdGamepad.ControllerInfo != null)
                            {
                                if (mdGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needMDActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (mdGamepad.ControllerInfo.ContainsKey("mapping2"))
                                    mapping2 = mdGamepad.ControllerInfo["mapping2"] == "yes";

                                if (needMDActivationSwitch && !md_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific MD mapping needs to be activated for this controller.");
                                    goto BypassMDControllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + mdGamepad.Name);

                            if (mdGamepad.Mapping != null)
                            {
                                if (mdGamepad.Driver == "dinput")
                                    name = joy.DeviceName;

                                ini.DeleteSection("input.genesis.mapping_1.p" + playerIndex);

                                if (mapping2)
                                    ini.DeleteSection("input.genesis.mapping_2.p" + playerIndex);

                                foreach (var button in mdGamepad.Mapping)
                                {
                                    string value = button.Value;
                                    string value2 = null;

                                    if (button.Value.Contains("_"))
                                    {
                                        string[] values = value.Split('_');
                                        value = values[0];
                                        value2 = values[1];
                                    }
                                    
                                    string iniSection = "[[input." + jgenSystem + ".mapping_1.p" + playerIndex + "." + button.Key + "]]";
                                    string iniSection2 = null;

                                    if (value2 != null)
                                        iniSection2 = "[[input." + jgenSystem + ".mapping_2.p" + playerIndex + "." + button.Key + "]]";

                                    if (value != null)
                                    {
                                        ini.WriteValue(iniSection, "type", "\"" + "Gamepad" + "\"");
                                        ini.WriteValue(iniSection, "gamepad_idx", index.ToString());
                                        ini.WriteValue(iniSection, "action", "\"" + value + "\"");

                                    }

                                    if (value2 != null)
                                    {
                                        ini.WriteValue(iniSection2, "type", "\"" + "Gamepad" + "\"");
                                        ini.WriteValue(iniSection2, "gamepad_idx", index.ToString());
                                        ini.WriteValue(iniSection2, "action", "\"" + value2 + "\"");
                                    }
                                    
                                }

                                SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());

                                return;
                            }
                            else
                                SimpleLogger.Instance.Info("[INFO] Missing mapping for Megadrive Gamepad, falling back to standard mapping.");
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for megadrive controller.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            BypassMDControllers:

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

            mapping = ConfigureMappingPerSystem(mapping, jgenSystem, playerIndex);

            if (noPlayerSystem.Contains(jgenSystem))
                ini.DeleteSection("input." + jgenSystem + ".mapping_1");
            else
                ini.DeleteSection("input." + jgenSystem + ".mapping_1.p" + playerIndex);

            foreach (var kv in mapping)
            {
                string iniSection = "input." + jgenSystem + ".mapping_1.p" + playerIndex + "." + kv.Key;
                if (noPlayerSystem.Contains(jgenSystem))
                    iniSection = "input." + jgenSystem + ".mapping_1." + kv.Key;

                string inputInfo = GetInputInfo(ctrl, kv.Value);
                if (inputInfo == null)
                    continue;

                ini.WriteValue("[[" + iniSection + "]]", "type", "\"" + "Gamepad" + "\"");
                ini.WriteValue("[[" + iniSection + "]]", "gamepad_idx", index.ToString());
                ini.WriteValue("[[" + iniSection + "]]", "action", "\"" + inputInfo + "\"");
            }

            if (jgenSystem == "smsgg" && playerIndex == 1)
            {
                string iniSection = "[[input.smsgg.mapping_1.pause]]";
                    string inputInfo = GetInputInfo(ctrl, InputKey.start);
                    if (inputInfo != null)
                    {
                        ini.WriteValue(iniSection, "type", "\"" + "Gamepad" + "\"");
                        ini.WriteValue(iniSection, "gamepad_idx", index.ToString());
                        ini.WriteValue(iniSection, "action", "\"" + inputInfo + "\"");
                    }
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private void ConfigureKeyboard(IniFileJGenesis ini, InputConfig keyboard, int playerIndex, string jgenSystem)
        {
            if (keyboard == null)
                return;

            Action<string, string, InputKey> WriteKeyboardMapping = (v, w, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                    ini.WriteValue(v, w, "\"" + SdlToKeyCode(a.Id) + "\"");
            };

            string section = "input." + jgenSystem + ".mapping_1.p1";

            if (noPlayerSystem.Contains(jgenSystem))
                section = "input." + jgenSystem + ".mapping_1";

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

            ini.DeleteSection(section);

            foreach (var kv in mapping)
            {
                string newSection = "[[" + section + "." + kv.Key + "]]";
                ini.WriteValue(newSection, "type", "\"" + "Keyboard" + "\"");
                WriteKeyboardMapping(newSection, "key", kv.Value);
            }

            if (jgenSystem == "smsgg")
            {
                ini.WriteValue("[[input.smsgg.mapping_1.pause]]", "type", "\"" + "Keyboard" + "\"");
                WriteKeyboardMapping("[[input.smsgg.mapping_1.pause]]", "key", InputKey.start);
            }
        }

        private void WriteKBHotkeys(IniFileJGenesis ini)
        {
            ini.DeleteSection("[[input.hotkeys.mapping_1.exit]]");

            foreach (var hotkey in hotkeys)
            {
                ini.WriteValue("[[input.hotkeys.mapping_1." + hotkey.Key + "]]", "type", "\"" + "Keyboard" + "\"");
                ini.WriteValue("[[input.hotkeys.mapping_1." + hotkey.Key + "]]", "key", "\"" + hotkey.Value + "\"");
                ini.DeleteSection("[[input.hotkeys.mapping_2." + hotkey.Key + "]]");
            }

            ini.WriteValue("input.hotkeys.mapping_2", "", null);
        }

        private void CleanupControls(IniFileJGenesis ini, string jgenSystem)
        {
            int maxPad = systemMaxPad.ContainsKey(jgenSystem) ? systemMaxPad[jgenSystem] : 1;

            Dictionary<string, InputKey> mapping = null;

            switch (jgenSystem)
            {
                case "smsgg":
                    mapping = smsMapping;
                    break;
                case "genesis":
                    mapping = mdMapping;
                    break;
                case "nes":
                    mapping = nesMapping;
                    break;
                case "game_boy":
                    mapping = gbMapping;
                    break;
                case "snes":
                    mapping = snesMapping;
                    break;
            }

            if (mapping != null)
            {
                if (noPlayerSystem.Contains(jgenSystem))
                {
                    foreach (var button in mapping)
                    {
                        ini.DeleteSection("[[input." + jgenSystem + ".mapping_1." + button.Key + "]]");
                        ini.DeleteSection("[[input." + jgenSystem + ".mapping_2." + button.Key + "]]");
                        ini.WriteValue("input." + jgenSystem + ".mapping_1", "", null);
                        ini.WriteValue("input." + jgenSystem + ".mapping_2", "", null);
                    }
                }

                else
                {
                    for (int i = 1; i <= maxPad; i++)
                    {
                        foreach (var button in mapping)
                        {
                            ini.DeleteSection("[[input." + jgenSystem + ".mapping_1.p" + i + "." + button.Key + "]]");
                            ini.DeleteSection("[[input." + jgenSystem + ".mapping_2.p" + i + "." + button.Key + "]]");
                            ini.WriteValue("input." + jgenSystem + ".mapping_1.p" + i, "", null);
                            ini.WriteValue("input." + jgenSystem + ".mapping_2.p" + i, "", null);
                        }
                    }

                    if (jgenSystem == "smsgg")
                    {
                        ini.DeleteSection("[[input.smsgg.mapping_1.pause]]");
                        ini.DeleteSection("[[input.smsgg.mapping_2.pause]]");
                    }
                }
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
            { "a", InputKey.a },
            { "b", InputKey.y },
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

        static readonly Dictionary<string, string> hotkeys = new Dictionary<string, string>()
        {
            { "exit", "Escape" },
            { "toggle_fullscreen", "Tab" },
            { "save_state", "F1" },
            { "load_state", "F2" },
            { "next_save_state_slot", "F4" },
            { "prev_save_state_slot", "F3" },
            { "soft_reset", "F8" },
            { "hard_reset", "F9" },
            { "pause", "F5" },
            { "step_frame", "F10" },
            { "fast_forward", "F7" },
            { "rewind", "F6" },
            { "open_debugger", "F11" }
        };

        private string GetInputInfo(Controller c, InputKey key)
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
                    return "Button " + pid;
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    pvalue = input.Value;
                    string direction = pvalue > 0 ? "+" : "-";
                    return "Axis " + pid + " " + direction;
                }

                if (input.Type == "hat")
                {
                    pid = input.Id;
                    pvalue = input.Value;
                    switch (pvalue)
                    {
                        case 1:
                            info[pid.ToString()] = "Up";
                            return "Hat " + pid + " Up";
                        case 2:
                            return "Hat " + pid + " Right";
                        case 4:
                            return "Hat " + pid + " Down";
                        case 8:
                            return "Hat " + pid + " Left";
                    }
                }
            }
            return null;
        }

        private static Dictionary<string, InputKey> ConfigureMappingPerSystem(Dictionary<string, InputKey> mapping, string jGenSystem, int playerIndex)
        {
            Dictionary<string, InputKey> newMapping = mapping;
            if (jGenSystem == "nes")
            {
                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                {
                    newMapping["a"] = InputKey.b;
                    newMapping["b"] = InputKey.a;
                }
            }

            else if (jGenSystem == "genesis")
            {
                string cType = Program.SystemConfig["genesis_p" + playerIndex + "_type"];
                if (Program.SystemConfig.isOptSet("megadrive_control_layout") && cType != "ThreeButton")
                {
                    switch (Program.SystemConfig["megadrive_control_layout"])
                    {
                        case "lr_zc":
                            {
                                newMapping["a"] = InputKey.a;
                                newMapping["b"] = InputKey.b;
                                newMapping["c"] = InputKey.pagedown;
                                newMapping["x"] = InputKey.y;
                                newMapping["z"] = InputKey.pageup;
                            }
                            break;
                        case "lr_yz":
                            {
                                newMapping["x"] = InputKey.pageup;
                                newMapping["y"] = InputKey.x;
                            }
                            break;
                    }
                }
            }

            else if (jGenSystem == "snes")
            {
                if (Program.SystemConfig.getOptBoolean("buttonsInvert"))
                {
                    newMapping["a"] = InputKey.a;
                    newMapping["b"] = InputKey.b;
                    newMapping["x"] = InputKey.y;
                    newMapping["y"] = InputKey.x;
                }
            }

            else if (jGenSystem == "smsgg")
            {
                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                {
                    newMapping["button1"] = InputKey.y;
                    newMapping["button2"] = InputKey.a;
                }
            }
            return newMapping;
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
