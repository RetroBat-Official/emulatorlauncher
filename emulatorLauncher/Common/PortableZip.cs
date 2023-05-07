using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace emulatorLauncher
{
    class ZipArchiveFileEntry
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
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, Filename);

            if (IsDirectory)
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return;
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

    class ZipArchive : IDisposable
    {
        private static System.Reflection.MethodInfo _zipOpenRead;

        private IDisposable _zipArchive;

        public static ZipArchive OpenRead(string path)
        {
            if (_zipOpenRead == null)
            {
                var afs = System.Reflection.Assembly.Load("System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                if (afs == null)
                    throw new Exception("Framework not supported");

                var ass = System.Reflection.Assembly.Load("System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                if (ass == null)
                    throw new Exception("Framework not supported");

                var zipFile = afs.GetTypes().FirstOrDefault(t => t.Name == "ZipFile");
                if (zipFile == null)
                    throw new Exception("Framework not supported");

                _zipOpenRead = zipFile.GetMember("OpenRead", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).FirstOrDefault() as System.Reflection.MethodInfo;
                if (_zipOpenRead == null)
                    throw new Exception("Framework not supported");
            }


            IDisposable zipArchive = _zipOpenRead.Invoke(null, new object[] { path }) as IDisposable;
            if (zipArchive == null)
                return null;

            ZipArchive ret = new ZipArchive();
            ret._zipArchive = zipArchive;
            return ret;
        }


        public IEnumerable<ZipArchiveFileEntry> Entries
        {
            get
            {
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

                    yield return e;
                }
            }
        }

        public void Dispose()
        {
            if (_zipArchive != null)
                _zipArchive.Dispose();
        }
    }
       
}
