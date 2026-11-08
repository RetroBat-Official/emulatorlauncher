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

                // Files without group are all mandatory
                foreach (var bios in item.Value.biosFiles.Where(b => string.IsNullOrEmpty(b.group)))
                {
                    string status = GetBiosStatus(bios, path, biosPath, romsPath, emulatorsPath);
                    if (status != null)
                        lines.Add(FormatBiosLine(status, bios));
                }

                // Files sharing a group: one existing file is enough
                foreach (var group in item.Value.biosFiles.Where(b => !string.IsNullOrEmpty(b.group)).GroupBy(b => b.group))
                {
                    var results = group.Select(b => new { Bios = b, Status = GetBiosStatus(b, path, biosPath, romsPath, emulatorsPath) }).ToList();

                    // When a file of the group exists, the other files are only alternatives
                    bool groupFound = results.Any(r => r.Status == null || r.Status == "INVALID");

                    foreach (var r in results.Where(r => r.Status != null))
                        lines.Add(FormatBiosLine(groupFound && r.Status == "MISSING" ? "OPTIONAL" : r.Status, r.Bios));
                }

                if (lines.Any())
                {
                    Console.WriteLine("> " + item.Key);

                    foreach (var line in lines)
                        Console.WriteLine(line);
                }
            }        
        }

        // Returns null when the file exists with the right md5, otherwise "MISSING", "OPTIONAL" or "INVALID"
        static string GetBiosStatus(BiosFile bios, string path, string biosPath, string romsPath, string emulatorsPath)
        {
            string fileName = Path.GetFullPath(Path.Combine(path, "..", bios.file));

            if (bios.file.StartsWith("bios/") && biosPath != null)
                fileName = Path.GetFullPath(Path.Combine(biosPath, bios.file.Substring("bios/".Length)));
            else if (bios.file.StartsWith("roms/") && romsPath != null)
                fileName = Path.GetFullPath(Path.Combine(romsPath, bios.file.Substring("roms/".Length)));
            else if (bios.file.StartsWith("emulators/") && emulatorsPath != null)
                fileName = Path.GetFullPath(Path.Combine(emulatorsPath, bios.file.Substring("emulators/".Length)));

            if (!File.Exists(fileName))
                return bios.optional ? "OPTIONAL" : "MISSING";

            if (string.IsNullOrEmpty(bios.md5))
                return null;

            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(fileName))
            {
                string md = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return md == bios.md5 ? null : "INVALID";
            }
        }

        static string FormatBiosLine(string status, BiosFile bios)
        {
            return status + " " + (string.IsNullOrEmpty(bios.md5) ? "-" : bios.md5) + " " + bios.file;
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
            public string group { get; set; }
            public bool optional { get; set; }
            public override string ToString() { return file; }
        }
    }
}
