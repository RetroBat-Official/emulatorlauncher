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
        private List<Sdl3GameController> _sdl3Controllers = new List<Sdl3GameController>();

        private void CreateControllerConfiguration(string cfgPath, string gamePath)
        {
            bool guns = SystemConfig.getOptBoolean("use_guns");

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");

                if (guns)
                    ConfigureLindberghGunsAutoOff(cfgPath, "lindbergh");
                return;
            }

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

                if (guns)
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
            ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
            ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
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

            string player = "P" + playerindex + "_";
            int cIndex = playerindex - 1;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            var guid = ctrl.Guid;
            if (ctrl.SdlWrappedTechID == SdlWrappedTechId.RawInput && ctrl.XInput != null)
                guid = guid.ToXInputGuid(ctrl.XInput.SubType);

            string guidString = guid.ToString().ToLowerInvariant();

            if (Sdl3GameController.ListJoysticks(out List<Sdl3GameController> Sdl3Controllers))
                _sdl3Controllers = Sdl3Controllers;

            if (_sdl3Controllers.Count > 0)
            {
                Sdl3GameController sdl3Controller = Controller.GetSDL3ControllerMatch(ctrl, _sdl3Controllers);
                
                if (sdl3Controller == null)
                {
                    sdl3Controller = _sdl3Controllers.FirstOrDefault();
                }

                if (sdl3Controller != null)
                {
                    if (sdl3Controller.GuidString != new string('0', 32))
                        guidString = RemoveGuidCRC(sdl3Controller.GuidString).ToLowerInvariant();
                }
            }

            if (samePad)
            {
                var sortedControllers = this.Controllers.OrderBy(i => i.DirectInput.DeviceIndex).ToList();
                cIndex = sortedControllers.IndexOf(ctrl);
            }

            if (SystemConfig.getOptBoolean("ll_reversepadindex"))
            {
                if (cIndex == 0)
                    cIndex = 1;
                else if (cIndex == 1)
                    cIndex = 0;
            }

            string gcIndex = "GC" + cIndex + "_";

            // Common section
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
                ini.WriteValue("Digital", "P1_Button1", "KEY_Left Ctrl, " + gcIndex + "BUTTON_A");
                ini.WriteValue("Digital", "P1_Button2", "KEY_Left Alt, " + gcIndex + "BUTTON_B");
                ini.WriteValue("Digital", "P1_Button3", "KEY_Space, " + gcIndex + "BUTTON_X");
                ini.WriteValue("Digital", "P1_Card1Insert", "KEY_F7, " + gcIndex + "BUTTON_GUIDE");
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
                ini.WriteValue("Digital", "P2_Button1", "KEY_A, " + gcIndex + "BUTTON_A");
                ini.WriteValue("Digital", "P2_Button2", "KEY_S, " + gcIndex + "BUTTON_B");
                ini.WriteValue("Digital", "P2_Button3", "KEY_Q, " + gcIndex + "BUTTON_X");
                ini.WriteValue("Digital", "P2_Card2Insert", "KEY_F8, " + gcIndex + "BUTTON_GUIDE");
            }

            // Driving section
            if (playerindex == 1)
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
                ini.WriteValue("Driving", "Up", "KEY_R, " + gcIndex + "BUTTON_DPUP");
                ini.WriteValue("Driving", "Down", "KEY_F, " + gcIndex + "BUTTON_DPDOWN");
                ini.WriteValue("Driving", "Left", "KEY_D, " + gcIndex + "BUTTON_DPLEFT");
                ini.WriteValue("Driving", "Right", "KEY_G, " + gcIndex + "BUTTON_DPRIGHT");
                ini.WriteValue("Driving", "CardInsert", "KEY_F7, " + gcIndex + "BUTTON_GUIDE");
                ini.WriteValue("Driving", "P1_Steer", gcIndex + "AXIS_LEFTX");
                ini.WriteValue("Driving", "P1_Gas", gcIndex + "AXIS_RIGHTTRIGGER");
                ini.WriteValue("Driving", "P1_Brake", gcIndex + "AXIS_LEFTTRIGGER");
            }

            // Flying section
            if (playerindex == 1)
            {
                ini.WriteValue("Flying", "Flying_Left", "KEY_Left, " + gcIndex + "BUTTON_DPLEFT");
                ini.WriteValue("Flying", "Flying_Right", "KEY_Right, " + gcIndex + "BUTTON_DPRIGHT");
                ini.WriteValue("Flying", "Flying_Up", "KEY_Up, " + gcIndex + "BUTTON_DPUP");
                ini.WriteValue("Flying", "Flying_Down", "KEY_Down, " + gcIndex + "BUTTON_DPDOWN");
                ini.WriteValue("Flying", "Throttle_Accelerate", "KEY_X, " + gcIndex + "BUTTON_RIGHTSHOULDER");
                ini.WriteValue("Flying", "Throttle_Slowdown", "KEY_Z, " + gcIndex + "BUTTON_LEFTSHOULDER");
                ini.WriteValue("Flying", "GunTrigger", "KEY_Left Ctrl " + gcIndex + "BUTTON_A");
                ini.WriteValue("Flying", "MissileTrigger", "KEY_Left Alt, " + gcIndex + "BUTTON_B");
                ini.WriteValue("Flying", "ClimaxSwitch", "KEY_Left Shift, " + gcIndex + "BUTTON_Y");
                ini.WriteValue("Flying", "Flying_X", gcIndex + "AXIS_LEFTX");
                ini.WriteValue("Flying", "Flying_Y", gcIndex + "AXIS_LEFTY");
                ini.WriteValue("Flying", "Throttle", gcIndex + "AXIS_RIGHTY_INVERTED, " + gcIndex + "AXIS_RIGHTTRIGGER_POSITIVE_HALF, " + gcIndex + "AXIS_LEFTTRIGGER_NEGATIVE_HALF");
            }

            // Shooting section
            if (playerindex == 1)
            {
                ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
                ini.WriteValue("Shooting", "P1_Trigger", "MOUSE_LEFT_BUTTON");
                ini.WriteValue("Shooting", "P1_Reload", "MOUSE_RIGHT_BUTTON");
                ini.WriteValue("Shooting", "P1_GunButton", "MOUSE_MIDDLE_BUTTON");
                ini.WriteValue("Shooting", "P1_ActionButton", "KEY_R");
                ini.WriteValue("Shooting", "P1_PedalLeft", "KEY_Left");
                ini.WriteValue("Shooting", "P1_PedalRight", "KEY_Right");
            }

            ini.WriteValue("ControllerGUIDs", player + "GUID", guidString);
        }

        public static string RemoveGuidCRC(string guid)
        {
            if (guid == null || guid.Length < 8)
                return guid;

            return guid.Substring(0, 4) + "0000" + guid.Substring(8);
        }
    }
}
