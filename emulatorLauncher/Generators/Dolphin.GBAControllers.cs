﻿using System;
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
        private static void GenerateControllerConfig_gba(string path, InputKeyMapping anyMapping, Dictionary<string, string> anyReverseAxes)
        {
            //string path = Program.AppConfig.GetFullPath("dolphin");
            string iniFile = Path.Combine(path, "User", "Config", "GBA.ini");

            SimpleLogger.Instance.Info("[INFO] Writing GBA controller configuration in : " + iniFile);

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
                    string gcpad = "GBA" + pad.PlayerIndex;
                    if (gcpad != null)
                        ini.ClearSection(gcpad);

                    if (pad.Config == null)
                        continue;

                    string guid = pad.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant();
                    var prod = pad.ProductID;
                    string gamecubepad = "gamecubepad" + (pad.PlayerIndex - 1);

                    if (gcAdapters.ContainsKey(guid) && Program.SystemConfig[gamecubepad] != "12" && Program.SystemConfig[gamecubepad] != "13")
                    {
                        ConfigureGCAdapter(gcpad, guid, pad, ini);
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
                            tech = "XInput";
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

                    bool xboxLayout = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "xbox";
                    bool revertXY = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "reverse_ab";

                    if (isNintendo && pad.PlayerIndex == 1)
                    {
                        string tempMapA = anyMapping[InputKey.a];
                        string tempMapB = anyMapping[InputKey.b];

                        if (tempMapB != null)
                            anyMapping[InputKey.a] = tempMapB;
                        if (tempMapA != null)
                            anyMapping[InputKey.b] = tempMapA;
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

                            if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                mapping = pad.GetXInputMapping(x.Key, true);
                                if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                    ini.WriteValue(gcpad, reverseAxis, xInputMapping[mapping]);
                            }
                        }
                        else if (forceSDL)
                        {
                            var input = pad.Config[x.Key];

                            if (input == null)
                                continue;

                            if (input.Type == "button")
                            {
                                if (input.Id == 0) // invert A&B
                                    ini.WriteValue(gcpad, value, "`Button 1`");
                                else if (input.Id == 1) // invert A&B
                                    ini.WriteValue(gcpad, value, "`Button 0`");
                                else
                                    ini.WriteValue(gcpad, value, "`Button " + input.Id.ToString() + "`");
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

                                ini.WriteValue(gcpad, value, axisValue(input, false));

                                if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                                    ini.WriteValue(gcpad, reverseAxis, axisValue(input, true));
                            }

                            else if (input.Type == "hat")
                            {
                                Int64 pid = input.Value;
                                switch (pid)
                                {
                                    case 1:
                                        ini.WriteValue(gcpad, value, "`Hat " + input.Id.ToString() + " N`");
                                        break;
                                    case 2:
                                        ini.WriteValue(gcpad, value, "`Hat " + input.Id.ToString() + " E`");
                                        break;
                                    case 4:
                                        ini.WriteValue(gcpad, value, "`Hat " + input.Id.ToString() + " S`");
                                        break;
                                    case 8:
                                        ini.WriteValue(gcpad, value, "`Hat " + input.Id.ToString() + " W`");
                                        break;
                                }
                            }
                        }

                        else // SDL
                        {
                            var input = pad.GetSdlMapping(x.Key);

                            if (input == null)
                                continue;

                            if (input.Type == "button")
                            {
                                if (input.Id == 0) // invert A&B
                                    ini.WriteValue(gcpad, value, "`Button 1`");
                                else if (input.Id == 1) // invert A&B
                                    ini.WriteValue(gcpad, value, "`Button 0`");
                                else
                                    ini.WriteValue(gcpad, value, "`Button " + input.Id.ToString() + "`");
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

                                ini.WriteValue(gcpad, value, axisValue(input, false));

                                if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                                    ini.WriteValue(gcpad, reverseAxis, axisValue(input, true));
                            }
                        }
                    }

                    SimpleLogger.Instance.Info("[INFO] Assigned controller " + pad.DevicePath + " to player : " + pad.PlayerIndex.ToString());
                }

                ini.Save();
            }
        }

        static readonly InputKeyMapping gbaMapping = new InputKeyMapping()
        {
            { InputKey.b,               "Buttons/B" },
            { InputKey.a,               "Buttons/A" },
            { InputKey.pageup,          "Buttons/L" },
            { InputKey.pagedown,        "Buttons/R" },
            { InputKey.select,          "Buttons/SELECT"},
            { InputKey.start,           "Buttons/START" },
            { InputKey.up,              "D-Pad/Up" },
            { InputKey.down,            "D-Pad/Down" },
            { InputKey.left,            "D-Pad/Left" },
            { InputKey.right,           "D-Pad/Right" },
            { InputKey.hotkey,          "Buttons/Hotkey" },
        };
    }
}
