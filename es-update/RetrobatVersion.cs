using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using emulatorLauncher;

namespace RetrobatUpdater
{
    static class RetrobatVersion
    {
        public static string GetInstallUrl(string relativePath)
        {
            string installerUrl = RegistryKeyEx.GetRegistryValue(RegistryKeyEx.CurrentUser, @"SOFTWARE\RetroBat", "InstallRootUrl") as string;
            if (string.IsNullOrEmpty(installerUrl))
                return null;

            if (installerUrl.EndsWith("/"))
                return installerUrl + relativePath;

            return installerUrl + "/" + relativePath;
        }

        public static string GetRemoteVersion(string branch)
        {
            string url = GetInstallUrl(string.Format("{0}/version.info", branch));

            string remoteVersion = WebTools.DownloadString(url);
            if (!string.IsNullOrEmpty(remoteVersion))
                return remoteVersion;

            return null;
        }

        private static string GetLocalVersionInfoPath()
        {
            /*
            string localFile = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location)), "system", "version.info");
            if (File.Exists(localFile))
                return localFile;
            */

            string localFile = Path.Combine(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location), "version.info");
            return localFile;
        }

        public static string GetLocalVersion()
        {
            string localFile = GetLocalVersionInfoPath();
            if (File.Exists(localFile))
                return File.ReadAllText(localFile);

            return null;
        }

        public static void SetLocalVersion(string version)
        {
            string localFile = GetLocalVersionInfoPath();
            File.WriteAllText(localFile, version);
        }
    }
}
