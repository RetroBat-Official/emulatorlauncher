using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace EmulatorLauncher.Common
{
    /// <summary>
    /// Classe permettant de lire les branches x86 et x64 (pas possible dans RegistryKey avec DotNet 4.0 et moins)
    /// </summary>
    public class RegistryKeyEx : IDisposable
    {
        public static RegistryKeyEx LocalMachine = new RegistryKeyEx(unchecked((uint)RegistryHive.LocalMachine), true);
        public static RegistryKeyEx CurrentUser = new RegistryKeyEx(unchecked((uint)RegistryHive.CurrentUser), true);
        public static RegistryKeyEx ClassesRoot = new RegistryKeyEx(unchecked((uint)RegistryHive.ClassesRoot), true);
        public static RegistryKeyEx Users = new RegistryKeyEx(unchecked((uint)RegistryHive.Users), true);
        
        private RegistryKeyEx(uint hKey, bool isSystemKey = false)
        {
            _hKey = hKey;
            _isSystemKey = isSystemKey;

        }

        private bool _isSystemKey;
        private uint _hKey;

        public RegistryKeyEx OpenSubKey(string subkey, RegistryViewEx view = RegistryViewEx.Default)
        {
            if (view == RegistryViewEx.Registry64 && !Is64BitOperatingSystem)
                return null;

            Win32Registry.RegSAM samView = 0;

            if (view == RegistryViewEx.Registry32 && IntPtr.Size == 8) // Laisser défault, si on tourne pas en x64
                samView = Win32Registry.RegSAM.WOW64_32Key;
            else if (view == RegistryViewEx.Registry64)
                samView = Win32Registry.RegSAM.WOW64_64Key;

            uint hKey = 0;

            uint err = Win32Registry.RegOpenKeyEx(unchecked((uint)_hKey), subkey, 0, Win32Registry.RegSAM.Read | Win32Registry.RegSAM.EnumerateSubKeys | samView, out hKey);
            if (err != 0)
                return null;

            return new RegistryKeyEx(hKey);
        }

        public RegistryKeyEx OpenWritableSubKey(string subkey, RegistryViewEx view = RegistryViewEx.Default)
        {
            if (view == RegistryViewEx.Registry64 && !Is64BitOperatingSystem)
                return null;

            Win32Registry.RegSAM samView = 0;

            if (view == RegistryViewEx.Registry32 && IntPtr.Size == 8) // Laisser défault, si on tourne pas en x64
                samView = Win32Registry.RegSAM.WOW64_32Key;
            else if (view == RegistryViewEx.Registry64)
                samView = Win32Registry.RegSAM.WOW64_64Key;

            uint hKey = 0;

            uint err = Win32Registry.RegOpenKeyEx(unchecked((uint)_hKey), subkey, 0, Win32Registry.RegSAM.Read | Win32Registry.RegSAM.Write| Win32Registry.RegSAM.EnumerateSubKeys | samView, out hKey);
            if (err != 0)
            {                
                err = Win32Registry.RegCreateKeyEx(unchecked((uint)_hKey), subkey, 0, null,
                    Win32Registry.RegOption.NonVolatile,
                    Win32Registry.RegSAM.Read | Win32Registry.RegSAM.Write | Win32Registry.RegSAM.EnumerateSubKeys | samView,
                    null,
                    out hKey,
                    IntPtr.Zero);

                if (err != 0)
                    return null;
            }

            return new RegistryKeyEx(hKey);
        }

        public object GetValue(string valueName, object defaultValue = null)
        {
            int lpType = 0;
            int lpcbData = 0;
            int num3 = Win32Registry.RegQueryValueEx(_hKey, valueName, null, ref lpType, (byte[])null, ref lpcbData);
            if (num3 != 0)
                return defaultValue;

            switch (lpType)
            {
                case 1:  // String = 1
                    {
                        RegistryValueKind subkeyKind = RegistryValueKind.String;
                        StringBuilder subkeyValueText = new StringBuilder((int)1024);
                        uint subKeyValueSize = (uint)subkeyValueText.Capacity;

                        uint errorCode = Win32Registry.RegQueryValueEx(_hKey, valueName, 0, ref subkeyKind, subkeyValueText, ref subKeyValueSize);
                        if (errorCode != 0)
                            return null;

                        return subkeyValueText.ToString();
                    }
                case 4:  // DWord
                    {
                        int num8 = 0;
                        num3 = Win32Registry.RegQueryValueEx(_hKey, valueName, null, ref lpType, ref num8, ref lpcbData);
                        return num8;                     
                    }
                case 3: // Binary
                case 5:
                    {
                        byte[] buffer2 = new byte[lpcbData];
                        num3 = Win32Registry.RegQueryValueEx(_hKey, valueName, null, ref lpType, buffer2, ref lpcbData);
                        return buffer2;
                    }

                    /*
                     Non implementé pour l'instant
                    Unknown = 0,                
                    ExpandString = 2,                
                    MultiString = 7,
                    QWord = 11*/
            }

            return defaultValue;
        }

        private static RegistryKeyEx GetBaseKeyFromKeyName(string keyName, out string subKeyName)
        {
            string str;
            if (keyName == null)
                throw new ArgumentNullException("keyName");

            int index = keyName.IndexOf('\\');
            if (index != -1)
                str = keyName.Substring(0, index).ToUpper(CultureInfo.InvariantCulture);
            else
                str = keyName.ToUpper(CultureInfo.InvariantCulture);

            RegistryKeyEx currentUser = null;
            switch (str)
            {
                case "HKEY_CURRENT_USER":
                    currentUser = CurrentUser;
                    break;

                case "HKEY_LOCAL_MACHINE":
                    currentUser = LocalMachine;
                    break;

                case "HKEY_CLASSES_ROOT":
                    currentUser = ClassesRoot;
                    break;

                case "HKEY_USERS":
                    currentUser = Users;
                    break;

                default:
                    throw new ArgumentException("Invalid keyName");
            }

            if ((index == -1) || (index == keyName.Length))
            {
                subKeyName = string.Empty;
                return currentUser;
            }
            subKeyName = keyName.Substring(index + 1, (keyName.Length - index) - 1);
            return currentUser;
        }

        public static object GetRegistryValue(string keyName, string valueName, RegistryViewEx view = RegistryViewEx.Default)
        {
            if (view == RegistryViewEx.Registry64 && !Is64BitOperatingSystem)
                return null;

            string str;
         
            RegistryKeyEx key2 = GetBaseKeyFromKeyName(keyName, out str).OpenSubKey(str, view);
            if (key2 == null)
                return null;

            object obj2;

            try
            {
                obj2 = key2.GetValue(valueName);
            }
            finally
            {
                key2.Close();
            }

            if (obj2 == null)
                return null;

            return obj2;
        }

        public static object GetRegistryValue(RegistryKeyEx hive, string keyName, string valueName, RegistryViewEx view = RegistryViewEx.Default)
        {
            if (view == RegistryViewEx.Registry64 && !Is64BitOperatingSystem)
                return null;

            RegistryKeyEx key2 = hive.OpenSubKey(keyName, view);
            if (key2 == null)
                return null;

            object obj2;

            try
            {
                obj2 = key2.GetValue(valueName);
            }
            finally
            {
                key2.Close();
            }

            if (obj2 == null)
                return null;

            return obj2;
        }
        
        public static bool SetValue(string keyName, string valueName, object value, RegistryViewEx view = RegistryViewEx.Default)
        {
            string str;

            RegistryKeyEx key2 = GetBaseKeyFromKeyName(keyName, out str).OpenWritableSubKey(str, view);
            if (key2 == null)
                return false;

            bool ret = false;

            try
            {
                ret = key2.SetValue(valueName, value);
            }
            finally
            {
                key2.Close();
            }

            return ret;
        }

        private RegistryValueKind CalculateValueKind(object value)
        {
            if (value is int)
                return RegistryValueKind.DWord;

            if (value is long)
                return RegistryValueKind.QWord;

            if (value is byte[])
                return RegistryValueKind.Binary;

            return RegistryValueKind.String;
        }



        public bool SetValue(string name, object value)
        {
            RegistryValueKind valueKind = CalculateValueKind(value);

            int size = 0;
            IntPtr pData = IntPtr.Zero;

            switch (valueKind)
            {
                case RegistryValueKind.String:
                    size = ((string)value).Length + 1;
                    pData = Marshal.StringToHGlobalAnsi((string)value);
                    break;
                case RegistryValueKind.DWord:
                    size = Marshal.SizeOf(typeof(Int32));
                    pData = Marshal.AllocHGlobal(size);
                    Marshal.WriteInt32(pData, (int)value);
                    break;
                case RegistryValueKind.QWord:
                    size = Marshal.SizeOf(typeof(Int64));
                    pData = Marshal.AllocHGlobal(size);
                    Marshal.WriteInt64(pData, (long)value);
                    break;
            }

            uint retVal = Win32Registry.RegSetValueEx(_hKey, name, 0, valueKind, pData, size);
            if (retVal != 0)
                return false;

            return true;
        }







        public void Close()
        {
            if (_hKey != 0)
            {
                Win32Registry.RegCloseKey(_hKey);
                _hKey = 0;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_hKey == 0 || _isSystemKey)
                return;

            Close();
        }

        public string[] GetSubkeysNames()
        {
            if (_hKey == 0)
                return new string[] { };

            uint numSubKeys;
            uint errorCode = Win32Registry.RegQueryInfoKey(_hKey, null, 0, 0, out numSubKeys, 0, 0, 0, 0, 0, 0, 0);

            if (errorCode != 0) return null;

            string[] names = new string[numSubKeys];

            for (uint i = 0; i < numSubKeys; i++)
            {
                uint maxKeySize = 1024;
                StringBuilder sb = new StringBuilder((int)maxKeySize);
                long writeTime;

                errorCode = Win32Registry.RegEnumKeyEx(_hKey, i, sb, ref maxKeySize, 0, 0, 0, out writeTime);
                if (errorCode != 0)
                    break;

                names[i] = sb.ToString();
            }

            return names;
        }

        public string[] GetValueNames()
        {
            if (_hKey == 0)
                return new string[] { };

            uint numSubKeys;
            uint errorCode = Win32Registry.RegQueryInfoKey(_hKey, null, 0, 0, 0, 0, 0, out numSubKeys, 0, 0, 0, 0);            

            if (errorCode != 0) 
                return null;

            string[] names = new string[numSubKeys];

            for (uint i = 0; i < numSubKeys; i++)
            {
                uint maxKeySize = 1024;
                StringBuilder sb = new StringBuilder((int)maxKeySize);

                errorCode = Win32Registry.RegEnumValue(_hKey, i, sb, ref maxKeySize, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);         
                if (errorCode != 0)
                    break;

                names[i] = sb.ToString();
            }

            return names;
        }
            
        #region Is64BitOperatingSystem

        public static bool Is64BitOperatingSystem
        {
            get
            {
                if (IntPtr.Size == 8)  // 64-bit programs run only on Win64
                    return true;
                else  // 32-bit programs run on both 32-bit and 64-bit Windows
                {
                    // Detect whether the current process is a 32-bit process 
                    // running on a 64-bit system.
                    bool flag;
                    return ((DoesWin32MethodExist("kernel32.dll", "IsWow64Process") && IsWow64Process(GetCurrentProcess(), out flag)) && flag);
                }
            }
        }

        static bool DoesWin32MethodExist(string moduleName, string methodName)
        {
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            if (moduleHandle == IntPtr.Zero)
            {
                return false;
            }
            return (GetProcAddress(moduleHandle, methodName) != IntPtr.Zero);
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule,
            [MarshalAs(UnmanagedType.LPStr)]string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        #endregion

        internal static class Win32Registry
        {
            [Flags]
            public enum RegOption
            {
                NonVolatile = 0x0,
                Volatile = 0x1,
                CreateLink = 0x2,
                BackupRestore = 0x4,
                OpenLink = 0x8
            }

            [Flags]
            public enum RegSAM : uint
            {
                QueryValue = 0x0001,
                SetValue = 0x0002,
                CreateSubKey = 0x0004,
                EnumerateSubKeys = 0x0008,
                Notify = 0x0010,
                CreateLink = 0x0020,
                WOW64_32Key = 0x0200,
                WOW64_64Key = 0x0100,
                WOW64_Res = 0x0300,
                StandardRightsRead = 0x00020000,
                Read = 0x00020019,
                Write = 0x00020006,
                Execute = 0x00020019,
                AllAccess = 0x000f003f
            }

            [StructLayout(LayoutKind.Sequential)]
            public class SECURITY_ATTRIBUTES
            {
                public int nLength;
                public IntPtr lpSecurityDescriptor;
                public int bInheritHandle;
            }

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegCloseKey(
                uint hKey);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegCreateKeyEx(
                uint hKey,
                string lpSubKey,
                int Reserved,
                string lpClass,
                RegOption dwOptions,
                RegSAM samDesired,
                SECURITY_ATTRIBUTES lpSecurityAttributes,
                out uint phkResult,
                IntPtr lpdwDisposition);


            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegQueryValueEx(
                uint kHey,
                string lpValueName,
                int lpReserved,
                ref Microsoft.Win32.RegistryValueKind lpType,
                StringBuilder lpData,
                ref uint lpcbData
            );

            [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
            public static extern int RegQueryValueEx(uint hKey, string lpValueName, int[] lpReserved, ref int lpType, [Out] byte[] lpData, ref int lpcbData);

            [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
            public static extern int RegQueryValueEx(uint hKey, string lpValueName, int[] lpReserved, ref int lpType, ref int lpData, ref int lpcbData);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegQueryValueEx(
                uint kHey,
                string lpValueName,
                int lpReserved,
                ref Microsoft.Win32.RegistryValueKind lpType,
                ref uint lpData,
                ref uint lpcbData
            );

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegSetValueEx(uint hKey,
                 [MarshalAs(UnmanagedType.LPStr)]
                 string lpValueName,
                 int Reserved,
                 RegistryValueKind dwType,
                 IntPtr lpData,
                 int cbData);     

            [DllImport("advapi32.dll", SetLastError = true)]
            internal static extern uint RegDeleteValue(
                uint hKey,
                string lpValueName);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegDeleteKey(
                uint hKey,
                string lpSubKey);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint RegOpenKeyEx(
                uint hKey,
                string lpSubKey,
                uint ulOptions,
                RegSAM samDesired,
                out uint phkResult);

            [DllImport("advapi32.dll", EntryPoint = "RegEnumKeyEx")]
            public extern static uint RegEnumKeyEx(uint hkey,
                uint index,
                StringBuilder lpName,
                ref uint lpcbName,
                uint reserved,
                uint lpClass,
                uint lpcbClass,
                out long lpftLastWriteTime);

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, BestFitMapping = false)]
            public static extern uint RegEnumValue(uint hKey,
                uint dwIndex,
                StringBuilder lpValueName, 
                ref uint lpcbValueName,
                IntPtr lpReserved_MustBeZero, IntPtr lpType, IntPtr lpData, IntPtr lpcbData);

            [DllImport("advapi32.dll", EntryPoint = "RegQueryInfoKey", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            public extern static uint RegQueryInfoKey(
                uint hkey,
                StringBuilder lpClass,
                uint lpcbClass,
                uint lpReserved,
                out uint lpcSubKeys,
                uint lpcbMaxSubKeyLen,
                uint lpcbMaxClassLen,
                uint lpcValues,
                uint lpcbMaxValueNameLen,
                uint lpcbMaxValueLen,
                uint lpcbSecurityDescriptor,
                uint lpftLastWriteTime);


            [DllImport("advapi32.dll", EntryPoint = "RegQueryInfoKey", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
            public extern static uint RegQueryInfoKey(
                uint hkey,
                StringBuilder lpClass,
                uint lpcbClass,
                uint lpReserved,
                uint lpcSubKeys,
                uint lpcbMaxSubKeyLen,
                uint lpcbMaxClassLen,
                out uint lpcValues,
                uint lpcbMaxValueNameLen,
                uint lpcbMaxValueLen,
                uint lpcbSecurityDescriptor,
                uint lpftLastWriteTime);

        }
    }

    public enum RegistryViewEx
    {
        Default = 0,

        //Affichage par défaut.
        Registry32 = 512,

        //Affichage 32 bits.
        Registry64 = 256,

        //Affichage 64 bits.
    }
}
