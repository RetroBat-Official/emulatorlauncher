using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace EmulatorLauncher.Common.Joysticks
{
    public class XInputDevice
    {
        private static XInputDevice[] _cache;

        public static XInputDevice[] GetDevices(bool skipIfDebuggerAttached = false)
        {
            if (_cache == null)
                _cache = GetDevicesInternal(skipIfDebuggerAttached);

            return _cache;
        }

        private static XInputDevice[] GetDevicesInternal(bool skipIfDebuggerAttached = true)
        {
        //    if (Debugger.IsAttached)
        //        return GetDevicesInternalWithDebuggerAttached(skipIfDebuggerAttached);

            var rawDevices = RawInputDevice.GetRawInputControllers()
                .Where(dev => dev.DevicePath != null && dev.DevicePath.Contains("IG_"))
                .OrderBy(dev => InputDevices.GetInputDeviceParent(dev.DevicePath))
                .ToList();

            var devices = new List<XInputDevice>();

            try
            {
              
              /*  using (_createFileWHook = new APIHook<CreateFileWDelegate>("api-ms-win-core-file-l1-1-0.dll", "CreateFileW", CustomCreateFileW))
                using (_deviceIoControlHook = new APIHook<DeviceIoControlDelegate>("api-ms-win-core-io-l1-1-0.dll", "DeviceIoControl", CustomDeviceIoControl))
                using (_coCreateInstanceHook = new APIHook<CoCreateInstanceDelegate>("api-ms-win-core-com-l1-1-0.dll", "CoCreateInstance", CustomCoCreateInstance)) // This one hangs if the debugger is attached
                using (_duplicateHandleHook = new APIHook<DuplicateHandleDelegate>("api-ms-win-core-handle-l1-1-0.dll", "DuplicateHandle", CustomDuplicateHandle)) // This one hangs if the debugger is attached                
               */ 
                {
                    // _deviceToName.Clear();

                    for (int i = 0; i < 4; i++)
                    {
                        // _hidPath = null;

                        var dev = new XInputDevice(i);
                        if (!dev.Connected)
                            continue;

                        var rawDevice = rawDevices.FirstOrDefault(rd => rd.ProductId == dev.ProductId && rd.VendorId == dev.VendorId);
                        if (rawDevice != null)
                        {
                            dev.Path = rawDevice.DevicePath;
                            dev.ParentPath = InputDevices.GetInputDeviceParent(dev.Path);
                            rawDevices.Remove(rawDevice);
                        }
                        /*
                        if (_hidPath != null)
                        {
                            var pidvid = VidPid.Parse(_hidPath);
                            if (pidvid != null && pidvid.ProductId == dev.ProductId && pidvid.VendorId == dev.VendorId && !devices.Any(d => d.Path == _hidPath))
                            {
                                dev.Path = _hidPath;
                                dev.ParentPath = InputDevices.GetInputDeviceParent(_hidPath);
                            }
                        }
                        */
                        devices.Add(dev);                        
                    }
                }

                SimpleLogger.Instance.Info("[XInput] GetDevicesInternal OK");

                FixMissingPaths(devices);
            }
            catch { }

            return devices.ToArray();
        }

        private static void FixMissingPaths(List<XInputDevice> devices)
        {
            var rawInput = RawInputDevice.GetRawInputControllers()
                .Where(r => r.DevicePath.Contains("IG_"))
                .OrderBy(r => InputDevices.GetInputDeviceParent(r.DevicePath))
                .ToArray();

            foreach (var dev in devices.Where(d => d.Path == null))
            {
                foreach (var ri in rawInput)
                {
                    if (ri.VendorId != dev.VendorId || ri.ProductId != dev.ProductId)
                        continue;

                    if (devices.Any(d => d.Path != null && InputDevices.GetInputDeviceParent(d.Path) == InputDevices.GetInputDeviceParent(ri.DevicePath)))
                        continue;

                    dev.Path = ri.DevicePath;
                    dev.ParentPath = InputDevices.GetInputDeviceParent(dev.Path);
                    break;
                }
            }
        }
        /*
        private static XInputDevice[] GetDevicesInternalWithDebuggerAttached(bool skipIfDebuggerAttached = true)
        {
            var devices = new List<XInputDevice>();

            var dddc = new int[] { 0, 1, 2, 3 }.Select(i => new NativeXInput(i)).Where(x => x.IsConnected).ToArray();
            if (dddc.Length == 0)
                return devices.ToArray();

            var indexToPath = new Dictionary<int, string>();

#if DEBUG
            if (dddc.Length > 1 && !skipIfDebuggerAttached)
            {
                try
                {
                    string path = System.IO.Path.GetTempFileName();
                    FileTools.TryDeleteFile(path);

                    var psi = new ProcessStartInfo();
                    psi.FileName = System.Reflection.Assembly.GetEntryAssembly().Location;
                    if (System.IO.Path.GetFileNameWithoutExtension(psi.FileName).ToLowerInvariant() == "emulatorlauncher")
                    {
                        psi.Arguments = "-queryxinputinfo \"" + path + "\"";
                        psi.UseShellExecute = false;

                        var process = Process.Start(psi);
                        process.WaitForExit();

                        if (System.IO.File.Exists(path))
                        {
                            string xml = System.IO.File.ReadAllText(path);
                            FileTools.TryDeleteFile(path);

                            foreach (var xin in xml.ExtractStrings("<xinput ", "/>"))
                            {
                                var index = xin.ExtractString("index=\"", "\"").ToInteger();
                                var hidpath = xin.ExtractString("path=\"", "\"");
                                indexToPath[index] = hidpath;
                            }
                        }
                    }
                }
                catch { }
            }
#endif

            for (int i = 0; i < 4; i++)
            {
                var dev = new XInputDevice(i);
                if (!dev.Connected)
                    continue;

                string path;
                if (indexToPath.TryGetValue(i, out path))
                {
                    dev.Path = path;
                    dev.ParentPath = InputDevices.GetInputDeviceParent(dev.Path);
                }

                devices.Add(dev);
            }

            FixMissingPaths(devices);
            return devices.ToArray();
        }
        */
        public XInputDevice(int index)
        {
            DeviceIndex = index;
            Name = "Input Pad #" + (DeviceIndex + 1).ToString();

            var xInput = new NativeXInput(index);
            Connected = xInput.IsConnected;
            if (!Connected)
                return;

            SubType = (XINPUT_DEVSUBTYPE)xInput.SubType;
            VendorId = xInput.VendorId;
            ProductId = xInput.ProductId;
        }

        public bool Connected { get; private set; }
        public string Path { get; private set; }
        public string ParentPath { get; set; }
        public XINPUT_DEVSUBTYPE SubType { get; private set; }
        public USB_VENDOR VendorId { get; set; }
        public USB_PRODUCT ProductId { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Path))
                return Name + " @ " + Path;

            return Name;
        }

        public int DeviceIndex { get; private set; }
        public string Name { get; private set; }

        #region Native XInput
        class NativeXInput
        {
            int _deviceIndex;

            public NativeXInput(int deviceIndex)
            {
                _deviceIndex = deviceIndex;

                var hModule = LoadLibrary("xinput1_4.dll");
                if (hModule == IntPtr.Zero)
                    hModule = LoadLibrary("xinput1_3.dll");
                if (hModule == IntPtr.Zero)
                    hModule = LoadLibrary("xinput9_1_0.dll");

                if (hModule == IntPtr.Zero)
                    return;

                XInputGetState xInputGetStatePtr = (XInputGetState)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "XInputGetState"), typeof(XInputGetState));
                if (xInputGetStatePtr != null)
                {
                    XInputState stateRef;
                    IsConnected = xInputGetStatePtr(_deviceIndex, out stateRef) == 0;
                }

                if (IsConnected)
                {
                    var procAddress = GetProcAddress(hModule, (IntPtr)108);
                    if (procAddress != IntPtr.Zero)
                    {
                        XInputGetCapabilitiesEx xInputGetCapabilitiesExPtr = (XInputGetCapabilitiesEx)Marshal.GetDelegateForFunctionPointer(procAddress, typeof(XInputGetCapabilitiesEx));
                        if (xInputGetCapabilitiesExPtr != null)
                        {
                            var temp = new XInputCapabilitiesEx();
                            if (xInputGetCapabilitiesExPtr(1, _deviceIndex, 0, out temp) == 0)
                                _caps = temp;
                        }
                    }

                    if (!_caps.HasValue)
                    {
                        XInputGetCapabilities xInputGetCapabilitiesPtr = (XInputGetCapabilities)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "XInputGetCapabilities"), typeof(XInputGetCapabilities));
                        if (xInputGetCapabilitiesPtr != null)
                        {
                            XInputCapabilities temp;
                            if (xInputGetCapabilitiesPtr(_deviceIndex, 0, out temp) == 0)
                            {
                                XInputCapabilitiesEx caps = new XInputCapabilitiesEx();
                                caps.vendorId = 0x045E;
                                caps.productId = 0x02A1;
                                caps.Capabilities = temp;

                                _caps = caps;
                            }
                        }
                    }
                }

                FreeLibrary(hModule);                
            }            

            private XInputCapabilitiesEx? _caps;

            public bool IsConnected { get; private set; }        
    
            public byte SubType { get { return _caps.HasValue ? _caps.Value.Capabilities.SubType : (byte) 0; } }
            public USB_VENDOR VendorId { get { return (USB_VENDOR) (_caps.HasValue ? _caps.Value.vendorId : (short)0); } }
            public USB_PRODUCT ProductId { get { return (USB_PRODUCT) (_caps.HasValue ? _caps.Value.productId : (short)0); } }

            #region Apis
            [DllImport("kernel32.dll")]
            static extern IntPtr LoadLibrary(string dllToLoad);

            [DllImport("kernel32.dll")]
            static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

            [DllImport("kernel32.dll")]
            static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr procedureName);

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

            struct XInputCapabilitiesEx
            {
                public XInputCapabilities Capabilities;
                public short vendorId;
                public short productId;
                public short revisionId;
                public short VersionNumber;
                public short unk1;
                public UInt32 unk2;
            };

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            delegate int XInputGetCapabilities(int dwUserIndex, int dwFlags, out XInputCapabilities capabilitiesRef);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            delegate int XInputGetCapabilitiesEx(int unk1, int dwUserIndex, int dwFlags, out XInputCapabilitiesEx capabilitiesRef);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            delegate int XInputGetState(int dwUserIndex, out XInputState capabilitiesRef);
            #endregion
        }

        #endregion

        #region Api Hooking
        
        /*
        private static APIHook<CreateFileWDelegate> _createFileWHook;
        private static APIHook<DeviceIoControlDelegate> _deviceIoControlHook;
        private static APIHook<DuplicateHandleDelegate> _duplicateHandleHook;
        private static APIHook<CoCreateInstanceDelegate> _coCreateInstanceHook;
        

        static Dictionary<IntPtr, string> _deviceToName = new Dictionary<IntPtr, string>();
        static string _hidPath;

        #region DuplicateHandle
        delegate bool DuplicateHandleDelegate(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle, 
            ref IntPtr lpTargetHandle,
            UInt32 dwDesiredAccess,
            bool bInheritHandle, 
            UInt32 dwOptions);

        [DebuggerStepThrough]
        static bool CustomDuplicateHandle(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle, 
            ref IntPtr lpTargetHandle,
            UInt32 dwDesiredAccess,
            bool bInheritHandle, 
            UInt32 dwOptions)
        {
            _duplicateHandleHook.Suspend();

            var ret = _duplicateHandleHook.NativeMethod(hSourceProcessHandle, hSourceHandle, hTargetProcessHandle, ref lpTargetHandle, dwDesiredAccess, bInheritHandle, dwOptions);

            _duplicateHandleHook.Resume();

            if (ret)
            {
                string value;
                if (_deviceToName.TryGetValue(hSourceHandle, out value))
                    _deviceToName[lpTargetHandle] = value;
            }

            return ret;
        }
        #endregion
        
        #region CoCreateInstance
        delegate int CoCreateInstanceDelegate(ref Guid ClassGuid, IntPtr pUnkOuter, int dwClsContext, ref Guid InterfaceGuid, ref IntPtr Result);

        private static Guid CLSID_DeviceBroker = new Guid("acc56a05-e277-4b1e-a43e-7a73e3cd6e6c");
        private static Guid IID_IDeviceBroker = new Guid("8604b268-34a6-4b1a-a59f-cdbd8379fd98");

        static int CustomCoCreateInstance(ref Guid ClassGuid, IntPtr pUnkOuter, int dwClsContext, ref Guid InterfaceGuid, ref IntPtr Result)
        {
            if (InterfaceGuid == IID_IDeviceBroker)
            {
                Result = IntPtr.Zero;
                return -2147467262; // E_NOINTERFACE
            }

            _coCreateInstanceHook.Suspend();            
            var ret = _coCreateInstanceHook.NativeMethod(ref ClassGuid, pUnkOuter, dwClsContext, ref InterfaceGuid, ref Result);
            _coCreateInstanceHook.Resume();
            return ret;
        }
        #endregion


        #region CreateFileW
        delegate IntPtr CreateFileWDelegate(
            IntPtr lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr SecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
            );

        static IntPtr CustomCreateFileW(
            IntPtr lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr SecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
            )
        {
            _createFileWHook.Suspend();

            var ret = _createFileWHook.NativeMethod(
                lpFileName,
                dwDesiredAccess,
                dwShareMode,
                SecurityAttributes,
                dwCreationDisposition,
                dwFlagsAndAttributes,
                hTemplateFile);

            _createFileWHook.Resume();

            if (ret != (IntPtr)(-1))
            {
                var fileName = APIHook.ReadWideStringPtr(lpFileName);
                if (!string.IsNullOrEmpty(fileName) && fileName.IndexOf("&IG_", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    _deviceToName[ret] = fileName;
            }

            return ret;
        }
        #endregion

        #region DeviceIoControl
        delegate bool DeviceIoControlDelegate(
            IntPtr hDevice,
            int IoControlCode,
            IntPtr InBuffer,
            int nInBufferSize,
            IntPtr OutBuffer,
            int nOutBufferSize,
            out int pBytesReturned,
            IntPtr Overlapped
            );

        static bool CustomDeviceIoControl(
            IntPtr hDevice,
            int IoControlCode,
            IntPtr InBuffer,
            int nInBufferSize,
            IntPtr OutBuffer,
            int nOutBufferSize,
            out int pBytesReturned,
            IntPtr Overlapped
            )
        {
            _deviceIoControlHook.Suspend();

            var ret = _deviceIoControlHook.NativeMethod(hDevice,
                IoControlCode,
                InBuffer,
                nInBufferSize,
                OutBuffer,
                nOutBufferSize,
                out pBytesReturned,
                Overlapped);

            _deviceIoControlHook.Resume();

            if (IoControlCode == -2147426292 && ret) // 0x8000e00c
            {
                string value;
                if (_deviceToName.TryGetValue(hDevice, out value))
                    _hidPath = value;
            }

            return ret;
        }
        #endregion
        */
        #endregion

        private static bool IsXInputDevice(string vendorId, string productId)
        {
            var ParseIds = new System.Text.RegularExpressions.Regex(@"([VP])ID_([\da-fA-F]{4})");
            // Used to grab the VID/PID components from the device ID string.                
            // Iterate over all PNP devices.                

            var devices = RawInputDevice.GetRawInputControllers();
            foreach (var device in devices)// HidGameDevice.GetGameDevices())
            {
                var DeviceId = device.DevicePath;
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
