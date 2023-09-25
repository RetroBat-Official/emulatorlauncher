using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace emulatorLauncher
{
    class DolphinSaveStatesMonitor : IDisposable
    {
        public bool IncrementalMode { get; set; }
        public int Slot { get; set; }

        private string _rom;
        private string _sharedPath;
        private string _dolphinPath;

        private FileSystemWatcher _fsw;

        public DolphinSaveStatesMonitor(string romfile, string dolphinPath, string sharedPath)
        {
            _rom = romfile;
            _sharedPath = sharedPath;
            _dolphinPath = dolphinPath;

            try { Directory.CreateDirectory(dolphinPath); }
            catch { }
            try { Directory.CreateDirectory(sharedPath); }
            catch { }

            _fsw = new FileSystemWatcher(dolphinPath);
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

        public bool CopyToPhysicalSlot(string state_file, int slot = -1)
        {
            if (slot < 0)
                slot = this.Slot;

            try
            {
                string stem = Path.GetFileNameWithoutExtension(state_file);

                string txt = Path.Combine(Path.GetDirectoryName(state_file), stem + ".txt");
                if (!File.Exists(txt))
                    return false;

                stem = File.ReadAllText(txt);

                string destFileName = Path.Combine(
                    _dolphinPath,
                    stem + ".s" + slot.ToString().PadLeft(2, '0'));

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
            if (ext == ".sav")
                return;

            if (Path.GetExtension(e.OldName) != ".tmp")
                return;

            if (IncrementalMode)
            {
                ext = ".s" + Slot.ToString().PadLeft(2, '0');

                if (File.Exists(Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ext)))
                {
                    Slot = GetNextFreeSlot();
                    ext = ".s" + Slot.ToString().PadLeft(2, '0');
                }

                Slot = GetNextFreeSlot();
            }

            string newFn = Path.Combine(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ext);

            try { File.Copy(e.FullPath, newFn, true); }
            catch { }

            File.WriteAllText(Path.ChangeExtension(newFn, ".txt"), Path.GetFileNameWithoutExtension(e.FullPath));

            // Get screenshot
            Thread.Sleep(350);

            var dt = new FileInfo(e.FullPath).LastWriteTime;

            var shots = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(_dolphinPath), "ScreenShots", Path.GetFileNameWithoutExtension(e.FullPath)));
            var shot = shots
                .Where(s => Math.Abs((new FileInfo(s).LastWriteTime - dt).Seconds) < 3)
                .OrderByDescending(s => new FileInfo(s).LastWriteTime)
                .FirstOrDefault();

            if (File.Exists(shot))
            {
            retry:
                int cnt = 0;

                try
                {
                    Thread.Sleep(250);

                    File.Copy(shot, newFn + ".png", true);
                    File.Delete(shot);
                }
                catch (IOException)
                {
                    cnt++;
                    if (cnt < 3)
                    {
                        Thread.Sleep(200);
                        goto retry;
                    }
                }
            }
        }

        private int GetNextFreeSlot()
        {
            var slots = new HashSet<int>();
            foreach (var sav in Directory.GetFiles(_sharedPath, Path.GetFileNameWithoutExtension(_rom) + ".s*"))
            {
                if (!Path.GetExtension(sav).StartsWith(".s"))
                    continue;

                slots.Add(Path.GetExtension(sav).Substring(2).ToInteger());
            }

            for (int i = 99999; i >= 0; i--)
            {
                if (slots.Contains(i))
                    return i + 1;
            }

            return 1;
        }
    }
}
