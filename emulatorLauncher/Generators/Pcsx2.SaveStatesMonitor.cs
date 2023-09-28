using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace emulatorLauncher
{
    class Pcsx2SaveStatesMonitor : IDisposable
    {
        public bool IncrementalMode { get; set; }
        public int Slot { get; set; }

        private string _rom;
        private string _sharedPath;
        private string _pcsx2Path;

        private FileSystemWatcher _fsw;

        public Pcsx2SaveStatesMonitor(string romfile, string pcsx2Path, string sharedPath)
        {
            _rom = romfile;
            _sharedPath = sharedPath;
            _pcsx2Path = pcsx2Path;

            GetNextFreeSlot();

            try { Directory.CreateDirectory(pcsx2Path); }
            catch { }
            try { Directory.CreateDirectory(sharedPath); }
            catch { }

            _fsw = new FileSystemWatcher(pcsx2Path);
            _fsw.Renamed += OnFileRenamed;
            _fsw.EnableRaisingEvents = true;
        }

        public bool CopyToPhysicalSlot(string state_file, int slot = -1)
        {
            if (slot < 0)
                slot = this.Slot;

            try
            {
                string stem = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(state_file));

                string txt = Path.Combine(Path.GetDirectoryName(state_file), stem + ".txt");
                if (!File.Exists(txt))
                    return false;

                stem = File.ReadAllText(txt);

                string destFileName = Path.Combine(
                    _pcsx2Path,
                    stem + "." + slot.ToString().PadLeft(2, '0') + ".p2s");

                File.Copy(state_file, destFileName, true);
            }
            catch { return false; }
            return true;
        }
        
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Renamed)
                return;

            var ext = Path.GetExtension(e.FullPath);
            if (ext != ".p2s")
                return;

            ext = Path.GetExtension(Path.GetFileNameWithoutExtension(e.FullPath)) + ext;
            
            if (IncrementalMode)
            {
                ext = "." + Slot.ToString().PadLeft(2, '0') + ".p2s";

                if (File.Exists(Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ext)))
                {
                    Slot = GetNextFreeSlot();
                    ext = "." + Slot.ToString().PadLeft(2, '0') + ".p2s";
                }

                Slot = GetNextFreeSlot();
            }
            
            string newFn = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ext);

            try { File.Copy(e.FullPath, newFn, true); }
            catch { }

            File.WriteAllText(
                Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".txt"),
                Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(e.FullPath)));

            // Extract screenshot
            try
            {
                using (var arch = ZipArchive.OpenRead(newFn))
                {
                    var sshot = arch.Entries.FirstOrDefault(en => en.Filename == "Screenshot.png");
                    if (sshot != null)
                    {
                        sshot.Extract(_sharedPath);

                        if (File.Exists(newFn + ".png"))
                            File.Delete(newFn + ".png");

                        File.Move(Path.Combine(_sharedPath, "Screenshot.png"), newFn + ".png");
                    }
                }
            }
            catch { }
        }

        private int GetNextFreeSlot()
        {
            var slots = new HashSet<int>();
            foreach (var sav in Directory.GetFiles(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".*.p2s"))
            {
                if (!Path.GetExtension(sav).StartsWith(".p2s"))
                    continue;

                var slot = Path.GetExtension(Path.GetFileNameWithoutExtension(sav)).Substring(1);
                slots.Add(slot.ToInteger());
            }

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
