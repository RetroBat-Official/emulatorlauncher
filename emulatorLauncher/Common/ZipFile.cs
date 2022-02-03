using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.ComponentModel;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class Zip
    {
        public static bool IsCompressedFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName) || Directory.Exists(fileName))
                return false;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".zip" || ext == ".7z" || ext == ".rar" || ext.Contains("squashfs");
        }

        public static bool IsSevenZipAvailable
        {
            get { return File.Exists(GetSevenZipPath()); }
        }

        static string GetSevenZipPath()
        {
            string fn = Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "7z.exe");
            if (File.Exists(fn))
                return fn;
            
            return Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "7za.exe");
        }

        static string GetRdSquashFSPath()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "rdsquashfs.exe");
        }

        private static System.Reflection.MethodInfo _zipOpenRead;

        // Dotnet 4.0 compatible Zip entries reader ( ZipFile exists since 4.5 )
        public static string[] ListEntries(string path)
        {
            if (!IsCompressedFile(path))
                return new string[] { };

            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext.Contains("squashfs") && File.Exists(GetRdSquashFSPath()))
                return GetSquashFsEntries(path);

            if (ext != ".zip")
                return GetSevenZipEntries(path);

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
        
        private static string[] GetSquashFsEntries(string archive)
        {
            var sevenZip = GetRdSquashFSPath();
            if (!File.Exists(sevenZip))
                return new string[] { };

            string output = Tools.Misc.RunWithOutput(GetRdSquashFSPath(), "-d \"" + archive + "\"");
            if (output == null)
                return new string[] { };

            List<string> ret = new List<string>();

            foreach (string str in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var args = str.SplitCommandLine();
                if (args.Length != 5)
                    continue;

                if (args[0] != "file")
                    continue;

                ret.Add(args[1]);
            }

            return ret.ToArray();
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
   
        static void ExtractSquashFS(string archive, string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null)
        {
            var rdsquashfs = GetRdSquashFSPath();
            if (!File.Exists(rdsquashfs))
                return;

            var entries = new HashSet<string>(GetSquashFsEntries(archive));
            if (entries == null || entries.Count == 0)
                return;

            try { Directory.Delete(destination, true); }
            catch { }

            Directory.CreateDirectory(destination);

            string args = "-u /. \"" + archive + "\"";
            if (!string.IsNullOrEmpty(fileNameToExtract))
                args = "-u \"" + fileNameToExtract + "\" \"" + archive + "\"";

            var px = new ProcessStartInfo()
            {
                FileName = rdsquashfs,
                WorkingDirectory = destination,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = (progress != null),
                CreateNoWindow = true
            };

            var proc = Process.Start(px);
            if (proc != null && progress != null)
            {
                var unpacking = new HashSet<string>(entries);

                int totalEntries = entries.Count; // *2;
                int lastpc = 0;

                try
                {
                    while (!proc.StandardOutput.EndOfStream)
                    {
                        string line = proc.StandardOutput.ReadLine();
                        Debug.WriteLine(line);

                        if (line.StartsWith("creating "))
                        {
                            line = line.Substring("creating ".Length).Trim();
                            entries.Remove(line);
                        }

                        if (line.StartsWith("unpacking "))
                        {
                            line = line.Substring("unpacking ".Length).Trim();
                            unpacking.Remove(line);
                        }

                        int pc = ((totalEntries - unpacking.Count) * 100) / totalEntries;
                        if (pc != lastpc)
                        {
                            lastpc = pc;
                            progress(null, new ProgressChangedEventArgs(pc, null));
                        }
                    }
                }
                catch { }
            }

            proc.WaitForExit();

            int code = proc.ExitCode;
            if (code == 2)
                throw new ApplicationException("Cannot open archive");
        }

        public static void Extract(string archive, string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null)
        {
            var ext = Path.GetExtension(archive).ToLowerInvariant();
            if (ext.Contains("squashfs") && File.Exists(GetRdSquashFSPath()))
            {
                ExtractSquashFS(archive, destination, fileNameToExtract, progress);
                return;
            }

            var sevenZip = GetSevenZipPath();
            if (!File.Exists(sevenZip))
                return;

            string args = "x -bsp1 \"" + archive + "\" -y -o\"" + destination + "\"";
            if (!string.IsNullOrEmpty(fileNameToExtract))
                args = "e -bsp1 \"" + archive + "\" \"" + fileNameToExtract + "\" -y -o\"" + destination + "\"";

            var px = new ProcessStartInfo()
            {
                FileName = GetSevenZipPath(),
                WorkingDirectory = Path.GetDirectoryName(GetSevenZipPath()),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = (progress != null),
                CreateNoWindow = true
            };

            var proc = Process.Start(px);

            if (proc != null && progress != null)
            {
                try
                {
                    StringBuilder sbOutput = new StringBuilder();

                    while (!proc.StandardOutput.EndOfStream)
                    {
                        string line = proc.StandardOutput.ReadLine();
                        if (line.Length >= 4 && line[3] == '%')
                        {
                            int pc;
                            if (int.TryParse(line.Substring(0, 3), out pc))
                                progress(null, new ProgressChangedEventArgs(pc, null));
                        }

                        sbOutput.AppendLine(line);
                    }
                }
                catch { }
            }

            proc.WaitForExit();

            int code = proc.ExitCode;
            if (code == 2)
                throw new ApplicationException("Cannot open archive");
        }
    }
}
