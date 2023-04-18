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

            // Machine
            commandArray.Add("-machine");
            if (SystemConfig.isOptSet("msx_machine") && !string.IsNullOrEmpty(SystemConfig["msx_machine"]))
                commandArray.Add(SystemConfig["msx_machine"]);
            else
                commandArray.Add("Panasonic_FS-A1GT");

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

            // Define type of rom to add argument
            var romExtension = Path.GetExtension(rom);
            string romtype = msxMedias.FirstOrDefault(x => x.Value.Contains(romExtension)).Key;
            if (SystemConfig.isOptSet("altromtype") && !string.IsNullOrEmpty(SystemConfig["altromtype"]))
                romtype = "-" + SystemConfig["altromtype"];

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

            // Save xml file
            xdoc.Save(settingsFile);
        }
    }
}
