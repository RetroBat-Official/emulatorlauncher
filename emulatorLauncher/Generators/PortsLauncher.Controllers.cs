using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using static EmulatorLauncher.PadToKeyboard.SendKey;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        #region cdogs
        private void ConfigureCDogsControls(DynamicJson settings)
        {
            if (_emulator != "cdogs")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            var input = settings.GetOrCreateContainer("Input");
            var player1 = input.GetOrCreateContainer("PlayerCodes0");
            var player2 = input.GetOrCreateContainer("PlayerCodes1");

            // Set config based on number of pads
            //int controllerCount = Controllers.Where(c => !c.IsKeyboard).Count();
            //Controller c1 = null;
            //Controller c2 = null;

            foreach (var s in cdogsKeyboard1)
                player1[s.Key] = s.Value;

            foreach (var s in cdogsKeyboard2)
                player2[s.Key] = s.Value;
        }
        #endregion

        #region cgenius
        private void ConfigureCGeniusControls(IniFile ini)
        {
            if (_emulator != "cgenius")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // clear existing pad sections of ini file
            for (int i = 0; i < 4; i++)
            {
                ini.ClearSection("input" + i.ToString());
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
            {
                ConfigureCGeniusInput(ini, controller, controller.PlayerIndex - 1);
            }
        }

        private void ConfigureCGeniusInput(IniFile ini, Controller ctrl, int padIndex)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            if (ctrl.IsKeyboard)
            {
                ini.WriteValue("input" + padIndex, "Back", "Key 27 (Escape)");
                ini.WriteValue("input" + padIndex, "Camlead", "Key 99 (C)");
                ini.WriteValue("input" + padIndex, "Down", "Key 1073741905 (Down)");
                ini.WriteValue("input" + padIndex, "Fire", "Key 32 (Space)");
                ini.WriteValue("input" + padIndex, "Help", "Key 1073741882 (F1)");
                ini.WriteValue("input" + padIndex, "Jump", "Key 1073742048 (Left Ctrl)");
                ini.WriteValue("input" + padIndex, "Left", "Key 1073741904 (Left)");
                ini.WriteValue("input" + padIndex, "Lower-Left", "Key 1073741901 (End)");
                ini.WriteValue("input" + padIndex, "Lower-Right", "Key 1073741902 (PageDown)");
                ini.WriteValue("input" + padIndex, "Pogo", "Key 1073742050 (Left Alt)");
                ini.WriteValue("input" + padIndex, "Quickload", "Key 1073741890 (F9)");
                ini.WriteValue("input" + padIndex, "Quicksave", "Key 1073741887 (F6)");
                ini.WriteValue("input" + padIndex, "Right", "Key 1073741903 (Right)");
                ini.WriteValue("input" + padIndex, "Run", "Key 1073742049 (Left Shift)");
                ini.WriteValue("input" + padIndex, "Status", "Key 13 (Return)");
                ini.WriteValue("input" + padIndex, "Up", "Key 1073741906 (Up)");
                ini.WriteValue("input" + padIndex, "Upper-Left", "Key 1073741898 (Home)");
                ini.WriteValue("input" + padIndex, "Upper-Right", "Key 1073741899 (PageUp)");
            }
            else
            {
                if (ctrl == null)
                    return;

                InputConfig joy = ctrl.Config;
                if (joy == null)
                    return;

                string joyPad = "Joy" + (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index) + "-";

                foreach (var button in cgeniusMapping)
                {
                    if (padIndex != 0 && (button.Value == "Back" || button.Value == "Help"))
                        continue;

                    InputKey toSet = button.Key;
                    
                    if (SystemConfig.isOptSet("cgenius_analogPad") && SystemConfig.getOptBoolean("cgenius_analogPad"))
                    {
                        if (button.Key == InputKey.up)
                            toSet = InputKey.leftanalogdown;
                        else if (button.Key == InputKey.down)
                            toSet = InputKey.leftanalogdown;
                        else if (button.Key == InputKey.left)
                            toSet = InputKey.leftanalogleft;
                        else if (button.Key == InputKey.right)
                            toSet = InputKey.leftanalogright;
                    }

                    var input = ctrl.Config[toSet];
                    if (input != null)
                        ini.WriteValue("input" + padIndex, button.Value, joyPad + GetSDLInputName(ctrl, toSet, "cgenius"));
                    else
                        ini.WriteValue("input" + padIndex, button.Value, "Key 0 ()");
                }

                if (padIndex == 0)
                {
                    ini.WriteValue("input" + padIndex, "Quickload", "Key 1073741890 (F9)");
                    ini.WriteValue("input" + padIndex, "Quicksave", "Key 1073741887 (F6)");
                }
                else
                {
                    ini.WriteValue("input" + padIndex, "Quickload", "Key 0 ()");
                    ini.WriteValue("input" + padIndex, "Quicksave", "Key 0 ()");
                }
                ini.WriteValue("input" + padIndex, "Lower-Left", "Key 1073741901 (End)");
                ini.WriteValue("input" + padIndex, "Lower-Right", "Key 1073741902 (PageDown)");
                ini.WriteValue("input" + padIndex, "Upper-Left", "Key 1073741898 (Home)");
                ini.WriteValue("input" + padIndex, "Upper-Right", "Key 1073741899 (PageUp)");

                BindBoolIniFeature(ini, "input" + padIndex, "Analog", "cgenius_analogPad", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "TwoButtonFiring", "cgenius_TwoButtonFiring", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "SuperPogo", "cgenius_SuperPogo", "true", "false");
                BindBoolIniFeatureOn(ini, "input" + padIndex, "ImpossiblePogo", "cgenius_ImpossiblePogo", "true", "false");
                BindBoolIniFeature(ini, "input" + padIndex, "AutoFire", "cgenius_AutoFire", "true", "false");
            }
        }
        #endregion

        #region dhewm3
        private void ConfigureDhewm3Controls(List<Dhewm3ConfigChange> changes)
        {
            if (_emulator != "dhewm3")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // Keyboard defaults
            changes.Add(new Dhewm3ConfigChange("bind", "TAB", "_impulse19"));
            changes.Add(new Dhewm3ConfigChange("bind", "ENTER", "_button2"));
            changes.Add(new Dhewm3ConfigChange("bind", "ESCAPE", "togglemenu"));
            changes.Add(new Dhewm3ConfigChange("bind", "SPACE", "_moveup"));
            changes.Add(new Dhewm3ConfigChange("bind", "/", "_impulse14"));
            changes.Add(new Dhewm3ConfigChange("bind", "0", "_impulse27"));
            changes.Add(new Dhewm3ConfigChange("bind", "1", "_impulse1"));
            changes.Add(new Dhewm3ConfigChange("bind", "2", "_impulse3"));
            changes.Add(new Dhewm3ConfigChange("bind", "3", "_impulse4"));
            changes.Add(new Dhewm3ConfigChange("bind", "4", "_impulse6"));
            changes.Add(new Dhewm3ConfigChange("bind", "5", "_impulse7"));
            changes.Add(new Dhewm3ConfigChange("bind", "6", "_impulse8"));
            changes.Add(new Dhewm3ConfigChange("bind", "7", "_impulse9"));
            changes.Add(new Dhewm3ConfigChange("bind", "8", "_impulse10"));
            changes.Add(new Dhewm3ConfigChange("bind", "9", "_impulse11"));
            changes.Add(new Dhewm3ConfigChange("bind", "[", "_impulse15"));
            changes.Add(new Dhewm3ConfigChange("bind", "\\", "_mlook"));
            changes.Add(new Dhewm3ConfigChange("bind", "]", "_impulse14"));
            changes.Add(new Dhewm3ConfigChange("bind", "a", "_moveleft"));
            changes.Add(new Dhewm3ConfigChange("bind", "c", "_movedown"));
            changes.Add(new Dhewm3ConfigChange("bind", "d", "_moveright"));
            changes.Add(new Dhewm3ConfigChange("bind", "f", "_impulse0"));
            changes.Add(new Dhewm3ConfigChange("bind", "q", "_impulse12"));
            changes.Add(new Dhewm3ConfigChange("bind", "r", "_impulse13"));
            changes.Add(new Dhewm3ConfigChange("bind", "s", "_back"));
            changes.Add(new Dhewm3ConfigChange("bind", "t", "clientMessageMode"));
            changes.Add(new Dhewm3ConfigChange("bind", "w", "_forward"));
            changes.Add(new Dhewm3ConfigChange("bind", "y", "clientMessageMode 1"));
            changes.Add(new Dhewm3ConfigChange("bind", "z", "_zoom"));
            changes.Add(new Dhewm3ConfigChange("bind", "BACKSPACE", "clientDropWeapon"));
            changes.Add(new Dhewm3ConfigChange("bind", "PAUSE", "pause"));
            changes.Add(new Dhewm3ConfigChange("bind", "F11", "pause"));
            changes.Add(new Dhewm3ConfigChange("bind", "UPARROW", "_forward"));
            changes.Add(new Dhewm3ConfigChange("bind", "DOWNARROW", "_back"));
            changes.Add(new Dhewm3ConfigChange("bind", "LEFTARROW", "_moveLeft"));
            changes.Add(new Dhewm3ConfigChange("bind", "RIGHTARROW", "_moveright"));
            changes.Add(new Dhewm3ConfigChange("bind", "ALT", "_strafe"));
            changes.Add(new Dhewm3ConfigChange("bind", "CTRL", "_attack"));
            changes.Add(new Dhewm3ConfigChange("bind", "SHIFT", "_speed"));
            changes.Add(new Dhewm3ConfigChange("bind", "DEL", "_lookdown"));
            changes.Add(new Dhewm3ConfigChange("bind", "PGDN", "_lookup"));
            changes.Add(new Dhewm3ConfigChange("bind", "END", "_impulse18"));
            changes.Add(new Dhewm3ConfigChange("bind", "F1", "_impulse28"));
            changes.Add(new Dhewm3ConfigChange("bind", "F2", "_impulse29"));
            changes.Add(new Dhewm3ConfigChange("bind", "F3", "_impulse17"));
            changes.Add(new Dhewm3ConfigChange("bind", "F5", "savegame quick"));
            changes.Add(new Dhewm3ConfigChange("bind", "F6", "_impulse20"));
            changes.Add(new Dhewm3ConfigChange("bind", "F7", "_impulse22"));
            changes.Add(new Dhewm3ConfigChange("bind", "F9", "loadgame quick"));
            changes.Add(new Dhewm3ConfigChange("bind", "F10", "dhewm3Settings"));
            changes.Add(new Dhewm3ConfigChange("bind", "F12", "screenshot"));
            changes.Add(new Dhewm3ConfigChange("bind", "MOUSE1", "_attack"));
            changes.Add(new Dhewm3ConfigChange("bind", "MOUSE2", "_moveup"));
            changes.Add(new Dhewm3ConfigChange("bind", "MOUSE3", "_zoom"));
            changes.Add(new Dhewm3ConfigChange("bind", "MWHEELDOWN", "_impulse15"));
            changes.Add(new Dhewm3ConfigChange("bind", "MWHEELUP", "_impulse14"));

            int controllerCount = Controllers.Where(c => !c.IsKeyboard).Count();
            if (controllerCount > 0)
            {
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_BTN_SOUTH", "_moveUp"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_BTN_EAST", "_moveDown"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_BTN_WEST", "_speed"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_BTN_NORTH", "_zoom"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_BTN_LSHOULDER", "_impulse13"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_BTN_RSHOULDER", "_attack"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_DPAD_UP", "_forward"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_DPAD_DOWN", "_back"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_DPAD_LEFT", "_moveLeft"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_DPAD_RIGHT", "_moveRight"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK1_UP", "_forward"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK1_DOWN", "_back"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK1_LEFT", "_moveLeft"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK1_RIGHT", "_moveRight"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK2_UP", "_lookUp"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK2_DOWN", "_lookDown"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK2_LEFT", "_left"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_STICK2_RIGHT", "_right"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_TRIGGER1", "_impulse15"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_TRIGGER2", "_impulse14"));
            }
        }
        #endregion

        #region pdark
        private void ConfigurePDarkControls(IniFile ini)
        {
            if (_emulator != "pdark")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            ini.WriteValue("Input", "FirstGamepadNum", "0");

            for (int i = 1; i < 5; i++)
            {
                string gameSection = "Game.Player" + i;
                ini.WriteValue(gameSection, "ExtendedControls", "1");

                string inputSection = "Input.Player" + i;

                if (SystemConfig.isOptSet("pdark_rumble") && !string.IsNullOrEmpty(SystemConfig["pdark_rumble"]))
                    ini.WriteValue(inputSection, "RumbleScale", SystemConfig["pdark_rumble"]);
                else
                    ini.WriteValue(inputSection, "RumbleScale", "0.500000");

                ini.WriteValue(inputSection, "LStickDeadzoneX", "4096");
                ini.WriteValue(inputSection, "LStickDeadzoneY", "4096");
                ini.WriteValue(inputSection, "RStickDeadzoneX", "4096");
                ini.WriteValue(inputSection, "RStickDeadzoneY", "6144");
                ini.WriteValue(inputSection, "LStickScaleX", "1.000000");
                ini.WriteValue(inputSection, "LStickScaleY", "1.000000");
                ini.WriteValue(inputSection, "RStickScaleX", "1.000000");
                ini.WriteValue(inputSection, "RStickScaleY", "1.000000");
                ini.WriteValue(inputSection, "StickCButtons", "0");
                ini.WriteValue(inputSection, "SwapSticks", "1");
                ini.WriteValue(inputSection, "ControllerIndex", "-1");

                string bindingSection = "Input.Player" + i + ".Binds";
                foreach (string key in pdarkMapping)
                {
                    ini.WriteValue(bindingSection, key, "NONE");
                }
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigurePDarkInput(ini, controller, controller.PlayerIndex);
        }

        private void ConfigurePDarkInput(IniFile ini, Controller ctrl, int playerIndex)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;

            string joyIndex = "JOY" + playerIndex.ToString();
            string inputSection = "Input.Player" + playerIndex;
            string bindingSection = "Input.Player" + playerIndex + ".Binds";

            ini.WriteValue(inputSection, "ControllerIndex", index.ToString());

            if (playerIndex == 1)
            {
                ini.WriteValue(bindingSection, "R_CBUTTONS", "D, JOY1_DPAD_RIGHT");
                ini.WriteValue(bindingSection, "L_CBUTTONS", "A, JOY1_DPAD_LEFT");
                ini.WriteValue(bindingSection, "D_CBUTTONS", "S, JOY1_DPAD_DOWN");
                ini.WriteValue(bindingSection, "U_CBUTTONS", "W, JOY1_DPAD_UP");
                ini.WriteValue(bindingSection, "R_TRIG", "MOUSE_RIGHT, Z, JOY1_LTRIGGER");
                ini.WriteValue(bindingSection, "L_TRIG", "F, X, JOY1_RSHOULDER");
                ini.WriteValue(bindingSection, "X_BUTTON", "R, JOY1_X");
                ini.WriteValue(bindingSection, "Y_BUTTON", "MOUSE_WHEEL_DN, JOY1_Y");
                ini.WriteValue(bindingSection, "L_JPAD", "MOUSE_WHEEL_UP, JOY1_B");
                ini.WriteValue(bindingSection, "D_JPAD", "Q, MOUSE_MIDDLE, JOY1_LSHOULDER");
                ini.WriteValue(bindingSection, "START_BUTTON", "RETURN, TAB, JOY1_START");
                ini.WriteValue(bindingSection, "Z_TRIG", "MOUSE_LEFT, SPACE, JOY1_RTRIGGER");
                ini.WriteValue(bindingSection, "B_BUTTON", "E");
                ini.WriteValue(bindingSection, "A_BUTTON", "JOY1_A");
                ini.WriteValue(bindingSection, "STICK_XNEG", "LEFT");
                ini.WriteValue(bindingSection, "STICK_XPOS", "RIGHT");
                ini.WriteValue(bindingSection, "STICK_YNEG", "DOWN");
                ini.WriteValue(bindingSection, "STICK_YPOS", "UP");
                ini.WriteValue(bindingSection, "ACCEPT_BUTTON", "JOY1_A");
                ini.WriteValue(bindingSection, "CANCEL_BUTTON", "JOY1_B");
                ini.WriteValue(bindingSection, "CK_2000", "LEFT_CTRL");
                ini.WriteValue(bindingSection, "CK_4000", "LEFT_SHIFT");
                ini.WriteValue(bindingSection, "CK_8000", "JOY1_LSTICK");
            }
            else
            {
                ini.WriteValue(bindingSection, "R_CBUTTONS", joyIndex + "_DPAD_RIGHT");
                ini.WriteValue(bindingSection, "L_CBUTTONS", joyIndex + "_DPAD_LEFT");
                ini.WriteValue(bindingSection, "D_CBUTTONS", joyIndex + "_DPAD_DOWN");
                ini.WriteValue(bindingSection, "U_CBUTTONS", joyIndex + "_DPAD_UP");
                ini.WriteValue(bindingSection, "R_TRIG", joyIndex + "_LTRIGGER");
                ini.WriteValue(bindingSection, "L_TRIG", joyIndex + "_RSHOULDER");
                ini.WriteValue(bindingSection, "X_BUTTON", joyIndex + "_X");
                ini.WriteValue(bindingSection, "Y_BUTTON", joyIndex + "_Y");
                ini.WriteValue(bindingSection, "L_JPAD", joyIndex + "_B");
                ini.WriteValue(bindingSection, "D_JPAD", joyIndex + "_LSHOULDER");
                ini.WriteValue(bindingSection, "START_BUTTON", joyIndex + "_START");
                ini.WriteValue(bindingSection, "Z_TRIG", joyIndex + "_RTRIGGER");
                ini.WriteValue(bindingSection, "A_BUTTON", joyIndex + "_A");
                ini.WriteValue(bindingSection, "ACCEPT_BUTTON", joyIndex + "_A");
                ini.WriteValue(bindingSection, "CANCEL_BUTTON", joyIndex + "_B");
                ini.WriteValue(bindingSection, "CK_8000", joyIndex + "_LSTICK");
            }
        }
        #endregion

        #region powerbomberman
        private void ConfigurePowerBombermanControls(IniFile ini)
        {
            if (_emulator != "powerbomberman")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // Clear sections
            for (int i = 1; i < 17; i++)
            {
                string playerSection = "PLAYER" + i.ToString();
                ini.WriteValue(playerSection, "type", "0");
                ini.Remove(playerSection, "type2");

                for (int j = 0; j < 11; j++)
                {
                    string key = "key" + j.ToString() + "_0";
                    ini.Remove(playerSection, key);
                }
            }

            if (!this.Controllers.Any(c => !c.IsKeyboard))
            {
                string section = "PLAYER1";

                ini.WriteValue(section, "type", "1");
                ini.WriteValue(section, "type2", "0");
                ini.WriteValue(section, "sensitivity", "0.30");
                ini.WriteValue(section, "key0_0", "40");
                ini.WriteValue(section, "key1_0", "39");
                ini.WriteValue(section, "key2_0", "38");
                ini.WriteValue(section, "key3_0", "37");
                ini.WriteValue(section, "key4_0", "13");

                List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                {
                    ini.WriteValue(section, "key5_0", "68");
                    ini.WriteValue(section, "key6_0", "83");
                    ini.WriteValue(section, "key7_0", "81");
                    ini.WriteValue(section, "key8_0", "90");
                    ini.WriteValue(section, "key9_0", "65");
                }
                else
                {
                    ini.WriteValue(section, "key5_0", "68");
                    ini.WriteValue(section, "key6_0", "83");
                    ini.WriteValue(section, "key7_0", "65");
                    ini.WriteValue(section, "key8_0", "87");
                    ini.WriteValue(section, "key9_0", "81");
                }

                ini.WriteValue(section, "key10_0", "69");
            }

            else
            {
                var xControllers = this.Controllers.Where(c => c.IsXInputDevice && !c.IsKeyboard).ToList();
                var dControllers = this.Controllers.Where(c => !c.IsXInputDevice && !c.IsKeyboard).ToList();
                var diDevices = new DirectInputInfo().GetDinputDevices();
                List<Controller> pbControllers = new List<Controller>();

                var guidOrderLookup = diDevices.Select((d, index) => new { d.InstanceGuid, index }).ToDictionary(x => x.InstanceGuid, x => x.index);
                if (dControllers.Count > 0)
                {
                    dControllers = dControllers.Where(c => guidOrderLookup.ContainsKey(c.DirectInput.InstanceGuid)).OrderBy(c => guidOrderLookup[c.DirectInput.InstanceGuid]).ToList();
                    pbControllers.AddRange(dControllers);
                }
                if (xControllers.Count > 0)
                {
                    xControllers = xControllers.Where(c => guidOrderLookup.ContainsKey(c.DirectInput.InstanceGuid)).OrderBy(c => guidOrderLookup[c.DirectInput.InstanceGuid]).ToList();
                    pbControllers.AddRange(xControllers);
                }

                int i = 0;
                foreach (var controller in pbControllers.Take(16))
                {
                    ConfigurePowerBombermanInput(ini, controller, controller.PlayerIndex, i);
                    i++;
                }
            }
        }

        private void ConfigurePowerBombermanInput(IniFile ini, Controller ctrl, int playerIndex, int padIndex)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            SdlToDirectInput dctrl = null;
            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            dctrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

            string section = "PLAYER" + playerIndex.ToString();
            string index = padIndex.ToString();

            if (ctrl.IsXInputDevice)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerIndex + ". Using generic xInput mapping for : " + guid);

                ini.WriteValue(section, "type", "3");
                ini.WriteValue(section, "type2", index);
                ini.WriteValue(section, "sensitivity", "0.30");

                ini.WriteValue(section, "key0_0", "-4");
                ini.WriteValue(section, "key1_0", "-1");
                ini.WriteValue(section, "key2_0", "-5");
                ini.WriteValue(section, "key3_0", "-2");
                ini.WriteValue(section, "key4_0", "7");
                ini.WriteValue(section, "key5_0", "1");
                ini.WriteValue(section, "key6_0", "0");
                ini.WriteValue(section, "key7_0", "2");
                ini.WriteValue(section, "key8_0", "3");
                ini.WriteValue(section, "key9_0", "4");
                ini.WriteValue(section, "key10_0", "5");
            }
            else if (dctrl != null && dctrl.ButtonMappings != null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerIndex + ". Using gamecontrollerDB file mapping for : " + guid);

                ini.WriteValue(section, "type", "3");
                ini.WriteValue(section, "type2", index);
                ini.WriteValue(section, "sensitivity", "0.30");

                ini.WriteValue(section, "key0_0", "-4");
                ini.WriteValue(section, "key1_0", "-1");
                ini.WriteValue(section, "key2_0", "-5");
                ini.WriteValue(section, "key3_0", "-2");
                ini.WriteValue(section, "key4_0", GetPBDinputMapping(dctrl, "start"));
                ini.WriteValue(section, "key5_0", GetPBDinputMapping(dctrl, "b"));
                ini.WriteValue(section, "key6_0", GetPBDinputMapping(dctrl, "a"));
                ini.WriteValue(section, "key7_0", GetPBDinputMapping(dctrl, "x"));
                ini.WriteValue(section, "key8_0", GetPBDinputMapping(dctrl, "y"));
                ini.WriteValue(section, "key9_0", GetPBDinputMapping(dctrl, "leftshoulder"));
                ini.WriteValue(section, "key10_0", GetPBDinputMapping(dctrl, "rightshoulder"));
            }

            else
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerIndex + ". No controller mapping found in gamecontrollerdb.txt file for guid : " + guid + ". Using es_input info.");
                
                ini.WriteValue(section, "type", "3");
                ini.WriteValue(section, "type2", index);
                ini.WriteValue(section, "sensitivity", "0.30");

                ini.WriteValue(section, "key0_0", "-4");
                ini.WriteValue(section, "key1_0", "-1");
                ini.WriteValue(section, "key2_0", "-5");
                ini.WriteValue(section, "key3_0", "-2");

                ini.WriteValue(section, "key4_0", GetSDLInputName(ctrl, InputKey.start, "pb"));
                ini.WriteValue(section, "key5_0", GetSDLInputName(ctrl, InputKey.b, "pb"));
                ini.WriteValue(section, "key6_0", GetSDLInputName(ctrl, InputKey.a, "pb"));
                ini.WriteValue(section, "key7_0", GetSDLInputName(ctrl, InputKey.y, "pb"));
                ini.WriteValue(section, "key8_0", GetSDLInputName(ctrl, InputKey.x, "pb"));
                ini.WriteValue(section, "key9_0", GetSDLInputName(ctrl, InputKey.pageup, "pb"));
                ini.WriteValue(section, "key10_0", GetSDLInputName(ctrl, InputKey.pagedown, "pb"));
            }
        }
        #endregion

        #region rtcw
        private void ConfigureRTCWControls(List<Dhewm3ConfigChange> changes)
        {
            if (_emulator != "rtcw")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            bool azerty = false;
            List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                azerty = true;

            // Keyboard defaults
            changes.Add(new Dhewm3ConfigChange("bind", "TAB", "notebook"));
            changes.Add(new Dhewm3ConfigChange("bind", "ENTER", "+activate"));
            changes.Add(new Dhewm3ConfigChange("bind", "ESCAPE", "togglemenu"));
            changes.Add(new Dhewm3ConfigChange("bind", "SPACE", "+moveup"));
            changes.Add(new Dhewm3ConfigChange("bind", "-", "zoomout"));
            changes.Add(new Dhewm3ConfigChange("bind", "0", "weaponbank 10"));
            changes.Add(new Dhewm3ConfigChange("bind", "1", "weaponbank 1"));
            changes.Add(new Dhewm3ConfigChange("bind", "2", "weaponbank 2"));
            changes.Add(new Dhewm3ConfigChange("bind", "3", "weaponbank 3"));
            changes.Add(new Dhewm3ConfigChange("bind", "4", "weaponbank 4"));
            changes.Add(new Dhewm3ConfigChange("bind", "5", "weaponbank 5"));
            changes.Add(new Dhewm3ConfigChange("bind", "6", "weaponbank 6"));
            changes.Add(new Dhewm3ConfigChange("bind", "7", "weaponbank 7"));
            changes.Add(new Dhewm3ConfigChange("bind", "8", "weaponbank 8"));
            changes.Add(new Dhewm3ConfigChange("bind", "9", "weaponbank 9"));
            changes.Add(new Dhewm3ConfigChange("bind", "=", "zoomin"));
            changes.Add(new Dhewm3ConfigChange("bind", "[", "weapnext"));
            changes.Add(new Dhewm3ConfigChange("bind", "\\", "+mlook"));
            changes.Add(new Dhewm3ConfigChange("bind", "]", "weapprev"));
            changes.Add(new Dhewm3ConfigChange("bind", "`", "toggleconsole"));
            changes.Add(new Dhewm3ConfigChange("bind", "a", azerty ? "+leanleft" : "+moveleft"));
            changes.Add(new Dhewm3ConfigChange("bind", "b", "+zoom"));
            changes.Add(new Dhewm3ConfigChange("bind", "c", "+movedown"));
            changes.Add(new Dhewm3ConfigChange("bind", "d", "+moveright"));
            changes.Add(new Dhewm3ConfigChange("bind", "e", "+leanright"));
            changes.Add(new Dhewm3ConfigChange("bind", "f", "+activate"));
            changes.Add(new Dhewm3ConfigChange("bind", "g", "+quickgren"));
            changes.Add(new Dhewm3ConfigChange("bind", "q", azerty ? "+moveleft" : "+leanleft"));
            changes.Add(new Dhewm3ConfigChange("bind", "r", "+reload"));
            changes.Add(new Dhewm3ConfigChange("bind", "s", "+back"));
            changes.Add(new Dhewm3ConfigChange("bind", "v", "+kick"));
            changes.Add(new Dhewm3ConfigChange("bind", "w", "+forward"));
            changes.Add(new Dhewm3ConfigChange("bind", "z", azerty ? "+forward" : "weapalt"));
            changes.Add(new Dhewm3ConfigChange("bind", "~", "toggleconsole"));
            changes.Add(new Dhewm3ConfigChange("bind", "CAPSLOCK", "+speed"));
            changes.Add(new Dhewm3ConfigChange("bind", "PAUSE", "pause"));
            changes.Add(new Dhewm3ConfigChange("bind", "UPARROW", "+forward"));
            changes.Add(new Dhewm3ConfigChange("bind", "DOWNARROW", "+back"));
            changes.Add(new Dhewm3ConfigChange("bind", "LEFTARROW", "+moveLeft"));
            changes.Add(new Dhewm3ConfigChange("bind", "RIGHTARROW", "+moveright"));
            changes.Add(new Dhewm3ConfigChange("bind", "ALT", "+strafe"));
            changes.Add(new Dhewm3ConfigChange("bind", "CTRL", "+attack"));
            changes.Add(new Dhewm3ConfigChange("bind", "SHIFT", "+sprint"));
            changes.Add(new Dhewm3ConfigChange("bind", "DEL", "+lookdown"));
            changes.Add(new Dhewm3ConfigChange("bind", "PGDN", "+lookup"));
            changes.Add(new Dhewm3ConfigChange("bind", "END", "centerview"));
            changes.Add(new Dhewm3ConfigChange("bind", "F1", "itemprev"));
            changes.Add(new Dhewm3ConfigChange("bind", "F2", "itemnext"));
            changes.Add(new Dhewm3ConfigChange("bind", "F3", "+useitem"));
            changes.Add(new Dhewm3ConfigChange("bind", "F4", "+scores"));
            changes.Add(new Dhewm3ConfigChange("bind", "F5", "savegame quicksave"));
            changes.Add(new Dhewm3ConfigChange("bind", "F9", "loadgame quicksave"));
            changes.Add(new Dhewm3ConfigChange("bind", "F10", "loadgame lastcheckpoint"));
            changes.Add(new Dhewm3ConfigChange("bind", "F11", "screenshot"));
            changes.Add(new Dhewm3ConfigChange("bind", "MOUSE1", "+attack"));
            changes.Add(new Dhewm3ConfigChange("bind", "MOUSE2", "+attack2"));
            changes.Add(new Dhewm3ConfigChange("bind", "MOUSE3", "weapalt"));
            changes.Add(new Dhewm3ConfigChange("bind", "MWHEELDOWN", "weapnext"));
            changes.Add(new Dhewm3ConfigChange("bind", "MWHEELUP", "weapprev"));

            if (azerty)
            {
                changes.Add(new Dhewm3ConfigChange("bind", "^", "weapnext"));
                changes.Add(new Dhewm3ConfigChange("bind", "$", "weapprev"));
            }

            // Controller defaults
            int controllerCount = Controllers.Where(c => !c.IsKeyboard).Count();
            if (controllerCount > 0)
            {
                var c1 = Controllers.Where(c => !c.IsKeyboard && c.PlayerIndex == 1).FirstOrDefault();
                string index = c1 != null ? c1.DeviceIndex.ToString() : "0";
                bool nintendo = c1.VendorID == USB_VENDOR.NINTENDO;

                changes.Add(new Dhewm3ConfigChange("seta", "in_joystick", "1"));
                changes.Add(new Dhewm3ConfigChange("seta", "in_joystickNo", index));
                changes.Add(new Dhewm3ConfigChange("seta", "in_joystickUseAnalog", "1"));

                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_B", nintendo ? "+moveup" : "+movedown"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_A", nintendo ? "+movedown" : "+moveup"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_X", nintendo ? "+activate" : "+reload"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_X", nintendo ? "+reload" : "+activate"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_BACK", "notebook"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_START", "togglemenu"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTSTICK_CLICK", "+sprint"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTSTICK_CLICK", "+kick"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTSHOULDER", "weapprev"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTSHOULDER", "weapnext"));
                changes.Add(new Dhewm3ConfigChange("bind", "JOY_DPAD_RIGHT", "_moveRight"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_DPAD_UP", "savegame quicksave"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_DPAD_DOWN", "loadgame quicksave"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_DPAD_LEFT", "itemprev"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_DPAD_RIGHT", "+useitem"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTSTICK_LEFT", "+moveleft"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTSTICK_RIGHT", "+moveright"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTSTICK_UP", "+forward"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTSTICK_DOWN", "+back"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTSTICK_LEFT", "+left"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTSTICK_RIGHT", "+right"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTSTICK_UP", "+lookup"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTSTICK_DOWN", "+lookdown"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_LEFTTRIGGER", "weapalt"));
                changes.Add(new Dhewm3ConfigChange("bind", "PAD0_RIGHTTRIGGER", "+attack"));
            }
            else
                changes.Add(new Dhewm3ConfigChange("seta", "in_joystick", "0"));
        }
        #endregion

        #region ship
        private Dictionary<string, InputKey> shipLayout = new Dictionary<string, InputKey>()
        {
            { "ls_d0", InputKey.leftanalogleft },       // stick left
            { "ls_d1", InputKey.leftanalogright },      // stick right
            { "ls_d2", InputKey.leftanalogup },         // stick up
            { "ls_d3", InputKey.leftanalogdown },       // stick down
            { "button_1", InputKey.rightanalogright },  // C right
            { "button_1024", InputKey.down },           // D-pad down
            { "button_16", InputKey.pagedown },         // R1
            { "button_16384", InputKey.y },             // West B
            { "button_2", InputKey.rightanalogleft },   // C left
            { "button_2048", InputKey.up },             // D-pad up
            { "button_256", InputKey.right },           // D-pad right
            { "button_32", InputKey.pageup },           // L1
            { "button_32768", InputKey.a },             // South A
            { "button_4", InputKey.rightanalogdown },   // C down
            { "button_4096", InputKey.start },          // Start
            { "button_512", InputKey.left },            // D-pad left
            { "button_8", InputKey.rightanalogup },     // C up
            { "button_8192", InputKey.l2 },             // L2 Z
        };
        private Dictionary<string, string> shipLayoutSDL = new Dictionary<string, string>()
        {
            { "ls_d0", "SDLA0-ADN" },       // stick left
            { "ls_d1", "SDLA0-ADP" },       // stick right
            { "ls_d2", "SDLA1-ADN" },       // stick up
            { "ls_d3", "SDLA1-ADP" },       // stick down
            { "button_1", "SDLA2-ADP" },    // C right
            { "button_1024", "SDLB12" },    // D-pad down
            { "button_16", "SDLB10" },      // R1
            { "button_16384", "SDLB2" },    // West B
            { "button_2", "SDLA2-ADN" },    // C left
            { "button_2048", "SDLB11" },    // D-pad up
            { "button_256", "SDLB14" },     // D-pad right
            { "button_32", "SDLB9" },       // L1
            { "button_32768", "SDLB0" },    // South A
            { "button_4", "SDLA3-ADP" },    // C down
            { "button_4096", "SDLB6" },     // Start
            { "button_512", "SDLB13" },     // D-pad left
            { "button_8", "SDLA3-ADN" },    // C up
            { "button_8192", "SDLA4-ADP" }, // L2 Z
        };

        private void ConfigureShipControls(JObject controllers)
        {
            if (_emulator != "2ship")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }
            int controllerCount = this.Controllers.Where(c => !c.IsKeyboard).Count();

            if (controllerCount == 0)
            {
                SimpleLogger.Instance.Info("[INFO] No controller available.");
                return;
            }

            var controller = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            if (controller.SdlController == null)
            {
                SimpleLogger.Instance.Info("[CONTROLS] Player 1 controller not known in SDL database, no configuration possible.");
                return;
            }

            // clear existing pad sections of ini file
            controllers.Remove("AxisDirectionMappings");
            controllers.Remove("ButtonMappings");
            controllers.Remove("GyroMappings");
            controllers.Remove("Port1");
            controllers.Remove("Port2");
            controllers.Remove("RumbleMappings");

            string n64guid = controller.Guid.ToLowerInvariant();
            string n64json = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
            bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");
            N64Controller n64Gamepad = null;

            if (File.Exists(n64json))
            {
                try
                {
                    var n64Controllers = N64Controller.LoadControllersFromJson(n64json);

                    if (n64Controllers != null)
                    {
                        n64Gamepad = N64Controller.GetN64Controller("ship2", n64guid, n64Controllers);

                        if (n64Gamepad.ControllerInfo != null && n64Gamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                        {
                            if (n64Gamepad.ControllerInfo["needActivationSwitch"] == "true")
                            {
                                if (!n64_pad)
                                {
                                    SimpleLogger.Instance.Info("[CONTROLS] Controller needs activation switch to work as N64 pad. Enable n64_pad option.");
                                    n64Gamepad = null;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            ConfigureShipInput(controllers, controller, controllerCount, n64Gamepad);
        }

        private void ConfigureShipInput(JObject controllers, Controller ctrl, int count, N64Controller n64Gamepad)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            if (ctrl.IsKeyboard)
                return;

            var cconfig = ctrl.Config;

            int deadzone = 20;
            int rumblestrength = 50;
            float gyrosensitivity = 1.0f;

            if (SystemConfig.isOptSet("ship2_deadzone") && !string.IsNullOrEmpty(SystemConfig["ship2_deadzone"]))
                deadzone = (int)double.Parse(SystemConfig["ship2_deadzone"], CultureInfo.InvariantCulture);
            if (SystemConfig.isOptSet("ship2_rumblestrength") && !string.IsNullOrEmpty(SystemConfig["ship2_rumblestrength"]))
                rumblestrength = (int)double.Parse(SystemConfig["ship2_rumblestrength"], CultureInfo.InvariantCulture);
            if (SystemConfig.isOptSet("ship2_gyroSensivity") && !string.IsNullOrEmpty(SystemConfig["ship2_gyroSensivity"]))
                gyrosensitivity = float.Parse(SystemConfig["ship2_gyroSensivity"], CultureInfo.InvariantCulture);

            SimpleLogger.Instance.Info("[CONTROLS] Configuring controller " + ctrl.Guid == null ? ctrl.DevicePath.ToString() : ctrl.Guid.ToString());

            JObject axisdirectionmappings;
            JObject buttonmappings;
            JObject gyromappings;
            JObject port1;
            JObject p1buttons;
            JObject p1leftstick;
            JObject p1rightstick;
            JObject p1gyro;
            JObject rumblemappings;

            axisdirectionmappings = new JObject();
            controllers["AxisDirectionMappings"] = axisdirectionmappings;

            buttonmappings = new JObject();
            controllers["ButtonMappings"] = buttonmappings;

            gyromappings = new JObject();
            controllers["GyroMappings"] = gyromappings;

            port1 = new JObject();
            controllers["Port1"] = port1;

            p1buttons = new JObject();
            port1["Buttons"] = p1buttons;

            p1leftstick = new JObject();
            port1["LeftStick"] = p1leftstick;

            p1rightstick = new JObject();
            port1["RightStick"] = p1rightstick;

            p1gyro = new JObject();
            port1["Gyro"] = p1gyro;

            rumblemappings = new JObject();
            controllers["RumbleMappings"] = rumblemappings;

            if (n64Gamepad != null)
            {
                SimpleLogger.Instance.Info("[CONTROLS] Using N64 mapping for controller " + ctrl.Guid.ToString());

                if (n64Gamepad.Mapping == null)
                {
                    SimpleLogger.Instance.Info("[CONTROLS] Mapping empty for controller " + ctrl.Guid.ToString());
                    return;
                }

                var layout = n64Gamepad.Mapping;
                if (n64Gamepad.ControllerInfo != null && n64Gamepad.ControllerInfo.ContainsKey("switch_trigger"))
                {
                    if (SystemConfig.isOptSet("ship2_controllayout") && SystemConfig["ship2_controllayout"] == "z_right")
                        layout["button_8192"] = n64Gamepad.ControllerInfo["switch_trigger"];
                }

                foreach (var line in layout)
                {
                    string button = line.Value;
                    string shipButton = line.Key;

                    JObject mapping = new JObject();

                    if (shipButton.StartsWith("ls_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 0;

                        string key = "P0-S0-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("rs_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 1;

                        string key = "P0-S1-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        if (button.StartsWith("SDLA"))
                        {
                            if (button.EndsWith("ADN"))
                                mapping["AxisDirection"] = -1;
                            else
                                mapping["AxisDirection"] = 1;
                        }

                        mapping["Bitmask"] = bitmask;

                        if (button.StartsWith("SDLA"))
                        {
                            mapping["ButtonMappingClass"] = "SDLAxisDirectionToButtonMapping";
                            mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        }
                        else
                        {
                            mapping["ButtonMappingClass"] = "SDLButtonToButtonMapping";
                            mapping["SDLControllerButton"] = button.Substring(4).ToInteger();
                        }

                        string key = "P0-B" + mask + "-" + button;
                        
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }
                    else
                        continue;
                }
            }

            else if (SystemConfig.getOptBoolean("ship2_noSDL"))
            {
                if (SystemConfig.isOptSet("ship2_controllayout") && SystemConfig["ship2_controllayout"] == "z_right")
                {
                    shipLayout["button_8192"] = InputKey.r2;
                }
                
                foreach (var line in shipLayout)
                {
                    var button = line.Value;
                    string shipButton = line.Key;

                    bool isAxis = cconfig[button].Type == "axis";
                    bool isHat = cconfig[button].Type == "hat";
                    long inputID = cconfig[button].Id;
                    long inputValue = cconfig[button].Value;

                    if (isHat)
                    {
                        switch (inputValue)
                        {
                            case 1:
                                inputID = 11;
                                inputValue = 1;
                                break;
                            case 2:
                                inputID = 14;
                                inputValue = 1;
                                break;
                            case 4:
                                inputID = 12;
                                inputValue = 1;
                                break;
                            case 8:
                                inputID = 13;
                                inputValue = 1;
                                break;
                        }
                    }

                    JObject mapping = new JObject();

                    if (isAxis && shipButton.StartsWith("ls_"))
                    {
                        mapping["AxisDirection"] = inputValue;
                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";
                        
                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = inputID;
                        mapping["Stick"] = 0;
                        
                        string key = "P0-S0-" + shipButton.Substring(3).ToUpperInvariant() + "-SDLA" + inputID.ToString() + "-AD" + (inputValue < 0 ? "N" : "P");
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (isAxis && shipButton.StartsWith("rs_"))
                    {
                        mapping["AxisDirection"] = inputValue;
                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = inputID;
                        mapping["Stick"] = 1;

                        string key = "P0-S1-" + shipButton.Substring(3).ToUpperInvariant() + "-SDLA" + inputID.ToString() + "-AD" + (inputValue < 0 ? "N" : "P");
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (isAxis && shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        mapping["AxisDirection"] = inputValue;
                        mapping["Bitmask"] = bitmask;
                        mapping["ButtonMappingClass"] = "SDLAxisDirectionToButtonMapping";
                        mapping["SDLControllerAxis"] = inputID;

                        string key = "P0-B" + mask + "-SDLA" + inputID.ToString() + "-AD" + (inputValue < 0 ? "N" : "P");
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }
                    else if (!isAxis && shipButton.StartsWith("ls_"))
                    {
                        mapping["AxisDirectionMappingClass"] = "SDLButtonToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerButton"] = inputID;
                        mapping["Stick"] = 0;

                        string key = "P0-S0-D" + shipButton.Substring(3).ToUpperInvariant() + "-SDLB" + inputID.ToString();
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (!isAxis && shipButton.StartsWith("rs_"))
                    {
                        mapping["AxisDirectionMappingClass"] = "SDLButtonToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerButton"] = inputID;
                        mapping["Stick"] = 1;

                        string key = "P0-S1-D" + shipButton.Substring(3).ToUpperInvariant() + "-SDLB" + inputID.ToString();
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (!isAxis && shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        mapping["Bitmask"] = bitmask;
                        mapping["ButtonMappingClass"] = "SDLButtonToButtonMapping";
                        mapping["SDLControllerButton"] = inputID;

                        string key = "P0-B" + mask + "-SDLB" + inputID.ToString();
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }

                    else
                        continue;
                }
            }
            else
            {
                if (SystemConfig.isOptSet("ship2_controllayout") && SystemConfig["ship2_controllayout"] == "z_right")
                {
                    shipLayoutSDL["button_8192"] = "SDLA5-ADP";
                }

                foreach (var line in shipLayoutSDL)
                {
                    string button = line.Value;
                    string shipButton = line.Key;
                    bool isNintendo = ctrl.VendorID == USB_VENDOR.NINTENDO;

                    if (isNintendo)
                    {
                        switch (button)
                        {
                            case "SDLB0":
                                button = "SDLB1";
                                break;
                            case "SDLB2":
                                button = "SDLB3";
                                break;
                            case "SDLB1":
                                button = "SDLB0";
                                break;
                            case "SDLB3":
                                button = "SDLB2";
                                break;
                        }
                    }

                    JObject mapping = new JObject();

                    if (shipButton.StartsWith("ls_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 0;

                        string key = "P0-S0-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("rs_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 1;

                        string key = "P0-S1-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        if (button.StartsWith("SDLA"))
                        {
                            if (button.EndsWith("ADN"))
                                mapping["AxisDirection"] = -1;
                            else
                                mapping["AxisDirection"] = 1;
                        }
                            
                        mapping["Bitmask"] = bitmask;

                        if (button.StartsWith("SDLA"))
                        {
                            mapping["ButtonMappingClass"] = "SDLAxisDirectionToButtonMapping";
                            mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        }
                        else
                        {
                            mapping["ButtonMappingClass"] = "SDLButtonToButtonMapping";
                            mapping["SDLControllerButton"] = button.Substring(4).ToInteger();
                        }
                        
                        string key = "P0-B" + mask + "-" + button;
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }
                    else
                        continue;
                }
            }

            port1["HasConfig"] = 1;
            port1["LEDMappingIds"] = "";

            // Rumble
            string rumbleID = "P0,";
            for (int i = 1; i < count; i++)
            {
                rumbleID += "P0,";
            }

            if (SystemConfig.getOptBoolean("ship2_rumble"))
            {
                port1["RumbleMappingIds"] = rumbleID;

                rumblemappings["P0"] = new JObject
                    {
                        { "HighFrequencyIntensity", rumblestrength },
                        { "LowFrequencyIntensity", rumblestrength },
                        { "RumbleMappingClass", "SDLRumbleMapping" }
                    };
            }
            else
                port1["RumbleMappingIds"] = "";

            // Gyro
            gyromappings["P0"] = new JObject
            {
                { "GyroMappingClass", "SDLGyroMapping" },
                { "Sensitivity", gyrosensitivity }
            };

            if (SystemConfig.getOptBoolean("ship2_gyro"))
                p1gyro["GyroMappingId"] = "P0";
            else
                p1gyro["GyroMappingId"] = "";
        }
        #endregion

        #region soh
        private void ConfigureSOHControls(JObject controllers)
        {
            if (_emulator != "soh")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }
            int controllerCount = this.Controllers.Where(c => !c.IsKeyboard).Count();

            if (controllerCount == 0)
            {
                SimpleLogger.Instance.Info("[INFO] No controller available.");
                return;
            }

            var controller = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            if (controller.SdlController == null)
            {
                SimpleLogger.Instance.Info("[CONTROLS] Player 1 controller not known in SDL database, no configuration possible.");
                return;
            }

            // clear existing pad sections of ini file
            controllers.Remove("AxisDirectionMappings");
            controllers.Remove("ButtonMappings");
            controllers.Remove("GyroMappings");
            controllers.Remove("Port1");
            controllers.Remove("Port2");
            controllers.Remove("RumbleMappings");

            string n64guid = controller.Guid.ToLowerInvariant();
            string n64json = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
            bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");
            N64Controller n64Gamepad = null;

            if (File.Exists(n64json))
            {
                try
                {
                    var n64Controllers = N64Controller.LoadControllersFromJson(n64json);

                    if (n64Controllers != null)
                    {
                        n64Gamepad = N64Controller.GetN64Controller("soh", n64guid, n64Controllers);

                        if (n64Gamepad.ControllerInfo != null && n64Gamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                        {
                            if (n64Gamepad.ControllerInfo["needActivationSwitch"] == "true")
                            {
                                if (!n64_pad)
                                {
                                    SimpleLogger.Instance.Info("[CONTROLS] Controller needs activation switch to work as N64 pad. Enable n64_pad option.");
                                    n64Gamepad = null;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            ConfigureSOHInput(controllers, controller, controllerCount, n64Gamepad);
        }

        private void ConfigureSOHInput(JObject controllers, Controller ctrl, int count, N64Controller n64Gamepad)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            if (ctrl.IsKeyboard)
                return;

            var cconfig = ctrl.Config;

            int deadzone = 20;
            int rumblestrength = 50;
            float gyrosensitivity = 1.0f;

            if (SystemConfig.isOptSet("soh_deadzone") && !string.IsNullOrEmpty(SystemConfig["soh_deadzone"]))
                deadzone = (int)double.Parse(SystemConfig["soh_deadzone"], CultureInfo.InvariantCulture);
            if (SystemConfig.isOptSet("soh_rumblestrength") && !string.IsNullOrEmpty(SystemConfig["soh_rumblestrength"]))
                rumblestrength = (int)double.Parse(SystemConfig["soh_rumblestrength"], CultureInfo.InvariantCulture);
            if (SystemConfig.isOptSet("soh_gyroSensivity") && !string.IsNullOrEmpty(SystemConfig["soh_gyroSensivity"]))
                gyrosensitivity = float.Parse(SystemConfig["soh_gyroSensivity"], CultureInfo.InvariantCulture);

            SimpleLogger.Instance.Info("[CONTROLS] Configuring controller " + ctrl.Guid == null ? ctrl.DevicePath.ToString() : ctrl.Guid.ToString());

            JObject axisdirectionmappings;
            JObject buttonmappings;
            JObject gyromappings;
            JObject port1;
            JObject p1buttons;
            JObject p1leftstick;
            JObject p1rightstick;
            JObject p1gyro;
            JObject rumblemappings;

            axisdirectionmappings = new JObject();
            controllers["AxisDirectionMappings"] = axisdirectionmappings;

            buttonmappings = new JObject();
            controllers["ButtonMappings"] = buttonmappings;

            gyromappings = new JObject();
            controllers["GyroMappings"] = gyromappings;

            port1 = new JObject();
            controllers["Port1"] = port1;

            p1buttons = new JObject();
            port1["Buttons"] = p1buttons;

            p1leftstick = new JObject();
            port1["LeftStick"] = p1leftstick;

            p1rightstick = new JObject();
            port1["RightStick"] = p1rightstick;

            p1gyro = new JObject();
            port1["Gyro"] = p1gyro;

            rumblemappings = new JObject();
            controllers["RumbleMappings"] = rumblemappings;

            if (n64Gamepad != null)
            {
                SimpleLogger.Instance.Info("[CONTROLS] Using N64 mapping for controller " + ctrl.Guid.ToString());

                if (n64Gamepad.Mapping == null)
                {
                    SimpleLogger.Instance.Info("[CONTROLS] Mapping empty for controller " + ctrl.Guid.ToString());
                    return;
                }

                var layout = n64Gamepad.Mapping;
                if (n64Gamepad.ControllerInfo != null && n64Gamepad.ControllerInfo.ContainsKey("switch_trigger"))
                {
                    if (SystemConfig.isOptSet("soh_controllayout") && SystemConfig["soh_controllayout"] == "z_right")
                        layout["button_8192"] = n64Gamepad.ControllerInfo["switch_trigger"];
                }

                foreach (var line in layout)
                {
                    string button = line.Value;
                    string shipButton = line.Key;

                    JObject mapping = new JObject();

                    if (shipButton.StartsWith("ls_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 0;

                        string key = "P0-S0-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("rs_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 1;

                        string key = "P0-S1-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        if (button.StartsWith("SDLA"))
                        {
                            if (button.EndsWith("ADN"))
                                mapping["AxisDirection"] = -1;
                            else
                                mapping["AxisDirection"] = 1;
                        }

                        mapping["Bitmask"] = bitmask;

                        if (button.StartsWith("SDLA"))
                        {
                            mapping["ButtonMappingClass"] = "SDLAxisDirectionToButtonMapping";
                            mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        }
                        else
                        {
                            mapping["ButtonMappingClass"] = "SDLButtonToButtonMapping";
                            mapping["SDLControllerButton"] = button.Substring(4).ToInteger();
                        }

                        string key = "P0-B" + mask + "-" + button;

                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }
                    else
                        continue;
                }
            }

            else if (SystemConfig.getOptBoolean("soh_noSDL"))
            {
                if (SystemConfig.isOptSet("soh_controllayout") && SystemConfig["soh_controllayout"] == "z_right")
                {
                    shipLayout["button_8192"] = InputKey.r2;
                }

                foreach (var line in shipLayout)
                {
                    var button = line.Value;
                    string shipButton = line.Key;

                    bool isAxis = cconfig[button].Type == "axis";
                    bool isHat = cconfig[button].Type == "hat";
                    long inputID = cconfig[button].Id;
                    long inputValue = cconfig[button].Value;

                    if (isHat)
                    {
                        switch (inputValue)
                        {
                            case 1:
                                inputID = 11;
                                inputValue = 1;
                                break;
                            case 2:
                                inputID = 14;
                                inputValue = 1;
                                break;
                            case 4:
                                inputID = 12;
                                inputValue = 1;
                                break;
                            case 8:
                                inputID = 13;
                                inputValue = 1;
                                break;
                        }
                    }

                    JObject mapping = new JObject();

                    if (isAxis && shipButton.StartsWith("ls_"))
                    {
                        mapping["AxisDirection"] = inputValue;
                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = inputID;
                        mapping["Stick"] = 0;

                        string key = "P0-S0-" + shipButton.Substring(3).ToUpperInvariant() + "-SDLA" + inputID.ToString() + "-AD" + (inputValue < 0 ? "N" : "P");
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (isAxis && shipButton.StartsWith("rs_"))
                    {
                        mapping["AxisDirection"] = inputValue;
                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = inputID;
                        mapping["Stick"] = 1;

                        string key = "P0-S1-" + shipButton.Substring(3).ToUpperInvariant() + "-SDLA" + inputID.ToString() + "-AD" + (inputValue < 0 ? "N" : "P");
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (isAxis && shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        mapping["AxisDirection"] = inputValue;
                        mapping["Bitmask"] = bitmask;
                        mapping["ButtonMappingClass"] = "SDLAxisDirectionToButtonMapping";
                        mapping["SDLControllerAxis"] = inputID;

                        string key = "P0-B" + mask + "-SDLA" + inputID.ToString() + "-AD" + (inputValue < 0 ? "N" : "P");
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }
                    else if (!isAxis && shipButton.StartsWith("ls_"))
                    {
                        mapping["AxisDirectionMappingClass"] = "SDLButtonToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerButton"] = inputID;
                        mapping["Stick"] = 0;

                        string key = "P0-S0-D" + shipButton.Substring(3).ToUpperInvariant() + "-SDLB" + inputID.ToString();
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (!isAxis && shipButton.StartsWith("rs_"))
                    {
                        mapping["AxisDirectionMappingClass"] = "SDLButtonToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerButton"] = inputID;
                        mapping["Stick"] = 1;

                        string key = "P0-S1-D" + shipButton.Substring(3).ToUpperInvariant() + "-SDLB" + inputID.ToString();
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (!isAxis && shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        mapping["Bitmask"] = bitmask;
                        mapping["ButtonMappingClass"] = "SDLButtonToButtonMapping";
                        mapping["SDLControllerButton"] = inputID;

                        string key = "P0-B" + mask + "-SDLB" + inputID.ToString();
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }

                    else
                        continue;
                }
            }
            else
            {
                if (SystemConfig.isOptSet("soh_controllayout") && SystemConfig["soh_controllayout"] == "z_right")
                {
                    shipLayoutSDL["button_8192"] = "SDLA5-ADP";
                }

                foreach (var line in shipLayoutSDL)
                {
                    string button = line.Value;
                    string shipButton = line.Key;
                    bool isNintendo = ctrl.VendorID == USB_VENDOR.NINTENDO;

                    if (isNintendo)
                    {
                        switch (button)
                        {
                            case "SDLB0":
                                button = "SDLB1";
                                break;
                            case "SDLB2":
                                button = "SDLB3";
                                break;
                            case "SDLB1":
                                button = "SDLB0";
                                break;
                            case "SDLB3":
                                button = "SDLB2";
                                break;
                        }
                    }

                    JObject mapping = new JObject();

                    if (shipButton.StartsWith("ls_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "ls_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "ls_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "ls_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "ls_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 0;

                        string key = "P0-S0-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "ls_d0":
                                p1leftstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d1":
                                p1leftstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d2":
                                p1leftstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "ls_d3":
                                p1leftstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1leftstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("rs_"))
                    {
                        if (button.EndsWith("ADN"))
                            mapping["AxisDirection"] = -1;
                        else
                            mapping["AxisDirection"] = 1;

                        mapping["AxisDirectionMappingClass"] = "SDLAxisDirectionToAxisDirectionMapping";

                        switch (shipButton)
                        {
                            case "rs_d0":
                                mapping["Direction"] = 0;
                                break;
                            case "rs_d1":
                                mapping["Direction"] = 1;
                                break;
                            case "rs_d2":
                                mapping["Direction"] = 2;
                                break;
                            case "rs_d3":
                                mapping["Direction"] = 3;
                                break;
                        }

                        mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        mapping["Stick"] = 1;

                        string key = "P0-S1-" + shipButton.Substring(3).ToUpperInvariant() + "-" + button;
                        axisdirectionmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        switch (shipButton)
                        {
                            case "rs_d0":
                                p1rightstick["LeftAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d1":
                                p1rightstick["RightAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d2":
                                p1rightstick["UpAxisDirectionMappingIds"] = newKey;
                                break;
                            case "rs_d3":
                                p1rightstick["DownAxisDirectionMappingIds"] = newKey;
                                break;
                        }
                        p1rightstick["DeadzonePercentage"] = deadzone;
                    }
                    else if (shipButton.StartsWith("button_"))
                    {
                        string mask = shipButton.Substring(7);
                        int bitmask = mask.ToInteger();

                        if (button.StartsWith("SDLA"))
                        {
                            if (button.EndsWith("ADN"))
                                mapping["AxisDirection"] = -1;
                            else
                                mapping["AxisDirection"] = 1;
                        }

                        mapping["Bitmask"] = bitmask;

                        if (button.StartsWith("SDLA"))
                        {
                            mapping["ButtonMappingClass"] = "SDLAxisDirectionToButtonMapping";
                            mapping["SDLControllerAxis"] = button.Substring(4, 1).ToInteger();
                        }
                        else
                        {
                            mapping["ButtonMappingClass"] = "SDLButtonToButtonMapping";
                            mapping["SDLControllerButton"] = button.Substring(4).ToInteger();
                        }

                        string key = "P0-B" + mask + "-" + button;
                        buttonmappings[key] = mapping;

                        string newKey = key + ",";
                        for (int i = 1; i < count; i++)
                        {
                            newKey += key + ",";
                        }

                        string buttonkey = mask + "ButtonMappingIds";
                        p1buttons[buttonkey] = newKey;
                    }
                    else
                        continue;
                }
            }

            port1["HasConfig"] = 1;
            port1["LEDMappingIds"] = "";

            // Rumble
            string rumbleID = "P0,";
            for (int i = 1; i < count; i++)
            {
                rumbleID += "P0,";
            }

            if (SystemConfig.getOptBoolean("soh_rumble"))
            {
                port1["RumbleMappingIds"] = rumbleID;

                rumblemappings["P0"] = new JObject
                    {
                        { "HighFrequencyIntensity", rumblestrength },
                        { "LowFrequencyIntensity", rumblestrength },
                        { "RumbleMappingClass", "SDLRumbleMapping" }
                    };
            }
            else
                port1["RumbleMappingIds"] = "";

            // Gyro
            gyromappings["P0"] = new JObject
            {
                { "GyroMappingClass", "SDLGyroMapping" },
                { "Sensitivity", gyrosensitivity }
            };

            if (SystemConfig.getOptBoolean("soh_gyro"))
                p1gyro["GyroMappingId"] = "P0";
            else
                p1gyro["GyroMappingId"] = "";
        }
        #endregion

        #region sonic3air
        private void ConfigureSonic3airControls(string configFolder, DynamicJson settings)
        {
            if (_emulator != "sonic3air")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            if (!Controllers.Any(c => !c.IsKeyboard))
                return;

            settings["PreferredGamepadPlayer1"] = string.Empty;
            settings["PreferredGamepadPlayer2"] = string.Empty;

            string inputSettingsFile = Path.Combine(configFolder, "settings_input.json");

            var inputJson = DynamicJson.Load(inputSettingsFile);

            var inputDevices = inputJson.GetOrCreateContainer("InputDevices");

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(2))
            {
                string deviceName = controller.Name;
                var device = inputDevices.GetOrCreateContainer(deviceName);
                bool isXinput = controller.IsXInputDevice;

                string[] deviceNames = new string[] { deviceName };
                device.SetObject("DeviceNames", deviceNames);

                string[] up = new string[] { GetSDLInputName(controller, InputKey.up, "sonic3air") };
                string[] down = new string[] { GetSDLInputName(controller, InputKey.down, "sonic3air") };
                string[] left = new string[] { GetSDLInputName(controller, InputKey.left, "sonic3air") };
                string[] right = new string[] { GetSDLInputName(controller, InputKey.right, "sonic3air") };
                string[] a = new string[] { GetSDLInputName(controller, InputKey.a, "sonic3air") };
                string[] b = new string[] { GetSDLInputName(controller, InputKey.b, "sonic3air") };
                string[] x = new string[] { GetSDLInputName(controller, InputKey.y, "sonic3air") };
                string[] y = new string[] { GetSDLInputName(controller, InputKey.x, "sonic3air") };
                string[] start = new string[] { GetSDLInputName(controller, InputKey.start, "sonic3air") };
                string[] back = new string[] { GetSDLInputName(controller, InputKey.select, "sonic3air") };
                string[] l = new string[] { GetSDLInputName(controller, InputKey.pageup, "sonic3air") };
                string[] r = new string[] { GetSDLInputName(controller, InputKey.pagedown, "sonic3air") };
                device.SetObject("Up", up);
                device.SetObject("Down", down);
                device.SetObject("Left", left);
                device.SetObject("Right", right);
                device.SetObject("A", a);
                device.SetObject("B", b);
                device.SetObject("X", x);
                device.SetObject("Y", y);
                device.SetObject("Start", start);
                device.SetObject("Back", back);
                device.SetObject("L", l);
                device.SetObject("R", r);

                if (controller.PlayerIndex == 1)
                    settings["PreferredGamepadPlayer1"] = deviceName;
                if (controller.PlayerIndex == 2)
                    settings["PreferredGamepadPlayer2"] = deviceName;
            }

            inputJson.Save();
        }
        #endregion

        #region starship
        private void ConfigureStarshipControls(JObject controllers)
        {
            if (_emulator != "starship")
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }
            int controllerCount = this.Controllers.Where(c => !c.IsKeyboard).Count();

            if (controllerCount == 0)
            {
                SimpleLogger.Instance.Info("[INFO] No controller available.");
                return;
            }
        }
        #endregion

        #region general tools
        /// <summary>
        /// Method to retrieve SDL button information
        /// </summary>
        /// <param name="c">Controller</param>
        /// <param name="key">InputKey</param>
        /// <param name="port">Name of the port</param>
        /// <param name="forceAxisSignPositive">Use for triggers for example to force positive axis</param>
        /// <returns></returns>
        private static string GetSDLInputName(Controller c, InputKey key, string port = "default", bool forceAxisSignPositive = false)
        {
            Int64 pid;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    if (port == "cgenius")
                        return "B" + pid;
                    else if (port == "sonic3air")
                        return "Button" + pid;
                    else if (port == "soh" && c.IsXInputDevice)
                    {
                        if (sohXinputRemap.ContainsKey(pid))
                            return sohXinputRemap[pid].ToString();
                        else
                            return pid.ToString();
                    }
                    else
                        return pid.ToString();
                }

                else if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1:
                            if (port == "cgenius")
                                return "H1";
                            else if (port == "soh")
                                return "11";
                            else if (port == "pb")
                                return "-5";
                            else
                                return "Pov0";
                        case 2:
                            if (port == "cgenius")
                                return "H2";
                            else if (port == "soh")
                                return "14";
                            else if (port == "pb")
                                return "-1";
                            else
                                return "Pov1";
                        case 4:
                            if (port == "cgenius")
                                return "H4";
                            else if (port == "soh")
                                return "12";
                            else if (port == "pb")
                                return "-4";
                            else
                                return "Pov2";
                        case 8:
                            if (port == "cgenius")
                                return "H8";
                            else if (port == "soh")
                                return "13";
                            else if (port == "pb")
                                return "-2";
                            else
                                return "Pov3";
                    }
                }

                else if (input.Type == "axis")
                {
                    pid = input.Id;
                    if (port == "sonic3air")
                    {
                        switch (pid)
                        {
                            case 0:
                                if (revertAxis) return "Axis1";
                                else return "Axis0";
                            case 1:
                                if (revertAxis) return "Axis3";
                                else return "Axis2";
                            case 2:
                                if (revertAxis) return "Axis5";
                                else return "Axis4";
                            case 3:
                                if (revertAxis) return "Axis7";
                                else return "Axis8";
                            case 4: return "Axis9";
                            case 5: return "Axis11";
                        }
                    }
                    else if (port == "soh")
                    {
                        if (revertAxis || forceAxisSignPositive) return (512 + pid).ToString();
                        else return "-" + (512 + pid).ToString();
                    }
                    else
                    {
                        if (revertAxis) return "A" + pid + "-";
                        else return "A" + pid + "+";
                    }
                }
            }
            return "";
        }

        private string GetPBDinputMapping(SdlToDirectInput c, string buttonkey, int axisDirection = 0)
        {
            if (c == null)
                return "";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "";
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "";
            }

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());
                return buttonID.ToString();
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "-5";
                    case 2:
                        return "-1";
                    case 4:
                        return "-4";
                    case 8:
                        return "-2";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    axisDirection = -1;
                }
                if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    axisDirection = 1;
                }

                return "0";

                /*switch (axisID)
                {
                    case 0:
                        if (axisDirection == 1) return "XAXIS_RIGHT_SWITCH";
                        else if (axisDirection == -1) return "XAXIS_LEFT_SWITCH";
                        else return "XAXIS";
                    case 1:
                        if (axisDirection == 1) return "YAXIS_DOWN_SWITCH";
                        else if (axisDirection == -1) return "YAXIS_UP_SWITCH";
                        else return "YAXIS";
                    case 2:
                        if (axisDirection == 1) return "ZAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "ZAXIS_NEG_SWITCH";
                        else return "ZAXIS";
                    case 3:
                        if (axisDirection == 1) return "RXAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RXAXIS_NEG_SWITCH";
                        else return "RXAXIS";
                    case 4:
                        if (axisDirection == 1) return "RYAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RYAXIS_NEG_SWITCH";
                        else return "RYAXIS";
                    case 5:
                        if (axisDirection == 1) return "RZAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RZAXIS_NEG_SWITCH";
                        else return "RZAXIS";
                }*/
            }
            return "";
        }
        #endregion

        #region Dictionaries
        /// <summary>
        /// Dictionaries and mappings can be added below if necessary, keep alphabetical order and name it with port name
        /// </summary>

        private static readonly Dictionary<string, string> cdogsKeyboard1 = new Dictionary<string, string>()
        {
            { "left", "80" },
            { "right", "79" },
            { "up", "82" },
            { "down", "81" },
            { "button1", "44" },
            { "button2", "29" },
            { "grenade", "22" },
            { "map", "4" }
        };

        private static readonly Dictionary<string, string> cdogsKeyboard2 = new Dictionary<string, string>()
        {
            { "left", "92" },
            { "right", "94" },
            { "up", "96" },
            { "down", "90" },
            { "button1", "88" },
            { "button2", "99" },
            { "grenade", "91" },
            { "map", "98" }
        };

        private readonly InputKeyMapping cgeniusMapping = new InputKeyMapping
        {
            { InputKey.select,      "Back" },
            { InputKey.pagedown,    "Camlead" },
            { InputKey.down,        "Down" },
            { InputKey.b,           "Fire" },
            { InputKey.start,       "Help" },
            { InputKey.a,           "Jump" },
            { InputKey.left,        "Left" },
            { InputKey.x,           "Pogo" },
            { InputKey.right,       "Right" },
            { InputKey.y,           "Run" },
            { InputKey.pageup,      "Status" },
            { InputKey.up,          "Up" }
        };

        private readonly List<string> pdarkMapping = new List<string>
        { "R_CBUTTONS", "L_CBUTTONS", "D_CBUTTONS", "U_CBUTTONS", "R_TRIG", "L_TRIG", "X_BUTTON", "Y_BUTTON", "R_JPAD", "L_JPAD", "D_JPAD", "U_JPAD", "START_BUTTON",
        "Z_TRIG", "B_BUTTON", "A_BUTTON", "STICK_XNEG", "STICK_XPOS", "STICK_YNEG", "STICK_YPOS", "ACCEPT_BUTTON", "CANCEL_BUTTON", "CK_0040",
        "CK_0080", "CK_0100", "CK_0200", "CK_0400", "CK_0800", "CK_1000", "CK_2000", "CK_4000", "CK_8000" };

        private static readonly Dictionary<long, long> sohXinputRemap = new Dictionary<long, long>()
        {
            { 6, 4 },
            { 7, 6 },
            { 4, 9 },
            { 5, 10 },
            { 8, 7 },
            { 9, 8 },
            { 10, 5 }
        };
        #endregion
    }
}