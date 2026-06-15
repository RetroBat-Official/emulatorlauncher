using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class LinuxloaderGenerator : Generator
    {
        private void CreateControllerConfiguration(string cfgPath, string gamePath)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");

                ConfigureLindberghGunsAutoOff(cfgPath, "lindbergh");
                return;
            }

            try
            {
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "1", EnvironmentVariableTarget.Process);
            }
            catch { }

            string gameCtrlFile = Path.Combine(gamePath, "controls.ini");
            if (File.Exists(gameCtrlFile))
            {
                AddFileForRestoration(gameCtrlFile);
                try { File.Delete(gameCtrlFile);}
                catch { }
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Linuxloader");

            bool samePad = false;
            if (this.Controllers.Count > 1)
            {
                string guid1 = this.Controllers[0].Guid.ToString();
                string guid2 = this.Controllers[1].Guid.ToString();

                if (guid1 == guid2)
                    samePad = true;
            }

            using (var ini = new IniFile(cfgPath, IniOptions.UseSpaces))
            {
                foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(2))
                    ConfigureInput(ini, controller, samePad);

                ConfigureLindberghGuns(ini, "lindbergh");

                ini.Save();
            }
        }

        private void ConfigureInput(IniFile ini, Controller controller, bool samePad)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard && controller.PlayerIndex == 1)
                ConfigureKeyboard(ini, controller.Config, controller.PlayerIndex);
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex, samePad);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            // Common section
            ini.WriteValue("Common", "Test", "KEY_9");
            ini.WriteValue("Common", "ExitGame", "KEY_Escape");
            ini.WriteValue("Common", "P1_Coin", "KEY_5");
            ini.WriteValue("Common", "P2_Coin", "KEY_6");
            ini.WriteValue("Common", "P1_Start", "KEY_1");
            ini.WriteValue("Common", "P2_Start", "KEY_2");
            ini.WriteValue("Common", "P1_Service", "KEY_0");

            // Digital section
            ini.WriteValue("Digital", "P1_Up", "KEY_Up");
            ini.WriteValue("Digital", "P1_Down", "KEY_Down");
            ini.WriteValue("Digital", "P1_Left", "KEY_Left");
            ini.WriteValue("Digital", "P1_Right", "KEY_Right");
            ini.WriteValue("Digital", "P1_Button1", "KEY_Left Ctrl");
            ini.WriteValue("Digital", "P1_Button2", "KEY_Left Alt");
            ini.WriteValue("Digital", "P1_Button3", "KEY_Space");
            ini.WriteValue("Digital", "P1_Card1Insert", "KEY_F7");
            ini.WriteValue("Digital", "P2_Up", "KEY_R");
            ini.WriteValue("Digital", "P2_Down", "KEY_F");
            ini.WriteValue("Digital", "P2_Left", "KEY_D");
            ini.WriteValue("Digital", "P2_Right", "KEY_G");
            ini.WriteValue("Digital", "P2_Button1", "KEY_A");
            ini.WriteValue("Digital", "P2_Button2", "KEY_S");
            ini.WriteValue("Digital", "P2_Button3", "KEY_Q");
            ini.WriteValue("Digital", "P2_Card2Insert", "KEY_F8");

            // Driving section
            ini.WriteValue("Driving", "P1_Steer_Left", "KEY_Left");
            ini.WriteValue("Driving", "P1_Steer_Right", "KEY_Right");
            ini.WriteValue("Driving", "P1_Gas_Digital", "KEY_Up");
            ini.WriteValue("Driving", "P1_Brake_Digital", "KEY_Down");
            ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift");
            ini.WriteValue("Driving", "Boost", "KEY_Left Ctrl");
            ini.WriteValue("Driving", "BoostRight", "KEY_Left Alt");
            ini.WriteValue("Driving", "GearUp", "KEY_X");
            ini.WriteValue("Driving", "GearDown", "KEY_Z");
            ini.WriteValue("Driving", "MusicChange", "KEY_Space");
            ini.WriteValue("Driving", "Up", "KEY_R");
            ini.WriteValue("Driving", "Down", "KEY_F");
            ini.WriteValue("Driving", "Left", "KEY_D");
            ini.WriteValue("Driving", "Right", "KEY_G");
            ini.WriteValue("Driving", "CardInsert", "KEY_F7");
            ini.WriteValue("Driving", "P1_Steer","");
            ini.WriteValue("Driving", "P1_Gas", "");
            ini.WriteValue("Driving", "P1_Brake", "");

            // Flying section
            ini.WriteValue("Flying", "Flying_Left", "KEY_Left");
            ini.WriteValue("Flying", "Flying_Right", "KEY_Right");
            ini.WriteValue("Flying", "Flying_Up", "KEY_Up");
            ini.WriteValue("Flying", "Flying_Down", "KEY_Down");
            ini.WriteValue("Flying", "Throttle_Accelerate", "KEY_X");
            ini.WriteValue("Flying", "Throttle_Slowdown", "KEY_Z");
            ini.WriteValue("Flying", "GunTrigger", "KEY_Left Ctrl");
            ini.WriteValue("Flying", "MissileTrigger", "KEY_Left Alt");
            ini.WriteValue("Flying", "ClimaxSwitch", "KEY_Left Shift");
            ini.WriteValue("Flying", "Flying_X", "");
            ini.WriteValue("Flying", "Flying_Y", "");
            ini.WriteValue("Flying", "Throttle", "");

            // Shooting section
            if (SystemConfig.isOptSet("ll_gunaxis_invert") && !string.IsNullOrEmpty(SystemConfig["ll_gunaxis_invert"]))
            {
                string invertAxis = SystemConfig["ll_gunaxis_invert"].ToLower();
                if (invertAxis == "x")
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X_INVERTED");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
                }
                else if (invertAxis == "y")
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y_INVERTED");
                }
                else if (invertAxis == "both")
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X_INVERTED");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y_INVERTED");
                }
            }
            else
            {
                ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
            }

            ini.WriteValue("Shooting", "P1_Trigger", "MOUSE_LEFT_BUTTON");
            ini.WriteValue("Shooting", "P1_Reload", "MOUSE_RIGHT_BUTTON");
            ini.WriteValue("Shooting", "P1_GunButton", "MOUSE_MIDDLE_BUTTON");
            ini.WriteValue("Shooting", "P1_ActionButton", "KEY_R");
            ini.WriteValue("Shooting", "P1_PedalLeft", "KEY_Left");
            ini.WriteValue("Shooting", "P1_PedalRight", "KEY_Right");
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex, bool samePad)
        {
            if (ctrl == null)
                return;

            bool testService = SystemConfig.getOptBoolean("ll_testmode");
            bool standardJoy = false;
            bool isXinput = ctrl.IsXInputDevice;

            string player = "P" + playerindex + "_";
            int cIndex = playerindex - 1;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            var guid = ctrl.Guid;
            if (ctrl.SdlWrappedTechID == SdlWrappedTechId.RawInput && ctrl.XInput != null)
                guid = guid.ToXInputGuid(ctrl.XInput.SubType);

            string guidString = guid.ToString().ToLowerInvariant();

            Sdl3GameController sdl3Controller = ctrl.Sdl3Controller;

            if (sdl3Controller != null)
            {
                if (!string.IsNullOrEmpty(sdl3Controller.GuidString) && sdl3Controller.GuidString != new string('0', 32))
                {
                    var cleaned = RemoveGuidCRC(sdl3Controller.GuidString);
                    if (!string.IsNullOrEmpty(cleaned))
                        guidString = cleaned.ToLowerInvariant();
                }
            }

            if (samePad)
            {
                if (sdl3Controller != null)
                    cIndex = sdl3Controller.EnumerationIndex;
                else
                {
                    var sortedControllers = this.Controllers.OrderBy(i => i.DirectInput?.DeviceIndex ?? i.DeviceIndex).ToList();
                    cIndex = sortedControllers.IndexOf(ctrl);
                }
            }

            if (SystemConfig.getOptBoolean("ll_reversepadindex"))
            {
                if (cIndex == 0)
                    cIndex = 1;
                else if (cIndex == 1)
                    cIndex = 0;
            }

            string gcIndex = "GC" + cIndex + "_";

            if (sdl3Controller != null && !sdl3Controller.IsGamepad)
            {
                standardJoy = true;
                gcIndex = "JOY" + cIndex + "_";
            }

            string b1 = "BUTTON_A";
            string b2 = "BUTTON_B";
            string b3 = "BUTTON_X";
            string b4 = "BUTTON_Y";
            string card = "BUTTON_GUIDE";
            string b6 = "BUTTON_RIGHTSHOULDER";
            string b5 = "BUTTON_LEFTSHOULDER";
            string b8 = "AXIS_RIGHTTRIGGER";
            string b7 = "AXIS_LEFTTRIGGER";
            bool panel = false;
            bool panel6 = false;

            if (SystemConfig.isOptSet("ll_steer_deadzone") && !string.IsNullOrEmpty(SystemConfig["ll_steer_deadzone"]))
            {
                string steerDdeadzone = SystemConfig["ll_steer_deadzone"];
                ini.WriteValue("Config", "Steer_DeadZone", steerDdeadzone);
            }

            // Common section
            if (!standardJoy)
            {
                if (SystemConfig.isOptSet("controller_layout") && !string.IsNullOrEmpty(SystemConfig["controller_layout"]))
                {
                    string layout = SystemConfig["controller_layout"];
                    switch (layout)
                    {
                        case "modern8":
                            panel = true;
                            b1 = "BUTTON_X";
                            b2 = "BUTTON_Y";
                            b3 = "BUTTON_RIGHTSHOULDER";
                            b4 = "BUTTON_A";
                            b5 = "BUTTON_B";
                            b6 = "AXIS_RIGHTTRIGGER";
                            b7 = "AXIS_LEFTSHOULDER";
                            b8 = "AXIS_LEFTTRIGGER";
                            card = "BUTTON_GUIDE";
                            break;
                        case "classic8":
                            panel = true;
                            b1 = "BUTTON_X";
                            b2 = "BUTTON_Y";
                            b3 = "AXIS_LEFTSHOULDER";
                            b4 = "BUTTON_A";
                            b5 = "BUTTON_B";
                            b6 = "BUTTON_RIGHTSHOULDER";
                            b7 = "AXIS_LEFTTRIGGER";
                            b8 = "AXIS_RIGHTTRIGGER";
                            card = "BUTTON_GUIDE";
                            break;
                        case "8alternative":
                            panel = true;
                            b1 = "BUTTON_A";
                            b2 = "BUTTON_X";
                            b3 = "BUTTON_Y";
                            b4 = "BUTTON_RIGHTSHOULDER";
                            b5 = "AXIS_LEFTSHOULDER";
                            b6 = "BUTTON_B";
                            b7 = "AXIS_RIGHTTRIGGER";
                            b8 = "AXIS_LEFTTRIGGER";
                            card = "BUTTON_GUIDE";
                            break;
                        case "6alternative":
                            panel = true;
                            panel6 = true;
                            b1 = "BUTTON_X";
                            b2 = "BUTTON_Y";
                            b3 = "AXIS_LEFTSHOULDER";
                            b4 = "BUTTON_A";
                            b5 = "BUTTON_B";
                            b6 = "BUTTON_RIGHTSHOULDER";
                            b7 = "AXIS_LEFTTRIGGER";
                            b8 = "AXIS_RIGHTTRIGGER";
                            card = "BUTTON_GUIDE";
                            break;
                        case "default":
                            break;
                    }
                }

                if (playerindex == 1)
                {
                    ini.WriteValue("Common", "Test", testService ? "KEY_9, " + gcIndex + "BUTTON_RIGHTSTICK" : "KEY_9");
                    ini.WriteValue("Common", "ExitGame", "KEY_Escape, " + gcIndex + "BUTTON_START + " + gcIndex + "BUTTON_BACK");
                    ini.WriteValue("Common", "P1_Coin", "KEY_5, " + gcIndex + "BUTTON_BACK");
                    ini.WriteValue("Common", "P2_Coin", "KEY_6");
                    ini.WriteValue("Common", "P1_Start", "KEY_1, " + gcIndex + "BUTTON_START");
                    ini.WriteValue("Common", "P2_Start", "KEY_2");
                    ini.WriteValue("Common", "P1_Service", testService ? "KEY_0, " + gcIndex + "BUTTON_LEFTSTICK" : "KEY_0");
                }
                if (playerindex == 2)
                {
                    ini.WriteValue("Common", "P2_Coin", "KEY_6, " + gcIndex + "BUTTON_BACK");
                    ini.WriteValue("Common", "P2_Start", "KEY_2, " + gcIndex + "BUTTON_START");
                }

                // Digital section
                if (playerindex == 1)
                {
                    ini.WriteValue("Digital", "P1_Up", "KEY_Up, " + gcIndex + "BUTTON_DPUP, " + gcIndex + "AXIS_LEFTY_NEGATIVE");
                    ini.WriteValue("Digital", "P1_Down", "KEY_Down, " + gcIndex + "BUTTON_DPDOWN, " + gcIndex + "AXIS_LEFTY_POSITIVE");
                    ini.WriteValue("Digital", "P1_Left", "KEY_Left, " + gcIndex + "BUTTON_DPLEFT, " + gcIndex + "AXIS_LEFTX_NEGATIVE");
                    ini.WriteValue("Digital", "P1_Right", "KEY_Right, " + gcIndex + "BUTTON_DPRIGHT, " + gcIndex + "AXIS_LEFTX_POSITIVE");
                    ini.WriteValue("Digital", "P1_Button1", "KEY_Left Ctrl, " + gcIndex + b1);
                    ini.WriteValue("Digital", "P1_Button2", "KEY_Left Alt, " + gcIndex + b2);
                    ini.WriteValue("Digital", "P1_Button3", "KEY_Space, " + gcIndex + b3);
                    ini.WriteValue("Digital", "P1_Card1Insert", "KEY_F7, " + gcIndex + card);
                    ini.WriteValue("Digital", "P2_Up", "KEY_R");
                    ini.WriteValue("Digital", "P2_Down", "KEY_F");
                    ini.WriteValue("Digital", "P2_Left", "KEY_D");
                    ini.WriteValue("Digital", "P2_Right", "KEY_G");
                    ini.WriteValue("Digital", "P2_Button1", "KEY_A");
                    ini.WriteValue("Digital", "P2_Button2", "KEY_S");
                    ini.WriteValue("Digital", "P2_Button3", "KEY_Q");
                    ini.WriteValue("Digital", "P2_Card2Insert", "KEY_F8");
                }
                if (playerindex == 2)
                {
                    ini.WriteValue("Digital", "P2_Up", "KEY_R, " + gcIndex + "BUTTON_DPUP, " + gcIndex + "AXIS_LEFTY_NEGATIVE");
                    ini.WriteValue("Digital", "P2_Down", "KEY_F, " + gcIndex + "BUTTON_DPDOWN, " + gcIndex + "AXIS_LEFTY_POSITIVE");
                    ini.WriteValue("Digital", "P2_Left", "KEY_D, " + gcIndex + "BUTTON_DPLEFT, " + gcIndex + "AXIS_LEFTX_NEGATIVE");
                    ini.WriteValue("Digital", "P2_Right", "KEY_G, " + gcIndex + "BUTTON_DPRIGHT, " + gcIndex + "AXIS_LEFTX_POSITIVE");
                    ini.WriteValue("Digital", "P2_Button1", "KEY_A, " + gcIndex + b1);
                    ini.WriteValue("Digital", "P2_Button2", "KEY_S, " + gcIndex + b2);
                    ini.WriteValue("Digital", "P2_Button3", "KEY_Q, " + gcIndex + b3);
                    ini.WriteValue("Digital", "P2_Card2Insert", "KEY_F8, " + gcIndex + card);
                }

                // Driving section
                if (playerindex == 1)
                {
                    if (panel)
                    {
                        ini.WriteValue("Driving", "P1_Steer_Left", "KEY_Left, " + gcIndex + "BUTTON_DPLEFT");
                        ini.WriteValue("Driving", "P1_Steer_Right", "KEY_Right, " + gcIndex + "BUTTON_DPRIGHT");
                        ini.WriteValue("Driving", "P1_Gas_Digital", "KEY_Up, " + gcIndex + b5);
                        ini.WriteValue("Driving", "P1_Brake_Digital", "KEY_Down, " + gcIndex + b4);

                        if (panel6)
                            ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift, " + gcIndex + "BUTTON_DPUP");
                        else
                            ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift, " + gcIndex + b7);

                        ini.WriteValue("Driving", "Boost", "KEY_Left Ctrl, " + gcIndex + b6);
                        ini.WriteValue("Driving", "BoostRight", "KEY_Left Alt, " + gcIndex + b3);
                        ini.WriteValue("Driving", "GearUp", "KEY_X, " + gcIndex + b2);
                        ini.WriteValue("Driving", "GearDown", "KEY_Z, " + gcIndex + b1);

                        if (panel6)
                            ini.WriteValue("Driving", "MusicChange", "KEY_Space, " + gcIndex + "BUTTON_DPDOWN");
                        else
                            ini.WriteValue("Driving", "MusicChange", "KEY_Space, " + gcIndex + b8);
                    }
                    else
                    {
                        ini.WriteValue("Driving", "P1_Steer_Left", "KEY_Left");
                        ini.WriteValue("Driving", "P1_Steer_Right", "KEY_Right");
                        ini.WriteValue("Driving", "P1_Gas_Digital", "KEY_Up");
                        ini.WriteValue("Driving", "P1_Brake_Digital", "KEY_Down");
                        ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift, " + gcIndex + "BUTTON_Y");
                        ini.WriteValue("Driving", "Boost", "KEY_Left Ctrl, " + gcIndex + "BUTTON_A");
                        ini.WriteValue("Driving", "BoostRight", "KEY_Left Alt, " + gcIndex + "BUTTON_B");
                        ini.WriteValue("Driving", "GearUp", "KEY_X, " + gcIndex + "BUTTON_RIGHTSHOULDER");
                        ini.WriteValue("Driving", "GearDown", "KEY_Z, " + gcIndex + "BUTTON_LEFTSHOULDER");
                        ini.WriteValue("Driving", "MusicChange", "KEY_Space, " + gcIndex + "BUTTON_X");
                    }
                    
                    ini.WriteValue("Driving", "Up", "KEY_R, " + gcIndex + "BUTTON_DPUP");
                    ini.WriteValue("Driving", "Down", "KEY_F, " + gcIndex + "BUTTON_DPDOWN");
                    ini.WriteValue("Driving", "Left", "KEY_D, " + gcIndex + "BUTTON_DPLEFT");
                    ini.WriteValue("Driving", "Right", "KEY_G, " + gcIndex + "BUTTON_DPRIGHT");
                    ini.WriteValue("Driving", "CardInsert", "KEY_F7, " + gcIndex + card);

                    if (panel)
                    {
                        ini.WriteValue("Driving", "P1_Steer", "");
                        ini.WriteValue("Driving", "P1_Gas", "");
                        ini.WriteValue("Driving", "P1_Brake", "");
                    }
                    else
                    {
                        ini.WriteValue("Driving", "P1_Steer", gcIndex + "AXIS_LEFTX");
                        ini.WriteValue("Driving", "P1_Gas", gcIndex + "AXIS_RIGHTTRIGGER");
                        ini.WriteValue("Driving", "P1_Brake", gcIndex + "AXIS_LEFTTRIGGER");
                    }
                }

                // Flying section
                if (playerindex == 1)
                {
                    ini.WriteValue("Flying", "Flying_Left", "KEY_Left, " + gcIndex + "BUTTON_DPLEFT");
                    ini.WriteValue("Flying", "Flying_Right", "KEY_Right, " + gcIndex + "BUTTON_DPRIGHT");
                    ini.WriteValue("Flying", "Flying_Up", "KEY_Up, " + gcIndex + "BUTTON_DPUP");
                    ini.WriteValue("Flying", "Flying_Down", "KEY_Down, " + gcIndex + "BUTTON_DPDOWN");

                    if (panel)
                    {
                        ini.WriteValue("Flying", "Throttle_Accelerate", "KEY_X, " + gcIndex + b5);
                        ini.WriteValue("Flying", "Throttle_Slowdown", "KEY_Z, " + gcIndex + b4);
                        ini.WriteValue("Flying", "GunTrigger", "KEY_Left Ctrl, " + gcIndex + b1);
                        ini.WriteValue("Flying", "MissileTrigger", "KEY_Left Alt, " + gcIndex + b2);
                        ini.WriteValue("Flying", "ClimaxSwitch", "KEY_Left Shift, " + gcIndex + b6);
                    }
                    else
                    {
                        ini.WriteValue("Flying", "Throttle_Accelerate", "KEY_X, " + gcIndex + "BUTTON_RIGHTSHOULDER");
                        ini.WriteValue("Flying", "Throttle_Slowdown", "KEY_Z, " + gcIndex + "BUTTON_LEFTSHOULDER");
                        ini.WriteValue("Flying", "GunTrigger", "KEY_Left Ctrl, " + gcIndex + "BUTTON_A");
                        ini.WriteValue("Flying", "MissileTrigger", "KEY_Left Alt, " + gcIndex + "BUTTON_B");
                        ini.WriteValue("Flying", "ClimaxSwitch", "KEY_Left Shift, " + gcIndex + "BUTTON_Y");
                    }

                    if (panel)
                    {
                        ini.WriteValue("Flying", "Flying_X","");
                        ini.WriteValue("Flying", "Flying_Y", "");
                        ini.WriteValue("Flying", "Throttle", "");
                    }
                    else
                    {
                        ini.WriteValue("Flying", "Flying_X", gcIndex + "AXIS_LEFTX");
                        ini.WriteValue("Flying", "Flying_Y", gcIndex + "AXIS_LEFTY");
                        ini.WriteValue("Flying", "Throttle", gcIndex + "AXIS_RIGHTY_INVERTED, " + gcIndex + "AXIS_RIGHTTRIGGER_POSITIVE_HALF, " + gcIndex + "AXIS_LEFTTRIGGER_NEGATIVE_HALF");
                    }
                }
            }

            else
            {
                b1 = "a";
                b2 = "b";
                b3 = "x";
                b4 = "y";
                card = "guide";
                b6 = "rightshoulder";
                b5 = "leftshoulder";
                b8 = "righttrigger";
                b7 = "lefttrigger";

                if (SystemConfig.isOptSet("controller_layout") && !string.IsNullOrEmpty(SystemConfig["controller_layout"]))
                {
                    string layout = SystemConfig["controller_layout"];
                    switch (layout)
                    {
                        case "modern8":
                            panel = true;
                            b1 = "x";
                            b2 = "y";
                            b3 = "rightshoulder";
                            b4 = "a";
                            b5 = "b";
                            b6 = "righttrigger";
                            b7 = "leftshoulder";
                            b8 = "lefttrigger";
                            card = "guide";
                            break;
                        case "classic8":
                            panel = true;
                            b1 = "x";
                            b2 = "y";
                            b3 = "leftshoulder";
                            b4 = "a";
                            b5 = "b";
                            b6 = "rightshoulder";
                            b7 = "lefttrigger";
                            b8 = "righttrigger";
                            card = "guide";
                            break;
                        case "8alternative":
                            panel = true;
                            b1 = "a";
                            b2 = "x";
                            b3 = "y";
                            b4 = "rightshoulder";
                            b5 = "leftshoulder";
                            b6 = "b";
                            b7 = "righttrigger";
                            b8 = "lefttrigger";
                            card = "guide";
                            break;
                        case "6alternative":
                            panel = true;
                            panel6 = true;
                            b1 = "x";
                            b2 = "y";
                            b3 = "leftshoulder";
                            b4 = "a";
                            b5 = "b";
                            b6 = "rightshoulder";
                            b7 = "lefttrigger";
                            b8 = "righttrigger";
                            card = "guide";
                            break;
                        case "default":
                            break;
                    }
                }

                SdlToDirectInput dinputController = null;
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
                string dguid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";

                if (File.Exists(gamecontrollerDB))
                {
                    dinputController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, dguid);
                    if (dinputController == null || dinputController.ButtonMappings == null)
                    {
                        SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt does not contain mapping for the controller " + guid + ". Controller mapping will not be available");
                        return;
                    }

                    if (playerindex == 1)
                    {
                        ini.WriteValue("Common", "Test", testService ? "KEY_9, " + GetDinputMapping(gcIndex, dinputController, "rightstick", isXinput) : "KEY_9");
                        ini.WriteValue("Common", "ExitGame", "KEY_Escape, " + GetDinputMapping(gcIndex, dinputController, "start", isXinput) + " + " + GetDinputMapping(gcIndex, dinputController, "back", isXinput));
                        ini.WriteValue("Common", "P1_Coin", "KEY_5, " + GetDinputMapping(gcIndex, dinputController, "back", isXinput));
                        ini.WriteValue("Common", "P2_Coin", "KEY_6");
                        ini.WriteValue("Common", "P1_Start", "KEY_1, " + GetDinputMapping(gcIndex, dinputController, "start", isXinput));
                        ini.WriteValue("Common", "P2_Start", "KEY_2");
                        ini.WriteValue("Common", "P1_Service", testService ? "KEY_0, " + GetDinputMapping(gcIndex, dinputController, "leftstick", isXinput) : "KEY_0");
                    }
                    if (playerindex == 2)
                    {
                        ini.WriteValue("Common", "P2_Coin", "KEY_6, " + GetDinputMapping(gcIndex, dinputController, "back", isXinput));
                        ini.WriteValue("Common", "P2_Start", "KEY_2, " + GetDinputMapping(gcIndex, dinputController, "start", isXinput));
                    }

                    // Digital section
                    if (playerindex == 1)
                    {
                        ini.WriteValue("Digital", "P1_Up", "KEY_Up, " + GetDinputMapping(gcIndex, dinputController, "dpup", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "lefty", isXinput, -1));
                        ini.WriteValue("Digital", "P1_Down", "KEY_Down, " + GetDinputMapping(gcIndex, dinputController, "dpdown", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "lefty", isXinput, 1));
                        ini.WriteValue("Digital", "P1_Left", "KEY_Left, " + GetDinputMapping(gcIndex, dinputController, "dpleft", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "leftx", isXinput, -1));
                        ini.WriteValue("Digital", "P1_Right", "KEY_Right, " + GetDinputMapping(gcIndex, dinputController, "dpright", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "leftx", isXinput, 1));
                        ini.WriteValue("Digital", "P1_Button1", "KEY_Left Ctrl, " + GetDinputMapping(gcIndex, dinputController, b1, isXinput));
                        ini.WriteValue("Digital", "P1_Button2", "KEY_Left Alt, " + GetDinputMapping(gcIndex, dinputController, b2, isXinput));
                        ini.WriteValue("Digital", "P1_Button3", "KEY_Space, " + GetDinputMapping(gcIndex, dinputController, b3, isXinput));
                        ini.WriteValue("Digital", "P1_Card1Insert", "KEY_F7, " + GetDinputMapping(gcIndex, dinputController, card, isXinput));
                        ini.WriteValue("Digital", "P2_Up", "KEY_R");
                        ini.WriteValue("Digital", "P2_Down", "KEY_F");
                        ini.WriteValue("Digital", "P2_Left", "KEY_D");
                        ini.WriteValue("Digital", "P2_Right", "KEY_G");
                        ini.WriteValue("Digital", "P2_Button1", "KEY_A");
                        ini.WriteValue("Digital", "P2_Button2", "KEY_S");
                        ini.WriteValue("Digital", "P2_Button3", "KEY_Q");
                        ini.WriteValue("Digital", "P2_Card2Insert", "KEY_F8");
                    }
                    if (playerindex == 2)
                    {
                        ini.WriteValue("Digital", "P2_Up", "KEY_R, " + GetDinputMapping(gcIndex, dinputController, "dpup", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "lefty", isXinput, -1));
                        ini.WriteValue("Digital", "P2_Down", "KEY_F, " + GetDinputMapping(gcIndex, dinputController, "dpdown", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "lefty", isXinput, 1));
                        ini.WriteValue("Digital", "P2_Left", "KEY_D, " + GetDinputMapping(gcIndex, dinputController, "dpleft", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "leftx", isXinput, -1));
                        ini.WriteValue("Digital", "P2_Right", "KEY_G, " + GetDinputMapping(gcIndex, dinputController, "dpright", isXinput) + ", " + GetDinputMapping(gcIndex, dinputController, "leftx", isXinput, 1));
                        ini.WriteValue("Digital", "P2_Button1", "KEY_A, " + GetDinputMapping(gcIndex, dinputController, b1, isXinput));
                        ini.WriteValue("Digital", "P2_Button2", "KEY_S, " + GetDinputMapping(gcIndex, dinputController, b2, isXinput));
                        ini.WriteValue("Digital", "P2_Button3", "KEY_Q, " + GetDinputMapping(gcIndex, dinputController, b3, isXinput));
                        ini.WriteValue("Digital", "P2_Card2Insert", "KEY_F8, " + GetDinputMapping(gcIndex, dinputController, card, isXinput));
                    }

                    // Driving section
                    if (playerindex == 1)
                    {
                        if (panel)
                        {
                            ini.WriteValue("Driving", "P1_Steer_Left", "KEY_Left, " + GetDinputMapping(gcIndex, dinputController, "dpleft", isXinput));
                            ini.WriteValue("Driving", "P1_Steer_Right", "KEY_Right, " + GetDinputMapping(gcIndex, dinputController, "dpright", isXinput));
                            ini.WriteValue("Driving", "P1_Gas_Digital", "KEY_Up, " + GetDinputMapping(gcIndex, dinputController, b5, isXinput));
                            ini.WriteValue("Driving", "P1_Brake_Digital", "KEY_Down, " + GetDinputMapping(gcIndex, dinputController, b4, isXinput));

                            if (panel6)
                                ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift, " + GetDinputMapping(gcIndex, dinputController, "dpup", isXinput));
                            else
                                ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift, " + GetDinputMapping(gcIndex, dinputController, b7, isXinput));

                            ini.WriteValue("Driving", "Boost", "KEY_Left Ctrl, " + GetDinputMapping(gcIndex, dinputController, b6, isXinput));
                            ini.WriteValue("Driving", "BoostRight", "KEY_Left Alt, " + GetDinputMapping(gcIndex, dinputController, b3, isXinput));
                            ini.WriteValue("Driving", "GearUp", "KEY_X, " + GetDinputMapping(gcIndex, dinputController, b2, isXinput));
                            ini.WriteValue("Driving", "GearDown", "KEY_Z, " + GetDinputMapping(gcIndex, dinputController, b1, isXinput));

                            if (panel6)
                                ini.WriteValue("Driving", "MusicChange", "KEY_Space, " + GetDinputMapping(gcIndex, dinputController, "dpdown", isXinput));
                            else
                                ini.WriteValue("Driving", "MusicChange", "KEY_Space, " + GetDinputMapping(gcIndex, dinputController, b8, isXinput));
                        }
                        else
                        {
                            ini.WriteValue("Driving", "P1_Steer_Left", "KEY_Left");
                            ini.WriteValue("Driving", "P1_Steer_Right", "KEY_Right");
                            ini.WriteValue("Driving", "P1_Gas_Digital", "KEY_Up");
                            ini.WriteValue("Driving", "P1_Brake_Digital", "KEY_Down");
                            ini.WriteValue("Driving", "ViewChange", "KEY_Left Shift, " + GetDinputMapping(gcIndex, dinputController, "y", isXinput));
                            ini.WriteValue("Driving", "Boost", "KEY_Left Ctrl, " + GetDinputMapping(gcIndex, dinputController, "x", isXinput));
                            ini.WriteValue("Driving", "BoostRight", "KEY_Left Alt, " + GetDinputMapping(gcIndex, dinputController, "a", isXinput));
                            ini.WriteValue("Driving", "GearUp", "KEY_X, " + GetDinputMapping(gcIndex, dinputController, "rightshoulder", isXinput));
                            ini.WriteValue("Driving", "GearDown", "KEY_Z, " + GetDinputMapping(gcIndex, dinputController, "leftshoulder", isXinput));
                            ini.WriteValue("Driving", "MusicChange", "KEY_Space, " + GetDinputMapping(gcIndex, dinputController, "b", isXinput));
                        }

                        ini.WriteValue("Driving", "Up", "KEY_R, " + GetDinputMapping(gcIndex, dinputController, "dpup", isXinput));
                        ini.WriteValue("Driving", "Down", "KEY_F, " + GetDinputMapping(gcIndex, dinputController, "dpdown", isXinput));
                        ini.WriteValue("Driving", "Left", "KEY_D, " + GetDinputMapping(gcIndex, dinputController, "dpleft", isXinput));
                        ini.WriteValue("Driving", "Right", "KEY_G, " + GetDinputMapping(gcIndex, dinputController, "dpright", isXinput));
                        ini.WriteValue("Driving", "CardInsert", "KEY_F7, " + GetDinputMapping(gcIndex, dinputController, card, isXinput));

                        if (panel)
                        {
                            ini.WriteValue("Driving", "P1_Steer", "");
                            ini.WriteValue("Driving", "P1_Gas", "");
                            ini.WriteValue("Driving", "P1_Brake", "");
                        }
                        else
                        {
                            ini.WriteValue("Driving", "P1_Steer", GetDinputMapping(gcIndex, dinputController, "leftx", isXinput, 0));
                            ini.WriteValue("Driving", "P1_Gas", GetDinputMapping(gcIndex, dinputController, "righttrigger", isXinput, 0));
                            ini.WriteValue("Driving", "P1_Brake", GetDinputMapping(gcIndex, dinputController, "lefttrigger", isXinput, 0));
                        }
                    }

                    // Flying section
                    if (playerindex == 1)
                    {
                        ini.WriteValue("Flying", "Flying_Left", "KEY_Left, " + GetDinputMapping(gcIndex, dinputController, "dpleft", isXinput));
                        ini.WriteValue("Flying", "Flying_Right", "KEY_Right, " + GetDinputMapping(gcIndex, dinputController, "dpright", isXinput));
                        ini.WriteValue("Flying", "Flying_Up", "KEY_Up, " + GetDinputMapping(gcIndex, dinputController, "dpup", isXinput));
                        ini.WriteValue("Flying", "Flying_Down", "KEY_Down, " + GetDinputMapping(gcIndex, dinputController, "dpdown", isXinput));

                        if (panel)
                        {
                            ini.WriteValue("Flying", "Throttle_Accelerate", "KEY_X, " + GetDinputMapping(gcIndex, dinputController, b5, isXinput));
                            ini.WriteValue("Flying", "Throttle_Slowdown", "KEY_Z, " + GetDinputMapping(gcIndex, dinputController, b4, isXinput));
                            ini.WriteValue("Flying", "GunTrigger", "KEY_Left Ctrl, " + GetDinputMapping(gcIndex, dinputController, b1, isXinput));
                            ini.WriteValue("Flying", "MissileTrigger", "KEY_Left Alt, " + GetDinputMapping(gcIndex, dinputController, b2, isXinput));
                            ini.WriteValue("Flying", "ClimaxSwitch", "KEY_Left Shift, " + GetDinputMapping(gcIndex, dinputController, b6, isXinput));
                        }
                        else
                        {
                            ini.WriteValue("Flying", "Throttle_Accelerate", "KEY_X, " + GetDinputMapping(gcIndex, dinputController, "rightshoulder", isXinput));
                            ini.WriteValue("Flying", "Throttle_Slowdown", "KEY_Z, " + GetDinputMapping(gcIndex, dinputController, "leftshoulder", isXinput));
                            ini.WriteValue("Flying", "GunTrigger", "KEY_Left Ctrl, " + GetDinputMapping(gcIndex, dinputController, "x", isXinput));
                            ini.WriteValue("Flying", "MissileTrigger", "KEY_Left Alt, " + GetDinputMapping(gcIndex, dinputController, "a", isXinput));
                            ini.WriteValue("Flying", "ClimaxSwitch", "KEY_Left Shift, " + GetDinputMapping(gcIndex, dinputController, "b", isXinput));
                        }

                        if (panel)
                        {
                            ini.WriteValue("Flying", "Flying_X", "");
                            ini.WriteValue("Flying", "Flying_Y", "");
                            ini.WriteValue("Flying", "Throttle", "");
                        }
                        else
                        {
                            ini.WriteValue("Flying", "Flying_X", GetDinputMapping(gcIndex, dinputController, "leftx", isXinput, 0));
                            ini.WriteValue("Flying", "Flying_Y", GetDinputMapping(gcIndex, dinputController, "lefty", isXinput, 0));
                            ini.WriteValue("Flying", "Throttle", GetDinputMapping(gcIndex, dinputController, "triggers_inverted", isXinput, 0));
                        }
                    }
                }
                else
                {
                    SimpleLogger.Instance.Warning("[CONTROLS] gamecontrollerdb.txt not found, mapping won't be available");
                    return;
                }
            }

            // Shooting section
            if (playerindex == 1)
            {
                if (SystemConfig.isOptSet("ll_gunaxis_invert") && !string.IsNullOrEmpty(SystemConfig["ll_gunaxis_invert"]))
                {
                    string invertAxis = SystemConfig["ll_gunaxis_invert"].ToLower();
                    if (invertAxis == "x")
                    {
                        ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X_INVERTED");
                        ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
                    }
                    else if (invertAxis == "y")
                    {
                        ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                        ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y_INVERTED");
                    }
                    else if (invertAxis == "both")
                    {
                        ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X_INVERTED");
                        ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y_INVERTED");
                    }
                }
                else
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
                }

                ini.WriteValue("Shooting", "P1_Trigger", "MOUSE_LEFT_BUTTON");
                ini.WriteValue("Shooting", "P1_Reload", "MOUSE_RIGHT_BUTTON");
                ini.WriteValue("Shooting", "P1_GunButton", "MOUSE_MIDDLE_BUTTON");
                ini.WriteValue("Shooting", "P1_ActionButton", "KEY_R");
                ini.WriteValue("Shooting", "P1_PedalLeft", "KEY_Left");
                ini.WriteValue("Shooting", "P1_PedalRight", "KEY_Right");
            }

            if (guidString != null)
                ini.WriteValue("ControllerGUIDs", player + "GUID", guidString);
        }

        private string GetDinputMapping(string index, SdlToDirectInput c, string buttonkey, bool isxinput, int plus = 0)
        {
            if (c == null)
                return "";

            if (!c.ButtonMappings.ContainsKey(buttonkey) && buttonkey != "triggers_inverted")
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "";
            }

            if (buttonkey == "triggers_inverted")
            {
                string ret = "";
                string rightvert = "righty";

                if (c.ButtonMappings.ContainsKey(rightvert) && c.ButtonMappings[rightvert].Contains("a"))
                {
                    string val = c.ButtonMappings[rightvert];
                    string axisIDspec;

                    if (val.StartsWith("-a") || val.StartsWith("+a"))
                        axisIDspec = val.Substring(2);

                    else
                        axisIDspec = val.Substring(1);
                    
                    ret += index + "AXIS_" + axisIDspec + "_INVERTED";
                }

                string righttrig = "righttrigger";
                string lefttrig = "lefttrigger";

                if (c.ButtonMappings.ContainsKey(righttrig) && c.ButtonMappings[righttrig].Contains("a") && c.ButtonMappings.ContainsKey(lefttrig) && c.ButtonMappings[lefttrig].Contains("a"))
                {
                    string valLT = c.ButtonMappings[lefttrig];
                    string valRT = c.ButtonMappings[righttrig];
                    string axisIDspecLT;
                    string axisIDspecRT;

                    if (isxinput && valRT == "a5")
                        valRT = "a2";

                    if (valLT.StartsWith("-a") || valLT.StartsWith("+a"))
                        axisIDspecLT = valLT.Substring(2);

                    else
                        axisIDspecLT = valLT.Substring(1);

                    if (valRT.StartsWith("-a") || valRT.StartsWith("+a"))
                        axisIDspecRT = valRT.Substring(2);

                    else
                        axisIDspecRT = valRT.Substring(1);

                    if (string.IsNullOrEmpty(ret))
                        ret = index + "AXIS_" + axisIDspecRT + "_POSITIVE_HALF" + ", " + index + "AXIS_" + axisIDspecLT + "_NEGATIVE_HALF";
                    else
                        ret += ", " + index + "AXIS_" + axisIDspecRT + "_POSITIVE_HALF" + ", " + index + "AXIS_" + axisIDspecLT + "_NEGATIVE_HALF";
                }

                return ret;
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
                return "";

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("-a"))
                plus = -1;

            if (button.StartsWith("+a"))
                plus = 1;

            if (isxinput)
            {
                if (button == "a5")
                    return index + "AXIS_2_NEGATIVE";
            }

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());
                return index + "BUTTON_" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return index + "HAT0_UP";
                    case 2:
                        return index + "HAT0_RIGHT";
                    case 4:
                        return index + "HAT0_DOWN";
                    case 8:
                        return index + "HAT0_LEFT";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                if (plus == 1) return index + "AXIS_" + axisID + "_POSITIVE";
                else if (plus == -1) return index + "AXIS_" + axisID + "_NEGATIVE";
                else return index + "AXIS_" + axisID;
            }

            return "Unassigned";
        }

        public static string RemoveGuidCRC(string guid)
        {
            if (guid == null || guid.Length < 8)
                return guid;

            return guid.Substring(0, 4) + "0000" + guid.Substring(8);
        }
    }
}
