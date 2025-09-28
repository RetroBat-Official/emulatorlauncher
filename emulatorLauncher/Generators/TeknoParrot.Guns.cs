﻿using System.Linq;
using TeknoParrotUi.Common;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using EmulatorLauncher.Common.Lightguns;
using System.Management;
using System;
using Keys = System.Windows.Forms.Keys;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private static bool _demulshooter = false;
        private static bool _sindenSoft = false;
        private static RawInputDevice _orgKeyboard = null;
        private static Dictionary<RawLightgun, RawInputDevice> _gunsKbAssociation = new Dictionary<RawLightgun, RawInputDevice>();

        private static bool ConfigureTPGuns(GameProfile userProfile, string rom)
        {
            if (!Program.SystemConfig.getOptBoolean("use_guns"))
                return false;

            // Return if game is definitely not a gun game !
            if (!userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("Gun") || j.ButtonName.Contains("GUN") || j.InputMappingString.ToLowerInvariant().Contains("lightgun")))
                return false;

            SimpleLogger.Instance.Info("[GUNS] Configuring Gun(s).");

            // Variables
            bool useOneGun = Program.SystemConfig.getOptBoolean("one_gun");
            bool useKb = Program.SystemConfig.getOptBoolean("tp_gunkeyboard");
            bool st_kb = Program.SystemConfig["tp_gunkeyboard"] == "2";
            RawInputDevice keyboard = null;
            string tpGameName = Path.GetFileNameWithoutExtension(userProfile.FileName).ToLowerInvariant();

            // Number of players
            int playerNumber = GetNumberOfPlayers(userProfile);

            // Get guns and guncount
            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }
            
            RawLightgun gun1 = null;
            RawLightgun gun2 = null;
            RawLightgun gun3 = null;
            RawLightgun gun4 = null;
            RawLightgun orggun1 = null;
            RawLightgun orggun2 = null;
            RawLightgun orggun3 = null;
            RawLightgun orggun4 = null;
            RawInputDevice wiimote1kb = null;
            RawInputDevice wiimote2kb = null;
            RawInputDevice wiimote3kb = null;
            RawInputDevice wiimote4kb = null;

            if (gunCount < 1)       // Return if no gun or mouse is connected !
                return false;

            SimpleLogger.Instance.Info("[GUNS] Found " + gunCount + " usable guns.");

            // Gun indexes
            gun1 = orggun1 = SetGun(guns, gunCount, 1);
            gun2 = orggun2 = SetGun(guns, gunCount, 2);
            gun3 = orggun3 = SetGun(guns, gunCount, 3);
            gun4 = orggun4 = SetGun(guns, gunCount, 4);

            if (GunIndexOverride(guns, 1, out int newIndex1))
                gun1 = guns[newIndex1];
            if (GunIndexOverride(guns, 2, out int newIndex2))
                gun2 = guns[newIndex2];
            if (GunIndexOverride(guns, 3, out int newIndex3))
                gun3 = guns[newIndex3];
            if (GunIndexOverride(guns, 4, out int newIndex4))
                gun4 = guns[newIndex4];

            // logs
            if (gun1 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 1: " + gun1.DevicePath.ToString());
            if (gun2 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 2: " + gun2.DevicePath.ToString());
            if (gun3 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 3: " + gun3.DevicePath.ToString());
            if (gun4 != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 4: " + gun4.DevicePath.ToString());

            // Demulshooter
            _demulshooter = Program.SystemConfig.getOptBoolean("use_demulshooter");
            if (_demulshooter)
            {
                string profileName = Path.GetFileNameWithoutExtension(userProfile.FileName);
                SimpleLogger.Instance.Info("[GUNS] Checking game compatibility for: " + profileName);

                if (Demulshooter.teknoParrotGames.ContainsKey(profileName))
                {
                    SimpleLogger.Instance.Info("[GUNS] Game is compatible with DemulShooter, disabling native controls.");
                    Demulshooter.StartDemulshooter("teknoparrot", "teknoparrot", rom, gun1, gun2, gun3, gun4);
                }
                else
                    SimpleLogger.Instance.Info("[GUNS] Game not found in DemulShooter compatibility list: " + profileName);
            }

            // Get keyboards
            var hidDevices = RawInputDevice.GetRawInputDevices();
            var keyboards = hidDevices.Where(t => t.Type == RawInputDeviceType.Keyboard).OrderBy(u => u.DevicePath).ToList();
            if (keyboards.Count > 0)
            {
                int kbCount = keyboards.Count;
                SimpleLogger.Instance.Info("[GUNS] Found " + kbCount + " usable keyboards.");
                keyboard = keyboards[0];
            }

            // Perform original assignment of keyboards to wiimotes
            if (orggun1 != null && orggun1.Type == RawLighGunType.MayFlashWiimote)
            {
                wiimote1kb = FindAssociatedKeyboard(orggun1.DevicePath, keyboards, keyboard);
                _gunsKbAssociation.Add(orggun1, wiimote1kb);
            }
            if (orggun2 != null && orggun2.Type == RawLighGunType.MayFlashWiimote)
            {
                wiimote2kb = FindAssociatedKeyboard(orggun2.DevicePath, keyboards, keyboard);
                _gunsKbAssociation.Add(orggun2, wiimote2kb);
            }
            if (orggun3 != null && orggun3.Type == RawLighGunType.MayFlashWiimote)
            {
                wiimote3kb = FindAssociatedKeyboard(orggun3.DevicePath, keyboards, keyboard);
                _gunsKbAssociation.Add(orggun3, wiimote3kb);
            }
            if (orggun4 != null && orggun4.Type == RawLighGunType.MayFlashWiimote)
            {
                wiimote4kb = FindAssociatedKeyboard(orggun4.DevicePath, keyboards, keyboard);
                _gunsKbAssociation.Add(orggun4, wiimote4kb);
            }

            if (_gunsKbAssociation.Count > 1 && Program.SystemConfig.getOptBoolean("WiimoteKbOrder"))
            {
                var enumerator = _gunsKbAssociation.GetEnumerator();

                enumerator.MoveNext();
                var key1 = enumerator.Current.Key;
                var value1 = enumerator.Current.Value;

                enumerator.MoveNext();
                var key2 = enumerator.Current.Key;
                var value2 = enumerator.Current.Value;

                if (value1 != null && value2 != null)
                {
                    _gunsKbAssociation.Remove(key1);
                    _gunsKbAssociation.Remove(key2);
                    _gunsKbAssociation.Add(key1, value2);
                    _gunsKbAssociation.Add(key2, value1);
                }
            }

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
            int kbs = 1;
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
                        SimpleLogger.Instance.Info("[GUNS] Identified keyboard " + kbs + " with name: " + desc1 + " and path:" + k.DevicePath);
                    }
                }
                catch { SimpleLogger.Instance.Info("[GUNS] Cannot get friendly name for Keyboard " + kbs); }
                kbs++;
            }

            if (keyboard != null && keyboard.FriendlyName != null)
            {
                SimpleLogger.Instance.Info("[GUNS] Using keyboard: " + keyboard.FriendlyName);
                _orgKeyboard = keyboard;
            }
            
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
            string tpMappingyml = Controller.GetSystemYmlMappingFile("teknoparrot", "", "teknoparrot", mappingPaths);
            var buttonMap = new Dictionary<string, string>();
            string gameName = null;
            

            if (tpMappingyml != null && File.Exists(tpMappingyml))
            {
                SimpleLogger.Instance.Info("[GUNS] mapping file found, searching game mapping.");

                YmlFile ymlFile = YmlFile.Load(tpMappingyml);
                string gunGameName = tpGameName + "_gun";
                game = ymlFile.Elements.Where(g => g.Name == gunGameName).FirstOrDefault() as YmlContainer;
                
                if (game == null)
                    game = ymlFile.Elements.Where(g => g.Name == tpGameName).FirstOrDefault() as YmlContainer;
                
                if (game != null)
                {
                    SimpleLogger.Instance.Info("[GUNS] mapping found for the game, retrieving buttons.");
                    gameName = game.Name;
                    foreach (var buttonEntry in game.Elements.Where(t => t.GetType().Name == "YmlElement"))
                    {
                        YmlElement button = buttonEntry as YmlElement;

                        if (button.Value == null)
                            continue;

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
                string gunID = null;
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
                bool wiimoteGun = Program.SystemConfig.isOptSet("WiimoteMode") && Program.SystemConfig["WiimoteMode"] == "wiimotegun";
                bool wiimoteGaming = Program.SystemConfig.isOptSet("WiimoteMode") && Program.SystemConfig["WiimoteMode"] == "game";
                bool wiimoteNormal = Program.SystemConfig.isOptSet("WiimoteMode") && Program.SystemConfig["WiimoteMode"] == "normal";

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

                            bool enumExists;
                            enumExists = Enum.TryParse(button.Key, out InputMapping inputEnum);
                            
                            if (button.Key.StartsWith("mouse"))
                                enumExists = Enum.TryParse(button.Value, out inputEnum);
                            
                            if (enumExists)
                                xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnum && !j.HideWithRawInput);
                            if (xmlPlace == null)
                                continue;

                            if (i == 1 && P1exclude.Any(v => xmlPlace.ButtonName.ToLowerInvariant().Contains(v.ToLowerInvariant())))
                                continue;
                            else if (i == 2 && !P2include.Any(v => xmlPlace.ButtonName.ToLowerInvariant().Contains(v.ToLowerInvariant())))
                                continue;
                            else if (i == 3 && !P3include.Any(v => xmlPlace.ButtonName.ToLowerInvariant().Contains(v.ToLowerInvariant())))
                                continue;
                            else if (i == 4 && !P4include.Any(v => xmlPlace.ButtonName.ToLowerInvariant().Contains(v.ToLowerInvariant())))
                                continue;

                            if (button.Value.StartsWith("kb_") && keyboard != null)
                            {
                                if (iGun.Type == RawLighGunType.Mouse)
                                {
                                    kbName = keyboard.FriendlyName;
                                    kbSuffix = keyboard.Manufacturer;

                                    if (wiimoteGun)
                                    {
                                        xmlPlace.RawInputButton = new RawInputButton
                                        {
                                            DevicePath = "null",
                                            DeviceType = RawDeviceType.Keyboard,
                                            MouseButton = RawMouseButton.None
                                        };
                                    }

                                    else
                                    {
                                        xmlPlace.RawInputButton = new RawInputButton
                                        {
                                            DevicePath = keyboard.DevicePath.ToString(),
                                            DeviceType = RawDeviceType.Keyboard,
                                            MouseButton = RawMouseButton.None
                                        };
                                    }

                                    string kbkey = button.Value.Split('_')[1];
                                    if (Numbers.Contains(kbkey))
                                        kbkey = "D" + kbkey;

                                    if (wiimoteGun)
                                    {
                                        if (kbkey == "D1")
                                        {
                                            xmlPlace.RawInputButton.KeyboardKey = Keys.ControlKey;
                                            xmlPlace.BindName = xmlPlace.BindNameRi = "Unknown Device ControlKey";
                                        }
                                        else if (kbkey == "D5")
                                        {
                                            xmlPlace.RawInputButton.KeyboardKey = Keys.Return;
                                            xmlPlace.BindName = xmlPlace.BindNameRi = "Unknown Device Return";
                                        }
                                        else
                                        {
                                            if (Enum.TryParse(kbkey, true, out Keys key))
                                                xmlPlace.RawInputButton.KeyboardKey = key;
                                            
                                            xmlPlace.BindName = xmlPlace.BindNameRi = "Unknown Device " + key.ToString();
                                        }
                                    }
                                    else
                                    {
                                        if (Enum.TryParse(kbkey, true, out Keys key))
                                            xmlPlace.RawInputButton.KeyboardKey = key;

                                        string kbNameOverride = null;
                                        string deviceToOverride = GetVIDPID(keyboard.DevicePath);
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
                                else if (iGun.Type == RawLighGunType.MayFlashWiimote)
                                {
                                    bool ts_nogun = false;
                                    if (st_kb && (button.Key.ToLowerInvariant().Contains("service") || button.Key.ToLowerInvariant().Contains("test")))
                                        ts_nogun = true;

                                    // Find keyboard associated to lightgun
                                    if (!useKb && !ts_nogun)
                                        keyboard = _gunsKbAssociation.ContainsKey(iGun) ? _gunsKbAssociation[iGun] : FindAssociatedKeyboard(iGun.DevicePath, keyboards, keyboard);

                                    string wiiButton = button.Value;
                                    // Use normal keyboard
                                    if (!WiiKBKeys.Contains(wiiButton) || useKb || ts_nogun)
                                    {
                                        var tempKb = _orgKeyboard;

                                        xmlPlace.RawInputButton = new RawInputButton
                                        {
                                            DevicePath = tempKb.DevicePath.ToString(),
                                            DeviceType = RawDeviceType.Keyboard,
                                            MouseButton = RawMouseButton.None
                                        };

                                        string kbkey = button.Value.Split('_')[1];
                                        if (Enum.TryParse(kbkey, true, out Keys key))
                                            xmlPlace.RawInputButton.KeyboardKey = key;

                                        kbName = tempKb.FriendlyName;
                                        kbSuffix = tempKb.Manufacturer;
                                        string kbNameOverride = null;
                                        string deviceToOverride = GetVIDPID(tempKb.DevicePath);
                                        string overridePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                                        string newName = GetDescriptionFromFile(overridePath, deviceToOverride);
                                        if (newName != null)
                                            kbNameOverride = newName;

                                        if (kbNameOverride != null)
                                            xmlPlace.BindName = xmlPlace.BindNameRi = kbNameOverride + " " + key.ToString();
                                        else
                                            xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " " + key.ToString();

                                        continue;
                                    }

                                    // Get Keyboard ID
                                    int firstIndex = keyboard.DevicePath.IndexOf('#');
                                    int secondIndex = keyboard.DevicePath.IndexOf('#', firstIndex + 1);
                                    secondIndex += 3;
                                    int lastIndex = keyboard.DevicePath.IndexOf('&', secondIndex + 1);

                                    kbName = keyboard.DevicePath.Substring(secondIndex, lastIndex - secondIndex).ToUpperInvariant();
                                    kbSuffix = "Mayflash DolphinBar";

                                    xmlPlace.RawInputButton = new RawInputButton
                                    {
                                        DevicePath = keyboard.DevicePath.ToString(),
                                        DeviceType = RawDeviceType.Keyboard,
                                        MouseButton = RawMouseButton.None
                                    };

                                    switch (wiiButton)
                                    {
                                        case "kb_1":
                                        case "kb_2":
                                        case "kb_3":
                                        case "kb_4":
                                            if (wiimoteGaming)
                                            {
                                                xmlPlace.RawInputButton.KeyboardKey = Keys.Return;
                                                xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " Return";
                                            }
                                            else
                                            {
                                                xmlPlace.RawInputButton.KeyboardKey = Keys.Up;
                                                xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " Up";
                                            }
                                            break;
                                        case "kb_5":
                                        case "kb_6":
                                        case "kb_7":
                                        case "kb_8":
                                            if (wiimoteGaming)
                                            {
                                                xmlPlace.RawInputButton.KeyboardKey = Keys.VolumeUp;
                                                xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " VolumeUp";
                                            }
                                            else
                                            {
                                                xmlPlace.RawInputButton.KeyboardKey = Keys.Down;
                                                xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " Down";
                                            }
                                            break;
                                        default:
                                            string kbkey = button.Value.Split('_')[1];
                                            if (Numbers.Contains(kbkey))
                                                kbkey = "D" + kbkey;

                                            if (Enum.TryParse(kbkey, true, out Keys key))
                                                xmlPlace.RawInputButton.KeyboardKey = key;

                                            xmlPlace.RawInputButton.KeyboardKey = key;
                                            xmlPlace.BindName = xmlPlace.BindNameRi = kbSuffix + " " + kbName + " " + kbkey;
                                            break;
                                    }
                                }
                                else
                                {
                                    bool ts_nogun = false;
                                    if (st_kb && (button.Key.ToLowerInvariant().Contains("service") || button.Key.ToLowerInvariant().Contains("test")))
                                        ts_nogun = true;

                                    // Find keyboard associated to lightgun
                                    int startIndex = iGun.DevicePath.IndexOf("VID");
                                    if (startIndex >= 0 && !useKb && !ts_nogun)
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

                                        if (keyboards.Any(k => k.DevicePath.Contains(searchPath)))
                                            keyboard = keyboards.FirstOrDefault(k => k.DevicePath.Contains(searchPath));
                                        else
                                        {
                                            searchPath = iGun.DevicePath.Substring(startIndex, endIndex - startIndex - 5);
                                            if (keyboards.Any(k => k.DevicePath.Contains(searchPath)))
                                                keyboard = keyboards.FirstOrDefault(k => k.DevicePath.Contains(searchPath));
                                        }
                                    }

                                    if (_orgKeyboard != null && (useKb || ts_nogun))
                                    {
                                        keyboard = _orgKeyboard;
                                        kbName = keyboard.Name;
                                        kbSuffix = keyboard.Manufacturer;
                                    }
                                    else
                                    {
                                        kbName = iGun.Name;
                                        kbSuffix = iGun.Manufacturer;
                                    }

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
                                    string deviceToOverride = GetVIDPID(keyboard.DevicePath);
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
                                if (iGun.Type == RawLighGunType.Mouse && wiimoteGun)
                                {
                                    string mouseButton = button.Key.ToLowerInvariant();
                                    if (mouseButton.StartsWith("mouseleft")) mouseButton = "LeftButton";
                                    else if (mouseButton.StartsWith("mousemiddle")) mouseButton = "MiddleButton";
                                    else if (mouseButton.StartsWith("mouseright")) mouseButton = "RightButton";

                                    xmlPlace.RawInputButton = new RawInputButton
                                    {
                                        DevicePath = "null",
                                        DeviceType = RawDeviceType.Mouse
                                    };

                                    if (Enum.TryParse(mouseButton, true, out RawMouseButton mbtn))
                                        xmlPlace.RawInputButton.MouseButton = mbtn;

                                    xmlPlace.RawInputButton.KeyboardKey = Keys.None;

                                    xmlPlace.BindName = xmlPlace.BindNameRi = "Unknown Device " + mouseButton;
                                }
                                
                                else if (iGun.Type == RawLighGunType.MayFlashWiimote)
                                {
                                    bool gunInvert = Program.SystemConfig.getOptBoolean("gun_invert");
                                    string mouseButton = button.Key.ToLowerInvariant();
                                   
                                    // Find keyboard associated to lightgun
                                    keyboard = _gunsKbAssociation.ContainsKey(iGun) ? _gunsKbAssociation[iGun] : FindAssociatedKeyboard(iGun.DevicePath, keyboards, keyboard);

                                    // Get Keyboard ID
                                    int kbfirstIndex = keyboard.DevicePath.IndexOf('#');
                                    int kbsecondIndex = keyboard.DevicePath.IndexOf('#', kbfirstIndex + 1);
                                    kbsecondIndex += 3;
                                    int kblastIndex = keyboard.DevicePath.IndexOf('&', kbsecondIndex + 1);

                                    kbName = keyboard.DevicePath.Substring(kbsecondIndex, kblastIndex - kbsecondIndex).ToUpperInvariant();

                                    // Get wiimote ID
                                    int mousefirstIndex = iGun.DevicePath.IndexOf('#');
                                    int mouse2index = iGun.DevicePath.IndexOf('#', mousefirstIndex + 1);
                                    mouse2index += 3;
                                    int endIndex = iGun.DevicePath.IndexOf('&', mouse2index + 1);
                                    string WiimoteID = iGun.DevicePath.Substring(mouse2index, endIndex - mouse2index).ToUpperInvariant();
                                    gunID = WiimoteID;

                                    if (mouseButton.StartsWith("mouseleft")) 
                                        mouseButton = gunInvert ? "RightButton" : "LeftButton";
                                    else if (mouseButton.StartsWith("mouseright")) 
                                        mouseButton = gunInvert ? "LeftButton" : "RightButton";

                                    if (mouseButton.StartsWith("mousemiddle") && wiimoteGaming)
                                    {
                                        mouseButton = "VolumeDown";

                                        xmlPlace.RawInputButton = new RawInputButton
                                        {
                                            DevicePath = keyboard.DevicePath.ToString(),
                                            DeviceType = RawDeviceType.Keyboard,
                                            MouseButton = RawMouseButton.None,
                                            KeyboardKey = Keys.VolumeDown,
                                        };
                                        xmlPlace.BindName = xmlPlace.BindNameRi = "Mayflash DolphinBar " + kbName + " VolumeDown";
                                    }
                                    else if (mouseButton.StartsWith("mousemiddle"))
                                    {
                                        mouseButton = "PageUp";

                                        xmlPlace.RawInputButton = new RawInputButton
                                        {
                                            DevicePath = keyboard.DevicePath.ToString(),
                                            DeviceType = RawDeviceType.Keyboard,
                                            MouseButton = RawMouseButton.None,
                                            KeyboardKey = Keys.PageUp,
                                        };
                                        xmlPlace.BindName = xmlPlace.BindNameRi = "Mayflash DolphinBar " + kbName + " PageUp";
                                    }
                                    else
                                    {
                                        xmlPlace.RawInputButton = new RawInputButton
                                        {
                                            DevicePath = iGun.DevicePath.ToString(),
                                            DeviceType = RawDeviceType.Mouse,
                                        };
                                        if (Enum.TryParse(mouseButton, true, out RawMouseButton mbtn))
                                            xmlPlace.RawInputButton.MouseButton = mbtn;

                                        xmlPlace.RawInputButton.KeyboardKey = Keys.None;

                                        xmlPlace.BindName = xmlPlace.BindNameRi = "Mayflash DolphinBar" + " " + WiimoteID + " " + mouseButton;
                                    }
                                }
                                else
                                {
                                    string mouseButton = button.Key.ToLowerInvariant();
                                    if (mouseButton.StartsWith("mouseleft")) mouseButton = "LeftButton";
                                    else if (mouseButton.StartsWith("mousemiddle")) mouseButton = "MiddleButton";
                                    else if (mouseButton.StartsWith("mouseright")) mouseButton = "RightButton";
                                    else if (mouseButton.StartsWith("mousebutton4")) mouseButton = "Button4";
                                    else if (mouseButton.StartsWith("mousebutton5")) mouseButton = "Button5";

                                    xmlPlace.RawInputButton = new RawInputButton
                                    {
                                        DevicePath = iGun.DevicePath.ToString(),
                                        DeviceType = RawDeviceType.Mouse
                                    };

                                    if (Enum.TryParse(mouseButton, true, out RawMouseButton mbtn))
                                        xmlPlace.RawInputButton.MouseButton = mbtn;

                                    xmlPlace.RawInputButton.KeyboardKey = Keys.None;

                                    string mouseNameOverride = null;
                                    string deviceToOverride = GetVIDPID(iGun.DevicePath);
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
                }

                // Add lightgun section
                JoystickButtons xmlLightgun = null;
                string searchLG = "P" + i + "LightGun";
                bool lgEnumExists = Enum.TryParse(searchLG, out InputMapping inputEnumLG);

                if (lgEnumExists)
                {
                    xmlLightgun = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnumLG && !j.HideWithRawInput);

                    if (iGun.Type == RawLighGunType.Mouse && wiimoteGun)
                    {
                        xmlLightgun.RawInputButton = new RawInputButton
                        {
                            DevicePath = "null",
                            DeviceType = RawDeviceType.Mouse,
                            KeyboardKey = Keys.None
                        };

                        xmlLightgun.BindName = xmlLightgun.BindNameRi = "Unknown Device";
                    }

                    else
                    {
                        xmlLightgun.RawInputButton = new RawInputButton
                        {
                            DevicePath = iGun.DevicePath.ToString(),
                            DeviceType = RawDeviceType.Mouse,
                            KeyboardKey = Keys.None
                        };

                        if (iGun.Type == RawLighGunType.MayFlashWiimote && gunID != null)
                        {
                            xmlLightgun.BindName = xmlLightgun.BindNameRi = "Mayflash DolphinBar" + " " + gunID;
                        }
                        else
                            xmlLightgun.BindName = xmlLightgun.BindNameRi = iGun.Manufacturer + " " + iGun.Name;
                    }
                }

                var inputAPI = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Input API");
                if (inputAPI != null)
                {
                    if (inputAPI.FieldOptions != null && inputAPI.FieldOptions.Any(f => f == "RawInput"))
                        inputAPI.FieldValue = "RawInput";
                }
            }

            return true;
        }

        private readonly static List<string> Numbers = new List<string>
        { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private readonly static List<string> WiiKBKeys = new List<string>
        { "kb_1", "kb_2", "kb_3", "kb_4", "kb_5", "kb_6", "kb_7", "kb_8", "kb_Up", "kb_Down", "kb_Left", "kb_Right", "kb_left", "kb_right", "kb_up", "kb_down" };

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

        private static RawLightgun SetGun(RawLightgun[] guns, int gunCount, int number)
        {
            if (gunCount > 3)
            {
                switch (number)
                {
                    case 1: return guns[0];
                    case 2: return guns[1];
                    case 3: return guns[2];
                    case 4: return guns[3];
                }
            }
            else if (gunCount > 2)
            {
                switch (number)
                {
                    case 1: return guns[0];
                    case 2: return guns[1];
                    case 3: return guns[2];
                }
            }

            else if (gunCount > 1)
            {
                switch (number)
                {
                    case 1: return guns[0];
                    case 2: return guns[1];
                }
            }
            else
            {
                switch (number)
                {
                    case 1: return guns[0];
                }
            }
            return null;
        }

        private static bool GunIndexOverride(RawLightgun[] guns, int index, out int newIndex)
        {
            newIndex = -1;
            string gunindex = "tp_gunindex" + index;
            if (Program.SystemConfig.isOptSet(gunindex) && !string.IsNullOrEmpty(Program.SystemConfig[gunindex]))
            {
                newIndex = Program.SystemConfig[gunindex].ToInteger();
                if (guns.Length > newIndex)
                    return true;
                else
                {
                    SimpleLogger.Instance.Info("[GUNS] Cannot override index for Gun " + index);
                    return false;
                }
            }
            return false;
        }

        private static string GetVIDPID(string path)
        {
            bool acpi = false;
            int vidIndex = path.IndexOf("VID");
            if (vidIndex < 0)
            {
                vidIndex = path.IndexOf("ACPI") + 5;
                if (vidIndex < 0)
                    return "";
                else
                    acpi = true;
            }

            if (vidIndex < 0)
                return "";
            
            int pidIndex = path.IndexOf("PID");
            if (pidIndex < 0 & acpi)
                pidIndex = path.IndexOf("#", vidIndex + 5);
            if (pidIndex < 0)
                return "";

            int endindex = acpi ? path.IndexOf("#", pidIndex) : path.IndexOf("&", pidIndex);
            if (endindex < 0)
                return path.Substring(vidIndex, path.Length - vidIndex);
            else
                return path.Substring(vidIndex, endindex - vidIndex);
        }

        private static string GetWiimoteVIDPID(string devicePath)
        {
            try
            {
                string[] parts = devicePath.Split('#');
                if (parts.Length < 3)
                    return null;

                string[] vidPidParts = parts[1].Split('&');
                string vidPid = $"{vidPidParts[0]}&{vidPidParts[1]}"; // Only take VID and PID

                string partAfterSecondHash = parts[2];
                char characterAfterSecondHash = partAfterSecondHash[0];

                return vidPid;
            }
            catch
            {
                return null;
            }
        }

        private static string GetWiimoteAssociationChar(string devicePath)
        {
            try
            {
                string[] parts = devicePath.Split('#');
                if (parts.Length < 3)
                    return "";

                string partAfterSecondHash = parts[2];
                char characterAfterSecondHash = partAfterSecondHash[0]; // First character

                return characterAfterSecondHash.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static RawInputDevice FindAssociatedKeyboard(string gunPath, List<RawInputDevice> keyboards, RawInputDevice keyboard)
        {
            string mouseVIDPID = GetWiimoteVIDPID(gunPath);
            string mouseChar = GetWiimoteAssociationChar(gunPath);
            string toSearch = mouseVIDPID + "_" + mouseChar;
            List<RawInputDevice> kbToIgnore = new List<RawInputDevice>();

            if (_gunsKbAssociation.Any(g => g.Key.DevicePath == gunPath))
            {
                var keyPair = _gunsKbAssociation.FirstOrDefault(g => g.Key.DevicePath == gunPath);
                keyboard = keyPair.Value;
                return keyboard;
            }
            else if (_gunsKbAssociation.Count > 0)
            {
                foreach (var pair in _gunsKbAssociation)
                {
                    kbToIgnore.Add(pair.Value);
                }
            }

            foreach (var kb in keyboards)
            {
                if (kbToIgnore.Contains(kb))
                {
                    continue;
                }
                
                string kbVIDPID = GetWiimoteVIDPID(kb.DevicePath);
                string kbChar = GetWiimoteAssociationChar(kb.DevicePath);

                if (kbVIDPID != null && kbChar != null)
                {
                    string toFind = kbVIDPID + "_" + kbChar;
                    if (toSearch.ToLowerInvariant() == toFind.ToLowerInvariant())
                    {
                        return kb;
                    }
                }
            }
            return keyboard;
        }
    }
}
