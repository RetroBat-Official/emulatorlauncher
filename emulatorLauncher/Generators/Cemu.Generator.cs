using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Linq;

namespace emulatorLauncher
{
    class CemuGenerator : Generator
    {
        public CemuGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("cemu");

            string exe = Path.Combine(path, "cemu.exe");
            if (!File.Exists(exe))
                return null;

            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                string romPath = Path.GetDirectoryName(rom);
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(romPath, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            string settingsFile = Path.Combine(path, "settings.xml");
            if (File.Exists(settingsFile))
            {
                try
                {
                    XDocument settings = XDocument.Load(settingsFile);

                    var fps = settings.Descendants().FirstOrDefault(d => d.Name == "FPS");
                    if (fps != null)
                    {                        
                        bool showFPS = SystemConfig.isOptSet("showFPS") && SystemConfig.getOptBoolean("showFPS");
                        if (showFPS.ToString().ToLower() != fps.Value)
                        {
                            fps.SetValue(showFPS);
                            settings.Save(settingsFile);
                        }
                    }
                }
                catch { }
            }

            CreateControllerConfiguration(path);

            string romdir = Path.GetDirectoryName(rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-f -g \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }
        
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string controllerProfiles = Path.Combine(path, "controllerProfiles");

            foreach (var controller in this.Controllers)
            {
                if (controller.Config == null)
                    continue;

                string controllerTxt = Path.Combine(controllerProfiles, "controller" + (controller.PlayerIndex - 1) + ".txt");
                using (IniFile ini = new IniFile(controllerTxt, true))
                {
                    ConfigureInput(ini, controller);
                    ini.Save();
                }
            }
        }

        private static void ConfigureInput(IniFile ini, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.Config.Type == "joystick")
                ConfigureJoystick(ini, controller.Config, controller.PlayerIndex -1);
            else
                ConfigureKeyboard(ini, controller.Config);
        }


