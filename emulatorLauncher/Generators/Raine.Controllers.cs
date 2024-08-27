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

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Raine");

            //UpdateSdlControllersWithHints();

            // clear existing pad sections of ini file
            ClearJoystickSection(ini);

            // Set hotkeys
            SetKeyboardHotKeys(ini);

            // Inject controllers
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigureInput(ini, controller);
        }

        private void ConfigureInput(IniFile ini, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex);
        }

        /// <summary>
        /// Gamepad configuration
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="ctrl"></param>
        /// <param name="playerIndex"></param>
        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerIndex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            //var guid = ctrl.GetSdlGuid(SdlVersion.SDL2_26, true).ToLowerInvariant();

            string iniSection = "default_game_key_config_sdl2";
            int index = ctrl.DeviceIndex + 1;
            bool isNintendo = ctrl.VendorID == USB_VENDOR.NINTENDO;
            int buttonStart = 65536;
            int axisStart = 512;
            int axisIncrement = 256;

            // Coin, service, test and flippers
            if (playerIndex == 1)
            {
                ini.WriteValue(iniSection, "Def_Coin_A_joystick", (buttonStart * 5 + index).ToString());
                ini.WriteValue(iniSection, "Def_Service_joystick", (buttonStart * 8 + index).ToString());
                ini.WriteValue(iniSection, "Def_Test_joystick", (buttonStart * 9 + index).ToString());
                ini.WriteValue(iniSection, "Def_Flipper_1_Left_joystick", (buttonStart * 10 + index).ToString());
                ini.WriteValue(iniSection, "Def_Flipper_1_Right_joystick", (buttonStart * 11 + index).ToString());
                ini.WriteValue(iniSection, "Def_Tilt_Left_joystick", ((axisStart + (axisIncrement * 9) + index)).ToString());
                ini.WriteValue(iniSection, "Def_Tilt_Right_joystick", ((axisStart + (axisIncrement * 11) + index)).ToString());
            }
            else if (playerIndex == 2)
                ini.WriteValue(iniSection, "Def_Coin_B_joystick", (buttonStart * 5 + index).ToString());
            else if (playerIndex == 3)
                ini.WriteValue(iniSection, "Def_Coin_C_joystick", (buttonStart * 5 + index).ToString());
            else if (playerIndex == 4)
                ini.WriteValue(iniSection, "Def_Coin_D_joystick", (buttonStart * 5 + index).ToString());

            // Buttons
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Start_joystick", (buttonStart * 7 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Up_joystick", ((axisStart + (axisIncrement * 2) + index)).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Down_joystick", ((axisStart + (axisIncrement * 3) + index)).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Left_joystick", ((axisStart + (axisIncrement * 0) + index)).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Right_joystick", ((axisStart + (axisIncrement * 1) + index)).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_1_joystick", isNintendo ? (buttonStart * 2 + index).ToString() : (buttonStart * 1 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_2_joystick", isNintendo ? (buttonStart * 1 + index).ToString() : (buttonStart * 2 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_3_joystick", isNintendo ? (buttonStart * 4 + index).ToString() : (buttonStart * 3 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_4_joystick", isNintendo ? (buttonStart * 3 + index).ToString() : (buttonStart * 4 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_5_joystick", (buttonStart * 10 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_6_joystick", (buttonStart * 11 + index).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_7_joystick", ((axisStart + (axisIncrement * 9) + index)).ToString());
            ini.WriteValue(iniSection, "Def_P" + playerIndex + "_Button_8_joystick", ((axisStart + (axisIncrement * 11) + index)).ToString());
            ini.WriteValue(iniSection, "Def_Coin_D_joystick", (buttonStart * 5 + index).ToString());

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
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

            var guidSection = ini.EnumerateKeys(joyConfigSection);
            foreach (var key in guidSection)
            {
                if (key.StartsWith("0300"))
                    ini.Remove(joyConfigSection, key);
            }
        }

        private void SetKeyboardHotKeys(IniFile ini)
        {
            string iniSection = "emulator_key_config_sdl2";

            ini.WriteValue(iniSection, "Save_Screenshot", "67");
            ini.WriteValue(iniSection, "Save_Screenshot_kmod", "0");
            ini.WriteValue(iniSection, "Save_state", "59");
            ini.WriteValue(iniSection, "Save_state_kmod", "0");
            ini.WriteValue(iniSection, "Switch_save_slot", "60");
            ini.WriteValue(iniSection, "Switch_save_slot_kmod", "0");
            ini.WriteValue(iniSection, "Load_state", "61");
            ini.WriteValue(iniSection, "Load_state_kmod", "0");
            ini.WriteValue(iniSection, "Pause_game", "62");
            ini.WriteValue(iniSection, "Pause_game_kmod", "0");
            ini.WriteValue(iniSection, "Return_to_GUI", "43");
            ini.WriteValue(iniSection, "Return_to_GUI_kmod", "0");
            ini.WriteValue(iniSection, "Save_Screenshot", "67");
            ini.WriteValue(iniSection, "Save_Screenshot_kmod", "0");
        }
    }
}