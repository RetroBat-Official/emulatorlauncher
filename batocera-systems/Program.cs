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
        static void Main(string[] args)
        {
            string path = Path.GetDirectoryName(typeof(Program).Assembly.Location);

            string biosPath = FindPath(path, new string[] { @".\..\bios", @".\..\..\bios" });
            string romsPath = FindPath(path, new string[] { @".\..\roms", @".\..\..\roms" });
            string emulatorsPath = FindPath(path, new string[] { @".\..\emulators", @".\..\..\emulators", @".\..\system\emulators" });

            string filter = args.SkipWhile(a => a != "--filter").Skip(1).FirstOrDefault();

            var serializer = new JavaScriptSerializer();
            var deserializedResult = serializer.Deserialize<Dictionary<string, SystemInfo>>(Encoding.UTF8.GetString(Properties.Resources.batocera_systems));

            foreach (var item in deserializedResult)
            {
                if (filter != null && filter != item.Key)
                    continue;

                var lines = new List<string>();

                foreach (var bios in item.Value.biosFiles)
                {
                    string fileName = Path.GetFullPath(Path.Combine(path, "..", bios.file));

                    if (bios.file.StartsWith("bios/") && biosPath != null)
                        fileName = Path.GetFullPath(Path.Combine(biosPath, bios.file.Substring("bios/".Length)));
                    else if (bios.file.StartsWith("roms/") && romsPath != null)
                        fileName = Path.GetFullPath(Path.Combine(romsPath, bios.file.Substring("roms/".Length)));
                    else if (bios.file.StartsWith("emulators/") && emulatorsPath != null)
                        fileName = Path.GetFullPath(Path.Combine(emulatorsPath, bios.file.Substring("emulators/".Length)));
                    
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

        static string FindPath(string relativeTo, string[] paths)
        {
            foreach (var bp in paths)
            {
                string test = Path.GetFullPath(Path.Combine(relativeTo, bp));
                if (Directory.Exists(test))
                    return test;
            }

            return null;
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
