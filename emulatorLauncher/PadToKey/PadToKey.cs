using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;
using emulatorLauncher.Tools;
using System.Reflection;

namespace emulatorLauncher.PadToKeyboard
{
    [XmlRoot("padToKey")]
    [XmlType("padToKey")]
    public class PadToKey
    {
        public static PadToKey Load(string xmlFile)
        {
            if (!File.Exists(xmlFile))
                return null;

            try
            {
                PadToKey ret = Misc.FromXml<PadToKey>(xmlFile);
                if (ret != null)
                    return ret;
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.Message);
#endif
                SimpleLogger.Instance.Error("PadToKey error : " + ex.Message);               
            }

            return null;
        }

        [XmlIgnore]
        public PadToKeyApp this[string process]
        {
            get
            {
                if (string.IsNullOrEmpty(process))
                    return null;

                process = process.ToLowerInvariant();
                var ret = Applications.FirstOrDefault(i => i.ProcessNames.Contains(process));
                if (ret != null)
                    return ret;

                return null;
            }
        }

        [XmlElement("app")]
        public List<PadToKeyApp> Applications { get; set; }
    }

    public class PadToKeyApp
    {
        public override string ToString()
        {
            return Name;
        }

        private List<string> _processNames;

        [XmlIgnore]
        public List<string> ProcessNames
        {
            get
            {
                if (_processNames == null)
                {
                    _processNames = new List<string>();

                    if (this.Name != null)
                        foreach (var nm in Name.ToLowerInvariant().Split(new char[] { ',' }))
                            _processNames.Add(nm.Trim());
                }

                return _processNames;
            }
        }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("input")]
        public List<PadToKeyInput> Input { get; set; }

        [XmlIgnore]
        public PadToKeyInput this[InputKey key]
        {
            get
            {
                return Input.FirstOrDefault(i => i.Name == key);
            }
        }

    }

    public class PadToKeyInput
    {
        [XmlAttribute("name")]
        public InputKey Name { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlAttribute("code")]
        public string Code { get; set; }

        [XmlIgnore]
        public Keys Keys
        {
            get
            {
                if (string.IsNullOrEmpty(Key) || Key.StartsWith("{") || Key.StartsWith("("))
                    return Keys.None;

                Keys ret = Keys.None;
                Enum.TryParse(Key, out ret);
                return ret;
            }
        }

        [XmlIgnore]
        public ScanCode ScanCode
        {
            get
            {
                if (string.IsNullOrEmpty(Code))
                    return (ScanCode)0;

                string code = Code.ToLowerInvariant();
                foreach (var fld in typeof(ScanCode).GetFields(BindingFlags.Static | BindingFlags.Public))
                    if (fld.Name.ToLowerInvariant() == code || fld.Name.ToLowerInvariant() == "key_"+ code)
                        return (ScanCode)fld.GetValue(null);
                
                return (ScanCode) 0;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(" name:" + Name);

            if (Key != null)
                sb.Append(" key:" + Key);

            return sb.ToString().Trim();
        }
    }
}
