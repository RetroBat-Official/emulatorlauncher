using System.Linq;
using TeknoParrotUi.Common;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using EmulatorLauncher.Common.Lightguns;
using System.Management;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private static bool ConfigureTPGuns(GameProfile userProfile)
        {
            if (!Program.SystemConfig.getOptBoolean("use_guns"))
                return false;

            if (!userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("Gun") || j.ButtonName.Contains("GUN")))
                return false;

            SimpleLogger.Instance.Info("[GUNS] Configuring Gun.");

            bool useOneGun = Program.SystemConfig.getOptBoolean("one_gun");
            bool multigun = false;
            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();
            var hidDevices = RawInputDevice.GetRawInputDevices();
            var mouseHIDInfo = hidDevices.Where(t => t.Type == RawInputDeviceType.Mouse).ToList();

            if (gunCount < 1)
                return false;

            Dictionary<ManagementObject, string> mouseList = new Dictionary<ManagementObject, string>();

            int i = 0;
            foreach (var gun in guns)
            {
                var mouse = mouseHIDInfo.Where(h => h.DevicePath == gun.DevicePath).FirstOrDefault();
                SimpleLogger.Instance.Info("[GUNS] Identified gun with name: " + gun.Name != null ? gun.Name : "NONAME");
                SimpleLogger.Instance.Info("[GUNS] Identified mouse with Manufacturer: " + mouse.Manufacturer != null ? mouse.Manufacturer : "NOMANUFACTURER");
                SimpleLogger.Instance.Info("[GUNS] Identified mouse with name: " + mouse.Name != null ? mouse.Name : "NONAME");
                SimpleLogger.Instance.Info("[GUNS] Identified mouse with vendorID: " + mouse.VendorId != null ? mouse.VendorId.ToString() : "NOVENDORID");
                
                string cleanPath = "";

                string devicepathHID1 = gun.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                SimpleLogger.Instance.Info("[GUNS] Identified gun with path: " + devicepathHID1);

                if (devicepathHID1.Length > 39)
                    cleanPath = devicepathHID1.Substring(0, devicepathHID1.Length - 39);
                SimpleLogger.Instance.Info("[GUNS] Identified gun with clean path: " + cleanPath);

                string query = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath + "'").Replace("\\", "\\\\");
                ManagementObjectSearcher moSearch1 = new ManagementObjectSearcher(query);
                ManagementObjectCollection moCollection1 = moSearch1.Get();
                foreach (ManagementObject mo in moCollection1.Cast<ManagementObject>())
                {
                    string desc1 = mo["Description"].ToString();
                    mouseList.Add(mo, desc1);
                    SimpleLogger.Instance.Info("[GUNS] Identified gun with name: " + desc1);
                }
                i++;
            }

            // Variables
            int playerNumber = 1;
            YmlContainer game = null;
            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            string tpMappingyml = null;
            var buttonMap = new Dictionary<string, string>();
            string gameName = null;
            string tpGameName = Path.GetFileNameWithoutExtension(userProfile.FileName).ToLowerInvariant();

            // Look for number of players based on string search in userprofile file (Px, Player x)
            if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P6") || j.ButtonName.Contains("Player 6")))
                playerNumber = 6;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P5") || j.ButtonName.Contains("Player 5")))
                playerNumber = 5;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P4") || j.ButtonName.Contains("Player 4")))
                playerNumber = 4;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P3") || j.ButtonName.Contains("Player 3")))
                playerNumber = 3;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P2") || j.ButtonName.Contains("Player 2")))
                playerNumber = 2;

            // Search mapping in yml file
            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "inputmapping");

                tpMappingyml = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(tpMappingyml))
                    break;
            }

            if (File.Exists(tpMappingyml))
            {
                YmlFile ymlFile = YmlFile.Load(tpMappingyml);
                game = ymlFile.Elements.Where(g => g.Name == tpGameName).FirstOrDefault() as YmlContainer;
                if (game != null)
                {
                    gameName = game.Name;
                    foreach (var buttonEntry in game.Elements)
                    {
                        YmlElement button = buttonEntry as YmlElement;
                        if (button.Name == "Players")
                        {
                            playerNumber = button.Value.ToInteger();
                        }

                        else if (button != null && button.Name != "Players")
                        {
                            buttonMap.Add(button.Name, button.Value);
                        }
                    }

                    gameMapping.Add(gameName, buttonMap);
                    SimpleLogger.Instance.Info("[INFO] Performing mapping based on teknoparrot.yml file");
                }
                else
                {
                    SimpleLogger.Instance.Warning("[GUNS] Game mapping not listed in teknoparrot.yml file.");
                    return false;
                }
            }
            else
            {
                SimpleLogger.Instance.Warning("[GUNS] File teknoparrot.yml does not exist.");
                return false;
            }

            return false;
        }
    }
}
