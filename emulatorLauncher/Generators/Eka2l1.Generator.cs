using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    class Eka2l1Generator : Generator
    {
        public Eka2l1Generator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("eka2l1");

            string exe = Path.Combine(path, "eka2l1_qt.exe");
            if (!File.Exists(exe))
                return null;

            List<string> args = new List<string>();
            args.Add("--fullscreen");

            if (Directory.Exists(rom))
            {
                string aif = Directory.EnumerateFiles(rom, "*.aif", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrEmpty(aif))
                    return null;

                var uuid = ExtractUUID(aif);

                args.Add("--device");
                args.Add("NEM-4");

                args.Add("--mount");
                args.Add(rom);     
               
                args.Add("--run");
                args.Add("0x"+uuid);

            }
            else if (Path.GetExtension(rom).ToLowerInvariant() == ".n-gage")
            {                    
                string ngage2registry = Path.Combine(path, "Data", "drives", "c", "sys", "install", "sisregistry", "2000a2ae");

                 // Check if game is installed in EKA2L1 registry, if not we install the .sis
                string ngage2installdir = Path.Combine(path, "Data", "drives", "e", "n-gage");
                string ngage2finalfileinstall = Path.Combine(path, "Data", "drives", "e", "n-gage", Path.GetFileName(rom));

                if (!Directory.Exists(ngage2registry))
                {
                    string bios = Path.Combine(AppConfig.GetFullPath("bios"), "N-Gage Installer.sis");
                    if (File.Exists(bios))
                    {
                        Process.Start(new ProcessStartInfo()
                        {
                            FileName = exe,
                            WorkingDirectory = path,
                            Arguments = "--install \"" + bios + "\""
                        }).WaitForExit();
                    }
                }
    
                var uuid = ExtractUUID(rom);

                // Game Installed Dir
                string installedgamedir = Path.Combine(path, "Data", "drives", "c", "private", uuid);
                if (!File.Exists(installedgamedir) && !File.Exists(ngage2finalfileinstall))
                {
                    try { Directory.CreateDirectory(Path.GetDirectoryName(ngage2finalfileinstall)); }
                    catch {}
                    File.Copy(rom, ngage2finalfileinstall);
                }
                    
                args.Add("--device");
                args.Add("RM-409");

                args.Add("--run");
                args.Add("0x20007b39");
            }
            else if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                var entries = Zip.ListEntries(rom).Where(e => !e.IsDirectory).Select(e => e.Filename).ToArray(); 
                string aif = entries.Where(e => Path.GetExtension(e).ToLowerInvariant() == ".aif").FirstOrDefault();

                string dest = Path.Combine(Path.GetTempPath(), Path.GetFileName(aif));

                Zip.Extract(rom, Path.GetTempPath(), aif);
                if (!File.Exists(dest))
                    return null;

                args.Add("--device");
                args.Add("NEM-4");

                args.Add("--mount");
                args.Add(rom);

                var uuid = ExtractUUID(dest);
                args.Add("--run");
                args.Add("0x" + uuid);
                File.Delete(dest);
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                WindowStyle = ProcessWindowStyle.Minimized,                
                Arguments = string.Join(" ", args.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray())
            };
        }

        private static string ExtractUUID(string file)
        {
            if (!File.Exists(file))
                return null;

            int startOffset = 16;
            if (Path.GetExtension(file).ToLowerInvariant() == ".n-gage")
                startOffset = 1264;

            var bytes = BitConverter.ToString(File.ReadAllBytes(file)).Replace("-", string.Empty);
            if (bytes.Length < startOffset + 8)
                return null;

            var data = bytes.Substring(startOffset, 8);
            var part1 = data.Substring(0, 2);
            var part2 = data.Substring(2, 2);
            var part3 = data.Substring(4, 2);
            var part4 = data.Substring(6, 2);

            return part4 + part3 + part2 + part1;            
        }

    }
}
