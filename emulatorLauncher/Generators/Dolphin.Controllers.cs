using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class DolphinControllers
    {
        private static int _p1sdlindex = 0;
        /// <summary>
        /// Cf. https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/InputCommon/ControllerInterface/SDL/SDL.cpp#L191
        /// </summary>
        private static void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>
            {
                "SDL_HINT_JOYSTICK_HIDAPI_PS4_RUMBLE = 1"
            };

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        public static bool WriteControllersConfig(string path, IniFile ini, string system, string rom, bool triforce, out bool sindenSoft)
        {
            sindenSoft = false;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return false;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Dolphin");

            // Set SID devices (controllers)
            if (!triforce)
                SetSIDDevices(ini, system);

            UpdateSdlControllersWithHints();

            #region wii
            if (system == "wii")
            {
                // Guns
                if (Program.SystemConfig.getOptBoolean("use_guns"))
                {
                    GenerateControllerConfig_wiilightgun(path, sindenSoft);
                    return true;
                }

                // Emulated wiimotes
                else if (Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig.getOptBoolean("emulatedwiimotes"))
                {
                    GenerateControllerConfig_emulatedwiimotes(path, rom);

                    // Remove gamecube mapping because pads will already be used as emulated wiimotes
                    RemoveControllerConfig_gamecube(path, ini);
                    return true;
                }

                // use real wiimote as emulated
                else if (Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig["emulatedwiimotes"] != "0" && Program.SystemConfig["emulatedwiimotes"] != "1")
                {
                    GenerateControllerConfig_realEmulatedwiimotes(path);
                    return true;
                }

                // Real wiimotes (default)
                else
                    GenerateControllerConfig_realwiimotes(path);

                // Additionnaly : configure gamecube pad (except if wii_gamecube is forced to OFF)
                if (Program.SystemConfig.isOptSet("wii_gamecube") && Program.SystemConfig["wii_gamecube"] == "0")
                    RemoveControllerConfig_gamecube(path, ini);

                GenerateControllerConfig_gc(path, gamecubeMapping);
            }
            #endregion

            #region triforce
            // Special mapping for triforce games to remove Z button from R1 (as this is used to access service menu and will be mapped to R3+L3)
            else if (triforce)
                GenerateControllerConfig_triforce(path);
            #endregion

            #region gamecube
            else
            {
                GenerateControllerConfig_gc(path, gamecubeMapping);

                // GBA controller for integrated GBA emulator linked to GameCube
                bool gbaConf = false;
                for (int i = 0; i < 4; i++)
                {
                    string gbaPad = "gamecubepad" + i.ToString();

                    if (Program.SystemConfig[gbaPad] == "13")
                    {
                        gbaConf = true;
                        continue;
                    }
                }

                if (gbaConf)
                    GenerateControllerConfig_gba(path, gbaMapping, gamecubeReverseAxes);
            }
            #endregion

            return true;
        }

        private static void ResetHotkeysToDefault(string iniFile)
        {
            if (Program.Controllers.Count == 0)
                return;

            bool forceSDL = false;
            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                forceSDL = true;

            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                string tech = "XInput";
                string deviceName = "Gamepad";
                int xIndex = 0;
                bool xinputAsSdl = false;

                if (c1 != null && c1.IsXInputDevice)
                    xIndex = c1.XInput != null ? c1.XInput.DeviceIndex : c1.DeviceIndex;

                if (c1.Config.Type == "keyboard")
                {
                    tech = "DInput";
                    deviceName = "Keyboard Mouse";
                }
                else if (!c1.IsXInputDevice || forceSDL)
                {
                    if (c1.IsXInputDevice && forceSDL)
                    {
                        xinputAsSdl = true;
                    }

                    var s = c1.SdlController;
                    if (s != null)
                    {
                        tech = xinputAsSdl? "XInput" : "SDL";
                        deviceName = s.Name;
                    }

                    string newNamePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                    string newName = SdlJoystickGuid.GetNameFromFile(newNamePath, c1.Guid, "dolphin");

                    if (newName != null)
                        deviceName = newName;
                }

                var ssss = "@(" + (GetSDLMappingName(c1, InputKey.hotkey) ?? "") + "&" + (GetSDLMappingName(c1, InputKey.y) ?? "") + ")";

                ini.WriteValue("Hotkeys", "Load State/Load State Slot 1", "F1");
                ini.WriteValue("Hotkeys", "Save State/Save State Slot 1", "@(Shift+F1)");

                if (c1.Config.Type != "keyboard")
                {
                    if (xinputAsSdl)
                        ini.WriteValue("Hotkeys", "Device", "SDL" + "/" + _p1sdlindex + "/" + deviceName);
                    else
                        ini.WriteValue("Hotkeys", "Device", tech + "/" + xIndex + "/" + deviceName);
                    ini.WriteValue("Hotkeys", "General/Toggle Pause", c1.IsXInputDevice? "@(Back+`Button B`)" : "@(Back+`Button E`)");
                    ini.WriteValue("Hotkeys", "General/Toggle Fullscreen", c1.IsXInputDevice ? "@(Back+`Button A`)" : "@(Back+`Button S`)");
                    ini.WriteValue("Hotkeys", "General/Exit", "@(Back+Start)");

                    // SaveStates
                    ini.WriteValue("Hotkeys", "General/Take Screenshot", c1.IsXInputDevice ? "@(Back+`Button X`)" : "@(Back+`Button W`)"); // Use Same value as SaveState....
                    ini.WriteValue("Hotkeys", "Save State/Save to Selected Slot", c1.IsXInputDevice ? "@(Back+`Button X`)" : "@(Back+`Button W`)");
                    ini.WriteValue("Hotkeys", "Load State/Load from Selected Slot", c1.IsXInputDevice ? "@(Back+`Button Y`)" : "@(Back+`Button N`)");
                    ini.WriteValue("Hotkeys", "Other State Hotkeys/Increase Selected State Slot", "@(Back+`Pad N`)");
                    ini.WriteValue("Hotkeys", "Other State Hotkeys/Decrease Selected State Slot", "@(Back+`Pad S`)");

                    ini.WriteValue("Hotkeys", "Emulation Speed/Decrease Emulation Speed", "@(Back+`Shoulder L`)");
                    ini.WriteValue("Hotkeys", "Emulation Speed/Increase Emulation Speed", "@(Back+`Shoulder R`)");
                }
                else        // Keyboard
                {
                    ini.WriteValue("Hotkeys", "Device", "DInput/0/Keyboard Mouse");
                    ini.WriteValue("Hotkeys", "General/Toggle Pause", "`F10`");
                    ini.WriteValue("Hotkeys", "General/Toggle Fullscreen", "@(Alt+RETURN)");
                    ini.WriteValue("Hotkeys", "General/Exit", "ESCAPE");
                    ini.WriteValue("Hotkeys", "General/Take Screenshot", "`F9`");
                    ini.WriteValue("Hotkeys", "General/Eject Disc", "Alt+E");
                    ini.WriteValue("Hotkeys", "General/Change Disc", "Alt+S");
                }
            }
        }

        private static void SetSIDDevices(IniFile ini, string system)
        {
            bool wiiGCPad = system == "wii" && Program.SystemConfig.isOptSet("wii_gamecube") && Program.SystemConfig.getOptBoolean("wii_gamecube");
            for (int i = 0; i < 4; i++)
            {
                var ctl = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == i + 1);
                bool gcPad = (system == "gamecube" && Program.SystemConfig.isOptSet("gamecubepad" + i) && Program.SystemConfig["gamecubepad" + i] == "12");
                bool gbaPad = (system == "gamecube" && Program.SystemConfig.isOptSet("gamecubepad" + i) && Program.SystemConfig["gamecubepad" + i] == "13");

                if (wiiGCPad || gcPad)
                    ini.WriteValue("Core", "SIDevice" + i, "12");

                else if (gbaPad)
                    ini.WriteValue("Core", "SIDevice" + i, "13");

                else if (ctl != null && ctl.Config != null)
                    ini.WriteValue("Core", "SIDevice" + i, "6");

                else
                    ini.WriteValue("Core", "SIDevice" + i, "0");
            }
        }

        private static string GetSDLMappingName(Controller pad, InputKey key)
        {
            var input = pad.GetSdlMapping(key);
            if (input == null)
                return null;

            if (input.Type == "button")
            {
                if (input.Id == 0) // invert A&B
                    return "`Button 1`";

                if (input.Id == 1) // invert A&B
                    return "`Button 0`";

                return "`Button " + input.Id.ToString() + "`";
            }

            if (input.Type == "axis")
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

                return axisValue(input, false);
                /*
                string reverseAxis;
                if (anyReverseAxes.TryGetValue(value, out reverseAxis))
                    ini.WriteValue(gcpad, reverseAxis, axisValue(input, true));*/
            }

            return null;
        }

        private static string ToDolphinKey(long id)
        {
            if (id >= 97 && id <= 122)
            {
                List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                {
                    if (id == 'a')
                        id = 'q';
                    else if (id == 'q')
                        id = 'a';
                    else if (id == 'w')
                        id = 'z';
                    else if (id == 'z')
                        id = 'w';
                }
                return ((char)id).ToString().ToUpper();
            }

            switch (id)
            {
                case 32: return "SPACE";
                case 13:
                case 0x4000009e: return "RETURN"; // Return2

                case 0x400000e1: return "LSHIFT"; // Shift = 
                case 0x400000e0: return "LCONTROL"; // Ctrl = 
                case 0x400000e2: return "LMENU"; // Alt = 

                case 0x4000004b: return "PRIOR"; // PageUp = ,
                case 0x4000004e: return "NEXT"; // PageDown = ,
                case 0x4000004d: return "END"; // End = ,
                case 0x4000004a: return "HOME"; // Home = ,
                case 0x40000050: return "LEFT"; // Left = ,
                case 0x40000052: return "UP"; // Up = ,
                case 0x4000004f: return "RIGHT"; // Right = ,
                case 0x40000051: return "DOWN"; // Down = 0x40000051,

                case 0x40000049: return "INSERT"; // Insert = 0x40000049,
                case 0x0000007f: return "DELETE"; // Delete = 0x0000007f,

                case 0x40000059: return "NUMPAD1";  //KP_1 = 0x40000059,
                case 0X4000005A: return "NUMPAD2";  //KP_2 = 0X4000005A,
                case 0x4000005b: return "NUMPAD3";  // KP_3 = ,
                case 0x4000005c: return "NUMPAD4"; // KP_4 = ,
                case 0x4000005d: return "NUMPAD5"; // KP_5 = ,
                case 0x4000005e: return "NUMPAD6"; // KP_6 = ,
                case 0x4000005f: return "NUMPAD7"; // KP_7 = ,
                case 0x40000060: return "NUMPAD8"; // KP_8 = ,
                case 0x40000061: return "NUMPAD9"; // KP_9 = ,
                case 0x40000062: return "NUMPAD0";  // KP_0 = 0x40000062,
                case 0x40000055: return "MULTIPLY"; // KP_Multiply
                case 0x40000057: return "ADD"; // KP_Plus
                case 0x40000056: return "SUBSTRACT"; // KP_Minus

                case 0x4000003a: return "F1"; // F1
                case 0x4000003b: return "F2"; // F2
                case 0x4000003c: return "F3"; // F3
                case 0x4000003d: return "F4"; // F4
                case 0x4000003e: return "F5"; // F5
                case 0x4000003f: return "F6"; // F6
                case 0x40000040: return "F7"; // F7
                case 0x40000041: return "F8"; // F8
                case 0x40000042: return "F9"; // F9
                case 0x40000043: return "F10"; // F10
                case 0x40000044: return "F11"; // F11
                case 0x40000045: return "F12"; // F12
                case 0x400000e6: return "RMENU"; // RightAlt
                case 0x400000e4: return "RCONTROL"; // RightCtrl
                case 0x400000e5: return "RSHIFT"; // RightShift
                case 0x40000058: return "NUMPADENTER"; // Kp_ENTER
                    /*        
                    KP_Period = 0x40000063,
                    KP_Divide = 0x40000054,

                    NumlockClear = 0x40000053,
                    ScrollLock = 0x40000047,                
                     * //Select = 0x40000077,
                    //PrintScreen = 0x40000046,
                    //LeftGui = 0x400000e3,
                    //RightGui = 0x400000e7,
                    //Application = 0x40000065,            
                    //Gui = 0x400000e3,
                    //Pause = 0x40000048,
                    //Capslock = 0x40000039,

                     */
            }

            return null;
        }

        static readonly Dictionary<XINPUTMAPPING, string> xInputMapping = new Dictionary<XINPUTMAPPING, string>()
        {
            { XINPUTMAPPING.X,                  "`Button Y`" },
            { XINPUTMAPPING.B,                  "`Button A`" },
            { XINPUTMAPPING.Y,                  "`Button X`" },
            { XINPUTMAPPING.A,                  "`Button B`" },
            { XINPUTMAPPING.BACK,               "Back" },
            { XINPUTMAPPING.START,              "Start" },
            { XINPUTMAPPING.LEFTSHOULDER,       "`Shoulder L`" },
            { XINPUTMAPPING.RIGHTSHOULDER,      "`Shoulder R`" },
            { XINPUTMAPPING.DPAD_UP,            "`Pad N`" },
            { XINPUTMAPPING.DPAD_DOWN,          "`Pad S`" },
            { XINPUTMAPPING.DPAD_LEFT,          "`Pad W`" },
            { XINPUTMAPPING.DPAD_RIGHT,         "`Pad E`" },
            { XINPUTMAPPING.LEFTANALOG_UP,      "`Left Y+`" },
            { XINPUTMAPPING.LEFTANALOG_DOWN,    "`Left Y-`" },
            { XINPUTMAPPING.LEFTANALOG_LEFT,    "`Left X-`" },
            { XINPUTMAPPING.LEFTANALOG_RIGHT,   "`Left X+`"},
            { XINPUTMAPPING.RIGHTANALOG_UP,     "`Right Y+`" },
            { XINPUTMAPPING.RIGHTANALOG_DOWN,   "`Right Y-`" },
            { XINPUTMAPPING.RIGHTANALOG_LEFT,   "`Right X-`" },
            { XINPUTMAPPING.RIGHTANALOG_RIGHT,  "`Right X+`"},
            { XINPUTMAPPING.LEFTSTICK,          "`Thumb L`" },
            { XINPUTMAPPING.RIGHTSTICK,         "`Thumb R`" },
            { XINPUTMAPPING.LEFTTRIGGER,        "`Trigger L`" },
            { XINPUTMAPPING.RIGHTTRIGGER,       "`Trigger R`" }
        };
    }
}
