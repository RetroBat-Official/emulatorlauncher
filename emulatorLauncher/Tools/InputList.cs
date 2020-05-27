using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;
using System.Management;
using System.Text.RegularExpressions;
using System.Globalization;
using SharpDX.DirectInput;

namespace emulatorLauncher.Tools
{
    [XmlRoot("inputList")]
    [XmlType("inputList")]
    public class InputList
    {
        public static InputConfig[] Load(string xmlFile)
        {
            if (!File.Exists(xmlFile))
                return null;

            try
            {
                InputList ret = Misc.FromXml<InputList>(xmlFile);
                if (ret != null)
                    return ret.InputConfigs.ToArray();
            }
            catch(Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.Message);
#endif
                SimpleLogger.Instance.Error("InputList error : " + ex.Message);                
            }

            return null;
        }

        [XmlElement("inputConfig")]
        public List<InputConfig> InputConfigs { get; set; }
    }

    public class InputConfig
    {
        public override string ToString()
        {
            return DeviceName;
        }

        [XmlIgnore]
        public Guid ProductGuid
        {
            get
            {
                return FromEmulationStationGuidString(DeviceGUID);
            }
        }

        public static System.Guid FromEmulationStationGuidString(string esGuidString)
        {
            if (esGuidString.Length == 32)
            {
                string guid =
                    esGuidString.Substring(6, 2) +
                    esGuidString.Substring(4, 2) +
                    esGuidString.Substring(2, 2) +
                    esGuidString.Substring(0, 2) +
                    "-" +
                    esGuidString.Substring(10, 2) +
                    esGuidString.Substring(8, 2) +
                    "-" +
                    esGuidString.Substring(14, 2) +
                    esGuidString.Substring(12, 2) +
                    "-" +
                    esGuidString.Substring(16, 4) +
                    "-" +
                    esGuidString.Substring(20);

                try { return new System.Guid(guid); }
                catch { }
            }

            return Guid.Empty;
        }


        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("deviceName")]
        public string DeviceName { get; set; }

        [XmlAttribute("deviceGUID")]
        public string DeviceGUID { get; set; }

        [XmlElement("input")]
        public List<Input> Input { get; set; }

        [XmlIgnore]
        public Input this[InputKey key]
        {
            get
            {
                return Input.FirstOrDefault(i => i.Name == key);
            }
        }

        bool IsXInputDevice(string vendorId, string productId)
        {
            var ParseIds = new Regex(@"([VP])ID_([\da-fA-F]{4})");
            // Used to grab the VID/PID components from the device ID string.                
            // Iterate over all PNP devices.                

            using (var QueryPnp = new ManagementObjectSearcher(@"\\.\root\cimv2", string.Format("Select * FROM Win32_PNPEntity"), new EnumerationOptions() { BlockSize = 20 }))
            {
                foreach (var PnpDevice in QueryPnp.Get())
                {
                    // Check if the DeviceId contains the tell-tale "IG_".                        
                    var DeviceId = (string)PnpDevice.Properties["DeviceID"].Value;


                    if (DeviceId.Contains("IG_"))
                    {
                        // Check the VID/PID components against the joystick's.                            
                        var Ids = ParseIds.Matches(DeviceId);
                        if (Ids.Count == 2)
                        {
                            ushort? VId = null, PId = null;
                            foreach (Match M in Ids)
                            {
                                ushort Value = ushort.Parse(M.Groups[2].Value, NumberStyles.HexNumber);
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
            }
            return false;
        }

        private bool? _isXinput;

        public bool IsXInputDevice()
        {
            if (_isXinput.HasValue)
                return _isXinput.Value;

            if (DeviceGUID == null || DeviceGUID.Length < 32 || !DeviceGUID.StartsWith("03000000"))
                _isXinput = false;
            else
            {
                string vendorId = (DeviceGUID.Substring(10, 2) + DeviceGUID.Substring(8, 2)).ToUpper();
                string productId = (DeviceGUID.Substring(18, 2) + DeviceGUID.Substring(16, 2)).ToUpper();

                _isXinput = IsXInputDevice(vendorId, productId);
            }

            return _isXinput.Value;
        }

        public Guid GetJoystickInstanceGuid()
        {
            var info = GetDirectInputInfo();
            if (info != null)
                return info.InstanceGuid;

            return Guid.Empty;
        }

        public class DirectInputInfo
        {
            public string Name { get; set; }
            public Guid InstanceGuid { get; set; }
            public Guid ProductGuid { get; set; }
            public bool IsXInput { get; set; }
        }

        public DirectInputInfo GetDirectInputInfo()
        {
            if (this.Type == "keyboard")
                return null;

            if (string.IsNullOrEmpty(DeviceGUID))
                return null;

            try
            {
                using (DirectInput directInput = new DirectInput())
                {
                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly))
                    {
                        var ret = TestDirectInputDevice(deviceInstance);
                        if (ret != null)
                            return ret;
                    }

                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly))
                    {
                        var ret = TestDirectInputDevice(deviceInstance);
                        if (ret != null)
                            return ret;
                    }
                }
            }
            catch { }

            return null;
        }

        private DirectInputInfo TestDirectInputDevice(DeviceInstance deviceInstance)
        {
            string vendorId = (this.DeviceGUID.Substring(10, 2) + this.DeviceGUID.Substring(8, 2)).ToUpper();
            string productId = (this.DeviceGUID.Substring(18, 2) + this.DeviceGUID.Substring(16, 2)).ToUpper();

            string guidString = deviceInstance.ProductGuid.ToString().Replace("-", "");
            if (guidString.EndsWith("504944564944"))
            {
                string dxproductId = guidString.Substring(0, 4).ToUpper();
                string dxvendorId = guidString.Substring(4, 4).ToUpper();

                if (vendorId == dxvendorId && productId == dxproductId)
                {
                    DirectInputInfo info = new DirectInputInfo();
                    info.Name = deviceInstance.InstanceName;
                    info.ProductGuid = deviceInstance.ProductGuid;
                    info.InstanceGuid = deviceInstance.InstanceGuid;
                    info.IsXInput = true;
                    return info;
                }
            }
            else if (this.ProductGuid == deviceInstance.ProductGuid || this.ProductGuid == deviceInstance.InstanceGuid)
            {
                DirectInputInfo info = new DirectInputInfo();
                info.Name = deviceInstance.InstanceName;
                info.ProductGuid = deviceInstance.ProductGuid;
                info.InstanceGuid = deviceInstance.InstanceGuid;
                info.IsXInput = true;
                return info;
            }

            return null;
        }

        public Input ToSdlCode(InputKey key)
        {
            Input input = this[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;
            
            var mapping = SdlGameControllers.GetGameControllerMapping(ProductGuid);
            if (mapping == null)
                return input;
            
            var sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Input.Value == input.Value && m.Input.Id == input.Id);
            if (sdlret == null)
                return input;

            Input ret = new Input() { Name = input.Name };
           
            if (sdlret.Button != SDL_CONTROLLER_BUTTON.INVALID)
            {
                ret.Type = "button";
                ret.Value = 1;
                ret.Id = (int)sdlret.Button;
                return ret;
            }
            
            if (sdlret.Axis != SDL_CONTROLLER_AXIS.INVALID)
            {
                ret.Type = "axis";
                ret.Id = (int)sdlret.Axis;
                ret.Value = 1;
                return ret;
            }

            return ToXInputCodes(key);
        }

        /// <summary>
        /// Translate XInput to DirectInput calls
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Input ToXInputCodes(InputKey key)
        {
            Input input = this[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            if (!IsXInputDevice())
                return input;

            Input ret = new Input();
            ret.Name = input.Name;
            ret.Type = input.Type;
            ret.Id = input.Id;
            ret.Value = input.Value;

            // Inverstion de start et select
            if (input.Type == "button" && input.Id == 6)
                ret.Id = 7;
            else if (input.Type == "button" && input.Id == 7)
                ret.Id = 6;

            if (input.Type == "axis")
            {
                // Axe 1 (up/down) inversé
                if (ret.Id == 1)
                    ret.Value = -ret.Value;
                else if (ret.Id == 2) // Axe 2 (l2) devient axe 4
                    ret.Id = 4;
                else if (ret.Id == 3) // Analog right left/right
                    ret.Id--;
                else if (ret.Id == 4) // Analog right up/down
                {
                    ret.Id = 3;
                    ret.Value = -ret.Value;
                }                
            }
         
            return ret;
        }

        public XINPUTMAPPING GetXInputMapping(InputKey key, bool revertAxis = false)
        {
            Input input = this[key];
            if (input == null)
                return XINPUTMAPPING.UNKNOWN;

            if (input.Type == "key")
                return XINPUTMAPPING.UNKNOWN;

            if (!IsXInputDevice())
                return XINPUTMAPPING.UNKNOWN;
            
            if (input.Type == "button")
                return (XINPUTMAPPING)input.Id;

            if (input.Type == "hat")
                return (XINPUTMAPPING) (input.Value + 10);

            if (input.Type == "axis")
            {
                switch (input.Id)
                {
                    case 2:
                        return XINPUTMAPPING.LEFTTRIGGER;

                    case 5:
                        return XINPUTMAPPING.RIGHTTRIGGER;
                    
                    case 0:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.LEFTANALOG_RIGHT;

                        return XINPUTMAPPING.LEFTANALOG_LEFT;

                    case 1:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.LEFTANALOG_DOWN;

                        return XINPUTMAPPING.LEFTANALOG_UP;

                    case 4:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.RIGHTANALOG_DOWN;

                        return XINPUTMAPPING.RIGHTANALOG_UP;

                    case 3:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.RIGHTANALOG_RIGHT;

                        return XINPUTMAPPING.RIGHTANALOG_LEFT;
                }
            }

            return XINPUTMAPPING.UNKNOWN;
        }

    }

    public class Input
    {
        [XmlAttribute("name")]
        public InputKey Name { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("id")]
        public long Id { get; set; }

        [XmlAttribute("value")]
        public long Value { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
                   
            if ((int) Name != 0)
                sb.Append(" name:" + Name);

            if (Type != null)
                sb.Append(" type:" + Type);

            sb.Append(" id:" + Id);            
            sb.Append(" value:" + Value);

            return sb.ToString().Trim();
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj))
                return true;
            
            Input src = obj as Input;
            if (src == null)
                return false;

            return Name == src.Name && Id == src.Id && Value == src.Value && Type == src.Type;            
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [Flags]
    public enum InputKey
    {        
        // batocera ES compatibility
        hotkey = 8,
        pageup = 512,
        pagedown = 131072,
        l2 = 1024,
        r2 = 262144,
        l3 = 2048,
        r3 = 524288,

        a = 1,
        b = 2,
        down = 4,
        hotkeyenable = 8,
        left = 16,
        leftanalogdown = 32,
        leftanalogleft = 64,
        leftanalogright = 128,
        leftanalogup = 256,
        leftshoulder = 512,
        leftthumb = 1024,
        lefttrigger = 2048,
        right = 4096,
        rightanalogup = 8192,
        rightanalogdown = 16384,
        rightanalogleft = 32768,
        rightanalogright = 65536,
        rightshoulder = 131072,
        rightthumb = 262144,
        righttrigger = 524288,
        select = 1048576,
        start = 2097152,
        up = 4194304,
        x = 8388608,
        y = 16777216,



        joystick1left = 64,
        joystick1up = 256,
        joystick2left = 32768,
        joystick2up = 8192

    }
    
    public enum XINPUTMAPPING
    {
        UNKNOWN = -1,

        A = 0,
        B = 1,
        Y = 2,
        X = 3,        
        LEFTSHOULDER = 4,
        RIGHTSHOULDER = 5,

        BACK = 6,
        START = 7,

        LEFTSTICK = 8,
        RIGHTSTICK = 9,
        GUIDE = 10,

        DPAD_UP = 11,
        DPAD_RIGHT = 12,
        DPAD_DOWN = 14,
        DPAD_LEFT = 18,

        LEFTANALOG_UP = 21,
        LEFTANALOG_RIGHT = 22,
        LEFTANALOG_DOWN = 24,
        LEFTANALOG_LEFT = 28,

        RIGHTANALOG_UP = 31,
        RIGHTANALOG_RIGHT = 32,
        RIGHTANALOG_DOWN = 34,
        RIGHTANALOG_LEFT = 38,

        RIGHTTRIGGER = 51,
        LEFTTRIGGER = 52
    }


    enum XINPUT_GAMEPAD
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        LEFTSHOULDER = 4,
        RIGHTSHOULDER = 5,

        BACK = 6,
        START = 7,

        LEFTSTICK = 8,
        RIGHTSTICK = 9,
        GUIDE = 10
    }

    enum XINPUT_HATS
    {
        DPAD_UP = 1,
        DPAD_RIGHT = 2,
        DPAD_DOWN = 4,
        DPAD_LEFT = 8
    }

    enum SDL_CONTROLLER_BUTTON
    {
        INVALID = -1,

        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        BACK = 4,
        GUIDE = 5,
        START = 6,
        LEFTSTICK = 7,
        RIGHTSTICK = 8,
        LEFTSHOULDER = 9,
        RIGHTSHOULDER = 10,
        DPAD_UP = 11,
        DPAD_DOWN = 12,
        DPAD_LEFT = 13,
        DPAD_RIGHT = 14
    };

    enum SDL_CONTROLLER_AXIS
    {
        INVALID = -1,

        LEFTX = 0,
        LEFTY = 1,
        RIGHTX = 2,
        RIGHTY = 3,
        TRIGGERLEFT = 4,
        TRIGGERRIGHT = 5
    }

}
