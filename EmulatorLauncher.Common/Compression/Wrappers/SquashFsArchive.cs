using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher.Common.Compression.Wrappers
{
    public class SquashFsArchive : IArchive
    {
        public static bool IsSquashFsAvailable
        {
            get { return File.Exists(GetRdSquashFSPath()); }
        }

        public static string GetRdSquashFSPath()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(Zip).Assembly.Location), "rdsquashfs.exe");
        }

        private SquashFsArchive() { }

        public string FileName { get; private set; }

        internal static SquashFsArchive Open(string path)
        {
            if (!IsSquashFsAvailable)
                return null;

            return new SquashFsArchive() { FileName = path };
        }

        private IArchiveEntry[] ListEntriesInternal()
        {
                var sevenZip = GetRdSquashFSPath();
                if (!File.Exists(sevenZip))
                    return new IArchiveEntry[] { };

                string output = ProcessExtensions.RunWithOutput(GetRdSquashFSPath(), "-d \"" + FileName + "\"");
                if (output == null)
                    return new IArchiveEntry[] { };

                var ret = new List<IArchiveEntry>();

                foreach (string str in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var args = str.SplitCommandLine();
                    if (args.Length < 5)
                        continue;

                    if (args[0] != "file" && args[0] != "dir")
                        continue;

                    SquashFsArchiveEntry e = new SquashFsArchiveEntry(this);
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

        private IArchiveEntry[] _cache;

        public IArchiveEntry[] Entries
        {
            get
            {
                if (_cache == null)
                    _cache = ListEntriesInternal();

                return _cache;
            }
        }

        public void Extract(string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null, ArchiveExtractionMode mode = ArchiveExtractionMode.Normal)
        {
            var rdsquashfs = GetRdSquashFSPath();
            if (!File.Exists(rdsquashfs))
                return;

            if (mode != ArchiveExtractionMode.Normal)
                throw new ApplicationException("Unsupported extraction mode");

            HashSet<string> entries = null;

            if (progress != null && fileNameToExtract == null)
            {
                entries = new HashSet<string>(this.Entries.Where(e => !e.IsDirectory).Select(e => e.Filename));
                if (entries == null || entries.Count == 0)
                    return;
            }

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

            string args = "--no-dev --no-sock --no-fifo --no-slink -u /. \"" + FileName + "\"";
            if (!string.IsNullOrEmpty(fileNameToExtract))
                args = "--no-dev --no-sock --no-fifo --no-slink -u \"" + fileNameToExtract.Replace("\\", "/") + "\" \"" + FileName + "\"";

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
            if (proc != null && progress != null && entries != null)
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

        public void Dispose()
        {

        }
    }

    public class SquashFsArchiveEntry : IArchiveEntry
    {
        internal SquashFsArchiveEntry(SquashFsArchive archive)
        {
            _archive = archive;
        }

        private SquashFsArchive _archive;

        public string Filename { get; internal set; }
        public bool IsDirectory { get; internal set; }
        public DateTime LastModified { get; internal set; }

        private long _len = -1;

        public long Length 
        {
            get
            {
                if (_len == -1)
                {
                    if (IsDirectory)
                        _len = 0;
                    else
                    {
                        string lineOutput = ProcessExtensions.RunWithOutput(SquashFsArchive.GetRdSquashFSPath(), "-s \"" + Filename + "\" \"" + _archive.FileName + "\"");

                        var fs = lineOutput.ExtractString("File size: ", "\r");

                        long len;
                        if (long.TryParse(fs, out len))
                            _len = len;
                        else
                            _len = 0;
                    }
                }

                return _len;
            }
            set
            {
                _len = value;
            }
        }

        public uint Crc32 { get; internal set; }
        public string HexCrc { get; internal set; }

        public void Extract(string directory)
        {
            _archive.Extract(directory, Filename);
        }
    }
}
