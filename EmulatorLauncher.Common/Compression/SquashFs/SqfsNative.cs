using System;
using System.Runtime.InteropServices;
using sqfs_u8 = System.Byte;
using sqfs_u16 = System.UInt16;
using sqfs_u32 = System.UInt32;
using sqfs_u64 = System.UInt64;
using size_t = System.UInt32;
using System.Text;

namespace SquashFS.Reader.Native
{
    internal static class SqfsNative
    {
        private const string DLL = "libsquashfs.dll";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int sqfs_dir_iterator_create(IntPtr reader, IntPtr idTable, IntPtr dataReader, IntPtr xattrReader, IntPtr iNode, out IntPtr iterator);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr sqfs_open_file([MarshalAs(UnmanagedType.LPStr)] string filename, uint flags);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void sqfs_free(IntPtr ptr);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_super_read(ref SqfsSuper super, IntPtr file);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqfs_dir_reader_create(ref SqfsSuper super, IntPtr cmp, IntPtr file, uint flags);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_dir_reader_get_root_inode(IntPtr rd, out IntPtr inode);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_compressor_config_init(ref SqfsCompressorConfig cfg, SQFS_COMPRESSOR id, uint block_size, ushort flags);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_compressor_create(ref SqfsCompressorConfig cfg, out IntPtr compressor);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_id_table_read(IntPtr tbl, IntPtr file, ref SqfsSuper super, IntPtr compressor);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_dir_reader_get_full_hierarchy(IntPtr reader, IntPtr idtbl, string path, uint flags, out IntPtr tree);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_data_reader_load_fragment_table(IntPtr reader, IntPtr idtbl, string path, uint flags, out IntPtr tree);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqfs_id_table_create(uint flags);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqfs_data_reader_create(IntPtr file, UIntPtr block_size, IntPtr compressor, uint flags);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqfs_data_reader_read(IntPtr reader, IntPtr inode, ulong offset, byte[] buffer, UIntPtr size);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_inode_get_file_size(IntPtr inode, ref sqfs_u64 size);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_data_reader_get_block(IntPtr dataReader, IntPtr iNode, size_t index, ref size_t size, out IntPtr output);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqfs_data_reader_get_fragment(IntPtr dataReader, IntPtr iNode, ref size_t size, out IntPtr output);

        public static sqfs_u32 sqfs_inode_get_file_block_count(sqfs_inode_generic_t inode)
        {
            return inode.payload_bytes_used / sizeof(sqfs_u32);
        }

        public static string GetName(this sqfs_tree_node_t node, IntPtr nodePtr)
        {
            if (nodePtr == IntPtr.Zero)
                return null;

            int offset = Marshal.SizeOf(typeof(sqfs_tree_node_t));
            IntPtr namePtr = IntPtr.Add(nodePtr, offset);

            return ReadUtf8String(namePtr);
        }

        private static string ReadUtf8String(IntPtr ptr)
        {
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;

            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);

