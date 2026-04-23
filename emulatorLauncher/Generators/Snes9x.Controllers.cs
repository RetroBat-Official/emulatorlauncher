using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Snes9xGenerator : Generator
    {
        private bool _sindenSoft = false;
        private bool _monoplayer = false;
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Snes9x");

            // clear existing pad sections of file
            for (int i = 1; i <= 8; i++)
            {
                ini.WriteValue("Controls\\Win", "Joypad" + i + ":Enabled", "FALSE");
            }

            var values = ini.EnumerateKeys("Controls\\Win").Where(k => k.StartsWith("Joypad") && k.Contains(":Extra")).ToList();
            foreach (var value in values)
            {
                ini.WriteValue("Controls\\Win", value, "Unassigned");
            }

            int padCount = this.Controllers.Count;

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                var guns = RawLightgun.GetRawLightguns();
                if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
                {
                    Guns.StartSindenSoftware();
                    _sindenSoft = true;
                }

                if (SystemConfig.isOptSet("snes9x_guntype"))
                {
                    string gunType = SystemConfig["snes9x_guntype"];
                    switch (gunType)
                    {
                        case "justifiers":
                            _commandArray.Add("-port2");
                            _commandArray.Add("two-justifiers");
                            break;
                        case "justifier":
                            _commandArray.Add("-port2");
                            _commandArray.Add("justifier");
                            break;
                        case "superscope":
                            _commandArray.Add("-port2");
                            _commandArray.Add("superscope");
                            break;
                    }
                }
            }
            else if (SystemConfig.isOptSet("snes9x_mouse"))
            {
                if (SystemConfig["snes9x_mouse"] == "port1")
                    _commandArray.Add("-port1");
                else if (SystemConfig["snes9x_mouse"] == "port2")
                    _commandArray.Add("-port2");
                _commandArray.Add("mouse1");
            }
            else if (padCount > 4)
            {
                _commandArray.Add("-port1");
                _commandArray.Add("mp5:1234");
                _commandArray.Add("-port2");
                _commandArray.Add("mp5:5678");
            }
            else if (padCount > 2)
            {
                _commandArray.Add("-port1");
                _commandArray.Add("mp5:1234");
            }

            ini.WriteValue("Controls", "UseDirectInput", SystemConfig.getOptBoolean("snes9x_dinput") ? "TRUE" : "FALSE");
            ini.WriteValue("Controls", "AllowMultipleBindings", "TRUE");
            ini.WriteValue("Controls", "MultiBindingMode", "TRUE");

            _monoplayer = this.Controllers.Count(c => !c.IsKeyboard) == 1;

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(8))
                ConfigureInput(ini, controller);

            // Some other stuff for background input
            ini.WriteValue("Controls\\Win", "Input:Background", "OFF");
            ini.WriteValue("Controls\\Win", "Input:BackgroundKeyHotkeys", "ON");

            ConfigureHotkeys(ini);
        }

        private void ConfigureInput(IniFile ini, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config);
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard)
        {
            if (keyboard == null)
                return;

            ini.WriteValue("Controls\\Win", "Joypad1:Enabled", "TRUE");

            return; 
            // TODO
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            // Initializing controller information
            //string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            //SdlToDirectInput controller;
            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DirectInput.DeviceIndex;
            string joyNb = "Joypad" + playerindex;
            bool isxinput = ctrl.IsXInputDevice;
            bool allowdiagonals = false;

            int index2 = index == 0 ? 1 : 0;

            if (SystemConfig.isOptSet("snes9x_allowdiagonals") && SystemConfig.getOptBoolean("snes9x_allowdiagonals"))
                allowdiagonals = true;

            /* Looking for gamecontrollerdb.txt file
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

            // Fetching controller mapping from gamecontrollerdb.txt file
            controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

            if (controller == null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid);
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ": " + guid + " found in gamecontrollerDB file.");

            if (controller.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return;
            }*/

            ini.WriteValue("Controls\\Win", joyNb + ":Enabled", "TRUE");

            ini.WriteValue("Controls\\Win", joyNb + ":Up", "(J" + index + ")POV Up");
            ini.WriteValue("Controls\\Win", joyNb + ":Down", "(J" + index + ")POV Down");
            ini.WriteValue("Controls\\Win", joyNb + ":Left", "(J" + index + ")POV Left");
            ini.WriteValue("Controls\\Win", joyNb + ":Right", "(J" + index + ")POV Right");

            if (SystemConfig.getOptBoolean("snes9x_analog"))
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Up:Extra1", "(J" + index + ")Up");
                ini.WriteValue("Controls\\Win", joyNb + ":Down:Extra1", "(J" + index + ")Down");
                ini.WriteValue("Controls\\Win", joyNb + ":Left:Extra1", "(J" + index + ")Left");
                ini.WriteValue("Controls\\Win", joyNb + ":Right:Extra1", "(J" + index + ")Right");
            }

            if (SystemConfig.getOptBoolean("buttonsInvert"))
            {
                ini.WriteValue("Controls\\Win", joyNb + ":A", GetInputKeyName(index, ctrl, InputKey.a));
                ini.WriteValue("Controls\\Win", joyNb + ":B", GetInputKeyName(index, ctrl, InputKey.b));
                ini.WriteValue("Controls\\Win", joyNb + ":Y", GetInputKeyName(index, ctrl, InputKey.x));
                ini.WriteValue("Controls\\Win", joyNb + ":X", GetInputKeyName(index, ctrl, InputKey.y));
            }
            else
            {
                ini.WriteValue("Controls\\Win", joyNb + ":A", GetInputKeyName(index, ctrl, InputKey.b));
                ini.WriteValue("Controls\\Win", joyNb + ":B", GetInputKeyName(index, ctrl, InputKey.a));
                ini.WriteValue("Controls\\Win", joyNb + ":Y", GetInputKeyName(index, ctrl, InputKey.y));
                ini.WriteValue("Controls\\Win", joyNb + ":X", GetInputKeyName(index, ctrl, InputKey.x));
            }

            ini.WriteValue("Controls\\Win", joyNb + ":L", GetInputKeyName(index, ctrl, InputKey.pageup));
            ini.WriteValue("Controls\\Win", joyNb + ":R", GetInputKeyName(index, ctrl, InputKey.pagedown));
            ini.WriteValue("Controls\\Win", joyNb + ":Start", GetInputKeyName(index, ctrl, InputKey.start));
            ini.WriteValue("Controls\\Win", joyNb + ":Select", GetInputKeyName(index, ctrl, InputKey.select));

            if (allowdiagonals)
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Up", "(J" + index + ")POV Up Left");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Up", "(J" + index + ")POV Up Right");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Down", "(J" + index + ")POV Dn Right");
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Down", "(J" + index + ")POV Dn Left");
            }
            else
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Up", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Up", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Down", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Down", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Up:Extra1", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Up:Extra1", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Down:Extra1", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Down:Extra1", "Unassigned");
            }

            if (_monoplayer)
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Up:Extra2", "(J" + index2 + ")POV Up");
                ini.WriteValue("Controls\\Win", joyNb + ":Down:Extra2", "(J" + index2 + ")POV Down");
                ini.WriteValue("Controls\\Win", joyNb + ":Left:Extra2", "(J" + index2 + ")POV Left");
                ini.WriteValue("Controls\\Win", joyNb + ":Right:Extra2", "(J" + index2 + ")POV Right");
                if (SystemConfig.getOptBoolean("snes9x_analog"))
                {
                    ini.WriteValue("Controls\\Win", joyNb + ":Up:Extra3", "(J" + index2 + ")Up");
                    ini.WriteValue("Controls\\Win", joyNb + ":Down:Extra3", "(J" + index2 + ")Down");
                    ini.WriteValue("Controls\\Win", joyNb + ":Left:Extra3", "(J" + index2 + ")Left");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right:Extra3", "(J" + index2 + ")Right");
                }

                if (SystemConfig.getOptBoolean("buttonsInvert"))
                {
                    ini.WriteValue("Controls\\Win", joyNb + ":A:Extra1", GetInputKeyName(index2, ctrl, InputKey.a));
                    ini.WriteValue("Controls\\Win", joyNb + ":B:Extra1", GetInputKeyName(index2, ctrl, InputKey.b));
                    ini.WriteValue("Controls\\Win", joyNb + ":Y:Extra1", GetInputKeyName(index2, ctrl, InputKey.x));
                    ini.WriteValue("Controls\\Win", joyNb + ":X:Extra1", GetInputKeyName(index2, ctrl, InputKey.y));
                }
                else
                {
                    ini.WriteValue("Controls\\Win", joyNb + ":A:Extra1", GetInputKeyName(index2, ctrl, InputKey.b));
                    ini.WriteValue("Controls\\Win", joyNb + ":B:Extra1", GetInputKeyName(index2, ctrl, InputKey.a));
                    ini.WriteValue("Controls\\Win", joyNb + ":Y:Extra1", GetInputKeyName(index2, ctrl, InputKey.y));
                    ini.WriteValue("Controls\\Win", joyNb + ":X:Extra1", GetInputKeyName(index2, ctrl, InputKey.x));
                }

                ini.WriteValue("Controls\\Win", joyNb + ":L:Extra1", GetInputKeyName(index2, ctrl, InputKey.pageup));
                ini.WriteValue("Controls\\Win", joyNb + ":R:Extra1", GetInputKeyName(index2, ctrl, InputKey.pagedown));
                ini.WriteValue("Controls\\Win", joyNb + ":Start:Extra1", GetInputKeyName(index2, ctrl, InputKey.start));
                ini.WriteValue("Controls\\Win", joyNb + ":Select:Extra1", GetInputKeyName(index2, ctrl, InputKey.select));

                if (allowdiagonals)
                {
                    ini.WriteValue("Controls\\Win", joyNb + ":Left+Up:Extra1", "(J" + index2 + ")POV Up Left");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right+Up:Extra1", "(J" + index2 + ")POV Up Right");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right+Down:Extra1", "(J" + index2 + ")POV Dn Right");
                    ini.WriteValue("Controls\\Win", joyNb + ":Left+Down:Extra1", "(J" + index2 + ")POV Dn Left");
                }
                else
                {
                    ini.WriteValue("Controls\\Win", joyNb + ":Left+Up", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right+Up", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right+Down", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Left+Down", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Left+Up:Extra1", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right+Up:Extra1", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Right+Down:Extra1", "Unassigned");
                    ini.WriteValue("Controls\\Win", joyNb + ":Left+Down:Extra1", "Unassigned");
                }
            }

            // Unassigned keys
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:AutoFire", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:AutoHold", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:TempTurbo", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:ClearAll", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:A", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:B", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:Y", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:X", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:L", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:R", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:Start", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:Select", "Unassigned");

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private string GetDinputMapping(int index, SdlToDirectInput c, string buttonkey, bool isxinput, int plus = 0)
        {
            if (c == null)
                return "Unassigned";

            if (!c.ButtonMappings.ContainsKey(buttonkey) && !buttonkey.StartsWith("diag"))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "Unassigned";
            }

            if (buttonkey.StartsWith("diag_"))
            {
                string [] buttonlist = buttonkey.Split('_');

                if (!c.ButtonMappings.ContainsKey(buttonlist[1]))
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonlist[1] + " in gamecontrollerdb file");
                    return "Unassigned";
                }
                string button1 = c.ButtonMappings[buttonlist[1]];

                if (!c.ButtonMappings.ContainsKey(buttonlist[2]))
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonlist[2] + " in gamecontrollerdb file");
                    return "Unassigned";
                }
                string button2 = c.ButtonMappings[buttonlist[2]];

                if (button1.StartsWith("b"))
                {
                    int button1ID = (button1.Substring(1).ToInteger());
                    int button2ID = (button2.Substring(1).ToInteger());
                    return "(J" + index + ")Button " + button1ID + " " + button2ID;
                }

                else if (button1.StartsWith("h"))
                {
                    int hat1ID = (button1.Substring(3).ToInteger());
                    int hat2ID = (button2.Substring(3).ToInteger());
                    string povIndex = "(J" + index + ")POV ";

                    switch (hat1ID)
                    {
                        case 1:
                            switch (hat2ID)
                            {
                                case 2:
                                    return povIndex + "Up Right";
                                case 8:
                                    return povIndex + "Up Left";
                            }
                            return "Unassigned";
                        case 2:
                            switch (hat2ID)
                            {
                                case 1:
                                    return povIndex + "Up Right";
                                case 4:
                                    return povIndex + "Dn Right";
                            }
                            return "Unassigned";
                        case 4:
                            switch (hat2ID)
                            {
                                case 2:
                                    return povIndex + "Dn Right";
                                case 8:
                                    return povIndex + "Dn Left";
                            }
                            return "Unassigned";
                        case 8:
                            switch (hat2ID)
                            {
                                case 1:
                                    return povIndex + "Up Left";
                                case 4:
                                    return povIndex + "Dn Left";
                            }
                            return "Unassigned";
                    }
                }
            }
            
            if (!c.ButtonMappings.ContainsKey(buttonkey))
                return "Unassigned";
                
            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("-a"))
                plus = -1;

            if (button.StartsWith("+a"))
                plus = 1;

            if (isxinput)
            {
                if (button == "a5")
                    return "(J" + index + ")Z Up";
            }

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());
                return "(J" + index + ")Button " + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "(J" + index + ")POV Up";
                    case 2:
                        return "(J" + index + ")POV Right";
                    case 4:
                        return "(J" + index + ")POV Down";
                    case 8:
                        return "(J" + index + ")POV Left";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                switch (axisID)
                {
                    case 0:
                        if (plus == 1) return "(J" + index + ")Right";
                        else return "(J" + index + ")Left";
                    case 1:
                        if (plus == 1) return "(J" + index + ")Down";
                        else return "(J" + index + ")Up";
                    case 2:
                        if (plus == 1) return "(J" + index + ")Z Up";
                        else return "(J" + index + ")Z Down";
                    case 3:
                        if (plus == 1) return "(J" + index + ")V Down";
                        else return "(J" + index + ")V Up";
                    case 4:
                        if (plus == 1) return "(J" + index + ")U Down";
                        else return "(J" + index + ")U Up";
                    case 5:
                        if (plus == 1) return "(J" + index + ")R Up";
                        else return "(J" + index + ")R Down";
                }
            }

            return "Unassigned";
        }

        private static string GetInputKeyName(int index, Controller c, InputKey key, string diag = null)
        {
            Int64 pid;

            bool isNintendo = c.VendorID == USB_VENDOR.NINTENDO;

            if (isNintendo)
            {
                if (key == InputKey.a)
                    key = InputKey.b;
                else if (key == InputKey.b)
                    key = InputKey.a;
                else if (key == InputKey.x)
                    key = InputKey.y;
                else if (key == InputKey.y)
                    key = InputKey.x;
            }

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    return "(J" + index + ")" + "Button " + pid;
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1:
                            return "(J" + index + ")POV Up";
                        case 2:
                            return "(J" + index + ")POV Right";
                        case 4:
                            return "(J" + index + ")POV Down";
                        case 8:
                            return "(J" + index + ")POV Left";
                    }
                }
            }
            return "Unassigned";
        }

        private void ConfigureHotkeys(IniFile ini)
        {
            ini.WriteValue("Controls", "AllowMultipleHotkeyBindings", "FALSE");
            ini.WriteValue("Controls", "HotkeyMultiBindingMode", "FALSE");

            // First force hotkeys with modifiers
            for (int i = 1; i < 11; i++)
            {
                if (i == 10)
                {
                    string value = "F10";
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SaveSlot0", value);
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SaveSlot0", "Shift");
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Key:LoadSlot0", value);
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:LoadSlot0", "Ctrl");
                }
                else
                {
                    string value = "F" + i.ToString();
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SaveSlot" + i.ToString(), value);
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SaveSlot" + i.ToString(), "Shift");
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Key:LoadSlot" + i.ToString(), value);
                    ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:LoadSlot" + i.ToString(), "Ctrl");
                }
            }

            if (Hotkeys.GetHotKeysFromFile("snes9x", "", out Dictionary<string, HotkeyResult> hotkeys))
            {
                foreach (var h in hotkeys)
                {
                    string key = "Key:" + h.Value.EmulatorKey;
                    string modKey = "Mods:" + h.Value.EmulatorKey;

                    ini.WriteValue("Controls\\Win\\Hotkeys", key, h.Value.EmulatorValue);
                    ini.WriteValue("Controls\\Win\\Hotkeys", modKey, "none");
                }

                _pad2Keyoverride = true;
                return;
            }

            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SlotSave", "F2");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SlotSave", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SlotLoad", "F4");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SlotLoad", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SlotPlus", "F7");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SlotPlus", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SlotMinus", "F6");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SlotMinus", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:SaveScreenShot", "F8");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:SaveScreenShot", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:Rewind", "Backspace");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:Rewind", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:FastForward", "L");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:FastForward", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:Pause", "P");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:Pause", "none");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Key:FrameAdvance", "K");
            ini.WriteValue("Controls\\Win\\Hotkeys", "Mods:FrameAdvance", "none");
        }
    }
}
