using System.Text;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher.Common.Wheels
{
    /// <summary>
    /// Wheel class with method to get connected known wheels
    /// Will be used in combination with standard configuration per model (in json file) to inject good configuration in emulators for each wheels
    /// Need to be completed with other wheels and needs to be tested by wheel owners
    /// </summary>
    public class RawWheel
    {
        #region Public Factory
        public static RawWheel[] GetRawWheels()
        {
            if (_cache == null)
            {
                SimpleLogger.Instance.Info("[WHEELS] Looping wheels.");
                _cache = GetRawWheelsInternal();
            }

            return _cache;
        }

        public static int GetUsableWheelsCount()
        {
            SimpleLogger.Instance.Info("[WHEELS] Searching for usable wheels.");
            var wheels = RawWheel.GetRawWheels();

            int wheelCount = wheels.Count();
            if (wheelCount > 0)
                return wheelCount;

            return 0;
        }
        #endregion

        #region Private methods
        private RawWheel() { }

        private static RawWheelType ExtractRawWheelType(string devicePath)
        {
            if (!string.IsNullOrEmpty(devicePath))
            {
                string[] logitechDrivingForceids = new string[] { "VID_046D&PID_C294", "VID_046D&PID_C298" };
                if (logitechDrivingForceids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.LogitechDrivingForce;                

                string[] logitechG25ids = new string[] { "VID_046D&PID_C299" };
                if (logitechG25ids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.LogitechG25;

                string[] logitechG27ids = new string[] { "VID_046D&PID_C29B" };
                if (logitechG27ids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.LogitechG27;

                string[] logitechG29ids = new string[] { "VID_046D&PID_C24F", "VID_046D&PID_C260" };
                if (logitechG29ids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.LogitechG29;

                string[] logitechMomoids = new string[] { "VID_046D&PID_CA03" };
                if (logitechMomoids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.LogitechMomo;

                string[] microsoftSideWinderids = new string[] { "VID_045E&PID_0034", "VID_045E&PID_001A" };
                if (microsoftSideWinderids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.MicrosoftSideWinder;

                string[] thrustmasterFerrariGTids = new string[] { "VID_044F&PID_B651", "VID_044F&PID_B654" };
                if (thrustmasterFerrariGTids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.ThrustmasterFerrariGT;

                string[] thrustmasterFFRacingids = new string[] { "VID_044F&PID_B605" };
                if (thrustmasterFFRacingids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.ThrustmasterForceFeedbackRacing;

                string[] thrustmasterRGTids = new string[] { "VID_044F&PID_B653" };
                if (thrustmasterRGTids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.ThrustmasterRallyGT;

                string[] thrustmasterT150ids = new string[] { "VID_044F&PID_B677" };
                if (thrustmasterT150ids.Any(d => devicePath.Contains(d)))
                    return RawWheelType.ThrustmasterT150;
            }

            return RawWheelType.Default;
        }

        private static RawWheel[] _cache;

        private static RawWheel[] GetRawWheelsInternal()
        {
            var wheelNames = new List<RawWheel>();
            // var wheelsDinput = new List<RawWheel>();
            SimpleLogger.Instance.Info("[WHEELS] Listing all wheels.");

            int index = 0;
            foreach (var device in RawInputDevice.GetRawInputControllers())    // Restrict to joysticks and gamepads
            {
                SimpleLogger.Instance.Info("[WHEELS] Searching wheel with " + device.DevicePath + "," + device.Type + "," + device.Name);
                wheelNames.Add(new RawWheel() { Name = device.Name, VendorID = device.VendorId.ToString().ToLowerInvariant(), ProductID = device.ProductId.ToString().ToLowerInvariant(), DevicePath = device.DevicePath.ToLowerInvariant(), Index = index, Type = ExtractRawWheelType(device.DevicePath) });
                
                index++;
            }

            /*  To test for later, getting index should be less complicated by interrogating dinput controllers instead of RawInput devices
            foreach (var device in DirectInputInfo.Controllers)
            {
                SimpleLogger.Instance.Info("[WHEELS] Searching wheel with " + device.DevicePath + ","  + device.Name);
                wheelsDinput.Add(new RawWheel() { Name = device.Name, VendorID = device.VendorId.ToString(), ProductID = device.ProductId.ToString(), DevicePath = device.DevicePath.ToLowerInvariant(), Index = device.DeviceIndex, Type = ExtractRawWheelType(device.DevicePath) });

                index++;
            }*/

            // Sorting (by index and put Gamepads at the end, do not remove them to enable usage of unknown wheels with standard configuration and fallback on gamepad)
            wheelNames.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));
            // wheelsDinput.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));

            return wheelNames.ToArray();
        }

        #endregion

        public int Index { get; set; }
        public string Name { get; set; }
        public string VendorID { get; set; }
        public string ProductID { get; set; }
        public string DevicePath { get; set; }
        public RawWheelType Type { get; private set; }

        private int GetWheelPriority()
        {
            if (Type == RawWheelType.Default)
                return 100 + Index;
            else
                return Index;
        }

        public override string ToString()
        {
            return Name + " [" + Type + "] [" + Index + "] [" + DevicePath + "]";
        }
    }

    public enum RawWheelType
    {
        LogitechDrivingForce,
        LogitechG25,
        LogitechG27,
        LogitechG29,
        LogitechMomo,
        MicrosoftSideWinder,
        ThrustmasterFerrariGT,
        ThrustmasterForceFeedbackRacing,
        ThrustmasterRallyGT,
        ThrustmasterT150,
        Default = 100
    }

    public class WheelMappingInfo
    {
        #region Factory
        public static Dictionary<string, WheelMappingInfo> Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = SimpleYml<WheelMappingInfo>
                        .Parse(Encoding.UTF8.GetString(Properties.Resources.wheels))
                        .ToDictionary(a => a.Wheeltype, a => a);
                }

                return _instance;
            }
        }

        private static Dictionary<string, WheelMappingInfo> _instance;

        public WheelMappingInfo()
        {
            WheelGuid = Inputsystems = Forcefeedback = Invertedaxis = Range = Throttle = Brake = Steer = Gearup = Geardown = Gear1 = Gear2 = Gear3 = Gear4 = Gear5 = Gear6 = Gear_reverse = "nul";
        }
        #endregion

        [YmlName]
        public string Wheeltype { get; set; }
        public string WheelGuid { get; set; }
        public string Inputsystems { get; set; }
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
    }
}
