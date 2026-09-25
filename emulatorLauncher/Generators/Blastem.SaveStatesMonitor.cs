using System;
using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// BlastEm keeps its save states in one folder per game, with fixed file names built by
    /// get_slot_name() : "slot_0" to "slot_9" plus "quicksave", with a ".state" extension in the
    /// native format and ".gst" when ui.state_format is set to gst. The game name is carried by the
    /// folder, never by the file name.
    ///
    /// SaveStatesWatcher expects the game name to be part of the file name on both sides, so the
    /// emulator side is bridged here with "slot_" standing in as the stem. That string is both what
    /// the base class filters on with Directory.GetFiles(path, stem + "*.*") and the marker used
    /// below to tell an emulator side name from a RetroBat one.
    ///
    /// Only "quicksave" can be written from a key or a pad : ui.save_state and ui.load_state are
    /// wired to QUICK_SAVE_SLOT in bindings.c, the numbered slots are reachable from the BlastEm
    /// menu only. It is therefore mapped onto RetroBat's autosave slot.
    /// </summary>
    class BlastemSaveStatesMonitor : SaveStatesWatcher
    {
        /// <summary>Stem standing for "written by BlastEm", and the Directory.GetFiles filter.</summary>
        private const string EmulatorStem = "slot_";
        private const string QuickSaveName = "quicksave";

        private readonly string _ext;
        private readonly string _shotPath;

        public BlastemSaveStatesMonitor(string romfile, string emulatorPath, string retrobatPath, string screenshotPath, bool useGstFormat)
            : base(romfile, emulatorPath, retrobatPath, SaveStatesWatcherMethod.Changed)
        {
            _shotPath = screenshotPath;
            _ext = useGstFormat ? ".gst" : ".state";
        }

        /// <summary>BlastEm has ten numbered slots, whatever es_savestates.cfg declares.</summary>
        protected override int LastSlot { get { return Math.Min(base.LastSlot, 9); } }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            string name = Path.GetFileNameWithoutExtension(filename);

            if (string.Equals(name, QuickSaveName, StringComparison.InvariantCultureIgnoreCase))
            {
                return new SaveStateFileInfo
                {
                    FullPath = filename,
                    FileName = EmulatorStem,
                    Slot = -1,
                    IsAutoSave = true
                };
            }

            if (name.StartsWith(EmulatorStem, StringComparison.InvariantCultureIgnoreCase))
            {
                int slot;
                if (int.TryParse(name.Substring(EmulatorStem.Length), out slot))
                {
                    return new SaveStateFileInfo
                    {
                        FullPath = filename,
                        FileName = EmulatorStem,
                        Slot = slot
                    };
                }
            }

            return base.ParseSaveStateFilename(filename);
        }

        protected override string MakeFilename(string fileName, int slot, FileNameType type)
        {
            if (fileName != EmulatorStem)
                return base.MakeFilename(fileName, slot, type);

            switch (type)
            {
                case FileNameType.AutoFile:
                    return QuickSaveName + _ext;

                case FileNameType.Image:
                    return EmulatorStem + slot + ".png";

                case FileNameType.AutoImage:
                    return QuickSaveName + ".png";

                default:
                    return EmulatorStem + slot + _ext;
            }
        }

        /// <summary>
        /// BlastEm writes no thumbnail : save_slot_info only carries a description and a date.
        /// The generic RetroBat icon is used instead, as the jgenesis monitor does.
        /// </summary>
        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                if (File.Exists(_shotPath))
                    File.Copy(_shotPath, destScreenShot, true);
            }
            catch { }
        }
    }
}