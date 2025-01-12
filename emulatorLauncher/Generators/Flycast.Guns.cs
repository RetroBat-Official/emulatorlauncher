using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Management;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common;
using System.Windows.Input;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class FlycastGenerator
    {
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

            // Get mapping in yml file
            YmlContainer game = null;
            var buttonMap = new Dictionary<string, string>();

            if (_isArcade)
            {
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

                if (File.Exists(mappingFile))
                    File.Delete(mappingFile);

                // SDL region (only 1 gun)
                using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
                {
                    if (useXandB.Contains(_romName))
                    {
                        ctrlini.WriteValue("digital", "bind0", "1:btn_x");
                        ctrlini.WriteValue("digital", "bind1", "2:btn_start");
                        ctrlini.WriteValue("digital", "bind2", "3:btn_b");
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

                    ctrlini.WriteValue("emulator", "dead_zone", "10");
                    ctrlini.WriteValue("emulator", "mapping_name", "Mouse");
                    ctrlini.WriteValue("emulator", "rumble_power", "100");
                    ctrlini.WriteValue("emulator", "version", "3");
                    ctrlini.Save();
                }

                // Multigun : raw input
                if (multigun)
                {
                    
                    RawLightgun lightgun1 = gunindexrevert ? guns[1] : guns[0];
                    RawLightgun lightgun2 = gunindexrevert ? guns[0] : guns[1];

                    RawInputDevice kb1 = null;
                    RawInputDevice kb2 = null;

                    if (lightgun1.Type == RawLighGunType.MayFlashWiimote)
                        kb1 = FindAssociatedKeyboardWiimote(lightgun1.DevicePath, keyboards, keyboard);
                    else
                        kb1 = FindAssociatedKeyboard(keyboards, lightgun1);

                    if (lightgun2.Type == RawLighGunType.MayFlashWiimote)
                        kb2 = FindAssociatedKeyboardWiimote(lightgun2.DevicePath, keyboards, keyboard);
                    else
                        kb2 = FindAssociatedKeyboard(keyboards, lightgun2);

                    List<RawInputDevice> kbdevices = new List<RawInputDevice>
                    {
                        kb1,
                        kb2
                    };
                    List<RawLightgun> mousedevices = new List<RawLightgun>
                    {
                        lightgun1,
                        lightgun2
                    };

                    string cleanPath1 = "";
                    string cleanPath2 = "";
                    string cleanPath3 = "";
                    string cleanPath4 = "";

                    Dictionary<RawLightgun, string> MouseDic = new Dictionary<RawLightgun, string>();
                    Dictionary<RawInputDevice, string> kbDic = new Dictionary<RawInputDevice, string>();

                    string devicepathHID1 = lightgun1.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID1.Length > 39)
                        cleanPath1 = devicepathHID1.Substring(0, devicepathHID1.Length - 39);
                    string devicepathHID2 = lightgun2.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID2.Length > 39)
                        cleanPath2 = devicepathHID2.Substring(0, devicepathHID2.Length - 39);
                    string devicepathHID3 = kb1.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID3.Length > 39)
                        cleanPath3 = devicepathHID3.Substring(0, devicepathHID3.Length - 39);
                    string devicepathHID4 = kb2.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                    if (devicepathHID4.Length > 39)
                        cleanPath4 = devicepathHID4.Substring(0, devicepathHID4.Length - 39);

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

                    string query3 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath3 + "'").Replace("\\", "\\\\");
                    ManagementObjectSearcher moSearch3 = new ManagementObjectSearcher(query3);
                    ManagementObjectCollection moCollection3 = moSearch3.Get();
                    foreach (ManagementObject mo in moCollection3.Cast<ManagementObject>())
                    {
                        string kb1desc = mo["Description"].ToString();
                        kbDic.Add(kb1, kb1desc);
                    }

                    string query4 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath4 + "'").Replace("\\", "\\\\");
                    ManagementObjectSearcher moSearch4 = new ManagementObjectSearcher(query4);
                    ManagementObjectCollection moCollection4 = moSearch4.Get();
                    foreach (ManagementObject mo in moCollection4.Cast<ManagementObject>())
                    {
                        string kb2desc = mo["Description"].ToString();
                        kbDic.Add(kb2, kb2desc);
                    }

                    ini.WriteValue("input", "RawInput", "yes");
                    ini.WriteValue("input", "maple_raw_keyboard_" + kb1.DevicePath.Substring(8), "0");
                    ini.WriteValue("input", "maple_raw_keyboard_" + kb2.DevicePath.Substring(8), "1");
                    ini.WriteValue("input", "maple_raw_mouse_" + lightgun1.DevicePath.Substring(8), "0");
                    ini.WriteValue("input", "maple_raw_mouse_" + lightgun2.DevicePath.Substring(8), "1");
                    ini.Remove("input", "maple_sdl_keyboard");
                    ini.Remove("input", "maple_sdl_mouse");

                    foreach (var mouse in MouseDic)
                    {
                        string devicePath = mouse.Key.DevicePath;
                        string vidpid = GetVIDPID(devicePath);

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
                                if (useXandB.Contains(_romName))
                                {
                                    ctrlini.WriteValue("digital", "bind0", "1:btn_x");
                                    ctrlini.WriteValue("digital", "bind1", "2:btn_start");
                                    ctrlini.WriteValue("digital", "bind2", "3:btn_b");
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
                            ctrlini.WriteValue("emulator", "saturation", "100");
                            ctrlini.WriteValue("emulator", "version", "3");
                        }
                    }

                    foreach (var kb in kbDic)
                    {
                        string devicePath = kb.Key.DevicePath;
                        string vidpid = GetVIDPID(devicePath);

                        string kbMapping = Path.Combine(mappingPath, "RAW_" + kb.Value + " [" + vidpid + "]" + ".cfg");
                        if (_isArcade)
                            kbMapping = Path.Combine(mappingPath, "RAW_" + kb.Value + " [" + vidpid + "]_arcade" + ".cfg");

                        using (var ctrlini = new IniFile(kbMapping, IniOptions.UseSpaces))
                        {
                            string vidPid = GetVIDPID(devicePath);
                            bool isWiimote = vidpid.Contains("VID_0079&PID_18");
                            if (_isArcade)
                            {
                                if (isWiimote)
                                {
                                    ctrlini.ClearSection("digital");
                                    ctrlini.WriteValue("digital", "bind0", "40:btn_start");
                                    ctrlini.WriteValue("digital", "bind1", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind2", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind3", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind4", "82:btn_d");
                                }
                                else
                                {
                                    ctrlini.ClearSection("digital");
                                    ctrlini.WriteValue("digital", "bind0", "6:insert_card");
                                    ctrlini.WriteValue("digital", "bind1", "22:btn_dpad2_up");
                                    ctrlini.WriteValue("digital", "bind10", "59:btn_jump_state");
                                    ctrlini.WriteValue("digital", "bind11", "69:btn_screenshot");
                                    ctrlini.WriteValue("digital", "bind12", "79:btn_dpad1_right");
                                    ctrlini.WriteValue("digital", "bind13", "80:btn_dpad1_left");
                                    ctrlini.WriteValue("digital", "bind14", "81:btn_dpad1_down");
                                    ctrlini.WriteValue("digital", "bind15", "82:btn_dpad1_up");
                                    ctrlini.WriteValue("digital", "bind2", "23:btn_dpad2_down");
                                    ctrlini.WriteValue("digital", "bind3", "27:btn_a");
                                    ctrlini.WriteValue("digital", "bind4", "29:btn_b");
                                    ctrlini.WriteValue("digital", "bind5", "30:btn_d");
                                    ctrlini.WriteValue("digital", "bind6", "33:btn_start");
                                    ctrlini.WriteValue("digital", "bind7", "43:btn_menu");
                                    ctrlini.WriteValue("digital", "bind8", "44:btn_fforward");
                                    ctrlini.WriteValue("digital", "bind9", "58:btn_quick_save");
                                }
                            }
                            else
                            {
                                ctrlini.ClearSection("digital");
                                ctrlini.WriteValue("digital", "bind0", "4:btn_trigger_left");
                                ctrlini.WriteValue("digital", "bind1", "6:reload");
                                ctrlini.WriteValue("digital", "bind10", "27:btn_a");
                                ctrlini.WriteValue("digital", "bind11", "29:btn_x");
                                ctrlini.WriteValue("digital", "bind12", "40:btn_start");
                                ctrlini.WriteValue("digital", "bind13", "43:btn_menu");
                                ctrlini.WriteValue("digital", "bind14", "44:btn_fforward");
                                ctrlini.WriteValue("digital", "bind15", "58:btn_quick_save");
                                ctrlini.WriteValue("digital", "bind16", "59:btn_jump_state");
                                ctrlini.WriteValue("digital", "bind17", "69:btn_screenshot");
                                ctrlini.WriteValue("digital", "bind18", "79:btn_dpad1_right");
                                ctrlini.WriteValue("digital", "bind19", "80:btn_dpad1_left");
                                ctrlini.WriteValue("digital", "bind2", "7:btn_trigger_right");
                                ctrlini.WriteValue("digital", "bind20", "81:btn_dpad1_down");
                                ctrlini.WriteValue("digital", "bind21", "82:btn_dpad1_up");
                                ctrlini.WriteValue("digital", "bind3", "8:btn_trigger2_right");
                                ctrlini.WriteValue("digital", "bind4", "12:btn_analog_up");
                                ctrlini.WriteValue("digital", "bind5", "13:btn_analog_left");
                                ctrlini.WriteValue("digital", "bind6", "14:btn_analog_down");
                                ctrlini.WriteValue("digital", "bind7", "15:btn_analog_right");
                                ctrlini.WriteValue("digital", "bind8", "20:btn_trigger2_left");
                                ctrlini.WriteValue("digital", "bind9", "22:btn_y");
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
