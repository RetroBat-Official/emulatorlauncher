using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using EmulatorLauncher.Common.FileFormats;

namespace batocera_store
{    
    [XmlType("packages")]
    [XmlRoot("packages")]
    public class InstalledPackages
    {
        public static InstalledPackages FromFile(string path)
        {
            if (File.Exists(path))
            {
                var ret = XmlExtensions.FromXml<InstalledPackages>(path);
                if (ret != null)
                {
                    ret._path = path;
                    return ret;
                }
            }

            return new InstalledPackages() { _path = path };
        }
        
        public void Save()
        {
            if (string.IsNullOrEmpty(_path))
                return;

            string xml = this.ToXml();
            if (!string.IsNullOrEmpty(xml))
                File.WriteAllText(_path, xml);
        }
        
        [XmlElement("package")]
        public List<InstalledPackage> Packages { get; set; }

        private string _path;
    }
}
