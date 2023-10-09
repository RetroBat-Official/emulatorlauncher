using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace EmulatorLauncher.Common.Lightguns
{
    public class RawLightgun
    {
        #region Public Factory
        public static RawLightgun[] GetRawLightguns()
        {
            if (_cache == null)
                _cache = GetRawLightgunsInternal();

            return _cache;
        }

        public static int GetUsableLightGunCount()
        {
            var guns = RawLightgun.GetRawLightguns();

            int gunCount = guns.Count(g => g.Type != RawLighGunType.Mouse);
            if (gunCount > 0)
                return gunCount;

            int mice = guns.Count(g => g.Type == RawLighGunType.Mouse);
            return mice;
        }

        public static bool IsSindenLightGunConnected()
        {
            // Find Sinden process
            var px = Process.GetProcessesByName("Lightgun").FirstOrDefault();
            if (px == null)
                return false;

            // When Sinden Lightgun app is running & Start is pressed, there's an ActiveMovie window in the process, with the class name "FilterGraphWindow"
            if (!User32.FindHwnds(px.Id, hWnd => User32.GetClassName(hWnd) == "FilterGraphWindow", false).Any())
                return false;

            // Check if any Sinden Gun is connected
            return RawLightgun.GetRawLightguns().Any(gun => gun.Type == RawLighGunType.SindenLightgun);
        }
        #endregion

        #region Private methods
        private RawLightgun() { }

        private static RawLighGunType ExtractRawLighGunType(string devicePath)
        {
            if (!string.IsNullOrEmpty(devicePath))
            {
                string[] sindenDeviceIds = new string[] { "VID_16C0&PID_0F01", "VID_16C0&PID_0F02", "VID_16C0&PID_0F38", "VID_16C0&PID_0F39" };
                if (sindenDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.SindenLightgun;

                string[] gun4irDeviceIds = new string[] { "VID_2341&PID_8042" };
                if (gun4irDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Gun4Ir;

                string[] mayFlashWiimoteIds = new string[] { "VID_0079&PID_1802" };  // Mayflash Wiimote, using mode 1
                if (mayFlashWiimoteIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.MayFlashWiimote;
            }

            return RawLighGunType.Mouse;
        }

        private static RawLightgun[] _cache;

        private static RawLightgun[] GetRawLightgunsInternal()
        {
            var mouseNames = new List<RawLightgun>();

            uint RIDI_DEVICENAME = 0x20000007;
            uint deviceCount = 0;
            uint dwSize = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

            uint retValue = GetRawInputDeviceList(null, ref deviceCount, dwSize);
            if (retValue == 0)
            {
                // Now allocate an array of the specified number of entries
                RAWINPUTDEVICELIST[] deviceList = new RAWINPUTDEVICELIST[deviceCount];

                // Now make the call again, using the array
                retValue = GetRawInputDeviceList(deviceList, ref deviceCount, dwSize);

                var miceList = deviceList.Where(d => d.Type == RawInputDeviceType.MOUSE).ToList();

                int index = 0;
                foreach (var mouse in miceList)
                {
                    uint pcbSize = 0;
                    string deviceName = "";
                    GetRawInputDeviceInfo(mouse.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);
                    if (pcbSize <= 0)
                        continue;

                    IntPtr pData = Marshal.AllocHGlobal((int)pcbSize);
                    GetRawInputDeviceInfo(mouse.hDevice, RIDI_DEVICENAME, pData, ref pcbSize);
                    deviceName = Marshal.PtrToStringAnsi(pData);
                    Marshal.FreeHGlobal(pData);

                    if (string.IsNullOrEmpty(deviceName))
                        continue;

                    IntPtr hhid = CreateFile(deviceName, 0, 3, IntPtr.Zero, 3, 0x00000080, IntPtr.Zero);
                    if (hhid != new IntPtr(-1))
                    {
                        StringBuilder buf = new StringBuilder(255);
                        buf.Clear();

                        if (HidD_GetProductString(hhid, buf, 255))
                            mouseNames.Add(new RawLightgun() { Name = buf.ToString(), DevicePath = deviceName, Index = index, Type = ExtractRawLighGunType(deviceName) });

                        CloseHandle(hhid);
                    }

                    index++;
                }
            }

            // Sort by, (sinden) lightgun, thenby wiimotes, then by physical index
            mouseNames.Sort((x, y) => x.GetGunPriority().CompareTo(y.GetGunPriority()));

            return mouseNames.ToArray();
        }

        #endregion

        #region Win32 Apis
        enum RawInputDeviceType : uint
        {
            MOUSE = 0,
            KEYBOARD = 1,
            HID = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public RawInputDeviceType Type;
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

        [DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, uint BufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CreateFile(String lpFileName, UInt32 dwDesiredAccess, UInt32 dwShareMode, IntPtr lpSecurityAttributes, UInt32 dwCreationDisposition, UInt32 dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);
        #endregion

        public int Index { get; set; }
        public string Name { get; set; }
        public string DevicePath { get; set; }
        public RawLighGunType Type { get; private set; }

        private int GetGunPriority()
        {
            switch (Type)
            {
                case RawLighGunType.Gun4Ir:
                    return Index;

                case RawLighGunType.SindenLightgun:
                    return 10 + Index;

                case RawLighGunType.MayFlashWiimote:
                    return 100 + Index;

                default:
                    if (Name != null && Name.IndexOf("lightgun", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        return 200 + Index;

                    if (Name != null && Name.IndexOf("wiimote", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        return 300 + Index;

                    break;
            }

            return 10000 + Index;
        }

        public override string ToString()
        {
            return Name + " [" + Type + "] [" + Index + "] [" + DevicePath + "]";
        }
    }

    public enum RawLighGunType
    {
        SindenLightgun,
        MayFlashWiimote, // Using mode 1
        Gun4Ir,
        Mouse
    }
}
