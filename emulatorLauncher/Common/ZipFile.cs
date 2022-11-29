using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.ComponentModel;
//using emulatorLauncher.Tools;

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

        public static string GetSevenZipPath()
        {
            string fn = Path.Combine(Path.GetDirectoryName(typeof(Zip).Assembly.Location), "7z.exe");
            if (File.Exists(fn))
                return fn;

            return Path.Combine(Path.GetDirectoryName(typeof(Zip).Assembly.Location), "7za.exe");
        }

        public static string GetRdSquashFSPath()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(Zip).Assembly.Location), "rdsquashfs.exe");
        }
        
        public static bool IsFreeDiskSpaceAvailableForExtraction(string filename, string pathForExtraction)
        {
            try
            {
                var totalRequiredSize = Zip.ListEntries(filename).Sum(f => f.Length);
                if (totalRequiredSize == 0)
                    totalRequiredSize = new FileInfo(filename).Length;

                long freeSpaceOnDrive = new DriveInfo(Path.GetPathRoot(pathForExtraction)).AvailableFreeSpace;
                if (freeSpaceOnDrive < totalRequiredSize)
                    return false;
            }
            catch { }

            return true;
        }

        private static System.Reflection.MethodInfo _zipOpenRead;

        // Dotnet 4.0 compatible Zip entries reader ( ZipFile exists since 4.5 )
        public static ZipEntry[] ListEntries(string path)
        {
            if (!IsCompressedFile(path))
                return new ZipEntry[] { };

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
                    return new ZipEntry[] { };

                List<ZipEntry> ret = new List<ZipEntry>();

                var entries = zipArchive.GetType().GetValue<System.Collections.IEnumerable>(zipArchive, "Entries");

                foreach (var entry in entries)
                {
                    var zipArchiveEntry = entry.GetType();

                    string fullName = zipArchiveEntry.GetValue<string>(entry, "FullName");
                    if (!string.IsNullOrEmpty(fullName))
                    {
                        ZipEntry e = new ZipEntry();
                        e.Filename = fullName;
                        e.Length = zipArchiveEntry.GetValue<long>(entry, "Length");
                        e.LastModified = zipArchiveEntry.GetValue<DateTimeOffset>(entry, "LastWriteTime").DateTime;

                        if (fullName.EndsWith("/"))
                        {
                            e.Filename = e.Filename.Substring(0, e.Filename.Length - 1);
                            e.IsDirectory = true;
                        }

                        ret.Add(e);
                    }
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

        public class SquashFsEntry : ZipEntry
        {
            private string _arch;

            public SquashFsEntry(string arch)
            {
                _arch = arch;
                base.Length = -1;
            }

            public override long Length
            {
                get
                {
                    if (base.Length == -1)
                    {
                        if (IsDirectory)
                            base.Length = 0;
                        else
                        {
                            string lineOutput = ProcessExtensions.RunWithOutput(GetRdSquashFSPath(), "-s \"" + Filename + "\" \"" + _arch + "\"");

                            var fs = lineOutput.ExtractString("File size: ", "\r");

                            long len;
                            if (long.TryParse(fs, out len))
                                base.Length = len;
                            else
                                base.Length = 0;
                        }
                    }

                    return base.Length;
                }
                set
                {
                    base.Length = value;
                }
            }
        }

        private static ZipEntry[] GetSquashFsEntries(string archive)
        {
            var sevenZip = GetRdSquashFSPath();
            if (!File.Exists(sevenZip))
                return new ZipEntry[] { };

            string output = ProcessExtensions.RunWithOutput(GetRdSquashFSPath(), "-d \"" + archive + "\"");
            if (output == null)
                return new ZipEntry[] { };

            List<ZipEntry> ret = new List<ZipEntry>();

            foreach (string str in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var args = str.SplitCommandLine();
                if (args.Length < 5)
                    continue;

                if (args[0] != "file" && args[0] != "dir")
                    continue;

                ZipEntry e = new SquashFsEntry(archive);
                e.Filename = args[1];
                e.IsDirectory = args[0] == "dir";
                if (e.IsDirectory)
                    e.Length = 0;

                if (args.Length >= 6)
                {
                    long lastModifiedSpan;
                    if (long.TryParse(args[5], out lastModifiedSpan))
                    {
                        var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        dt = dt.AddSeconds(lastModifiedSpan);
                        e.LastModified = dt;
                    }

                    e.Length = 0;
                }

                if (args.Length >= 7)
                {
                    long len;
                    if (long.TryParse(args[6], out len))
                        e.Length = len;
                }

                ret.Add(e);
            }

            return ret.ToArray();
        }

        private static Regex _listArchiveRegex = new Regex(@"^(\d{2,4}-\d{2,4}-\d{2,4})\s+(\d{2}:\d{2}:\d{2})\s+(.{5})\s+(\d+)\s+(\d+)?\s+(.+)");

        private static ZipEntry[] GetSevenZipEntries(string archive)
        {
            var sevenZip = GetSevenZipPath();
            if (!File.Exists(sevenZip))
                return new ZipEntry[] { };

            string output = ProcessExtensions.RunWithOutput(GetSevenZipPath(), "l \"" + archive + "\"");
            if (output == null)
                return new ZipEntry[] { };

            int num = 0;

            List<ZipEntry> ret = new List<ZipEntry>();

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
                    {
                        ZipEntry e = new ZipEntry();
                        e.Filename = groups[6];
                        e.IsDirectory = groups[3].Contains("D");

                        DateTime date;
                        if (DateTime.TryParse(groups[1] + " " + groups[2], out date))
                            e.LastModified = date;

                        long len;
                        if (long.TryParse(groups[4], out len))
                            e.Length = len;

                        ret.Add(e);
                    }
                }
            }

            return ret.ToArray();
        }
   
        static void ExtractSquashFS(string archive, string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null)
        {
            var rdsquashfs = GetRdSquashFSPath();
            if (!File.Exists(rdsquashfs))
                return;

            var entries = new HashSet<string>(GetSquashFsEntries(archive).Where(e => !e.IsDirectory).Select(e => e.Filename));
            if (entries == null || entries.Count == 0)
                return;

            if (fileNameToExtract == null)
            {
                try { Directory.Delete(destination, true); }
                catch { }

                Directory.CreateDirectory(destination);
            }
            else
            {
                destination = Path.GetDirectoryName(Path.Combine(destination, fileNameToExtract));
                if (!Directory.Exists(destination))
                    Directory.CreateDirectory(destination);
            }

            string args = "--no-dev --no-sock --no-fifo --no-slink -u /. \"" + archive + "\"";
            if (!string.IsNullOrEmpty(fileNameToExtract))
                args = "--no-dev --no-sock --no-fifo --no-slink -u \"" + fileNameToExtract.Replace("\\", "/") + "\" \"" + archive + "\"";

            if (progress == null)
                args = "-q " + args;

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

        public static void Extract(string archive, string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null, bool keepFolder = false)
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
            {
                if (keepFolder)
                    args = "x -bsp1 \"" + archive + "\" \"" + fileNameToExtract + "\" -y -o\"" + destination + "\"";
                else
                    args = "e -bsp1 \"" + archive + "\" \"" + fileNameToExtract + "\" -y -o\"" + destination + "\"";
            }

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


        public static void CleanupUncompressedWSquashFS(string zipFile, string uncompressedPath)
        {
            if (Path.GetExtension(zipFile).ToLowerInvariant() != ".wsquashfs")
                return;

            string[] pathsToDelete = new string[]
                {
                    "dosdevices",
                    "system.reg",
                    "userdef.reg",
                    "user.reg",
                    ".update-timestamp",
                    "drive_c\\windows",
                    "drive_c\\Program Files\\Common Files\\System",
                    "drive_c\\Program Files\\Common Files\\Microsoft Shared",
                    "drive_c\\Program Files\\Internet Explorer",
                    "drive_c\\Program Files\\Windows Media Player",
                    "drive_c\\Program Files\\Windows NT",
                    "drive_c\\Program Files (x86)\\Common Files\\System",
                    "drive_c\\Program Files (x86)\\Common Files\\Microsoft Shared",
                    "drive_c\\Program Files (x86)\\Internet Explorer",
                    "drive_c\\Program Files (x86)\\Windows Media Player",
                    "drive_c\\Program Files (x86)\\Windows NT",
                    "drive_c\\users\\Public",
                    "drive_c\\ProgramData\\Microsoft"
                };

            foreach (var path in pathsToDelete)
            {
                string folder = Path.Combine(uncompressedPath, path);
                if (Directory.Exists(folder))
                {
                    try { Directory.Delete(folder, true); }
                    catch { }
                }
                else if (File.Exists(folder))
                {
                    try { File.Delete(folder); }
                    catch { }
                }

                try
                {
                    var parent = Path.GetDirectoryName(folder);
                    if (Directory.Exists(parent))
                        Directory.Delete(parent);
                }
                catch { }
            }
        }

    }

    class ZipEntry
    {
        public string Filename { get; set; }
        public bool IsDirectory { get; set; }
        
        virtual public long Length { get; set; }
        virtual public DateTime LastModified { get; set; }
 
        public override string ToString()
        {
            return Filename;
        }
    }

}
