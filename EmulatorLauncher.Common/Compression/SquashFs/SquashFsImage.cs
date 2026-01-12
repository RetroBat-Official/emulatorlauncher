using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using SquashFS.Reader.Native;

namespace SquashFS.Reader
{
    internal sealed class SquashFsImage : IDisposable
    {
        private readonly IntPtr _file;
        private readonly IntPtr _dirReader;
        private readonly IntPtr _dataReader;
        private readonly SqfsSuper _super;

        public SquashFsEntry[] Entries { get; private set; }

        public static SquashFsImage Open(string path)
        {
            IntPtr file = SqfsNative.sqfs_open_file(path, 1);
            if (file == IntPtr.Zero)
                throw new SquashFsException("Unable to open SquashFS");

            return new SquashFsImage(file);
        }

        private SquashFsImage(IntPtr file)
        {
            _file = file;

            var super = new SqfsSuper();
            var cfg = new SqfsCompressorConfig();

            if (SqfsNative.sqfs_super_read(ref super, _file) != 0)
                return;

            SqfsNative.sqfs_compressor_config_init(ref cfg, (SQFS_COMPRESSOR)super.compression_id, super.block_size, (ushort)SQFS_COMP_FLAG.SQFS_COMP_FLAG_UNCOMPRESS);

            IntPtr compressor;
            var retC = SqfsNative.sqfs_compressor_create(ref cfg, out compressor);
            if (retC != 0)
            {
                // fprintf(stderr, "%s: error creating compressor: %d.\n", argv[1], ret);
                return;
            }

            IntPtr idtbl = SqfsNative.sqfs_id_table_create(0);
            if (idtbl == IntPtr.Zero)
            {
                //                fputs("Error creating ID table.\n", stderr);
                return;
            }

            if (SqfsNative.sqfs_id_table_read(idtbl, _file, ref super, compressor) != 0)
            {
                //fprintf(stderr, "%s: error loading ID table.\n", argv[1]);
                return;
            }

            var dirReader = SqfsNative.sqfs_dir_reader_create(ref super, compressor, file, 0);
            if (dirReader == IntPtr.Zero)
            {
                // fprintf(stderr, "%s: error creating directory reader.\n", argv[1]);
                return;
            }


            _dataReader = SqfsNative.sqfs_data_reader_create(file, (UIntPtr)super.block_size, compressor, 0);
            if (_dataReader == IntPtr.Zero)
            {
                // fprintf(stderr, "%s: error creating data reader.\n", argv[1]);
                return;
            }

            IntPtr rootInode;
            int ret = SqfsNative.sqfs_dir_reader_get_root_inode(dirReader, out rootInode);
            if (ret != 0)
            {
                // fprintf(stderr, "%s: error reading root inode.\n", filename);
                return;
            }

            ret = SqfsNative.sqfs_dir_reader_get_full_hierarchy(dirReader, idtbl, "/", 0, out IntPtr tree);
            if (ret != 0)
                return;

            _dirReader = dirReader;
            _super = super;

            sqfs_tree_node_t node = (sqfs_tree_node_t)Marshal.PtrToStructure(tree, typeof(sqfs_tree_node_t));
            Entries = ReadDirectory(node);

            SqfsNative.sqfs_free(rootInode);

        }


