using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// Azahar writes <TITLEID>.<NN>.cst in user\states (slot 00 = quick save, 01-10 = menu slots).
    /// Each state is imported once complete, as the next RetroBat slot. The states have no screenshot: the RetroBat save state icon is used.
    /// </summary>
    class AzaharSaveStatesMonitor : SaveStatesWatcher
    {
        // Movie states (<TITLEID>.movie<ID>.<NN>.cst) are ignored
        private static readonly Regex _stateRegex = new Regex(@"^([0-9A-F]{16})\.([0-9]{2})\.cst$", RegexOptions.IgnoreCase);
        private static readonly Regex _titleIdRegex = new Regex(@"^[0-9A-F]{16}$", RegexOptions.IgnoreCase);

        // The state is complete when it has not changed for this delay
        private static readonly TimeSpan _quietDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan _maxWait = TimeSpan.FromSeconds(15);

        private readonly string _iconPath;
        private readonly string _romStem;

        private readonly object _importLock = new object();
        private readonly Dictionary<string, KeyValuePair<DateTime, long>> _imported = new Dictionary<string, KeyValuePair<DateTime, long>>(StringComparer.InvariantCultureIgnoreCase);

        public AzaharSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string iconPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            _iconPath = iconPath;
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        /// <summary>
        /// Title id of the game, stored in <rom>.txt by the watcher at the first imported state (null before).
        /// </summary>
        public static string GetTitleId(string sharedPath, string rom)
        {
            try
            {
                string txt = Path.Combine(sharedPath, Path.GetFileNameWithoutExtension(rom) + ".txt");
                if (!File.Exists(txt))
                    return null;

                string titleId = File.ReadAllText(txt).Trim();
                return _titleIdRegex.IsMatch(titleId) ? titleId.ToUpperInvariant() : null;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// File name Azahar uses for a state
        /// </summary>
        public static string GetEmulatorStateFile(string emulatorPath, string titleId, int slot)
        {
            return Path.Combine(emulatorPath, titleId.ToUpperInvariant() + "." + slot.ToString("00") + ".cst");
        }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Emulator side : <TITLEID>.<NN>.cst
            var match = _stateRegex.Match(Path.GetFileName(filename));
            bool emulatorFile = match.Success && string.Equals(Path.GetDirectoryName(filename), EmulatorPath.TrimEnd('\\'), StringComparison.InvariantCultureIgnoreCase);

            if (!emulatorFile)
                return base.ParseSaveStateFilename(filename);

            // One save gives several change notifications : each state is imported once, when it is complete
            lock (_importLock)
            {
                FileInfo file;
                if (!WaitForCompleteFile(filename, out file))
                    return null;

                KeyValuePair<DateTime, long> last;
                if (_imported.TryGetValue(filename, out last) && last.Key == file.LastWriteTimeUtc && last.Value == file.Length)
                    return null;

                _imported[filename] = new KeyValuePair<DateTime, long>(file.LastWriteTimeUtc, file.Length);
            }

            return new SaveStateFileInfo()
            {
                FullPath = filename,
                FileName = match.Groups[1].Value.ToUpperInvariant(),
                Slot = match.Groups[2].Value.ToInteger()
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

            SimpleLogger.Instance.Warning("[Azahar] Save state still written after " + _maxWait.TotalSeconds + " s, not imported : " + path);
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

                var latest = new DirectoryInfo(SaveStatesPath).GetFiles(_romStem + ".*.cst")
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
