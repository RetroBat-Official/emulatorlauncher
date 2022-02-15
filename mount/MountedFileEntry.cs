using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using emulatorLauncher;
using System.Threading;

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

        public override string PhysicalPath
        {
            get
            {
                string source = this.Filename;
                if (source.StartsWith("\\") || source.StartsWith("/"))
                    source = source.Substring(1);

                return Path.Combine(FileSystem.ExtractionDirectory, source);
            }
        }

        public bool Queryed { get; set; }
        
        public override Stream GetPhysicalFileStream(System.IO.FileAccess access = System.IO.FileAccess.Read)
        {
            Queryed = true;

            lock (_lock)
            {
                string source = this.Filename;
                if (source.StartsWith("\\") || source.StartsWith("/"))
                    source = source.Substring(1);

                string physicalPath = PhysicalPath;
                if (!File.Exists(physicalPath))
                {
                    var parent = Path.GetDirectoryName(physicalPath);
                    if (!Directory.Exists(parent))
                        Directory.CreateDirectory(parent);

                //    Console.WriteLine("Extracting: " + source);

                    int time = Environment.TickCount;
                    Zip.Extract(FileSystem.FileName, FileSystem.ExtractionDirectory, source, null, true);
                    int elapsed = Environment.TickCount - time;

                    if (access != (System.IO.FileAccess)0 && File.Exists(physicalPath))
                    {
                        try
                        {
                            // Open as exclusive after extraction or we're not sure file buffers are Ok. Why !?
                            using (var tmp = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                tmp.ReadByte();
                                tmp.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("------------------------------------------------------------------------------ " + ex.Message);
                        }
                    }

                 //   Console.WriteLine("Extracted: " + source + " => " + elapsed + "ms");
                }

                if (access != (System.IO.FileAccess)0 && File.Exists(physicalPath))
                    return new FileStream(physicalPath, FileMode.Open, access, FileShare.ReadWrite);

                return null;
            }
        }
    }
}
