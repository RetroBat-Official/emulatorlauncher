using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher.Common.Joysticks
{
    public class VidPid
    {
        private VidPid() { }

        private static Regex vidPidRegex = new Regex(@"VID_([0-9A-F]+).*PID_([0-9A-F]+)", RegexOptions.IgnoreCase);

        public static VidPid Parse(string hidPath)
        {            
            // Match the regular expression on the HID path
            Match vidPidMatch = vidPidRegex.Match(hidPath);
            if (vidPidMatch.Success)
            {
                // Extract VID and PID values
                string vidValue = vidPidMatch.Groups[1].Value;
                string pidValue = vidPidMatch.Groups[2].Value;

                // Convert hexadecimal strings to short
                short vidShort = Convert.ToInt16(vidValue, 16);
                short pidShort = Convert.ToInt16(pidValue, 16);

                var ret = new VidPid();
                ret.ProductId = (USB_PRODUCT) pidShort;
                ret.VendorId = (USB_VENDOR) vidShort;
                return ret;
            }

            return null;
        }

        public USB_VENDOR VendorId { get; set; }
        public USB_PRODUCT ProductId { get; set; }
    }

}
