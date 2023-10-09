using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using EmulatorLauncher.Common.FileFormats;

namespace batocera_store
{
    [XmlType("repositories")]
    [XmlRoot("repositories")]
    public class RepositoryFactory
    {
        public static Repository[] FromFile(string path)
        {
            var ret = XmlExtensions.FromXml<RepositoryFactory>(path);
            if (ret != null)
                return ret.Items;

            return null;
        }

        [XmlElement("repository")]
        public Repository[] Items { get; set; }
    }
}
