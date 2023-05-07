using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;

namespace emulatorLauncher.Tools
{
    class HdiGameDevice
    {
        private static List<HdiGameDevice> _devices;

        public static HdiGameDevice[] GetGameDevices()
        {
            if (_devices == null)
            {
                List<HdiGameDevice> devices = new List<HdiGameDevice>();

                try
                {
                    using (var QueryPnp = new ManagementObjectSearcher(@"\\.\root\cimv2", string.Format("SELECT * FROM Win32_PNPEntity WHERE Present = True AND PNPClass = 'HIDClass'"), new EnumerationOptions() { BlockSize = 48 }))
                    {
                        foreach (var PnpDevice in QueryPnp.Get())
                        {
                            var hardwareID = (string[])PnpDevice.Properties["HardwareID"].Value;
                            if (hardwareID == null || !hardwareID.Contains("HID_DEVICE_SYSTEM_GAME"))
                                continue;

                            devices.Add(new HdiGameDevice(PnpDevice));
                        }
                    }
                }
                catch { }

                _devices = devices;
            }

            return _devices.ToArray();
        }

        private HdiGameDevice(ManagementBaseObject PnpDevice)
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
