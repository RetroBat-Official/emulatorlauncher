using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.EmulationStation;
using static EmulatorLauncher.PadToKeyboard.SendKey;
using EmulatorLauncher.Common.Compression.Wrappers;

namespace EmulatorLauncher
{
    public class Installer
    {
        static List<Installer> installers = new List<Installer>
        {            
            // emulator / installation folder(s) / executable(s)
            // the 7z filename on the website must be the first installation folder name
            { new Installer("altirra", "altirra", "Altirra64.exe") },
            { new Installer("angle", "retroarch" ) },
            { new Installer("apple2", "applewin") },
            { new Installer("apple2gs", "gsplus") },
            { new Installer("applewin") },
            { new Installer("arcadeflashweb") },
            { new Installer("ares", "ares", "ares.exe") },
            { new Installer("azahar", "azahar", "azahar.exe") },
            { new Installer("bam", "fpinball", "Future Pinball.exe") },
            { new Installer("bigpemu", "bigpemu", "BigPEmu.exe") },
            { new Installer("bizhawk", "bizhawk", "EmuHawk.exe") },
            { new Installer("capriceforever", "capriceforever", "Caprice64.exe") },
            { new Installer("cdogs", new string[] { "cdogs", "cdogs/bin" }, "cdogs-sdl.exe") },
            { new Installer("cemu", "cemu", "Cemu.exe") },
            { new Installer("cgenius", "cgenius", "CGenius.exe") },
            { new Installer("chihiro", new string[] { "cxbx-reloaded", "cxbx-r" }, "cxbx.exe") },
            { new Installer("citra", "citra", "citra-qt.exe") },
            { new Installer("citron", "citron", "citron.exe") },
            { new Installer("corsixth", "corsixth", "CorsixTH.exe") },
            { new Installer("cxbx", new string[] { "cxbx-reloaded", "cxbx-r" }, "cxbx.exe") },
            { new Installer("daphne") },
            { new Installer("demul") },
            { new Installer("demul-old", "demul-old", "demul.exe") },
            { new Installer("devilutionx", "devilutionx", "devilutionx.exe") },
            { new Installer("dhewm3", "dhewm3", "dhewm3.exe") },
            { new Installer("dolphin", "dolphin-emu", "Dolphin.exe") },
            { new Installer("dosbox") },
            { new Installer("duckstation", new string[] { "duckstation"}, new string[] { "duckstation-qt-x64-ReleaseLTCG.exe" }) },
            { new Installer("eduke32", "eduke32", "eduke32.exe") },
            { new Installer("eka2l1", "eka2l1", "eka2l1_qt.exe") },
            { new Installer("fbneo", "fbneo", "fbneo64.exe") },
            { new Installer("flycast", "flycast", "flycast.exe") },
            { new Installer("fpinball", "fpinball", "Future Pinball.exe") },
            { new Installer("gemrb", "gemrb", "gemrb.exe") },
            { new Installer("gopher64", "gopher64", "gopher64-windows-x86_64.exe") },
            { new Installer("gsplus") },
            { new Installer("gzdoom", "gzdoom", "gzdoom.exe") },
            { new Installer("hatari", "hatari", "hatari.exe") },
            { new Installer("hbmame", "hbmame", "hbmameui.exe") },
            { new Installer("hypseus", "hypseus", "hypseus.exe") },
            { new Installer("jgenesis", "jgenesis", "jgenesis-gui.exe") },
            { new Installer("jynx", "jynx", "Jynx-Windows-64bit.exe") },
            { new Installer("kega-fusion", "kega-fusion", "Fusion.exe") },
            { new Installer("kronos", "kronos", "kronos.exe") },
            { new Installer("libretro", "retroarch" ) },
            { new Installer("lime3ds", "lime3ds", "lime3ds.exe") },
            { new Installer("love") },
            { new Installer("m2emulator", "m2emulator", "emulator_multicpu.exe") },
            { new Installer("magicengine", "magicengine", "pce.exe") },
            { new Installer("mame64", new string[] { "mame", "mame64" }, new string[] { "mame.exe", "mame64.exe", "mame32.exe" }) },
            { new Installer("mandarine", "mandarine", "mandarine.exe") },
            { new Installer("mednafen", "mednafen") },
            { new Installer("melonds", "melonds", "melonDS.exe") },
            { new Installer("mesen") },
            { new Installer("mgba", "mgba") },
            { new Installer("model3", "supermodel") },
            { new Installer("mupen64", "mupen64", "RMG.exe") },
            { new Installer("nosgba", "nosgba", "no$gba.exe") },
            { new Installer("openbor") },
            { new Installer("opengoal", "opengoal", "gk.exe") },
            { new Installer("openjazz", "openjazz", "OpenJazz.exe") },
            { new Installer("openmsx", "openmsx", "openmsx.exe") },
            { new Installer("oricutron") },
            { new Installer("pcsx2", "pcsx2", "pcsx2-qt.exe") },
            { new Installer("pcsx2-16", "pcsx2-16", "pcsx2.exe") },
            { new Installer("pdark", "pdark", "pd.x86_64.exe") },
            { new Installer("phoenix", "phoenix", "PhoenixEmuProject.exe") },
            { new Installer("play", "play", "Play.exe") },
            { new Installer("ppsspp", "ppsspp", "PPSSPPWindows64.exe") },
            { new Installer("project64", "project64") },
            { new Installer("ps3", "rpcs3") },
            { new Installer("psxmame", "psxmame", "mame.exe") },
            { new Installer("raine") },
            { new Installer("raze", "raze", "raze.exe") },
            { new Installer("redream") },
            { new Installer("rpcs3") },
            { new Installer("ruffle", "ruffle", "ruffle.exe") },
            { new Installer("ryujinx", "ryujinx", "Ryujinx.exe") },
            { new Installer("scummvm") },
            { new Installer("shadps4", "shadps4", "shadPS4.exe") },
            { new Installer("simcoupe") },
            { new Installer("simple64", "simple64", "simple64-gui.exe") },
            { new Installer("singe2", "singe2", "Singe-v2.10-Windows-x86_64.exe") },
            { new Installer("snes9x", "snes9x", "snes9x-x64.exe") },
            { new Installer("soh", "soh", "soh.exe") },
            { new Installer("solarus", "solarus", "solarus-run.exe") },
            { new Installer("sonic3air", "sonic3air", "Sonic3AIR.exe") },
            { new Installer("sonicmania", "sonicmania", "RSDKv5U_x64.exe") },
            { new Installer("sonicretro", "sonicretro", "RSDKv4_64.exe") },
            { new Installer("sonicretrocd", "sonicretrocd", "RSDKv3_64.exe") },
            { new Installer("ssf", "ssf", "SSF.exe") },
            { new Installer("starship", "starship", "Starship.exe") },
            { new Installer("stella", "stella", "Stella.exe") },
            { new Installer("supermodel") },
            { new Installer("theforceengine", "theforceengine", "TheForceEngine.exe") },
            { new Installer("triforce", new string[] { "dolphin-triforce"}, new string[] { "dolphinWX.exe", "dolphin.exe" }) },
            { new Installer("tsugaru", "tsugaru", "tsugaru_cui.exe") },
            { new Installer("vita3k", "vita3k", "Vita3K.exe") },
            { new Installer("vpinball", new string[] {"vpinball" }, new string[] { "VPinballX.exe", "vpinballx.exe", "VPinballX64.exe" }) },
            { new Installer("winarcadia", "winarcadia", "WinArcadia.exe") },
            { new Installer("winuae", "winuae", "winuae64.exe") },
            { new Installer("xbox", new string[] { "cxbx-reloaded", "cxbx-r" }, "cxbx.exe") },
            { new Installer("xemu", "xemu") },
            { new Installer("xenia", "xenia", "xenia.exe") },
            { new Installer("xenia-canary", "xenia-canary", "xenia_canary.exe" ) },
            { new Installer("xenia-manager", "xenia-manager", "XeniaManager.DesktopApp.exe") },
            { new Installer("xm6pro", "xm6pro", "XM6.exe") },
            { new Installer("xroar", "xroar", "xroar.exe") },
            { new Installer("yabasanshiro", "yabasanshiro", "yabasanshiro.exe") },
            { new Installer("yuzu", "yuzu", "yuzu.exe") },
            { new Installer("zesarux", "zesarux", "zesarux.exe") },
            { new Installer("zinc", "zinc", "ZiNc.exe") } 
        };