        private static void ConfigureKeyboard(IniFile ini, InputConfig keyboard)
        {
            if (keyboard == null)
                return;

            ini.WriteValue("General", "emulate", "Wii U GamePad");
            ini.WriteValue("General", "api", "Keyboard");
            ini.WriteValue("General", "controller", null);

            ini.WriteValue("Controller", "rumble", "0");
            ini.WriteValue("Controller", "leftRange", "1");
            ini.WriteValue("Controller", "rightRange", "1");
            ini.WriteValue("Controller", "leftDeadzone", "0.15");
            ini.WriteValue("Controller", "rightDeadzone", "0.15");
            ini.WriteValue("Controller", "buttonThreshold", "0.5");

            Action<string, InputKey> writeIni = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    byte value = SdlToKeyCode(a.Id);
                    ini.WriteValue("Controller", v, "key_" + value.ToString());
                }
                else if (ini.GetValue("Controller", v) != null && ini.GetValue("Controller", v).StartsWith("button"))
                    ini.WriteValue("Controller", v, "");
            };

            writeIni("1", InputKey.a);
            writeIni("2", InputKey.b);
            writeIni("3", InputKey.x);
            writeIni("4", InputKey.y);

            writeIni("5", InputKey.pageup);
            writeIni("6", InputKey.pagedown);

            writeIni("7", InputKey.lefttrigger);
            writeIni("8", InputKey.righttrigger);

            writeIni("9", InputKey.start);
            writeIni("10", InputKey.select);

            writeIni("11", InputKey.up);
            writeIni("12", InputKey.down);
            writeIni("13", InputKey.left);
            writeIni("14", InputKey.right);

            if (ini.GetValue("Controller", "15") != null && ini.GetValue("Controller", "15").StartsWith("button"))
                ini.WriteValue("Controller", "15", null);

            if (ini.GetValue("Controller", "16") != null && ini.GetValue("Controller", "16").StartsWith("button"))
                ini.WriteValue("Controller", "16", null);

            writeIni("17", InputKey.joystick1up);
            writeIni("18", InputKey.joystick1up);
            writeIni("19", InputKey.joystick1left);
            writeIni("20", InputKey.joystick1left);

            writeIni("21", InputKey.joystick2up);
            writeIni("22", InputKey.joystick2up);
            writeIni("23", InputKey.joystick2left);
            writeIni("24", InputKey.joystick2left);

            writeIni("26", InputKey.hotkeyenable);
        }

        private static void ConfigureJoystick(IniFile ini, InputConfig joy, int playerIndex)
        {
            if (joy == null)
                return;

            string api;
            string guid = string.Empty;

            if (joy.IsXInputDevice())
            {
                api = "XInput";
                guid = "0";
            }
            else
            {
                api = "DirectInput";
                Guid gd = joy.GetJoystickInstanceGuid();
                if (gd == Guid.Empty)
                    return;

                guid = gd.ToString().ToUpper();
            }

            if (playerIndex == 0)
                ini.WriteValue("General", "emulate", "Wii U GamePad");
            else
                ini.WriteValue("General", "emulate", "Wii U Classic Controller");
            
            ini.WriteValue("General", "api", api);
            ini.WriteValue("General", "controller", guid);

            ini.WriteValue("Controller", "rumble", "0");
            ini.WriteValue("Controller", "leftRange", "1");
            ini.WriteValue("Controller", "rightRange", "1");
            ini.WriteValue("Controller", "leftDeadzone", "0.15");
            ini.WriteValue("Controller", "rightDeadzone", "0.15");
            ini.WriteValue("Controller", "buttonThreshold", "0.5");

            Action<string, InputKey, bool> writeIni = (v, k, r) =>                
            { 
                var val = GetInputValue(joy, k, api, r); 
                ini.WriteValue("Controller", v, val); 
            };

            writeIni("1", InputKey.a, false);
            writeIni("2", InputKey.b, false);
            writeIni("3", InputKey.x, false);
            writeIni("4", InputKey.y, false);

            writeIni("5", InputKey.pageup, false);
            writeIni("6", InputKey.pagedown, false);

            writeIni("7", InputKey.leftthumb, false); 
            writeIni("8", InputKey.rightthumb, false);

            writeIni("9", InputKey.start, false);
            writeIni("10", InputKey.select, false);

            writeIni("11", InputKey.up, false);
            writeIni("12", InputKey.down, false);
            writeIni("13", InputKey.left, false);
            writeIni("14", InputKey.right, false);

            writeIni("15", InputKey.lefttrigger, false);
            writeIni("16", InputKey.righttrigger, false);

            writeIni("17", InputKey.joystick1up, false);
            writeIni("18", InputKey.joystick1up, true);
            writeIni("19", InputKey.joystick1left, false);
            writeIni("20", InputKey.joystick1left, true);

            writeIni("21", InputKey.joystick2up, false);
            writeIni("22", InputKey.joystick2up, true);
            writeIni("23", InputKey.joystick2left, false);
            writeIni("24", InputKey.joystick2left, true);
            /*
            if (joy[InputKey.select] != null && !joy[InputKey.select].Equals(joy[InputKey.hotkey]))
                writeIni("26", InputKey.hotkey, false);
            else*/
                ini.WriteValue("Controller", "26", null);
        }

        private static string GetInputValue(InputConfig joy, InputKey ik, string api, bool invertAxis = false)
        {
            var a = joy[ik];
            if (a == null)
            {
                if (api == "XInput" && GetCEmuInputIndex(ik) == 7)
                    return "button_1000000";
                else if (api == "XInput" && GetCEmuInputIndex(ik) == 8)
                    return "button_2000000";

                return null;
            }

            Int64 val = a.Id;
            Int64 pid = 1;

            if (a.Type == "hat")
            {
                pid = a.Value;
                if (val == 0)
                {
                    switch (pid)
                    {
                        case 1: pid = 0x04000000; break;
                        case 2: pid = 0x20000000; break;
                        case 4: pid = 0x08000000; break;
                        case 8: pid = 0x10000000; break;
                    }

                    val = 0;
                }
                else
                {
                    pid = 0x2000000 * pid;
                    val = 0;
                }
            }

            if (a.Type == "axis")
            {
                pid = a.Value;

                int axisVal = invertAxis ? -1 : 1;

                if (api == "XInput" && val == 1 || val == 4)
                    axisVal = -axisVal;

                switch (val)
                {
                    case 0: // left analog left/right
                        if (pid == axisVal)     pid = 0x0040000000;                                
                        else                    pid = 0x1000000000;
                        break;
                    case 1: // left analog up/down
                        if (pid == axisVal)     pid = 0x0080000000; 
                        else                    pid = 0x2000000000;
                        break;
                    case 2: // Triggers Analogiques L+R
                        if (pid == axisVal)     pid = 0x0100000000;                                 
                        else                    pid = 0x4000000000;
                        break;
                    case 3: // right analog left/right
                        if (pid == axisVal)     pid = 0x0200000000; 
                        else                    pid = 0x8000000000;
                        break;
                    case 4: // right analog up/down
                        if (pid == axisVal)     pid = 0x0400000000; 
                        else                    pid = 0x10000000000;
                        break;
                    case 5: // Triggers Analogiques R
                        if (pid == axisVal) pid = 0x0800000000;
                        else                pid = 0x10000000000;
                        break;
                }

                if (pid == axisVal && val == 1)
                    pid = 0x80000000;
                else
                    val = 0;
            }

            // Invert start/select on XInput
            if (api == "XInput" && val == 7)
                val = 6;
            else if (api == "XInput" && val == 6)
                val = 7;

            //(GetCEmuInputIndex(ik, api == "XInput") + (invertAxis ? 1 : 0)).ToString() + " = 
            string ret = "button_" + (pid << (int)val).ToString("X");
            return ret;
        }

        public static int GetCEmuInputIndex(InputKey k, bool XInput = false)
        {
            if (XInput && k == InputKey.start)
                return 10;
            else if (XInput && k == InputKey.select)
                return 9;

            switch (k)
            {
                case InputKey.a: return 1;
                case InputKey.b: return 2;
                case InputKey.x: return 3;
                case InputKey.y: return 4;

                case InputKey.leftshoulder: return 5;
                case InputKey.rightshoulder: return 6;

                case InputKey.lefttrigger: return 7;
                case InputKey.righttrigger: return 8;

                case InputKey.start: return 9;
                case InputKey.select: return 10;

                case InputKey.up: return 11;
                case InputKey.down: return 12;
                case InputKey.left: return 13;
                case InputKey.right: return 14;

                case InputKey.leftthumb: return 15;
                case InputKey.rightthumb: return 16;

                case InputKey.leftanalogup: return 17;
                case InputKey.leftanalogdown: return 18;
                case InputKey.leftanalogleft: return 19;
                case InputKey.leftanalogright: return 20;


                case InputKey.rightanalogup: return 21;
                case InputKey.rightanalogdown: return 22;
                case InputKey.rightanalogleft: return 23;
                case InputKey.rightanalogright: return 24;

                case InputKey.hotkeyenable: return 26;
            }
            return -1;
        }

        private static byte SdlToKeyCode(long sdlCode)
        {
            switch (sdlCode)
            {
                //Select = 0x40000077,
                //PrintScreen = 0x40000046,
                //LeftGui = 0x400000e3,
                //RightGui = 0x400000e7,
                //Application = 0x40000065,
                //Kp_ENTER = 0x40000058,
                //Gui = 0x400000e3,
                //Pause = 0x40000048,
                //Capslock = 0x40000039,

                case 0x4000009e: return 13; // Return2

                case 0x400000e1: return 16; // Shift = 
                case 0x400000e0: return 17; // Ctrl = 
                case 0x400000e2: return 18; // Alt = 

                case 0x4000004b: return 33; // PageUp = ,
                case 0x4000004e: return 34; // PageDown = ,
                case 0x4000004d: return 35; // End = ,
                case 0x4000004a: return 36; // Home = ,
                case 0x40000050: return 37; // Left = ,
                case 0x40000052: return 38; // Up = ,
                case 0x4000004f: return 39; // Right = ,
                case 0x40000051: return 40; // Down = 0x40000051,

                case 0x40000049: return 45; // Insert = 0x40000049,
                case 0x0000007f: return 46; // Delete = 0x0000007f,
                    
                case 0x40000059: return 97;  //KP_1 = 0x40000059,
                case 0X4000005A: return 98;  //KP_2 = 0X4000005A,
                case 0x4000005b: return 99;  // KP_3 = ,
                case 0x4000005c: return 100; // KP_4 = ,
                case 0x4000005d: return 101; // KP_5 = ,
                case 0x4000005e: return 102; // KP_6 = ,
                case 0x4000005f: return 103; // KP_7 = ,
                case 0x40000060: return 104; // KP_8 = ,
                case 0x40000061: return 105; // KP_9 = ,
                case 0x40000062: return 96;  // KP_0 = 0x40000062,
                case 0x40000055: return 106; // KP_Multiply
                case 0x40000057: return 107; // KP_Plus
                case 0x40000056: return 109; // KP_Minus

                case 0x4000003a: return 112; // F1
                case 0x4000003b: return 113; // F2
                case 0x4000003c: return 114; // F3
                case 0x4000003d: return 115; // F4
                case 0x4000003e: return 116; // F5
                case 0x4000003f: return 117; // F6
                case 0x40000040: return 118; // F7
                case 0x40000041: return 119; // F8
                case 0x40000042: return 120; // F9
                case 0x40000043: return 121; // F10
                case 0x40000044: return 122; // F11
                case 0x40000045: return 123; // F12
                /*        
            KP_Period = 0x40000063,
            KP_Divide = 0x40000054,
                   
            NumlockClear = 0x40000053,
            ScrollLock = 0x40000047,*/
                //RightShift = 0x400000e5,
                //LeftCtrl = 0x400000e0,
                //   RightCtrl = 0x400000e4,
                //     RightAlt = 0x400000e6,
            }

            sdlCode = sdlCode & 0xFFFF;
            byte value = (byte)((char)sdlCode).ToString().ToUpper()[0];
            return value;
        }
    }
}
