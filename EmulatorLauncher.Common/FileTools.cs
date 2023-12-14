using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Security.Cryptography;

namespace EmulatorLauncher.Common
{
    public static class FileTools
    {
        /// <summary>
        /// Get MD5 hash
        /// </summary>
        /// <param name="file"></param>
        public static string GetMD5(string file)
        {
            if (!File.Exists(file))
                return null;

            using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(file))
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
        }

        /// <summary>
        /// Get SHA-1 hash
        /// </summary>
        /// <param name="file"></param>
        public static string GetSHA1(string file)
        {
            if (!File.Exists(file))
                return null;

            using (FileStream fs = File.OpenRead(file))
            {
                SHA1 sha = new SHA1Managed();
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", string.Empty).ToLower();
            }
        }

        /// <summary>
        /// Get File size
        /// </summary>
        /// <param name="file"></param>
        public static long GetFileSize(string file)
        {
            if (!File.Exists(file))
                return 0;

            return new FileInfo(file).Length;            
        }

        public static bool ExtractGZipBytes(byte[] bytes, string fileName)
        {
            try
            {
                using (var reader = new MemoryStream(bytes))
                {
                    using (var decompressedStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    {
                        using (GZipStream decompressionStream = new GZipStream(reader, CompressionMode.Decompress))
                        {
                            decompressionStream.CopyTo(decompressedStream);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[ReadGZipStream] Failed " + ex.Message, ex);
            }

            return false;
        }

        public static string GetRelativePath(string basePath, string targetPath)
        {
            Uri baseUri = new Uri(basePath);
            Uri targetUri = new Uri(targetPath);

            Uri relativeUri = baseUri.MakeRelativeUri(targetUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', '\\');
        }

        public static string FindFreeDriveLetter()
        {
            var drives = DriveInfo.GetDrives();

            for (char letter = 'Z'; letter >= 'D'; letter--)
                if (!drives.Any(d => d.Name == letter + ":\\"))
                    return letter + ":\\";

            return null;
        }

        private static void TryMoveFile(string sourceFileName, string destFileName)
        {
            if (File.Exists(sourceFileName))
            {
                if (File.Exists(destFileName))
                {
                    try { File.Delete(destFileName); }
                    catch { }
                }

                try { File.Move(sourceFileName, destFileName); }
                catch { }
            }
        }

        public static void TryCreateDirectory(string path)
        {
            if (Directory.Exists(path))
                return;

            try { Directory.CreateDirectory(path); }
            catch { }
        }

        public static void TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return;

            try { File.Delete(path); }
            catch { }
        }

        public static void TryCopyFile(string sourceFileName, string destFileName, bool overwrite = true)
        {
            if (!File.Exists(sourceFileName))
                return;

            try { File.Copy(sourceFileName, destFileName, overwrite); }
            catch { }
        }

        #region Apis
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint OPEN_EXISTING = 3;
        const uint FILE_SHARE_READ = 1;
        const uint FILE_SHARE_WRITE = 2;

        static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);
        #endregion

        public static bool IsFileLocked(string path)
        {
            if (!File.Exists(path))
                return false;

            IntPtr handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == INVALID_HANDLE_VALUE)
                return true;

            CloseHandle(handle);
            return false;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException("Source directory not found: " + dir.FullName);

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        public static void CreateSymlink(string destinationLink, string pathToLink, bool directory = true)
        {
            string workingDirectory = Path.GetDirectoryName(destinationLink);
            string directoryName = Path.GetFileName(destinationLink);

            var psi = new ProcessStartInfo("cmd.exe", directory ?
                "/C mklink /J \"" + directoryName + "\" \"" + pathToLink + "\"" :
                "/C mklink \"" + directoryName + "\" \"" + pathToLink + "\"");
            psi.WorkingDirectory = workingDirectory;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi).WaitForExit();
        }

        public static void CompressDirectory(string _outputFolder)
        {
            var dir = new DirectoryInfo(_outputFolder);

            if (!dir.Exists)
            {
                dir.Create();
            }

            if ((dir.Attributes & FileAttributes.Compressed) == 0)
            {
                try
                {
                    // Enable compression for the output folder
                    // (this will save a ton of disk space)

                    string objPath = "Win32_Directory.Name=" + "'" + dir.FullName.Replace("\\", @"\\").TrimEnd('\\') + "'";

                    using (ManagementObject obj = new ManagementObject(objPath))
                    {
                        using (obj.InvokeMethod("Compress", null, null))
                        {
                            // I don't really care about the return value, 
                            // if we enabled it great but it can also be done manually
                            // if really needed
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Cannot enable compression for folder '" + dir.FullName + "': " + ex.Message, "WMI");
                }
            }
        }

        [DllImport("shell32.dll")]
        public static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out]StringBuilder lpszPath, int nFolder, bool fCreate);

        public static string GetSystemDirectory()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                StringBuilder path = new StringBuilder(260);
                SHGetSpecialFolderPath(IntPtr.Zero, path, 0x0029, false); // CSIDL_SYSTEMX86
                return path.ToString();
            }

            return Environment.SystemDirectory;
        }

        public static string GetShortcutTarget(string file)
        {
            try
            {
                if (System.IO.Path.GetExtension(file).ToLower() != ".lnk")
                {
                    throw new Exception("Supplied file must be a .LNK file");
                }

                FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read);
                using (System.IO.BinaryReader fileReader = new BinaryReader(fileStream))
                {
                    fileStream.Seek(0x14, SeekOrigin.Begin);     // Seek to flags
                    uint flags = fileReader.ReadUInt32();        // Read flags
                    if ((flags & 1) == 1)
                    {                      // Bit 1 set means we have to
                                           // skip the shell item ID list
                        fileStream.Seek(0x4c, SeekOrigin.Begin); // Seek to the end of the header
                        uint offset = fileReader.ReadUInt16();   // Read the length of the Shell item ID list
                        fileStream.Seek(offset, SeekOrigin.Current); // Seek past it (to the file locator info)
                    }

                    long fileInfoStartsAt = fileStream.Position; // Store the offset where the file info
                                                                 // structure begins
                    uint totalStructLength = fileReader.ReadUInt32(); // read the length of the whole struct
                    fileStream.Seek(0xc, SeekOrigin.Current); // seek to offset to base pathname
                    uint fileOffset = fileReader.ReadUInt32(); // read offset to base pathname
                                                               // the offset is from the beginning of the file info struct (fileInfoStartsAt)
                    fileStream.Seek((fileInfoStartsAt + fileOffset), SeekOrigin.Begin); // Seek to beginning of
                                                                                        // base pathname (target)
                    long pathLength = (totalStructLength + fileInfoStartsAt) - fileStream.Position - 2; // read
                                                                                                        // the base pathname. I don't need the 2 terminating nulls.
                    char[] linkTarget = fileReader.ReadChars((int)pathLength); // should be unicode safe
                    var link = new string(linkTarget);

                    int begin = link.IndexOf("\0\0");
                    if (begin > -1)
                    {
                        int end = link.IndexOf("\\\\", begin + 2) + 2;
                        end = link.IndexOf('\0', end) + 1;

                        string firstPart = link.Substring(0, begin);
                        string secondPart = link.Substring(end);

                        return firstPart + secondPart;
                    }
                    else
                    {
                        return link;
                    }
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
