using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;

namespace batocera_padsinfo
{
    class Program
    {
        static string ToEmulationStationGuidString(Guid guid)
        {
            string esGuidString = guid.ToString();

            // 030000005e040000e002000000007801

            string ret =
                esGuidString.Substring(6, 2) +
                esGuidString.Substring(4, 2) +
                esGuidString.Substring(2, 2) +
                esGuidString.Substring(0, 2) +
                esGuidString.Substring(10+1, 2) +
                esGuidString.Substring(8+1, 2) +
                esGuidString.Substring(14+2, 2) +
                esGuidString.Substring(12+2, 2) +
                esGuidString.Substring(16+3, 4) +
                esGuidString.Substring(20+4);

            return ret;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("<?xml version=\"1.0\"?>");
            Console.WriteLine("<pads>");

            SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);

            int numJoysticks = SDL.SDL_NumJoysticks();
            for (int i = 0; i < numJoysticks; i++)
            {
                IntPtr joy = SDL.SDL_JoystickOpen(i);
                var guid = ToEmulationStationGuidString(SDL.SDL_JoystickGetGUID(joy));
                var name = SDL.SDL_JoystickName(joy);

                string devicePath = null;
                string hidpath = SDL.SDL_JoystickPathForIndex(i);
                if (!string.IsNullOrEmpty(hidpath))
                    devicePath = InputDevices.GetInputDeviceParent(hidpath);

                string level = "100";
                SDL.SDL_JoystickPowerLevel pw = SDL.SDL_JoystickCurrentPowerLevel(joy);
                if (pw == SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_UNKNOWN)
                    continue;

                string status = "Discharging";

                switch (pw)
                {
                    case SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_EMPTY:
                        level = "0";
                        break;
                    case SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_LOW:
                        level = "25";
                        break;
                    case SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_MEDIUM:
                        level = "50";
                        break;
                    case SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_MAX:
                        level = "75";
                        break;
                    case SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_FULL:
                        level = "100";
                        break;
                    case SDL.SDL_JoystickPowerLevel.SDL_JOYSTICK_POWER_WIRED:
                        level = "0";
                        status = "Charging";
                        break;
                }


                if (!string.IsNullOrEmpty(devicePath))
                {
                    Console.WriteLine(string.Format("  <pad device=\"{0}\" name=\"{1}\" id=\"{2}\" battery=\"{3}\" status=\"{4}\" path=\"{5}\" />",
                    guid,
                    name,
                    i,
                    level,
                    status,
                    devicePath
                    ));
                }
                else
                {
                    Console.WriteLine(string.Format("  <pad device=\"{0}\" name=\"{1}\" id=\"{2}\" battery=\"{3}\" status=\"{4}\" />",
                        guid,
                        name,
                        i,
                        level,
                        status
                        ));
                }
                SDL.SDL_JoystickClose(joy);
            }

            SDL.SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_Quit();

            Console.WriteLine("</pads>");
        }
    }
}