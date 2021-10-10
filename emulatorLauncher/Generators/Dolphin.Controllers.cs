using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using emulatorLauncher.Tools;
using System.Globalization;

namespace emulatorLauncher
{
    class DolphinControllers
    {
        public static bool WriteControllersConfig(string path, string system, string rom)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return false;
            
            if (system == "wii")
            {
                if (Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig.getOptBoolean("emulatedwiimotes"))
                {
                    generateControllerConfig_emulatedwiimotes(path, rom);
                    //removeControllerConfig_gamecube(); // because pads will already be used as emulated wiimotes
                    return true;
                }
                else
                    generateControllerConfig_realwiimotes(path, "WiimoteNew.ini", "Wiimote");

                generateControllerConfig_gamecube(path, rom, gamecubeWiiMapping);
            }
            else
                generateControllerConfig_gamecube(path, rom, gamecubeMapping);
            return true;            
        }

        class InputKeyMapping : List<KeyValuePair<InputKey, string>>
        {
            public InputKeyMapping() { }

            public InputKeyMapping(InputKeyMapping source)
            {
                this.AddRange(source);
            }

            public void Add(InputKey key, string value)
            {
                this.Add(new KeyValuePair<InputKey, string>(key, value));
            }

            public string this[InputKey key]
            {
                get
                {
                    return this.Where(i => i.Key == key).Select(i => i.Value).FirstOrDefault();
                }
                set
                {
                    var idx = this.FindIndex(i => i.Key == key);
                    if (idx >= 0)
                        this[idx] = new KeyValuePair<InputKey,string>(key, value);
                    else 
                        this.Add(key, value);
                }
            }
        }

        static InputKeyMapping gamecubeMapping = new InputKeyMapping()
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

        static InputKeyMapping gamecubeWiiMapping = new InputKeyMapping()
        { 
            { InputKey.l3,              "Main Stick/Modifier"},
            { InputKey.r3,              "C-Stick/Modifier"},
            { InputKey.l2,              "Triggers/L-Analog" },    
            { InputKey.r2,              "Triggers/R-Analog"},
            { InputKey.y,               "Buttons/Y" },  
            { InputKey.b,               "Buttons/B" },
            { InputKey.x,               "Buttons/X" },  
            { InputKey.a,               "Buttons/A" },
            { InputKey.select,          "Buttons/Z" },  
            { InputKey.start,           "Buttons/Start" },
            { InputKey.pageup,          "Triggers/L" }, 
            { InputKey.pagedown,        "Triggers/R" },
            { InputKey.up,              "D-Pad/Up" }, 
            { InputKey.down,            "D-Pad/Down" }, 
            { InputKey.left,            "D-Pad/Left" }, 
            { InputKey.right,           "D-Pad/Right" },
            { InputKey.joystick1up,     "Main Stick/Up" }, 
            { InputKey.joystick1left,   "Main Stick/Left" },
            { InputKey.joystick2up,     "C-Stick/Up" },    
            { InputKey.joystick2left,   "C-Stick/Left"}          
        };

        static Dictionary<string, string> gamecubeReverseAxes = new Dictionary<string,string>()
        {
            { "Main Stick/Up",   "Main Stick/Down" },
            { "Main Stick/Left", "Main Stick/Right" },
            { "C-Stick/Up",      "C-Stick/Down" },
            { "C-Stick/Left",    "C-Stick/Right" }
        };

        // if joystick1up is missing on the pad, use up instead
        static Dictionary<string, string> gamecubeReplacements = new Dictionary<string, string>()
        {
            { "joystick1up", "up" },
            { "joystick1left", "left" },
            { "joystick1down", "down" },
            { "joystick1right", "right" }
        };

        static InputKeyMapping _wiiMapping = new InputKeyMapping 
        {
            { InputKey.x,               "Buttons/2" },  
            { InputKey.b,               "Buttons/A" },
            { InputKey.y,               "Buttons/1" },  
            { InputKey.a,               "Buttons/B" },
            { InputKey.pageup,          "Buttons/-" },  
            { InputKey.pagedown,        "Buttons/+" },
            { InputKey.select,          "Buttons/Home" },
            { InputKey.up,              "D-Pad/Up" }, 
            { InputKey.down,            "D-Pad/Down" }, 
            { InputKey.left,            "D-Pad/Left" }, 
            { InputKey.right,           "D-Pad/Right" },
            { InputKey.joystick1up,     "IR/Up" }, 
            { InputKey.joystick1left,   "IR/Left" },
            { InputKey.joystick2up,     "Tilt/Forward" }, 
            { InputKey.joystick2left,   "Tilt/Left" }
        };

