using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmulatorLauncher.Common.Joysticks
{
    public class DirectInputInfo
    {
        private static DirectInputInfo[] _controllers;

        public static DirectInputInfo[] Controllers
        {
            get
            {
                if (_controllers == null)
                {
                    var ret = new List<DirectInputInfo>();

                    int index = 0;

                    try
                    {
                        using (var directInput = new SharpDX.DirectInput.DirectInput())
                        {
                            foreach (var deviceInstance in directInput.GetDevices())
                            {
                                if (deviceInstance.Usage != SharpDX.Multimedia.UsageId.GenericGamepad && deviceInstance.Usage != SharpDX.Multimedia.UsageId.GenericJoystick)
                                    continue;
                                
                                string guidString = deviceInstance.ProductGuid.ToString().Replace("-", "");

                                string dxproductId = guidString.Substring(0, 4).ToUpper();
                                string dxvendorId = guidString.Substring(4, 4).ToUpper();

                                DirectInputInfo info = new DirectInputInfo();
                                info.DeviceIndex = index;
                                info.Name = deviceInstance.InstanceName;
                                info.ProductGuid = deviceInstance.ProductGuid;
                                info.InstanceGuid = deviceInstance.InstanceGuid;
                                info.VendorId = ushort.Parse(dxvendorId, System.Globalization.NumberStyles.HexNumber);
                                info.ProductId = ushort.Parse(dxproductId, System.Globalization.NumberStyles.HexNumber);

                                try
                                {
                                    using (var joystick = new SharpDX.DirectInput.Joystick(directInput, deviceInstance.InstanceGuid))
                                    {
                                        info.DevicePath = joystick.Properties.InterfacePath;
                                        info.JoystickID = joystick.Properties.JoystickId;
                                        info.ParentDevice = InputDevices.GetInputDeviceParent(info.DevicePath);
                                    }
                                }
                                catch { }

                                ret.Add(info);
                                index++;
                            }
                        }
                    }
                    catch { }

                    _controllers = ret.ToArray();
                }

                return _controllers;
            }
        }

        public int DeviceIndex { get; set; }
        public string Name { get; set; }
        public Guid InstanceGuid { get; set; }
        public Guid ProductGuid { get; set; }
        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }
        public string DevicePath { get; set; }
        public string ParentDevice { get; set; }
        public int JoystickID { get; set; }

        public bool TestDirectInputDevice(string deviceGuid)
        {
            if (deviceGuid == null || deviceGuid.Length < 32)
                return false;

            string vendorId = (deviceGuid.Substring(10, 2) + deviceGuid.Substring(8, 2)).ToUpper();
            string productId = (deviceGuid.Substring(18, 2) + deviceGuid.Substring(16, 2)).ToUpper();

            string guidString = ProductGuid.ToString().Replace("-", "");
            if (guidString.EndsWith("504944564944"))
            {
                string dxproductId = guidString.Substring(0, 4).ToUpper();
                string dxvendorId = guidString.Substring(4, 4).ToUpper();

                if (vendorId == dxvendorId && productId == dxproductId)
                    return true;
            }
            else
            {
                Guid productGuid = deviceGuid.FromSdlGuidString();
                if (productGuid == ProductGuid || productGuid == InstanceGuid)
                    return true;
            }

            return false;
        }

        public override string ToString()
        {
            return Name;
        }

        public static string SdlToDikCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x0D: return "28";
                case 0x00: return "";
                case 0x08: return "14";
                case 0x09: return "15";
                case 0x1B: return "1";
                case 0x20: return "57";
                case 0x21: return "";
                case 0x22: return "";
                case 0x23: return "";
                case 0x24: return "";
                case 0x25: return "";
                case 0x26: return "";
                case 0x27: return "";
                case 0x28: return "";
                case 0x29: return "";
                case 0x2A: return "";
                case 0x2B: return "";
                case 0x2C: return "51";
                case 0x2D: return "12";
                case 0x2E: return "52";
                case 0x2F: return "53";
                case 0x30: return "11";
                case 0x31: return "2";
                case 0x32: return "3";
                case 0x33: return "4";
                case 0x34: return "5";
                case 0x35: return "6";
                case 0x36: return "7";
                case 0x37: return "8";
                case 0x38: return "9";
                case 0x39: return "10";
                case 0x3A: return "";
                case 0x3B: return "39";
                case 0x3C: return "";
                case 0x3D: return "13";
                case 0x3F: return "";
                case 0x40: return "145";
                case 0x5B: return "26";
                case 0x5C: return "43";
                case 0x5D: return "27";
                case 0x5E: return "";
                case 0x5F: return "147";
                case 0x60: return "40";
                case 0x61: return "30";
                case 0x62: return "48";
                case 0x63: return "46";
                case 0x64: return "32";
                case 0x65: return "18";
                case 0x66: return "33";
                case 0x67: return "34";
                case 0x68: return "35";
                case 0x69: return "23";
                case 0x6A: return "36";
                case 0x6B: return "37";
                case 0x6C: return "38";
                case 0x6D: return "50";
                case 0x6E: return "49";
                case 0x6F: return "24";
                case 0x70: return "25";
                case 0x71: return "16";
                case 0x72: return "19";
                case 0x73: return "31";
                case 0x74: return "20";
                case 0x75: return "22";
                case 0x76: return "47";
                case 0x77: return "17";
                case 0x78: return "45";
                case 0x79: return "21";
                case 0x7A: return "44";
                case 0x7F: return "211";
                case 0x40000039: return "58";
                case 0x4000003A: return "59";
                case 0x4000003B: return "60";
                case 0x4000003C: return "61";
                case 0x4000003D: return "62";
                case 0x4000003E: return "63";
                case 0x4000003F: return "64";
                case 0x40000040: return "65";
                case 0x40000041: return "66";
                case 0x40000042: return "67";
                case 0x40000043: return "68";
                case 0x40000044: return "87";
                case 0x40000045: return "88";
                case 0x40000046: return "183";
                case 0x40000047: return "70";
                case 0x40000048: return "197";
                case 0x40000049: return "210";
                case 0x4000004A: return "199";
                case 0x4000004B: return "";
                case 0x4000004D: return "207";
                case 0x4000004E: return "";
                case 0x4000004F: return "205";
                case 0x40000050: return "203";
                case 0x40000051: return "208";
                case 0x40000052: return "200";
                case 0x40000053: return "69";
                case 0x40000054: return "78";
                case 0x40000055: return "55";
                case 0x40000056: return "74";
                case 0x40000057: return "";
                case 0x40000058: return "";
                case 0x40000059: return "79";
                case 0x4000005A: return "80";
                case 0x4000005B: return "81";
                case 0x4000005C: return "75";
                case 0x4000005D: return "76";
                case 0x4000005E: return "77";
                case 0x4000005F: return "71";
                case 0x40000060: return "72";
                case 0x40000061: return "73";
                case 0x40000062: return "82";
                case 0x40000063: return "83";
                case 0x40000067: return "141";
                case 0x40000068: return "100";
                case 0x40000069: return "101";
                case 0x4000006A: return "102";
                case 0x4000006B: return "";
                case 0x4000006C: return "";
                case 0x4000006D: return "";
                case 0x4000006E: return "";
                case 0x4000006F: return "";
                case 0x40000070: return "";
                case 0x40000071: return "";
                case 0x40000072: return "";
                case 0x40000073: return "";
                case 0x40000074: return "";
                case 0x40000075: return "";
                case 0x40000076: return "";
                case 0x40000077: return "";
                case 0x40000078: return "164";
                case 0x40000079: return "";
                case 0x4000007A: return "";
                case 0x4000007B: return "";
                case 0x4000007C: return "";
                case 0x4000007D: return "";
                case 0x4000007E: return "";
                case 0x4000007F: return "160";
                case 0x40000080: return "176";
                case 0x40000081: return "174";
                case 0x40000085: return "";
                case 0x400000E0: return "29";
                case 0x400000E1: return "42";
                case 0x400000E2: return "56";
                case 0x400000E4: return "157";
                case 0x400000E5: return "54";
                case 0x400000E6: return "184";
                case 0x40000101: return "";
                case 0x40000102: return "153";
                case 0x40000103: return "";
                case 0x40000105: return "162";
            }
            return "None";
        }
    }
        
}
