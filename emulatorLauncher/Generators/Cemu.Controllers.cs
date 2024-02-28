using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class CemuGenerator
    {
        /// <summary>
        /// Cf. https://github.com/cemu-project/Cemu/blob/main/src/input/api/SDL/SDLControllerProvider.cpp#L21
        /// </summary>
        /// <param name="pcsx2ini"></param>
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();
            
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_PS4 = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_PS5 = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_PS4_RUMBLE = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_PS5_RUMBLE = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_GAMECUBE = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_SWITCH = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_JOY_CONS = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_STADIA = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_STEAM = 1");
            hints.Add("SDL_HINT_JOYSTICK_HIDAPI_LUNA = 1");
            
            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        /// <summary>
        /// Create controller configuration
        /// </summary>
        /// <param name="path"></param>
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            UpdateSdlControllersWithHints();

            string folder = Path.Combine(path, "controllerProfiles");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // If Wiimotes is set, do not use ES controllers but force wiimotes
            if (Program.SystemConfig.isOptSet("use_wiimotes") && (Program.SystemConfig["use_wiimotes"] != "0"))
            {
                int nbWiimotes = SystemConfig["use_wiimotes"].ToInteger();
                
                for (int i = 0; i <= nbWiimotes - 1; i++)
                {
                    string controllerXml = Path.Combine(folder, "controller" + i + ".xml");
                    var settings = new XmlWriterSettings() { Encoding = Encoding.UTF8, Indent = true, IndentChars = ("\t"), OmitXmlDeclaration = false };
                    using (XmlWriter writer = XmlWriter.Create(controllerXml, settings))
                        ConfigureWiimotes(writer, i);
                }
            }

            else
            {
                // Create a single controllerprofile file for each controller
                foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                {
                    if (controller.Config == null)
                        continue;

                    string controllerXml = Path.Combine(folder, "controller" + (controller.PlayerIndex - 1) + ".xml");

                    // Create xml file with correct settings
                    var settings = new XmlWriterSettings() { Encoding = Encoding.UTF8, Indent = true, IndentChars = ("\t"), OmitXmlDeclaration = false };

                    // Go to input configuration
                    using (XmlWriter writer = XmlWriter.Create(controllerXml, settings))
                        ConfigureInputXml(writer, controller);
                }
            }

        }

        /// <summary>
        /// Configure input - routing between joystick or keyboard
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="controller"></param>
        private void ConfigureInputXml(XmlWriter writer, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboardXml(writer, controller.Config);
            else    
                ConfigureJoystickXml(writer, controller, controller.PlayerIndex - 1);
        }

        /// <summary>
        /// Keyboard configuration in xml format
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="keyboard"></param>
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
            Action<string, InputKey> WriteInputKeyMapping = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    writer.WriteStartElement("entry");
                    writer.WriteElementString("mapping", v);
                    writer.WriteElementString("button", SdlToKeyCode(a.Id).ToString());
                    writer.WriteEndElement();
                }
            };

            //create button mapping part of the xml document            
            WriteInputKeyMapping("1", InputKey.a);
            WriteInputKeyMapping("2", InputKey.b);
            WriteInputKeyMapping("3", InputKey.x);
            WriteInputKeyMapping("4", InputKey.y);
            WriteInputKeyMapping("5", InputKey.pageup);
            WriteInputKeyMapping("6", InputKey.pagedown);
            WriteInputKeyMapping("7", InputKey.l2);
            WriteInputKeyMapping("8", InputKey.r2);
            WriteInputKeyMapping("9", InputKey.start);
            WriteInputKeyMapping("10", InputKey.select);
            WriteInputKeyMapping("11", InputKey.up);
            WriteInputKeyMapping("12", InputKey.down);
            WriteInputKeyMapping("13", InputKey.left);
            WriteInputKeyMapping("14", InputKey.right);
            WriteInputKeyMapping("15", InputKey.l3);
            WriteInputKeyMapping("16", InputKey.r3);
            WriteInputKeyMapping("17", InputKey.joystick1up);
            WriteInputKeyMapping("18", InputKey.joystick1down);
            WriteInputKeyMapping("19", InputKey.joystick1left);
            WriteInputKeyMapping("20", InputKey.joystick1right);
            WriteInputKeyMapping("21", InputKey.joystick2up);
            WriteInputKeyMapping("22", InputKey.joystick2down);
            WriteInputKeyMapping("23", InputKey.joystick2left);
            WriteInputKeyMapping("24", InputKey.joystick2right);
            WriteInputKeyMapping("26", InputKey.hotkey);

            //close xml elements
            writer.WriteEndElement();//end of mappings
            writer.WriteEndElement();//end of controller
            writer.WriteEndElement();//end of emulated_controller
            writer.WriteEndDocument();
        }

        /// <summary>
        /// Wiimote configuration
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="playerIndex"></param>
        private static void ConfigureWiimotes(XmlWriter writer, int playerIndex)
        {
            string wiimoteType = "5";
            if (Program.SystemConfig.isOptSet("cemu_wiimotep" + playerIndex) && !string.IsNullOrEmpty(Program.SystemConfig["cemu_wiimotep" + playerIndex]))
                wiimoteType = Program.SystemConfig["cemu_wiimotep" + playerIndex];

            //Create start of the xml document until mappings part
            writer.WriteStartDocument();
            writer.WriteStartElement("emulated_controller");
            writer.WriteElementString("type", "Wiimote");
            writer.WriteElementString("device_type", wiimoteType);
            writer.WriteStartElement("controller");
            writer.WriteElementString("api", "Wiimote");
            writer.WriteElementString("uuid", playerIndex.ToString());
            writer.WriteElementString("display_name", "Controller " + (playerIndex + 1).ToString());
            writer.WriteElementString("motion", "true");

            //set rumble if option is set
            if (Program.SystemConfig.isOptSet("cemu_enable_rumble") && !string.IsNullOrEmpty(Program.SystemConfig["cemu_enable_rumble"]))
                writer.WriteElementString("rumble", Program.SystemConfig["cemu_enable_rumble"]);

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
            writer.WriteElementString("packet_delay", "25");
            writer.WriteStartElement("mappings");

            //Define action to generate key bindings
            Action<string, string> WriteWiimoteMapping = (m, b) =>
            {
                writer.WriteStartElement("entry");
                writer.WriteElementString("mapping", m);
                writer.WriteElementString("button", b);
                writer.WriteEndElement();
            };

            WriteWiimoteMapping("9", "3");
            WriteWiimoteMapping("17", "15");
            WriteWiimoteMapping("1", "11");
            WriteWiimoteMapping("2", "10");
            WriteWiimoteMapping("3", "9");
            WriteWiimoteMapping("4", "8");
            WriteWiimoteMapping("7", "4");
            WriteWiimoteMapping("8", "12");
            WriteWiimoteMapping("10", "2");
            WriteWiimoteMapping("11", "0");
            WriteWiimoteMapping("12", "1");
            WriteWiimoteMapping("5", "17");
            WriteWiimoteMapping("6", "16");
            WriteWiimoteMapping("13", "39");
            WriteWiimoteMapping("14", "45");
            WriteWiimoteMapping("15", "44");
            WriteWiimoteMapping("16", "38");

            writer.WriteEndElement();//end of mappings
            writer.WriteEndElement();//end of controller
            writer.WriteEndElement();//end of emulated_controller
            writer.WriteEndDocument();
        }

        /// <summary>
        /// Joysticks configuration
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="ctrl"></param>
        /// <param name="playerIndex"></param>
        private void ConfigureJoystickXml(XmlWriter writer, Controller ctrl, int playerIndex)
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
                .GroupBy(c => c.Guid.ToLowerInvariant())
                .Where(c => c.Key == ctrl.Guid.ToLowerInvariant())
                .SelectMany(c => c)
                .OrderBy(c => c.GetSdlControllerIndex())
                .ToList()
                .IndexOf(ctrl);

            string uuid = index + "_" + ctrl.GetSdlGuid(_sdlVersion, true).ToLowerInvariant(); //string uuid of the cemu config file, based on old sdl2 guids ( pre 2.26 ) without crc-16

            // Define type of controller
            // Players 1 defaults to WIIU Gamepad but can be changed in features
            // Player 2 defaults to Pro controller but can be changed in features
            // Players 3 and 4 default to pro controllers and cannot be changed
            bool procontroller = false;
            if (playerIndex == 0)
            {
                string cemuController = "cemu_controllerp" + playerIndex;
                if (Program.SystemConfig.isOptSet(cemuController) && (Program.SystemConfig[cemuController] == "procontroller"))
                {
                    type = "Wii U Pro Controller";
                    procontroller = true;
                }
                else
                {
                    type = "Wii U GamePad";
                    procontroller = false;
                }
            }

            else if (playerIndex == 1)
            {
                string cemuController = "cemu_controllerp" + playerIndex;
                if (Program.SystemConfig.isOptSet(cemuController) && (Program.SystemConfig[cemuController] == "gamepad"))
                {
                    type = "Wii U GamePad";
                    procontroller = false;
                }
                else
                {
                    type = "Wii U Pro Controller";
                    procontroller = true;
                }
            }

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
            Action<string, InputKey, bool> WriteMapping = (v, k, r) =>
            {
                var a = joy[k];
                if (a != null)
                {
                    var val = GetInputValuexml(ctrl, k, api, r);
                    writer.WriteStartElement("entry");
                    writer.WriteElementString("mapping", v);
                    writer.WriteElementString("button", GetInputValuexml(ctrl, k, api, r));
                    writer.WriteEndElement();
                }
            };

            //Write mappings of buttons

            //revert gamepadbuttons if set in features
            if (Program.SystemConfig.getOptBoolean("gamepadbuttons"))
            {
                WriteMapping("1", InputKey.a, false);
                WriteMapping("2", InputKey.b, false);
                WriteMapping("3", InputKey.y, false);
                WriteMapping("4", InputKey.x, false);
            }
            else
            {
                WriteMapping("1", InputKey.b, false);
                WriteMapping("2", InputKey.a, false);
                WriteMapping("3", InputKey.x, false);
                WriteMapping("4", InputKey.y, false);
            }

            WriteMapping("5", InputKey.pageup, false);
            WriteMapping("6", InputKey.pagedown, false);
            WriteMapping("7", InputKey.l2, false);
            WriteMapping("8", InputKey.r2, false);
            WriteMapping("9", InputKey.start, false);
            WriteMapping("10", InputKey.select, false);

            //Pro controller skips 11 while Gamepad continues numbering
            if (procontroller)
            {
                WriteMapping("12", InputKey.up, false);
                WriteMapping("13", InputKey.down, false);
                WriteMapping("14", InputKey.left, false);
                WriteMapping("15", InputKey.right, false);
                WriteMapping("16", InputKey.l3, false);
                WriteMapping("17", InputKey.r3, false);
                WriteMapping("18", InputKey.leftanalogup, false);
                WriteMapping("19", InputKey.leftanalogup, true);
                WriteMapping("20", InputKey.leftanalogleft, false);
                WriteMapping("21", InputKey.leftanalogleft, true);
                WriteMapping("22", InputKey.rightanalogup, false);
                WriteMapping("23", InputKey.rightanalogup, true);
                WriteMapping("24", InputKey.rightanalogleft, false);
                WriteMapping("25", InputKey.rightanalogleft, true);
            }
            else
            {
                WriteMapping("11", InputKey.up, false);
                WriteMapping("12", InputKey.down, false);
                WriteMapping("13", InputKey.left, false);
                WriteMapping("14", InputKey.right, false);
                WriteMapping("15", InputKey.l3, false);
                WriteMapping("16", InputKey.r3, false);
                WriteMapping("17", InputKey.leftanalogup, false);
                WriteMapping("18", InputKey.leftanalogup, true);
                WriteMapping("19", InputKey.leftanalogleft, false);
                WriteMapping("20", InputKey.leftanalogleft, true);
                WriteMapping("21", InputKey.rightanalogup, false);
                WriteMapping("22", InputKey.rightanalogup, true);
                WriteMapping("23", InputKey.rightanalogleft, false);
                WriteMapping("24", InputKey.rightanalogleft, true);
            }

            //close xml sections 
            writer.WriteEndElement();//end of mappings
            writer.WriteEndElement();//end of controller
            writer.WriteEndElement();//end of emulated_controller
            writer.WriteEndDocument();
        }

        /// <summary>
        /// Generate key bindings
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="ik"></param>
        /// <param name="api"></param>
        /// <param name="invertAxis"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Search keyboard keycode
        /// </summary>
        /// <param name="sdlCode"></param>
        /// <returns></returns>
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
