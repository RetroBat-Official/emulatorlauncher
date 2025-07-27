using System.IO;
using System.Collections.Generic;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher.Libretro
{
    partial class LibretroControllers
    {
        // Used to automatically map special controllers like N64, saturn, megadrive
        // The mapping is forced from json file located in retrobat\system\resources\inputmapping

        private static List<string> digitalDpadStrings = new List<string> { "down_btn", "up_btn", "left_btn", "right_btn" };
        private static List<string> analogDpadStrings = new List<string> { "down_axis", "up_axis", "left_axis", "right_axis" };

        private static bool performSpecialMapping(out Dictionary<string, string> inputConfig, string system, Controller controller, ConfigFile retroconfig)
        {
            inputConfig = new Dictionary<string, string>();
            bool analogDpad = Program.SystemConfig.getOptBoolean("analogDpad");
            bool useArcadeStick = Program.SystemConfig.getOptBoolean("arcade_stick");

            // specific mapping for arcade sticks*
            if (useArcadeStick)
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string stickjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "arcade_sticks.json");
                foreach (var path in mappingPaths)
                {
                    string result = path
                        .Replace("{systempath}", "system")
                        .Replace("{userpath}", "user");

                    string stickjson2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "arcade_sticks.json");

                    if (File.Exists(stickjson2))
                    {
                        stickjson = stickjson2;
                        break;
                    }
                }

                if (!File.Exists(stickjson))
                {
                    SimpleLogger.Instance.Info("[Controller] No json file found for Arcade Stick special mapping.");
                    return false;
                }

                else
                {
                    try
                    {
                        var arcadeControllers = ArcadeStickController.LoadControllersFromJson(stickjson);

                        if (arcadeControllers == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                            return false;
                        }

                        ArcadeStickController arcadeStick = ArcadeStickController.GetArcadeController("libretro", guid, _inputDriver, arcadeControllers);
                        if (arcadeStick == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Arcade Stick.");
                            return false;
                        }

                        if (arcadeStick.Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + arcadeStick.Name);
                            return false;
                        }

                        if (arcadeStick.ControllerInfo != null)
                        {
                            if (arcadeStick.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = arcadeStick.ControllerInfo["input_analog_sensitivity"];
                            if (arcadeStick.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = arcadeStick.ControllerInfo["input_joypad_driver"];
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + arcadeStick.Name);

                        foreach (var button in arcadeStick.Mapping)
                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;

                        if (arcadeStick.HotKeyMapping != null && controller.PlayerIndex == 1)
                        {
                            foreach (var hotkey in arcadeStick.HotKeyMapping)
                                inputConfig[hotkey.Key] = hotkey.Value;
                            _specialControllerHotkey = true;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + arcadeStick.Name);

                        _inputDriver = "sdl2";

                        if (inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for N64 like controllers
            if (system == "n64")
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                bool needActivationSwitch = false;
                bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");

                string n64json = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
                foreach (var path in mappingPaths)
                {
                    string result = path
                        .Replace("{systempath}", "system")
                        .Replace("{userpath}", "user");

                    string n64json2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "n64Controllers.json");

                    if (File.Exists(n64json2))
                    {
                        n64json = n64json2;
                        break;
                    }
                }

                if (!File.Exists(n64json))
                {
                    SimpleLogger.Instance.Info("[Controller] No N64 JSON file found.");
                    return false;
                }

                else
                {
                    try
                    {
                        var n64Controllers = N64Controller.LoadControllersFromJson(n64json);

                        if (n64Controllers == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                            return false;
                        }

                        N64Controller n64Gamepad = N64Controller.GetN64Controller("libretro", guid, _inputDriver, n64Controllers);
                        if (n64Gamepad == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for N64 controller.");
                            return false;
                        }

                        if (n64Gamepad.Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + n64Gamepad.Name);
                            return false;
                        }

                        if (n64Gamepad.ControllerInfo != null)
                        {
                            if (n64Gamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                needActivationSwitch = n64Gamepad.ControllerInfo["needActivationSwitch"] == "yes";

                            if (needActivationSwitch && !n64_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific n64 mapping needs to be activated for this controller.");
                                return false;
                            }

                            if (n64Gamepad.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = n64Gamepad.ControllerInfo["input_analog_sensitivity"];
                            if (n64Gamepad.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = n64Gamepad.ControllerInfo["input_joypad_driver"];
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + n64Gamepad.Name);

                        foreach (var button in n64Gamepad.Mapping)
                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;

                        if (n64Gamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                        {
                            foreach (var hotkey in n64Gamepad.HotKeyMapping)
                                inputConfig[hotkey.Key] = hotkey.Value;
                            _specialControllerHotkey = true;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + n64Gamepad.Name);

                        _inputDriver = "sdl2";

                        if (inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for megadrive-like controllers
            else if (mdSystems.Contains(system))
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string mdjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mdControllers.json");
                bool needActivationSwitch = false;
                bool md_pad = Program.SystemConfig.getOptBoolean("md_pad");

                foreach (var path in mappingPaths)
                {
                    string result = path
                        .Replace("{systempath}", "system")
                        .Replace("{userpath}", "user");

                    string mdjson2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "mdControllers.json");

                    if (File.Exists(mdjson2))
                    {
                        mdjson = mdjson2;
                        break;
                    }
                }

                if (!File.Exists(mdjson))
                {
                    SimpleLogger.Instance.Info("[Controller] No Megadrive JSON file found.");
                    return false;
                }

                else
                {
                    try
                    {
                        var megadriveControllers = MegadriveController.LoadControllersFromJson(mdjson);

                        if (megadriveControllers == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                            return false;
                        }

                        MegadriveController mdGamepad = MegadriveController.GetMDController("libretro", guid, _inputDriver, megadriveControllers);
                        if (mdGamepad == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Megadrive controller.");
                            return false;
                        }

                        if (mdGamepad.Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + mdGamepad.Name);
                            return false;
                        }

                        if (mdGamepad.ControllerInfo != null)
                        {
                            if (mdGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                needActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                            if (needActivationSwitch && !md_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific megadrive mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + mdGamepad.Name);

                        foreach (var button in mdGamepad.Mapping)
                        {
                            if (analogDpad && digitalDpadStrings.Contains(button.Key))
                                continue;
                            else if (!analogDpad && analogDpadStrings.Contains(button.Key))
                                continue;

                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;
                        }

                        if (mdGamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                        {
                            foreach (var hotkey in mdGamepad.HotKeyMapping)
                                inputConfig[hotkey.Key] = hotkey.Value;
                            _specialControllerHotkey = true;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + mdGamepad.Name);

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for saturn-like controllers
            else if (system == "saturn")
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string saturnjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "saturnControllers.json");
                bool needActivationSwitch = false;
                bool sat_pad = Program.SystemConfig.getOptBoolean("saturn_pad");

                foreach (var path in mappingPaths)
                {
                    string result = path
                        .Replace("{systempath}", "system")
                        .Replace("{userpath}", "user");

                    string saturnjson2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "saturnControllers.json");

                    if (File.Exists(saturnjson2))
                    {
                        saturnjson = saturnjson2;
                        break;
                    }
                }

                if (!File.Exists(saturnjson))
                {
                    SimpleLogger.Instance.Info("[Controller] No Saturn JSON file found.");
                    return false;
                }

                else
                {
                    try
                    {
                        var saturnControllers = MegadriveController.LoadControllersFromJson(saturnjson);

                        if (saturnControllers == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                            return false;
                        }

                        MegadriveController saturnGamepad = MegadriveController.GetMDController("libretro", guid, _inputDriver, saturnControllers);
                        if (saturnGamepad == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Saturn controller.");
                            return false;
                        }

                        if (saturnGamepad.Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + saturnGamepad.Name);
                            return false;
                        }

                        if (saturnGamepad.ControllerInfo != null)
                        {
                            if (saturnGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                needActivationSwitch = saturnGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                            if (needActivationSwitch && !sat_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific saturn mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + saturnGamepad.Name);

                        foreach (var button in saturnGamepad.Mapping)
                        {
                            if (analogDpad && digitalDpadStrings.Contains(button.Key))
                                continue;
                            else if (!analogDpad && analogDpadStrings.Contains(button.Key))
                                continue;

                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;
                        }

                        if (saturnGamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                        {
                            foreach (var hotkey in saturnGamepad.HotKeyMapping)
                                inputConfig[hotkey.Key] = hotkey.Value;
                            _specialControllerHotkey = true;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + saturnGamepad.Name);

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for megadrive-like controllers for 3DO system
            else if (system == "3do")
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string specjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "3doControllers.json");
                bool needActivationSwitch = false;
                bool spec_pad = Program.SystemConfig.getOptBoolean("3do_pad");
                
                foreach (var path in mappingPaths)
                {
                    string result = path
                        .Replace("{systempath}", "system")
                        .Replace("{userpath}", "user");

                    string specjson2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "3doControllers.json");

                    if (File.Exists(specjson2))
                    {
                        specjson = specjson2;
                        break;
                    }
                }
                
                if (!File.Exists(specjson))
                {
                    SimpleLogger.Instance.Info("[Controller] No 3DO JSON file found.");
                    return false;
                }

                else
                {
                    try
                    {
                        var specControllers = MegadriveController.LoadControllersFromJson(specjson);

                        if (specControllers == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                            return false;
                        }

                        MegadriveController specGamepad = MegadriveController.GetMDController("libretro", guid, _inputDriver, specControllers);
                        if (specGamepad == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for 3DO controller.");
                            return false;
                        }

                        if (specGamepad.Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + specGamepad.Name);
                            return false;
                        }

                        if (specGamepad.ControllerInfo != null)
                        {
                            if (specGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                needActivationSwitch = specGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                            if (needActivationSwitch && !spec_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific 3DO mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + specGamepad.Name);

                        foreach (var button in specGamepad.Mapping)
                        {
                            if (analogDpad && digitalDpadStrings.Contains(button.Key))
                                continue;
                            else if (!analogDpad && analogDpadStrings.Contains(button.Key))
                                continue;
                            
                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;
                        }

                        if (specGamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                        {
                            foreach (var hotkey in specGamepad.HotKeyMapping)
                                inputConfig[hotkey.Key] = hotkey.Value;
                            _specialControllerHotkey = true;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + specGamepad.Name);

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        static readonly string[] mappingPaths =
        {            
            // User specific
            "{userpath}\\inputmapping",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping",
        };
    }
}