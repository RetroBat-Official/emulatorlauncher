using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher.Libretro
{
    class LibRetroStateFileManager : IDisposable
    {
        public static LibRetroStateFileManager FromSaveStateFile(string state_file)
        {
            if (string.IsNullOrEmpty(state_file) || !File.Exists(state_file))
                return null;

            return new LibRetroStateFileManager(state_file);
        }

        public bool IsAutoFile { get; set; }

        private LibRetroStateFileManager(string state_file)
        {
            var autoFilename = GetAutoFilename(state_file);

            // If it's the autosave file, then ignore
            if (Path.GetFullPath(state_file).Equals(Path.GetFullPath(autoFilename), StringComparison.InvariantCultureIgnoreCase))
            {
                IsAutoFile = true;
                return;
            }         

            // Copy to state.auto file, dispose will restore the file
            if (File.Exists(autoFilename))
            {
                try { File.Delete(autoFilename + ".bak"); }
                catch { }
                File.Move(autoFilename, autoFilename + ".bak");

                mAutoFileBackup = autoFilename;
            }

            // Copy to state.auto.png file
            var autoImage = autoFilename + ".png";
            if (File.Exists(autoImage))
            {
                try { File.Delete(autoImage + ".bak"); }
                catch { }
                File.Move(autoImage, autoImage + ".bak");

                mAutoImageBackup = autoImage;
            }

            File.Copy(state_file, autoFilename, true);

            mAutoFileToDelete = autoFilename;
            mAutoFileHash = FileTools.GetMD5(mAutoFileToDelete);
        }

        private string mAutoImageBackup;
        private string mAutoFileBackup;

        private string mAutoFileToDelete;
        private string mAutoFileHash;

        private string GetAutoFilename(string state_file)
        {
            string fn = Path.Combine(
                Path.GetDirectoryName(state_file),
                Path.GetFileNameWithoutExtension(state_file.Replace(".state.auto", "")) + ".state.auto");

            return fn;
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(mAutoFileToDelete) && mAutoFileHash == FileTools.GetMD5(mAutoFileToDelete))
            {
                try { File.Delete(mAutoFileToDelete); }
                catch { }             
            }

            if (!string.IsNullOrEmpty(mAutoFileBackup))
            {
                try { File.Delete(mAutoFileBackup); }
                catch { }
                File.Move(mAutoFileBackup + ".bak", mAutoFileBackup);
            }

            if (!string.IsNullOrEmpty(mAutoImageBackup))
            {
                try { File.Delete(mAutoImageBackup); }
                catch { }
                File.Move(mAutoImageBackup + ".bak", mAutoImageBackup);
            }
        }
    }

}
