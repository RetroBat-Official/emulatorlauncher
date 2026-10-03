using System.Collections.Generic;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    /*
    public enum FieldType
    {
        Text = 0,
        Numeric = 1,
        Bool = 2,
        Dropdown = 3,
        Slider = 4
    }*/
    public class FieldInformation
    {
        public string CategoryName { get; set; }
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public string FieldType { get; set; }
        public int FieldMin { get; set; }
        public int FieldMax { get; set; }
        public List<string> FieldOptions { get; set; }

        // Keep elements unknown to this model (added by newer TeknoParrot versions) when the profile is saved again
        [XmlAnyElement]
        public System.Xml.XmlElement[] UnknownElements { get; set; }
    }
}
