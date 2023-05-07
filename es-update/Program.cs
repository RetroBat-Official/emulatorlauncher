using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using emulatorLauncher;
using System.Security.Cryptography;

namespace RetrobatUpdater
{
    class Program
    {
        static string UpdateRelativeUrl = "{0}/archives/retrobat-v{1}.zip";

        static int Main(string[] args)
        {
            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info("[Startup] " + Environment.CommandLine);

            try
            {
                string rootPath = args.SkipWhile(a => a != "-root").Skip(1).FirstOrDefault();
                if (string.IsNullOrEmpty(rootPath))
                    rootPath = Path.GetDirectoryName(Path.GetDirectoryName(typeof(Program).Assembly.Location));

                string branch = args.SkipWhile(a => a != "-branch").Skip(1).FirstOrDefault();
                if (string.IsNullOrEmpty(branch))
                    branch = "stable";

                string localVersion = RetrobatVersion.GetLocalVersion();
                if (string.IsNullOrEmpty(localVersion))
                {
                    ConsoleOutput("Retrobat is not properly installed");
                    return 1;
                }

                string remoteVersion = RetrobatVersion.GetRemoteVersion(branch);
                if (string.IsNullOrEmpty(remoteVersion) || remoteVersion == localVersion)
                {
                    ConsoleOutput("No update available");
                    return 1;
                }

                string url = RetrobatVersion.GetInstallUrl(string.Format(UpdateRelativeUrl, branch, remoteVersion));
                if (string.IsNullOrEmpty(url))
                    return -1;

                /*
#if DEBUG
                if (Directory.Exists(@"H:\retrobat"))
                {
                    rootPath = @"H:\retrobat";
                    var z = ProcessUpgradeActions(@"c:\temp\upgrade.xml", @"H:\retrobat", localVersion);
                    z.IsOverridable("retrobat.ini");
                    return -1;
                }
#endif
                */

                // Create temporary download folder
                string tempDirectory = Path.Combine(Path.GetTempPath(), "es-download");

                try
                {
                    if (Directory.Exists(tempDirectory))
                        Directory.Delete(tempDirectory, true);
                }
                catch { }

                try { Directory.CreateDirectory(tempDirectory); }
                catch { }

                // Download Zip Archive
                int lastPercent = -1;

                string file = WebTools.DownloadFile(url, tempDirectory, (o, e) =>
                {
                    int percent = e.ProgressPercentage;
                    if (percent != lastPercent)
                    {
                        ConsoleOutput("Downloading >>> " + percent.ToString() + "%");
                        lastPercent = percent;
                    }
                });

                if (string.IsNullOrEmpty(file))
                {
                    SimpleLogger.Instance.Error("Failed to download update");
                    ConsoleOutput("Failed to download update");
                    return 1;
                }

                // Check SHA
                if (false) // disabled
                {
                    try
                    {
                        string remoteSha = WebTools.DownloadString(url + ".sha256.txt");
                        if (!string.IsNullOrEmpty(remoteSha))
                        {
                            var localSha = GetSha256(file);

                            if (remoteSha != localSha)
                            {
                                SimpleLogger.Instance.Warning("SHA mismatch");
                            }
                        }
                    }
                    catch { }
                }                

                // Extract zip archive
                lastPercent = -1;

                using (var archive = ZipArchive.OpenRead(file))
                {
                    var entries = archive.Entries.ToList();

                    // Find upgrade.xml
                    UpgradeInformationFile upgradeInfo = null;
                    var upgrade = entries.FirstOrDefault(e => e.Filename == "upgrade.xml");
                    if (upgrade != null)
                    {
                        upgrade.Extract(tempDirectory);

                        // Process upgrade actions
                        string upgradeFile = Path.Combine(tempDirectory, upgrade.Filename);
                        upgradeInfo = ProcessUpgradeActions(upgradeFile, rootPath, localVersion);

                        try { File.Delete(upgradeFile); }
                        catch { }
                    }

                    int idx = 0;
                    foreach (var entry in entries)
                    {
                        // Don't extract upgrade.xml file
                        if (entry.Filename == "upgrade.xml")
                            continue;

                        // don't overwrite if excluded
                        if (upgradeInfo != null && !upgradeInfo.IsOverridable(entry.Filename))
                            if (File.Exists(Path.Combine(rootPath, entry.Filename)))
                                continue;
                        
                        entry.Extract(rootPath);

                        int percent = (idx * 100) / entries.Count;
                        if (percent != lastPercent)
                        {
                            ConsoleOutput("Installing >>> " + percent.ToString() + "%");
                            lastPercent = percent;
                        }

                        idx++;
                    }
                }

                // Delete zip file
                try { File.Delete(file); }
                catch { }

                // Update local version info
                RetrobatVersion.SetLocalVersion(remoteVersion);

                ConsoleOutput("UPDATE DONE");
                return 0;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error(ex.Message, ex);
                ConsoleOutput(ex.Message);
                return 1;
            }
        }


        static bool _cursorTopSupported = true;

        static void ConsoleOutput(string line)
        {
            if (_cursorTopSupported)
            {
                try
                {
                    int currentLineCursor = Console.CursorTop;
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, currentLineCursor);
                }
                catch 
                {
                    _cursorTopSupported = false;
                }
            }

            Console.WriteLine(line);

            if (_cursorTopSupported)
            {
                try
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
                catch { }
            }
        }

        private static string GetSha256(string filePath)
        {
            using (var SHA256 = SHA256Managed.Create())
            {
                using (FileStream fileStream = System.IO.File.OpenRead(filePath))
                {
                    string result = "";
                    foreach (var hash in SHA256.ComputeHash(fileStream))
                        result += hash.ToString("x2");

                    return result;
                }
            }
        }

        private static UpgradeInformationFile ProcessUpgradeActions(string upgradeFile, string rootPath, string localVersion)
        {
            UpgradeInformationFile upgrades = null;

            try
            {
                if (File.Exists(upgradeFile))
                {
                    upgrades = UpgradeInformationFile.FromXml(upgradeFile);
                    if (upgrades != null)
                        upgrades.Process(rootPath, localVersion);
                }                
            }
            catch { }

            return upgrades ?? new UpgradeInformationFile();
        }
    }
}
