using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Linq.Expressions;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class Model3Generator : Generator
    {

        /// <summary>
        /// Cf. https://github.com/trzy/Supermodel
        /// </summary>
        /// <param name="ini"></param>
        private void UpdateSdlControllersWithHints(IniFile pcsx2ini)
        {
            var hints = new List<string>();
            hints.Add("SDL_JOYSTICK_HIDAPI_WII = 1");

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        /// <summary>
        /// Create controller configuration
        /// </summary>
        /// <param name="ini"></param>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            UpdateSdlControllersWithHints(ini);

            //.ini file for supermodel has entry for 2 players.
            //If 2 controllers or more are connected : get controllers from p1 and p2
            //If only 1 controller is connected : get player 1 controller
            if (Program.Controllers.Count > 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                var c2 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                if (c1.IsKeyboard)
                    WriteKeyboardMapping(ini, c1);
                else
                    WriteJoystickMapping(ini, c1, c2);
            }
            else if (Program.Controllers.Count == 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                if (c1.IsKeyboard)
                    WriteKeyboardMapping(ini, c1);
                else
                    WriteJoystickMapping(ini, c1);
            }
            else if (Program.Controllers.Count == 0)
                return;
        }

        /// <summary>
        /// Gamepad
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        // All mappings generated here are detailed in the wiki, this is the most balanced mapping after intensive testing, if users need different mapping they might disable autoconfiguration
        private void WriteJoystickMapping(IniFile ini, Controller c1, Controller c2 = null)
        {
            if (c1 == null || c1.Config == null)
                return;

            //initialize controller index, supermodel uses directinput controller index (+1)
            //only index of player 1 is initialized as there might be only 1 controller at that point
            int j2index = -1;
            int j1index = c1.SdlController !=null ? c1.SdlController.Index + 1 : c1.DeviceIndex + 1;

            //If a secod controller is connected, get controller index of player 2, if there is no 2nd controller, just increment the index
            if (c2 != null && c2.Config != null)
                j2index = c2.SdlController != null ? c2.SdlController.Index + 1 : c2.DeviceIndex + 1;

            //initialize tech : as default we will use sdl instead of dinput, as there are less differences in button mappings in sdl !
            string tech = "sdl";

            //Variables n1 and n2 will be used to check if controllers are "nintendo", if this is the case, buttons will be reverted
            string n1 = null;
            string n2 = null;

            //Main tech sent to the .ini file will be based on technology of controller of player 1
            //Unfortunately, supermodel does not allow to enter a different input technology for p1 and p2, moreover xinput controllers are not recognizes when inputting sdl !
            //This means that if 2 players are playing, they will need to use the same controller type, either 2 xinput, either 2 sdl
            if (c1.IsXInputDevice)
                tech = "xinput";
            
            //Check if controllers are NINTENDO, will be used to revert buttons for sdl
            if (c1.VendorID == USB_VENDOR.NINTENDO)
                n1 = "nintendo";
            if (c2 != null && c2.Config != null && c2.VendorID == USB_VENDOR.NINTENDO)
                n2 = "nintendo";

            // Override tech if option is set in es_features
            if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "nintendo")
            {
                tech = "sdl";
                n1 = "nintendo";
                n2 = "nintendo";
            }
            else if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "sdl")
                tech = "sdl";
            else if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "xinput")
                tech = "xinput";

            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();

            bool multigun = SystemConfig.isOptSet("multigun") && SystemConfig.getOptBoolean("multigun");
            string mouseIndex1 = "1";
            string mouseIndex2 = "2";

            if (gunCount > 0 && guns.Length > 0)
            {
                mouseIndex1 = (guns[0].Index + 1).ToString();
                if (gunCount > 1 && guns.Length > 1)
                    mouseIndex2 = (guns[1].Index + 1).ToString();
            }

            if (SystemConfig.isOptSet("supermodel_gun1") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun1"]))
                mouseIndex1 = SystemConfig["supermodel_gun1"];
            if (SystemConfig.isOptSet("supermodel_gun2") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun2"]))
                mouseIndex2 = SystemConfig["supermodel_gun2"];

            string mouse1 = "MOUSE" + mouseIndex1;
            string mouse2 = "MOUSE" + mouseIndex2;

            if (!multigun)
            {
                mouse1 = mouse2 = "MOUSE";
                ini.WriteValue(" Global ", "Crosshairs", "1");
            }
            else
                ini.WriteValue(" Global ", "Crosshairs", "3");

            // Force player index if option is set in es_features
            if (SystemConfig.isOptSet("model3_p1index") && !string.IsNullOrEmpty(SystemConfig["model3_p1index"]))
                j1index = SystemConfig["model3_p1index"].ToInteger();
            if (SystemConfig.isOptSet("model3_p2index") && !string.IsNullOrEmpty(SystemConfig["model3_p2index"]))
                j2index = SystemConfig["model3_p2index"].ToInteger();

            bool multiplayer = j2index != -1;
            bool enableServiceMenu = SystemConfig.isOptSet("m3_service") && SystemConfig.getOptBoolean("m3_service");

            //Now write buttons mapping for generic sdl case (when player 1 controller is NOT XINPUT)
            if (tech == "sdl")
            {
                if (multigun)
                    ini.WriteValue(" Global ", "InputSystem", "rawinput");
                else
                    ini.WriteValue(" Global ", "InputSystem", "sdl");

                //common - start to start and select to input coins
                //service menu and test menu can be accessed via L3 and R3 buttons if option is enabled

                ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,JOY" + j1index + "_BUTTON7\"");
                ini.WriteValue(" Global ", "InputCoin1", "\"KEY_3,JOY" + j1index + "_BUTTON5\"");
                ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_5,JOY" + j1index + "_BUTTON8\"" : "\"KEY_5\"");
                ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_6,JOY" + j1index + "_BUTTON9\"" : "\"KEY_6\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputStart2", "\"KEY_2,JOY" + j2index + "_BUTTON7\"");
                    ini.WriteValue(" Global ", "InputCoin2", "\"KEY_4,JOY" + j2index + "_BUTTON5\"");
                    ini.WriteValue(" Global ", "InputServiceB", enableServiceMenu ? "\"KEY_7,JOY" + j2index + "_BUTTON8\"" : "\"KEY_7\"");
                    ini.WriteValue(" Global ", "InputTestB", enableServiceMenu ? "\"KEY_8,JOY" + j2index + "_BUTTON9\"" : "\"KEY_8\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputStart2");
                    ini.Remove(" Global ", "InputCoin2");
                    ini.Remove(" Global ", "InputServiceB");
                    ini.Remove(" Global ", "InputTestB");
                }
                    
                //4-way digital joysticks - directional stick
                ini.WriteValue(" Global ", "InputJoyUp", "\"JOY" + j1index + "_YAXIS_NEG,JOY" + j1index + "_POV1_UP\"");
                ini.WriteValue(" Global ", "InputJoyDown", "\"JOY" + j1index + "_YAXIS_POS,JOY" + j1index + "_POV1_DOWN\"");
                ini.WriteValue(" Global ", "InputJoyLeft", "\"JOY" + j1index + "_XAXIS_NEG,JOY" + j1index + "_POV1_LEFT\"");
                ini.WriteValue(" Global ", "InputJoyRight", "\"JOY" + j1index + "_XAXIS_POS,JOY" + j1index + "_POV1_RIGHT\"");
                
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoyUp2", "\"JOY" + j2index + "_YAXIS_NEG,JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputJoyDown2", "\"JOY" + j2index + "_YAXIS_POS,JOY" + j2index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputJoyLeft2", "\"JOY" + j2index + "_XAXIS_NEG,JOY" + j2index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputJoyRight2", "\"JOY" + j2index + "_XAXIS_POS,JOY" + j2index + "_POV1_RIGHT\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputJoyUp2");
                    ini.Remove(" Global ", "InputJoyDown2");
                    ini.Remove(" Global ", "InputJoyLeft2");
                    ini.Remove(" Global ", "InputJoyRight2");
                }
                
                //Fighting game buttons - used for virtua fighters will be mapped with the 4 buttons
                ini.WriteValue(" Global ", "InputPunch", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputKick", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputGuard", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputEscape", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");
                
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputPunch2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON4\"" : "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputKick2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON3\"" : "\"JOY" + j2index + "_BUTTON4\"");
                    ini.WriteValue(" Global ", "InputGuard2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputEscape2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON1\"" : "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputPunch2");
                    ini.Remove(" Global ", "InputKick2");
                    ini.Remove(" Global ", "InputGuard2");
                    ini.Remove(" Global ", "InputEscape2");
                }
                
                //Spikeout buttons
                ini.WriteValue(" Global ", "InputShift", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1,JOY" + j1index + "_BUTTON11\"" : "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON11\"");
                ini.WriteValue(" Global ", "InputBeat", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputCharge", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputJump", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");

                //Virtua Striker buttons
                ini.WriteValue(" Global ", "InputShortPass", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputLongPass", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputShoot", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputShortPass2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON4\"" : "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputLongPass2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputShoot2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON1\"" : "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputShortPass2");
                    ini.Remove(" Global ", "InputLongPass2");
                    ini.Remove(" Global ", "InputShoot2");
                }
                

                //Steering wheel - left analog stick horizontal axis
                ini.WriteValue(" Global ", "InputSteeringLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteeringRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteering", "\"JOY" + j1index + "_XAXIS\"");

                //Pedals - accelerate with R2, brake with L2
                ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_RYAXIS_POS\"");

                //Up/down shifter manual transmission (all racers) - L1 gear down and R1 gear up
                ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_BUTTON11\"");
                ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_BUTTON10\"");

                //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with right stick (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_RXAXIS_POS\"");
                ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_ZAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputGearShiftN", "NONE");

                //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race) - the 4 buttons will be used to change view in the games listed
                ini.WriteValue(" Global ", "InputVR1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputVR2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputVR3", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputVR4", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2) - use north button to change view in these games
                ini.WriteValue(" Global ", "InputViewChange", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");

                //Handbrake (Dirt Devils, Sega Rally 2) - south button to handbrake in these games
                ini.WriteValue(" Global ", "InputHandBrake", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                //Harley-Davidson controls
                ini.WriteValue(" Global ", "InputRearBrake", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputMusicSelect", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Virtual On macros
                ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"JOY" + j1index + "_ZAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyForward", "\"JOY" + j1index + "_YAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"JOY" + j1index + "_YAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"JOY" + j1index + "_XAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"JOY" + j1index + "_XAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyJump", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputTwinJoyCrouch", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                //Virtual On individual joystick mapping
                ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

                //Virtual On buttons
                ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"JOY" + j1index + "_RYAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4,JOY" + j1index + "_BUTTON10\"" : "\"JOY" + j1index + "_BUTTON3,JOY" + j1index + "_BUTTON10\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1,JOY" + j1index + "_BUTTON11\"" : "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON11\"");

                //Analog joystick (Star Wars Trilogy)
                ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyX", "\"JOY" + j1index + "_XAXIS_INV," + mouse1 + "_XAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyY", "\"JOY" + j1index + "_YAXIS_INV," + mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger", n1 == "nintendo" ? "\"JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON4," + mouse1 + "_LEFT_BUTTON\"" : "\"JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON3," + mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2," + mouse1 + "_RIGHT_BUTTON\"" : "\"JOY" + j1index + "_BUTTON1," + mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

                //Light guns (Lost World) - MOUSE
                ini.WriteValue(" Global ", "InputGunLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunX", "\"" + mouse1 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputGunY", "\"" + mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputTrigger", "\"" + mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputOffscreen", "\"" + mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAutoTrigger", "1");
                ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");
                
                if (multigun)
                {
                    ini.WriteValue(" Global ", "InputGunX2", "\"" + mouse2 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputGunY2", "\"" + mouse2 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputTrigger2", "\"" + mouse2 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputOffscreen2", "\"" + mouse2 + "_RIGHT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAutoTrigger2", "1");
                }
                else
                {
                    ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAutoTrigger2", "1");
                }

                //Analog guns (Ocean Hunter, LA Machineguns) - MOUSE
                ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + mouse1 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"" + mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"" + mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");

                if (multigun)
                {
                    ini.WriteValue(" Global ", "InputAnalogGunX2", "\"" + mouse2 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY2", "\"" + mouse2 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"" + mouse2 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"" + mouse2 + "_RIGHT_BUTTON\"");
                }
                else
                {
                    ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");
                }

                //Ski Champ controls
                ini.WriteValue(" Global ", "InputSkiLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputSkiY", "\"JOY" + j1index + "_ZAXIS\"");
                ini.WriteValue(" Global ", "InputSkiPollLeft", "\"JOY" + j1index + "_RYAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiPollRight", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiSelect1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputSkiSelect2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputSkiSelect3", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Magical Truck Adventure controls
                ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLever1", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputMagicalPedal1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputMagicalLever2", "\"JOY" + j2index + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputMagicalPedal2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputMagicalLever2");
                    ini.Remove(" Global ", "InputMagicalPedal2");
                }

                //Sega Bass Fishing / Get Bass controls
                ini.WriteValue(" Global ", "InputFishingRodLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputFishingRodY", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickX", "\"JOY" + j1index + "_ZAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickY", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputFishingReel", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputFishingCast", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputFishingSelect", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");
                ini.WriteValue(" Global ", "InputFishingTension", "\"NONE\"");

                //deadzones - set 5 as default deadzone, good compromise to avoid joystick drift
                ini.WriteValue(" Global ", "InputJoy" + j1index + "XDeadZone", "5");
                ini.WriteValue(" Global ", "InputJoy" + j1index + "YDeadZone", "5");
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoy" + j2index + "XDeadZone", "5");
                    ini.WriteValue(" Global ", "InputJoy" + j2index + "YDeadZone", "5");
                }

                //other stuff
                ini.WriteValue(" Global ", "DirectInputConstForceLeftMax", "100");
                ini.WriteValue(" Global ", "DirectInputConstForceRightMax", "100");
                ini.WriteValue(" Global ", "DirectInputSelfCenterMax", "100");
                ini.WriteValue(" Global ", "DirectInputFrictionMax", "100");
                ini.WriteValue(" Global ", "DirectInputVibrateMax", "100");
            }

            //xinput case - this is used when player 1 controller is a xinput controller
            else
            {
                if (multigun)
                    ini.WriteValue(" Global ", "InputSystem", "rawinput");
                else
                    ini.WriteValue(" Global ", "InputSystem", "xinput");

                //common - L3 and R3 will be used to navigate service menu
                ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,JOY" + j1index + "_BUTTON8\"");
                ini.WriteValue(" Global ", "InputCoin1", "\"KEY_3,JOY" + j1index + "_BUTTON7\"");
                ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_5,JOY" + j1index + "_BUTTON9\"" : "\"KEY_5\"");
                ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_6,JOY" + j1index + "_BUTTON10\"" : "\"KEY_6\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputStart2", "\"KEY_2,JOY" + j2index + "_BUTTON8\"");
                    ini.WriteValue(" Global ", "InputCoin2", "\"KEY_4,JOY" + j2index + "_BUTTON7\"");
                    ini.WriteValue(" Global ", "InputServiceB", enableServiceMenu ? "\"KEY_7,JOY" + j2index + "_BUTTON9\"" : "\"KEY_7\"");
                    ini.WriteValue(" Global ", "InputTestB", enableServiceMenu ? "\"KEY_8,JOY" + j2index + "_BUTTON10\"" : "\"KEY_8\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputStart2");
                    ini.Remove(" Global ", "InputCoin2");
                    ini.Remove(" Global ", "InputServiceB");
                    ini.Remove(" Global ", "InputTestB");
                }

                //4-way digital joysticks
                ini.WriteValue(" Global ", "InputJoyUp", "\"JOY" + j1index + "_YAXIS_NEG,JOY" + j1index + "_POV1_UP\"");
                ini.WriteValue(" Global ", "InputJoyDown", "\"JOY" + j1index + "_YAXIS_POS,JOY" + j1index + "_POV1_DOWN\"");
                ini.WriteValue(" Global ", "InputJoyLeft", "\"JOY" + j1index + "_XAXIS_NEG,JOY" + j1index + "_POV1_LEFT\"");
                ini.WriteValue(" Global ", "InputJoyRight", "\"JOY" + j1index + "_XAXIS_POS,JOY" + j1index + "_POV1_RIGHT\"");
                
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoyUp2", "\"JOY" + j2index + "_YAXIS_NEG,JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputJoyDown2", "\"JOY" + j2index + "_YAXIS_POS,JOY" + j2index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputJoyLeft2", "\"JOY" + j2index + "_XAXIS_NEG,JOY" + j2index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputJoyRight2", "\"JOY" + j2index + "_XAXIS_POS,JOY" + j2index + "_POV1_RIGHT\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputJoyUp2");
                    ini.Remove(" Global ", "InputJoyDown2");
                    ini.Remove(" Global ", "InputJoyLeft2");
                    ini.Remove(" Global ", "InputJoyRight2");
                }

                //Fighting game buttons
                ini.WriteValue(" Global ", "InputPunch", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputKick", "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputGuard", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputEscape", "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputPunch2", "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputKick2", "\"JOY" + j2index + "_BUTTON4\"");
                    ini.WriteValue(" Global ", "InputGuard2", "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputEscape2", "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputPunch2");
                    ini.Remove(" Global ", "InputKick2");
                    ini.Remove(" Global ", "InputGuard2");
                    ini.Remove(" Global ", "InputEscape2");
                }

                //Spikeout buttons
                ini.WriteValue(" Global ", "InputShift", "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON6\"");
                ini.WriteValue(" Global ", "InputBeat", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputCharge", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputJump", "\"JOY" + j1index + "_BUTTON4\"");

                //Virtua Striker buttons
                ini.WriteValue(" Global ", "InputShortPass", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputLongPass", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputShoot", "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputShortPass2", "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputLongPass2", "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputShoot2", "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputShortPass2");
                    ini.Remove(" Global ", "InputLongPass2");
                    ini.Remove(" Global ", "InputShoot2");
                }

                //Steering wheel
                ini.WriteValue(" Global ", "InputSteeringLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteeringRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteering", "\"JOY" + j1index + "_XAXIS\"");

                //Pedals
                ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_ZAXIS_POS\"");

                //Up/down shifter manual transmission (all racers)
                ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_BUTTON6\"");
                ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_BUTTON5\"");

                //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race)
                ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_RYAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_RYAXIS_POS\"");
                ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_RXAXIS_POS\"");
                ini.WriteValue(" Global ", "InputGearShiftN", "NONE");

                //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race)
                ini.WriteValue(" Global ", "InputVR1", "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputVR2", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputVR3", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputVR4", "\"JOY" + j1index + "_BUTTON2\"");

                //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2)
                ini.WriteValue(" Global ", "InputViewChange", "\"JOY" + j1index + "_BUTTON4\"");

                //Handbrake (Dirt Devils, Sega Rally 2)
                ini.WriteValue(" Global ", "InputHandBrake", "\"JOY" + j1index + "_BUTTON1\"");

                //Harley-Davidson controls
                ini.WriteValue(" Global ", "InputRearBrake", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputMusicSelect", "\"JOY" + j1index + "_BUTTON2\"");

                //Virtual On macros
                ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"JOY" + j1index + "_RXAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyForward", "\"JOY" + j1index + "_YAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"JOY" + j1index + "_YAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"JOY" + j1index + "_XAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"JOY" + j1index + "_XAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyJump", "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputTwinJoyCrouch", "\"JOY" + j1index + "_BUTTON1\"");

                //Virtual On individual joystick mapping
                ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

                //Virtual On buttons
                ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo1", "\"JOY" + j1index + "_BUTTON3,JOY" + j1index + "_BUTTON\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo2", "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON6\"");

                //Analog joystick (Star Wars Trilogy)
                ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyX", "\"" + mouse1 + "_XAXIS_INV,JOY" + j1index + "_XAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyY", "\"" + mouse1 + "_YAXIS,JOY" + j1index + "_YAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger", "\"" + mouse1 + "_LEFT_BUTTON,JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent", "\"" + mouse1 + "_RIGHT_BUTTON,JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

                //Light guns (Lost World)
                ini.WriteValue(" Global ", "InputGunLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunX", "\"" + mouse1 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputGunY", "\"" + mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputTrigger", "\"" + mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputOffscreen", "\"" + mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAutoTrigger", "1");
                ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");

                if (multigun)
                {
                    ini.WriteValue(" Global ", "InputGunX2", "\"" + mouse2 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputGunY2", "\"" + mouse2 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputTrigger2", "\"" + mouse2 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputOffscreen2", "\"" + mouse2 + "_RIGHT_BUTTON\"");
                }
                else
                {
                    ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
                }

                ini.WriteValue(" Global ", "InputAutoTrigger2", "1");

                //Analog guns (Ocean Hunter, LA Machineguns)
                ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + mouse1 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"" + mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"" + mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");
                
                if (multigun)
                {
                    ini.WriteValue(" Global ", "InputAnalogGunX2", "\"" + mouse2 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY2", "\"" + mouse2 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"" + mouse2 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"" + mouse2 + "_RIGHT_BUTTON\"");
                }
                else
                {
                    ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");
                }


                //Ski Champ controls
                ini.WriteValue(" Global ", "InputSkiLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputSkiY", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputSkiPollLeft", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiPollRight", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiSelect1", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputSkiSelect2", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputSkiSelect3", "\"JOY" + j1index + "_BUTTON2\"");

                //Magical Truck Adventure controls
                ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLever1", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputMagicalPedal1", "\"JOY" + j1index + "_BUTTON1\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputMagicalLever2", "\"JOY" + j2index + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputMagicalPedal2", "\"JOY" + j2index + "_BUTTON1\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputMagicalLever2");
                    ini.Remove(" Global ", "InputMagicalPedal2");
                }

                //Sega Bass Fishing / Get Bass controls
                ini.WriteValue(" Global ", "InputFishingRodLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputFishingRodY", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickX", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickY", "\"JOY" + j1index + "_RYAXIS\"");
                ini.WriteValue(" Global ", "InputFishingReel", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputFishingCast", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputFishingSelect", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputFishingTension", "\"NONE\"");

                //deadzones
                ini.WriteValue(" Global ", "InputJoy" + j1index + "XDeadZone", "5");
                ini.WriteValue(" Global ", "InputJoy" + j1index + "YDeadZone", "5");
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoy" + j2index + "XDeadZone", "5");
                    ini.WriteValue(" Global ", "InputJoy" + j2index + "YDeadZone", "5");
                }

                //other stuff
                ini.WriteValue(" Global ", "XInputConstForceThreshold", "20");
                ini.WriteValue(" Global ", "XInputConstForceMax", "40");
                ini.WriteValue(" Global ", "XInputVibrateMax", "100");
            }
        }
        /// <summary>
        /// Keyboard
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="c1"></param>
        // no need to diferentiate qwerty and azerty (on azerty keyboard A is recognized as Q)
        private void WriteKeyboardMapping(IniFile ini, Controller c1)
        {
            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();

            bool multigun = SystemConfig.isOptSet("multigun") && SystemConfig.getOptBoolean("multigun");
            string mouseIndex1 = "1";
            string mouseIndex2 = "2";

            if (gunCount > 0 && guns.Length > 0)
            {
                mouseIndex1 = (guns[0].Index + 1).ToString();
                if (gunCount > 1 && guns.Length > 1)
                    mouseIndex2 = (guns[1].Index + 1).ToString();
            }

            if (SystemConfig.isOptSet("supermodel_gun1") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun1"]))
                mouseIndex1 = SystemConfig["supermodel_gun1"];
            if (SystemConfig.isOptSet("supermodel_gun2") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun2"]))
                mouseIndex2 = SystemConfig["supermodel_gun2"];

            if (multigun)
                ini.WriteValue(" Global ", "InputSystem", "rawinput");
            else
                ini.WriteValue(" Global ", "InputSystem", "dinput");

            string mouse1 = "MOUSE" + mouseIndex1;
            string mouse2 = "MOUSE" + mouseIndex2;

            if (!multigun)
            {
                mouse1 = mouse2 = "MOUSE";
                ini.WriteValue(" Global ", "Crosshairs", "1");
            }
            else
                ini.WriteValue(" Global ", "Crosshairs", "3");

            //common
            ini.WriteValue(" Global ", "InputStart1", "\"KEY_1\"");
            ini.WriteValue(" Global ", "InputStart2", "\"KEY_2\"");
            ini.WriteValue(" Global ", "InputCoin1", "\"KEY_3\"");
            ini.WriteValue(" Global ", "InputCoin2", "\"KEY_4\"");
            ini.WriteValue(" Global ", "InputServiceA", "\"KEY_5\"");
            ini.WriteValue(" Global ", "InputServiceB", "\"KEY_7\"");
            ini.WriteValue(" Global ", "InputTestA", "\"KEY_6\"");
            ini.WriteValue(" Global ", "InputTestB", "\"KEY_8\"");

            //4-way digital joysticks
            ini.WriteValue(" Global ", "InputJoyUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputJoyDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputJoyLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputJoyRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputJoyUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputJoyDown2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputJoyLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputJoyRight2", "\"NONE\"");

            //Fighting game buttons
            ini.WriteValue(" Global ", "InputPunch", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputKick", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputGuard", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputEscape", "\"KEY_F\"");
            ini.WriteValue(" Global ", "InputPunch2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputKick2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGuard2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputEscape2", "\"NONE\"");

            //Spikeout buttons
            ini.WriteValue(" Global ", "InputShift", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputBeat", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputCharge", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputJump", "\"KEY_F\"");

            //Virtua Striker buttons
            ini.WriteValue(" Global ", "InputShortPass", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputLongPass", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputShoot", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputShortPass2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputLongPass2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputShoot2", "\"NONE\"");

            //Steering wheel
            ini.WriteValue(" Global ", "InputSteeringLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputSteeringRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputSteering", "\"NONE\"");

            //Pedals
            ini.WriteValue(" Global ", "InputAccelerator", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputBrake", "\"KEY_DOWN\"");

            //Up/down shifter manual transmission (all racers)
            ini.WriteValue(" Global ", "InputGearShiftUp", "\"KEY_Y\"");
            ini.WriteValue(" Global ", "InputGearShiftDown", "\"KEY_H\"");

            //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race)
            ini.WriteValue(" Global ", "InputGearShift1", "\"KEY_Q\"");
            ini.WriteValue(" Global ", "InputGearShift2", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputGearShift3", "\"KEY_E\"");
            ini.WriteValue(" Global ", "InputGearShift4", "\"KEY_R\"");
            ini.WriteValue(" Global ", "InputGearShiftN", "\"KEY_T\"");

            //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race)
            ini.WriteValue(" Global ", "InputVR1", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputVR2", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputVR3", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputVR4", "\"KEY_F\"");

            //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2)
            ini.WriteValue(" Global ", "InputViewChange", "\"KEY_A\"");

            //Handbrake (Dirt Devils, Sega Rally 2)
            ini.WriteValue(" Global ", "InputHandBrake", "\"KEY_S\"");

            //Harley-Davidson controls
            ini.WriteValue(" Global ", "InputRearBrake", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputMusicSelect", "\"KEY_D\"");

            //Virtual On macros
            ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"KEY_Q\"");
            ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputTwinJoyForward", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputTwinJoyJump", "\"KEY_E\"");
            ini.WriteValue(" Global ", "InputTwinJoyCrouch", "\"KEY_R\"");

            //Virtual On individual joystick mapping
            ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

            //Virtual On buttons
            ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputTwinJoyTurbo1", "\"KEY_Z\"");
            ini.WriteValue(" Global ", "InputTwinJoyTurbo2", "\"KEY_X\"");

            //Analog joystick (Star Wars Trilogy)
            ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputAnalogJoyX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogJoyY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogJoyTrigger", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputAnalogJoyEvent", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

            //Light guns (Lost World)
            ini.WriteValue(" Global ", "InputGunLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputGunRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputGunUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputGunDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputGunX", "\"" + mouse1 + "_XAXIS\"");
            ini.WriteValue(" Global ", "InputGunY", "\"" + mouse1 + "_YAXIS\"");
            ini.WriteValue(" Global ", "InputTrigger", "\"KEY_A," + mouse1 + "_LEFT_BUTTON\"");
            ini.WriteValue(" Global ", "InputOffscreen", "\"KEY_S," + mouse1 + "_RIGHT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAutoTrigger", "1");
            ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");

            if (multigun)
            {
                ini.WriteValue(" Global ", "InputGunX2", "\"" + mouse2 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputGunY2", "\"" + mouse2 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputTrigger2", "\"" + mouse2 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputOffscreen2", "\"" + mouse2 + "_RIGHT_BUTTON\"");
            }
            else
            {
                ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
            }
            ini.WriteValue(" Global ", "InputAutoTrigger2", "1");

            //Analog guns (Ocean Hunter, LA Machineguns)
            ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputAnalogGunRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputAnalogGunUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputAnalogGunDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + mouse1 + "_XAXIS\"");
            ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + mouse1 + "_YAXIS\"");
            ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"KEY_A," + mouse1 + "_LEFT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"KEY_S," + mouse1 + "_RIGHT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");

            if (multigun)
            {
                ini.WriteValue(" Global ", "InputAnalogGunX2", "\"" + mouse2 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogGunY2", "\"" + mouse2 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"" + mouse2 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"" + mouse2 + "_RIGHT_BUTTON\"");
            }
            else
            {
                ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");
            }

            //Ski Champ controls
            ini.WriteValue(" Global ", "InputSkiLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputSkiRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputSkiUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputSkiDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputSkiX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputSkiY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputSkiPollLeft", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputSkiPollRight", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputSkiSelect1", "\"KEY_Q\"");
            ini.WriteValue(" Global ", "InputSkiSelect2", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputSkiSelect3", "\"KEY_E\"");

            //Magical Truck Adventure controls
            ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalLever1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalLever2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalPedal1", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputMagicalPedal2", "\"KEY_S\"");

            //Sega Bass Fishing / Get Bass controls
            ini.WriteValue(" Global ", "InputFishingRodLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputFishingRodRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputFishingRodUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputFishingRodDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputFishingStickLeft", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputFishingStickRight", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputFishingStickUp", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputFishingStickDown", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputFishingRodX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingRodY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingStickX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingStickY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingReel", "\"KEY_SPACE\"");
            ini.WriteValue(" Global ", "InputFishingCast", "\"KEY_Z\"");
            ini.WriteValue(" Global ", "InputFishingSelect", "\"KEY_X\"");
            ini.WriteValue(" Global ", "InputFishingTension", "\"KEY_T\"");
        }
    }
}
