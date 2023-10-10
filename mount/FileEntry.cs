using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Mount
{
    abstract class FileEntry
    {
        public FileEntry()
        {
            Children = new List<FileEntry>();
        }

        public string Filename { get; set; }
        public bool IsDirectory { get; set; }
        public uint Attributes { get; set; }

        public List<FileEntry> Children { get; protected set; }

        public abstract string PhysicalPath { get; }
        public abstract long Length { get; }
        public abstract DateTime LastWriteTime { get; }
        public abstract DateTime CreationTime { get; }
        public abstract DateTime LastAccessTime { get; }

        public abstract Stream GetPhysicalFileStream(System.IO.FileAccess access = System.IO.FileAccess.Read);

        public override string ToString()
        {
            return Filename.ToString();
        }
    }
}
