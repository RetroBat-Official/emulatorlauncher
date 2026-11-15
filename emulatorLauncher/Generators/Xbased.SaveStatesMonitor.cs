using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// xbased writes states\<TITLEID>.<version>\slot<N>.hxs (a .tmp file renamed when complete), then slot<N>.bmp.
    /// A state needs the blob store next to it (blobs\<hash>.gz: the game's cache drives), so the blobs are copied
    /// to the RetroBat folder too: a state chosen in EmulationStation is loaded from there with loadstate=.
    /// </summary>
    class XbasedSaveStatesMonitor : SaveStatesWatcher
    {
        private static readonly Regex _stateRegex = new Regex(@"^([0-9A-F]{8}\.[0-9]+)\\slot([0-9]+)\.hxs$", RegexOptions.IgnoreCase);

        // slot<N>.bmp is written after the state
        private static readonly TimeSpan _pictureWait = TimeSpan.FromSeconds(5);

        private readonly string _romStem;

        public XbasedSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath, SaveStatesWatcherMethod.Rename)
        {
            _romStem = Path.GetFileNameWithoutExtension(romfile);
        }

        // The states are in a folder per game
        protected override bool IncludeSubdirectories { get { return true; } }

        protected override SaveStateFileInfo ParseSaveStateFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            // Emulator side : <TITLEID>.<version>\slot<N>.hxs
            string root = EmulatorPath.TrimEnd('\\') + "\\";
            if (filename.StartsWith(root, StringComparison.InvariantCultureIgnoreCase))
            {
                var match = _stateRegex.Match(filename.Substring(root.Length));
                if (!match.Success)
                    return null;

                return new SaveStateFileInfo()
                {
                    FullPath = filename,
                    FileName = match.Groups[1].Value.ToUpperInvariant(),
                    Slot = match.Groups[2].Value.ToInteger()
                };
            }

            // RetroBat side : es_savestates.cfg pattern
            return base.ParseSaveStateFilename(filename);
        }

        /// <summary>
        /// Called once the state is copied: blobs of the game, then its picture as png.
        /// In incremental mode the RetroBat slot differs from the emulator one: the picture goes next to the latest state.
        /// </summary>
        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            string gameFolder = Path.GetDirectoryName(saveState);

            CopyBlobs(gameFolder);

            try
            {
                var latest = new DirectoryInfo(SaveStatesPath).GetFiles(_romStem + ".*.hxs")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                var info = latest == null ? null : base.ParseSaveStateFilename(latest.FullName);
                if (info != null && !info.IsAutoSave)
                    destScreenShot = Path.Combine(SaveStatesPath, MakeFilename(_romStem, info.Slot, FileNameType.Image));

                string bmp = Path.ChangeExtension(saveState, ".bmp");
                if (!WaitForPicture(bmp, File.GetLastWriteTimeUtc(saveState)))
                    return;

                using (var source = Image.FromFile(bmp))
                {
                    // Thumbnail size, the console's picture is 640x480
                    int width = Math.Min(640, source.Width);
                    int height = source.Height * width / source.Width;

                    using (var thumb = new Bitmap(source, width, height))
                        thumb.Save(destScreenShot, ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[xbased] Save state picture not copied : " + ex.Message);
            }
        }

        private static bool WaitForPicture(string bmp, DateTime stateTime)
        {
            var limit = DateTime.UtcNow + _pictureWait;

            while (DateTime.UtcNow < limit)
            {
                if (File.Exists(bmp) && File.GetLastWriteTimeUtc(bmp) >= stateTime && !FileTools.IsFileLocked(bmp))
                    return true;

                Thread.Sleep(100);
            }

            return false;
        }

        /// <summary>
        /// Blobs are named by their content: only the missing ones are copied, one store for all the games.
        /// </summary>
        private void CopyBlobs(string gameFolder)
        {
            string source = Path.Combine(gameFolder, "blobs");
            if (!Directory.Exists(source))
                return;

            string target = Path.Combine(SaveStatesPath, "blobs");

            try
            {
                FileTools.TryCreateDirectory(target);

                foreach (var file in Directory.GetFiles(source))
                {
                    string name = Path.GetFileName(file);
                    if (name.Equals("index", StringComparison.InvariantCultureIgnoreCase) || name.EndsWith(".tmp", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    string dest = Path.Combine(target, name);
                    if (!File.Exists(dest))
                        File.Copy(file, dest);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[xbased] Save state blobs not copied : " + ex.Message);
            }
        }
    }
}
