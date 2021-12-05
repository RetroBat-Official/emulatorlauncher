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

namespace emulatorLauncher
{
    class Installer
    {
        static Dictionary<string, Installer> installers = new Dictionary<string, Installer>
        {            
            { "arcadeflashweb", new Installer("arcadeflashweb") },           
            { "libretro", new Installer("retroarch" ) }, { "angle", new Installer("retroarch" ) }, // "libretro_cores.7z",
            { "duckstation", new Installer("duckstation", "duckstation-nogui-x64-ReleaseLTCG.exe") },  
            { "kega-fusion", new Installer("kega-fusion", "Fusion.exe") }, 
            { "mesen", new Installer("mesen") }, 
            { "model3", new Installer("supermodel") }, { "supermodel", new Installer("supermodel") }, 
            { "ps3", new Installer("rpcs3") }, { "rpcs3", new Installer("rpcs3") }, 
            { "ps2", new Installer("pcsx2") }, { "pcsx2", new Installer("pcsx2") }, 
            { "fpinball", new Installer("fpinball", "Future Pinball.exe") }, { "bam", new Installer("fpinball", "BAM\\FPLoader.exe") }, 
            { "cemu", new Installer("cemu") }, { "wiiu", new Installer("cemu") },
            { "applewin", new Installer("applewin") }, { "apple2", new Installer("applewin") },
            { "gsplus", new Installer("gsplus") }, { "apple2gs", new Installer("gsplus") },             
            { "cxbx", new Installer("cxbx-reloaded", "cxbx.exe") }, { "chihiro", new Installer("cxbx-reloaded", "cxbx.exe") }, { "xbox", new Installer("cxbx-reloaded", "cxbx.exe") },
            { "citra", new Installer("citra") },            
            { "daphne", new Installer("daphne") },
            { "demul-old", new Installer("demul-old", "demul.exe") }, 
            { "demul", new Installer("demul") }, 
            { "dolphin", new Installer("dolphin-emu", "dolphin.exe") }, 
            { "triforce", new Installer("dolphin-triforce", "dolphinWX.exe") },  
            { "dosbox", new Installer("dosbox") },                      
            { "love", new Installer("love") }, 
            { "m2emulator", new Installer("m2emulator", "emulator.exe") },
            { "mednafen", new Installer("mednafen") },        
            { "mgba", new Installer("mgba") }, 
            { "openbor", new Installer("openbor") }, 
            { "oricutron", new Installer("oricutron") },             
            { "ppsspp", new Installer("ppsspp", "PPSSPPWindows64.exe") }, 
            { "project64", new Installer("project64") }, 
            { "raine", new Installer("raine") },             
            { "mame64", new Installer("mame", "mame.exe") },
            { "ryujinx", new Installer("ryujinx", "ryujinx.exe") },            
            { "redream", new Installer("redream") },             
            { "simcoupe", new Installer("simcoupe") }, 
            { "snes9x", new Installer("snes9x", "snes9x-x64.exe") }, 
            { "solarus", new Installer("solarus", "solarus-run.exe") },             
            { "tsugaru", new Installer("tsugaru", "tsugaru_cui.exe") }, 
            { "vpinball", new Installer("vpinball", "vpinballx.exe") }, 
            { "winuae", new Installer("winuae", "winuae64.exe") }, 
            { "xemu", new Installer("xemu") }, 
            { "xenia-canary", new Installer("xenia-canary", "xenia_canary.exe" ) }
        };

        public string GetPackageUrl()
        {
            string installerUrl = Program.AppConfig["installers"];
            if (string.IsNullOrEmpty(installerUrl))
                return string.Empty;

            return installerUrl
                .Replace("%UPDATETYPE%", UpdateType())
                .Replace("%FOLDERNAME%", FolderName);
        }

        private string RunWithOutput(ProcessStartInfo ps)
        {
            List<string> lines = new List<string>();

            ps.UseShellExecute = false;
            ps.RedirectStandardOutput = true;
            ps.RedirectStandardError = true;
            ps.CreateNoWindow = true;

            var proc = new Process();
            proc.StartInfo = ps;
            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return (err ?? "") + (output ?? "");
        }

