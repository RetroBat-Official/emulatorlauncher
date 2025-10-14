﻿using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EmulatorLauncher.Libretro
{
    partial class LibretroControllers
    {
        private static bool _specialController = false;
        private static bool _specialControllerHotkey = false;
        private static bool _noHotkey = false;
        private static bool _singleButtonShortcuts = false;
        private static string _inputDriver = "sdl2";
        private static readonly HashSet<string> disabledAnalogModeSystems = new HashSet<string> { "n64", "dreamcast", "gamecube", "3ds" };
        static readonly List<string> mdSystems = new List<string>() { "megadrive", "genesis", "megadrive-msu", "genesis-msu", "segacd", "megacd", "sega32x", "mega32x" };
        static readonly List<string> systemButtonInvert = new List<string>() { "snes", "snes-msu", "sattelaview", "sufami", "sfc" };
        static readonly List<string> coreNoRemap = new List<string>() { "mednafen_snes" };
        static readonly List<string> arcadeSystems = new List<string>() { "arcade", "mame", "hbmame", "fbneo", "cave", "cps1", "cps2", "cps3", "atomiswave", "naomi", "naomi2", "gaelco", "segastv", "neogeo64" };
        private static Dictionary<int, int> _indexes = new Dictionary<int, int>();

        public static bool WriteControllersConfig(ConfigFile retroconfig, string system, string core)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return false;
            }

            _indexes.Clear();

            if (Program.SystemConfig.isOptSet("input_driver") && Program.SystemConfig["input_driver"] == "xinput")
                _inputDriver = "xinput";

            if (Program.SystemConfig.isOptSet("input_driver") && Program.SystemConfig["input_driver"] == "dinput")
                _inputDriver = "dinput";

            // no menu in non full uimode
            if (Program.SystemConfig.isOptSet("uimode") && Program.SystemConfig["uimode"] != "Full")
            {
                if (retroarchspecialsALT.ContainsKey(InputKey.x))
                    retroarchspecialsALT.Remove(InputKey.x);
                if (retroarchspecials.ContainsKey(InputKey.a))
                    retroarchspecials.Remove(InputKey.a);
            }

            CleanControllerConfig(retroconfig);

            int controllerNb = Program.Controllers.Count;
            if (controllerNb < 4)
                retroconfig["input_max_users"] = "4";
            else
                retroconfig["input_max_users"] = (controllerNb + 1).ToString();

            if (Program.SystemConfig.getOptBoolean("revertXIndex"))
                Controller.SetXinputReversedIndex(Program.Controllers);

            foreach (var controller in Program.Controllers)
            {
                WriteControllerConfig(retroconfig, controller, system, core);
            }

            // Check for duplicate indexes and log it (and try and fix it)
            try
            {
                var duplicatesWithKeys = _indexes
                .GroupBy(kv => kv.Value)
                .Where(g => g.Count() > 1)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(kv => kv.Key).ToList()
                );

                if (duplicatesWithKeys.Count > 0)
                {
                    foreach (var pair in duplicatesWithKeys)
                        SimpleLogger.Instance.Warning($"[WARNING] Value {pair.Key} is duplicated for keys: {string.Join(", ", pair.Value)}");

                    // Replace sdlcontroller index with deviceindex, we don't understand why sdl is not null and returns same index 0 for all controllers !
                    if (_inputDriver == "sdl2")
                    {
                        SimpleLogger.Instance.Info("[INFO] Trying to fix duplicate index.");

                        foreach (var controller in Program.Controllers)
                        {
                            int index = controller.DeviceIndex;
                            retroconfig[string.Format("input_player{0}_joypad_index", controller.PlayerIndex)] = index.ToString();
                        }
                    }
                }
            }
            catch { SimpleLogger.Instance.Warning("[WARNING] Failed duplicate index check"); }

            WriteKBHotKeyConfig(retroconfig, core);
            if (_specialController && _specialControllerHotkey)
                return true;

            WriteHotKeyConfig(retroconfig);

            return true;
        }
        
        static public List<InputKey> retroarchdirs = new List<InputKey>() { InputKey.up, InputKey.down, InputKey.left, InputKey.right };

        static public Dictionary<InputKey, string> retroarchjoysticks = new Dictionary<InputKey, string>()
        {
            { InputKey.joystick1up, "l_y"}, 
            { InputKey.joystick1left, "l_x"}, 
            { InputKey.joystick2up, "r_y"}, 
            { InputKey.joystick2left, "r_x"}
        };

        static public Dictionary<string, string> typetoname = new Dictionary<string, string>()
        {
            { "button", "btn"}, 
            { "hat", "btn"}, 
            { "axis", "axis"}, 
            { "key", "key"}
        };

        static public Dictionary<long, string> hatstoname = new Dictionary<long, string>()
        {
            { 1, "up"}, 
            { 2, "right"}, 
            { 3, "down"}, 
            { 4, "left"}
        };

        static public Dictionary<InputKey, string> retroarchspecials = new Dictionary<InputKey, string>()
        {
            { InputKey.start, "exit_emulator"},
            { InputKey.b, "pause_toggle"},
            { InputKey.a, "menu_toggle"},
            { InputKey.x, "load_state"},
            { InputKey.y, "save_state"},
            { InputKey.pageup, "disk_eject_toggle"},
            { InputKey.pagedown, "ai_service"},
            { InputKey.l2, "disk_prev"},
            { InputKey.r2, "disk_next"},
            { InputKey.r3, "screenshot"},
            { InputKey.up, "state_slot_increase"},
            { InputKey.down, "state_slot_decrease"},
            { InputKey.left, "rewind"},
            { InputKey.right, "hold_fast_forward"}
        };

        static public Dictionary<InputKey, string> retroarchspecialsALT = new Dictionary<InputKey, string>()
        {
            { InputKey.start, "exit_emulator"},
            { InputKey.b, "pause_toggle"},
            { InputKey.a, "state_slot_decrease"},
            { InputKey.x, "menu_toggle"},
            { InputKey.y, "state_slot_increase"},
            { InputKey.pageup, "load_state"},
            { InputKey.pagedown, "save_state"},
            { InputKey.l2, "rewind"},
            { InputKey.r2, "hold_fast_forward"},
            { InputKey.r3, "screenshot"},
            { InputKey.up, "ai_service"},
            { InputKey.down, "disk_eject_toggle"},
            { InputKey.left, "disk_prev"},
            { InputKey.right, "disk_next"}
        };

        static public Dictionary<string, InputKey> turbobuttons = new Dictionary<string, InputKey>()
        {
            { "L1", InputKey.pageup},
            { "R1", InputKey.pagedown},
            { "L2", InputKey.l2},
            { "R2", InputKey.r2}
        };

        private static void CleanControllerConfig(ConfigFile retroconfig)
        {
            retroconfig.DisableAll("input_player");

            for(int i = 1 ; i <= 5 ; i++)
                retroconfig[string.Format("input_player{0}_joypad_index", i)] = (i - 1).ToString();

            foreach (var specialkey in retroarchspecials)
                retroconfig.DisableAll("input_" + specialkey.Value);

            retroconfig.DisableAll("input_toggle_fast_forward");
        }

        private static void WriteKBHotKeyConfig(ConfigFile config, string core)
        {
            // Keyboard defaults
            config["input_enable_hotkey"] = "nul";

#if DEBUG
            config["input_exit_emulator"] = "tilde";
#else
            config["input_exit_emulator"] = "escape";
#endif            
            // Overwrite hotkeys with a file
            string kbHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "retroarch_kb_hotkeys.yml");

            if (!File.Exists(kbHotkeyFile))
                kbHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "retroarch_kb_hotkeys.yml");

            if (File.Exists(kbHotkeyFile))
            {
                YmlFile ymlFile = YmlFile.Load(kbHotkeyFile);

                if (ymlFile != null)
                {
                    YmlContainer kbHotkeyList = ymlFile.Elements.Where(c => c.Name == core).FirstOrDefault() as YmlContainer;

                    if (kbHotkeyList == null)
                        kbHotkeyList = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

                    if (kbHotkeyList != null)
                    {
                        SimpleLogger.Instance.Info("[GENERATOR] Overwriting keyboard hotkeys with values from : " + kbHotkeyFile);

                        var kbHotkeys = kbHotkeyList.Elements;

                        if (kbHotkeys != null & kbHotkeys.Count > 0)
                        {
                            foreach (var kbHotkey in kbHotkeys)
                            {
                                YmlElement hotkey = kbHotkey as YmlElement;

                                if (hotkey != null && hotkey.Name.StartsWith("input_") && !hotkey.Name.EndsWith("_btn") && !hotkey.Name.EndsWith("_mbtn") && !hotkey.Name.EndsWith("_axis"))
                                    config[hotkey.Name] = hotkey.Value;
                            }
                            return;
                        }
                    }
                }
            }

            // If no file use RetroBat standard
            config["input_menu_toggle"] = "f1";
            config["input_save_state"] = "f2";
            config["input_load_state"] = "f4";
            config["input_desktop_menu_toggle"] = "f5";
            config["input_state_slot_decrease"] = "f6";
            config["input_state_slot_increase"] = "f7";
            config["input_screenshot"] = "f8";
            config["input_rewind"] = "backspace";
            config["input_hold_fast_forward"] = "l";
            config["input_shader_next"] = "m";
            config["input_shader_prev"] = "n";
            config["input_bind_hold"] = "2";
            config["input_bind_timeout"] = "5";
        }
        private static void WriteHotKeyConfig(ConfigFile config)
        {
            var c0 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            if (c0 == null || c0.Config == null)
                return;

            if (Program.SystemConfig.getOptBoolean("fastforward_toggle"))
            {
                retroarchspecialsALT[InputKey.r2] = "toggle_fast_forward";
                retroarchspecials[InputKey.right] = "toggle_fast_forward";
            }

            if (Misc.HasWiimoteGun())
            {
                var keyB = Program.Controllers.FirstOrDefault(c => c.Name == "Keyboard");
                if (keyB != null && keyB.Config != null)
                {
                    foreach (var specialkey in retroarchspecials)
                    {
                        var input = GetInputCode(keyB, specialkey.Key);
                        if (input != null && input.Type == "key")
                            config[string.Format("input_{0}", specialkey.Value)] = GetConfigValue(input);
                    }

                    var wiiMoteHotKey = GetInputCode(keyB, InputKey.hotkey) ?? GetInputCode(keyB, InputKey.select);
                    if (wiiMoteHotKey != null && wiiMoteHotKey.Type == "key")
                        config["input_enable_hotkey"] = GetConfigValue(wiiMoteHotKey);
                }
            }

            var hotKey = GetInputCode(c0, InputKey.hotkey);
            if (hotKey != null && hotKey.Type != "key" && !_singleButtonShortcuts)                    
                config[string.Format("input_enable_hotkey_{0}", typetoname[hotKey.Type])] = GetConfigValue(hotKey);
            if (_singleButtonShortcuts)
            {
                config["input_enable_hotkey"] = config["input_enable_hotkey_axis"] = config["input_enable_hotkey_btn"] = config["input_enable_hotkey_mbtn"] = "nul";
            }
        }

        private static string GetAnalogMode(Controller controller, string system)
        {
            if (Program.SystemConfig.isOptSet("analogToDpad") && !string.IsNullOrEmpty(Program.SystemConfig["analogToDpad"]))
                return Program.SystemConfig["analogToDpad"];

            if (disabledAnalogModeSystems.Contains(system))
                return "0";
           
            foreach (var dirkey in retroarchdirs)
            {
                var k = GetInputCode(controller, dirkey);
                if (k != null && (k.Type == "button" || k.Type == "hat"))
                    return "1";
            }
            
            return "0";
        }

        private static Dictionary<string, string> GenerateControllerConfig(ConfigFile retroconfig, Controller controller, string system, string core)
        {
            Dictionary<InputKey, string> retroarchbtns = new Dictionary<InputKey, string>()
            {
                { InputKey.b, "a" },
                { InputKey.a, "b" }, // A and B reverted for RetroBat
                { InputKey.x, "x" }, 
                { InputKey.y, "y" },
                { InputKey.pageup, "l" },
                { InputKey.pagedown, "r"}, 
                { InputKey.l2, "l2"},
                { InputKey.r2, "r2"},
                { InputKey.l3, "l3"}, 
                { InputKey.r3, "r3"},
                { InputKey.start, "start"}, 
                { InputKey.select, "select"}
            };

            // some input adaptations for some cores...
            // MAME service menu
            if (system == "mame")
            {
                // Invert Dip switches and set it on r3 instead ( less annoying )
                retroarchbtns[InputKey.l3] = "r3";
                retroarchbtns[InputKey.r3] = "l3";
            }

            if (performSpecialMapping(out Dictionary<string, string> specialConfig, system, controller, retroconfig))
                return specialConfig;

            // N64: Z is important, in case L2 (z) is not available for this pad, use L1
            if (system == "n64")
            {
                if (controller.Config != null && controller.Config.Input != null && !controller.Config.Input.Any(i => i.Name == InputKey.r2))
                {
                    retroarchbtns[InputKey.pageup] = "l2";
                    retroarchbtns[InputKey.l2] = "l";
                }
            }

            // Reverse buttons clockwise option for super nintendo libretro cores
            if (systemButtonInvert.Contains(system) && Program.Features.IsSupported("buttonsInvert") && Program.SystemConfig.getOptBoolean("buttonsInvert") && coreNoRemap.Contains(core))
            {
                retroarchbtns[InputKey.a] = "a";
                retroarchbtns[InputKey.b] = "b";
                retroarchbtns[InputKey.x] = "y";
                retroarchbtns[InputKey.y] = "x";
            }

            var config = new Dictionary<string, string>();
            var conflicts = new List<string>();

            foreach (var btnkey in retroarchbtns)
            {
                var input = GetInputCode(controller, btnkey.Key);
                if (input == null)
                    continue;

                if (input.Type == "key")
                {
                    // For arcade systems, when using keyboard, set 1 and 5.... as select and start
                    if (arcadeSystems.Contains(system) && controller.IsKeyboard && controller.PlayerIndex < 5 && (btnkey.Value == "start" || btnkey.Value == "select"))
                    {
                        int index = controller.PlayerIndex;
                        string value = btnkey.Value;
                        switch (value)
                        {
                            case "select":
                                int selectNumValue = index + 4;
                                value = "num" + selectNumValue.ToString();
                                break;
                            case "start":
                                int startNumValue = index;
                                value = "num" + startNumValue.ToString();
                                break;
                        }

                        config[string.Format("input_player{0}_{1}", controller.PlayerIndex, btnkey.Value)] = value;
                        conflicts.AddRange(retroconfig.Where(i => i.Value == value).Select(i => i.Name));
                    }

                    else
                    {
                        string value = GetConfigValue(input);
                        config[string.Format("input_player{0}_{1}", controller.PlayerIndex, btnkey.Value)] = value;
                        conflicts.AddRange(retroconfig.Where(i => i.Value == value).Select(i => i.Name));
                    }
                }
                else
                    config[string.Format("input_player{0}_{1}_{2}", controller.PlayerIndex, btnkey.Value, typetoname[input.Type])] = GetConfigValue(input);
            }

            foreach (var btnkey in retroarchdirs)
            {
                var input = GetInputCode(controller, btnkey);
                if (input == null)
                    continue;

                if (input.Type == "key")
                    config[string.Format("input_player{0}_{1}", controller.PlayerIndex, btnkey)] = GetConfigValue(input);
                else
                    config[string.Format("input_player{0}_{1}_{2}", controller.PlayerIndex, btnkey, typetoname[input.Type])] = GetConfigValue(input);
            }

            foreach (var btnkey in retroarchjoysticks)
            {
                var input = GetInputCode(controller, btnkey.Key);
                if (input == null)
                    continue;

                if (input.Value < 0)
                {
                    config[string.Format("input_player{0}_{1}_minus_axis", controller.PlayerIndex, btnkey.Value)] = "-" + input.Id.ToString();
                    config[string.Format("input_player{0}_{1}_plus_axis", controller.PlayerIndex, btnkey.Value)] = "+" + input.Id.ToString();
                }
                else
                {
                    config[string.Format("input_player{0}_{1}_minus_axis", controller.PlayerIndex, btnkey.Value)] = "+" + input.Id.ToString();
                    config[string.Format("input_player{0}_{1}_plus_axis", controller.PlayerIndex, btnkey.Value)] = "-" + input.Id.ToString();
                }
            }

            var hotKey = GetInputCode(controller, InputKey.hotkey);
            if (hotKey == null)
            {
                SimpleLogger.Instance.Info("[GENERATOR] No hotkey configured, all retroarch shortcuts will be disabled.");
                _noHotkey = true;
            }
            
            if (controller.PlayerIndex == 1 && !_noHotkey)
            {
                if (Program.SystemConfig.getOptBoolean("fastforward_toggle"))
                {
                    retroarchspecials[InputKey.right] = "toggle_fast_forward";
                    retroarchspecialsALT[InputKey.r2] = "toggle_fast_forward";
                }

                var hotkeyList = retroarchspecials;
                if (Program.SystemConfig.getOptBoolean("alt_hotkeys"))
                    hotkeyList = retroarchspecialsALT;

                // override shortcuts from file
                string cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "retroarch_controller_hotkeys.yml");

                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "retroarch_controller_hotkeys.yml");

                if (File.Exists(cHotkeyFile))
                {
                    YmlFile ymlFile = YmlFile.Load(cHotkeyFile);
                    if (ymlFile != null)
                    {
                        YmlContainer cHotkeyList = ymlFile.Elements.Where(c => c.Name == system).FirstOrDefault() as YmlContainer;

                        if (cHotkeyList == null)
                            cHotkeyList = ymlFile.Elements.Where(c => c.Name == core).FirstOrDefault() as YmlContainer;

                        if (cHotkeyList == null)
                            cHotkeyList = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

                        if (cHotkeyList != null)
                        {
                            SimpleLogger.Instance.Info("[GENERATOR] Overwriting controller hotkeys with values from : " + cHotkeyFile);

                            var cHotkeys = cHotkeyList.Elements;

                            if (cHotkeys != null & cHotkeys.Count > 0)
                            {
                                hotkeyList.Clear();
                                foreach (var cHotkey in cHotkeys)
                                {
                                    YmlElement hotkey = cHotkey as YmlElement;
                                    string value = hotkey.Value;

                                    if (hotkey.Name == "noHotkey" && value == "true")
                                    {
                                        _singleButtonShortcuts = true;
                                        continue;
                                    }

                                    if (Program.SystemConfig.getOptBoolean("fastforward_toggle") && hotkey.Value == "hold_fast_forward")
                                        value = "toggle_fast_forward";
                                    
                                    if (Enum.TryParse(hotkey.Name, true, out InputKey key) && value != null)
                                        hotkeyList[key] = value;
                                }
                            }
                        }
                    }
                }

                foreach (var specialkey in hotkeyList)
                {
                    var input = GetInputCode(controller, specialkey.Key);
                    if (input == null)
                        continue;

                    if (input.Type != "key")
                        config[string.Format("input_{0}_{1}", specialkey.Value, typetoname[input.Type])] = GetConfigValue(input);
                }
            }

            foreach (var conflict in conflicts)
            {
                if (conflict != null && (conflict.StartsWith("input_toggle") || conflict.StartsWith("input_hold")))
                    config[conflict] = "nul";
            }

            return config;
        }

        private static Input GetInputCode(Controller controller, InputKey btnkey)
        {
            if (_inputDriver == "sdl2")
                return controller.GetSdlMapping(btnkey);

            if (_inputDriver == "dinput")
                return controller.GetDirectInputMapping(btnkey);

            return controller.GetXInputInput(btnkey);
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private static void WriteControllerConfig(ConfigFile retroconfig, Controller controller, string system, string core)
        {
            // Dolphin, when using xinput controller, will only work if joypad driver is set to xinput
            // So set to xinput in auto, if not forced in settings
            if (controller.PlayerIndex == 1 && core == "dolphin" && controller.IsXInputDevice && !Program.SystemConfig.isOptSet("input_driver"))
                _inputDriver = "xinput";
            
            var generatedConfig = GenerateControllerConfig(retroconfig, controller, system, core);
            foreach (var key in generatedConfig)
                retroconfig[key.Key] = key.Value;

            /// Turbo button (if shared feature is set)
            /// input_player{0}_turbo_{1} = turbo activation button (turbokey)
            /// input_turbo_default_button = button that is turbo'ed (turbo_default_button)
            /// 3 turbo modes in RetroArch : 
            /// classic : Press turbokey & button to become turbo & keep button pressed ==> turbo action will be enabled until you release button
            /// toggle : Press turbokey & button to become turbo ==> turbo will press automatically until you press button again to stop turbo
            /// hold : Press and hold turbokey to act as turbo for turbo_default_button
            if (Program.SystemConfig.isOptSet("enable_turbo") && !string.IsNullOrEmpty(Program.SystemConfig["enable_turbo"]))
            {
                // Define turbo mode
                retroconfig["input_turbo_mode"] = Program.SystemConfig["enable_turbo"];

                // Set up a default turbo button if selected (this is the target button to be turbo'd and is necessary in HOLD mode)
                if (Program.SystemConfig.isOptSet("turbo_default_button") && !string.IsNullOrEmpty(Program.SystemConfig["turbo_default_button"]))
                    retroconfig["input_turbo_default_button"] = Program.SystemConfig["turbo_default_button"];
                else
                    retroconfig["input_turbo_default_button"] = "0";

                // In Retroarch 1.21 the target button turboe'd is "input_turbo_button" and the values change
                // Also turboactivationbutton can be set with "input_turbo_bind" - not needing the code below

                // Define turbo activation button based on joypad input key mapping (4 options available L1, R1, L2, R2)
                if (Program.SystemConfig.isOptSet("turbo_button") && !string.IsNullOrEmpty(Program.SystemConfig["turbo_button"]))
                {
                    string turbobutton = Program.SystemConfig["turbo_button"];
                    InputKey turbokey;
                    if (turbobuttons.ContainsKey(turbobutton))
                    {
                        turbokey = turbobuttons[turbobutton];
                        var input = GetInputCode(controller, turbokey);
                        if (controller.Name == "Keyboard")
                        {
                            retroconfig[string.Format("input_player{0}_turbo", controller.PlayerIndex)] = GetConfigValue(input);
                        }
                        else
                            retroconfig[string.Format("input_player{0}_turbo_{1}", controller.PlayerIndex, typetoname[input.Type])] = GetConfigValue(input);
                    }

                    else
                    {
                        retroconfig[string.Format("input_player{0}_turbo_btn", controller.PlayerIndex)] = "nul";
                        retroconfig[string.Format("input_player{0}_turbo_axis", controller.PlayerIndex)] = "nul";
                    }
                }
                else
                {
                    retroconfig[string.Format("input_player{0}_turbo_btn", controller.PlayerIndex)] = "nul";
                    retroconfig[string.Format("input_player{0}_turbo_axis", controller.PlayerIndex)] = "nul";
                }
            }
            else
            {
                retroconfig["input_turbo_mode"] = "0";
                retroconfig["input_turbo_default_button"] = "0";
                retroconfig[string.Format("input_player{0}_turbo_btn", controller.PlayerIndex)] = "nul";
                retroconfig[string.Format("input_player{0}_turbo_axis", controller.PlayerIndex)] = "nul";
            }

            if (controller.Name != null && controller.Name == "Keyboard")
                return;

            retroconfig["input_joypad_driver"] = _inputDriver;

            int index = controller.DeviceIndex;
            if (index < 0)
                index = controller.PlayerIndex - 1;
           
            bool forceArcadeIndex = false;

            if (Program.SystemConfig.getOptBoolean("arcade_stick"))
            {
                int pIndex = controller.PlayerIndex;
                string forcePIndex = "p" + pIndex.ToString() + "_stick_index";

                if (Program.SystemConfig.isOptSet(forcePIndex) && !string.IsNullOrEmpty(Program.SystemConfig[forcePIndex]))
                {
                    if (int.TryParse(Program.SystemConfig[forcePIndex], out int stickIndex))
                    {
                        index = stickIndex;
                        forceArcadeIndex = true;
                        SimpleLogger.Instance.Info("[INFO] Force arcade stick index for player " + pIndex + " to: " + index);
                    }
                }
            }

            if (!forceArcadeIndex)
            {
                if (_inputDriver == "sdl2" && !string.IsNullOrEmpty(controller.DevicePath) && controller.SdlController != null)
                    index = controller.SdlController.Index;
                else if (_inputDriver == "dinput" && controller.DirectInput != null && controller.DirectInput.DeviceIndex > -1)
                    index = controller.DirectInput.DeviceIndex;
                else if (_inputDriver == "xinput" && controller.XInput != null && controller.XInput.DeviceIndex > -1)
                {
                    if (Program.SystemConfig.getOptBoolean("revertXIndex") && controller.xIndexReversed != -1)
                        index = controller.xIndexReversed;
                    else
                        index = controller.XInput.DeviceIndex;
                }
            }

            if (!_indexes.ContainsKey(controller.PlayerIndex))
                _indexes[controller.PlayerIndex] = index;

            retroconfig[string.Format("input_player{0}_joypad_index", controller.PlayerIndex)] = index.ToString();
            retroconfig[string.Format("input_player{0}_analog_dpad_mode", controller.PlayerIndex)] = GetAnalogMode(controller, system);

        }

        public static string GetConfigValue(Input input)
        {
            if (input.Type == "button")
                return input.Id.ToString();

            if (input.Type == "axis")
            {
                if (input.Value < 0)
                    return "-" + input.Id.ToString();
                else
                    return "+" + input.Id.ToString();
            }

            if (_inputDriver == "sdl2")
            {
                if (input.Type == "hat")
                {
                    if (input.Value == 2) // SDL_HAT_RIGHT
                        return "14";
                    else if (input.Value == 4) // SDL_HAT_DOWN
                        return "12";
                    else if (input.Value == 8) // SDL_HAT_LEFT
                        return "13";

                    return "11"; // UP
                }
            }
            else
            {
                // xinput / directInput            
                if (input.Type == "hat")
                {
                    if (input.Value == 2) // SDL_HAT_RIGHT
                        return "h" + input.Id + "right";
                    else if (input.Value == 4) // SDL_HAT_DOWN
                        return "h" + input.Id + "down";
                    else if (input.Value == 8) // SDL_HAT_LEFT
                        return "h" + input.Id + "left";

                    return "h" + input.Id + "up"; // UP
                }
            }

            if (input.Type == "key")
            {
                int id = (int) input.Id;
                List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                {
                    if (id == 'a')
                        id = 'q';
                    else if (id == 'q')
                        id = 'a';
                    else if (id == 'w')
                        id = 'z';
                    else if (id == 'z')
                        id = 'w';
                }
                
                SDL.SDL_Keycode code = (SDL.SDL_Keycode) id;

                if (input_config_key_map.ContainsKey(code))
                {
                    var rk = input_config_key_map[code];
                    var test = input_config_names.Where(i => i.Value == rk).Select(i => i.Key).FirstOrDefault();
                    if (test != null)
                        return test;
                }

                return ((char)id).ToString();
            }

            return input.Id.ToString();
        }

        enum retro_key
        {
            RETROK_UNKNOWN = 0,
            RETROK_FIRST = 0,
            RETROK_BACKSPACE = 8,
            RETROK_TAB = 9,
            RETROK_CLEAR = 12,
            RETROK_RETURN = 13,
            RETROK_PAUSE = 19,
            RETROK_ESCAPE = 27,
            RETROK_SPACE = 32,
            RETROK_EXCLAIM = 33,
            RETROK_QUOTEDBL = 34,
            RETROK_HASH = 35,
            RETROK_DOLLAR = 36,
            RETROK_AMPERSAND = 38,
            RETROK_QUOTE = 39,
            RETROK_LEFTPAREN = 40,
            RETROK_RIGHTPAREN = 41,
            RETROK_ASTERISK = 42,
            RETROK_PLUS = 43,
            RETROK_COMMA = 44,
            RETROK_MINUS = 45,
            RETROK_PERIOD = 46,
            RETROK_SLASH = 47,
            RETROK_0 = 48,
            RETROK_1 = 49,
            RETROK_2 = 50,
            RETROK_3 = 51,
            RETROK_4 = 52,
            RETROK_5 = 53,
            RETROK_6 = 54,
            RETROK_7 = 55,
            RETROK_8 = 56,
            RETROK_9 = 57,
            RETROK_COLON = 58,
            RETROK_SEMICOLON = 59,
            RETROK_LESS = 60,
            RETROK_EQUALS = 61,
            RETROK_GREATER = 62,
            RETROK_QUESTION = 63,
            RETROK_AT = 64,
            RETROK_LEFTBRACKET = 91,
            RETROK_BACKSLASH = 92,
            RETROK_RIGHTBRACKET = 93,
            RETROK_CARET = 94,
            RETROK_UNDERSCORE = 95,
            RETROK_BACKQUOTE = 96,
            RETROK_a = 97,
            RETROK_b = 98,
            RETROK_c = 99,
            RETROK_d = 100,
            RETROK_e = 101,
            RETROK_f = 102,
            RETROK_g = 103,
            RETROK_h = 104,
            RETROK_i = 105,
            RETROK_j = 106,
            RETROK_k = 107,
            RETROK_l = 108,
            RETROK_m = 109,
            RETROK_n = 110,
            RETROK_o = 111,
            RETROK_p = 112,
            RETROK_q = 113,
            RETROK_r = 114,
            RETROK_s = 115,
            RETROK_t = 116,
            RETROK_u = 117,
            RETROK_v = 118,
            RETROK_w = 119,
            RETROK_x = 120,
            RETROK_y = 121,
            RETROK_z = 122,
            RETROK_LEFTBRACE = 123,
            RETROK_BAR = 124,
            RETROK_RIGHTBRACE = 125,
            RETROK_TILDE = 126,
            RETROK_DELETE = 127,

            RETROK_KP0 = 256,
            RETROK_KP1 = 257,
            RETROK_KP2 = 258,
            RETROK_KP3 = 259,
            RETROK_KP4 = 260,
            RETROK_KP5 = 261,
            RETROK_KP6 = 262,
            RETROK_KP7 = 263,
            RETROK_KP8 = 264,
            RETROK_KP9 = 265,
            RETROK_KP_PERIOD = 266,
            RETROK_KP_DIVIDE = 267,
            RETROK_KP_MULTIPLY = 268,
            RETROK_KP_MINUS = 269,
            RETROK_KP_PLUS = 270,
            RETROK_KP_ENTER = 271,
            RETROK_KP_EQUALS = 272,

            RETROK_UP = 273,
            RETROK_DOWN = 274,
            RETROK_RIGHT = 275,
            RETROK_LEFT = 276,
            RETROK_INSERT = 277,
            RETROK_HOME = 278,
            RETROK_END = 279,
            RETROK_PAGEUP = 280,
            RETROK_PAGEDOWN = 281,

            RETROK_F1 = 282,
            RETROK_F2 = 283,
            RETROK_F3 = 284,
            RETROK_F4 = 285,
            RETROK_F5 = 286,
            RETROK_F6 = 287,
            RETROK_F7 = 288,
            RETROK_F8 = 289,
            RETROK_F9 = 290,
            RETROK_F10 = 291,
            RETROK_F11 = 292,
            RETROK_F12 = 293,
            RETROK_F13 = 294,
            RETROK_F14 = 295,
            RETROK_F15 = 296,

            RETROK_NUMLOCK = 300,
            RETROK_CAPSLOCK = 301,
            RETROK_SCROLLOCK = 302,
            RETROK_RSHIFT = 303,
            RETROK_LSHIFT = 304,
            RETROK_RCTRL = 305,
            RETROK_LCTRL = 306,
            RETROK_RALT = 307,
            RETROK_LALT = 308,
            RETROK_RMETA = 309,
            RETROK_LMETA = 310,
            RETROK_LSUPER = 311,
            RETROK_RSUPER = 312,
            RETROK_MODE = 313,
            RETROK_COMPOSE = 314,

            RETROK_HELP = 315,
            RETROK_PRINT = 316,
            RETROK_SYSREQ = 317,
            RETROK_BREAK = 318,
            RETROK_MENU = 319,
            RETROK_POWER = 320,
            RETROK_EURO = 321,
            RETROK_UNDO = 322,
            RETROK_OEM_102 = 323,

            RETROK_LAST
        }

        static readonly Dictionary<string, retro_key> input_config_names = new Dictionary<string, retro_key>()
        {
           { "left", retro_key.RETROK_LEFT },
           { "right", retro_key.RETROK_RIGHT },
           { "up", retro_key.RETROK_UP },
           { "down", retro_key.RETROK_DOWN },
           { "enter", retro_key.RETROK_RETURN },
           { "kp_enter", retro_key.RETROK_KP_ENTER },
           { "tab", retro_key.RETROK_TAB },
           { "insert", retro_key.RETROK_INSERT },
           { "del", retro_key.RETROK_DELETE },
           { "end", retro_key.RETROK_END },
           { "home", retro_key.RETROK_HOME },
           { "rshift", retro_key.RETROK_RSHIFT },
           { "shift", retro_key.RETROK_LSHIFT },
           { "ctrl", retro_key.RETROK_LCTRL },
           { "alt", retro_key.RETROK_LALT },
           { "space", retro_key.RETROK_SPACE },
           { "escape", retro_key.RETROK_ESCAPE },
           { "add", retro_key.RETROK_KP_PLUS },
           { "subtract", retro_key.RETROK_KP_MINUS },
           { "kp_plus", retro_key.RETROK_KP_PLUS },
           { "kp_minus", retro_key.RETROK_KP_MINUS },
           { "f1", retro_key.RETROK_F1 },
           { "f2", retro_key.RETROK_F2 },
           { "f3", retro_key.RETROK_F3 },
           { "f4", retro_key.RETROK_F4 },
           { "f5", retro_key.RETROK_F5 },
           { "f6", retro_key.RETROK_F6 },
           { "f7", retro_key.RETROK_F7 },
           { "f8", retro_key.RETROK_F8 },
           { "f9", retro_key.RETROK_F9 },
           { "f10", retro_key.RETROK_F10 },
           { "f11", retro_key.RETROK_F11 },
           { "f12", retro_key.RETROK_F12 },
           { "num0", retro_key.RETROK_0 },
           { "num1", retro_key.RETROK_1 },
           { "num2", retro_key.RETROK_2 },
           { "num3", retro_key.RETROK_3 },
           { "num4", retro_key.RETROK_4 },
           { "num5", retro_key.RETROK_5 },
           { "num6", retro_key.RETROK_6 },
           { "num7", retro_key.RETROK_7 },
           { "num8", retro_key.RETROK_8 },
           { "num9", retro_key.RETROK_9 },
           { "pageup",          retro_key.RETROK_PAGEUP },
           { "pagedown",        retro_key.RETROK_PAGEDOWN },
           { "keypad0",         retro_key.RETROK_KP0 },
           { "keypad1",         retro_key.RETROK_KP1 },
           { "keypad2",         retro_key.RETROK_KP2 },
           { "keypad3",         retro_key.RETROK_KP3 },
           { "keypad4",         retro_key.RETROK_KP4 },
           { "keypad5",         retro_key.RETROK_KP5 },
           { "keypad6",         retro_key.RETROK_KP6 },
           { "keypad7",         retro_key.RETROK_KP7 },
           { "keypad8",         retro_key.RETROK_KP8 },
           { "keypad9",         retro_key.RETROK_KP9 },
           { "period",          retro_key.RETROK_PERIOD },
           { "capslock",        retro_key.RETROK_CAPSLOCK },
           { "numlock",         retro_key.RETROK_NUMLOCK },
           { "backspace",       retro_key.RETROK_BACKSPACE },
           { "multiply",        retro_key.RETROK_KP_MULTIPLY },
           { "divide",          retro_key.RETROK_KP_DIVIDE },
           { "print_screen",    retro_key.RETROK_PRINT },
           { "scroll_lock",     retro_key.RETROK_SCROLLOCK },
           { "tilde",           retro_key.RETROK_BACKQUOTE },
           { "backquote",       retro_key.RETROK_BACKQUOTE },
           { "pause", retro_key.RETROK_PAUSE },

           { "quote", retro_key.RETROK_QUOTE },
           { "comma", retro_key.RETROK_COMMA },
           { "minus", retro_key.RETROK_MINUS },
           { "slash", retro_key.RETROK_SLASH },
           { "semicolon", retro_key.RETROK_SEMICOLON },
           { "equals", retro_key.RETROK_EQUALS },
           { "leftbracket", retro_key.RETROK_LEFTBRACKET },
           { "backslash", retro_key.RETROK_BACKSLASH },
           { "rightbracket", retro_key.RETROK_RIGHTBRACKET },
           { "kp_period", retro_key.RETROK_KP_PERIOD },
           { "kp_equals", retro_key.RETROK_KP_EQUALS },
           { "rctrl", retro_key.RETROK_RCTRL },
           { "ralt", retro_key.RETROK_RALT },

           { "caret", retro_key.RETROK_CARET },
           { "underscore", retro_key.RETROK_UNDERSCORE },
           { "exclaim", retro_key.RETROK_EXCLAIM },
           { "quotedbl", retro_key.RETROK_QUOTEDBL },
           { "hash", retro_key.RETROK_HASH },
           { "dollar", retro_key.RETROK_DOLLAR },
           { "ampersand", retro_key.RETROK_AMPERSAND },
           { "leftparen", retro_key.RETROK_LEFTPAREN },
           { "rightparen", retro_key.RETROK_RIGHTPAREN },
           { "asterisk", retro_key.RETROK_ASTERISK },
           { "plus", retro_key.RETROK_PLUS },
           { "colon", retro_key.RETROK_COLON },
           { "less", retro_key.RETROK_LESS },
           { "greater", retro_key.RETROK_GREATER },
           { "question", retro_key.RETROK_QUESTION },
           { "at", retro_key.RETROK_AT },

           { "f13", retro_key.RETROK_F13 },
           { "f14", retro_key.RETROK_F14 },
           { "f15", retro_key.RETROK_F15 },

           { "rmeta", retro_key.RETROK_RMETA },
           { "lmeta", retro_key.RETROK_LMETA },
           { "lsuper", retro_key.RETROK_LSUPER },
           { "rsuper", retro_key.RETROK_RSUPER },
           { "mode", retro_key.RETROK_MODE },
           { "compose", retro_key.RETROK_COMPOSE },

           { "help", retro_key.RETROK_HELP },
           { "sysreq", retro_key.RETROK_SYSREQ },
           { "break", retro_key.RETROK_BREAK },
           { "menu", retro_key.RETROK_MENU },
           { "power", retro_key.RETROK_POWER },
           { "euro", retro_key.RETROK_EURO },
           { "undo", retro_key.RETROK_UNDO },
           { "clear", retro_key.RETROK_CLEAR },
           { "oem102", retro_key.RETROK_OEM_102 },

           { "nul", retro_key.RETROK_UNKNOWN }
        };        
        
        static readonly Dictionary<SDL.SDL_Keycode, retro_key> input_config_key_map = new Dictionary<SDL.SDL_Keycode, retro_key>()
        {
           { SDL.SDL_Keycode.SDLK_BACKSPACE, retro_key.RETROK_BACKSPACE },
           { SDL.SDL_Keycode.SDLK_TAB, retro_key.RETROK_TAB },
           { SDL.SDL_Keycode.SDLK_CLEAR, retro_key.RETROK_CLEAR },
           { SDL.SDL_Keycode.SDLK_RETURN, retro_key.RETROK_RETURN },
           { SDL.SDL_Keycode.SDLK_PAUSE, retro_key.RETROK_PAUSE },
           { SDL.SDL_Keycode.SDLK_ESCAPE, retro_key.RETROK_ESCAPE },
           { SDL.SDL_Keycode.SDLK_SPACE, retro_key.RETROK_SPACE },
           { SDL.SDL_Keycode.SDLK_EXCLAIM, retro_key.RETROK_EXCLAIM },
           { SDL.SDL_Keycode.SDLK_QUOTEDBL, retro_key.RETROK_QUOTEDBL },
           { SDL.SDL_Keycode.SDLK_HASH, retro_key.RETROK_HASH },
           { SDL.SDL_Keycode.SDLK_DOLLAR, retro_key.RETROK_DOLLAR },
           { SDL.SDL_Keycode.SDLK_AMPERSAND, retro_key.RETROK_AMPERSAND },
           { SDL.SDL_Keycode.SDLK_QUOTE, retro_key.RETROK_QUOTE },
           { SDL.SDL_Keycode.SDLK_LEFTPAREN, retro_key.RETROK_LEFTPAREN },
           { SDL.SDL_Keycode.SDLK_RIGHTPAREN, retro_key.RETROK_RIGHTPAREN },
           { SDL.SDL_Keycode.SDLK_ASTERISK, retro_key.RETROK_ASTERISK },
           { SDL.SDL_Keycode.SDLK_PLUS, retro_key.RETROK_PLUS },
           { SDL.SDL_Keycode.SDLK_COMMA, retro_key.RETROK_COMMA },
           { SDL.SDL_Keycode.SDLK_MINUS, retro_key.RETROK_MINUS },
           { SDL.SDL_Keycode.SDLK_PERIOD, retro_key.RETROK_PERIOD },
           { SDL.SDL_Keycode.SDLK_SLASH, retro_key.RETROK_SLASH },
           { SDL.SDL_Keycode.SDLK_0, retro_key.RETROK_0 },
           { SDL.SDL_Keycode.SDLK_1, retro_key.RETROK_1 },
           { SDL.SDL_Keycode.SDLK_2, retro_key.RETROK_2 },
           { SDL.SDL_Keycode.SDLK_3, retro_key.RETROK_3 },
           { SDL.SDL_Keycode.SDLK_4, retro_key.RETROK_4 },
           { SDL.SDL_Keycode.SDLK_5, retro_key.RETROK_5 },
           { SDL.SDL_Keycode.SDLK_6, retro_key.RETROK_6 },
           { SDL.SDL_Keycode.SDLK_7, retro_key.RETROK_7 },
           { SDL.SDL_Keycode.SDLK_8, retro_key.RETROK_8 },
           { SDL.SDL_Keycode.SDLK_9, retro_key.RETROK_9 },
           { SDL.SDL_Keycode.SDLK_COLON, retro_key.RETROK_COLON },
           { SDL.SDL_Keycode.SDLK_SEMICOLON, retro_key.RETROK_SEMICOLON },
           { SDL.SDL_Keycode.SDLK_LESS, retro_key.RETROK_OEM_102 },
           { SDL.SDL_Keycode.SDLK_EQUALS, retro_key.RETROK_EQUALS },
           { SDL.SDL_Keycode.SDLK_GREATER, retro_key.RETROK_GREATER },
           { SDL.SDL_Keycode.SDLK_QUESTION, retro_key.RETROK_QUESTION },
           { SDL.SDL_Keycode.SDLK_AT, retro_key.RETROK_AT },
           { SDL.SDL_Keycode.SDLK_LEFTBRACKET, retro_key.RETROK_LEFTBRACKET },
           { SDL.SDL_Keycode.SDLK_BACKSLASH, retro_key.RETROK_BACKSLASH },
           { SDL.SDL_Keycode.SDLK_RIGHTBRACKET, retro_key.RETROK_RIGHTBRACKET },
           { SDL.SDL_Keycode.SDLK_CARET, retro_key.RETROK_CARET },
           { SDL.SDL_Keycode.SDLK_UNDERSCORE, retro_key.RETROK_UNDERSCORE },
           { SDL.SDL_Keycode.SDLK_BACKQUOTE, retro_key.RETROK_BACKQUOTE },
           { SDL.SDL_Keycode.SDLK_a, retro_key.RETROK_a },
           { SDL.SDL_Keycode.SDLK_b, retro_key.RETROK_b },
           { SDL.SDL_Keycode.SDLK_c, retro_key.RETROK_c },
           { SDL.SDL_Keycode.SDLK_d, retro_key.RETROK_d },
           { SDL.SDL_Keycode.SDLK_e, retro_key.RETROK_e },
           { SDL.SDL_Keycode.SDLK_f, retro_key.RETROK_f },
           { SDL.SDL_Keycode.SDLK_g, retro_key.RETROK_g },
           { SDL.SDL_Keycode.SDLK_h, retro_key.RETROK_h },
           { SDL.SDL_Keycode.SDLK_i, retro_key.RETROK_i },
           { SDL.SDL_Keycode.SDLK_j, retro_key.RETROK_j },
           { SDL.SDL_Keycode.SDLK_k, retro_key.RETROK_k },
           { SDL.SDL_Keycode.SDLK_l, retro_key.RETROK_l },
           { SDL.SDL_Keycode.SDLK_m, retro_key.RETROK_m },
           { SDL.SDL_Keycode.SDLK_n, retro_key.RETROK_n },
           { SDL.SDL_Keycode.SDLK_o, retro_key.RETROK_o },
           { SDL.SDL_Keycode.SDLK_p, retro_key.RETROK_p },
           { SDL.SDL_Keycode.SDLK_q, retro_key.RETROK_q },
           { SDL.SDL_Keycode.SDLK_r, retro_key.RETROK_r },
           { SDL.SDL_Keycode.SDLK_s, retro_key.RETROK_s },
           { SDL.SDL_Keycode.SDLK_t, retro_key.RETROK_t },
           { SDL.SDL_Keycode.SDLK_u, retro_key.RETROK_u },
           { SDL.SDL_Keycode.SDLK_v, retro_key.RETROK_v },
           { SDL.SDL_Keycode.SDLK_w, retro_key.RETROK_w },
           { SDL.SDL_Keycode.SDLK_x, retro_key.RETROK_x },
           { SDL.SDL_Keycode.SDLK_y, retro_key.RETROK_y },
           { SDL.SDL_Keycode.SDLK_z, retro_key.RETROK_z },
           { SDL.SDL_Keycode.SDLK_DELETE, retro_key.RETROK_DELETE },
           { SDL.SDL_Keycode.SDLK_KP_0, retro_key.RETROK_KP0 },
           { SDL.SDL_Keycode.SDLK_KP_1, retro_key.RETROK_KP1 },
           { SDL.SDL_Keycode.SDLK_KP_2, retro_key.RETROK_KP2 },
           { SDL.SDL_Keycode.SDLK_KP_3, retro_key.RETROK_KP3 },
           { SDL.SDL_Keycode.SDLK_KP_4, retro_key.RETROK_KP4 },
           { SDL.SDL_Keycode.SDLK_KP_5, retro_key.RETROK_KP5 },
           { SDL.SDL_Keycode.SDLK_KP_6, retro_key.RETROK_KP6 },
           { SDL.SDL_Keycode.SDLK_KP_7, retro_key.RETROK_KP7 },
           { SDL.SDL_Keycode.SDLK_KP_8, retro_key.RETROK_KP8 },
           { SDL.SDL_Keycode.SDLK_KP_9, retro_key.RETROK_KP9 },
           { SDL.SDL_Keycode.SDLK_KP_PERIOD, retro_key.RETROK_KP_PERIOD },
           { SDL.SDL_Keycode.SDLK_KP_DIVIDE, retro_key.RETROK_KP_DIVIDE },
           { SDL.SDL_Keycode.SDLK_KP_MULTIPLY, retro_key.RETROK_KP_MULTIPLY },
           { SDL.SDL_Keycode.SDLK_KP_MINUS, retro_key.RETROK_KP_MINUS },
           { SDL.SDL_Keycode.SDLK_KP_PLUS, retro_key.RETROK_KP_PLUS },
           { SDL.SDL_Keycode.SDLK_KP_ENTER, retro_key.RETROK_KP_ENTER },
           { SDL.SDL_Keycode.SDLK_KP_EQUALS, retro_key.RETROK_KP_EQUALS },
           { SDL.SDL_Keycode.SDLK_UP, retro_key.RETROK_UP },
           { SDL.SDL_Keycode.SDLK_DOWN, retro_key.RETROK_DOWN },
           { SDL.SDL_Keycode.SDLK_RIGHT, retro_key.RETROK_RIGHT },
           { SDL.SDL_Keycode.SDLK_LEFT, retro_key.RETROK_LEFT },
           { SDL.SDL_Keycode.SDLK_INSERT, retro_key.RETROK_INSERT },
           { SDL.SDL_Keycode.SDLK_HOME, retro_key.RETROK_HOME },
           { SDL.SDL_Keycode.SDLK_END, retro_key.RETROK_END },
           { SDL.SDL_Keycode.SDLK_PAGEUP, retro_key.RETROK_PAGEUP },
           { SDL.SDL_Keycode.SDLK_PAGEDOWN, retro_key.RETROK_PAGEDOWN },
          
           { SDL.SDL_Keycode.SDLK_F1, retro_key.RETROK_F1 },
           { SDL.SDL_Keycode.SDLK_F2, retro_key.RETROK_F2 },
           { SDL.SDL_Keycode.SDLK_F3, retro_key.RETROK_F3 },
           { SDL.SDL_Keycode.SDLK_F4, retro_key.RETROK_F4 },
           { SDL.SDL_Keycode.SDLK_F5, retro_key.RETROK_F5 },
           { SDL.SDL_Keycode.SDLK_F6, retro_key.RETROK_F6 },
           { SDL.SDL_Keycode.SDLK_F7, retro_key.RETROK_F7 },
           { SDL.SDL_Keycode.SDLK_F8, retro_key.RETROK_F8 },
           { SDL.SDL_Keycode.SDLK_F9, retro_key.RETROK_F9 },
           { SDL.SDL_Keycode.SDLK_F10, retro_key.RETROK_F10 },
           { SDL.SDL_Keycode.SDLK_F11, retro_key.RETROK_F11 },
           { SDL.SDL_Keycode.SDLK_F12, retro_key.RETROK_F12 },
           { SDL.SDL_Keycode.SDLK_F13, retro_key.RETROK_F13 },
           { SDL.SDL_Keycode.SDLK_F14, retro_key.RETROK_F14 },
           { SDL.SDL_Keycode.SDLK_F15, retro_key.RETROK_F15 }, 
           { SDL.SDL_Keycode.SDLK_NUMLOCKCLEAR, retro_key.RETROK_NUMLOCK },
           { SDL.SDL_Keycode.SDLK_CAPSLOCK, retro_key.RETROK_CAPSLOCK },
           { SDL.SDL_Keycode.SDLK_SCROLLLOCK, retro_key.RETROK_SCROLLOCK },
           { SDL.SDL_Keycode.SDLK_RSHIFT, retro_key.RETROK_RSHIFT },
           { SDL.SDL_Keycode.SDLK_LSHIFT, retro_key.RETROK_LSHIFT },
           { SDL.SDL_Keycode.SDLK_RCTRL, retro_key.RETROK_RCTRL },
           { SDL.SDL_Keycode.SDLK_LCTRL, retro_key.RETROK_LCTRL },
           { SDL.SDL_Keycode.SDLK_RALT, retro_key.RETROK_RALT },
           { SDL.SDL_Keycode.SDLK_LALT, retro_key.RETROK_LALT },
           { SDL.SDL_Keycode.SDLK_LGUI, retro_key.RETROK_LSUPER },
           { SDL.SDL_Keycode.SDLK_RGUI, retro_key.RETROK_RSUPER },
           { SDL.SDL_Keycode.SDLK_MODE, retro_key.RETROK_MODE },
           { SDL.SDL_Keycode.SDLK_HELP, retro_key.RETROK_HELP },
           { SDL.SDL_Keycode.SDLK_PRINTSCREEN, retro_key.RETROK_PRINT },
           { SDL.SDL_Keycode.SDLK_SYSREQ, retro_key.RETROK_SYSREQ },                                                                   
           //{ SDL.SDL_Keycode.SDLK_PAUSE, retro_key.RETROK_BREAK },
           { SDL.SDL_Keycode.SDLK_MENU, retro_key.RETROK_MENU },
           { SDL.SDL_Keycode.SDLK_POWER, retro_key.RETROK_POWER },
           { SDL.SDL_Keycode.SDLK_UNDO, retro_key.RETROK_UNDO },
      //     { 0, retro_key.RETROK_UNKNOWN },*/
        };
    }
}