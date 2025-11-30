using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace EmulatorLauncher.Common.Compression.Wrappers
{
    public class ZipArchiveFileEntry : IArchiveEntry
    {
        private object _entry;

        public ZipArchiveFileEntry(object zipArchiveEntry)
        {
            _entry = zipArchiveEntry;
        }

        public System.IO.Stream Open()
        {
            object obj = _entry.GetType().GetMethod("Open").Invoke(_entry, new object[] { });
            return obj as Stream;
        }

        public void Extract(string directory)
        {
            Extract(directory, null);
        }

        internal void Extract(string directory, string path)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (path == null)
                path = Path.GetFullPath(Path.Combine(directory, Filename));

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

            using (var stream = Open())
            {
                if (stream == null)
                    return;

                bool canRetry = true;

                FileStream fileStream = null;

            retry:
                try
                {
                    fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                }
                catch (IOException ex)
                {
                    int errorCode = Marshal.GetHRForException(ex) & ((1 << 16) - 1);
                    if (canRetry && (errorCode == 32 || errorCode == 33))
                    {
                        canRetry = false;

                        try
                        {
                            string newName = Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path) + "_");
                            if (File.Exists(newName))
                                File.Delete(newName);

                            File.Move(path, newName);
                            goto retry;
                        }
                        catch { throw ex; }
                    }
                }

                if (fileStream != null)
                {
                    stream.CopyTo(fileStream);
                    fileStream.Dispose();
                }

                stream.Close();
            }
        }

        public string Filename { get; set; }
        public bool IsDirectory { get; set; }
        public long Length { get; set; }
        public DateTime LastModified { get; set; }
        public uint Crc32 { get; set; }

        public string HexCrc
        {
            get { return Crc32.ToString("x8"); }
        }

        public override string ToString()
        {
            return Filename;
        }
    }

    public class ZipArchive : IDisposable, IArchive
    {
        static void EnsureAssembly()
        {
            if (_zipCreateFromDirectory != null && _zipOpenRead != null)
                return;

            var afs = System.Reflection.Assembly.Load("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            if (afs == null)
                throw new Exception("Framework not supported");

            var ass = System.Reflection.Assembly.Load("System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            if (ass == null)
                throw new Exception("Framework not supported");

            var zipFile = afs.GetTypes().FirstOrDefault(t => t.Name == "ZipFile");
            if (zipFile == null)
                throw new Exception("Framework not supported");

            _zipCreateFromDirectory = zipFile.GetMember("CreateFromDirectory", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).FirstOrDefault() as System.Reflection.MethodInfo;
            if (_zipCreateFromDirectory == null)
                throw new Exception("Framework not supported");

            _zipOpenRead = zipFile.GetMember("OpenRead", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).FirstOrDefault() as System.Reflection.MethodInfo;
            if (_zipOpenRead == null)
                throw new Exception("Framework not supported");
        }

        private static System.Reflection.MethodInfo _zipCreateFromDirectory;

        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
        {
            EnsureAssembly();
            _zipCreateFromDirectory.Invoke(null, new object[] { sourceDirectoryName, destinationArchiveFileName });
        }

        private static System.Reflection.MethodInfo _zipOpenRead;

        private IDisposable _zipArchive;

        internal static ZipArchive Open(string path)
        {
            EnsureAssembly();
            IDisposable zipArchive = _zipOpenRead.Invoke(null, new object[] { path }) as IDisposable;
            if (zipArchive == null)
                return null;

            ZipArchive ret = new ZipArchive();
            ret._zipArchive = zipArchive;
            return ret;
        }

        public IArchiveEntry[] Entries
        {
            get
            {
                var ret = new List<IArchiveEntry>();

                var entries = _zipArchive.GetType().GetValue<System.Collections.IEnumerable>(_zipArchive, "Entries");
                foreach (var entry in entries)
                {
                    var zipArchiveEntry = entry.GetType();

                    string fullName = zipArchiveEntry.GetValue<string>(entry, "FullName");
                    if (string.IsNullOrEmpty(fullName))
                        continue;

                    ZipArchiveFileEntry e = new ZipArchiveFileEntry(entry);
                    e.Filename = fullName;
                    e.Length = zipArchiveEntry.GetValue<long>(entry, "Length");
                    e.LastModified = zipArchiveEntry.GetValue<DateTimeOffset>(entry, "LastWriteTime").DateTime;
                    e.Crc32 = zipArchiveEntry.GetFieldValue<uint>(entry, "_crc32");

                    if (fullName.EndsWith("/"))
                    {
                        e.Filename = e.Filename.Substring(0, e.Filename.Length - 1);
                        e.IsDirectory = true;
                    }

                    ret.Add(e);
                }

                return ret.ToArray();
            }
        }

        public void Dispose()
        {
            if (_zipArchive != null)
                _zipArchive.Dispose();
        }

        public void Extract(string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null, ArchiveExtractionMode mode = ArchiveExtractionMode.Normal)
        {
            var entries = this.Entries;

            int idx = 0;
            foreach (var entry in entries)
            {
                if (fileNameToExtract == null || fileNameToExtract == entry.Filename)
                {
                    ZipArchiveFileEntry fe = entry as ZipArchiveFileEntry;
                    if (fe == null)
                        entry.Extract(destination);
                    else
                    {
                        string path = null;

                        if (mode == ArchiveExtractionMode.Flat)
                            path = Path.Combine(destination, Path.GetFileName(entry.Filename));
                        else if (mode == ArchiveExtractionMode.SkipRootFolder)
                        {
                            string normalized = entry.Filename.Replace('/', '\\');
                            int index = normalized.IndexOf('\\');
                            if (index < 0)
                                continue;

                            path = Path.Combine(destination, normalized.Substring(index + 1));
                        }


                        fe.Extract(destination, path);
                    }
                }
                if (progress != null)
                    progress(this, new ProgressChangedEventArgs(idx * 100 / entries.Length, null));

                idx++;
            }
        }
    }

}
