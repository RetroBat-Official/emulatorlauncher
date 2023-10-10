using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using EmulatorLauncher.Common.Compression;

namespace Mount
{
    class MountedFileEntry : FileEntry
    {
        private ZipEntry _entry;

        public MountedFileEntry(ZipEntry entry, DokanOperations fs)
        {
            _entry = entry;

            Filename = entry.Filename;
            IsDirectory = entry.IsDirectory;
            Attributes = (uint)FileAttributes.Normal; // entry.Attributes;
            FileSystem = fs;
        }

        public override long Length
        {
            get
            {
                if (_len >= 0)
                    return _len;

                if (IsDirectory)
                    _len = 0;
                else
                {
                    if (File.Exists(PhysicalPath))
                        _len = new FileInfo(PhysicalPath).Length;
                    else
                        _len = _entry.Length;
                }

                return _len;
            }
        }

        private long _len = -1;

        public override DateTime LastWriteTime { get { return _entry.LastModified; } }
        public override DateTime CreationTime { get { return _entry.LastModified; } }
        public override DateTime LastAccessTime { get { return _entry.LastModified; } }

        public DokanOperations FileSystem { get; set; }

        private object _lock = new object();

        private string _physicalPath;

        public override string PhysicalPath
        {
            get
            {
                if (_physicalPath == null)
                {
                    string source = this.Filename;
                    if (source.StartsWith("\\") || source.StartsWith("/"))
                        source = source.Substring(1);

                    _physicalPath = Path.Combine(FileSystem.ExtractionDirectory, source);
                }

                return _physicalPath;
            }
        }

        public bool Queryed { get; set; }
        
        public override Stream GetPhysicalFileStream(System.IO.FileAccess access = System.IO.FileAccess.Read)
        {
            Queryed = true;

            lock (_lock)
            {
                string physicalPath = PhysicalPath;
                if (File.Exists(physicalPath))
                {
                    if (access != (System.IO.FileAccess)0)
                        return new FileStream(physicalPath, FileMode.Open, access, FileShare.ReadWrite);

                    return null;
                }
                else
                {
                    string source = this.Filename;
                    if (source.StartsWith("\\") || source.StartsWith("/"))
                        source = source.Substring(1);

                    var parent = Path.GetDirectoryName(physicalPath);
                    if (!Directory.Exists(parent))
                        Directory.CreateDirectory(parent);

                    string tmpDirectory = Path.Combine(FileSystem.ExtractionDirectory, Guid.NewGuid().ToString());
                    if (!Directory.Exists(tmpDirectory))
                        Directory.CreateDirectory(tmpDirectory);

                    string tmpFile = Path.GetFullPath(Path.Combine(tmpDirectory, source));

                    Zip.Extract(FileSystem.FileName, tmpDirectory, source, null, true);

                    bool exists = File.Exists(tmpFile);
                    if (exists)
                        File.Move(tmpFile, physicalPath);

                    ThreadPool.QueueUserWorkItem((a) =>
                    {
                        try { Directory.Delete(tmpDirectory, true); }
                        catch { }
                    });

                    if (exists && access != (System.IO.FileAccess)0)
                        return new FileStream(physicalPath, FileMode.Open, access, FileShare.ReadWrite);

                }
                
                return null;
            }
        }
    }
}
