using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace Mount
{
    public class DokanOperations : IDokanOperations
    {
        public string FileName { get; private set; }
        public string OverlayPath { get; private set; }
        public string ExtractionDirectory { get; private set; }

        private Dictionary<string, FileEntry> _entries;
        private OverlayDeletionRepository _overlay;

        public DokanOperations(string filepath, string extractionDirectory = null, string overlayPath = null)
        {
            FileName = filepath;
            ExtractionDirectory = string.IsNullOrEmpty(extractionDirectory) ? Path.Combine(Path.GetTempPath(), ".mountfs", Path.GetFileName(filepath)) : extractionDirectory;

            if (overlayPath != null && (overlayPath.StartsWith("./") || overlayPath.StartsWith(".\\")))
                overlayPath = Path.GetFullPath(Path.Combine(ExtractionDirectory, overlayPath));
            else if (overlayPath == ".")                   
                overlayPath = Path.GetFullPath(Path.Combine(ExtractionDirectory, ".overlay"));

            OverlayPath = overlayPath;

            _entries = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            _entries["\\"] = new MountedFileEntry(new ZipEntry() { Filename = "\\", Length = 0, IsDirectory = true }, this);

            var entries = Zip.ListEntries(FileName);
            foreach (var z in entries)
            {
                var entryName = "\\" + z.Filename.Replace("/", "\\");
                if (IsEntryNameValid(entryName))
                    _entries[entryName] = new MountedFileEntry(z, this);
            }

            LoadOverlay();
            RebuildTreeChildren();
        }

        HashSet<string> hiddenEntries = new HashSet<string>
        {
            "\\.update-timestamp",
            "\\dosdevices",
            "\\drive_c\\windows",
            "\\drive_c\\.windows-serial",
            "\\drive_c\\program files\\common files",
            "\\drive_c\\program files\\internet explorer",
            "\\drive_c\\program files\\windows media player",
            "\\drive_c\\program files\\windows nt",
            "\\drive_c\\program files (x86)\\common files",
            "\\drive_c\\program files (x86)\\internet explorer",
            "\\drive_c\\program files (x86)\\windows media player",
            "\\drive_c\\program files (x86)\\windows nt",
            "\\drive_c\\programdata",
            "\\system.reg",
            "\\user.reg",
            "\\userdef.reg"
        };

        private bool IsEntryNameValid(string fileName)
        {
            return !hiddenEntries.Contains(fileName.ToLowerInvariant());
        }

        public static bool DebugOutput { get; set; }
        
        #region IDokanOperations
        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            maximumComponentLength = 1024;
            fileSystemName = Path.GetExtension(FileName).Substring(1).ToUpperInvariant();
            volumeLabel = Path.GetFileNameWithoutExtension(FileName);
            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.UnicodeOnDisk;

            if (string.IsNullOrEmpty(OverlayPath))
                features |= FileSystemFeatures.ReadOnlyVolume; // | FileSystemFeatures.VolumeIsCompressed

            return NtStatus.Success;
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {            
            bool isWrite = access.HasFlag(DokanNet.FileAccess.WriteData) || access.HasFlag(DokanNet.FileAccess.GenericWrite);
            bool isRead = access.HasFlag(DokanNet.FileAccess.ReadData) || access.HasFlag(DokanNet.FileAccess.GenericRead);

            if (fileName == "\\")
            {
                info.IsDirectory = true;
                return DokanResult.Success;
            }

            if (DebugOutput)
                Console.WriteLine("Open: " + fileName + " : " + mode.ToString() + " ( " + access.ToString() + " ) ");

            var item = GetFile(fileName);

            if (mode == FileMode.CreateNew || (mode == FileMode.Create && item == null))
                item = CreateToOverlay(fileName, null, info.IsDirectory);

            if (item == null)
                return DokanResult.FileNotFound;

            info.IsDirectory = item.IsDirectory;

            if (access == DokanNet.FileAccess.Delete)
            {
                if (string.IsNullOrEmpty(OverlayPath))
                    return NtStatus.AccessDenied;

                return NtStatus.Success;
            }

            if (access == DokanNet.FileAccess.Synchronize || access == DokanNet.FileAccess.ReadAttributes)
                return NtStatus.Success;          

            if (isWrite && string.IsNullOrEmpty(OverlayPath))
                return NtStatus.AccessDenied;            

            if (!info.IsDirectory && info.Context == null)
            {            
                if (isRead)
                {
                    System.IO.FileAccess fileAccess = System.IO.FileAccess.Read;
                    if (isWrite)
                        fileAccess |= System.IO.FileAccess.Write;

                    var m = item as MountedFileEntry;
                    if (m != null && !m.Queryed)
                    {
                        m.Queryed = true;
                        PreloadParentFolderEntries(fileName);
                    }
                
                    info.Context = item.GetPhysicalFileStream(fileAccess);

                    if (DebugOutput)
                        Console.WriteLine("CreateFile (" + fileAccess.ToString() + "): " + fileName);
                }
                else if (isWrite)
                {
                    if (DebugOutput)
                        Console.WriteLine("CreateFile (FileAccess.Write): " + fileName);
                }
            }
                        
            return NtStatus.Success;            
        }

        private string Extension(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant(); 
        }

        private bool IsPreloadable(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var ext = Extension(fileName);
            return ext == ".dll" || ext == ".exe";
        }

        private void PreloadParentFolderEntries(string fileName)
        {
            var parent = GetFile(Path.GetDirectoryName(fileName)) as MountedFileEntry;
            if (parent == null || !parent.IsDirectory)
                return;

            Task.Factory.StartNew(() =>
            {
                int take = Environment.ProcessorCount;

                string ext = Extension(fileName);
                if (ext == ".exe" || ext == ".dll")
                    take = 2 * Environment.ProcessorCount;

                var children = parent.Children.OfType<MountedFileEntry>()
                    .Where(p => !p.Queryed && !p.IsDirectory && !File.Exists(p.PhysicalPath))  // (Extension(p.Filename) == ext || IsPreloadable(p.Filename)) && 
                    .OrderByDescending(p => Extension(p.Filename) == ext)
                    .ThenByDescending(p => IsPreloadable(p.Filename))
                    .Take(take)
                    .ToArray();

                foreach (var child in children)
                    child.Queryed = true;

                Parallel.ForEach(children, new ParallelOptions { MaxDegreeOfParallelism = take }, child =>
                {
                    if (DebugOutput)
                        Console.WriteLine("Preloading: " + child.Filename);

                    child.GetPhysicalFileStream((System.IO.FileAccess)0);

                    if (DebugOutput)
                        Console.WriteLine("Preloaded: " + child.Filename);
                });
            });
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {          
            if (info.IsDirectory)
                return;

            if (DebugOutput)
                Console.WriteLine("Cleanup: " + fileName);

            Stream stream = info.Context as Stream;
            if (stream != null)
            {
                info.Context = null;

                ThreadPool.QueueUserWorkItem((a) =>
                    {
                        stream.Close();
                        stream.Dispose();
                    });
            }

            if (info.DeleteOnClose)
            {
                if (info.IsDirectory)
                    DeleteDirectory(fileName, info);
                else
                    DeleteFile(fileName, info);
            }
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            if (info.IsDirectory)
                return;

            if (DebugOutput)
                Console.WriteLine("CloseFile: " + fileName);

            Stream stream = info.Context as Stream;
            if (stream != null)
            {
                info.Context = null;

                ThreadPool.QueueUserWorkItem((a) =>
                    {
                        stream.Close();
                        stream.Dispose();
                    });
            }
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            if (DebugOutput)
                Console.WriteLine("ReadFile: " + fileName + " @ " + offset + " => " + buffer.Length);
            
            Stream stream = info.Context as Stream;

            if (stream == null)
            {
                if (DebugOutput)
                    Console.WriteLine("ReadFile: NULL CONTEXT");

                //called when file is read as memory memory mapeded file usualy notepad and stuff
                var item = GetFile(fileName);
                if (item == null)
                {
                    bytesRead = 0;
                    return NtStatus.FileInvalid;
                }

                using (var tempStream = item.GetPhysicalFileStream())
                {
                    tempStream.Position = offset;
                    bytesRead = tempStream.Read(buffer, 0, buffer.Length);
                    tempStream.Close();                  
                }

                return NtStatus.Success;
            }
            
            lock (stream)
            {
                if (stream.Position != offset)
                    stream.Position = offset;

                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }

            return NtStatus.Success;
        }
        
        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var item = GetFile(oldName);
            if (item == null)
                return DokanResult.FileNotFound;

            string fullPath = Path.Combine(OverlayPath, newName.Substring(1));

            if (!replace)
            {
                var destItem = GetFile(newName);
                if (destItem != null)
                    return NtStatus.CannotDelete;
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (item is OverlayFileEntry)
            {
                if (item.IsDirectory)
                    Directory.Move(item.PhysicalPath, fullPath);
                else
                    File.Move(item.PhysicalPath, fullPath);

                item.Filename = newName.Substring(1);
                ((OverlayFileEntry)item).SetPhysicalPath(fullPath);

                _entries.Remove(oldName.Replace("/", "\\"));
                _entries[newName.Replace("/", "\\")] = item;
                _overlay.RestoreFile(item.Filename);
            }
            else
            {
                if (item.IsDirectory)
                {
                    FileTools.CopyDirectory(item.PhysicalPath, fullPath, true);

                    var childItems = item.Traverse(c => c.Children).Skip(1).ToList();
                    var keysToRemove = _entries.Where(e => childItems.Contains(e.Value)).Select(e => e.Key).ToList();
                    foreach (var keyToRemove in keysToRemove)
                        _entries.Remove(keyToRemove);

                    AddPathToOverlay(fullPath);
                }
                else
                {
                    using (var fs = new FileStream(fullPath, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.ReadWrite))
                    using (var stream = item.GetPhysicalFileStream())
                        stream.CopyTo(fs);
                }

                DeleteFile(oldName, info);

                var ret = new OverlayFileEntry(newName, fullPath);
                _entries[newName.Replace("/", "\\")] = ret;
                _overlay.RestoreFile(ret.Filename);
            }

            RebuildTreeChildren();

            return NtStatus.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            bytesWritten = 0;

            Stream stream = info.Context as Stream;

            var item = GetFile(fileName);            
            if (item == null || !(item is OverlayFileEntry))
            {
                item = CreateToOverlay(fileName, item);

                if (stream != null)
                {
                    stream.Dispose();
                    stream = item.GetPhysicalFileStream(System.IO.FileAccess.ReadWrite);
                    info.Context = stream;
                }
            }

            if (stream == null && item is OverlayFileEntry)
            {
                if (DebugOutput)
                    Console.WriteLine("WriteFile: NULL CONTEXT");

                if (offset == 0)
                {
                    _overlay.RestoreFile(item.Filename);
                    File.WriteAllText(item.PhysicalPath, "");
                }

                using (var tempStream = item.GetPhysicalFileStream(System.IO.FileAccess.Write))
                {
                    tempStream.Position = offset;
                    tempStream.Write(buffer, 0, buffer.Length);
                    bytesWritten = (int)(tempStream.Position - offset);
                    tempStream.Seek(0, SeekOrigin.End);
                    ((OverlayFileEntry)item).SetLength(tempStream.Length);
                    tempStream.Close();                    
                }

                return NtStatus.Success;
            }

            lock (stream)
            {
                stream.Position = offset;
                stream.Write(buffer, 0, buffer.Length);
                bytesWritten = (int)(stream.Position - offset);
              
                return NtStatus.Success;
            }
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            Stream stream = info.Context as Stream;
            if (stream != null)
                stream.Flush();

            return NtStatus.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            var item = GetFile(fileName);
            if (item == null)
            {
                fileInfo = default(FileInformation);
                return DokanResult.FileNotFound;
            }

            fileInfo = GetFileInformation(item);

            return NtStatus.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = FindFilesHelper(fileName, "*");
            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = FindFilesHelper(fileName, searchPattern);
            return DokanResult.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            return NtStatus.DiskFull;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return NtStatus.DiskFull;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            var item = GetFile(fileName);
            if (item == null)
                return DokanResult.Success;
           
            _entries.Remove(fileName);

            foreach (var entry in _entries)
                entry.Value.Children.Remove(item);

            if (item is OverlayFileEntry)
            {
                if (item.IsDirectory)
                {
                    try { Directory.Delete(item.PhysicalPath, true); }
                    catch { return NtStatus.DirectoryNotEmpty; }
                }
                else
                    File.Delete(item.PhysicalPath);
            }
            else
                _overlay.DeleteFile(item.Filename);
            
            return NtStatus.Success;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return DeleteFile(fileName, info);            
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            FileStream stream = info.Context as FileStream;
            if (stream != null)
                stream.SetLength(length);

            var item = GetFile(fileName) as OverlayFileEntry;
            if (item != null)
                item.SetLength(length);             

            return NtStatus.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            FileStream stream = info.Context as FileStream;
            if (stream != null)
                stream.SetLength(length);

            var item = GetFile(fileName) as OverlayFileEntry;
            if (item != null)
                item.SetLength(length);             

            return NtStatus.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            if (string.IsNullOrEmpty(OverlayPath))
            {
                totalNumberOfBytes = new FileInfo(FileName).Length;
                totalNumberOfFreeBytes = 0;
                freeBytesAvailable = 0;
            }
            else
            {
                try
                {
                    var driveInfo = new DriveInfo(Path.GetPathRoot(OverlayPath));
                    freeBytesAvailable = driveInfo.AvailableFreeSpace;
                    totalNumberOfBytes = driveInfo.TotalSize;
                    totalNumberOfFreeBytes = driveInfo.TotalFreeSpace;
                }
                catch
                {
                    totalNumberOfBytes = new FileInfo(FileName).Length;
                    totalNumberOfFreeBytes = 0;
                    freeBytesAvailable = 0;
                }
            }

            return NtStatus.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out System.Security.AccessControl.FileSystemSecurity security, System.Security.AccessControl.AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return NtStatus.NotImplemented;
        }

        public NtStatus SetFileSecurity(string fileName, System.Security.AccessControl.FileSystemSecurity security, System.Security.AccessControl.AccessControlSections sections, IDokanFileInfo info)
        {
            return NtStatus.DiskFull;
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return NtStatus.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }
        #endregion

        #region Helpers

        private void RebuildTreeChildren()
        {
            foreach (var file in _entries.Values)
                file.Children.Clear();

            foreach (var file in _entries.Values)
            {
                if (file.Filename == "\\")
                    continue;

                var dir = "\\" + Path.GetDirectoryName(file.Filename);

                FileEntry entry;
                if (_entries.TryGetValue(dir, out entry))
                    entry.Children.Add(file);
            }
        }

        private void LoadOverlay()
        {
            if (string.IsNullOrEmpty(OverlayPath))
                return;

            // Add local files & directories
            if (!Directory.Exists(OverlayPath))
                Directory.CreateDirectory(OverlayPath);

            AddPathToOverlay(OverlayPath);

            // Overlay of deleted files
            _overlay = new OverlayDeletionRepository(Path.Combine(OverlayPath, ".deletions"));
            foreach (var removed in _overlay.DeletedFiles)
                _entries.Remove(removed.Replace("/", "\\"));
        }

        private void AddPathToOverlay(string path)
        {
            var allLocalFiles = Directory.GetDirectories(path, "*.*", SearchOption.AllDirectories).Concat(
                                Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));

            foreach (var file in allLocalFiles)
            {
                if (file.IndexOf(OverlayPath) != 0)
                    continue;

                var relative = file.Substring(OverlayPath.Length);
                var relativeLower = relative;

                if (relativeLower == "\\.deletions")
                    continue;

                FileEntry entry = new OverlayFileEntry(relative, file);
                _entries[relativeLower] = entry;
            }
        }

        private FileEntry GetFile(string fileName)
        {
            FileEntry value;
            if (_entries.TryGetValue(fileName, out value))
                return value;

            return null;
        }

        IList<FileInformation> FindFilesHelper(string fileName, string searchPattern)
        {
            var item = GetFile(fileName);
            if (item == null)
                return null;

            if (item.IsDirectory)
            {
                if (item.Children == null)
                    return new FileInformation[] { };

                var matcher = GetMatcher(searchPattern);
                return item.Children.Where(x => matcher(Path.GetFileName(x.Filename))).Select(x => GetFileInformation(x)).ToList();
            }
            return null;
        }

        static Func<string, bool> GetMatcher(string searchPattern)
        {
            // Strange : dokan replaces * with < -> Restore * back
            searchPattern = searchPattern.Replace("<", "*");

            if (searchPattern == "*") 
                return (k) => true;

            if (searchPattern.IndexOf('?') == -1 && searchPattern.IndexOf('*') == -1) 
                return key => key.Equals(searchPattern, StringComparison.OrdinalIgnoreCase);

            var regex = "^" + Regex.Escape(searchPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return key => Regex.IsMatch(key, regex, RegexOptions.IgnoreCase);
        }
        
        FileEntry CreateToOverlay(string fileName, FileEntry item, bool directory = false)
        {
            if (string.IsNullOrEmpty(fileName) || fileName == "\\")
                return item;

            string fullPath = Path.Combine(OverlayPath, fileName.Substring(1));

            if (item == null)
            {
                if (directory)
                    Directory.CreateDirectory(fullPath);
                else
                    File.WriteAllText(fullPath, "");

                var ret = new OverlayFileEntry(fileName, fullPath);
                ret.IsDirectory = directory;
                _entries[fileName.Replace("/", "\\")] = ret;
                _overlay.RestoreFile(ret.Filename);
                RebuildTreeChildren();

                return ret;
            }

            if (item != null && !(item is OverlayFileEntry))
            {
                if (directory)
                {
                    FileTools.CopyDirectory(item.PhysicalPath, fullPath, true);

                    var childItems = item.Traverse(c => c.Children).Skip(1).ToList();
                    var keysToRemove = _entries.Where(e => childItems.Contains(e.Value)).Select(e => e.Key).ToList();
                    foreach (var keyToRemove in keysToRemove)
                        _entries.Remove(keyToRemove);

                    AddPathToOverlay(fullPath);
                }
                else
                {
                    using (var fs = new FileStream(fullPath, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.ReadWrite))
                    using (var stream = item.GetPhysicalFileStream())
                        stream.CopyTo(fs);
                }

                var ret = new OverlayFileEntry(fileName, fullPath);
                ret.IsDirectory = directory;
                _entries[fileName.Replace("/", "\\")] = ret;
                _overlay.RestoreFile(ret.Filename);
                RebuildTreeChildren();

                return ret;
            }

            return item;
        }

        private FileInformation GetFileInformation(FileEntry item)
        {
            return new FileInformation()
            {
                Attributes = item.IsDirectory ? FileAttributes.Directory : FileAttributes.NotContentIndexed, //(FileAttributes)item.Attributes,
                Length = item.Length,
                CreationTime = item.CreationTime,
                FileName = Path.GetFileName(item.Filename),
                LastAccessTime = item.LastAccessTime,
                LastWriteTime = item.LastWriteTime
            };
        }

        #endregion


        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return NtStatus.Success;
        }
    }
}