        // Some emulators do not set correctly version in executable and require specific treatment !
        static readonly List<string>noVersionExe = new List<string> { "flycast", "rmg", "play", "eduke32", "mesen", "fbneo" };

        #region Properties
        public string Emulator { get; private set; }
        public string[] Folders { get; private set; }
        public string[] Executables { get; private set; }
        public string DefaultFolderName { get { return Folders[0]; } }
        public string ServerVersion { get; private set; }
        public string ServerFileName { get; set; }

        public string PackageUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(ServerFileName))
                    return GetUpdateUrl(ServerFileName);

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

            if (!SevenZipArchive.IsSevenZipAvailable)
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
                string shortExe = Path.GetFileNameWithoutExtension(exe).ToLower();
                if (string.IsNullOrEmpty(exe))
                    return null;

                var versionInfo = FileVersionInfo.GetVersionInfo(exe);

                string version = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
                if (version != "0.0.0.0" && !noVersionExe.Contains(shortExe))
                    return version;

                // Retroarch specific
                if (Path.GetFileNameWithoutExtension(exe).ToLower() == "retroarch")
                {
                    var output = ProcessExtensions.RunWithOutput(exe, "--version");

                    string ret = output.ExtractString("Version:", " ("); 
                    if (string.IsNullOrEmpty(ret))
                        ret = output.ExtractString(" -- v", " -- "); // Format before 1.16

                    output = StringExtensions.FormatVersionString(ret);

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
                else if (Path.GetFileNameWithoutExtension(exe).ToLower() == "flycast")
                {
                    var output = versionInfo.FileVersion.Substring(1);
                    Version ver = new Version();
                    int firstDashIndex = output.IndexOf('-');
                    if (firstDashIndex == -1 || output.IndexOf('-', firstDashIndex + 1) == -1)
                    {
                        output = StringExtensions.FormatVersionString(output);
                        
                        if (Version.TryParse(output, out ver))
                            return ver.ToString();
                    }

                    int secondDashIndex = output.IndexOf('-', firstDashIndex + 1);
                    output = output.Substring(0, secondDashIndex).Replace('-', '.');
                    string[] parts = output.Split('.');
                    if (parts.Length == 4)
                    {
                        if (Version.TryParse(output, out ver))
                            return ver.ToString();
                    }
                    else if (parts.Length == 3)
                    {
                        output = parts[0] + "." + parts[1] + ".0" + "." + parts[2];
                        if (Version.TryParse(output, out ver))
                            return ver.ToString();
                    }
                    else if (parts.Length == 2)
                    {
                        output = parts[0] + "." + parts[1] + ".0" + ".0";
                        if (Version.TryParse(output, out ver))
                            return ver.ToString();
                    }
                    else if (parts.Length == 1)
                    {
                        output = parts[0] + ".0" + ".0" + ".0";
                        if (Version.TryParse(output, out ver))
                            return ver.ToString();
                    }
                    else if (parts.Length > 4)
                    {
                        output = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];
                        if (Version.TryParse(output, out ver))
                            return ver.ToString();
                    }

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
                else if (Path.GetFileNameWithoutExtension(exe).ToLower() == "rmg")
                {
                    var output = ProcessExtensions.RunWithOutput(exe, "-v");
                    output = StringExtensions.FormatVersionString(output.ExtractString("Rosalie's Mupen GUI v", "\r"));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                /*else if (Path.GetFileNameWithoutExtension(exe).ToLower() == "play")
                {
                    var output = versionInfo.ProductVersion.Substring(0, 7);
                    output = StringExtensions.FormatVersionString(output);

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }*/
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

            if (!checkRootPath && folder == null)
            {
                var retroarchDefault = Program.SystemConfig.GetFullPath("retroarch");
                if (!string.IsNullOrEmpty(retroarchDefault))
                    folder = Path.Combine(Path.GetDirectoryName(retroarchDefault), DefaultFolderName);
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

                string cachedFile = Path.Combine(GetTempPath(), "versions.xml");

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

                var serverVersion = settings
                    .Descendants()
                    .Where(d => d.Name == "system" && d.Attribute("name") != null && d.Attribute("version") != null && d.Attribute("name").Value == DefaultFolderName)
                    .Select(d => new { Version = d.Attribute("version").Value, Path = d.Attribute("file") == null ? null : d.Attribute("file").Value })
                    .FirstOrDefault();

                if (serverVersion == null)
                    return false;

                Version local = new Version();
                Version server = new Version();
                if (Version.TryParse(GetInstalledVersion(), out local) && Version.TryParse(serverVersion.Version, out server))
                {
                    if (local < server)
                    {
                        ServerFileName = serverVersion.Path;
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

        public static string GetTempPath()
        {
            string ret = Path.Combine(Path.GetTempPath(), "emulationstation.tmp");
            if (!Directory.Exists(ret))
                Directory.CreateDirectory(ret);

            return ret;
        }

        public static void DownloadAndInstall(string url, string installFolder, ProgressChangedEventHandler progress = null)
        {
            string localFile = Path.GetFileName(url);
            string fn = Path.Combine(GetTempPath(), localFile);

            try { if (File.Exists(fn)) File.Delete(fn); }
            catch { }

            try
            {
                using (FileStream fileStream = new FileStream(fn, FileMode.Create))
                    WebTools.DownloadToStream(fileStream, url, progress);

                if (progress != null)
                    progress(null, new ProgressChangedEventArgs(100, null));

                Zip.Extract(fn, installFolder);                
            }
            finally
            {
                try { if (File.Exists(fn)) File.Delete(fn); }
                catch { }
            }
        }

        public void DownloadAndInstall(ProgressChangedEventHandler progress = null)
        {
            DownloadAndInstall(PackageUrl, GetInstallFolder(), progress);           
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

                try
                {
                    installer.DownloadAndInstall();
                    sys.Add(installer.DefaultFolderName);
                }
                catch(Exception ex) 
                {
                    Console.WriteLine("failed " + ex.Message);
                }
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
