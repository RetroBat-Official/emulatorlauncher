using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class MednafenGenerator : Generator
    {
        static readonly List<string> systemWithAutoconfig = new List<string>() { "apple2", "gb", "gba", "gg", "lynx", "md", "nes", "ngp", "pce", "pcfx", "psx", "sms", "snes", "ss", "wswan" };
        //static readonly List<string> mouseMapping = new List<string>() { "justifier", "gun", "guncon", "superscope", "zapper" };

        static readonly Dictionary<string, string> defaultPadType = new Dictionary<string, string>()
        {
            { "apple2", "gamepad" },
            { "lynx", "builtin.gamepad"},
            { "gb", "builtin.gamepad"},
            { "gba", "builtin.gamepad"},
            { "gg", "builtin.gamepad"},
            { "md", "gamepad6" },
            { "nes", "gamepad" },
            { "ngp", "builtin.gamepad"},
            { "pce", "gamepad" },
            { "pcfx", "gamepad" },
            { "psx", "dualshock" },
            { "sms", "gamepad" },
            { "snes", "gamepad" },
            { "ss", "gamepad" },
            { "wswan", "gamepad" }
        };

        static readonly Dictionary<string, int> inputPortNb = new Dictionary<string, int>()
        {
            { "apple2", 2 },
            { "lynx", 1 },
            { "md", 8 },
            { "gb", 1 },
            { "gba", 1 },
            { "gg", 1 },
            { "nes", 4 },
            { "ngp", 1 },
            { "pce", 5 },
            { "pcfx", 8 },
            { "psx", 8 },
            { "sms", 2 },
            { "snes", 8 },
            { "ss", 12 },
            { "wswan", 1 }
        };

        private void CreateControllerConfiguration(MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;
            if (!systemWithAutoconfig.Contains(mednafenCore))
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Mednafen");

            Dictionary<Controller, string> double_pads = new Dictionary<Controller, string>();

            // First, set all controllers to none
            if (mednafenCore != "lynx" && mednafenCore != "sms" && mednafenCore != "wswan" && mednafenCore != "gb" && mednafenCore != "gba" && mednafenCore != "ngp" && mednafenCore != "gg")
                CleanUpConfigFile(mednafenCore, cfg);

            // Define maximum pads accepted by mednafen core
            int maxPad = inputPortNb[mednafenCore];
          
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
            {
                string deviceID = "";
                
                if (controller.DirectInput != null)
                    deviceID = "0x" + controller.DirectInput.ProductGuid.ToString().Replace("-", "");
                else
                    deviceID = "";

                if (controller.IsXInputDevice)
                {
                    string idSection = "0000";
                    short wButtons = controller.XInput.Wbuttons;
                    idSection = ((ushort)(wButtons < 0 ? 65536 + wButtons : wButtons)).ToString("X4").ToLowerInvariant();

                    deviceID = "0x000000000000000000010004" + idSection + "0000";
                }

                string newDeviceIDPath = Path.Combine(AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                string newDeviceID = SdlJoystickGuid.GetGuidFromFile(newDeviceIDPath, controller.Guid, "mednafen");
                if (newDeviceID != null)
                    deviceID = newDeviceID;

                double_pads.Add(controller, deviceID);
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(controller, cfg, mednafenCore, double_pads, system);
        }

        private void ConfigureInput(Controller controller, MednafenConfigFile cfg, string mednafenCore, Dictionary<Controller, string> double_pads, string system)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard && this.Controllers.Count(i => !i.IsKeyboard) == 0)
                ConfigureKeyboard(controller, cfg, mednafenCore, system);
            else
                ConfigureJoystick(controller, cfg, mednafenCore, double_pads, system);
        }

        #region joystick
        private void ConfigureJoystick(Controller controller, MednafenConfigFile cfg, string mednafenCore, Dictionary<Controller, string> double_pads, string system)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            if (controller.DirectInput == null)
                return;

            int nbAxis = controller.NbAxes;
            bool hatfix = hatFix.Contains(controller.ProductID);
            bool mdSpecialPad = false;
            bool mdSpecialPadHK = false;
            bool saturnSpecialPad = false;
            bool saturnSpecialPadHK = false;
            MegadriveController mdGamepad = null;
            MegadriveController saturnGamepad = null;

            string guid1 = (controller.Guid.ToString()).Substring(0, 24) + "00000000";
            // Fetch information in retrobat/system/tools/gamecontrollerdb.txt file
            SdlToDirectInput dinputCtrl = null;
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                gamecontrollerDB = null;
            }

            if (gamecontrollerDB != null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

                dinputCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1);

                if (dinputCtrl == null)
                    SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid1);
                else
                    SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + " : " + guid1 + " found in gamecontrollerDB file.");
            }

            int playerIndex = controller.PlayerIndex;

            // Define default type of controller per core
            string padType = defaultPadType[mednafenCore];

            // Change controller type if set in features
            if (Program.SystemConfig.isOptSet("mednafen_controller_type") && !string.IsNullOrEmpty(Program.SystemConfig["mednafen_controller_type"]))
                padType = Program.SystemConfig["mednafen_controller_type"];

            // Define pad mapping to use per core - see dictionnaries
            string mapping = mednafenCore + "_" + padType;

            // Initiate dictionnaries
            Dictionary<string, InputKey> newmapping = new Dictionary<string, InputKey>();
            Dictionary<string, string> gunMapping = new Dictionary<string, string>();

            // Manage guns
            if (Program.SystemConfig.isOptSet("mednafen_gun") && Program.SystemConfig.getOptBoolean("mednafen_gun"))
            {
                if (!gunPort.ContainsKey(mednafenCore))
                    return;

                string gunType;
                string psx_gun = "guncon";
                if (Program.SystemConfig.isOptSet("mednafen_psx_gun") && !string.IsNullOrEmpty(Program.SystemConfig["mednafen_psx_gun"]))
                    psx_gun = Program.SystemConfig["mednafen_psx_gun"];

                int portNumber = gunPort[mednafenCore];

                if (mednafenCore == "psx")
                {
                    gunType = psx_gun;
                    gunMapping = gunMappingToUse[psx_gun];
                }
                else
                {
                    gunType = gunName[mednafenCore];
                    gunMapping = gunMappingToUse[mednafenCore];
                }

                if (portNumber == 1)
                {
                    cfg[mednafenCore + ".input.port" + portNumber] = gunType;

                    foreach (var entry in gunMapping)
                        cfg[mednafenCore + ".input.port" + portNumber + "." + gunType + "." + entry.Key] = entry.Value;

                    playerIndex += 1;
                }
                else if (this.Controllers.Count(i => !i.IsKeyboard) == 1 && portNumber == 2)
                {
                    cfg[mednafenCore + ".input.port" + portNumber] = gunType;

                    foreach (var entry in gunMapping)
                        cfg[mednafenCore + ".input.port" + portNumber + "." + gunType + "." + entry.Key] = entry.Value;
                }
                else if (this.Controllers.Count(i => !i.IsKeyboard) > 1 && portNumber == 2 && playerIndex > 1)
                {
                    cfg[mednafenCore + ".input.port" + portNumber] = gunType;

                    foreach (var entry in gunMapping)
                        cfg[mednafenCore + ".input.port" + portNumber + "." + gunType + "." + entry.Key] = entry.Value;

                    playerIndex += 1;
                }
            }

            // Manage controller mapping
            Dictionary<InputKey, string> buttonMapping = dinputMapping;

            if (controller.IsXInputDevice)
                buttonMapping = xboxmapping;

            bool dinput = (buttonMapping == dinputMapping && dinputCtrl != null);

            // Search for megadrive specific controllers
            bool needMDActivationSwitch = false;
            bool md_pad = Program.SystemConfig.getOptBoolean("md_pad");

            if (mednafenCore == "md")
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string mdjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mdControllers.json");

                if (File.Exists(mdjson))
                {
                    try
                    {
                        var megadriveControllers = MegadriveController.LoadControllersFromJson(mdjson);

                        if (megadriveControllers != null)
                        {
                            mdGamepad = MegadriveController.GetMDController("mednafen", guid, megadriveControllers);
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

                                if (mdGamepad.Mapping != null)
                                    mdSpecialPad = true;
                                else
                                    SimpleLogger.Instance.Info("[Controller] Missing mapping for mednafen : " + mdGamepad.Name);

                                if (mdGamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                                    mdSpecialPadHK = true;
                                else
                                    SimpleLogger.Instance.Info("[Controller] Missing mapping for mednafen hotkeys : " + mdGamepad.Name);
                            }
                            else
                                SimpleLogger.Instance.Info("[Controller] No specific mapping found for Megadrive controller.");
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                    }
                    catch { }
                }
            }

        BypassMDControllers:

            // Search for saturn specific controllers
            bool needSatActivationSwitch = false;
            bool sat_pad = Program.SystemConfig.getOptBoolean("saturn_pad");

            if (mednafenCore == "ss")
            {
                string guid = controller.Guid.ToString().ToLowerInvariant();
                string saturnjson = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "saturnControllers.json");

                if (File.Exists(saturnjson))
                {
                    try
                    {
                        var saturnControllers = MegadriveController.LoadControllersFromJson(saturnjson);

                        if (saturnControllers != null)
                        {
                            saturnGamepad = MegadriveController.GetMDController("mednafen", guid, saturnControllers);
                            if (saturnGamepad != null)
                            {
                                if (mdGamepad.ControllerInfo != null)
                                {
                                    if (mdGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                        needSatActivationSwitch = mdGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                    if (needSatActivationSwitch && !sat_pad)
                                    {
                                        SimpleLogger.Instance.Info("[Controller] Specific Saturn mapping needs to be activated for this controller.");
                                        goto BypassSATControllers;
                                    }
                                }

                                SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + saturnGamepad.Name);

                                if (saturnGamepad.Mapping != null)
                                    saturnSpecialPad = true;
                                else
                                    SimpleLogger.Instance.Info("[Controller] Missing mapping for mednafen : " + saturnGamepad.Name);

                                if (saturnGamepad.HotKeyMapping != null && controller.PlayerIndex == 1)
                                    saturnSpecialPadHK = true;
                                else
                                    SimpleLogger.Instance.Info("[Controller] Missing mapping for mednafen hotkeys : " + saturnGamepad.Name);
                            }
                            else
                                SimpleLogger.Instance.Info("[Controller] No specific mapping found for Saturn controller.");
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                    }
                    catch { }
                }
            }

            BypassSATControllers:

            // Else continue
            string deviceID = "";
            
            if (controller.DirectInput != null)
                deviceID = "0x" + controller.DirectInput.ProductGuid.ToString().Replace("-", "");
            else
                deviceID = "";

            if (controller.IsXInputDevice)
            {
                string idSection = "0000";
                short wButtons = controller.XInput.Wbuttons;
                idSection = ((ushort)(wButtons < 0 ? 65536 + wButtons : wButtons)).ToString("X4").ToLowerInvariant();

                deviceID = "0x000000000000000000010004" + idSection + "0000";
            }

            string newDeviceIDPath = Path.Combine(AppConfig.GetFullPath("tools"), "controllerinfo.yml");
            string newDeviceID = SdlJoystickGuid.GetGuidFromFile(newDeviceIDPath, controller.Guid, "mednafen");
            if (newDeviceID != null)
                deviceID = newDeviceID;

            int nsamePad = 0;
            var valueCounts = new Dictionary<string, int>();

            foreach (var pair in double_pads)
            {
                if (valueCounts.ContainsKey(pair.Value))
                    valueCounts[pair.Value]++;
                else
                    valueCounts[pair.Value] = 1;
            }
            nsamePad = valueCounts[deviceID];

            if (nsamePad > 0)
            {
                var cList = double_pads.Where(i => i.Value == deviceID).OrderBy(c => c.Key.DirectInput.DeviceIndex).ToList();
                int dinputIndex = cList.FindIndex(kvp => kvp.Key.Equals(controller));

                char lastChar = deviceID[deviceID.Length - 1];
                int lastInt = Convert.ToInt32(lastChar.ToString(), 16);
                lastInt += dinputIndex;
                string newLastChar = lastInt.ToString("X");
                deviceID = deviceID.Substring(0, deviceID.Length - 1) + newLastChar;
            }

            if (mappingToUse.ContainsKey(mapping))
                newmapping = mappingToUse[mapping];

            // Special case for psx dualshock and driving games
            if (mednafenCore == "psx" && SystemConfig.getOptBoolean("psx_triggerswap"))
            {
                padType = "dualshock";
                mapping = mednafenCore + "_" + padType + "_gtspecial";
                newmapping = mappingToUse[mapping];
                cfg["psx.input.port" + playerIndex + ".dualshock.l2"] = "none";
                cfg["psx.input.port" + playerIndex + ".dualshock.r2"] = "none";
            }

            // Specifics per system
            // megadrive mapping when using special controller
            if (mednafenCore == "md" && mdSpecialPad)
            {
                cfg[mednafenCore + ".input.port" + playerIndex] = padType;

                foreach (var button in mdGamepad.Mapping)
                {
                    if (button.Value.Contains("_or_"))
                    {
                        string[] delimiter = new string[] { "_or_" };
                        var values = button.Value.Split(delimiter, StringSplitOptions.None);
                        string value1 = values[0];
                        string value2 = values[1];
                        cfg[mednafenCore + ".input.port" + playerIndex + "." + padType + "." + button.Key] = "joystick " + deviceID + " " + value1 + " || " + "joystick " + deviceID + " " + value2;
                    }
                    else
                        cfg[mednafenCore + ".input.port" + playerIndex + "." + padType + "." + button.Key] = "joystick " + deviceID + " " + button.Value;
                }

                if (mdSpecialPadHK && playerIndex == 1)
                {
                    foreach (var button in mdGamepad.HotKeyMapping)
                    {
                        if (button.Value.Contains("_or_"))
                        {
                            string[] orSplitter = new string[] { "_or_" };
                            var combinaisons = button.Value.Split(orSplitter, StringSplitOptions.None);
                            string combination1 = combinaisons[0];
                            string combination2 = combinaisons[1];

                            string[] delimiters = new string[] { "_and_" };
                            var buttons1 = combination1.Split(delimiters, StringSplitOptions.None);
                            var buttons2 = combination2.Split(delimiters, StringSplitOptions.None);

                            if (buttons1.Length < 2 || buttons2.Length < 2)
                                continue;

                            if (button.Key == "load_state")
                                cfg["command.load_state"] = "keyboard 0x0 64" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "save_state")
                                cfg["command.save_state"] = "keyboard 0x0 62" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "state_slot_dec")
                                cfg["command.state_slot_dec"] = "keyboard 0x0 45" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "state_slot_inc")
                                cfg["command.state_slot_inc"] = "keyboard 0x0 46" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "toggle_help")
                                cfg["command.toggle_help"] = "keyboard 0x0 58" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "select_disk")
                                cfg["command.select_disk"] = "keyboard 0x0 63" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "take_snapshot")
                                cfg["command.take_snapshot"] = "keyboard 0x0 66" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "fast_forward")
                                cfg["command.fast_forward"] = "keyboard 0x0 53" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "state_rewind")
                                cfg["command.state_rewind"] = "keyboard 0x0 42" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "pause")
                                cfg["command.pause"] = "keyboard 0x0 72" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                            else if (button.Key == "exit")
                                cfg["command.exit"] = "keyboard 0x0 69" + " || " + "joystick " + deviceID + " " + buttons1[0] + " && " + "joystick " + deviceID + " " + buttons1[1] + " || " + "joystick " + deviceID + " " + buttons2[0] + " && " + "joystick " + deviceID + " " + buttons2[1];
                        }

                        else
                        {
                            string[] delimiters = new string[] { "_and_" };
                            var buttons = button.Value.Split(delimiters, StringSplitOptions.None);

                            if (buttons.Length < 2)
                                continue;

                            if (button.Key == "load_state")
                                cfg["command.load_state"] = "keyboard 0x0 64" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "save_state")
                                cfg["command.save_state"] = "keyboard 0x0 62" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "state_slot_dec")
                                cfg["command.state_slot_dec"] = "keyboard 0x0 45" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "state_slot_inc")
                                cfg["command.state_slot_inc"] = "keyboard 0x0 46" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "toggle_help")
                                cfg["command.toggle_help"] = "keyboard 0x0 58" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "select_disk")
                                cfg["command.select_disk"] = "keyboard 0x0 63" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "take_snapshot")
                                cfg["command.take_snapshot"] = "keyboard 0x0 66" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "fast_forward")
                                cfg["command.fast_forward"] = "keyboard 0x0 53" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "state_rewind")
                                cfg["command.state_rewind"] = "keyboard 0x0 42" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "pause")
                                cfg["command.pause"] = "keyboard 0x0 72" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                            else if (button.Key == "exit")
                                cfg["command.exit"] = "keyboard 0x0 69" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        }
                    }
                }

                SimpleLogger.Instance.Info("[INFO] Assigned md controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
                return;
            }

            // saturn mapping when using special controller
            if (mednafenCore == "ss" && saturnSpecialPad)
            {
                cfg[mednafenCore + ".input.port" + playerIndex] = padType;

                foreach (var button in saturnGamepad.Mapping)
                    cfg[mednafenCore + ".input.port" + playerIndex + "." + padType + "." + button.Key] = "joystick " + deviceID + " " + button.Value;

                if (saturnSpecialPadHK && playerIndex == 1)
                {
                    foreach (var button in saturnGamepad.HotKeyMapping)
                    {
                        string[] delimiters = new string[] { "_and_" };
                        var buttons = button.Value.Split(delimiters, StringSplitOptions.None);

                        if (buttons.Length < 2)
                            continue;

                        if (button.Key == "load_state")
                            cfg["command.load_state"] = "keyboard 0x0 64" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "save_state")
                            cfg["command.save_state"] = "keyboard 0x0 62" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "state_slot_dec")
                            cfg["command.state_slot_dec"] = "keyboard 0x0 45" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "state_slot_inc")
                            cfg["command.state_slot_inc"] = "keyboard 0x0 46" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "toggle_help")
                            cfg["command.toggle_help"] = "keyboard 0x0 58" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "select_disk")
                            cfg["command.select_disk"] = "keyboard 0x0 63" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "take_snapshot")
                            cfg["command.take_snapshot"] = "keyboard 0x0 66" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "fast_forward")
                            cfg["command.fast_forward"] = "keyboard 0x0 53" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "state_rewind")
                            cfg["command.state_rewind"] = "keyboard 0x0 42" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "pause")
                            cfg["command.pause"] = "keyboard 0x0 72" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                        else if (button.Key == "exit")
                            cfg["command.exit"] = "keyboard 0x0 69" + " || " + "joystick " + deviceID + " " + buttons[0] + " && " + "joystick " + deviceID + " " + buttons[1];
                    }
                }

                SimpleLogger.Instance.Info("[INFO] Assigned md controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
                return;
            }

            // apple2 only accepts atari joystick in port 2
            if (mednafenCore == "apple2" && playerIndex == 2)
            {
                if (SystemConfig["mednafen_controller_type"] == "atari")
                {
                    cfg[mednafenCore + ".input.port" + 2] = "atari";
                    foreach (var entry in apple2atari)
                    {
                        InputKey joyButton = entry.Value;
                        string value = buttonMapping[joyButton];

                        if (dinput)
                            cfg[mednafenCore + ".input.port" + 2 + ".atari." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                        else
                            cfg[mednafenCore + ".input.port" + 2 + ".atari." + entry.Key] = "joystick " + deviceID + " " + value;
                    }
                }
                else
                    cfg[mednafenCore + ".input.port" + 2] = "paddle";
            }

            else if (mednafenCore == "lynx")
            {
                foreach (var entry in lynxmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["lynx.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["lynx.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            else if (mednafenCore == "gba")
            {
                foreach (var entry in gbamapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["gba.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["gba.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            else if (mednafenCore == "gb")
            {
                foreach (var entry in gbmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["gb.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["gb.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + value;
                }

                foreach (var entry in gbtiltmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["gb.input.tilt.tilt." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["gb.input.tilt.tilt." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            else if (mednafenCore == "gg")
            {
                foreach (var entry in ggmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["gg.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["gg.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            else if (mednafenCore == "ngp")
            {
                foreach (var entry in ngpmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["ngp.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["ngp.input.builtin.gamepad." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            else if (mednafenCore == "wswan")
            {
                cfg["wswan.input.builtin"] = padType;

                foreach (var entry in newmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg["wswan.input.builtin." + padType + "." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg["wswan.input.builtin." + padType + "." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            else
            {
                bool noType = false;
                if (mednafenCore == "snes" && playerIndex > 2)
                    noType = true;
                else if (mednafenCore == "sms")
                    noType = true;

                if (!noType)
                    cfg[mednafenCore + ".input.port" + playerIndex] = padType;

                newmapping = ConfigureMappingPerSystem(newmapping, system, padType, cfg);

                foreach (var entry in newmapping)
                {
                    InputKey joyButton = entry.Value;
                    string value = buttonMapping[joyButton];

                    if (dinput)
                        cfg[mednafenCore + ".input.port" + playerIndex + "." + padType + "." + entry.Key] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, value, nbAxis, hatfix);
                    else
                        cfg[mednafenCore + ".input.port" + playerIndex + "." + padType + "." + entry.Key] = "joystick " + deviceID + " " + value;
                }
            }

            if (system == "segastv" && playerIndex == 1)
            {
                if (dinput)
                {
                    cfg["command.insert_coin"] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix);
                    cfg["ss.input.builtin.builtin.stv_test"] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.r3], nbAxis, hatfix);
                    cfg["ss.input.builtin.builtin.stv_service"] = "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.l3], nbAxis, hatfix);
                }
                else
                {
                    cfg["command.insert_coin"] = "joystick " + deviceID + " " + buttonMapping[InputKey.select];
                    cfg["ss.input.builtin.builtin.stv_test"] = "joystick " + deviceID + " " + buttonMapping[InputKey.r3];
                    cfg["ss.input.builtin.builtin.stv_service"] = "joystick " + deviceID + " " + buttonMapping[InputKey.l3];
                }
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());

            if (controller.PlayerIndex == 1)
            {
                if (dinput)
                {
                    cfg["command.load_state"] = "keyboard 0x0 64" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.x], nbAxis, hatfix);
                    cfg["command.save_state"] = "keyboard 0x0 62" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.y], nbAxis, hatfix);
                    cfg["command.state_slot_dec"] = "keyboard 0x0 45" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.down], nbAxis, hatfix);
                    cfg["command.state_slot_inc"] = "keyboard 0x0 46" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.up], nbAxis, hatfix);
                    cfg["command.toggle_help"] = "keyboard 0x0 58" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.a], nbAxis, hatfix);
                    cfg["command.select_disk"] = "keyboard 0x0 63" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.r2], nbAxis, hatfix);
                    cfg["command.take_snapshot"] = "keyboard 0x0 66" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.r3], nbAxis, hatfix);
                    cfg["command.fast_forward"] = "keyboard 0x0 53" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.right], nbAxis, hatfix);
                    cfg["command.state_rewind"] = "keyboard 0x0 42" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.left], nbAxis, hatfix);
                    cfg["command.pause"] = "keyboard 0x0 72" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.b], nbAxis, hatfix);
                    cfg["command.exit"] = "keyboard 0x0 69" + " || " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.select], nbAxis, hatfix) + " && " + "joystick " + deviceID + " " + GetDinputMapping(dinputCtrl, buttonMapping[InputKey.start], nbAxis, hatfix);
                }
                else
                {
                    cfg["command.load_state"] = "keyboard 0x0 64" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.x];
                    cfg["command.save_state"] = "keyboard 0x0 62" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.y];
                    cfg["command.state_slot_dec"] = "keyboard 0x0 45" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.down];
                    cfg["command.state_slot_inc"] = "keyboard 0x0 46" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.up];
                    cfg["command.toggle_help"] = "keyboard 0x0 58" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.a];
                    cfg["command.select_disk"] = "keyboard 0x0 63" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.r2];
                    cfg["command.take_snapshot"] = "keyboard 0x0 66" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.r3];
                    cfg["command.fast_forward"] = "keyboard 0x0 53" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.right];
                    cfg["command.state_rewind"] = "keyboard 0x0 42" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.left];
                    cfg["command.pause"] = "keyboard 0x0 72" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.b];
                    cfg["command.exit"] = "keyboard 0x0 69" + " || " + "joystick " + deviceID + " " + buttonMapping[InputKey.select] + " && " + "joystick " + deviceID + " " + buttonMapping[InputKey.start];
                }
            }
        }
        #endregion

        #region keyboard
        private static void ConfigureKeyboard(Controller controller, MednafenConfigFile cfg, string mednafenCore, string system)
        {
            if (controller == null)
                return;

            InputConfig keyboard = controller.Config;
            if (keyboard == null)
                return;

            Action<string, int, string, string, InputKey> WriteKeyboardMapping = (u, v, w, x, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    int id = (int)a.Id;

                    SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                    List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                    if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                        keycode = azertyLayoutMapping[keycode];

                    int mednafenKey = mednafenKeyCodes[keycode];

                    cfg[u + ".input.port" + v + "." + w + "." + x] = "keyboard 0x0 " + mednafenKey;
                }
            };

            string padType = defaultPadType[mednafenCore];

            if (Program.SystemConfig.isOptSet("mednafen_controller_type") && !string.IsNullOrEmpty(Program.SystemConfig["mednafen_controller_type"]))
                padType = Program.SystemConfig["mednafen_controller_type"];

            string mapping = mednafenCore + "_" + padType;

            Dictionary<string, InputKey> newmapping = new Dictionary<string, InputKey>();
            Dictionary<string, string> gunMapping = new Dictionary<string, string>();

            if (Program.SystemConfig.isOptSet("mednafen_gun") && Program.SystemConfig.getOptBoolean("mednafen_gun"))
            {
                if (!gunPort.ContainsKey(mednafenCore))
                    return;

                string gunType;
                string psx_gun = "guncon";
                if (Program.SystemConfig.isOptSet("mednafen_psx_gun") && !string.IsNullOrEmpty(Program.SystemConfig["mednafen_psx_gun"]))
                    psx_gun = Program.SystemConfig["mednafen_psx_gun"];

                int portNumber = gunPort[mednafenCore];

                if (mednafenCore == "psx")
                {
                    gunType = psx_gun;
                    gunMapping = gunMappingToUse[psx_gun];
                }
                else
                {
                    gunType = gunName[mednafenCore];
                    gunMapping = gunMappingToUse[mednafenCore];
                }

                cfg[mednafenCore + ".input.port" + portNumber] = gunType;

                foreach (var entry in gunMapping)
                    cfg[mednafenCore + ".input.port" + portNumber + "." + gunType + "." + entry.Key] = entry.Value;

                if (portNumber == 1)
                {
                    if (mappingToUse.ContainsKey(mapping))
                        newmapping = mappingToUse[mapping];

                    cfg[mednafenCore + ".input.port2"] = padType;

                    foreach (var entry in newmapping)
                        WriteKeyboardMapping(mednafenCore, 2, padType, entry.Key, entry.Value);
                }
                else
                {
                    if (mappingToUse.ContainsKey(mapping))
                        newmapping = mappingToUse[mapping];

                    cfg[mednafenCore + ".input.port1"] = padType;

                    foreach (var entry in newmapping)
                        WriteKeyboardMapping(mednafenCore, 1, padType, entry.Key, entry.Value);
                }
            }

            if (mednafenCore == "apple2")
                cfg[mednafenCore + ".input.port" + 2] = "paddle";

            else if (mednafenCore == "lynx")
            {
                foreach (var entry in lynxmapping)
                {
                    var a = keyboard[entry.Value];
                    if (a != null)
                    {
                        int id = (int)a.Id;

                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int mednafenKey = mednafenKeyCodes[keycode];

                        cfg["lynx.input.builtin.gamepad." + entry.Key] = "keyboard 0x0 " + mednafenKey;
                    }
                }
            }

            else if (mednafenCore == "gba")
            {
                foreach (var entry in gbamapping)
                {
                    var a = keyboard[entry.Value];
                    if (a != null)
                    {
                        int id = (int)a.Id;

                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int mednafenKey = mednafenKeyCodes[keycode];

                        cfg["gba.input.builtin.gamepad." + entry.Key] = "keyboard 0x0 " + mednafenKey;
                    }
                }
            }

            else if (mednafenCore == "gb")
            {
                foreach (var entry in gbmapping)
                {
                    var a = keyboard[entry.Value];
                    if (a != null)
                    {
                        int id = (int)a.Id;

                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int mednafenKey = mednafenKeyCodes[keycode];

                        cfg["gb.input.builtin.gamepad." + entry.Key] = "keyboard 0x0 " + mednafenKey;
                    }
                }
            }

            else if (mednafenCore == "gg")
            {
                foreach (var entry in ggmapping)
                {
                    var a = keyboard[entry.Value];
                    if (a != null)
                    {
                        int id = (int)a.Id;

                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int mednafenKey = mednafenKeyCodes[keycode];

                        cfg["gg.input.builtin.gamepad." + entry.Key] = "keyboard 0x0 " + mednafenKey;
                    }
                }
            }

            else if (mednafenCore == "ngp")
            {
                foreach (var entry in ngpmapping)
                {
                    var a = keyboard[entry.Value];
                    if (a != null)
                    {
                        int id = (int)a.Id;

                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int mednafenKey = mednafenKeyCodes[keycode];

                        cfg["ngp.input.builtin.gamepad." + entry.Key] = "keyboard 0x0 " + mednafenKey;
                    }
                }
            }

            else if (mednafenCore == "wswan")
            {
                if (mappingToUsekb.ContainsKey(mapping))
                    newmapping = mappingToUsekb[mapping];

                cfg["wswan.input.builtin"] = padType;

                foreach (var entry in newmapping)
                {
                    var a = keyboard[entry.Value];
                    if (a != null)
                    {
                        int id = (int)a.Id;

                        SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                        List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                        if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                            keycode = azertyLayoutMapping[keycode];

                        int mednafenKey = mednafenKeyCodes[keycode];

                        cfg["wswan.input.builtin." + padType + "." + entry.Key] = "keyboard 0x0 " + mednafenKey;
                    }
                }
            }

            else
            {
                bool noType = false;
                if (mednafenCore == "sms")
                    noType = true;

                if (mappingToUse.ContainsKey(mapping))
                {
                    newmapping = mappingToUse[mapping];
                }

                if (!noType)
                    cfg[mednafenCore + ".input.port1"] = padType;

                foreach (var entry in newmapping)
                    WriteKeyboardMapping(mednafenCore, 1, padType, entry.Key, entry.Value);
            }

            if (system == "segastv")
            {
                var coin = keyboard[InputKey.select];
                if (coin != null)
                {
                    int id = (int)coin.Id;

                    SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                    List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                    if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                        keycode = azertyLayoutMapping[keycode];

                    int mednafenKey = mednafenKeyCodes[keycode];

                    cfg["command.insert_coin"] = "keyboard 0x0 " + mednafenKey;
                }
                else
                    cfg["command.insert_coin"] = "keyboard 0x0 62"; //F5

                var test = keyboard[InputKey.l3];
                if (test != null)
                {
                    int id = (int)test.Id;

                    SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                    List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                    if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                        keycode = azertyLayoutMapping[keycode];

                    int mednafenKey = mednafenKeyCodes[keycode];

                    cfg["ss.input.builtin.builtin.stv_test"] = "keyboard 0x0 " + mednafenKey;
                }
                else
                    cfg["ss.input.builtin.builtin.stv_test"] = "keyboard 0x0 65"; //F8

                var service = keyboard[InputKey.r3];
                if (service != null)
                {
                    int id = (int)service.Id;

                    SDL.SDL_Keycode keycode = (SDL.SDL_Keycode)id;

                    List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
                    if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId) && azertyLayoutMapping.ContainsKey(keycode))
                        keycode = azertyLayoutMapping[keycode];

                    int mednafenKey = mednafenKeyCodes[keycode];

                    cfg["ss.input.builtin.builtin.stv_service"] = "keyboard 0x0 " + mednafenKey;
                }
                else
                    cfg["ss.input.builtin.builtin.stv_service"] = "keyboard 0x0 66";
            }
        }
        #endregion

        #region controller Mapping

        static readonly Dictionary<string, InputKey> gbmapping = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "rapid_a", InputKey.x },
            { "rapid_b", InputKey.y },
            { "right", InputKey.right },
            { "select", InputKey.select },
            { "start", InputKey.start },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> gbtiltmapping = new Dictionary<string, InputKey>()
        {
            { "down", InputKey.rightanalogdown },
            { "left", InputKey.rightanalogleft },
            { "right", InputKey.rightanalogright },
            { "up", InputKey.rightanalogup }
        };

        static readonly Dictionary<string, InputKey> gbamapping = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "rapid_a", InputKey.x },
            { "rapid_b", InputKey.y },
            { "right", InputKey.right },
            { "select", InputKey.select },
            { "shoulder_l", InputKey.pageup },
            { "shoulder_r", InputKey.pagedown },
            { "start", InputKey.start },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> ggmapping = new Dictionary<string, InputKey>()
        {
            { "button1", InputKey.a },
            { "button2", InputKey.b },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "rapid_button1", InputKey.y },
            { "rapid_button2", InputKey.x },
            { "right", InputKey.right },
            { "start", InputKey.start },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> lynxmapping = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "option_1", InputKey.pageup },
            { "option_2", InputKey.pagedown },
            { "pause", InputKey.start },
            { "rapid_a", InputKey.x },
            { "rapid_b", InputKey.y },
            { "right", InputKey.right },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> mdgamepad = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.y },
            { "b", InputKey.a },
            { "c", InputKey.b },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "rapid_a", InputKey.pageup },
            { "rapid_b", InputKey.x },
            { "rapid_c", InputKey.pagedown },
            { "right", InputKey.right },
            { "start", InputKey.start },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> mdgamepad2 = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.a },
            { "b", InputKey.b },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "rapid_a", InputKey.y },
            { "rapid_b", InputKey.x },
            { "right", InputKey.right },
            { "start", InputKey.start },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> mdgamepad6 = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.y },
            { "b", InputKey.a },
            { "c", InputKey.b },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "mode", InputKey.select },
            { "right", InputKey.right },
            { "start", InputKey.start },
            { "up", InputKey.up },
            { "x", InputKey.pageup },
            { "y", InputKey.x },
            { "z", InputKey.pagedown },
        };

        static readonly Dictionary<string, InputKey> nesgamepad = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.a },
            { "b", InputKey.y },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "rapid_a", InputKey.b },
            { "rapid_b", InputKey.x },
            { "right", InputKey.right },
            { "select", InputKey.select },
            { "start", InputKey.start },
            { "up", InputKey.up },
        };

        static readonly Dictionary<string, InputKey> ngpmapping = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.a },
            { "b", InputKey.b },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "option", InputKey.start },
            { "rapid_a", InputKey.y },
            { "rapid_b", InputKey.x },
            { "right", InputKey.right },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> pcegamepad = new Dictionary<string, InputKey>()
        {
            { "down", InputKey.down },
            { "i", InputKey.b },
            { "ii", InputKey.a },
            { "iii", InputKey.y },
            { "iv", InputKey.x },
            { "left", InputKey.left },
            { "mode_select", InputKey.l2 },
            { "right", InputKey.right },
            { "run", InputKey.start },
            { "select", InputKey.select },
            { "up", InputKey.up },
            { "v", InputKey.pageup },
            { "vi", InputKey.pagedown },
        };

        static readonly Dictionary<string, InputKey> pcfxgamepad = new Dictionary<string, InputKey>()
        {
            { "down", InputKey.down },
            { "i", InputKey.b },
            { "ii", InputKey.a },
            { "iii", InputKey.x },
            { "iv", InputKey.y },
            { "left", InputKey.left },
            { "mode1", InputKey.l2 },
            { "mode2", InputKey.r2 },
            { "right", InputKey.right },
            { "run", InputKey.start },
            { "select", InputKey.select },
            { "up", InputKey.up },
            { "v", InputKey.pageup },
            { "vi", InputKey.pagedown },
        };

        static readonly Dictionary<string, InputKey> psxgamepad = new Dictionary<string, InputKey>()
        {
            { "circle", InputKey.b },
            { "cross", InputKey.a },
            { "down", InputKey.down },
            { "l1", InputKey.pageup },
            { "l2", InputKey.l2 },
            { "left", InputKey.left },
            { "r1", InputKey.pagedown },
            { "r2", InputKey.r2 },
            { "right", InputKey.right },
            { "select", InputKey.select },
            { "square", InputKey.y },
            { "start", InputKey.start },
            { "triangle", InputKey.x },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> psxdualshock = new Dictionary<string, InputKey>()
        {
            { "circle", InputKey.b },
            { "cross", InputKey.a },
            { "down", InputKey.down },
            { "l1", InputKey.pageup },
            { "l2", InputKey.l2 },
            { "l3", InputKey.l3 },
            { "left", InputKey.left },
            { "lstick_down", InputKey.leftanalogdown },
            { "lstick_left", InputKey.leftanalogleft },
            { "lstick_right", InputKey.leftanalogright },
            { "lstick_up", InputKey.leftanalogup },
            { "r1", InputKey.pagedown },
            { "r2", InputKey.r2 },
            { "r3", InputKey.r3 },
            { "right", InputKey.right },
            { "rstick_down", InputKey.rightanalogdown },
            { "rstick_left", InputKey.rightanalogleft },
            { "rstick_right", InputKey.rightanalogright },
            { "rstick_up", InputKey.rightanalogup },
            { "select", InputKey.select },
            { "square", InputKey.y },
            { "start", InputKey.start },
            { "triangle", InputKey.x },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> psxdualshockgt = new Dictionary<string, InputKey>()
        {
            { "circle", InputKey.b },
            { "cross", InputKey.a },
            { "down", InputKey.down },
            { "l1", InputKey.pageup },
            { "l3", InputKey.l3 },
            { "left", InputKey.left },
            { "lstick_down", InputKey.leftanalogdown },
            { "lstick_left", InputKey.leftanalogleft },
            { "lstick_right", InputKey.leftanalogright },
            { "lstick_up", InputKey.leftanalogup },
            { "r1", InputKey.pagedown },
            { "r3", InputKey.r3 },
            { "right", InputKey.right },
            { "rstick_down", InputKey.l2 },
            { "rstick_left", InputKey.rightanalogleft },
            { "rstick_right", InputKey.rightanalogright },
            { "rstick_up", InputKey.r2 },
            { "select", InputKey.select },
            { "square", InputKey.y },
            { "start", InputKey.start },
            { "triangle", InputKey.x },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> snesgamepad = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "down", InputKey.down },
            { "l", InputKey.pageup },
            { "left", InputKey.left },
            { "r", InputKey.pagedown },
            { "rapid_a", InputKey.l2 },
            { "rapid_b", InputKey.r2 },
            { "right", InputKey.right },
            { "select", InputKey.select },
            { "start", InputKey.start },
            { "up", InputKey.up },
            { "x", InputKey.x },
            { "y", InputKey.y },
        };

        static readonly Dictionary<string, InputKey> ssgamepad = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.a },
            { "b", InputKey.b },
            { "c", InputKey.pagedown },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "ls", InputKey.l2 },
            { "right", InputKey.right },
            { "rs", InputKey.r2 },
            { "start", InputKey.start },
            { "up", InputKey.up },
            { "x", InputKey.y },
            { "y", InputKey.x },
            { "z", InputKey.pageup }
        };

        static readonly Dictionary<string, InputKey> apple2gamepad = new Dictionary<string, InputKey>()
        {
            { "button1", InputKey.a },
            { "button2", InputKey.b },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "resistance_select", InputKey.pageup },
            { "right", InputKey.right },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> apple2joystick = new Dictionary<string, InputKey>()
        {
            { "button1", InputKey.a },
            { "button2", InputKey.b },
            { "resistance_select", InputKey.pageup },
            { "stick_down", InputKey.leftanalogdown },
            { "stick_left", InputKey.leftanalogleft },
            { "stick_right", InputKey.leftanalogright },
            { "stick_up", InputKey.leftanalogup }
        };

        static readonly Dictionary<string, InputKey> apple2atari = new Dictionary<string, InputKey>()
        {
            { "button", InputKey.a },
            { "down", InputKey.down },
            { "left", InputKey.left },
            { "right", InputKey.right },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> smsgamepad = new Dictionary<string, InputKey>()
        {
            { "down", InputKey.down },
            { "fire1", InputKey.a },
            { "fire2", InputKey.b },
            { "left", InputKey.left },
            { "pause", InputKey.start },
            { "rapid_fire1", InputKey.y },
            { "rapid_fire2", InputKey.x },
            { "right", InputKey.right },
            { "up", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> wswanhorizontal = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "down-x", InputKey.down },
            { "down-y", InputKey.leftanalogdown },
            { "left-x", InputKey.left },
            { "left-y", InputKey.leftanalogleft },
            { "rapid_a", InputKey.x },
            { "rapid_b", InputKey.y },
            { "right-x", InputKey.right },
            { "right-y", InputKey.leftanalogright },
            { "start", InputKey.start },
            { "up-x", InputKey.up },
            { "up-y", InputKey.leftanalogup }
        };

        static readonly Dictionary<string, InputKey> wswanvertical = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "ap", InputKey.x },
            { "b", InputKey.a },
            { "bp", InputKey.y },
            { "down-x", InputKey.rightanalogdown },
            { "down-y", InputKey.down },
            { "left-x", InputKey.rightanalogleft },
            { "left-y", InputKey.left },
            { "right-x", InputKey.rightanalogright },
            { "right-y", InputKey.right },
            { "start", InputKey.start },
            { "up-x", InputKey.rightanalogup },
            { "up-y", InputKey.up }
        };

        static readonly Dictionary<string, InputKey> wswanhorizontalkb = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "b", InputKey.a },
            { "down-x", InputKey.down },
            { "down-y", InputKey.pageup },
            { "left-x", InputKey.left },
            { "left-y", InputKey.l2 },
            { "rapid_a", InputKey.x },
            { "rapid_b", InputKey.y },
            { "right-x", InputKey.right },
            { "right-y", InputKey.r2 },
            { "start", InputKey.start },
            { "up-x", InputKey.up },
            { "up-y", InputKey.pagedown }
        };

        static readonly Dictionary<string, InputKey> wswanverticalkb = new Dictionary<string, InputKey>()
        {
            { "a", InputKey.b },
            { "ap", InputKey.x },
            { "b", InputKey.a },
            { "bp", InputKey.y },
            { "down-x", InputKey.down },
            { "down-y", InputKey.pageup },
            { "left-x", InputKey.left },
            { "left-y", InputKey.l2 },
            { "right-x", InputKey.right },
            { "right-y", InputKey.r2 },
            { "start", InputKey.start },
            { "up-x", InputKey.up },
            { "up-y", InputKey.pagedown }
        };
        #endregion

        #region Gun Mapping
        static readonly Dictionary<string, int> gunPort = new Dictionary<string, int>()
        {
            { "nes", 1 },
            { "snes", 2 },
            { "ss", 1 },
            { "psx", 1 }
        };

        static readonly Dictionary<string, string> gunName = new Dictionary<string, string>()
        {
            { "nes", "zapper" },
            { "snes", "superscope" },
            { "ss", "gun" }
        };

        static readonly Dictionary<string, string> neszapper = new Dictionary<string, string>()
        {
            { "away_trigger", "mouse 0x0 button_right" },
            { "trigger", "mouse 0x0 button_left" },
            { "x_axis", "mouse 0x0 cursor_x-+" },
            { "y_axis", "mouse 0x0 cursor_y-+" },
        };

        static readonly Dictionary<string, string> psxguncon = new Dictionary<string, string>()
        {
            { "a", "mouse 0x0 button_right" },
            { "b", "mouse 0x0 button_middle" },
            { "offscreen_shot", "keyboard 0x0 44" },    //space
            { "trigger", "mouse 0x0 button_left" },
            { "x_axis", "mouse 0x0 cursor_x-+" },
            { "y_axis", "mouse 0x0 cursor_y-+" },
        };

        static readonly Dictionary<string, string> psxjustifier = new Dictionary<string, string>()
        {
            { "o", "mouse 0x0 button_right" },
            { "offscreen_shot", "keyboard 0x0 44" },    //space
            { "start", "mouse 0x0 button_middle" },
            { "trigger", "mouse 0x0 button_left" },
            { "x_axis", "mouse 0x0 cursor_x-+" },
            { "y_axis", "mouse 0x0 cursor_y-+" },
        };

        static readonly Dictionary<string, string> snessuperscope = new Dictionary<string, string>()
        {
            { "cursor", "mouse 0x0 button_right" },
            { "offscreen_shot", "keyboard 0x0 44" },    //space
            { "pause", "keyboard 0x0 77" },
            { "trigger", "mouse 0x0 button_left" },
            { "turbo", "mouse 0x0 button_middle" },
            { "x_axis", "mouse 0x0 cursor_x-+" },
            { "y_axis", "mouse 0x0 cursor_y-+" },
        };

        static readonly Dictionary<string, string> saturngun = new Dictionary<string, string>()
        {
            { "offscreen_shot", "mouse 0x0 button_right" },
            { "start", "mouse 0x0 button_middle" },
            { "trigger", "mouse 0x0 button_left" },
            { "x_axis", "mouse 0x0 cursor_x-+" },
            { "y_axis", "mouse 0x0 cursor_y-+" },
        };
        #endregion

        #region Mapping link
        static readonly Dictionary<string, Dictionary<string, InputKey>> mappingToUse = new Dictionary<string, Dictionary<string, InputKey>>()
        {
            { "apple2_gamepad", apple2gamepad },
            { "apple2_joystick", apple2joystick },
            { "nes_gamepad", nesgamepad },
            { "snes_gamepad", snesgamepad },
            { "md_gamepad", mdgamepad },
            { "md_gamepad2", mdgamepad2 },
            { "md_gamepad6", mdgamepad6 },
            { "pce_gamepad", pcegamepad },
            { "pcfx_gamepad", pcfxgamepad },
            { "sms_gamepad", smsgamepad },
            { "ss_gamepad", ssgamepad },
            { "psx_gamepad", psxgamepad },
            { "psx_dualshock", psxdualshock },
            { "psx_dualshock_gtspecial", psxdualshockgt },
            { "wswan_gamepad", wswanhorizontal },
            { "wswan_gamepadraa", wswanvertical }
        };

        static readonly Dictionary<string, Dictionary<string, InputKey>> mappingToUsekb = new Dictionary<string, Dictionary<string, InputKey>>()
        {
            { "wswan_gamepad", wswanhorizontalkb },
            { "wswan_gamepadraa", wswanverticalkb }
        };

        static readonly Dictionary<string, Dictionary<string, string>> gunMappingToUse = new Dictionary<string, Dictionary<string, string>>()
        {
            { "nes", neszapper },
            { "snes", snessuperscope },
            { "ss", saturngun },
            { "guncon", psxguncon },
            { "justifier", psxjustifier }
        };
        #endregion

        #region Mednafen keycodes
        static readonly Dictionary<SDL.SDL_Keycode, int> mednafenKeyCodes = new Dictionary<SDL.SDL_Keycode, int>()
        {
            { SDL.SDL_Keycode.SDLK_UNKNOWN, 0 },
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

        #region dinputMappping

        static readonly Dictionary<InputKey, string> dinputMapping = new Dictionary<InputKey, string>()
        {
            { InputKey.b, "b" },
            { InputKey.a, "a" },
            { InputKey.y, "x" },
            { InputKey.x, "y" },
            { InputKey.up, "dpup" },
            { InputKey.down, "dpdown" },
            { InputKey.left, "dpleft" },
            { InputKey.right, "dpright" },
            { InputKey.pageup, "leftshoulder" },
            { InputKey.pagedown, "rightshoulder" },
            { InputKey.l2, "lefttrigger" },
            { InputKey.r2, "righttrigger" },
            { InputKey.l3, "leftstick" },
            { InputKey.r3, "rightstick" },
            { InputKey.select, "back" },
            { InputKey.start, "start" },
            { InputKey.leftanalogup, "-lefty" },
            { InputKey.leftanalogdown, "+lefty" },
            { InputKey.leftanalogleft, "-leftx" },
            { InputKey.leftanalogright, "+leftx" },
            { InputKey.rightanalogup, "-righty" },
            { InputKey.rightanalogdown, "+righty" },
            { InputKey.rightanalogleft, "-rightx" },
            { InputKey.rightanalogright, "+rightx" },
        };

        /*static readonly Dictionary<InputKey, string> ds4ds5dinputmapping = new Dictionary<InputKey, string>()
        {
            { InputKey.b, "button_2" },
            { InputKey.a, "button_1" },
            { InputKey.y, "button_0" },
            { InputKey.x, "button_3" },
            { InputKey.up, "abs_7-" },
            { InputKey.down, "abs_7+" },
            { InputKey.left, "abs_6-" },
            { InputKey.right, "abs_6+" },
            { InputKey.pageup, "button_4" },
            { InputKey.pagedown, "button_5" },
            { InputKey.l2, "button_6" },
            { InputKey.r2, "button_7" },
            { InputKey.l3, "button_10" },
            { InputKey.r3, "button_11" },
            { InputKey.select, "button_8" },
            { InputKey.start, "button_9" },
            { InputKey.leftanalogup, "abs_1-" },
            { InputKey.leftanalogdown, "abs_1+" },
            { InputKey.leftanalogleft, "abs_0-" },
            { InputKey.leftanalogright, "abs_0+" },
            { InputKey.rightanalogup, "abs_5-" },
            { InputKey.rightanalogdown, "abs_5+" },
            { InputKey.rightanalogleft, "abs_2-" },
            { InputKey.rightanalogright, "abs_2+" },
        };*/

        static readonly Dictionary<InputKey, string> xboxmapping = new Dictionary<InputKey, string>()
        {
            { InputKey.b, "button_13" },
            { InputKey.a, "button_12" },
            { InputKey.y, "button_14" },
            { InputKey.x, "button_15" },
            { InputKey.up, "button_0" },
            { InputKey.down, "button_1" },
            { InputKey.left, "button_2" },
            { InputKey.right, "button_3" },
            { InputKey.pageup, "button_8" },
            { InputKey.pagedown, "button_9" },
            { InputKey.l2, "abs_4+" },
            { InputKey.r2, "abs_5+" },
            { InputKey.l3, "button_6" },
            { InputKey.r3, "button_7" },
            { InputKey.select, "button_5" },
            { InputKey.start, "button_4" },
            { InputKey.leftanalogup, "abs_1+" },
            { InputKey.leftanalogdown, "abs_1-" },
            { InputKey.leftanalogleft, "abs_0-" },
            { InputKey.leftanalogright, "abs_0+" },
            { InputKey.rightanalogup, "abs_3+" },
            { InputKey.rightanalogdown, "abs_3-" },
            { InputKey.rightanalogleft, "abs_2-" },
            { InputKey.rightanalogright, "abs_2+" },
        };
        #endregion

        private void CleanUpConfigFile(string core, MednafenConfigFile cfg)
        {
            for (int i = 1; i <= inputPortCleanupNb[core]; i++)
                cfg[core + ".input.port" + i] = core == "apple2" ? "paddle" : "none";
        }

        static readonly Dictionary<string, int> inputPortCleanupNb = new Dictionary<string, int>()
        {
            { "apple2", 2 },
            { "md", 8 },
            { "nes", 4 },
            { "pce", 5 },
            { "pcfx", 8 },
            { "psx", 8 },
            { "snes", 2 },
            { "ss", 12 }
        };

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey, int nbAxis, bool hatfix)
        {
            if (c == null)
                return "";

            int direction = 1;

            if (buttonkey.StartsWith("-"))
            {
                buttonkey = buttonkey.Substring(1);
                direction = -1;
            }
            else if (buttonkey.StartsWith("+"))
            {
                buttonkey = buttonkey.Substring(1);
            }

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
                return "button_" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        if (hatfix)
                            return "abs_" + (nbAxis - 1) + "-";
                        else
                            return "abs_" + (nbAxis + 1) + "-";
                    case 2:
                        if (hatfix)
                            return "abs_" + (nbAxis - 2) + "+";
                        else
                            return "abs_" + nbAxis + "+";
                    case 4:
                        if (hatfix)
                            return "abs_" + (nbAxis - 1) + "+";
                        else
                            return "abs_" + (nbAxis + 1) + "+";
                    case 8:
                        if (hatfix)
                            return "abs_" + (nbAxis - 2) + "-";
                        else
                            return "abs_" + nbAxis + "-";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    direction = -1;
                }

                else if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    direction = 1;
                }

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                if (direction == 1) return "abs_" + axisID + "+";
                else return "abs_" + axisID + "-";
            }

            return "";
        }

        private static Dictionary<string, InputKey> ConfigureMappingPerSystem(Dictionary<string, InputKey> mapping, string system, string padType, MednafenConfigFile cfg)
        {
            Dictionary<string, InputKey> newMapping = mapping;
            if (system == "nes")
            {
                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                {
                    newMapping["a"] = InputKey.b;
                    newMapping["b"] = InputKey.a;
                    newMapping["rapid_a"] = InputKey.x;
                    newMapping["rapid_b"] = InputKey.y;
                }
                if (!Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                {
                    newMapping.Remove("rapid_a");
                    newMapping.Remove("rapid_b");

                    for (int i = 1; i <= 4; i++)
                    {
                        cfg["nes.input.port" + i + ".gamepad.rapid_a"] = "";
                        cfg["nes.input.port" + i + ".gamepad.rapid_b"] = "";
                    }
                }
            }
            else if (system == "megadrive" && padType == "gamepad6")
            {
                if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                {
                    newMapping["a"] = InputKey.a;
                    newMapping["b"] = InputKey.b;
                    newMapping["c"] = InputKey.pagedown;
                    newMapping["x"] = InputKey.y;
                    newMapping["z"] = InputKey.pageup;
                }
                else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                {
                    newMapping["x"] = InputKey.x;
                    newMapping["y"] = InputKey.pageup;
                }
            }
            else if (system == "mastersystem")
            {
                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                {
                    newMapping["fire1"] = InputKey.y;
                    newMapping["fire2"] = InputKey.a;
                    newMapping["rapid_fire1"] = InputKey.x;
                    newMapping["rapid_fire2"] = InputKey.b;
                }
            }
            else if (system == "saturn")
            {
                bool switchTriggers = Program.SystemConfig.getOptBoolean("saturn_invert_triggers");
                if (Program.SystemConfig.isOptSet("saturn_padlayout") && !string.IsNullOrEmpty(Program.SystemConfig["saturn_padlayout"]))
                {
                    switch (Program.SystemConfig["saturn_padlayout"])
                    {
                        case "lr_yz":
                            if (switchTriggers)
                            {
                                newMapping["a"] = InputKey.y;
                                newMapping["b"] = InputKey.a;
                                newMapping["c"] = InputKey.b;
                                newMapping["x"] = InputKey.x;
                                newMapping["ls"] = InputKey.pageup;
                                newMapping["rs"] = InputKey.pagedown;
                                newMapping["y"] = InputKey.l2;
                                newMapping["z"] = InputKey.r2;
                                break;
                            }
                            else
                            {
                                newMapping["a"] = InputKey.y;
                                newMapping["b"] = InputKey.a;
                                newMapping["c"] = InputKey.b;
                                newMapping["x"] = InputKey.x;
                                newMapping["y"] = InputKey.pageup;
                                newMapping["z"] = InputKey.pagedown;
                                break;
                            }
                        case "lr_xz":
                            if (switchTriggers)
                            {
                                newMapping["a"] = InputKey.y;
                                newMapping["b"] = InputKey.a;
                                newMapping["c"] = InputKey.b;
                                newMapping["ls"] = InputKey.pageup;
                                newMapping["rs"] = InputKey.pagedown;
                                newMapping["x"] = InputKey.l2;
                                newMapping["z"] = InputKey.r2;
                                break;
                            }
                            else
                            {
                                newMapping["a"] = InputKey.y;
                                newMapping["b"] = InputKey.a;
                                newMapping["c"] = InputKey.b;
                                newMapping["x"] = InputKey.pageup;
                                newMapping["z"] = InputKey.pagedown;
                                break;
                            }
                        case "lr_zc":
                            if (switchTriggers)
                            {
                                newMapping["ls"] = InputKey.pageup;
                                newMapping["rs"] = InputKey.pagedown;
                                newMapping["z"] = InputKey.l2;
                                newMapping["c"] = InputKey.r2;
                                break;
                            }
                            break;
                    }
                }
            }
            return newMapping;
        }

        private static readonly List<USB_PRODUCT> hatFix = new List<USB_PRODUCT>
        {
            USB_PRODUCT.NINTENDO_SWITCH_PRO
        };
    }
}