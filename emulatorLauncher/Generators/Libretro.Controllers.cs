using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;
using System.Globalization;
using System.IO;

namespace emulatorLauncher.libRetro
{
    class LibretroControllers
    {
        private static string _inputDriver = "sdl2";
        private static HashSet<string> disabledAnalogModeSystems = new HashSet<string> { "n64", "dreamcast", "gamecube", "3ds" };

        public static bool WriteControllersConfig(ConfigFile retroconfig, string system, string core)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return false;
           
            /*
            bool allXInput = !Program.Controllers.Where(c => c.Input != null && c.Input.Type != "keyboard").Any(j => !j.Input.IsXInputDevice());
            if (allXInput)
                _inputDriver = "xinput";
            else
                SetupAutoConfig();
            */

            // no menu in non full uimode
            if (Program.SystemConfig.isOptSet("uimode") && Program.SystemConfig["uimode"] != "Full" && retroarchspecials.ContainsKey(InputKey.a))
                retroarchspecials.Remove(InputKey.a);

            cleanControllerConfig(retroconfig);

            foreach (var controller in Program.Controllers)
                WriteControllerConfig(retroconfig, controller, system);

            WriteHotKeyConfig(retroconfig);
            return true;
        }

        private static void SetupAutoConfig()
        {
            List<Controller> excludedControllers = new List<Controller>();

            var retroarchPath = Path.Combine(Program.AppConfig.GetFullPath("retroarch"), "autoconfig", _inputDriver);
            if (Directory.Exists(retroarchPath))
            {
                foreach (var file in Directory.GetFiles(retroarchPath, "*.cfg", SearchOption.AllDirectories))
                {
                    var cfg = ConfigFile.FromFile(file);
                    if (cfg == null || cfg["input_driver"] != "sdl2")
                        continue;

                    var sdl = SdlGameControllers.GetGameController(cfg["input_device"]);
                    if (sdl != null)
                    {
                        var ctl = Program.Controllers.FirstOrDefault(f => f.Config.ProductGuid == sdl.Guid);
                        if (ctl != null)
                            excludedControllers.Add(ctl);
                    }
                }

                foreach (var controller in Program.Controllers.Where(c => !excludedControllers.Contains(c)))
                {
                    var sdl = SdlGameControllers.GetGameController(controller.Config.ProductGuid);
                    if (sdl != null)
                    {
                        var cfg = new ConfigFile();

                        cfg["input_device"] = sdl.Name;
                        if (sdl.Name != controller.Config.DeviceName)
                            cfg["input_device_display_name"] = controller.Config.DeviceName;

                        cfg["input_driver"] = "sdl2";
                        cfg["input_vendor_id"] = sdl.VendorId.ToString();
                        cfg["input_product_id"] = sdl.ProductId.ToString();
                        cfg.Save(Path.Combine(retroarchPath, sdl.Name + ".cfg"), true);
                    }
                }
            }
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

            { InputKey.b, "reset"}, 
            { InputKey.a, "menu_toggle"},  // A et B inversés par rapport à batocera

            { InputKey.x, "load_state"}, 
            { InputKey.y, "save_state"}, 
            { InputKey.pageup, "screenshot"}, 
            //{ InputKey.start, "exit_emulator"},  
            { InputKey.up, "state_slot_increase"},  
            { InputKey.down, "state_slot_decrease"},  
            { InputKey.left, "rewind"},  
            { InputKey.right, "hold_fast_forward"}, 
            { InputKey.l2, "shader_prev"},  
            { InputKey.r2, "shader_next"},              
            { InputKey.pagedown, "ai_service"}      
        };
        
        private static void cleanControllerConfig(ConfigFile retroconfig)
        {
            retroconfig.DisableAll("input_player");
            foreach (var specialkey in retroarchspecials)
                retroconfig.DisableAll("input_" + specialkey.Value);
        }

        private static void WriteHotKeyConfig(ConfigFile config)
        {
            // Keyboard defaults
            config["input_enable_hotkey"] = "nul";
            config["input_enable_hotkey_axis"] = "nul";
            config["input_enable_hotkey_btn"] = "nul";
            config["input_enable_hotkey_mbtn"] = "nul";
            config["input_exit_emulator"] = "escape";
            config["input_menu_toggle"] = "f1";
            config["input_save_state"] = "f2";
            config["input_load_state"] = "f4";
            config["input_desktop_menu_toggle"] = "f5";
            config["input_state_slot_decrease"] = "f6";
            config["input_state_slot_increase"] = "f7";
            config["input_screenshot"] = "f8";
            config["input_rewind"] = "backspace";
            config["hold_fast_forward"] = "l";
            config["input_shader_next"] = "m";
            config["input_shader_prev"] = "n";

            config["input_bind_hold"] = "2";
            config["input_bind_timeout"] = "5";

            

            //config["input_ai_service"] = "nul";

            var c0 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            if (c0 == null || c0.Config == null)
                return;

            var hotKey = GetInputCode(c0, Tools.InputKey.hotkey);
            if (hotKey != null)
            {
                if (hotKey.Type != "key")
//                    config["input_enable_hotkey"] = getConfigValue(hotKey);
  //              else
                    config[string.Format("input_enable_hotkey_{0}", typetoname[hotKey.Type])] = getConfigValue(hotKey);
            }
        }

