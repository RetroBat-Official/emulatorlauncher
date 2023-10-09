using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace EmulatorLauncher.Common.Joysticks
{
    public static class InputDevices
    {
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

            IntPtr pdnDevInst;
            int apiResult = CM_Locate_DevNodeA(out pdnDevInst, path, CM_LOCATE_DEVNODE_NORMAL);
            if (apiResult == CR_SUCCESS)
            {
                if (CM_Get_Parent(out pdnDevInst, pdnDevInst, 0) == CR_SUCCESS)
                {
                    StringBuilder buf = new StringBuilder(255);
                    buf.Clear();

                    if (CM_Get_Device_IDA(pdnDevInst, buf, 255, 0) == CR_SUCCESS)
                        return buf.ToString();
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
