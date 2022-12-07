using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace emulatorLauncher.Tools
{
    class WinmmJoystick
    {
        private static WinmmJoystick[] _controllers;

        public static WinmmJoystick[] Controllers
        {
            get
            {
                if (_controllers == null)
                {
                    var ret = new List<WinmmJoystick>();

                    int directInputIndex = 0;

                    int count = joyGetNumDevs();
                    for (int i = 0; i < count; i++)
                    {
                        JOYCAPS js_caps = new JOYCAPS();
                        joyGetDevCaps(i, ref js_caps, Marshal.SizeOf(js_caps));

                        JOYINFOEX js_info = new JOYINFOEX();
                        js_info.dwSize = Marshal.SizeOf(js_info);
                        js_info.dwFlags = JOY_RETURNALL;
                        var device_status = joyGetPosEx(i, ref js_info);

                        if (device_status == JOYERR_NOERROR)
                        {
                            var joy = new WinmmJoystick()
                            {
                                Index = i,
                                VendorId = js_caps.wMid,
                                ProductId = js_caps.wPid,
                                Name = "Joystick #" + (i + 1).ToString(),
                                DirectInputIndex = -1
                            };

                            if (js_caps.szRegKey == "DINPUT.DLL")
                            {
                                joy.DirectInputIndex = directInputIndex;
                                directInputIndex++;
                            }

                            ret.Add(joy);
                        }
                    }

                    _controllers = ret.ToArray();
                }

                return _controllers;
            }
        }

        public int Index { get; private set; }
        public int VendorId { get; private set; }
        public int ProductId { get; private set; }
        public string Name { get; private set; }
        public int DirectInputIndex { get; private set; }

        public override string ToString()
        {
            return Name;
        }
        #region Api
        static Int32 JOYERR_NOERROR = 0;

        static Int32 JOY_RETURNX = 0x00000001;
        static Int32 JOY_RETURNY = 0x00000002;
        static Int32 JOY_RETURNZ = 0x00000004;
        static Int32 JOY_RETURNR = 0x00000008;
        static Int32 JOY_RETURNU = 0x00000010;
        static Int32 JOY_RETURNV = 0x00000020;
        static Int32 JOY_RETURNPOV = 0x00000040;
        static Int32 JOY_RETURNBUTTONS = 0x00000080;

        // static Int32 JOY_RETURNRAWDATA = 0x00000100;
        // static Int32 JOY_RETURNPOVCTS = 0x00000200;
        // static Int32 JOY_RETURNCENTERED = 0x00000400;
        // static Int32 JOY_USEDEADZONE = 0x00000800;

        static Int32 JOY_RETURNALL = (JOY_RETURNX | JOY_RETURNY | JOY_RETURNZ | JOY_RETURNR | JOY_RETURNU | JOY_RETURNV | JOY_RETURNPOV | JOY_RETURNBUTTONS);

        const String WINMM_NATIVE_LIBRARY = "winmm.dll";
        const CallingConvention CALLING_CONVENTION = CallingConvention.StdCall;

        [DllImport(WINMM_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), System.Security.SuppressUnmanagedCodeSecurity]
        static extern Int32 joyGetNumDevs();

        [DllImport(WINMM_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), System.Security.SuppressUnmanagedCodeSecurity]
        static extern Int32 joyGetDevCaps(Int32 uJoyID, ref JOYCAPS pjc, Int32 cbjc);

        [DllImport(WINMM_NATIVE_LIBRARY, CallingConvention = CALLING_CONVENTION), System.Security.SuppressUnmanagedCodeSecurity]
        static extern Int32 joyGetPosEx(Int32 uJoyID, ref JOYINFOEX pji);

        [StructLayout(LayoutKind.Sequential)]
        struct JOYCAPS
        {
            public ushort wMid;
            public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String szPname;
            public Int32 wXmin;
            public Int32 wXmax;
            public Int32 wYmin;
            public Int32 wYmax;
            public Int32 wZmin;
            public Int32 wZmax;
            public Int32 wNumButtons;
            public Int32 wPeriodMin;
            public Int32 wPeriodMax;
            public Int32 wRmin;
            public Int32 wRmax;
            public Int32 wUmin;
            public Int32 wUmax;
            public Int32 wVmin;
            public Int32 wVmax;
            public Int32 wCaps;
            public Int32 wMaxAxes;
            public Int32 wNumAxes;
            public Int32 wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public String szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public String szOEMVxD;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JOYINFOEX
        {
            public Int32 dwSize;
            public Int32 dwFlags;
            public Int32 dwXpos;
            public Int32 dwYpos;
            public Int32 dwZpos;
            public Int32 dwRpos;
            public Int32 dwUpos;
            public Int32 dwVpos;
            public Int32 dwButtons;
            public Int32 dwButtonNumber;
            public Int32 dwPOV;
            public Int32 dwReserved1;
            public Int32 dwReserved2;
        }
        #endregion
    }


}
