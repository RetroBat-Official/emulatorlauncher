using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher.Tools
{
    class XInputDevice
    {
        public XInputDevice(int index)
        {
            DeviceIndex = index;
            Name = "Input Pad #" + (DeviceIndex + 1).ToString();                        
        }

        public override string ToString()
        {
            return Name;
        }

        public int DeviceIndex { get; private set; }
        public string Name { get; private set; }

        XINPUT_DEVSUBTYPE? _subType;

        public XINPUT_DEVSUBTYPE SubType
        {
            get
            {
                if (!_subType.HasValue)
                {
                    _subType = XINPUT_DEVSUBTYPE.GAMEPAD;

                    try
                    {
                        // Get subtype from SharpDX.XInput
                        var ctrl = new SharpDX.XInput.Controller((SharpDX.XInput.UserIndex)DeviceIndex);
                        if (ctrl.IsConnected)
                        {
                            var caps = ctrl.GetCapabilities(SharpDX.XInput.DeviceQueryType.Any);
                            _subType = (XINPUT_DEVSUBTYPE)caps.SubType;
                        }
                    }
                    catch { }
                }

                return _subType.Value;
            }
        }

        private static bool IsXInputDevice(string vendorId, string productId)
        {
            var ParseIds = new System.Text.RegularExpressions.Regex(@"([VP])ID_([\da-fA-F]{4})");
            // Used to grab the VID/PID components from the device ID string.                
            // Iterate over all PNP devices.                

            foreach (var device in HdiGameDevice.GetGameDevices())
            {
                var DeviceId = device.DeviceId;
                if (DeviceId.Contains("IG_"))
                {
                    // Check the VID/PID components against the joystick's.                            
                    var Ids = ParseIds.Matches(DeviceId);
                    if (Ids.Count == 2)
                    {
                        ushort? VId = null, PId = null;
                        foreach (System.Text.RegularExpressions.Match M in Ids)
                        {
                            ushort Value = ushort.Parse(M.Groups[2].Value, System.Globalization.NumberStyles.HexNumber);
                            switch (M.Groups[1].Value)
                            {
                                case "V": VId = Value; break;
                                case "P": PId = Value; break;
                            }
                        }

                        //if (VId.HasValue && this.VendorId == VId && PId.HasValue && this.ProductId == PId) return true; 
                        if (VId.HasValue && vendorId == VId.Value.ToString("X4") && PId.HasValue && productId == PId.Value.ToString("X4"))
                            return true;
                    }
                }
            }

            return false;
        }

        private static string[] _xInputDriverIdentifiers = new string[] { "72" /* 'r' for RawInput */, "78" /* 'x' for XInput */ };

        public static bool IsXInputDevice(string deviceGuid)
        {
            if (deviceGuid == null || deviceGuid.Length < 32 || !deviceGuid.StartsWith("03") /* XInput is always 03 (USB) */ || !_xInputDriverIdentifiers.Contains(deviceGuid.Substring(28, 2)))
                return false;

            string vendorId = (deviceGuid.Substring(10, 2) + deviceGuid.Substring(8, 2)).ToUpper();
            string productId = (deviceGuid.Substring(18, 2) + deviceGuid.Substring(16, 2)).ToUpper();

            return IsXInputDevice(vendorId, productId);
        }
    }

    enum XINPUT_GAMEPAD
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        LEFTSHOULDER = 4,
        RIGHTSHOULDER = 5,

        BACK = 6,
        START = 7,

        LEFTSTICK = 8,
        RIGHTSTICK = 9,
        GUIDE = 10
    }

    enum XINPUT_HATS
    {
        DPAD_UP = 1,
        DPAD_RIGHT = 2,
        DPAD_DOWN = 4,
        DPAD_LEFT = 8
    }

    public enum XINPUTMAPPING
    {
        UNKNOWN = -1,

        A = 0,
        B = 1,
        Y = 2,
        X = 3,
        LEFTSHOULDER = 4,
        RIGHTSHOULDER = 5,

        BACK = 6,
        START = 7,

        LEFTSTICK = 8,
        RIGHTSTICK = 9,
        GUIDE = 10,

        DPAD_UP = 11,
        DPAD_RIGHT = 12,
        DPAD_DOWN = 14,
        DPAD_LEFT = 18,

        LEFTANALOG_UP = 21,
        LEFTANALOG_RIGHT = 22,
        LEFTANALOG_DOWN = 24,
        LEFTANALOG_LEFT = 28,

        RIGHTANALOG_UP = 31,
        RIGHTANALOG_RIGHT = 32,
        RIGHTANALOG_DOWN = 34,
        RIGHTANALOG_LEFT = 38,

        RIGHTTRIGGER = 51,
        LEFTTRIGGER = 52
    }

    [Flags]
    public enum XInputButtonFlags : ushort
    {
        NONE = 0,
        DPAD_UP = 1,
        DPAD_DOWN = 2,
        DPAD_LEFT = 4,
        DPAD_RIGHT = 8,
        START = 16,
        BACK = 32,
        LEFTTRIGGER = 64,
        RIGHTTRIGGER = 128,
        LEFTSHOULDER = 256,
        RIGHTSHOULDER = 512,
        A = 4096,
        B = 8192,
        X = 16384,
        Y = 32768
    }

    public enum XINPUT_DEVSUBTYPE
    {
        UNKNOWN = 0x00,
        GAMEPAD = 0x01,
        WHEEL = 0x02,
        ARCADE_STICK = 0x03,
        FLIGHT_STICK = 0x04,
        DANCE_PAD = 0x05,
        GUITAR = 0x06,
        GUITAR_ALTERNATE = 0x07,
        DRUM_KIT = 0x08,
        GUITAR_BASS = 0x0B,
        ARCADE_PAD = 0x13
    }
}
