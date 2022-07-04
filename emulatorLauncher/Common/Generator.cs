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
        
        #region Custom game unzip

        private static string GetUnCompressedFolderPath()
        {
            string romPath = Program.AppConfig.GetFullPath("roms");
            if (!string.IsNullOrEmpty(romPath))
                return Path.Combine(romPath, ".uncompressed");

            return null;
        }

        private IsoFile _mountedIso;
        private MountFile _mountFile;
        private GameUnzip _unzip;

        protected string TryUnZipGameIfNeeded(string system, string fileName, bool silent = false)
        {
            if (Path.GetExtension(fileName).ToLowerInvariant() == ".iso")
            {
                _mountedIso = IsoFile.MountIso(fileName);
                if (_mountedIso != null)
                    return _mountedIso.Drive.Name;
            }

            // Try mount file as a drive letter
            if (Zip.IsCompressedFile(fileName))
            {
                string extractionPath = GetUnCompressedFolderPath();

                if (Program.SystemConfig["decompressedfolders"] != "keep")
                {
                    // Decompression for mounting is generally faster in temp path as it's generally a SSD Drive...
                    extractionPath = Path.Combine(Path.GetTempPath(), ".uncompressed", Path.GetFileName(fileName));
                }

                if (string.IsNullOrEmpty(extractionPath))
                    return fileName;

                string overlayPath = ".";
                string savesPath = Program.AppConfig.GetFullPath("saves");
                if (!string.IsNullOrEmpty(savesPath))
                    overlayPath = Path.Combine(savesPath, system, Path.GetFileName(fileName));

                _mountFile = MountFile.Mount(fileName, extractionPath, overlayPath);
                if (_mountFile != null)
                    return _mountFile.DriveLetter;
            }

            // Try simple decompression
            if (Zip.IsCompressedFile(fileName))
            {
                string extractionPath = GetUnCompressedFolderPath();
                if (string.IsNullOrEmpty(extractionPath))
                    return fileName;

                if (!Directory.Exists(extractionPath))
                {
                    Directory.CreateDirectory(extractionPath);
                    FileTools.CompressDirectory(extractionPath);
                }

                // Simple decompression
                _unzip = GameUnzip.UnZipGame(system, fileName, silent);
                if (_unzip != null)
                    return _unzip.UncompressedPath;
            }

            return fileName;
        }

        public void ValidateUncompressedGame()
        {
            if (_unzip != null)
                _unzip.SilentDelete = false;
        }

        public virtual void Cleanup()
        {
            if (_mountFile != null)
            {
                // Delete overlay path if it's emptt
                try { Directory.Delete(_mountFile.OverlayPath); }
                catch { }

                string extractionPath = _mountFile.ExtractionPath;

                _mountFile.Dispose();
                _mountFile = null;

                string uncompressedFolderPath = GetUnCompressedFolderPath();

                if (Program.SystemConfig["decompressedfolders"] != "keep")
                    uncompressedFolderPath = Path.Combine(Path.GetTempPath(), ".uncompressed");

                bool deleteExtractedFiles = Program.SystemConfig["decompressedfolders"] != "keep"; // Program.SystemConfig["decompressedfolders"] == "delete";
                /*
                if (Program.SystemConfig["decompressedfolders"] != "keep" && Program.SystemConfig["decompressedfolders"] != "delete")
                {
                    using (var frm = new InstallerFrm())
                    {
                        frm.SetLabel(Properties.Resources.KeepUncompressedFile);
                        if (frm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                            deleteExtractedFiles = true;
                    }
                }
                */
                // Delete Extraction path if required
                if (deleteExtractedFiles)
                {
                    SimpleLogger.Instance.Info("Directory.Delete(" + extractionPath + ", true)");

                    try { Directory.Delete(extractionPath, true); }
                    catch(Exception ex) { SimpleLogger.Instance.Error("Can't delete " + extractionPath + " : " + ex.Message); }

                    SimpleLogger.Instance.Info("Directory.Delete(" + Path.GetDirectoryName(extractionPath) + ", false)");

                    try { Directory.Delete(Path.GetDirectoryName(extractionPath)); }
                    catch (Exception ex) { SimpleLogger.Instance.Error("Can't delete " + extractionPath + " : " + ex.Message); }

                    SimpleLogger.Instance.Info("Directory.Delete(" + uncompressedFolderPath + ", false)");

                    try { Directory.Delete(uncompressedFolderPath); }
                    catch { }                
                }
            }

            if (_mountedIso != null)
            {

                _mountedIso.Dispose();
                _mountedIso = null;
            }

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
                if (!Zip.IsCompressedFile(filename))
                    return null;

                string path = Path.GetDirectoryName(filename);
                string extractionPath = GetUnCompressedFolderPath();

                if (!Zip.IsFreeDiskSpaceAvailableForExtraction(filename, extractionPath))
                    throw new Exception("Not enough free space on drive to decompress");


                if (!Directory.Exists(extractionPath))
                {
                    Directory.CreateDirectory(extractionPath);
                    FileTools.CompressDirectory(extractionPath);
                }

                extractionPath = Path.Combine(extractionPath, system);
                string dest = Path.Combine(extractionPath, Path.GetFileName(filename));

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
                        Zip.CleanupUncompressedWSquashFS(filename, extractionPath);

                        try { Directory.SetLastWriteTime(dest, DateTime.Now); }
                        catch { }

                        var ret = new GameUnzip();
                        ret.ZipFile = filename;
                        ret.ExtractionPath = extractionPath;
                        ret.UncompressedPath = dest;
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
            else
                _filesToRestore[file] = null;
        }

        public void RestoreFiles()
        {
            if (_filesToRestore == null)
                return;

            foreach (var file in _filesToRestore)
            {
                if (file.Value == null)
                {
                    try
                    {
                        if (File.Exists(file.Key))
                            File.Delete(file.Key);
                    }
                    catch { }
                }
                else
                    File.WriteAllBytes(file.Key, file.Value);
            }
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
            var args = px.SplitCommandLine().Skip(1).ToArray();
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
        
        protected void BindFeature(ConfigFile cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                cfg[settingName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindBoolFeature(ConfigFile cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else
                    cfg[settingName] = falseValue;
            }
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