        static Dictionary<string, string> wiiReverseAxes = new Dictionary<string,string>()
        {
            { "IR/Up",      "IR/Down"},
            { "IR/Left",    "IR/Right"},
            { "Swing/Up",   "Swing/Down"},
            { "Swing/Left", "Swing/Right"},
            { "Tilt/Left",  "Tilt/Right"},
            { "Tilt/Forward", "Tilt/Backward"},
            { "Nunchuk/Stick/Up" ,  "Nunchuk/Stick/Down"},
            { "Nunchuk/Stick/Left", "Nunchuk/Stick/Right"},
            { "Classic/Right Stick/Up" , "Classic/Right Stick/Down"},
            { "Classic/Right Stick/Left" , "Classic/Right Stick/Right"},
            { "Classic/Left Stick/Up" , "Classic/Left Stick/Down"},
            { "Classic/Left Stick/Left" , "Classic/Left Stick/Right" }
        };

        private static void generateControllerConfig_emulatedwiimotes(string path, string rom)
        {
            var extraOptions = new Dictionary<string, string>();
            extraOptions["Source"] = "1";

            var wiiMapping = new InputKeyMapping(_wiiMapping);

            if (rom.Contains(".side.") && Program.SystemConfig["controller_mode"] != "disabled" && Program.SystemConfig["controller_mode"] != "cc")
            {
                extraOptions["Options/Sideways Wiimote"] = "1";
                wiiMapping[InputKey.x] = "Buttons/B";
                wiiMapping[InputKey.y] = "Buttons/A";
                wiiMapping[InputKey.a] = "Buttons/2";
                wiiMapping[InputKey.b] = "Buttons/1";
                //wiiMapping[InputKey.l2] = "Shake/X";
                //wiiMapping[InputKey.l2] = "Shake/Y";
                wiiMapping[InputKey.l2] = "Shake/Z";
            }

            // i: infrared, s: swing, t: tilt, n: nunchuk
            // 12 possible combinations : is si / it ti / in ni / st ts / sn ns / tn nt

            // i
            if (rom.Contains(".is.") || rom.Contains(".it.") || rom.Contains(".in.") ||
                (Program.SystemConfig.isOptSet("controller_mode") && Program.SystemConfig["controller_mode"] != "disabled" && Program.SystemConfig["controller_mode"] != "in" && Program.SystemConfig["controller_mode"] != "cc"))
            {
                wiiMapping[InputKey.joystick1up] = "IR/Up";
                wiiMapping[InputKey.joystick1left] = "IR/Left";
            }

            if (rom.Contains(".si.") || rom.Contains(".ti.") || rom.Contains(".ni.") || Program.SystemConfig["controller_mode"] == "in")
            {
                wiiMapping[InputKey.joystick2up] = "IR/Up";
                wiiMapping[InputKey.joystick2left] = "IR/Left";
            }

            // s
            if (rom.Contains(".si.") || rom.Contains(".st.") || rom.Contains(".sn."))
            {
                wiiMapping[InputKey.joystick1up]   = "Swing/Up";
                wiiMapping[InputKey.joystick1left] = "Swing/Left";
            }

            if (rom.Contains(".is.") || rom.Contains(".ts.") || rom.Contains(".ns.") || Program.SystemConfig["controller_mode"] == "is")
            {
                wiiMapping[InputKey.joystick2up]   = "Swing/Up";
                wiiMapping[InputKey.joystick2left] = "Swing/Left";
            }

            // t
            if (rom.Contains(".ti.") || rom.Contains(".ts.") || rom.Contains(".tn."))
            {
                wiiMapping[InputKey.joystick2up] = "Tilt/Forward";
                wiiMapping[InputKey.joystick2left] = "Tilt/Left";
            }

            if (rom.Contains(".it.") || rom.Contains(".st.") || rom.Contains(".nt.") || Program.SystemConfig["controller_mode"] == "it")
            {
                wiiMapping[InputKey.joystick2up] = "Tilt/Forward";
                wiiMapping[InputKey.joystick2left] = "Tilt/Left";
            }

            // n
            if (rom.Contains(".ni.") || rom.Contains(".ns.") || rom.Contains(".nt.") || Program.SystemConfig["controller_mode"] == "in")
            {
                extraOptions["Extension"] = "Nunchuk";
                wiiMapping[InputKey.l2] = "Nunchuk/Buttons/C";
                wiiMapping[InputKey.r2] = "Nunchuk/Buttons/Z";
                wiiMapping[InputKey.joystick1up] = "Nunchuk/Stick/Up";
                wiiMapping[InputKey.joystick1left] = "Nunchuk/Stick/Left";
            }

            if (rom.Contains(".in.") || rom.Contains(".sn.") || rom.Contains(".tn."))
            {
                extraOptions["Extension"] = "Nunchuk";
                wiiMapping[InputKey.l2] = "Nunchuk/Buttons/C";
                wiiMapping[InputKey.r2] = "Nunchuk/Buttons/Z";
                wiiMapping[InputKey.joystick2up] = "Nunchuk/Stick/Up";
                wiiMapping[InputKey.joystick2left] = "Nunchuk/Stick/Left";
            }

            // cc : Classic Controller Settings
            if (rom.Contains(".cc.") || Program.SystemConfig["controller_mode"] == "cc")
            {
                extraOptions["Extension"] = "Classic";
                wiiMapping[InputKey.x] = "Classic/Buttons/X";
                wiiMapping[InputKey.y] = "Classic/Buttons/Y";
                wiiMapping[InputKey.b] = "Classic/Buttons/B";
                wiiMapping[InputKey.a] = "Classic/Buttons/A";
                wiiMapping[InputKey.select] = "Classic/Buttons/-";
                wiiMapping[InputKey.start] = "Classic/Buttons/+";
                wiiMapping[InputKey.pageup] = "Classic/Triggers/L";
                wiiMapping[InputKey.pagedown] = "Classic/Triggers/R";
                wiiMapping[InputKey.l2] = "Classic/Buttons/ZL";
                wiiMapping[InputKey.r2] = "Classic/Buttons/ZR";
                wiiMapping[InputKey.up] = "Classic/D-Pad/Up";
                wiiMapping[InputKey.down] = "Classic/D-Pad/Down";
                wiiMapping[InputKey.left] = "Classic/D-Pad/Left";
                wiiMapping[InputKey.right] = "Classic/D-Pad/Right";
                wiiMapping[InputKey.joystick1up] = "Classic/Left Stick/Up";
                wiiMapping[InputKey.joystick1left] = "Classic/Left Stick/Left";
                wiiMapping[InputKey.joystick2up] = "Classic/Right Stick/Up";
                wiiMapping[InputKey.joystick2left] = "Classic/Right Stick/Left";
            }

            generateControllerConfig_any(path, "WiimoteNew.ini", "Wiimote", wiiMapping, wiiReverseAxes, null, extraOptions);
        }

