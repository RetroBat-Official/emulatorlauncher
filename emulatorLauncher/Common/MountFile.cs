using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using emulatorLauncher.imapi2;
using System.Runtime.InteropServices.ComTypes;
using System.Management;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace emulatorLauncher.Tools
{
    class MountFile : IDisposable
    {
        public static MountFile Mount(string filename, string extractionpath, string overlayPath)
        {
            if (!Zip.IsCompressedFile(filename))
                return null;

            string mountPath = Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "mount.exe");
            if (!File.Exists(mountPath))
                return null;

            string dokan = Environment.GetEnvironmentVariable("DokanLibrary1");
            if (!Directory.Exists(dokan))
                return null;

            dokan = Path.Combine(dokan, "dokan1.dll");
            if (!File.Exists(dokan))
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

            int time = Environment.TickCount;
            int elapsed = 0;

            while (elapsed < 5000)
            {
                if (mountProcess.WaitForExit(10))
                    return null;

                if (Directory.Exists(drive))
                {
                    Job.Current.AddProcess(mountProcess);
                    return new MountFile(mountProcess, filename, drive, extractionpath, overlayPath);
                }

                int newTime = Environment.TickCount;
                elapsed = time - newTime;
                time = newTime;
            }

            try { mountProcess.Kill(); }
            catch { }

            return null;
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
            Process.Start("https://github.com/dokan-dev/dokany/releases/tag/v1.4.0.1000");
        }
    }
}