    private SquashFsEntry[] ReadDirectory(sqfs_tree_node_t node, string path = null)
        {
            List<SquashFsEntry> ret = new List<SquashFsEntry>();

            sqfs_inode_generic_t inode = (sqfs_inode_generic_t)Marshal.PtrToStructure(node.inode, typeof(sqfs_inode_generic_t));

            if (inode.baseInode.type.HasFlag(SQFS_INODE_TYPE.SQFS_INODE_DIR) || inode.baseInode.type.HasFlag(SQFS_INODE_TYPE.SQFS_INODE_EXT_DIR))
            {
                IntPtr nPtr = node.children;
                while (nPtr != IntPtr.Zero)
                {
                    sqfs_tree_node_t n = (sqfs_tree_node_t)Marshal.PtrToStructure(nPtr, typeof(sqfs_tree_node_t));
                    sqfs_inode_generic_t ni = (sqfs_inode_generic_t)Marshal.PtrToStructure(n.inode, typeof(sqfs_inode_generic_t));

                    var name = n.GetName(nPtr);
                    string pathAndName = path == null ? name : Path.Combine(path, name);

               /*
                    System.UInt64 filesz = 0;
                    SqfsNative.sqfs_inode_get_file_size(n.inode, ref filesz);

                    var blocks = SqfsNative.sqfs_inode_get_file_block_count(ni);
                    for (uint i = 0; i < blocks; ++i)
                    {
                        // diff = (filesz < block_size) ? filesz : block_size;

                        IntPtr chunk;
                        uint chunk_size = 0;
                        var err = SqfsNative.sqfs_data_reader_get_block(_dataReader, n.inode, i, ref chunk_size, out chunk);
                    }

                    if (filesz > 0)
                    {
                        IntPtr chunk;
                        uint chunk_size = 0;
                        var err = SqfsNative.sqfs_data_reader_get_fragment(_dataReader, n.inode, ref chunk_size, out chunk);
                        if (err != 0)
                        {                            
                         //   return -1;
                        }
                    }
                  */
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ni.baseInode.mod_time).ToLocalTime();

                    if (ni.baseInode.type == SQFS_INODE_TYPE.SQFS_INODE_DIR)
                    {
                        ret.Add(new SquashFsEntry() { Name = name, FullPath = pathAndName, ModificationDate = epoch, IsDirectory = true });
                        ret.AddRange(ReadDirectory(n, pathAndName));
                    }
                    else if (ni.baseInode.type == SQFS_INODE_TYPE.SQFS_INODE_EXT_DIR)
                    {
                        ret.Add(new SquashFsEntry() { Name = name, FullPath = pathAndName, ModificationDate = epoch, IsDirectory = true });
                        ret.AddRange(ReadDirectory(n, pathAndName));
                    }
                    else if (ni.baseInode.type == SQFS_INODE_TYPE.SQFS_INODE_EXT_FILE)
                    {
                        ulong fileSize = ni.data.file_ext.file_size;
                        ret.Add(new SquashFsEntry() { Inode = n.inode, Name = name, FullPath = pathAndName, ModificationDate = epoch, Size = fileSize });
                    }
                    else if (ni.baseInode.type == SQFS_INODE_TYPE.SQFS_INODE_FILE)
                    {
                        ulong fileSize = ni.data.file.file_size;
                        ret.Add(new SquashFsEntry() { Inode = n.inode, Name = name, FullPath = pathAndName, ModificationDate = epoch, Size = fileSize });
                    }
                    else if (ni.baseInode.type == SQFS_INODE_TYPE.SQFS_INODE_SLINK)
                    {
                        // TODO 
                    }
                    else
                    {
                        
                    }

                    nPtr = n.next;
                }
            }

            return ret.ToArray();
        }
      
        public void ExtractFile(string squashPath, string outputPath)
        {
            var entry = Entries.FirstOrDefault(e => e.FullPath == squashPath);
            if (entry == null || entry.IsDirectory)
                throw new FileNotFoundException(squashPath);
            
            using (var fs = File.Create(Path.Combine(outputPath, entry.Name)))
            {
                byte[] buffer = new byte[128 * 1024];
                ulong offset = 0;

                while (offset < (ulong)entry.Size)
                {
                    var read = SqfsNative.sqfs_data_reader_read(
                        _dataReader,
                        entry.Inode,
                        offset,
                        buffer,
                        (UIntPtr)buffer.Length);

                    int n = read.ToInt32();
                    fs.Write(buffer, 0, n);
                    offset += (ulong)n;
                }
            }
        }

        public void Dispose()
        {
            if (_dirReader != IntPtr.Zero)
                SqfsNative.sqfs_free(_dirReader);

            if (_dataReader != IntPtr.Zero)
                SqfsNative.sqfs_free(_dataReader);

            if (_file != IntPtr.Zero)
                SqfsNative.sqfs_free(_file);
        }
    }

}
