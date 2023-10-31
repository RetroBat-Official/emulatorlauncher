using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection.Emit;

namespace EmulatorLauncher.Common
{
    public class APIHook<TDelegate> : IDisposable where TDelegate : class
    {
        public APIHook(string dll, string procedure, TDelegate callback)
        {
            _oldEntry = null;
            _newEntry = null;

            IntPtr hModule = APIHook.GetModuleHandle(dll);
            if (hModule == IntPtr.Zero)
            {
                try { hModule = APIHook.LoadLibrary(dll); }
                catch { }

                if (hModule == IntPtr.Zero)
                    return;
            }

            IntPtr lpAddress = Marshal.GetFunctionPointerForDelegate(callback as Delegate);
            if (lpAddress == IntPtr.Zero)
                return;

            _address = APIHook.GetProcAddress(hModule, procedure);
            if (_address == IntPtr.Zero)
                return;

            if (IntPtr.Size == 8)
            {
                byte[] bytes = APIHook.AddBytes(new byte[2] { 0x48, 0xB8 }, BitConverter.GetBytes((long)lpAddress));
                _newEntry = APIHook.AddBytes(bytes, new byte[2] { 0xFF, 0xE0 });
            }
            else
                _newEntry = APIHook.AddBytes(new byte[1] { 0xE9 }, BitConverter.GetBytes((Int32)((Int32)lpAddress - (Int32)_address - 5)));

            int entrySize = _newEntry.Length;

            if (!APIHook.VirtualProtect(_address, entrySize, APIHook.PAGE_EXECUTE_READWRITE, ref lpflOldProtect))
            {
                _newEntry = null;
                return;
            }

            _callback = callback;
            _oldEntry = new byte[entrySize];

            Marshal.Copy(_address, _oldEntry, 0, entrySize);
            Marshal.Copy(_newEntry, 0, _address, entrySize);
        }

        public void Dispose()
        {
            Suspend();
            _address = IntPtr.Zero;
            _callback = null;
        }

        public TDelegate NativeMethod
        {
            get
            {
                return Marshal.GetDelegateForFunctionPointer(_address, typeof(TDelegate))  as TDelegate;
            }
        }

        public void Suspend()
        {
            if (_address != IntPtr.Zero && _oldEntry != null)
                Marshal.Copy(_oldEntry, 0, _address, _oldEntry.Length);
        }

        public void Resume()
        {
            if (_address != IntPtr.Zero && _newEntry != null)
                Marshal.Copy(_newEntry, 0, _address, _newEntry.Length);
        }

        private int lpflOldProtect = 0;

        private byte[] _oldEntry;
        private byte[] _newEntry;

        private IntPtr _address;
        private TDelegate _callback;
    }

    public static class APIHook
    {
        public static string ReadWideStringPtr(IntPtr ptr)
        {
            int i = 0;
            while (true)
            {
                var data = Marshal.ReadInt16(ptr, i);
                if (data == 0)
                    break;

                i += 2;
            }

            byte[] bufferIn = new byte[i];
            Marshal.Copy(ptr, bufferIn, 0, i);
            return System.Text.Encoding.Unicode.GetString(bufferIn);
        }


        internal const int PAGE_EXECUTE_READWRITE = 0x40;

        internal static byte[] AddBytes(byte[] a, byte[] b)
        {
            List<byte> retArray = new List<byte>();

            for (int i = 0; i < a.Length; i++)
                retArray.Add(a[i]);

            for (int i = 0; i < b.Length; i++)
                retArray.Add(b[i]);

            return retArray.ToArray();
        }  

        [DllImport("kernel32.dll")]
        internal static extern bool VirtualProtect(IntPtr lpAddress, int dwSize, int flNewProtect, ref int lpflOldProtect);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
