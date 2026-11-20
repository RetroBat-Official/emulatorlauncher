using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmulatorLauncher.Common
{
    public enum MonitorOrder
    {
        /// <summary>SDL2, SDL3, Qt : primary first, then EnumDisplayMonitors order.</summary>
        SdlLike,

        /// <summary>WinForms Screen.AllScreens, RetroArch win32 : raw EnumDisplayMonitors order.</summary>
        EnumDisplayMonitors
    }

    /// <summary>
    /// Single point of truth for screen identification.
    ///
    /// The only stable identifier between EmulationStation (SDL2), EmulatorLauncher (WinForms),
    /// the emulators, and the Windows API is the adapter name ("\\.\DISPLAY1").
    /// An index is only valid at the boundaries (user menu, command line, emulator config file) : we immediately convert to Screen, and only transport
    /// the DeviceName.
    ///
    /// Both orders come from the same EnumDisplayMonitors enumeration:
    ///   SDL_windowsmodes.c / WIN_AddDisplays()  (SDL2 and SDL3) : 2 passes, primary first
    ///   qwindowsscreen.cpp / monitorData()      (Qt 5 and 6)    : prepend the primary
    ///   Screen.AllScreens                       (WinForms)     : one pass, no reordering
    ///   win32_common.c / win32_monitor_init()   (RetroArch)    : one pass, no reordering
    /// </summary>
    public static class Displays
    {
        public static Screen[] SdlOrder
        {
            get
            {
                var all = Screen.AllScreens;
                return all.Where(s => s.Primary).Concat(all.Where(s => !s.Primary)).ToArray();
            }
        }

        public static Screen[] Order(MonitorOrder order)
        {
            return order == MonitorOrder.SdlLike ? SdlOrder : Screen.AllScreens;
        }

        public static Screen FromIndex(int index, MonitorOrder order)
        {
            var list = Order(order);
            return (index < 0 || index >= list.Length) ? null : list[index];
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        /// <summary>
        /// Name of the monitor plugged on this adapter (EnumDisplayDevices DeviceString, e.g. "Generic PnP Monitor"), or null.
        /// </summary>
        public static string GetMonitorName(Screen screen)
        {
            if (screen == null)
                return null;

            try
            {
                var dd = new DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));

                if (EnumDisplayDevices(screen.DeviceName, 0, ref dd, 0))
                    return dd.DeviceString;
            }
            catch { }

            return null;
        }

        #region SDL display name
        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int DISPLAYCONFIG_MODE_INFO_SIZE = 64;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public uint refreshRateNumerator;
            public uint refreshRateDenominator;
            public uint scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public uint type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string monitorDevicePath;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, IntPtr modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        /// <summary>
        /// Display name as returned by SDL_GetDisplayName (SDL_windowsmodes.c / WIN_AddDisplay) :
        /// monitor friendly name (e.g. "DELL U2720Q"), else the EnumDisplayDevices name (e.g. "Generic PnP Monitor"). Null if not found.
        /// </summary>
        public static string GetSdlDisplayName(Screen screen)
        {
            if (screen == null)
                return null;

            string name = GetMonitorFriendlyName(screen.DeviceName);
            return string.IsNullOrEmpty(name) ? GetMonitorName(screen) : name;
        }

        private static string GetMonitorFriendlyName(string deviceName)
        {
            IntPtr modes = IntPtr.Zero;

            try
            {
                DISPLAYCONFIG_PATH_INFO[] paths;
                uint pathCount, modeCount;
                int rc;

                do
                {
                    if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) != 0)
                        return null;

                    if (modes != IntPtr.Zero)
                        Marshal.FreeHGlobal(modes);

                    paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                    modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * DISPLAYCONFIG_MODE_INFO_SIZE);

                    rc = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                }
                while (rc == ERROR_INSUFFICIENT_BUFFER);

                if (rc != 0)
                    return null;

                for (int i = 0; i < pathCount; i++)
                {
                    var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    source.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    source.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    source.header.adapterId = paths[i].targetInfo.adapterId;
                    source.header.id = paths[i].sourceInfo.id;

                    if (DisplayConfigGetDeviceInfo(ref source) != 0)
                        return null;

                    if (source.viewGdiDeviceName != deviceName)
                        continue;

                    var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    target.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    target.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    target.header.adapterId = paths[i].targetInfo.adapterId;
                    target.header.id = paths[i].targetInfo.id;

                    return DisplayConfigGetDeviceInfo(ref target) == 0 ? target.monitorFriendlyDeviceName : null;
                }
            }
            catch { }
            finally
            {
                if (modes != IntPtr.Zero)
                    Marshal.FreeHGlobal(modes);
            }

            return null;
        }
        #endregion

        public static int IndexOf(Screen screen, MonitorOrder order)
        {
            if (screen == null)
                return -1;

            return Array.FindIndex(Order(order), s => s.DeviceName == screen.DeviceName);
        }

        public static Screen FromDeviceName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return null;

            return Screen.AllScreens.FirstOrDefault(
                s => s.DeviceName.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// The x of "\\.\DISPLAYx" is NOT an index: Windows numbering can have
        /// gaps (DISPLAY1 + DISPLAY3 after disconnection). Only use this for
        /// emulators that expect this name (MAME, FBNeo, BigPEmu, Future Pinball).
        /// </summary>
        public static int ToDisplayNumber(Screen screen)
        {
            if (screen == null)
                return -1;

            string name = screen.DeviceName;
            int p = name.LastIndexOf("DISPLAY", StringComparison.InvariantCultureIgnoreCase);
            if (p < 0)
                return -1;

            int n;
            return int.TryParse(name.Substring(p + 7), out n) ? n : -1;
        }

        public static void LogAll(string tag = "[Displays]")
        {
            foreach (var s in Screen.AllScreens)
                SimpleLogger.Instance.Info(
                    tag + " device=" + s.DeviceName +
                    " primary=" + s.Primary +
                    " bounds=" + s.Bounds +
                    " bpp=" + s.BitsPerPixel +
                    " sdlLike=" + IndexOf(s, MonitorOrder.SdlLike) +
                    " enumOrder=" + IndexOf(s, MonitorOrder.EnumDisplayMonitors));
        }
    }
}