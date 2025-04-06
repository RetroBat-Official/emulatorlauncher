using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.ComponentModel;
using EmulatorLauncher.Common.Compression.Wrappers;

namespace EmulatorLauncher.Common.Compression
{
    public class Zip
    {
        public static IArchive Open(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
                return null;

            if (ext.Contains("squashfs"))
            {
                try
                {
                    // Try to open with 7z
                    if (SevenZipArchive.IsSevenZipAvailable)
                        return SevenZipArchive.Open(path);
                }
                catch { }

                if (SquashFsArchive.IsSquashFsAvailable)
                    return SquashFsArchive.Open(path);
            }

            try
            {
                // Try 7z first as it's faster
                if (SevenZipArchive.IsSevenZipAvailable)
                    return SevenZipArchive.Open(path);
            }
            catch { }

            return ZipArchive.Open(path);
        }

        public static bool IsCompressedFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName) || Directory.Exists(fileName))
                return false;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".zip" || ext == ".7z" || ext == ".rar" || ext.Contains("squashfs");
        }

        public static bool IsFreeDiskSpaceAvailableForExtraction(string filename, string pathForExtraction)
        {
            try
            {
                var totalRequiredSize = Zip.ListEntries(filename).Sum(f => f.Length);
                if (totalRequiredSize == 0)
                    totalRequiredSize = new FileInfo(filename).Length;

                long freeSpaceOnDrive = new DriveInfo(Path.GetPathRoot(pathForExtraction)).AvailableFreeSpace;
                if (freeSpaceOnDrive < totalRequiredSize)
                    return false;
            }
            catch { }

            return true;
        }

        public static IArchiveEntry[] ListEntries(string path)
        {
            IArchiveEntry[] ret;
            if (!_entriesCache.TryGetValue(path, out ret))
            {
                ret = ListEntriesInternal(path);
                _entriesCache[path] = ret;
            }

            return ret;
        }

        private static IArchiveEntry[] ListEntriesInternal(string path)
        {
            try
            {
                if (IsCompressedFile(path))
                    using (var archive = Zip.Open(path))
                        return archive == null ? new IArchiveEntry[0] : archive.Entries;                
            }
            catch { }
            
            return new IArchiveEntry[0];
        }

        private static Dictionary<string, IArchiveEntry[]> _entriesCache = new Dictionary<string, IArchiveEntry[]>();

        public static void Extract(string path, string destination, string fileNameToExtract = null, ProgressChangedEventHandler progress = null, bool keepFolder = false)
        {
            if (!IsCompressedFile(path))
                return;

            using (var archive = Zip.Open(path))
                if (archive != null)
                    archive.Extract(destination, fileNameToExtract, progress, keepFolder);
        }

        public static void CleanupUncompressedWSquashFS(string zipFile, string uncompressedPath)
        {
            if (Path.GetExtension(zipFile).ToLowerInvariant() != ".wsquashfs")
                return;

            string[] pathsToDelete = new string[]
                {
                    "dosdevices",
                    "system.reg",
                    "userdef.reg",
                    "user.reg",
                    ".update-timestamp",
                    "drive_c\\windows",
                    "drive_c\\Program Files\\Common Files\\System",
                    "drive_c\\Program Files\\Common Files\\Microsoft Shared",
                    "drive_c\\Program Files\\Internet Explorer",
                    "drive_c\\Program Files\\Windows Media Player",
                    "drive_c\\Program Files\\Windows NT",
                    "drive_c\\Program Files (x86)\\Common Files\\System",
                    "drive_c\\Program Files (x86)\\Common Files\\Microsoft Shared",
                    "drive_c\\Program Files (x86)\\Internet Explorer",
                    "drive_c\\Program Files (x86)\\Windows Media Player",
                    "drive_c\\Program Files (x86)\\Windows NT",
                    "drive_c\\users\\Public",
                    "drive_c\\ProgramData\\Microsoft"
                };

            foreach (var path in pathsToDelete)
            {
                string folder = Path.Combine(uncompressedPath, path);
                if (Directory.Exists(folder))
                {
                    try { Directory.Delete(folder, true); }
                    catch { }
                }
                else if (File.Exists(folder))
                {
                    try { File.Delete(folder); }
                    catch { }
                }

                try
                {
                    var parent = Path.GetDirectoryName(folder);
                    if (Directory.Exists(parent))
                        Directory.Delete(parent);
                }
                catch { }
            }
        }
    }

}
