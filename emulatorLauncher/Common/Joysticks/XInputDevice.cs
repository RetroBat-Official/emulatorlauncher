using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher.Tools
{
    class XInputDevice
    {
        public XInputDevice(int index)
        {
            DeviceIndex = index;
            Name = "Input Pad #" + (DeviceIndex + 1).ToString();
        }

        public override string ToString()
        {
            return Name;
        }

        public int DeviceIndex { get; set; }
        public string Name { get; set; }

        private static bool IsXInputDevice(string vendorId, string productId)
        {
            var ParseIds = new System.Text.RegularExpressions.Regex(@"([VP])ID_([\da-fA-F]{4})");
            // Used to grab the VID/PID components from the device ID string.                
            // Iterate over all PNP devices.                

            foreach (var device in HdiGameDevice.GetGameDevices())
            {
                var DeviceId = device.DeviceId;
                if (DeviceId.Contains("IG_"))
                {
                    // Check the VID/PID components against the joystick's.                            
                    var Ids = ParseIds.Matches(DeviceId);
                    if (Ids.Count == 2)
                    {
                        ushort? VId = null, PId = null;
                        foreach (System.Text.RegularExpressions.Match M in Ids)
                        {
                            ushort Value = ushort.Parse(M.Groups[2].Value, System.Globalization.NumberStyles.HexNumber);
                            switch (M.Groups[1].Value)
                            {
                                case "V": VId = Value; break;
                                case "P": PId = Value; break;
                            }
                        }

                        //if (VId.HasValue && this.VendorId == VId && PId.HasValue && this.ProductId == PId) return true; 
                        if (VId.HasValue && vendorId == VId.Value.ToString("X4") && PId.HasValue && productId == PId.Value.ToString("X4"))
                            return true;
                    }
                }
            }

            return false;
        }

        private static string[] _xInputDriverIdentifiers = new string[] { "72" /* 'r' for RawInput */, "78" /* 'x' for XInput */ };

        public static bool IsXInputDevice(string deviceGuid)
        {
            if (deviceGuid == null || deviceGuid.Length < 32 || !deviceGuid.StartsWith("03") /* XInput is always 03 (USB) */ || !_xInputDriverIdentifiers.Contains(deviceGuid.Substring(28, 2)))
                return false;

            string vendorId = (deviceGuid.Substring(10, 2) + deviceGuid.Substring(8, 2)).ToUpper();
            string productId = (deviceGuid.Substring(18, 2) + deviceGuid.Substring(16, 2)).ToUpper();

            return IsXInputDevice(vendorId, productId);
        }
    }
}
