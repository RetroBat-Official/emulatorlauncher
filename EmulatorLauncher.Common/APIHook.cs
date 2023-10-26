using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace EmulatorLauncher.Common
{
    public class APIHook : IDisposable
    {
        public APIHook(string dll, string procedure, Delegate callback)         
        {
            _oldEntry = null;
            _newEntry = null;

            IntPtr hModule = GetModuleHandle(dll);
            if (hModule == IntPtr.Zero)
            {
                try { hModule = LoadLibrary(dll); }
                catch { }

                if (hModule == IntPtr.Zero)
                    return;
            }

            IntPtr lpAddress = Marshal.GetFunctionPointerForDelegate(callback);
            if (lpAddress == IntPtr.Zero)
                return;

            _address = GetProcAddress(hModule, procedure);
            if (_address == IntPtr.Zero)
                return;

            if (IntPtr.Size == 8)
            {
                // x64 :
                // BYTE byMovCode = 0x48;
                // BYTE byAddrType = 0xB8;
                // unsigned __int64 ui64Address;
                // BYTE byJmpCode = 0xFF;
                // BYTE byRegisterCode = 0xE0;

                byte[] bytes = AddBytes(new byte[2] { 0x48, 0xB8 }, BitConverter.GetBytes((long)lpAddress));
                _newEntry = AddBytes(bytes, new byte[2] { 0xFF, 0xE0 });
            }
            else
            {
                // x86 :
                // BYTE byMovCode = 0xE9;
                // DWORD relativeJump;

                _newEntry = AddBytes(new byte[1] { 0xE9 }, BitConverter.GetBytes((Int32)((Int32)lpAddress - (Int32)_address - 5)));
            }

            int entrySize = _newEntry.Length;

            if (!VirtualProtect(_address, entrySize, PAGE_EXECUTE_READWRITE, ref lpflOldProtect))
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
        private Delegate _callback;

        #region API
        const int PAGE_EXECUTE_READWRITE = 0x40;

        static byte[] AddBytes(byte[] a, byte[] b)
        {
            List<byte> retArray = new List<byte>();

            for (int i = 0; i < a.Length; i++)
                retArray.Add(a[i]);

            for (int i = 0; i < b.Length; i++)
                retArray.Add(b[i]);

            return retArray.ToArray();
        }  

        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, int dwSize, int flNewProtect, ref int lpflOldProtect);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        #endregion
    }
}
