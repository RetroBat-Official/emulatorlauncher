using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
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

            args.Add("--device");
            args.Add("NEM-4");

            if (Directory.Exists(rom))
            {
                string aif = Directory.EnumerateFiles(rom, "*.aif", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrEmpty(aif))
                    return null;

                var uuid = ExtractUUID(aif);

                args.Add("--mount");
                args.Add(rom);     
               
                args.Add("--run");
                args.Add("0x"+uuid);

            }
            else if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                var entries = Zip.ListEntries(rom);                
                string aif = entries.Where(e => Path.GetExtension(e).ToLowerInvariant() == ".aif").FirstOrDefault();

                string dest = Path.Combine(Path.GetTempPath(), Path.GetFileName(aif));

                Zip.Extract(rom, Path.GetTempPath(), aif);
                if (!File.Exists(dest))
                    return null;

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

        private static string ExtractUUID(string aif)
        {
            if (!File.Exists(aif))
                return null;

            var bytes = BitConverter.ToString(File.ReadAllBytes(aif)).Replace("-", string.Empty);
            if (bytes.Length < 24)
                return null;

            var data = bytes.Substring(16, 8);
            var part1 = data.Substring(0, 2);
            var part2 = data.Substring(2, 2);
            var part3 = data.Substring(4, 2);
            var part4 = data.Substring(6, 2);

            return part4 + part3 + part2 + part1;            
        }

    }
}
