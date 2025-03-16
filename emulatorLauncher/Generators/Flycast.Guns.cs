using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Management;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common;
using System;

namespace EmulatorLauncher
{
    partial class FlycastGenerator
    {
        private bool _demulshooter = false;
        private static readonly List<string> reloadWithButtonB = new List<string>
        { "confmiss", "hotd2", "hotd2e", "hotd2o", "hotd2p", "manicpnc", "mok", "otrigger", "tduno", "tduno2", "zunou", "claychal",
            "rangrmsn", "sprtshot", "waidrive", "xtrmhnt2", "xtrmhunt" };
        private static readonly List<string> useXandB = new List<string> { "kick4csh" };
        private static readonly List<string> useXandA = new List<string> { "shootopl", "shootpl", "shootplm", "shootplmp" };

        private void ConfigureFlycastGuns(IniFile ini, string mappingPath, string system)
        {
            bool useOneGun = SystemConfig.getOptBoolean("one_gun");
            bool guninvert = SystemConfig.getOptBoolean("gun_invert");
            bool gunindexrevert = SystemConfig.getOptBoolean("gun_index_revert");
            bool multigun = false;
            if (SystemConfig["flycast_controller1"] == "7" && SystemConfig["flycast_controller2"] == "7")
                multigun = true;

            var guns = RawLightgun.GetRawLightguns();

            if (guns.Length < 1)
                return;
            else
                SimpleLogger.Instance.Info("[GUNS] Found " + guns.Length + " usable guns.");

            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }

            // If DemulShooter is enabled, configure it
            if (SystemConfig.getOptBoolean("use_demulshooter"))
            {
                _demulshooter = true;
                SimpleLogger.Instance.Info("[INFO] Configuring DemulShooter");
                var gun1 = guns.Length > 0 ? guns[0] : null;
                var gun2 = guns.Length > 1 ? guns[1] : null;

                if (gunindexrevert)
                {
                    if (guns.Length > 1)
                    {
                        gun1 = guns[1];
                        gun2 = guns[2];
                    }
                }

                Demulshooter.StartDemulshooter("flycast", system, _romName, gun1, gun2);
                return;
            }

            // Get mapping in yml file
            YmlContainer game = null;
            var buttonMap = new Dictionary<string, string>();

            string flycastMapping = null;

            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "inputmapping");

                flycastMapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(flycastMapping))
                    break;
            }

            if (File.Exists(flycastMapping))
            {
                YmlFile ymlFile = YmlFile.Load(flycastMapping);

                game = ymlFile.Elements.Where(c => c.Name == _romName).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => _romName.StartsWith(g.Name)).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => g.Name == "default_" + system).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;

                if (game != null)
                {
                    var gameName = game.Name;


                    foreach (var buttonEntry in game.Elements)
                    {
                        if (buttonEntry is YmlElement button)
                            buttonMap.Add(button.Name, button.Value);
                    }
                }
            }
            bool useFileMapping = buttonMap != null && buttonMap.Count > 0;
                
            // Get keyboards
            RawInputDevice keyboard = null;
            var hidDevices = RawInputDevice.GetRawInputDevices();
            var keyboards = hidDevices.Where(t => t.Type == RawInputDeviceType.Keyboard).OrderBy(u => u.DevicePath).ToList();
            if (keyboards.Count > 0)
            {
                int kbCount = keyboards.Count;
                SimpleLogger.Instance.Info("[GUNS] Found " + kbCount + " keyboards.");
                keyboard = keyboards[0];
            }

            if (guns.Length > 1 && !useOneGun)
                multigun = true;

            ini.WriteValue("input", "maple_sdl_mouse", "0");

            string mappingFile = Path.Combine(mappingPath, "SDL_Default Mouse.cfg");
            if (_isArcade)
                mappingFile = Path.Combine(mappingPath, "SDL_Default Mouse_arcade.cfg");

            string kbmappingFile = Path.Combine(mappingPath, "SDL_Keyboard.cfg");
            if (_isArcade)
                kbmappingFile = Path.Combine(mappingPath, "SDL_Keyboard_arcade.cfg");

            if (File.Exists(mappingFile))
                File.Delete(mappingFile);
            if (File.Exists(kbmappingFile))
                File.Delete(kbmappingFile);

            // SDL region (only 1 gun)
            using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
            {
                // Mouse binds
                if (SystemConfig.isOptSet("flycast_mousegunbuttons") && !string.IsNullOrEmpty(SystemConfig["flycast_mousegunbuttons"]))
                {
                    string[] buttons = SystemConfig["flycast_mousegunbuttons"].Split('_');
                    Dictionary<string, string> mouseBMap = new Dictionary<string, string>();

                    if (buttons.Length > 2)
                    {
                        mouseBMap.Add("bind0", buttons[0]);
                        mouseBMap.Add("bind1", buttons[1]);
                        mouseBMap.Add("bind2", buttons[2]);
                    }
                    else if (buttons.Length > 1)
                    {
                        mouseBMap.Add("bind0", buttons[0]);
                        mouseBMap.Add("bind1", buttons[1]);
                        mouseBMap.Add("bind2", "3:btn_start");
                    }
                    else if (buttons.Length > 0)
                    {
                        mouseBMap.Add("bind0", buttons[0]);
                        mouseBMap.Add("bind1", guninvert ? "2:reload" : "2:btn_a");
                        mouseBMap.Add("bind2", "3:btn_start");
                    }

                    int i = 1;
                    foreach (var mouseB in mouseBMap)
                    {
                        string mvalue = mouseB.Value;

                        switch (mvalue)
                        {
                            case "a":
                                mvalue = i + ":btn_a";
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;
                            case "r":
                                mvalue = i + ":reload";
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;
                            case "s":
                                mvalue = i + ":btn_start";
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;
                            case "x":
                                mvalue = i + ":btn_x";
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;
                            case "b":
                                mvalue = i + ":btn_b";
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;
                            case "y":
                                mvalue = i + ":btn_y";
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;  
                            default:
                                ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                break;
                        }
                        i++;
                    }
                }
                else
                {
                    if (useXandB.Contains(_romName))
                    {
                        ctrlini.WriteValue("digital", "bind0", "1:btn_b");
                        ctrlini.WriteValue("digital", "bind1", "2:btn_x");
                        ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                    }
                    else if (useXandA.Contains(_romName))
                    {
                        ctrlini.WriteValue("digital", "bind0", "1:btn_a");
                        ctrlini.WriteValue("digital", "bind1", "2:btn_x");
                        ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                    }
                    else
                    {
                        if (reloadWithButtonB.Contains(_romName))
                        {
                            ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:btn_b");
                            ctrlini.WriteValue("digital", "bind1", guninvert ? "2:btn_b" : "2:btn_a");
                        }
                        else
                        {
                            ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:reload");
                            ctrlini.WriteValue("digital", "bind1", guninvert ? "2:reload" : "2:btn_a");
                        }

                        ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                    }
                }

                ctrlini.WriteValue("emulator", "dead_zone", "10");
                ctrlini.WriteValue("emulator", "mapping_name", "Mouse");
                ctrlini.WriteValue("emulator", "rumble_power", "100");
                ctrlini.WriteValue("emulator", "version", "3");
                ctrlini.Save();
            }

            using (var kbini = new IniFile(kbmappingFile, IniOptions.UseSpaces))
            {
                if (guns[0].Type == RawLighGunType.MayFlashWiimote)
                {
                    if (SystemConfig.isOptSet("WiimoteMode") && !string.IsNullOrEmpty(SystemConfig["WiimoteMode"]))
                    {
                        string wiiMode = SystemConfig["WiimoteMode"];

                        if (wiiMode == "game")
                        {
                            kbini.WriteValue("digital", "bind0", "40:btn_start");
                            kbini.WriteValue("digital", "bind1", "41:btn_d");
                            kbini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                            kbini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                            kbini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                            kbini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                            kbini.WriteValue("digital", "bind6", "128:btn_a");
                            kbini.WriteValue("digital", "bind7", "129:btn_b");
                            kbini.WriteValue("digital", "bind8", "43:btn_menu");
                            kbini.WriteValue("digital", "bind9", "59:btn_jump_state");      //F2
                            kbini.WriteValue("digital", "bind10", "66:btn_screenshot");     //F9
                            kbini.WriteValue("digital", "bind11", "61:btn_fforward");       //F4
                            kbini.WriteValue("digital", "bind12", "58:btn_quick_save");     //F1
                            kbini.WriteValue("digital", "bind13", "20:btn_x");              //Q
                        }
                        else if (wiiMode == "normal")
                        {
                            kbini.WriteValue("digital", "bind0", "75:btn_start");
                            kbini.WriteValue("digital", "bind1", "78:btn_d");
                            kbini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                            kbini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                            kbini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                            kbini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                            kbini.WriteValue("digital", "bind6", "43:btn_menu");
                            kbini.WriteValue("digital", "bind7", "224:btn_b");              //Leftctrl
                            kbini.WriteValue("digital", "bind8", "225:btn_a");              //Leftshift
                            kbini.WriteValue("digital", "bind9", "59:btn_jump_state");      //F2
                            kbini.WriteValue("digital", "bind10", "66:btn_screenshot");     //F9
                            kbini.WriteValue("digital", "bind11", "61:btn_fforward");       //F4
                            kbini.WriteValue("digital", "bind12", "58:btn_quick_save");     //F1
                            kbini.WriteValue("digital", "bind13", "20:btn_x");              //Q
                        }
                        else
                        {
                            kbini.WriteValue("digital", "bind0", "30:btn_start");
                            kbini.WriteValue("digital", "bind1", "34:btn_d");
                            kbini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                            kbini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                            kbini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                            kbini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                            kbini.WriteValue("digital", "bind6", "43:btn_menu");
                            kbini.WriteValue("digital", "bind7", "224:btn_b");
                            kbini.WriteValue("digital", "bind8", "225:btn_a");
                            kbini.WriteValue("digital", "bind9", "59:btn_jump_state");      //F2
                            kbini.WriteValue("digital", "bind10", "66:btn_screenshot");     //F9
                            kbini.WriteValue("digital", "bind11", "61:btn_fforward");       //F4
                            kbini.WriteValue("digital", "bind12", "58:btn_quick_save");     //F1
                            kbini.WriteValue("digital", "bind13", "20:btn_x");              //Q
                        }
                    }
                }
                else if (guns[0].Type == RawLighGunType.Mouse && SystemConfig.isOptSet("WiimoteMode") && SystemConfig["WiimoteMode"] == "wiimotegun")
                {
                    kbini.WriteValue("digital", "bind0", "26:btn_a");
                    kbini.WriteValue("digital", "bind1", "40:btn_d");
                    kbini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                    kbini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                    kbini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                    kbini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                    kbini.WriteValue("digital", "bind6", "228:btn_start");
                    kbini.WriteValue("digital", "bind7", "43:btn_menu");
                    kbini.WriteValue("digital", "bind8", "224:btn_b");
                    kbini.WriteValue("digital", "bind9", "59:btn_jump_state");   //F2
                    kbini.WriteValue("digital", "bind10", "66:btn_screenshot");   //F9
                    kbini.WriteValue("digital", "bind11", "61:btn_fforward");      //F4
                    kbini.WriteValue("digital", "bind12", "58:btn_quick_save");    //F1
                    kbini.WriteValue("digital", "bind13", "20:btn_x");              //Q
                }
                else
                {
                    kbini.WriteValue("digital", "bind0", "30:btn_start");
                    kbini.WriteValue("digital", "bind1", "34:btn_d");
                    kbini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                    kbini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                    kbini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                    kbini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                    kbini.WriteValue("digital", "bind6", "43:btn_menu");
                    kbini.WriteValue("digital", "bind7", "224:btn_b");
                    kbini.WriteValue("digital", "bind8", "4:btn_a");
                    kbini.WriteValue("digital", "bind9", "59:btn_jump_state");   //F2
                    kbini.WriteValue("digital", "bind10", "66:btn_screenshot");   //F9
                    kbini.WriteValue("digital", "bind11", "61:btn_fforward");      //F4
                    kbini.WriteValue("digital", "bind12", "58:btn_quick_save");    //F1
                    kbini.WriteValue("digital", "bind13", "20:btn_x");              //Q
                }

                kbini.WriteValue("emulator", "dead_zone", "10");
                kbini.WriteValue("emulator", "mapping_name", "Keyboard");
                kbini.WriteValue("emulator", "rumble_power", "100");
                kbini.WriteValue("emulator", "version", "3");
                kbini.Save();
            }

            // Multigun : raw input
            if (multigun)
            {
                    
                RawLightgun lightgun1 = gunindexrevert ? guns[1] : guns[0];
                RawLightgun lightgun2 = gunindexrevert ? guns[0] : guns[1];

                RawInputDevice kb1 = null;
                RawInputDevice kb2 = null;
                RawInputDevice kb3 = null;

                if (lightgun1.Type == RawLighGunType.MayFlashWiimote)
                    kb1 = FindAssociatedKeyboardWiimote(lightgun1.DevicePath, keyboards, keyboard);
                else
                    kb1 = FindAssociatedKeyboard(keyboards, lightgun1);

                if (lightgun2.Type == RawLighGunType.MayFlashWiimote)
                    kb2 = FindAssociatedKeyboardWiimote(lightgun2.DevicePath, keyboards, keyboard);
                else
                    kb2 = FindAssociatedKeyboard(keyboards, lightgun2);

                if (kb1 != null && kb2 != null)
                    kb3 = keyboards.FirstOrDefault(k => k != kb1 && k != kb2);
                else if (kb1 != null)
                    kb3 = keyboards.FirstOrDefault(k => k != kb1);
                else if (kb2 != null)
                    kb3 = keyboards.FirstOrDefault(k => k != kb2);
                else
                    kb3 = keyboards.FirstOrDefault();

                List<RawInputDevice> kbdevices = new List<RawInputDevice>();
                if (kb1 != null)
                    kbdevices.Add(kb1);
                if (kb2 != null)
                    kbdevices.Add(kb2);
                if (kb3 != null)
                    kbdevices.Add(kb3);

                List<RawLightgun> mousedevices = new List<RawLightgun>
                {
                    lightgun1,
                    lightgun2
                };

                string cleanPath1 = "";
                string cleanPath2 = "";
                string cleanPath3 = "";
                string cleanPath4 = "";
                string cleanPath5 = "";

                Dictionary<RawLightgun, string> MouseDic = new Dictionary<RawLightgun, string>();
                Dictionary<RawInputDevice, string> kbDic = new Dictionary<RawInputDevice, string>();

                string devicepathHID1 = lightgun1.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                if (devicepathHID1.Length > 39)
                    cleanPath1 = devicepathHID1.Substring(0, devicepathHID1.Length - 39);
                string devicepathHID2 = lightgun2.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                if (devicepathHID2.Length > 39)
                    cleanPath2 = devicepathHID2.Substring(0, devicepathHID2.Length - 39);

                if (kb1 != null)
                {
                    string devicepathHID3 = kb1.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID3.Length > 39)
                        cleanPath3 = devicepathHID3.Substring(0, devicepathHID3.Length - 39);

                    string query3 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath3 + "'").Replace("\\", "\\\\");
                    ManagementObjectSearcher moSearch3 = new ManagementObjectSearcher(query3);
                    ManagementObjectCollection moCollection3 = moSearch3.Get();
                    foreach (ManagementObject mo in moCollection3.Cast<ManagementObject>())
                    {
                        string kb1desc = mo["Description"].ToString();
                        kbDic.Add(kb1, kb1desc);
                    }
                }
                if (kb2 != null)
                {
                    string devicepathHID4 = kb2.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID4.Length > 39)
                        cleanPath4 = devicepathHID4.Substring(0, devicepathHID4.Length - 39);

                    string query4 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath4 + "'").Replace("\\", "\\\\");
                    ManagementObjectSearcher moSearch4 = new ManagementObjectSearcher(query4);
                    ManagementObjectCollection moCollection4 = moSearch4.Get();
                    foreach (ManagementObject mo in moCollection4.Cast<ManagementObject>())
                    {
                        string kb2desc = mo["Description"].ToString();
                        kbDic.Add(kb2, kb2desc);
                    }
                }

                if (kb3 != null)
                {
                    string devicepathHID5 = kb3.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID5.Length > 39)
                        cleanPath4 = devicepathHID5.Substring(0, devicepathHID5.Length - 39);

                    string query5 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath5 + "'").Replace("\\", "\\\\");
                    ManagementObjectSearcher moSearch5 = new ManagementObjectSearcher(query5);
                    ManagementObjectCollection moCollection5 = moSearch5.Get();
                    foreach (ManagementObject mo in moCollection5.Cast<ManagementObject>())
                    {
                        string kb3desc = mo["Description"].ToString();
                        kbDic.Add(kb3, kb3desc);
                    }
                }

                string query1 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath1 + "'").Replace("\\", "\\\\");
                ManagementObjectSearcher moSearch1 = new ManagementObjectSearcher(query1);
                ManagementObjectCollection moCollection1 = moSearch1.Get();
                foreach (ManagementObject mo in moCollection1.Cast<ManagementObject>())
                {
                    string lightgun1desc = mo["Description"].ToString();
                    MouseDic.Add(lightgun1, lightgun1desc);
                }

                string query2 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath2 + "'").Replace("\\", "\\\\");
                ManagementObjectSearcher moSearch2 = new ManagementObjectSearcher(query2);
                ManagementObjectCollection moCollection2 = moSearch2.Get();
                foreach (ManagementObject mo in moCollection2.Cast<ManagementObject>())
                {
                    string lightgun2desc = mo["Description"].ToString();
                    MouseDic.Add(lightgun2, lightgun2desc);
                }

                ini.WriteValue("input", "RawInput", "yes");
                if (kb1 != null)
                    ini.WriteValue("input", "maple_raw_keyboard_" + kb1.DevicePath.Substring(8), "0");
                if (kb2 != null)
                    ini.WriteValue("input", "maple_raw_keyboard_" + kb2.DevicePath.Substring(8), "1");
                if (kb3 != null)
                    ini.WriteValue("input", "maple_raw_keyboard_" + kb3.DevicePath.Substring(8), "0");
                
                ini.WriteValue("input", "maple_raw_mouse_" + lightgun1.DevicePath.Substring(8), "0");
                ini.WriteValue("input", "maple_raw_mouse_" + lightgun2.DevicePath.Substring(8), "1");

                if (!SystemConfig.isOptSet("flycast_controller1"))
                    ini.WriteValue("input", "device1", "7");
                if (!SystemConfig.isOptSet("flycast_controller2"))
                    ini.WriteValue("input", "device2", "7");

                ini.Remove("input", "maple_sdl_keyboard");
                ini.Remove("input", "maple_sdl_mouse");

                foreach (var mouse in MouseDic)
                {
                    string devicePath = mouse.Key.DevicePath;
                    string vidpid = GetVIDPID(devicePath);
                    RawLightgun gun = mouse.Key;

                    string mouseMapping = Path.Combine(mappingPath, "RAW_" + mouse.Value + " [" + vidpid + "]" + ".cfg");
                    if (_isArcade)
                        mouseMapping = Path.Combine(mappingPath, "RAW_" + mouse.Value + " [" + vidpid + "]_arcade" + ".cfg");

                    using (var ctrlini = new IniFile(mouseMapping, IniOptions.UseSpaces))
                    {
                        if (_isArcade)
                        {
                            if (useFileMapping)
                            {
                                string MouseRight1 = guninvert ? "1:btn_a" : "1:btn_b";
                                string MouseLeft1 = guninvert ? "2:btn_b" : "2:btn_a";
                                    
                                if (buttonMap.TryGetValue("south", out string MouseLeft))
                                    ctrlini.WriteValue("digital", "bind1", "2:" + MouseLeft);
                                else
                                    ctrlini.WriteValue("digital", "bind1", guninvert ? "2:btn_b" : "2:btn_a");

                                if (buttonMap.TryGetValue("west", out string MouseRight))
                                    ctrlini.WriteValue("digital", "bind0", "1:" + MouseRight);
                                else
                                    ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:btn_b");

                                ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                            }
                            else
                            {
                                ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:btn_b");
                                ctrlini.WriteValue("digital", "bind1", guninvert ? "2:btn_b" : "2:btn_a");
                                ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                            }
                        }
                        else
                        {
                            // Mouse buttons
                            if (SystemConfig.isOptSet("flycast_mousegunbuttons") && !string.IsNullOrEmpty(SystemConfig["flycast_mousegunbuttons"]))
                            {
                                string[] buttons = SystemConfig["flycast_mousegunbuttons"].Split('_');
                                Dictionary<string, string> mouseBMap = new Dictionary<string, string>();

                                if (buttons.Length > 2)
                                {
                                    mouseBMap.Add("bind0", buttons[0]);
                                    mouseBMap.Add("bind1", buttons[1]);
                                    mouseBMap.Add("bind2", buttons[2]);
                                }
                                else if (buttons.Length > 1)
                                {
                                    mouseBMap.Add("bind0", buttons[0]);
                                    mouseBMap.Add("bind1", buttons[1]);
                                    mouseBMap.Add("bind2", "3:btn_start");
                                }
                                else if (buttons.Length > 0)
                                {
                                    mouseBMap.Add("bind0", buttons[0]);
                                    mouseBMap.Add("bind1", guninvert ? "2:reload" : "2:btn_a");
                                    mouseBMap.Add("bind2", "3:btn_start");
                                }

                                int i = 1;
                                foreach (var mouseB in mouseBMap)
                                {
                                    string mvalue = mouseB.Value;

                                    switch (mvalue)
                                    {
                                        case "a":
                                            mvalue = i + ":btn_a";
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                        case "r":
                                            mvalue = i + ":reload";
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                        case "s":
                                            mvalue = i + ":btn_start";
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                        case "x":
                                            mvalue = i + ":btn_x";
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                        case "b":
                                            mvalue = i + ":btn_b";
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                        case "y":
                                            mvalue = i + ":btn_y";
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                        default:
                                            ctrlini.WriteValue("digital", mouseB.Key, mvalue);
                                            break;
                                    }
                                    i++;
                                }
                            }
                            else
                            {
                                if (useXandB.Contains(_romName))
                                {
                                    ctrlini.WriteValue("digital", "bind0", "1:btn_b");
                                    ctrlini.WriteValue("digital", "bind1", "2:btn_x");
                                    ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                                }
                                else if (useXandA.Contains(_romName))
                                {
                                    ctrlini.WriteValue("digital", "bind0", "1:btn_a");
                                    ctrlini.WriteValue("digital", "bind1", "2:btn_x");
                                    ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                                }
                                else
                                {
                                    if (reloadWithButtonB.Contains(_romName))
                                    {
                                        ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:btn_b");
                                        ctrlini.WriteValue("digital", "bind1", guninvert ? "2:btn_b" : "2:btn_a");
                                    }
                                    else
                                    {
                                        ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:reload");
                                        ctrlini.WriteValue("digital", "bind1", guninvert ? "2:reload" : "2:btn_a");
                                    }
                                    ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                                }
                            }
                        }
                        ctrlini.WriteValue("emulator", "dead_zone", "10");
                        ctrlini.WriteValue("emulator", "mapping_name", "Mouse");
                        ctrlini.WriteValue("emulator", "rumble_power", "100");
                        ctrlini.WriteValue("emulator", "saturation", "100");
                        ctrlini.WriteValue("emulator", "version", "3");
                    }
                }

                foreach (var kb in kbDic)
                {
                    int index = 1;
                    if (kb.Key == kb2)
                        index = 2;
                    if (kb.Key == kb3)
                        index = 3;

                    string devicePath = kb.Key.DevicePath;
                    string vidpid = GetVIDPID(devicePath);

                    string kbMapping = Path.Combine(mappingPath, "RAW_" + kb.Value + " [" + vidpid + "]" + ".cfg");
                    if (_isArcade)
                        kbMapping = Path.Combine(mappingPath, "RAW_" + kb.Value + " [" + vidpid + "]_arcade" + ".cfg");

                    using (var ctrlini = new IniFile(kbMapping, IniOptions.UseSpaces))
                    {
                        string vidPid = GetVIDPID(devicePath);
                        bool isWiimote = vidpid.Contains("VID_0079&PID_1802");
                        if (_isArcade)
                        {
                            if (index == 3)
                            {
                                ctrlini.ClearSection("digital");

                                ctrlini.WriteValue("digital", "bind0", "6:insert_card");        //C
                                ctrlini.WriteValue("digital", "bind1", "17:btn_dpad2_up");      //N
                                ctrlini.WriteValue("digital", "bind10", "59:btn_jump_state");   //F2
                                ctrlini.WriteValue("digital", "bind11", "66:btn_screenshot");   //F9
                                ctrlini.WriteValue("digital", "bind12", "79:btn_dpad1_right");  //RIGHT
                                ctrlini.WriteValue("digital", "bind13", "80:btn_dpad1_left");   //LEFT
                                ctrlini.WriteValue("digital", "bind14", "81:btn_dpad1_down");   //DOWN
                                ctrlini.WriteValue("digital", "bind15", "82:btn_dpad1_up");     //UP
                                ctrlini.WriteValue("digital", "bind2", "5:btn_dpad2_down");     //B
                                ctrlini.WriteValue("digital", "bind3", "20:btn_a");             //Q
                                ctrlini.WriteValue("digital", "bind4", "29:btn_b");             //Z
                                ctrlini.WriteValue("digital", "bind5", "34:btn_d");             //5
                                ctrlini.WriteValue("digital", "bind6", "30:btn_start");         //1
                                ctrlini.WriteValue("digital", "bind7", "43:btn_menu");          //TAB
                                ctrlini.WriteValue("digital", "bind8", "61:btn_fforward");      //F4
                                ctrlini.WriteValue("digital", "bind9", "58:btn_quick_save");    //F1
                            }
                            else if (isWiimote)
                            {
                                ctrlini.ClearSection("digital");

                                if (SystemConfig.isOptSet("WiimoteMode") && !string.IsNullOrEmpty(SystemConfig["WiimoteMode"]))
                                {
                                    string wiiMode = SystemConfig["WiimoteMode"];

                                    if (wiiMode == "game")
                                    {
                                        ctrlini.WriteValue("digital", "bind0", "40:btn_start");
                                        ctrlini.WriteValue("digital", "bind1", "41:btn_d");
                                        ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                        ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                        ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                        ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                        ctrlini.WriteValue("digital", "bind6", "128:btn_a");
                                        ctrlini.WriteValue("digital", "bind7", "129:btn_b");
                                    }
                                    else if (wiiMode == "normal")
                                    {
                                        ctrlini.WriteValue("digital", "bind0", "75:btn_start");
                                        ctrlini.WriteValue("digital", "bind1", "78:btn_d");
                                        ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                        ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                        ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                        ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                        ctrlini.WriteValue("digital", "bind6", "224:btn_b");
                                        ctrlini.WriteValue("digital", "bind7", "225:btn_a");
                                    }
                                    else
                                    {
                                        ctrlini.WriteValue("digital", "bind0", "30:btn_start");
                                        ctrlini.WriteValue("digital", "bind1", "34:btn_d");
                                        ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                        ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                        ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                        ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                        ctrlini.WriteValue("digital", "bind6", "224:btn_b");
                                        ctrlini.WriteValue("digital", "bind7", "225:btn_a");
                                    }
                                }
                                else
                                {
                                    ctrlini.WriteValue("digital", "bind0", "30:btn_start");
                                    ctrlini.WriteValue("digital", "bind1", "34:btn_d");
                                    ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                    ctrlini.WriteValue("digital", "bind6", "224:btn_b");
                                    ctrlini.WriteValue("digital", "bind7", "225:btn_a");
                                }

                                if (index == 1)
                                {
                                    ctrlini.WriteValue("digital", "bind8", "43:btn_menu");          //TAB
                                    ctrlini.WriteValue("digital", "bind9", "59:btn_jump_state");    //F2
                                    ctrlini.WriteValue("digital", "bind10", "66:btn_screenshot");   //F9
                                    ctrlini.WriteValue("digital", "bind11", "61:btn_fforward");     //F4
                                    ctrlini.WriteValue("digital", "bind12", "58:btn_quick_save");   //F1
                                    ctrlini.WriteValue("digital", "bind13", "20:btn_x");            //Q
                                }
                            }
                            else
                            {
                                ctrlini.ClearSection("digital");

                                if (index == 1)
                                {
                                    ctrlini.WriteValue("digital", "bind0", "6:insert_card");        //C
                                    ctrlini.WriteValue("digital", "bind1", "17:btn_dpad2_up");      //N
                                    ctrlini.WriteValue("digital", "bind10", "59:btn_jump_state");   //F2
                                    ctrlini.WriteValue("digital", "bind11", "66:btn_screenshot");   //F9
                                    ctrlini.WriteValue("digital", "bind12", "79:btn_dpad1_right");  //RIGHT
                                    ctrlini.WriteValue("digital", "bind13", "80:btn_dpad1_left");   //LEFT
                                    ctrlini.WriteValue("digital", "bind14", "81:btn_dpad1_down");   //DOWN
                                    ctrlini.WriteValue("digital", "bind15", "82:btn_dpad1_up");     //UP
                                    ctrlini.WriteValue("digital", "bind2", "5:btn_dpad2_down");     //B
                                    ctrlini.WriteValue("digital", "bind3", "20:btn_a");             //Q
                                    ctrlini.WriteValue("digital", "bind4", "29:btn_b");             //Z
                                    ctrlini.WriteValue("digital", "bind5", "34:btn_d");             //5
                                    ctrlini.WriteValue("digital", "bind6", "30:btn_start");         //1
                                    ctrlini.WriteValue("digital", "bind7", "43:btn_menu");          //TAB
                                    ctrlini.WriteValue("digital", "bind8", "61:btn_fforward");      //F4
                                    ctrlini.WriteValue("digital", "bind9", "58:btn_quick_save");    //F1
                                }
                                else
                                {
                                    ctrlini.WriteValue("digital", "bind0", "27:btn_dpad1_right");  //X
                                    ctrlini.WriteValue("digital", "bind1", "26:btn_dpad1_left");   //W
                                    ctrlini.WriteValue("digital", "bind2", "25:btn_dpad1_down");   //V
                                    ctrlini.WriteValue("digital", "bind3", "22:btn_a");            //S
                                    ctrlini.WriteValue("digital", "bind4", "4:btn_b");             //A
                                    ctrlini.WriteValue("digital", "bind5", "35:btn_d");            //6
                                    ctrlini.WriteValue("digital", "bind6", "31:btn_start");        //2
                                    ctrlini.WriteValue("digital", "bind7", "24:btn_dpad1_up");     //U
                                }
                            }
                        }
                        // Dreamcast
                        else
                        {
                            ctrlini.ClearSection("digital");

                            if (index == 3)
                            {
                                ctrlini.WriteValue("digital", "bind0", "29:btn_a");                    //Z
                                ctrlini.WriteValue("digital", "bind1", "30:btn_start");                //1               
                                ctrlini.WriteValue("digital", "bind2", "43:btn_menu");                 //TAB
                                ctrlini.WriteValue("digital", "bind3", "61:btn_fforward");             //F4
                                ctrlini.WriteValue("digital", "bind4", "58:btn_quick_save");           //F1
                                ctrlini.WriteValue("digital", "bind5", "59:btn_jump_state");           //F2
                                ctrlini.WriteValue("digital", "bind6", "66:btn_screenshot");           //F9
                                ctrlini.WriteValue("digital", "bind7", "79:btn_dpad1_right");
                                ctrlini.WriteValue("digital", "bind8", "80:btn_dpad1_left");
                                ctrlini.WriteValue("digital", "bind9", "81:btn_dpad1_down");
                                ctrlini.WriteValue("digital", "bind10", "82:btn_dpad1_up");
                                ctrlini.WriteValue("digital", "bind11", "27:btn_b");                    //X
                                ctrlini.WriteValue("digital", "bind12", "6:btn_c");                     //C
                            }
                            //wiimotes
                            else if (SystemConfig.isOptSet("WiimoteMode") && !string.IsNullOrEmpty(SystemConfig["WiimoteMode"]))
                            {
                                string wiiMode = SystemConfig["WiimoteMode"];

                                if (wiiMode == "game")
                                {
                                    ctrlini.WriteValue("digital", "bind0", "40:btn_start");         //return
                                    ctrlini.WriteValue("digital", "bind1", "41:btn_d");             //Escape
                                    ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                    ctrlini.WriteValue("digital", "bind6", "128:btn_a");            //volumeup
                                    ctrlini.WriteValue("digital", "bind7", "129:btn_b");            //volumedown
                                }
                                else if (wiiMode == "normal")
                                {
                                    ctrlini.WriteValue("digital", "bind0", "75:btn_start");         //pageup
                                    ctrlini.WriteValue("digital", "bind1", "78:btn_d");             //pagedown
                                    ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                    ctrlini.WriteValue("digital", "bind6", "224:btn_b");            //Lctrl
                                    ctrlini.WriteValue("digital", "bind7", "225:btn_a");            //Lshift
                                }
                                else
                                {
                                    ctrlini.WriteValue("digital", "bind0", "30:btn_start");         //1
                                    ctrlini.WriteValue("digital", "bind1", "34:btn_d");             //5
                                    ctrlini.WriteValue("digital", "bind2", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind3", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind4", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind5", "82:btn_dpad1_up");
                                    ctrlini.WriteValue("digital", "bind6", "224:btn_b");            //Lctrl
                                    ctrlini.WriteValue("digital", "bind7", "225:btn_a");            //Lshift
                                }

                                if (index == 1)
                                {
                                    ctrlini.WriteValue("digital", "bind8", "43:btn_menu");          //TAB
                                    ctrlini.WriteValue("digital", "bind9", "59:btn_jump_state");    //F2
                                    ctrlini.WriteValue("digital", "bind10", "66:btn_screenshot");   //F9
                                    ctrlini.WriteValue("digital", "bind11", "61:btn_fforward");     //F4
                                    ctrlini.WriteValue("digital", "bind12", "58:btn_quick_save");   //F1
                                    ctrlini.WriteValue("digital", "bind13", "20:btn_x");            //Q
                                }
                            }
                            //non-wiimotes
                            else
                            {
                                if (index == 1)
                                {
                                    ctrlini.WriteValue("digital", "bind0", "34:btn_a");                    //5
                                    ctrlini.WriteValue("digital", "bind1", "30:btn_start");                //1               
                                    ctrlini.WriteValue("digital", "bind2", "43:btn_menu");                 //TAB
                                    ctrlini.WriteValue("digital", "bind3", "61:btn_fforward");             //F4
                                    ctrlini.WriteValue("digital", "bind4", "58:btn_quick_save");           //F1
                                    ctrlini.WriteValue("digital", "bind5", "59:btn_jump_state");           //F2
                                    ctrlini.WriteValue("digital", "bind6", "66:btn_screenshot");           //F9
                                    ctrlini.WriteValue("digital", "bind7", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind8", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind9", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind10", "82:btn_dpad1_up");
                                    ctrlini.WriteValue("digital", "bind11", "20:btn_b");                    //Q
                                }
                                else
                                {
                                    ctrlini.WriteValue("digital", "bind0", "22:btn_b");                    //S
                                    ctrlini.WriteValue("digital", "bind1", "31:btn_start");                //2               
                                    ctrlini.WriteValue("digital", "bind2", "27:btn_dpad1_right");          //X
                                    ctrlini.WriteValue("digital", "bind3", "26:btn_dpad1_left");           //W
                                    ctrlini.WriteValue("digital", "bind4", "25:btn_dpad1_down");           //V
                                    ctrlini.WriteValue("digital", "bind5", "24:btn_dpad1_up");             //U
                                    ctrlini.WriteValue("digital", "bind6", "35:btn_a");                    //6
                                }
                            }
                        }

                        ctrlini.WriteValue("emulator", "dead_zone", "10");
                        ctrlini.WriteValue("emulator", "mapping_name", "Mouse");
                        ctrlini.WriteValue("emulator", "rumble_power", "100");
                        ctrlini.WriteValue("emulator", "saturation", "100");
                        ctrlini.WriteValue("emulator", "version", "3");
                    }
                }
            }

            if (SystemConfig.isOptSet("flycast_crosshair") && SystemConfig.getOptBoolean("flycast_crosshair"))
            {
                if (multigun)
                {
                    ini.WriteValue("config", "rend.CrossHairColor1", "-1073675782");
                    ini.WriteValue("config", "rend.CrossHairColor2", "-1073547006");
                }
                else
                {
                    ini.WriteValue("config", "rend.CrossHairColor1", "-1073675782");
                    ini.WriteValue("config", "rend.CrossHairColor2", "0");
                }
            }
            else
            {
                ini.WriteValue("config", "rend.CrossHairColor1", "0");
                ini.WriteValue("config", "rend.CrossHairColor2", "0");
            }
        }
        private RawInputDevice FindAssociatedKeyboardWiimote(string gunPath, List<RawInputDevice> keyboards, RawInputDevice keyboard)
        {
            string mouseVIDPID = GetWiimoteVIDPID(gunPath);
            string mouseChar = GetWiimoteAssociationChar(gunPath);
            string toSearch = mouseVIDPID + "_" + mouseChar;

            foreach (var kb in keyboards)
            {
                string kbVIDPID = GetWiimoteVIDPID(kb.DevicePath);
                string kbChar = GetWiimoteAssociationChar(kb.DevicePath);

                if (kbVIDPID != null && kbChar != null)
                {
                    string toFind = kbVIDPID + "_" + kbChar;
                    if (toSearch.ToLowerInvariant() == toFind.ToLowerInvariant())
                    {
                        return kb; // Return the matching keyboard
                    }
                }
            }
            return keyboard; // No match found
        }

        private static string GetWiimoteVIDPID(string devicePath)
        {
            try
            {
                // Split the path by '#'
                string[] parts = devicePath.Split('#');
                if (parts.Length < 3)
                    return null;

                // The second part contains VID and PID (ignore the MI_ part)
                string[] vidPidParts = parts[1].Split('&');
                string vidPid = $"{vidPidParts[0]}&{vidPidParts[1]}"; // Only take VID and PID

                // The third part contains the character after the second #
                string partAfterSecondHash = parts[2];
                char characterAfterSecondHash = partAfterSecondHash[0]; // First character

                return vidPid;
            }
            catch
            {
                return null; // Return null on error
            }
        }

        private static string GetWiimoteAssociationChar(string devicePath)
        {
            try
            {
                // Split the path by '#'
                string[] parts = devicePath.Split('#');
                if (parts.Length < 3)
                    return "";

                // The third part contains the character after the second #
                string partAfterSecondHash = parts[2];
                char characterAfterSecondHash = partAfterSecondHash[0]; // First character

                return characterAfterSecondHash.ToString();
            }
            catch
            {
                return ""; // Return null on error
            }
        }

        private static RawInputDevice FindAssociatedKeyboard(List<RawInputDevice> keyboards, RawLightgun iGun)
        {
            int startIndex = iGun.DevicePath.IndexOf("VID");
            if (startIndex >= 0)
            {
                int endIndex = iGun.DevicePath.IndexOf('#', startIndex);
                if (endIndex == -1) return keyboards[0];
                if (iGun.DevicePath.Contains("MI_"))
                {
                    endIndex = iGun.DevicePath.IndexOf("MI_", startIndex);
                    if (endIndex == -1) return keyboards[0];
                    endIndex += 5;
                }
                string searchPath = iGun.DevicePath.Substring(startIndex, endIndex - startIndex);

                if (keyboards.Any(k => k.DevicePath.Contains(searchPath)))
                    return keyboards.FirstOrDefault(k => k.DevicePath.Contains(searchPath));
                else
                {
                    searchPath = iGun.DevicePath.Substring(startIndex, endIndex - startIndex - 5);
                    if (keyboards.Any(k => k.DevicePath.Contains(searchPath)))
                        return keyboards.FirstOrDefault(k => k.DevicePath.Contains(searchPath));
                }
            }
            return keyboards[0];
        }

        private static string GetVIDPID(string path)
        {
            int acpiIndex = path.IndexOf("ACPI");
            if (acpiIndex >= 0)
                return "----ACPI";

            int startIndex = path.IndexOf("#") + 1;
            if (startIndex < 0)
                return "";

            int endIndex = path.IndexOf("#", startIndex);
            if (endIndex < 0)
                return path.Substring(startIndex, path.Length - endIndex);

            return path.Substring(startIndex, endIndex - startIndex);
        }
    }
}
