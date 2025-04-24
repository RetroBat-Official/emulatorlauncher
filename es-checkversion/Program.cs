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

                string remoteVersion = RetrobatVersion.GetRemoteVersion(branch, localVersion);
                if (string.IsNullOrEmpty(remoteVersion))
                    throw new ApplicationException("Unable to get remote version");
                SimpleLogger.Instance.Info("[INFO] Available remote Version: " + remoteVersion);

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
