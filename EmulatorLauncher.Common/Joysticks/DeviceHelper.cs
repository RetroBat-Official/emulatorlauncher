using EmulatorLauncher.Common;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace EmulatorLauncher.Common.Joysticks
{
    public static class DeviceHelper
    {
        #region P/Invoke

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_DevNode_Registry_Property(uint dnDevInst, uint ulProperty,
            out uint pulRegDataType, byte[] buffer, ref uint pullLength, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_DevNode_PropertyW(uint dnDevInst, ref DEVPROPKEY PropertyKey,
            out uint PropertyType, byte[] PropertyBuffer, ref uint PropertyBufferSize, uint ulFlags);

        private const uint CR_BUFFER_SMALL = 0x1A;
        private const uint DEVPROP_TYPE_STRING = 0x12;

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        private static DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new DEVPROPKEY
        {
            fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2),
            pid = 4
        };
        private const uint CM_LOCATE_DEVNODE_NORMAL = 0;
        private const uint CR_SUCCESS = 0;

        private const uint CM_DRP_DEVICEDESC = 0x1;
        private const uint CM_DRP_MFG = 0xC;

        #endregion

        /// <summary>
        /// Resolves a RawInput device path (e.g. \\?\HID#VID_045E&PID_0750#...) 
        /// to its friendly name and manufacturer using CfgMgr32.
        /// </summary>
        public static bool GetFriendlyName(string devicePath, out string friendlyName, out string manufacturer)
        {
            friendlyName = "";
            manufacturer = "";

            string deviceId = ConvertToDeviceId(devicePath);
            if (string.IsNullOrEmpty(deviceId))
                return false;

            int ret = CM_Locate_DevNode(out uint devInst, deviceId, CM_LOCATE_DEVNODE_NORMAL);
            if (ret != CR_SUCCESS)
            {
                SimpleLogger.Instance.Info($"[GUNS] CM_Locate_DevNode failed for '{deviceId}' with code {ret}");
                return false;
            }

            friendlyName = GetDevNodeProperty(devInst, CM_DRP_DEVICEDESC);
            manufacturer = GetDevNodeProperty(devInst, CM_DRP_MFG);
            return true;
        }

        /// <summary>
        /// Converts a RawInput device path to a SetupAPI-compatible DeviceID.
        /// e.g. \\?\HID#VID_045E&PID_0750#6&1a2b3c4d&0&0000#{4d1e55b2-...}
        ///   => HID\VID_045E&PID_0750\6&1A2B3C4D&0&0000
        /// </summary>
        private static string ConvertToDeviceId(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath) || devicePath.Length < 5)
                return "";

            // Strip leading \\?\ and replace # separators with backslashes
            string deviceId = devicePath
                .Substring(4)
                .ToUpperInvariant()
                .Replace("#", "\\");

            // Strip the trailing \{interface-GUID} segment
            int lastBackslash = deviceId.LastIndexOf('\\');
            if (lastBackslash <= 0)
                return "";

            return deviceId.Substring(0, lastBackslash);
        }

        private static string GetDevNodeProperty(uint devInst, uint property)
        {
            uint size = 0;

            CM_Get_DevNode_Registry_Property(devInst, property, out _, null, ref size, 0);
            if (size == 0)
                return "";

            byte[] buffer = new byte[size];
            int ret = CM_Get_DevNode_Registry_Property(devInst, property, out _, buffer, ref size, 0);
            if (ret != CR_SUCCESS)
                return "";

            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }

        public static string GetParentBusReportedDeviceDesc(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return "";

            int ret = CM_Locate_DevNode(out uint devInst, deviceId, CM_LOCATE_DEVNODE_NORMAL);
            if (ret != CR_SUCCESS)
            {
                SimpleLogger.Instance.Info($"[DEVICE] CM_Locate_DevNode failed for '{deviceId}' with code {ret}");
                return "";
            }

            ret = CM_Get_Parent(out uint parentInst, devInst, 0);
            if (ret != CR_SUCCESS)
            {
                SimpleLogger.Instance.Info($"[DEVICE] CM_Get_Parent failed for '{deviceId}' with code {ret}");
                return "";
            }

            uint size = 0;
            ret = CM_Get_DevNode_PropertyW(parentInst, ref DEVPKEY_Device_BusReportedDeviceDesc,
                out _, null, ref size, 0);

            if (ret != CR_BUFFER_SMALL || size == 0)
                return "";

            byte[] buffer = new byte[size];
            ret = CM_Get_DevNode_PropertyW(parentInst, ref DEVPKEY_Device_BusReportedDeviceDesc,
                out uint propType, buffer, ref size, 0);

            if (ret != CR_SUCCESS || propType != DEVPROP_TYPE_STRING)
                return "";

            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }

        /// <summary>
        /// Returns the name reported by the device hardware (USB iProduct / Bluetooth name), as shown in
        /// Device Manager > Details > "Bus reported device description".
        /// Starts from the HID node and walks up to its parents, as composite and XInput devices
        /// report it on the USB node. Limited to 2 levels to avoid reaching the USB hub.
        /// </summary>
        public static string GetBusReportedDeviceName(string devicePath, int maxParentLevels = 2)
        {
            string deviceId = ConvertToDeviceId(devicePath);
            if (string.IsNullOrEmpty(deviceId))
                return "";

            uint devInst;
            if (CM_Locate_DevNode(out devInst, deviceId, CM_LOCATE_DEVNODE_NORMAL) != CR_SUCCESS)
                return "";

            for (int level = 0; level <= maxParentLevels; level++)
            {
                string name = GetDevNodeStringProperty(devInst, DEVPKEY_Device_BusReportedDeviceDesc);
                if (!string.IsNullOrEmpty(name))
                    return name;

                uint parentInst;
                if (CM_Get_Parent(out parentInst, devInst, 0) != CR_SUCCESS)
                    break;

                devInst = parentInst;
            }

            return "";
        }

        private static string GetDevNodeStringProperty(uint devInst, DEVPROPKEY key)
        {
            uint size = 0;
            uint propType;

            int ret = CM_Get_DevNode_PropertyW(devInst, ref key, out propType, null, ref size, 0);
            if (ret != CR_BUFFER_SMALL || size == 0)
                return "";

            byte[] buffer = new byte[size];
            ret = CM_Get_DevNode_PropertyW(devInst, ref key, out propType, buffer, ref size, 0);
            if (ret != CR_SUCCESS || propType != DEVPROP_TYPE_STRING)
                return "";

            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        private static extern bool HidD_GetManufacturerString(IntPtr HidDeviceObject, StringBuilder Buffer, uint BufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        private static extern bool HidD_GetProductString(IntPtr HidDeviceObject, StringBuilder Buffer, uint BufferLength);

        /// <summary>
        /// Reads the USB manufacturer (iManufacturer) and product (iProduct) strings of a HID device.
        /// The device is opened without read/write access, so it works even when another process owns it.
        /// </summary>
        public static bool GetHidStrings(string devicePath, out string manufacturer, out string product)
        {
            manufacturer = "";
            product = "";

            if (string.IsNullOrEmpty(devicePath))
                return false;

            // dwDesiredAccess = 0 (query only), FILE_SHARE_READ | FILE_SHARE_WRITE, OPEN_EXISTING
            IntPtr handle = CreateFileW(devicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return false;

            try
            {
                // BufferLength is in bytes
                var buf = new StringBuilder(256);
                if (HidD_GetManufacturerString(handle, buf, (uint)(buf.Capacity * 2)))
                    manufacturer = buf.ToString().Trim();

                buf.Clear();
                if (HidD_GetProductString(handle, buf, (uint)(buf.Capacity * 2)))
                    product = buf.ToString().Trim();

                return true;
            }
            finally
            {
                CloseHandle(handle);
            }
        }
    }
}