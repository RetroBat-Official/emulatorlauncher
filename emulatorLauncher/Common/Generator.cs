﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using Newtonsoft.Json.Linq;

namespace EmulatorLauncher
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
        private string _customPathValue = null;

        protected string TryUnZipGameIfNeeded(string system, string fileName, bool silent = false, bool tryMount = true)
        {
            if (Path.GetExtension(fileName).ToLowerInvariant() == ".iso")
            {
                _mountedIso = IsoFile.MountIso(fileName);
                if (_mountedIso != null)
                    return _mountedIso.Drive.Name;
            }

            // Try mount file as a drive letter
            if (Zip.IsCompressedFile(fileName) && tryMount)
            {
                string extractionPath = GetUnCompressedFolderPath();

                if (SystemConfig.isOptSet("decompressedpath") && !string.IsNullOrEmpty(SystemConfig["decompressedpath"]))
                {
                    extractionPath = SystemConfig["decompressedpath"].Replace('/', '\\');
                    extractionPath = Path.Combine(extractionPath, ".uncompressed");

                    if (!Directory.Exists(extractionPath))
                        try { Directory.CreateDirectory(extractionPath); }
                        catch { }
                    if (Directory.Exists(extractionPath))
                        _customPathValue = extractionPath;
                }

                else if (Program.SystemConfig["decompressedfolders"] != "keep")
                {
                    // Decompression for mounting is generally faster in temp path as it's generally a SSD Drive...
                    extractionPath = Path.Combine(Path.GetTempPath(), ".uncompressed", Path.GetFileName(fileName));
                }
                else
                    extractionPath = Path.Combine(extractionPath, Path.GetFileName(fileName));

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

                if (SystemConfig.isOptSet("decompressedpath") && !string.IsNullOrEmpty(SystemConfig["decompressedpath"]))
                {
                    extractionPath = SystemConfig["decompressedpath"].Replace('/', '\\');
                    extractionPath = Path.Combine(extractionPath, ".uncompressed");
                    _customPathValue = extractionPath;
                }

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
            SimpleLogger.Instance.Info("[Generator] Cleanup.");
            if (_mountFile != null)
            {
                // Delete overlay path if it's empty
                try { Directory.Delete(_mountFile.OverlayPath); }
                catch { }

                string extractionPath = _mountFile.ExtractionPath;

                _mountFile.Dispose();
                _mountFile = null;

                string uncompressedFolderPath = GetUnCompressedFolderPath();

                if (SystemConfig.isOptSet("decompressedpath") && !string.IsNullOrEmpty(SystemConfig["decompressedpath"]))
                {
                    extractionPath = SystemConfig["decompressedpath"].Replace('/', '\\');
                    extractionPath = Path.Combine(extractionPath, ".uncompressed");
                }

                else if (Program.SystemConfig["decompressedfolders"] != "keep")
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
                    SimpleLogger.Instance.Info("[Generator] Directory.Delete(" + extractionPath + ", true)");

                    try { Directory.Delete(extractionPath, true); }
                    catch(Exception ex) { SimpleLogger.Instance.Error("Can't delete " + extractionPath + " : " + ex.Message); }
                    
                    try 
                    {
                        string parent = Path.GetDirectoryName(extractionPath);
                        if (Directory.Exists(parent))
                        {
                            SimpleLogger.Instance.Info("[Generator] Directory.Delete(" + parent + ", false)");
                            Directory.Delete(parent);
                        }
                    }
                    catch (Exception ex) 
                    { 
                        SimpleLogger.Instance.Error("Can't delete " + extractionPath + " : " + ex.Message); 
                    }

                    try
                    {
                        if (Directory.Exists(uncompressedFolderPath))
                        {
                            SimpleLogger.Instance.Info("[Generator] Directory.Delete(" + uncompressedFolderPath + ", false)");
                            Directory.Delete(uncompressedFolderPath);
                        }
                    }
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
                        frm.SetLabel(EmulatorLauncher.Properties.Resources.KeepUncompressedFile);
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

                if (Program.SystemConfig.isOptSet("decompressedpath") && !string.IsNullOrEmpty(Program.SystemConfig["decompressedpath"]))
                {
                    extractionPath = Program.SystemConfig["decompressedpath"].Replace('/', '\\');
                    extractionPath = Path.Combine(extractionPath, ".uncompressed");
                }

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

        public string GetUnzippedRomForSystem(string rom, string core, string system)
        {
            string[] psxExtensions = new string[] { ".m3u", ".cue", ".img", ".mdf", ".pbp", ".toc", ".cbn", ".ccd", ".iso", ".cso" };
            string[] extensions = new string[] { };

            if (system == "psx")
                extensions = psxExtensions;
            else
                return null;

            string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
            if (Directory.Exists(uncompressedRomPath))
            {
                string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                ValidateUncompressedGame();
                return rom;
            }
            
            return null;
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
            catch { }

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

        public static bool IsEmulationStationWindowed()
        {
            Rectangle bounds;
            return IsEmulationStationWindowed(out bounds);
        }

        public static bool IsEmulationStationWindowed(out Rectangle bounds, bool updateSize = false)
        {
            bool isWindowed = false;

            bounds = new Rectangle();

            var process = GetParentProcess(Process.GetCurrentProcess());
            if (process == null)
                return false;

            var px = process.GetProcessCommandline();
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

        // xml bindfeatures
        protected void BindFeature(System.Xml.Linq.XElement cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
           if (force || Features.IsSupported(featureName))
                cfg.SetElementValue(settingName, SystemConfig.GetValueOrDefault(featureName, defaultValue));
        }

        protected void BindBoolFeature(System.Xml.Linq.XElement cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            { 
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg.SetElementValue(settingName, trueValue);
                else
                    cfg.SetElementValue(settingName, falseValue);
            }
        }

        protected void BindBoolFeatureOn(System.Xml.Linq.XElement cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg.SetElementValue(settingName, falseValue);
                else
                    cfg.SetElementValue(settingName, trueValue);
            }
        }

        protected void BindFeatureSlider(System.Xml.Linq.XElement cfg, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        cfg.SetElementValue(settingName, SystemConfig.GetValueOrDefault(featureName, value.Substring(0, value.Length - toRemove)));
                    else
                        cfg.SetElementValue(settingName, SystemConfig.GetValueOrDefault(featureName, value));
                }
                else
                    cfg.SetElementValue(settingName, defaultValue);
            }
        }

        // yml and bml bindfeatures
        protected void BindFeature(YmlContainer cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                cfg[settingName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindFeature(BmlContainer cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                cfg[settingName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindFeatureSlider(BmlContainer cfg, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        cfg[settingName] = value.Substring(0, value.Length - toRemove);
                    else
                        cfg[settingName] = value;
                }
                else
                    cfg[settingName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue);
            }
        }

        protected void BindBoolFeature(YmlContainer cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else
                    cfg[settingName] = falseValue;
            }   
        }

        protected void BindBoolFeatureOn(YmlContainer cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = trueValue;
            }
        }

        protected void BindFeatureSlider(YmlContainer cfg, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        cfg[settingName] = value.Substring(0, value.Length - toRemove);
                    else
                        cfg[settingName] = value;
                }
                else
                    cfg[settingName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue);
            }
        }

        protected void BindBoolFeature(BmlContainer cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else
                    cfg[settingName] = falseValue;
            }
        }

        protected void BindBoolFeatureOn(BmlContainer cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = trueValue;
            }
        }

        protected void BindFeature(IniSection cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                cfg[settingName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        // Qtini bindfeatures
        protected void BindQtIniFeature(IniFile ini, string section, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                bool isCustomValue = SystemConfig.isOptSet(featureName) && !string.IsNullOrEmpty(SystemConfig[featureName]);
                string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);

                ini.WriteValue(section, settingName + "\\default", isCustomValue ? "false" : "true");                
                ini.WriteValue(section, settingName, value);                
            }
        }

        protected void BindQtBoolIniFeature(IniFile ini, string section, string settingName, string featureName, string trueValue, string falseValue, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                bool isCustomValue = SystemConfig.isOptSet(featureName) && !string.IsNullOrEmpty(SystemConfig[featureName]);
                string value = defaultValue;

                if (SystemConfig.isOptSet(featureName))
                {
                    if (SystemConfig.getOptBoolean(featureName))
                        value = trueValue;
                    else if (!SystemConfig.getOptBoolean(featureName))
                        value = falseValue;
                }

                ini.WriteValue(section, settingName + "\\default", isCustomValue ? "false" : "true");
                ini.WriteValue(section, settingName, value);
            }
        }

        // Dynamic json bindfeatures
        protected void BindFeature(DynamicJson cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                cfg[settingName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindBoolFeature(DynamicJson cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else
                    cfg[settingName] = falseValue;
            } 
        }

        protected void BindBoolFeatureOn(DynamicJson cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = trueValue;
            }
        }

        protected void BindBoolFeatureAuto(DynamicJson cfg, string settingName, string featureName, string trueValue, string falseValue, string autoValue, bool force = false) // use when there is an "auto" value !
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = autoValue;
            }
        }

        protected void BindFeatureSlider(DynamicJson cfg, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        cfg[settingName] = value.Substring(0, value.Length - toRemove);
                    else
                        cfg[settingName] = value;
                }
                else
                    cfg[settingName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue);
            }
        }

        // NewtonsoftJson bind features
        protected void BindFeature(JObject json, string settingsName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                json[settingsName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindFeatureInt(JObject json, string settingsName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                json[settingsName] = SystemConfig.GetValueOrDefault(featureName, defaultValue).ToInteger();
        }

        protected void BindBoolFeature(JObject json, string settingsName, string featureName, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = true;
                else
                    json[settingsName] = false;
            }
        }

        protected void BindBoolFeatureInt(JObject json, string settingsName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = trueValue.ToInteger();
                else
                    json[settingsName] = falseValue.ToInteger();
            }
        }

        protected void BindBoolFeatureOn(JObject json, string settingsName, string featureName, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = false;
                else
                    json[settingsName] = true;
            }
        }

        protected void BindBoolFeature(JObject json, string settingsName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = trueValue;
                else
                    json[settingsName] = falseValue;
            }
        }

        protected void BindBoolFeatureOnInt(JObject json, string settingsName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = falseValue.ToInteger();
                else
                    json[settingsName] = trueValue.ToInteger();
            }
        }

        protected void BindBoolFeatureAuto(JObject json, string settingsName, string featureName, string trueValue, string falseValue, string autoValue, bool force = false) // use when there is an "auto" value !
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = trueValue;
                else if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = falseValue;
                else
                    json[settingsName] = autoValue;
            }
        }

        protected void BindFeatureSlider(JObject json, string settingsName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        json[settingsName] = value.Substring(0, value.Length - toRemove);
                    else
                        json[settingsName] = value;
                }
                else
                    json[settingsName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue);
            }
        }

        protected void BindFeatureSliderDouble(JObject json, string settingsName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        json[settingsName] = value.Substring(0, value.Length - toRemove).ToDouble();
                    else
                        json[settingsName] = value.ToDouble();
                }
                else
                    json[settingsName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue).ToDouble();
            }
        }

        protected void BindFeatureSliderInt(JObject json, string settingsName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        json[settingsName] = value.Substring(0, value.Length - toRemove).ToInteger();
                    else
                        json[settingsName] = value.ToInteger();
                }
                else
                    json[settingsName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue).ToInteger();
            }
        }

        // Dynamic Json
        protected void BindFeature(dynamic json, string settingsName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                json[settingsName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindFeatureInt(dynamic json, string settingsName, string featureName, int defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                string value = SystemConfig.GetValueOrDefault(featureName, defaultValue.ToString());
                if (int.TryParse(value, out int ret))
                    json[settingsName] = ret;
                else
                    json[settingsName] = defaultValue;
            }
        }

        protected void BindBoolFeatureDefaultFalse(dynamic json, string settingsName, string featureName,bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = true;
                else
                    json[settingsName] = false;
            }
        }

        protected void BindBoolFeatureAuto(dynamic json, string settingsName, string featureName, string trueValue, string falseValue, string autoValue, bool force = false) // use when there is an "auto" value !
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = trueValue;
                else if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = falseValue;
                else
                    json[settingsName] = autoValue;
            }
        }

        protected void BindBoolFeatureDefaultTrue(dynamic json, string settingsName, string featureName, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    json[settingsName] = false;
                else
                    json[settingsName] = true;
            }
        }

        // cfg bindfeatures
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

        protected void BindBoolFeatureOn(ConfigFile cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = trueValue;
            }
        }

        protected void BindBoolFeatureAuto(ConfigFile cfg, string settingName, string featureName, string trueValue, string falseValue, string autoValue, bool force = false) // use when there is an "auto" value !
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = autoValue;
            }
        }

        protected void BindFeature(ConfigFile cfg, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                cfg[settingName] = SystemConfig.GetValueOrDefault(featureName, defaultValue);
        }

        protected void BindFeatureSlider(ConfigFile cfg, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        cfg[settingName] = value.Substring(0, value.Length - toRemove);
                    else
                        cfg[settingName] = value;
                }
                else
                    cfg[settingName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue);
            }
        }

        // ini bindfeatures
        protected void BindIniFeature(IniFile ini, string section, string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (force || Features.IsSupported(featureName))
                    ini.WriteValue(section, settingName, SystemConfig.GetValueOrDefault(featureName, defaultValue));
            }
        }

        protected void BindBoolIniFeature(IniFile ini, string section, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    ini.WriteValue(section, settingName, trueValue);
                else
                    ini.WriteValue(section, settingName, falseValue);
            }
        }

        protected void BindBoolIniFeatureOn(IniFile ini, string section, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    ini.WriteValue(section, settingName, falseValue);
                else
                    ini.WriteValue(section, settingName, trueValue);
            }
        }

        protected void BindIniFeatureSlider(IniFile ini, string section, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        ini.WriteValue(section, settingName, value.Substring(0, value.Length - toRemove));
                    else
                        ini.WriteValue(section, settingName, value);
                }
                else
                    ini.WriteValue(section, settingName, SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue));
            }
        }

        protected void BindBoolIniFeatureAuto(IniFile ini, string section, string settingName, string featureName, string trueValue, string falseValue, string autoValue, bool force = false) // use when there is an "auto" value !
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    ini.WriteValue(section, settingName, trueValue);
                else if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    ini.WriteValue(section, settingName, falseValue);
                else
                    ini.WriteValue(section, settingName, autoValue);
            }
        }

        protected void SetIniPath(IniFile ini, string section, string settingName, string pathName)
        {
            if (!Directory.Exists(pathName))
                try { Directory.CreateDirectory(pathName); }
                catch { }

            if (!string.IsNullOrEmpty(pathName))
                ini.WriteValue(section, settingName, pathName);
        }

        // JGenesis
        protected void BindBoolIniFeature(IniFileJGenesis ini, string section, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    ini.WriteValue(section, settingName, trueValue);
                else
                    ini.WriteValue(section, settingName, falseValue);
            }
        }
        protected void BindBoolIniFeatureOn(IniFileJGenesis ini, string section, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    ini.WriteValue(section, settingName, falseValue);
                else
                    ini.WriteValue(section, settingName, trueValue);
            }
        }

        protected void BindIniFeatureSlider(IniFileJGenesis ini, string section, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        ini.WriteValue(section, settingName, value.Substring(0, value.Length - toRemove));
                    else
                        ini.WriteValue(section, settingName, value);
                }
                else
                    ini.WriteValue(section, settingName, SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue));
            }
        }

        // Fbneo config file
        protected void BindBoolFeature(FbneoConfigFile cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = trueValue;
                else
                    cfg[settingName] = falseValue;
            }
        }

        protected void BindBoolFeatureOn(FbneoConfigFile cfg, string settingName, string featureName, string trueValue, string falseValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (SystemConfig.isOptSet(featureName) && !SystemConfig.getOptBoolean(featureName))
                    cfg[settingName] = falseValue;
                else
                    cfg[settingName] = trueValue;
            }
        }

        protected void BindFeatureSlider(FbneoConfigFile cfg, string settingName, string featureName, string defaultValue, int decimalPlaces = 0, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
            {
                if (decimalPlaces > 0 && decimalPlaces < 7)
                {
                    int toRemove = 6 - decimalPlaces;
                    string value = SystemConfig.GetValueOrDefault(featureName, defaultValue);
                    if (value != defaultValue)
                        cfg[settingName] = value.Substring(0, value.Length - toRemove);
                    else
                        cfg[settingName] = value;
                }
                else
                    cfg[settingName] = SystemConfig.GetValueOrDefaultSlider(featureName, defaultValue);
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
