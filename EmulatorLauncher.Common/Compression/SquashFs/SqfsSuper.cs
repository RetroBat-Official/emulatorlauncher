using System.Runtime.InteropServices;

namespace SquashFS.Reader.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SqfsSuper
    {
        public uint magic;
        public uint inode_count;
        public uint mod_time;
        public uint block_size;
        public uint frag_count;

        public ushort compression_id;
        public ushort block_log;
        public ushort flags;
        public ushort id_count;
        public ushort version_major;
        public ushort version_minor;

        public ulong root_inode_ref;
        public ulong bytes_used;
        public ulong id_table_start;
        public ulong xattr_id_table_start;
        public ulong inode_table_start;
        public ulong directory_table_start;
        public ulong fragment_table_start;
        public ulong export_table_start;
    }
}
