using System;
using System.IO;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    /// <summary>
    /// Model3-Vulkan writes <game>.zst<slot> in Data\Saves (slots 1 to 16) : a ZIP container (meta.json, core.zst, thumb.png)
    /// written to <file>.tmp, then renamed (HostCore\VkState.cs, Save). The rename is imported, thumb.png becomes the RetroBat screenshot.
    /// </summary>
    class Model3VulkanSaveStatesMonitor : SaveStatesWatcher
    {
        private readonly string _iconPath;
        private readonly string _romStem;

        public Model3VulkanSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath, string iconPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Rename)
        {
            _iconPath = iconPath;
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        /// <summary>
        /// In incremental mode, the RetroBat slot differs from the emulator slot given to the base class:
        /// the picture is written next to the state that has just been copied (most recent one).
        /// </summary>
        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                var latest = new DirectoryInfo(SaveStatesPath).GetFiles(_romStem + ".zst*")
                    .Where(f => !f.Extension.Equals(".png", StringComparison.InvariantCultureIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                var info = latest == null ? null : ParseSaveStateFilename(latest.FullName);
                if (info != null && !info.IsAutoSave)
                    destScreenShot = Path.Combine(SaveStatesPath, MakeFilename(_romStem, info.Slot, FileNameType.Image));

                if (ExtractThumbnail(saveState, destScreenShot))
                    return;

                if (File.Exists(_iconPath))
                    File.Copy(_iconPath, destScreenShot, true);
            }
            catch { }
        }

        private static bool ExtractThumbnail(string saveState, string destScreenShot)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "model3-vulkan-thumb");

            try
            {
                if (Directory.Exists(tempPath))
                    Directory.Delete(tempPath, true);

                Directory.CreateDirectory(tempPath);

                // A legacy .st file (no thumbnail) is not a ZIP archive
                using (var archive = Zip.Open(saveState))
                {
                    if (archive == null || !archive.Entries.Any(e => e.Filename == "thumb.png"))
                        return false;

                    archive.Extract(tempPath, "thumb.png");
                }

                string thumb = Path.Combine(tempPath, "thumb.png");
                if (!File.Exists(thumb))
                    return false;

                File.Copy(thumb, destScreenShot, true);
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Model3Vulkan] Unable to read the save state picture : " + ex.Message);
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true); } catch { }
            }
        }
    }
}
