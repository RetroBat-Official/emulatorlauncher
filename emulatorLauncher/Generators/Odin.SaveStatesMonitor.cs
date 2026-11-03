using System;
using System.IO;
using System.Linq;
using System.Threading;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// Odin keeps one state per game, <set>.odinstate in [paths] states (F2 saves, F4 loads), written in place (fopen / fwrite, state_file.cpp)
    /// without blocking the other readers: the file is imported once its size and date stop changing, as the next RetroBat slot.
    /// The states have no screenshot: the RetroBat save state icon is used.
    /// </summary>
    class OdinSaveStatesMonitor : SaveStatesWatcher
    {
        // The state is complete when it has not changed for this delay
        private static readonly TimeSpan _quietDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan _maxWait = TimeSpan.FromSeconds(15);

        private readonly string _iconPath;
        private readonly string _romStem;

        private readonly object _importLock = new object();
        private DateTime _importedWriteTime;
        private long _importedLength = -1;

        public OdinSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string iconPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            _iconPath = iconPath;
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Emulator side : <set>.odinstate
            bool emulatorFile = Path.GetExtension(filename).Equals(".odinstate", StringComparison.InvariantCultureIgnoreCase) &&
                                string.Equals(Path.GetDirectoryName(filename), EmulatorPath.TrimEnd('\\'), StringComparison.InvariantCultureIgnoreCase);

            if (!emulatorFile)
                return base.ParseSaveStateFilename(filename);

            // One save gives several change notifications : the state is imported once, when it is complete
            lock (_importLock)
            {
                FileInfo file;
                if (!WaitForCompleteFile(filename, out file))
                    return null;

                if (file.LastWriteTimeUtc == _importedWriteTime && file.Length == _importedLength)
                    return null;

                _importedWriteTime = file.LastWriteTimeUtc;
                _importedLength = file.Length;
            }

            return new SaveStateFileInfo()
            {
                FullPath = filename,
                FileName = Path.GetFileNameWithoutExtension(filename),
                Slot = 0
            };
        }

        private static bool WaitForCompleteFile(string path, out FileInfo file)
        {
            file = null;
            DateTime start = DateTime.UtcNow;
            DateTime lastWrite = DateTime.MinValue;
            long lastLength = -1;
            DateTime stableSince = DateTime.UtcNow;

            while (DateTime.UtcNow - start < _maxWait)
            {
                try
                {
                    file = new FileInfo(path);
                    if (!file.Exists)
                        return false;

                    if (file.LastWriteTimeUtc != lastWrite || file.Length != lastLength)
                    {
                        lastWrite = file.LastWriteTimeUtc;
                        lastLength = file.Length;
                        stableSince = DateTime.UtcNow;
                    }
                    else if (file.Length > 0 && DateTime.UtcNow - stableSince >= _quietDelay)
                        return true;
                }
                catch { }

                Thread.Sleep(100);
            }

            SimpleLogger.Instance.Warning("[Odin] Save state still written after " + _maxWait.TotalSeconds + " s, not imported : " + path);
            return false;
        }

        /// <summary>
        /// In incremental mode, the RetroBat slot differs from the emulator slot given to the base class:
        /// the icon is written next to the state that has just been copied (most recent one).
        /// </summary>
        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                if (!File.Exists(_iconPath))
                    return;

                var latest = new DirectoryInfo(SaveStatesPath).GetFiles(_romStem + ".*.odinstate")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                var info = latest == null ? null : base.ParseSaveStateFilename(latest.FullName);
                if (info != null && !info.IsAutoSave)
                    destScreenShot = Path.Combine(SaveStatesPath, MakeFilename(_romStem, info.Slot, FileNameType.Image));

                File.Copy(_iconPath, destScreenShot, true);
            }
            catch { }
        }
    }
}