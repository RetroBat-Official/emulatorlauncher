using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher.Common
{
    abstract class SaveStatesWatcher : IDisposable
    {
        protected virtual string FilePattern { get { return _infos == null ? null : _infos.FilePattern; } }
        protected virtual string ImagePattern { get { return _infos == null ? null : _infos.ImagePattern; } }

        protected virtual string AutoFilePattern { get { return _infos == null ? null : _infos.AutoFilePattern; } }
        protected virtual string AutoImagePattern { get { return _infos == null ? null : _infos.AutoImagePattern; } }

        protected virtual int FirstSlot { get { return _infos == null ? 0 : _infos.FirstSlot; } }
        protected virtual int LastSlot { get { return _infos == null ? 999999 : _infos.LastSlot; } }

        public bool IncrementalMode { get; private set; }
        public int Slot { get; private set; }

        public string SaveStatesPath { get { return _retrobatPath; } }
        public string EmulatorPath { get { return _emulatorPath; } }

        private string _rom;
        private string _retrobatPath;
        private string _emulatorPath;

        private FileSystemWatcher _fsw;
        private Regex _fileRegex;
        private Regex _autoRegex;
        private SaveStatesWatcherMethod _method;

        private SaveStateEmulatorInfo _infos;

        public bool IsValid { get { return _infos != null; } }

        public SaveStatesWatcher(string romfile, string emulatorPath, string retrobatPath, SaveStatesWatcherMethod method = SaveStatesWatcherMethod.Rename)
        {
            _infos = Program.EsSaveStates[Program.SystemConfig["emulator"]];
            
            IncrementalMode = Program.EsSaveStates.IsIncremental(Program.SystemConfig["emulator"]);

            if (IncrementalMode)
                Slot = FirstSlot;
            else
                Slot = (Program.SystemConfig["state_slot"] ?? FirstSlot.ToString()).ToInteger();

            _method = method;
            _rom = romfile;
            _retrobatPath = retrobatPath;
            _emulatorPath = emulatorPath;

            FileTools.TryCreateDirectory(_retrobatPath);
            FileTools.TryCreateDirectory(_emulatorPath);

#if DEBUG
            GetNextFreeSlot();
#endif

            _fsw = new FileSystemWatcher(_emulatorPath);

            if (_method == SaveStatesWatcherMethod.Changed)
                _fsw.Changed += OnChanged;
            else
                _fsw.Renamed += OnFileRenamed;

            _fsw.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            if (_fsw != null)
            {
                _fsw.Dispose();
                _fsw = null;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted || e.ChangeType == WatcherChangeTypes.Renamed)
                return;

            var saveState = ParseSaveStateFilename(e.FullPath);
            if (saveState == null)
                return;

            System.Diagnostics.Debug.WriteLine("OnChanged : " + saveState.FullPath);

            SetTimeout(() =>
                {
                    lock (_lock)
                    {
                        if (!FileTools.IsFileLocked(saveState.FullPath))
                        {
                            System.Diagnostics.Debug.WriteLine("ImportSaveState : " + saveState.FullPath);
                            ImportSaveState(saveState);
                        }
                    }
                }, 100);
        }

        private static object _lock = new object();

        private void SetTimeout(Action action, int timeout)
        {
            Thread t = new Thread(
                () =>
                {
                    Thread.Sleep(timeout);
                    action.Invoke();
                }
            );
            t.Start();
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Renamed)
                return;

            var saveState = ParseSaveStateFilename(e.FullPath);
            if (saveState == null)
                return;

            ImportSaveState(saveState);
        }

        private void ImportSaveState(SaveStateFileInfo saveState)
        {
            string stem = Path.GetFileNameWithoutExtension(_rom);
            string newFn = Path.Combine(_retrobatPath, MakeFilename(stem, saveState.Slot, saveState.IsAutoSave ? FileNameType.AutoFile : FileNameType.File));

            if (IncrementalMode && !saveState.IsAutoSave)
            {
                newFn = Path.Combine(_retrobatPath, MakeFilename(stem, this.Slot, FileNameType.File));
                if (File.Exists(newFn))
                {
                    Slot = GetNextFreeSlot();
                    newFn = Path.Combine(_retrobatPath, MakeFilename(stem, this.Slot, FileNameType.File));
                }

                Slot = GetNextFreeSlot();
            }

            FileTools.TryCopyFile(saveState.FullPath, newFn);

            // Screenshot
            try { SaveScreenshot(saveState.FullPath, Path.Combine(_retrobatPath, MakeFilename(stem, saveState.Slot, saveState.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image))); }
            catch { }

            // Filename information
            File.WriteAllText(Path.Combine(_retrobatPath, stem + ".txt"), saveState.FileName);
        }

        /// <summary>
        /// Used to provide another source for screenshot ( ie. Dolphin / pcsx2 )
        /// </summary>
        /// <param name="saveState"></param>
        /// <returns></returns>
        protected virtual void SaveScreenshot(string saveState, string destScreenShot)
        {
            var info = ParseSaveStateFilename(saveState);
            if (info == null)
                return;

            var screenShot = Path.Combine(_emulatorPath, MakeFilename(info.FileName, info.Slot, info.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image));
            if (!File.Exists(screenShot))
                return;

            File.Copy(screenShot, destScreenShot, true);
        }

        protected enum FileNameType
        {
            File,
            Image,
            AutoFile,
            AutoImage
        }

        protected virtual string MakeFilename(string fileName, int slot, FileNameType type/* = FileNameType.File*/)
        {
            var pattern = FilePattern;

            switch (type)
            {
                case FileNameType.Image:
                    pattern = ImagePattern;
                    break;
                case FileNameType.AutoFile:
                    pattern = AutoFilePattern;
                    break;
                case FileNameType.AutoImage:
                    pattern = AutoImagePattern;
                    break;
            }

            if (!string.IsNullOrEmpty(pattern))
            {
                pattern = pattern.Replace("{{romfilename}}", fileName);
                pattern = pattern.Replace("{{slot}}", slot == 0 ? "" : slot.ToString());
                pattern = pattern.Replace("{{slot0}}", slot.ToString());
                pattern = pattern.Replace("{{slot00}}", slot.ToString().PadLeft(2, '0'));
                pattern = pattern.Replace("{{slot2d}}", slot.ToString().PadLeft(2, '0'));
            }

            return pattern;
        }

        public bool IsLaunchingAutoSave(string state_file = null)
        {
            if (string.IsNullOrEmpty(state_file))
                state_file = Program.SystemConfig["state_file"];

            var info = ParseSaveStateFilename(state_file);
            return info != null && info.IsAutoSave;
        }

        public void PrepareEmulatorRepository(string state_file = null, int slot = -1)
        {
            if (IncrementalMode)
            {
                if (string.IsNullOrEmpty(state_file))
                    state_file = Program.SystemConfig["state_file"];

                CopyToPhysicalSlot(state_file, slot);
            }
            else
                SyncPhysicalPath();
        }

        public bool CopyToPhysicalSlot(string state_file, int slot = -1)
        {
            if (string.IsNullOrEmpty(state_file) || !File.Exists(state_file))
                return false;

            if (slot < 0)
                slot = this.Slot;

            var info = ParseSaveStateFilename(state_file);
            if (info == null)
                return false;

            try
            {
                string txt = Path.Combine(_retrobatPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
                if (!File.Exists(txt))
                    return false;

                CleanupEmulatorSaveStates();

                var ppssppName = File.ReadAllText(txt);

                string destFileName = Path.Combine(_emulatorPath, MakeFilename(ppssppName, slot, FileNameType.File));
                FileTools.TryCopyFile(state_file, destFileName);

                var screenShot = Path.Combine(_retrobatPath, MakeFilename(info.FileName, info.Slot, info.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image)); ;
                var destScreenShot = Path.Combine(_emulatorPath, MakeFilename(Path.GetFileName(destFileName), info.Slot, info.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image));
                FileTools.TryCopyFile(screenShot, destScreenShot);
            }
            catch
            { 
                return false; 
            }

            return true;
        }

        public void SyncPhysicalPath()
        {
            string ppssppName = Path.Combine(_retrobatPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
            if (!File.Exists(ppssppName))
                return;

            ppssppName = File.ReadAllText(ppssppName);

            var fnlan = GetRetrobatSaveStates();
            var fnpsp = GetEmulatorSaveStates();

            foreach (var dist in fnpsp)
            {
                if (fnlan.Any(f => f.Slot == dist.Slot))
                    continue;

                FileTools.TryDeleteFile(dist.FullPath);

                var screenShot = Path.Combine(_emulatorPath, MakeFilename(dist.FileName, dist.Slot, dist.IsAutoSave ? FileNameType.AutoImage: FileNameType.Image)); //MakeImageFilename(Path.GetFileName(dist.FullPath)));
                FileTools.TryDeleteFile(screenShot);
            }

            foreach (var local in fnlan)
            {
                if (local.Slot > LastSlot)
                    continue;

                var psav = fnpsp.FirstOrDefault(f => f.Slot == local.Slot);
                if (psav == null || psav.FileSize != local.FileSize)
                {
                    string dest = Path.Combine(_emulatorPath, MakeFilename(ppssppName, local.Slot, local.IsAutoSave ? FileNameType.AutoFile : FileNameType.File));
                    FileTools.TryCopyFile(local.FullPath, dest);

                    var screenShot = Path.Combine(_retrobatPath, MakeFilename(Path.GetFileName(local.FullPath), local.Slot, local.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image));
                    var destScreenShot = Path.Combine(_emulatorPath, MakeFilename(Path.GetFileName(dest), local.Slot, local.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image));
                    FileTools.TryCopyFile(screenShot, destScreenShot);
                }
            }
        }

        protected virtual void CleanupEmulatorSaveStates()
        {
            foreach (var state in GetEmulatorSaveStates())
                FileTools.TryDeleteFile(state.FullPath);
        }

        protected virtual int GetNextFreeSlot()
        {
            var slots = GetRetrobatSaveStates().Where(s => !s.IsAutoSave).Select(s => s.Slot);
            if (slots.Any())
                return slots.Max() + 1;
                
            return FirstSlot;            
        }

        private SaveStateFileInfo[] GetEmulatorSaveStates()
        {
            string txt = Path.Combine(_retrobatPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
            if (!File.Exists(txt))
                return new SaveStateFileInfo[] { };

            txt = File.ReadAllText(txt);

            return GetSaveStatesInternal(_emulatorPath, txt);
        }

        private SaveStateFileInfo[] GetRetrobatSaveStates()
        {
            return GetSaveStatesInternal(_retrobatPath, Path.GetFileNameWithoutExtension(_rom));
        }

        private SaveStateFileInfo[] GetSaveStatesInternal(string path, string stem)
        {
            var ret = new List<SaveStateFileInfo>();

            foreach (var file in Directory.GetFiles(path, stem + "*.*"))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".txt" || ext == ".jpg" || ext == ".png")
                    continue;

                var state = ParseSaveStateFilename(file);
                if (state == null || state.FileName != stem)
                    continue;

                ret.Add(state);
            }

            return ret.ToArray();
        }

        protected virtual SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (!string.IsNullOrEmpty(AutoFilePattern))
            {
                if (_autoRegex == null)
                {
                    var pattern = AutoFilePattern;

                    pattern = pattern.Replace("\\", "\\\\");
                    pattern = pattern.Replace(".", "\\.");
                    pattern = pattern.Replace("{{romfilename}}", "(.*)");
                    pattern = pattern.Replace("{{slot}}", "($|[0-9]+)");
                    pattern = pattern.Replace("{{slot0}}", "([0-9]+)");
                    pattern = pattern.Replace("{{slot00}}", "([0-9]+)");
                    pattern = pattern.Replace("{{slot2d}}", "([0-9]+)");
                    pattern = "^" + pattern + "$";

                    _autoRegex = new Regex(pattern);
                }

                var matches = _autoRegex.Matches(Path.GetFileName(filename));
                if (matches.Count == 1 && matches[0].Groups.Count > 1)
                {
                    var fn = new SaveStateFileInfo();
                    fn.FullPath = filename;
                    fn.FileName = matches[0].Groups[1].Value;
                    fn.Slot = -1;
                    fn.IsAutoSave = true;
                    return fn;
                }
            }

            if (!string.IsNullOrEmpty(FilePattern))
            {
                if (_fileRegex == null)
                {
                    var pattern = FilePattern;

                    pattern = pattern.Replace("\\", "\\\\");
                    pattern = pattern.Replace(".", "\\.");
                    pattern = pattern.Replace("{{romfilename}}", "(.*)");
                    pattern = pattern.Replace("{{slot}}", "($|[0-9]+)");
                    pattern = pattern.Replace("{{slot0}}", "([0-9]+)");
                    pattern = pattern.Replace("{{slot00}}", "([0-9]+)");
                    pattern = pattern.Replace("{{slot2d}}", "([0-9]+)");
                    pattern = "^" + pattern + "$";

                    _fileRegex = new Regex(pattern);
                }

                var matches = _fileRegex.Matches(Path.GetFileName(filename));
                if (matches.Count == 1 && matches[0].Groups.Count > 2)
                {
                    var fn = new SaveStateFileInfo();
                    fn.FullPath = filename;
                    fn.FileName = matches[0].Groups[1].Value;
                    fn.Slot = matches[0].Groups[2].Value.ToInteger();
                    return fn;
                }
            }

            return null;
        }
    }

    enum SaveStatesWatcherMethod
    {
        Rename,
        Changed
    }

    class SaveStateFileInfo
    {
        public string FileName { get; set; }
        public int Slot { get; set; }
        public string FullPath { get; set; }
        public bool IsAutoSave { get; set; }

        public long FileSize
        {
            get { return new FileInfo(FullPath).Length; }
        }

        public override string ToString() { return FileName + " (" + (IsAutoSave ? "autosave" : Slot.ToString()) + ")"; }
    }
}
