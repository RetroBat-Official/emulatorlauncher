using System;

namespace SquashFS.Reader
{
    internal sealed class SquashFsEntry
    {
        public string Name { get; internal set; }
        public string FullPath { get; internal set; }
        public bool IsDirectory { get; internal set; }
        public ulong Size { get; internal set; }
        public System.DateTime ModificationDate { get; set; }

        public override string ToString()
        {
            return FullPath;
        }

        internal IntPtr Inode;
    }
}
