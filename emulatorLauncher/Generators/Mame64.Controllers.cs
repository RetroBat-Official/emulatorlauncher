using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DI = SharpDX.DirectInput;

namespace EmulatorLauncher
{
    partial class Mame64Generator
    {
        private bool _sindenSoft = false;
        private bool _messcfgInput = false;
        private GameMapping _gameMapping = null;
        private Layout _gameLayout = null;
        private List<string> _filesToRestore = new List<string>();
        private string _messSystem = null;
        static readonly Dictionary<string, string> messFiles = new Dictionary<string, string>()
            {
                { "advision", "advision"},
                { "apfm1000", "apfm1000"},
                { "arcadia", "arcadia"},
                { "astrocade", "astrocde"},
                { "casloopy", "casloopy"},
                { "crvision", "crvision"},
                { "gamecom", "gamecom"},
                { "supracan", "supracan"}
            };

        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>
            {
                "SDL_JOYSTICK_HIDAPI_WII = 1"
            };

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private bool ConfigureMameControllers(string path, bool hbmame, string rom, string messSystem)
        {
            if (Program.SystemConfig["disableautocontrollers"] == "1")
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

            if (Controllers.Count == 1 && gunCount == 0)
            {
                var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                if (c1?.IsKeyboard == true)
                    return false;
            }

            UpdateSdlControllersWithHints();

            // Delete input that would be in cfg file
            string cfgFile = messFiles.ContainsKey(messSystem) ? GetMameCfgPath(messFiles[messSystem] + ".cfg") : GetMameCfgPath(Path.GetFileNameWithoutExtension(rom) + ".cfg");
            if (File.Exists(cfgFile))
                DeleteInputincfgFile(cfgFile);
            
            string defaultcfgFile = GetMameCfgPath("default.cfg");
            if (File.Exists(defaultcfgFile))
                DeleteInputincfgFile(defaultcfgFile);

            // Get specific mapping if it exists
            string MappingFileName;
            if (messFiles.ContainsKey(messSystem))
            {
                MappingFileName = messFiles[messSystem] + ".xml";
                _messcfgInput = true;
                _messSystem = messFiles[messSystem];
            }
            else
                MappingFileName = Path.GetFileNameWithoutExtension(rom) + ".xml";

            string layout = SystemConfig["controller_layout"] ?? "default";
            if (layout == "classic8")
                layout = "6alternative";

            string specificMappingPath = Path.Combine(AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "mame", MappingFileName);
            if (!File.Exists(specificMappingPath))
                specificMappingPath = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "mame", MappingFileName);

            if (File.Exists(specificMappingPath))
            {
                var gameMapping = GameMapping.LoadGameFromXml(specificMappingPath);

                if (gameMapping != null)
                {
                    var gameLayout = gameMapping.Layouts.FirstOrDefault(l => l.Type == layout) ?? gameMapping.Layouts.FirstOrDefault(l => l.Type == "default");

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
            bool analogDpad = SystemConfig.getOptBoolean("analogDpad");
            bool revertX = SystemConfig.getOptBoolean("revertXIndex");
            bool dpadonly = SystemConfig.getOptBoolean("mame_dpadandstick");
            var diDevices = new DirectInputInfo().GetDinputDevices();
            string driver = SystemConfig["mame_joystick_driver"];
            var mameControllers = new List<Controller>();

            if (driver == "xinput")
            {
                mameControllers = this.Controllers
                    .Where(c => c.IsXInputDevice && !c.IsKeyboard)
                    .OrderBy(c => c.XInput.DeviceIndex)
                    .ToList();
            }

            else if (driver == "dinput")
            {
                foreach (var di in diDevices)
                {
                    var match = this.Controllers.FirstOrDefault(c =>
                        c.DirectInput?.InstanceGuid == di.InstanceGuid);

                    if (match != null)
                        mameControllers.Add(match);
                    else if (di.Subtype == 259)
                        mameControllers.Add(null);
                }
            }
            else
            {
                // hybrid
                foreach (var di in diDevices)
                {
                    var match = this.Controllers.FirstOrDefault(c =>
                        c.DirectInput?.InstanceGuid == di.InstanceGuid &&
                        !c.IsXInputDevice);

                    if (match != null)
                        mameControllers.Add(match);
                    else if (di.Subtype == 259)
                        mameControllers.Add(null);
                }

                mameControllers.AddRange(
                    this.Controllers
                        .Where(c => c.IsXInputDevice)
                        .OrderBy(c => c.XInput.DeviceIndex));
            }

            // GUNS & MOUSES
            bool forceOneGun = SystemConfig.getOptBoolean("mame_forceOneGun");
            _multigun = gunCount > 1 && !forceOneGun;

            if (gunCount > 0)
                ConfigureGunRemap(input, guns, _multigun);


            if (SystemConfig.getOptBoolean("mame_forceOneGun"))
                _multigun = false;

            string mouseIndex1 = GetGunIndexOrDefault("p1_gunIndex", "1", 1);
            string mouseIndex2 = GetGunIndexOrDefault("p2_gunIndex", "2", 2);
            string mouseIndex3 = GetGunIndexOrDefault("p3_gunIndex", "3", 3);
            string mouseIndex4 = GetGunIndexOrDefault("p4_gunIndex", "4", 4);

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                ConfigureLightguns(input, mouseIndex1, mouseIndex2, mouseIndex3, mouseIndex4, gunCount, hbmame);
            }

            else
            {
                bool multiplayer = mameControllers.Where(c => c != null).ToList().Count > 1;
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
                bool hasGameDb = File.Exists(gamecontrollerDB);
                if (!hasGameDb)
                {
                    SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder.");
                    gamecontrollerDB = null;
                }
                var mapping = hbmame ? hbxInputMapping : xInputMapping;

                // Generate controller mapping
                for (int index = 0; index < mameControllers.Count; index++)
                {
                    var controller = mameControllers[index];

                    if (controller == null)
                        continue;

                    int i = controller.PlayerIndex;
                    int cIndex = index + 1;
                    string joy = "JOYCODE_" + cIndex + "_";
                    bool isXinput = controller.IsXInputDevice && driver != "dinput";
                    bool xinputCtrl = controller.IsXInputDevice;
                    string guid = (controller.Guid.ToString()).Substring(0, 24) + "00000000";
                    SdlToDirectInput ctrlr = null;

                    if (driver == "xinput")
                    {
                        int xIndex = cIndex;
                        joy = "JOYCODE_" + xIndex + "_";
                        input.Add(new XElement("mapdevice", new XAttribute("device", "XInput Player " + xIndex), new XAttribute("controller", "JOYCODE_" + xIndex)));
                    }

                    // Override index through option
                    string forcedIndex = SystemConfig[$"mame_p{controller.PlayerIndex}_forceindex"];
                    if (!string.IsNullOrEmpty(forcedIndex))
                        joy = $"JOYCODE_{forcedIndex}_";

                    // Get dinput mapping information
                    if (!isXinput && gamecontrollerDB != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Player " + i + " . Fetching gamecontrollerdb.txt file with guid : " + guid);

                        try
                        {
                            ctrlr = analogDpad
                                ? GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid, true)
                                : GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);
                        }
                        catch { }

                        if (ctrlr?.ButtonMappings == null)
                            SimpleLogger.Instance.Info($"[INFO] Player {i}. No controller mapping found in gamecontrollerdb.txt file for guid : {guid}");
                        else
                            SimpleLogger.Instance.Info($"[INFO] Player {i}: controller mapping found in gamecontrollerDB file.");
                    }

                    // Invert player 1 & 2 with feature
                    bool invert = revertX && mameControllers.Count > 1;
                    if (invert && i <= 2)
                        i = 3 - i;

                    // PLAYER 1
                    // Add UI mapping for player 1 to control MAME UI + Service menu
                    if (i == 1)
                    {
                        if (isXinput)
                            ConfigurePlayer1XInput(i, input, mapping, joy, mouseIndex1, hbmame, dpadonly, layout, multiplayer);
                        else
                            ConfigurePlayer1DInput(i, input, ctrlr, joy, mouseIndex1, hbmame, dpadonly, xinputCtrl, layout, multiplayer);
                    }

                    // OTHER PLAYERS
                    // Max 8 players for mame
                    else if (i <= 8)
                    {
                        if (isXinput)
                            ConfigurePlayersXInput(i, input, mapping, joy, mouseIndex2, mouseIndex3, mouseIndex4, hbmame, dpadonly, layout);
                        else
                            ConfigurePlayersDInput(i, input, ctrlr, joy, mouseIndex2, mouseIndex3, mouseIndex4, dpadonly, xinputCtrl, layout);
                    }

                    SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
                }

