using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Threading;

namespace EmulatorLauncher
{
    partial class OpenMSXGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {

            string path = AppConfig.GetFullPath("openmsx");

            // Bezels with reshade
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            bool result = false;
            string environmentVariable;
            environmentVariable = Environment.GetEnvironmentVariable("OPENMSX_HOME", EnvironmentVariableTarget.User);
            
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
            if (SystemConfig.isOptSet("altromtype") && !string.IsNullOrEmpty(SystemConfig["altromtype"]) && system != "colecovision")
                romtype = "-" + SystemConfig["altromtype"];

            if (system == "colecovision")
                romtype = "-cart";

            // Define Machine
            string machine = "C-BIOS_MSX2";

            if (SystemConfig.isOptSet("msx_machine") && !string.IsNullOrEmpty(SystemConfig["msx_machine"]))
                machine = SystemConfig["msx_machine"];

            else if (system == "msx1")
                machine = "Philips_VG_8020";
            else if (system == "msx2")
                machine = "Philips_NMS_8245";
            else if (system == "msx2+")
                machine = "Panasonic_FS-A1WSX";
            else if (system == "msxturbor")
                machine = "Panasonic_FS-A1GT";

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
                            machine = defaultMachineCassette;
                            break;
                        }
                }
            }

            if (system == "colecovision")
                machine = "ColecoVision_SGM";

            if (system == "spectravideo")
                machine = "Spectravideo_SVI-738_SE";

            // Perform check of BIOS files for the machine
            string systemRomsPath = Path.Combine(sharePath, "share", "systemroms");
            bool keyExists = biosFiles.ContainsKey(machine);

            if (keyExists && Directory.Exists(systemRomsPath))
            {
                string[] systemRomsContent = Directory.GetFiles(systemRomsPath, "*.*")
                                     .Select(Path.GetFileName)
                                     .ToArray();

                string[] biosFileRequired = biosFiles.Single(s => s.Key == machine).Value;

                foreach (var c in biosFileRequired)
                {
                    if (!systemRomsContent.Contains(c))
                        throw new ApplicationException(machine + " machine has missing BIOS file " + "'" + c + "'" + " in 'bios\\openmsx\\share\\systemroms' folder.");
                }
            }
            
            else if (keyExists && !Directory.Exists(systemRomsPath))
                throw new ApplicationException(machine + " machine has all BIOS files missing in 'bios\\openmsx\\share\\systemroms' folder.");

            commandArray.Add("-machine");
            commandArray.Add(machine);

            // I/O slot Extensions .Where(c => c.Name != null && c.Name.StartWith(...))
            if (system != "colecovision")
            {
                List<string> extensionlist = SystemConfig
                    .Where(c => c.Name != null && c.Name.StartsWith(system + ".ext_") && c.Value == "1")
                    .Select(c => c.Name.Replace(system + ".ext_", ""))
                    .ToList();

                for (int i = 0; i < extensionlist.Count; i++)
                {
                    commandArray.Add("-ext");
                    commandArray.Add(extensionlist[i]);
                }
            }

            // Cartridge slot extension (MSX machines only have 2 cartridge slots, one is taken by the game so it leaves place for one extension
            if (SystemConfig.isOptSet("cart_extension") && !string.IsNullOrEmpty(SystemConfig["cart_extension"]) && system != "colecovision")
            {
                if (SystemConfig.isOptSet("altromtype") && SystemConfig["altromtype"] == "carta")
                    commandArray.Add("-extb");
                else
                    commandArray.Add("-ext");
                
                commandArray.Add(SystemConfig["cart_extension"]);
            }

            // Run scripts
            string scriptspath = Path.Combine(path, "share", "scripts");
            if (!Directory.Exists(scriptspath)) try { Directory.CreateDirectory(scriptspath); }
                catch { }

            checkOrCreateScripts(scriptspath);

            if (romtype != null && romtype == "-cassetteplayer")
            {
                commandArray.Add("-script");
                commandArray.Add("\"" + Path.Combine(scriptspath, "autoruncassettes.tcl") + "\"");
            }
            else if (romtype != null && romtype == "-laserdisc")
            {
                commandArray.Add("-script");
                commandArray.Add("\"" + Path.Combine(scriptspath, "autorunlaserdisc.tcl") + "\"");
            }


            // Add media type
            if (romtype != null)
               commandArray.Add(romtype);
            
            commandArray.Add("\"" + rom + "\"");

            // Setup controllers (using tcl scripts)
            commandArray.AddRange(configureControllers(scriptspath));

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        /// <summary>
        /// Configure emulator features (settings.xml)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="rom"></param>
        private void SetupConfiguration(string path, string rom)
        {
            string sharepath = Path.Combine(path, "share");
            if (!Directory.Exists(sharepath)) try { Directory.CreateDirectory(sharepath); }
                catch { }

            string settingsFile = Path.Combine(sharepath, "settings.xml");

            var xdoc = File.Exists(settingsFile) ? XDocument.Load(settingsFile) : new XDocument(new XDeclaration("1.0", "utf-8", null));
            if (!File.Exists(settingsFile))
            {
                XDocumentType documentType = new XDocumentType("settings", null, "settings.dtd", null);
                xdoc.AddFirst(documentType);
            }

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
            bool disablefs = (SystemConfig.getOptBoolean("msx_fullscreen") || IsEmulationStationWindowed()) && !SystemConfig.getOptBoolean("forcefullscreen");

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
            string iconset = "none";
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

            // Clean existing bindings
            var bindings = topnode.GetOrCreateElement("bindings");
            bindings.RemoveAll();

            // Save xml file
            using (var writer = new XmlTextWriter(settingsFile, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;
                xdoc.Save(writer);
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            Process process = Process.Start(path);
            Thread.Sleep(4000);
            process.WaitForExit();

            if (bezel != null)
                bezel.Dispose();

            return 0;
        }

        /// <summary>
        /// Configure emulator features (settings.xml)
        /// </summary>
        /// <param name="path"></param>
        private void checkOrCreateScripts(string path)
        {
            foreach (var script in scriptFiles)
            {
                string scriptFile = Path.Combine(path, script.Key + ".tcl");
                string[] scriptValue = script.Value;

                using (StreamWriter sw = new StreamWriter(scriptFile, false))
                {
                    foreach (string text in scriptValue)
                        sw.WriteLine(text);

                    sw.Close();
                }
            }
        }

        static Dictionary<string, string[]> msxMedias = new Dictionary<string, string[]>()
        {
            { "-cart", new string[] { ".mx1", ".mx2", ".ri", ".rom" } },
            { "-diska", new string[] { ".di1", ".di2", ".dmk", ".dsk", ".fd1", ".fd2", ".xsa" } },
            { "-laserdisc", new string[] { ".ogv" } },
            { "-cassetteplayer", new string[] { ".cas", ".wav" } }
        };

        static List<string> machineWithDiskDrive = new List<string>() { "Panasonic_FS-A1GT", "Panasonic_FS-A1WSX", "National_FS-5500F2", "Philips_NMS_8245", "National_CF-3300" };
        static List<string> machineWithCassette = new List<string>() { "Panasonic_FS-A1WSX", "National_FS-5500F2", "Pioneer_PX-7", "Philips_NMS_8245", "National_CF-3300", "Philips_VG_8020" };
        static List<string> machineWithLaserdisc = new List<string>() { "Pioneer_PX-7" };

        static string defaultDiskMachine = "Panasonic_FS-A1GT";
        static string defaultMachineCassette = "Panasonic_FS-A1WSX";
        static string defaultLaserdiscMachine = "Pioneer_PX-7";

        static Dictionary<string, string[]> scriptFiles = new Dictionary<string, string[]>()
        {
            { "autoruncassettes", new string[] {"set autoruncassettes on" } },
            { "autorunlaserdisc", new string[] {"set autorunlaserdisc on" } },
            { "removealljoysticks", new string[] {"unplug joyporta", "unplug joyportb" } },
            { "plugmouse", new string[] { "unplug joyporta", "unplug joyportb", "plug joyporta mouse", "set grabinput on" } }
        };

        static Dictionary<string, string[]> biosFiles = new Dictionary<string, string[]>()
        {
            { "National_CF-3300", new string[] { "cf-3300_basic-bios1.rom", "cf-3300_disk.rom" } },
            { "National_FS-5500F2", new string[] { "fs-5500_basic-bios2.rom", "fs-5500_disk.rom", "fs-5500_kanjibasic.rom", "fs-5500_kanjifont.rom", "fs-5500_msx2sub.rom", "fs-5500_superimp.rom" } },
            { "Panasonic_FS-A1GT", new string[] { "fs-a1gt_firmware.rom", "fs-a1gt_kanjifont.rom" } },
            { "Panasonic_FS-A1WSX", new string[] { "fs-a1wsx_basic-bios2p.rom", "fs-a1wsx_disk.rom" , "fs-a1wsx_firmware.rom", "fs-a1wsx_fmbasic.rom", "fs-a1wsx_kanjibasic.rom", "fs-a1wsx_kanjifont.rom", "fs-a1wsx_msx2psub.rom" } },
            { "Philips_NMS_8245", new string[] { "nms8245_basic-bios2.rom", "nms8245_disk.rom", "nms8245_disk_1.06.rom", "nms8245_msx2sub.rom" } },
            { "Philips_VG_8020", new string[] { "vg8020_basic-bios1.rom" } },
            { "Pioneer_PX-7", new string[] { "px-7_basic-bios1.rom", "px-7_pbasic.rom" } },
            { "ColecoVision_SGM", new string[] { "coleco.rom" } },
            { "Spectravideo_SVI-738_SE", new string[] { "svi-738_disk.rom", "svi-738_rs232.rom", "svi-738_se_basic-bios1.rom" } }

        };
    }
}
