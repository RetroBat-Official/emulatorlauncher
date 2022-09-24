using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher.Tools
{
    // Controllers known by SDL2 & Retroarch
    class SdlGameControllers
    {
        static SdlGameControllers()
        {
            _controllers = new Dictionary<Guid, SdlGameControllers>();

            SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);
            
            int numJoysticks = SDL.SDL_NumJoysticks();
            SimpleLogger.Instance.Info("[SdlGameControllers] " + numJoysticks + " SDL controller(s) connected");

            var ver = SDL.Version;

            for (int i = 0; i < numJoysticks; i++)
            {
                var guid = SDL.SDL_JoystickGetDeviceGUID(i);
                if (_controllers.ContainsKey(guid))
                    continue;

                var sdlGuid = InputConfig.ToSdlGuidString(guid);

                var name = SDL.SDL_GameControllerNameForIndex(i);
                if (string.IsNullOrEmpty(name))
                    continue;
                
                SdlGameControllers ctl = new SdlGameControllers();
                ctl.Guid = guid;
                ctl.VendorId = int.Parse((sdlGuid.Substring(10, 2) + sdlGuid.Substring(8, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.ProductId = int.Parse((sdlGuid.Substring(18, 2) + sdlGuid.Substring(16, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.Name = name;
                ctl.Path = SDL.SDL_JoystickPathForIndex(i);
               
                if (SDL.SDL_IsGameController(i) != SDL.SDL_bool.SDL_TRUE)
                {
                    SimpleLogger.Instance.Info("[SdlGameControllers] Loading Unknown SDL controller mapping : " + i + " => " + ctl.ToString());
                    continue;
                }
                else
                    SimpleLogger.Instance.Info("[SdlGameControllers] Loading SDL controller mapping : " + i + " => " + ctl.ToString());

                var mappingString = SDL.SDL_GameControllerMappingForDeviceIndex(i);
                if (string.IsNullOrEmpty(mappingString))
                    continue;

                string[] mapArray = mappingString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (mapArray.Length == 0)
                    continue;

                ctl.Mapping = ExtractMapping(mapArray.Skip(2));
                ctl.SdlBinding = mappingString;

                _controllers[ctl.Guid] = ctl;
            }

            // Add all other mappings ( Debug without physical controller )
            for (int i = 0; i < SDL.SDL_GameControllerNumMappings(); i++)
            {
                var mappingString = SDL.SDL_GameControllerMappingForIndex(i);

                string[] mapArray = mappingString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
                if (mapArray.Length == 0)
                    continue;

                SdlGameControllers ctl = new SdlGameControllers();
                ctl.Guid = InputConfig.FromSdlGuidString(mapArray[0]);
                if (_controllers.ContainsKey(ctl.Guid))
                    continue;

                ctl.VendorId = int.Parse((mapArray[0].Substring(10, 2) + mapArray[0].Substring(8, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.ProductId = int.Parse((mapArray[0].Substring(18, 2) + mapArray[0].Substring(16, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.Name = mapArray[1];
                ctl.Mapping = ExtractMapping(mapArray.Skip(2));
                ctl.SdlBinding = mappingString;

                _controllers[ctl.Guid] = ctl;
            }
    
            SDL.SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_Quit();
        }

        private static SdlControllerMapping[] ExtractMapping(IEnumerable<string> mapArray)
        {
            var sdlMapping = new List<SdlControllerMapping>();

            foreach (var tt in mapArray)
            {
                var map = tt.Split(new char[] { ':' });
                if (map.Length != 2)
                    continue;

                var sm = new SdlControllerMapping();

                char half_axis_output = ' ';

                string name = map[0];
                if (name.Length > 0 && (name[0] == '+' || name[0] == '-'))
                {
                    half_axis_output = name[0];
                    name = name.Substring(1);
                }

                if (half_axis_output == '+')
                {
                    sm.AxisMin = 0;
                    sm.AxisMax = 32767;
                }
                else if (half_axis_output == '-')
                {
                    sm.AxisMin = 0;
                    sm.AxisMax = -32768;
                }
                else
                {
                    sm.AxisMin = -32768;
                    sm.AxisMax = 32767;
                }

                sm.Button = SDL_CONTROLLER_BUTTON.INVALID;
                sm.Axis = SDL_CONTROLLER_AXIS.INVALID;

                switch (map[0])
                {
                    // Buttons
                    case "a": sm.Button = SDL_CONTROLLER_BUTTON.A; break;
                    case "b": sm.Button = SDL_CONTROLLER_BUTTON.B; break;
                    case "back": sm.Button = SDL_CONTROLLER_BUTTON.BACK; break;
                    case "dpdown": sm.Button = SDL_CONTROLLER_BUTTON.DPAD_DOWN; break;
                    case "dpleft": sm.Button = SDL_CONTROLLER_BUTTON.DPAD_LEFT; break;
                    case "dpright": sm.Button = SDL_CONTROLLER_BUTTON.DPAD_RIGHT; break;
                    case "dpup": sm.Button = SDL_CONTROLLER_BUTTON.DPAD_UP; break;
                    case "leftshoulder": sm.Button = SDL_CONTROLLER_BUTTON.LEFTSHOULDER; break;
                    case "rightstick": sm.Button = SDL_CONTROLLER_BUTTON.RIGHTSTICK; break;
                    case "leftstick": sm.Button = SDL_CONTROLLER_BUTTON.LEFTSTICK; break;
                    case "rightshoulder": sm.Button = SDL_CONTROLLER_BUTTON.RIGHTSHOULDER; break;
                    case "start": sm.Button = SDL_CONTROLLER_BUTTON.START; break;
                    case "x": sm.Button = SDL_CONTROLLER_BUTTON.X; break;
                    case "y": sm.Button = SDL_CONTROLLER_BUTTON.Y; break;
                    case "guide": sm.Button = SDL_CONTROLLER_BUTTON.GUIDE; break;

                    // Axis
                    case "lefttrigger": sm.Axis = SDL_CONTROLLER_AXIS.TRIGGERLEFT; break;
                    case "righttrigger": sm.Axis = SDL_CONTROLLER_AXIS.TRIGGERRIGHT; break;
                    case "leftx": sm.Axis = SDL_CONTROLLER_AXIS.LEFTX; break;
                    case "lefty": sm.Axis = SDL_CONTROLLER_AXIS.LEFTY; break;
                    case "rightx": sm.Axis = SDL_CONTROLLER_AXIS.RIGHTX; break;
                    case "righty": sm.Axis = SDL_CONTROLLER_AXIS.RIGHTY; break;

                    default:
                        continue;
                }

                Input cfg = new Input();

                if (map[1].StartsWith("b"))
                {
                    cfg.Type = "button";
                    cfg.Id = map[1].Substring(1).ToInteger();
                    cfg.Value = 1;
                }
                else if (map[1].StartsWith("a"))
                {
                    cfg.Type = "axis";
                    cfg.Id = map[1].Substring(1).ToInteger();
                    cfg.Value = 1;
                }
                else if (map[1].StartsWith("+a"))
                {
                    cfg.Type = "axis";
                    cfg.Id = map[1].Substring(2).ToInteger();
                    cfg.Value = 1;
                }
                else if (map[1].StartsWith("-a"))
                {
                    cfg.Type = "axis";
                    cfg.Id = map[1].Substring(2).ToInteger();
                    cfg.Value = -1;
                }
                else if (map[1].StartsWith("h"))
                {
                    var hatIds = map[1].Substring(1).Split(new char[] { '.' }).Select(v => v.ToInteger()).ToArray();
                    if (hatIds.Length > 1)
                    {
                        cfg.Type = "hat";
                        cfg.Id = hatIds[0];
                        cfg.Value = hatIds[1];
                    }
                }
                else
                    continue;

                sm.Input = cfg;
                sdlMapping.Add(sm);
            }

            return sdlMapping.ToArray();
        }

        public static bool IsGameController(Guid guid)
        {
            return _controllers.ContainsKey(guid);
        }

        public static SdlControllerMapping[] GetGameControllerMapping(Guid guid)
        {
            SdlGameControllers ctrl;
            if (_controllers.TryGetValue(guid, out ctrl))
                return ctrl.Mapping;

            return null;
        }

        public static SdlGameControllers GetGameController(Guid guid)
        {
            SdlGameControllers ctrl;
            if (_controllers.TryGetValue(guid, out ctrl))
                return ctrl;

            return null;
        }

        public static SdlGameControllers GetGameController(string name)
        {
            return _controllers.Values.Where(c => c.Name == name).FirstOrDefault();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Path))
                return Guid + ", " + Name + ", " + Path;

            return Guid + ", " + Name;
        }

        static Dictionary<Guid, SdlGameControllers> _controllers;

        public Guid Guid { get; set; }
        public string Name { get; set; }
        public SdlControllerMapping[] Mapping { get; set; }
        public string SdlBinding { get; set; }
        public int VendorId { get; set; }
        public int ProductId { get; set; }

        public string Path { get; set; }
    }

    class SdlControllerMapping
    {
        public SDL_CONTROLLER_BUTTON Button { get; set; }
        public SDL_CONTROLLER_AXIS Axis { get; set; }

        public int AxisMin { get; set; }
        public int AxisMax { get; set; }

        public Input Input { get; set; }

        public override string ToString()
        {
            if (Button == SDL_CONTROLLER_BUTTON.INVALID)
                return Axis.ToString() + " => " + Input.ToString();

            return Button.ToString() + "  => " + Input.ToString();
        }
    }
}