        private static void generateControllerConfig_gamecube(string path, string rom, InputKeyMapping anyMapping)
        {
            generateControllerConfig_any(path, "GCPadNew.ini", "GCPad", anyMapping, gamecubeReverseAxes, gamecubeReplacements);        
        }

        static Dictionary<XINPUTMAPPING, string> xInputMapping = new Dictionary<XINPUTMAPPING, string>()
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
        /*
        private static void removeControllerConfig_gamecube()
        {
            string path = Program.AppConfig.GetFullPath("dolphin");

            string iniFile = Path.Combine(path, "User", "Config", "GCPadNew.ini");
            if (!File.Exists(iniFile))
                return;

            File.Delete(iniFile);
        }*/

        private static void generateControllerConfig_realwiimotes(string path, string filename, string anyDefKey)
        {
            string iniFile = Path.Combine(path, "User", "Config", filename);

            using (IniFile ini = new IniFile(iniFile, true))
            {
                for (int i = 0; i < 5; i++)
                {
                    ini.ClearSection("[" + anyDefKey + i.ToString() + "]");
                    ini.WriteValue("[" + anyDefKey + i.ToString() + "]", "Source", "2");
                }

                ini.Save();
            }
        }

        private static void generateControllerConfig_any(string path, string filename, string anyDefKey, InputKeyMapping anyMapping, Dictionary<string, string> anyReverseAxes, Dictionary<string, string> anyReplacements, Dictionary<string, string> extraOptions = null)
        {
            //string path = Program.AppConfig.GetFullPath("dolphin");
            string iniFile = Path.Combine(path, "User", "Config", filename);

            int nsamepad = 0;

            Dictionary<string, int> double_pads = new Dictionary<string,int>();

            using (IniFile ini = new IniFile(iniFile, true))
            {
                foreach (var pad in Program.Controllers)
                {
                    string gcpad = anyDefKey + pad.PlayerIndex;
                    ini.ClearSection(gcpad);

                    if (pad.Config == null)
                        continue;
                  
                    // SIDevice0 = 7 -> Keyb GCKeyNew.ini
                    // SIDevice1 = 6 -> controlleur standard GCPadNew.ini

                    string tech = "XInput";
                    string deviceName = "Gamepad";

                    if (pad.Config.Type == "keyboard")
                    {
                        tech = "DInput";
                        deviceName = "Keyboard Mouse";                        
                    } 
                    else if (!pad.Config.IsXInputDevice())
                    {
                        var di = pad.Config.GetDirectInputInfo();
                        if (di == null)
                            continue;
                        
                        tech = "DInput";
                        deviceName = di.Name;
                    }
             
                    if (double_pads.ContainsKey(tech + "/" + deviceName))
                        nsamepad = double_pads[tech + "/" + deviceName];
                    else 
                        nsamepad = 0;

                    double_pads[tech + "/" + deviceName] = nsamepad + 1;

                    ini.WriteValue(gcpad, "Device", tech + "/" + nsamepad.ToString() + "/" + deviceName);

                    if (extraOptions != null)
                       foreach(var xtra in extraOptions)
                           ini.WriteValue(gcpad, xtra.Key, xtra.Value);

                    foreach (var x in anyMapping)
                    {
                        string keyName = x.Value;

                        if (pad.Config.Type == "keyboard")
                        {
                            var value = x.Value;

                            if (x.Key == InputKey.a)
                                value = "Buttons/A";
                            else if (x.Key == InputKey.b)
                                value = "Buttons/B";
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
                            var mapping = pad.Config.GetXInputMapping(x.Key);
                            if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                ini.WriteValue(gcpad, x.Value, xInputMapping[mapping]);

                            string reverseAxis;
                            if (anyReverseAxes.TryGetValue(x.Value, out reverseAxis))
                            {
                                mapping = pad.Config.GetXInputMapping(x.Key, true);
                                if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                    ini.WriteValue(gcpad, reverseAxis, xInputMapping[mapping]);
                            }
                        }
                        else
                        {
                            var input = pad.Config[x.Key];
                            if (input == null)
                                continue;

                            if (input.Type == "button")
                            {
                                if (input.Id == 0) // Invert A & B
                                    ini.WriteValue(gcpad, x.Value, "`Button 1`");
                                else if (input.Id == 1) // Invert A & B
                                    ini.WriteValue(gcpad, x.Value, "`Button 0`");
                                else
                                    ini.WriteValue(gcpad, x.Value, "`Button "+ input.Id.ToString() + "`");
                            }
                            else if (input.Type == "hat")
                            {
                                string hat = "`Hat " + input.Id + " N`";

                                if (input.Value == 2) // SDL_HAT_RIGHT
                                    hat = "`Hat " + input.Id + " E`";
                                else if (input.Value == 4) // SDL_HAT_DOWN
                                    hat = "`Hat " + input.Id + " S`";
                                else if (input.Value == 8) // SDL_HAT_LEFT
                                    hat = "`Hat " + input.Id + " W`";

                                ini.WriteValue(gcpad, x.Value, hat);
                            }
                            else if (input.Type == "axis")
                            {
                                Func<Input, bool, string> axisValue = (inp, revertAxis) =>
                                {                                     
                                    string axis = "`Axis ";

                                    if (inp.Id == 2 || inp.Id == 5)
                                        axis += "Z";
                                    else if (inp.Id == 0 || inp.Id == 3)
                                        axis += "X";
                                    else
                                        axis += "Y";

                                    if (inp.Id == 3 || inp.Id == 4)
                                        axis += "r";

                                    if (inp.Id == 5)
                                        revertAxis = !revertAxis;

                                    if ((!revertAxis && inp.Value > 0) || (revertAxis && inp.Value < 0))                                            
                                        axis += "+";
                                    else 
                                        axis += "-";

                                    return axis+"`";
                                };
                                
                                ini.WriteValue(gcpad, x.Value, axisValue(input, false));

                                string reverseAxis;
                                if (anyReverseAxes.TryGetValue(x.Value, out reverseAxis))
                                    ini.WriteValue(gcpad, reverseAxis, axisValue(input, true));
                            }
                        }
                    }

                    if (tech == "XInput")
                    {
//                        ini.WriteValue(gcpad, "Main Stick/Modifier", "`Thumb L`");
  //                      ini.WriteValue(gcpad, "C-Stick/Modifier" , "`Thumb R`");
                        ini.WriteValue(gcpad, "Main Stick/Dead Zone", "5.0000000000000000");
                        ini.WriteValue(gcpad, "C-Stick/Dead Zone", "5.0000000000000000");
                        ini.WriteValue(gcpad, "Rumble/Motor", "`Motor L``Motor R`");
                    }
                }

                ini.Save();
            }
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
    }
}
