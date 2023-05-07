using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace emulatorLauncher
{
    class ScreenResolution : IDisposable
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

        public static ScreenResolution CurrentResolution
        {
            get
            {
                DEVMODE mode = new DEVMODE();
                EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode);
                return new ScreenResolution(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmBitsPerPel, mode.dmDisplayFrequency);
            }
        }

        public static ScreenResolution FromScreenIndex(int index)
        {
            Screen screen = Screen.AllScreens.Skip(index).FirstOrDefault();
            if (screen == null)
                return CurrentResolution;

            return FromSize(screen.Bounds.Width, screen.Bounds.Height);
        }

        public static ScreenResolution FromSize(int width, int height)
        {
            return new ScreenResolution(width, height, 32, 60);
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

            int frequency;
            if (!int.TryParse(values[3], out frequency))
                return null;

            return new ScreenResolution(width, height, bitCount, frequency);
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int BitsPerPel { get; private set; }
        public int DisplayFrequency { get; private set; }

        DEVMODE originalMode = new DEVMODE();
        bool changed = false;

        private ScreenResolution(int width, int height, int bitCount, int frequency)
        {
            changed = false;
            
            Width = width;
            Height = height;
            BitsPerPel = bitCount;
            DisplayFrequency = frequency;
        }

        /// <summary>
        /// Changing the settings
        /// </summary>
        public void Apply()
        {
            var cur = CurrentResolution;
            if (cur.Width == Width && cur.Height == Height && cur.BitsPerPel == BitsPerPel && cur.DisplayFrequency == DisplayFrequency)
                return;

            originalMode.dmSize = (short)Marshal.SizeOf(originalMode);

            // Retrieving current settings
            // to edit them
            EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref originalMode);

            // Making a copy of the current settings
            // to allow reseting to the original mode
            DEVMODE newMode = originalMode;

            // Changing the settings
            newMode.dmPelsWidth = Width;
            newMode.dmPelsHeight = Height;
            newMode.dmBitsPerPel = BitsPerPel;
            newMode.dmDisplayFrequency = DisplayFrequency;

            SimpleLogger.Instance.Info("[ScreenResolution] Setting resolution to " + newMode.dmPelsWidth + "x" + newMode.dmPelsHeight + "x" + newMode.dmBitsPerPel + " " + newMode.dmDisplayFrequency + "Hz");
            
            changed = ChangeDisplaySettings(ref newMode, 0) == DISP_CHANGE_SUCCESSFUL;
        }

        public void Dispose()
        {
            if (changed)
            {
                SimpleLogger.Instance.Info("[ScreenResolution] Restoring resolution to " + originalMode.dmPelsWidth + "x" + originalMode.dmPelsHeight + "x" + originalMode.dmBitsPerPel + " " + originalMode.dmDisplayFrequency + "Hz");
                ChangeDisplaySettings(ref originalMode, 0);
            }

            changed = false;
        }

        const int ENUM_CURRENT_SETTINGS = -1;
        const int ENUM_REGISTRY_SETTINGS = -2;
        const int DISP_CHANGE_SUCCESSFUL = 0;

        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll")]
        static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

        [DllImport("user32.dll")]
        static extern DISP_CHANGE ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, ChangeDisplaySettingsFlags dwflags, IntPtr lParam);

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
