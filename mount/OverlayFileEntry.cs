using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Mount
{
    class OverlayFileEntry : FileEntry
    {
        public OverlayFileEntry(string relativePath, string fullPath)
        {
            if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1);

            Filename = relativePath;
            _physicalPath = fullPath;

            if (Directory.Exists(_physicalPath))
            {
                IsDirectory = true;

                var fi = new DirectoryInfo(_physicalPath);
                _lastWriteTime = fi.LastWriteTime;
                _lastAccessTime = fi.LastAccessTime;
                _creationTime = fi.CreationTime;
                _length = 0;
                Attributes = (uint)fi.Attributes;
            }
            else if (File.Exists(_physicalPath))
            {
                var fi = new FileInfo(_physicalPath);
                _lastWriteTime = fi.LastWriteTime;
                _lastAccessTime = fi.LastAccessTime;
                _creationTime = fi.CreationTime;
                _length = fi.Length;
                Attributes = (uint)fi.Attributes;
            }
        }

        public override string PhysicalPath { get { return _physicalPath; } }
        private string _physicalPath;

        public override long Length { get { return _length; } }
        private long _length;

        public override DateTime LastWriteTime { get { return _lastWriteTime; } }
        private DateTime _lastWriteTime;

        public override DateTime CreationTime { get { return _creationTime; } }
        private DateTime _creationTime;

        public override DateTime LastAccessTime { get { return _lastAccessTime; } }
        private DateTime _lastAccessTime;

        public override Stream GetPhysicalFileStream(System.IO.FileAccess access = System.IO.FileAccess.Read)
        {
            if (!string.IsNullOrEmpty(PhysicalPath) && File.Exists(PhysicalPath))
                return new FileStream(PhysicalPath, FileMode.Open, access, FileShare.ReadWrite);

            return null;
        }

        public void SetLength(long size) { _length = size; }
        public void SetPhysicalPath(string path) { _physicalPath = path; }
    }

}
