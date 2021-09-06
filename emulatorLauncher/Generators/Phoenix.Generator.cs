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
            BIOS
        }
        private const string emuExe = "PhoenixEmuProject.exe";
        private const string biosName = "[BIOS] Atari Jaguar (World).j64";
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = core == "phoenix-32" ? "ph-win32" : "ph-win64";
            string emuPath = AppConfig.GetFullPath(folderName);
            string biosPath = AppConfig.GetFullPath("bios");
            string settingsFile = Path.Combine(emuPath, "phoenix.config.xml");
            string biosFile = Path.Combine(biosPath, biosName);
            string exe = Path.Combine(emuPath, emuExe);
            if (!File.Exists(exe))
            {
                SimpleLogger.Instance.Info("ERROR: " + exe + " not found");
                return null;
            }
            if (!File.Exists(biosFile))
            {
                SimpleLogger.Instance.Info("ERROR: " + biosFile + " not found");
                return null;
            }

            try
            {
                if (File.Exists(settingsFile))
                {
                    XDocument settings = XDocument.Load(settingsFile);
                    XElement library = settings.Root.Element("Library");
                    XElement platformJaguar = library != null ? library.Element("Platform-Jaguar") : null;

                    if (platformJaguar == null)
                    {
                        // Settings file invalid. Let's delete it and create new one
                        File.Delete(settingsFile);
                        GenerateConfig(rom, biosFile, settingsFile);
                    }
                    else
                    {
                        AppendDumpConfig(platformJaguar, DumpType.CARTRIDGE, rom);
                        AppendDumpConfig(platformJaguar, DumpType.BIOS, biosFile);
                        settings.Save(settingsFile);
                    }
                }
                else
                {
                    GenerateConfig(rom, biosFile, settingsFile);
                }
            }
            catch (Exception e)
            {
                SimpleLogger.Instance.Info(e.Message.ToString());
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

        public override void RunAndWait(ProcessStartInfo path)
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

                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;

                SendKeys.SendWait("%");
                SendKeys.SendWait("{RIGHT}");
                SendKeys.SendWait("{ENTER}");
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
        }


        private void GenerateConfig(string rom, string bios, string settingsFile)
        {
            var joystickConfig = "j0a1@0@-1,j0a1@0@1,j0a0@0@-1,j0a0@0@1,j0b2,j0b0,j0b1,j0b6,j0b7,j0b3,j0a5@0@-1,j0a6@0@1,j0a5@0@1,j0a6@0@-1,j0b4,j0b5,j0a2@0@1,j0a2@0@-1,vk57,vk48,vk219";
            XDocument settings =
              new XDocument(
                  new XDeclaration("1.0", "utf-8", null),
                  new XElement("root", new XAttribute("Platform", "Jaguar"),
                      new XElement("Settings",
                            new XElement("Global",
                                new XElement("Platform-Jaguar",
                                    new XElement("Input", new XAttribute("hide-cursor", "true"),
                                        new XElement("Device", new XAttribute("name", "Joy"), joystickConfig)
                                    )
                                )
                            ),
                            new XElement("Video", new XAttribute("keep-aspect", "true")),
                            new XElement("Others", new XAttribute("no-autosave", "true"))
                      ),
                      new XElement("Library",
                          new XElement("Platform-Jaguar",
                              new XElement("Machine-Save-States"),
                              new XElement("Records")
                          )
                      )
                  )
              );

            var platform = settings.Root.Element("Library").Element("Platform-Jaguar");
            AppendDumpConfig(platform, DumpType.CARTRIDGE, rom);
            AppendDumpConfig(platform, DumpType.BIOS, bios);
            settings.Save(settingsFile);
        }

        private void AppendDumpConfig(XElement platformConfig, DumpType type, string fileName)
        {
            var filePath = Path.GetDirectoryName(fileName); 
            var md5Hash = getMD5Hash(fileName);
            var sha1Hash = GetSha1Hash(fileName);
            var fileSize = GetFileSize(fileName);
            var element = platformConfig.Element(type.ToString());
            if (element == null)
            {
                platformConfig.Add(new XElement(type.ToString()));
                element = platformConfig.Element(type.ToString());
            }

            element.SetAttributeValue("expanded", "true");
            element.SetAttributeValue("attach", fileName);
            element.SetAttributeValue("last-path", filePath);

            var dump = element.Descendants("Dump").Where(ele => (string)ele.Attribute("md5") == md5Hash).FirstOrDefault();

            if (dump == null)
            {
                dump = new XElement("Dump");
                element.Add(dump);
            }
            dump.SetAttributeValue("path", fileName);
            dump.SetAttributeValue("fast-md5", md5Hash);
            dump.SetAttributeValue("md5", md5Hash);
            dump.SetAttributeValue("sh1", sha1Hash);
            dump.SetAttributeValue("size", fileSize);
        }

        private string getMD5Hash(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                }
            }
        }

        private string GetSha1Hash(string fileName)
        {
            using (FileStream fs = File.OpenRead(fileName))
            {
                SHA1 sha = new SHA1Managed();
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", string.Empty).ToLower();
            }
        }

        private string GetFileSize(string fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            return fi.Length.ToString();
        }
    }
}
