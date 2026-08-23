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

        public static bool MoveHandleToScreen(IntPtr handle, int targetMonitorIndex)
        {
            if (handle == IntPtr.Zero)
                return false;

            Screen[] screens = Screen.AllScreens;
            if (targetMonitorIndex < 0 || targetMonitorIndex >= screens.Length)
            {
                SimpleLogger.Instance.Warning($"[SCREENMOVER] Target monitor index {targetMonitorIndex} is out of range. Available screens: {screens.Length}");
                return false;
            }

            Screen targetScreen = screens[targetMonitorIndex];
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
        /// Index in Screen.AllScreens of the screen where the window is actually located.
        /// </summary>
        public static int GetScreenIndex(IntPtr handle, int fallbackIndex)
        {
            if (handle == IntPtr.Zero)
                return fallbackIndex;

            string deviceName = Screen.FromHandle(handle).DeviceName;
            int index = Array.FindIndex(Screen.AllScreens, s => s.DeviceName == deviceName);
            return index < 0 ? fallbackIndex : index;
        }

        /// <summary>
        /// Wait for the emulator to be ready to move the window
        /// </summary>
        public static IntPtr MoveWindowWhenReady(Process process, Predicate<IntPtr> selector, int targetMonitorIndex,
            bool waitFullscreen, int timeoutMs = 30000, int pollMs = 250, int holdMs = 3000)
        {
            if (process == null)
                return IntPtr.Zero;

            Screen[] screens = Screen.AllScreens;
            if (targetMonitorIndex < 0 || targetMonitorIndex >= screens.Length)
                return IntPtr.Zero;

            Screen target = screens[targetMonitorIndex];
            IntPtr handle = IntPtr.Zero;
            var sw = Stopwatch.StartNew();

            // Phase 1 : Wait for the window to exist and be stable.
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (process.HasExited)
                    return IntPtr.Zero;

                handle = User32.FindHwnds(process.Id, selector, true).FirstOrDefault();

                if (handle != IntPtr.Zero && (!waitFullscreen || IsFullscreenOnItsScreen(handle)))
                    break;

                handle = IntPtr.Zero;
                Thread.Sleep(pollMs);
            }

            if (handle == IntPtr.Zero)
            {
                SimpleLogger.Instance.Warning($"[SCREENMOVER] No ready window found after {sw.ElapsedMilliseconds} ms, leaving placement untouched.");
                return IntPtr.Zero;
            }

            SimpleLogger.Instance.Info($"[SCREENMOVER] Window ready after {sw.ElapsedMilliseconds} ms on {Screen.FromHandle(handle).DeviceName}, target is {target.DeviceName}");

            // Phase 2 + 3 : move, then re-apply if the emulator takes control again.
            var hold = Stopwatch.StartNew();
            int corrections = 0;

            while (hold.ElapsedMilliseconds < holdMs)
            {
                if (process.HasExited)
                    return handle;

                if (!Screen.FromHandle(handle).DeviceName.Equals(target.DeviceName))
                {
                    MoveHandleToScreen(handle, targetMonitorIndex);
                    corrections++;
                }

                Thread.Sleep(pollMs);
            }

            bool ok = Screen.FromHandle(handle).DeviceName.Equals(target.DeviceName);
            SimpleLogger.Instance.Info($"[SCREENMOVER] Placement {(ok ? "confirmed" : "FAILED")} on {Screen.FromHandle(handle).DeviceName} ({corrections} correction(s))");

            return handle;
        }

        public static void MoveWindow(Process process, int targetMonitorIndex = 0, int maxRetries = 20, int retryDelayMs = 2000)
        {
            SimpleLogger.Instance.Info($"[SCREENMOVER] Starting process of moving {process.ProcessName} to monitor {targetMonitorIndex}");

            try
            {
                if (process == null)
                    return;

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

                

                Screen[] screens = Screen.AllScreens;
                if (targetMonitorIndex < 0 || targetMonitorIndex >= screens.Length)
                {
                    SimpleLogger.Instance.Warning($"[SCREENMOVER] Target monitor index {targetMonitorIndex} is out of range. Available screens: {screens.Length}");
                    return;
                }

                Screen targetScreen = screens[targetMonitorIndex];
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

        public static bool MoveWindow(Process process, Predicate<IntPtr> selector, int targetMonitorIndex, int maxRetries = 40, int retryDelayMs = 250)
        {
            if (process == null)
                return false;

            for (int i = 0; i < maxRetries; i++)
            {
                if (process.HasExited)
                    return false;

                IntPtr handle = User32.FindHwnds(process.Id, selector, true).FirstOrDefault();
                if (handle != IntPtr.Zero)
                {
                    MoveHandleToScreen(handle, targetMonitorIndex);
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
