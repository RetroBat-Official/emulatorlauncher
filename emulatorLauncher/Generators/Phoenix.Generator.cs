using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class PhoenixGenerator : Generator
    {
        enum DumpType
        {
            CARTRIDGE,  //for jaguar
            CDROM,      //for 3DO
            BIOS
        }
        
        private const string emuExe = "PhoenixEmuProject.exe";
        
        //define bios files
        private const string biosName3do = "panafz10.bin";
        private const string biosNamejaguar = "[BIOS] Atari Jaguar (World).j64";

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = "phoenix";
            string emuPath = AppConfig.GetFullPath(folderName);
            string biosPath = AppConfig.GetFullPath("bios");
            string biosfile3do = Path.Combine(biosPath, biosName3do);
            string biosfilejaguar = Path.Combine(biosPath, biosNamejaguar);
            string settingsFile = Path.Combine(emuPath, "phoenix.config.xml");
            string exe = Path.Combine(emuPath, emuExe);

            //return error if .exe does not exist
            if (!File.Exists(exe))
            {
                SimpleLogger.Instance.Info("ERROR: " + exe + " not found");
                return null;
            }

            //unzip .zip and .7z and define rom as unzipped file
            if ((Path.GetExtension(rom).ToLower() == ".zip") || (Path.GetExtension(rom).ToLower() == ".7z"))
            {
                string romunzipped = TryUnZipGameIfNeeded(system, rom);
                if (Directory.Exists(romunzipped))
                {
                    rom = Path.Combine(romunzipped, Directory.GetFiles(romunzipped, "*.*").FirstOrDefault());
                    if (!File.Exists(rom))
                        throw new ApplicationException("Unable to find any game in the provided folder");
                }
            }
            try
            {    
                //check that :xml settings file exist and write necessary info to it, if file does not exist return error
                if (File.Exists(settingsFile))
                {
                    XDocument configfile = XDocument.Load(settingsFile);
                    XElement library = configfile.Root.Element("Library");
                    XElement platform3do = library != null ? library.Element("Platform-3DO") : null;
                    XElement platformjaguar = library != null ? library.Element("Platform-Jaguar") : null;

                    //generate settings file for the right platform (jaguar or 3DO)
                    if ((system == "3do") && (platform3do != null))
                    {
                        //General configuration
                        SetupGeneralConfiguration(configfile, system);

                        //write game information
                        AppendConfig(platform3do, DumpType.CDROM, rom);

                        //write bios information
                        AppendConfig(platform3do, DumpType.BIOS, biosfile3do);

                        //save file
                        configfile.Save(settingsFile);
                    }

                    else if ((system == "jaguar") && (platformjaguar != null))
                    {
                        //other configuration
                        SetupGeneralConfiguration(configfile, system);

                        //write game information
                        AppendConfig(platformjaguar, DumpType.CARTRIDGE, rom);

                        //write bios information
                        AppendConfig(platformjaguar, DumpType.BIOS, biosfilejaguar);

                        //save file
                        configfile.Save(settingsFile);
                    }

                    else
                    {
                        SimpleLogger.Instance.Info("ERROR: settings file does not contain" + system + "platform");
                        return null;
                    }
                }
                else
                {
                    SimpleLogger.Instance.Info("ERROR: settings file does not exist");
                    return null;
                }
            }

            catch (Exception e)
            {
                SimpleLogger.Instance.Info(e.Message.ToString());
            }


            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, emuPath, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "",
                WorkingDirectory = emuPath,
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;
            
            var process = Process.Start(path);

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }


                //get emulator window and press the power button (ALT > DOWN > RIGHT > ENTER)
                //Then set to fullscreen via F11
                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;
                System.Threading.Thread.Sleep(500);
                SendKeys.SendWait("%");
                SendKeys.SendWait("{DOWN}");
                SendKeys.SendWait("{RIGHT}");
                SendKeys.SendWait("{ENTER}");
                System.Threading.Thread.Sleep(1000);
                
                if (fullscreen)
                    SendKeys.SendWait("{F11}");
                
                break;
            }

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
            if (process != null)
                process.WaitForExit();
            if (bezel != null)
                bezel.Dispose();

            process.WaitForExit();
            int exitCode = process.ExitCode;

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return exitCode;
        }

        /// <summary>
        /// Setup phoenix.config.xml file
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="system"></param>
        private void SetupGeneralConfiguration(XDocument xml, string system)
        {
            //start by setting the root platform (jaguar or 3do) - this will enable the emulator to be on the correct platform at start
            if (system == "3do")
                xml.Root.SetAttributeValue("Platform", "3DO");
            else if (system == "jaguar")
                xml.Root.SetAttributeValue("Platform", "Jaguar");
            else
            {
                SimpleLogger.Instance.Info("ERROR: system" + system + "unknown");
                return;
            }

            XElement settings = xml.Root.Element("Settings");
            XElement global = settings.Element("Global");

            var settingsPlatform = global.Element("Platform-3DO");
            if (system == "jaguar")
                settingsPlatform = global.Element("Platform-Jaguar");
            
            ConfigureControllers(settingsPlatform);

            //Video settings
            XElement video = settings.Element("Video");
            video.SetAttributeValue("keep-aspect", "true");
            
            if (SystemConfig.isOptSet("phoenix_vsync") && SystemConfig.getOptBoolean("phoenix_vsync"))
                video.SetAttributeValue("vsynk", "false");
            else
                video.SetAttributeValue("vsynk", "true");

            if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                video.SetAttributeValue("screen-shader", "1");
            else
                video.SetAttributeValue("screen-shader", "0");

            if (system == "3do")
            {
                XElement options3do = settingsPlatform.Element("Options");

                var renderer = options3do.Descendants("Item").Where(x => (string)x.Attribute("index") == "Render").FirstOrDefault();

                if (SystemConfig.isOptSet("phoenix_renderer") && !string.IsNullOrEmpty(SystemConfig["phoenix_renderer"]))
                    renderer.SetAttributeValue("value", SystemConfig["phoenix_renderer"]);
                else
                    renderer.SetAttributeValue("value", "0");
            }
        }

        /// <summary>
        /// Setup phoenix.config.xml file
        /// </summary>
        /// <param name="platformConfig"></param>
        /// <param name="type"></param>
        /// <param name="filename"></param>
        private void AppendConfig(XElement platformConfig, DumpType type, string filename)
        {
            //define strings
            string filepath = Path.GetDirectoryName(filename);
            string filenamenoext = Path.GetFileNameWithoutExtension(filename);
            string filemd5Hash = FileTools.GetMD5(filename);
            string filesha1Hash = FileTools.GetSHA1(filename);
            string fileSize = FileTools.GetFileSize(filename).ToString();

            //phoenix does not accept .cue format, so we will read cue file to get the actual image in iso, img or bin format
            if (Path.GetExtension(filename).ToLower() == ".cue")
            {
                var fromcue = MultiDiskImageFile.FromFile(filename);
                var cuefile = fromcue.Files[0];
                string binmd5hash = FileTools.GetMD5(cuefile);
                string binsha1Hash = FileTools.GetSHA1(cuefile);
                string binSize = FileTools.GetFileSize(cuefile).ToString();

                //write library settings for cue files
                var elem = platformConfig.Element(type.ToString());
                if (type == DumpType.CDROM)
                    elem = platformConfig.Element("CD-ROM");
                else
                    elem = platformConfig.Element(type.ToString());
                elem.SetAttributeValue("expanded", "true");
                elem.SetAttributeValue("attach", cuefile);
                elem.SetAttributeValue("last-path", filepath);
                
                //delete existing dump nodes as it slows down emu startup time
                elem.RemoveNodes();
                
                //generate unique dump element
                var dumpcue = new XElement(elem);
                dumpcue = new XElement("Dump");
                elem.Add(dumpcue);

                dumpcue.SetAttributeValue("path", cuefile);
                dumpcue.SetAttributeValue("size", binSize);
                dumpcue.SetAttributeValue("fast-md5", binmd5hash);
                dumpcue.SetAttributeValue("md5", binmd5hash);
                dumpcue.SetAttributeValue("sh1", binsha1Hash);
            }
            
            else
            {
                //write library settings
                var element = platformConfig.Element(type.ToString());

                if (type == DumpType.CDROM)
                    element = platformConfig.Element("CD-ROM");
                else
                    element = platformConfig.Element(type.ToString());

                //write library settings
                element.SetAttributeValue("expanded", "true");
                element.SetAttributeValue("attach", filename);
                element.SetAttributeValue("last-path", filepath);

                //delete existing dump nodes as it slows down emu startup time
                element.RemoveNodes();

                //generate unique dump element
                var dump = new XElement(element);
                dump = new XElement("Dump");
                element.Add(dump);

                dump.SetAttributeValue("path", filename);
                dump.SetAttributeValue("size", fileSize);
                dump.SetAttributeValue("fast-md5", filemd5Hash);
                dump.SetAttributeValue("md5", filemd5Hash);
                dump.SetAttributeValue("sh1", filesha1Hash);
            }
        }
    }
}
