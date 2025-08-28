﻿using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using DI = SharpDX.DirectInput;

namespace EmulatorLauncher
{
    partial class Mame64Generator
    {
        private bool _sindenSoft = false;
        private GameMapping _gameMapping = null;
        private Layout _gameLayout = null;

        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>
            {
                "SDL_JOYSTICK_HIDAPI_WII = 1"
            };

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private bool ConfigureMameControllers(string path, bool hbmame, string rom)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return false;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for MAME");

            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }

            if (Controllers.Count == 0 && gunCount == 0)
                return false;

            else if (Controllers.Count == 1 && gunCount == 0)
            {
                var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                if (c1.IsKeyboard)
                    return false;
            }

            UpdateSdlControllersWithHints();

            // Get specific mapping if it exists
            string MappingFileName = Path.GetFileNameWithoutExtension(rom) + ".xml";
            string layout = "default";
            if (SystemConfig.isOptSet("mame_controller_layout") && !string.IsNullOrEmpty(SystemConfig["mame_controller_layout"]))
                layout = SystemConfig["mame_controller_layout"];

            string specificMappingPath = Path.Combine(AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "mame", MappingFileName);
            if (!File.Exists(specificMappingPath))
                specificMappingPath = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mame", MappingFileName);

            if (File.Exists(specificMappingPath))
            {
                var gameMapping = GameMapping.LoadGameFromXml(specificMappingPath);

                if (gameMapping != null)
                {
                    var gameLayout = gameMapping.Layouts.Where(l => l.Type == layout).FirstOrDefault();

                    if (gameLayout != null)
                    {
                        _gameMapping = gameMapping;
                        _gameLayout = gameLayout;
                    }
                }
            }

            // Delete existing config file if any
            string inputConfig = Path.Combine(path, "retrobat_auto.cfg");
            if (File.Exists(inputConfig))
                File.Delete(inputConfig);

            // Complex logic to get pad index for MAME
            // MAME uses dinput index for dinput but for hybrid ...
            // ... it reads first non-xinput controllers and sorts them by enumeration order
            // ... finally it reads other XInput controllers also sorted by enumeration index
            Dictionary<Controller, int> hybridController = new Dictionary<Controller, int>();
            var mameconfig = new XElement("mameconfig", new XAttribute("version", "10"));
            var system = new XElement("system", new XAttribute("name", "default"));
            var input = new XElement("input");
            var mameControllers = new List<Controller>();
            var xControllers = this.Controllers.Where(c => c.IsXInputDevice).OrderBy(i => i.XInput.DeviceIndex).ToList();
            var diDevices = new DirectInputInfo().GetDinputDevices();

            if (SystemConfig["mame_joystick_driver"] == "xinput")
                mameControllers = this.Controllers.Where(c => c.IsXInputDevice && !c.IsKeyboard).OrderBy(i => i.XInput.DeviceIndex).ToList();

            else if (SystemConfig["mame_joystick_driver"] == "dinput")
            {
                foreach (var ct in diDevices)
                {
                    var cont = this.Controllers.Where(c => c.DirectInput != null && c.DirectInput.InstanceGuid == ct.InstanceGuid).FirstOrDefault();
                    if (cont != null)
                        mameControllers.Add(cont);
                }
            }

            else
            {
                foreach (var ct in diDevices)
                {
                    var cont = this.Controllers.Where(c => c.DirectInput != null && !c.IsXInputDevice && c.DirectInput.InstanceGuid == ct.InstanceGuid).FirstOrDefault();
                    if (cont != null)
                        mameControllers.Add(cont);
                }
                if (xControllers.Count > 0)
                    mameControllers.AddRange(xControllers);
            }

            // Specific Gun4IR & Sinden mapping
            if (guns.Length > 0 && SystemConfig.isOptSet("mame_gun_config"))
            {
                if (guns[0] != null && guns[0].Type == RawLighGunType.Gun4Ir && SystemConfig["mame_gun_config"] == "gun4ir")
                {
                    bool multigun4ir = guns[1] != null && guns[1].Type == RawLighGunType.Gun4Ir;
                    ConfigureLightguns(input, RawLighGunType.Gun4Ir, multigun4ir, hbmame);

                    XDocument xdocgun = new XDocument(new XDeclaration("1.0", null, null));
                    xdocgun.Add(mameconfig);
                    mameconfig.Add(system);
                    system.Add(input);

                    xdocgun.Save(inputConfig);

                    if (!File.Exists(inputConfig))
                        return false;

                    return true;
                }

                else if (guns[0] != null && guns[0].Type == RawLighGunType.SindenLightgun && SystemConfig["mame_gun_config"] == "sinden")
                {
                    bool multiSinden = guns[1] != null && guns[1].Type == RawLighGunType.SindenLightgun;
                    ConfigureLightguns(input, RawLighGunType.SindenLightgun, multiSinden, hbmame);

                    XDocument xdocgun = new XDocument(new XDeclaration("1.0", null, null));
                    xdocgun.Add(mameconfig);
                    mameconfig.Add(system);
                    system.Add(input);

                    xdocgun.Save(inputConfig);

                    if (!File.Exists(inputConfig))
                        return false;

                    return true;
                }

                else if (guns[0] != null && guns[0].Type == RawLighGunType.RetroShooter && SystemConfig["mame_gun_config"] == "retroshooter")
                {
                    bool multiRetroshooters = guns[1] != null && guns[1].Type == RawLighGunType.RetroShooter;
                    ConfigureLightguns(input, RawLighGunType.RetroShooter, multiRetroshooters, hbmame);

                    XDocument xdocgun = new XDocument(new XDeclaration("1.0", null, null));
                    xdocgun.Add(mameconfig);
                    mameconfig.Add(system);
                    system.Add(input);

                    xdocgun.Save(inputConfig);

                    if (!File.Exists(inputConfig))
                        return false;

                    return true;
                }

                else if (guns[0] != null && guns[0].Type == RawLighGunType.Blamcon && SystemConfig["mame_gun_config"] == "blamcon")
                {
                    bool multiBlamcon = guns[1] != null && guns[1].Type == RawLighGunType.Blamcon;
                    ConfigureLightguns(input, RawLighGunType.Blamcon, multiBlamcon, hbmame);

                    XDocument xdocgun = new XDocument(new XDeclaration("1.0", null, null));
                    xdocgun.Add(mameconfig);
                    mameconfig.Add(system);
                    system.Add(input);

                    xdocgun.Save(inputConfig);

                    if (!File.Exists(inputConfig))
                        return false;

                    return true;
                }
            }

