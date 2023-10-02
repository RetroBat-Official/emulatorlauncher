using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace emulatorLauncher
{
    static class FileTools
    {
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

        public static string FindFreeDriveLetter()
        {
            var drives = DriveInfo.GetDrives();

            for (char letter = 'Z'; letter >= 'D'; letter--)
                if (!drives.Any(d => d.Name == letter + ":\\"))
                    return letter + ":\\";

            return null;
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

        public static string GetLnkTargetPath(string filepath)
        {
            using (var br = new BinaryReader(System.IO.File.OpenRead(filepath)))
            {
                // skip the first 20 bytes (HeaderSize and LinkCLSID)
                br.ReadBytes(0x14);
                // read the LinkFlags structure (4 bytes)
                uint lflags = br.ReadUInt32();
                // if the HasLinkTargetIDList bit is set then skip the stored IDList 
                // structure and header
                if ((lflags & 0x01) == 1)
                {
                    br.ReadBytes(0x34);
                    var skip = br.ReadUInt16(); // this counts of how far we need to skip ahead
                    br.ReadBytes(skip);
                }
                // get the number of bytes the path contains
                var length = br.ReadUInt32();
                // skip 12 bytes (LinkInfoHeaderSize, LinkInfoFlgas, and VolumeIDOffset)
                br.ReadBytes(0x0C);
                // Find the location of the LocalBasePath position
                var lbpos = br.ReadUInt32();
                // Skip to the path position 
                // (subtract the length of the read (4 bytes), the length of the skip (12 bytes), and
                // the length of the lbpos read (4 bytes) from the lbpos)
                br.ReadBytes((int)lbpos - 0x14);
                var size = length - lbpos - 0x02;
                var bytePath = br.ReadBytes((int)size);
                var path = Encoding.UTF8.GetString(bytePath, 0, bytePath.Length);
                return path;
            }
        }
    }
}
