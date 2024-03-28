using System.Text;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    /// <summary>
    /// Wheel class with method to get connected known wheels
    /// Will be used in combination with standard configuration per model (in json file) to inject good configuration in emulators for each wheels
    /// Need to be completed with other wheels and needs to be tested by wheel owners
    /// </summary>
    public class Wheel
    {
        public Wheel() { }

        public static WheelType GetWheelType(string devicePath)
        {
            if (!string.IsNullOrEmpty(devicePath))
            {
                List<string> knownWheelsVidPid = knownWheelsTypes.Keys.ToList();
                
                if (knownWheelsVidPid.Any(d => devicePath.Contains(d)))
                    return knownWheelsTypes[knownWheelsVidPid.First(d => devicePath.Contains(d))];

                else
                    return WheelType.Default;
            }

            else
                return WheelType.Default;
        }

        public int DinputIndex { get; set; }
        public int SDLIndex { get; set; }
        public int XInputIndex { get; set; }
        public int ControllerIndex { get; set; }
        public string Name { get; set; }
        public string VendorID { get; set; }
        public string ProductID { get; set; }
        public string DevicePath { get; set; }
        public WheelType Type { get; set; }

        public int GetWheelPriority()
        {
            if (Type == WheelType.Default)
                return 100 + (int)Type;
            else
                return (int)Type;
        }

        private static readonly Dictionary<string, WheelType> knownWheelsTypes = new Dictionary<string, WheelType>
        {
            // Logitech
            { "VID_046D&PID_C24F", WheelType.LogitechG29 },             // G29
            { "VID_046D&PID_C260", WheelType.LogitechG29alt },          // G29
            { "VID_046D&PID_C262", WheelType.LogitechG920 },            // G920
            { "VID_046D&PID_C266", WheelType.LogitechG923PS },          // G923 PS
            { "VID_046D&PID_C26E", WheelType.LogitechG923X },           // G923 Xbox
            { "VID_046D&PID_C294", WheelType.LogitechDrivingForce },    // Driving force
            { "VID_046D&PID_C298", WheelType.LogitechDrivingForce },    // Driving Force
            { "VID_046D&PID_C299", WheelType.LogitechG25 },             // G25
            { "VID_046D&PID_C29A", WheelType.LogitechDrivingForceGT },  // Driving Force GT
            { "VID_046D&PID_C29B", WheelType.LogitechG27 },             // G27
            { "VID_046D&PID_CA03", WheelType.LogitechMomo },            // Momo
            // Microsoft
            { "VID_045E&PID_001A", WheelType.MicrosoftSideWinder },     // Sidewinder
            { "VID_045E&PID_0034", WheelType.MicrosoftSideWinder },     // Sidewinder
            // Thrustmaster
            { "VID_044F&PID_B605", WheelType.ThrustmasterForceFeedbackRacing },     // Force Feedback Racing
            { "VID_044F&PID_B651", WheelType.ThrustmasterFerrariGT },               // Ferrari GT
            { "VID_044F&PID_B653", WheelType.ThrustmasterRallyGT },                 // Rally GT
            { "VID_044F&PID_B654", WheelType.ThrustmasterFerrariGT },               // Ferrari GT
            { "VID_044F&PID_B677", WheelType.ThrustmasterT150 },                    // T150
            { "VID_044F&PID_B66E", WheelType.ThrustmasterT300RS }                   // T300RS
        };

        public static List<WheelType> shifterOtherDevice = new List<WheelType>
        {
            WheelType.ThrustmasterT300RS,
            WheelType.ThrustmasterT150,
            WheelType.ThrustmasterRallyGT,
            WheelType.ThrustmasterFerrariGT,
            WheelType.ThrustmasterForceFeedbackRacing,
        };
    }

    public enum WheelType
    {
        LogitechG923X,
        LogitechG923PS,
        LogitechG29,
        LogitechG29alt,
        LogitechG920,
        LogitechG27,
        LogitechDrivingForceGT,
        LogitechG25,
        LogitechDrivingForce,
        ThrustmasterT300RS,
        ThrustmasterT150,
        ThrustmasterRallyGT,
        LogitechMomo,
        MicrosoftSideWinder,
        ThrustmasterFerrariGT,
        ThrustmasterForceFeedbackRacing,
        Default = 100
    }

    public class WheelMappingInfo
    {
        #region Factory
        public static Dictionary<string, WheelMappingInfo> InstanceW
        {
            get
            {
                if (_instanceW == null)
                {
                    _instanceW = SimpleYml<WheelMappingInfo>
                        .Parse(Encoding.UTF8.GetString(Properties.Resources.wheelmapping))
                        .ToDictionary(a => a.Wheeltype, a => a);
                }

                return _instanceW;
            }
        }

        private static Dictionary<string, WheelMappingInfo> _instanceW;

        public WheelMappingInfo()
        {
            WheelGuid = Inputsystems = Pcsx2_Type = Forcefeedback = Invertedaxis = Range = Throttle = Brake = Clutch = Steer = Gearup = Geardown = Gear1 = Gear2 = Gear3 = Gear4 = Gear5 = Gear6 = Gear_reverse = DpadUp = DpadDown = DpadLeft = DpadRight = "nul";
        }
        #endregion

        [YmlName]
        public string Wheeltype { get; set; }
        public string WheelGuid { get; set; }
        public string Inputsystems { get; set; }
        public string Pcsx2_Type { get; set; }
        public string Forcefeedback { get; set; }
        public string Invertedaxis { get; set; }
        public string Range { get; set; }
        public string Throttle { get; set; }
        public string Brake { get; set; }
        public string Clutch { get; set; }
        public string Steer { get; set; }
        public string Gearup { get; set; }
        public string Geardown { get; set; }
        public string Gear1 { get; set; }
        public string Gear2 { get; set; }
        public string Gear3 { get; set; }
        public string Gear4 { get; set; }
        public string Gear5 { get; set; }
        public string Gear6 { get; set; }
        public string Gear_reverse { get; set; }
        public string DpadUp { get; set; }
        public string DpadDown { get; set; }
        public string DpadLeft { get; set; }
        public string DpadRight { get; set; }
    }

    public class WheelSDLMappingInfo
    {
        #region Factory
        public static Dictionary<string, WheelSDLMappingInfo> InstanceWSDL
        {
            get
            {
                if (_instanceWSDL == null)
                {
                    _instanceWSDL = SimpleYml<WheelSDLMappingInfo>
                        .Parse(Encoding.UTF8.GetString(Properties.Resources.wheelsdlmapping))
                        .ToDictionary(a => a.Wheeltype, a => a);
                }

                return _instanceWSDL;
            }
        }

        private static Dictionary<string, WheelSDLMappingInfo> _instanceWSDL;

        public WheelSDLMappingInfo()
        {
            WheelGuid  = SDLDeviceName = Pcsx2_Type = Forcefeedback = Invertedaxis = Range = Throttle = Brake = Clutch = Steer = Start = Select = Dpad = Gearup = Geardown = South = East = North = West = L1 = L2 = L3 = R1 = R2 = R3 = "nul";
        }
        #endregion

        [YmlName]
        public string Wheeltype { get; set; }
        public string WheelGuid { get; set; }
        public string SDLDeviceName { get; set; }
        public string Pcsx2_Type { get; set; }
        public string Forcefeedback { get; set; }
        public string Invertedaxis { get; set; }
        public string Range { get; set; }
        public string Throttle { get; set; }
        public string Brake { get; set; }
        public string Clutch { get; set; }
        public string Steer { get; set; }
        public string Start { get; set; }
        public string Select { get; set; }
        public string Dpad { get; set; }
        public string Gearup { get; set; }
        public string Geardown { get; set; }
        public string South { get; set; }
        public string East { get; set; }
        public string North { get; set; }
        public string West { get; set; }
        public string L1 { get; set; }
        public string L2 { get; set; }
        public string L3 { get; set; }
        public string R1 { get; set; }
        public string R2 { get; set; }
        public string R3 { get; set; }
    }
}
