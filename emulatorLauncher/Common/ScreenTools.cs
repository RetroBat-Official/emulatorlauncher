using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public static void MoveWindow(Process process, int targetMonitorIndex = 0, int maxRetries = 20, int retryDelayMs = 2000)
        {
            SimpleLogger.Instance.Info($"[SCREENMOVER] Starting process of moving {process.ProcessName} to monitor {targetMonitorIndex}");

            try
            {
                if (process == null)
                    return;

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

                    const int WS_POPUP = unchecked((int)0x80000000);
                    const int WS_VISIBLE = 0x10000000;

                    int style = User32.GetWindowLong(handle, GWL.STYLE);
                    style |= WS_POPUP | WS_VISIBLE;

                    User32.SetWindowLong(handle, GWL.STYLE, new IntPtr(style));

                    User32.SetWindowPosBool(handle, IntPtr.Zero,
                    targetScreen.Bounds.Left,
                    targetScreen.Bounds.Top,
                    targetScreen.Bounds.Width,
                    targetScreen.Bounds.Height,
                    SWP.NOZORDER | SWP.SHOWWINDOW);
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

            bool result = User32.SetWindowPosBool(hWnd, IntPtr.Zero, x, y, w, h, SWP.NOZORDER | SWP.SHOWWINDOW);

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                SimpleLogger.Instance.Error($"[SCREENMOVER] Failed to move window. Error code: {error}");
            }
            else
            {
                SimpleLogger.Instance.Info("[SCREENMOVER] Window moved successfully.");
            }
        }
    }
}
