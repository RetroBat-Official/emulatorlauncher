using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace EmulatorLauncher
{
    partial class DolphinControllers
    {
        private static bool _triforcectrl = false;

        private static void GenerateControllerConfig_triforce(string path, TriforceGame triforceGame, string region, bool crediar)
        {
            //string path = Program.AppConfig.GetFullPath("dolphin");
            string iniFile = Path.Combine(path, "User", "Config", "GCPadNew.ini");

            var anyMapping = triforceMapping;

            if (Program.SystemConfig.isOptSet("triforce_mapping") && !string.IsNullOrEmpty(Program.SystemConfig["triforce_mapping"]))
            {
                string mappingKey = Program.SystemConfig["triforce_mapping"];

                if (mappingKeys.ContainsKey(mappingKey))
                    anyMapping = mappingKeys[mappingKey];
            }
            else if (triforceGame != null && triforceGame.InputProfile != null)
            {
                anyMapping = triforceGame.InputProfile;
            }

            if (_emulator == "dolphin" && _emulator != "dolphin-emu")
            {
                anyMapping[InputKey.l3] = "Triforce/Service";
                anyMapping[InputKey.r3] = "Triforce/Test";
                anyMapping[InputKey.select] = "Triforce/Coin";
            }

            SimpleLogger.Instance.Info("[INFO] Triforce: Writing controller configuration in : " + iniFile);

            bool vs4axis = Program.SystemConfig.isOptSet("triforce_mapping") && Program.SystemConfig["triforce_mapping"] == "vs4";
            Dictionary<string, string> anyReverseAxes = vs4axis ? vs4ReverseAxes : gamecubeReverseAxes;

            bool forceSDL = false;
            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                forceSDL = true;

            int nsamepad = 0;

            Dictionary<string, int> double_pads = new Dictionary<string, int>();

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

                    string guid = pad.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant();
                    var prod = pad.ProductID;
                    string gamecubepad = "gamecubepad" + (pad.PlayerIndex - 1);

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

                        deviceName = pad.Name ?? "";

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

                    foreach (var x in anyMapping)
                    {
                        string value = x.Value;

                        if (pad.Config.Type == "keyboard")
                        {
                            if (x.Key == InputKey.a)
                                value = "Buttons/A";
                            else if (x.Key == InputKey.b)
                                value = "Buttons/B";
                            else if (x.Key == InputKey.x)
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

                            if (value.StartsWith("Triforce/"))
                            {
                                if (value == "Triforce/Service")
                                    name = string.IsNullOrEmpty(name) ? "`2`" : name + "|`2`";
                                else if (value == "Triforce/Test")
                                    name = string.IsNullOrEmpty(name) ? "`1`" : name + "|`1`";
                                else if (value == "Triforce/Coin")
                                    name = string.IsNullOrEmpty(name) ? "`5`" : name + "|`5`";
                            }

                            ini.WriteValue(gcpad, value, name);
                        }
                        else if (tech == "XInput")
                        {
                            var mapping = pad.GetXInputMapping(x.Key);
                            if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                            {
                                string name = xInputMapping[mapping];

                                if (value.StartsWith("Triforce/"))
                                {
                                    if (value == "Triforce/Service")
                                        name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:2`" : name + "|`DInput/0/Keyboard Mouse:2`";
                                    else if (value == "Triforce/Test")
                                        name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:1`" : name + "|`DInput/0/Keyboard Mouse:1`";
                                    else if (value == "Triforce/Coin")
                                        name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:5`" : name + "|`DInput/0/Keyboard Mouse:5`";
                                }

                                ini.WriteValue(gcpad, value, name);
                            }

                            if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                mapping = pad.GetXInputMapping(x.Key, true);
                                if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                    ini.WriteValue(gcpad, reverseAxis, xInputMapping[mapping]);
                            }
                        }

                        else
                        {
                            var input = pad.GetSdlMapping(x.Key);
                            if (xinputAsSdl)
                                input = pad.Config[x.Key];

                            if (input == null)
                                continue;

                            if (input.Type == "button")
                            {
                                if (input.Id == 0) // invert A&B
                                    ini.WriteValue(gcpad, value, "`Button 1`");
                                else if (input.Id == 1) // invert A&B
                                    ini.WriteValue(gcpad, value, "`Button 0`");
                                else
                                {
                                    string name = "`Button " + input.Id.ToString() + "`";
                                    
                                    if (value.StartsWith("Triforce/"))
                                    {
                                        if (value == "Triforce/Service")
                                            name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:2`" : name + "|`DInput/0/Keyboard Mouse:2`";
                                        else if (value == "Triforce/Test")
                                            name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:1`" : name + "|`DInput/0/Keyboard Mouse:1`";
                                        else if (value == "Triforce/Coin")
                                            name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:5`" : name + "|`DInput/0/Keyboard Mouse:5`";
                                    }

                                    ini.WriteValue(gcpad, value, name);
                                }
                            }
                            else if (input.Type == "axis")
                            {
                                Func<Input, bool, string> axisValue = (inp, revertAxis) =>
                                {
                                    string axis = "`Axis ";

                                    if (inp.Id == 0 || inp.Id == 1 || inp.Id == 2 || inp.Id == 3)
                                        axis += inp.Id;

                                    if ((!revertAxis && inp.Value > 0) || (revertAxis && inp.Value < 0))
                                        axis += "+";
                                    else
                                        axis += "-";

                                    if (inp.Id == 4 || inp.Id == 5)
                                        axis = "`Full Axis " + inp.Id + "+";

                                    return axis + "`";
                                };

                                string name = axisValue(input, false);

                                if (value.StartsWith("Triforce/"))
                                {
                                    if (value == "Triforce/Service")
                                        name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:2`" : name + "|`DInput/0/Keyboard Mouse:2`";
                                    else if (value == "Triforce/Test")
                                        name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:1`" : name + "|`DInput/0/Keyboard Mouse:1`";
                                    else if (value == "Triforce/Coin")
                                        name = string.IsNullOrEmpty(name) ? "`DInput/0/Keyboard Mouse:5`" : name + "|`DInput/0/Keyboard Mouse:5`";
                                }

                                ini.WriteValue(gcpad, value, name);

                                if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                                    ini.WriteValue(gcpad, reverseAxis, axisValue(input, true));
                            }

                            // For Crediar Dolphin : Z button is used to access test menu, do not map it with R1
                            if (crediar)
                                ini.WriteValue(gcpad, "Buttons/Z", "@(`Button 7`+`Button 8`)");
                        }
                    }

                    ini.WriteValue(gcpad, "Main Stick/Modifier/Range", "50.0");
                    ini.WriteValue(gcpad, "C-Stick/Modifier/Range", "50.0");

                    // DEADZONE
                    if (Program.SystemConfig.isOptSet("triforce_deadzone") && !string.IsNullOrEmpty(Program.SystemConfig["triforce_deadzone"]))
                    {
                        string deadzone = Program.SystemConfig["triforce_deadzone"].ToIntegerString() + ".0";
                        ini.WriteValue(gcpad, "Main Stick/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "C-Stick/Dead Zone", deadzone);
                    }
                    else
                    {
                        ini.WriteValue(gcpad, "Main Stick/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "C-Stick/Dead Zone", "15.0");
                    }

                    // SENSITIVITY
                    if (Program.SystemConfig.isOptSet("triforce_sensitivity") && !string.IsNullOrEmpty(Program.SystemConfig["triforce_sensitivity"]))
                    {
                        string sensitivity = Program.SystemConfig["triforce_sensitivity"].ToIntegerString() + ".0";
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

                    SimpleLogger.Instance.Info("[INFO] Assigned controller " + pad.DevicePath + " to player : " + pad.PlayerIndex.ToString());
                }

                ini.Save();
            }

            _triforcectrl = true;

            // Reset hotkeys
            string hotkeyini = Path.Combine(path, "User", "Config", "Hotkeys.ini");
            ResetHotkeysToDefault(hotkeyini);
        }

        public static readonly InputKeyMapping triforceMapping = new InputKeyMapping()
        {
            { InputKey.l2,              "Triggers/L-Analog" },
            { InputKey.r2,              "Triggers/R-Analog"},
            { InputKey.y,               "Buttons/Y" },
            { InputKey.b,               "Buttons/B" },
            { InputKey.select,          "Buttons/X" },
            { InputKey.a,               "Buttons/A" },
            { InputKey.start,           "Buttons/Start" },
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
            { InputKey.r3,              "Buttons/Z" }
        };

        public static readonly InputKeyMapping mkMapping = new InputKeyMapping()
        {
            { InputKey.l2,              "Triggers/L-Analog" },
            { InputKey.r2,              "Triggers/R-Analog"},
            { InputKey.b,               "Buttons/B" }, // cancel
            { InputKey.select,          "Buttons/X" },// coin
            { InputKey.a,               "Buttons/A" }, // item
            { InputKey.start,           "Buttons/Start" }, // start
            { InputKey.l2,              "Triggers/L" }, // brake
            { InputKey.r2,              "Triggers/R" }, // gaz
            { InputKey.joystick1left,   "Main Stick/Left" }, // turn
            { InputKey.r3,              "Buttons/Z" }
        };

        public static readonly InputKeyMapping vsMapping = new InputKeyMapping()
        {
            { InputKey.b,               "Triggers/R" }, // short pass
            { InputKey.y,               "Buttons/A" }, // long pass
            { InputKey.select,          "Buttons/X" },
            { InputKey.a,               "Triggers/L" }, // shoot
            { InputKey.x,               "Buttons/B" }, // dash
            { InputKey.start,           "Buttons/Start" }, // start
            { InputKey.r3,              "Buttons/Z" },
            { InputKey.joystick1up,     "Main Stick/Left" }, // movement
            { InputKey.joystick1left,   "Main Stick/Down" }, // movement
            { InputKey.right,           "D-Pad/Up" },
            { InputKey.up,              "D-Pad/Left" },
            { InputKey.down,            "D-Pad/Right" }
        };

        public static readonly InputKeyMapping vs2002Mapping = new InputKeyMapping()
        {
            { InputKey.b,               "Triggers/R" }, // short pass
            { InputKey.a,               "Buttons/A" }, // long pass
            { InputKey.select,          "Buttons/X" },
            { InputKey.y,               "Triggers/L" }, // shoot
            { InputKey.start,           "Buttons/Start" }, // start
            { InputKey.r3,              "Buttons/Z" },
            { InputKey.up,              "D-Pad/Up" },
            { InputKey.down,            "D-Pad/Down" },
            { InputKey.left,            "D-Pad/Left" },
            { InputKey.right,           "D-Pad/Right" }
        };

        public static readonly InputKeyMapping fzeroMapping = new InputKeyMapping()
        {
            { InputKey.l2,              "Triggers/L-Analog" },
            { InputKey.r2,              "Triggers/R-Analog"},
            { InputKey.pageup,          "Buttons/A" }, // paddle
            { InputKey.pagedown,        "Buttons/B" }, // paddle
            { InputKey.y,               "Buttons/Y" }, // boost
            { InputKey.select,          "Buttons/X" },
            { InputKey.start,           "Buttons/Start" }, // start
            { InputKey.up,              "D-Pad/Up" }, // view 1
            { InputKey.down,            "D-Pad/Down" }, // view 2
            { InputKey.left,            "D-Pad/Left" }, // view 3
            { InputKey.right,           "D-Pad/Right" }, // view 4
            { InputKey.joystick1up,     "Main Stick/Up" },
            { InputKey.joystick1left,   "Main Stick/Left" }, // turn
            { InputKey.r3,              "Buttons/Z" }
        };

        public static readonly Dictionary<string, string> vs4ReverseAxes = new Dictionary<string, string>()
        {
            { "Main Stick/Down",   "Main Stick/Up" },
            { "Main Stick/Left",   "Main Stick/Right" }
        };

        static readonly Dictionary<string, InputKeyMapping> mappingKeys = new Dictionary<string, InputKeyMapping>()
        {
            { "mk",         mkMapping       },
            { "fzero",      fzeroMapping    },
            { "vs2002",     vs2002Mapping   },
            { "vs4",        vsMapping       },
            { "standard",   triforceMapping }
        };
    }
}
