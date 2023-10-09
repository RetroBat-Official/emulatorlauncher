using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using EmulatorLauncher.Common.EmulationStation;

namespace batocera_store
{
    public class Package
    {
        public override string ToString()
        {
            return Name.ToString();
        }
         
        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("repository")]
        public string Repository { get; set; }

        [XmlElement("available_version")]
        public string AvailableVersion { get; set; }

        [XmlElement("description")]
        public string Description { get; set; }

        [XmlElement("url")]
        public string Url { get; set; }

        [XmlElement("packager")]
        public string Packager { get; set; }

        [XmlElement("download_size")]
        public string DownloadSize { get; set; }

        [XmlElement("installed_size")]
        public string InstalledSize { get; set; }

        [XmlElement("status")]
        public string Status { get; set; }

        [XmlElement("licence")]
        public string Licence { get; set; }

        [XmlElement("group")]
        public string Group { get; set; }

        [XmlElement("arch")]        
        public string Arch { get; set; }

        [XmlElement("preview_url")]
        public string PreviewUrl { get; set; }

        [XmlElement("download_url")]
        public string DownloadUrl { get; set; }

        // Online packages
        [XmlElement("game")]
        public List<PackageGame> Games { get; set; }

        [XmlElement("version")]
        public string Version { get; set; }
    }

    [XmlRoot("game")]
    [XmlType("game")]
    public class PackageGame : Game
    {
        [XmlAttribute("system")]
        public string System { get; set; }
    }
}
