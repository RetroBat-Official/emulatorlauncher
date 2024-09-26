using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class Eka2l1Generator : Generator
    {
        public Eka2l1Generator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _gameUUID;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("eka2l1");

            string exe = Path.Combine(path, "eka2l1_qt.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            List<string> args = new List<string>();
            
            if (fullscreen)
                args.Add("--fullscreen");

            string device = "NEM-4";
            if (SystemConfig.isOptSet("eka2l1_device") && !string.IsNullOrEmpty(SystemConfig["eka2l1_device"]))
                device = SystemConfig["eka2l1_device"];

            args.Add("--device");
            args.Add(device);

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

                _gameUUID = uuid;

            }

            else if (Path.GetExtension(rom) == ".symbian")
            {
                string[] lines = File.ReadAllLines(rom);
                string appName;

                if (lines.Length > 0 && !string.IsNullOrEmpty(lines[0]))
                    appName = lines[0];
                else
                    appName = Path.GetFileNameWithoutExtension(rom);

                args.Add("--run");
                args.Add(appName);
            }

            /*else if (Path.GetExtension(rom).ToLowerInvariant() == ".n-gage")
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
            }*/

            SetupConfiguration(path);
            SetupGame(path);
            SetupControllers(path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                WindowStyle = ProcessWindowStyle.Minimized,                
                Arguments = string.Join(" ", args.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray())
            };
        }

        private void SetupConfiguration(string path)
        {
            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));

            string dataFolder = Path.Combine(AppConfig.GetFullPath("bios"), "eka2l1", "data").Replace("\\", "/");

            // Handle Core part of yml file
            yml["data-storage"] = dataFolder;

            BindBoolFeature(yml, "enable-nearest-neighbor-filter", "eka2l1_nearest_neighbor_filter", "true", "false");
            BindFeature(yml, "integer-scaling", "smooth", "false");

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                yml["current-keybind-profile"] = "default";
            else
                yml["current-keybind-profile"] = "retrobat";

            // Save to yml file
            yml.Save();
        }

        private void SetupGame(string path)
        {
            if (_gameUUID == null)
                return;

            if (!Directory.Exists(Path.Combine(path, "compat")))
                try { Directory.CreateDirectory(Path.Combine(path, "compat")); }
                catch {}
            
            var ymlFile = Path.Combine(path, "compat", _gameUUID + ".yml");
            YmlFile yml;
            if (File.Exists(ymlFile))
                yml = YmlFile.Load(ymlFile);
            else
                yml = new YmlFile();
            
            yml["fps"] = "60";
            yml["time-delay"] = "0";
            yml["should-child-inherit-setting"] = "true";

            if (SystemConfig.isOptSet("eka2l1_rotate") && !string.IsNullOrEmpty(SystemConfig["eka2l1_rotate"]))
                yml["screen-rotation"] = SystemConfig["eka2l1_rotate"];
            else
                yml["screen-rotation"] = "0";

            if (SystemConfig.isOptSet("eka2l1_upscale") && !string.IsNullOrEmpty(SystemConfig["eka2l1_upscale"]))
                yml["screen-upscale"] = SystemConfig["eka2l1_upscale"];
            else
                yml["screen-upscale"] = "4";

            if (SystemConfig.isOptSet("eka2l1_upscale_filter") && !string.IsNullOrEmpty(SystemConfig["eka2l1_upscale_filter"]))
            {
                yml["screen-upscale-method"] = "1";
                yml["filter-shader-path"] = SystemConfig["eka2l1_upscale_filter"];
            }
            else
            {
                yml["screen-upscale-method"] = "0";
                yml["filter-shader-path"] = "Default";
            }

            yml["t9-bypass-hack"] = "true";
            
            // Save to yml file
            yml.Save(ymlFile);
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
