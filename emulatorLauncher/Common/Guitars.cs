using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmulatorLauncher.Common
{
    /// <summary>
    /// Guitar class with method to get connected known guitars
    /// Will be used in combination with standard configuration per model (in yml file) to inject good configuration in emulators for each guitar
    /// Need to be completed with other guitars and needs to be tested by guitar owners
    /// </summary>
    public class Guitar
    {
        public Guitar() { }

        public static GuitarType GetGuitarType(string devicePath)
        {
            if (!string.IsNullOrEmpty(devicePath))
            {
                List<string> knownWheelsVidPid = knownWheelsTypes.Keys.ToList();

                if (knownWheelsVidPid.Any(d => devicePath.Contains(d)))
                    return knownWheelsTypes[knownWheelsVidPid.First(d => devicePath.Contains(d))];

                else
                    return GuitarType.Default;
            }

            else
                return GuitarType.Default;
        }

        public int DinputIndex { get; set; }
        public int SDLIndex { get; set; }
        public int XInputIndex { get; set; }
        public int ControllerIndex { get; set; }
        public string Name { get; set; }
        public string VendorID { get; set; }
        public string ProductID { get; set; }
        public string DevicePath { get; set; }
        public string Driver { get; set; }
        public GuitarType Type { get; set; }
        public Dictionary<string, string> ButtonMapping { get; set; }

        public int GetGuitarPriority()
        {
            if (Type == GuitarType.Default)
                return 100 + (int)Type;
            else
                return (int)Type;
        }

        private static readonly Dictionary<string, GuitarType> knownWheelsTypes = new Dictionary<string, GuitarType>
        {
            { "VID_1430&PID_4748", GuitarType.CRKD_Guitar_XP }
        };
    }

    public enum GuitarType
    {
        CRKD_Guitar_XP,
        Default = 100
    }
}
