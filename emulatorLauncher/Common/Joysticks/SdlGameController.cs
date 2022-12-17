using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher.Tools
{
    // Controllers known by SDL2 & Retroarch
    class SdlGameController
    {
        static SdlGameController()
        {
            _controllersByGuid = new Dictionary<Guid, SdlGameController>();
            _controllersByPath = new Dictionary<string, SdlGameController>();
                       
            SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);
            
            int numJoysticks = SDL.SDL_NumJoysticks();
            SimpleLogger.Instance.Info("[SdlGameController] " + numJoysticks + " SDL controller(s) connected");

            var ver = SDL.Version;

            for (int i = 0; i < numJoysticks; i++)
            {
                var guid = SDL.SDL_JoystickGetDeviceGUID(i);
                var sdlGuid = guid.ToSdlGuidString();

                var name = SDL.SDL_GameControllerNameForIndex(i);

                SdlGameController ctl = new SdlGameController();
                ctl.Index = i;
                ctl.Guid = guid;
                ctl.VendorId = ushort.Parse((sdlGuid.Substring(10, 2) + sdlGuid.Substring(8, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.ProductId = ushort.Parse((sdlGuid.Substring(18, 2) + sdlGuid.Substring(16, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.Name = name;

                string hidpath = SDL.SDL_JoystickPathForIndex(i);
                if (!string.IsNullOrEmpty(hidpath))
                {
                    _controllersByPath[hidpath] = ctl;

                    ctl.Path = InputDevices.GetInputDeviceParent(hidpath);
                    _controllersByPath[ctl.Path] = ctl;

                    var shortenPath = InputDevices.ShortenDevicePath(ctl.Path);
                    if (shortenPath != ctl.Path)
                        _controllersByPath[shortenPath] = ctl;
                }

                _controllersByPath[i.ToString().PadLeft(4, '0') + "@" + sdlGuid] = ctl;

                if (SDL.SDL_IsGameController(i) != SDL.SDL_bool.SDL_TRUE)
                {
                    SimpleLogger.Instance.Info("[SdlGameController] Loading Unknown SDL controller mapping : " + i + " => " + ctl.ToString());
                    continue;
                }
                else
                    SimpleLogger.Instance.Info("[SdlGameController] Loading SDL controller mapping : " + i + " => " + ctl.ToString());

                if (_controllersByGuid.ContainsKey(guid))
                    continue;

                if (string.IsNullOrEmpty(name))
                    continue;

                var mappingString = SDL.SDL_GameControllerMappingForDeviceIndex(i);
                if (string.IsNullOrEmpty(mappingString))
                    continue;

                string[] mapArray = mappingString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (mapArray.Length == 0)
                    continue;

                ctl.Mapping = ExtractMapping(mapArray.Skip(2));
                ctl.SdlBinding = mappingString;

                _controllersByGuid[ctl.Guid] = ctl;

                var oldGuid = ctl.Guid.ConvertSdlGuid(ctl.Name, SdlVersion.SDL2_0_X);
                if (ctl.Guid != oldGuid)
                    _controllersByGuid[oldGuid] = ctl;

                oldGuid = ctl.Guid.ConvertSdlGuid(ctl.Name, SdlVersion.SDL2_26);
                if (ctl.Guid != oldGuid)
                    _controllersByGuid[oldGuid] = ctl;
            }

            // Add all other mappings ( Debug without physical controller )
            for (int i = 0; i < SDL.SDL_GameControllerNumMappings(); i++)
            {
                var mappingString = SDL.SDL_GameControllerMappingForIndex(i);

                string[] mapArray = mappingString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
                if (mapArray.Length == 0)
                    continue;

                SdlGameController ctl = new SdlGameController();
                ctl.Guid = mapArray[0].FromSdlGuidString();
                if (_controllersByGuid.ContainsKey(ctl.Guid))
                    continue;

                ctl.VendorId = ushort.Parse((mapArray[0].Substring(10, 2) + mapArray[0].Substring(8, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.ProductId = ushort.Parse((mapArray[0].Substring(18, 2) + mapArray[0].Substring(16, 2)).ToUpper(), System.Globalization.NumberStyles.HexNumber);
                ctl.Name = mapArray[1];
                ctl.Mapping = ExtractMapping(mapArray.Skip(2));
                ctl.SdlBinding = mappingString;

                _controllersByGuid[ctl.Guid] = ctl;

                var oldGuid = ctl.Guid.ConvertSdlGuid(ctl.Name, SdlVersion.SDL2_0_X);
                if (ctl.Guid != oldGuid)
                    _controllersByGuid[oldGuid] = ctl;

                oldGuid = ctl.Guid.ConvertSdlGuid(ctl.Name, SdlVersion.SDL2_26);
                if (ctl.Guid != oldGuid)
                    _controllersByGuid[oldGuid] = ctl;
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
        
        public static SdlGameController GetGameController(Guid guid)
        {
            SdlGameController ctrl;
            if (_controllersByGuid.TryGetValue(guid, out ctrl))
                return ctrl;

            var oldGuid = guid.ConvertSdlGuid("", SdlVersion.SDL2_0_X);
            if (guid != oldGuid)
            {
                if (_controllersByGuid.TryGetValue(oldGuid, out ctrl))
                    return ctrl;
            }

            return null;
        }

        public static int GetControllerIndex(Controller ctrl)
        {
            var sdlDev = SdlGameController.GetGameControllerByPath(ctrl.DevicePath);
            return sdlDev != null ? sdlDev.Index : ctrl.DeviceIndex;
        }

        public static SdlGameController GetGameControllerByPath(string path)
        {
            SdlGameController ctrl;

            if (_controllersByPath.TryGetValue(path, out ctrl))
                return ctrl;

            if (_controllersByPath.TryGetValue(InputDevices.ShortenDevicePath(path), out ctrl))
                return ctrl;            

            return null;
        }

        public static SdlGameController GetGameController(string name)
        {
            return _controllersByGuid.Values.Where(c => c.Name == name).FirstOrDefault();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Path))
                return Guid + ", " + Name + ", " + Path;

            return Guid + ", " + Name;
        }

        static Dictionary<Guid, SdlGameController> _controllersByGuid;
        static Dictionary<string, SdlGameController> _controllersByPath;

        public int Index { get; set; }
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public SdlControllerMapping[] Mapping { get; set; }
        public string SdlBinding { get; set; }
        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }

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

    enum SDL_CONTROLLER_BUTTON
    {
        INVALID = -1,

        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        BACK = 4,
        GUIDE = 5,
        START = 6,
        LEFTSTICK = 7,
        RIGHTSTICK = 8,
        LEFTSHOULDER = 9,
        RIGHTSHOULDER = 10,
        DPAD_UP = 11,
        DPAD_DOWN = 12,
        DPAD_LEFT = 13,
        DPAD_RIGHT = 14
    };

    enum SDL_CONTROLLER_AXIS
    {
        INVALID = -1,

        LEFTX = 0,
        LEFTY = 1,
        RIGHTX = 2,
        RIGHTY = 3,
        TRIGGERLEFT = 4,
        TRIGGERRIGHT = 5
    }
}
