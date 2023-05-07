using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using emulatorLauncher;
using System.IO;
using System.Xml.Serialization;
using emulatorLauncher.Tools;
using System.Net;

namespace batocera_store
{
    class Program
    {
        static string RootInstallPath { get { return Path.GetDirectoryName(Path.GetDirectoryName(typeof(Program).Assembly.Location)); } }
        static Repository[] Repositories { get; set; }
        static ConfigFile AppConfig { get; set; }

        static int Main(string[] args)
        {
            if (args.Length == 0)
            {                
                Console.WriteLine("usage :");
                Console.WriteLine("  install <package>");
                Console.WriteLine("  remove  <package>");
                Console.WriteLine("  list");
                Console.WriteLine("  list-repositories");
                Console.WriteLine("  clean");
                Console.WriteLine("  clean-all");
                Console.WriteLine("  refresh");
                Console.WriteLine("  update");
                return 0;
            }

            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info("[Startup] " + Environment.CommandLine);

            AppConfig = ConfigFile.FromFile(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "emulatorLauncher.cfg"));
            AppConfig.ImportOverrides(ConfigFile.FromArguments(args));

            var repositories = new List<Repository>();

            foreach (var path in Directory.GetFiles(Path.GetDirectoryName(typeof(Program).Assembly.Location), "batocera-store*.cfg"))
                repositories.AddRange(RepositoryFactory.FromFile(path));

            if (repositories.Count == 0)
            {
                SimpleLogger.Instance.Error("batocera-store.cfg not found or not valid");                        
                Console.WriteLine("batocera-store.cfg not found or not valid");
                return 1;
            }

            Repositories = repositories.ToArray();

            switch ((args[0]??"").Replace("-", "").ToLowerInvariant())
            {
                case "clean":
                case "clean-all":
                case "cleanall":
                case "refresh":
                    CleanPackages();
                    break;

                case "update":
                    // Mise à jour des packages installés ( si version > )
                    UpdatePackages();                    
                    break;

                case "list-repositories":
                case "listrepositories":
                    ListRepositories();
                    break;

                case "list":
                    ListPackages();
                    break;

                case "remove":
                case "uninstall":
                    if (args.Length < 2)
                    {
                        SimpleLogger.Instance.Error("invalid command line");
                        Console.WriteLine("invalid arguments");
                        break;
                    }

                    if (!UnInstall(args[1]))
                        return 1;

                    break;
                    
                case "install":
                    if (args.Length < 2)
                    {
                        SimpleLogger.Instance.Error("invalid command line");
                        Console.WriteLine("invalid arguments");
                        break;
                    }

                    if (!Install(args[1]))
                        return 1;

                    break;
            }           

            return 0;
        }

        static void ListRepositories()
        {
            foreach (var repo in Repositories)
                Console.WriteLine(repo.Name);
        }

        static void ListPackages()
        {
            var allInstalledPackages = PackageFileManager.GetInstalledPackages()
                .Packages
                .GroupBy(p => p.Repository)
                .ToDictionary(n => n.Key, n => n.ToDictionary(p => p.Name, p => p));
                        
            var items = new List<Package>();

            foreach (var repo in Repositories)
            {
                var packages = repo.GetOnlinePackages();
                if (packages == null)
                    continue;

                Dictionary<string, InstalledPackage> repoInstalledPackages = null;
                allInstalledPackages.TryGetValue(repo.Name, out repoInstalledPackages);

                foreach (var package in packages)
                {
                    package.Repository = repo.Name;

                    InstalledPackage installedPackage = null;

                    if (repoInstalledPackages != null)
                        repoInstalledPackages.TryGetValue(package.Name, out installedPackage);

                    if (installedPackage != null)
                    {
                        package.Status = "installed";
                        
                        if (string.IsNullOrEmpty(package.DownloadSize))
                            package.DownloadSize = installedPackage.DownloadSize;

                        package.InstalledSize = installedPackage.InstalledSize;
                    }
                    else
                    {
                        package.InstalledSize = null;
                        package.Status = null;
                    }

                    if (!string.IsNullOrEmpty(package.Version))
                    {
                        package.AvailableVersion = package.Version;
                        package.Version = null;
                    }

                    package.Games = null;
                    items.Add(package);
                }
            }

            var store = new StorePackages() { Packages = items.ToArray() };
            var xml = store.ToXml();

            Console.WriteLine(xml);
        }

