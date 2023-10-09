using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher.Common.Joysticks
{
    /// <summary>
    /// This class allows convertion of HID joystick codes to DirectInput codes
    /// </summary>
    public class HidToDirectInput
    {
        public Input FromInput(SdlJoystickGuid guid, Input input)
        {
            if (guid.WrappedTechID != SdlWrappedTechId.HID)
                return null;

            var prod = ((int)guid.ProductId).ToString("X4");
            var vend = ((int)guid.VendorId).ToString("X4");

            var device = devices.FirstOrDefault(dev => dev.product == prod && dev.vendor == vend);
            if (device == null)
            {
                SimpleLogger.Instance.Warning("Unknown HID device : " + guid.ToString() + ", " + guid.ProductId + ", " + guid.VendorId);
                return null;
            }
            
            string hidInput = FormatInput(input);

            var dinputMapping = device.mappings.Where(m => m.hid == hidInput).Select(m => m.dinput).FirstOrDefault();
            if (!string.IsNullOrEmpty(dinputMapping))
            {
                var split = dinputMapping.Split(new char[] { '/' });
                if (split.Length == 3)
                {
                    var newInput = new Input();
                    newInput.Name = input.Name;
                    newInput.Type = split[0];
                    newInput.Id = split[1].ToInteger();
                    newInput.Value = split[2].ToInteger();
                    return newInput;
                }
            }

            return input;
        }

        public static HidToDirectInput Instance
        {
            get
            {
                if (_instance == null)
                    _instance = HidToDirectInput.FromEsInputData(Encoding.UTF8.GetString(Properties.Resources.hidtodinput));

                return _instance;
            }
        }
        
        private static HidToDirectInput _instance;

        private static string FormatInput(Input input)
        {
            return input.Type + "/" + input.Id + "/" + input.Value;
        }

        private static HidToDirectInput FromEsInputData(string esInputData)
        {
            var input00 = File.Exists(esInputData) ? EsInput.Load(esInputData) : EsInput.Parse(esInputData);

            var w = new HidToDirectInput();

            foreach (var dev in input00)
            {
                var sdlGuid = new SdlJoystickGuid(dev.DeviceGUID);
                if (sdlGuid.WrappedTechID != SdlWrappedTechId.HID)
                    continue;

                var vendorId = sdlGuid.VendorId;
                var productId = sdlGuid.ProductId;

                var wr = new HidToDirectInputDevice() { name = dev.DeviceName, vendor = ((int)vendorId).ToString("X4"), product = ((int)productId).ToString("X4") };
                if (w.devices.Any(a => a.vendor == wr.vendor && a.product == wr.product))
                    continue;

                foreach (var hidInput in dev.Input)
                {
                    var dinputInput = input00
                        .Select(a => new { SdlGuid = new SdlJoystickGuid(a.DeviceGUID), Item = a })
                        .Where(a => a.SdlGuid.WrappedTechID == SdlWrappedTechId.DirectInput && a.SdlGuid.VendorId == vendorId && a.SdlGuid.ProductId == productId )
                        .SelectMany(a => a.Item.Input)
                        .FirstOrDefault(ai => ai.Name == hidInput.Name);

                    if (dinputInput != null)
                    {
                        string codeHid = FormatInput(hidInput);
                        string codeDInput = FormatInput(dinputInput);

                        if (codeHid != codeDInput)
                            wr.mappings.Add(new HidToDirectInputMapping() { hid = codeHid, dinput = codeDInput });
                    }
                }

                if (wr.mappings.Any())
                    w.devices.Add(wr);
            }

            return w;
        }

        private List<HidToDirectInputDevice> devices = new List<HidToDirectInputDevice>();
    }

    public class HidToDirectInputDevice
    {
        public HidToDirectInputDevice()
        {
            mappings = new List<HidToDirectInputMapping>();
        }

        public string name { get; set; }
        public string vendor { get; set; }
        public string product { get; set; }
        public List<HidToDirectInputMapping> mappings { get; set; }
    }

    public class HidToDirectInputMapping
    {
        public string hid { get; set; }
        public string dinput { get; set; }
    }


}