                // mess does not accept ctrlr overrides, so copy to cfg file
                if (_messcfgInput)
                {
                    SaveMessConfig(cfgFile, input, messFiles[messSystem]);
                }
            }

            // Generate xml document
            XDocument xdoc = new XDocument(new XDeclaration("1.0", null, null));
            xdoc.Add(mameconfig);
            mameconfig.Add(system);

            if (input.HasElements)
                system.Add(input);

            xdoc.Save(inputConfig);

            if (!File.Exists(inputConfig))
                return false;

            return true;
        }

        #region configuration
        private void ConfigurePlayer1XInput(int i, XElement input, Dictionary<string, string> mapping, string joy, string mouseIndex1, bool hbmame, bool dpadonly, string layout, bool multiplayer = false)
        {
            bool ignoreStart1 = false;
            bool ignoreCoin1 = false;
            bool stop = false;
            bool forceKbStart = false;
            bool forceKbCoin = false;

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
                    {
                        if (!multiplayer)
                            forceKbStart = true;
                        else
                            continue;
                    }
                    if (button.Type.Contains("COIN2") || button.Type.Contains("COIN3") || button.Type.Contains("COIN4") || button.Type.Contains("COIN5") || button.Type.Contains("COIN6"))
                    {
                        if (!multiplayer)
                            forceKbCoin = true;
                        else
                            continue;
                    }
                    if (button.Type.Contains("COIN1"))
                        ignoreCoin1 = true;
                    if (button.Type.Contains("START1"))
                        ignoreStart1 = true;

                    var port = new XElement("port",
                        new XAttribute("type", button.Type),
                        button.Tag == null ? null : new XAttribute("tag", button.Tag),
                        button.Mask == null ? null : new XAttribute("mask", button.Mask),
                        button.DefValue == null ? null : new XAttribute("defvalue", button.DefValue));

                    foreach (var map in button.ButtonMappings)
                    {
                        string mappingText = map.Mapping;
                        if (forceKbStart && button.Type.StartsWith("START") && button.Type != "START1")
                            mappingText = "NONE";
                        if (forceKbCoin && button.Type.StartsWith("COIN") && button.Type != "COIN1")
                            mappingText = "NONE";

                        if (!string.Equals(mappingText, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            mappingText = GetSpecificMappingX(joy, mouseIndex1, mappingText, i);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }

                    input.Add(port);
                }

                stop = true;
            }

            if (hbmame)
            {
                AddPort(input, "UI_CONFIGURE", $"{joy}{mapping["select"]} {joy}{mapping["south"]} OR KEYCODE_TAB");
            }
            else
            {
                AddPort(input, "UI_MENU", $"{joy}{mapping["select"]} {joy}{mapping["south"]} OR KEYCODE_TAB");
            }

            AddPort(input, "UI_SELECT", $"{joy}{mapping["south"]} OR KEYCODE_ENTER");
            AddPort(input, "UI_BACK", $"{joy}{mapping["east"]} OR KEYCODE_ESC");
            AddPort(input, "UI_CANCEL", $"{joy}{mapping["select"]} {joy}{mapping["start"]} OR KEYCODE_ESC");
            AddPort(input, "UI_UP", $"{joy}{mapping["up"]} OR {joy}{mapping["lsup"]} OR KEYCODE_UP");
            AddPort(input, "UI_DOWN", $"{joy}{mapping["down"]} OR {joy}{mapping["lsdown"]} OR KEYCODE_DOWN");
            AddPort(input, "UI_LEFT", $"{joy}{mapping["left"]} OR {joy}{mapping["lsleft"]} OR KEYCODE_LEFT");
            AddPort(input, "UI_RIGHT", $"{joy}{mapping["right"]} OR {joy}{mapping["lsright"]} OR KEYCODE_RIGHT");
            AddPort(input, "UI_PAUSE", $"KEYCODE_P");
            AddPort(input, "UI_REWIND_SINGLE", $"{joy}{mapping["select"]} {joy}{mapping["left"]} OR KEYCODE_BACKSPACE");
            AddPort(input, "UI_FAST_FORWARD", $"{joy}{mapping["select"]} {joy}{mapping["right"]} OR KEYCODE_SPACE");
            AddPort(input, "UI_SAVE_STATE", $"{joy}{mapping["select"]} {joy}{mapping["west"]} OR KEYCODE_F2");
            AddPort(input, "UI_LOAD_STATE", $"{joy}{mapping["select"]} {joy}{mapping["north"]} OR KEYCODE_F4");
            AddPort(input, "SERVICE", $"{joy}{mapping["l3"]} {joy}{mapping["r3"]} OR KEYCODE_0");
            AddPort(input, "SERVICE1", $"{joy}{mapping["l3"]} {joy}{mapping["r3"]} OR KEYCODE_9");
            AddPort(input, "TILT", $"{joy}{mapping["select"]} {joy}{mapping["r1"]} OR KEYCODE_T");
            AddPort(input, "TILT1", $"{joy}{mapping["select"]} {joy}{mapping["r1"]} OR KEYCODE_T");

            // Start & coin
            if (!ignoreStart1)
                AddPort(input, "START" + i, joy + mapping["start"] + " OR KEYCODE_1 OR MOUSECODE_" + mouseIndex1 + "_START OR GUNCODE_" + mouseIndex1 + "_START");
            if (!ignoreCoin1)
                AddPort(input, "COIN" + i, joy + mapping["select"] + " OR KEYCODE_5 OR MOUSECODE_" + mouseIndex1 + "_SELECT OR GUNCODE_" + mouseIndex1 + "_SELECT");

            if (stop)
                return;

            // Standard joystick buttons and directions
            string up = dpadonly? $"{joy}{mapping["up"]} OR KEYCODE_UP" : $"{joy}{mapping["up"]} OR {joy}{mapping["lsup"]} OR KEYCODE_UP";
            AddPort(input, $"P{i}_JOYSTICK_UP", up);

            string down = dpadonly ? $"{joy}{mapping["down"]} OR KEYCODE_DOWN" : $"{joy}{mapping["down"]} OR {joy}{mapping["lsdown"]} OR KEYCODE_DOWN";
            AddPort(input, $"P{i}_JOYSTICK_DOWN", down);

            string left = dpadonly ? $"{joy}{mapping["left"]} OR KEYCODE_LEFT" : $"{joy}{mapping["left"]} OR {joy}{mapping["lsleft"]} OR KEYCODE_LEFT";
            AddPort(input, $"P{i}_JOYSTICK_LEFT", left);

            string right = dpadonly ? $"{joy}{mapping["right"]} OR KEYCODE_RIGHT" : $"{joy}{mapping["right"]} OR {joy}{mapping["lsright"]} OR KEYCODE_RIGHT";
            AddPort(input, $"P{i}_JOYSTICK_RIGHT", right);

            // Right joystick
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_UP", joy + mapping["rsup"] + " OR KEYCODE_I");
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_DOWN", joy + mapping["rsdown"] + " OR KEYCODE_K");
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_LEFT", joy + mapping["rsleft"] + " OR KEYCODE_J");
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_RIGHT", joy + mapping["rsright"] + " OR KEYCODE_L");

            // Left joystick
            AddPort(input, "P" + i + "_JOYSTICKLEFT_UP", joy + mapping["lsup"] + " OR KEYCODE_E");
            AddPort(input, "P" + i + "_JOYSTICKLEFT_DOWN", joy + mapping["lsdown"] + " OR KEYCODE_D");
            AddPort(input, "P" + i + "_JOYSTICKLEFT_LEFT", joy + mapping["lsleft"] + " OR KEYCODE_S");
            AddPort(input, "P" + i + "_JOYSTICKLEFT_RIGHT", joy + mapping["lsright"] + " OR KEYCODE_F");

            List<ButtonLayoutMapping> buttons = new List<ButtonLayoutMapping>();

            if (layout == "modern8")
            {
                buttons.Add(new ButtonLayoutMapping("BUTTON1", "west", "OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1"));
                buttons.Add(new ButtonLayoutMapping("BUTTON2", "north", "OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2"));
                buttons.Add(new ButtonLayoutMapping("BUTTON3", "r1", "OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2 OR GUNCODE_" + mouseIndex1 + "_BUTTON3"));
                buttons.Add(new ButtonLayoutMapping("BUTTON4", "south", "OR KEYCODE_LSHIFT"));
                buttons.Add(new ButtonLayoutMapping("BUTTON5", "east", "OR KEYCODE_Z"));
                buttons.Add(new ButtonLayoutMapping("BUTTON6", "r2trigger", "OR KEYCODE_X"));
                buttons.Add(new ButtonLayoutMapping("BUTTON7", "l1", "OR KEYCODE_C"));
                buttons.Add(new ButtonLayoutMapping("BUTTON8", "l2trigger", "OR KEYCODE_V"));
            }
            else if (layout == "6alternative")
            {
                buttons.Add(new ButtonLayoutMapping("BUTTON1", "west", "OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1"));
                buttons.Add(new ButtonLayoutMapping("BUTTON2", "north", "OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2"));
                buttons.Add(new ButtonLayoutMapping("BUTTON3", "l1", "OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2 OR GUNCODE_" + mouseIndex1 + "_BUTTON3"));
                buttons.Add(new ButtonLayoutMapping("BUTTON4", "south", "OR KEYCODE_LSHIFT"));
                buttons.Add(new ButtonLayoutMapping("BUTTON5", "east", "OR KEYCODE_Z"));
                buttons.Add(new ButtonLayoutMapping("BUTTON6", "r1", "OR KEYCODE_X"));
                buttons.Add(new ButtonLayoutMapping("BUTTON7", "l2trigger", "OR KEYCODE_C"));
                buttons.Add(new ButtonLayoutMapping("BUTTON8", "r2trigger", "OR KEYCODE_V"));
            }
            else
            {
                buttons.Add(new ButtonLayoutMapping("BUTTON1", "west", "OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1"));
                buttons.Add(new ButtonLayoutMapping("BUTTON2", "south", "OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2"));
                buttons.Add(new ButtonLayoutMapping("BUTTON3", "east", "OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2 OR GUNCODE_" + mouseIndex1 + "_BUTTON3"));
                buttons.Add(new ButtonLayoutMapping("BUTTON4", "north", "OR KEYCODE_LSHIFT"));
                buttons.Add(new ButtonLayoutMapping("BUTTON5", "l1", "OR KEYCODE_Z"));
                buttons.Add(new ButtonLayoutMapping("BUTTON6", "r1", "OR KEYCODE_X"));
                buttons.Add(new ButtonLayoutMapping("BUTTON7", "l2trigger", "OR KEYCODE_C"));
                buttons.Add(new ButtonLayoutMapping("BUTTON8", "r2trigger", "OR KEYCODE_V"));
            }

            foreach (ButtonLayoutMapping bm in buttons)
            {
                AddPort(input, "P" + i + "_" + bm.Button, joy + mapping[bm.MappingKey] + " " + bm.Extra);
            }

            AddPort(input, "P" + i + "_BUTTON9", joy + mapping["l3"] + " OR KEYCODE_B");
            AddPort(input, "P" + i + "_BUTTON10", joy + mapping["r3"] + " OR KEYCODE_N");
            AddPort(input, "P" + i + "_START", joy + mapping["start"] + " OR KEYCODE_1 OR MOUSECODE_" + mouseIndex1 + "_START OR GUNCODE_" + mouseIndex1 + "_START");
            AddPort(input, "P" + i + "_SELECT", joy + mapping["select"] + " OR KEYCODE_5 OR MOUSECODE_" + mouseIndex1 + "_SELECT OR GUNCODE_" + mouseIndex1 + "_SELECT");

            // Pedals and other devices
            AddPort(input, "P" + i + "_PEDAL", standard: joy + mapping["r2"], increment: joy + mapping["south"] + " KEYCODE_LCONTROL");
            AddPort(input, "P" + i + "_PEDAL2", standard: joy + mapping["l2"], increment: joy + mapping["east"] + " OR KEYCODE_LALT");

            AddPort(input, "P" + i + "_PEDAL3", "KEYCODE_SPACE");


            AddPort(input, "P" + i + "_PADDLE", joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_PADDLE_V", joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");

            AddPort(input, "P" + i + "_POSITIONAL", joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_POSITIONAL_V", joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");

            AddPort(input, "P" + i + "_DIAL", joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_DIAL_V", joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");

            AddPort(input, "P" + i + "_TRACKBALL_X", joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_TRACKBALL_Y", joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");

            AddPort(input, "P" + i + "_AD_STICK_X", joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_AD_STICK_Y", joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");
            AddPort(input, "P" + i + "_AD_STICK_Z", joy + mapping["rs_y"], "KEYCODE_Z", "KEYCODE_A");

            AddPort(input, "P" + i + "_LIGHTGUN_X", joy + mapping["ls_x"] + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_LIGHTGUN_Y", joy + mapping["ls_y"] + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");

            AddPort(input, "P" + i + "_MOUSE_X", "MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS", "KEYCODE_RIGHT", "KEYCODE_LEFT");
            AddPort(input, "P" + i + "_MOUSE_Y", "MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS", "KEYCODE_DOWN", "KEYCODE_UP");
        }

        private void ConfigurePlayer1DInput(int i, XElement input, SdlToDirectInput ctrlr, string joy, string mouseIndex1, bool hbmame, bool dpadonly, bool xinputCtrl, string layout, bool multiplayer = false)
        {
            bool ignoreStart1 = false;
            bool ignoreCoin1 = false;
            bool stop = false;
            bool forceKbStart = false;
            bool forceKbCoin = false;

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
                    {
                        if (!multiplayer)
                            forceKbStart = true;
                        else
                            continue;
                    }
                    if (button.Type.Contains("COIN2") || button.Type.Contains("COIN3") || button.Type.Contains("COIN4") || button.Type.Contains("COIN5") || button.Type.Contains("COIN6"))
                    {
                        if (!multiplayer)
                            forceKbCoin = true;
                        else
                            continue;
                    }
                    if (button.Type.Contains("COIN1"))
                        ignoreCoin1 = true;
                    if (button.Type.Contains("START1"))
                        ignoreStart1 = true;

                    var port = new XElement("port",
                        new XAttribute("type", button.Type),
                        button.Tag == null ? null : new XAttribute("tag", button.Tag),
                        button.Mask == null ? null : new XAttribute("mask", button.Mask),
                        button.DefValue == null ? null : new XAttribute("defvalue", button.DefValue));

                    foreach (var map in button.ButtonMappings)
                    {
                        string mappingText = map.Mapping;
                        if (forceKbStart && button.Type.StartsWith("START") && button.Type != "START1")
                            mappingText = "NONE";
                        if (forceKbCoin && button.Type.StartsWith("COIN") && button.Type != "COIN1")
                            mappingText = "NONE";

                        if (!string.Equals(mappingText, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            mappingText = GetSpecificMappingD(ctrlr, joy, mouseIndex1, mappingText, i, xinputCtrl);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }

                    input.Add(port);
                }

                stop = true;
            }

            if (hbmame)
            {
                AddPort(input, "UI_CONFIGURE", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_TAB");
            }
            else
            {
                AddPort(input, "UI_MENU", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_TAB");
            }

            AddPort(input, "UI_SELECT", joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_ENTER");
            AddPort(input, "UI_BACK", joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_ESC");
            AddPort(input, "UI_CANCEL", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_ESC");
            AddPort(input, "UI_UP", joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_UP");
            AddPort(input, "UI_DOWN", joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_DOWN");
            AddPort(input, "UI_LEFT", joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_LEFT");
            AddPort(input, "UI_RIGHT", joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_RIGHT");
            AddPort(input, "UI_PAUSE", "KEYCODE_P");
            AddPort(input, "UI_REWIND_SINGLE", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_BACKSPACE");
            AddPort(input, "UI_FAST_FORWARD", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_SPACE");
            AddPort(input, "UI_SAVE_STATE", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_F2");
            AddPort(input, "UI_LOAD_STATE", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_F4");
            AddPort(input, "SERVICE", joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_0");
            AddPort(input, "SERVICE1", joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_9");
            AddPort(input, "TILT", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_T");
            AddPort(input, "TILT1", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_T");

            // Start & coin
            if (!ignoreStart1)
                AddPort(input, "START" + i, joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1 OR MOUSECODE_" + mouseIndex1 + "_START OR GUNCODE_" + mouseIndex1 + "_START");
            if (!ignoreCoin1)
                AddPort(input, "COIN" + i, joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5 OR MOUSECODE_" + mouseIndex1 + "_SELECT OR GUNCODE_" + mouseIndex1 + "_SELECT");

            if (stop)
                return;

            // Standard joystick buttons and directions
            string up = dpadonly ? $"{joy}{GetDinputMapping(ctrlr, "dpup", xinputCtrl)} OR KEYCODE_UP" : $"{joy}{GetDinputMapping(ctrlr, "dpup", xinputCtrl)} OR {joy}{GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1)} OR KEYCODE_UP";
            AddPort(input, $"P{i}_JOYSTICK_UP", up);

            string down = dpadonly ? $"{joy}{GetDinputMapping(ctrlr, "dpdown", xinputCtrl)} OR KEYCODE_DOWN" : $"{joy}{GetDinputMapping(ctrlr, "dpdown", xinputCtrl)} OR {joy}{GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1)} OR KEYCODE_DOWN";
            AddPort(input, $"P{i}_JOYSTICK_DOWN", down);

            string left = dpadonly ? $"{joy}{GetDinputMapping(ctrlr, "dpleft", xinputCtrl)} OR KEYCODE_LEFT" : $"{joy}{GetDinputMapping(ctrlr, "dpleft", xinputCtrl)} OR {joy}{GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1)} OR KEYCODE_LEFT";
            AddPort(input, $"P{i}_JOYSTICK_LEFT", left);

            string right = dpadonly ? $"{joy}{GetDinputMapping(ctrlr, "dpright", xinputCtrl)} OR KEYCODE_RIGHT" : $"{joy}{GetDinputMapping(ctrlr, "dpright", xinputCtrl)} OR {joy}{GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1)} OR KEYCODE_RIGHT";
            AddPort(input, $"P{i}_JOYSTICK_RIGHT", right);

            // JOYSTICK RIGHT
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_UP", joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1) + " OR KEYCODE_I");
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_DOWN", joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1) + " OR KEYCODE_K");
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_LEFT", joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1) + " OR KEYCODE_J");
            AddPort(input, "P" + i + "_JOYSTICKRIGHT_RIGHT", joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1) + " OR KEYCODE_L");

            // JOYSTICK LEFT
            AddPort(input, "P" + i + "_JOYSTICKLEFT_UP", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_E");
            AddPort(input, "P" + i + "_JOYSTICKLEFT_DOWN", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_D");
            AddPort(input, "P" + i + "_JOYSTICKLEFT_LEFT", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_S");
            AddPort(input, "P" + i + "_JOYSTICKLEFT_RIGHT", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_F");

            // BUTTONS
            if (layout == "modern8")
            {
                AddPort(input, "P" + i + "_BUTTON1", joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1");
                AddPort(input, "P" + i + "_BUTTON2", joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2");
                AddPort(input, "P" + i + "_BUTTON3", joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2 OR GUNCODE_" + mouseIndex1 + "_BUTTON3");
                AddPort(input, "P" + i + "_BUTTON4", joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_LSHIFT");
                AddPort(input, "P" + i + "_BUTTON5", joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_Z");
                AddPort(input, "P" + i + "_BUTTON6", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl) + " OR KEYCODE_X");
                AddPort(input, "P" + i + "_BUTTON7", joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl) + " OR KEYCODE_C");
                AddPort(input, "P" + i + "_BUTTON8", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl) + " OR KEYCODE_V");
            }
            else if (layout == "6alternative")
            {
                AddPort(input, "P" + i + "_BUTTON1", joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1");
                AddPort(input, "P" + i + "_BUTTON2", joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2");
                AddPort(input, "P" + i + "_BUTTON3", joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl) + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2 OR GUNCODE_" + mouseIndex1 + "_BUTTON3");
                AddPort(input, "P" + i + "_BUTTON4", joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_LSHIFT");
                AddPort(input, "P" + i + "_BUTTON5", joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_Z");
                AddPort(input, "P" + i + "_BUTTON6", joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_X");
                AddPort(input, "P" + i + "_BUTTON7", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl) + " OR KEYCODE_C");
                AddPort(input, "P" + i + "_BUTTON8", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl) + " OR KEYCODE_V");
            }
            else
            {
                AddPort(input, "P" + i + "_BUTTON1", joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_LCONTROL OR MOUSECODE_" + mouseIndex1 + "_BUTTON1 OR GUNCODE_" + mouseIndex1 + "_BUTTON1");
                AddPort(input, "P" + i + "_BUTTON2", joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT OR MOUSECODE_" + mouseIndex1 + "_BUTTON3 OR GUNCODE_" + mouseIndex1 + "_BUTTON2");
                AddPort(input, "P" + i + "_BUTTON3", joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_SPACE OR MOUSECODE_" + mouseIndex1 + "_BUTTON2 OR GUNCODE_" + mouseIndex1 + "_BUTTON3");
                AddPort(input, "P" + i + "_BUTTON4", joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_LSHIFT");
                AddPort(input, "P" + i + "_BUTTON5", joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl) + " OR KEYCODE_Z");
                AddPort(input, "P" + i + "_BUTTON6", joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_X");
                AddPort(input, "P" + i + "_BUTTON7", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl) + " OR KEYCODE_C");
                AddPort(input, "P" + i + "_BUTTON8", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl) + " OR KEYCODE_V");
            }

            AddPort(input, $"P{i}_BUTTON9", joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " OR KEYCODE_B");
            AddPort(input, $"P{i}_BUTTON10", joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_N");
            AddPort(input, $"P{i}_START", joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1 OR MOUSECODE_" + mouseIndex1 + "_START OR GUNCODE_" + mouseIndex1 + "_START");
            AddPort(input, $"P{i}_SELECT", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5 OR MOUSECODE_" + mouseIndex1 + "_COIN OR GUNCODE_" + mouseIndex1 + "_COIN");

            AddPort(input, $"P{i}_PEDAL", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl, 1), increment: joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " KEYCODE_LCONTROL");
            AddPort(input, $"P{i}_PEDAL2", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl, 1), increment: joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT");
            AddPort(input, $"P{i}_PEDAL3", null, increment: "KEYCODE_SPACE");
            AddPort(input, $"P{i}_PADDLE", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_PADDLE_V", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
            AddPort(input, $"P{i}_POSITIONAL", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_POSITIONAL_V", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
            AddPort(input, $"P{i}_DIAL", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_DIAL_V", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
            AddPort(input, $"P{i}_TRACKBALL_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_TRACKBALL_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
            AddPort(input, $"P{i}_AD_STICK_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_AD_STICK_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
            AddPort(input, $"P{i}_AD_STICK_Z", joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 0), increment: "KEYCODE_Z", decrement: "KEYCODE_A");
            AddPort(input, $"P{i}_LIGHTGUN_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_LIGHTGUN_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
            AddPort(input, $"P{i}_MOUSE_X", "MOUSECODE_" + mouseIndex1 + "_XAXIS OR GUNCODE_" + mouseIndex1 + "_XAXIS", increment: "KEYCODE_RIGHT", decrement: "KEYCODE_LEFT");
            AddPort(input, $"P{i}_MOUSE_Y", "MOUSECODE_" + mouseIndex1 + "_YAXIS OR GUNCODE_" + mouseIndex1 + "_YAXIS", increment: "KEYCODE_DOWN", decrement: "KEYCODE_UP");
        }

        private void ConfigurePlayersXInput(int i, XElement input, Dictionary<string, string> mapping, string joy, string mouseIndex2, string mouseIndex3, string mouseIndex4, bool hbmame, bool dpadonly, string layout)
        {
            int j = i + 4;
            string gunIndex = (i == 3) ? mouseIndex3 : (i == 4) ? mouseIndex4 : mouseIndex2;
           
            bool ignoreStartx = false;
            bool ignoreCoinx = false;

            // Specific mapping if available
            if (!hbmame && _gameLayout != null)
            {
                if (_gameMapping.Name != null)
                    SimpleLogger.Instance.Info("[INFO] Performing specific mapping (xinput) for: " + _gameMapping.Name);

                input.Add(new XElement
                    ("port", new XAttribute("type", "START" + i),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_" + i + " OR MOUSECODE_" + gunIndex + "_START OR GUNCODE_" + gunIndex + "_START")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "COIN" + i),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_" + j + " OR MOUSECODE_" + gunIndex + "_SELECT OR GUNCODE_" + gunIndex + "_SELECT")));

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
                    if (_messcfgInput && button.Type == "OTHER")
                        continue;
                    if (button.Type.StartsWith("START"))
                        ignoreStartx = true;
                    if (button.Type.StartsWith("COIN"))
                        ignoreCoinx = true;

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
                            mappingText = GetSpecificMappingX(joy, gunIndex, mappingText, i);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);
                        port.Add(buttonMap);
                    }

                    input.Add(port); 
                }

                AddPort(input, $"P{i}_START", joy + mapping["start"]);
                AddPort(input, $"P{i}_SELECT", joy + mapping["select"]);
                if (!ignoreStartx)
                    AddPort(input, $"START{i}", joy + mapping["start"] + $" OR KEYCODE_{i} OR MOUSECODE_{gunIndex}_START OR GUNCODE_{gunIndex}_START");
                if (!ignoreCoinx)
                    AddPort(input, $"COIN{i}", joy + mapping["select"] + $" OR KEYCODE_{j} OR MOUSECODE_{gunIndex}_SELECT OR GUNCODE_{gunIndex}_SELECT");

                return;
            }

            string up = dpadonly ? $"{joy}{mapping["up"]}" : $"{joy}{mapping["up"]} OR {joy}{mapping["lsup"]}";
            AddPort(input, $"P{i}_JOYSTICK_UP", up);

            string down = dpadonly ? $"{joy}{mapping["down"]}" : $"{joy}{mapping["down"]} OR {joy}{mapping["lsdown"]}";
            AddPort(input, $"P{i}_JOYSTICK_DOWN", down);

            string left = dpadonly ? $"{joy}{mapping["left"]}" : $"{joy}{mapping["left"]} OR {joy}{mapping["lsleft"]}";
            AddPort(input, $"P{i}_JOYSTICK_LEFT", left);

            string right = dpadonly ? $"{joy}{mapping["right"]}" : $"{joy}{mapping["right"]} OR {joy}{mapping["lsright"]}";
            AddPort(input, $"P{i}_JOYSTICK_RIGHT", right);

            AddPort(input, $"P{i}_JOYSTICKRIGHT_UP", joy + mapping["rsup"]);
            AddPort(input, $"P{i}_JOYSTICKRIGHT_DOWN", joy + mapping["rsdown"]);
            AddPort(input, $"P{i}_JOYSTICKRIGHT_LEFT", joy + mapping["rsleft"]);
            AddPort(input, $"P{i}_JOYSTICKRIGHT_RIGHT", joy + mapping["rsright"]);

            AddPort(input, $"P{i}_JOYSTICKLEFT_UP", joy + mapping["lsup"]);
            AddPort(input, $"P{i}_JOYSTICKLEFT_DOWN", joy + mapping["lsdown"]);
            AddPort(input, $"P{i}_JOYSTICKLEFT_LEFT", joy + mapping["lsleft"]);
            AddPort(input, $"P{i}_JOYSTICKLEFT_RIGHT", joy + mapping["lsright"]);

            if (_multigun)
            {
                AddPort(input, $"P{i}_BUTTON1", joy + mapping["south"] + $" OR MOUSECODE_{gunIndex}_BUTTON1 OR GUNCODE_{gunIndex}_BUTTON1");
                AddPort(input, $"P{i}_BUTTON2", joy + mapping["east"] + $" OR MOUSECODE_{gunIndex}_BUTTON3 OR GUNCODE_{gunIndex}_BUTTON2");
                AddPort(input, $"P{i}_BUTTON3", joy + mapping["west"] + $" OR MOUSECODE_{gunIndex}_BUTTON2 OR GUNCODE_{gunIndex}_BUTTON3");
            }
            else
            {
                if (layout == "modern8")
                {
                    AddPort(input, $"P{i}_BUTTON1", joy + mapping["west"]);
                    AddPort(input, $"P{i}_BUTTON2", joy + mapping["north"]);
                    AddPort(input, $"P{i}_BUTTON3", joy + mapping["r1"]);
                }
                else if (layout == "6alternative")
                {
                    AddPort(input, $"P{i}_BUTTON1", joy + mapping["west"]);
                    AddPort(input, $"P{i}_BUTTON2", joy + mapping["north"]);
                    AddPort(input, $"P{i}_BUTTON3", joy + mapping["l1"]);
                }
                else
                {
                    AddPort(input, $"P{i}_BUTTON1", joy + mapping["south"]);
                    AddPort(input, $"P{i}_BUTTON2", joy + mapping["east"]);
                    AddPort(input, $"P{i}_BUTTON3", joy + mapping["west"]);
                }
            }

            if (layout == "modern8")
            {
                AddPort(input, $"P{i}_BUTTON4", joy + mapping["west"]);
                AddPort(input, $"P{i}_BUTTON5", joy + mapping["north"]);
                AddPort(input, $"P{i}_BUTTON6", joy + mapping["r1"]);
                AddPort(input, $"P{i}_BUTTON7", joy + mapping["l1"]);
                AddPort(input, $"P{i}_BUTTON8", joy + mapping["r2trigger"]);
            }
            else if (layout == "6alternative")
            {
                AddPort(input, $"P{i}_BUTTON4", joy + mapping["west"]);
                AddPort(input, $"P{i}_BUTTON5", joy + mapping["north"]);
                AddPort(input, $"P{i}_BUTTON6", joy + mapping["l1"]);
                AddPort(input, $"P{i}_BUTTON7", joy + mapping["l2trigger"]);
                AddPort(input, $"P{i}_BUTTON8", joy + mapping["r2trigger"]);
            }
            else
            {
                AddPort(input, $"P{i}_BUTTON4", joy + mapping["north"]);
                AddPort(input, $"P{i}_BUTTON5", joy + mapping["l1"]);
                AddPort(input, $"P{i}_BUTTON6", joy + mapping["r1"]);
                AddPort(input, $"P{i}_BUTTON7", joy + mapping["l2trigger"]);
                AddPort(input, $"P{i}_BUTTON8", joy + mapping["r2trigger"]);
            }

            AddPort(input, $"P{i}_BUTTON9", joy + mapping["l3"]);
            AddPort(input, $"P{i}_BUTTON10", joy + mapping["r3"]);
            AddPort(input, $"P{i}_START", joy + mapping["start"]);
            AddPort(input, $"P{i}_SELECT", joy + mapping["select"]);
            AddPort(input, $"START{i}", joy + mapping["start"] + $" OR KEYCODE_{i} OR MOUSECODE_{gunIndex}_START OR GUNCODE_{gunIndex}_START");
            AddPort(input, $"COIN{i}", joy + mapping["select"] + $" OR KEYCODE_{j} OR MOUSECODE_{gunIndex}_SELECT OR GUNCODE_{gunIndex}_SELECT");

            // Pedals and other devices
            AddPort(input, $"P{i}_PEDAL", joy + mapping["r2"], increment: joy + mapping["south"]);
            AddPort(input, $"P{i}_PEDAL2", joy + mapping["l2"], increment: joy + mapping["east"]);
            AddPort(input, $"P{i}_PEDAL3", standard: null, increment: joy + mapping["south"]);
            AddPort(input, $"P{i}_PADDLE", joy + mapping["ls_x"]);
            AddPort(input, $"P{i}_PADDLE_V", joy + mapping["ls_y"]);
            AddPort(input, $"P{i}_DIAL", joy + mapping["ls_x"]);
            AddPort(input, $"P{i}_DIAL_V", joy + mapping["ls_y"]);
            AddPort(input, $"P{i}_TRACKBALL_X", joy + mapping["ls_x"]);
            AddPort(input, $"P{i}_TRACKBALL_Y", joy + mapping["ls_y"]);

            if (_multigun)
            {
                AddPort(input, $"P{i}_LIGHTGUN_X", $"{joy}{mapping["ls_x"]} OR MOUSECODE_{gunIndex}_XAXIS OR GUNCODE_{gunIndex}_XAXIS");
                AddPort(input, $"P{i}_LIGHTGUN_Y", $"{joy}{mapping["ls_y"]} OR MOUSECODE_{gunIndex}_YAXIS OR GUNCODE_{gunIndex}_YAXIS");
                AddPort(input, $"P{i}_MOUSE_X", $"MOUSECODE_{gunIndex}_XAXIS OR GUNCODE_{gunIndex}_XAXIS");
                AddPort(input, $"P{i}_MOUSE_Y", $"MOUSECODE_{gunIndex}_YAXIS OR GUNCODE_{gunIndex}_YAXIS");
                AddPort(input, $"P{i}_AD_STICK_X", $"{joy}{mapping["ls_x"]} OR MOUSECODE_{gunIndex}_XAXIS OR GUNCODE_{gunIndex}_XAXIS");
                AddPort(input, $"P{i}_AD_STICK_Y", $"{joy}{mapping["ls_y"]} OR MOUSECODE_{gunIndex}_YAXIS OR GUNCODE_{gunIndex}_YAXIS");
            }
            else
            {
                AddPort(input, $"P{i}_LIGHTGUN_X", $"{joy}{mapping["ls_x"]}");
                AddPort(input, $"P{i}_LIGHTGUN_Y", $"{joy}{mapping["ls_y"]}");
                AddPort(input, $"P{i}_AD_STICK_X", $"{joy}{mapping["ls_x"]}");
                AddPort(input, $"P{i}_AD_STICK_Y", $"{joy}{mapping["ls_y"]}");
            }

            AddPort(input, $"P{i}_AD_STICK_Z", $"{joy}{mapping["rs_y"]}");
        }

        private void ConfigurePlayersDInput(int i, XElement input, SdlToDirectInput ctrlr, string joy, string mouseIndex2, string mouseIndex3, string mouseIndex4, bool dpadonly, bool xinputCtrl, string layout)
        {
            int j = i + 4;

            string gunIndex = (i == 3) ? mouseIndex3 : (i == 4) ? mouseIndex4 : mouseIndex2;

            bool ignoreStartx = false;
            bool ignoreCoinx = false;

            if (_gameLayout != null)
            {
                if (_gameMapping.Name != null)
                    SimpleLogger.Instance.Info("[INFO] Performing specific mapping (dinput) for: " + _gameMapping.Name);

                input.Add(new XElement
                ("port", new XAttribute("type", "START" + i),
                    new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_" + i + " OR MOUSECODE_" + gunIndex + "_START OR GUNCODE_" + gunIndex + "_START")));

                input.Add(new XElement
                    ("port", new XAttribute("type", "COIN" + i),
                        new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_" + j + " OR MOUSECODE_" + gunIndex + "_SELECT OR GUNCODE_" + gunIndex + "_SELECT")));

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
                    if (_messcfgInput && button.Type == "OTHER")
                        continue;
                    if (button.Type.StartsWith("START"))
                        ignoreStartx = true;
                    if (button.Type.StartsWith("COIN"))
                        ignoreCoinx = true;

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
                            mappingText = GetSpecificMappingD(ctrlr, joy, gunIndex, mappingText, i, xinputCtrl);
                        }

                        var buttonMap = new XElement("newseq", new XAttribute("type", map.Type), mappingText);

                        port.Add(buttonMap);
                    }

                    input.Add(port);
                }

                AddPort(input, $"P{i}_START", joy + GetDinputMapping(ctrlr, "start", xinputCtrl));
                AddPort(input, $"P{i}_SELECT", joy + GetDinputMapping(ctrlr, "back", xinputCtrl));

                if (!ignoreStartx)
                    AddPort(input, $"START{i}", joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_" + i + " OR MOUSECODE_" + gunIndex + "_START OR GUNCODE_" + gunIndex + "_START");
                if (!ignoreCoinx)
                    AddPort(input, $"COIN{i}", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_" + j + " OR MOUSECODE_" + gunIndex + "_SELECT OR GUNCODE_" + gunIndex + "_SELECT");


                return;
            }

            AddPort(input, $"P{i}_JOYSTICK_UP", dpadonly ? joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) : joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1));
            AddPort(input, $"P{i}_JOYSTICK_DOWN", dpadonly ? joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) : joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1));
            AddPort(input, $"P{i}_JOYSTICK_LEFT", dpadonly ? joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) : joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1));
            AddPort(input, $"P{i}_JOYSTICK_RIGHT", dpadonly ? joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) : joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR " + joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1));

            AddPort(input, $"P{i}_JOYSTICKRIGHT_UP", joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1));
            AddPort(input, $"P{i}_JOYSTICKRIGHT_DOWN", joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1));
            AddPort(input, $"P{i}_JOYSTICKRIGHT_LEFT", joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1));
            AddPort(input, $"P{i}_JOYSTICKRIGHT_RIGHT", joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1));
            AddPort(input, $"P{i}_JOYSTICKLEFT_UP", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1));
            AddPort(input, $"P{i}_JOYSTICKLEFT_DOWN", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1));
            AddPort(input, $"P{i}_JOYSTICKLEFT_LEFT", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1));
            AddPort(input, $"P{i}_JOYSTICKLEFT_RIGHT", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1));

            // Case of 2 or more guns
            if (_multigun)
            {
                AddPort(input, $"P{i}_BUTTON1", joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR MOUSECODE_" + gunIndex + "_BUTTON1 OR GUNCODE_" + gunIndex + "_BUTTON1");
                AddPort(input, $"P{i}_BUTTON2", joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR MOUSECODE_" + gunIndex + "_BUTTON3 OR GUNCODE_" + gunIndex + "_BUTTON2");
                AddPort(input, $"P{i}_BUTTON3", joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR MOUSECODE_" + gunIndex + "_BUTTON2 OR GUNCODE_" + gunIndex + "_BUTTON3");
            }
            else
            {
                if (layout == "modern8")
                {
                    AddPort(input, $"P{i}_BUTTON1", joy + GetDinputMapping(ctrlr, "x", xinputCtrl));
                    AddPort(input, $"P{i}_BUTTON2", joy + GetDinputMapping(ctrlr, "y", xinputCtrl));
                    AddPort(input, $"P{i}_BUTTON3", joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl));
                }
                else if (layout == "6alternative")
                {
                    AddPort(input, $"P{i}_BUTTON1", joy + GetDinputMapping(ctrlr, "x", xinputCtrl));
                    AddPort(input, $"P{i}_BUTTON2", joy + GetDinputMapping(ctrlr, "y", xinputCtrl));
                    AddPort(input, $"P{i}_BUTTON3", joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl));
                }
                else
                {
                    AddPort(input, $"P{i}_BUTTON1", joy + GetDinputMapping(ctrlr, "a", xinputCtrl));
                    AddPort(input, $"P{i}_BUTTON2", joy + GetDinputMapping(ctrlr, "b", xinputCtrl));
                    AddPort(input, $"P{i}_BUTTON3", joy + GetDinputMapping(ctrlr, "x", xinputCtrl));
                }
            }

            if (layout == "modern8")
            {
                AddPort(input, $"P{i}_BUTTON4", joy + GetDinputMapping(ctrlr, "a", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON5", joy + GetDinputMapping(ctrlr, "b", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON6", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON7", joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON8", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl));
            }
            else if (layout == "6alternative")
            {
                AddPort(input, $"P{i}_BUTTON4", joy + GetDinputMapping(ctrlr, "a", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON5", joy + GetDinputMapping(ctrlr, "b", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON6", joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON7", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON8", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl));
            }
            else
            {
                AddPort(input, $"P{i}_BUTTON4", joy + GetDinputMapping(ctrlr, "y", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON5", joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON6", joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON7", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl));
                AddPort(input, $"P{i}_BUTTON8", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl));
            }

            AddPort(input, $"P{i}_BUTTON9", joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl));
            AddPort(input, $"P{i}_BUTTON10", joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl));
            AddPort(input, $"P{i}_START", joy + GetDinputMapping(ctrlr, "start", xinputCtrl));
            AddPort(input, $"P{i}_SELECT", joy + GetDinputMapping(ctrlr, "back", xinputCtrl));
            AddPort(input, $"START{i}", joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_" + i + " OR MOUSECODE_" + gunIndex + "_START OR GUNCODE_" + gunIndex + "_START");
            AddPort(input, $"COIN{i}", joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_" + j + " OR MOUSECODE_" + gunIndex + "_SELECT OR GUNCODE_" + gunIndex + "_SELECT");

            // Pedals
            AddPort(input, $"P{i}_PEDAL", joy + GetDinputMapping(ctrlr, "righttrigger", xinputCtrl, 1), increment: joy + GetDinputMapping(ctrlr, "a", xinputCtrl));
            AddPort(input, $"P{i}_PEDAL2", joy + GetDinputMapping(ctrlr, "lefttrigger", xinputCtrl, 1), increment: joy + GetDinputMapping(ctrlr, "b", xinputCtrl));
            AddPort(input, $"P{i}_PEDAL3", standard: null, increment: joy + GetDinputMapping(ctrlr, "a", xinputCtrl));
            AddPort(input, $"P{i}_PADDLE", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0));
            AddPort(input, $"P{i}_PADDLE_V", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0));
            AddPort(input, $"P{i}_DIAL", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0));
            AddPort(input, $"P{i}_DIAL_V", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0));
            AddPort(input, $"P{i}_TRACKBALL_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0));
            AddPort(input, $"P{i}_TRACKBALL_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0));

            if (_multigun)
            {
                AddPort(input, $"P{i}_LIGHTGUN_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + gunIndex + "_XAXIS OR GUNCODE_" + gunIndex + "_XAXIS");
                AddPort(input, $"P{i}_LIGHTGUN_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + gunIndex + "_YAXIS OR GUNCODE_" + gunIndex + "_YAXIS");
                AddPort(input, $"P{i}_MOUSE_X", "MOUSECODE_" + gunIndex + "_XAXIS OR GUNCODE_" + gunIndex + "_XAXIS");
                AddPort(input, $"P{i}_MOUSE_Y", "MOUSECODE_" + gunIndex + "_YAXIS OR GUNCODE_" + gunIndex + "_YAXIS");
                AddPort(input, $"P{i}_AD_STICK_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0) + " OR MOUSECODE_" + gunIndex + "_XAXIS OR GUNCODE_" + gunIndex + "_XAXIS");
                AddPort(input, $"P{i}_AD_STICK_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0) + " OR MOUSECODE_" + gunIndex + "_YAXIS OR GUNCODE_" + gunIndex + "_YAXIS");
            }
            else
            {
                AddPort(input, $"P{i}_LIGHTGUN_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0));
                AddPort(input, $"P{i}_LIGHTGUN_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0));
                AddPort(input, $"P{i}_AD_STICK_X", joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 0));
                AddPort(input, $"P{i}_AD_STICK_Y", joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 0));
            }

            // Z axis
            AddPort(input, $"P{i}_AD_STICK_Z", joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 0));
        }

        private void ConfigureGunRemap(XElement guninput, RawLightgun[] guns, bool multi = false)
        {
            Regex regex = new Regex(@"HID#(?:\{([^}]+)\}|([^#]+))");
            int i = 1;
            int mouseCount = 0;
            foreach (RawLightgun gun in guns)
            {
                var type = gun.Type;
                Match match = regex.Match(gun.DevicePath);

                if (match.Success)
                {
                    string result = !string.IsNullOrEmpty(match.Groups[1].Value)? match.Groups[1].Value : match.Groups[2].Value;
                    result.Replace("&", "&amp;");
                    string target;

                    if (gun.Type == RawLighGunType.Mouse || _mouseGun)
                    {
                        if (mouseCount > 0)
                            continue;
                        target = "MOUSECODE_" + i;
                        guninput.Add(new XElement("mapdevice", new XAttribute("device", result), new XAttribute("controller", target)));
                        mouseCount++;
                        i++;
                    }
                    else
                    {
                        target = "GUNCODE_" + i;
                        guninput.Add(new XElement("mapdevice", new XAttribute("device", result), new XAttribute("controller", target)));
                        i++;
                    }
                }  
            }
        }

        private string GetGunIndexOrDefault(string key, string defaultValue, int playerNumber)
        {
            if (SystemConfig.isOptSet(key))
            {
                string value = SystemConfig[key];
                if (!string.IsNullOrEmpty(value))
                {
                    SimpleLogger.Instance.Info($"[GUNS] Overwriting Gun {playerNumber} index : {value}");
                    return value;
                }
            }

            return defaultValue;
        }

        private void SaveMessConfig(string cfgFile, XElement input, string systemName)
        {
            try
            {
                XDocument doc;

                if (File.Exists(cfgFile))
                {
                    doc = XDocument.Load(cfgFile);

                    var systemElement = doc.Root?
                        .Elements("system")
                        .FirstOrDefault(e => (string)e.Attribute("name") == systemName);

                    if (systemElement == null)
                    {
                        systemElement = new XElement("system",
                            new XAttribute("name", systemName));

                        doc.Root?.Add(systemElement);
                    }

                    // Remove existing input block
                    systemElement.Element("input")?.Remove();

                    systemElement.Add(input);
                }
                else
                {
                    doc = new XDocument(
                        new XComment("This file is autogenerated; comments and unknown tags will be stripped"),
                        new XElement("mameconfig",
                            new XAttribute("version", "10"),
                            new XElement("system",
                                new XAttribute("name", systemName),
                                input
                            )
                        )
                    );
                }

                doc.Save(cfgFile);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Info("[ERROR] Failed to write MESS config: " + ex.Message);
            }
        }

        private string GetMameCfgPath(string fileName)
        {
            string folder = _separatecfg ? "cfg64" : "cfg";
            return Path.Combine(AppConfig.GetFullPath("saves"), "mame", folder, fileName);
        }

        private void DeleteInputincfgFile(string cfgFile)
        {
            // Backup cfgfile
            string backup = cfgFile + ".backup";
            try
            {
                File.Copy(cfgFile, backup, true);
                _filesToRestore.Add(cfgFile);
            }
            catch { }

            // Modify cfgfile
            try
            {
                XDocument doc = XDocument.Load(cfgFile);

                XElement inputElement = doc.Root?
                    .Element("system")?
                    .Element("input");

                if (inputElement != null)
                {
                    // Remove all child elements that are not DIPSWITCH
                    inputElement.Elements()
                        .Where(e => (string)e.Attribute("type") != "DIPSWITCH")
                        .Remove();
                }

                doc.Save(cfgFile);
            }
            catch { }
        }

        private static void AddPort(XElement input, string type, string standard, string increment = null, string decrement = null)
        {
            var port = new XElement("port",
                new XAttribute("type", type),
                new XElement("newseq", new XAttribute("type", "standard"), standard));

            if (increment != null)
                port.Add(new XElement("newseq", new XAttribute("type", "increment"), increment));

            if (decrement != null)
                port.Add(new XElement("newseq", new XAttribute("type", "decrement"), decrement));

            input.Add(port);
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
            bool reverse = false;

            string[] parts = mapping.Split(new string[] { "OR" }, StringSplitOptions.None);

            foreach (string part in parts)
            {
                string value = part.Trim();

                if (value.StartsWith("JOY") || value.StartsWith("joy"))
                {
                    string[] mapParts = value.Split('_');
                    if (mapParts.Length > 1)
                    {
                        string source = mapParts[1];

                        if (mapParts.Length > 2 && mapParts[2] == "reverse")
                            reverse = true;

                        string target = null;

                        if (specificToXinput.ContainsKey(source))
                            source = specificToXinput[source];

                        if (xInputMapping.ContainsKey(source))
                        {
                            target = xInputMapping[source];

                            string toAdd = joy + target;
                            if (reverse)
                                toAdd += "_REVERSE";

                            if (string.IsNullOrEmpty(ret))
                                ret += toAdd;
                            else
                                ret += " OR " + toAdd;
                        }
                    }
                }
                else if (value.StartsWith("GUN") || value.StartsWith("gun"))
                {
                    if (player >1 && !_multigun)
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
                else if (value.StartsWith("MOUSE") || value.StartsWith("mouse"))
                {
                    if (player > 1 && !_multigun)
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

                else if (value.StartsWith("KEY") || value.StartsWith("key"))
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
            bool reverse = false;

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

                        if (mapParts.Length > 2 && mapParts[2] == "reverse")
                            reverse = true;

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

                        if (reverse)
                            target += "_REVERSE";

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
                    if (player > 1 && !_multigun)
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
                    if (player > 1 && !_multigun)
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

        private void ConfigureLightguns(XElement guninput, string index1, string index2, string index3, string index4, int gunNb, bool hbmame = false)
        {
            AddPort(guninput, hbmame ? "UI_CONFIGURE" : "UI_MENU", standard: "KEYCODE_TAB");

            AddPort(guninput, "UI_SELECT", standard: "GUNCODE_" + index1 + "_BUTTON1 OR KEYCODE_ENTER OR MOUSECODE_" + index1 + "_BUTTON1");
            AddPort(guninput, "UI_BACK", standard: "GUNCODE_" + index1 + "_BUTTON3 OR MOUSECODE_" + index1 + "_BUTTON3 OR KEYCODE_ESC");
            AddPort(guninput, "UI_CANCEL", standard: "KEYCODE_ESC");
            AddPort(guninput, "UI_UP", standard: "KEYCODE_UP");
            AddPort(guninput, "UI_DOWN", standard: "KEYCODE_DOWN");
            AddPort(guninput, "UI_LEFT", standard: "KEYCODE_LEFT");
            AddPort(guninput, "UI_RIGHT", standard: "KEYCODE_RIGHT");
            AddPort(guninput, "UI_PAUSE", standard: "KEYCODE_P");
            AddPort(guninput, "UI_REWIND_SINGLE", standard: "KEYCODE_BACKSPACE");
            AddPort(guninput, "UI_FAST_FORWARD", standard: "KEYCODE_SPACE");
            AddPort(guninput, "UI_SAVE_STATE", standard: "KEYCODE_F2");
            AddPort(guninput, "UI_LOAD_STATE", standard: "KEYCODE_F4");
            AddPort(guninput, "SERVICE", standard: "KEYCODE_0");
            AddPort(guninput, "SERVICE1", standard: "KEYCODE_9");

            // Joystick directions
            AddPort(guninput, "P1_JOYSTICK_UP", standard: "KEYCODE_UP");
            AddPort(guninput, "P1_JOYSTICK_DOWN", standard: "KEYCODE_DOWN");
            AddPort(guninput, "P1_JOYSTICK_LEFT", standard: "KEYCODE_LEFT");
            AddPort(guninput, "P1_JOYSTICK_RIGHT", standard: "KEYCODE_RIGHT");

            // Buttons
            AddPort(guninput, "P1_BUTTON1", standard: "GUNCODE_" + index1 + "_BUTTON1 OR MOUSECODE_" + index1 + "_BUTTON1");
            AddPort(guninput, "P1_BUTTON2", standard: "GUNCODE_" + index1 + "_BUTTON2 OR MOUSECODE_" + index1 + "_BUTTON2");
            AddPort(guninput, "P1_BUTTON3", standard: "GUNCODE_" + index1 + "_BUTTON3 OR MOUSECODE_" + index1 + "_BUTTON3");

            // Start & Select
            AddPort(guninput, "P1_START", standard: "KEYCODE_1");
            AddPort(guninput, "P1_SELECT", standard: "KEYCODE_5");

            // Start & Coin aliases
            AddPort(guninput, "START1", standard: "KEYCODE_1");
            AddPort(guninput, "COIN1", standard: "KEYCODE_5");

            // Analog / Lightgun / Mouse axes
            AddPort(guninput, "P1_AD_STICK_X", standard: "GUNCODE_" + index1 + "_XAXIS OR MOUSECODE_" + index1 + "_XAXIS");
            AddPort(guninput, "P1_AD_STICK_Y", standard: "GUNCODE_" + index1 + "_YAXIS OR MOUSECODE_" + index1 + "_YAXIS");
            AddPort(guninput, "P1_LIGHTGUN_X", standard: "GUNCODE_" + index1 + "_XAXIS OR MOUSECODE_" + index1 + "_XAXIS");
            AddPort(guninput, "P1_LIGHTGUN_Y", standard: "GUNCODE_" + index1 + "_YAXIS OR MOUSECODE_" + index1 + "_YAXIS");
            AddPort(guninput, "P1_MOUSE_X", standard: "GUNCODE_" + index1 + "_XAXIS OR MOUSECODE_" + index1 + "_XAXIS");
            AddPort(guninput, "P1_MOUSE_Y", standard: "GUNCODE_" + index1 + "_YAXIS OR MOUSECODE_" + index1 + "_YAXIS");

            if (_multigun)
            {
                // Player 2
                AddPort(guninput, "P2_JOYSTICK_UP", "GUNCODE_" + index2 + "_BUTTON7");
                AddPort(guninput, "P2_JOYSTICK_DOWN", "GUNCODE_" + index2 + "_BUTTON9");
                AddPort(guninput, "P2_JOYSTICK_LEFT", "GUNCODE_" + index2 + "_BUTTON10");
                AddPort(guninput, "P2_JOYSTICK_RIGHT", "GUNCODE_" + index2 + "_BUTTON8");
                AddPort(guninput, "P2_BUTTON1", "GUNCODE_" + index2 + "_BUTTON1 OR MOUSECODE_" + index2 + "_BUTTON1");
                AddPort(guninput, "P2_BUTTON2", "GUNCODE_" + index2 + "_BUTTON2 OR MOUSECODE_" + index2 + "_BUTTON2");
                AddPort(guninput, "P2_BUTTON3", "GUNCODE_" + index2 + "_BUTTON3 OR MOUSECODE_" + index2 + "_BUTTON3");
                AddPort(guninput, "P2_START", "KEYCODE_2");
                AddPort(guninput, "P2_SELECT", "KEYCODE_6");
                AddPort(guninput, "START2", "KEYCODE_2");
                AddPort(guninput, "COIN2", "KEYCODE_6");
                AddPort(guninput, "P2_AD_STICK_X", "GUNCODE_" + index2 + "_XAXIS OR MOUSECODE_" + index2 + "_XAXIS");
                AddPort(guninput, "P2_AD_STICK_Y", "GUNCODE_" + index2 + "_YAXIS OR MOUSECODE_" + index2 + "_YAXIS");
                AddPort(guninput, "P2_LIGHTGUN_X", "GUNCODE_" + index2 + "_XAXIS OR MOUSECODE_" + index2 + "_XAXIS");
                AddPort(guninput, "P2_LIGHTGUN_Y", "GUNCODE_" + index2 + "_YAXIS OR MOUSECODE_" + index2 + "_YAXIS");
                AddPort(guninput, "P2_MOUSE_X", "GUNCODE_" + index2 + "_XAXIS OR MOUSECODE_" + index2 + "_XAXIS");
                AddPort(guninput, "P2_MOUSE_Y", "GUNCODE_" + index2 + "_YAXIS OR MOUSECODE_" + index2 + "_YAXIS");

                if (gunNb > 2) // Player 3
                    for (int p = 3; p <= Math.Min(gunNb, 4); p++)
                    {
                        string idx = p == 3 ? index3 : index4;
                        AddPort(guninput, $"P{p}_JOYSTICK_UP", "GUNCODE_" + idx + "_BUTTON7");
                        AddPort(guninput, $"P{p}_JOYSTICK_DOWN", "GUNCODE_" + idx + "_BUTTON9");
                        AddPort(guninput, $"P{p}_JOYSTICK_LEFT", "GUNCODE_" + idx + "_BUTTON10");
                        AddPort(guninput, $"P{p}_JOYSTICK_RIGHT", "GUNCODE_" + idx + "_BUTTON8");
                        AddPort(guninput, $"P{p}_BUTTON1", "GUNCODE_" + idx + "_BUTTON1 OR MOUSECODE_" + idx + "_BUTTON1");
                        AddPort(guninput, $"P{p}_BUTTON2", "GUNCODE_" + idx + "_BUTTON2 OR MOUSECODE_" + idx + "_BUTTON2");
                        AddPort(guninput, $"P{p}_BUTTON3", "GUNCODE_" + idx + "_BUTTON3 OR MOUSECODE_" + idx + "_BUTTON3");
                        AddPort(guninput, $"P{p}_START", "KEYCODE_" + p);
                        AddPort(guninput, $"P{p}_SELECT", "KEYCODE_" + (p + 4));
                        AddPort(guninput, $"START{p}", "KEYCODE_" + p);
                        AddPort(guninput, $"COIN{p}", "KEYCODE_" + (p + 4));
                        AddPort(guninput, $"P{p}_AD_STICK_X", "GUNCODE_" + idx + "_XAXIS OR MOUSECODE_" + idx + "_XAXIS");
                        AddPort(guninput, $"P{p}_AD_STICK_Y", "GUNCODE_" + idx + "_YAXIS OR MOUSECODE_" + idx + "_YAXIS");
                        AddPort(guninput, $"P{p}_LIGHTGUN_X", "GUNCODE_" + idx + "_XAXIS OR MOUSECODE_" + idx + "_XAXIS");
                        AddPort(guninput, $"P{p}_LIGHTGUN_Y", "GUNCODE_" + idx + "_YAXIS OR MOUSECODE_" + idx + "_YAXIS");
                        AddPort(guninput, $"P{p}_MOUSE_X", "GUNCODE_" + idx + "_XAXIS OR MOUSECODE_" + idx + "_XAXIS");
                        AddPort(guninput, $"P{p}_MOUSE_Y", "GUNCODE_" + idx + "_YAXIS OR MOUSECODE_" + idx + "_YAXIS");
                    }
            }
        }
        #endregion

        #region Xinput mapping dictionnaries
        static readonly Dictionary<string, string> xInputMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON7" },
            { "r3",             "BUTTON8" },
            { "l2",             "SLIDER1_NEG" },
            { "r2",             "SLIDER2_NEG" },
            { "l2trigger",      "SLIDER1_NEG_SWITCH" },
            { "r2trigger",      "SLIDER2_NEG_SWITCH" },
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
            { "l2trigger",      "RZAXIS_NEG_SWITCH" },  //differs
            { "r2trigger",      "ZAXIS_NEG_SWITCH"},    //differs
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

        private class ButtonLayoutMapping
        {
            public string Button { get; set; }
            public string MappingKey { get; set; }
            public string Extra { get; set; }

            public ButtonLayoutMapping(string button, string mappingKey, string extra)
            {
                Button = button;
                MappingKey = mappingKey;
                Extra = extra;
            }
        }
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
