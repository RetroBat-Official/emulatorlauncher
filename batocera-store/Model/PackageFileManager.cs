using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace batocera_store
{
    static class PackageFileManager
    {
        static string RootPath
        {
            get
            {
                var path = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "store");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        public static InstalledPackages GetInstalledPackages()
        {
            string file = Path.Combine(RootPath, "packages.cfg");

            InstalledPackages ret =  InstalledPackages.FromFile(file);

            if (ret.Packages == null)
                ret.Packages = new List<InstalledPackage>();

            return ret;
        }

        public static Package[] GetOnlinePackages(this Repository repo)
        {
            string file = Path.Combine(RootPath, repo.Name + ".cfg");

            if (!File.Exists(file) || (DateTime.Now - File.GetCreationTime(file) > new TimeSpan(0, 0, 15, 0)))
                DownloadControlFile(repo);

            if (File.Exists(file))
            {
                var packages = file.FromXml<StorePackages>();
                if (packages != null)
                    return packages.Packages;
            }

            return null;
        }

        static void DownloadControlFile(this Repository repo)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Url))
                return;

            string file = Path.Combine(RootPath, repo.Name + ".cfg");

            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch 
            {
            }

            Uri uri = new Uri(repo.Url + "/store.xml");

            if (uri.Scheme == "file")
            {
                File.Copy(uri.AbsoluteUri, file, true);
                return;
            }

            try
            {
                using (FileStream fileStream = new FileStream(file, FileMode.Create))
                    WebTools.DownloadToStream(fileStream, uri.AbsoluteUri);

                File.SetCreationTime(file, DateTime.Now);
            }
            catch
            {
                try 
                { 
                    if (File.Exists(file)) 
                        File.Delete(file); 
                }
                catch { }
            }
        }

        public static void Cleanup(this Repository repo)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Url))
                return;

            string file = Path.Combine(RootPath, repo.Name + ".cfg");

            try 
                { 
                    if (File.Exists(file)) 
                        File.Delete(file); 
                }
                catch { }
        }

        public static string DownloadPackageSetup(this Repository repo, Package package, ProgressChangedEventHandler progress = null)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Url))
                return null;

            string localFile = Path.Combine(Path.GetTempPath(), package.Name + ".7z");

            Uri uri = new Uri(!string.IsNullOrEmpty(package.DownloadUrl) ? package.DownloadUrl : repo.Url + "/" + package.Name + ".7z");

            if (!string.IsNullOrEmpty(package.DownloadUrl))
            {
                string ext = Path.GetExtension(package.DownloadUrl).ToLowerInvariant();
                if (ext.Length == 4)
                    localFile = Path.ChangeExtension(localFile, ext);
            }

            if (uri.Scheme == "file")
            {
                File.Copy(uri.AbsoluteUri, localFile, true);
                return localFile;
            }

            try
            {
                if (File.Exists(localFile))
                    File.Delete(localFile);

                using (FileStream fileStream = new FileStream(localFile, FileMode.Create))
                    WebTools.DownloadToStream(fileStream, uri.AbsoluteUri, progress);
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(localFile))
                        File.Delete(localFile);
                }
                catch { }

                throw ex;
            }

            return localFile;
        }
    }
}
