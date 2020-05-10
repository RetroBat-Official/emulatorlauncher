using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class CemuGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("cemu");

            string exe = Path.Combine(path, "cemu.exe");
            if (!File.Exists(exe))
                return null;

            CreateControllerConfiguration(path);
            //     AutoSelectControllerProfile(path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-f -g \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }
        /*
        private static void AutoSelectControllerProfile(string path)
        {
            List<ControllerProfile> profiles = new List<ControllerProfile>();

            string[] files = Directory.GetFiles(Path.Combine(path, "controllerProfiles"), "*.txt");
            foreach (var file in files.Where(f => !Path.GetFileName(f).StartsWith("controller")))
            {
                string emulate = GetPrivateProfileString("General", "emulate", file);
                if (emulate != "Wii U GamePad")
                    continue;

                string api = GetPrivateProfileString("General", "api", file);
                string controller = GetPrivateProfileString("General", "controller", file);
                if (controller == null)
                    controller = string.Empty;

                profiles.Add(new ControllerProfile()
                {
                    FileName = file,
                    Api = api,
                    DeviceGuid = controller.ToUpper() // !string.IsNullOrEmpty(controller) && api == "DirectInput" ? new Guid(controller) : Guid.Empty,
                });
            }

            ControllerProfile toAssign = null;

            if (InputDevices.NvidiaShieldExists())
            {
                string nvGuid = new Guid("ECBB3D3D-C2EA-4861-983F-B3E15BDC6C52").ToString().ToUpper();
                var nvidiaProfile = profiles.FirstOrDefault(p => p.DeviceGuid == nvGuid);
                if (nvidiaProfile != null)
                    toAssign = nvidiaProfile;
            }

            if (toAssign == null)
                toAssign = profiles.FirstOrDefault(p => InputDevices.JoystickExists(p.Guid));

            if (toAssign == null && InputDevices.IsXInputControllerConnected(0))
                toAssign = profiles.FirstOrDefault(p => p.Api == "XInput" && p.DeviceGuid == "0");

            if (toAssign == null)
                toAssign = profiles.FirstOrDefault(p => p.Api == "Keyboard");

            if (toAssign != null)
            {
                try { File.Copy(toAssign.FileName, Path.Combine(Path.GetDirectoryName(toAssign.FileName), "controller0.txt"), true); }
                catch { }
            }
        }
        */

        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            foreach (var controller in this.Controllers)
            {
                if (controller.Input == null)
                    continue;

                string file = Path.Combine(path, "controllerProfiles", controller.Input.DeviceName + ".txt");

                string sb = ConfigureInput(controller.Input);
                if (sb != null)
                {
                    try { File.WriteAllText(file, sb); }
                    catch { }
                }
            }
        }

        private static string ConfigureInput(InputConfig input)
        {
            if (input == null)
                return null;

            if (input.Type == "joystick")
                return ConfigureJoystick(input);

            return ConfigureKeyboard(input);
        }


        private static string ConfigureKeyboard(InputConfig keyboard)
        {
            if (keyboard == null)
                return null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[General]");
            sb.AppendLine("emulate = Wii U GamePad");
            sb.AppendLine("api = Keyboard");
            sb.AppendLine();

            sb.AppendLine("[Controller]");
            sb.AppendLine("rumble = 0,000000");
            sb.AppendLine("leftRange = 1,000000");
            sb.AppendLine("rightRange = 1,000000");
            sb.AppendLine("leftDeadzone = 0,000000");
            sb.AppendLine("rightDeadzone = 0,000000");

            foreach (InputKey ik in Enum.GetValues(typeof(InputKey)).Cast<InputKey>().OrderBy(i => GetCEmuInputIndex(i)))
            {
                var a = keyboard[ik];
                if (a == null)
                    continue;

                byte value = SdlToKeyCode(a.Id);

                sb.AppendLine(GetCEmuInputIndex(ik).ToString() + " = key_" + value.ToString());
            }

            return sb.ToString();
        }

        private static string ConfigureJoystick(InputConfig joy)
        {
            if (joy == null)
                return null;

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
                    return null;

                guid = gd.ToString().ToUpper();
            }

            StringBuilder sbJoy = new StringBuilder();

            sbJoy.AppendLine("[General]");
            sbJoy.AppendLine("emulate = Wii U GamePad");
            sbJoy.AppendLine("api = " + api);
            sbJoy.AppendLine("controller = " + guid);
            sbJoy.AppendLine();

            sbJoy.AppendLine("[Controller]");
            sbJoy.AppendLine("rumble = 0,000000");
            sbJoy.AppendLine("leftRange = 1,000000");
            sbJoy.AppendLine("rightRange = 1,000000");
            sbJoy.AppendLine("leftDeadzone = 0,250000");
            sbJoy.AppendLine("rightDeadzone = 0,250000");

            foreach (InputKey ik in Enum.GetValues(typeof(InputKey)).Cast<InputKey>().OrderBy(i => GetCEmuInputIndex(i, api == "XInput")))
            {
                var a = joy[ik];
                if (a == null)
                {
                    if (api == "XInput" && GetCEmuInputIndex(ik) == 7)
                        sbJoy.AppendLine("7 = button_1000000");
                    else if (api == "XInput" && GetCEmuInputIndex(ik) == 8)
                        sbJoy.AppendLine("8 = button_2000000");

                    continue;
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
                            case 1: pid = 0x4000000; break;
                            case 2: pid = 0x20000000; break;
                            case 4: pid = 0x8000000; break;
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

                    switch (val)
                    {
                        case 0: // X      
                            if (pid == 1) pid = 0x40000000; else pid = 0x1000000000;
                            break;
                        case 1: // Y
                            if (pid == 1) pid = 0x80000000; else pid = 0x2000000000;
                            break;
                        case 2: // Triggers Analogiques L+R
                            if (pid == 1) pid = 0x100000000; else pid = 0x4000000000;
                            break;
                        case 3: // X
                            if (pid == 1) pid = 0x800000000; else pid = 0x20000000000;
                            //if (pid == 1) pid = 0x200000000; else pid = 0x8000000000;
                            break;
                        case 4: // Y

                            if (pid == 1) pid = 0x400000000; else pid = 0x10000000000;
                            break;
                    }

                    if (pid == 1 && val == 1)
                        pid = 0x80000000;
                    else
                        val = 0;
                }

                string name = GetCEmuInputIndex(ik, api == "XInput").ToString() + " = button_" + (pid << (int)val).ToString("X");
                sbJoy.AppendLine(name);
            }

            return sbJoy.ToString();
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

                case 0x400000e1: return 16;// Shift = 
                case 0x400000e0: return 17; // Ctrl = 
                case 0x400000e2: return 18; //Alt = 

                case 0x4000004b: return 33; // PageUp = ,
                case 0x4000004e: return 34; // PageDown = ,
                case 0x4000004d: return 35; // End = ,
                case 0x4000004a: return 36; //Home = ,
                case 0x40000050: return 37; // Left = ,
                case 0x40000052: return 38; // Up = ,
                case 0x4000004f: return 39; //Right = ,
                case 0x40000051: return 40; //Down = 0x40000051,

                case 0x40000049: return 45; // Insert = 0x40000049,
                case 0x0000007f: return 46; // Delete = 0x0000007f,


                case 0x40000059: return 97; //KP_1 = 0x40000059,
                case 0X4000005A: return 98; //KP_2 = 0X4000005A,
                case 0x4000005b: return 99; // KP_3 = ,
                case 0x4000005c: return 100; // KP_4 = ,
                case 0x4000005d: return 101; // KP_5 = ,
                case 0x4000005e: return 102; // KP_6 = ,
                case 0x4000005f: return 103; // KP_7 = ,
                case 0x40000060: return 104; // KP_8 = ,
                case 0x40000061: return 105; // KP_9 = ,
                case 0x40000062: return 96; //KP_0 = 0x40000062,
                case 0x40000055: return 106; // KP_Multiply
                case 0x40000057: return 107; // KP_Plus
                case 0x40000056: return 109; // KP_Minus

                case 0x4000003a: return 112; // F1
                case 0x4000003b: return 113; // F1
                case 0x4000003c: return 114; // F1
                case 0x4000003d: return 115; // F1
                case 0x4000003e: return 116; // F1
                case 0x4000003f: return 117; // F1
                case 0x40000040: return 118; // F1
                case 0x40000041: return 119; // F1
                case 0x40000042: return 120; // F1
                case 0x40000043: return 121; // F1
                case 0x40000044: return 122; // F1
                case 0x40000045: return 123; // F1
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
