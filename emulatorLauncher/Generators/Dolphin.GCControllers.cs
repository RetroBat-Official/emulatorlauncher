using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class DolphinControllers
    {
        private static void GenerateControllerConfig_gc(string path, InputKeyMapping anyMapping)
        {
            //string path = Program.AppConfig.GetFullPath("dolphin");
            string iniFile = Path.Combine(path, "User", "Config", "GCPadNew.ini");

            SimpleLogger.Instance.Info("[INFO] Writing Gamecube controller configuration in : " + iniFile);

            bool forceSDL = false;
            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                forceSDL = true;

            int nsamepad = 0;

            Dictionary<string, int> double_pads = new Dictionary<string, int>();
            Dictionary<string, string> specialHK = null;

            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                foreach (var pad in Program.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                {
                    bool xinputAsSdl = false;
                    bool isNintendo = pad.VendorID == USB_VENDOR.NINTENDO;
                    string gcpad = "GCPad" + pad.PlayerIndex;
                    if (gcpad != null)
                        ini.ClearSection(gcpad);

                    if (pad.Config == null)
                        continue;

                    // SIDevice0 = 7 -> Keyb GCKeyNew.ini
                    // SIDevice1 = 6 -> controlleur standard GCPadNew.ini

                    string guid = pad.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant();
                    var prod = pad.ProductID;
                    string gamecubepad = "gamecubepad" + (pad.PlayerIndex - 1);

                    if (ConfigureGCAdapter(gcpad, gamecubepad, guid, pad, ini, out specialHK))
                    {
                        if (specialHK != null)
                        {
                            foreach (var hk in specialHK)
                            {
                                if (!_gcSpecialHotkeys.ContainsKey(hk.Key))
                                    _gcSpecialHotkeys.Add(hk.Key, hk.Value);
                            }
                        }
                        continue;
                    }

                    string tech = "XInput";
                    string deviceName = "Gamepad";
                    int xIndex = 0;

                    if (pad.Config.Type == "keyboard")
                    {
                        tech = "DInput";
                        deviceName = "Keyboard Mouse";
                    }
                    else if (!pad.IsXInputDevice || forceSDL)
                    {
                        var s = pad.SdlController;
                        if (s == null)
                            continue;

                        tech = "SDL";

                        if (pad.IsXInputDevice)
                        {
                            xinputAsSdl = true;
                        }

                        deviceName = pad.Name != null ? pad.Name : "";

                        string newNamePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                        if (File.Exists(newNamePath))
                        {
                            string newName = SdlJoystickGuid.GetNameFromFile(newNamePath, pad.Guid, "dolphin");

                            if (newName != null)
                                deviceName = newName;
                        }
                    }

                    if (double_pads.ContainsKey(tech + "/" + deviceName))
                        nsamepad = double_pads[tech + "/" + deviceName];
                    else
                        nsamepad = 0;

                    if (pad.PlayerIndex == 1)
                        _p1sdlindex = nsamepad;

                    double_pads[tech + "/" + deviceName] = nsamepad + 1;

                    if (pad.IsXInputDevice)
                        xIndex = pad.XInput != null ? pad.XInput.DeviceIndex : pad.DeviceIndex;

                    if (tech == "XInput" && !xinputAsSdl)
                        ini.WriteValue(gcpad, "Device", tech + "/" + xIndex + "/" + deviceName);
                    else if (xinputAsSdl)
                        ini.WriteValue(gcpad, "Device", "SDL" + "/" + nsamepad.ToString() + "/" + deviceName);
                    else
                        ini.WriteValue(gcpad, "Device", tech + "/" + nsamepad.ToString() + "/" + deviceName);

                    bool positional = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "position";
                    bool xboxLayout = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "xbox";
                    bool revertXY = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "reverse_ab";
                    bool rumble = !Program.SystemConfig.isOptSet("input_rumble") || Program.SystemConfig.getOptBoolean("input_rumble");

                    if (isNintendo && pad.PlayerIndex == 1)
                    {
                        string tempMapA = anyMapping[InputKey.a];
                        string tempMapB = anyMapping[InputKey.b];
                        string tempMapX = anyMapping[InputKey.x];
                        string tempMapY = anyMapping[InputKey.y];

                        if (tempMapB != null)
                            anyMapping[InputKey.a] = tempMapB;
                        if (tempMapA != null)
                            anyMapping[InputKey.b] = tempMapA;
                        if (tempMapY != null)
                            anyMapping[InputKey.x] = tempMapY;
                        if (tempMapX != null)
                            anyMapping[InputKey.y] = tempMapX;
                    }

                    // Microphone
                    if (Program.SystemConfig.isOptSet("dolphin_gcpad_microphone") && Program.SystemConfig.getOptBoolean("dolphin_gcpad_microphone") && pad.PlayerIndex == 2)
                    {
                        anyMapping[InputKey.pageup] = "Microphone/Button";
                    }

                    foreach (var x in anyMapping)
                    {
                        string value = x.Value;

                        if (xboxLayout && reversedButtons.ContainsKey(x.Key))
                        {
                            value = reversedButtons[x.Key];
                            if (isNintendo)
                            {
                                if (value == "Buttons/B")
                                    value = "Buttons/A";
                                else if (value == "Buttons/A")
                                    value = "Buttons/B";
                                else if (value == "Buttons/X")
                                    value = "Buttons/Y";
                                else if (value == "Buttons/Y")
                                    value = "Buttons/X";
                            }
                        }

                        if (revertXY && reversedButtonsXY.ContainsKey(x.Key))
                        {
                            value = reversedButtonsXY[x.Key];
                            if (isNintendo)
                            {
                                if (value == "Buttons/B")
                                    value = "Buttons/A";
                                else if (value == "Buttons/A")
                                    value = "Buttons/B";
                                else if (value == "Buttons/X")
                                    value = "Buttons/Y";
                                else if (value == "Buttons/Y")
                                    value = "Buttons/X";
                            }
                        }

                        if (positional && reversedButtonsRotate.ContainsKey(x.Key))
                        {
                            value = reversedButtonsRotate[x.Key];
                            if (isNintendo)
                            {
                                if (value == "Buttons/B")
                                    value = "Buttons/Y";
                                else if (value == "Buttons/A")
                                    value = "Buttons/X";
                                else if (value == "Buttons/X")
                                    value = "Buttons/A";
                                else if (value == "Buttons/Y")
                                    value = "Buttons/B";
                            }
                        }

                        if (pad.Config.Type == "keyboard")
                        {
                            if (x.Key == InputKey.a && xboxLayout)
                                value = "Buttons/B";
                            else if (x.Key == InputKey.a)
                                value = "Buttons/A";
                            else if (x.Key == InputKey.b && xboxLayout)
                                value = "Buttons/A";
                            else if (x.Key == InputKey.b)
                                value = "Buttons/B";
                            else if (x.Key == InputKey.x && (xboxLayout || revertXY))
                                value = "Buttons/Y";
                            else if (x.Key == InputKey.x)
                                value = "Buttons/X";
                            else if (x.Key == InputKey.y && (xboxLayout || revertXY))
                                value = "Buttons/X";
                            else if (x.Key == InputKey.y)
                                value = "Buttons/Y";
                            else if (x.Key == InputKey.up)
                                value = "Main Stick/Up";
                            else if (x.Key == InputKey.down)
                                value = "Main Stick/Down";
                            else if (x.Key == InputKey.left)
                                value = "Main Stick/Left";
                            else if (x.Key == InputKey.right)
                                value = "Main Stick/Right";

                            if (x.Key == InputKey.joystick1left || x.Key == InputKey.joystick1up)
                                continue;

                            var input = pad.Config[x.Key];
                            if (input == null)
                                continue;

                            var name = ToDolphinKey(input.Id);
                            ini.WriteValue(gcpad, value, name);
                        }
                        else if (tech == "XInput")
                        {
                            var mapping = pad.GetXInputMapping(x.Key);
                            if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                ini.WriteValue(gcpad, value, xInputMapping[mapping]);

                            if (gamecubeReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                mapping = pad.GetXInputMapping(x.Key, true);
                                if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                    ini.WriteValue(gcpad, reverseAxis, xInputMapping[mapping]);
                            }
                        }

                        else // SDL
                        {
                            var input = pad.GetSdlMapping(x.Key);

                            ini.WriteValue(gcpad, value, dolphinSDLMapping[x.Key]);

                            if (gamecubeReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                var revertKey = joyRevertAxis[x.Key];
                                ini.WriteValue(gcpad, reverseAxis, dolphinSDLMapping[revertKey]);
                            }
                        }
                    }

                    ini.WriteValue(gcpad, "Main Stick/Modifier/Range", "50.0");
                    ini.WriteValue(gcpad, "C-Stick/Modifier/Range", "50.0");

                    // DEADZONE
                    if (Program.SystemConfig.isOptSet("dolphin_gcpad_deadzone") && !string.IsNullOrEmpty(Program.SystemConfig["dolphin_gcpad_deadzone"]))
                    {
                        string deadzone = Program.SystemConfig["dolphin_gcpad_deadzone"].ToIntegerString() + ".0";
                        ini.WriteValue(gcpad, "Main Stick/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "C-Stick/Dead Zone", deadzone);
                    }
                    else
                    {
                        ini.WriteValue(gcpad, "Main Stick/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "C-Stick/Dead Zone", "15.0");
                    }

                    // SENSITIVITY
                    if (Program.SystemConfig.isOptSet("dolphin_gcpad_sensitivity") && !string.IsNullOrEmpty(Program.SystemConfig["dolphin_gcpad_sensitivity"]))
                    {
                        string sensitivity = Program.SystemConfig["dolphin_gcpad_sensitivity"].ToIntegerString() + ".0";
                        ini.WriteValue(gcpad, "Main Stick/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "Main Stick/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "Main Stick/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "Main Stick/Right/Range", sensitivity);
                        ini.WriteValue(gcpad, "C-Stick/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "C-Stick/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "C-Stick/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "C-Stick/Right/Range", sensitivity);
                    }
                    else
                    {
                        ini.WriteValue(gcpad, "Main Stick/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "Main Stick/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "Main Stick/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "Main Stick/Right/Range", "100.0");
                        ini.WriteValue(gcpad, "C-Stick/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "C-Stick/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "C-Stick/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "C-Stick/Right/Range", "100.0");
                    }

                    if (tech == "XInput")
                    {
                        ini.WriteValue(gcpad, "Main Stick/Calibration", "100.00 101.96 108.24 109.27 115.00 109.59 106.10 101.96 100.00 101.96 105.22 107.49 117.34 112.43 108.24 101.96 100.00 101.96 108.24 116.11 116.57 116.72 108.24 101.96 100.00 101.96 108.24 109.75 115.91 109.18 107.47 101.96");
                        ini.WriteValue(gcpad, "C-Stick/Calibration", "100.00 101.96 108.24 112.26 122.26 118.12 108.24 101.96 100.00 101.96 108.24 114.92 117.37 115.98 108.24 101.96 100.00 101.96 105.40 112.07 114.52 113.89 104.20 99.64 99.97 101.73 106.63 108.27 103.63 104.40 107.15 101.96");
                    }

                    if (prod == USB_PRODUCT.NINTENDO_SWITCH_PRO)
                    {
                        ini.WriteValue(gcpad, "Main Stick/Calibration", "98.50 101.73 102.04 106.46 104.62 102.21 102.00 100.53 97.00 96.50 99.95 100.08 102.40 99.37 99.60 100.17 99.60 100.14 98.87 100.48 102.45 101.12 100.92 97.92 99.00 99.92 100.83 100.45 102.27 98.45 97.16 97.36");
                        ini.WriteValue(gcpad, "C-Stick/Calibration", "98.19 101.79 101.37 102.32 103.05 101.19 99.56 99.11 98.45 100.60 98.65 100.67 99.85 97.31 97.24 96.36 95.94 97.94 98.17 100.24 99.22 98.10 99.69 98.77 97.14 100.45 99.08 100.13 102.61 101.37 100.55 97.03");
                    }

                    if (prod == USB_PRODUCT.SONY_DS3 ||
                        prod == USB_PRODUCT.SONY_DS4 ||
                        prod == USB_PRODUCT.SONY_DS4_DONGLE ||
                        prod == USB_PRODUCT.SONY_DS4_SLIM ||
                        prod == USB_PRODUCT.SONY_DS5)
                    {
                        ini.WriteValue(gcpad, "Main Stick/Calibration", "100.00 101.96 104.75 107.35 109.13 110.30 105.04 101.96 100.00 101.96 105.65 105.14 105.94 103.89 104.87 101.04 100.00 101.96 107.16 107.49 105.93 103.65 102.31 101.96 100.00 101.96 103.68 108.28 108.05 105.96 103.66 101.48");
                        ini.WriteValue(gcpad, "C-Stick/Calibration", "100.00 101.96 104.31 104.51 105.93 104.41 103.44 101.96 100.00 101.96 104.07 105.45 109.33 107.39 104.91 101.96 100.00 101.96 106.79 107.84 105.66 104.16 102.91 100.38 98.14 101.63 105.29 107.30 106.77 104.73 104.87 100.92");
                    }

                    else
                    {
                        ini.WriteValue(gcpad, "Main Stick/Calibration", "100.00 101.96 104.75 107.35 109.13 110.30 105.04 101.96 100.00 101.96 105.65 105.14 105.94 103.89 104.87 101.04 100.00 101.96 107.16 107.49 105.93 103.65 102.31 101.96 100.00 101.96 103.68 108.28 108.05 105.96 103.66 101.48");
                        ini.WriteValue(gcpad, "C-Stick/Calibration", "100.00 101.96 104.31 104.51 105.93 104.41 103.44 101.96 100.00 101.96 104.07 105.45 109.33 107.39 104.91 101.96 100.00 101.96 106.79 107.84 105.66 104.16 102.91 100.38 98.14 101.63 105.29 107.30 106.77 104.73 104.87 100.92");
                    }

                    // RUMBLE
                    if (rumble)
                    {
                        if (tech == "XInput")
                            ini.WriteValue(gcpad, "Rumble/Motor", "`Motor L`|`Motor R`");
                        else
                            ini.WriteValue(gcpad, "Rumble/Motor", "Motor");
                    }

                    SimpleLogger.Instance.Info("[INFO] Assigned controller " + pad.DevicePath + " to player : " + pad.PlayerIndex.ToString());
                }

                ini.Save();
            }

            // Reset hotkeys
            string hotkeyini = Path.Combine(path, "User", "Config", "Hotkeys.ini");
            if (File.Exists(hotkeyini))
                ResetHotkeysToDefault(hotkeyini, _gcSpecialHotkeys);
        }

        private static void RemoveControllerConfig_gamecube(string path, IniFile ini)
        {
            for (int i = 0; i < 4; i++)
                ini.WriteValue("Core", "SIDevice" + i, "0");
        }

        static readonly InputKeyMapping gamecubeMapping = new InputKeyMapping()
        {
            { InputKey.l3,              "Main Stick/Modifier"},
            { InputKey.r3,              "C-Stick/Modifier"},
            { InputKey.l2,              "Triggers/L-Analog" },
            { InputKey.r2,              "Triggers/R-Analog"},
            { InputKey.y,               "Buttons/Y" },
            { InputKey.b,               "Buttons/B" },
            { InputKey.x,               "Buttons/X" },
            { InputKey.a,               "Buttons/A" },
            { InputKey.start,           "Buttons/Start" },
            { InputKey.pagedown,        "Buttons/Z" },
            { InputKey.l2,              "Triggers/L" },
            { InputKey.r2,              "Triggers/R" },
            { InputKey.up,              "D-Pad/Up" },
            { InputKey.down,            "D-Pad/Down" },
            { InputKey.left,            "D-Pad/Left" },
            { InputKey.right,           "D-Pad/Right" },
            { InputKey.joystick1up,     "Main Stick/Up" },
            { InputKey.joystick1left,   "Main Stick/Left" },
            { InputKey.joystick2up,     "C-Stick/Up" },
            { InputKey.joystick2left,   "C-Stick/Left"},
            { InputKey.hotkey,          "Buttons/Hotkey" },
        };

        static readonly InputKeyMapping reversedButtons = new InputKeyMapping()
        {
            { InputKey.b,               "Buttons/A" },
            { InputKey.a,               "Buttons/B" },
            { InputKey.x,               "Buttons/Y" },
            { InputKey.y,               "Buttons/X" }
        };

        static readonly InputKeyMapping reversedButtonsXY = new InputKeyMapping()
        {
            { InputKey.x,               "Buttons/Y" },
            { InputKey.y,               "Buttons/X" }
        };

        static readonly InputKeyMapping reversedButtonsRotate = new InputKeyMapping()
        {
            { InputKey.b,               "Buttons/A" },
            { InputKey.y,               "Buttons/B" },
            { InputKey.x,               "Buttons/Y" },
            { InputKey.a,               "Buttons/X" }
        };

        static readonly Dictionary<string, string> gamecubeReverseAxes = new Dictionary<string, string>()
        {
            { "Main Stick/Up",   "Main Stick/Down" },
            { "Main Stick/Left", "Main Stick/Right" },
            { "C-Stick/Up",      "C-Stick/Down" },
            { "C-Stick/Left",    "C-Stick/Right" }
        };

        static readonly Dictionary<InputKey, InputKey> joyRevertAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.joystick1up,   InputKey.joystick1down },
            { InputKey.joystick2up,   InputKey.joystick2down },
            { InputKey.joystick1left,   InputKey.joystick1right },
            { InputKey.joystick2left,   InputKey.joystick2right },
        };

        private static bool ConfigureGCAdapter(string gcpad, string gamecubepad, string guid, Controller pad, IniFile ini, out Dictionary<string, string> specialHK)
        {
            specialHK = null;

            if (Program.SystemConfig[gamecubepad] == "12" || Program.SystemConfig[gamecubepad] == "13")
                return false;

            string gcjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "GCControllers.json");
            if (!File.Exists(gcjson))
                return false;

            SimpleLogger.Instance.Info("[Controller] Checking specific Gamecube mapping for controller " + guid);

            bool needGCActivationSwitch = false;
            bool gc_pad = Program.SystemConfig.getOptBoolean("gc_pad");

            try
            {
                var gcControllers = GCController.LoadControllersFromJson(gcjson);

                if (gcControllers == null)
                    return false;

                GCController gcGamepad = GCController.GetGCController("dolphin", guid, gcControllers);

                if (gcGamepad == null)
                {
                    SimpleLogger.Instance.Info("[Controller] No specific Gamecube mapping found for controller " + guid);
                    return false;
                }

                if (gcGamepad.ControllerInfo != null)
                {
                    if (gcGamepad.ControllerInfo.ContainsKey("needActivationSwitch") && gcGamepad.ControllerInfo["needActivationSwitch"] == "true")
                        needGCActivationSwitch = gcGamepad.ControllerInfo["needActivationSwitch"] == "true";

                    if (needGCActivationSwitch && !gc_pad)
                    {
                        SimpleLogger.Instance.Info("[Controller] Specific Gamecube mapping needs to be activated for this controller.");
                        return false;
                    }

                    SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + gcGamepad.Name);

                    if (gcGamepad.Mapping == null)
                    {
                        SimpleLogger.Instance.Info("[Controller] Mapping empty for " + gcGamepad.Name);
                        return false;
                    }

                    ini.WriteValue(gcpad, "Main Stick/Modifier/Range", "50.");
                    ini.WriteValue(gcpad, "C-Stick/Modifier/Range", "50.");

                    string deviceName = gcGamepad.Name;

                    if (deviceName == null)
                    {
                        SimpleLogger.Instance.Info("[Controller] Name missing for " + gcGamepad.Name);
                        return false;
                    }

                    string tech = "SDL";

                    if (gcGamepad.Driver != null)
                        tech = gcGamepad.Driver;

                    int padIndex = Program.Controllers.Where(g => g.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant() == guid).OrderBy(o => o.DeviceIndex).ToList().IndexOf(pad);

                    string device = tech + "/" + padIndex + "/" + deviceName;
                    ini.WriteValue(gcpad, "Device", device);

                    foreach (var button in gcGamepad.Mapping)
                        ini.WriteValue(gcpad, button.Key, button.Value);

                    if (gcpad.EndsWith("1"))
                    {
                        specialHK = gcGamepad.HotKeyMapping;
                        specialHK.Add("Device", device);
                    }

                    return true;
                }
            }
            catch { return false; }

            return false;
        }
    }
}
