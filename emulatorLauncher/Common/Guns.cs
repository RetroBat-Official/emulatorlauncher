using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using EmulatorLauncher.Common;
using System.Collections.Generic;

namespace EmulatorLauncher
{
    public class Guns
    {
        public static void StartSindenSoftware()
        {
            List<string> mouse_left = new List<string>() { "cbButtonTrigger", "cbButtonTriggerB" };
            List<string> mouse_right = new List<string>() 
            { 
                "cbButtonPumpAction", "cbButtonFrontLeft", "cbButtonTriggerOffscreen", "cbButtonPumpActionOffscreen", "cbButtonFrontLeftOffscreen",
                "cbButtonPumpActionB", "cbButtonFrontLeftB", "cbButtonTriggerOffscreenB", "cbButtonPumpActionOffscreenB", "cbButtonFrontLeftOffscreenB"
            };
            List<string> mouse_middle = new List<string>() { "cbButtonRearLeft", "cbButtonRearLeftOffscreen", "cbButtonRearLeftB", "cbButtonRearLeftOffscreenB" };
            List<string> joy_1 = new List<string>() { "cbButtonTrigger", "cbButtonTriggerB" };
            List<string> joy_2 = new List<string>() { "cbButtonPumpAction", "cbButtonTriggerOffscreen", "cbButtonPumpActionOffscreen", "cbButtonPumpActionB", "cbButtonTriggerOffscreenB", "cbButtonPumpActionOffscreenB" };
            List<string> joy_3 = new List<string>() { "cbButtonFrontLeft", "cbButtonFrontLeftOffscreen", "cbButtonFrontLeftB", "cbButtonFrontLeftOffscreenB" };
            List<string> joy_4 = new List<string>() { "cbButtonFrontRight", "cbButtonFrontRightOffscreen", "cbButtonFrontRightB", "cbButtonFrontRightOffscreenB" };
            List<string> joy_5 = new List<string>() { "cbButtonRearLeft", "cbButtonRearLeftOffscreen", "cbButtonRearLeftB", "cbButtonRearLeftOffscreenB" };
            List<string> joy_6 = new List<string>() { "cbButtonRearRight", "cbButtonRearRightOffscreen", "cbButtonRearRightB", "cbButtonRearRightOffscreenB" };
            List<string> joy_7 = new List<string>() { "cbButtonUp", "cbButtonUpOffscreen", "cbButtonUpB", "cbButtonUpOffscreenB" };
            List<string> joy_8 = new List<string>() { "cbButtonDown", "cbButtonDownOffscreen", "cbButtonDownB", "cbButtonDownOffscreenB" };
            List<string> joy_9 = new List<string>() { "cbButtonLeft", "cbButtonLeftOffscreen", "cbButtonLeftB", "cbButtonLeftOffscreenB" };
            List<string> joy_10 = new List<string>() { "cbButtonRight", "cbButtonRightOffscreen", "cbButtonRightB", "cbButtonRightOffscreenB" };

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
                            SetElementValue(appSettings, "chkAutoStart", "1");
                            SetElementValue(appSettings, "chkStartInTray", "1");

                            if (Program.SystemConfig.getOptBoolean("sindenJoyMode"))
                            {
                                foreach (string button in joy_1)
                                    SetElementValue(appSettings, button, "94");
                                foreach (string button in joy_2)
                                    SetElementValue(appSettings, button, "95");
                                foreach (string button in joy_3)
                                    SetElementValue(appSettings, button, "96");
                                foreach (string button in joy_4)
                                    SetElementValue(appSettings, button, "97");
                                foreach (string button in joy_5)
                                    SetElementValue(appSettings, button, "98");
                                foreach (string button in joy_6)
                                    SetElementValue(appSettings, button, "99");
                                foreach (string button in joy_7)
                                    SetElementValue(appSettings, button, "100");
                                foreach (string button in joy_8)
                                    SetElementValue(appSettings, button, "101");
                                foreach (string button in joy_9)
                                    SetElementValue(appSettings, button, "102");
                                foreach (string button in joy_10)
                                    SetElementValue(appSettings, button, "103");

                                SetElementValue(appSettings, "chkEnableJoystick", "1");
                            }
                            else if (Program.Features.IsSupported("sindenJoyMode"))
                            {
                                foreach (string button in mouse_left)
                                    SetElementValue(appSettings, button, "1");
                                foreach (string button in mouse_right)
                                    SetElementValue(appSettings, button, "3");
                                foreach (string button in mouse_middle)
                                    SetElementValue(appSettings, button, "2");
                                foreach (string button in joy_7)
                                    SetElementValue(appSettings, button, "74");
                                foreach (string button in joy_8)
                                    SetElementValue(appSettings, button, "75");
                                foreach (string button in joy_9)
                                    SetElementValue(appSettings, button, "76");
                                foreach (string button in joy_10)
                                    SetElementValue(appSettings, button, "77");

                                SetElementValue(appSettings, "cbButtonFrontRight", "9");
                                SetElementValue(appSettings, "cbButtonFrontRightB", "10");
                                SetElementValue(appSettings, "cbButtonFrontRightOffscreen", "13");
                                SetElementValue(appSettings, "cbButtonFrontRightOffscreenB", "14");
                                SetElementValue(appSettings, "cbButtonRearRight", "7");
                                SetElementValue(appSettings, "cbButtonRearRightB", "7");
                                SetElementValue(appSettings, "cbButtonRearRightOffscreen", "7");
                                SetElementValue(appSettings, "cbButtonRearRightOffscreenB", "7");

                                SetElementValue(appSettings, "chkEnableJoystick", "0");
                            }
                        }

                        xmlDocument.Save(cfgFile);
                        SimpleLogger.Instance.Info("[GUNS] Sinden software succesfully configured.");
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
                    else
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Cannot start Sinden lightgun software, executable not found.");
                    }
                }
            }
            catch { }
        }

        public static void KillSindenSoftware()
        {
            if (!Program.SystemConfig.getOptBoolean("sindenKill"))
                return;

            var px = Process.GetProcessesByName("Lightgun").FirstOrDefault();
            if (px == null)
                return;
            else
            {
                SimpleLogger.Instance.Info("[INFO] Closing Sinden software.");
                try { px.Kill(); }
                catch { SimpleLogger.Instance.Info("[WARNING] Failed to terminate Sinden software."); }
            }
        }

        private static void SetElementValue(XElement appSettings, string attribute, string value)
        {
            try
            {
                XElement element = appSettings.Elements("add").FirstOrDefault(e => e.Attribute("key")?.Value == attribute);

                if (element != null)
                {
                    element.SetAttributeValue("value", value);
                }
                else
                {
                    XElement newElement = new XElement("add", new XAttribute("key", attribute), new XAttribute("value", value));
                    appSettings.Add(newElement);
                }
            }
            catch { }
        }
    }
}