        private string FormatVersion(string version)
        {
            var numbers = version.Split(new char[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (numbers.Count < 4)
                numbers.Add("0");

            return string.Join(".", numbers.Take(4).ToArray());
        }

        public string GetInstalledVersion()
        {
            try
            {
                string exe = Path.Combine(GetInstallFolder(), LocalExeName);

                var versionInfo = FileVersionInfo.GetVersionInfo(exe);

                string version = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
                if (version != "0.0.0.0")
                    return version;

                // Retroarch specific
                if (Path.GetFileNameWithoutExtension(LocalExeName).ToLower() == "retroarch")
                {
                    var output = RunWithOutput(new ProcessStartInfo() { Arguments = "--version", FileName = exe });
                    output = FormatVersion(output.ExtractString(" -- v", " -- "));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else if (Path.GetFileNameWithoutExtension(LocalExeName).ToLower() == "demul")
                {
                    var output = RunWithOutput(new ProcessStartInfo() { Arguments = "--help", FileName = exe });
                    output = FormatVersion(output.ExtractString(") v", "\r"));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else if (Path.GetFileNameWithoutExtension(LocalExeName).ToLower() == "dolphin")
                {
                    var output = RunWithOutput(new ProcessStartInfo() { Arguments = "--version", FileName = exe });
                    output = FormatVersion(output.ExtractString("Dolphin ", "\r"));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else if (Path.GetFileNameWithoutExtension(LocalExeName).ToLower() == "gsplus")
                {
                    var output = RunWithOutput(new ProcessStartInfo() { Arguments = "--help", FileName = exe });
                    output = FormatVersion(output.ExtractString("GSplus v", " "));

                    Version ver = new Version();
                    if (Version.TryParse(output, out ver))
                        return ver.ToString();
                }
                else
                {
                    // Fake version number based on last write time
                    var date = File.GetLastWriteTime(exe).ToString("0.yy.MM.dd");
                    return date;
                }
                
               
            }
            catch { }

            return null;
        }

        public static void CollectVersions()
        {
            List<systeminfo> sys = new List<systeminfo>();

            foreach (var inst in installers)
            {
                if (sys.Any(s => s.name == inst.Value.FolderName))
                    continue;

                sys.Add(new systeminfo()
                {
                    name = inst.Value.FolderName,
                    version = inst.Value.GetInstalledVersion()
                });
            }

            var xml = sys
                .OrderBy(s => s.name)
                .ToArray().ToXml().Replace("ArrayOfSystem>", "systems>");

            string fn = Path.Combine(Path.GetTempPath(), "systems.xml");
            File.WriteAllText(fn, xml);
            Process.Start(fn);
        }

        public Installer(string zipName, string exe = null)
        {
            FolderName = zipName;
            LocalExeName = (exe == null ? zipName + ".exe" : exe);
        }

        public string FolderName { get; set; }
        public string LocalExeName { get; set; }

        public static Installer FindInstaller()
        {
            Installer installer = installers.Where(g => g.Key == Program.SystemConfig["emulator"]).Select(g => g.Value).FirstOrDefault();
            if (installer == null && !string.IsNullOrEmpty(Program.SystemConfig["emulator"]) && Program.SystemConfig["emulator"].StartsWith("lr-"))
                installer = installers.Where(g => g.Key == "libretro").Select(g => g.Value).FirstOrDefault();
            if (installer == null)
                installer = installers.Where(g => g.Key == Program.SystemConfig["system"]).Select(g => g.Value).FirstOrDefault();

            return installer;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        public static void InstallAllAndCollect(string customFolder)
        {
            _customInstallFolder = customFolder;

            try { Directory.CreateDirectory(_customInstallFolder); }
            catch { }

            HashSet<string> sys = new HashSet<string>();

            AllocConsole();

            foreach (var installer in installers.Values)
            {
                if (sys.Contains(installer.FolderName))
                    continue;

                Console.WriteLine(installer.FolderName);
                installer.DownloadAndInstall();
                sys.Add(installer.FolderName);
            }
           
            FreeConsole();

            CollectVersions();

            try { Directory.Delete(_customInstallFolder, true); }
            catch { }

            _customInstallFolder = null;
        }

        private static string _customInstallFolder;

        public string GetInstallFolder()
        {
            if (!string.IsNullOrEmpty(_customInstallFolder))
                return Path.Combine(_customInstallFolder, FolderName);

            string folder = Program.AppConfig.GetFullPath(FolderName);
            if (string.IsNullOrEmpty(folder))
            {
                foreach (var inst in installers)
                {
                    // Find another emulator folder - retroarch should always be there
                    string curr = Program.AppConfig.GetFullPath(inst.Value.FolderName);
                    if (!string.IsNullOrEmpty(curr))
                    {
                        if (curr.EndsWith("\\"))
                            curr = curr.Substring(0, curr.Length-1);

                        if (Directory.Exists(curr))
                        return Path.Combine(Path.GetDirectoryName(curr), FolderName);
                    }
                }
            }

            return folder;
        }

        public bool IsInstalled()
        {
            string folder = GetInstallFolder();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return false;

            string exe = Path.Combine(folder, LocalExeName);
            if (!File.Exists(exe))
                return false;

            return true;
        }

        public string GetLocalFilename()
        {
            return Path.Combine(Path.GetTempPath(), FolderName + ".7z");
        }

        public static string UpdateType()
        {
            string ret = Program.SystemConfig["updates.type"];
            if (string.IsNullOrEmpty(ret))
                return "stable";

            return ret;
        }

        public string GetSevenZipPath()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "7za.exe");
        }

        public bool CanInstall()
        {
            if (!File.Exists(GetSevenZipPath()))
                return false;

            if (string.IsNullOrEmpty(GetPackageUrl()))
                return false;

            try
            {
                var req = WebRequest.Create(GetPackageUrl());
                req.Method = "HEAD";

                var resp = req.GetResponse() as HttpWebResponse;
                return resp.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error(ex.Message);
            }

            return false;
        }

        public static void ReadResponseStream(WebResponse response, Stream destinationStream, ProgressChangedEventHandler progress = null)
        {
            if (destinationStream == null)
                throw new ArgumentException("Stream null");

            long length = (int)response.ContentLength;
            long pos = 0;

            using (Stream sr = response.GetResponseStream())
            {
                byte[] buffer = new byte[1024];
                int bytes = 0;

                while ((bytes = sr.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destinationStream.Write(buffer, 0, bytes);

                    pos += bytes;

                    if (progress != null && length > 0)
                        progress(null, new ProgressChangedEventArgs((int)((pos * 100) / length), null));
                }

                sr.Close();
            }

            response.Close();

            if (length > 0 && pos != length)
                throw new Exception("Incomplete download : " + length);
        }

        public bool DownloadAndInstall(ProgressChangedEventHandler progress = null)
        {
        retry:
            try
            {
               
                var req = WebRequest.Create(GetPackageUrl());
                ((HttpWebRequest)req).UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows Phone OS 7.5; Trident/5.0; IEMobile/9.0)";

                var resp = req.GetResponse() as HttpWebResponse;
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    string fn = GetLocalFilename();

                    try { if (File.Exists(fn)) File.Delete(fn); }
                    catch { }

                    using (FileStream fileStream = new FileStream(fn, FileMode.Create))
                    {
                        ReadResponseStream(resp, fileStream, progress);
                    }

                    if (progress != null)
                        progress(null, new ProgressChangedEventArgs(100, null));

                    var px = new ProcessStartInfo()
                    {
                        FileName = GetSevenZipPath(),
                        WorkingDirectory = Path.GetDirectoryName(GetSevenZipPath()),
                        Arguments = "x \"" + fn + "\" -y -o\"" + GetInstallFolder() + "\"",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(px).WaitForExit();

                    try { if (File.Exists(fn)) File.Delete(fn); }
                    catch { }

                    return true;
                }
            }
            catch (WebException ex)
            {
                if ((ex.Response as HttpWebResponse).StatusCode == (HttpStatusCode)429)
                {
                    Console.WriteLine("429 - " + GetPackageUrl() + " : Retrying");
                    System.Threading.Thread.Sleep(30000);
                    goto retry;
                }
                SimpleLogger.Instance.Error(ex.Message);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error(ex.Message);
            }

            return false;
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
