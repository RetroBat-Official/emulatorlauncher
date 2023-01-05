using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Linq;
using System.Windows.Forms;
using emulatorLauncher.PadToKeyboard;
using System.Security.Cryptography;
using emulatorLauncher;

namespace emulatorLauncher
{
    class PhoenixGenerator : Generator
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

            //Video settings
            XElement video = settings.Element("Video");
            video.SetAttributeValue("keep-aspect", "true");
            video.SetAttributeValue("vsynk", "true");
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
            string filemd5Hash = getMD5Hash(filename);
            string filesha1Hash = GetSha1Hash(filename);
            string fileSize = GetFileSize(filename);

            //phoenix does not accept .cue format, so we will read cue file to get the actual image in iso, img or bin format
            if (Path.GetExtension(filename).ToLower() == ".cue")
            {
                var fromcue = MultiDiskImageFile.FromFile(filename);
                var cuefile = fromcue.Files[0];
                string binmd5hash = getMD5Hash(cuefile);
                string binsha1Hash = GetSha1Hash(cuefile);
                string binSize = GetFileSize(cuefile);

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

        /// <summary>
        /// Get MD5 hash
        /// </summary>
        /// <param name="file"></param>
        private string getMD5Hash(string file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                }
            }
        }

        /// <summary>
        /// Get SHA-1 hash
        /// </summary>
        /// <param name="file"></param>
        private string GetSha1Hash(string file)
        {
            using (FileStream fs = File.OpenRead(file))
            {
                SHA1 sha = new SHA1Managed();
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", string.Empty).ToLower();
            }
        }

        /// <summary>
        /// Get File size
        /// </summary>
        /// <param name="file"></param>
        private string GetFileSize(string file)
        {
            FileInfo fi = new FileInfo(file);
            return fi.Length.ToString();
        }
    }
}
