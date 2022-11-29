using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace batocera_store
{
    public class InstalledPackage
    {
        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("repository")]
        public string Repository { get; set; }

        [XmlElement("version")]
        public string Version { get; set; }

        [XmlElement("download_size")]
        public string DownloadSize { get; set; }

        [XmlElement("installed_size")]
        public string InstalledSize { get; set; }

        [XmlElement("installed_files")]
        public InstalledFiles InstalledFiles { get; set; }
    }

    public class InstalledFiles
    {
        [XmlElement("file")]
        public List<string> Files { get; set; }
    }
}
