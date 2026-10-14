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