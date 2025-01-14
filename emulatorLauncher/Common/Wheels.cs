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
            // Asetek
            { "VID_2433&PID_F300", WheelType.AsetekInvicta },
            { "VID_2433&PID_F301", WheelType.AsetekForte },
            { "VID_2433&PID_F303", WheelType.AsetekLaPrima },
            { "VID_2433&PID_F306", WheelType.AsetekTonyKannan },
            // Cammus
            { "VID_3416&PID_0301", WheelType.CammusC5 },
            { "VID_3416&PID_0302", WheelType.CammusC12 },
            // Fanatec
            { "VID_0EB7&PID_0E03", WheelType.FanatecCSLElite },
            { "VID_0EB7&PID_0005", WheelType.FanatecCSLElitePS4 },
            { "VID_0EB7&PID_0020", WheelType.FanatecDD },
            { "VID_0EB7&PID_0001", WheelType.FanatecClubSportWheelBaseV2 },
            { "VID_0EB7&PID_0004", WheelType.FanatecClubSportWheelBaseV25 },
            { "VID_0EB7&PID_0006", WheelType.FanatecPodiumDD1 },
            { "VID_0EB7&PID_0007", WheelType.FanatecPodiumDD2 },
            { "VID_0EB7&PID_0011", WheelType.FanatecCSRElite },
            // Logitech
            { "VID_046D&PID_CA03", WheelType.LogitechMomo },
            { "VID_046D&PID_C295", WheelType.LogitechMomo },
            { "VID_046D&PID_C298", WheelType.LogitechDrivingForce },
            { "VID_046D&PID_C299", WheelType.LogitechG25 },
            { "VID_046D&PID_C29A", WheelType.LogitechDrivingForceGT },
            { "VID_046D&PID_C29B", WheelType.LogitechG27 },
            { "VID_046D&PID_C24F", WheelType.LogitechG29 },
            { "VID_046D&PID_C260", WheelType.LogitechG29 },
            { "VID_046D&PID_C262", WheelType.LogitechG920 },
            { "VID_046D&PID_C26D", WheelType.LogitechG923X },
            { "VID_046D&PID_C266", WheelType.LogitechG923PS },
            { "VID_046D&PID_C267", WheelType.LogitechG923PS },
            { "VID_046D&PID_C26E", WheelType.LogitechG923X },
            { "VID_046D&PID_C294", WheelType.LogitechDrivingForce },
            { "VID_046D&PID_C272", WheelType.LogitechGPro },
            // Microsoft
            { "VID_045E&PID_001A", WheelType.MicrosoftSideWinder },
            { "VID_045E&PID_0034", WheelType.MicrosoftSideWinder },
            // Moza Racing
            { "VID_346E&PID_0005", WheelType.MozaR3 },
            { "VID_346E&PID_0004", WheelType.MozaR5 },
            { "VID_346E&PID_0002", WheelType.MozaR9 },
            { "VID_346E&PID_0006", WheelType.MozaR12 },
            { "VID_346E&PID_0000", WheelType.MozaR16 },
            // Simagic
            { "VID_0483&PID_0522", WheelType.SimagicM10 },
            // Simcube
            { "VID_16D0&PID_0D5A", WheelType.Simucube1 },
            { "VID_16D0&PID_0D61", WheelType.Simucube2Sport },
            { "VID_16D0&PID_0D60", WheelType.Simucube2Pro },
            { "VID_16D0&PID_0D5F", WheelType.Simucube2Ultimate },
            // SimXperience
            { "VID_1FC9&PID_804C", WheelType.SimXperienceAccuForcePro },
            // Thrustmaster
            { "VID_044F&PID_B56A", WheelType.ThrustmasterF430FF },
            { "VID_044F&PID_B605", WheelType.ThrustmasterNascarProFF2 },
            { "VID_044F&PID_B651", WheelType.ThrustmasterFGTRumbleForce },
            { "VID_044F&PID_B653", WheelType.ThrustmasterRGT },
            { "VID_044F&PID_B654", WheelType.ThrustmasterFGTFF },
            { "VID_044F&PID_B677", WheelType.ThrustmasterT150 },
            { "VID_044F&PID_B696", WheelType.ThrustmasterT248 },
            { "VID_044F&PID_B66E", WheelType.ThrustmasterT300RSPS3 },
            { "VID_044F&PID_B66F", WheelType.ThrustmasterT300RSPS3ADV },
            { "VID_044F&PID_B66D", WheelType.ThrustmasterT300RSPS4 },
            { "VID_044F&PID_B669", WheelType.ThrustmasterTX },
            // VRS
            { "VID_0483&PID_A355", WheelType.VRSDirectForcePro },
            // Test
            { "VID_045E&PID_02FF", WheelType.LogitechG29 }
        };

        public static List<WheelType> shifterOtherDevice = new List<WheelType>
        {
            WheelType.ThrustmasterF430FF,
            WheelType.ThrustmasterFGTFF,
            WheelType.ThrustmasterFGTRumbleForce,
            WheelType.ThrustmasterNascarProFF2,
            WheelType.ThrustmasterRGT,
            WheelType.ThrustmasterT150,
            WheelType.ThrustmasterT248,
            WheelType.ThrustmasterT300RSPS3,
            WheelType.ThrustmasterT300RSPS3ADV,
            WheelType.ThrustmasterT300RSPS4,
            WheelType.ThrustmasterTSXV,
            WheelType.ThrustmasterTX
        };
    }

    public enum WheelType
    {
        AsetekInvicta,
        AsetekForte,
        AsetekLaPrima,
        AsetekTonyKannan,
        CammusC5,
        CammusC12,
        FanatecCSLElite,
        FanatecCSLElitePS4,
        FanatecDD,
        FanatecDDPro,
        FanatecClubsportDD,
        FanatecClubSportWheelBaseV2,
        FanatecClubSportWheelBaseV25,
        FanatecPodiumDD1,
        FanatecPodiumDD2,
        FanatecCSRElite,
        LogitechMomo,
        LogitechDrivingForce,
        LogitechDrivingForceGT,
        LogitechG25,
        LogitechG27,
        LogitechG29,
        LogitechG920,
        LogitechG923X,
        LogitechG923PS,
        LogitechGPro,
        MozaR3,
        MozaR5,
        MozaR9,
        MozaR9V2,
        MozaR12,
        MozaR16,
        MozaR21,
        SimagicM10,
        SimagicAlphaMini,
        SimagicAlpha,
        SimagicAlphaUltimate,
        Simucube1,
        Simucube2Sport,
        Simucube2Pro,
        Simucube2Ultimate,
        SimXperienceAccuForcePro,
        ThrustmasterNascarProFF2,
        ThrustmasterFGTRumbleForce,
        ThrustmasterRGT,
        ThrustmasterFGTFF,
        ThrustmasterF430FF,
        ThrustmasterT150,
        ThrustmasterT248,
        ThrustmasterT300RSPS3,
        ThrustmasterT300RSPS3ADV,
        ThrustmasterT300RSPS4,
        ThrustmasterTX,
        ThrustmasterTSXV,
        MicrosoftSideWinder,
        VRSDirectForcePro,
        Default = 100
    }

    /*public class WheelMappingInfo
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
            WheelGuid = Inputsystems = Pcsx2_Type = Forcefeedback = Invertedaxis = PositiveTriggers = Range = Throttle = Brake = Clutch = Steer = Gearup = Geardown = Gear1 = Gear2 = Gear3 = Gear4 = Gear5 = Gear6 = Gear_reverse = DpadUp = DpadDown = DpadLeft = DpadRight = "nul";
        }
        #endregion

        [YmlName]
        public string Wheeltype { get; set; }
        public string WheelGuid { get; set; }
        public string Inputsystems { get; set; }
        public string Pcsx2_Type { get; set; }
        public string Forcefeedback { get; set; }
        public string Invertedaxis { get; set; }
        public string PositiveTriggers { get; set; }
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
            WheelGuid  = SDLDeviceName = Pcsx2_Type = Forcefeedback = Invertedaxis = PositiveTriggers = Range = Throttle = Brake = Clutch = Steer = Start = Select = Dpad = Gearup = Geardown = South = East = North = West = L1 = L2 = L3 = R1 = R2 = R3 = "nul";
        }
        #endregion

        [YmlName]
        public string Wheeltype { get; set; }
        public string WheelGuid { get; set; }
        public string SDLDeviceName { get; set; }
        public string Pcsx2_Type { get; set; }
        public string Forcefeedback { get; set; }
        public string Invertedaxis { get; set; }
        public string PositiveTriggers { get; set; }
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
    }*/
}
