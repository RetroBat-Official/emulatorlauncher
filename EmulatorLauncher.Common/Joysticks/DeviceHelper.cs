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
    }
}