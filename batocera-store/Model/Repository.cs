using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace batocera_store
{
    public class Repository
    {
        public override string ToString()
        {
            return Name.ToString();
        }

        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("url")]
        public string Url { get; set; }
    }
}
