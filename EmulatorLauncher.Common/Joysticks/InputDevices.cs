using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace EmulatorLauncher.Common.Joysticks
{
    public static class InputDevices
    {
        /// <summary>
        /// Extracts the HID top-level collection token from a device path ("&Col02" -> "#COL02").
        /// The token is normalized to upper case, because RawInput reports the path in upper case
        /// while DirectInput reports it in lower case.
        /// Returns an empty string when the device exposes a single collection.
        /// </summary>
        private static string GetHidCollectionSuffix(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return string.Empty;

            int idx = devicePath.IndexOf("&COL", StringComparison.InvariantCultureIgnoreCase);
            if (idx < 0)
                return string.Empty;

            int start = idx + 4;
            int end = start;
            while (end < devicePath.Length && char.IsDigit(devicePath[end]))
                end++;

            if (end == start)
                return string.Empty;

            return "#COL" + devicePath.Substring(start, end - start);
        }

        /// <summary>
        /// Removes the collection token appended by GetInputDeviceParent, for the few callers that
        /// need to feed the result back to a Windows API expecting a plain device ID.
        /// </summary>
        public static string StripHidCollectionSuffix(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return devicePath;

            int idx = devicePath.IndexOf("#COL", StringComparison.InvariantCultureIgnoreCase);
            return idx < 0 ? devicePath : devicePath.Substring(0, idx);
        }

        public static string GetInputDeviceParent(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return "";

            string path = devicePath;

            int vidindex = path.IndexOf("VID_", StringComparison.InvariantCultureIgnoreCase);
            if (vidindex >= 0)
            {
                int cut = path.IndexOf("#{", vidindex);
                if (cut >= 0)
                    path = path.Substring(0, cut);
            }

            path = path.Replace(@"\\?\", "").Replace("#", "\\");

            // Multi-collection HID devices (Xin-Mo / Xinmotek dual arcade encoders, DragonRise and
            // "Twin USB" 2-players boards, ...) expose one joystick per top-level collection, and all
            // these collections share the exact same parent device node. Returning the bare parent
            // would give the very same path to every player, so all players would end up pointing at
            // the same joystick. Keep the collection index to make the result unique again.
            // The "#" separator is deliberate: ShortenDevicePath cuts between the last "\" and the
            // last "&", so a "&COL02" suffix would be swallowed.
            string collection = GetHidCollectionSuffix(devicePath);

            IntPtr pdnDevInst;
            int apiResult = CM_Locate_DevNodeA(out pdnDevInst, path, CM_LOCATE_DEVNODE_NORMAL);
            if (apiResult == CR_SUCCESS)
            {
                if (CM_Get_Parent(out pdnDevInst, pdnDevInst, 0) == CR_SUCCESS)
                {
                    StringBuilder buf = new StringBuilder(255);
                    buf.Clear();

                    if (CM_Get_Device_IDA(pdnDevInst, buf, 255, 0) == CR_SUCCESS)
                        return buf.ToString() + collection;
                }
            }

            return devicePath;
        }

        public static string ShortenDevicePath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return devicePath;

            if (devicePath.ToUpperInvariant().StartsWith("USB\\"))
            {
                int lastSplit = devicePath.LastIndexOf("\\");
                if (lastSplit >= 0)
                {
                    int lastAnd = devicePath.LastIndexOf("&");
                    if (lastAnd > lastSplit)
                    {
                        string ret = devicePath;
                        ret = ret.Substring(0, lastSplit + 1) + ret.Substring(lastAnd + 1);
                        return ret;
                    }
                }
            }

            return devicePath;
        }

        #region Apis
        const int CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
        const int CR_SUCCESS = 0x00000000;

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern int CM_Get_Device_IDA(IntPtr dnDevInst, StringBuilder Buffer, int BufferLen, int ulFlags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern int CM_Locate_DevNodeA(out IntPtr pdnDevInst, string pDeviceID, int ulFlags);

        [DllImport("setupapi.dll")]
        static extern int CM_Get_Parent(out IntPtr pdnDevInst, IntPtr dnDevInst, int ulFlags);
        #endregion
    }
}
