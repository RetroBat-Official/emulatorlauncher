using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace EmulatorLauncher.Common.Joysticks
{
    public class XInputDevice
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

        #region Native XInput
        class NativeXInput : IDisposable
        {
            IntPtr _hModule;
            XInputGetState _xInputGetState;
            XInputGetCapabilities _xInputGetCapabilities;
            int _deviceIndex;

            public NativeXInput(int deviceIndex)
            {
                _deviceIndex = deviceIndex;

                _hModule = LoadLibrary("xinput1_4.dll");
                if (_hModule == IntPtr.Zero)
                    _hModule = LoadLibrary("xinput1_3.dll");
                if (_hModule == IntPtr.Zero)
                    _hModule = LoadLibrary("xinput9_1_0.dll");

                if (_hModule != IntPtr.Zero)
                {
                    _xInputGetState = (XInputGetState)Marshal.GetDelegateForFunctionPointer(GetProcAddress(_hModule, "XInputGetState"), typeof(XInputGetState));
                    _xInputGetCapabilities = (XInputGetCapabilities)Marshal.GetDelegateForFunctionPointer(GetProcAddress(_hModule, "XInputGetCapabilities"), typeof(XInputGetCapabilities));
                }
            }

            public void Dispose()
            {
                if (_hModule != IntPtr.Zero)
                {
                    FreeLibrary(_hModule);
                    _hModule = IntPtr.Zero;
                }
            }

            public bool IsConnected
            {
                get
                {
                    if (_xInputGetState != null)
                    {
                        XInputState stateRef;
                        return _xInputGetState(_deviceIndex, out stateRef) == 0;
                    }

                    return false;
                }
            }

            public byte SubType
            {
                get
                {
                    if (_xInputGetCapabilities != null)
                    {
                        XInputCapabilities temp;
                        if (_xInputGetCapabilities(_deviceIndex, 0, out temp) == 0)
                            return temp.SubType;
                    }

                    return 0;
                }
            }

            #region Apis
            [DllImport("kernel32.dll")]
            static extern IntPtr LoadLibrary(string dllToLoad);

            [DllImport("kernel32.dll")]
            static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

            [DllImport("kernel32.dll")]
            static extern bool FreeLibrary(IntPtr hModule);

            [StructLayout(LayoutKind.Explicit)]
            struct XInputGamepad
            {
                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(0)]
                public short wButtons;

                [MarshalAs(UnmanagedType.I1)]
                [FieldOffset(2)]
                public byte bLeftTrigger;

                [MarshalAs(UnmanagedType.I1)]
                [FieldOffset(3)]
                public byte bRightTrigger;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(4)]
                public short sThumbLX;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(6)]
                public short sThumbLY;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(8)]
                public short sThumbRX;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(10)]
                public short sThumbRY;
            }

            [StructLayout(LayoutKind.Explicit)]
            struct XInputState
            {
                [FieldOffset(0)]
                public int PacketNumber;

                [FieldOffset(4)]
                public XInputGamepad Gamepad;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct XInputVibration
            {
                [MarshalAs(UnmanagedType.I2)]
                public ushort LeftMotorSpeed;

                [MarshalAs(UnmanagedType.I2)]
                public ushort RightMotorSpeed;
            }

            [StructLayout(LayoutKind.Explicit)]
            struct XInputCapabilities
            {
                [MarshalAs(UnmanagedType.I1)]
                [FieldOffset(0)]
                byte Type;

                [MarshalAs(UnmanagedType.I1)]
                [FieldOffset(1)]
                public byte SubType;

                [MarshalAs(UnmanagedType.I2)]
                [FieldOffset(2)]
                public short Flags;

                [FieldOffset(4)]
                public XInputGamepad Gamepad;

                [FieldOffset(16)]
                public XInputVibration Vibration;
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            delegate int XInputGetCapabilities(int dwUserIndex, int dwFlags, out XInputCapabilities capabilitiesRef);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            delegate int XInputGetState(int dwUserIndex, out XInputState capabilitiesRef);
            #endregion
        }

        #endregion

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
                        using (var xInput = new NativeXInput(DeviceIndex))
                            if (xInput.IsConnected)
                                _subType = (XINPUT_DEVSUBTYPE)xInput.SubType;
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

            foreach (var device in HidGameDevice.GetGameDevices())
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
