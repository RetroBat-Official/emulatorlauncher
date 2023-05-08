using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Security.Cryptography;

namespace batocera_systems
{
    class Program
    {
        static string[] biospaths = 
            {
                @".\..\bios",
                @".\..\..\bios",
            };

        static string[] rompaths = 
            {
                @".\..\roms",
                @".\..\..\roms",
            };

        static void Main(string[] args)
        {
            string root = null;
            string path = Path.GetDirectoryName(typeof(Program).Assembly.Location);

            foreach (var bp in biospaths)
            {
                string test = Path.GetFullPath(Path.Combine(path, bp));
                if (Directory.Exists(test))
                {
                    root = test;
                    break;
                }
            }

            if (root == null)
                root = Path.GetFullPath(Path.Combine(path, @".\..\bios"));

            string romRoot = null;            

            foreach (var bp in rompaths)
            {
                string test = Path.GetFullPath(Path.Combine(path, bp));
                if (Directory.Exists(test))
                {
                    romRoot = test;
                    break;
                }
            }

            string filter = args.SkipWhile(a => a != "--filter").Skip(1).FirstOrDefault();

            var serializer = new JavaScriptSerializer();
            var deserializedResult = serializer.Deserialize<Dictionary<string, SystemInfo>>(Encoding.UTF8.GetString(Properties.Resources.batocera_systems));

            foreach (var item in deserializedResult)
            {
                if (filter != null && filter != item.Key)
                    continue;

                List<string> lines = new List<string>();

                foreach (var bios in item.Value.biosFiles)
                {
                    string fileName = Path.Combine(root, bios.file.StartsWith("bios/") ? bios.file.Substring(5) : bios.file);
                    if (bios.file.StartsWith("roms/") && romRoot != null)
                        fileName = Path.Combine(romRoot, bios.file.Replace("roms/", ""));
                    
                    if (!File.Exists(fileName))
                        lines.Add("MISSING " + (string.IsNullOrEmpty(bios.md5) ? "-" : bios.md5) + " " + bios.file);
                    else if (!string.IsNullOrEmpty(bios.md5))
                    {
                        using (var md5 = MD5.Create())
                        {
                            using (var stream = File.OpenRead(fileName))
                            {
                                var hash = md5.ComputeHash(stream);
                                string md = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();                                

                                if (md != bios.md5)
                                    lines.Add("INVALID " + (string.IsNullOrEmpty(bios.md5) ? "-" : bios.md5) + " " + bios.file);
                            }
                        }                        
                    }
                }

                if (lines.Any())
                {
                    Console.WriteLine("> " + item.Key);
                    foreach (var line in lines)
                        Console.WriteLine(line);

                }
            }        
        }

        public class SystemInfo
        {
            public string name { get; set; }
            public List<BiosFile> biosFiles { get; set; }

            public override string ToString() { return name; }
        }

        public class BiosFile
        {
            public string md5 { get; set; }
            public string file { get; set; }

            public override string ToString() { return file; }
        }
    }
}
