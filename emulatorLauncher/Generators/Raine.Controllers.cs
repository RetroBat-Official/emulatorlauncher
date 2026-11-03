using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class RaineGenerator : Generator
    {
        /*
        /// <summary>
        /// Cf. 
        /// </summary>
        /// <param name="ini"></param>
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>
            { };

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private bool _pad2Keyoverride = false;
        private SdlVersion _sdlVersion = SdlVersion.SDL2_0_X;

        // SDL2 game controller buttons and axes (SDL_GameControllerButton / SDL_GameControllerAxis), read by Raine for the game controllers
        private const int GC_A = 0, GC_B = 1, GC_X = 2, GC_Y = 3, GC_BACK = 4, GC_START = 6, GC_LEFTSTICK = 7, GC_RIGHTSTICK = 8, GC_LEFTSHOULDER = 9, GC_RIGHTSHOULDER = 10;
        private const int GC_AXIS_LEFTX = 0, GC_AXIS_LEFTY = 1, GC_AXIS_TRIGGERLEFT = 4, GC_AXIS_TRIGGERRIGHT = 5;

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                SetKeyboardHotKeys(ini);
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Raine");

            // clear existing pad sections of ini file
            ClearJoystickSection(ini);

            // Set hotkeys
            SetKeyboardHotKeys(ini);

            // Inject controllers
            var pads = this.Controllers.Where(c => !c.IsKeyboard && c.Config != null).OrderBy(c => c.PlayerIndex).ToList();
            bool pinned = PinJoysticks(ini, pads);

            for (int i = 0; i < pads.Count && i < 4; i++)
                ConfigureJoystick(ini, pads[i], pads[i].PlayerIndex, pinned ? i + 1 : pads[i].DeviceIndex + 1);
        }

        /// <summary>
        /// [emulator_joy_config] : Raine gives each joystick the index stored for its SDL GUID (control.c, get_joy_index), else its SDL player index.
        /// Identical controllers share their GUID : they cannot be pinned, Raine order is used.
        /// </summary>
        private bool PinJoysticks(IniFile ini, List<Controller> pads)
        {
            var guids = pads.Select(c => c.GetSdlGuid(_sdlVersion, true).ToLowerInvariant()).ToList();

            if (guids.Distinct().Count() != guids.Count)
            {
                SimpleLogger.Instance.Warning("[Raine] Several controllers share the same SDL GUID, they are not pinned : player order may differ.");
                return false;
            }

            for (int i = 0; i < pads.Count; i++)
                ini.WriteValue("emulator_joy_config", guids[i], i.ToString());

            return true;
        }

        /// <summary>
        /// Gamepad configuration. Raine reads the SDL game controllers with the standard layout,
        /// the other devices (arcade encoders, joysticks) with their SDL numbering, the one of es_input.cfg.
        /// </summary>
        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex, int joy)
        {
            // Raine has 4 players
            if (playerIndex > 4)
                return;

            string iniSection = "default_game_key_config_sdl2";
            string player = "Def_P" + playerIndex + "_";

            bool gameController = ctrl.SdlController != null || ctrl.IsXInputDevice;
            bool isNintendo = ctrl.VendorID == USB_VENDOR.NINTENDO;

            // Game controller button, or es_input.cfg input of the device
            Func<InputKey, int, string> button = (key, gcButton) => (gameController ? RaineJoy(joy, 0, gcButton + 1, 0) : GetRawCode(ctrl, key, joy)).ToString();
            Func<InputKey, int, bool, string> axis = (key, gcAxis, positive) => (gameController ? RaineJoy(joy, AxisCode(gcAxis, positive), 0, 0) : GetRawCode(ctrl, key, joy)).ToString();

            // Coin, service, test and flippers
            if (playerIndex == 1)
            {
                ini.WriteValue(iniSection, "Def_Coin_A_joystick", button(InputKey.select, GC_BACK));

                // Test opens the test menu : R3, never L3 which is pressed while playing with the stick
                ini.WriteValue(iniSection, "Def_Service_joystick", button(InputKey.l3, GC_LEFTSTICK));
                ini.WriteValue(iniSection, "Def_Test_joystick", button(InputKey.r3, GC_RIGHTSTICK));

                ini.WriteValue(iniSection, "Def_Flipper_1_Left_joystick", button(InputKey.pageup, GC_LEFTSHOULDER));
                ini.WriteValue(iniSection, "Def_Flipper_1_Right_joystick", button(InputKey.pagedown, GC_RIGHTSHOULDER));
                ini.WriteValue(iniSection, "Def_Tilt_Left_joystick", axis(InputKey.l2, GC_AXIS_TRIGGERLEFT, true));
                ini.WriteValue(iniSection, "Def_Tilt_Right_joystick", axis(InputKey.r2, GC_AXIS_TRIGGERRIGHT, true));
            }
            else
                ini.WriteValue(iniSection, "Def_Coin_" + (char)('A' + playerIndex - 1) + "_joystick", button(InputKey.select, GC_BACK));

            // Directions : left stick for the game controllers (Raine moves the d-pad on it, USE HAT AND STICK)
            ini.WriteValue(iniSection, player + "Start_joystick", button(InputKey.start, GC_START));
            ini.WriteValue(iniSection, player + "Up_joystick", axis(InputKey.up, GC_AXIS_LEFTY, false));
            ini.WriteValue(iniSection, player + "Down_joystick", axis(InputKey.down, GC_AXIS_LEFTY, true));
            ini.WriteValue(iniSection, player + "Left_joystick", axis(InputKey.left, GC_AXIS_LEFTX, false));
            ini.WriteValue(iniSection, player + "Right_joystick", axis(InputKey.right, GC_AXIS_LEFTX, true));

            // Buttons 1-4 by position : south, east, west, north (Nintendo game controllers give the button labels)
            ini.WriteValue(iniSection, player + "Button_1_joystick", button(InputKey.a, isNintendo ? GC_B : GC_A));
            ini.WriteValue(iniSection, player + "Button_2_joystick", button(InputKey.b, isNintendo ? GC_A : GC_B));
            ini.WriteValue(iniSection, player + "Button_3_joystick", button(InputKey.y, isNintendo ? GC_Y : GC_X));
            ini.WriteValue(iniSection, player + "Button_4_joystick", button(InputKey.x, isNintendo ? GC_X : GC_Y));
            ini.WriteValue(iniSection, player + "Button_5_joystick", button(InputKey.pageup, GC_LEFTSHOULDER));
            ini.WriteValue(iniSection, player + "Button_6_joystick", button(InputKey.pagedown, GC_RIGHTSHOULDER));
            ini.WriteValue(iniSection, player + "Button_7_joystick", axis(InputKey.l2, GC_AXIS_TRIGGERLEFT, true));
            ini.WriteValue(iniSection, player + "Button_8_joystick", axis(InputKey.r2, GC_AXIS_TRIGGERRIGHT, true));

            SimpleLogger.Instance.Info("[Raine] Player " + playerIndex + " : " + ctrl.Name + " => joystick " + joy + (gameController ? " (game controller)" : " (joystick)"));
        }

        // Raine joystick code (control_internal.h, JOY macro) : joystick | axis << 8 | button << 16 | hat << 24, each one counted from 1
        private static int RaineJoy(int joy, int axis, int button, int hat)
        {
            return joy | (axis << 8) | (button << 16) | (hat << 24);
        }

        // AXIS_LEFT / AXIS_RIGHT of Raine : negative or positive side of an axis
        private static int AxisCode(int axis, bool positive)
        {
            return (axis + 1) * 2 + (positive ? 1 : 0);
        }

        /// <summary>
        /// Raw SDL input of es_input.cfg (button, axis side or hat direction) as a Raine joystick code, 0 when not configured.
        /// </summary>
        private static int GetRawCode(Controller ctrl, InputKey key, int joy)
        {
            var input = ctrl.Config[key];
            if (input == null)
                return 0;

            switch (input.Type)
            {
                case "button":
                    return RaineJoy(joy, 0, (int)input.Id + 1, 0);
                case "axis":
                    return RaineJoy(joy, AxisCode((int)input.Id, input.Value > 0), 0, 0);
                case "hat":
                    return RaineJoy(joy, 0, 0, (((int)input.Id + 1) << 4) | (int)input.Value);
            }

            return 0;
        }

        static readonly List<string> genericControls = new List<string>() { "Def_Coin_A_joystick", "Def_Coin_B_joystick", "Def_Coin_C_joystick", "Def_Coin_D_joystick", "Def_Service_joystick", "Def_Test_joystick", "Def_Service_A_joystick", "Def_Service_B_joystick", "Def_Service_C_joystick", "Next_Game_joystick", "Prev_Game_joystick" };
        static readonly List<string> defaultPlayerControls = new List<string>() { "Start_joystick", "Up_joystick", "Down_joystick", "Left_joystick", "Right_joystick", "Button_1_joystick", "Button_2_joystick", "Button_3_joystick", "Button_4_joystick", "Button_5_joystick", "Button_6_joystick", "Button_7_joystick", "Button_8_joystick" };
        static readonly List<string> defaultP1OnlyControls = new List<string>() { "A_joystick", "E_joystick", "I_joystick", "M_joystick", "Kan_joystick", "B_joystick", "F_joystick", "J_joystick", "N_joystick", "Reach_joystick", "C_joystick", "G_joystick", "K_joystick", "Chi_joystick", "Ron_joystick", "D_joystick", "H_joystick", "L_joystick", "Pon_joystick" };
        static readonly List<string> playerControls = new List<string>() { "B1_B2_joystick", "B3_B4_joystick", "B2_B3_joystick", "B1_B2_B3_joystick", "B2_B3_B4_joystick" };
        static readonly List<string> flipperControls = new List<string>() { "Def_Flipper_1_Left_joystick", "Def_Flipper_1_Right_joystick", "Def_Flipper_2_Left_joystick", "Def_Flipper_2_Right_joystick", "Def_Tilt_Left_joystick", "Def_Tilt_Right_joystick" };
        static readonly List<string> buttonControls = new List<string>() { "Def_Button_1_Left_joystick", "Def_Button_1_Right_joystick", "Def_Button_2_Left_joystick", "Def_Button_2_Right_joystick" };

        private void ClearJoystickSection(IniFile ini)
        {
            string iniSection = "default_game_key_config_sdl2";
            string joyConfigSection = "emulator_joy_config";

            foreach (var key in genericControls)
            {
                ini.WriteValue(iniSection, key, "0");
            }

            for (int i = 1; i < 5; i++)
            {
                foreach (var key in defaultPlayerControls)
                {
                    string keyName = "Def_P" + i + "_" + key;
                    ini.WriteValue(iniSection, keyName, "0");
                }
            }

            foreach (var key in defaultP1OnlyControls)
            {
                string keyName = "Def_P1_" + key;
                ini.WriteValue(iniSection, keyName, "0");
            }

            for (int i = 1; i < 3; i++)
            {
                foreach (var key in playerControls)
                {
                    string keyName = "Player" + i + "_" + key;
                    ini.WriteValue(iniSection, keyName, "0");
                }
            }

            foreach (var key in flipperControls)
            {
                ini.WriteValue(iniSection, key, "0");
            }

            foreach (var key in buttonControls)
            {
                ini.WriteValue(iniSection, key, "0");
            }

            // Joystick indexes saved by Raine (SDL GUID = index), whatever the bus (USB 0300, Bluetooth 0500...)
            var guidSection = ini.EnumerateKeys(joyConfigSection);
            foreach (var key in guidSection)
            {
                if (key.Length == 32 && key.All(Uri.IsHexDigit))
                    ini.Remove(joyConfigSection, key);
            }

            // Write coin & start for keyboard
            ini.WriteValue(iniSection, "Def_P1_Start", "30");
            ini.WriteValue(iniSection, "Def_P2_Start", "31");
            ini.WriteValue(iniSection, "Def_P3_Start", "32");
            ini.WriteValue(iniSection, "Def_P4_Start", "33");
            ini.WriteValue(iniSection, "Def_Coin_A", "34");
            ini.WriteValue(iniSection, "Def_Coin_B", "35");
            ini.WriteValue(iniSection, "Def_Coin_C", "36");
            ini.WriteValue(iniSection, "Def_Coin_D", "37");

            // Test 0, Service 9 (SDL scancodes)
            ini.WriteValue(iniSection, "Def_Test", "39");
            ini.WriteValue(iniSection, "Def_Service", "38");
        }

        private void SetKeyboardHotKeys(IniFile ini)
        {
            string iniSection = "emulator_key_config_sdl2";
            var iniValues = ini.EnumerateValues(iniSection);

            ini.Remove(iniSection, "Return_to_GUI");

            ini.WriteValue(iniSection, "Reset_game", "66");        // F9
            ini.WriteValue(iniSection, "Reset_game_kmod", "0");
            ini.WriteValue(iniSection, "Stop_emulation", "69");        // F10
            ini.WriteValue(iniSection, "Stop_emulation_kmod", "0");

            if (Hotkeys.GetHotKeysFromFile("raine", "", out Dictionary<string, HotkeyResult> hotkeys))
            {
                foreach (var hk in hotkeys)
                {
                    string key = hk.Value.EmulatorKey;
                    string keyKmod = key + "_kmod";
                    string value = hk.Value.EmulatorValue;

                    if (iniValues.Any(i => i.Value == value && i.Key != key && !i.Key.StartsWith("Def_") && !i.Key.StartsWith("Player") && !i.Key.StartsWith("key_layer") && i.Key != "mouse_sensitivity"))
                    {
                        var v = iniValues.Where(i => i.Value == value && i.Key != key).FirstOrDefault();
                        string kmod = v.Key + "_kmod";
                        if (iniValues.Any(j => j.Key == kmod))
                        {
                            var vkmod = iniValues.Where(j => j.Key == kmod).FirstOrDefault();
                            if (vkmod.Value == "0")
                            {
                                ini.WriteValue(iniSection, v.Key, "0");
                            }
                        }
                    }
                    ini.WriteValue(iniSection, key, value);
                    ini.WriteValue(iniSection, keyKmod, "0");
                }
                _pad2Keyoverride = true;
                return;
            }

            ini.WriteValue(iniSection, "Save_Screenshot", "65");            // F8
            ini.WriteValue(iniSection, "Save_Screenshot_kmod", "0");        // No modifier
            ini.WriteValue(iniSection, "Save_state", "59");                 // F2
            ini.WriteValue(iniSection, "Save_state_kmod", "0");
            ini.WriteValue(iniSection, "Switch_save_slot", "63");           // F6
            ini.WriteValue(iniSection, "Switch_save_slot_kmod", "0");
            ini.WriteValue(iniSection, "Load_state", "61");                 // F4
            ini.WriteValue(iniSection, "Load_state_kmod", "0");
            ini.WriteValue(iniSection, "Pause_game", "19");                 // P
            ini.WriteValue(iniSection, "Pause_game_kmod", "0");
            ini.WriteValue(iniSection, "Return_to_GUI", "58");              // F1
            ini.WriteValue(iniSection, "Return_to_GUI_kmod", "0");
            ini.WriteValue(iniSection, "Fullscreen", "9");                  // F
            ini.WriteValue(iniSection, "Fullscreen_kmod", "0");
            ini.WriteValue(iniSection, "Forward_1_frame_in_pause", "14");   // K
            ini.WriteValue(iniSection, "Forward_1_frame_in_pause_kmod", "0");
            ini.WriteValue(iniSection, "Quit_without_saving", "41");        // ESC
            ini.WriteValue(iniSection, "Quit_without_saving_kmod", "0");
        }
    }
}