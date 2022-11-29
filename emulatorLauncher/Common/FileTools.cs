using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace emulatorLauncher
{
    class FileTools
    {
        public static string FindFreeDriveLetter()
        {
            var drives = DriveInfo.GetDrives();

            for (char letter = 'Z'; letter >= 'D'; letter--)
                if (!drives.Any(d => d.Name == letter + ":\\"))
                    return letter + ":\\";

            return null;
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
                "/C mklink /D \"" + directoryName + "\" \"" + pathToLink + "\"" :
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
    }
}
