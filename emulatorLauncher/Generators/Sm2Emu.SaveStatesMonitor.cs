using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// sm2-emu writes <set>.<slot>.sm2state in <nvram_dir>\states (slots "quick", 1 to 4), in place (fopen / fwrite):
    /// Windows reports several changes for one save, only the first one is imported.
    /// The files have no screenshot: the RetroBat save state icon is used.
    /// </summary>
    class Sm2EmuSaveStatesMonitor : SaveStatesWatcher
    {
        private static readonly Regex _sm2StateRegex = new Regex(@"^(.+)\.(quick|[0-9]+)\.sm2state$", RegexOptions.IgnoreCase);

        // Changes of the same file reported within this delay belong to the same save
        private static readonly TimeSpan _duplicateDelay = TimeSpan.FromSeconds(2);

        private readonly string _iconPath;
        private readonly string _romStem;

        private readonly object _lastChangeLock = new object();
        private string _lastChangePath;
        private DateTime _lastChangeTime;

        public Sm2EmuSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string iconPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            _iconPath = iconPath;
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Emulator side : <set>.quick.sm2state / <set>.<n>.sm2state
            var match = _sm2StateRegex.Match(Path.GetFileName(filename));
            if (match.Success && filename.StartsWith(EmulatorPath, StringComparison.InvariantCultureIgnoreCase))
            {
                // One save gives several change notifications (write, close): keep the first one,
                // the base class imports it 100 ms later, once sm2-emu has closed the file
                lock (_lastChangeLock)
                {
                    DateTime now = DateTime.UtcNow;
                    if (string.Equals(_lastChangePath, filename, StringComparison.InvariantCultureIgnoreCase) && now - _lastChangeTime < _duplicateDelay)
                        return null;

                    _lastChangePath = filename;
                    _lastChangeTime = now;
                }

                return new SaveStateFileInfo()
                {
                    FullPath = filename,
                    FileName = match.Groups[1].Value,
                    Slot = match.Groups[2].Value.Equals("quick", StringComparison.InvariantCultureIgnoreCase) ? 0 : match.Groups[2].Value.ToInteger()
                };
            }

            // RetroBat side : es_savestates.cfg pattern
            return base.ParseSaveStateFilename(filename);
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

                var latest = new DirectoryInfo(SaveStatesPath).GetFiles(_romStem + ".*.sm2state")
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
