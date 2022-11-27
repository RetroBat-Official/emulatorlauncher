using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Linq;
using System.Xml;

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

            rom = TryUnZipGameIfNeeded(system, rom);

            //read m3u if rom is in m3u format
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

            //controller configuration
            CreateControllerConfiguration(path);

            string romdir = Path.GetDirectoryName(rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-f -g \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }

        //Create controller configuration
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string controllerProfiles = Path.Combine(path, "controllerProfiles");

            //Create a single controllerprofile file for each controller
            foreach (var controller in this.Controllers)
            {
                if (controller.Config == null)
                    continue;

                string controllerXml = Path.Combine(controllerProfiles, "controller" + (controller.PlayerIndex - 1) + ".xml");
                
                //Create xml file with correct settings
                XmlWriterSettings Settings = new XmlWriterSettings();
                Settings.Encoding = Encoding.UTF8;
                Settings.Indent = true;
                Settings.IndentChars = ("\t");
                Settings.OmitXmlDeclaration = false;
                
                //Go to input configuration
                using (XmlWriter writer = XmlWriter.Create(controllerXml, Settings))
                    ConfigureInputXml(writer, controller);
            }
        }

        //Configure input - routing between joystick or keyboard
        private static void ConfigureInputXml(XmlWriter writer, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.Config.Type == "joystick")
                ConfigureJoystickXml(writer, controller, controller.PlayerIndex - 1);
            else
                ConfigureKeyboardXml(writer, controller.Config);
        }

        //configuration of keyboard in xml format
        private static void ConfigureKeyboardXml(XmlWriter writer, InputConfig keyboard)
        {
            if (keyboard == null)
                return;

            //Create start of the xml document until mappings part
            writer.WriteStartDocument();
            writer.WriteStartElement("emulated_controller");
            writer.WriteElementString("type", "Wii U GamePad");
            writer.WriteStartElement("controller");
            writer.WriteElementString("api", "Keyboard");
            writer.WriteElementString("uuid", "keyboard");
            writer.WriteElementString("display_name", "Keyboard");
            writer.WriteStartElement("axis");
            writer.WriteElementString("deadzone", "0.25");
            writer.WriteElementString("range", "1");
            writer.WriteEndElement();//end of axis
            writer.WriteStartElement("rotation");
            writer.WriteElementString("deadzone", "0.25");
            writer.WriteElementString("range", "1");
            writer.WriteEndElement();//end of rotation
            writer.WriteStartElement("trigger");
            writer.WriteElementString("deadzone", "0.25");
            writer.WriteElementString("range", "1");
            writer.WriteEndElement();//end of trigger
            writer.WriteStartElement("mappings");

            //Define action to generate key mappings based on SdlToKeyCode
            Action<string, InputKey> writemapping = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    byte value = SdlToKeyCode(a.Id);
                    writer.WriteStartElement("entry");
                    writer.WriteElementString("mapping", v);
                    writer.WriteElementString("button", value.ToString());
                    writer.WriteEndElement();//end of entry
                }
                else
                    return;
            };

            //create button mapping part of the xml document            
            writemapping("1", InputKey.a);
            writemapping("2", InputKey.b);
            writemapping("3", InputKey.x);
            writemapping("4", InputKey.y);
            writemapping("5", InputKey.pageup);
            writemapping("6", InputKey.pagedown);
            writemapping("7", InputKey.l2);
            writemapping("8", InputKey.r2);
            writemapping("9", InputKey.start);
            writemapping("10", InputKey.select);
            writemapping("11", InputKey.up);
            writemapping("12", InputKey.down);
            writemapping("13", InputKey.left);
            writemapping("14", InputKey.right);
            writemapping("15", InputKey.l3);
            writemapping("16", InputKey.r3);
            writemapping("17", InputKey.joystick1up);
            writemapping("18", InputKey.joystick1down);
            writemapping("19", InputKey.joystick1left);
            writemapping("20", InputKey.joystick1right);
            writemapping("21", InputKey.joystick2up);
            writemapping("22", InputKey.joystick2down);
            writemapping("23", InputKey.joystick2left);
            writemapping("24", InputKey.joystick2right);
            writemapping("26", InputKey.hotkey);

            //close xml elements
            writer.WriteEndElement();//end of mappings
            writer.WriteEndElement();//end of controller
            writer.WriteEndElement();//end of emulated_controller
            writer.WriteEndDocument();
        }

        //Configuration of joysticks
        private static void ConfigureJoystickXml(XmlWriter writer, Controller ctrl, int playerIndex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            string xbox = "";
            if (ctrl.IsXInputDevice)
                xbox = "yes";

            // Get joystick data (type, api, guid, index)
            string type;                            //will be used to switch from Gamepad to Pro Controller
            string api = "SDLController";           //all controllers in cemu are mapped as sdl controllers                              
            string devicename = joy.DeviceName;

            int index = Program.Controllers                
                .GroupBy(c => c.Guid)
                .Where(c => c.Key == ctrl.Guid)
                .SelectMany(c => c)
                .OrderBy(c => SdlGameController.GetControllerIndex(c))
                .ToList()
                .IndexOf(ctrl);

            string uuid = index + "_" + ctrl.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant(); //string uuid of the cemu config file, based on old sdl2 guids ( pre 2.26 ) without crc-16

            //WiiU and cemu only allow 2 Gamepads, players 1&2 will be set as Gamepads, following players as Pro Controller(s)
            bool procontroller = false;
            if (playerIndex == 0 || playerIndex == 1)
                type = "Wii U GamePad";
            else
            {
                type = "Wii U Pro Controller";
                procontroller = true;               //bool will be used later as button mapping is not the same between Gamepad & Pro controller
            }

            //Create start of the xml document until mappings part
            writer.WriteStartDocument();
            writer.WriteStartElement("emulated_controller");
            writer.WriteElementString("type", type);
            writer.WriteStartElement("controller");
            writer.WriteElementString("api", api);
            writer.WriteElementString("uuid", uuid);
            writer.WriteElementString("display_name", devicename);

            //set rumble if option is set
            if (Program.SystemConfig.isOptSet("cemu_enable_rumble") && !string.IsNullOrEmpty(Program.SystemConfig["cemu_enable_rumble"]))
                writer.WriteElementString("rumble", Program.SystemConfig["cemu_enable_rumble"]);

            //set motion if option is set in features
            if (xbox != "yes" && Program.SystemConfig.isOptSet("cemu_enable_motion") && Program.SystemConfig.getOptBoolean("cemu_enable_motion"))
                writer.WriteElementString("motion", Program.SystemConfig["cemu_enable_motion"]);

            //Default deadzones and ranges for axis, rotation and trigger
            writer.WriteStartElement("axis");
            writer.WriteElementString("deadzone", "0.25");
            writer.WriteElementString("range", "1");
            writer.WriteEndElement();//end of axis
            writer.WriteStartElement("rotation");
            writer.WriteElementString("deadzone", "0.25");
            writer.WriteElementString("range", "1");
            writer.WriteEndElement();//end of rotation
            writer.WriteStartElement("trigger");
            writer.WriteElementString("deadzone", "0.25");
            writer.WriteElementString("range", "1");
            writer.WriteEndElement();//end of trigger
            writer.WriteStartElement("mappings");

            //Define action to generate key bindings
            Action<string, InputKey, bool> writemapping = (v, k, r) =>
            {
                var a = joy[k];
                if (a != null)
                {
                    var val = GetInputValuexml(ctrl, k, api, r);
                    writer.WriteStartElement("entry");
                    writer.WriteElementString("mapping", v);
                    writer.WriteElementString("button", val);
                    writer.WriteEndElement();//end of entry
                }
                else
                    return;
            };

            //Write mappings of buttons

            //revert gamepadbuttons if set in features
            if (ctrl.IsXInputDevice && Program.SystemConfig.isOptSet("gamepadbuttons") && Program.SystemConfig.getOptBoolean("gamepadbuttons"))
            {
                writemapping("1", InputKey.b, false);
                writemapping("2", InputKey.a, false);
                writemapping("3", InputKey.x, false);
                writemapping("4", InputKey.y, false);
            }
            else if (ctrl.IsXInputDevice)
            {
                writemapping("1", InputKey.a, false);
                writemapping("2", InputKey.b, false);
                writemapping("3", InputKey.y, false);
                writemapping("4", InputKey.x, false);
            }
            else
            {
                writemapping("1", InputKey.b, false);
                writemapping("2", InputKey.a, false);
                writemapping("3", InputKey.x, false);
                writemapping("4", InputKey.y, false);
            }
                    
            writemapping("5", InputKey.pageup, false);
            writemapping("6", InputKey.pagedown, false);
            writemapping("7", InputKey.l2, false);
            writemapping("8", InputKey.r2, false);
            writemapping("9", InputKey.start, false);
            writemapping("10", InputKey.select, false);
            
            //Pro controller skips 11 while Gamepad continues numbering
            if (procontroller)
            {
                writemapping("12", InputKey.up, false);
                writemapping("13", InputKey.down, false);
                writemapping("14", InputKey.left, false);
                writemapping("15", InputKey.right, false);
                writemapping("16", InputKey.l3, false);
                writemapping("17", InputKey.r3, false);
                writemapping("18", InputKey.leftanalogup, false);
                writemapping("19", InputKey.leftanalogup, true);
                writemapping("20", InputKey.leftanalogleft, false);
                writemapping("21", InputKey.leftanalogleft, true);
                writemapping("22", InputKey.rightanalogup, false);
                writemapping("23", InputKey.rightanalogup, true);
                writemapping("24", InputKey.rightanalogleft, false);
                writemapping("25", InputKey.rightanalogleft, true);
            }
            else
            {
                writemapping("11", InputKey.up, false);
                writemapping("12", InputKey.down, false);
                writemapping("13", InputKey.left, false);
                writemapping("14", InputKey.right, false);
                writemapping("15", InputKey.l3, false);
                writemapping("16", InputKey.r3, false);
                writemapping("17", InputKey.leftanalogup, false);
                writemapping("18", InputKey.leftanalogup, true);
                writemapping("19", InputKey.leftanalogleft, false);
                writemapping("20", InputKey.leftanalogleft, true);
                writemapping("21", InputKey.rightanalogup, false);
                writemapping("22", InputKey.rightanalogup, true);
                writemapping("23", InputKey.rightanalogleft, false);
                writemapping("24", InputKey.rightanalogleft, true);
            }

            //close xml sections 
            writer.WriteEndElement();//end of mappings
            writer.WriteEndElement();//end of controller
            writer.WriteEndElement();//end of emulated_controller
            writer.WriteEndDocument();
        }

        //Generate key bindings
        private static string GetInputValuexml(Controller ctrl, InputKey ik, string api, bool invertAxis = false)
        {
            InputConfig joy = ctrl.Config;

            var a = joy[ik];        //inputkey
            Int64 val = a.Id;       //id from es_input config file
            Int64 pid = 1;          //pid will be used to retrieve value in es_input config file for hat and axis

            //L1 and R1 for XInput sends wrong id, cemu is based on SDl id's
            if (ctrl.IsXInputDevice && a.Type == "button" && val == 5)
                return "10";
            else if (ctrl.IsXInputDevice && a.Type == "button" && val == 4)
                return "9";

            //Return code for left and right triggers (l2 & r2)
            if (a.Type == "axis" && val == 4)
                return "42";
            if (a.Type == "axis" && val == 5)
                return "43";

            //start and select for XInput sends wrong id, cemu is based on SDl id's
            if (ctrl.IsXInputDevice && a.Type == "button" && val == 6)
                return "4";
            else if (ctrl.IsXInputDevice && a.Type == "button" && val == 7)
                return "6";

            //D-pad for XInput is identified as "hat", retrieve value to define right direction
            if (a.Type == "hat")
            {
                pid = a.Value;
                switch (pid)
                {
                    case 1: return "11";
                    case 4: return "12";
                    case 8: return "13";
                    case 2: return "14";
                }
            }

            //Set return values for left and right sticks
            if (a.Type == "axis")
            {
                pid = a.Value;                      //get value
                int axisVal = invertAxis ? -1 : 1;  //if mapping is "true"

                switch (val)
                {
                    case 0: // left analog left/right
                        if (pid == axisVal) return "38";
                        else return "44";
                    case 1: // left analog up/down
                        if (pid == axisVal) return "39";
                        else return "45";
                    case 2: // right analog left/right
                        if (pid == axisVal) return "40";
                        else return "46";
                    case 3: // right analog up/down
                        if (pid == axisVal) return "41";
                        else return "47";
                }
            }

            //l3 and r3 (thumbs) have different id than cemu
            if (ctrl.IsXInputDevice && a.Type == "button" && val == 8)
                return "7";
            else if (ctrl.IsXInputDevice && a.Type == "button" && val == 9)
                return "8";

            string ret = val.ToString();
            return ret;

        }

        //Search keyboard keycode
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
            }

            sdlCode = sdlCode & 0xFFFF;
            byte value = (byte)((char)sdlCode).ToString().ToUpper()[0];
            return value;
        }
    }
}
