using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace EmulatorLauncher.Common
{
    public class HighPerformancePowerScheme : IDisposable
    {
        PowerScheme _oldPowerSheme;

        public HighPerformancePowerScheme()
        {
            try
            {
                var powerSchemes = HighPerformancePowerScheme.GetPowerSchemes();

                var oldPowerScheme = powerSchemes.FirstOrDefault(s => s.IsActive);
                var newPowerScheme = powerSchemes.FirstOrDefault(s => s.Guid == new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"));

                if (newPowerScheme != null && oldPowerScheme != null && oldPowerScheme != newPowerScheme)
                {
                    SetActivePowerScheme(newPowerScheme.Guid);
                    _oldPowerSheme = oldPowerScheme;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_oldPowerSheme != null)
            {
                SetActivePowerScheme(_oldPowerSheme.Guid);                
                _oldPowerSheme = null;
            }
        }
        
        class PowerScheme
        {
            public Guid Guid { get; private set; }
            public string Name { get; private set; }
            public bool IsActive { get; private set; }

            public PowerScheme(string name, Guid guid) : this(name, guid, false) { }

            public PowerScheme(string name, Guid guid, bool isActive)
            {
                this.Name = name;
                this.Guid = guid;
                this.IsActive = isActive;
            }

            public override string ToString()
            {
                return this.Name;
            }
        }

        static Guid GetActivePowerScheme()
        {
            Guid activeSchema = Guid.Empty;
            IntPtr guidPtr = IntPtr.Zero;

            try
            {
                var errCode = PowerGetActiveScheme(IntPtr.Zero, out guidPtr);

                if (errCode != 0) { throw new Exception("GetActiveGuid() failed with code " + errCode); }
                if (guidPtr == IntPtr.Zero) { throw new Exception("GetActiveGuid() returned null pointer for GUID"); }

                activeSchema = (Guid)Marshal.PtrToStructure(guidPtr, typeof(Guid));
            }
            finally
            {
                if (guidPtr != IntPtr.Zero) { LocalFree(guidPtr); }
            }

            return activeSchema;
        }

        static void SetActivePowerScheme(Guid guid)
        {
            var errCode = PowerSetActiveScheme(IntPtr.Zero, ref guid);
            if (errCode != 0) { throw new Exception("SetActiveGuid() failed with code " + errCode); }
        }

        private const int ERROR_NO_MORE_ITEMS = 259;

        static List<PowerScheme> GetPowerSchemes()
        {
            var activeGuid = GetActivePowerScheme();
            return GetPowerSchemesGuids().Select(guid => new PowerScheme(GetPowerPlanName(guid), guid, guid == activeGuid)).ToList();
        }

        static IEnumerable<Guid> GetPowerSchemesGuids()
        {
            var schemeGuid = Guid.Empty;

            uint sizeSchemeGuid = (uint)Marshal.SizeOf(typeof(Guid));
            uint schemeIndex = 0;

            while (true)
            {
                uint errCode = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid);
                if (errCode == ERROR_NO_MORE_ITEMS) { yield break; }
                if (errCode != 0) { throw new Exception("GetPowerSchemeGUIDs() failed when getting buffer pointer with code " + errCode); }

                yield return schemeGuid;
                schemeIndex++;
            }
        }

        static string GetPowerPlanName(Guid guid)
        {
            string name = string.Empty;

            IntPtr bufferPointer = IntPtr.Zero;
            uint bufferSize = 0;

            try
            {
                var errCode = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, bufferPointer, ref bufferSize);
                if (errCode != 0) { throw new Exception("GetPowerPlanName() failed when getting buffer size with code " + errCode); }

                if (bufferSize <= 0) { return String.Empty; }
                bufferPointer = Marshal.AllocHGlobal((int)bufferSize);

                errCode = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, bufferPointer, ref bufferSize);
                if (errCode != 0) { throw new Exception("GetPowerPlanName() failed when getting buffer pointer with code " + errCode); }

                name = Marshal.PtrToStringUni(bufferPointer);
            }
            finally
            {
                if (bufferPointer != IntPtr.Zero) { Marshal.FreeHGlobal(bufferPointer); }
            }

            return name;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
        static extern uint PowerSetActiveScheme(IntPtr UserPowerKey, ref Guid ActivePolicyGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
        static extern uint PowerGetActiveScheme(IntPtr UserPowerKey, out IntPtr ActivePolicyGuid);

        [DllImport("powrprof.dll", EntryPoint = "PowerReadFriendlyName")]
        static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid, IntPtr PowerSettingGuid, IntPtr BufferPtr, ref uint BufferSize);

        [DllImport("PowrProf.dll")]
        static extern UInt32 PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, UInt32 AcessFlags, UInt32 Index, ref Guid Buffer, ref UInt32 BufferSize);

        enum AccessFlags : uint
        {
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18
        }
    }
}
