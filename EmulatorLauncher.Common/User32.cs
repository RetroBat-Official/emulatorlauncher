using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.IO;

namespace EmulatorLauncher.Common
{
    public static class User32
    {
        public static IntPtr FindHwnd(int processId)
        {
            return FindHwnds(processId).FirstOrDefault();
        }

        public static IEnumerable<IntPtr> FindProcessWnds(int processId)
        {
            return FindProcessWnds(processId, GetDesktopWindow());
        }

        private static IEnumerable<IntPtr> FindProcessWnds(int processId, IntPtr hWndParent)
        {
            IntPtr hWnd = GetWindow(hWndParent, GW.CHILD);
            while (hWnd != IntPtr.Zero)
            {
                if (IsWindowVisible(hWnd))
                {
                    GetWindowThreadProcessId(hWnd, out uint wndProcessId);
                    if (wndProcessId == processId)
                        yield return hWnd;

                    foreach (var hWndChild in FindProcessWnds(processId, hWnd))
                        yield return hWndChild;                    
                }

                hWnd = GetWindow(hWnd, GW.HWNDNEXT);
            }
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

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetActiveWindow(IntPtr hWnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindow(IntPtr hWnd, GW cmd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);
        private const int ASFW_ANY = -1;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref IntPtr pvParam, uint fWinIni);
        const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LockSetForegroundWindow(uint uLockCode);
        private const uint LSFW_UNLOCK = 2;

        public static void ForceForegroundWindow(IntPtr hWnd)
        {
            if (!IsWindow(hWnd)) 
                return;

            IntPtr hWndParent = GetParent(hWnd);
            while (hWndParent != IntPtr.Zero && hWndParent != GetDesktopWindow())
            {                                  
                hWnd = hWndParent;
                hWndParent = GetParent(hWnd);
            }

            uint currentThread = Kernel32.GetCurrentThreadId();

            IntPtr activeWindow = GetForegroundWindow();
            uint activeProcess;
            uint activeThread = GetWindowThreadProcessId(activeWindow, out activeProcess);

            uint windowProcess;
            uint windowThread = GetWindowThreadProcessId(hWnd, out windowProcess);

            if (currentThread != activeThread)
                AttachThreadInput(currentThread, activeThread, true);
            if (windowThread != currentThread)
                AttachThreadInput(windowThread, currentThread, true);

            IntPtr oldTimeout = IntPtr.Zero, newTimeout = IntPtr.Zero;
            SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref newTimeout, 0);
            LockSetForegroundWindow(LSFW_UNLOCK);
            AllowSetForegroundWindow(ASFW_ANY);

            SetForegroundWindow(hWnd);
            ShowWindowAsync(hWnd, SW.SHOW);

            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);

            if (currentThread != activeThread)
                AttachThreadInput(currentThread, activeThread, false);
            if (windowThread != currentThread)
                AttachThreadInput(windowThread, currentThread, false);
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetProp(IntPtr hWnd, string lpString);

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

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowLong(IntPtr hWnd, GWL nIndex, IntPtr longValue);

        public static bool IsExclusiveFullScreen(IntPtr hWnd)
        {
            var style = (WS) GetWindowLong(hWnd, GWL.STYLE);
            var styleX = (WS_EX) GetWindowLong(hWnd, GWL.EXSTYLE);

            RECT window = GetWindowRect(hWnd);
            RECT screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

            if (window.bottom != screen.bottom || window.right != screen.right)
                return false;

            // !style.HasFlag(WS.POPUP) &&
            return
                !style.HasFlag(WS.BORDER) && 
                styleX.HasFlag(WS_EX.TOPMOST) && !styleX.HasFlag(WS_EX.CLIENTEDGE) && !styleX.HasFlag(WS_EX.WINDOWEDGE) && !styleX.HasFlag(WS_EX.STATICEDGE);
                
        }

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
            return (WS_EX)SetWindowLong(hWnd, GWL.EXSTYLE, (IntPtr)value);
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

