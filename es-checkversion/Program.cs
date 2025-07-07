using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using RetrobatUpdater;
using EmulatorLauncher.Common;

namespace es_checkversion
{
    class Program
    {
        private static List<string> oldFiles = new List<string>
        {
            "7z.dll.old",
            "emulationstation.exe.old",
            "Emulatorlauncher.Common.dll.old",
            "es-update.exe.old",
            "FreeImage.dll.old",
            "libcurl.dll.old",
            "libvlc.dll.old",
            "libvlccore.dll.old",
            "SDL2.dll.old",
            "SDL2_mixer.dll.old",
        };

        static int Main(string[] args)
        {
            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info("[Startup] " + Environment.CommandLine);

            try
            {
                string branch = args.SkipWhile(a => a != "-branch").Skip(1).FirstOrDefault();
                if (string.IsNullOrEmpty(branch))
                    branch = "stable";

                SimpleLogger.Instance.Info("[INFO] Branch: " + branch);

                string localVersion = RetrobatVersion.GetLocalVersion();
                if (string.IsNullOrEmpty(localVersion))
                    throw new ApplicationException("Retrobat is not properly installed");
                SimpleLogger.Instance.Info("[INFO] Local Version: " + localVersion);

                string esVersionFile = Path.Combine(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location), "version.info");
                if (!string.IsNullOrEmpty(localVersion))
                    File.WriteAllText(esVersionFile, localVersion);

                string remoteVersion = RetrobatVersion.GetRemoteVersion(branch, localVersion);
                if (string.IsNullOrEmpty(remoteVersion))
                    throw new ApplicationException("Unable to get remote version");
                SimpleLogger.Instance.Info("[INFO] Available remote Version: " + remoteVersion);

                foreach (string old in oldFiles)
                {
                    string oldFile = Path.Combine(Path.GetDirectoryName(typeof(RetrobatVersion).Assembly.Location), old);
                    if (File.Exists(oldFile))
                    {
                        SimpleLogger.Instance.Info("[INFO] Removing old file: " + oldFile);
                        try { File.Delete(oldFile); }
                        catch { }
                    }
                }

                if (localVersion != remoteVersion)
                {
                    SimpleLogger.Instance.Info("[INFO] A new version is available for the " + branch + " branch, version:" + remoteVersion);
                    Console.WriteLine(remoteVersion);
                    return 0;
                }
                else
                    SimpleLogger.Instance.Info("[INFO] You already have the latest version for the branch: " + branch);

                return 1;
            }
            catch(Exception ex)
            {
                SimpleLogger.Instance.Error(ex.Message);
                return 1;
            }
        }
    }
}
