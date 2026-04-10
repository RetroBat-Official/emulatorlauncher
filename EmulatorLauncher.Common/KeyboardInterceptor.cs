using EmulatorLauncher.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EmulatorLauncher.Common
{
    public class KeyboardInterceptor : IDisposable
    {
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;
        private Process _targetProcess;
        private bool _closing;
        private KeyTrigger _trigger;
        private int _timeoutSeconds;
        public IntPtr TargetHwnd { get; set; }

        public KeyboardInterceptor(Process process, KeyTrigger trigger, int timeoutSeconds = 3)
        {
            _targetProcess = process;
            _trigger = trigger;
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            _timeoutSeconds = timeoutSeconds;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            const int WM_KEYDOWN = 0x0100;

            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == (int)_trigger.Key && !_closing && ModifiersPressed(_trigger.Modifiers))
                {
                    if (_trigger.Modifiers != KeyModifiers.None && _targetProcess != null)
                    {
                        SimpleLogger.Instance.Info($"[KEYBOARDHOOK] {_trigger.Modifiers} + {_trigger.Key} pressed for process {_targetProcess.ProcessName}, exiting process.");
                    }
                    else if (_trigger.Modifiers == KeyModifiers.None && _targetProcess != null)
                    {
                        SimpleLogger.Instance.Info($"[KEYBOARDHOOK] {_trigger.Key} pressed for process {_targetProcess.ProcessName}, exiting process.");
                    }

                    //if (GetForegroundWindow() != _targetProcess.MainWindowHandle)
                    //    return CallNextHookEx(_hookID, nCode, wParam, lParam);

                    _closing = true;

                    Task.Factory.StartNew(() => CloseEmulator(_targetProcess, _timeoutSeconds, TargetHwnd),TaskCreationOptions.LongRunning);

                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private bool ModifiersPressed(KeyModifiers required)
        {
            bool alt = (GetKeyState(VK_MENU) & 0x8000) != 0;
            bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            bool shift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;

            if (required.HasFlag(KeyModifiers.Alt) && !alt) return false;
            if (required.HasFlag(KeyModifiers.Ctrl) && !ctrl) return false;
            if (required.HasFlag(KeyModifiers.Shift) && !shift) return false;

            // Also make sure no extra modifiers are pressed (optional)
            if (!required.HasFlag(KeyModifiers.Alt) && alt) return false;
            if (!required.HasFlag(KeyModifiers.Ctrl) && ctrl) return false;
            if (!required.HasFlag(KeyModifiers.Shift) && shift) return false;

            return true;
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        static void CloseEmulator(Process process, int timeoutSeconds = 3, IntPtr hwndOverride = default)
        {
            if (process == null || process.HasExited)
                return;

            process.Refresh();
            IntPtr hwnd = hwndOverride != IntPtr.Zero? hwndOverride : process.MainWindowHandle;
            
            if (hwnd != IntPtr.Zero)
            {
                SimpleLogger.Instance.Info($"[KEYBOARDHOOK] Sending WM_CLOSE directly to window {hwnd}");
                SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                SimpleLogger.Instance.Info($"[KEYBOARDHOOK] No window handle found for {process.ProcessName}");
            }

            if (process.WaitForExit(timeoutSeconds * 1000))
                return;

            SimpleLogger.Instance.Info($"[KEYBOARDHOOK] Process did not exit after {timeoutSeconds} seconds, trying ALT+F4.");

            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, WM_SYSKEYDOWN, (IntPtr)Keys.F4, (IntPtr)0);

            if (process.WaitForExit(timeoutSeconds * 1000))
                return;

            try
            {
                SimpleLogger.Instance.Info($"[KEYBOARDHOOK] Process did not exit after {timeoutSeconds} seconds, killing.");
                process.Kill();
            }
            catch { }
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_CLOSE = 0x0010;
        private const int WM_SYSKEYDOWN = 0x0104;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int VK_MENU = 0x12; // ALT
        private const int VK_CONTROL = 0x11; // CTRL
        private const int VK_SHIFT = 0x10; // SHIFT

        [Flags]
        public enum KeyModifiers
        {
            None = 0,
            Alt = 1,
            Ctrl = 2,
            Shift = 4
        }

        public class KeyTrigger
        {
            public Keys Key { get; set; }
            public KeyModifiers Modifiers { get; set; }

            public KeyTrigger(Keys key, KeyModifiers modifiers = KeyModifiers.None)
            {
                Key = key;
                Modifiers = modifiers;
            }
        }
    }
}