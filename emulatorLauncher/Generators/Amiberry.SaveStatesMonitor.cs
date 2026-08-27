using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class AmiberrySaveStatesMonitor : SaveStatesWatcher
    {
        private const int ScreenshotTimeoutMs = 2000;
        private const int ScreenshotPollMs = 100;

        private readonly string _fallbackImage;
        private readonly string _thumbnailPath;
        private readonly string _romStem;
        private readonly string _emulatorBase;
        private readonly List<string> _expectedBases = new List<string>();
        private readonly Regex _amiberryRegex;

        private bool _highSlotWarned;

        public AmiberrySaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string thumbnailPath, string fallbackImage = null) : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Changed)
        {
            _thumbnailPath = thumbnailPath;
            _fallbackImage = fallbackImage;
            _romStem = Path.GetFileNameWithoutExtension(romfile);

            // RetroBat side: files are named after the rom stem.
            _expectedBases.Add(_romStem);

            // Emulator side: Amiberry names them after the inserted media. For a plain
            // rom that is the same string, but a .m3u puts disk 1 in DF0, so the state
            // is named after that disk instead of after the playlist.
            if (Path.GetExtension(romfile).ToLowerInvariant() == ".m3u")
            {
                try
                {
                    var m3u = MultiDiskImageFile.FromFile(romfile);
                    foreach (var disk in m3u.Files)
                    {
                        string stem = Path.GetFileNameWithoutExtension(disk);
                        if (!string.IsNullOrEmpty(stem) && !_expectedBases.Contains(stem))
                            _expectedBases.Add(stem);
                    }
                }
                catch { }
            }

            _emulatorBase = _expectedBases.Count > 1 ? _expectedBases[1] : _romStem;

            try
            {
                string txt = Path.Combine(sharedPath, _romStem + ".txt");
                if (File.Exists(txt))
                {
                    string recorded = File.ReadAllText(txt).Trim();
                    if (!string.IsNullOrEmpty(recorded))
                        _emulatorBase = recorded;
                }
            }
            catch { }

            string alternation = string.Join("|", _expectedBases.Select(Regex.Escape));
            _amiberryRegex = new Regex(@"^(?<base>" + alternation + @")(?:-(?<slot>[0-9]{1,2}))?\.uss$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (_amiberryRegex == null)
                return base.ParseSaveStateFilename(filename);

            string name = Path.GetFileName(filename);
            if (string.IsNullOrEmpty(name))
                return null;

            var match = _amiberryRegex.Match(name);
            if (!match.Success)
                return null;

            if (!match.Groups["slot"].Success)
                return null;

            int slot = match.Groups["slot"].Value.ToInteger();

            if (slot < FirstSlot || slot > LastSlot)
            {
                if (!_highSlotWarned)
                {
                    SimpleLogger.Instance.Warning("[Amiberry] Save state in slot " + slot + " ignored: RetroBat exposes slots " + FirstSlot + " to " + LastSlot + ". Use one of those in Amiberry's Savestates panel.");
                    _highSlotWarned = true;
                }
                return null;
            }

            return new SaveStateFileInfo
            {
                FullPath = filename,
                FileName = match.Groups["base"].Value,
                Slot = slot
            };
        }

        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            var info = ParseSaveStateFilename(saveState);
            if (info == null)
                return;

            // Not EmulatorPath: the thumbnail lives wherever screenshot_dir points.
            string source = Path.Combine(_thumbnailPath, MakeFilename(info.FileName, info.Slot, info.IsAutoSave ? FileNameType.AutoImage : FileNameType.Image));

            if (!CopyThumbnailWhenReady(source, destScreenShot))
            {
                SimpleLogger.Instance.Warning("[Amiberry] No thumbnail for " + Path.GetFileName(saveState) +
                    " after " + ScreenshotTimeoutMs + "ms (expected " + source + ").");

                if (!string.IsNullOrEmpty(_fallbackImage) && File.Exists(_fallbackImage))
                {
                    try { File.Copy(_fallbackImage, destScreenShot, true); }
                    catch { }
                }
            }
        }

        private static bool CopyThumbnailWhenReady(string source, string dest)
        {
            int waited = 0;
            while (waited < ScreenshotTimeoutMs)
            {
                if (File.Exists(source) && !FileTools.IsFileLocked(source))
                {
                    try
                    {
                        File.Copy(source, dest, true);
                        return true;
                    }
                    catch (IOException)
                    {
                        // Still being written; retry.
                    }
                }

                Thread.Sleep(ScreenshotPollMs);
                waited += ScreenshotPollMs;
            }

            return false;
        }

        public string GetQuickStateFilePath()
        {
            int slot = Slot;
            if (slot < FirstSlot || slot > LastSlot)
                return null;

            return Path.Combine(EmulatorPath, MakeFilename(_emulatorBase, slot, FileNameType.File));
        }

        /// <summary>
        /// Drop any thumbnail in the folder that no longer has a matching .uss.
        /// </summary>
        public void PruneOrphanThumbnails()
        {
            try
            {
                if (string.IsNullOrEmpty(_thumbnailPath) || !Directory.Exists(_thumbnailPath))
                    return;

                foreach (var image in Directory.GetFiles(_thumbnailPath, "*.png"))
                {
                    string stem = Path.GetFileNameWithoutExtension(image);

                    // Same anchored matching as ParseSaveStateFilename, so a thumbnail
                    // from another game is never a candidate.
                    if (!_amiberryRegex.IsMatch(stem + ".uss"))
                        continue;

                    if (File.Exists(Path.Combine(EmulatorPath, stem + ".uss")))
                        continue;

                    FileTools.TryDeleteFile(image);
                    SimpleLogger.Instance.Info("[Amiberry] Removed orphan thumbnail: " + Path.GetFileName(image));
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Amiberry] Could not prune thumbnails: " + ex.Message);
            }
        }

        /// <summary>
        /// Promote potential slot 0 to the first free slot once the emulator
        /// </summary>
        public void RescueDefaultSlot()
        {
            try
            {
                string source = _expectedBases
                    .Select(b => Path.Combine(EmulatorPath, b + ".uss"))
                    .FirstOrDefault(File.Exists);

                if (source == null)
                    return;

                int slot = GetNextFreeSlot();
                if (slot < FirstSlot || slot > LastSlot)
                {
                    SimpleLogger.Instance.Warning("[Amiberry] A state was saved in Amiberry's slot 0 but all " +
                        "RetroBat slots (" + FirstSlot + "-" + LastSlot + ") are taken. Left in place: " + source);
                    return;
                }

                string sourceBase = Path.GetFileNameWithoutExtension(source);
                string sourceImage = Path.Combine(_thumbnailPath, sourceBase + ".png");

                string destState = Path.Combine(SaveStatesPath, MakeFilename(_romStem, slot, FileNameType.File));
                string destImage = Path.Combine(SaveStatesPath, MakeFilename(_romStem, slot, FileNameType.Image));

                FileTools.TryCopyFile(source, destState);

                if (File.Exists(sourceImage))
                    FileTools.TryCopyFile(sourceImage, destImage);
                else if (!string.IsNullOrEmpty(_fallbackImage) && File.Exists(_fallbackImage))
                    FileTools.TryCopyFile(_fallbackImage, destImage);

                File.WriteAllText(Path.Combine(SaveStatesPath, _romStem + ".txt"), sourceBase);

                // Remove the originals so the next session does not promote them again.
                try { File.Delete(source); } catch { }
                if (File.Exists(sourceImage))
                    try { File.Delete(sourceImage); } catch { }

                SimpleLogger.Instance.Info("[Amiberry] State saved in Amiberry's slot 0 promoted to RetroBat slot " + slot + ".");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Amiberry] Could not recover Amiberry's slot 0: " + ex.Message);
            }
        }
    }
}
