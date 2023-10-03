using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace emulatorLauncher
{
    abstract class SaveStatesWatcher : IDisposable
    {
        protected abstract string FilePattern { get; }
        protected abstract string ImagePattern { get; }
        protected virtual int FirstSlot { get { return 0; } }
        protected virtual int LastSlot { get { return 999999; } }

        public bool IncrementalMode { get; set; }
        public int Slot { get; set; }

        public string SaveStatesPath { get { return _retrobatPath; } }
        public string EmulatorPath { get { return _emulatorPath; } }

        private string _rom;
        private string _retrobatPath;
        private string _emulatorPath;

        private FileSystemWatcher _fsw;
        private Regex _fileRegex;
        private SaveStatesWatcherMethod _method;

        public SaveStatesWatcher(string romfile, string emulatorPath, string retrobatPath, SaveStatesWatcherMethod method = SaveStatesWatcherMethod.Rename)
        {
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
            string newFn = Path.Combine(_retrobatPath, MakeFilename(stem, saveState.Slot));

            if (IncrementalMode)
            {
                newFn = Path.Combine(_retrobatPath, MakeFilename(stem, this.Slot));
                if (File.Exists(newFn))
                {
                    Slot = GetNextFreeSlot();
                    newFn = Path.Combine(_retrobatPath, MakeFilename(stem, this.Slot));
                }

                Slot = GetNextFreeSlot();
            }

            try { File.Copy(saveState.FullPath, newFn, true); }
            catch { }

            // Screenshot
            try { SaveScreenshot(saveState.FullPath, Path.Combine(_retrobatPath, MakeImageFilename(newFn))); }
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
            var screenShot = Path.Combine(_emulatorPath, MakeImageFilename(saveState));
            if (!File.Exists(screenShot))
                return;

            File.Copy(screenShot, destScreenShot, true);
        }
        
        protected virtual string MakeFilename(string fileName, int slot, bool imagePattern = false)
        {
            var pattern = imagePattern ? ImagePattern : FilePattern;

            pattern = pattern.Replace("{{romfilename}}", fileName);
            pattern = pattern.Replace("{{slot}}", slot == 0 ? "" : slot.ToString());
            pattern = pattern.Replace("{{slot0}}", slot.ToString());
            pattern = pattern.Replace("{{slot00}}", slot.ToString().PadLeft(2, '0'));
            pattern = pattern.Replace("{{slot2d}}", slot.ToString().PadLeft(2, '0'));

            return pattern;
        }

        protected virtual string MakeImageFilename(string fileName)
        {
            var fi = ParseSaveStateFilename(fileName);
            if (fi == null)
                return null;

            return MakeFilename(fi.FileName, fi.Slot, true);
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

            try
            {
                string txt = Path.Combine(_retrobatPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
                if (!File.Exists(txt))
                    return false;

                CleanupEmulatorSaveStates();

                var ppssppName = File.ReadAllText(txt);

                string destFileName = Path.Combine(_emulatorPath, MakeFilename(ppssppName, slot));

                File.Copy(state_file, destFileName, true);

                try
                {
                    var screenShot = Path.Combine(_retrobatPath, MakeImageFilename(Path.GetFileName(state_file)));
                    var destScreenShot = Path.Combine(_emulatorPath, MakeImageFilename(Path.GetFileName(destFileName)));

                    if (File.Exists(screenShot))
                        File.Copy(screenShot, destScreenShot, true);
                }
                catch { }
            }
            catch { return false; }
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

                try { File.Delete(dist.FullPath); }
                catch { }

                try
                {
                    var screenShot = Path.Combine(_emulatorPath, MakeImageFilename(Path.GetFileName(dist.FullPath)));
                    if (File.Exists(screenShot))
                        File.Delete(screenShot);
                }
                catch { }
            }

            foreach (var local in fnlan)
            {
                if (local.Slot > LastSlot)
                    continue;

                var psav = fnpsp.FirstOrDefault(f => f.Slot == local.Slot);
                if (psav == null || psav.FileSize != local.FileSize)
                {
                    string dest = Path.Combine(_emulatorPath, MakeFilename(ppssppName, local.Slot));

                    try { File.Copy(local.FullPath, dest, true); }
                    catch { }

                    try
                    {
                        var screenShot = Path.Combine(_retrobatPath, MakeImageFilename(Path.GetFileName(local.FullPath)));
                        var destScreenShot = Path.Combine(_emulatorPath, MakeImageFilename(Path.GetFileName(dest)));

                        if (File.Exists(screenShot))
                            File.Copy(screenShot, destScreenShot, true);
                    }
                    catch { }
                }
            }
        }

        protected virtual void CleanupEmulatorSaveStates()
        {
            foreach (var state in GetEmulatorSaveStates())
            {
                try { File.Delete(state.FullPath); }
                catch { }
            }
        }

        protected virtual int GetNextFreeSlot()
        {
            var slots = GetRetrobatSaveStates().Select(s => s.Slot);
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

        public long FileSize
        {
            get { return new FileInfo(FullPath).Length; }
        }

        public override string ToString() { return FileName + " (" + Slot + ")"; }
    }
}
