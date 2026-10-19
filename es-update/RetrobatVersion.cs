using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common;

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

        public static string GetInstallUrlNew(string relativePath)
        {
            string installerUrl = RegistryKeyEx.GetRegistryValue(RegistryKeyEx.CurrentUser, @"SOFTWARE\RetroBat", "InstallRootUrlNew") as string;
            if (string.IsNullOrEmpty(installerUrl))
                return null;

            if (installerUrl.EndsWith("/"))
                return installerUrl + relativePath;

            return installerUrl + "/" + relativePath;
        }

        public static string GetRemoteVersion(string branch, string localversion)
        {
            string relativePath = string.Format("{0}/{1}/remote_version.info", branch, localversion);

            // Try the new host first, then the legacy one.
            // Each attempt is isolated: a network failure on one host (timeout, DNS, TLS, HTTP 403 on VPN IPs...)
            // must not prevent the fallback, as WebTools.DownloadString throws instead of returning null.
            foreach (string url in new string[] { GetInstallUrlNew(relativePath), GetInstallUrl(relativePath) }.Distinct())
            {
                if (string.IsNullOrEmpty(url))
                    continue;

                try
                {
                    string remoteVersion = WebTools.DownloadString(url);
                    if (!string.IsNullOrEmpty(remoteVersion))
                    {
                        SimpleLogger.Instance.Info("[INFO] Remote version retrieved from: " + url);
                        return remoteVersion;
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error("[ERROR] Unable to get remote version from " + url + " : " + ex.Message);
                }
            }

            return null;
        }

        private static string GetLocalVersionInfoPath()
        {
            /*
            string localFile = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location)), "system", "version.info");
            if (File.Exists(localFile))
                return localFile;
            */

            string localFile = Path.Combine(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location),"..", "system", "version.info");
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
            string esVersionFile = Path.Combine(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location), "version.info");
            File.WriteAllText(esVersionFile, version);
        }
    }
}
