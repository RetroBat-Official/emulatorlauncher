using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class CxbxGenerator : Generator
    {
        #region XboxIsoVfs management
        private Process _xboxIsoVfs;      
        private string _dokanDriveLetter;

        private string MountIso(string rom)
        {
            if (Path.GetExtension(rom).ToLowerInvariant() != ".iso")
                return rom;
                                
            string xboxIsoVfsPath = Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "xbox-iso-vfs.exe");
            if (!File.Exists(xboxIsoVfsPath))
                return rom;

            // Check dokan is installed
            string dokan = Environment.GetEnvironmentVariable("DokanLibrary1");
            if (!Directory.Exists(dokan))
                return rom;

            dokan = Path.Combine(dokan, "dokan1.dll");
            if (!File.Exists(dokan))
                return rom;

            var drive = FindFreeDriveLetter();
            if (drive == null)
                return rom;

            _xboxIsoVfs = Process.Start(new ProcessStartInfo()
            {
                FileName = xboxIsoVfsPath,
                WorkingDirectory = Path.GetDirectoryName(rom),
                Arguments = "\"" + rom + "\" " + drive,
                UseShellExecute = false,    
                CreateNoWindow = true
            });

            int time = Environment.TickCount;
            int elapsed = 0;

            while (elapsed < 5000)
            {
                if (_xboxIsoVfs.WaitForExit(10))
                    return rom;

                if (Directory.Exists(drive))
                {
                    Job.Current.AddProcess(_xboxIsoVfs);

                    _dokanDriveLetter = drive;
                    return drive;
                }

                int newTime = Environment.TickCount;
                elapsed = time - newTime;
                time = newTime;
            }

            try { _xboxIsoVfs.Kill(); }
            catch { }

            return rom;
        }


        private static string FindFreeDriveLetter()
        {
            var drives = DriveInfo.GetDrives();
            for (char letter = 'Z'; letter >= 'D'; letter++)
            {
                if (!drives.Any(d => d.Name == letter + ":\\"))
                    return letter + ":\\";
            }

            return null;
        }

        #endregion

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            rom = MountIso(rom);

            string path = null;
            
            if ((core != null && core == "chihiro") || (emulator != null && emulator == "chihiro"))
                path = AppConfig.GetFullPath("chihiro");
            
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-r");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-reloaded");

            // If rom is a directory
            if (Directory.Exists(rom))
            {
                var xbe = Directory.GetFiles(rom, "*.xbe").FirstOrDefault(r => Path.GetFileNameWithoutExtension(r).ToLowerInvariant() == "default");
                if (string.IsNullOrEmpty(xbe))
                    xbe = Directory.GetFiles(rom, "*.xbe").FirstOrDefault();

                if (!string.IsNullOrEmpty(xbe))
                    rom = xbe;
            }

            string loaderExe = Path.Combine(path, "cxbxr-ldr.exe");
            if (File.Exists(loaderExe))
            {
                return new ProcessStartInfo()
                {
                    FileName = loaderExe,
                    WorkingDirectory = path,
                    Arguments = "/load \"" + rom + "\"",
                };
            }

            string exe = Path.Combine(path, "cxbx.exe");
            if (!File.Exists(exe))
                return null;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }
    }
}
