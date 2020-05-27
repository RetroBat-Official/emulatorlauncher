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
            _controllers = new List<SdlGameControllers>();

            SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);

            int numJoysticks = SDL.SDL_NumJoysticks();
            for (int i = 0; i < numJoysticks; i++)
            {
                if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
                {
                    var mappingString = SDL.SDL_GameControllerMappingForDeviceIndex(i);
                    if (mappingString == null)
                        continue;

                    string[] mapArray = mappingString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();
                    if (mapArray.Length == 0)
                        continue;

                    List<SdlControllerMapping> sdlMapping = new List<SdlControllerMapping>();

                    foreach (var tt in mapArray)
                    {
                        var map = tt.Split(new char[] { ':' });
                        if (map.Length == 2)
                        {
                            SdlControllerMapping sm = new SdlControllerMapping();

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

                            // 030000007d0400000840000000000000,Destroyer Tiltpad,+leftx:h0.2,+lefty:h0.4,-leftx:h0.8,-lefty:h0.1,a:b1,b:b2,dpdown:+a1,dpleft:-a0,dpright:+a0,dpup:-a1,leftshoulder:b4,rightshoulder:b5,x:b0,y:b3,platform:Windows,

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
                            else if (map[1].StartsWith("h")) // h0.4
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
                    }
                    
                    SdlGameControllers ctl = new SdlGameControllers();

                    ctl.Guid = SDL.SDL_JoystickGetDeviceGUID(i);
                    ctl.Name = SDL.SDL_GameControllerNameForIndex(i);
                    ctl.Mapping = sdlMapping;
                    
                    _controllers.Add(ctl);
                }
            }

            SDL.SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_Quit();
        }

        public static bool IsGameController(Guid guid)
        {
            return _controllers.Any(c => c.Guid == guid);
        }

        public static List<SdlControllerMapping> GetGameControllerMapping(Guid guid)
        {
            return _controllers.Where(c => c.Guid == guid).Select(c => c.Mapping).FirstOrDefault();
        }

        static List<SdlGameControllers> _controllers;

        public Guid Guid { get; set; }
        public string Name { get; set; }

        public List<SdlControllerMapping> Mapping { get; set; }

        public override string ToString()
        {
            return Guid + " " + Name;
        }
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
