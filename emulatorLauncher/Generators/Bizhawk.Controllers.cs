using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        /* When adding new Bizhawk core/system:
         * - Fill inputPortNb (check in bizhawk number of controllers for the system - don't forget multitaps)
         * - Fill systemController dictionnary (with the name of the controller found in config.ini)
         * - Fill mappingToUse dictionnary and add a default mapping (check config.ini)
         * - If only 1 player fill systemMonoPlayer list (this is used to remove P1 for controls)
         * - If system is a computer, fill computersystem list
         * - Configure both keyboard and joystick (use standard bizhawk mappings if necessary)
         * - It's possible to map multiple buttons/keys to a single target button (separated by ", " : example channelf)
         * - If there are buttons on the console (without P1 for example : they need to be configured in the joystick "Specifics" region)
         * - Analog controls need to be specified in the specific analog section
         * - Bizhawk treats all gamepads as XInput except the gamepads not known by SDL (these are treated as dinput)
        */

        private static readonly List<string> systemMonoPlayer = new List<string>() { "3ds", "apple2", "gb", "gbc", "gba", "lynx", "nds" };
        private static readonly List<string> computersystem = new List<string>() { "apple2" };

        private static readonly Dictionary<string, int> inputPortNb = new Dictionary<string, int>()
        {
            { "A26", 2 },
            { "Atari2600Hawk", 2 },
            { "A78", 2 },
            { "AppleII", 1 },
            { "Ares64", 4 },
            { "BSNES", 8 },
            { "ChannelFHawk", 2 },
            { "Coleco", 2 },
            { "Cygne", 1 },
            { "Encore", 1 },
            { "Faust", 8 },
            { "Gambatte", 1 },
            { "GBHawk", 1 },
            { "Genplus-gx", 8 },
            { "Handy", 1 },
            { "HyperNyma", 5 },
            { "IntelliHawk", 2 },
            { "Jaguar", 2 },
            { "Lynx", 1 },
            { "mGBA", 1 },
            { "melonDS", 1 },
            { "Mupen64Plus", 4 },
            { "NeoPop", 1 },
            { "NesHawk", 4 },
            { "Nymashock", 8 },
            { "O2Hawk", 2 },
            { "Octoshock", 8 },
            { "PCEHawk", 5 },
            { "PCFX", 2 },
            { "PicoDrive", 2 },
            { "quickerNES", 2 },
            { "QuickNes", 2 },
            { "SameBoy", 1 },
            { "Saturnus", 12 },
            { "SMSHawk", 2 },
            { "Snes9x", 5 },
            { "TIC-80", 4 },
            { "TurboNyma", 5 },
            { "Uzem", 2 },
            { "VectrexHawk", 2 },
            { "VirtualBoyee", 1 },
            { "ZXHawk", 3 }
        };

        private void CreateControllerConfiguration(DynamicJson json, string system, string core)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for BizHawk");

            json["InputHotkeyOverrideOptions"] = "0";

            int maxPad = inputPortNb[core];

            // Specifics
            if (system == "gamegear")
                maxPad = 1;
            if (system == "sgb" && core == "Gambatte")
                maxPad = 4;
            if (system == "sgb" && core == "BSNES")
                maxPad = 2;

            if (!computersystem.Contains(system))
                ResetControllerConfiguration(json, maxPad, system, core);

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(controller, json, system, core);

            ConfigureHotkeys(json);
        }

        private void ConfigureHotkeys(DynamicJson json)
        {
            var hotkeyBindings = json.GetOrCreateContainer("HotkeyBindings");
            for (int i = 1; i < 11; i++)
            {
                hotkeyBindings["Save State " + i] = "Ctrl+F" + i;
                hotkeyBindings["Load State " + i] = "Shift+F" + i;
            }
            hotkeyBindings["Rewind"] = "F1";
            hotkeyBindings["Fast Forward"] = "F2";
            hotkeyBindings["Pause"] = "F9";
            hotkeyBindings["Quick Load"] = "F3";
            hotkeyBindings["Quick Save"] = "F4";
            hotkeyBindings["Screenshot"] = "F12";
            hotkeyBindings["Previous Slot"] = "F5";
            hotkeyBindings["Next Slot"] = "F6";
        }

        private void ConfigureInput(Controller controller, DynamicJson json, string system, string core)
        {
            if (controller == null || controller.Config == null)
                return;

            if (computersystem.Contains(system))
                ConfigureKeyboardSystem(json, system);
            else if (controller.IsKeyboard)
                ConfigureKeyboard(controller, json, system, core);
            else
                ConfigureJoystick(controller, json, system, core);

            if (system == "odyssey2" || system == "zxspectrum")
                ConfigureKeyboardSystem(json, system);
        }

        private void ConfigureJoystick(Controller controller, DynamicJson json, string system, string core)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            bool monoplayer = systemMonoPlayer.Contains(system);
            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);

            // Define mapping to use
            InputKeyMapping mapping = mappingToUse[system];

            // Specific cases
            if (system == "psx")
            {
                if (core == "Nymashock" && SystemConfig.isOptSet("bizhawk_psx_digital") && SystemConfig.getOptBoolean("bizhawk_psx_digital"))
                    mapping = psxNymaMapping;
                else if (core == "Nymashock")
                    mapping = dualshockNymaMapping;
                else if (SystemConfig.isOptSet("bizhawk_psx_digital") && SystemConfig.getOptBoolean("bizhawk_psx_digital"))
                    mapping = psxOctoMapping;
            }

            if (system == "sgb" && core == "BSNES")
            {
                mapping = snesMapping;
                controllerConfig = trollers.GetOrCreateContainer(systemController["snes"]);
            }

            if (core == "GBHawk")
            {
                monoplayer = false;
                controllerConfig = trollers.GetOrCreateContainer("Gameboy Controller H");
            }

            // Perform mapping
            int playerIndex = controller.PlayerIndex;
            if (Features.IsSupported("bizhawk_swapcontrols") && Program.SystemConfig.getOptBoolean("bizhawk_swapcontrols"))
            {
                if (controller.PlayerIndex == 1)
                    playerIndex = 2;
                else if (controller.PlayerIndex == 2)
                    playerIndex = 1;
            }

            int index = controller.SdlController != null ? controller.SdlController.Index + 1 : controller.DeviceIndex + 1;
            string guid = controller.Guid.ToString().ToLowerInvariant();
            bool isDInput = false;

            if (controller.SdlController == null && !controller.IsXInputDevice)
                isDInput = true;

            #region specialControllers
            // Special treatment for N64 controllers
            N64Controller n64Gamepad = null;
            bool n64ControllerFound = false;
            string n64json = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
            bool needn64ActivationSwitch = false;
            bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");

            if (File.Exists(n64json) && system == "n64")
            {
                try
                {
                    var n64Controllers = N64Controller.LoadControllersFromJson(n64json);
                    if (n64Controllers != null)
                    {
                        n64Gamepad = N64Controller.GetN64Controller("bizhawk", guid, n64Controllers);

                        if (n64Gamepad != null)
                        {
                            if (n64Gamepad.ControllerInfo != null)
                            {
                                if (n64Gamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needn64ActivationSwitch = n64Gamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needn64ActivationSwitch && !n64_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific n64 mapping needs to be activated for this controller.");
                                    goto Bypassn64Controllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + n64Gamepad.Name);

                            bool useDInput = false;

                            if (n64Gamepad.ControllerInfo != null)
                            {
                                if (n64Gamepad.ControllerInfo.ContainsKey("dinput"))
                                    useDInput = n64Gamepad.ControllerInfo["dinput"] == "true";
                            }

                            if (n64Gamepad.Mapping != null)
                            {
                                foreach (var x in n64Gamepad.Mapping)
                                {
                                    string key = x.Key;
                                    string value = x.Value;
                                    controllerConfig["P" + playerIndex + " " + key] = useDInput ? "J" + index + " " + value : "X" + index + " " + value;
                                }
                                n64ControllerFound = true;
                            }
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for N64 controller.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            Bypassn64Controllers:

            // Special treatment for Megadrive controllers
            MegadriveController mdGamepad = null;
            bool mdControllerFound = false;
            string mdjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mdControllers.json");
            bool needMDActivationSwitch = false;
            bool md_pad = Program.SystemConfig.getOptBoolean("md_pad");

            if (File.Exists(mdjson) && _mdSystems.Contains(system))
            {
                try
                {
                    var mdControllers = MegadriveController.LoadControllersFromJson(mdjson);
                    if (mdControllers != null)
                    {
                        mdGamepad = MegadriveController.GetMDController("bizhawk", guid, mdControllers);

                        if (mdGamepad != null)
                        {
                            if (mdGamepad.ControllerInfo != null)
                            {
                                if (mdGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needMDActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needMDActivationSwitch && !md_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific MD mapping needs to be activated for this controller.");
                                    goto BypassMDControllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + mdGamepad.Name);

                            bool useDInput = false;

                            if (mdGamepad.ControllerInfo != null)
                            {
                                if (mdGamepad.ControllerInfo.ContainsKey("dinput"))
                                    useDInput = mdGamepad.ControllerInfo["dinput"] == "true";
                            }

                            if (mdGamepad.Mapping != null)
                            {
                                foreach (var x in mdGamepad.Mapping)
                                {
                                    if (x.Value.Contains("_"))
                                    {
                                        string key = x.Key;
                                        var values = x.Value.Split('_');
                                        string value1 = values[0];
                                        string value2 = values[1];
                                        controllerConfig["P" + playerIndex + " " + key] = useDInput ? "J" + index + " " + value1 + ", " + "J" + index + " " + value2 : "X" + index + " " + value1 + ", " + "X" + index + " " + value2;
                                    }
                                    else
                                    {
                                        string key = x.Key;
                                        string value = x.Value;
                                        controllerConfig["P" + playerIndex + " " + key] = useDInput ? "J" + index + " " + value : "X" + index + " " + value;
                                    }
                                }
                                mdControllerFound = true;
                            }

                            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
                            return;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Megadrive controller.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            BypassMDControllers:

            // Special treatment for Saturn controllers
            MegadriveController saturnGamepad;
            bool saturnControllerFound = false;
            bool needSATActivationSwitch = false;
            bool sat_pad = Program.SystemConfig.getOptBoolean("saturn_pad");

            string saturnjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "saturnControllers.json");
            if (File.Exists(saturnjson) && system == "saturn")
            {
                try
                {
                    var saturnControllers = MegadriveController.LoadControllersFromJson(saturnjson);
                    if (saturnControllers != null)
                    {
                        saturnGamepad = MegadriveController.GetMDController("bizhawk", guid, saturnControllers);

                        if (saturnGamepad != null)
                        {
                            if (mdGamepad.ControllerInfo != null)
                            {
                                if (mdGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needSATActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needSATActivationSwitch && !sat_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific Saturn mapping needs to be activated for this controller.");
                                    goto BypassSATControllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + saturnGamepad.Name);

                            bool useDInput = false;

                            if (saturnGamepad.ControllerInfo != null)
                            {
                                if (saturnGamepad.ControllerInfo.ContainsKey("dinput"))
                                    useDInput = saturnGamepad.ControllerInfo["dinput"] == "true";
                            }

                            if (saturnGamepad.Mapping != null)
                            {
                                foreach (var x in saturnGamepad.Mapping)
                                {
                                    string key = x.Key;
                                    string value = x.Value;
                                    controllerConfig["P" + playerIndex + " " + key] = useDInput ? "J" + index + " " + value : "X" + index + " " + value;
                                }
                                saturnControllerFound = true;
                            }

                            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
                            return;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Saturn controller.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            BypassSATControllers:
            #endregion

            if (!n64ControllerFound && !mdControllerFound && !saturnControllerFound)
            {
                mapping = ConfigureMappingPerSystem(mapping, system, core);
                foreach (var x in mapping)
                {
                    string value = x.Value;
                    InputKey key = x.Key;

                    if (!monoplayer)
                    {
                        if (isDInput)
                            controllerConfig["P" + playerIndex + " " + value] = "J" + index + " " + GetDInputKeyName(controller, key);
                        else
                            controllerConfig["P" + playerIndex + " " + value] = "X" + index + " " + GetXInputKeyName(controller, key);
                    }
                    else
                    {
                        if (isDInput)
                            controllerConfig[value] = "J" + index + " " + GetDInputKeyName(controller, key);
                        else
                            controllerConfig[value] = "X" + index + " " + GetXInputKeyName(controller, key);
                    }
                }
            }

            // Specifics
            if (system == "atari2600" || system == "atari7800")
            {
                controllerConfig["Power"] = "";
                
                if (isDInput)
                    controllerConfig["Reset"] = "J" + index + " " + GetDInputKeyName(controller, InputKey.select);
                else
                    controllerConfig["Reset"] = "X" + index + " " + GetXInputKeyName(controller, InputKey.select);
                
                if (isDInput)
                    controllerConfig["Select"] = "J" + index + " " + GetDInputKeyName(controller, InputKey.start);
                else
                    controllerConfig["Select"] = "X" + index + " " + GetXInputKeyName(controller, InputKey.start);
                
                controllerConfig["Toggle Left Difficulty"] = "";
                controllerConfig["Toggle Right Difficulty"] = "";

                if (system == "atari7800")
                {
                    controllerConfig["BW"] = "";
                    controllerConfig["Pause"] = "";
                }
            }

            if (system == "channelf")
            {
                controllerConfig["TIME"] = isDInput? "J" + index + " " + GetDInputKeyName(controller, InputKey.l3) : "X" + index + " " + GetXInputKeyName(controller, InputKey.l3);
                controllerConfig["MODE"] = isDInput ? "J" + index + " " + GetDInputKeyName(controller, InputKey.r3) : "X" + index + " " + GetXInputKeyName(controller, InputKey.r3);
                controllerConfig["HOLD"] = isDInput ? "J" + index + " " + GetDInputKeyName(controller, InputKey.select) : "X" + index + " " + GetXInputKeyName(controller, InputKey.select);
                controllerConfig["START"] = isDInput ? "J" + index + " " + GetDInputKeyName(controller, InputKey.start) : "X" + index + " " + GetXInputKeyName(controller, InputKey.start);
                controllerConfig["RESET"] = "Backspace";

                if (!isDInput)
                {
                    if (!string.IsNullOrEmpty(GetXInputKeyName(controller, InputKey.leftanalogup)))
                    {
                        controllerConfig["P" + playerIndex + " Forward"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.leftanalogup);
                        controllerConfig["P" + playerIndex + " Back"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.leftanalogdown);
                        controllerConfig["P" + playerIndex + " Left"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.leftanalogleft);
                        controllerConfig["P" + playerIndex + " Right"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.leftanalogright);
                    }

                    if (!string.IsNullOrEmpty(GetXInputKeyName(controller, InputKey.rightanalogup)))
                    {
                        controllerConfig["P" + playerIndex + " CCW"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.rightanalogleft);
                        controllerConfig["P" + playerIndex + " CW"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.rightanalogright);
                        controllerConfig["P" + playerIndex + " Pull"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.rightanalogup);
                        controllerConfig["P" + playerIndex + " Push"] += ", " + "X" + playerIndex + " " + GetXInputKeyName(controller, InputKey.rightanalogdown);
                    }
                }
            }

            if (system == "colecovision")
            {
                for (int i = 1; i < 3; i++)
                {
                    controllerConfig["P" + i + " Key 0"] = "";
                    controllerConfig["P" + i + " Key 9"] = "";
                }
            }

            if (system == "jaguar")
            {
                for (int i = 1; i < 3; i++)
                {
                    controllerConfig["P" + i + " 7"] = "";
                    controllerConfig["P" + i + " 8"] = "";
                    controllerConfig["P" + i + " 9"] = "";
                    controllerConfig["P" + i + " Asterisk"] = "";
                    controllerConfig["P" + i + " Pound"] = "";
                }
                controllerConfig["Power"] = "";
            }

            if (system == "mastersystem" || system == "sg1000" || system == "multivision")
            {
                if (isDInput)
                    controllerConfig["Pause"] = "J" + index + " " + GetDInputKeyName(controller, InputKey.select);
                else
                    controllerConfig["Pause"] = "X" + index + " " + GetXInputKeyName(controller, InputKey.start);
            }

            if (system == "pcfx")
            {
                if (isDInput)
                {
                    controllerConfig["P1 Mode 1: Set A"] = "J" + index + " Y AxisLeft";
                    controllerConfig["P1 Mode 1: Set B"] = "J" + index + " Y AxisRight";
                    controllerConfig["P1 Mode 2: Set A"] = "J" + index + " Y AxisUp";
                    controllerConfig["P1 Mode 2: Set B"] = "J" + index + " Y AxisDown";
                }
                else
                {
                    controllerConfig["P1 Mode 1: Set A"] = "X" + index + " RStickLeft";
                    controllerConfig["P1 Mode 1: Set B"] = "X" + index + " RStickRight";
                    controllerConfig["P1 Mode 2: Set A"] = "X" + index + " RStickUp";
                    controllerConfig["P1 Mode 2: Set B"] = "X" + index + " RStickDown";
                }
            }

            if (system == "psx" && !isDInput)
                controllerConfig["P" + playerIndex + " Analog"] = "X" + index + " Guide";

            if (system == "tic80")
            {
                controllerConfig["Mouse Left Click"] = "WMouse L";
                controllerConfig["Mouse Middle Click"] = "WMouse M";
                controllerConfig["Mouse Right Click"] = "WMouse R";
            }

            // Configure analog part of .ini
            var analog = json.GetOrCreateContainer("AllTrollersAnalog");
            var analogConfig = analog.GetOrCreateContainer(systemController[system]);

            string deadzone = "0.15";

            if (SystemConfig.isOptSet("bizhawk_deadzone") && !string.IsNullOrEmpty(SystemConfig["bizhawk_deadzone"]))
            {
                deadzone = SystemConfig["bizhawk_deadzone"].TrimEnd('0');
            }

            if (system == "n64")
            {
                bool revertXAxis = false;
                bool revertYAxis = false;
                bool useDInput = false;
                var xAxis = analogConfig.GetOrCreateContainer("P" + playerIndex + " X Axis");
                var yAxis = analogConfig.GetOrCreateContainer("P" + playerIndex + " Y Axis");

                if (n64Gamepad != null && n64Gamepad.ControllerInfo != null)
                {
                    if (n64Gamepad.ControllerInfo.ContainsKey("XInvert"))
                        revertXAxis = n64Gamepad.ControllerInfo["XInvert"] == "true";
                    if (n64Gamepad.ControllerInfo.ContainsKey("YInvert"))
                        revertYAxis = n64Gamepad.ControllerInfo["YInvert"] == "true";
                    if (n64Gamepad.ControllerInfo.ContainsKey("dinput"))
                        useDInput = n64Gamepad.ControllerInfo["dinput"] == "true";
                }

                xAxis["Value"] = useDInput ? "J" + index + " X Axis" : "X" + index + " LeftThumbX Axis";
                xAxis.SetObject("Mult", revertXAxis ? -1.0 : 1.0);
                xAxis.SetObject("Deadzone", deadzone);

                yAxis["Value"] = useDInput ? "J" + index + " Y Axis" : "X" + index + " LeftThumbY Axis";
                yAxis.SetObject("Mult", revertYAxis ? -1.0 : 1.0);
                yAxis.SetObject("Deadzone", deadzone);
            }

            if (system == "3ds")
            {
                var cPadX = analogConfig.GetOrCreateContainer("Circle Pad X");
                var cPadY = analogConfig.GetOrCreateContainer("Circle Pad Y");
                var cStickX = analogConfig.GetOrCreateContainer("C-Stick X");
                var cStickY = analogConfig.GetOrCreateContainer("C-Stick Y");
                var touchX = analogConfig.GetOrCreateContainer("Touch X");
                var touchY = analogConfig.GetOrCreateContainer("Touch Y");
                var tiltX = analogConfig.GetOrCreateContainer("Tilt X");
                var tiltY = analogConfig.GetOrCreateContainer("Tilt Y");

                cPadX["Value"] = "X" + index + " LeftThumbX Axis"; ;
                cPadX.SetObject("Mult", 1.0);
                cPadX.SetObject("Deadzone", deadzone);

                cPadY["Value"] = "X" + index + " LeftThumbY Axis";
                cPadY.SetObject("Mult", 1.0);
                cPadY.SetObject("Deadzone", deadzone);

                if (Program.SystemConfig.isOptSet("bizhawk3ds_analog_function"))
                {
                    string layout = Program.SystemConfig["bizhawk3ds_analog_function"];
                    switch (layout)
                    {
                        case "C-Stick and Touchscreen Pointer":
                            if (isDInput)
                                controllerConfig["Touch"] = "J" + index + " " + GetDInputKeyName(controller, InputKey.l3);
                            else
                                controllerConfig["Touch"] = "X" + index + " " + GetXInputKeyName(controller, InputKey.l3);

                            cStickX["Value"] = "X" + index + " RightThumbX Axis";
                            cStickX.SetObject("Mult", 1.0);
                            cStickX.SetObject("Deadzone", deadzone);

                            cStickY["Value"] = "X" + index + " RightThumbY Axis";
                            cStickY.SetObject("Mult", 1.0);
                            cStickY.SetObject("Deadzone", deadzone);

                            touchX["Value"] = "X" + index + " RightThumbX Axis";
                            touchX.SetObject("Mult", 1.0);
                            touchX.SetObject("Deadzone", deadzone);

                            touchY["Value"] = "X" + index + " RightThumbX Axis";
                            touchY.SetObject("Mult", 1.0);
                            touchY.SetObject("Deadzone", deadzone);
                            break;
                        
                        case "Touchscreen Pointer":
                            if (isDInput)
                                controllerConfig["Touch"] = "J" + index + " " + GetDInputKeyName(controller, InputKey.l3);
                            else
                                controllerConfig["Touch"] = "X" + index + " " + GetXInputKeyName(controller, InputKey.l3);

                            cStickX["Value"] = "";
                            cStickX.SetObject("Mult", 1.0);
                            cStickX.SetObject("Deadzone", 0.0);

                            cStickY["Value"] = "";
                            cStickY.SetObject("Mult", 1.0);
                            cStickY.SetObject("Deadzone", 0.0);

                            touchX["Value"] = "X" + index + " RightThumbX Axis";
                            touchX.SetObject("Mult", 1.0);
                            touchX.SetObject("Deadzone", deadzone);

                            touchY["Value"] = "X" + index + " RightThumbX Axis";
                            touchY.SetObject("Mult", 1.0);
                            touchY.SetObject("Deadzone", deadzone);
                            break;

                        case "C-Stick":
                            if (isDInput)
                                controllerConfig["Touch"] = "J" + index + " " + GetDInputKeyName(controller, InputKey.l3);
                            else
                                controllerConfig["Touch"] = "X" + index + " " + GetXInputKeyName(controller, InputKey.l3);

                            cStickX["Value"] = "X" + index + " RightThumbX Axis";
                            cStickX.SetObject("Mult", 1.0);
                            cStickX.SetObject("Deadzone", deadzone);

                            cStickY["Value"] = "X" + index + " RightThumbY Axis";
                            cStickY.SetObject("Mult", 1.0);
                            cStickY.SetObject("Deadzone", deadzone);

                            touchX["Value"] = "WMouse X";
                            touchX.SetObject("Mult", 1.0);
                            touchX.SetObject("Deadzone", 0.0);

                            touchY["Value"] = "WMouse Y";
                            touchY.SetObject("Mult", 1.0);
                            touchY.SetObject("Deadzone", 0.0);
                            break;
                    }
                }
                else
                {
                    cPadX["Value"] = "X" + index + " LeftThumbX Axis"; ;
                    cPadX.SetObject("Mult", 1.0);
                    cPadX.SetObject("Deadzone", deadzone);

                    cPadY["Value"] = "X" + index + " LeftThumbY Axis";
                    cPadY.SetObject("Mult", 1.0);
                    cPadY.SetObject("Deadzone", deadzone);

                    cStickX["Value"] = "X" + index + " RightThumbX Axis";
                    cStickX.SetObject("Mult", 1.0);
                    cStickX.SetObject("Deadzone", deadzone);

                    cStickY["Value"] = "X" + index + " RightThumbY Axis";
                    cStickY.SetObject("Mult", 1.0);
                    cStickY.SetObject("Deadzone", deadzone);

                    touchX["Value"] = "WMouse X";
                    touchX.SetObject("Mult", 1.0);
                    touchX.SetObject("Deadzone", 0.0);

                    touchY["Value"] = "WMouse Y";
                    touchY.SetObject("Mult", 1.0);
                    touchY.SetObject("Deadzone", 0.0);

                    controllerConfig["Touch"] = "WMouse L";
                    controllerConfig["Tilt"] = "WMouse R";
                }
                
                tiltX["Value"] = "WMouse X";
                tiltX.SetObject("Mult", 1.0);
                tiltX.SetObject("Deadzone", 0.0);

                tiltY["Value"] = "WMouse Y";
                tiltY.SetObject("Mult", 1.0);
                tiltY.SetObject("Deadzone", 0.0);

                controllerConfig["Tilt"] = "WMouse R";
            }

            if (system == "intellivision")
            {
                var discx = analogConfig.GetOrCreateContainer("P" + playerIndex + " Disc X");
                var discy = analogConfig.GetOrCreateContainer("P" + playerIndex + " Disc Y");

                discx["Value"] = "X" + index + " LeftThumbX Axis";
                discx.SetObject("Mult", 1.0);
                discx.SetObject("Deadzone", deadzone);

                discy["Value"] = "X" + index + " LeftThumbY Axis";
                discy.SetObject("Mult", 1.0);
                discy.SetObject("Deadzone", deadzone);

                if (Program.SystemConfig.getOptBoolean("bizhawk_intv_swapcontrols"))
                {
                    var discx1 = analogConfig.GetOrCreateContainer("P1 Disc X");
                    var discy1 = analogConfig.GetOrCreateContainer("P1 Disc Y");
                    discx1["Value"] = "";
                    discy1["Value"] = "";
                }
                else
                {
                    var discx2 = analogConfig.GetOrCreateContainer("P2 Disc X");
                    var discy2 = analogConfig.GetOrCreateContainer("P2 Disc Y");
                    discx2["Value"] = "";
                    discy2["Value"] = "";
                }
            }

            if (system == "nds")
            {
                var xAxis = analogConfig.GetOrCreateContainer("Touch X");
                var yAxis = analogConfig.GetOrCreateContainer("Touch Y");

                if (SystemConfig.getOptBoolean("bizhawk_nds_mouse"))
                {
                    controllerConfig["Touch"] = "WMouse L";

                    xAxis["Value"] = "WMouse X";
                    xAxis.SetObject("Mult", 1.0);
                    xAxis.SetObject("Deadzone", 0.0);

                    yAxis["Value"] = "WMouse Y";
                    yAxis.SetObject("Mult", 1.0);
                    yAxis.SetObject("Deadzone", 0.0);
                }

                else
                {
                    xAxis["Value"] = "X" + index + " RightThumbX Axis";
                    xAxis.SetObject("Mult", 1.0);
                    xAxis.SetObject("Deadzone", 0.0);

                    yAxis["Value"] = "X" + index + " RightThumbY Axis";
                    yAxis.SetObject("Mult", -1.0);
                    yAxis.SetObject("Deadzone", 0.0);
                }
            }

            if (system == "psx")
            {
                var lStickH = analogConfig.GetOrCreateContainer("P" + playerIndex + " Left Stick Left / Right");
                var lStickV = analogConfig.GetOrCreateContainer("P" + playerIndex + " Left Stick Up / Down");
                var rStickH = analogConfig.GetOrCreateContainer("P" + playerIndex + " Right Stick Left / Right");
                var rStickV = analogConfig.GetOrCreateContainer("P" + playerIndex + " Right Stick Up / Down");

                lStickH["Value"] = "X" + index + " LeftThumbX Axis";
                lStickH.SetObject("Mult", 1.0);
                lStickH.SetObject("Deadzone", deadzone);

                lStickV["Value"] = "X" + index + " LeftThumbY Axis";
                lStickV.SetObject("Mult", 1.0);
                lStickV.SetObject("Deadzone", deadzone);

                rStickH["Value"] = "X" + index + " RightThumbX Axis";
                rStickH.SetObject("Mult", 1.0);
                rStickH.SetObject("Deadzone", deadzone);

                rStickV["Value"] = "X" + index + " RightThumbY Axis";
                rStickV.SetObject("Mult", 1.0);
                rStickV.SetObject("Deadzone", deadzone);
            }

            if (system == "tic80")
            {
                controllerConfig["Mouse Left Click"] = "WMouse L";
                controllerConfig["Mouse Middle Click"] = "WMouse M";
                controllerConfig["Mouse Right Click"] = "WMouse R";

                var mouseX = analogConfig.GetOrCreateContainer("Mouse Position X");
                var mouseY = analogConfig.GetOrCreateContainer("Mouse Position Y");
                mouseX["Value"] = "WMouse X";
                mouseX.SetObject("Mult", 1.0);
                mouseX.SetObject("Deadzone", 0.0);
                mouseY["Value"] = "WMouse Y";
                mouseY.SetObject("Mult", 1.0);
                mouseY.SetObject("Deadzone", 0.0);
            }

            if (core == "Cygne")
            {
                controllerConfig["P2 X1"] = GetXInputKeyName(controller, InputKey.y);
                controllerConfig["P2 X3"] = GetXInputKeyName(controller, InputKey.b);
                controllerConfig["P2 X4"] = GetXInputKeyName(controller, InputKey.a);
                controllerConfig["P2 X2"] = GetXInputKeyName(controller, InputKey.x);
                controllerConfig["P2 Y1"] = GetXInputKeyName(controller, InputKey.left);
                controllerConfig["P2 Y3"] = GetXInputKeyName(controller, InputKey.right);
                controllerConfig["P2 Y4"] = GetXInputKeyName(controller, InputKey.down);
                controllerConfig["P2 Y2"] = GetXInputKeyName(controller, InputKey.up);
                controllerConfig["P2 Start"] = GetXInputKeyName(controller, InputKey.start);
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
        }

        private static void ConfigureKeyboard(Controller controller, DynamicJson json, string system, string core)
        {
            if (controller == null)
                return;

            if (controller.PlayerIndex != 1)
                return;

            InputConfig keyboard = controller.Config;
            if (keyboard == null)
                return;

            json["InputHotkeyOverrideOptions"] = "1";
            
            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);
            var analog = json.GetOrCreateContainer("AllTrollersAnalog");
            var analogConfig = analog.GetOrCreateContainer(systemController[system]);
            bool monoplayer = systemMonoPlayer.Contains(system);

            // Define mapping to use
            var mapping = mappingToUse[system];

            // Specific cases
            if (system == "psx")
            {
                if (core == "Nymashock" && Program.SystemConfig.isOptSet("bizhawk_psx_digital") && Program.SystemConfig.getOptBoolean("bizhawk_psx_digital"))
                    mapping = psxNymaMapping;
                else if (core == "Nymashock")
                    mapping = dualshockNymaMapping;
                else if (Program.SystemConfig.isOptSet("bizhawk_psx_digital") && Program.SystemConfig.getOptBoolean("bizhawk_psx_digital"))
                    mapping = psxOctoMapping;
            }

            if (system == "sgb" && core == "BSNES")
            {
                mapping = snesMapping;
                controllerConfig = trollers.GetOrCreateContainer(systemController["snes"]);
            }

            if (core == "GBHawk")
            {
                monoplayer = false;
                controllerConfig = trollers.GetOrCreateContainer("Gameboy Controller H");
            }

            if (core == "VirtualBoyee")
                mapping = vbKbMapping;

            // Perform mapping
            foreach (var x in mapping)
            {
                string value = x.Value;
                var a = keyboard[x.Key];
                if (a != null)
                {
                    if (monoplayer)
                        controllerConfig[value] = SdlToKeyCode(a.Id);
                    else
                        controllerConfig["P1 " + value] = SdlToKeyCode(a.Id);
                }
            }

            if (system == "atari2600" || system == "atari7800")
            {
                controllerConfig["Reset"] = "";
                controllerConfig["Toggle Left Difficulty"] = "";
                controllerConfig["Toggle Right Difficulty"] = "";
                controllerConfig["Power"] = "";
                controllerConfig["Select"] = SdlToKeyCode(keyboard[InputKey.start].Id);

                if (system == "atari7800")
                {
                    controllerConfig["BW"] = "";
                    controllerConfig["Pause"] = SdlToKeyCode(keyboard[InputKey.select].Id);
                }
            }

            if (system == "channelf")
            {
                controllerConfig["TIME"] = "T";
                controllerConfig["MODE"] = "M";
                controllerConfig["HOLD"] = "Space";
                controllerConfig["START"] = "Enter";
                controllerConfig["RESET"] = "Backspace";

                if (Program.SystemConfig.getOptBoolean("bizhawk_swapcontrols"))
                {
                    controllerConfig["P1 Forward"] = "Keypad8";
                    controllerConfig["P1 Back"] = "Keypad2";
                    controllerConfig["P1 Left"] = "Keypad4";
                    controllerConfig["P1 Right"] = "Keypad6";
                    controllerConfig["P1 CCW"] = "Left";
                    controllerConfig["P1 CW"] = "Right";
                    controllerConfig["P1 Pull"] = "Up";
                    controllerConfig["P1 Push"] = "Down";
                    controllerConfig["P2 Forward"] = "W";
                    controllerConfig["P2 Back"] = "S";
                    controllerConfig["P2 Left"] = "A";
                    controllerConfig["P2 Right"] = "D";
                    controllerConfig["P2 CCW"] = "J";
                    controllerConfig["P2 CW"] = "L";
                    controllerConfig["P2 Pull"] = "I";
                    controllerConfig["P2 Push"] = "K";
                }
                else
                {
                    controllerConfig["P1 Forward"] = "W";
                    controllerConfig["P1 Back"] = "S";
                    controllerConfig["P1 Left"] = "A";
                    controllerConfig["P1 Right"] = "D";
                    controllerConfig["P1 CCW"] = "J";
                    controllerConfig["P1 CW"] = "L";
                    controllerConfig["P1 Pull"] = "I";
                    controllerConfig["P1 Push"] = "K";
                    controllerConfig["P2 Forward"] = "Keypad8";
                    controllerConfig["P2 Back"] = "Keypad2";
                    controllerConfig["P2 Left"] = "Keypad4";
                    controllerConfig["P2 Right"] = "Keypad6";
                    controllerConfig["P2 CCW"] = "Left";
                    controllerConfig["P2 CW"] = "Right";
                    controllerConfig["P2 Pull"] = "Up";
                    controllerConfig["P2 Push"] = "Down";
                }
            }

            if (system == "colecovision")
            {
                controllerConfig["P1 Key 0"] = "Number0";
                controllerConfig["P1 Key 1"] = "Number1";
                controllerConfig["P1 Key 2"] = "Number2";
                controllerConfig["P1 Key 3"] = "Number3";
                controllerConfig["P1 Key 4"] = "Number4";
                controllerConfig["P1 Key 5"] = "Number5";
                controllerConfig["P1 Key 6"] = "Number6";
                controllerConfig["P1 Key 7"] = "Number7";
                controllerConfig["P1 Key 8"] = "Number8";
                controllerConfig["P1 Key 9"] = "Number9";
                controllerConfig["P1 Star"] = "Minus";
                controllerConfig["P1 Pound"] = "Plus";
                controllerConfig["P2 Star"] = "";
                controllerConfig["P2 Pound"] = "";
            }

            if (system == "intellivision")
            {
                if (Program.SystemConfig.getOptBoolean("bizhawk_swapcontrols"))
                {
                    controllerConfig["P2 Key 0"] = "Number0";
                    controllerConfig["P2 Key 1"] = "Number1";
                    controllerConfig["P2 Key 2"] = "Number2";
                    controllerConfig["P2 Key 3"] = "Number3";
                    controllerConfig["P2 Key 4"] = "Number4";
                    controllerConfig["P2 Key 5"] = "Number5";
                    controllerConfig["P2 Key 6"] = "Number6";
                    controllerConfig["P2 Key 7"] = "Number7";
                    controllerConfig["P2 Key 8"] = "Number8";
                    controllerConfig["P2 Key 9"] = "Number9";
                    controllerConfig["P2 Enter"] = "Enter";
                    controllerConfig["P2 Clear"] = "Backspace";

                    var discx = analogConfig.GetOrCreateContainer("P2 Disc X");
                    var discy = analogConfig.GetOrCreateContainer("P2 Disc Y");

                    discx["Value"] = "WMouse X";
                    discx.SetObject("Mult", 1.0);
                    discx.SetObject("Deadzone", 0.0);

                    discy["Value"] = "WMouse Y";
                    discy.SetObject("Mult", 1.0);
                    discy.SetObject("Deadzone", 0.0);

                    var discx1 = analogConfig.GetOrCreateContainer("P1 Disc X");
                    var discy1 = analogConfig.GetOrCreateContainer("P1 Disc Y");
                    discx1["Value"] = "";
                    discy1["Value"] = "";
                }
                else
                {
                    controllerConfig["P1 Key 0"] = "Number0";
                    controllerConfig["P1 Key 1"] = "Number1";
                    controllerConfig["P1 Key 2"] = "Number2";
                    controllerConfig["P1 Key 3"] = "Number3";
                    controllerConfig["P1 Key 4"] = "Number4";
                    controllerConfig["P1 Key 5"] = "Number5";
                    controllerConfig["P1 Key 6"] = "Number6";
                    controllerConfig["P1 Key 7"] = "Number7";
                    controllerConfig["P1 Key 8"] = "Number8";
                    controllerConfig["P1 Key 9"] = "Number9";
                    controllerConfig["P1 Enter"] = "Enter";
                    controllerConfig["P1 Clear"] = "Backspace";

                    var discx = analogConfig.GetOrCreateContainer("P1 Disc X");
                    var discy = analogConfig.GetOrCreateContainer("P1 Disc Y");

                    discx["Value"] = "WMouse X";
                    discx.SetObject("Mult", 1.0);
                    discx.SetObject("Deadzone", 0.0);

                    discy["Value"] = "WMouse Y";
                    discy.SetObject("Mult", 1.0);
                    discy.SetObject("Deadzone", 0.0);

                    var discx2 = analogConfig.GetOrCreateContainer("P2 Disc X");
                    var discy2 = analogConfig.GetOrCreateContainer("P2 Disc Y");
                    discx2["Value"] = "";
                    discy2["Value"] = "";
                }
            }

            if (system == "jaguar")
            {
                controllerConfig["P1 0"] = "Number0";
                controllerConfig["P1 1"] = "Number1";
                controllerConfig["P1 2"] = "Number2";
                controllerConfig["P1 3"] = "Number3";
                controllerConfig["P1 4"] = "Number4";
                controllerConfig["P1 5"] = "Number5";
                controllerConfig["P1 6"] = "Number6";
                controllerConfig["P1 7"] = "Number7";
                controllerConfig["P1 8"] = "Number8";
                controllerConfig["P1 9"] = "Number9";
                controllerConfig["P1 Asterisk"] = "Minus";
                controllerConfig["P1 Pound"] = "Plus";
                controllerConfig["P2 7"] = "";
                controllerConfig["P2 8"] = "";
                controllerConfig["P2 9"] = "";
                controllerConfig["P2 Asterisk"] = "";
                controllerConfig["P2 Pound"] = "";
            }

            if (system == "3ds")
            {
                var cPadX = analogConfig.GetOrCreateContainer("Circle Pad X");
                var cPadY = analogConfig.GetOrCreateContainer("Circle Pad Y");
                var cX = analogConfig.GetOrCreateContainer("C-Stick X");
                var cY = analogConfig.GetOrCreateContainer("C-Stick Y");
                var touchX = analogConfig.GetOrCreateContainer("Touch X");
                var touchY = analogConfig.GetOrCreateContainer("Touch Y");
                var tiltX = analogConfig.GetOrCreateContainer("Tilt X");
                var tiltY = analogConfig.GetOrCreateContainer("Tilt Y");

                cPadX["Value"] = "";
                cPadX.SetObject("Mult", 1.0);
                cPadX.SetObject("Deadzone", 0.0);

                cPadY["Value"] = "";
                cPadY.SetObject("Mult", 1.0);
                cPadY.SetObject("Deadzone", 0.0);

                cX["Value"] = "";
                cX.SetObject("Mult", 1.0);
                cX.SetObject("Deadzone", 0.0);

                cY["Value"] = "";
                cY.SetObject("Mult", 1.0);
                cY.SetObject("Deadzone", 0.0);

                touchX["Value"] = "WMouse X";
                touchX.SetObject("Mult", 1.0);
                touchX.SetObject("Deadzone", 0.0);

                touchY["Value"] = "WMouse Y";
                touchY.SetObject("Mult", 1.0);
                touchY.SetObject("Deadzone", 0.0);

                tiltX["Value"] = "WMouse X";
                tiltX.SetObject("Mult", 1.0);
                tiltX.SetObject("Deadzone", 0.0);

                tiltY["Value"] = "WMouse Y";
                tiltY.SetObject("Mult", 1.0);
                tiltY.SetObject("Deadzone", 0.0);

                controllerConfig["Touch"] = "WMouse L";
                controllerConfig["Tilt"] = "WMouse R";
            }

            if (system == "nds")
            {
                var xAxis = analogConfig.GetOrCreateContainer("Touch X");
                var yAxis = analogConfig.GetOrCreateContainer("Touch Y");

                xAxis["Value"] = "WMouse X";
                xAxis.SetObject("Mult", 1.0);
                xAxis.SetObject("Deadzone", 0.0);

                yAxis["Value"] = "WMouse Y";
                yAxis.SetObject("Mult", 1.0);
                yAxis.SetObject("Deadzone", 0.0);

                controllerConfig["Touch"] = "WMouse L";
            }

            if (system == "tic80")
            {
                controllerConfig["Mouse Left Click"] = "WMouse L";
                controllerConfig["Mouse Middle Click"] = "WMouse M";
                controllerConfig["Mouse Right Click"] = "WMouse R";

                var mouseX = analogConfig.GetOrCreateContainer("Mouse Position X");
                var mouseY = analogConfig.GetOrCreateContainer("Mouse Position Y");
                mouseX["Value"] = "WMouse X";
                mouseX.SetObject("Mult", 1.0);
                mouseX.SetObject("Deadzone", 0.0);
                mouseY["Value"] = "WMouse Y";
                mouseY.SetObject("Mult", 1.0);
                mouseY.SetObject("Deadzone", 0.0);
            }

            if (core == "Cygne")
            {
                controllerConfig["P2 X1"] = SdlToKeyCode(keyboard[InputKey.y].Id);
                controllerConfig["P2 X3"] = SdlToKeyCode(keyboard[InputKey.b].Id);
                controllerConfig["P2 X4"] = SdlToKeyCode(keyboard[InputKey.a].Id);
                controllerConfig["P2 X2"] = SdlToKeyCode(keyboard[InputKey.x].Id);
                controllerConfig["P2 Y1"] = SdlToKeyCode(keyboard[InputKey.left].Id);
                controllerConfig["P2 Y3"] = SdlToKeyCode(keyboard[InputKey.right].Id);
                controllerConfig["P2 Y4"] = SdlToKeyCode(keyboard[InputKey.down].Id);
                controllerConfig["P2 Y2"] = SdlToKeyCode(keyboard[InputKey.up].Id);
                controllerConfig["P2 Start"] = SdlToKeyCode(keyboard[InputKey.start].Id);
            }
        }

        private static readonly InputKeyMapping n3dsMapping = new InputKeyMapping()
        {
            { InputKey.b,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.x,                   "X" },
            { InputKey.y,                   "Y" },
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" },
            { InputKey.l2,                  "ZL" },
            { InputKey.r2,                  "ZR" },
            { InputKey.select,              "Select" },
            { InputKey.start,               "Start" }
        };

        private static readonly InputKeyMapping atariMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "Button" }
        };

        private static readonly InputKeyMapping channelfMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Forward"},
            { InputKey.down,                "Back"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.y,                   "CCW" },
            { InputKey.b,                   "CW" },
            { InputKey.x,                   "Pull" },
            { InputKey.a,                   "Push" }
        };

        private static readonly InputKeyMapping colecoMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.a,                   "L" },
            { InputKey.b,                   "R" },
            { InputKey.x,                   "Key 1" },
            { InputKey.y,                   "Key 2" },
            { InputKey.pagedown,            "Key 3" },
            { InputKey.pageup,              "Key 4" },
            { InputKey.r2,                  "Key 5" },
            { InputKey.l2,                  "Key 6" },
            { InputKey.r3,                  "Key 7" },
            { InputKey.l3,                  "Key 8" },
            { InputKey.select,              "Star" },
            { InputKey.start,               "Pound" }
        };

        private static readonly InputKeyMapping dualshockNymaMapping = new InputKeyMapping()
        {
            { InputKey.up,              "D-Pad Up"},
            { InputKey.down,            "D-Pad Down"},
            { InputKey.left,            "D-Pad Left" },
            { InputKey.right,           "D-Pad Right"},
            { InputKey.a,               "X" },
            { InputKey.b,               "○" },
            { InputKey.y,               "□" },
            { InputKey.x,               "△" },
            { InputKey.pageup,          "L1" },
            { InputKey.pagedown,        "R1" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Start" },
            { InputKey.l2,              "L2" },
            { InputKey.r2,              "R2" },
            { InputKey.l3,              "Left Stick, Button" },
            { InputKey.r3,              "Right Stick, Button" }
        };

        private static readonly InputKeyMapping dualshockNymaGTMapping = new InputKeyMapping()
        {
            { InputKey.up,              "D-Pad Up"},
            { InputKey.down,            "D-Pad Down"},
            { InputKey.left,            "D-Pad Left" },
            { InputKey.right,           "D-Pad Right"},
            { InputKey.a,               "X" },
            { InputKey.b,               "○" },
            { InputKey.y,               "□" },
            { InputKey.x,               "△" },
            { InputKey.pageup,          "L1" },
            { InputKey.pagedown,        "R1" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Start" },
            { InputKey.rightanalogdown, "L2" },
            { InputKey.rightanalogup,   "R2" },
            { InputKey.l3,              "Left Stick, Button" },
            { InputKey.r3,              "Right Stick, Button" }
        };

        private static readonly InputKeyMapping dualshockOctoMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "Cross" },
            { InputKey.b,               "Circle" },
            { InputKey.y,               "Square" },
            { InputKey.x,               "Triangle" },
            { InputKey.pageup,          "L1" },
            { InputKey.pagedown,        "R1" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Start" },
            { InputKey.l2,              "L2" },
            { InputKey.r2,              "R2" },
            { InputKey.l3,              "L3" },
            { InputKey.r3,              "R3" }
        };

        private static readonly InputKeyMapping dualshockOctoGTMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "Cross" },
            { InputKey.b,               "Circle" },
            { InputKey.y,               "Square" },
            { InputKey.x,               "Triangle" },
            { InputKey.pageup,          "L1" },
            { InputKey.pagedown,        "R1" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Start" },
            { InputKey.rightanalogdown, "L2" },
            { InputKey.rightanalogup,   "R2" },
            { InputKey.l3,              "L3" },
            { InputKey.r3,              "R3" }
        };

        private static readonly InputKeyMapping gbMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.a,               "B" },
            { InputKey.b,               "A" }
        };

        private static readonly InputKeyMapping gbaMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.a,               "B" },
            { InputKey.b,               "A" },
            { InputKey.pageup,          "L" },
            { InputKey.pagedown,        "R" }
        };

        private static readonly InputKeyMapping ggMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "B1" },
            { InputKey.b,               "B2" },
            { InputKey.start,           "Start" }
        };

        private static readonly InputKeyMapping intvMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "L" },
            { InputKey.b,               "R" },
            { InputKey.start,           "Enter" },
            { InputKey.select,          "Clear" },
            { InputKey.y,               "Top" },
            { InputKey.x,               "Key 0" },
            { InputKey.pageup,          "Key 1" },
            { InputKey.rightanalogup,   "Key 2" },
            { InputKey.pagedown,        "Key 3" },
            { InputKey.rightanalogleft, "Key 4" },
            { InputKey.r3,              "Key 5" },
            { InputKey.rightanalogright,"Key 6" },
            { InputKey.l2,              "Key 7" },
            { InputKey.rightanalogdown, "Key 8" },
            { InputKey.r2,              "Key 9" },
        };

        private static readonly InputKeyMapping jaguarMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.b,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.y,                   "C" },
            { InputKey.start,               "Option" },
            { InputKey.select,              "Pause" },
            { InputKey.x,                   "0" },
            { InputKey.pageup,              "1" },
            { InputKey.pagedown,            "2" },
            { InputKey.l2,                  "3" },
            { InputKey.r2,                  "4" },
            { InputKey.l3,                  "5" },
            { InputKey.r3,                  "6" }
        };

        private static readonly InputKeyMapping lynxMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.b,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.pageup,              "Option 1" },
            { InputKey.pagedown,            "Option 2" },
            { InputKey.start,               "Pause" }
        };

        private static readonly InputKeyMapping mdMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.y,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.b,                   "C" },
            { InputKey.start,               "Start" },
            { InputKey.pageup,              "X" },
            { InputKey.x,                   "Y" },
            { InputKey.pagedown,            "Z" },
            { InputKey.select,              "Mode" },
        };

        private static readonly InputKeyMapping mdMapping_lr_zc = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.a,                   "A" },
            { InputKey.b,                   "B" },
            { InputKey.pagedown,            "C" },
            { InputKey.start,               "Start" },
            { InputKey.y,                   "X" },
            { InputKey.x,                   "Y" },
            { InputKey.pageup,              "Z" },
            { InputKey.select,              "Mode" },
        };

        private static readonly InputKeyMapping mdMapping_lr_yz = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.y,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.b,                   "C" },
            { InputKey.start,               "Start" },
            { InputKey.pageup,              "Y" },
            { InputKey.x,                   "X" },
            { InputKey.pagedown,            "Z" },
            { InputKey.select,              "Mode" },
        };

        private static readonly InputKeyMapping n64Mapping = new InputKeyMapping()
        {
            { InputKey.leftanalogup,        "A Up" },
            { InputKey.leftanalogdown,      "A Down" },
            { InputKey.leftanalogleft,      "A Left" },
            { InputKey.leftanalogright,     "A Right" },
            { InputKey.up,                  "DPad U"},
            { InputKey.down,                "DPad D"},
            { InputKey.left,                "DPad L" },
            { InputKey.right,               "DPad R"},
            { InputKey.start,               "Start" },
            { InputKey.r2,                  "Z" },
            { InputKey.y,                   "B" },
            { InputKey.a,                   "A" },
            { InputKey.rightanalogup,       "C Up" },
            { InputKey.rightanalogdown,     "C Down" },
            { InputKey.rightanalogleft,     "C Left" },
            { InputKey.rightanalogright,    "C Right" },
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" }
        };

        private static readonly InputKeyMapping n64Mapping_zl = new InputKeyMapping()
        {
            { InputKey.leftanalogup,        "A Up" },
            { InputKey.leftanalogdown,      "A Down" },
            { InputKey.leftanalogleft,      "A Left" },
            { InputKey.leftanalogright,     "A Right" },
            { InputKey.up,                  "DPad U"},
            { InputKey.down,                "DPad D"},
            { InputKey.left,                "DPad L" },
            { InputKey.right,               "DPad R"},
            { InputKey.start,               "Start" },
            { InputKey.l2,                  "Z" },
            { InputKey.y,                   "B" },
            { InputKey.a,                   "A" },
            { InputKey.rightanalogup,       "C Up" },
            { InputKey.rightanalogdown,     "C Down" },
            { InputKey.rightanalogleft,     "C Left" },
            { InputKey.rightanalogright,    "C Right" },
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" }
        };

        private static readonly InputKeyMapping n64Mapping_xbox = new InputKeyMapping()
        {
            { InputKey.leftanalogup,        "A Up" },
            { InputKey.leftanalogdown,      "A Down" },
            { InputKey.leftanalogleft,      "A Left" },
            { InputKey.leftanalogright,     "A Right" },
            { InputKey.up,                  "DPad U"},
            { InputKey.down,                "DPad D"},
            { InputKey.left,                "DPad L" },
            { InputKey.right,               "DPad R"},
            { InputKey.start,               "Start" },
            { InputKey.l2,                  "Z" },
            { InputKey.b,                   "B" },
            { InputKey.a,                   "A" },
            { InputKey.rightanalogup,       "C Up" },
            { InputKey.rightanalogdown,     "C Down" },
            { InputKey.rightanalogleft,     "C Left" },
            { InputKey.rightanalogright,    "C Right" },
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" }
        };

        private static readonly InputKeyMapping ndsMapping = new InputKeyMapping()
        {
            { InputKey.b,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.x,                   "X" },
            { InputKey.y,                   "Y" },
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" },
            { InputKey.select,              "Select" },
            { InputKey.start,               "Start" },
            { InputKey.r2,                  "Touch" },
        };

        private static readonly InputKeyMapping nesMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.y,               "B" },
            { InputKey.a,               "A" }
        };

        private static readonly InputKeyMapping nesMapping_rotate = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.b,               "A" },
            { InputKey.a,               "B" }
        };

        private static readonly InputKeyMapping ngpMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.b,               "B" },
            { InputKey.a,               "A" },
            { InputKey.start,           "Option"}
        };

        private static readonly InputKeyMapping o2Mapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.b,               "F" }
        };

        private static readonly InputKeyMapping pceMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.b,               "I" },
            { InputKey.a,               "II" },
            { InputKey.y,               "III" },
            { InputKey.x,               "IV" },
            { InputKey.pageup,          "V" },
            { InputKey.pagedown,        "VI" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Run" },
            { InputKey.l2,              "Mode: Set 2-button" },
            { InputKey.r2,              "Mode: Set 6-button" }
        };

        private static readonly InputKeyMapping pcfxMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.b,               "I" },
            { InputKey.a,               "II" },
            { InputKey.x,               "III" },
            { InputKey.y,               "IV" },
            { InputKey.pageup,          "V" },
            { InputKey.pagedown,        "VI" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Run" }
        };

        private static readonly InputKeyMapping psxOctoMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "Cross" },
            { InputKey.b,               "Circle" },
            { InputKey.y,               "Square" },
            { InputKey.x,               "Triangle" },
            { InputKey.pageup,          "L1" },
            { InputKey.pagedown,        "R1" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Start" },
            { InputKey.l2,              "L2" },
            { InputKey.r2,              "R2" }
        };

        private static readonly InputKeyMapping psxNymaMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "X" },
            { InputKey.b,               "○" },
            { InputKey.y,               "□" },
            { InputKey.x,               "△" },
            { InputKey.pageup,          "L1" },
            { InputKey.pagedown,        "R1" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Start" },
            { InputKey.l2,              "L2" },
            { InputKey.r2,              "R2" }
        };

        private static readonly InputKeyMapping saturnMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.start,               "Start" },
            { InputKey.pageup,              "Z" },
            { InputKey.x,                   "Y" },
            { InputKey.pagedown,            "C" },
            { InputKey.y,                   "X" },
            { InputKey.a,                   "A" },
            { InputKey.b,                   "B" },
            { InputKey.l2,                  "L" },
            { InputKey.r2,                  "R" }
        };

        private static readonly InputKeyMapping smsMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "B1" },
            { InputKey.b,               "B2" }
        };

        private static readonly InputKeyMapping smsMapping_rotate = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.y,               "B1" },
            { InputKey.a,               "B2" }
        };

        private static readonly InputKeyMapping snesMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.a,               "B" },
            { InputKey.b,               "A" },
            { InputKey.x,               "X" },
            { InputKey.y,               "Y" },
            { InputKey.pageup,          "L" },
            { InputKey.pagedown,        "R" }
        };

        private static readonly InputKeyMapping snesMapping_invert = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.b,               "B" },
            { InputKey.a,               "A" },
            { InputKey.y,               "X" },
            { InputKey.x,               "Y" },
            { InputKey.pageup,          "L" },
            { InputKey.pagedown,        "R" }
        };

        private static readonly InputKeyMapping tic80Mapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "B" },
            { InputKey.b,               "A" },
            { InputKey.y,               "Y" },
            { InputKey.x,               "X" }
        };

        private static readonly InputKeyMapping vbMapping = new InputKeyMapping()
        {
            { InputKey.leftanalogup,        "L_Up"},
            { InputKey.leftanalogdown,      "L_Down"},
            { InputKey.leftanalogleft,      "L_Left" },
            { InputKey.leftanalogright,     "L_Right"},
            { InputKey.rightanalogup,       "R_Up"},
            { InputKey.rightanalogdown,     "R_Down"},
            { InputKey.rightanalogleft,     "R_Left" },
            { InputKey.rightanalogright,    "R_Right"},
            { InputKey.y,                   "B" },
            { InputKey.a,                   "A" },
            { InputKey.pagedown,            "R" },
            { InputKey.pageup,              "L" },
            { InputKey.select,              "Select" },
            { InputKey.start,               "Start" }
        };

        private static readonly InputKeyMapping vbKbMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "L_Up"},
            { InputKey.down,                "L_Down"},
            { InputKey.left,                "L_Left" },
            { InputKey.right,               "L_Right"},
            { InputKey.rightanalogup,       "R_Up"},
            { InputKey.rightanalogdown,     "R_Down"},
            { InputKey.rightanalogleft,     "R_Left" },
            { InputKey.rightanalogright,    "R_Right"},
            { InputKey.y,                   "B" },
            { InputKey.a,                   "A" },
            { InputKey.pagedown,            "R" },
            { InputKey.pageup,              "L" },
            { InputKey.select,              "Select" },
            { InputKey.start,               "Start" }
        };

        private static readonly InputKeyMapping vecMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.b,               "Button 1" },
            { InputKey.a,               "Button 2" },
            { InputKey.x,               "Button 3" },
            { InputKey.y,               "Button 4" }
        };

        private static readonly InputKeyMapping wswanMapping = new InputKeyMapping()
        {
            { InputKey.up,              "X1"},
            { InputKey.down,            "X3"},
            { InputKey.left,            "X4" },
            { InputKey.right,           "X2"},
            { InputKey.x,               "Y1" },
            { InputKey.a,               "Y3" },
            { InputKey.y,               "Y4" },
            { InputKey.b,               "Y2" },
            { InputKey.start,           "Start" },
            { InputKey.pagedown,        "B" },
            { InputKey.pageup,          "A" }
        };

        private static readonly InputKeyMapping zxMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "Button" }
        };

        private static string GetXInputKeyName(Controller c, InputKey key)
        {
            Int64 pid;
            bool isxinput = c.IsXInputDevice;
            
            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: 
                            return "A";
                        case 1: 
                            return "B";
                        case 2:
                            return "X";
                        case 3:
                            return "Y";
                        case 4:
                            if (isxinput) return "LeftShoulder";
                            else return "Back";
                        case 5:
                            if (isxinput) return "RightShoulder";
                            else return "Guide";
                        case 6:
                            if (isxinput) return "Back";
                            else return "Start";
                        case 7:
                            if (isxinput) return "Start";
                            else return "LeftThumb";
                        case 8: 
                            if (isxinput) return "LeftThumb";
                            else return "RightThumb";
                        case 9: 
                            if (isxinput) return "RightThumb";
                            else return "LeftShoulder";
                        case 10: 
                            if (isxinput) return "Guide";
                            else return "RightShoulder";
                        case 11:
                            return "DpadUp";
                        case 12:
                            return "DpadDown";
                        case 13:
                            return "DpadLeft";
                        case 14:
                            return "DpadRight";
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LStickRight";
                            else return "LStickLeft";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LStickDown";
                            else return "LStickUp";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RStickRight";
                            else return "RStickLeft";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RStickDown";
                            else return "RStickUp";
                        case 4: return "LeftTrigger";
                        case 5: return "RightTrigger";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "DpadUp";
                        case 2: return "DpadRight";
                        case 4: return "DpadDown";
                        case 8: return "DpadLeft";
                    }
                }
            }
            return "";
        }

        private static string GetDInputKeyName(Controller c, InputKey key)
        {
            Int64 pid;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                    return "B" + (input.Id + 1);

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "X AxisRight";
                            else return "X AxisLeft";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "X AxisDown";
                            else return "X AxisUp";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "";
                            else return "";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "";
                            else return "";
                        case 4:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "";
                            else return "";
                        case 5:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "";
                            else return "";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    long hatID = input.Id;
                    string pov = "POV" + hatID;
                    switch (pid)
                    {
                        case 1: return pov + "U";
                        case 2: return pov + "R";
                        case 4: return pov + "D";
                        case 8: return pov + "L";
                    }
                }
            }
            return "";
        }

        private static InputKeyMapping ConfigureMappingPerSystem(InputKeyMapping mapping, string system, string core)
        {
            InputKeyMapping newMapping = mapping;

            if (system == "3ds")
            {
                if (Program.SystemConfig.getOptBoolean("gamepadbuttons"))
                {
                    newMapping[InputKey.y] = "X";
                    newMapping[InputKey.a] = "A";
                    newMapping[InputKey.b] = "B";
                    newMapping[InputKey.x] = "Y";
                }
            }

            if (system == "nes" && Program.SystemConfig.getOptBoolean("rotate_buttons"))
                return nesMapping_rotate;

            if ((system == "megadrive" || system == "sega32x") && !Program.SystemConfig.getOptBoolean("md_3buttons"))
            {
                if (Program.SystemConfig.isOptSet("megadrive_control_layout"))
                {
                    switch (Program.SystemConfig["megadrive_control_layout"])
                    {
                        case "lr_zc":
                            return mdMapping_lr_zc;
                        case "lr_yz":
                            return mdMapping_lr_yz;
                    }
                }
            }

            if (system == "snes" || (system == "sgb" && core == "BSNES"))
            {
                if (Program.SystemConfig.getOptBoolean("buttonsInvert"))
                    return snesMapping_invert;
            }

            if (system == "mastersystem")
            {
                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                    return smsMapping_rotate;
            }

            if (system == "n64")
            {
                bool switchTriggers = !Program.SystemConfig.isOptSet("ares64_inputprofile") || Program.SystemConfig["ares64_inputprofile"] == "zl";
                bool n64XboxLayout = Program.SystemConfig.isOptSet("ares64_inputprofile") && Program.SystemConfig["ares64_inputprofile"] == "xbox";
                if (n64XboxLayout)
                    return n64Mapping_xbox;
                if (switchTriggers)
                    return n64Mapping_zl;
            }

            if (system == "saturn")
            {
                bool switchTriggers = Program.SystemConfig.getOptBoolean("saturn_invert_triggers");
                if (Program.SystemConfig.isOptSet("saturn_padlayout") && !string.IsNullOrEmpty(Program.SystemConfig["saturn_padlayout"]))
                {
                    switch (Program.SystemConfig["saturn_padlayout"])
                    {
                        case "lr_yz":
                            if (switchTriggers)
                            {
                                newMapping[InputKey.y] = "A";
                                newMapping[InputKey.a] = "B";
                                newMapping[InputKey.b] = "C";
                                newMapping[InputKey.x] = "X";
                                newMapping[InputKey.pageup] = "L";
                                newMapping[InputKey.pagedown] = "R";
                                newMapping[InputKey.l2] = "Y";
                                newMapping[InputKey.r2] = "Z";
                                break;
                            }
                            else
                            {
                                newMapping[InputKey.y] = "A";
                                newMapping[InputKey.a] = "B";
                                newMapping[InputKey.b] = "C";
                                newMapping[InputKey.x] = "X";
                                newMapping[InputKey.pageup] = "Y";
                                newMapping[InputKey.pagedown] = "Z";
                                break;
                            }
                        case "lr_xz":
                            if (switchTriggers)
                            {
                                newMapping[InputKey.y] = "A";
                                newMapping[InputKey.a] = "B";
                                newMapping[InputKey.b] = "C";
                                newMapping[InputKey.pageup] = "L";
                                newMapping[InputKey.pagedown] = "R";
                                newMapping[InputKey.l2] = "X";
                                newMapping[InputKey.r2] = "Z";
                                break;
                            }
                            else
                            {
                                newMapping[InputKey.y] = "A";
                                newMapping[InputKey.a] = "B";
                                newMapping[InputKey.b] = "C";
                                newMapping[InputKey.pageup] = "X";
                                newMapping[InputKey.pagedown] = "Z";
                                break;
                            }
                        case "lr_zc":
                            if (switchTriggers)
                            {
                                newMapping[InputKey.pageup] = "L";
                                newMapping[InputKey.pagedown] = "R";
                                newMapping[InputKey.l2] = "Z";
                                newMapping[InputKey.r2] = "C";
                                break;
                            }
                            break;
                    }
                }
            }

            if (system == "psx" && Program.SystemConfig.getOptBoolean("psx_triggerswap"))
            {
                if (core == "Nymashock")
                    return dualshockNymaGTMapping;
                else
                    return dualshockOctoGTMapping;
            }

                return newMapping;
        }

        private static readonly Dictionary<string, string> systemController = new Dictionary<string, string>()
        {
            { "3ds", "3DS Controller" },
            { "apple2", "Apple IIe Keyboard" },
            { "atari2600", "Atari 2600 Basic Controller" },
            { "atari7800", "Atari 7800 Basic Controller" },
            { "c64", "Commodore 64 Controller" },
            { "channelf", "ChannelF Controller" },
            { "colecovision", "ColecoVision Basic Controller" },
            { "gamegear", "GG Controller" },
            { "gb", "Gameboy Controller" },
            { "gba", "GBA Controller" },
            { "intellivision", "Intellivision Controller" },
            { "jaguar", "Jaguar Controller" },
            { "jaguarcd", "Jaguar Controller" },
            { "lynx", "Lynx Controller" },
            { "mastersystem", "SMS Controller" },
            { "megadrive", "GPGX Genesis Controller" },
            { "multivision", "SMS Controller" },
            { "n64", "Nintendo 64 Controller" },
            { "nds", "NDS Controller" },
            { "nes", "NES Controller" },
            { "ngp", "NeoGeo Portable Controller" },
            { "odyssey2", "O2 Joystick" },
            { "pcengine", "PC Engine Controller" },
            { "pcenginecd", "PC Engine Controller" },
            { "pcfx", "PC-FX Controller" },
            { "psx", "PSX Front Panel" },
            { "saturn", "Saturn Controller" },
            { "sega32x", "PicoDrive Genesis Controller" },
            { "sg1000", "SMS Controller" },
            { "sgb", "Gameboy Controller" },
            { "snes", "SNES Controller" },
            { "tic80", "TIC-80 Controller" },
            { "uzebox", "SNES Controller" },
            { "vectrex", "Vectrex Digital Controller" },
            { "virtualboy", "VirtualBoy Controller" },
            { "wswan", "WonderSwan Controller" },
            { "wswanc", "WonderSwan Controller" },
            { "zxspectrum", "ZXSpectrum Controller" },
        };

        private static readonly Dictionary<string, InputKeyMapping> mappingToUse = new Dictionary<string, InputKeyMapping>()
        {
            { "3ds", n3dsMapping },
            { "atari2600", atariMapping },
            { "atari7800", atariMapping },
            { "channelf", channelfMapping },
            { "colecovision", colecoMapping },
            { "gamegear", ggMapping },
            { "gb", gbMapping },
            { "gba", gbaMapping },
            { "gbc", gbMapping },
            { "intellivision", intvMapping },
            { "jaguar", jaguarMapping },
            { "jaguarcd", jaguarMapping },
            { "lynx", lynxMapping },
            { "mastersystem", smsMapping },
            { "megadrive", mdMapping },
            { "multivision", smsMapping },
            { "n64", n64Mapping },
            { "nds", ndsMapping },
            { "nes", nesMapping },
            { "ngp", ngpMapping },
            { "odyssey2", o2Mapping },
            { "pcengine", pceMapping },
            { "pcenginecd", pceMapping },
            { "pcfx", pcfxMapping },
            { "psx", dualshockOctoMapping },
            { "saturn", saturnMapping },
            { "sega32x", mdMapping },
            { "sg1000", smsMapping },
            { "sgb", gbMapping },
            { "snes", snesMapping },
            { "tic80", tic80Mapping },
            { "uzebox", snesMapping },
            { "vectrex", vecMapping },
            { "virtualboy", vbMapping },
            { "wswan", wswanMapping },
            { "wswanc", wswanMapping },
            { "zxspectrum", zxMapping }
        };

        private void ResetControllerConfiguration(DynamicJson json, int totalNB, string system, string core)
        {
            bool monoplayer = systemMonoPlayer.Contains(system);
            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);

            InputKeyMapping mapping = mappingToUse[system];

            if (system == "psx")
            {
                if (core == "Nymashock" && Program.SystemConfig.isOptSet("bizhawk_psx_digital") && SystemConfig.getOptBoolean("bizhawk_psx_digital"))
                    mapping = psxNymaMapping;
                else if (core == "Nymashock")
                    mapping = dualshockNymaMapping;
                else if (SystemConfig.isOptSet("bizhawk_psx_digital") && SystemConfig.getOptBoolean("bizhawk_psx_digital"))
                    mapping = psxOctoMapping;
            }

            if (system == "sgb" && core == "BSNES")
                mapping = snesMapping;

            if (core == "GBHawk")
            {
                monoplayer = false;
                controllerConfig = trollers.GetOrCreateContainer("Gameboy Controller H");
            }

            if (core == "Cygne")
                totalNB = 2;

            if (monoplayer)
            {
                foreach (var x in mapping)
                {
                    string value = x.Value;
                    controllerConfig[value] = "";
                }
            }

            else
            {
                for (int i = 1; i < totalNB; i++)
                {
                    foreach (var x in mapping)
                    {
                        string value = x.Value;
                        controllerConfig["P" + i + " " + value] = "";
                    }
                }
            }
        }

        private static string SdlToKeyCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x0D: return "Enter";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x20: return "Space";
                case 0x27: return "Apostrophe";
                case 0x2B: return "Plus";
                case 0x2C: return "Comma";
                case 0x2D: return "Minus";
                case 0x2E: return "Period";
                case 0x2F: return "Slash";
                case 0x30: return "Number0";
                case 0x31: return "Number1";
                case 0x32: return "Number2";
                case 0x33: return "Number3";
                case 0x34: return "Number4";
                case 0x35: return "Number5";
                case 0x36: return "Number6";
                case 0x37: return "Number7";
                case 0x38: return "Number8";
                case 0x39: return "Number9";
                case 0x3A: return "Semicolon";
                case 0x3B: return "Semicolon";
                case 0x3C: return "Comma";
                case 0x3D: return "Equal";
                case 0x3F: return "Shift+Slash";
                case 0x5B: return "LeftBracket";
                case 0x5C: return "Backslash";
                case 0x5D: return "RightBracket";
                case 0x5F: return "Minus";
                case 0x60: return "Apostrophe";
                case 0x61: return "A";
                case 0x62: return "B";
                case 0x63: return "C";
                case 0x64: return "D";
                case 0x65: return "E";
                case 0x66: return "F";
                case 0x67: return "G";
                case 0x68: return "H";
                case 0x69: return "I";
                case 0x6A: return "J";
                case 0x6B: return "K";
                case 0x6C: return "L";
                case 0x6D: return "M";
                case 0x6E: return "N";
                case 0x6F: return "O";
                case 0x70: return "P";
                case 0x71: return "Q";
                case 0x72: return "R";
                case 0x73: return "S";
                case 0x74: return "T";
                case 0x75: return "U";
                case 0x76: return "V";
                case 0x77: return "W";
                case 0x78: return "X";
                case 0x79: return "Y";
                case 0x7A: return "Z";
                case 0x7F: return "Delete";
                case 0x40000039: return "CapsLock";
                case 0x4000003A: return "F1";
                case 0x4000003B: return "F2";
                case 0x4000003C: return "F3";
                case 0x4000003D: return "F4";
                case 0x4000003E: return "F5";
                case 0x4000003F: return "F6";
                case 0x40000040: return "F7";
                case 0x40000041: return "F8";
                case 0x40000042: return "F9";
                case 0x40000043: return "F10";
                case 0x40000044: return "F11";
                case 0x40000045: return "F12";
                case 0x40000047: return "ScrollLock";
                case 0x40000048: return "Pause";
                case 0x40000049: return "Insert";
                case 0x4000004A: return "Home";
                case 0x4000004B: return "PageUp";
                case 0x4000004D: return "End";
                case 0x4000004E: return "PageDown";
                case 0x4000004F: return "Right";
                case 0x40000050: return "Left";
                case 0x40000051: return "Down";
                case 0x40000052: return "Up";
                case 0x40000053: return "NumLock";
                case 0x40000054: return "KeypadDivide";
                case 0x40000055: return "KeypadMultiply";
                case 0x40000056: return "KeypadSubtract";
                case 0x40000057: return "KeypadAdd";
                case 0x40000058: return "KeypadEnter";
                case 0x40000059: return "Keypad1";
                case 0x4000005A: return "Keypad2";
                case 0x4000005B: return "Keypad3";
                case 0x4000005C: return "Keypad4";
                case 0x4000005D: return "Keypad5";
                case 0x4000005E: return "Keypad6";
                case 0x4000005F: return "Keypad7";
                case 0x40000060: return "Keypad8";
                case 0x40000061: return "Keypad9";
                case 0x40000062: return "Keypad0";
                case 0x40000063: return "KeypadDecimal";
                case 0x40000067: return "KeypadEquals";
                case 0x40000068: return "F13";
                case 0x40000069: return "F14";
                case 0x4000006A: return "F15";
                case 0x4000006B: return "F16";
                case 0x4000006C: return "F17";
                case 0x4000006D: return "F18";
                case 0x4000006E: return "F19";
                case 0x4000006F: return "F20";
                case 0x40000070: return "F21";
                case 0x40000071: return "F22";
                case 0x40000072: return "F23";
                case 0x40000073: return "F24";
                case 0x4000007F: return "Volume Mute";
                case 0x40000080: return "Volume Up";
                case 0x40000081: return "Volume Down";
                case 0x40000085: return "KeypadDecimal";
                case 0x400000E0: return "Ctrl";
                case 0x400000E1: return "Shift";
                case 0x400000E2: return "Alt";
                case 0x400000E4: return "Ctrl";
                case 0x400000E5: return "Shift";
                case 0x400000E6: return "Alt";
            }
            return "";
        }

        private static void ConfigureKeyboardSystem(DynamicJson json, string system)
        {
            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);

            Dictionary<string, string> kbmapping = null;

            if (system == "apple2")
                kbmapping = apple2Mapping;

            else if (system == "odyssey2")
                kbmapping = o2KbMapping;

            else if (system == "zxspectrum")
                kbmapping = zxkbMapping;

            if (kbmapping == null)
                return;

            foreach (var x in kbmapping)
            {
                string value = x.Value;
                string key = x.Key;
                controllerConfig[key] = value;
            }
        }

        private static readonly Dictionary<string, string> apple2Mapping = new Dictionary<string, string>()
        {
            { "Delete", "Delete" },
            { "Left", "Left" },
            { "Tab", "Tab" },
            { "Down", "Down" },
            { "Up", "Up" },
            { "Return", "Enter" },
            { "Right", "Right" },
            { "Escape", "Escape" },
            { "Space", "Space" },
            { "'", "Apostrophe" },
            { ",", "Comma" },
            { "-", "Minus" },
            { ".", "Period" },
            { "/", "Slash" },
            { "0", "Number0" },
            { "1", "Number1" },
            { "2", "Number2"},
            { "3", "Number3" },
            { "4", "Number4" },
            { "5", "Number5" },
            { "6", "Number6" },
            { "7", "Number7" },
            { "8", "Number8" },
            { "9", "Number9" },
            { ";", "Semicolon" },
            { "=", "Equals" },
            { "[", "LeftBracket" },
            { "\\", "Backslash" },
            { "]", "RightBracket" },
            { "`", "Backtick" },
            { "A", "A" },
            { "B", "B" },
            { "C", "C" },
            { "D", "D" },
            { "E", "E" },
            { "F", "F" },
            { "G", "G" },
            { "H", "H" },
            { "I", "I" },
            { "J", "J" },
            { "K", "K" },
            { "L", "L" },
            { "M", "M" },
            { "N", "N" },
            { "O", "O" },
            { "P", "P" },
            { "Q", "Q" },
            { "R", "R" },
            { "S", "S" },
            { "T", "T" },
            { "U", "U" },
            { "V", "V" },
            { "W", "W" },
            { "X", "X" },
            { "Y", "Y" },
            { "Z", "Z" },
            { "Control", "Ctrl" },
            { "Shift", "Shift" },
            { "Caps Lock", "CapsLock" },
            { "White Apple", "Home" },
            { "Black Apple", "End" },
            { "Previous Disk", "PageUp" },
            { "Next Disk", "PageDown" }
        };

        private static readonly Dictionary<string, string> o2KbMapping = new Dictionary<string, string>()
        {
            { "0", "Number0" },
            { "1", "Number1" },
            { "2", "Number2"},
            { "3", "Number3" },
            { "4", "Number4" },
            { "5", "Number5" },
            { "6", "Number6" },
            { "7", "Number7" },
            { "8", "Number8" },
            { "9", "Number9" },
            { "YES", "Y" },
            { "NO", "N" },
            { "ENT", "Enter" },
            { "SPC", "Space" },
            { "?", "Question" },
            { "L", "L" },
            { "P", "P" },
            { "+", "Plus" },
            { "W", "W" },
            { "E", "E" },
            { "R", "R" },
            { "T", "T" },
            { "U", "U" },
            { "I", "I" },
            { "O", "O" },
            { "Q", "Q" },
            { "S", "S" },
            { "D", "D" },
            { "F", "F" },
            { "G", "G" },
            { "H", "H" },
            { "J", "J" },
            { "K", "K" },
            { "A", "A" },
            { "Z", "Z" },
            { "X", "X" },
            { "C", "C" },
            { "V", "V" },
            { "B", "B" },
            { "M", "M" },
            { "PERIOD", "Period" },
            { "-", "Minus" },
            { "*", "Multiply" },
            { "/", "Slash" },
            { "=", "Equals" },
            { "CLR", "Backspace" },
        };

        private static readonly Dictionary<string, string> zxkbMapping = new Dictionary<string, string>()
        {
            { "Play Tape", "F2" },
            { "Stop Tape", "F3" },
            { "RTZ Tape", "F4" },
            { "Insert Next Tape", "F5" },
            { "Insert Previous Tape", "F6" },
            { "Next Tape Block", "F7" },
            { "Prev Tape Block", "F8" },
            { "Get Tape Status", "F9" },
            { "Insert Next Disk", "F10" },
            { "Insert Previous Disk", "F11" },
            { "Get Disk Status", "F12" },
            { "Key True Video", "Escape" },
            { "Key Inv Video", "F1" },
            { "Key 0", "Number0" },
            { "Key 1", "Number1" },
            { "Key 2", "Number2"},
            { "Key 3", "Number3" },
            { "Key 4", "Number4" },
            { "Key 5", "Number5" },
            { "Key 6", "Number6" },
            { "Key 7", "Number7" },
            { "Key 8", "Number8" },
            { "Key 9", "Number9" },
            { "Key Break", "Backspace" },
            { "Key Delete", "Delete" },
            { "Key Graph", "Backtick" },
            { "Key A", "A" },
            { "Key B", "B" },
            { "Key C", "C" },
            { "Key D", "D" },
            { "Key E", "E" },
            { "Key F", "F" },
            { "Key G", "G" },
            { "Key H", "H" },
            { "Key I", "I" },
            { "Key J", "J" },
            { "Key K", "K" },
            { "Key L", "L" },
            { "Key M", "M" },
            { "Key N", "N" },
            { "Key O", "O" },
            { "Key P", "P" },
            { "Key Q", "Q" },
            { "Key R", "R" },
            { "Key S", "S" },
            { "Key T", "T" },
            { "Key U", "U" },
            { "Key V", "V" },
            { "Key W", "W" },
            { "Key X", "X" },
            { "Key Y", "Y" },
            { "Key Z", "Z" },
            { "Key Extend Mode", "Tab" },
            { "Key Edit", "Alt" },
            { "Key Return", "Enter" },
            { "Key Caps Shift", "Shift" },
            { "Key Caps Lock", "CapsLock" },
            { "Key Period", "Period" },
            { "Key Symbol Shift", "Ctrl" },
            { "Key Semi-Colon", "Semicolon" },
            { "Key Quote", "Quote" },
            { "Key Left Cursor", "Left" },
            { "Key Right Cursor", "Right" },
            { "Key Space", "Space" },
            { "Key Up Cursor", "Up" },
            { "Key Down Cursor", "Down" },
            { "Key Comma", "Comma" }
        };
    }
}