using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace emulatorLauncher
{
    class Zip
    {
        public static bool IsSevenZipAvailable
        {
            get { return File.Exists(GetSevenZipPath()); }
        }

        static string GetSevenZipPath()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "7za.exe");
        }

        private static System.Reflection.MethodInfo _zipOpenRead;

        // Dotnet 4.0 compatible Zip entries reader ( ZipFile exists since 4.5 )
        public static string[] ListEntries(string path)
        {
            if (!File.Exists(path))
                return new string[] { };

            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".7z" || ext == ".rar")
                return GetSevenZipEntries(path);

            if (ext != ".zip")
                return new string[] { };

            IDisposable zipArchive = null;

            try
            {
                if (_zipOpenRead == null)
                {
                    var afs = System.Reflection.Assembly.Load("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                    if (afs == null)
                        return GetSevenZipEntries(path);

                    var ass = System.Reflection.Assembly.Load("System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                    if (ass == null)
                        return GetSevenZipEntries(path);

                    var zipFile = afs.GetTypes().FirstOrDefault(t => t.Name == "ZipFile");
                    if (zipFile == null)
                        return GetSevenZipEntries(path);

                    _zipOpenRead = zipFile.GetMember("OpenRead", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).FirstOrDefault() as System.Reflection.MethodInfo;
                    if (_zipOpenRead == null)
                        return GetSevenZipEntries(path);
                }
                             
                zipArchive = _zipOpenRead.Invoke(null, new object[] { path }) as IDisposable;
                if (zipArchive == null)
                    return new string[] { };

                List<string> ret = new List<string>();

                var prop = zipArchive.GetType().GetProperty("Entries");

                var entries = prop.GetValue(zipArchive, null) as System.Collections.IEnumerable;
                foreach (var entry in entries)
                {
                    string fullName = entry.GetType().GetProperty("FullName").GetValue(entry, null) as string;
                    if (!string.IsNullOrEmpty(fullName))
                        ret.Add(fullName);
                }

                return ret.ToArray();
            }
            catch
            {
                return GetSevenZipEntries(path);
            }
            finally
            {
                if (zipArchive != null)
                    zipArchive.Dispose();               
            }
        }


        private static Regex _listArchiveRegex = new Regex(@"^(\d{2,4}-\d{2,4}-\d{2,4})\s+(\d{2}:\d{2}:\d{2})\s+(.{5})\s+(\d+)\s+(\d+)?\s+(.+)");

        private static string[] GetSevenZipEntries(string archive)
        {
            var sevenZip = GetSevenZipPath();
            if (!File.Exists(sevenZip))
                return new string[] { };

            string output = Tools.Misc.RunWithOutput(GetSevenZipPath(), "l \"" + archive + "\"");
            if (output == null)
                return new string[] { };

            int num = 0;

            List<string> ret = new List<string>();

            foreach (string str in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (str.StartsWith("---"))
                    num++;
                else if (_listArchiveRegex.IsMatch(str) && num == 1)
                {
                    var matches = _listArchiveRegex.Matches(str);

                    List<string> groups = matches[0]
                        .Groups.Cast<Group>()
                        .Select(x => x.Value)
                        .ToList();

                    if (groups.Count == 7)
                        ret.Add(groups[6]);
                }
            }

            return ret.ToArray();
        }

        public static void Extract(string archive, string destination, string fileNameToExtract = null)
        {
            var sevenZip = GetSevenZipPath();
            if (!File.Exists(sevenZip))
                return;

            string args = "x \"" + archive + "\" -y -o\"" + destination + "\"";
            if (!string.IsNullOrEmpty(fileNameToExtract))
                args = "e \"" + archive + "\" \""+fileNameToExtract+"\" -y -o\"" + destination + "\"";

            var px = new ProcessStartInfo()
            {
                FileName = GetSevenZipPath(),
                WorkingDirectory = Path.GetDirectoryName(GetSevenZipPath()),
                Arguments = args,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(px).WaitForExit();
        }
    }
}