        static void CleanPackages()
        {
            foreach (var repo in Repositories)
                repo.Cleanup();
        }
        
        static bool UnInstall(string packageName)
        {
            Repository repository = null;
            Package package = null;

            foreach (var repo in Repositories)
            {
                package = repo.GetOnlinePackages().FirstOrDefault(p => p.Name == packageName);
                if (package != null)
                {
                    repository = repo;
                    break;
                }
            }

            var installedPackages = PackageFileManager.GetInstalledPackages();

            InstalledPackage installed = null;

            if (repository != null)
                installed = installedPackages.Packages.FirstOrDefault(p => p.Repository == repository.Name && p.Name == packageName);

            if (installed == null)
                installed = installedPackages.Packages.FirstOrDefault(p => p.Name == packageName);

            if (installed == null)
            {
                SimpleLogger.Instance.Error("Package " + packageName + " is not installed");
                Console.WriteLine("Package " + packageName + " is not installed");
                return false;
            }

            NotifyProgress("Removing " + packageName);

            if (installed.InstalledFiles != null && installed.InstalledFiles.Files != null)
            {
                string root = RootInstallPath;

                foreach (var file in installed.InstalledFiles.Files)
                {
                    string fullPath = Path.Combine(root, file);
                    
                    if (File.Exists(fullPath))
                    {
                        try { File.Delete(fullPath); }
                        catch { }
                    }
                }

                foreach (var file in installed.InstalledFiles.Files.ToArray().Reverse())
                {
                    string fullPath = Path.Combine(root, file);

                    if (Directory.Exists(fullPath))
                    {
                        try { Directory.Delete(fullPath); }
                        catch { }
                    }
                }

                if (package != null)
                    NotifyEmulationStation(package, "removegames");
            }

            installedPackages.Packages.Remove(installed);
            installedPackages.Save();

            Console.WriteLine("OK");
            return true;
        }

