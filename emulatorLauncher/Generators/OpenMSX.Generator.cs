using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;

namespace emulatorLauncher
{
    class openMSXGenerator : Generator
    {

        static Dictionary<string, string[]> msxMedias = new Dictionary<string, string[]>()
        {
            { "-cart", new string[] { ".mx1", ".mx2", ".ri", ".rom" } },
            { "-diska", new string[] { ".di1", ".di2", ".dmk", ".dsk", ".fd1", ".fd2", ".xsa" } },
            { "-laserdisc", new string[] { ".ogv" } },
            { "-cassetteplayer", new string[] { ".cas", ".wav" } }
        };

        static List<string> machineWithDiskDrive = new List<string>() { "Panasonic_FS-A1GT", "Panasonic_FS-A1WSX", "National_FS-5500F2", "Philips_NMS_8245", "National_CF-3300" };
        static List<string> machineWithCassette = new List<string>() { "Panasonic_FS-A1WSX", "National_FS-5500F2", "Pioneer_PX-7", "Philips_NMS_8245", "National_CF-3300" };
        static List<string> machineWithLaserdisc = new List<string>() { "Pioneer_PX-7" };

        static string defaultDiskMachine = "Panasonic_FS-A1GT";
        static string defaultMachineCassette = "Panasonic_FS-A1WSX";
        static string defaultLaserdiscMachine = "Pioneer_PX-7";

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            bool result = false;
            string environmentVariable;
            environmentVariable = Environment.GetEnvironmentVariable("OPENMSX_HOME", EnvironmentVariableTarget.User);
            string path = AppConfig.GetFullPath("openmsx");
            if (!Directory.Exists(path))
                return null;
            
            string sharePath = Path.Combine(AppConfig.GetFullPath("bios"), "openmsx");
            if (!Directory.Exists(sharePath)) try { Directory.CreateDirectory(sharePath); }
                catch { }

            if (string.IsNullOrEmpty(environmentVariable))
                Environment.SetEnvironmentVariable("OPENMSX_HOME", sharePath, EnvironmentVariableTarget.User);
            else
            {
                result = environmentVariable.Equals(sharePath);
                if (!result)
                    Environment.SetEnvironmentVariable("OPENMSX_HOME", sharePath, EnvironmentVariableTarget.User);
            }

            string exe = Path.Combine(path, "openmsx.exe");
            if (!File.Exists(exe))
                return null;

            // Setup xml file
            SetupConfiguration(sharePath, rom);

            //setting up command line parameters
            var commandArray = new List<string>();

            // Define type of rom
            var romExtension = Path.GetExtension(rom);
            string romtype = msxMedias.FirstOrDefault(x => x.Value.Contains(romExtension)).Key;
            if (SystemConfig.isOptSet("altromtype") && !string.IsNullOrEmpty(SystemConfig["altromtype"]))
                romtype = "-" + SystemConfig["altromtype"];

            // Define Machine
            string machine = "Panasonic_FS-A1GT";
            commandArray.Add("-machine");
            if (SystemConfig.isOptSet("msx_machine") && !string.IsNullOrEmpty(SystemConfig["msx_machine"]))
                machine = SystemConfig["msx_machine"];

            if (romtype != null)
            {
                switch (romtype)
                {
                    case "-cart":
                        break;
                    case "-diska":
                        if (machineWithDiskDrive.Contains(machine))
                            break;
                        else
                        { 
                            machine = defaultDiskMachine;
                            break;
                        }
                    case "-laserdisc":
                        if (machineWithLaserdisc.Contains(machine))
                            break;
                        else
                        {
                            machine = defaultLaserdiscMachine;
                            break;
                        }
                    case "-cassetteplayer":
                        if (machineWithCassette.Contains(machine))
                            break;
                        else
                        {
                            machine = defaultDiskMachine;
                            break;
                        }
                }
            }
                
            commandArray.Add(machine);

            // I/O slot Extensions .Where(c => c.Name != null && c.Name.StartWith(...))
            List<string> extensionlist = SystemConfig
                .Where(c => c.Name != null && c.Name.StartsWith(system + ".ext_") && c.Value == "1")
                .Select(c => c.Name.Replace(system + ".ext_", ""))
                .ToList();

            for (int i = 0; i < extensionlist.Count; i++)
            {
                commandArray.Add("-ext");
                commandArray.Add(extensionlist[i]);
            }

            // Cartridge slot extension (MSX machines only have 2 cartridge slots, one is taken by the game so it leaves place for one extension
            if (SystemConfig.isOptSet("cart_extension") && !string.IsNullOrEmpty(SystemConfig["cart_extension"]))
            {
                if (SystemConfig.isOptSet("altromtype") && SystemConfig["altromtype"] == "carta")
                    commandArray.Add("-extb");
                else
                    commandArray.Add("-ext");
                
                commandArray.Add(SystemConfig["cart_extension"]);
            }

