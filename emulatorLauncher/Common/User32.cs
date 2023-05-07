using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace emulatorLauncher
{
    public static class User32
    {
        public static IntPtr FindHwnd(int processId)
        {
            return FindHwnds(processId).FirstOrDefault();
        }

        public static IEnumerable<IntPtr> FindHwnds(int processId, Predicate<IntPtr> func = null, bool visibleOnly = true)
        {
            IntPtr hWnd = GetWindow(GetDesktopWindow(), GW.CHILD);
            while (hWnd != IntPtr.Zero)
            {
                if (!visibleOnly || IsWindowVisible(hWnd))
                {
                    uint wndProcessId;
                    GetWindowThreadProcessId(hWnd, out wndProcessId);
                    if (wndProcessId == processId)
                    {
                        if (func == null || func(hWnd))
                            yield return hWnd;
                    }
                }

                hWnd = GetWindow(hWnd, GW.HWNDNEXT);
            }
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr SetMenu(IntPtr hWnd, IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindow(IntPtr hWnd, GW cmd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetProp(IntPtr hWnd, string lpString);

        public enum GW : int
        {
            HWNDFIRST = 0,
            HWNDLAST = 1,
            HWNDNEXT = 2,
            HWNDPREV = 3,
            OWNER = 4,
            CHILD = 5
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowText(IntPtr hWnd)
        {
            int capacity = GetWindowTextLength(hWnd) * 2;
            StringBuilder lpString = new StringBuilder(capacity);
            GetWindowText(hWnd, lpString, lpString.Capacity);
            return lpString.ToString();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        public static string GetClassName(IntPtr hWnd)
        {
            StringBuilder wndClass = new StringBuilder(256);
            if (User32.GetClassName(hWnd, wndClass, 256) != 0)
                return wndClass.ToString();

            return string.Empty;
        }

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowLong(IntPtr hWnd, GWL nIndex);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int newLong);

        // GetWindowStyle
        public static WS SetWindowStyle(IntPtr hWnd, WS value)
        {
            return (WS)SetWindowLong(hWnd, (int)GWL.STYLE, (int)value);
        }

        public static WS GetWindowStyle(IntPtr hWnd)
        {
            return (WS)GetWindowLong(hWnd, GWL.STYLE);
        }

        // ExStyle
        public static WS_EX SetWindowStyleEx(IntPtr hWnd, WS_EX value)
        {
            return (WS_EX)SetWindowLong(hWnd, (int)GWL.EXSTYLE, (int)value);
        }

        public static WS_EX GetWindowStyleEx(IntPtr hWnd)
        {
            return (WS_EX)GetWindowLong(hWnd, GWL.EXSTYLE);
        }

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetWindowRect(IntPtr hWnd, [Out] out RECT rect);

        public static RECT GetWindowRect(IntPtr hWnd)
        {
            RECT rect;
            if (!GetWindowRect(hWnd, out rect))
                rect = new RECT();

            return rect;
        }

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndAfter, int X, int Y, int Width, int Height, [MarshalAs(UnmanagedType.U4)]SWP flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pprSrc,
            Int32 crKey, ref BLENDFUNCTION pblend, Int32 dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int ShowWindow(IntPtr hWnd, SW cmdShow);
    }

    public static class Kernel32
    {
        public static bool IsRunningInConsole()
        {
            return AttachConsole(-1);
        }

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId = ATTACH_PARENT_PROCESS);
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();

        public static BinaryType GetBinaryType(string lpAppName)
        {
            try
            {
                BinaryType ret;
                if (GetBinaryType(lpAppName, out ret))
                    return ret;
            }
            catch { }

            return (BinaryType) (-1);
        }

        [DllImport("kernel32.dll")]
        static extern bool GetBinaryType(string lpApplicationName, out BinaryType lpBinaryType);
    }

    public static class Gdi32
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public Int32 x;
        public Int32 y;

        public POINT(Int32 x, Int32 y)
        { this.x = x; this.y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public Int32 cx;
        public Int32 cy;

        public SIZE(Int32 cx, Int32 cy)
        { this.cx = cx; this.cy = cy; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ARGB
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Alpha;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    public enum GWL : int
    {
        WNDPROC = -4,
        HINSTANCE = -6,
        HWNDPARENT = -8,
        STYLE = -16,
        EXSTYLE = -20,
        USERDATA = -21,
        ID = -12
    }

    [Flags]
    public enum WS : uint
    {
        OVERLAPPED = 0x00000000,
        POPUP = 0x80000000,
        CHILD = 0x40000000,
        MINIMIZE = 0x20000000,
        VISIBLE = 0x10000000,
        DISABLED = 0x08000000,
        CLIPSIBLINGS = 0x04000000,
        CLIPCHILDREN = 0x02000000,
        MAXIMIZE = 0x01000000,
        CAPTION = 0x00C00000,
        BORDER = 0x00800000,
        DLGFRAME = 0x00400000,
        VSCROLL = 0x00200000,
        HSCROLL = 0x00100000,
        SYSMENU = 0x00080000,
        THICKFRAME = 0x00040000,
        GROUP = 0x00020000,
        TABSTOP = 0x00010000,
        MINIMIZEBOX = 0x00020000,
        MAXIMIZEBOX = 0x00010000
    }

    [Flags]
    public enum WS_EX : uint
    {
        DLGMODALFRAME = 0x00000001,
        NOPARENTNOTIFY = 0x00000004,
        TOPMOST = 0x00000008,
        ACCEPTFILES = 0x00000010,
        TRANSPARENT = 0x00000020,
        MDICHILD = 0x00000040,
        TOOLWINDOW = 0x00000080,
        WINDOWEDGE = 0x00000100,
        CLIENTEDGE = 0x00000200,
        CONTEXTHELP = 0x00000400,
        RIGHT = 0x00001000,
        LEFT = 0x00000000,
        RTLREADING = 0x00002000,
        LTRREADING = 0x00000000,
        LEFTSCROLLBAR = 0x00004000,
        RIGHTSCROLLBAR = 0x00000000,
        CONTROLPARENT = 0x00010000,
        STATICEDGE = 0x00020000,
        APPWINDOW = 0x00040000,
        OVERLAPPEDWINDOW = (WINDOWEDGE | CLIENTEDGE),
        PALETTEWINDOW = (WINDOWEDGE | TOOLWINDOW | TOPMOST),
        LAYERED = 0x00080000,
        NOINHERITLAYOUT = 0x00100000, // Disable inheritence of mirroring by children
        LAYOUTRTL = 0x00400000, // Right to left mirroring
        COMPOSITED = 0x02000000,
        NOACTIVATE = 0x08000000
    }

    [Flags]
    public enum SWP : int
    {
        NOSIZE = 0x0001,
        NOMOVE = 0x0002,
        NOZORDER = 0x0004,
        NOREDRAW = 0x0008,
        NOACTIVATE = 0x0010,
        FRAMECHANGED = 0x0020,  /* The frame changed: send WM_NCCALCSIZE */
        SHOWWINDOW = 0x0040,
        HIDEWINDOW = 0x0080,
        NOCOPYBITS = 0x0100,
        NOOWNERZORDER = 0x0200,  /* Don't do owner Z ordering */
        NOSENDCHANGING = 0x0400,  /* Don't send WM_WINDOWPOSCHANGING */
        DRAWFRAME = 0x0800,
        NOREPOSITION = 0x1000,
        DEFERERASE = 0x2000,
        ASYNCWINDOWPOS = 0x4000
    }

    public enum SW
    {
        HIDE = 0,
        SHOWNORMAL = 1,
        SHOWMINIMIZED = 2,
        SHOWMAXIMIZED = 3,
        SHOWNOACTIVATE = 4,
        SHOW = 5,
        MINIMIZE = 6,
        SHOWMINNOACTIVE = 7,
        SHOWNA = 8,
        RESTORE = 9,
        SHOWDEFAULT = 10,
        FORCEMINIMIZE = 11
    }

    public enum BinaryType : int
    {
        SCS_32BIT_BINARY = 0, // A 32-bit Windows-based application
        SCS_64BIT_BINARY = 6, // A 64-bit Windows-based application.
        SCS_DOS_BINARY = 1, // An MS-DOS – based application
        SCS_OS216_BINARY = 5, // A 16-bit OS/2-based application
        SCS_PIF_BINARY = 3, // A PIF file that executes an MS-DOS – based application
        SCS_POSIX_BINARY = 4, // A POSIX – based application
        SCS_WOW_BINARY = 2    // A 16-bit Windows-based application
    }
}
