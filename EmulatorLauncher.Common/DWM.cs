using System;
using System.Runtime.InteropServices;

namespace EmulatorLauncher.Common
{
    public static class DWM
    {
        [DllImport("dwmapi.dll")]
        public static extern int DwmIsCompositionEnabled(ref int en);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr, ref int pvAttribute, int attrSize);
    }

    public enum DWMWINDOWATTRIBUTE : int
    {
        DWMWA_ALLOW_NCPAINT = 4,
        DWMWA_CAPTION_BUTTON_BOUNDS = 5,
        DWMWA_FLIP3D_POLICY = 8,
        DWMWA_FORCE_ICONIC_REPRESENTATION = 7,
        DWMWA_LAST = 9,
        DWMWA_NCRENDERING_ENABLED = 1,
        DWMWA_NCRENDERING_POLICY = 2,
        DWMWA_NONCLIENT_RTL_LAYOUT = 6,
        DWMWA_TRANSITIONS_FORCEDISABLED = 3,
        DWMWA_WINDOW_CORNER_PREFERENCE = 33
    }

    public enum DWM_WINDOW_CORNER_PREFERENCE : int
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }
}
