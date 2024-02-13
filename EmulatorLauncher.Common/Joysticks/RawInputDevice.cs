using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher.Common
{
    public class RawInputDevice
    {
        public string Name { get; private set; }
        public string Manufacturer { get; set; }

        public RawInputDeviceType Type { get; private set; }
        public IntPtr Handle { get; private set; }

        public USB_VENDOR VendorId { get; set; }
        public USB_PRODUCT ProductId { get; set; }

        public string DevicePath
        {
            get { return _devicePath; }
            private set
            {
                if (_devicePath == value)
                    return;

                _devicePath = value;

                var vidpid = VidPid.Parse(_devicePath);
                if (vidpid != null)
                {
                    VendorId = vidpid.VendorId;
                    ProductId = vidpid.ProductId;
                }
            }
        }

        private string _devicePath;

        public static RawInputDevice[] GetRawInputDevices()
        {
            if (_cache == null)
                _cache = GetRawInputDeviceInternal();

            return _cache;
        }

        public static RawInputDevice[] GetRawInputControllers()
        {
            return GetRawInputDevices()
                .Where(dev => dev.Type == RawInputDeviceType.Joystick || dev.Type == RawInputDeviceType.GamePad)
                .ToArray();
        }

        private static RawInputDevice[] _cache;

        public static string GetRawInputDeviceName(IntPtr hDevice)
        {
            uint RIDI_DEVICENAME = 0x20000007;

            uint pcbSize = 0;
            string deviceName = "";
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);
            if (pcbSize <= 0)
                return null;

            IntPtr pData = Marshal.AllocHGlobal((int)pcbSize);
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pData, ref pcbSize);
            deviceName = Marshal.PtrToStringAnsi(pData);
            Marshal.FreeHGlobal(pData);

            return deviceName;
        }

        private static RawInputDevice[] GetRawInputDeviceInternal()
        {
            var mouseNames = new List<RawInputDevice>();

            uint RIDI_DEVICENAME = 0x20000007;
            uint RIDI_DEVICEINFO = 0x2000000b;

            uint deviceCount = 0;
            uint dwSize = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

            uint retValue = GetRawInputDeviceList(null, ref deviceCount, dwSize);
            if (retValue == 0)
            {
                // Now allocate an array of the specified number of entries
                RAWINPUTDEVICELIST[] deviceList = new RAWINPUTDEVICELIST[deviceCount];

                // Now make the call again, using the array
                retValue = GetRawInputDeviceList(deviceList, ref deviceCount, dwSize);

                int index = 0;
                foreach (var device in deviceList)
                {
                    RawInputDeviceType type = (RawInputDeviceType)(int)device.Type;

                    uint pcbSize = 0;
                    string deviceName = "";
                    GetRawInputDeviceInfo(device.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);
                    if (pcbSize <= 0)
                        continue;

                    IntPtr pData = Marshal.AllocHGlobal((int)pcbSize);
                    GetRawInputDeviceInfo(device.hDevice, RIDI_DEVICENAME, pData, ref pcbSize);
                    deviceName = Marshal.PtrToStringAnsi(pData);
                    Marshal.FreeHGlobal(pData);

                    if (device.Type == RIM_TYPE.HID) // RIM_TYPEHID
                    {
                        RID_DEVICE_INFO deviceInfo = new RID_DEVICE_INFO();
                        pcbSize = (uint)Marshal.SizeOf(deviceInfo);
                        uint val = GetRawInputDeviceInfo(device.hDevice, RIDI_DEVICEINFO, ref deviceInfo, ref pcbSize);
                        if (val != unchecked((uint)-1))
                        {
                            if (deviceInfo.hid.usUsagePage == 1 && deviceInfo.hid.usUsage == 5)
                                type = RawInputDeviceType.GamePad;
                            else if (deviceInfo.hid.usUsagePage == 1 && deviceInfo.hid.usUsage == 4)
                                type = RawInputDeviceType.Joystick;
                        }
                    }

                    if (string.IsNullOrEmpty(deviceName))
                        continue;

                    IntPtr hhid = CreateFile(deviceName, 0, 3, IntPtr.Zero, 3, 0x00000080, IntPtr.Zero);
                    if (hhid != new IntPtr(-1))
                    {
                        StringBuilder buf = new StringBuilder(255);
                        buf.Clear();

                        string manufacturer = null;
                        if (HidD_GetManufacturerString(hhid, buf, 255))
                            manufacturer = buf.ToString();

                        string product = null;
                        if (HidD_GetProductString(hhid, buf, 255))
                            product = buf.ToString();

                        if (!string.IsNullOrEmpty(product))
                            mouseNames.Add(new RawInputDevice() { Handle = device.hDevice, Name = product, Manufacturer = manufacturer, DevicePath = deviceName, Type = type });

                        CloseHandle(hhid);
                    }

                    index++;
                }
            }

            return mouseNames.ToArray();
        }

        public override string ToString()
        {
            return Name + " (" + Type.ToString() + ")";
        }

        #region Win32 Apis
        public enum RIM_TYPE : uint
        {
            MOUSE = 0,
            KEYBOARD = 1,
            HID = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RID_DEVICE_INFO_MOUSE
        {
            public uint dwId;
            public uint dwNumberOfButtons;
            public uint dwSampleRate;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fHasHorizontalWheel;
        }

        public struct RID_DEVICE_INFO_KEYBOARD
        {
            public uint dwType;
            public uint dwSubType;
            public uint dwKeyboardMode;
            public uint dwNumberOfFunctionKeys;
            public uint dwNumberOfIndicators;
            public uint dwNumberOfKeysTotal;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RID_DEVICE_INFO_HID
        {
            public uint dwVendorId;
            public uint dwProductId;
            public uint dwVersionNumber;
            public ushort usUsagePage;
            public ushort usUsage;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RID_DEVICE_INFO
        {
            [FieldOffset(0)]
            public uint cbSize;

            [FieldOffset(4)]
            public RIM_TYPE dwType;

            [FieldOffset(8)]
            public RID_DEVICE_INFO_MOUSE mouse;

            [FieldOffset(8)]
            public RID_DEVICE_INFO_KEYBOARD keyboard;

            [FieldOffset(8)]
            public RID_DEVICE_INFO_HID hid;
        }


        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public RIM_TYPE Type;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetRawInputDeviceList
        (
            [In, Out] RAWINPUTDEVICELIST[] RawInputDeviceList,
            ref uint NumDevices,
            uint Size /* = (uint)Marshal.SizeOf(typeof(RawInputDeviceList)) */
        );

        [DllImport("User32.dll")]
        static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("User32.dll")]
        static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, ref RID_DEVICE_INFO pData, ref uint pcbSize);

        [DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, uint BufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool HidD_GetManufacturerString(IntPtr HidDeviceObject, StringBuilder Buffer, uint BufferLength);


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CreateFile(String lpFileName, UInt32 dwDesiredAccess, UInt32 dwShareMode, IntPtr lpSecurityAttributes, UInt32 dwCreationDisposition, UInt32 dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);
        #endregion
    }

    public enum RawInputDeviceType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        Hid = 2,
        Joystick = 3,
        GamePad = 4
    }

}
