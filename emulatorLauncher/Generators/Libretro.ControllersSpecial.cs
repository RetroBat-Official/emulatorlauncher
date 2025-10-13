﻿using System.IO;
using System.Collections.Generic;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Linq;

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
            bool ignoreSystemSpecificMapping = false;
            string guid = controller.Guid.ToString().ToLowerInvariant();

            // First look if the user has a specific mapping file set for the controller
            string userMapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "retroarch_controller.json");
            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "user");

                string userMapping2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "retroarch_controller.json");

                if (File.Exists(userMapping2))
                {
                    userMapping = userMapping2;
                    break;
                }
            }
            if (File.Exists(userMapping))
            {
                try
                {
                    var userControllers = UserController.LoadControllersFromJson(userMapping);

                    if (userControllers == null)
                    {
                        SimpleLogger.Instance.Info("[Controller] Error loading custom mapping JSON file.");
                        goto BypassUserController;
                    }

                    UserController userController = UserController.GetUserController("libretro", guid, _inputDriver, userControllers);
                    if (userController == null)
                    {
                        SimpleLogger.Instance.Info("[Controller] No custom mapping found for User Controller.");
                        goto BypassUserController;
                    }

                    if (userController.Mapping == null)
                    {
                        SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + userController.Name);
                        goto BypassUserController;
                    }

                    if (userController.ControllerInfo != null)
                    {
                        if (userController.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                            retroconfig["input_analog_sensitivity"] = userController.ControllerInfo["input_analog_sensitivity"];
                        if (userController.ControllerInfo.ContainsKey("input_joypad_driver"))
                            inputConfig["input_joypad_driver"] = userController.ControllerInfo["input_joypad_driver"];
                        if (userController.ControllerInfo.ContainsKey("ignoreSystemSpecificMapping") && userController.ControllerInfo["ignoreSystemSpecificMapping"] == "true")
                            ignoreSystemSpecificMapping = true;
                    }

                    SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + userController.Name);

                    foreach (var button in userController.Mapping)
                        inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;

                    if (userController.HotKeyMapping != null && controller.PlayerIndex == 1)
                    {
                        foreach (var hotkey in userController.HotKeyMapping)
                            inputConfig[hotkey.Key] = hotkey.Value;
                        _specialControllerHotkey = true;
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + userController.Name);

                    if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
                        _inputDriver = inputConfig["input_joypad_driver"];

                    _specialController = true;
                    
                    if (ignoreSystemSpecificMapping)
                        return true;
                    else
                        goto BypassStickMapping;
                }
                catch { }
            }

        BypassUserController:

            // specific mapping for arcade sticks
            if (useArcadeStick)
            {
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

                        SimpleLogger.Instance.Info("[Controller] Performing specific arcade stick mapping for " + arcadeStick.Name);

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

                        if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

        BypassStickMapping:
            // Specific mapping for N64 like controllers
            if (system == "n64")
            {
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
                                needActivationSwitch = n64Gamepad.ControllerInfo["needActivationSwitch"] == "true";

                            if (needActivationSwitch && !n64_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific n64 mapping needs to be activated for this controller.");
                                return false;
                            }

                            if (n64Gamepad.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = n64Gamepad.ControllerInfo["input_analog_sensitivity"];
                            if (n64Gamepad.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = n64Gamepad.ControllerInfo["input_joypad_driver"];
                            if (n64Gamepad.ControllerInfo.ContainsKey("switch_trigger") && !string.IsNullOrEmpty(n64Gamepad.ControllerInfo["switch_trigger"]))
                            {
                                if (Program.SystemConfig.getOptBoolean("n64_special_trigger"))
                                {
                                    n64Gamepad.Mapping["l2_btn"] = n64Gamepad.ControllerInfo["switch_trigger"];
                                }
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific n64 mapping for " + n64Gamepad.Name);

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

                        //_inputDriver = "sdl2";

                        if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
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
                                needActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "true";
                            if (mdGamepad.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = mdGamepad.ControllerInfo["input_analog_sensitivity"];
                            if (mdGamepad.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = mdGamepad.ControllerInfo["input_joypad_driver"];
                            if (mdGamepad.ControllerInfo.ContainsKey("analogdpad"))
                                analogDpad = mdGamepad.ControllerInfo["analogdpad"] == "true";

                            if (needActivationSwitch && !md_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific megadrive mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific megadrive mapping for " + mdGamepad.Name);

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

                        if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for gamecube-like controllers
            else if (system == "gamecube" || system == "gc")
            {
                string gcjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "GCControllers.json");
                bool needActivationSwitch = false;
                bool gc_pad = Program.SystemConfig.getOptBoolean("gc_pad");

                foreach (var path in mappingPaths)
                {
                    string result = path
                        .Replace("{systempath}", "system")
                        .Replace("{userpath}", "user");

                    string gcjson2 = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result, "GCControllers.json");

                    if (File.Exists(gcjson2))
                    {
                        gcjson = gcjson2;
                        break;
                    }
                }

                if (!File.Exists(gcjson))
                {
                    SimpleLogger.Instance.Info("[Controller] No Gamecube JSON file found.");
                    return false;
                }

                else
                {
                    try
                    {
                        var gcControllers = GCController.LoadControllersFromJson(gcjson);

                        if (gcControllers == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                            return false;
                        }

                        GCController gcGamepad = GCController.GetGCController("libretro", guid, _inputDriver, gcControllers);
                        if (gcGamepad == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Gamecube controller.");
                            return false;
                        }

                        if (gcGamepad.Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro : " + gcGamepad.Name);
                            return false;
                        }

                        if (gcGamepad.ControllerInfo != null)
                        {
                            if (gcGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                needActivationSwitch = gcGamepad.ControllerInfo["needActivationSwitch"] == "true";
                            if (gcGamepad.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = gcGamepad.ControllerInfo["input_analog_sensitivity"];
                            if (gcGamepad.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = gcGamepad.ControllerInfo["input_joypad_driver"];

                            if (needActivationSwitch && !gc_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific Gamecube mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific Gamecube mapping for " + gcGamepad.Name);

                        foreach (var button in gcGamepad.Mapping)
                        {
                            if (analogDpad && digitalDpadStrings.Contains(button.Key))
                                continue;
                            else if (!analogDpad && analogDpadStrings.Contains(button.Key))
                                continue;

                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;
                        }

                        if (gcGamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                        {
                            foreach (var hotkey in gcGamepad.HotKeyMapping)
                                inputConfig[hotkey.Key] = hotkey.Value;
                            _specialControllerHotkey = true;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Missing mapping for libretro hotkeys : " + gcGamepad.Name);

                        if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for saturn-like controllers
            else if (system == "saturn")
            {
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
                            if (saturnGamepad.ControllerInfo.ContainsKey("needActivationSwitch") && saturnGamepad.ControllerInfo["needActivationSwitch"] == "true")
                                needActivationSwitch = saturnGamepad.ControllerInfo["needActivationSwitch"] == "true";
                            if (saturnGamepad.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = saturnGamepad.ControllerInfo["input_analog_sensitivity"];
                            if (saturnGamepad.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = saturnGamepad.ControllerInfo["input_joypad_driver"];
                            if (saturnGamepad.ControllerInfo.ContainsKey("analogdpad"))
                                analogDpad = saturnGamepad.ControllerInfo["analogdpad"] == "true";

                            if (needActivationSwitch && !sat_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific saturn mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific saturn mapping for " + saturnGamepad.Name);

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

                        if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            // Specific mapping for megadrive-like controllers for 3DO system
            else if (system == "3do")
            {
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
                            if (specGamepad.ControllerInfo.ContainsKey("needActivationSwitch") && specGamepad.ControllerInfo["needActivationSwitch"] == "true")
                                needActivationSwitch = specGamepad.ControllerInfo["needActivationSwitch"] == "true";
                            if (specGamepad.ControllerInfo.ContainsKey("input_analog_sensitivity"))
                                retroconfig["input_analog_sensitivity"] = specGamepad.ControllerInfo["input_analog_sensitivity"];
                            if (specGamepad.ControllerInfo.ContainsKey("input_joypad_driver"))
                                inputConfig["input_joypad_driver"] = specGamepad.ControllerInfo["input_joypad_driver"];
                            if (specGamepad.ControllerInfo.ContainsKey("analogdpad"))
                                analogDpad = specGamepad.ControllerInfo["analogdpad"] == "true";

                            if (needActivationSwitch && !spec_pad)
                            {
                                SimpleLogger.Instance.Info("[Controller] Specific 3DO mapping needs to be activated for this controller.");
                                return false;
                            }
                        }

                        SimpleLogger.Instance.Info("[Controller] Performing specific 3DO mapping for " + specGamepad.Name);

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

                        if (inputConfig.ContainsKey("input_joypad_driver") && inputConfig["input_joypad_driver"] != null)
                            _inputDriver = inputConfig["input_joypad_driver"];

                        _specialController = true;
                        return true;
                    }
                    catch { }
                }
            }

            if (_specialController)
                return true;
            else
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