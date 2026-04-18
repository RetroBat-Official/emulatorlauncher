using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Management;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace EmulatorLauncher.Common.Compression
{
    public class MountFile : IDisposable
    {
        public static MountFile Mount(string filename, string extractionpath, string overlayPath, bool useSquash)
        {
            if (!Zip.IsCompressedFile(filename))
                return null;

            string mountPath = Path.Combine(Path.GetDirectoryName(typeof(MountFile).Assembly.Location), "mount.exe");

            if (useSquash && IsWinFspAvailable())
            {
                string mountExeDir = Path.GetDirectoryName(mountPath);
                mountPath = Path.Combine(mountExeDir, "mountsquashfs.exe");

                if (File.Exists(mountPath))
                    SimpleLogger.Instance.Info("[GENERATOR] Using winfsp for mounting drive.");
                else
                {
                    mountPath = Path.Combine(Path.GetDirectoryName(typeof(MountFile).Assembly.Location), "mount.exe");
                    SimpleLogger.Instance.Info("[GENERATOR] winfsp not found, using dokan for mounting drive.");
                }
            }
            else if (useSquash && !IsWinFspAvailable() && !IsDokanAvailable())
            {
                SimpleLogger.Instance.Info("[GENERATOR] Neither WinFSP nor Dokan available, cannot mount.");
                return null;
            }
            else if (IsDokanAvailable())
            {
                SimpleLogger.Instance.Info("[GENERATOR] Using Dokan for mounting drive.");
            }
            else
                return null;

            if (!File.Exists(mountPath))
                return null;

            var drive = FileTools.FindFreeDriveLetter();
            if (drive == null)
                return null;

            if (!Zip.IsFreeDiskSpaceAvailableForExtraction(filename, extractionpath))
                return null;

            if (!Directory.Exists(extractionpath))
            {
                Directory.CreateDirectory(extractionpath);
                FileTools.CompressDirectory(extractionpath);
            }
         
            List<string> args = new List<string>();      
    
            if (Debugger.IsAttached)
                args.Add("-debug");

            args.Add("-drive");
            args.Add(drive.Substring(0, 2));

            if (!string.IsNullOrEmpty(extractionpath))
            {
             //   extractionpath = Path.Combine(extractionpath, Path.GetFileName(filename));

                args.Add("-extractionpath");
                args.Add("\"" + extractionpath + "\"");

                Directory.CreateDirectory(extractionpath);
            }

            if (!string.IsNullOrEmpty(overlayPath))
            {
                args.Add("-overlay");
                args.Add("\"" + overlayPath + "\"");

                Directory.CreateDirectory(overlayPath);
            }

            args.Add("\"" + filename + "\"");

            var mountProcess = Process.Start(new ProcessStartInfo()
            {
                FileName = mountPath,
                WorkingDirectory = Path.GetDirectoryName(filename),
                Arguments = string.Join(" ", args.ToArray()),
                UseShellExecute = false,
                CreateNoWindow = !Debugger.IsAttached
            });

            int startTime = Environment.TickCount;

            while (Environment.TickCount - startTime < 5000)
            {
                if (mountProcess.WaitForExit(10))
                    return null;

                if (Directory.Exists(drive))
                {
                    Job.Current.AddProcess(mountProcess);
                    return new MountFile(mountProcess, filename, drive, extractionpath, overlayPath);
                }
            }

            try { mountProcess.Kill(); }
            catch { }

            return null;
        }

        private static bool IsWinFspAvailable()
        {
            // WinFSP standard install paths
            string dll64 = @"C:\Program Files (x86)\WinFsp\bin\winfsp-x64.dll";
            string dll32 = @"C:\Program Files (x86)\WinFsp\bin\winfsp-x86.dll";

            return File.Exists(dll64) || File.Exists(dll32);
        }

        private static bool IsDokanAvailable()
        {
            string dokan = Environment.GetEnvironmentVariable("DokanLibrary2");
            if (!Directory.Exists(dokan))
                return false;

            dokan = Path.Combine(dokan, "dokan2.dll");
            if (!File.Exists(dokan))
                return false;

            return true;
        }

        private MountFile(Process process, string filename, string driveLetter, string extractionpath, string overlayPath)
        {
            Filename = filename;
            DriveLetter = driveLetter;
            ExtractionPath = extractionpath;
            OverlayPath = overlayPath;
            Process = process;
        }

        public string Filename { get; private set; }
        public string DriveLetter { get; private set; }
        public string ExtractionPath { get; private set; }
        public string OverlayPath { get; private set; }

        public Process Process { get; private set; }

        public void UnMount()
        {
            try { Process.Kill(); }
            catch { }
        }

        public void Dispose()
        {
            UnMount();
        }

        public static void ShowDownloadDokanPage()
        {
            Process.Start("https://github.com/dokan-dev/dokany/releases");
        }
    }
}
