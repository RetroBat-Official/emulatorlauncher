using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;

namespace emulatorLauncher
{
    class Installer
    {
        public string GetPackageUrl()
        {
            string installerUrl = Program.AppConfig["installers"];
            if (string.IsNullOrEmpty(installerUrl))
                return string.Empty;

            return installerUrl
                .Replace("%UPDATETYPE%", UpdateType())
                .Replace("%FOLDERNAME%", FolderName);
        }

        static Dictionary<string, Installer> installers = new Dictionary<string, Installer>
        {            
            { "libretro", new Installer("retroarch" ) }, { "angle", new Installer("retroarch" ) }, // "libretro_cores.7z",
            { "duckstation", new Installer("duckstation", "duckstation-nogui-x64-ReleaseLTCG.exe") },  
            { "kega-fusion", new Installer("kega-fusion", "Fusion.exe") }, 
            { "mesen", new Installer("mesen") }, 
            { "model3", new Installer("supermodel") }, { "supermodel", new Installer("supermodel") }, 
            { "ps3", new Installer("rpcs3") }, { "rpcs3", new Installer("rpcs3") }, 
            { "ps2", new Installer("pcsx2") }, { "pcsx2", new Installer("pcsx2") }, 
            { "fpinball", new Installer("fpinball") }, { "bam", new Installer("fpinball") }, 
            { "cemu", new Installer("cemu") }, { "wiiu", new Installer("cemu") },
            { "applewin", new Installer("applewin") }, { "apple2", new Installer("applewin") },
            { "gsplus", new Installer("gsplus") }, { "apple2gs", new Installer("gsplus") },             
            { "cxbx", new Installer("cxbx-reloaded", "cxbx.exe") }, { "chihiro", new Installer("cxbx-reloaded", "cxbx.exe") }, { "xbox", new Installer("cxbx-reloaded", "cxbx.exe") },
            { "arcadeflashweb", new Installer("arcadeflashweb") },           
            { "citra", new Installer("citra") },            
            { "daphne", new Installer("citra") },
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
            { "ppsspp", new Installer("ppsspp") }, 
            { "project64", new Installer("project64") }, 
            { "raine", new Installer("raine") }, 
            { "redream", new Installer("redream") },             
            { "simcoupe", new Installer("simcoupe") }, 
            { "snes9x", new Installer("snes9x", "snes9x-x64.exe") }, 
            { "solarus", new Installer("solarus", "solarus-run.exe") },             
            { "tsugaru", new Installer("tsugaru", "tsugaru_cui.exe") }, 
            { "vpinball", new Installer("vpinball") }, 
            { "winuae", new Installer("winuae", "winuae64.exe") }, 
            { "xemu", new Installer("xemu") }, 
            { "xenia-canary", new Installer("xenia-canary", "xenia_canary.exe" ) }
        };

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

        public string GetInstallFolder()
        {
            string folder = Program.AppConfig.GetFullPath(FolderName);
            if (string.IsNullOrEmpty(folder))
            {
                foreach (var inst in installers)
                {
                    // Find another emulator folder - retroarch should always be there
                    string curr = Program.AppConfig.GetFullPath(inst.Value.FolderName);
                    if (!string.IsNullOrEmpty(curr) && Directory.Exists(curr))
                        return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(curr)), FolderName);
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
            try
            {
                var req = WebRequest.Create(GetPackageUrl());

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

                    var px = new ProcessStartInfo()
                    {
                        FileName = GetSevenZipPath(),
                        WorkingDirectory = Path.GetDirectoryName(GetSevenZipPath()),
                        Arguments = "x \"" + fn + "\" -y -o\"" + GetInstallFolder() + "\"",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(px).WaitForExit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error(ex.Message);
            }

            return false;
        }
    }

}