        private static string getAnalogMode(Controller controller, string system)
        {
            if (disabledAnalogModeSystems.Contains(system))
                return "0";
           
            foreach (var dirkey in retroarchdirs)
            {
                var k = GetInputCode(controller, dirkey);
                if (k != null && k.Type == "button" || k.Type == "hat")
                    return "1";
            }

            return "0";
        }

        private static Dictionary<string, string> generateControllerConfig(Controller controller, string system)
        {
            Dictionary<InputKey, string> retroarchbtns = new Dictionary<InputKey, string>()
            {
                { InputKey.b, "a" },
                { InputKey.a, "b" }, // A et B inversés par rapport à batocera
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

            if (system == "mame")
            {
                // Invert Dip switches and set it on r3 instead ( less annoying )
                retroarchbtns[InputKey.l3] = "r3";
                retroarchbtns[InputKey.r3] = "l3";
            }

            if (system == "atari800")
            {
                retroarchbtns[InputKey.b] = "b";
                retroarchbtns[InputKey.a] = "a";
            }
            else if (system == "atari5200")
            {
                retroarchbtns = new Dictionary<InputKey, string>()
                {
                    { InputKey.b, "x" },
                    { InputKey.a, "a" },
                    { InputKey.x, "b" }, 
                    { InputKey.y, "y" },
                    { InputKey.start, "start"}, 
                    { InputKey.pagedown, "select"} // select
                };
            }

            if (system == "gamecube")
            {
                retroarchbtns = new Dictionary<InputKey, string>()
                {
                    { InputKey.b, "a" },
                    { InputKey.a, "b" }, // A et B inversés par rapport à batocera
                    { InputKey.x, "x" }, 
                    { InputKey.y, "y" },
                    { InputKey.l2, "l2"},
                    { InputKey.r2, "r2"},
                    { InputKey.l3, "l3"}, 
                    { InputKey.r3, "r3"},
                    { InputKey.start, "start"}, 
                    { InputKey.pagedown, "select"} // select
                };
            }
            
            if (system == "n64")
            {
                // some input adaptations for some cores...
                // z is important, in case l2 (z) is not available for this pad, use l1
                if (controller.Config != null && controller.Config.Input != null && !controller.Config.Input.Any(i => i.Name == InputKey.r2))
                {
                    retroarchbtns[InputKey.pageup] = "l2";
                    retroarchbtns[InputKey.l2] = "l";
                }
            }

            var config = new Dictionary<string, string>();

            foreach (var btnkey in retroarchbtns)
            {
                var input = GetInputCode(controller, btnkey.Key);
                if (input == null)
                    continue;

                if (input.Type == "key")
                    config[string.Format("input_player{0}_{1}", controller.PlayerIndex, btnkey.Value)] = getConfigValue(input);
                else
                    config[string.Format("input_player{0}_{1}_{2}", controller.PlayerIndex, btnkey.Value, typetoname[input.Type])] = getConfigValue(input);
            }

            foreach (var btnkey in retroarchdirs)
            {
                var input = GetInputCode(controller, btnkey);
                if (input == null)
                    continue;

                if (input.Type == "key")
                    config[string.Format("input_player{0}_{1}", controller.PlayerIndex, btnkey)] = getConfigValue(input);
                else
                    config[string.Format("input_player{0}_{1}_{2}", controller.PlayerIndex, btnkey, typetoname[input.Type])] = getConfigValue(input);
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

            if (controller.PlayerIndex == 1)
            {
                foreach (var specialkey in retroarchspecials)
                {
                    var input = GetInputCode(controller, specialkey.Key);
                    if (input == null)
                        continue;

                    if (input.Type != "key")
            //            config[string.Format("input_{0}", specialkey.Value)] = getConfigValue(input);
            //        else
                        config[string.Format("input_{0}_{1}", specialkey.Value, typetoname[input.Type])] = getConfigValue(input);
                }
            }
            return config;
        }

        private static Input GetInputCode(Controller controller, InputKey btnkey)
        {
            if (_inputDriver == "sdl2")
                return controller.Config.ToSdlCode(btnkey);

            return controller.Config.ToXInputCodes(btnkey);
        }

        private static void WriteControllerConfig(ConfigFile retroconfig, Controller controller, string system)
        {
            // Seul sdl2 reconnait le bouton Guide
            retroconfig["input_joypad_driver"] = _inputDriver;

            // keyboard_gamepad_enable = "true"
            // keyboard_gamepad_mapping_type = "1"

            var generatedConfig = generateControllerConfig(controller, system);
            foreach (var key in generatedConfig)
                retroconfig[key.Key] = key.Value;

            retroconfig[string.Format("input_player{0}_joypad_index", controller.PlayerIndex)] = (controller.PlayerIndex-1).ToString();
            retroconfig[string.Format("input_player{0}_analog_dpad_mode", controller.PlayerIndex)] = getAnalogMode(controller, system);
        }

        private static string getConfigValue(Input input)
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
            {            // sdl2
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

        static Dictionary<string, retro_key> input_config_names = new Dictionary<string, retro_key>()
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
        
        static Dictionary<SDL.SDL_Keycode, retro_key> input_config_key_map = new Dictionary<SDL.SDL_Keycode, retro_key>()
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