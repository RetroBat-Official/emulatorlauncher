using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class FlycastGenerator
    {
        /// <summary>
        /// cf. https://github.com/flyinghead/flycast/blob/master/core/sdl/sdl.cpp
        /// </summary>
        /*private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            if (hints.Count > 0)
            {
                SdlGameController.ReloadWithHints(string.Join(",", hints));
                Program.Controllers.ForEach(c => c.ResetSdlController());
            }
        }*/

        private void CreateControllerConfiguration(string path, string system, IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Flycast");

            // Reset controller attribution
            ini.WriteValue("input", "RawInput", "no");
            ini.WriteValue("input", "maple_sdl_keyboard", "0");
            ini.WriteValue("input", "maple_sdl_mouse", "0");
            ini.WriteValue("config", "rend.CrossHairColor1", "0");
            ini.WriteValue("config", "rend.CrossHairColor2", "0");

            for (int i = 1; i < 5; i++)
            {
                ini.WriteValue("input", "device" + i, "0");
                ini.WriteValue("input", "device" + i + ".1" , "1");
                ini.WriteValue("input", "device" + i + ".2", "10");
                ini.Remove("input", "maple_sdl_joystick_" + (i-1));
            }
            
            string mappingPath = Path.Combine(path, "mappings");

            bool guns = SystemConfig.getOptBoolean("use_guns") || SystemConfig["flycast_controller1"] == "7" || SystemConfig["flycast_controller2"] == "7";
            bool useWheel = SystemConfig.getOptBoolean("use_wheel");

            Dictionary<string, int> double_pads = new Dictionary<string, int>();
            int nsamepad = 0;

            if (useWheel)
                ConfigureFlycastWheels(ini, mappingPath, system);

            else
            {
                foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                    ConfigureInput(ini, controller, mappingPath, system, double_pads, nsamepad);

                if (guns)
                    ConfigureFlycastGuns(ini, mappingPath);
            }
        }

        private void ConfigureInput(IniFile ini, Controller controller, string mappingPath, string system, Dictionary<string, int> double_pads, int nsamepad)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, mappingPath, system);
            else
                ConfigureJoystick(ini, controller, mappingPath, system, double_pads, nsamepad);

            if (controller.PlayerIndex == 1 && !controller.IsKeyboard)
                ConfigureKBHotkeys(ini, mappingPath, system);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, string mappingPath, string system)
        {
            if (keyboard == null)
                return;

            string mappingFile = Path.Combine(mappingPath, "SDL_Keyboard.cfg");

            if (_isArcade)
                mappingFile = Path.Combine(mappingPath, "SDL_Keyboard_arcade.cfg");

            if (File.Exists(mappingFile))
                File.Delete(mappingFile);

            if (SystemConfig.isOptSet("flycast_controller1") && !string.IsNullOrEmpty(SystemConfig["flycast_controller1"]))
                ini.WriteValue("input", "device1", SystemConfig["flycast_controller1"]);
            else
                ini.WriteValue("input", "device1", "0");

            ini.WriteValue("input", "device1.1", "1");

            if (SystemConfig.isOptSet("flycast_extension1") && !string.IsNullOrEmpty(SystemConfig["flycast_extension1"]))
                ini.WriteValue("input", "device1.2", SystemConfig["flycast_extension1"]);
            else
                ini.WriteValue("input", "device1.2", "10");

            using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
            {
                Action<int, InputKey, string> WriteKeyboardMapping = (v, k, m) =>
                {
                    var a = keyboard[k];

                    if (a != null)
                    {
                        int id = (int)a.Id;
                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int flycastKey = keycodeToHID[keycode];

                        ctrlini.WriteValue("digital", "bind" + v, flycastKey + ":" + m);
                    }
                };

                bool fightGame6Buttons = SystemConfig.isOptSet("flycast_fightgame6buttons") && SystemConfig.getOptBoolean("flycast_fightgame6buttons");

                ctrlini.ClearSection("digital");
                ctrlini.ClearSection("emulator");
                ctrlini.ClearSection("analog");

                if (_isArcade)
                {
                    WriteKeyboardMapping(0, InputKey.l2, "btn_trigger_left");                                       // left trigger
                    WriteKeyboardMapping(1, fightGame6Buttons ? InputKey.x : InputKey.b, "btn_b");                  // button 2
                    WriteKeyboardMapping(10, fightGame6Buttons ? InputKey.y : InputKey.a, "btn_a");                 // button 1
                    WriteKeyboardMapping(11, InputKey.r2, "btn_trigger_right");                                     // right trigger
                    ctrlini.WriteValue("digital", "bind12", "34:btn_d");                                            // coin (5)
                    WriteKeyboardMapping(13, InputKey.start, "btn_start");                                          // start
                    ctrlini.WriteValue("digital", "bind14", "43:btn_menu");                                         // menu (tab)
                    ctrlini.WriteValue("digital", "bind15", "44:btn_fforward");                                     // fast forward (space)
                    ctrlini.WriteValue("digital", "bind16", "58:btn_dpad2_up");                                     // service (F1)
                    ctrlini.WriteValue("digital", "bind17", "59:btn_dpad2_down");                                   // test (F2)
                    WriteKeyboardMapping(18, InputKey.right, "btn_dpad1_right");
                    WriteKeyboardMapping(19, InputKey.left, "btn_dpad1_left");
                    WriteKeyboardMapping(2, fightGame6Buttons ? InputKey.b : InputKey.pagedown, "btn_y");           // button 5 (strong or medium kick)
                    WriteKeyboardMapping(20, InputKey.down, "btn_dpad1_down");
                    WriteKeyboardMapping(21, InputKey.up, "btn_dpad1_up");
                    ctrlini.WriteValue("digital", "bind22", "90:axis2_down");                                       // right stick down (numpad 2)
                    ctrlini.WriteValue("digital", "bind23", "92:axis2_left");                                       // right stick left (numpad 4)
                    ctrlini.WriteValue("digital", "bind24", "94:axis2_right");                                      // right stick right (numpad 6)
                    ctrlini.WriteValue("digital", "bind25", "96:axis2_up");                                         // right stick up (numpad 8)
                    ctrlini.WriteValue("digital", "bind26", "41:btn_escape");                               
                    ctrlini.WriteValue("digital", "bind27", "60:btn_jump_state");                                   // F3 load state
                    ctrlini.WriteValue("digital", "bind28", "61:btn_quick_save");                                   // F4 save state
                    WriteKeyboardMapping(3, fightGame6Buttons ? InputKey.pagedown : InputKey.pageup, "btn_z");      // button 6
                    ctrlini.WriteValue("digital", "bind4", "12:btn_analog_up");                                     // analog stick up (i)
                    ctrlini.WriteValue("digital", "bind5", "13:btn_analog_left");                                   // analog stick up (j)
                    ctrlini.WriteValue("digital", "bind6", "14:btn_analog_down");                                   // analog stick up (k)
                    ctrlini.WriteValue("digital", "bind7", "15:btn_analog_right");                                  // analog stick up (l)
                    WriteKeyboardMapping(8, fightGame6Buttons ? InputKey.a : InputKey.x, "btn_x");                  // button 4
                    WriteKeyboardMapping(9, fightGame6Buttons ? InputKey.pageup : InputKey.y, "btn_c");             // button 3
                }
                else
                {
                    WriteKeyboardMapping(0, InputKey.b, "btn_b");                                                   // B
                    WriteKeyboardMapping(1, InputKey.x, "btn_y");                                                   // Y
                    WriteKeyboardMapping(10, InputKey.start, "btn_start");                                          // START
                    ctrlini.WriteValue("digital", "bind11", "43:btn_menu");                                         // menu (tab)
                    ctrlini.WriteValue("digital", "bind12", "44:btn_fforward");                                     // fast forward (space)
                    WriteKeyboardMapping(13, InputKey.right, "btn_dpad1_right");
                    WriteKeyboardMapping(14, InputKey.left, "btn_dpad1_left");
                    WriteKeyboardMapping(15, InputKey.down, "btn_dpad1_down");
                    WriteKeyboardMapping(16, InputKey.up, "btn_dpad1_up");
                    ctrlini.WriteValue("digital", "bind17", "90:btn_dpad2_down");                                   // right stick down (numpad 2)
                    ctrlini.WriteValue("digital", "bind18", "92:btn_dpad2_left");                                   // right stick left (numpad 4)
                    ctrlini.WriteValue("digital", "bind19", "94:btn_dpad2_right");                                  // right stick right (numpad 6)
                    WriteKeyboardMapping(2, InputKey.pageup, "btn_trigger_left");
                    ctrlini.WriteValue("digital", "bind20", "96:btn_dpad2_up");                                     // right stick up (numpad 8)
                    ctrlini.WriteValue("digital", "bind21", "41:btn_escape");
                    ctrlini.WriteValue("digital", "bind22", "60:btn_jump_state");                                   // F3 load state
                    ctrlini.WriteValue("digital", "bind23", "61:btn_quick_save");                                   // F4 save state
                    ctrlini.WriteValue("digital", "bind3", "12:btn_analog_up");                                     // analog stick up (i)
                    ctrlini.WriteValue("digital", "bind4", "13:btn_analog_left");                                   // analog stick up (j)
                    ctrlini.WriteValue("digital", "bind5", "14:btn_analog_down");                                   // analog stick up (k)
                    ctrlini.WriteValue("digital", "bind6", "15:btn_analog_right");                                  // analog stick up (l)
                    WriteKeyboardMapping(7, InputKey.y, "btn_x");                                                   // X
                    WriteKeyboardMapping(8, InputKey.pagedown, "btn_trigger_right");
                    WriteKeyboardMapping(9, InputKey.a, "btn_a");                                                   // A
                }

                ctrlini.WriteValue("emulator", "dead_zone", "10");
                ctrlini.WriteValue("emulator", "mapping_name", "Keyboard");
                ctrlini.WriteValue("emulator", "rumble_power", "100");
                ctrlini.WriteValue("emulator", "version", "3");

                ctrlini.Save();
            }
        }

        private void ConfigureKBHotkeys(IniFile ini, string mappingPath, string system)
        {
            string mappingFile = Path.Combine(mappingPath, "SDL_Keyboard.cfg");

            if (_isArcade)
                mappingFile = Path.Combine(mappingPath, "SDL_Keyboard_arcade.cfg");

            if (File.Exists(mappingFile))
                File.Delete(mappingFile);

            using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
            {
                ctrlini.ClearSection("digital");
                ctrlini.ClearSection("emulator");
                ctrlini.ClearSection("analog");

                if (_isArcade)
                {
                    ctrlini.WriteValue("digital", "bind0", "4:btn_y");
                    ctrlini.WriteValue("digital", "bind1", "6:btn_c");
                    ctrlini.WriteValue("digital", "bind10", "25:btn_x");
                    ctrlini.WriteValue("digital", "bind11", "26:btn_trigger_right");
                    ctrlini.WriteValue("digital", "bind12", "27:btn_b");
                    ctrlini.WriteValue("digital", "bind13", "29:btn_a");
                    ctrlini.WriteValue("digital", "bind14", "34:btn_d");
                    ctrlini.WriteValue("digital", "bind15", "40:btn_start");
                    ctrlini.WriteValue("digital", "bind16", "41:btn_escape");
                    ctrlini.WriteValue("digital", "bind17", "42:reload");
                    ctrlini.WriteValue("digital", "bind18", "43:btn_menu");
                    ctrlini.WriteValue("digital", "bind19", "58:btn_quick_save");
                    ctrlini.WriteValue("digital", "bind2", "7:btn_dpad2_left");
                    ctrlini.WriteValue("digital", "bind20", "59:btn_jump_state");
                    ctrlini.WriteValue("digital", "bind21", "61:btn_fforward");
                    ctrlini.WriteValue("digital", "bind22", "62:btn_dpad2_up");
                    ctrlini.WriteValue("digital", "bind23", "63:btn_dpad2_down");
                    ctrlini.WriteValue("digital", "bind24", "66:btn_screenshot");
                    ctrlini.WriteValue("digital", "bind25", "79:btn_dpad1_right");
                    ctrlini.WriteValue("digital", "bind26", "80:btn_dpad1_left");
                    ctrlini.WriteValue("digital", "bind27", "81:btn_dpad1_down");
                    ctrlini.WriteValue("digital", "bind28", "82:btn_dpad1_up");
                    ctrlini.WriteValue("digital", "bind29", "90:axis2_down");
                    ctrlini.WriteValue("digital", "bind3", "9:btn_dpad2_right");
                    ctrlini.WriteValue("digital", "bind30", "92:axis2_left");
                    ctrlini.WriteValue("digital", "bind31", "94:axis2_right");
                    ctrlini.WriteValue("digital", "bind32", "96:axis2_up");
                    ctrlini.WriteValue("digital", "bind4", "12:btn_analog_up");
                    ctrlini.WriteValue("digital", "bind5", "13:btn_analog_left");
                    ctrlini.WriteValue("digital", "bind6", "14:btn_analog_down");
                    ctrlini.WriteValue("digital", "bind7", "15:btn_analog_right");
                    ctrlini.WriteValue("digital", "bind8", "20:btn_trigger_left");
                    ctrlini.WriteValue("digital", "bind9", "22:btn_z");
                }
                else
                {
                    ctrlini.WriteValue("digital", "bind0", "4:btn_x");
                    ctrlini.WriteValue("digital", "bind1", "12:btn_analog_up");
                    ctrlini.WriteValue("digital", "bind10", "40:btn_start");
                    ctrlini.WriteValue("digital", "bind11", "41:btn_escape");
                    ctrlini.WriteValue("digital", "bind12", "43:btn_menu");
                    ctrlini.WriteValue("digital", "bind13", "58:btn_quick_save");
                    ctrlini.WriteValue("digital", "bind14", "59:btn_jump_state");
                    ctrlini.WriteValue("digital", "bind15", "61:btn_fforward");
                    ctrlini.WriteValue("digital", "bind16", "66:btn_screenshot");
                    ctrlini.WriteValue("digital", "bind17", "79:btn_dpad1_right");
                    ctrlini.WriteValue("digital", "bind18", "80:btn_dpad1_left");
                    ctrlini.WriteValue("digital", "bind19", "81:btn_dpad1_down");
                    ctrlini.WriteValue("digital", "bind2", "13:btn_analog_left");
                    ctrlini.WriteValue("digital", "bind20", "82:btn_dpad1_up");
                    ctrlini.WriteValue("digital", "bind3", "14:btn_analog_down");
                    ctrlini.WriteValue("digital", "bind4", "15:btn_analog_right");
                    ctrlini.WriteValue("digital", "bind5", "20:btn_trigger_left");
                    ctrlini.WriteValue("digital", "bind6", "22:btn_y");
                    ctrlini.WriteValue("digital", "bind7", "26:btn_trigger_right");
                    ctrlini.WriteValue("digital", "bind8", "27:btn_b");
                    ctrlini.WriteValue("digital", "bind9", "29:btn_a");
                }

                ctrlini.WriteValue("emulator", "dead_zone", "10");
                ctrlini.WriteValue("emulator", "mapping_name", "Keyboard");
                ctrlini.WriteValue("emulator", "rumble_power", "100");
                ctrlini.WriteValue("emulator", "saturation", "100");
                ctrlini.WriteValue("emulator", "version", "3");

                ctrlini.Save();
            }
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, string mappingPath, string system, Dictionary<string, int> double_pads, int nsamepad)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            SimpleLogger.Instance.Info("[GAMEPAD] Configuring gamepad for player " + ctrl.PlayerIndex + " and system " + system);

            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;
            int playerIndex = ctrl.PlayerIndex;
            string deviceName = ctrl.SdlController != null ? ctrl.SdlController.Name : ctrl.Name;
            bool serviceMenu = SystemConfig.isOptSet("flycast_service_menu") && SystemConfig.getOptBoolean("flycast_service_menu");

            //Define tech (SDL or XInput)
            string tech = ctrl.IsXInputDevice ? "XInput" : "SDL";

            // Test if triggers are analog or digital
            bool analogTriggers = false;
            bool switchToDpad = SystemConfig.isOptSet("flycast_usedpad") && SystemConfig.getOptBoolean("flycast_usedpad");
            bool useR1L1 = SystemConfig.isOptSet("dreamcast_use_shoulders") && SystemConfig.getOptBoolean("dreamcast_use_shoulders");
            
            var r2test = joy[InputKey.r2];
            if (joy[InputKey.r2] != null)
                analogTriggers = r2test.Type == "axis";

            string mappingFile = Path.Combine(mappingPath, "SDL_" + deviceName + ".cfg");
            if (_isArcade)
                mappingFile = Path.Combine(mappingPath, "SDL_" + deviceName + "_arcade.cfg");

            if (SystemConfig.isOptSet("flycast_controller" + playerIndex) && !string.IsNullOrEmpty(SystemConfig["flycast_controller" + playerIndex]))
                ini.WriteValue("input", "device" + playerIndex, SystemConfig["flycast_controller" + playerIndex]);
            else
                ini.WriteValue("input", "device" + playerIndex, "0");

            ini.WriteValue("input", "device" + playerIndex + ".1", "1");

            if (SystemConfig.isOptSet("flycast_extension" + playerIndex) && !string.IsNullOrEmpty(SystemConfig["flycast_extension" + playerIndex]))
                ini.WriteValue("input", "device" + playerIndex + ".2", SystemConfig["flycast_extension" + playerIndex]);
            else
                ini.WriteValue("input", "device" + playerIndex + ".2", "10");

            SimpleLogger.Instance.Info("[INPUT] Assigning " + ctrl.Name + " with index " + index + " to player " + playerIndex);
            ini.WriteValue("input", "maple_sdl_joystick_" + index, (playerIndex - 1).ToString());

            SimpleLogger.Instance.Info("[GAMEPAD] Generating flycast mapping file : " + mappingFile + " number" + nsamepad);

            // Do not generate twice the same mapping file
            if (double_pads.ContainsKey(mappingFile))
                nsamepad = double_pads[mappingFile];
            else
                nsamepad = 0;

            double_pads[mappingFile] = nsamepad + 1;

            if (nsamepad > 0)
            {
                SimpleLogger.Instance.Info("[GAMEPAD] Mapping file " + mappingFile + " already generated for the same controller");
                return;
            }

            if (File.Exists(mappingFile))
                File.Delete(mappingFile);

            using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
            {
                ctrlini.ClearSection("analog");
                ctrlini.ClearSection("digital");
                ctrlini.ClearSection("emulator");

                List<string> analogBinds = new List<string>();
                List<string> digitalBinds = new List<string>();
                YmlContainer game = null;

                if (_isArcade)
                {
                    Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
                    string flycastMapping = null;

                    foreach (var path in mappingPaths)
                    {
                        string result = path
                            .Replace("{systempath}", "system")
                            .Replace("{userpath}", "inputmapping");

                        flycastMapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                        if (File.Exists(flycastMapping))
                            break;
                    }

                    if (File.Exists(flycastMapping))
                    {
                        YmlFile ymlFile = YmlFile.Load(flycastMapping);

                        game = ymlFile.Elements.Where(c => c.Name == _romName).FirstOrDefault() as YmlContainer;

                        if (game == null)
                            game = ymlFile.Elements.Where(g => _romName.StartsWith(g.Name)).FirstOrDefault() as YmlContainer;

                        if (game == null)
                            game = ymlFile.Elements.Where(g => g.Name == "default_" + system).FirstOrDefault() as YmlContainer;

                        if (game == null)
                            game = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;

                        if (game != null)
                        {
                            var gameName = game.Name;
                            var buttonMap = new Dictionary<string, string>();

                            foreach (var buttonEntry in game.Elements)
                            {
                                YmlElement button = buttonEntry as YmlElement;
                                if (button != null)
                                {
                                    buttonMap.Add(button.Name, button.Value);
                                }
                            }
                            gameMapping.Add(gameName, buttonMap);

                            if (buttonMap.Count > 0)
                            {
                                foreach (var button in buttonMap)
                                {
                                    switch (button.Key)
                                    {
                                        case "leftanalogleft":
                                        case "leftanalogright":
                                        case "leftanalogup":
                                        case "leftanalogdown":
                                            if (switchToDpad)
                                                digitalBinds.Add(GetInputKeyName(ctrl, switchToDpadKeys[button.Key], tech) + ":" + button.Value);
                                            else
                                                analogBinds.Add(GetInputKeyName(ctrl, yamlToInputKey[button.Key], tech) + ":" + button.Value);
                                            break;
                                        case "rightanalogleft":
                                        case "rightanalogright":
                                        case "rightanalogup":
                                        case "rightanalogdown":
                                            analogBinds.Add(GetInputKeyName(ctrl, yamlToInputKey[button.Key], tech) + ":" + button.Value);
                                            break;
                                        case "l2":
                                        case "r2":
                                            if (analogTriggers)
                                                analogBinds.Add(GetInputKeyName(ctrl, yamlToInputKey[button.Key], tech) + ":" + button.Value);
                                            else
                                                digitalBinds.Add(GetInputKeyName(ctrl, yamlToInputKey[button.Key], tech) + ":" + button.Value);
                                            break;
                                        case "south":
                                        case "north":
                                        case "east":
                                        case "west":
                                        case "l1":
                                        case "r1":
                                        case "l3":
                                        case "r3":
                                        case "select":
                                        case "start":
                                            digitalBinds.Add(GetInputKeyName(ctrl, yamlToInputKey[button.Key], tech) + ":" + button.Value);
                                            break;
                                        case "up":
                                        case "down":
                                        case "left":
                                        case "right":
                                            if (switchToDpad)
                                                break;
                                            else
                                                digitalBinds.Add(GetInputKeyName(ctrl, yamlToInputKey[button.Key], tech) + ":" + button.Value);
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    if (game == null)
                    {
                        if (!switchToDpad)
                        {
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogleft, tech) + ":btn_dpad1_left");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogright, tech) + ":btn_dpad1_right");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogup, tech) + ":btn_dpad1_up");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogdown, tech) + ":btn_dpad1_down");
                        }
                        else
                        {
                            digitalBinds.Add(GetInputKeyName(ctrl, InputKey.up, tech) + ":btn_dpad1_up");
                            digitalBinds.Add(GetInputKeyName(ctrl, InputKey.down, tech) + ":btn_dpad1_down");
                            digitalBinds.Add(GetInputKeyName(ctrl, InputKey.left, tech) + ":btn_dpad1_left");
                            digitalBinds.Add(GetInputKeyName(ctrl, InputKey.right, tech) + ":btn_dpad1_right");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogleft, tech) + ":btn_analog_left");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogright, tech) + ":btn_analog_right");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogup, tech) + ":btn_analog_up");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogdown, tech) + ":btn_analog_down");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogleft, tech) + ":axis2_left");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogright, tech) + ":axis2_right");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogup, tech) + ":axis2_up");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogdown, tech) + ":axis2_down");
                        }

                        if (analogTriggers)
                        {
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.l2, tech) + ":btn_trigger_left");
                            analogBinds.Add(GetInputKeyName(ctrl, InputKey.r2, tech) + ":btn_trigger_right");
                        }
                        else
                        {
                            digitalBinds.Add(GetInputKeyName(ctrl, InputKey.l2, tech) + ":btn_trigger_left");
                            digitalBinds.Add(GetInputKeyName(ctrl, InputKey.r2, tech) + ":btn_trigger_right");
                        }

                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.a, tech) + ":btn_a");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.b, tech) + ":btn_b");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.y, tech) + ":btn_c");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.x, tech) + ":btn_x");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pageup, tech) + ":btn_z");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pagedown, tech) + ":btn_y");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.start, tech) + ":btn_start");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.select, tech) + ":btn_d");                                          // coin
                    }

                    if (tech == "SDL")
                        digitalBinds.Add("5:btn_menu");
                    else
                        digitalBinds.Add("10:btn_menu");                                                                                // Guide button (emulator menu)

                    if (serviceMenu)
                    {
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.r3, tech) + ":btn_dpad2_down");               // service menu
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.l3, tech) + ":btn_dpad2_up");                 // test
                    }

                    for (int i = 0; i < analogBinds.Count; i++)
                        ctrlini.WriteValue("analog", "bind" + i, analogBinds[i]);

                    for (int i = 0; i < digitalBinds.Count; i++)
                        ctrlini.WriteValue("digital", "bind" + i, digitalBinds[i]);
                }
                
                else
                {
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogleft, tech) + ":btn_analog_left");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogright, tech) + ":btn_analog_right");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogup, tech) + ":btn_analog_up");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.leftanalogdown, tech) + ":btn_analog_down");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogleft, tech) + ":btn_dpad2_left");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogright, tech) + ":btn_dpad2_right");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogup, tech) + ":btn_dpad2_up");
                    analogBinds.Add(GetInputKeyName(ctrl, InputKey.rightanalogdown, tech) + ":btn_dpad2_down");

                    if (analogTriggers && !useR1L1)
                    {
                        analogBinds.Add(GetInputKeyName(ctrl, InputKey.l2, tech) + ":btn_trigger_left");
                        analogBinds.Add(GetInputKeyName(ctrl, InputKey.r2, tech) + ":btn_trigger_right");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pageup, tech) + ":btn_z");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pagedown, tech) + ":btn_c");
                    }
                    else if (analogTriggers && useR1L1)
                    {
                        analogBinds.Add(GetInputKeyName(ctrl, InputKey.l2, tech) + ":btn_z");
                        analogBinds.Add(GetInputKeyName(ctrl, InputKey.r2, tech) + ":btn_c");
                    }

                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.b, tech) + ":btn_b");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.a, tech) + ":btn_a");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.down, tech) + ":btn_dpad1_down");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.left, tech) + ":btn_dpad1_left");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.right, tech) + ":btn_dpad1_right");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.up, tech) + ":btn_dpad1_up");

                    if (!analogTriggers && !useR1L1)
                    {
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.l2, tech) + ":btn_trigger_left");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.r2, tech) + ":btn_trigger_right");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pageup, tech) + ":btn_z");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pagedown, tech) + ":btn_c");
                    }
                    else if (useR1L1)
                    {
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pageup, tech) + ":btn_trigger_left");
                        digitalBinds.Add(GetInputKeyName(ctrl, InputKey.pagedown, tech) + ":btn_trigger_right");
                    }

                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.x, tech) + ":btn_y");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.y, tech) + ":btn_x");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.l3, tech) + ":btn_menu");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.start, tech) + ":btn_start");
                    digitalBinds.Add(GetInputKeyName(ctrl, InputKey.r3, tech) + ":btn_d");
                    
                    for (int i = 0; i < analogBinds.Count; i++)
                        ctrlini.WriteValue("analog", "bind" + i, analogBinds[i]);

                    for (int i = 0; i < digitalBinds.Count; i++)
                        ctrlini.WriteValue("digital", "bind" + i, digitalBinds[i]);
                }

                BindIniFeatureSlider(ctrlini, "emulator", "dead_zone", "flycast_deadzone", "15");
                ctrlini.WriteValue("emulator", "mapping_name", deviceName);
                BindIniFeatureSlider(ctrlini, "emulator", "rumble_power", "flycast_rumble", "100");
                ctrlini.WriteValue("emulator", "version", "3");

                ctrlini.Save();
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private static string GetInputKeyName(Controller c, InputKey key, string tech)
        {
            Int64 pid = -1;

            // If controller is nintendo, A/B and X/Y are reversed
            //bool revertbuttons = (c.VendorID == VendorId.USB_VENDOR_NINTENDO);

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    return pid.ToString();
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                                return pid.ToString() + "+";
                            else
                                return pid.ToString() + "-";
                        case 4:
                        case 5:
                            return pid.ToString() + "+";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return tech == "XInput" ? "256" : pid.ToString();
                        case 2: return tech == "XInput" ? "259" : pid.ToString();
                        case 4: return tech == "XInput" ? "257" : pid.ToString();
                        case 8: return tech == "XInput" ? "258" : pid.ToString();
                    }
                }
            }
            return pid.ToString();
        }

        #region keyboard mapping
        static readonly Dictionary<SDL.SDL_Keycode, int> keycodeToHID = new Dictionary<SDL.SDL_Keycode, int>()
        {
            { SDL.SDL_Keycode.SDLK_a, 4 },
            { SDL.SDL_Keycode.SDLK_b, 5 },
            { SDL.SDL_Keycode.SDLK_c, 6 },
            { SDL.SDL_Keycode.SDLK_d, 7 },
            { SDL.SDL_Keycode.SDLK_e, 8 },
            { SDL.SDL_Keycode.SDLK_f, 9 },
            { SDL.SDL_Keycode.SDLK_g, 10 },
            { SDL.SDL_Keycode.SDLK_h, 11 },
            { SDL.SDL_Keycode.SDLK_i, 12 },
            { SDL.SDL_Keycode.SDLK_j, 13 },
            { SDL.SDL_Keycode.SDLK_k, 14 },
            { SDL.SDL_Keycode.SDLK_l, 15 },
            { SDL.SDL_Keycode.SDLK_m, 16 },
            { SDL.SDL_Keycode.SDLK_n, 17 },
            { SDL.SDL_Keycode.SDLK_o, 18 },
            { SDL.SDL_Keycode.SDLK_p, 19 },
            { SDL.SDL_Keycode.SDLK_q, 20 },
            { SDL.SDL_Keycode.SDLK_r, 21 },
            { SDL.SDL_Keycode.SDLK_s, 22 },
            { SDL.SDL_Keycode.SDLK_t, 23 },
            { SDL.SDL_Keycode.SDLK_u, 24 },
            { SDL.SDL_Keycode.SDLK_v, 25 },
            { SDL.SDL_Keycode.SDLK_w, 26 },
            { SDL.SDL_Keycode.SDLK_x, 27 },
            { SDL.SDL_Keycode.SDLK_y, 28 },
            { SDL.SDL_Keycode.SDLK_z, 29 },
            { SDL.SDL_Keycode.SDLK_1, 30 },
            { SDL.SDL_Keycode.SDLK_2, 31 },
            { SDL.SDL_Keycode.SDLK_3, 32 },
            { SDL.SDL_Keycode.SDLK_4, 33 },
            { SDL.SDL_Keycode.SDLK_5, 34 },
            { SDL.SDL_Keycode.SDLK_6, 35 },
            { SDL.SDL_Keycode.SDLK_7, 36 },
            { SDL.SDL_Keycode.SDLK_8, 37 },
            { SDL.SDL_Keycode.SDLK_9, 38 },
            { SDL.SDL_Keycode.SDLK_0, 39 },
            { SDL.SDL_Keycode.SDLK_RETURN, 40 },
            { SDL.SDL_Keycode.SDLK_ESCAPE, 41 },
            { SDL.SDL_Keycode.SDLK_BACKSPACE, 42 },
            { SDL.SDL_Keycode.SDLK_TAB, 43 },
            { SDL.SDL_Keycode.SDLK_SPACE, 44 },
            { SDL.SDL_Keycode.SDLK_MINUS, 45 },
            { SDL.SDL_Keycode.SDLK_EQUALS, 46 },
            { SDL.SDL_Keycode.SDLK_LEFTBRACKET, 47 },
            { SDL.SDL_Keycode.SDLK_RIGHTBRACKET, 48 },
            { SDL.SDL_Keycode.SDLK_BACKSLASH, 49 },
            { SDL.SDL_Keycode.SDLK_SEMICOLON, 51 },
            { SDL.SDL_Keycode.SDLK_QUOTE, 52 },
            { SDL.SDL_Keycode.SDLK_BACKQUOTE, 53 },
            { SDL.SDL_Keycode.SDLK_COMMA, 54 },
            { SDL.SDL_Keycode.SDLK_PERIOD, 55 },
            { SDL.SDL_Keycode.SDLK_SLASH, 56 },
            { SDL.SDL_Keycode.SDLK_CAPSLOCK, 57 },
            { SDL.SDL_Keycode.SDLK_F1, 58 },
            { SDL.SDL_Keycode.SDLK_F2, 59 },
            { SDL.SDL_Keycode.SDLK_F3, 60 },
            { SDL.SDL_Keycode.SDLK_F4, 61 },
            { SDL.SDL_Keycode.SDLK_F5, 62 },
            { SDL.SDL_Keycode.SDLK_F6, 63 },
            { SDL.SDL_Keycode.SDLK_F7, 64 },
            { SDL.SDL_Keycode.SDLK_F8, 65 },
            { SDL.SDL_Keycode.SDLK_F9, 66 },
            { SDL.SDL_Keycode.SDLK_F10, 67 },
            { SDL.SDL_Keycode.SDLK_F11, 68 },
            { SDL.SDL_Keycode.SDLK_F12, 69 },
            { SDL.SDL_Keycode.SDLK_PRINTSCREEN, 70 },
            { SDL.SDL_Keycode.SDLK_SCROLLLOCK, 71 },
            { SDL.SDL_Keycode.SDLK_PAUSE, 72 },
            { SDL.SDL_Keycode.SDLK_INSERT, 73 },
            { SDL.SDL_Keycode.SDLK_HOME, 74 },
            { SDL.SDL_Keycode.SDLK_PAGEUP, 75 },
            { SDL.SDL_Keycode.SDLK_DELETE, 76 },
            { SDL.SDL_Keycode.SDLK_END, 77 },
            { SDL.SDL_Keycode.SDLK_PAGEDOWN, 78 },
            { SDL.SDL_Keycode.SDLK_RIGHT, 79 },
            { SDL.SDL_Keycode.SDLK_LEFT, 80 },
            { SDL.SDL_Keycode.SDLK_DOWN, 81 },
            { SDL.SDL_Keycode.SDLK_UP, 82 },
            { SDL.SDL_Keycode.SDLK_NUMLOCKCLEAR, 83 },
            { SDL.SDL_Keycode.SDLK_KP_DIVIDE, 84 },
            { SDL.SDL_Keycode.SDLK_KP_MULTIPLY, 85 },
            { SDL.SDL_Keycode.SDLK_KP_MINUS, 86 },
            { SDL.SDL_Keycode.SDLK_KP_PLUS, 87 },
            { SDL.SDL_Keycode.SDLK_KP_ENTER, 88 },
            { SDL.SDL_Keycode.SDLK_KP_1, 89 },
            { SDL.SDL_Keycode.SDLK_KP_2, 90 },
            { SDL.SDL_Keycode.SDLK_KP_3, 91 },
            { SDL.SDL_Keycode.SDLK_KP_4, 92 },
            { SDL.SDL_Keycode.SDLK_KP_5, 93 },
            { SDL.SDL_Keycode.SDLK_KP_6, 94 },
            { SDL.SDL_Keycode.SDLK_KP_7, 95 },
            { SDL.SDL_Keycode.SDLK_KP_8, 96 },
            { SDL.SDL_Keycode.SDLK_KP_9, 97 },
            { SDL.SDL_Keycode.SDLK_KP_0, 98 },
            { SDL.SDL_Keycode.SDLK_KP_PERIOD, 99 },
            { SDL.SDL_Keycode.SDLK_APPLICATION, 101 },
            { SDL.SDL_Keycode.SDLK_POWER, 102 },
            { SDL.SDL_Keycode.SDLK_KP_EQUALS, 103 },
            { SDL.SDL_Keycode.SDLK_F13, 104 },
            { SDL.SDL_Keycode.SDLK_F14, 105 },
            { SDL.SDL_Keycode.SDLK_F15, 106 },
            { SDL.SDL_Keycode.SDLK_F16, 107 },
            { SDL.SDL_Keycode.SDLK_F17, 108 },
            { SDL.SDL_Keycode.SDLK_F18, 109 },
            { SDL.SDL_Keycode.SDLK_F19, 110 },
            { SDL.SDL_Keycode.SDLK_F20, 111 },
            { SDL.SDL_Keycode.SDLK_F21, 112 },
            { SDL.SDL_Keycode.SDLK_F22, 113 },
            { SDL.SDL_Keycode.SDLK_F23, 114 },
            { SDL.SDL_Keycode.SDLK_F24, 115 },
            { SDL.SDL_Keycode.SDLK_EXECUTE, 116 },
            { SDL.SDL_Keycode.SDLK_HELP, 117 },
            { SDL.SDL_Keycode.SDLK_MENU, 118 },
            { SDL.SDL_Keycode.SDLK_SELECT, 119 },
            { SDL.SDL_Keycode.SDLK_STOP, 120 },
            { SDL.SDL_Keycode.SDLK_AGAIN, 121 },
            { SDL.SDL_Keycode.SDLK_UNDO, 122 },
            { SDL.SDL_Keycode.SDLK_CUT, 123 },
            { SDL.SDL_Keycode.SDLK_COPY, 124 },
            { SDL.SDL_Keycode.SDLK_PASTE, 125 },
            { SDL.SDL_Keycode.SDLK_FIND, 126 },
            { SDL.SDL_Keycode.SDLK_MUTE, 127 },
            { SDL.SDL_Keycode.SDLK_VOLUMEUP, 128 },
            { SDL.SDL_Keycode.SDLK_VOLUMEDOWN, 129 },
            { SDL.SDL_Keycode.SDLK_KP_COMMA, 133 },
            { SDL.SDL_Keycode.SDLK_KP_EQUALSAS400, 134 },
            { SDL.SDL_Keycode.SDLK_ALTERASE, 153 },
            { SDL.SDL_Keycode.SDLK_SYSREQ, 154 },
            { SDL.SDL_Keycode.SDLK_CANCEL, 155 },
            { SDL.SDL_Keycode.SDLK_CLEAR, 156 },
            { SDL.SDL_Keycode.SDLK_PRIOR, 157 },
            { SDL.SDL_Keycode.SDLK_RETURN2, 158 },
            { SDL.SDL_Keycode.SDLK_SEPARATOR, 159 },
            { SDL.SDL_Keycode.SDLK_OUT, 160 },
            { SDL.SDL_Keycode.SDLK_OPER, 161 },
            { SDL.SDL_Keycode.SDLK_CLEARAGAIN, 162 },
            { SDL.SDL_Keycode.SDLK_CRSEL, 163 },
            { SDL.SDL_Keycode.SDLK_EXSEL, 164 },
            { SDL.SDL_Keycode.SDLK_KP_00, 176 },
            { SDL.SDL_Keycode.SDLK_KP_000, 177 },
            { SDL.SDL_Keycode.SDLK_THOUSANDSSEPARATOR, 178 },
            { SDL.SDL_Keycode.SDLK_DECIMALSEPARATOR, 179 },
            { SDL.SDL_Keycode.SDLK_CURRENCYUNIT, 180 },
            { SDL.SDL_Keycode.SDLK_CURRENCYSUBUNIT, 181 },
            { SDL.SDL_Keycode.SDLK_KP_LEFTPAREN, 182 },
            { SDL.SDL_Keycode.SDLK_KP_RIGHTPAREN, 183 },
            { SDL.SDL_Keycode.SDLK_KP_LEFTBRACE, 184 },
            { SDL.SDL_Keycode.SDLK_KP_RIGHTBRACE, 185 },
            { SDL.SDL_Keycode.SDLK_KP_TAB, 186 },
            { SDL.SDL_Keycode.SDLK_KP_BACKSPACE, 187 },
            { SDL.SDL_Keycode.SDLK_KP_A, 188 },
            { SDL.SDL_Keycode.SDLK_KP_B, 189 },
            { SDL.SDL_Keycode.SDLK_KP_C, 190 },
            { SDL.SDL_Keycode.SDLK_KP_D, 191 },
            { SDL.SDL_Keycode.SDLK_KP_E, 192 },
            { SDL.SDL_Keycode.SDLK_KP_F, 193 },
            { SDL.SDL_Keycode.SDLK_KP_XOR, 194 },
            { SDL.SDL_Keycode.SDLK_KP_POWER, 195 },
            { SDL.SDL_Keycode.SDLK_KP_PERCENT, 196 },
            { SDL.SDL_Keycode.SDLK_KP_LESS, 197 },
            { SDL.SDL_Keycode.SDLK_KP_GREATER, 198 },
            { SDL.SDL_Keycode.SDLK_KP_AMPERSAND, 199 },
            { SDL.SDL_Keycode.SDLK_KP_DBLAMPERSAND, 200 },
            { SDL.SDL_Keycode.SDLK_KP_VERTICALBAR, 201 },
            { SDL.SDL_Keycode.SDLK_KP_DBLVERTICALBAR, 202 },
            { SDL.SDL_Keycode.SDLK_KP_COLON, 203 },
            { SDL.SDL_Keycode.SDLK_KP_HASH, 204 },
            { SDL.SDL_Keycode.SDLK_KP_SPACE, 205 },
            { SDL.SDL_Keycode.SDLK_KP_AT, 206 },
            { SDL.SDL_Keycode.SDLK_KP_EXCLAM, 207 },
            { SDL.SDL_Keycode.SDLK_KP_MEMSTORE, 208 },
            { SDL.SDL_Keycode.SDLK_KP_MEMRECALL, 209 },
            { SDL.SDL_Keycode.SDLK_KP_MEMCLEAR, 210 },
            { SDL.SDL_Keycode.SDLK_KP_MEMADD, 211 },
            { SDL.SDL_Keycode.SDLK_KP_MEMSUBTRACT, 212 },
            { SDL.SDL_Keycode.SDLK_KP_MEMMULTIPLY, 213 },
            { SDL.SDL_Keycode.SDLK_KP_MEMDIVIDE, 214 },
            { SDL.SDL_Keycode.SDLK_KP_PLUSMINUS, 215 },
            { SDL.SDL_Keycode.SDLK_KP_CLEAR, 216 },
            { SDL.SDL_Keycode.SDLK_KP_CLEARENTRY, 217 },
            { SDL.SDL_Keycode.SDLK_KP_BINARY, 218 },
            { SDL.SDL_Keycode.SDLK_KP_OCTAL, 219 },
            { SDL.SDL_Keycode.SDLK_KP_DECIMAL, 220 },
            { SDL.SDL_Keycode.SDLK_KP_HEXADECIMAL, 221 },
            { SDL.SDL_Keycode.SDLK_LCTRL, 224 },
            { SDL.SDL_Keycode.SDLK_LSHIFT, 225 },
            { SDL.SDL_Keycode.SDLK_LALT, 226 },
            { SDL.SDL_Keycode.SDLK_LGUI, 227 },
            { SDL.SDL_Keycode.SDLK_RCTRL, 228 },
            { SDL.SDL_Keycode.SDLK_RSHIFT, 229 },
            { SDL.SDL_Keycode.SDLK_RALT, 230 },
            { SDL.SDL_Keycode.SDLK_RGUI, 231 },
            { SDL.SDL_Keycode.SDLK_MODE, 257 },
            { SDL.SDL_Keycode.SDLK_AUDIONEXT, 258 },
            { SDL.SDL_Keycode.SDLK_AUDIOPREV, 259 },
            { SDL.SDL_Keycode.SDLK_AUDIOSTOP, 260 },
            { SDL.SDL_Keycode.SDLK_AUDIOPLAY, 261 },
            { SDL.SDL_Keycode.SDLK_AUDIOMUTE, 262 },
            { SDL.SDL_Keycode.SDLK_MEDIASELECT, 263 },
            { SDL.SDL_Keycode.SDLK_WWW, 264 },
            { SDL.SDL_Keycode.SDLK_MAIL, 265 },
            { SDL.SDL_Keycode.SDLK_CALCULATOR, 266 },
            { SDL.SDL_Keycode.SDLK_COMPUTER, 267 },
            { SDL.SDL_Keycode.SDLK_AC_SEARCH, 268 },
            { SDL.SDL_Keycode.SDLK_AC_HOME, 269 },
            { SDL.SDL_Keycode.SDLK_AC_BACK, 270 },
            { SDL.SDL_Keycode.SDLK_AC_FORWARD, 271 },
            { SDL.SDL_Keycode.SDLK_AC_STOP, 272 },
            { SDL.SDL_Keycode.SDLK_AC_REFRESH, 273 },
            { SDL.SDL_Keycode.SDLK_AC_BOOKMARKS, 274 },
            { SDL.SDL_Keycode.SDLK_BRIGHTNESSDOWN, 275 },
            { SDL.SDL_Keycode.SDLK_BRIGHTNESSUP, 276 },
            { SDL.SDL_Keycode.SDLK_DISPLAYSWITCH, 277 },
            { SDL.SDL_Keycode.SDLK_KBDILLUMTOGGLE, 278 },
            { SDL.SDL_Keycode.SDLK_KBDILLUMDOWN, 279 },
            { SDL.SDL_Keycode.SDLK_KBDILLUMUP, 280 },
            { SDL.SDL_Keycode.SDLK_EJECT, 281 },
            { SDL.SDL_Keycode.SDLK_SLEEP, 282 }
        };

        static readonly Dictionary<SDL.SDL_Keycode, SDL.SDL_Keycode> azertyLayoutMapping = new Dictionary<SDL.SDL_Keycode, SDL.SDL_Keycode>()
        {
            { SDL.SDL_Keycode.SDLK_a, SDL.SDL_Keycode.SDLK_q },
            { SDL.SDL_Keycode.SDLK_q, SDL.SDL_Keycode.SDLK_a },
            { SDL.SDL_Keycode.SDLK_z, SDL.SDL_Keycode.SDLK_w },
            { SDL.SDL_Keycode.SDLK_w, SDL.SDL_Keycode.SDLK_z },
            { SDL.SDL_Keycode.SDLK_m, SDL.SDL_Keycode.SDLK_COMMA },
            { SDL.SDL_Keycode.SDLK_SEMICOLON, SDL.SDL_Keycode.SDLK_m },
            { SDL.SDL_Keycode.SDLK_COMMA, SDL.SDL_Keycode.SDLK_SEMICOLON },
            { SDL.SDL_Keycode.SDLK_PERIOD, SDL.SDL_Keycode.SDLK_KP_COLON },
            { SDL.SDL_Keycode.SDLK_SLASH, SDL.SDL_Keycode.SDLK_EXCLAIM },
        };
        #endregion

        #region controllerinformation
        /*static readonly Dictionary<string, int> deviceType = new Dictionary<string, int>()
        {
            { "controller", 0 },
            { "lightgun", 7 },
            { "keyboard", 5 },
            { "mouse", 6 },
            { "ascii_stick", 4 },
            { "twinstick", 8 },
            { "none", 10 }
        };*/

        /*static readonly Dictionary<string, int> extensionType = new Dictionary<string, int>()
        {
            { "vmu", 1 },
            { "purupuru", 3 },
            { "microphone", 2 },
            { "none", 10 }
        };*/
        #endregion

        static readonly Dictionary<string, InputKey> yamlToInputKey = new Dictionary<string, InputKey>()
        {
            { "leftanalogleft", InputKey.leftanalogleft },
            { "leftanalogright", InputKey.leftanalogright },
            { "leftanalogup", InputKey.leftanalogup },
            { "leftanalogdown", InputKey.leftanalogdown },
            { "rightanalogleft", InputKey.rightanalogleft },
            { "rightanalogright", InputKey.rightanalogright },
            { "rightanalogup", InputKey.rightanalogup },
            { "rightanalogdown", InputKey.rightanalogdown },
            { "south", InputKey.a },
            { "east", InputKey.b },
            { "north", InputKey.x },
            { "west", InputKey.y },
            { "select", InputKey.select },
            { "start", InputKey.start },
            { "l1", InputKey.pageup },
            { "r1", InputKey.pagedown },
            { "l2", InputKey.l2 },
            { "r2", InputKey.r2 },
            { "l3", InputKey.l3 },
            { "r3", InputKey.r3 },
            { "up", InputKey.up },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "right", InputKey.right },
        };

        static readonly Dictionary<string, InputKey> switchToDpadKeys = new Dictionary<string, InputKey>()
        {
            { "leftanalogleft", InputKey.left },
            { "leftanalogright", InputKey.right },
            { "leftanalogup", InputKey.up },
            { "leftanalogdown", InputKey.down },
        };

        static string[] mappingPaths =
        {            
            // User specific
            "{userpath}\\flycast_Arcade.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\flycast_Arcade.yml",
        };
    }
}
