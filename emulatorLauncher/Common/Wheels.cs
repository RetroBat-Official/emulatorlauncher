using System.Text;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

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
                string[] logitechDrivingForceids = new string[] { "VID_046D&PID_C294", "VID_046D&PID_C298" };
                if (logitechDrivingForceids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechDrivingForce;                

                string[] logitechG25ids = new string[] { "VID_046D&PID_C299" };
                if (logitechG25ids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG25;

                string[] logitechG27ids = new string[] { "VID_046D&PID_C29B" };
                if (logitechG27ids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG27;

                string[] logitechG29ids = new string[] { "VID_046D&PID_C24F" };
                if (logitechG29ids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG29;

                string[] logitechG29altids = new string[] { "VID_046D&PID_C260" };
                if (logitechG29ids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG29alt;

                string[] logitechG920ids = new string[] { "VID_046D&PID_C262" };
                if (logitechG920ids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG920;

                string[] logitechG923xboxids = new string[] { "VID_046D&PID_C26E" };
                if (logitechG923xboxids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG923X;

                string[] logitechG923PSids = new string[] { "VID_046D&PID_C266" };
                if (logitechG923PSids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechG923PS;

                string[] logitechMomoids = new string[] { "VID_046D&PID_CA03" };
                if (logitechMomoids.Any(d => devicePath.Contains(d)))
                    return WheelType.LogitechMomo;

                string[] microsoftSideWinderids = new string[] { "VID_045E&PID_0034", "VID_045E&PID_001A" };
                if (microsoftSideWinderids.Any(d => devicePath.Contains(d)))
                    return WheelType.MicrosoftSideWinder;

                string[] thrustmasterFerrariGTids = new string[] { "VID_044F&PID_B651", "VID_044F&PID_B654" };
                if (thrustmasterFerrariGTids.Any(d => devicePath.Contains(d)))
                    return WheelType.ThrustmasterFerrariGT;

                string[] thrustmasterFFRacingids = new string[] { "VID_044F&PID_B605" };
                if (thrustmasterFFRacingids.Any(d => devicePath.Contains(d)))
                    return WheelType.ThrustmasterForceFeedbackRacing;

                string[] thrustmasterRGTids = new string[] { "VID_044F&PID_B653" };
                if (thrustmasterRGTids.Any(d => devicePath.Contains(d)))
                    return WheelType.ThrustmasterRallyGT;

                string[] thrustmasterT150ids = new string[] { "VID_044F&PID_B677" };
                if (thrustmasterT150ids.Any(d => devicePath.Contains(d)))
                    return WheelType.ThrustmasterT150;
            }

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
    }

    public enum WheelType
    {
        LogitechG923X,
        LogitechG923PS,
        LogitechG29,
        LogitechG29alt,
        LogitechG920,
        LogitechG27,
        LogitechG25,
        LogitechDrivingForce,
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
            WheelGuid = Inputsystems = Pcsx2_Type = Forcefeedback = Invertedaxis = Range = Throttle = Brake = Steer = Gearup = Geardown = Gear1 = Gear2 = Gear3 = Gear4 = Gear5 = Gear6 = Gear_reverse = DpadUp = DpadDown = DpadLeft = DpadRight = "nul";
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
            WheelGuid  = Pcsx2_Type = Forcefeedback = Invertedaxis = Range = Throttle = Brake = Steer = Start = Select = Dpad = Gearup = Geardown = South = East = North = West = L1 = L2 = R1 = R2 = "nul";
        }
        #endregion

        [YmlName]
        public string Wheeltype { get; set; }
        public string WheelGuid { get; set; }
        public string Pcsx2_Type { get; set; }
        public string Forcefeedback { get; set; }
        public string Invertedaxis { get; set; }
        public string Range { get; set; }
        public string Throttle { get; set; }
        public string Brake { get; set; }
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
        public string R1 { get; set; }
        public string R2 { get; set; }
    }
}
