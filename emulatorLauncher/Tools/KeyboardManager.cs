using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace emulatorLauncher.Tools
{
    public class KeyboardManager : IDisposable
    {
        class KeyboardAction
        {
            public KeyboardAction(KeyboardManager mgr, Action action, Func<uint, uint, bool> keyTest)
            {
                KeyboardManager = mgr;
                Execute = action;
                KeyboardTest = keyTest;
            }

            public Func<uint, uint, bool> KeyboardTest { get; set; }
            public Action Execute { get; set; }
            public KeyboardManager KeyboardManager { get; set; }
        }

        private static List<KeyboardAction> _actions = new List<KeyboardAction>();

        public KeyboardManager(Action a, Func<uint, uint, bool> testKey = null)
        {
            RegisterKeyboardAction(a, testKey);

            lock (_lock)
            {
                if (_keyboardHookCount == 0)
                    SetKeyboardHook();

                _keyboardHookCount++;
            }
        }

        public void UnregisterKeyboardAction()
        {
            lock (_lock)
            {
                 var toRemove = _actions.FirstOrDefault(a => a.KeyboardManager == this);
                 if (toRemove != null)
                    _actions.Remove(toRemove);
            }
        }

        public void RegisterKeyboardAction(Action action, uint vkCode, uint scanCode)
        {
            RegisterKeyboardAction(action, (vk, scan) => vk == vkCode && scan == scanCode);
        }

        public void RegisterKeyboardAction(Action action, Func<uint, uint, bool> keyTest = null)
        {
            if (keyTest == null)
                keyTest = (vkCode, scanCode) => vkCode == 27 && scanCode == 1;

            lock (_lock)
            {
                _actions.Add(new KeyboardAction(this, action, keyTest));
            }
        }
        
        public void Dispose()
        {
            UnregisterKeyboardAction();

            lock (_lock)
            {
                _keyboardHookCount--;

                if (_keyboardHookCount == 0)
                    RemoveKeyboardHook();
            }
        }

        public delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, uint threadId);

        // UnhookWindowsHookEx is used to uninstall the hook.
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(int idHook);

        private static object _lock = new object();

        private static int _keyboardHookCount;
        private static int _keyboardHookHandle;
        private static HookProc _keyboardHookProcedure;
        //private static IKeyboardHandler _handler;

        private const int WH_KEYBOARD = 2;
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_CALLWNDPROC = 4;     

        public static bool RemoveKeyboardHook()
        {
            if (_keyboardHookHandle != 0)
            {
                bool ret = UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = 0;
                return ret;
            }

            return true;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);
        
        [StructLayout(LayoutKind.Sequential)]
        public class KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public KBDLLHOOKSTRUCTFlags flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [Flags]
        public enum KBDLLHOOKSTRUCTFlags : uint
        {
            LLKHF_EXTENDED = 0x01,
            LLKHF_INJECTED = 0x10,
            LLKHF_ALTDOWN = 0x20,
            LLKHF_UP = 0x80,
        }

        private static int KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)0x0100 && _actions != null && _actions.Count > 0) // WM_KEYDOWN
            {
                KBDLLHOOKSTRUCT kbd = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                foreach (var action in _actions)
                    if (action.KeyboardTest(kbd.vkCode, kbd.scanCode))
                        action.Execute();                    
            }
            
            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }
                
        public static void SetKeyboardHook()
        {
            RemoveKeyboardHook();

            _keyboardHookProcedure = new HookProc(KeyboardHookProc);

            // WH_KEYBOARD_LL ne fonctionne pas avec Office 2013.
            // Il faut utiliser WH_KEYBOARD à la place. Aussi : WH_KEYBOARD ne marche pas si ThreadId est à Zero...

            //GetCurrentModuleHandle(), GetCurrentThreadId() 
            _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookProcedure, IntPtr.Zero, 0);
            if (_keyboardHookHandle == 0)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode);
            }
        }

    }
}
