using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;

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
                    esGuidString.Substring(8, 4) +
                    "-" +
                    esGuidString.Substring(12, 4) +
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
                   
            sb.Append(" name:" + Name);

            if (Type != null)
                sb.Append(" type:" + Type);

            sb.Append(" id:" + Id);            
            sb.Append(" value:" + Value);

            return sb.ToString().Trim();
        }
    }

    [Flags]
    public enum InputKey
    {
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

        // batocera ES compatibility
        hotkey = 8,
        pagedown = 512,
        pageup = 131072,
        l2 = 1024,
        r2 = 262144,
        l3 = 2048,
        r3 = 524288,

        joystick1left = 64,
        joystick1up = 256,
        joystick2left = 32768,
        joystick2up = 8192

    }    
}
