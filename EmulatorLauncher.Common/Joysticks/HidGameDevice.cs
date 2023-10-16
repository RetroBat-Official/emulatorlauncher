using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;

namespace EmulatorLauncher.Common.Joysticks
{
    public class HidGameDevice
    {
        public class UsbGameDevice : HidGameDevice
        {
            public UsbGameDevice(ManagementObject mo, HidGameDevice dev)
                : base(mo)
            {
                HdiGameDevice = dev;
            }

            public HidGameDevice HdiGameDevice { get; set; }

            public void Enable(bool enable)
            {
                var mo = GetDeviceFromName(this.PNPDeviceID);
                if (mo != null)
                    mo.InvokeMethod(enable ? "Enable" : "Disable", null, null);
            }
        }

        private static List<HidGameDevice> _devices;

        public static UsbGameDevice[] GetUsbGameDevices()
        {
            return GetGameDevices()
                .Select(dev => dev.AsUsbGameDevice())
                .Where(dev => dev != null)
                .ToArray();
        }

        private UsbGameDevice _usbDevice;
        private bool _usbDeviceQueryed;

        public UsbGameDevice AsUsbGameDevice()
        {
            if (!_usbDeviceQueryed)
            {
                _usbDeviceQueryed = true;

                try
                {
                    var pt = InputDevices.GetInputDeviceParent(PNPDeviceID);
                    if (pt.StartsWith("USB"))
                    {
                        var mo = GetDeviceFromName(pt);
                        if (mo != null)
                        {
                            var dev = new UsbGameDevice(mo, this);
                            mo.Dispose();
                            _usbDevice = dev;
                        }
                    }
                }
                catch { }
            }
             
            return _usbDevice;            
        }

        public static HidGameDevice[] GetGameDevices()
        {
            if (_devices == null)
            {
                var devices = new List<HidGameDevice>();

                try
                {
                    using (var QueryPnp = new ManagementObjectSearcher(@"\\.\root\cimv2", string.Format("SELECT * FROM Win32_PNPEntity WHERE Present = True AND PNPClass = 'HIDClass'"), new EnumerationOptions() { BlockSize = 48 }))
                    {
                        foreach (ManagementObject PnpDevice in QueryPnp.Get())
                        {
                            var hardwareID = (string[])PnpDevice.Properties["HardwareID"].Value;
                            if (hardwareID == null || !hardwareID.Contains("HID_DEVICE_SYSTEM_GAME"))
                                continue;

                            if (!"OK".Equals(PnpDevice.Properties["Status"].Value))
                                continue;

                            devices.Add(new HidGameDevice(PnpDevice));
                        }
                    }
                }
                catch { }

                _devices = devices;
            }

            return _devices.ToArray();
        }

        private static ManagementObject GetDeviceFromName(string name)
        {
            using (var myDevices = new ManagementObjectSearcher("root\\CIMV2", @"SELECT * FROM Win32_PnPEntity WHERE Present = True AND PNPClass = 'HIDClass'"))
            {
                foreach (ManagementObject item in myDevices.Get())
                {
                    var dev = new HidGameDevice(item);
                    if (dev.PNPDeviceID == name)
                        return item;
                }
            }

            return null;
        }

        protected HidGameDevice(ManagementBaseObject PnpDevice)
        {
            DeviceId = (string)PnpDevice.Properties["DeviceID"].Value;
            Caption = (string)PnpDevice.Properties["Caption"].Value;
            ClassGuid = (string)PnpDevice.Properties["ClassGuid"].Value;
            PNPClass = (string)PnpDevice.Properties["PNPClass"].Value;
            PNPDeviceID = (string)PnpDevice.Properties["PNPDeviceID"].Value;
            Status = (string)PnpDevice.Properties["Status"].Value;
            Description = (string)PnpDevice.Properties["Description"].Value;
            Manufacturer = (string)PnpDevice.Properties["Manufacturer"].Value;
            Name = (string)PnpDevice.Properties["Name"].Value;
            HardwareID = (string[])PnpDevice.Properties["HardwareID"].Value;

            SystemCreationClassName = (string)PnpDevice.Properties["SystemCreationClassName"].Value;
            SystemName = (string)PnpDevice.Properties["SystemName"].Value;

            Present = (bool)PnpDevice.Properties["Present"].Value;
        }

        public override string ToString()
        {
            return Caption;
        }

        public string Caption { get; set; }
        public string ClassGuid { get; set; }
        public string Description { get; set; }
        public string DeviceId { get; set; }
        public string[] HardwareID { get; set; }
        public string Manufacturer { get; set; }
        public string Name { get; set; }
        public string PNPClass { get; set; }
        public string PNPDeviceID { get; set; }
        public string Status { get; set; }

        public string SystemCreationClassName { get; set; }
        public string SystemName { get; set; }

        public bool Present { get; set; }
    }

}
