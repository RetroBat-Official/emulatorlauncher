using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Rpcs3Generator
    {
        /// <summary>
        /// Create controller configuration
        /// </summary>
        /// <param name="path"></param>
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            //Path does not exist by default so create it if inexistent
            string folder = Path.Combine(path, "config", "input_configs", "global");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Check if config file already exists or not and create it if not
            string controllerSettings = Path.Combine(folder, "Default.yml");

            var yml = YmlFile.Load(controllerSettings);

            Dictionary<string, int> double_pads = new Dictionary<string, int>();
            int nsamepad = 1;

            //Create a single Player block in the file for each player
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(yml, controller, double_pads, nsamepad);

            // Save to yml file
            yml.Save();
        }

        /// <summary>
        /// Configure controller
        /// </summary>
        /// <param name="controllerSettings"></param>
        /// <param name="controller"></param>
        /// <param name="yml"></param>
        private void ConfigureInput(YmlContainer yml, Controller controller, Dictionary<string, int> double_pads, int nsamepad)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(yml, controller.Config, controller.PlayerIndex);
            else
                ConfigureJoystick(yml, controller, controller.PlayerIndex, double_pads, nsamepad);
        }

        /// <summary>
        /// Keyboard configuration
        /// </summary>
        /// <param name="controllerSettings"></param>
        /// <param name="keyboard"></param>
        /// <param name="yml"></param>
        private void ConfigureKeyboard(YmlContainer yml, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            //Create player section (only 1 player with keyboard)
            var player = yml.GetOrCreateContainer("Player " + playerindex + " Input");
            player["Handler"] = "Keyboard";
            player["Device"] = "Keyboard";

            var config = player.GetOrCreateContainer("Config");

            //Define action to generate key mappings based on SdlToKeyCode
            Action<string, InputKey> writemapping = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    string value = SdlToKeyCode(a.Id);
                    config[v] = value;
                }
                else
                    return;
            };

            //Perform mappings based on es_input
            writemapping("Left Stick Left", InputKey.leftanalogleft);
            writemapping("Left Stick Down", InputKey.leftanalogdown);
            writemapping("Left Stick Right", InputKey.leftanalogright);
            writemapping("Left Stick Up", InputKey.leftanalogup);
            writemapping("Right Stick Left", InputKey.rightanalogleft);
            writemapping("Right Stick Down", InputKey.rightanalogdown);
            writemapping("Right Stick Right", InputKey.rightanalogright);
            writemapping("Right Stick Up", InputKey.rightanalogup);
            writemapping("Start", InputKey.start);
            writemapping("Select", InputKey.select);
            writemapping("PS Button", InputKey.hotkey);
            writemapping("Square", InputKey.y);
            writemapping("Cross", InputKey.b);
            writemapping("Circle", InputKey.a);
            writemapping("Triangle", InputKey.x);
            writemapping("Left", InputKey.left);
            writemapping("Down", InputKey.down);
            writemapping("Right", InputKey.right);
            writemapping("Up", InputKey.up);
            writemapping("R1", InputKey.pagedown);
            writemapping("R2", InputKey.r2);
            writemapping("R3", InputKey.r3);
            writemapping("L1", InputKey.pageup);
            writemapping("L2", InputKey.l2);
            writemapping("L3", InputKey.l3);

            var motionx = config.GetOrCreateContainer("Motion Sensor X");
            motionx["Axis"] = "\"\"";
            motionx["Mirrored"] = "false";
            motionx["Shift"] = "0";

            var motiony = config.GetOrCreateContainer("Motion Sensor Y");
            motiony["Axis"] = "\"\"";
            motiony["Mirrored"] = "false";
            motiony["Shift"] = "0";

            var motionz = config.GetOrCreateContainer("Motion Sensor Z");
            motionz["Axis"] = "\"\"";
            motionz["Mirrored"] = "false";
            motionz["Shift"] = "0";

            var motiong = config.GetOrCreateContainer("Motion Sensor G");
            motiong["Axis"] = "\"\"";
            motiong["Mirrored"] = "false";
            motiong["Shift"] = "0";

            config["Pressure Intensity Button"] = "\"\"";
            config["Pressure Intensity Percent"] = "50";
            config["Left Stick Multiplier"] = "100";
            config["Right Stick Multiplier"] = "100";
            config["Left Stick Deadzone"] = "0";
            config["Right Stick Deadzone"] = "0";
            config["Left Trigger Threshold"] = "0";
            config["Right Trigger Threshold"] = "0";
            config["Left Pad Squircling Factor"] = "0";
            config["Right Pad Squircling Factor"] = "0";
            config["Color Value R"] = "0";
            config["Color Value G"] = "0";
            config["Color Value B"] = "0";
            config["Blink LED when battery is below 20%"] = "true";
            config["Use LED as a battery indicator"] = "false";
            config["LED battery indicator brightness"] = "50";
            config["Player LED enabled"] = "true";
            config["Enable Large Vibration Motor"] = "true";
            config["Enable Small Vibration Motor"] = "true";
            config["Switch Vibration Motors"] = "false";
            config["Mouse Movement Mode"] = "Relative";
            config["Mouse Deadzone X Axis"] = "60";
            config["Mouse Deadzone Y Axis"] = "60";
            config["Mouse Acceleration X Axis"] = "200";
            config["Mouse Acceleration Y Axis"] = "250";
            config["Left Stick Lerp Factor"] = "100";
            config["Right Stick Lerp Factor"] = "100";
            config["Analog Button Lerp Factor"] = "100";
            config["Trigger Lerp Factor"] = "100";
            config["Device Class Type"] = "0";
            config["Vendor ID"] = "1356";
            config["Product ID"] = "616";
            player["Buddy Device"] = "\"\"";
        }

        /// <summary>
        /// Joystick configuration
        /// </summary>
        /// <param name="controllerSettings"></param>
        /// <param name="ctrl"></param>
        /// <param name="playerIndex"></param>
        /// <param name="yml"></param>
        private void ConfigureJoystick(YmlContainer yml, Controller ctrl, int playerIndex, Dictionary<string, int> double_pads, int nsamepad)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            //set type of controller
            string devicename = joy.DeviceName;
            string guid = ctrl.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant();
            var prod = ctrl.ProductID;

            //define type of controller
            string tech = "SDL";
            if (prod == USB_PRODUCT.SONY_DS5)
                tech = "DualSense";
            else if (prod == USB_PRODUCT.SONY_DS4 || prod == USB_PRODUCT.SONY_DS4_DONGLE || prod == USB_PRODUCT.SONY_DS4_SLIM)
                tech = "DS4";
            else if (prod == USB_PRODUCT.SONY_DS3)
                tech = "DS3";
            else if (ctrl.IsXInputDevice)
                tech = "XInput";

            //Create Player block titles
            string playerBlockTitle = "Player" + " " + playerIndex + " " + "Input";
            var player = yml.GetOrCreateContainer(playerBlockTitle);

            //set controller handler : use specific Dualshock handler for Dualshocks, XInput for XInput controllers, SDL for all other cases
            if (tech == "DualSense")
                player["Handler"] = "DualSense";
            else if (tech == "DS4")
                player["Handler"] = "DualShock 4";
            else if (tech == "DS3")
                player["Handler"] = "DualShock 3";
            else if (tech == "XInput")
                player["Handler"] = "XInput";
            else
                player["Handler"] = "SDL";

            //Set device & index (incremental for Dualshocks and XInput, actual device index for SDL)
            
            if (double_pads.ContainsKey(tech + "/" + devicename))
                nsamepad = double_pads[tech + "/" + devicename];
            else
                nsamepad = 1;

            double_pads[tech + "/" + devicename] = nsamepad + 1;

            if (tech == "DualSense" || tech == "DS4" || tech == "DS3")
                player["Device"] = "\"" + tech + " Pad #" + nsamepad + "\"";
            else if (tech == "XInput")
            {
                int index = ctrl.XInput.DeviceIndex + 1;
                player["Device"] = "\"" + "XInput Pad #" + index + "\"";
            }
            else
                player["Device"] = devicename + " " + nsamepad;

            //config part
            var config = player.GetOrCreateContainer("Config");

            if (tech == "DualSense" || tech == "DS4" || tech == "DS3")
            {
                config["Left Stick Left"] = GetInputKeyNameDS(ctrl, InputKey.joystick1left, tech);    //LS X-
                config["Left Stick Down"] = GetInputKeyNameDS(ctrl, InputKey.joystick1down, tech);    //LS Y-
                config["Left Stick Right"] = GetInputKeyNameDS(ctrl, InputKey.joystick1right, tech);  //LS X+
                config["Left Stick Up"] = GetInputKeyNameDS(ctrl, InputKey.joystick1up, tech);        //LS Y+
                config["Right Stick Left"] = GetInputKeyNameDS(ctrl, InputKey.joystick2left, tech);   //RS X-
                config["Right Stick Down"] = GetInputKeyNameDS(ctrl, InputKey.joystick2down, tech);   //RS Y-
                config["Right Stick Right"] = GetInputKeyNameDS(ctrl, InputKey.joystick2right, tech); //RS X+
                config["Right Stick Up"] = GetInputKeyNameDS(ctrl, InputKey.joystick2up, tech);       //RS Y+
                config["Start"] = GetInputKeyNameDS(ctrl, InputKey.start, tech);                      //Options (Start on DS3)
                config["Select"] = GetInputKeyNameDS(ctrl, InputKey.select, tech);                    //Share (Select on DS3)
                config["PS Button"] = "PS Button";                                                    //PS Button (fixed)
                config["Square"] = GetInputKeyNameDS(ctrl, InputKey.y, tech);                         //Square
                config["Cross"] = GetInputKeyNameDS(ctrl, InputKey.b, tech);                          //Cross
                config["Circle"] = GetInputKeyNameDS(ctrl, InputKey.a, tech);                         //Circle
                config["Triangle"] = GetInputKeyNameDS(ctrl, InputKey.x, tech);                       //Triangle
                config["Left"] = GetInputKeyNameDS(ctrl, InputKey.left, tech);                        //Left
                config["Down"] = GetInputKeyNameDS(ctrl, InputKey.down, tech);                        //Down
                config["Right"] = GetInputKeyNameDS(ctrl, InputKey.right, tech);                      //Right
                config["Up"] = GetInputKeyNameDS(ctrl, InputKey.up, tech);                            //Up
                config["R1"] = GetInputKeyNameDS(ctrl, InputKey.r1, tech);                            //R1
                config["R2"] = GetInputKeyNameDS(ctrl, InputKey.r2, tech);                            //R2
                config["R3"] = GetInputKeyNameDS(ctrl, InputKey.r3, tech);                            //R3
                config["L1"] = GetInputKeyNameDS(ctrl, InputKey.l1, tech);                            //L1
                config["L2"] = GetInputKeyNameDS(ctrl, InputKey.l2, tech);                            //L2
                config["L3"] = GetInputKeyNameDS(ctrl, InputKey.l3, tech);                            //L3
            }

            else if (tech == "XInput")
            {
                config["Left Stick Left"] = GetInputKeyNameX(ctrl, InputKey.joystick1left, tech);    //LS X-
                config["Left Stick Down"] = GetInputKeyNameX(ctrl, InputKey.joystick1down, tech);    //LS Y-
                config["Left Stick Right"] = GetInputKeyNameX(ctrl, InputKey.joystick1right, tech);  //LS X+
                config["Left Stick Up"] = GetInputKeyNameX(ctrl, InputKey.joystick1up, tech);        //LS Y+
                config["Right Stick Left"] = GetInputKeyNameX(ctrl, InputKey.joystick2left, tech);   //RS X-
                config["Right Stick Down"] = GetInputKeyNameX(ctrl, InputKey.joystick2down, tech);   //RS Y-
                config["Right Stick Right"] = GetInputKeyNameX(ctrl, InputKey.joystick2right, tech); //RS X+
                config["Right Stick Up"] = GetInputKeyNameX(ctrl, InputKey.joystick2up, tech);       //RS Y+
                config["Start"] = GetInputKeyNameX(ctrl, InputKey.start, tech);                      //Start
                config["Select"] = GetInputKeyNameX(ctrl, InputKey.select, tech);                    //Back
                config["PS Button"] = "Guide";                                                       //Guide (fixed)
                config["Square"] = GetInputKeyNameX(ctrl, InputKey.y, tech);                         //Y
                config["Cross"] = GetInputKeyNameX(ctrl, InputKey.b, tech);                          //B
                config["Circle"] = GetInputKeyNameX(ctrl, InputKey.a, tech);                         //A
                config["Triangle"] = GetInputKeyNameX(ctrl, InputKey.x, tech);                       //X
                config["Left"] = GetInputKeyNameX(ctrl, InputKey.left, tech);                        //Left
                config["Down"] = GetInputKeyNameX(ctrl, InputKey.down, tech);                        //Down
                config["Right"] = GetInputKeyNameX(ctrl, InputKey.right, tech);                      //Right
                config["Up"] = GetInputKeyNameX(ctrl, InputKey.up, tech);                            //Up
                config["R1"] = GetInputKeyNameX(ctrl, InputKey.r1, tech);                            //RB
                config["R2"] = GetInputKeyNameX(ctrl, InputKey.r2, tech);                            //RT
                config["R3"] = GetInputKeyNameX(ctrl, InputKey.r3, tech);                            //RS
                config["L1"] = GetInputKeyNameX(ctrl, InputKey.l1, tech);                            //LB
                config["L2"] = GetInputKeyNameX(ctrl, InputKey.l2, tech);                            //LT
                config["L3"] = GetInputKeyNameX(ctrl, InputKey.l3, tech);                            //LS
            }

            else if (tech == "SDL")
            {
                config["Left Stick Left"] = GetInputKeyNameSDL(ctrl, InputKey.joystick1left, tech);    //LS X-
                config["Left Stick Down"] = GetInputKeyNameSDL(ctrl, InputKey.joystick1down, tech);    //LS Y-
                config["Left Stick Right"] = GetInputKeyNameSDL(ctrl, InputKey.joystick1right, tech);  //LS X+
                config["Left Stick Up"] = GetInputKeyNameSDL(ctrl, InputKey.joystick1up, tech);        //LS Y+
                config["Right Stick Left"] = GetInputKeyNameSDL(ctrl, InputKey.joystick2left, tech);   //RS X-
                config["Right Stick Down"] = GetInputKeyNameSDL(ctrl, InputKey.joystick2down, tech);   //LS Y-
                config["Right Stick Right"] = GetInputKeyNameSDL(ctrl, InputKey.joystick2right, tech); //LS X+
                config["Right Stick Up"] = GetInputKeyNameSDL(ctrl, InputKey.joystick2up, tech);       //LS Y+
                config["Start"] = GetInputKeyNameSDL(ctrl, InputKey.start, tech);                      //Start
                config["Select"] = GetInputKeyNameSDL(ctrl, InputKey.select, tech);                    //Back
                config["PS Button"] = "Guide";                                                         //Guide
                config["Square"] = GetInputKeyNameSDL(ctrl, InputKey.x, tech);                         //X (or Y on nintendo)
                config["Cross"] = GetInputKeyNameSDL(ctrl, InputKey.a, tech);                          //A (or B on nintendo)
                config["Circle"] = GetInputKeyNameSDL(ctrl, InputKey.b, tech);                         //B (or A on nintendo)
                config["Triangle"] = GetInputKeyNameSDL(ctrl, InputKey.y, tech);                       //Y (or X on nintendo)
                config["Left"] = GetInputKeyNameSDL(ctrl, InputKey.left, tech);                        //Left
                config["Down"] = GetInputKeyNameSDL(ctrl, InputKey.down, tech);                        //Down
                config["Right"] = GetInputKeyNameSDL(ctrl, InputKey.right, tech);                      //Right
                config["Up"] = GetInputKeyNameSDL(ctrl, InputKey.up, tech);                            //Up
                config["R1"] = GetInputKeyNameSDL(ctrl, InputKey.r1, tech);                            //RB
                config["R2"] = GetInputKeyNameSDL(ctrl, InputKey.r2, tech);                            //RT
                config["R3"] = GetInputKeyNameSDL(ctrl, InputKey.r3, tech);                            //RS
                config["L1"] = GetInputKeyNameSDL(ctrl, InputKey.l1, tech);                            //LB
                config["L2"] = GetInputKeyNameSDL(ctrl, InputKey.l2, tech);                            //LT
                config["L3"] = GetInputKeyNameSDL(ctrl, InputKey.l3, tech);                            //LS
            }
            
            //motion controls
            var motionx = config.GetOrCreateContainer("Motion Sensor X");
            motionx["Axis"] = "\"\"";
            motionx["Mirrored"] = "false";
            motionx["Shift"] = "0";
            var motiony = config.GetOrCreateContainer("Motion Sensor Y");
            motiony["Axis"] = "\"\"";
            motiony["Mirrored"] = "false";
            motiony["Shift"] = "0";
            var motionz = config.GetOrCreateContainer("Motion Sensor Z");
            motionz["Axis"] = "\"\"";
            motionz["Mirrored"] = "false";
            motionz["Shift"] = "0";
            var motiong = config.GetOrCreateContainer("Motion Sensor G");
            motiong["Axis"] = "\"\"";
            motiong["Mirrored"] = "false";
            motiong["Shift"] = "0";

            //other settings
            config["Pressure Intensity Button"] = "\"\"";
            config["Pressure Intensity Percent"] = "50";
            config["Left Stick Multiplier"] = "100";
            config["Right Stick Multiplier"] = "100";
            if (tech == "XInput")
                config["Left Stick Deadzone"] = "7849";         //Maybe add a feature
            else
                config["Left Stick Deadzone"] = "50";           //Maybe add a feature
            if (tech == "XInput")
                config["Right Stick Deadzone"] = "8689";        //Maybe add a feature
            else
                config["Right Stick Deadzone"] = "50";          //Maybe add a feature
            if (tech == "XInput")
                config["Left Trigger Threshold"] = "30";
            else
                config["Left Trigger Threshold"] = "0";
            if (tech == "XInput")
                config["Right Trigger Threshold"] = "30";
            else
                config["Right Trigger Threshold"] = "0";
            config["Left Pad Squircling Factor"] = "8000";
            config["Right Pad Squircling Factor"] = "8000";
            config["Color Value R"] = "0";
            config["Color Value G"] = "0";
            if (tech == "DualSense" || tech == "DS4")
                config["Color Value B"] = "20";
            else
                config["Color Value B"] = "0";
            config["Blink LED when battery is below 20%"] = "true";
            config["Use LED as a battery indicator"] = "false";
            if (tech == "DS4")
                config["LED battery indicator brightness"] = "10";
            else
                config["LED battery indicator brightness"] = "50";
            config["Player LED enabled"] = "true";
            config["Enable Large Vibration Motor"] = "true";    //Maybe add a feature
            config["Enable Small Vibration Motor"] = "true";    //Maybe add a feature
            config["Switch Vibration Motors"] = "false";
            config["Mouse Movement Mode"] = "Relative";
            config["Mouse Deadzone X Axis"] = "60";             //Maybe add a feature when managing guns in the future
            config["Mouse Deadzone Y Axis"] = "60";             //Maybe add a feature when managing guns in the future
            config["Mouse Acceleration X Axis"] = "200";        //Maybe add a feature when managing guns in the future
            config["Mouse Acceleration Y Axis"] = "250";        //Maybe add a feature when managing guns in the future
            config["Left Stick Lerp Factor"] = "100";
            config["Right Stick Lerp Factor"] = "100";
            config["Analog Button Lerp Factor"] = "100";
            config["Trigger Lerp Factor"] = "100";
            config["Device Class Type"] = "0";
            config["Vendor ID"] = "1356";
            config["Product ID"] = "616";
            player["Buddy Device"] = "\"\"";            
        }

        private static string GetInputKeyNameDS(Controller c, InputKey key, string tech)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return "Circle";
                        case 1: return "Cross";
                        case 2: return "Square";
                        case 3: return "Triangle";
                        case 4:
                            if (tech == "DS3") return "Select";
                            else return "Share";
                        case 5: return "PS Button";
                        case 6:
                            if (tech == "DS3") return "Start";
                            else return "Options";
                        case 7: return "L3";
                        case 8: return "R3";
                        case 9: return "L1";
                        case 10: return "R1";
                        case 11: return "Up";
                        case 12: return "Down";
                        case 13: return "Left";
                        case 14: return "Right";
                    }
                }
                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LS X+";
                            else return "LS X-";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LS Y-";
                            else return "LS Y+";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RS X+";
                            else return "RS X-";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RS Y-";
                            else return "RS Y+";
                        case 4:return "L2";
                        case 5: return "R2";
                    }
                }
            }
            return "\"\"";
        }

        private static string GetInputKeyNameX(Controller c, InputKey key, string tech)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return "B";
                        case 1: return "A";
                        case 2: return "X";
                        case 3: return "Y";
                        case 4: return "LB";
                        case 5: return "RB";
                        case 6: return "Back";
                        case 7: return "Start";
                        case 8: return "LS";
                        case 9: return "RS";
                        case 10: return "Guide";
                    }
                }
                
                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LS X+";
                            else return "LS X-";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LS Y-";
                            else return "LS Y+";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RS X+";
                            else return "RS X-";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RS Y-";
                            else return "RS Y+";
                        case 4: return "LT";
                        case 5: return "RT";
                    }
                }
                
                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "Up";
                        case 2: return "Right";
                        case 4: return "Down";
                        case 8: return "Left";
                    }
                }
            }
                return "\"\"";
        }

        private static string GetInputKeyNameSDL(Controller c, InputKey key, string tech)
        {
            Int64 pid = -1;

            // If controller is nintendo, A/B and X/Y are reversed
            bool revertbuttons = (c.VendorID == USB_VENDOR.NINTENDO) || (Program.SystemConfig.isOptSet("rpcs3_gamepadbuttons") && Program.SystemConfig.getOptBoolean("rpcs3_gamepadbuttons"));

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];

            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return revertbuttons ? "A" : "B";
                        case 1: return revertbuttons ? "B" : "A";
                        case 2: return revertbuttons ? "Y" : "X";
                        case 3: return revertbuttons ? "X" : "Y";
                        case 4: return "Back";
                        case 5: return "Guide";
                        case 6: return "Start";
                        case 7: return "LS";
                        case 8: return "RS";
                        case 9: return "LB";
                        case 10: return "RB";
                        case 11: return "Up";
                        case 12: return "Down";
                        case 13: return "Left";
                        case 14: return "Right";
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LS X+";
                            else return "LS X-";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LS Y-";
                            else return "LS Y+";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RS X+";
                            else return "RS X-";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RS Y-";
                            else return "RS Y+";
                        case 4: return "LT";
                        case 5: return "RT";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "Up";
                        case 2: return "Right";
                        case 4: return "Down";
                        case 8: return "Left";
                    }
                }
            }

            return "\"\"";
        }

        /// <summary>
        /// Search keyboard keycodes
        /// </summary>
        /// <param name="sdlCode"></param>
        /// <returns></returns>
        private static string SdlToKeyCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x0D: return "Enter";
                case 0x00: return "\"\"";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x1B: return "Esc";
                case 0x20: return "Space";
                case 0x21: return "\"!\"";
                case 0x22: return "\"" + @"\" + "\"" + "\"";
                case 0x23: return "\"#\"";
                case 0x24: return "$";
                case 0x25: return "\"%\"";
                case 0x26: return "\"&\"";
                case 0x27: return @"\";
                case 0x28: return "(";
                case 0x29: return ")";
                case 0x2A: return "\"*\"";
                case 0x2B: return "\"+\"";
                case 0x2C: return "\",\"";
                case 0x2D: return "\"-\"";
                case 0x2E: return "\".\"";
                case 0x2F: return "/";
                case 0x30: return "0";
                case 0x31: return "1";
                case 0x32: return "2";
                case 0x33: return "3";
                case 0x34: return "4";
                case 0x35: return "5";
                case 0x36: return "6";
                case 0x37: return "7";
                case 0x38: return "8";
                case 0x39: return "9";
                case 0x3A: return "\":\"";
                case 0x3B: return ";";
                case 0x3C: return "<";
                case 0x3D: return "=";
                case 0x3F: return ">";
                case 0x40: return "\"" + "@" + "\"";
                case 0x5B: return "\"[\"";
                case 0x5C: return @"\";
                case 0x5D: return "\"]\"";
                case 0x5E: return "^";
                case 0x5F: return "_";
                case 0x60: return "\"'\"";
                case 0x61: return "A";
                case 0x62: return "B";
                case 0x63: return "C";
                case 0x64: return "D";
                case 0x65: return "E";
                case 0x66: return "F";
                case 0x67: return "G";
                case 0x68: return "H";
                case 0x69: return "I";
                case 0x6A: return "J";
                case 0x6B: return "K";
                case 0x6C: return "L";
                case 0x6D: return "M";
                case 0x6E: return "N";
                case 0x6F: return "O";
                case 0x70: return "P";
                case 0x71: return "Q";
                case 0x72: return "R";
                case 0x73: return "S";
                case 0x74: return "T";
                case 0x75: return "U";
                case 0x76: return "V";
                case 0x77: return "W";
                case 0x78: return "X";
                case 0x79: return "Y";
                case 0x7A: return "Z";
                case 0x7F: return "Del";
                case 0x40000039: return "CapsLock";
                case 0x4000003A: return "F1";
                case 0x4000003B: return "F2";
                case 0x4000003C: return "F3";
                case 0x4000003D: return "F4";
                case 0x4000003E: return "F5";
                case 0x4000003F: return "F6";
                case 0x40000040: return "F7";
                case 0x40000041: return "F8";
                case 0x40000042: return "F9";
                case 0x40000043: return "F10";
                case 0x40000044: return "F11";
                case 0x40000045: return "F12";
                case 0x40000046: return "PrintScreen";
                case 0x40000047: return "ScrollLock";
                case 0x40000048: return "Pause";
                case 0x40000049: return "Ins";
                case 0x4000004A: return "Home";
                case 0x4000004B: return "PgUp";
                case 0x4000004D: return "End";
                case 0x4000004E: return "PgDown";
                case 0x4000004F: return "Right";
                case 0x40000050: return "Left";
                case 0x40000051: return "Down";
                case 0x40000052: return "Up";
                case 0x40000053: return "NumLock";
                case 0x40000054: return "Num+/";
                case 0x40000055: return "Num+*";
                case 0x40000056: return "Num+-";
                case 0x40000057: return "Num++";
                case 0x40000058: return "Num+Enter";
                case 0x40000059: return "Num+1";
                case 0x4000005A: return "Num+2";
                case 0x4000005B: return "Num+3";
                case 0x4000005C: return "Num+4";
                case 0x4000005D: return "Num+5";
                case 0x4000005E: return "Num+6";
                case 0x4000005F: return "Num+7";
                case 0x40000060: return "Num+8";
                case 0x40000061: return "Num+9";
                case 0x40000062: return "Num+0";
                case 0x40000063: return "Num+.";
                case 0x40000067: return "Num+=";
                case 0x40000068: return "F13";
                case 0x40000069: return "F14";
                case 0x4000006A: return "F15";
                case 0x4000006B: return "F16";
                case 0x4000006C: return "F17";
                case 0x4000006D: return "F18";
                case 0x4000006E: return "F19";
                case 0x4000006F: return "F20";
                case 0x40000070: return "F21";
                case 0x40000071: return "F22";
                case 0x40000072: return "F23";
                case 0x40000073: return "F24";
                case 0x40000074: return "Execute";
                case 0x40000075: return "Help";
                case 0x40000076: return "Menu";
                case 0x40000077: return "Select";
                case 0x40000078: return "Stop";
                case 0x40000079: return "Again";
                case 0x4000007A: return "Undo";
                case 0x4000007B: return "Cut";
                case 0x4000007C: return "Copy";
                case 0x4000007D: return "Paste";
                case 0x4000007E: return "Menu";
                case 0x4000007F: return "Volume Mute";
                case 0x40000080: return "Volume Up";
                case 0x40000081: return "Volume Down";
                case 0x40000085: return "Num+,";
                case 0x400000E0: return "Ctrl Left";
                case 0x400000E1: return "Shift Left";
                case 0x400000E2: return "Alt";
                case 0x400000E4: return "Ctrl Right";
                case 0x400000E5: return "Shift Right";
                case 0x40000101: return "Mode";
                case 0x40000102: return "Media Next";
                case 0x40000103: return "Media Previous";
                case 0x40000105: return "Media Play";
            }
            return "\"\"";
        }
    }
}
