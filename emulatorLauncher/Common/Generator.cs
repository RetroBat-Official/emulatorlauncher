using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;
using System.IO;
using System.Drawing;

namespace emulatorLauncher
{
    abstract class Generator
    {
        #region Custom game unzip

        private static string GetUnCompressedFolderPath()
        {
            string romPath = Program.AppConfig.GetFullPath("roms");
            if (!string.IsNullOrEmpty(romPath))
                return Path.Combine(romPath, ".uncompressed");

            return null;
        }

        protected string TryUnZipGameIfNeeded(string system, string fileName, bool silent = false)
        {
            if (string.IsNullOrEmpty(GetUnCompressedFolderPath()))
                return fileName;

            _unzip = GameUnzip.UnZipGame(system, fileName, silent);
            if (_unzip != null)
                return _unzip.UncompressedPath;

            return fileName;
        }

        public void ValidateUncompressedGame()
        {
            if (_unzip != null)
                _unzip.SilentDelete = false;
        }

        private GameUnzip _unzip;

        public virtual void Cleanup()
        {
            if (_unzip != null)
            {
                if (!_unzip.SilentDelete && Program.SystemConfig["decompressedfolders"] == "keep")
                    return;

                if (_unzip.SilentDelete || Program.SystemConfig["decompressedfolders"] == "delete")
                {
                    try { Directory.Delete(_unzip.UncompressedPath, true); }
                    catch { }

                    try { Directory.Delete(_unzip.ExtractionPath); }
                    catch { }
                }
                else
                {
                    using (var frm = new InstallerFrm())
                    {
                        frm.SetLabel(Properties.Resources.KeepUncompressedFile);
                        if (frm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        {
                            try { Directory.Delete(_unzip.UncompressedPath, true); }
                            catch { }

                            try { Directory.Delete(_unzip.ExtractionPath); }
                            catch { }
                        }
                    }
                }

                try { Directory.Delete(GetUnCompressedFolderPath()); }
                catch { }                

                _unzip = null;
            }
        }
        
        private class GameUnzip
        {
            public static GameUnzip UnZipGame(string system, string filename, bool silent = false)
            {
                string path = Path.GetDirectoryName(filename);

                string extractionPath = GetUnCompressedFolderPath();

                if (!Directory.Exists(extractionPath))
                {
                    Directory.CreateDirectory(extractionPath);
                    Misc.CompressDirectory(extractionPath);
                }

                extractionPath = Path.Combine(extractionPath, system);
                string dest = Path.Combine(extractionPath, Path.GetFileNameWithoutExtension(filename));

                if (Directory.Exists(dest) && Directory.GetFiles(dest, "*.*", SearchOption.AllDirectories).Any())
                {
                    try { Directory.SetLastWriteTime(dest, DateTime.Now); }
                    catch { }

                    var ret = new GameUnzip();
                    ret.ZipFile = filename;
                    ret.ExtractionPath = extractionPath;
                    ret.UncompressedPath = dest;
                    return ret;
                }

                if (Zip.IsCompressedFile(filename))
                {
                    if (silent)
                        Zip.Extract(filename, dest);
                    else
                    {
                        using (var frm = new InstallerFrm())
                        {
                            if (!frm.UnCompressFile(filename, dest))
                                throw new Exception("Unable to decompress file");
                        }
                    }

                    if (Directory.Exists(dest))
                    {
                        try { Directory.SetLastWriteTime(dest, DateTime.Now); }
                        catch { }

                        var ret = new GameUnzip();
                        ret.ZipFile = filename;
                        ret.ExtractionPath = extractionPath;
                        ret.UncompressedPath = dest;
                        ret.CleanupUncompressedWSquashFS();
                        return ret;
                    }
                }

                return null;
            }

            private GameUnzip() { SilentDelete = true; }

            public string ZipFile { get; set; }
            public string UncompressedPath { get; set; }
            public string ExtractionPath { get; set; }
            public bool SilentDelete { get; set; }

            private void CleanupUncompressedWSquashFS()
            {
                if (Path.GetExtension(ZipFile).ToLowerInvariant() != ".wsquashfs")
                    return;

                string[] pathsToDelete = new string[]
                {
                    "dosdevices",
                    "system.reg",
                    "userdef.reg",
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
                    string folder = Path.Combine(UncompressedPath, path);
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

        #endregion

        #region Error
        protected void SetCustomError(string message)
        {
            try
            {
                ExitCode = ExitCodes.CustomError;
                Program.WriteCustomErrorFile(message);
            }
            catch 
            { 
                
            }
        }

        public ExitCodes ExitCode { get; protected set; }
        #endregion

        public Generator()
        {
            UseEsPadToKey = true;
            DependsOnDesktopResolution = false;
            ExitCode = ExitCodes.EmulatorNotInstalled;
        }

        protected EsFeatures Features { get { return Program.Features; } }
        protected ConfigFile AppConfig { get { return Program.AppConfig; } }
        protected ConfigFile SystemConfig { get { return Program.SystemConfig; } }
        protected List<Controller> Controllers { get { return Program.Controllers; } }

        public abstract ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution);

        public virtual int RunAndWait(ProcessStartInfo path)
        {
            try 
            {
                var process = Process.Start(path);
                process.WaitForExit();
                                 
                int exitCode = process.ExitCode;

                if (exitCode == unchecked((int)0xc0000005)) // Null pointer - happen sometimes with Yuzu
                    return 0;

                if (exitCode == unchecked((int)0xc0000374)) // Heap corruption - happen sometimes with scummvm
                    return 0;

                return exitCode;
            }
            catch 
            { 

            }

            return -1;
        }

        public bool DependsOnDesktopResolution { get; protected set; }
        public bool UseEsPadToKey { get; protected set; }

        public virtual PadToKey SetupCustomPadToKeyMapping(PadToKeyboard.PadToKey mapping)
        {
            return mapping;
        }


        private Dictionary<string, byte[]> _filesToRestore;

        protected void AddFileForRestoration(string file)
        {
            if (_filesToRestore == null)
                _filesToRestore = new Dictionary<string, byte[]>();

            if (File.Exists(file))
            {
                try { _filesToRestore[file] = File.ReadAllBytes(file); }
                catch { }
            }
        }

        public void RestoreFiles()
        {
            if (_filesToRestore == null)
                return;

            foreach (var file in _filesToRestore)
                File.WriteAllBytes(file.Key, file.Value);
        }

        #region IsEmulationStationWindowed
        static Process GetParentProcess(Process process)
        {
            if (process == null)
                return null;

            try
            {
                using (var query = new System.Management.ManagementObjectSearcher("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId=" + process.Id))
                {
                    return query.Get()
                      .OfType<System.Management.ManagementObject>()
                      .Select(p => Process.GetProcessById((int)(uint)p["ParentProcessId"]))
                      .FirstOrDefault();
                }
            }
            catch
            {

            }

            return null;
        }

        static string GetProcessCommandline(Process process)
        {
            if (process == null)
                return null;

            try
            {
                using (var cquery = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + process.Id))
                {
                    var commandLine = cquery.Get()
                        .OfType<System.Management.ManagementObject>()
                        .Select(p => (string)p["CommandLine"])
                        .FirstOrDefault();

                    return commandLine;
                }
            }
            catch
            {

            }

            return null;
        }

        public static bool IsEmulationStationWindowed(out Rectangle bounds, bool updateSize = false)
        {
            bool isWindowed = false;

            bounds = new Rectangle();

            var process = GetParentProcess(Process.GetCurrentProcess());
            if (process == null)
                return false;

            var px = GetProcessCommandline(process);
            if (string.IsNullOrEmpty(px))
                return false;

            if (px.IndexOf("emulationstation", StringComparison.InvariantCultureIgnoreCase) < 0)
                return false;

            // Check parent process is EmulationStation. Get its commandline, see if it's using "--windowed --resolution X Y", import settings
            var args = Misc.SplitCommandLine(px).Skip(1).ToArray();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg == "--windowed")
                    isWindowed = true;
                else if (arg == "--resolution" && i + 2 < args.Length)
                    bounds = new Rectangle(0, 0, args[i + 1].ToInteger(), args[i + 2].ToInteger());
            }

            if (isWindowed && bounds.Width > 0 && bounds.Height > 0)
            {
                try
                {
                    var hWnd = process.MainWindowHandle;
                    if (hWnd != IntPtr.Zero && User32.IsWindowVisible(hWnd))
                    {
                        var rect = User32.GetWindowRect(hWnd);
                        if (rect.left != rect.right && rect.top != rect.bottom)
                        {
                            bounds.X = rect.left;
                            bounds.Y = rect.top;

                            if (updateSize)
                            {
                                bounds.Width = rect.right - rect.left;
                                bounds.Height = rect.bottom - rect.top;
                            }
                        }
                        
                    }
                }
                catch { }
            }

            return isWindowed && bounds.Width > 0 && bounds.Height > 0;
        }
        #endregion

        protected string GetCurrentLanguage()
        {
            if (!SystemConfig.isOptSet("Language") || string.IsNullOrEmpty(SystemConfig["Language"]))
                return string.Empty;

            string s = SystemConfig["Language"].ToLowerInvariant();

            int cut = s.IndexOf("_");
            if (cut >= 0)
                return s.Substring(0, cut);

            return s;
        }
    }

    enum ExitCodes : int
    {
        OK = 0,
        EmulatorExitedUnexpectedly = 200,
        BadCommandLine = 201,
        InvalidConfiguration = 202,
        UnknownEmulator = 203,
        EmulatorNotInstalled = 204,
        MissingCore = 205,

        CustomError = 299
    }


}