        [DllImport("user32.dll")]
        public static extern bool LockWindowUpdate(IntPtr hWndLock);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetFocus();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndAfter, int X, int Y, int Width, int Height, [MarshalAs(UnmanagedType.U4)]SWP flags);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern int GetSystemMetrics(int nIndex);

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

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

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindowAsync(IntPtr windowHandle, SW cmdShow);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr SetCursor(HandleRef hcursor);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern short RegisterClass(WNDCLASS wc);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.U2)]
        public static extern short RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr DefWindowProcW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr DefaultWndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
    
    public static class Kernel32
    {
        public static bool IsRunningInConsole()
        {
            return AttachConsole(-1);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]        
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ntdll.dll", SetLastError = false)]
        public static extern IntPtr NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern IntPtr NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll")]
        public static extern bool DebugActiveProcess(uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool DebugActiveProcessStop(uint dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        public static extern int GetProcessId(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId = ATTACH_PARENT_PROCESS);
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

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
        
        public static bool IsX64(string dllorExePath)
        {
            if (!File.Exists(dllorExePath))
                return false;

            if (Path.GetExtension(dllorExePath).ToLowerInvariant() == ".exe")
                return GetBinaryType(dllorExePath) == BinaryType.SCS_64BIT_BINARY;

            try
            {
                using (FileStream stream = new FileStream(dllorExePath, FileMode.Open, FileAccess.Read))
                {
                    // Read the DOS header
                    byte[] dosHeader = new byte[64];
                    stream.Read(dosHeader, 0, 64);

                    // Get the offset to the PE signature
                    int peOffset = BitConverter.ToInt32(dosHeader, 60);

                    // Move to the PE signature
                    stream.Seek(peOffset, SeekOrigin.Begin);

                    // Read the PE signature (4 bytes)
                    byte[] peSignature = new byte[4];
                    stream.Read(peSignature, 0, 4);

                    // Check if it matches the "PE\0\0" signature
                    if (peSignature[0] == 0x50 && peSignature[1] == 0x45 && peSignature[2] == 0x00 && peSignature[3] == 0x00)
                    {
                        // Read the machine architecture (2 bytes)
                        byte[] machine = new byte[2];
                        stream.Read(machine, 0, 2);

                        ushort machineValue = BitConverter.ToUInt16(machine, 0);
                        if (machineValue == 0x8664)
                            return true;

                        if (machineValue == 0x014c)
                            return false; // X86

                        // Inconnu
                    }
                }
            }
            catch (Exception ex)
            {

            }

            return false;
        }
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

        public RECT(int left, int top, int right, int bottom)
        {
            this.top = top;
            this.left = left;
            this.right = right;
            this.bottom = bottom;
        }

        public static implicit operator RECT(Rectangle f)
        {
            return new RECT(f.Left, f.Top, f.Right, f.Bottom);
        }

        public void Inflate(int x, int y)
        {
            left -= x;
            right += x;
            top -= y;
            bottom += y;
        }
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

    public enum GW : int
    {
        HWNDFIRST = 0,
        HWNDLAST = 1,
        HWNDNEXT = 2,
        HWNDPREV = 3,
        OWNER = 4,
        CHILD = 5
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
    public enum ProcessAccessFlags : uint
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VirtualMemoryOperation = 0x00000008,
        VirtualMemoryRead = 0x00000010,
        VirtualMemoryWrite = 0x00000020,
        DuplicateHandle = 0x00000040,
        CreateProcess = 0x000000080,
        SetQuota = 0x00000100,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        QueryLimitedInformation = 0x00001000,
        Synchronize = 0x00100000
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

    [Flags]
    public enum RedrawWindowFlags : uint
    {
        Invalidate = 0x1,
        InternalPaint = 0x2,
        Erase = 0x4,
        Validate = 0x8,
        NoInternalPaint = 0x10,
        NoErase = 0x20,
        NoChildren = 0x40,
        AllChildren = 0x80,
        UpdateNow = 0x100,
        EraseNow = 0x200,
        Frame = 0x400,
        NoFrame = 0x800
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

    public class WaitCursor : IDisposable
    {
        public WaitCursor()
        {
            BeginWaitCursor();
        }

        public static void BeginWaitCursor()
        {
            User32.SetCursor(new HandleRef(System.Windows.Forms.Cursors.WaitCursor, System.Windows.Forms.Cursors.WaitCursor.Handle));
        }

        public static void EndWaitCursor()
        {
            User32.SetCursor(new HandleRef(System.Windows.Forms.Cursors.Arrow, System.Windows.Forms.Cursors.Arrow.Handle));
        }

        public void Dispose()
        {
            EndWaitCursor();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class WNDCLASS
    {
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance = IntPtr.Zero;
        public IntPtr hIcon = IntPtr.Zero;
        public IntPtr hCursor = IntPtr.Zero;
        public IntPtr hbrBackground = IntPtr.Zero;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct WNDCLASSEX
    {
        [MarshalAs(UnmanagedType.U4)]
        public int cbSize;
        [MarshalAs(UnmanagedType.U4)]
        public int style;
        public IntPtr lpfnWndProc; // not WndProc
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct AccentPolicy
    {
        public AccentState AccentState; // 3 = BlurBehind, 4 = Acrylic
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    public enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_INVALID_STATE = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    public enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

}
