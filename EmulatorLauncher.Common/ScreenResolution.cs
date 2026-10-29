using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EmulatorLauncher.Common
{
    public class ScreenResolution : IDisposable
    {
        public static void SetHighDpiAware(string processPath)
        {
            try
            {
                RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);
                if (regKeyc != null)
                {
                    var val = regKeyc.GetValue(processPath, "").ToString();

                    if (string.IsNullOrEmpty(val))
                        regKeyc.SetValue(processPath, "~ HIGHDPIAWARE");
                    else if (!val.Contains("HIGHDPIAWARE"))
                        regKeyc.SetValue(processPath, val + " HIGHDPIAWARE");

                    regKeyc.Close();
                }
            }
            catch { }
        }

        private string _deviceName;   // "\\.\DISPLAY2", null = ecran courant

        public static ScreenResolution CurrentResolution
        {
            get { return GetCurrentResolution(null); }
        }

        public static ScreenResolution GetCurrentResolution(string deviceName)
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref mode))
                return null;

            var ret = new ScreenResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel,
                mode.dmDisplayFrequency, (mode.dmDisplayFlags & 2) == 2);

            ret._deviceName = deviceName;
            return ret;
        }

        public static ScreenResolution FromSize(int width, int height)
        {
            return new ScreenResolution(width, height, 32, 60, false);
        }

        public static ScreenResolution FromScreen(Screen screen)
        {
            if (screen == null)
                return CurrentResolution;

            var ret = FromSize(screen.Bounds.Width, screen.Bounds.Height);
            ret._deviceName = screen.DeviceName;
            return ret;
        }

        /// <summary>Obsolete : l'index depend de l'ordre d'enumeration. Utiliser FromScreen.</summary>
        public static ScreenResolution FromScreenIndex(int index)
        {
            return FromScreen(Displays.FromIndex(index, MonitorOrder.EnumDisplayMonitors));
        }

        public static ScreenResolution Parse(string gameResolution)
        {
            if (string.IsNullOrEmpty(gameResolution))
                return null;

            var values = gameResolution.Split(new char[] { 'x' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 4)
                return null;

            int width;
            if (!int.TryParse(values[0], out width))
                return null;

            int height;
            if (!int.TryParse(values[1], out height))
                return null;

            int bitCount;
            if (!int.TryParse(values[2], out bitCount))
                return null;

            bool interlaced = false;
            if (values[3].EndsWith("i"))
            {
                interlaced = true;
                values[3] = values[3].Substring(0, values[3].Length - 1);
            }

            int frequency;
            if (!int.TryParse(values[3], out frequency))
                return null;

            return new ScreenResolution(width, height, bitCount, frequency, interlaced);
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int BitsPerPel { get; private set; }
        public int DisplayFrequency { get; private set; }
        public bool Interlaced { get; set; }

        /// <summary>
        /// Target Windows orientation of the display. Null = keep the current orientation.
        /// Width/Height are always expressed in the current orientation (as listed by the videomode feature),
        /// they are swapped by Apply() when switching between landscape and portrait.
        /// A change including the orientation is dynamic only (registry untouched) : a reboot restores the user setting if the launcher crashes.
        /// </summary>
        public ScreenOrientation? Orientation { get; set; }

        DEVMODE originalMode = new DEVMODE();
        ChangeDisplaySettingsFlags _applyFlags = ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY;
        bool changed = false;
        public bool HasChanged { get { return changed; } }

        private ScreenResolution(int width, int height, int bitCount, int frequency, bool interlaced)
        {
            changed = false;

            Width = width;
            Height = height;
            BitsPerPel = bitCount;
            DisplayFrequency = frequency;
            Interlaced = interlaced;
        }

        /// <summary>Ecran cible. A definir avant Apply().</summary>
        public void SetTargetScreen(Screen screen)
        {
            _deviceName = screen == null ? null : screen.DeviceName;
        }

        public string DeviceName { get { return _deviceName; } }

        public void Apply()
        {
            DEVMODE current = new DEVMODE();
            current.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(_deviceName, ENUM_CURRENT_SETTINGS, ref current))
            {
                SimpleLogger.Instance.Warning("[ScreenResolution] EnumDisplaySettings failed for " +
                    (_deviceName ?? "current display"));
                return;
            }

            ScreenOrientation orientation = Orientation.HasValue ? Orientation.Value : current.dmDisplayOrientation;

            // Width/Height are expressed in the current orientation : swap them when switching between landscape and portrait
            int width = Width;
            int height = Height;
            if (IsPortrait(current.dmDisplayOrientation) != IsPortrait(orientation))
            {
                width = Height;
                height = Width;
            }

            bool curInterlaced = (current.dmDisplayFlags & 2) == 2;

            if (current.dmPelsWidth == width && current.dmPelsHeight == height &&
                current.dmBitsPerPel == BitsPerPel && current.dmDisplayFrequency == DisplayFrequency &&
                curInterlaced == Interlaced && current.dmDisplayOrientation == orientation)
                return;

            originalMode = current;

            DEVMODE newMode = current;
            newMode.dmPelsWidth = width;
            newMode.dmPelsHeight = height;
            newMode.dmBitsPerPel = BitsPerPel;
            newMode.dmDisplayFrequency = DisplayFrequency;
            newMode.dmDisplayFlags = Interlaced ? 2 : 0;

            if (Orientation.HasValue)
            {
                newMode.dmDisplayOrientation = orientation;
                newMode.dmFields |= DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT;
            }

            // Orientation changes are dynamic only, plain resolution changes keep the previous behavior
            _applyFlags = Orientation.HasValue ? ChangeDisplaySettingsFlags.CDS_NONE : ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY;

            SimpleLogger.Instance.Info("[ScreenResolution] Setting " + (_deviceName ?? "current display") +
                " to " + width + "x" + height + "x" + BitsPerPel + " " + DisplayFrequency + "Hz" +
                (Interlaced ? " (Interlaced)" : "") +
                (Orientation.HasValue ? " orientation " + current.dmDisplayOrientation + " -> " + orientation : ""));

            var result = ChangeDisplaySettingsEx(_deviceName, ref newMode, IntPtr.Zero, _applyFlags, IntPtr.Zero);

            changed = result == DISP_CHANGE.Successful;

            if (!changed)
                SimpleLogger.Instance.Warning("[ScreenResolution] ChangeDisplaySettingsEx failed : " + result);
        }

        public void Dispose()
        {
            if (changed)
            {
                SimpleLogger.Instance.Info("[ScreenResolution] Restoring " + (_deviceName ?? "current display") +
                    " to " + originalMode.dmPelsWidth + "x" + originalMode.dmPelsHeight + "x" +
                    originalMode.dmBitsPerPel + " " + originalMode.dmDisplayFrequency + "Hz orientation " +
                    originalMode.dmDisplayOrientation);

                ChangeDisplaySettingsEx(_deviceName, ref originalMode, IntPtr.Zero, _applyFlags, IntPtr.Zero);
            }

            changed = false;
        }

        /// <summary>Current Windows orientation of a display (null = primary display).</summary>
        public static ScreenOrientation GetOrientation(string deviceName)
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref mode))
                return ScreenOrientation.Angle0;

            return mode.dmDisplayOrientation;
        }

        public static bool IsPortrait(ScreenOrientation orientation)
        {
            return orientation == ScreenOrientation.Angle90 || orientation == ScreenOrientation.Angle270;
        }

        /// <summary>
        /// Live bounds of a display in physical pixels (virtual desktop coordinates), read from the display driver.
        /// Unlike Screen.Bounds, not cached by WinForms (valid right after a mode change) and not affected by DPI virtualization.
        /// Returns null if the driver does not report the position : callers should fall back to Screen.Bounds.
        /// </summary>
        public static Rectangle? GetDisplayBounds(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return null;

            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref mode))
                return null;

            if ((mode.dmFields & DM_POSITION) == 0 || mode.dmPelsWidth <= 0 || mode.dmPelsHeight <= 0)
                return null;

            return new Rectangle(mode.dmPositionX, mode.dmPositionY, mode.dmPelsWidth, mode.dmPelsHeight);
        }

        /// <summary>
        /// Positions of all displays, captured before a display mode change and restored after it.
        /// Changing the size or orientation of one display can make Windows move the others : Restore() puts them back.
        /// </summary>
        public class LayoutSnapshot
        {
            private readonly Dictionary<string, Point> _positions = new Dictionary<string, Point>();

            private LayoutSnapshot() { }

            public static LayoutSnapshot Capture()
            {
                var ret = new LayoutSnapshot();

                foreach (var screen in Screen.AllScreens)
                {
                    DEVMODE mode = new DEVMODE();
                    mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                    if (EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref mode) && (mode.dmFields & DM_POSITION) != 0)
                        ret._positions[screen.DeviceName] = new Point(mode.dmPositionX, mode.dmPositionY);
                }

                return ret;
            }

            /// <summary>Puts back every display that moved. Call it after the display modes have been restored.</summary>
            public void Restore()
            {
                bool pending = false;

                foreach (var item in _positions)
                {
                    DEVMODE mode = new DEVMODE();
                    mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

                    if (!EnumDisplaySettings(item.Key, ENUM_CURRENT_SETTINGS, ref mode) || (mode.dmFields & DM_POSITION) == 0)
                        continue;

                    if (mode.dmPositionX == item.Value.X && mode.dmPositionY == item.Value.Y)
                        continue;

                    SimpleLogger.Instance.Info("[ScreenResolution] Moving " + item.Key + " back from " +
                        mode.dmPositionX + "," + mode.dmPositionY + " to " + item.Value.X + "," + item.Value.Y);

                    mode.dmPositionX = item.Value.X;
                    mode.dmPositionY = item.Value.Y;
                    mode.dmFields = DM_POSITION;

                    // Stored only, applied all together below : no intermediate overlapping layout
                    var result = ChangeDisplaySettingsEx(item.Key, ref mode, IntPtr.Zero,
                        ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_NORESET, IntPtr.Zero);

                    if (result == DISP_CHANGE.Successful)
                        pending = true;
                    else
                        SimpleLogger.Instance.Warning("[ScreenResolution] Unable to move " + item.Key + " back : " + result);
                }

                if (!pending)
                    return;

                var applied = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, ChangeDisplaySettingsFlags.CDS_NONE, IntPtr.Zero);
                if (applied != DISP_CHANGE.Successful)
                    SimpleLogger.Instance.Warning("[ScreenResolution] Unable to apply the restored display layout : " + applied);
            }
        }

        const int DM_POSITION = 0x00000020;
        const int DM_DISPLAYORIENTATION = 0x00000080;
        const int DM_PELSWIDTH = 0x00080000;
        const int DM_PELSHEIGHT = 0x00100000;
        const int ENUM_CURRENT_SETTINGS = -1;
        const int ENUM_REGISTRY_SETTINGS = -2;
        const int DISP_CHANGE_SUCCESSFUL = 0;

        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        [DllImport("user32.dll")]
        static extern DISP_CHANGE ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, ChangeDisplaySettingsFlags dwflags, IntPtr lParam);

        // Null device and null mode : applies the settings stored with CDS_NORESET
        [DllImport("user32.dll")]
        static extern DISP_CHANGE ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, ChangeDisplaySettingsFlags dwflags, IntPtr lParam);

        enum DISP_CHANGE : int
        {
            Successful = 0,
            Restart = 1,
            Failed = -1,
            BadMode = -2,
            NotUpdated = -3,
            BadFlags = -4,
            BadParam = -5,
            BadDualView = -6
        }

        [Flags()]
        enum ChangeDisplaySettingsFlags : uint
        {
            CDS_NONE = 0,
            CDS_UPDATEREGISTRY = 0x00000001,
            CDS_TEST = 0x00000002,
            CDS_FULLSCREEN = 0x00000004,
            CDS_GLOBAL = 0x00000008,
            CDS_SET_PRIMARY = 0x00000010,
            CDS_VIDEOPARAMETERS = 0x00000020,
            CDS_ENABLE_UNSAFE_MODES = 0x00000100,
            CDS_DISABLE_UNSAFE_MODES = 0x00000200,
            CDS_RESET = 0x40000000,
            CDS_RESET_EX = 0x20000000,
            CDS_NORESET = 0x10000000
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DEVMODE
        {
            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

    }
}
