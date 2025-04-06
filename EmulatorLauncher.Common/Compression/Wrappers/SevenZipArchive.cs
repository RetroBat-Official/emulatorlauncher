using EmulatorLauncher.Common.Compression.SevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EmulatorLauncher.Common.Compression.Wrappers
{
    /// <summary>
    /// Read 7z files using '7z.dll'
    /// </summary>
    public class SevenZipArchive : IArchive
    {
        public static bool IsSevenZipAvailable
        {
            get { return File.Exists(GetSevenZipPath()); }
        }

        private static string GetSevenZipPath()
        {            
            return Path.Combine(Path.GetDirectoryName(typeof(Zip).Assembly.Location), IntPtr.Size == 8 ? "7z-x64.dll" : "7z.dll");
        }

        private SevenZipArchive() { }

        internal static SevenZipArchive Open(string path)
        {
            if (!IsSevenZipAvailable)
                return null;

            try
            {
                var archive = new ArchiveFile(path, GetSevenZipPath());
                if (archive != null)
                    return new SevenZipArchive() { _sevenZipArchive = archive };
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("Can't open " + path, ex);
            }

            return null;
        }

        private ArchiveFile _sevenZipArchive;

        public IArchiveEntry[] Entries
        {
            get
            {
                return _sevenZipArchive.Entries.Select(e => new SevenZipArchiveEntry(e)).ToArray();
            }
        }

        public void Dispose()
        {
            if (_sevenZipArchive != null)
            {
                _sevenZipArchive.Dispose();
                _sevenZipArchive = null;
            }
        }

        public void Extract(string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null, bool keepFolder = false)
        {
            if (fileNameToExtract != null)
            {
                var e = Entries.FirstOrDefault(ent => ent.Filename == fileNameToExtract);
                if (e != null)
                    e.Extract(destination);

                return;
            }
            
            _sevenZipArchive.Extract(destination, progress, true);
        }
    }

    public class SevenZipArchiveEntry : IArchiveEntry
    {
        private Entry _entry;

        internal SevenZipArchiveEntry(Entry entry)
        {
            _entry = entry;
        }

        public string Filename => _entry.FileName;
        public bool IsDirectory => _entry.IsFolder;
        public DateTime LastModified => _entry.LastWriteTime;
        public long Length => (long) _entry.Size;

        public uint Crc32 => _entry.CRC;
        public string HexCrc { get { return Crc32.ToString("x8"); } }

        public void Extract(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string path = Path.GetFullPath(Path.Combine(directory, Filename));

                if (IsDirectory)
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    return;
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }

                _entry.Extract(path);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("Can't extract " + Filename, ex);
            }
        }

        public override string ToString()
        {
            return Filename;
        }
    }

    #region SevenZipExeArchive
    /// <summary>
    /// Read 7z files using '7z.exe'
    /// </summary>
    [Obsolete]
    public class SevenZipExeArchive : IArchive
    {        
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

        private SevenZipExeArchive() { }

        public static SevenZipExeArchive OpenRead(string path)
        {
            if (!IsSevenZipAvailable)
                return null;

            return new SevenZipExeArchive() { _filePath = path };
        }

        private string _filePath;

        private static Regex _listArchiveRegex = new Regex(@"^(\d{2,4}-\d{2,4}-\d{2,4})\s+(\d{2}:\d{2}:\d{2})\s+(.{5})\s+(\d+)\s+(\d+)?\s+(.+)");

        private IArchiveEntry[] ListEntriesInternal()
        {
            var sevenZip = GetSevenZipPath();
            if (!File.Exists(sevenZip))
                return new IArchiveEntry[] { };

            string output = ProcessExtensions.RunWithOutput(GetSevenZipPath(), "l \"" + _filePath + "\"");
            if (output == null)
                return new IArchiveEntry[] { };

            int num = 0;

            List<IArchiveEntry> ret = new List<IArchiveEntry>();

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
                        SevenZipExeArchiveEntry e = new SevenZipExeArchiveEntry(this);
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

        IArchiveEntry[] _cache;

        public IArchiveEntry[] Entries
        {
            get
            {
                if (_cache == null)
                    _cache = ListEntriesInternal();

                return _cache;
            }
        }

        public void Extract(string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null, bool keepFolder = false)
        {
            var sevenZip = GetSevenZipPath();
            if (!File.Exists(sevenZip))
                return;

            string args = "x -bsp1 \"" + _filePath + "\" -y -o\"" + destination + "\"";
            if (!string.IsNullOrEmpty(fileNameToExtract))
            {
                if (keepFolder)
                    args = "x -bsp1 \"" + _filePath + "\" \"" + fileNameToExtract + "\" -y -o\"" + destination + "\"";
                else
                    args = "e -bsp1 \"" + _filePath + "\" \"" + fileNameToExtract + "\" -y -o\"" + destination + "\"";
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

        public void Dispose() { }
    }

    public class SevenZipExeArchiveEntry : IArchiveEntry
    {
        internal SevenZipExeArchiveEntry(SevenZipExeArchive archive)
        {
            _archive = archive;
        }

        private SevenZipExeArchive _archive;

        public string Filename { get; internal set; }
        public bool IsDirectory { get; internal set; }
        public DateTime LastModified { get; internal set; }
        public long Length { get; internal set; }

        public uint Crc32 { get; internal set; }
        public string HexCrc { get; internal set; }

        public void Extract(string directory)
        {
            _archive.Extract(directory, Filename);
        }
    }
    #endregion
}
