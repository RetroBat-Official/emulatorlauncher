using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// OpenCDi writes <game>.state (slot 0) and <game>.state<n> in emulators\opencdi\states,
    /// through a .tmp file renamed at the end. The states have no screenshot: the RetroBat save state icon is used.
    /// </summary>
    class OpenCdiSaveStatesMonitor : SaveStatesWatcher
    {
        private static readonly Regex _stateRegex = new Regex(@"^(.+)\.state([0-9]*)$", RegexOptions.IgnoreCase);

        private readonly string _iconPath;
        private readonly string _romStem;

        public OpenCdiSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string iconPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Rename)
        {
            _iconPath = iconPath;
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        /// <summary>
        /// File name OpenCDi uses for a state (state_path in src/main.rs)
        /// </summary>
        public static string GetEmulatorStateFile(string emulatorPath, string rom, int slot)
        {
            string gameName = Path.GetFileNameWithoutExtension(rom);
            string safeName = new string(gameName.Select(c => char.IsLetterOrDigit(c) || " ()-_.,'&!".IndexOf(c) >= 0 ? c : '_').ToArray());
            if (string.IsNullOrEmpty(safeName))
                safeName = "opencdi";

            return Path.Combine(emulatorPath, safeName + ".state" + (slot == 0 ? "" : slot.ToString()));
        }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Emulator side : <game>.state / <game>.state<n>
            var match = _stateRegex.Match(Path.GetFileName(filename));
            if (match.Success && filename.StartsWith(EmulatorPath, StringComparison.InvariantCultureIgnoreCase))
            {
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