            // Generate controller mapping
            foreach (var controller in mameControllers)
            {
                int i = controller.PlayerIndex;
                int cIndex = mameControllers.ToList().IndexOf(controller) + 1;
                string joy = "JOYCODE_" + cIndex + "_";
                bool dpadonly = SystemConfig.isOptSet("mame_dpadandstick") && SystemConfig.getOptBoolean("mame_dpadandstick");
                bool isXinput = controller.IsXInputDevice && SystemConfig["mame_joystick_driver"] != "dinput";
                bool xinputCtrl = controller.IsXInputDevice;
                string guid = (controller.Guid.ToString()).Substring(0, 24) + "00000000";
                SdlToDirectInput ctrlr = null;
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

                if (SystemConfig["mame_joystick_driver"] == "xinput")
                {
                    int xIndex = mameControllers.OrderBy(c => c.DeviceIndex).ToList().IndexOf(controller) + 1;
                    joy = "JOYCODE_" + xIndex + "_";
                    input.Add(new XElement("mapdevice", new XAttribute("device", "XInput Player " + xIndex), new XAttribute("controller", "JOYCODE_" + xIndex)));
                }

                else
                {
                    joy = "JOYCODE_" + cIndex + "_";
                }

                // Override index through option
                string indexOption = "mame_p" + controller.PlayerIndex + "_forceindex";
                if (SystemConfig.isOptSet(indexOption) && !string.IsNullOrEmpty(SystemConfig[indexOption]))
                    joy = "JOYCODE_" + SystemConfig[indexOption] + "_";

                // Get dinput mapping information
                if (!isXinput)
                {
                    if (!File.Exists(gamecontrollerDB))
                    {
                        SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                        gamecontrollerDB = null;
                    }
                    if (gamecontrollerDB != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Player " + i + " . Fetching gamecontrollerdb.txt file with guid : " + guid);

                        try
                        {
                            if (SystemConfig.getOptBoolean("analogDpad"))
                                ctrlr = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid, true);
                            else
                                ctrlr = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);
                        }
                        catch { }

                        if (ctrlr == null || ctrlr.ButtonMappings == null)
                            SimpleLogger.Instance.Info("[INFO] Player " + i + ". No controller mapping found in gamecontrollerdb.txt file for guid : " + guid);
                        else
                            SimpleLogger.Instance.Info("[INFO] Player " + i + ": controller mapping found in gamecontrollerDB file.");
                    }
                }

                // GUNS
                string mouseIndex1 = "1";
                string mouseIndex2 = "2";

                if (gunCount > 0 && guns.Length > 0)
                {
                    SimpleLogger.Instance.Info("[GUNS] Found " + guns.Length + " gun(s).");

                    mouseIndex1 = (guns[0].Index + 1).ToString();
                    SimpleLogger.Instance.Info("[GUNS] Gun 1 index : " + mouseIndex1);

                    if (_multigun && guns.Length > 1)
                    {
                        SimpleLogger.Instance.Info("[GUNS] Multimouse enabled");
                        mouseIndex2 = (guns[1].Index + 1).ToString();
                        SimpleLogger.Instance.Info("[GUNS] Gun 2 index : " + mouseIndex2);
                    }
                }

                if (SystemConfig.isOptSet("mame_gun1") && !string.IsNullOrEmpty(SystemConfig["mame_gun1"]))
                {
                    mouseIndex1 = SystemConfig["mame_gun1"];
                    SimpleLogger.Instance.Info("[GUNS] Overwriting Gun 1 index : " + mouseIndex1);
                }
                if (SystemConfig.isOptSet("mame_gun2") && !string.IsNullOrEmpty(SystemConfig["mame_gun2"]))
                {
                    mouseIndex2 = SystemConfig["mame_gun2"];
                    SimpleLogger.Instance.Info("[GUNS] Overwriting Gun 2 index : " + mouseIndex2);
                }

                // define mapping for xInput case
                var mapping = hbmame ? hbxInputMapping : xInputMapping;

                // Invert player 1 & 2 with feature
                bool invert = SystemConfig.getOptBoolean("mame_indexswitch") && mameControllers.Count > 1;
                if (invert)
                {
                    if (i == 1)
                        i = 2;
                    else if (i == 2)
                        i = 1;
                }

                // PLAYER 1
                // Add UI mapping for player 1 to control MAME UI + Service menu
                if (i == 1)
                {
                    if (isXinput)
                        ConfigurePlayer1XInput(i, input, mapping, joy, mouseIndex1, hbmame, dpadonly);
                    else
                        ConfigurePlayer1DInput(i, input, ctrlr, joy, mouseIndex1, hbmame, dpadonly, xinputCtrl);
                }

                // OTHER PLAYERS
                // Max 8 players for mame
                else if (i <= 8)
                {
                    if (isXinput)
                        ConfigurePlayersXInput(i, input, mapping, joy, mouseIndex2, hbmame, dpadonly);
                    else
                        ConfigurePlayersDInput(i, input, ctrlr, joy, mouseIndex2, dpadonly, xinputCtrl);
                }

                SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
            }

            // Generate xml document
            XDocument xdoc = new XDocument(new XDeclaration("1.0", null, null));
            xdoc.Add(mameconfig);
            mameconfig.Add(system);
            system.Add(input);

            xdoc.Save(inputConfig);

            if (!File.Exists(inputConfig))
                return false;

            return true;
        }

        #region configuration
        private void ConfigurePlayer1XInput(int i, XElement input, Dictionary<string, string> mapping, string joy, string mouseIndex1, bool hbmame, bool dpadonly)
        {
            if (hbmame)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "UI_CONFIGURE"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["south"] + " OR KEYCODE_TAB")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "UI_MENU"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["south"] + " OR KEYCODE_TAB")));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR KEYCODE_ENTER")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_BACK"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR KEYCODE_ESC")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_CANCEL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["start"] + " OR KEYCODE_ESC")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"] + " OR KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"] + " OR KEYCODE_DOWN")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"] + " OR KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"] + " OR KEYCODE_RIGHT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_PAUSE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["east"] + " OR KEYCODE_P")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_REWIND_SINGLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["left"] + " OR KEYCODE_TILDE KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_FAST_FORWARD"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["right"] + " OR KEYCODE_INSERT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_SAVE_STATE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["west"] + " OR KEYCODE_F7 KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_LOAD_STATE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["north"] + " OR KEYCODE_F7")));

            input.Add(new XElement
                ("port", new XAttribute("type", "SERVICE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " " + joy + mapping["r3"] + " OR KEYCODE_F2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "SERVICE1"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " " + joy + mapping["r3"] + " OR KEYCODE_9")));

            input.Add(new XElement
                ("port", new XAttribute("type", "TILT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["r1"] + " OR KEYCODE_T")));

            input.Add(new XElement
                ("port", new XAttribute("type", "TILT1"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["r1"] + " OR KEYCODE_T")));

            // Start & coin
            input.Add(new XElement
                ("port", new XAttribute("type", "START" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "COIN" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_5")));

            // Specific mapping if available
            if (!hbmame && _gameLayout != null)
            {
                if (_gameMapping.Name != null)
                    SimpleLogger.Instance.Info("[INFO] Performing specific mapping (xinput) for: " + _gameMapping.Name);

                foreach (var button in _gameLayout.Buttons)
                {
                    string player = button.Player;
                    if (player != null && player != "1")
                        continue;
                    if (button.Type.Contains("P2_") || button.Type.Contains("P3_") || button.Type.Contains("P4_") || button.Type.Contains("P5_") || button.Type.Contains("P6_"))
                        continue;
                    if (button.Type.Contains("START2") || button.Type.Contains("START3") || button.Type.Contains("START4") || button.Type.Contains("START5") || button.Type.Contains("START6"))
                        continue;
                    if (button.Type.Contains("COIN2") || button.Type.Contains("COIN3") || button.Type.Contains("COIN4") || button.Type.Contains("COIN5") || button.Type.Contains("COIN6"))
                        continue;

                    var port = new XElement("port",
                        new XAttribute("type", button.Type),
                        button.Tag == null ? null : new XAttribute("tag", button.Tag),
                        button.Mask == null ? null : new XAttribute("mask", button.Mask),
                        button.DefValue == null ? null : new XAttribute("defvalue", button.DefValue));

                    foreach (var map in button.ButtonMappings)
                    {
                        string mappingText = map.Mapping;
                        if (!string.Equals(mappingText, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            mappingText = GetSpecificMappingX(joy, mouseIndex1, mappingText, i);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }
                  
                    input.Add(port);
                }

                return;
            }

            // Standard joystick buttons and directions
            if (dpadonly)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR KEYCODE_UP")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR KEYCODE_DOWN")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR KEYCODE_LEFT")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR KEYCODE_RIGHT")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"] + " OR KEYCODE_UP")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"] + " OR KEYCODE_DOWN")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"] + " OR KEYCODE_LEFT")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"] + " OR KEYCODE_RIGHT")));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsup"] + " OR KEYCODE_I")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsdown"] + " OR KEYCODE_K")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsleft"] + " OR KEYCODE_J")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsright"] + " OR KEYCODE_L")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsup"] + " OR KEYCODE_E")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsdown"] + " OR KEYCODE_D")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsleft"] + " OR KEYCODE_S")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsright"] + " OR KEYCODE_F")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"] + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["north"] + " OR KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l1"] + " OR KEYCODE_Z")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r1"] + " OR KEYCODE_X")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"] + " OR KEYCODE_C")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"] + " OR KEYCODE_V")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON9"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " OR KEYCODE_B")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON10"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r3"] + " OR KEYCODE_N")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_START"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_5")));

            // Pedals and other devices
            if (hbmame)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"]),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"] + " KEYCODE_LCONTROL")));


                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"]),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"] + " OR KEYCODE_LALT")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"] + "_NEG"),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"] + " KEYCODE_LCONTROL")));


                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"] + "_NEG"),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"] + " OR KEYCODE_LALT")));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_SPACE")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_POSITIONAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_POSITIONAL_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rs_y"]),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_Z"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_A")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));
        }

        private void ConfigurePlayer1DInput(int i, XElement input, SdlToDirectInput ctrlr, string joy, string mouseIndex1, bool hbmame, bool dpadonly, bool xinputCtrl)
        {
            if (hbmame)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "UI_CONFIGURE"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_TAB")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "UI_MENU"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_TAB")));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_ENTER")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_BACK"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_ESC")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_CANCEL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_ESC")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_DOWN")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_RIGHT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_PAUSE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_P")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_REWIND_SINGLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_TILDE KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_FAST_FORWARD"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_INSERT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_SAVE_STATE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_F7 KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_LOAD_STATE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_F7")));

            input.Add(new XElement
                ("port", new XAttribute("type", "SERVICE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_F2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "SERVICE1"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_9")));

            input.Add(new XElement
                ("port", new XAttribute("type", "TILT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_T")));

            input.Add(new XElement
                ("port", new XAttribute("type", "TILT1"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_T")));

            // Specific mapping if available
            if (!hbmame && _gameLayout != null)
            {
                if (_gameMapping.Name != null)
                    SimpleLogger.Instance.Info("[INFO] Performing specific mapping (dinput) for: " + _gameMapping.Name);

                foreach (var button in _gameLayout.Buttons)
                {
                    string player = button.Player;
                    if (player != null && player != "1")
                        continue;
                    if (button.Type.Contains("P2_") || button.Type.Contains("P3_") || button.Type.Contains("P4_") || button.Type.Contains("P5_") || button.Type.Contains("P6_"))
                        continue;
                    if (button.Type.Contains("START2") || button.Type.Contains("START3") || button.Type.Contains("START4") || button.Type.Contains("START5") || button.Type.Contains("START6"))
                        continue;
                    if (button.Type.Contains("COIN2") || button.Type.Contains("COIN3") || button.Type.Contains("COIN4") || button.Type.Contains("COIN5") || button.Type.Contains("COIN6"))
                        continue;

                    var port = new XElement("port",
                        new XAttribute("type", button.Type),
                        button.Tag == null ? null : new XAttribute("tag", button.Tag),
                        button.Mask == null ? null : new XAttribute("mask", button.Mask),
                        button.DefValue == null ? null : new XAttribute("defvalue", button.DefValue));

                    foreach (var map in button.ButtonMappings)
                    {
                        string mappingText = map.Mapping;
                        if (!string.Equals(mappingText, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            mappingText = GetSpecificMappingD(ctrlr, joy, mouseIndex1, mappingText, i, xinputCtrl);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }

                    input.Add(port);
                }

                return;
            }

            // Standard joystick buttons and directions
            if (dpadonly)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR KEYCODE_UP")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR KEYCODE_DOWN")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_LEFT")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_RIGHT")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_UP")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_DOWN")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_LEFT")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_RIGHT")));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1) + " OR KEYCODE_I")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1) + " OR KEYCODE_K")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1) + " OR KEYCODE_J")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1) + " OR KEYCODE_L")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_E")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_D")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_S")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_F")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl) + " OR KEYCODE_Z")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_X")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl) + " OR KEYCODE_C")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl) + " OR KEYCODE_V")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON9"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " OR KEYCODE_B")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON10"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_N")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_START"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5")));

            // Pedals and other devices
            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl, 1)),
                    new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " KEYCODE_LCONTROL")));


            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl, 1)),
                    new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_SPACE")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_POSITIONAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_POSITIONAL_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 0)),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_Z"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_A")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_XAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex1 + "_YAXIS"),
                    new XElement("newseq", new XAttribute("type", "increment"), "KEYCODE_DOWN"),
                    new XElement("newseq", new XAttribute("type", "decrement"), "KEYCODE_UP")));

            // Start & coin
            input.Add(new XElement
                ("port", new XAttribute("type", "START" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "COIN" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5")));
        }

        private void ConfigurePlayersXInput(int i, XElement input, Dictionary<string, string> mapping, string joy, string mouseIndex2, bool hbmame, bool dpadonly)
        {
            // Specific mapping if available
            if (!hbmame && _gameLayout != null)
            {
                if (_gameMapping.Name != null)
                    SimpleLogger.Instance.Info("[INFO] Performing specific mapping (xinput) for: " + _gameMapping.Name);

                foreach (var button in _gameLayout.Buttons)
                {
                    string player = button.Player;
                    if (player != null && player == "1")
                        continue;
                    if (button.Type.Contains("P1_"))
                        continue;
                    if (button.Type.Contains("START1"))
                        continue;
                    if (button.Type.Contains("COIN1"))
                        continue;

                    var port = new XElement("port",
                        new XAttribute("type", button.Type),
                        button.Tag == null ? null : new XAttribute("tag", button.Tag),
                        button.Mask == null ? null : new XAttribute("mask", button.Mask),
                        button.DefValue == null ? null : new XAttribute("defvalue", button.DefValue));

                    foreach (var map in button.ButtonMappings)
                    {
                        string mappingText = map.Mapping;
                        if (!string.Equals(mappingText, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            mappingText = GetSpecificMappingX(joy, mouseIndex2, mappingText, i);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }

                    input.Add(port);
                }

                return;
            }

            if (dpadonly)
            {
                input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"])));
            }
            else
            {
                input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"])));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsup"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsdown"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsleft"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsright"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsup"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsdown"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsleft"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsright"])));

            // Case of 2 guns only for now, cannot test more than 2 guns so stop here
            if (_multigun && (i == 2))
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON1 OR GUNCODE_" + mouseIndex2 + "_BUTTON1")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON3 OR GUNCODE_" + mouseIndex2 + "_BUTTON2")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"] + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON2")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                         new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"])));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["north"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l1"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r1"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON9"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON10"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r3"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_START"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "START" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "COIN" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"])));

            // Pedals and other devices
            if (hbmame)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"]),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"]),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"])));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r2"] + "_NEG"),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l2"] + "_NEG"),
                        new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["east"])));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                    new XElement("newseq", new XAttribute("type", "increment"), joy + mapping["south"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rs_y"])));

            if (_multigun && (i == 2))
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex2 + "_XAXIS OR GUNCODE_" + mouseIndex2 + "_XAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex2 + "_YAXIS OR GUNCODE_" + mouseIndex2 + "_YAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_XAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_YAXIS")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_x"])));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["ls_y"])));
            }
        }

        private void ConfigurePlayersDInput(int i, XElement input, SdlToDirectInput ctrlr, string joy, string mouseIndex2, bool dpadonly, bool xinputCtrl)
        {
            if (_gameLayout != null)
            {
                if (_gameMapping.Name != null)
                    SimpleLogger.Instance.Info("[INFO] Performing specific mapping (dinput) for: " + _gameMapping.Name);

                foreach (var button in _gameLayout.Buttons)
                {
                    string player = button.Player;
                    if (player != null && player == "1")
                        continue;
                    if (button.Type.Contains("P1_"))
                        continue;
                    if (button.Type.Contains("START1"))
                        continue;
                    if (button.Type.Contains("COIN1"))
                        continue;

                    var port = new XElement("port",
                        new XAttribute("type", button.Type),
                        button.Tag == null ? null : new XAttribute("tag", button.Tag),
                        button.Mask == null ? null : new XAttribute("mask", button.Mask),
                        button.DefValue == null ? null : new XAttribute("defvalue", button.DefValue));

                    foreach (var map in button.ButtonMappings)
                    {
                        string mappingText = map.Mapping;
                        if (!string.Equals(mappingText, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            mappingText = GetSpecificMappingD(ctrlr, joy, mouseIndex2, mappingText, i, xinputCtrl);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }

                    input.Add(port);
                }

                return;
            }

            if (dpadonly)
            {
                input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl))));
            }
            else
            {
                input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1))));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1))));

            // Case of 2 guns only for now, cannot test more than 2 guns so stop here
            if (_multigun && (i == 2))
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON1 OR GUNCODE_" + mouseIndex2 + "_BUTTON1")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON3 OR GUNCODE_" + mouseIndex2 + "_BUTTON2")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR MOUSECODE_" + mouseIndex2 + "_BUTTON2")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                         new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl))));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "y", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON9"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_BUTTON10"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_START"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "START" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "COIN" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl))));

            // Pedals and other devices
            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl, 1)),
                    new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL2"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl, 1)),
                    new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PEDAL3"),
                    new XElement("newseq", new XAttribute("type", "increment"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_PADDLE_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_DIAL_V"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_TRACKBALL_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));

            input.Add(new XElement
                ("port", new XAttribute("type", "P" + i + "_AD_STICK_Z"),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 0))));

            if (_multigun && (i == 2))
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex2 + "_XAXIS OR GUNCODE_" + mouseIndex2 + "_XAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex2 + "_YAXIS OR GUNCODE_" + mouseIndex2 + "_YAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_MOUSE_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_XAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_MOUSE_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), "MOUSECODE_" + mouseIndex2 + "_YAXIS")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0))));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P" + i + "_LIGHTGUN_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0))));
            }
        }

        private void ConfigureLightguns(XElement input, RawLighGunType lightgunType, bool multi = false, bool hbmame = false)
        {
            if (lightgunType == RawLighGunType.Gun4Ir)
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_2341&PID_8042"), new XAttribute("controller", "GUNCODE_1")));
            else if (lightgunType == RawLighGunType.SindenLightgun)
            {
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_16C0&PID_0F38"), new XAttribute("controller", "GUNCODE_1")));
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_16C0&PID_0F01"), new XAttribute("controller", "GUNCODE_1")));
            }
            else if (lightgunType == RawLighGunType.RetroShooter)
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_0483&PID_5750"), new XAttribute("controller", "GUNCODE_1")));
            else if (lightgunType == RawLighGunType.Blamcon)
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_3673&PID_0101"), new XAttribute("controller", "GUNCODE_1")));

            if (multi && lightgunType == RawLighGunType.Gun4Ir)
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_2341&PID_8043"), new XAttribute("controller", "GUNCODE_2")));
            else if (multi && lightgunType == RawLighGunType.SindenLightgun)
            {
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_16C0&PID_0F39"), new XAttribute("controller", "GUNCODE_2")));
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_16C0&PID_0F02"), new XAttribute("controller", "GUNCODE_2")));
            }
            else if (multi && lightgunType == RawLighGunType.RetroShooter)
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_0483&PID_5751"), new XAttribute("controller", "GUNCODE_2")));
            else if (multi && lightgunType == RawLighGunType.Blamcon)
                input.Add(new XElement("mapdevice", new XAttribute("device", "VID_3673&PID_0102"), new XAttribute("controller", "GUNCODE_2")));

            if (hbmame)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "UI_CONFIGURE"),
                        new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_5 GUNCODE_1_BUTTON2" + " OR KEYCODE_TAB")));
            }
            else
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "UI_MENU"),
                        new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_5 GUNCODE_1_BUTTON2" + " OR KEYCODE_TAB")));
            }

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON1" + " OR GUNCODE_1_BUTTON2" + " OR KEYCODE_ENTER")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_BACK"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON3" + " OR KEYCODE_ESC")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_CANCEL"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_5 GUNCODE_1_BUTTON3" + " OR KEYCODE_ESC")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_DOWN")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_RIGHT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_PAUSE"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_5 KEYCODE_UP" + " OR KEYCODE_P")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_REWIND_SINGLE"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_TILDE KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_FAST_FORWARD"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_INSERT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_SAVE_STATE"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_F7 KEYCODE_LSHIFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "UI_LOAD_STATE"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_F7")));

            input.Add(new XElement
                ("port", new XAttribute("type", "SERVICE"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_5 KEYCODE_DOWN" + " OR KEYCODE_F2")));

            input.Add(new XElement
                ("port", new XAttribute("type", "SERVICE1"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_5 KEYCODE_DOWN" + " OR KEYCODE_9")));

            // Standard joystick buttons and directions
            input.Add(new XElement
                ("port", new XAttribute("type", "P1_JOYSTICK_UP"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_UP")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_JOYSTICK_DOWN"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_DOWN")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_JOYSTICK_LEFT"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_LEFT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_JOYSTICK_RIGHT"),
                    new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_RIGHT")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_BUTTON1"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_BUTTON2"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON4 OR GUNCODE_1_BUTTON2 OR KEYCODE_F1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_BUTTON3"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON3")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_START"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON6 OR KEYCODE_1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_SELECT"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON5 OR KEYCODE_5")));

            // Start & coin
            input.Add(new XElement
                ("port", new XAttribute("type", "START1"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON6 OR KEYCODE_1")));

            input.Add(new XElement
                ("port", new XAttribute("type", "COIN1"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_BUTTON5 OR KEYCODE_5")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_AD_STICK_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_XAXIS")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_AD_STICK_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_YAXIS")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_LIGHTGUN_X"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_XAXIS")));

            input.Add(new XElement
                ("port", new XAttribute("type", "P1_LIGHTGUN_Y"),
                    new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_1_YAXIS")));

            if (multi)
            {
                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_JOYSTICK_UP"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON7")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_JOYSTICK_DOWN"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON9")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_JOYSTICK_LEFT"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON10")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_JOYSTICK_RIGHT"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON8")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_BUTTON1"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON1")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_BUTTON2"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON4 OR GUNCODE_2_BUTTON2")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_BUTTON3"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON3")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_START"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON6 OR KEYCODE_2")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_SELECT"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON5 OR KEYCODE_6")));

                // Start & coin
                input.Add(new XElement
                    ("port", new XAttribute("type", "START2"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON6 OR KEYCODE_2")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "COIN2"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_BUTTON5 OR KEYCODE_6")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_AD_STICK_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_XAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_AD_STICK_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_YAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_LIGHTGUN_X"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_XAXIS")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "P2_LIGHTGUN_Y"),
                        new XElement("newseq", new XAttribute("type", "standard"), "GUNCODE_2_YAXIS")));
            }
        }

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey, bool isXinput, int axisDirection = 0)
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

            if (buttonkey == "dpup" || buttonkey == "dpleft")
                axisDirection = -1;
            else if (buttonkey == "dpdown" || buttonkey == "dpright")
                axisDirection = 1;

            string button = c.ButtonMappings[buttonkey];

            // For xInput : specific treatment of axis
            if (isXinput && button == "a5")
                button = "a2";
            if (isXinput && button == "a2")
                axisDirection = 1;

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger()) + 1;
                return "BUTTON" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "HAT1UP";
                    case 2:
                        return "HAT1RIGHT";
                    case 4:
                        return "HAT1DOWN";
                    case 8:
                        return "HAT1LEFT";
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

                switch (axisID)
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
                }
            }
            return "";
        }

        private string GetSpecificMappingX(string joy, string mouseindex, string mapping, int player)
        {
            string ret = "";

            string[] parts = mapping.Split(new string[] { "OR" }, StringSplitOptions.None);

            foreach (string part in parts)
            {
                string value = part.Trim();

                if (value.StartsWith("JOY"))
                {
                    string[] mapParts = value.Split('_');
                    if (mapParts.Length > 1)
                    {
                        string source = mapParts[1];
                        string target = null;

                        if (specificToXinput.ContainsKey(source))
                            source = specificToXinput[source];

                        if (xInputMapping.ContainsKey(source))
                        {
                            target = xInputMapping[source];

                            if (string.IsNullOrEmpty(ret))
                                ret += joy + target;
                            else
                                ret += " OR " + joy + target;
                        }
                    }
                }
                else if (value.StartsWith("GUN"))
                {
                    if (player > 2)
                        ret = "";
                    else if (player == 2 && !_multigun)
                        ret = "";
                    else
                    {
                        string[] mapParts = value.Split('_');
                        if (mapParts.Length > 1)
                        {
                            string target = mapParts[1];
                            if (string.IsNullOrEmpty(ret))
                                ret += "GUNCODE_" + mouseindex + "_" + target;
                            else
                                ret += " OR " + "GUNCODE_" + mouseindex + "_" + target;
                        }
                    }
                }
                else if (value.StartsWith("MOUSE"))
                {
                    if (player > 2)
                        ret = "";
                    else if (player == 2 && !_multigun)
                        ret = "";
                    else
                    {
                        string[] mapParts = value.Split('_');
                        if (mapParts.Length > 1)
                        {
                            string target = mapParts[1];
                            if (string.IsNullOrEmpty(ret))
                                ret += "MOUSECODE_" + mouseindex + "_" + target;
                            else
                                ret += " OR " + "MOUSECODE_" + mouseindex + "_" + target;
                        }
                    }
                }

                else if (value.StartsWith("KEY"))
                {
                    if (string.IsNullOrEmpty(ret))
                        ret += value;
                    else
                        ret += " OR " + value;
                }
            }

            return ret;
        }

        private string GetSpecificMappingD(SdlToDirectInput c, string joy, string mouseindex, string mapping, int player, bool isXinput)
        {
            string ret = "";

            string[] parts = mapping.Split(new string[] { "OR" }, StringSplitOptions.None);

            foreach (string part in parts)
            {
                string value = part.Trim();

                if (value.StartsWith("JOY"))
                {
                    string[] mapParts = value.Split('_');
                    if (mapParts.Length > 1)
                    {
                        int axisDirection = 0;
                        string source = mapParts[1];
                        string target = null;

                        if (source.EndsWith("trigger"))
                        {
                            switch (source)
                            {
                                case "l2trigger":
                                    source = "lefttrigger";
                                    axisDirection = 1;
                                    break;
                                case "r2trigger":
                                    source = "righttrigger";
                                    axisDirection = 1;
                                    break;
                            }
                        }

                        if (specificToDInput.ContainsKey(source))
                            source = specificToDInput[source];

                        if (source.StartsWith("ls"))
                        {
                            switch (source)
                            {
                                case "lsup":
                                    axisDirection = -1;
                                    source = "lefty";
                                    break;
                                case "lsdown":
                                    axisDirection = 1;
                                    source = "lefty";
                                    break;
                                case "lsleft":
                                    axisDirection = -1;
                                    source = "leftx";
                                    break;
                                case "lsright":
                                    axisDirection = 1;
                                    source = "leftx";
                                    break;
                            }
                        }

                        else if (source.StartsWith("rs"))
                        {
                            switch (source)
                            {
                                case "rsup":
                                    axisDirection = -1;
                                    source = "righty";
                                    break;
                                case "rsdown":
                                    axisDirection = 1;
                                    source = "righty";
                                    break;
                                case "rsleft":
                                    axisDirection = -1;
                                    source = "rightx";
                                    break;
                                case "rsright":
                                    axisDirection = 1;
                                    source = "rightx";
                                    break;
                            }
                        }

                        else if (specificToXinput.ContainsKey(source))
                        {
                            axisDirection = 0;
                            switch (source)
                            {
                                case "leftstickx":
                                    source = "leftx";
                                    break;
                                case "leftsticky":
                                    source = "lefty";
                                    break;
                                case "rightstickx":
                                    source = "rightx";
                                    break;
                                case "rightsticky":
                                    source = "righty";
                                    break;
                            }
                        }

                        target = GetDinputMapping(c, source, isXinput, axisDirection);

                        if (!string.IsNullOrEmpty(target))
                        {
                            if (string.IsNullOrEmpty(ret))
                                ret += joy + target;
                            else
                                ret += " OR " + joy + target;
                        }
                    }
                }
                else if (value.StartsWith("GUN"))
                {
                    if (player > 2)
                        ret = "";
                    else if (player == 2 && !_multigun)
                        ret = "";
                    else
                    {
                        string[] mapParts = value.Split('_');
                        if (mapParts.Length > 1)
                        {
                            string target = mapParts[1];
                            if (string.IsNullOrEmpty(ret))
                                ret += "GUNCODE_" + mouseindex + "_" + target;
                            else
                                ret += " OR " + "GUNCODE_" + mouseindex + "_" + target;
                        }
                    }
                }
                else if (value.StartsWith("MOUSE"))
                {
                    if (player > 2)
                        ret = "";
                    else if (player == 2 && !_multigun)
                        ret = "";
                    else
                    {
                        string[] mapParts = value.Split('_');
                        if (mapParts.Length > 1)
                        {
                            string target = mapParts[1];
                            if (string.IsNullOrEmpty(ret))
                                ret += "MOUSECODE_" + mouseindex + "_" + target;
                            else
                                ret += " OR " + "MOUSECODE_" + mouseindex + "_" + target;
                        }
                    }
                }

                else if (value.StartsWith("KEY"))
                {
                    if (string.IsNullOrEmpty(ret))
                        ret += value;
                    else
                        ret += " OR " + value;
                }
            }

            return ret;
        }
        #endregion

        #region Xinput mapping dictionnaries
        static readonly Dictionary<string, string> xInputMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON7" },
            { "r3",             "BUTTON8" },
            { "l2",             "SLIDER1" },
            { "r2",             "SLIDER2" },
            { "l2trigger",      "SLIDER1_NEG" },
            { "r2trigger",      "SLIDER2_NEG" },
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "START" },
            { "select",         "SELECT" },
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "HAT1UP" },
            { "down",           "HAT1DOWN" },
            { "left",           "HAT1LEFT" },
            { "right",          "HAT1RIGHT" },
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH" },
            { "rsup",           "RZAXIS_NEG_SWITCH" },
            { "rsdown",         "RZAXIS_POS_SWITCH" },
            { "rsleft",         "ZAXIS_NEG_SWITCH" },
            { "rsright",        "ZAXIS_POS_SWITCH" },
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };

        static readonly Dictionary<string, string> specificToXinput = new Dictionary<string, string>()
        {
            { "leftstickx",           "ls_x" },
            { "leftsticky",           "ls_y" },
            { "rightstickx",           "rs_x" },
            { "rightsticky",           "rs_y" }
        };

        static readonly Dictionary<string, string> specificToDInput = new Dictionary<string, string>()
        {
            { "l3",             "leftstick" },
            { "r3",             "rightstick" },
            { "l2",             "lefttrigger" },
            { "r2",             "righttrigger" },
            { "north",          "y" },
            { "south",          "a" },
            { "west",           "x" },
            { "east",           "b" },
            { "start",          "start" },
            { "select",         "back" },
            { "l1",             "leftshoulder" },
            { "r1",             "rightshoulder" },
            { "up",             "dpup" },
            { "down",           "dpdown" },
            { "left",           "dpleft" },
            { "right",          "dpright" },
        };

        static readonly Dictionary<string, string> hbxInputMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON9"},
            { "r3",             "BUTTON10"},
            { "l2",             "RZAXIS_NEG_SWITCH" },  //differs
            { "r2",             "ZAXIS_NEG_SWITCH"},    //differs
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "BUTTON7" },    //differs
            { "select",         "BUTTON8" },    //differs
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "DPADUP" },     //differs
            { "down",           "DPADDOWN" },   //differs
            { "left",           "DPADLEFT" },   //differs
            { "right",          "DPADRIGHT" },  //differs
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH"},
            { "rsup",           "RYAXIS_NEG_SWITCH" },  //differs
            { "rsdown",         "RYAXIS_POS_SWITCH" },  //differs
            { "rsleft",         "RXAXIS_NEG_SWITCH" },  //differs
            { "rsright",        "RXAXIS_POS_SWITCH"},   //differs
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };
        #endregion
    }

    public class ButtonMapping
    {
        public string Type { get; set; }
        public string Mapping { get; set; }
    }

    public class Button
    {
        public string Type { get; set; }
        public string Player { get; set; }
        public string Tag { get; set; }
        public string Mask { get; set; }
        public string DefValue { get; set; }
        public string Function { get; set; }
        public string X { get; set; }
        public string Y { get; set; }
        public string Color { get; set; }
        public List<ButtonMapping> ButtonMappings { get; set; }
    }

    public class Layout
    {
        public string Type { get; set; }
        public string JoystickColor { get; set; }
        public List<Button> Buttons { get; set; }
    }

    public class GameMapping
    {
        public string Name { get; set; }
        public string Rom { get; set; }
        public List<Layout> Layouts { get; set; }

        private static string GetAttr(XElement element, string attrName)
        {
            return element == null ? null : (string)element.Attribute(attrName);
        }

        private static XElement GetElem(XElement parent, string name)
        {
            return parent == null ? null : parent.Element(name);
        }

        public static GameMapping LoadGameFromXml(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var gameElement = doc.Root;
            if (gameElement == null) return null;

            var game = new GameMapping
            {
                Name = GetAttr(gameElement, "name"),
                Rom = GetAttr(gameElement, "rom"),
                Layouts = new List<Layout>()
            };

            var layoutsElement = GetElem(gameElement, "layouts");
            if (layoutsElement != null)
            {
                foreach (var l in layoutsElement.Elements("layout"))
                {
                    var layout = new Layout
                    {
                        Type = GetAttr(l, "type"),
                        JoystickColor = GetAttr(l, "joystickcolor"),
                        Buttons = new List<Button>()
                    };

                    foreach (var b in l.Elements("button"))
                    {
                        var button = new Button
                        {
                            Type = GetAttr(b, "type"),
                            Player = GetAttr(b, "player"),
                            Tag = GetAttr(b, "tag"),
                            Mask = GetAttr(b, "mask"),
                            DefValue = GetAttr(b, "defvalue"),
                            Function = GetAttr(b, "function"),
                            X = GetAttr(b, "x"),
                            Y = GetAttr(b, "y"),
                            Color = GetAttr(b, "color"),
                            ButtonMappings = new List<ButtonMapping>()
                        };

                        foreach (var m in b.Elements("mapping"))
                        {
                            var mapping = new ButtonMapping
                            {
                                Type = GetAttr(m, "type"),
                                Mapping = GetAttr(m, "name"),
                            };
                            button.ButtonMappings.Add(mapping);
                        }
                        layout.Buttons.Add(button);
                    }
                    game.Layouts.Add(layout);
                }
            }

            return game;
        }
    }
}