            return Encoding.UTF8.GetString(buffer);
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsDirEntry
    {
        public uint inode;
        public ushort type;
        public ushort name_size;
        public IntPtr name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct sqfs_tree_node_t
    {
        public IntPtr parent; // sqfs_tree_node_t
        public IntPtr children; // sqfs_tree_node_t
        public IntPtr next; // sqfs_tree_node_t
        public IntPtr inode; // sqfs_inode_generic_t

        public uint uid;
        public uint gid;

        // name[] follows immediately in memory
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct sqfs_readdir_state_t
    {
        public sqfs_u64 inode_block;
        public sqfs_u64 block;
        public size_t offset;
        public size_t size;
        public size_t entries;
        public sqfs_u32 inum_base;
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct sqfs_dir_reader_state_t
    {       
        public sqfs_readdir_state_t cursor;
        public sqfs_u64 parent_ref;
        public sqfs_u64 dir_ref;
        public sqfs_u8 state;
    };

    internal enum SQFS_COMPRESSOR : ushort
    {
        SQFS_COMP_GZIP = 1,
        SQFS_COMP_LZO = 2,
        SQFS_COMP_XZ = 3,
        SQFS_COMP_LZMA = 4,
        SQFS_COMP_LZ4 = 5,
        SQFS_COMP_ZSTD = 6,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsGzipOptions
    {
        public ushort window_size;

        // 14 bytes padding = 7 ushort
        public ushort pad0;
        public ushort pad1;
        public ushort pad2;
        public ushort pad3;
        public ushort pad4;
        public ushort pad5;
        public ushort pad6;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsLzoOptions
    {
        public ushort algorithm;

        public ushort pad0;
        public ushort pad1;
        public ushort pad2;
        public ushort pad3;
        public ushort pad4;
        public ushort pad5;
        public ushort pad6;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsXzLzmaOptions
    {
        public uint dict_size;
        public byte lc;
        public byte lp;
        public byte pb;

        public byte pad0;
        public byte pad1;
        public byte pad2;
        public byte pad3;
        public byte pad4;
        public byte pad5;
        public byte pad6;
        public byte pad7;
        public byte pad8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct SqfsCompressorOptions
    {
        [FieldOffset(0)] public SqfsGzipOptions gzip;
        [FieldOffset(0)] public SqfsLzoOptions lzo;
        [FieldOffset(0)] public SqfsXzLzmaOptions xz;
        [FieldOffset(0)] public SqfsXzLzmaOptions lzma;

        [FieldOffset(0)] public ulong pad0;
        [FieldOffset(8)] public ulong pad1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SqfsCompressorConfig
    {
        public ushort id;
        public ushort flags;
        public uint block_size;
        public uint level;
        public SqfsCompressorOptions opt;
    }

    [Flags]
    internal enum SQFS_COMP_FLAG : ushort
    {
        // LZ4
        SQFS_COMP_FLAG_LZ4_HC = 0x0001,
        SQFS_COMP_FLAG_LZ4_ALL = 0x0001,

        // LZMA v1
        SQFS_COMP_FLAG_LZMA_EXTREME = 0x0001,
        SQFS_COMP_FLAG_LZMA_ALL = 0x0001,

        // XZ filters
        SQFS_COMP_FLAG_XZ_X86 = 0x0001,
        SQFS_COMP_FLAG_XZ_POWERPC = 0x0002,
        SQFS_COMP_FLAG_XZ_IA64 = 0x0004,
        SQFS_COMP_FLAG_XZ_ARM = 0x0008,
        SQFS_COMP_FLAG_XZ_ARMTHUMB = 0x0010,
        SQFS_COMP_FLAG_XZ_SPARC = 0x0020,

        SQFS_COMP_FLAG_XZ_EXTREME = 0x0100,
        SQFS_COMP_FLAG_XZ_ALL = 0x013F,

        // GZIP
        SQFS_COMP_FLAG_GZIP_DEFAULT = 0x0001,
        SQFS_COMP_FLAG_GZIP_FILTERED = 0x0002,
        SQFS_COMP_FLAG_GZIP_HUFFMAN = 0x0004,
        SQFS_COMP_FLAG_GZIP_RLE = 0x0008,
        SQFS_COMP_FLAG_GZIP_FIXED = 0x0010,
        SQFS_COMP_FLAG_GZIP_ALL = 0x001F,

        // Generic
        SQFS_COMP_FLAG_UNCOMPRESS = 0x8000,
        SQFS_COMP_FLAG_GENERIC_ALL = 0x8000,
    }

    internal enum SQFS_LZO_ALGORITHM : ushort
    {
        SQFS_LZO1X_1 = 0,
        SQFS_LZO1X_1_11 = 1,
        SQFS_LZO1X_1_12 = 2,
        SQFS_LZO1X_1_15 = 3,
        SQFS_LZO1X_999 = 4,
    }

    // Types de base
    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInode
    {
        public sqfs_u32 type;
        public sqfs_u32 uid;
        public sqfs_u32 gid;
        public sqfs_u32 size;
        public sqfs_u32 nlink;
        public sqfs_u32 mtime;
        public sqfs_u32 offset;
        public sqfs_u32 start_block;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeDev
    {
        public sqfs_u32 nlink;
        public sqfs_u32 devno;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeDevExt
    {
        public sqfs_u32 nlink;
        public sqfs_u32 devno;
        public sqfs_u32 xattr_idx;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeIpc
    {
        public sqfs_u32 nlink;
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeIpcExt
    {
        public sqfs_u32 nlink;
        public sqfs_u32 xattr_idx;
    };

	[StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeSlink
    {
        public sqfs_u32 nlink;
        public sqfs_u32 target_size;
	}

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeSlinkExt
    {
		public sqfs_u32 nlink;
		public sqfs_u32 target_size;
        public sqfs_u32 xattr_idx;
	}

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeFile
    {
		public sqfs_u32 blocks_start;
		public sqfs_u32 fragment_index;
		public sqfs_u32 fragment_offset;
        public sqfs_u32 file_size;
	}

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeFileExt
    {
		public sqfs_u64 blocks_start;
		public sqfs_u64 file_size;
		public sqfs_u64 sparse;
		public sqfs_u32 nlink;
		public sqfs_u32 fragment_idx;
		public sqfs_u32 fragment_offset;
        public sqfs_u32 xattr_idx;
	}

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeDir
    {
		public sqfs_u32 start_block;
		public sqfs_u32 nlink;
		public sqfs_u16 size;
		public sqfs_u16 offset;
        public sqfs_u32 parent_inode;
	};

    [StructLayout(LayoutKind.Sequential)]
    internal struct SqfsInodeDirExt
    {
		public sqfs_u32 nlink;
		public sqfs_u32 size;
		public sqfs_u32 start_block;
		public sqfs_u32 parent_inode;
		public sqfs_u16 inodex_count;
		public sqfs_u16 offset;
        public sqfs_u32 xattr_idx;
	}

	// Exemple union : dev, file, dir, slink, ipc
	[StructLayout(LayoutKind.Explicit)]
    internal struct SqfsInodeData
    {
        [FieldOffset(0)]
        public SqfsInodeDev dev;

        [FieldOffset(0)]
        public SqfsInodeDevExt dev_ext;

        [FieldOffset(0)]
        public SqfsInodeIpc ipc;

        [FieldOffset(0)]
        public SqfsInodeIpcExt ipc_ext;

        [FieldOffset(0)]
        public SqfsInodeSlink slink;

        [FieldOffset(0)]
        public SqfsInodeSlinkExt slink_ext;

        [FieldOffset(0)]
        public SqfsInodeFile file;

        [FieldOffset(0)]
        public SqfsInodeFileExt file_ext;

        [FieldOffset(0)]
        public SqfsInodeDir dir;

        [FieldOffset(0)]
        public SqfsInodeDirExt dir_ext;
    }

    [Flags]
    internal enum SQFS_INODE_TYPE : sqfs_u16
    {
        SQFS_INODE_DIR = 1,
        SQFS_INODE_FILE = 2,
        SQFS_INODE_SLINK = 3,
        SQFS_INODE_BDEV = 4,
        SQFS_INODE_CDEV = 5,
        SQFS_INODE_FIFO = 6,
        SQFS_INODE_SOCKET = 7,
        SQFS_INODE_EXT_DIR = 8,
        SQFS_INODE_EXT_FILE = 9,
        SQFS_INODE_EXT_SLINK = 10,
        SQFS_INODE_EXT_BDEV = 11,
        SQFS_INODE_EXT_CDEV = 12,
        SQFS_INODE_EXT_FIFO = 13,
        SQFS_INODE_EXT_SOCKET = 14,
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct sqfs_inode_t
    {
        public SQFS_INODE_TYPE type;
        public sqfs_u16 mode; // SQFS_INODE_MODE
        public sqfs_u16 uid_idx;
        public sqfs_u16 gid_idx;
        public sqfs_u32 mod_time;
        public sqfs_u32 inode_number;
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct sqfs_inode_generic_t
    {
        public sqfs_inode_t baseInode;
        public sqfs_u32 payload_bytes_available;
        public sqfs_u32 payload_bytes_used;

        public SqfsInodeData data;  // union
        public IntPtr extra;        // ptr to extra[]
    }
}
