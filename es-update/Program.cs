using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace RetrobatUpdater
{
    class Program
    {
        static string UpdateRelativeUrl = "{0}/archives/retrobat-v{1}.zip";

        static int Main(string[] args)
        {
            string logFile = "es-update.log";
            File.WriteAllText(logFile, string.Empty);
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

                string remoteVersion = RetrobatVersion.GetRemoteVersion(branch, localVersion);
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

                try 
                { 
                    Directory.CreateDirectory(tempDirectory);
                    SimpleLogger.Instance.Info("[INFO] Creation of temporary folder: " + tempDirectory);
                }
                catch { SimpleLogger.Instance.Error("[ERROR] Unable to create temp folder: " + tempDirectory); }

                // Download Zip Archive
                int lastPercent = -1;
                SimpleLogger.Instance.Info("[INFO] Downloading update from: " + url);
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
                    SimpleLogger.Instance.Error("[ERROR] Failed to download update");
                    ConsoleOutput("Failed to download update");
                    return 1;
                }       

                // Extract zip archive
                lastPercent = -1;

                using (var archive = Zip.Open(file))
                {
                    var entries = archive.Entries.ToList();

                    // Find upgrade.xml
                    UpgradeInformationFile upgradeInfo = null;
                    var upgrade = entries.FirstOrDefault(e => e.Filename == "system\\upgrade.xml");
                    if (upgrade != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Reading upgrade file: " + upgrade);
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
                        if (entry.Filename == "system\\upgrade.xml")
                            continue;

                        // don't overwrite if excluded
                        if (upgradeInfo != null && !upgradeInfo.IsOverridable(entry.Filename))
                            if (File.Exists(Path.Combine(rootPath, entry.Filename)))
                                continue;

                        try
                        {
                            string target = Path.Combine(rootPath, entry.Filename);

                            // Do not overwrite NVRAM files if they exist
                            if (File.Exists(target))
                            {
                                bool isNVRAM = CheckNVRam(target);
                                if (isNVRAM)
                                {
                                    SimpleLogger.Instance.Info("[INFO] Skipped NVRAM File : " + target + " as it already exists.");
                                    continue;
                                }
                            }

                            if (File.Exists(target))
                            {
                                string copyTarget = target + ".old";
                                if (File.Exists(copyTarget))
                                    try { File.Delete(copyTarget); } catch { } // delete old copy
                                File.Move(target, target + ".old");
                            }
                            entry.Extract(rootPath);
                            FileTools.TryDeleteFile(target + ".old");

                            SimpleLogger.Instance.Info("Copied File : " + target);

                            if (File.Exists(target + ".old"))
                            {
                                SimpleLogger.Instance.Info("Could not delete old file : " + target + ".old");
                            }
                        }
                        catch 
                        { }

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

        private static bool CheckNVRam(string path)
        {
            if (path.Contains("emulators\\flycast\\data") && (path.EndsWith(".eeprom") || path.EndsWith(".nvmem") || path.EndsWith(".nvmem2")))
                return true;
            if (path.Contains("saves\\mame\\nvram"))
                return true;
            if (path.Contains("emulators\\m2emulator\\NVDATA") && path.EndsWith(".DAT"))
                return true;
            if (path.Contains("emulators\\supermodel\\NVRAM") && path.EndsWith(".nv"))
                return true;

            return false;
        }

        private static UpgradeInformationFile ProcessUpgradeActions(string upgradeFile, string rootPath, string localVersion)
        {
            UpgradeInformationFile upgrades = null;

            try
            {
                if (File.Exists(upgradeFile))
                {
                    SimpleLogger.Instance.Info("[INFO] Reading upgrade.xml file, looking for upgrade actions.");
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
