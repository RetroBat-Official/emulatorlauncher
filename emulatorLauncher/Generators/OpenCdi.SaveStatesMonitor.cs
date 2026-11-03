using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// OpenCDi writes <game>_slot<n>.st in emulators\opencdi\savestates (F6 = slot 1, menu = slots 1 to 5).
    /// The states have no screenshot: the RetroBat save state icon is used.
    /// </summary>
    class OpenCdiSaveStatesMonitor : SaveStatesWatcher
    {
        private static readonly Regex _stateRegex = new Regex(@"^(.+)_slot([0-9]+)\.st$", RegexOptions.IgnoreCase);

        // Changes of the same file reported within this delay belong to the same save
        private static readonly TimeSpan _duplicateDelay = TimeSpan.FromSeconds(2);

        private readonly string _iconPath;
        private readonly string _romStem;

        private readonly object _lastChangeLock = new object();
        private string _lastChangePath;
        private DateTime _lastChangeTime;

        public OpenCdiSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string iconPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            _iconPath = iconPath;
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        /// <summary>
        /// File name OpenCDi uses for a state
        /// </summary>
        public static string GetEmulatorStateFile(string emulatorPath, string rom, int slot)
        {
            string gameName = Path.GetFileNameWithoutExtension(rom);
            string safeName = new string(gameName.Select(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '(' || c == ')' ? c : '_').ToArray()).Trim();
            if (string.IsNullOrEmpty(safeName))
                safeName = "cdi_bios";

            return Path.Combine(emulatorPath, safeName + "_slot" + slot + ".st");
        }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Emulator side : <game>_slot<n>.st
            var match = _stateRegex.Match(Path.GetFileName(filename));
            if (match.Success && filename.StartsWith(EmulatorPath, StringComparison.InvariantCultureIgnoreCase))
            {
                // One save gives several change notifications: keep the first one,
                // the base class imports it 100 ms later, once OpenCDi has closed the file
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
                    Slot = match.Groups[2].Value.ToInteger()
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

                var latest = new DirectoryInfo(SaveStatesPath).GetFiles(_romStem + ".*.st")
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