        static bool Install(string packageName)
        {
            Repository repository = null;
            Package package = null;

            foreach (var repo in Repositories)
            {
                package = repo.GetOnlinePackages().FirstOrDefault(p => p.Name == packageName);
                if (package != null)
                {
                    repository = repo;
                    break;
                }
            }
          
            if (package == null)
            {
                Console.WriteLine("Package " + packageName + " not found");
                return false;
            }

            // Check disc space
            if (!string.IsNullOrEmpty(package.InstalledSize))
            {
                long size;
                if (long.TryParse(package.InstalledSize, out size) && size != 0)
                {
                    try
                    {
                        var drv = new DriveInfo(RootInstallPath.Substring(0, 1));

                        long freeSpace = drv.TotalFreeSpace / 1024;
                        if (size > freeSpace)
                        {
                            SimpleLogger.Instance.Error("Not enough space on drive to install");
                            Console.WriteLine("Not enough space on drive to install");
                            return false;
                        }
                    }
                    catch { }
                }
            }

            try
            {
                int pc = -1;

                int time = Environment.TickCount;

                string installFile = repository.DownloadPackageSetup(package, (o, pe) =>
                {
                    int curTime = Environment.TickCount;
                    if (pc != pe.ProgressPercentage && curTime - time > 50)
                    {
                        time = curTime;
                        pc = pe.ProgressPercentage;
                        NotifyProgress("Downloading " + packageName, pe.ProgressPercentage);
                    }
                });

                if (File.Exists(installFile))
                {
                    var entries = Zip.ListEntries(installFile);
                    if (entries.Length == 0)
                    {
                        Console.WriteLine("Error : file does not contain any content to install");
                        SimpleLogger.Instance.Error("Error : file '" + installFile + "' does not contain any content to install");

                        try { File.Delete(installFile); }
                        catch { }

                        return false;
                    }
                    else
                    {
                        NotifyProgress("Extracting " + packageName);

                        Zip.Extract(installFile, RootInstallPath);

                        NotifyProgress("Installing " + packageName);

                        NotifyEmulationStation(package, "addgames");

                        var installedPackages = PackageFileManager.GetInstalledPackages();

                        var installed = installedPackages.Packages.FirstOrDefault(p => p.Repository == repository.Name && p.Name == package.Name);
                        if (installed != null)
                            installedPackages.Packages.Remove(installed);

                        installedPackages.Packages.Add(new InstalledPackage()
                        {
                            Name = package.Name,
                            Repository = repository.Name,
                            Version = package.AvailableVersion,                            
                            DownloadSize = (new FileInfo(installFile).Length / 1024).ToString(),
                            InstalledSize = (entries.Sum(e => e.Length) / 1024).ToString(),
                            InstalledFiles = new InstalledFiles() { Files = entries.Select(e => e.Filename).ToList() }
                        });

                        installedPackages.Save();
                    }

                    try { File.Delete(installFile); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error : " + ex.Message);
                return false;
            }

            Console.WriteLine("OK");
            return true;
        }

        private static void UpdatePackages()
        {
            var allInstalledPackages = PackageFileManager.GetInstalledPackages()
                .Packages
                .GroupBy(p => p.Repository)
                .ToDictionary(n => n.Key, n => n.ToDictionary(p => p.Name, p => p));

            foreach (var repository in Repositories)
            {
                Dictionary<string, InstalledPackage> repoInstalledPackages = null;
                if (!allInstalledPackages.TryGetValue(repository.Name, out repoInstalledPackages))
                    continue;

                repository.Cleanup();

                foreach (var package in repository.GetOnlinePackages())
                {
                    InstalledPackage installedPackage = null;
                    if (!repoInstalledPackages.TryGetValue(package.Name, out installedPackage))
                        continue;

                    if (installedPackage.Version != package.AvailableVersion)
                        Install(package.Name);
                }
            }
        }

        private static void NotifyProgress(string text, int percent = -1)
        {
            if (percent < 0)
                Console.WriteLine(text);
            else
                Console.WriteLine(text + " >>> " + percent.ToString() + "%");
        }

        private static void NotifyEmulationStation(Package package, string verb)
        {
            if (package.Games == null || !package.Games.Any())
                return;

            foreach (var gp in package.Games.GroupBy(g => g.System))
            {
                if (string.IsNullOrEmpty(gp.Key))
                    continue;

                var gamelist = new GameList() { Games = new System.ComponentModel.BindingList<Game>() };
                foreach (var game in gp)
                {
                    try { gamelist.Games.Add(game.ToXml().FromXmlString<Game>()); }
                    catch { }
                }

                var xml = gamelist.ToXml();

                try
                {
                    WebTools.PostString("http://" + "127.0.0.1:1234/" + verb + "/" + gp.Key, xml);
                }
                catch (WebException wx)
                {
                    SimpleLogger.Instance.Debug("http://" + "127.0.0.1:1234/" + verb + "/" + gp.Key);
                    
                    string result = wx.Response != null ? wx.Response.ReadResponseString() : null;
                    if (!string.IsNullOrEmpty(result))
                        SimpleLogger.Instance.Error("Unable to add games to emulationstation : " + result, wx);                        
                    else
                        SimpleLogger.Instance.Error("Unable to add games to emulationstation", wx);                        
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Debug("http://" + "127.0.0.1:1234/" + verb + "/" + gp.Key);            

                    SimpleLogger.Instance.Error("Unable to add games to emulationstation", ex);
                }
            }
        }
    }    
}