            // Run scripts
            string scriptspath = Path.Combine(path, "share", "scripts");
            if (romtype != null && romtype == "-cassetteplayer")
            {
                commandArray.Add("-script");
                commandArray.Add(Path.Combine(scriptspath, "autoruncassettes.tcl"));
            }
            else if (romtype != null && romtype == "-laserdisc")
            {
                commandArray.Add("-script");
                commandArray.Add(Path.Combine(scriptspath, "autorunlaserdisc.tcl"));
            }

            // Add media type
            if (romtype != null)
               commandArray.Add(romtype);

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args + " \"" + rom + "\"",
            };
        }

        /// <summary>
        /// Configure emulator features (settings.xml)
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfiguration(string path, string rom)
        {
            string settingsFile = Path.Combine(path, "share", "settings.xml");

            var xdoc = File.Exists(settingsFile) ? XDocument.Load(settingsFile) : new XDocument();
            var topnode = xdoc.GetOrCreateElement("settings");
            var settings = topnode.GetOrCreateElement("settings");

            // Vsync
            bool vsyncoff = SystemConfig.getOptBoolean("msx_vsync");

            XElement vsync = xdoc.Descendants()
            .Where(x => (string)x.Attribute("id") == "vsync").FirstOrDefault();

            if (vsync == null)
            {
                vsync = new XElement("setting", new XAttribute("id", "vsync"));
                vsync.SetValue(vsyncoff ? "false" : "true");
                settings.Add(vsync);
            }
            else
                vsync.SetValue(vsyncoff ? "false" : "true");

            // Fullscreen
            bool disablefs = SystemConfig.getOptBoolean("msx_fullscreen");
            
            XElement fullscreen = xdoc.Descendants()
            .Where(x => (string)x.Attribute("id") == "fullscreen").FirstOrDefault();
            
            if (fullscreen == null)
            {
                fullscreen = new XElement("setting", new XAttribute("id", "fullscreen"));
                fullscreen.SetValue(disablefs ? "false" : "true");
                settings.Add(fullscreen);
            }
            else
                fullscreen.SetValue(disablefs ? "false" : "true");

            // Scale factor
            string scale = "2";
            if (SystemConfig.isOptSet("msx_scale") && !string.IsNullOrEmpty(SystemConfig["msx_scale"]))
                scale = SystemConfig["msx_scale"];

            XElement scalefactor = xdoc.Descendants()
            .Where(x => (string)x.Attribute("id") == "scale_factor").FirstOrDefault();

            if (scalefactor == null)
            {
                scalefactor = new XElement("setting", new XAttribute("id", "scale_factor"));
                scalefactor.SetValue(scale);
                settings.Add(scalefactor);
            }
            else
                scalefactor.SetValue(scale);

            // Scale algorythm
            string algorythm = "simple";
            if (SystemConfig.isOptSet("msx_scale_algo") && !string.IsNullOrEmpty(SystemConfig["msx_scale_algo"]))
                algorythm = SystemConfig["msx_scale_algo"];

            XElement scalealgo = xdoc.Descendants()
            .Where(x => (string)x.Attribute("id") == "scale_algorithm").FirstOrDefault();

            if (scalealgo == null)
            {
                scalealgo = new XElement("setting", new XAttribute("id", "scale_algorithm"));
                scalealgo.SetValue(algorythm);
                settings.Add(scalealgo);
            }
            else
                scalealgo.SetValue(algorythm);

            // Audio Resampler
            string audioresampler = "blip";
            if (SystemConfig.isOptSet("msx_resampler") && !string.IsNullOrEmpty(SystemConfig["msx_resampler"]))
                audioresampler = SystemConfig["msx_resampler"];

            XElement resampler = xdoc.Descendants()
            .Where(x => (string)x.Attribute("id") == "resampler").FirstOrDefault();

            if (resampler == null)
            {
                resampler = new XElement("setting", new XAttribute("id", "resampler"));
                resampler.SetValue(audioresampler);
                settings.Add(resampler);
            }
            else
                resampler.SetValue(audioresampler);

            // OSD icon set
            string iconset = "set1";
            if (SystemConfig.isOptSet("msx_osdset") && !string.IsNullOrEmpty(SystemConfig["msx_osdset"]))
                iconset = SystemConfig["msx_osdset"];

            XElement osdiconset = xdoc.Descendants()
            .Where(x => (string)x.Attribute("id") == "osd_leds_set").FirstOrDefault();

            if (osdiconset == null)
            {
                osdiconset = new XElement("setting", new XAttribute("id", "osd_leds_set"));
                osdiconset.SetValue(iconset);
                settings.Add(osdiconset);
            }
            else
                osdiconset.SetValue(iconset);

            // Save xml file
            xdoc.Save(settingsFile);
        }
    }
}
