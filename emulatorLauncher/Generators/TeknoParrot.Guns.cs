using System.Linq;
using TeknoParrotUi.Common;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using EmulatorLauncher.Common.Lightguns;
using System.Management;
using System;
using Keys = System.Windows.Forms.Keys;
using EmulatorLauncher.Common.Joysticks;
using System.Windows.Input;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private static bool ConfigureTPGuns(GameProfile userProfile)
        {
            if (!Program.SystemConfig.getOptBoolean("use_guns"))
                return false;

            // Return if game is definitely not a gun game !
            if (!userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("Gun") || j.ButtonName.Contains("GUN")))
                return false;

            SimpleLogger.Instance.Info("[GUNS] Configuring Gun(s).");

            // Variables
            bool useOneGun = Program.SystemConfig.getOptBoolean("one_gun");
            bool useKb = Program.SystemConfig.getOptBoolean("tp_gunkeyboard");
            RawInputDevice keyboard = null;
            
            // Number of players
            int playerNumber = GetNumberOfPlayers(userProfile);

            // Get guns and guncount
            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();
            RawLightgun gun1 = null;
            RawLightgun gun2 = null;
            RawLightgun gun3 = null;
            RawLightgun gun4 = null;

            if (gunCount < 1)       // Return if no gun or mouse is connected !
                return false;

            SimpleLogger.Instance.Info("[GUNS] Found " + gunCount + " usable guns.");

            if (gunCount > 3)
            {
                gun1 = guns[0];
                gun2 = guns[1];
                gun3 = guns[2];
                gun4 = guns[3];
            }
            else if (gunCount > 2)
            {
                gun1 = guns[0];
                gun2 = guns[1];
                gun3 = guns[2];
            }

            else if (gunCount > 1)
            {
                gun1 = guns[0];
                gun2 = guns[1];
            }
            else
            {
                gun1 = guns[0];
            }

            if (Program.SystemConfig.isOptSet("tp_gunindex1") && !string.IsNullOrEmpty(Program.SystemConfig["tp_gunindex1"]))
            {
                int index = Program.SystemConfig["tp_gunindex1"].ToInteger();
                if (guns.Length > index)
                    gun1 = guns[Program.SystemConfig["tp_gunindex1"].ToInteger()];
                else
                    SimpleLogger.Instance.Info("[GUNS] Cannot override index for Gun 1");
            }
            if (Program.SystemConfig.isOptSet("tp_gunindex2") && !string.IsNullOrEmpty(Program.SystemConfig["tp_gunindex2"]))
            {
                int index = Program.SystemConfig["tp_gunindex2"].ToInteger();
                if (guns.Length > index)
                    gun2 = guns[Program.SystemConfig["tp_gunindex2"].ToInteger()];
                else
                    SimpleLogger.Instance.Info("[GUNS] Cannot override index for Gun 2");
            }
            if (Program.SystemConfig.isOptSet("tp_gunindex3") && !string.IsNullOrEmpty(Program.SystemConfig["tp_gunindex3"]))
            {
                int index = Program.SystemConfig["tp_gunindex3"].ToInteger();
                if (guns.Length > index)
                    gun3 = guns[Program.SystemConfig["tp_gunindex3"].ToInteger()];
                else
                    SimpleLogger.Instance.Info("[GUNS] Cannot override index for Gun 3");
            }
            if (Program.SystemConfig.isOptSet("tp_gunindex4") && !string.IsNullOrEmpty(Program.SystemConfig["tp_gunindex4"]))
            {
                int index = Program.SystemConfig["tp_gunindex4"].ToInteger();
                if (guns.Length > index)
                    gun4 = guns[Program.SystemConfig["tp_gunindex4"].ToInteger()];
                else
                    SimpleLogger.Instance.Info("[GUNS] Cannot override index for Gun 4");
            }

            // logs
            if (gun1 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 1: " + gun1.DevicePath.ToString());
            if (gun2 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 1: " + gun2.DevicePath.ToString());
            if (gun3 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 1: " + gun3.DevicePath.ToString());
            if (gun4 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 1: " + gun4.DevicePath.ToString());


            // Get keyboards
            var hidDevices = RawInputDevice.GetRawInputDevices();
            var keyboards = hidDevices.Where(t => t.Type == RawInputDeviceType.Keyboard).OrderBy(u => u.DevicePath).ToList();
            if (keyboards.Count > 0)
                keyboard = keyboards[0];

            // Define alternative keyboard to use in case multiple keyboards
            if (keyboards.Count > 1 && Program.SystemConfig.isOptSet("tp_kbindex") && !string.IsNullOrEmpty(Program.SystemConfig["tp_kbindex"]))
            {
                int kbCount = keyboards.Count();
                int kbIndex = Program.SystemConfig["tp_kbindex"].ToInteger();
                if (kbIndex > kbCount)
                    keyboard = keyboards[kbCount - 1];
                else
                    keyboard = keyboards[kbIndex];
            }

            // Fetch name of keyboard to add in TP userprofile !
            foreach (var k in keyboards)
            {
                string cleanPath = "";

                string devicepathHID1 = k.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");

                try
                {
                    if (devicepathHID1.Length > 39)
                        cleanPath = devicepathHID1.Substring(0, devicepathHID1.Length - 39);

                    string query = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath + "'").Replace("\\", "\\\\");
                    ManagementObjectSearcher moSearch1 = new ManagementObjectSearcher(query);
                    ManagementObjectCollection moCollection1 = moSearch1.Get();
                    foreach (ManagementObject mo in moCollection1.Cast<ManagementObject>())
                    {
                        string desc1 = mo["Description"].ToString();
                        string manuf = mo["Manufacturer"].ToString();
                        k.FriendlyName = desc1;
                        k.Manufacturer = manuf;
                        SimpleLogger.Instance.Info("[GUNS] Identified gun with name: " + desc1);
                    }
                }
                catch { SimpleLogger.Instance.Info("[GUNS] Cannot get friendly name for Keyboard."); }
            }

            if (keyboard != null && keyboard.FriendlyName != null)
                SimpleLogger.Instance.Info("[GUNS] Using keyboard: " + keyboard.FriendlyName);
            
            // Cleanup
            foreach (var joyButton in userProfile.JoystickButtons)
            {
                joyButton.RawInputButton = null;
                joyButton.BindName = null;
                joyButton.BindNameDi = null;
                joyButton.BindNameRi = null;
                joyButton.BindNameXi = null;
                joyButton.DirectInputButton = null;
                joyButton.XInputButton = null;
            }

            // Variables
            YmlContainer game = null;
            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            string tpMappingyml = null;
            var buttonMap = new Dictionary<string, string>();
            string gameName = null;
            string tpGameName = Path.GetFileNameWithoutExtension(userProfile.FileName).ToLowerInvariant();

            // Search mapping in yml file and build buttonMap dictionnary
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
                            continue;
                        }

                        else if (button != null)
                        {
                            buttonMap.Add(button.Name, button.Value);
                        }
                    }

                    gameMapping.Add(gameName, buttonMap);
                    SimpleLogger.Instance.Info("[GUNS] Performing mapping based on teknoparrot.yml file");
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

            // Finalize number of players
            if (useOneGun || gunCount == 1)
                playerNumber = 1;

            else if (playerNumber > 1)
            {
                switch (playerNumber)
                {
                    case 2:
                        if (gunCount > 1)
                            playerNumber = 2;
                        break;
                    case 3:
                        {
                            if (gunCount > 2) playerNumber = 3;
                            else if (gunCount == 2) playerNumber = 2;
                            else if (gunCount == 1) playerNumber = 1;
                            break;
                        }
                    case 4:
                    case 5:
                    case 6:
                        {
                            if (gunCount > 3) playerNumber = 4;
                            else if (gunCount == 3) playerNumber = 3;
                            else if (gunCount == 2) playerNumber = 2;
                            else if (gunCount == 1) playerNumber = 1;
                            break;
                        }
                }
            }

            /// Build the xml gun section for each gun
            /// Use the buttonMap dictionnary to get buttons from the yml file
            /// Use keyboards and guns information

            for (int i = 1; i <= playerNumber; i++)
            {
                // Define gun for the player
                var iGun = gun1;
                if (i == 2)
                {
                    if (gun2 == null)
                        continue;
                    else
                        iGun = gun2;
                }
                else if (i == 3)
                {
                    if (gun3 == null)
                        continue;
                    else
                        iGun = gun3;
                }
                else if (i == 4)
                {
                    if (gun4 == null)
                        continue;
                    else
                        iGun = gun4;
                }

                // Variables
                string gunPath = iGun.DevicePath;
                string kbName;
                string kbSuffix;

                // Add manufacturer and name in some cases
                if (iGun.Manufacturer == null || iGun.Manufacturer == "")
                    iGun.Manufacturer = "Unknown Manufacturer";
                else
                    iGun.Manufacturer = iGun.Manufacturer;
                
                if (iGun.Name == null || iGun.Name == "")
                    iGun.Name = "Unknown Product";
                else
                    iGun.Name = iGun.Name;

                if (useKb && keyboard != null)
                {
                    kbName = keyboard.FriendlyName;
                    kbSuffix = keyboard.Manufacturer;
                }
               
                if (game != null)
                {
                    if (buttonMap.Count > 0)
                    {
                        foreach (var button in buttonMap)
                        {
                            JoystickButtons xmlPlace = null;
                            if (button.Value == null || button.Value == "" || button.Key == "Players")
                                continue;
                            else if (button.Value.StartsWith("kb_") && keyboard != null)
                            {
                                if (iGun.Type == RawLighGunType.Mouse)
                                {
                                    bool enumExists = Enum.TryParse(button.Key, out InputMapping inputEnum);
                                    if (enumExists)
                                        xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnum && !j.HideWithRawInput);
                                    if (xmlPlace == null)
                                        continue;

                                    kbName = keyboard.FriendlyName;
                                    kbSuffix = keyboard.Manufacturer;

                                    xmlPlace.RawInputButton = new RawInputButton
                                    {
                                        DevicePath = keyboard.DevicePath.ToString(),
                                        DeviceType = RawDeviceType.Keyboard,
                                        MouseButton = RawMouseButton.None
                                    };

                                    string kbkey = button.Value.Split('_')[1];
                                    if (Numbers.Contains(kbkey))
                                        kbkey = "D" + kbkey;

                                    if (Enum.TryParse(kbkey, true, out Keys key))
                                        xmlPlace.RawInputButton.KeyboardKey = key;

                                    string kbNameOverride = null;
                                    string deviceToOverride = "keyboard" + i;
                                    string overridePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                                    string newName = GetDescriptionFromFile(overridePath, deviceToOverride);
                                    if (newName != null)
                                        kbNameOverride = newName;

                                    if (kbNameOverride != null)
                                        xmlPlace.BindName = xmlPlace.BindNameRi = kbNameOverride + " " + key.ToString();
                                    else
                                        xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " " + key.ToString();
                                }
                                else
                                {
                                    // Find keyboard associated to lightgun
                                    int startIndex = iGun.DevicePath.IndexOf("VID");
                                    if (startIndex >= 0)
                                    {
                                        int endIndex = iGun.DevicePath.IndexOf('#', startIndex);
                                        if (endIndex == -1) continue;
                                        if (iGun.DevicePath.Contains("MI_"))
                                        {
                                            endIndex = iGun.DevicePath.IndexOf("MI_", startIndex);
                                            if (endIndex == -1) continue;
                                            endIndex += 5;
                                        }
                                        string searchPath = iGun.DevicePath.Substring(startIndex, endIndex - startIndex);

                                        keyboard = keyboards.FirstOrDefault(k => k.DevicePath.Contains(searchPath));
                                    }

                                    bool enumExists = Enum.TryParse(button.Key, out InputMapping inputEnum);
                                    if (enumExists)
                                        xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnum && !j.HideWithRawInput);
                                    if (xmlPlace == null)
                                        continue;

                                    kbName = iGun.Name;
                                    kbSuffix = iGun.Manufacturer;

                                    xmlPlace.RawInputButton = new RawInputButton
                                    {
                                        DevicePath = keyboard.DevicePath.ToString(),
                                        DeviceType = RawDeviceType.Keyboard,
                                        MouseButton = RawMouseButton.None
                                    };

                                    string kbkey = button.Value.Split('_')[1];
                                    if (Numbers.Contains(kbkey))
                                        kbkey = "D" + kbkey;

                                    if (Enum.TryParse(kbkey, true, out Keys key))
                                        xmlPlace.RawInputButton.KeyboardKey = key;

                                    string kbNameOverride = null;
                                    string deviceToOverride = "Lightgunkeyboard" + i;
                                    string overridePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                                    string newName = GetDescriptionFromFile(overridePath, deviceToOverride);
                                    if (newName != null)
                                        kbNameOverride = newName;

                                    if (kbNameOverride != null)
                                        xmlPlace.BindName = xmlPlace.BindNameRi = kbNameOverride + " " + key.ToString();
                                    else
                                        xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " " + key.ToString();
                                }
                            }
                            else if (button.Key.StartsWith("mouse"))
                            {
                                bool enumExists = Enum.TryParse(button.Value, out InputMapping inputEnum);
                                if (enumExists)
                                    xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnum && !j.HideWithRawInput);
                                if (xmlPlace == null)
                                    continue;

                                string mouseButton = button.Key.ToLowerInvariant();
                                if (mouseButton == "mouseleft") mouseButton = "LeftButton";
                                else if (mouseButton == "mousemiddle") mouseButton = "MiddleButton";
                                else if (mouseButton == "mouseright") mouseButton = "RightButton";
                                else if (mouseButton == "mousebutton4") mouseButton = "Button4";
                                else if (mouseButton == "mousebutton5") mouseButton = "Button5";

                                xmlPlace.RawInputButton = new RawInputButton
                                {
                                    DevicePath = iGun.DevicePath.ToString(),
                                    DeviceType = RawDeviceType.Mouse
                                };

                                if (Enum.TryParse(mouseButton, true, out RawMouseButton mbtn))
                                    xmlPlace.RawInputButton.MouseButton = mbtn;
                                
                                xmlPlace.RawInputButton.KeyboardKey = Keys.None;

                                string mouseNameOverride = null;
                                string deviceToOverride = "mouse" + i;
                                string overridePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                                string newName = GetDescriptionFromFile(overridePath, deviceToOverride);
                                if (newName != null)
                                    mouseNameOverride = newName;

                                if (mouseNameOverride != null)
                                    xmlPlace.BindName = xmlPlace.BindNameRi = mouseNameOverride + " " + mouseButton;
                                else
                                    xmlPlace.BindName = xmlPlace.BindNameRi = iGun.Manufacturer + " " + iGun.Name + " " + mouseButton;
                            }
                        }
                    }
                }

                // Add lightgun section
                JoystickButtons xmlLightgun = null;
                string searchLG = "P" + i + "LightGun";
                bool lgEnumExists = Enum.TryParse(searchLG, out InputMapping inputEnumLG);

                if (lgEnumExists)
                {
                    xmlLightgun = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnumLG && !j.HideWithRawInput);
                    xmlLightgun.RawInputButton = new RawInputButton
                    {
                        DevicePath = iGun.DevicePath.ToString(),
                        DeviceType = RawDeviceType.Mouse,
                        KeyboardKey = Keys.None
                    };
                    xmlLightgun.BindName = xmlLightgun.BindNameRi = iGun.Manufacturer + " " + iGun.Name;
                }
            }

            return false;
        }

        private readonly static List<string> Numbers = new List<string>
        { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private static string GetDescriptionFromFile(string path, string device)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var yml = YmlFile.Load(path);
                if (yml != null)
                {
                    var deviceInfo = yml.GetContainer("teknoparrotdevicenames");
                    if (deviceInfo != null)
                    {
                        string outputName = deviceInfo[device];
                        if (!string.IsNullOrEmpty(outputName))
                        {
                            SimpleLogger.Instance.Info("[GUNS] Device override: " + outputName.ToLowerInvariant());
                            return outputName;
                        }
                    }
                }
            }
            catch { return null; }

            return null;
        }
    }
}
