using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    public class Guns
    {
        public static void StartSindenSoftware()
        {
            try
            {
                var px = Process.GetProcessesByName("Lightgun").FirstOrDefault();
                if (px != null)
                {
                    try
                    {
                        var sindenProcess = px.GetProcessCommandline().SplitCommandLine().FirstOrDefault();
                        if (!string.IsNullOrEmpty(sindenProcess))
                            SimpleLogger.Instance.Info("[GUNS] Sinden process already running: " + sindenProcess);
                    }
                    catch
                    { }
                }
                else
                {
                    string sindenExe = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "sinden", "Lightgun.exe");
                    string sindenPath = Path.GetDirectoryName(sindenExe);
                    string cfgFile = Path.Combine(sindenPath, "Lightgun.exe.Config");

                    if (!File.Exists(sindenExe))
                    {
                        if (Program.SystemConfig.isOptSet("sindensoftwarepath") && !string.IsNullOrEmpty(Program.SystemConfig["sindensoftwarepath"]))
                        {
                            sindenExe = Program.SystemConfig["sindensoftwarepath"];
                            sindenPath = Path.GetDirectoryName(sindenExe);
                            cfgFile = Path.Combine(sindenPath, "Lightgun.exe.Config");
                        }
                    }

                    if (!File.Exists(sindenExe))
                        return;

                    if (File.Exists(cfgFile))
                    {
                        XDocument xmlDocument = XDocument.Load(cfgFile);
                        XElement appSettings = xmlDocument.Descendants("appSettings").FirstOrDefault();

                        if (appSettings != null)
                        {
                            XElement element = appSettings.Elements("add").FirstOrDefault(e => e.Attribute("key")?.Value == "chkAutoStart");

                            if (element != null)
                            {
                                element.SetAttributeValue("value", "1");
                            }
                            else
                            {
                                XElement newElement = new XElement("add", new XAttribute("key", "chkAutoStart"), new XAttribute("value", "1"));
                                appSettings.Add(newElement);
                            }
                        }
                        xmlDocument.Save(cfgFile);
                        SimpleLogger.Instance.Info("[GUNS] Sinden software succesfully configured to start automatically.");
                    }

                    if (File.Exists(sindenExe))
                    {
                        var lightGunPx = new ProcessStartInfo()
                        {
                            FileName = sindenExe,
                            WorkingDirectory = sindenPath,
                            WindowStyle = ProcessWindowStyle.Minimized,
                        };

                        using (Process process = new Process())
                        {
                            process.StartInfo = lightGunPx;
                            process.Start();
                            SimpleLogger.Instance.Info("[GUNS] Starting Sinden Software.");
                        }
                    }
                }
            }
            catch { }
        }
    }
}
