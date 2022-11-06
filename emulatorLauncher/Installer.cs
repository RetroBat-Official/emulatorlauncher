using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace emulatorLauncher
{
    public class Installer
    {
        static List<Installer> installers = new List<Installer>
        {            
            // emulator / installation folder(s) / executable(s)
            // the 7z filename on the website must be the first installation folder name

            { new Installer("arcadeflashweb") },           
            { new Installer("libretro", "retroarch" ) }, { new Installer("angle", "retroarch" ) }, // "libretro_cores.7z" ???
            { new Installer("duckstation", "duckstation", "duckstation-nogui-x64-ReleaseLTCG.exe") },  
            { new Installer("kega-fusion", "kega-fusion", "Fusion.exe") }, 
            { new Installer("mesen") }, 
            { new Installer("model3", "supermodel") }, 
            { new Installer("supermodel") }, 
            { new Installer("rpcs3") }, { new Installer("ps3", "rpcs3") }, 
            { new Installer("pcsx2", "pcsx2", "pcsx2x64.exe") },
            { new Installer("pcsx2-16", "pcsx2-16", "pcsx2.exe") },
            { new Installer("fpinball", "fpinball", "Future Pinball.exe") }, { new Installer("bam", "fpinball", "Future Pinball.exe") }, 
            { new Installer("cemu") }, { new Installer("wiiu", "cemu") },
            { new Installer("applewin") }, { new Installer("apple2", "applewin") },
            { new Installer("gsplus") }, { new Installer("apple2gs", "gsplus") },             
            { new Installer("cxbx", new string[] { "cxbx-reloaded", "cxbx-r" }, "cxbx.exe") }, 
            { new Installer("chihiro", new string[] { "cxbx-reloaded", "cxbx-r" }, "cxbx.exe") }, 
            { new Installer("xbox", new string[] { "cxbx-reloaded", "cxbx-r" }, "cxbx.exe") },             
            { new Installer("citra") },            
            { new Installer("daphne") },
            { new Installer("demul") }, 
            { new Installer("demul-old", "demul-old", "demul.exe") }, 
            { new Installer("dolphin", new string[] { "dolphin-emu", "dolphin" }, "dolphin.exe") }, 
            { new Installer("triforce", "dolphin-triforce", "dolphinWX.exe") },  
            { new Installer("dosbox") },
            { new Installer("hypseus", "hypseus", "hypseus.exe") },
            { new Installer("love") }, 
            { new Installer("m2emulator", "m2emulator", "emulator_multicpu.exe") },
            { new Installer("mednafen", "mednafen") },        
            { new Installer("mgba", "mgba") }, 
            { new Installer("openbor") }, 
            { new Installer("scummvm") },             
            { new Installer("oricutron") },             
            { new Installer("ppsspp", "ppsspp", "PPSSPPWindows64.exe") }, 
            { new Installer("project64", "project64") }, 
            { new Installer("raine") },             
            { new Installer("mame64", new string[] { "mame", "mame64" }, new string[] { "mame.exe", "mame64.exe", "mame32.exe" }) },      
            { new Installer("redream") },             
            { new Installer("simcoupe") }, 
            { new Installer("snes9x", "snes9x", "snes9x-x64.exe") }, 
            { new Installer("solarus", "solarus", "solarus-run.exe") },             
            { new Installer("tsugaru", "tsugaru", "tsugaru_cui.exe") }, 
            { new Installer("vpinball", "vpinball", "vpinballx.exe") }, 
            { new Installer("winuae", "winuae", "winuae64.exe") }, 
            { new Installer("xemu", "xemu") },
			{ new Installer("nosgba", "nosgba", "no$gba.exe") },
            { new Installer("yuzu", "yuzu", "yuzu.exe") },
            { new Installer("ryujinx", "ryujinx", "Ryujinx.exe") },
            { new Installer("xenia-canary", "xenia-canary", "xenia_canary.exe" ) }
        };

        #region Properties
        public string Emulator { get; private set; }
        public string[] Folders { get; private set; }
        public string[] Executables { get; private set; }

        public string DefaultFolderName { get { return Folders[0]; } }
        public string ServerVersion { get; private set; }

        public string PackageUrl
        {
            get
            {
                return GetUpdateUrl(DefaultFolderName + ".7z");
            }
        }

        public static string GetUpdateUrl(string fileName)
        {
            string installerUrl = RegistryKeyEx.GetRegistryValue(
                RegistryKeyEx.CurrentUser,
                @"SOFTWARE\RetroBat",
                "InstallRootUrl") as string;

            if (string.IsNullOrEmpty(installerUrl))
                return string.Empty;

            if (installerUrl.EndsWith("/"))
                installerUrl = installerUrl + UpdatesType + "/emulators/" + fileName;
            else
                installerUrl = installerUrl + "/" + UpdatesType + "/emulators/" + fileName;

            return installerUrl;
        }

        public static string UpdatesType
        {
            get
            {
                string ret = Program.SystemConfig["updates.type"];
                if (string.IsNullOrEmpty(ret))
                    return "stable";

                return ret;
            }
        }

        public override string ToString()
        {
            return Emulator;
        }

        #endregion

        #region Constructors
        private Installer(string emulator)
        {
            Emulator = emulator;
            Folders = new string[] { emulator };
            Executables = new string[] {  emulator + ".exe" };
        }

        private Installer(string emulator, string folder, string exe = null)
        {
            Emulator = emulator;
            Folders = new string[] { folder };
            Executables = new string[] { exe == null ? folder + ".exe" : exe };
        }

        private Installer(string emulator, string[] folders, string exe = null)
        {
            Emulator = emulator;
            Folders = folders;
            Executables = new string[] { exe == null ? folders.First() + ".exe" : exe };
        }

        private Installer(string emulator, string[] folders, string[] executables)
        {
            Emulator = emulator;
            Folders = folders.ToArray();
            Executables = executables;
        }
        #endregion

        #region Factory
        public static Installer GetInstaller(string emulator = null)
        {
            if (!Misc.IsAvailableNetworkActive())
                return null;

            if (!Zip.IsSevenZipAvailable)
                return null;

            if (emulator == null)
                emulator = Program.SystemConfig["emulator"];

            if (string.IsNullOrEmpty(emulator))
                return null;

            Installer installer = installers.Where(g => g.Emulator == emulator).FirstOrDefault();
            if (installer == null && emulator.StartsWith("lr-"))
                installer = installers.Where(g => g.Emulator == "libretro").FirstOrDefault();
            if (installer == null)
                installer = installers.Where(g => g.Emulator == emulator).FirstOrDefault();
            if (installer == null)
                installer = installers.Where(g => g.Folders != null && g.Folders.Any(f => f == emulator)).FirstOrDefault();

            if (installer != null && string.IsNullOrEmpty(installer.PackageUrl))
                return null;

            return installer;
        }
        #endregion

        public string GetInstalledVersion()
        {
            try
            {
                string exe = GetInstalledExecutable();
                if (string.IsNullOrEmpty(exe))
                    return null;

                var versionInfo = FileVersionInfo.GetVersionInfo(exe);

                string version = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
                if (version != "0.0.0.0")
                    return version;

                // Retroarch specific
                if (Path.GetFileNameWithoutExtension(exe).ToLower() == "retroarch")
                {
                    var output = ProcessExtensions.RunWithOutput(exe, "--version");
                    output = StringExtensions.FormatVersionString(output.ExtractString(" -- v", " -- "));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else if (Path.GetFileNameWithoutExtension(exe).ToLower() == "demul")
                {
                    var output = ProcessExtensions.RunWithOutput(exe, "--help");
                    output = StringExtensions.FormatVersionString(output.ExtractString(") v", "\r"));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else if (Path.GetFileNameWithoutExtension(exe).ToLower() == "dolphin")
                {
                    var output = ProcessExtensions.RunWithOutput(exe, "--version");
                    output = StringExtensions.FormatVersionString(output.ExtractString("Dolphin ", "\r"));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else if (Path.GetFileNameWithoutExtension(exe).ToLower() == "gsplus")
                {
                    var output = ProcessExtensions.RunWithOutput(exe, "--help");
                    output = StringExtensions.FormatVersionString(output.ExtractString("GSplus v", " "));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else
                {
                    // Fake version number based on last write time
                    var date = File.GetLastWriteTime(exe).ToUniversalTime().ToString("0.yy.MM.dd");
                    return date;
                }
                
               
            }
            catch { }

            return null;
        }

        private static string _customInstallFolder;

        public string GetInstalledExecutable()
        {
            foreach (var folder in Folders)
            {
                string otherFolder = Program.AppConfig.GetFullPath(folder);
                if (string.IsNullOrEmpty(otherFolder))
                    continue;
                
                foreach (var executable in Executables)
                {
                    string exe = Path.Combine(otherFolder, executable);
                    if (File.Exists(exe))
                        return exe;
                }                
            }

            return null;
        }

        public string GetInstallFolder(bool checkRootPath = true)
        {
            if (!string.IsNullOrEmpty(_customInstallFolder))
                return Path.Combine(_customInstallFolder, DefaultFolderName);

            string folder = null;

            // If already installed
            string exe = GetInstalledExecutable();
            if (!string.IsNullOrEmpty(exe))
                folder = Path.GetDirectoryName(exe);

            if (checkRootPath && string.IsNullOrEmpty(folder))
            {
                foreach (var inst in installers)
                {
                    if (inst == this)
                        continue;

                    // Find another emulator folder - retroarch should always be there
                    string curr = inst.GetInstallFolder(false);
                    if (!string.IsNullOrEmpty(curr))
                    {
                        if (curr.EndsWith("\\"))
                            curr = curr.Substring(0, curr.Length-1);

                        if (Directory.Exists(curr))
                            return Path.Combine(Path.GetDirectoryName(curr), DefaultFolderName);
                    }
                }
            }

            return folder;
        }

        public bool IsInstalled()
        {
            return !string.IsNullOrEmpty(GetInstalledExecutable());
        }

        public bool HasUpdateAvailable()
        {
            if (!IsInstalled())
                return false;

            try
            {               
                string xml = null;

                string cachedFile = Path.Combine(Path.GetTempPath(), "emulationstation.tmp", "versions.xml");

                if (File.Exists(cachedFile) && DateTime.Now - File.GetCreationTime(cachedFile) <= new TimeSpan(1, 0, 0, 0))
                {
                    xml = File.ReadAllText(cachedFile);
                }
                else
                {
                    string url = Installer.GetUpdateUrl("versions.xml");
                    if (string.IsNullOrEmpty(url))
                        return false;

                    xml = WebTools.DownloadString(url);

                    if (!string.IsNullOrEmpty(xml))
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(cachedFile);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            if (File.Exists(cachedFile)) 
                                File.Delete(cachedFile);
                        }
                        catch { }

                        File.WriteAllText(cachedFile, xml);
                        File.SetCreationTime(cachedFile, DateTime.Now);
                    }
                }

                if (string.IsNullOrEmpty(xml))
                    return false;
                
                var settings = XDocument.Parse(xml);
                if (settings == null)
                    return false;
                
                string serverVersion = settings
                    .Descendants()
                    .Where(d => d.Name == "system" && d.Attribute("name") != null && d.Attribute("version") != null && d.Attribute("name").Value == DefaultFolderName)
                    .Select(d => d.Attribute("version").Value)
                    .FirstOrDefault();

                if (serverVersion == null)
                    return false;

                Version local = new Version();
                Version server = new Version();
                if (Version.TryParse(GetInstalledVersion(), out local) && Version.TryParse(serverVersion, out server))
                {
                    if (local < server)
                    {
                        ServerVersion = server.ToString();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error(ex.Message);
            }

            return false;
        }


        public bool CanInstall()
        {
            return WebTools.UrlExists(PackageUrl);
        }

        public static bool DownloadAndInstall(string url, string installFolder, ProgressChangedEventHandler progress = null)
        {
            string localFile = Path.GetFileName(url);
            string fn = Path.Combine(Path.GetTempPath(), "emulationstation.tmp", localFile);

            try { if (File.Exists(fn)) File.Delete(fn); }
            catch { }

            try
            {
                using (FileStream fileStream = new FileStream(fn, FileMode.Create))
                    WebTools.DownloadToStream(fileStream, url, progress);

                if (progress != null)
                    progress(null, new ProgressChangedEventArgs(100, null));

                Zip.Extract(fn, installFolder);
                return true;
            }
            finally
            {
                try { if (File.Exists(fn)) File.Delete(fn); }
                catch { }
            }

            return false;
        }

        public bool DownloadAndInstall(ProgressChangedEventHandler progress = null)
        {
            return DownloadAndInstall(PackageUrl, GetInstallFolder(), progress);           
        }

        #region CollectVersions
        public static void CollectVersions()
        {
            List<systeminfo> sys = new List<systeminfo>();

            foreach (var inst in installers)
            {
                if (sys.Any(s => s.name == inst.DefaultFolderName))
                    continue;

                sys.Add(new systeminfo()
                {
                    name = inst.DefaultFolderName,
                    version = inst.GetInstalledVersion()
                });
            }

            var xml = sys
                .OrderBy(s => s.name)
                .ToArray().ToXml().Replace("ArrayOfSystem>", "systems>");

            string fn = Path.Combine(Path.GetTempPath(), "systems.xml");
            File.WriteAllText(fn, xml);
            Process.Start(fn);
        }

        public static void InstallAllAndCollect(string customFolder)
        {
            _customInstallFolder = customFolder;

            try { Directory.CreateDirectory(_customInstallFolder); }
            catch { }

            HashSet<string> sys = new HashSet<string>();

            Kernel32.AllocConsole();

            foreach (var installer in installers)
            {
                if (sys.Contains(installer.DefaultFolderName))
                    continue;

                Console.WriteLine(installer.DefaultFolderName);
                installer.DownloadAndInstall();
                sys.Add(installer.DefaultFolderName);
            }

            Kernel32.FreeConsole();

            CollectVersions();

            try { Directory.Delete(_customInstallFolder, true); }
            catch { }

            _customInstallFolder = null;
        }
        #endregion        

        public static void UpdateAll(ProgressChangedEventHandler progress = null)
        {
            HashSet<string> sys = new HashSet<string>();

            List<Installer> toInstall = new List<Installer>();
            foreach (var installer in installers)
            {
                if (sys.Contains(installer.DefaultFolderName))
                    continue;

                if (!installer.IsInstalled())
                    continue;

                if (!installer.HasUpdateAvailable())
                    continue;

                toInstall.Add(installer);
            }

            int pos = 0;

            foreach (var installer in toInstall)
            {
                int cur = pos;

                installer.DownloadAndInstall((o, pe) =>
                    {
                        int globalPercentage = (cur + pe.ProgressPercentage) / toInstall.Count;

                        if (progress != null)
                            progress(o, new ProgressChangedEventArgs(globalPercentage, installer.Emulator));
                    });
                
                pos += 100;
            }
        }
    }

    [XmlType("system")]
    public class systeminfo
    {
        [XmlAttribute]
        public string name { get; set; }

        [XmlAttribute]
        public string version { get; set; }

        [XmlAttribute]
        public string date { get; set; }
    };
}
