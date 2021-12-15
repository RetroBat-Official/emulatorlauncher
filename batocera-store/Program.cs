using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using emulatorLauncher;
using System.IO;
using System.Xml.Serialization;

namespace batocera_store
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("usage :");
                Console.WriteLine("  install <package>");
                Console.WriteLine("  remove  <package>");
                Console.WriteLine("  list");
                Console.WriteLine("  list-repositories");
                Console.WriteLine("  clean");
                Console.WriteLine("  clean-all");
                Console.WriteLine("  refresh");
                Console.WriteLine("  update");
                return 0;
            }

            AppConfig = ConfigFile.FromFile(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "emulatorLauncher.cfg"));
            AppConfig.ImportOverrides(ConfigFile.FromArguments(args));
            
            switch (args[0])
            {
                case "install": 
                    Thread.Sleep(1000);
                    Console.WriteLine("OK");
                    break;

                case "list":
                    Console.WriteLine(Properties.Resources.batocera_store);
                    break;
            }           

            return 0;
        }

        public static ConfigFile AppConfig { get; private set; }
    }
    
    [XmlType("packages")]
    [XmlRoot("packages")]
    public class BatoceraStorePackages
    {
        [XmlElement("package")]
        public Package[] Packages { get; set; }
    }

    public class Package
    {
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
    }
}
