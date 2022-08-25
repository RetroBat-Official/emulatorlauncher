using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace batocera_store
{
    [XmlType("packages")]
    [XmlRoot("packages")]
    public class StorePackages
    {
        [XmlElement("package")]
        public Package[] Packages { get; set; }
    }

}
