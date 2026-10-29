using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace EmulatorLauncher
{
    partial class ArcadeDuckGenerator : Generator
    {
        private const int MaxPorts = 4;

        private bool _forceSDL = false;
        private Dictionary<string, int> _sdlSlotsByPath;
        private Dictionary<int, int> _sdlSlotsByIndex;

        private readonly Dictionary<string, Dictionary<string, List<string>>> _buttonMappings = new Dictionary<string, Dictionary<string, List<string>>>();

        // Layout aliases resolved by ArcadeDuck (arcade_control_registry.cpp, s_layout_aliases)
        static readonly Dictionary<string, string> _layoutAliases = new Dictionary<string, string>()
        {
            { "glpracr2", "glpracr" }, { "tekken3", "tekken" }, { "tektagt", "tekken" }, { "sfex2", "sfex" },
            { "fgtlayer", "sfex" }, { "soulclbr", "souledge" }, { "hypbbc2p", "hyperbbc" },
        };

        // MAME keyboard defaults for coin/start (also sent by most lightguns : Gun4IR, Sinden, Retro Shooter...)
        private static readonly string[] _mameCoinKeys = new string[] { "5", "6", "7", "8" };
        private static readonly string[] _mameStartKeys = new string[] { "1", "2", "3", "4" };

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for ArcadeDuck");

            _forceSDL = SystemConfig.getOptBoolean("input_forceSDL");

            var ports = _db != null ? _db.GetPorts(_game) : null;
            if (ports == null || ports.Count == 0)
                ports = Enumerable.Range(0, 2).Select(i => new ArcadeDuckDatabase.Port { Type = "arcade", Layout = "" }).ToList();

            // Input sources (keyboard and pointer are always enabled by ArcadeDuck)
            var pads = this.Controllers.Where(c => !c.IsKeyboard && c.Config != null).ToList();
            bool useXInput = !_forceSDL && pads.Any(c => c.IsXInputDevice);
            bool useSdl = _forceSDL || pads.Any(c => !c.IsXInputDevice);

            ini.WriteValue("InputSources", "DInput", "false");
            ini.WriteValue("InputSources", "XInput", useXInput ? "true" : "false");
            ini.WriteValue("InputSources", "SDL", useSdl ? "true" : "false");
            ini.WriteValue("InputSources", "RawInput", "false");    // Enabled later only if a mouse or a gun is bound

            if (useSdl)
                ResolveSdlPlayerSlots();

            // Ports are fully rewritten at each launch
            for (int i = 1; i <= MaxPorts; i++)
                ini.ClearSection("ArcadeControllerPort" + i);

            ini.ClearSection("ArcadeOperator");
            ResetHotkeysToDefault(ini);

            var mice = new ArcadeDuckPointerDevices(ports.Take(MaxPorts).Count(p => p != null && p.Type == "lightgun"));

            for (int i = 0; i < MaxPorts; i++)
            {
                string section = "ArcadeControllerPort" + (i + 1);
                var port = i < ports.Count ? ports[i] : null;

                if (port == null || string.IsNullOrEmpty(port.Type) || port.Type == "none")
                {
                    ini.WriteValue(section, "Type", "none");
                    continue;
                }

                ini.WriteValue(section, "Type", port.Type);
                ini.WriteValue(section, "Layout", port.Layout ?? "");

                var ctrl = this.Controllers.FirstOrDefault(c => c.PlayerIndex == i + 1 && c.Config != null);
                if (ctrl != null)
                {
                    if (ctrl.IsKeyboard)
                        ConfigureKeyboard(ini, section, port, ctrl.Config);
                    else
                        ConfigureJoystick(ini, section, port, ctrl, i);
                }

                // Mouse / gun for trackball and lightgun ports
                if (port.Type == "lightgun" || port.Type == "trackball")
                    mice.ConfigurePort(ini, section, port, i);

                // Coin (5-8) and start (1-4) always on keyboard keys, also used by lightguns to insert coins and start
                ini.AppendValue(section, "Coin", "Keyboard/" + _mameCoinKeys[i]);
                ini.AppendValue(section, "Start", "Keyboard/" + _mameStartKeys[i]);
            }

            mice.Finish(ini);

            // Operator panel (test / service)
            ini.AppendValue("ArcadeOperator", "Test", "Keyboard/9");
            ini.AppendValue("ArcadeOperator", "Service", "Keyboard/0");
        }

        #region SDL player slots
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_JoystickGetDevicePlayerIndex(int device_index);

        /// <summary>
        /// ArcadeDuck (SDL2 2.32) names SDL devices "SDL-N" where N is NOT the SDL device index but a player id (sdl_input_source.cpp, OpenDevice) :
        /// - the SDL player index when valid and not already used (SDL gives a free index to every game controller when it is added, XInput devices
        ///   read through the legacy driver keep their XInput slot),
        /// - otherwise the first free id among the devices already opened, in the order devices were added.
        /// The same enumeration is done here with the hints ArcadeDuck sets (SetHints) to get the same result.
        /// </summary>
        private void ResolveSdlPlayerSlots()
        {
            _sdlSlotsByPath = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            _sdlSlotsByIndex = new Dictionary<int, int>();

            try
            {
                // ArcadeDuck loads the controller database from its data root (portable) first, then from its resources
                string controllerDb = Path.Combine(_path, "gamecontrollerdb.txt");
                if (!File.Exists(controllerDb))
                    controllerDb = Path.Combine(_path, "resources", "gamecontrollerdb.txt");

                if (File.Exists(controllerDb))
                    SDL.SDL_SetHint("SDL_GAMECONTROLLERCONFIG_FILE", controllerDb);

                SDL.SDL_SetHint("SDL_JOYSTICK_HIDAPI_WII", "1");
                SDL.SDL_SetHint("SDL_JOYSTICK_HIDAPI_PS3", "1");

                if (SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK | SDL.SDL_INIT_GAMECONTROLLER) < 0)
                    return;

                var used = new HashSet<int>();
                int count = SDL.SDL_NumJoysticks();

                for (int i = 0; i < count; i++)
                {
                    int slot = SDL_JoystickGetDevicePlayerIndex(i);
                    if (slot < 0 || used.Contains(slot))
                    {
                        slot = 0;
                        while (used.Contains(slot))
                            slot++;
                    }

                    used.Add(slot);
                    _sdlSlotsByIndex[i] = slot;

                    string hidpath = SDL.SDL_JoystickPathForIndex(i);
                    if (!string.IsNullOrEmpty(hidpath))
                    {
                        _sdlSlotsByPath[hidpath] = slot;

                        string parent = InputDevices.GetInputDeviceParent(hidpath);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            _sdlSlotsByPath[parent] = slot;
                            _sdlSlotsByPath[InputDevices.ShortenDevicePath(parent)] = slot;
                        }
                    }

                    SimpleLogger.Instance.Info("[ArcadeDuck] SDL device " + i + " => SDL-" + slot + " (" + (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE ? "game controller" : "joystick") + ") " + hidpath);
                }

                SDL.SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK | SDL.SDL_INIT_GAMECONTROLLER);
                SDL.SDL_Quit();
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[ArcadeDuck] Unable to resolve SDL player slots : " + ex.Message);
            }
        }

        private int GetSdlSlot(Controller ctrl)
        {
            int slot;

            if (_sdlSlotsByPath != null && !string.IsNullOrEmpty(ctrl.DevicePath))
            {
                if (_sdlSlotsByPath.TryGetValue(ctrl.DevicePath, out slot) || _sdlSlotsByPath.TryGetValue(InputDevices.ShortenDevicePath(ctrl.DevicePath), out slot))
                    return slot;
            }

            int deviceIndex = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;

            if (_sdlSlotsByIndex != null && _sdlSlotsByIndex.TryGetValue(deviceIndex, out slot))
                return slot;

            return deviceIndex;
        }

        private string GetPadPrefix(Controller ctrl)
        {
            if (ctrl.IsXInputDevice && !_forceSDL && ctrl.XInput != null)
                return "XInput-" + ctrl.XInput.DeviceIndex + "/";

            return "SDL-" + GetSdlSlot(ctrl) + "/";
        }
        #endregion

        #region Joystick
        static readonly string[] mappingPaths =
        {
            // User specific
            "{userpath}\\inputmapping\\arcadeduck.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\arcadeduck.yml",
        };

        // yml keys are physical positions. With this GetInputKeyName (same as DuckStation), InputKey.x gives the west button and InputKey.y the north button.
        static readonly Dictionary<string, InputKey> yamlToPadInputKey = new Dictionary<string, InputKey>()
        {
            { "south", InputKey.a }, { "east", InputKey.b }, { "west", InputKey.x }, { "north", InputKey.y },
            { "l1", InputKey.pageup }, { "r1", InputKey.pagedown }, { "l2", InputKey.l2 }, { "r2", InputKey.r2 },
            { "l3", InputKey.l3 }, { "r3", InputKey.r3 }, { "select", InputKey.select }, { "start", InputKey.start },
            { "up", InputKey.up }, { "down", InputKey.down }, { "left", InputKey.left }, { "right", InputKey.right },
        };

        // EmulationStation keyboard configuration uses the SNES naming (x = north, y = west)
        static readonly Dictionary<string, InputKey> yamlToKeyboardInputKey = new Dictionary<string, InputKey>()
        {
            { "south", InputKey.a }, { "east", InputKey.b }, { "west", InputKey.y }, { "north", InputKey.x },
            { "l1", InputKey.pageup }, { "r1", InputKey.pagedown }, { "l2", InputKey.l2 }, { "r2", InputKey.r2 },
            { "l3", InputKey.l3 }, { "r3", InputKey.r3 }, { "select", InputKey.select }, { "start", InputKey.start },
            { "up", InputKey.up }, { "down", InputKey.down }, { "left", InputKey.left }, { "right", InputKey.right },
        };

        /// <summary>
        /// Button mapping of arcade panels (physical button => ArcadeDuck controls), from 'arcadeduck.yml' like the other arcade generators.
        /// Containers are searched for the set, its parent, then the ArcadeDuck control layout of the game (e.g. 'sfex', 'tekken'),
        /// each one with the 'controller_layout' suffix first, then 'default'.
        /// Without yml file, the RetroBat default layout is used (same as MAME / FBNeo : B1 west, B2 south, B3 east, B4 north, B5 L1, B6 R1).
        /// </summary>
        private Dictionary<string, List<string>> GetArcadeButtonMapping(string layout)
        {
            layout = layout ?? "";

            Dictionary<string, List<string>> mapping;
            if (_buttonMappings.TryGetValue(layout, out mapping))
                return mapping;

            mapping = LoadYmlButtonMapping(layout);

            if (mapping == null)
            {
                mapping = new Dictionary<string, List<string>>()
                {
                    { "west", new List<string> { "Button1" } },
                    { "south", new List<string> { "Button2" } },
                    { "east", new List<string> { "Button3" } },
                    { "north", new List<string> { "Button4" } },
                    { "l1", new List<string> { "Button5" } },
                    { "r1", new List<string> { "Button6" } },
                };
            }

            _buttonMappings[layout] = mapping;
            return mapping;
        }

        private Dictionary<string, List<string>> LoadYmlButtonMapping(string layout)
        {
            string ymlFile = Controller.GetSystemYmlMappingFile("arcadeduck", "", "arcadeduck", mappingPaths);
            if (string.IsNullOrEmpty(ymlFile))
                return null;

            YmlFile yml;
            try { yml = YmlFile.Load(ymlFile); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[ArcadeDuck] Unable to read " + ymlFile + " : " + ex.Message);
                return null;
            }

            if (yml == null)
                return null;

            string controllerLayout = SystemConfig["controller_layout"];
            if (controllerLayout == "classic8")
                controllerLayout = "6alternative";

            var bases = new List<string>();
            if (_game != null)
            {
                bases.Add(_game.Id);
                if (!string.IsNullOrEmpty(_game.Parent))
                    bases.Add(_game.Parent);
            }
            if (!string.IsNullOrEmpty(layout))
            {
                bases.Add(layout);

                string canonical;
                if (_layoutAliases.TryGetValue(layout, out canonical))
                    bases.Add(canonical);
            }
            bases.Add("default");

            var searchNames = new List<string>();
            foreach (var b in bases.Distinct(StringComparer.InvariantCultureIgnoreCase))
            {
                if (!string.IsNullOrEmpty(controllerLayout))
                    searchNames.Add(b + "_" + controllerLayout);
                searchNames.Add(b);
            }

            foreach (var name in searchNames)
            {
                var container = yml.Elements.FirstOrDefault(e => e.Name == name) as YmlContainer;
                if (container == null)
                    continue;

                var ret = new Dictionary<string, List<string>>();
                foreach (var element in container.Elements.OfType<YmlElement>())
                {
                    if (string.IsNullOrEmpty(element.Name) || string.IsNullOrEmpty(element.Value) || !yamlToPadInputKey.ContainsKey(element.Name))
                        continue;

                    // A physical button can drive several controls : "Button1, Enter"
                    ret[element.Name] = element.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
                }

                SimpleLogger.Instance.Info("[ArcadeDuck] Button mapping '" + name + "' loaded from " + ymlFile);
                return ret;
            }

            return null;
        }

        private static void AppendPadBinding(IniFile ini, string section, string key, string prefix, Controller ctrl, InputKey inputKey)
        {
            string name = GetInputKeyName(ctrl, inputKey);
            if (string.IsNullOrEmpty(name) || name == "None")
                return;

            ini.AppendValue(section, key, prefix + name);
        }

        private void ConfigureJoystick(IniFile ini, string section, ArcadeDuckDatabase.Port port, Controller ctrl, int portIndex)
        {
            string prefix = GetPadPrefix(ctrl);
            SimpleLogger.Instance.Info("[ArcadeDuck] Port " + (portIndex + 1) + " (" + port.Type + ") : " + ctrl.Name + " => " + prefix.TrimEnd('/'));

            AppendPadBinding(ini, section, "Coin", prefix, ctrl, InputKey.select);
            AppendPadBinding(ini, section, "Start", prefix, ctrl, InputKey.start);

            switch (port.Type)
            {
                case "driving":
                    // Steering is converted to a centered full axis by ArcadeDuck (NormalizeCenteredControllerAxisBinding)
                    string steering = GetInputKeyName(ctrl, InputKey.leftanalogleft);
                    if (steering.EndsWith("LeftX"))
                        ini.AppendValue(section, "Steering", prefix + "FullLeftX");

                    AppendPadBinding(ini, section, "Accelerator", prefix, ctrl, InputKey.r2);
                    AppendPadBinding(ini, section, "Brake", prefix, ctrl, InputKey.l2);
                    AppendPadBinding(ini, section, "GearUp", prefix, ctrl, InputKey.pagedown);
                    AppendPadBinding(ini, section, "GearDown", prefix, ctrl, InputKey.pageup);
                    AppendPadBinding(ini, section, "MusicNext", prefix, ctrl, InputKey.pagedown);
                    AppendPadBinding(ini, section, "MusicPrevious", prefix, ctrl, InputKey.pageup);
                    AppendPadBinding(ini, section, "View", prefix, ctrl, InputKey.y);
                    AppendPadBinding(ini, section, "Horn", prefix, ctrl, InputKey.x);
                    AppendPadBinding(ini, section, "Handbrake", prefix, ctrl, InputKey.b);
                    AppendPadBinding(ini, section, "Button1", prefix, ctrl, InputKey.a);
                    AppendPadBinding(ini, section, "Button2", prefix, ctrl, InputKey.b);
                    WriteMenuBindings(ini, section, prefix, ctrl);
                    break;

                case "lightgun":
                    // Aiming is done with a gun or a mouse (see ArcadeDuck.Guns.cs), the pad only gives coin, start and reload
                    AppendPadBinding(ini, section, "Reload", prefix, ctrl, InputKey.b);
                    AppendPadBinding(ini, section, "Button2", prefix, ctrl, InputKey.x);
                    WriteMenuBindings(ini, section, prefix, ctrl);
                    break;

                case "trackball":
                    WriteDirections(ini, section, prefix, ctrl);
                    AppendPadBinding(ini, section, "Button1", prefix, ctrl, InputKey.a);
                    AppendPadBinding(ini, section, "Button2", prefix, ctrl, InputKey.b);
                    break;

                case "tokimeki":
                    WriteDirections(ini, section, prefix, ctrl);
                    AppendPadBinding(ini, section, "Button1", prefix, ctrl, InputKey.a);
                    AppendPadBinding(ini, section, "GSRIncrease", prefix, ctrl, InputKey.pagedown);
                    AppendPadBinding(ini, section, "GSRDecrease", prefix, ctrl, InputKey.pageup);
                    break;

                default:
                    WriteDirections(ini, section, prefix, ctrl);
                    foreach (var button in GetArcadeButtonMapping(port.Layout))
                    {
                        foreach (var control in button.Value)
                            AppendPadBinding(ini, section, control, prefix, ctrl, yamlToPadInputKey[button.Key]);
                    }
                    break;
            }

            // Hotkeys and operator buttons (test / service) from player 1 controller
            if (portIndex == 0)
                WriteJoystickHotkeys(ini, prefix, ctrl);
        }

        private static void WriteDirections(IniFile ini, string section, string prefix, Controller ctrl)
        {
            // D-pad and left stick, ArcadeDuck turns a single stick half axis into a digital input
            AppendPadBinding(ini, section, "Up", prefix, ctrl, InputKey.up);
            AppendPadBinding(ini, section, "Down", prefix, ctrl, InputKey.down);
            AppendPadBinding(ini, section, "Left", prefix, ctrl, InputKey.left);
            AppendPadBinding(ini, section, "Right", prefix, ctrl, InputKey.right);
            AppendPadBinding(ini, section, "Up", prefix, ctrl, InputKey.leftanalogup);
            AppendPadBinding(ini, section, "Down", prefix, ctrl, InputKey.leftanalogdown);
            AppendPadBinding(ini, section, "Left", prefix, ctrl, InputKey.leftanalogleft);
            AppendPadBinding(ini, section, "Right", prefix, ctrl, InputKey.leftanalogright);
        }

        private static void WriteMenuBindings(IniFile ini, string section, string prefix, Controller ctrl)
        {
            // Cabinet menu buttons (Kart Duel, Truck Kyosokyoku, Golgo 13...)
            AppendPadBinding(ini, section, "SelectUp", prefix, ctrl, InputKey.up);
            AppendPadBinding(ini, section, "SelectDown", prefix, ctrl, InputKey.down);
            AppendPadBinding(ini, section, "SelectLeft", prefix, ctrl, InputKey.left);
            AppendPadBinding(ini, section, "SelectRight", prefix, ctrl, InputKey.right);
            AppendPadBinding(ini, section, "Enter", prefix, ctrl, InputKey.a);
            AppendPadBinding(ini, section, "Select", prefix, ctrl, InputKey.a);
        }

        private void WriteJoystickHotkeys(IniFile ini, string prefix, Controller ctrl)
        {
            // Operator panel on player 1 controller : R3 = test (operator menu), L3 = service credit
            string r3 = GetInputKeyName(ctrl, InputKey.r3);
            string l3 = GetInputKeyName(ctrl, InputKey.l3);
            if (r3 != "None")
                ini.AppendValue("ArcadeOperator", "Test", prefix + r3);
            if (l3 != "None")
                ini.AppendValue("ArcadeOperator", "Service", prefix + l3);

            string hotKeyName = GetInputKeyName(ctrl, InputKey.hotkey);
            if (string.IsNullOrEmpty(hotKeyName) || hotKeyName == "None")
                return;

            // User controller hotkeys from arcadeduck_controller_hotkeys.yml / controller_hotkeys.yml, otherwise RetroBat defaults
            var padHotkeys = new List<KeyValuePair<InputKey, string>>();

            if (Hotkeys.GetPadHKFromFile("arcadeduck", "", out Dictionary<string, string> padHKDic))
            {
                foreach (var hotkey in padHKDic)
                {
                    // The yml uses the SNES naming (x = north, y = west), this GetInputKeyName gives west for InputKey.x
                    string value = hotkey.Value;
                    if (value == "x")
                        value = "y";
                    else if (value == "y")
                        value = "x";

                    if (Enum.TryParse(value, true, out InputKey inputKey))
                        padHotkeys.Add(new KeyValuePair<InputKey, string>(inputKey, hotkey.Key));
                }
            }
            else
                padHotkeys.AddRange(_padHotkeys);

            foreach (var hotkey in padHotkeys)
            {
                string inputKeyName = GetInputKeyName(ctrl, hotkey.Key);
                if (string.IsNullOrEmpty(inputKeyName) || inputKeyName == "None")
                    continue;

                string hkKey = hotkey.Value;
                if (hkKey == "FastForward" && SystemConfig.getOptBoolean("fastforward_toggle"))
                    hkKey = "ToggleFastForward";

                ini.AppendValue("Hotkeys", hkKey, prefix + hotKeyName + " & " + prefix + inputKeyName);
            }
        }

        private static readonly Dictionary<InputKey, string> _padHotkeys = new Dictionary<InputKey, string>()
        {
            { InputKey.start, "PowerOff" },
            { InputKey.a, "OpenPauseMenu" },
            { InputKey.x, "SaveSelectedSaveState" },
            { InputKey.y, "LoadSelectedSaveState" },
            { InputKey.up, "SelectNextSaveStateSlot" },
            { InputKey.down, "SelectPreviousSaveStateSlot" },
            { InputKey.left, "Rewind" },
            { InputKey.right, "FastForward" },
            { InputKey.l3, "TogglePause" },
            { InputKey.r3, "Screenshot" },
        };

        private void ResetHotkeysToDefault(IniFile ini)
        {
            ini.ClearSection("Hotkeys");

            // User keyboard hotkeys from arcadeduck_kb_hotkeys.yml / kb_hotkeys.yml (key names converted with kbhotkeysdics.json)
            if (Hotkeys.GetHotKeysFromFile("arcadeduck", "", out Dictionary<string, HotkeyResult> custoHotkeys) && custoHotkeys.Count > 0)
            {
                foreach (var hotkey in custoHotkeys)
                    ini.AppendValue("Hotkeys", hotkey.Value.EmulatorKey, "Keyboard/" + hotkey.Value.EmulatorValue);

                return;
            }

            // RetroBat defaults, ArcadeDuck uses Qt key names (qtkeycodes.cpp)
            ini.AppendValue("Hotkeys", "OpenPauseMenu", "Keyboard/F1");
            ini.AppendValue("Hotkeys", "SaveSelectedSaveState", "Keyboard/F2");
            ini.AppendValue("Hotkeys", "LoadSelectedSaveState", "Keyboard/F4");
            ini.AppendValue("Hotkeys", "SelectPreviousSaveStateSlot", "Keyboard/F6");
            ini.AppendValue("Hotkeys", "SelectNextSaveStateSlot", "Keyboard/F7");
            ini.AppendValue("Hotkeys", "Screenshot", "Keyboard/F8");
            ini.AppendValue("Hotkeys", "TogglePause", "Keyboard/P");
            ini.AppendValue("Hotkeys", "Rewind", "Keyboard/Backspace");
            ini.AppendValue("Hotkeys", "FastForward", "Keyboard/L");
            ini.AppendValue("Hotkeys", "ToggleFastForward", "Keyboard/Space");
            ini.AppendValue("Hotkeys", "FrameStep", "Keyboard/K");
            ini.AppendValue("Hotkeys", "ToggleFullscreen", "Keyboard/F");
            ini.AppendValue("Hotkeys", "PowerOff", "Keyboard/Escape");
        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            Int64 pid;
            bool hwOrder = c.UsesHardwareButtonOrder;

            key = key.GetRevertedAxis(out bool revertAxis);

            /*if (c.VendorID == USB_VENDOR.NINTENDO)
            {
                if (key == InputKey.a)
                    key = InputKey.b;
                else if (key == InputKey.b)
                    key = InputKey.a;
                else if (key == InputKey.x)
                    key = InputKey.y;
                else if (key == InputKey.y)
                    key = InputKey.x;
            }*/

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return "A";
                        case 1: return "B";
                        case 2: return "Y";
                        case 3: return "X";
                        case 4: return hwOrder ? "LeftShoulder" : "Back";
                        case 5: return hwOrder ? "RightShoulder" : "Guide";
                        case 6: return hwOrder ? "Back" : "Start";
                        case 7: return hwOrder ? "Start" : "LeftStick";
                        case 8: return hwOrder ? "LeftStick" : "RightStick";
                        case 9: return hwOrder ? "RightStick" : "LeftShoulder";
                        case 10: return hwOrder ? "Guide" : "RightShoulder";
                        case 11: return "DPadUp";
                        case 12: return "DPadDown";
                        case 13: return "DPadLeft";
                        case 14: return "DPadRight";
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    bool positive = (!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0);
                    switch (pid)
                    {
                        case 0: return positive ? "+LeftX" : "-LeftX";
                        case 1: return positive ? "+LeftY" : "-LeftY";
                        case 2: return positive ? "+RightX" : "-RightX";
                        case 3: return positive ? "+RightY" : "-RightY";
                        case 4: return "+LeftTrigger";
                        case 5: return "+RightTrigger";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "DPadUp";
                        case 2: return "DPadRight";
                        case 4: return "DPadDown";
                        case 8: return "DPadLeft";
                    }
                }
            }
            return "None";
        }
        #endregion

        #region Keyboard
        private void ConfigureKeyboard(IniFile ini, string section, ArcadeDuckDatabase.Port port, InputConfig keyboard)
        {
            Action<string, InputKey> append = (key, k) =>
            {
                var a = keyboard[k];
                if (a == null)
                    return;

                string name = SdlToQtKeyName(a.Id);
                if (name != null)
                    ini.AppendValue(section, key, "Keyboard/" + name);
            };

            append("Coin", InputKey.select);
            append("Start", InputKey.start);

            if (port.Type == "driving")
            {
                // Steering needs an analog axis : not mapped on keyboard
                append("Accelerator", InputKey.a);
                append("Brake", InputKey.b);
                append("View", InputKey.x);
                append("GearUp", InputKey.pagedown);
                append("GearDown", InputKey.pageup);
                append("Button1", InputKey.a);
                append("Button2", InputKey.b);
                return;
            }

            // Lightgun layouts have no directions (Konami GV would read them, and DolphinBar Wiimotes use the arrow keys)
            if (port.Type == "lightgun")
            {
                append("Reload", InputKey.b);
                return;
            }

            append("Up", InputKey.up);
            append("Down", InputKey.down);
            append("Left", InputKey.left);
            append("Right", InputKey.right);

            if (port.Type == "tokimeki")
            {
                append("Button1", InputKey.a);
                append("GSRIncrease", InputKey.pagedown);
                append("GSRDecrease", InputKey.pageup);
                return;
            }

            if (port.Type == "trackball")
            {
                append("Button1", InputKey.a);
                append("Button2", InputKey.b);
                return;
            }

            // Same physical layout as the pads (arcadeduck.yml)
            foreach (var button in GetArcadeButtonMapping(port.Layout))
            {
                foreach (var control in button.Value)
                    append(control, yamlToKeyboardInputKey[button.Key]);
            }
        }

        /// <summary>
        /// Converts an SDL keycode (EmulationStation keyboard configuration) to the Qt key name used by ArcadeDuck.
        /// </summary>
        private static string SdlToQtKeyName(long sdlCode)
        {
            if (sdlCode >= 0x61 && sdlCode <= 0x7A)
                return ((char)(sdlCode - 0x20)).ToString();         // A-Z

            if (sdlCode >= 0x30 && sdlCode <= 0x39)
                return ((char)sdlCode).ToString();                  // 0-9

            if (sdlCode >= 0x4000003A && sdlCode <= 0x40000045)
                return "F" + (sdlCode - 0x4000003A + 1);            // F1-F12

            if (sdlCode >= 0x40000059 && sdlCode <= 0x40000061)
                return "Numpad" + (sdlCode - 0x40000059 + 1);       // Numpad1-9

            switch (sdlCode)
            {
                case 0x0D: return "Return";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x1B: return "Escape";
                case 0x20: return "Space";
                case 0x27: return "Apostrophe";
                case 0x2C: return "Comma";
                case 0x2D: return "Minus";
                case 0x2E: return "Period";
                case 0x2F: return "Slash";
                case 0x3B: return "Semicolon";
                case 0x3D: return "Equal";
                case 0x5B: return "BracketLeft";
                case 0x5C: return "Backslash";
                case 0x5D: return "BracketRight";
                case 0x60: return "QuoteLeft";
                case 0x7F: return "Delete";
                case 0x40000039: return "CapsLock";
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
                case 0x40000054: return "NumpadSlash";
                case 0x40000055: return "NumpadAsterisk";
                case 0x40000056: return "NumpadMinus";
                case 0x40000057: return "NumpadPlus";
                case 0x40000058: return "NumpadEnter";
                case 0x40000062: return "Numpad0";
                case 0x40000063: return "NumpadPeriod";
                case 0x400000E0:
                case 0x400000E4: return "Control";
                case 0x400000E1:
                case 0x400000E5: return "Shift";
                case 0x400000E2:
                case 0x400000E6: return "Alt";
            }

            return null;
        }
        #endregion
    }
}
