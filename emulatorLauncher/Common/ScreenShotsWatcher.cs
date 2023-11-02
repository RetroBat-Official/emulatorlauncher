using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher.Common
{
    class ScreenShotsWatcher : IDisposable
    {
        public ScreenShotsWatcher(string path, string system, string rom)
        {
            DeleteIfAssigned = true;
            _system = system;
            _rom = rom;

            _fsw = new FileSystemWatcher(path);
            _fsw.Changed += OnChanged;
            _fsw.Renamed += OnFileRenamed;
            _fsw.EnableRaisingEvents = true;
        }

        public bool DeleteIfAssigned { get; set; }

        private string _system;
        private string _rom;

        public string LastScreenshot { get; set; }

        public void Dispose()
        {
            if (File.Exists(LastScreenshot))
            {
                try
                {
                    var bytes = File.ReadAllBytes(LastScreenshot);
                    EmulationStationServices.AddImageToGameListIfMissing(_system, _rom, bytes, GetMimeType(LastScreenshot));

                    if (DeleteIfAssigned)
                        FileTools.TryDeleteFile(LastScreenshot);
                }
                catch { }
            }

            if (_fsw != null)
            {
                _fsw.Dispose();
                _fsw = null;
            }
        }

        private FileSystemWatcher _fsw;

        private string GetMimeType(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();

            if (ext == ".jpg")
                return "image/jpeg";

            if (ext == ".png")
                return "image/png";

            return null;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted || e.ChangeType == WatcherChangeTypes.Renamed)
                return;

            if (GetMimeType(e.FullPath) != null)
                LastScreenshot = e.FullPath;
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Renamed)
                return;

            if (GetMimeType(e.FullPath) != null)
                LastScreenshot = e.FullPath;
        }
    }

}
