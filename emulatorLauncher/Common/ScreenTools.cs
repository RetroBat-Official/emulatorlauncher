using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    /// <summary>
    /// Manipulation de fenetres et placement sur ecran.
    ///
    /// Aucune methode publique ne prend d'index : un index n'a pas de sens sans son ordre
    /// d'enumeration (SDL ou EnumDisplayMonitors). La conversion se fait en amont, via
    /// Program.TargetScreen ou Displays.FromIndex(), et on ne transporte que des Screen.
    /// </summary>
    internal class ScreenTools
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static bool MoveHandleToScreen(IntPtr handle, Screen targetScreen)
        {
            if (handle == IntPtr.Zero || targetScreen == null)
                return false;

            Screen currentScreen = Screen.FromHandle(handle);

            if (currentScreen.DeviceName.Equals(targetScreen.DeviceName))
                return true;

            SimpleLogger.Instance.Info($"[SCREENMOVER] Moving window from {currentScreen.DeviceName} to {targetScreen.DeviceName}");

            Rectangle b = targetScreen.Bounds;

            User32.SetWindowPos(handle, IntPtr.Zero, b.Left, b.Top, b.Width, b.Height, SWP.NOZORDER | SWP.SHOWWINDOW);

            return Screen.FromHandle(handle).DeviceName.Equals(targetScreen.DeviceName);
        }

        private const int WS_CAPTION = 0x00C00000;

        /// <summary>
        /// True when the emulator is fullscreen
        /// </summary>
        private static bool IsFullscreenOnItsScreen(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return false;

            int style = User32.GetWindowLong(hWnd, GWL.STYLE);
            if ((style & WS_CAPTION) == WS_CAPTION)
                return false;

            var rect = User32.GetWindowRect(hWnd);
            var bounds = Screen.FromHandle(hWnd).Bounds;

            return (rect.right - rect.left) >= bounds.Width && (rect.bottom - rect.top) >= bounds.Height;
        }

        /// <summary>
        /// Screen where the window is actually located, or the fallback when unknown.
        /// </summary>
        public static Screen GetScreen(IntPtr handle, Screen fallback)
        {
            if (handle == IntPtr.Zero)
                return fallback;

            return Screen.FromHandle(handle) ?? fallback;
        }

        public static IntPtr WaitForReadyWindow(Process process, Predicate<IntPtr> selector, bool waitFullscreen, int timeoutMs = 30000, int pollMs = 250)
        {
            if (process == null)
                return IntPtr.Zero;

            IntPtr handle = IntPtr.Zero;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (process.HasExited)
                    return IntPtr.Zero;

                handle = User32.FindHwnds(process.Id, selector, true).FirstOrDefault();

                if (handle != IntPtr.Zero && (!waitFullscreen || IsFullscreenOnItsScreen(handle)))
                {
                    SimpleLogger.Instance.Info($"[SCREENMOVER] Window ready after {sw.ElapsedMilliseconds} ms on {Screen.FromHandle(handle).DeviceName}");
                    return handle;
                }

                handle = IntPtr.Zero;
                Thread.Sleep(pollMs);
            }

            SimpleLogger.Instance.Warning($"[SCREENMOVER] No ready window found after {sw.ElapsedMilliseconds} ms.");
            return IntPtr.Zero;
        }

        public static bool HoldWindowOnScreen(Process process, IntPtr handle, Screen target,
            int holdMs = 3000, int pollMs = 250)
        {
            if (process == null || handle == IntPtr.Zero || target == null)
                return false;

            var hold = Stopwatch.StartNew();
            int corrections = 0;

            while (hold.ElapsedMilliseconds < holdMs)
            {
                if (process.HasExited)
                    return false;

                if (!Screen.FromHandle(handle).DeviceName.Equals(target.DeviceName))
                {
                    MoveHandleToScreen(handle, target);
                    corrections++;
                }

                Thread.Sleep(pollMs);
            }

            bool ok = Screen.FromHandle(handle).DeviceName.Equals(target.DeviceName);
            SimpleLogger.Instance.Info($"[SCREENMOVER] Placement {(ok ? "confirmed" : "FAILED")} on {Screen.FromHandle(handle).DeviceName} ({corrections} correction(s))");

            return ok;
        }

        public static void MoveWindow(Process process, Screen targetScreen = null, int maxRetries = 20, int retryDelayMs = 2000)
        {
            if (process == null)
                return;

            if (targetScreen == null)
                targetScreen = Program.TargetScreen;

            SimpleLogger.Instance.Info($"[SCREENMOVER] Starting process of moving {process.ProcessName} to {targetScreen.DeviceName}");

            try
            {
                Thread.Sleep(200);
                IntPtr handle = IntPtr.Zero;

                for (int i = 0; i < maxRetries; i++)
                {
                    if (process.HasExited)
                        return;

                    process.Refresh();
                    handle = process.MainWindowHandle;

                    if (handle != IntPtr.Zero)
                    {
                        break;
                    }

                    Thread.Sleep(retryDelayMs);
                }

                if (handle == IntPtr.Zero)
                {
                    SimpleLogger.Instance.Warning($"[SCREENMOVER] Could not find process '{process.ProcessName}' with a valid window after {maxRetries} retries. Giving up.");
                    return;
                }

                process.WaitForInputIdle(2000);
                Thread.Sleep(200);

                process.Refresh();
                handle = process.MainWindowHandle;

                if (handle == IntPtr.Zero)
                {
                    SimpleLogger.Instance.Warning("[SCREENMOVER] Handle lost after idle wait.");
                    return;
                }

                Screen currentScreen = Screen.FromHandle(handle);

                SimpleLogger.Instance.Info($"[SCREENMOVER] Window is currently on: {currentScreen.DeviceName} (Primary: {currentScreen.Primary})");
                SimpleLogger.Instance.Info($"[SCREENMOVER] Target screen is: {targetScreen.DeviceName} (Primary: {targetScreen.Primary})");

                if (!currentScreen.DeviceName.Equals(targetScreen.DeviceName))
                {
                    SimpleLogger.Instance.Info($"[SCREENMOVER] Window is on the wrong screen. Moving...");
                    IntPtr HWND_TOPMOST = new IntPtr(-1);

                    Rectangle monitorBounds = targetScreen.Bounds;
                    int x = monitorBounds.Left;
                    int y = monitorBounds.Top;
                    int width = monitorBounds.Width;
                    int height = monitorBounds.Height;

                    User32.SetWindowPos(handle, HWND_TOPMOST, x, y, width, height, SWP.SHOWWINDOW);
                }
                else
                {
                    SimpleLogger.Instance.Info($"[SCREENMOVER] Window is already on the correct screen. No action taken.");
                    //ApplyFullscreenStyle(handle);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error($"[SCREENMOVER] Exception occurred: {ex}");
            }
        }

        public static bool MoveWindow(Process process, Predicate<IntPtr> selector, Screen targetScreen, int maxRetries = 40, int retryDelayMs = 250)
        {
            if (process == null || targetScreen == null)
                return false;

            for (int i = 0; i < maxRetries; i++)
            {
                if (process.HasExited)
                    return false;

                IntPtr handle = User32.FindHwnds(process.Id, selector, true).FirstOrDefault();
                if (handle != IntPtr.Zero)
                {
                    MoveHandleToScreen(handle, targetScreen);
                    return true;
                }

                Thread.Sleep(retryDelayMs);
            }

            SimpleLogger.Instance.Warning("[SCREENMOVER] No matching window found within timeout.");
            return false;
        }

        public static void LogProcessWindows(Process process)
        {
            if (process == null)
                return;

            foreach (var h in User32.FindHwnds(process.Id, null, true))
            {
                var rect = User32.GetWindowRect(h);
                SimpleLogger.Instance.Info($"[SCREENMOVER] hwnd={h} class='{User32.GetClassName(h)}' " +
                    $"rect=({rect.left},{rect.top})-({rect.right},{rect.bottom}) " +
                    $"screen={Screen.FromHandle(h).DeviceName}");
            }
        }

        static void ApplyFullscreenStyle(IntPtr handle)
        {
            const int WS_POPUP = unchecked((int)0x80000000);
            const int WS_VISIBLE = 0x10000000;

            int style = User32.GetWindowLong(handle, GWL.STYLE);
            style |= WS_POPUP | WS_VISIBLE;
            User32.SetWindowLong(handle, GWL.STYLE, new IntPtr(style));
        }

        static void MoveWindowToScreen(IntPtr hWnd, Screen targetScreen)
        {
            int x = targetScreen.Bounds.Left;
            int y = targetScreen.Bounds.Top;
            int w = targetScreen.Bounds.Width;
            int h = targetScreen.Bounds.Height;

            SimpleLogger.Instance.Info($"[SCREENMOVER] Moving window to ({x}, {y}, {w}, {h})");

            User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, w, h, SWP.NOZORDER | SWP.SHOWWINDOW);
        }
    }
}