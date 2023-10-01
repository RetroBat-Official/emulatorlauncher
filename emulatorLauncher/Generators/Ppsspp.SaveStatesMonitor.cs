using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace emulatorLauncher
{
    class PpssppSaveStatesMonitor : IDisposable
    {
        public bool IncrementalMode { get; set; }
        public int Slot { get; set; }

        private string _rom;
        private string _sharedPath;
        private string _ppssppPath;

        private FileSystemWatcher _fsw;

        public PpssppSaveStatesMonitor(string romfile, string pcsx2Path, string sharedPath)
        {
            _rom = romfile;
            _sharedPath = sharedPath;
            _ppssppPath = pcsx2Path;

            // GetNextFreeSlot();

            try { Directory.CreateDirectory(pcsx2Path); }
            catch { }
            try { Directory.CreateDirectory(sharedPath); }
            catch { }

            _fsw = new FileSystemWatcher(pcsx2Path);
            _fsw.Renamed += OnFileRenamed;
            _fsw.EnableRaisingEvents = true;
        }

        public void SyncPhysicalPath()
        {
            string ppssppName = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
            if (!File.Exists(ppssppName))
                return;

            ppssppName = File.ReadAllText(ppssppName);

            var fnlan = Directory.GetFiles(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + "_*.ppst").Select(f => FileNameData.FromFile(f)).ToArray();
            var fnpsp = Directory.GetFiles(_ppssppPath, ppssppName + "_*.ppst").Select(f => FileNameData.FromFile(f)).ToArray();

            foreach (var dist in fnpsp)
            {
                if (fnlan.Any(f => f.Slot == dist.Slot))
                    continue;

                string dest = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + "_" + dist.Slot + ".ppst");

                try { File.Delete(dist.FullPath); }
                catch { }

                try
                {
                    if (File.Exists(Path.ChangeExtension(dist.FullPath, ".jpg")))
                        File.Delete(Path.ChangeExtension(dist.FullPath, ".jpg"));
                }
                catch { }
            }

            foreach (var local in fnlan)
            {
                if (local.Slot.ToInteger() > 4)
                    continue;

                var psav = fnpsp.FirstOrDefault(f => f.Slot == local.Slot);
                if (psav == null || psav.FileSize != local.FileSize)
                {
                    string dest = Path.Combine(_ppssppPath, ppssppName + "_" + local.Slot + ".ppst");
                    
                    try { File.Copy(local.FullPath, dest, true); }
                    catch { }

                    try 
                    { 
                        if (File.Exists(Path.ChangeExtension(local.FullPath, ".jpg")))
                            File.Copy(Path.ChangeExtension(local.FullPath, ".jpg"), Path.ChangeExtension(dest, ".jpg"), true); 
                    }
                    catch { }
                }
            }
        }

        private void CleanupSaveStates()
        {
            string txt = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
            if (!File.Exists(txt))
                return;

            var ppssppName = File.ReadAllText(txt);

            var fnpsp = Directory.GetFiles(_ppssppPath, ppssppName + "_*.*").Select(f => FileNameData.FromFile(f)).ToArray();
            foreach (var file in fnpsp)
            {
                try { File.Delete(file.FullPath); }
                catch { }
            }
        }

        public bool CopyToPhysicalSlot(string state_file, int slot = -1)
        {
            if (string.IsNullOrEmpty(state_file))
                return false;

            if (slot < 0)
                slot = this.Slot;

            try
            {
                string txt = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".txt");
                if (!File.Exists(txt))
                    return false;

                CleanupSaveStates();

                var ppssppName = File.ReadAllText(txt);

                string destFileName = Path.Combine(_ppssppPath, ppssppName + "_" + slot.ToString() + ".ppst");

                File.Copy(state_file, destFileName, true);

                try
                {
                    if (File.Exists(Path.ChangeExtension(state_file, ".jpg")))
                        File.Copy(Path.ChangeExtension(state_file, ".jpg"), Path.ChangeExtension(destFileName, ".jpg"), true);
                }
                catch { }
            }
            catch { return false; }
            return true;
        }
        
        class FileNameData
        {
            public static FileNameData FromFile(string filename)
            {
                var matches = Regex.Matches(Path.GetFileName(filename), @"(.*)_([0-9]+).ppst");
                if (matches.Count == 1 && matches[0].Groups.Count == 3)
                {
                    var fn = new FileNameData();
                    fn.FullPath = filename;
                    fn.FileName = matches[0].Groups[1].Value;
                    fn.Slot = matches[0].Groups[2].Value;
                    return fn;
                }

                return new FileNameData() { FullPath = filename, FileName = Path.GetFileNameWithoutExtension(filename), Slot = "0" };
            }

            public string FileName { get; set; }
            public string Slot { get; set; }
            public string FullPath { get; set; }

            public long FileSize
            {
                get { return new FileInfo(FullPath).Length; }
            }

            public override string ToString() { return FileName + "_" + Slot + ".ppst"; }
        }


        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Renamed)
                return;

            var ext = Path.GetExtension(e.FullPath);
            if (ext != ".ppst")
                return;

            if (e.FullPath.Contains(".undo.ppst"))
                return;

            var slot = FileNameData.FromFile(e.FullPath).Slot;

            string newFn = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + "_" + slot + ext);
            
            if (IncrementalMode)
            {
                newFn = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + "_" + Slot.ToString() + ext);
                if (File.Exists(newFn))
                {
                    Slot = GetNextFreeSlot();
                    newFn = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + "_" + Slot.ToString() + ext);
                }

                Slot = GetNextFreeSlot();
            }

            try { File.Copy(e.FullPath, newFn, true); }
            catch { }

            // Extract screenshot
            try
            {
                string thumb = Path.ChangeExtension(e.FullPath, ".jpg");
                if (File.Exists(thumb))
                    File.Copy(thumb, Path.ChangeExtension(newFn, ".jpg"), true);
            }
            catch { }

            // Filename information
            File.WriteAllText(
                Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".txt"),
                FileNameData.FromFile(e.FullPath).FileName);
        }

        private int GetNextFreeSlot()
        {
            var slots = new HashSet<int>();

            foreach (var sav in Directory.GetFiles(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + "_*.ppst"))
                slots.Add(FileNameData.FromFile(sav).Slot.ToInteger());

            for (int i = 99999; i >= 0; i--)
            {
                if (slots.Contains(i))
                    return i + 1;
            }

            return 1;
        }

        public void Dispose()
        {
            if (_fsw != null)
            {
                _fsw.Dispose();
                _fsw = null;
            }
        }
    }
}
