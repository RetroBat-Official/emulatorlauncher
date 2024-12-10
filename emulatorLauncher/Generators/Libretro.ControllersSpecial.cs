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

        private static bool performSpecialMapping(out Dictionary<string, string> inputConfig, string system, Controller controller, ConfigFile retroconfig)
        {
            inputConfig = new Dictionary<string, string>();

            // Specific mapping for N64 like controllers
            if (system == "n64")
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string n64json = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
                bool needActivationSwitch = false;
                bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");

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
                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;

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
                            inputConfig[string.Format("input_player{0}_{1}", controller.PlayerIndex, button.Key)] = button.Value;

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

            return false;
        }
    }
}