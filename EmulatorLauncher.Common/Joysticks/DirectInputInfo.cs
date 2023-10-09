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
    }
        
}
