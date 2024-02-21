using System;
using System.Text.RegularExpressions;

namespace EmulatorLauncher.Common.Joysticks
{
    public class VidPid
    {
        private VidPid() { }

        private static Regex vidPidRegex = new Regex(@"VID_([0-9A-F]+).*PID_([0-9A-F]+)", RegexOptions.IgnoreCase);
        private static Regex vidPidRegex2 = new Regex(@"VID&([0-9A-F]+).*PID&([0-9A-F]+)", RegexOptions.IgnoreCase);

        public static VidPid Parse(string hidPath)
        {            
            // Match the regular expression on the HID path
            Match vidPidMatch = vidPidRegex.Match(hidPath);

            if (!vidPidMatch.Success)
            {
                Match vidPidMatch2 = vidPidRegex2.Match(hidPath);

                if (vidPidMatch2.Success)
                {
                    // Extract VID and PID values
                    if (vidPidMatch2.Groups[1].Length > 3 && vidPidMatch2.Groups[2].Length > 3)
                    {
                        string vidValue = vidPidMatch2.Groups[1].Value.Substring(vidPidMatch2.Groups[1].Length - 4);
                        string pidValue = vidPidMatch2.Groups[2].Value.Substring(vidPidMatch2.Groups[2].Length - 4);
                        // Convert hexadecimal strings to short
                        short vidShort = Convert.ToInt16(vidValue, 16);
                        short pidShort = Convert.ToInt16(pidValue, 16);
                        var ret = new VidPid();
                        ret.ProductId = (USB_PRODUCT)pidShort;
                        ret.VendorId = (USB_VENDOR)vidShort;
                        return ret;
                    }
                }
            }

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
