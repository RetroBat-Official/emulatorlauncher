using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Linq;
using System.Windows.Forms;
using emulatorLauncher.PadToKeyboard;
using System.Security.Cryptography;

namespace emulatorLauncher
{
    class PhoenixGenerator : Generator
    {
        enum DumpType
        {
            CARTRIDGE,
            CDROM,
            BIOS
        }
        
        private const string emuExe = "PhoenixEmuProject.exe";
        
        //define bios file to use for 3DO
        private const string biosName3do = "panafz10.bin";
        
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            //same emulator cannot be used for both jaguar and 3DO, so if in the future we want to add it for jaguar we will create it in "phoenix-jaguar" folder
            //it is required to click on the jaguar logo to change to jaguar and on the 3DO logo to come back to 3DO, not possible via menu, config-file or shortcut
            string folderName = "phoenix-3do";
            string emuPath = AppConfig.GetFullPath(folderName);
            string biosPath = AppConfig.GetFullPath("bios");
            string biosfile3do = Path.Combine(biosPath, biosName3do);
            string settingsFile = Path.Combine(emuPath, "phoenix.config.xml");
            string exe = Path.Combine(emuPath, emuExe);
            
            //return error if .exe does not exist
            if (!File.Exists(exe))
            {
                SimpleLogger.Instance.Info("ERROR: " + exe + " not found");
                return null;
            }

            //generate 3DO part - jaguar part can be added later if needed
            if (system == "3do")
            {
                try
                {
                    
                    //check that :xml settings file exist and write necessary info to it, if file does not exist return error
                    if (File.Exists(settingsFile))
                    {
                        XDocument configfile = XDocument.Load(settingsFile);
                        XElement library = configfile.Root.Element("Library");
                        XElement platform3do = library != null ? library.Element("Platform-3DO") : null;

                        //check that 3DO platform is initialized in settings file and write necessary info to it, else return error
                        if (platform3do != null)
                        {
                            //write game information
                            Append3doConfig(platform3do, DumpType.CDROM, rom);
                            
                            //write bios information
                            Append3doConfig(platform3do, DumpType.BIOS, biosfile3do);

                            //other configuration
                            SetupGeneralConfiguration(configfile);

                            //save file
                            configfile.Save(settingsFile);
                        }

                        else
                        {
                            SimpleLogger.Instance.Info("ERROR: settings file does not contain 3DO platform");
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
            }

            else
            {
                SimpleLogger.Instance.Info("ERROR: system is not supported");
                return null;
            }

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
        private void SetupGeneralConfiguration(XDocument xml)
        {
            XElement settings = xml.Root.Element("Settings");

            //Video settings
            XElement video = settings.Element("Video");
            video.SetAttributeValue("keep-aspect", "true");
            video.SetAttributeValue("vsynk", "true");
        }

        /// <summary>
        /// Setup 3DO part in phoenix.config.xml file
        /// </summary>
        /// <param name="platformConfig"></param>
        /// <param name="type"></param>
        /// <param name="filename"></param>
        private void Append3doConfig(XElement platformConfig, DumpType type, string filename)
        {
            //define strings - phoenix does not accept .cue format, so we also get associated .bin
            //.bin file needs to be the same name as .cue file
            string filepath = Path.GetDirectoryName(filename);
            string filenamenoext = Path.GetFileNameWithoutExtension(filename);
            string binfile = Path.Combine(filepath, string.Concat(filenamenoext, ".bin"));
            string filemd5Hash = getMD5Hash(filename);
            string binmd5hash = getMD5Hash(binfile);
            string filesha1Hash = GetSha1Hash(filename);
            string binsha1Hash = GetSha1Hash(binfile);
            string fileSize = GetFileSize(filename);
            string binSize = GetFileSize(binfile);

            //write platform settings
            var element = platformConfig.Element(type.ToString());
            
            //For 3DO, attribute for rom is CD-ROM
            if (type == DumpType.CDROM)
                element = platformConfig.Element("CD-ROM");
            else
                element = platformConfig.Element(type.ToString());

            //set attributes for rom and BIOS element
            element.SetAttributeValue("expanded", "true");
            if (Path.GetExtension(filename).ToLower() == ".cue")        //if game is in .cue format, change to .bin file
                element.SetAttributeValue("attach", binfile);
            else
                element.SetAttributeValue("attach", filename);
            
            element.SetAttributeValue("last-path", filepath);

            //set attributes of DUMP element
            var dump = new XElement(element);

            if (Path.GetExtension(filename).ToLower() == ".cue")
                dump = element.Descendants("Dump").Where(ele => (string)ele.Attribute("md5") == binmd5hash).FirstOrDefault();       //if game is in .cue look for the bin md5hash to avoid creating duplicate line
            else
                dump = element.Descendants("Dump").Where(ele => (string)ele.Attribute("md5") == filemd5Hash).FirstOrDefault();

            if (dump == null)
            {
                dump = new XElement("Dump");
                element.Add(dump);
            }
            if (Path.GetExtension(filename).ToLower() == ".cue")        //if game is in .cue format, get information of .bin file
            {
                dump.SetAttributeValue("path", binfile);
                dump.SetAttributeValue("size", binSize);
                dump.SetAttributeValue("fast-md5", binmd5hash);
                dump.SetAttributeValue("md5", binmd5hash);
                dump.SetAttributeValue("sh1", binsha1Hash);
            }  
            else
            {
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
