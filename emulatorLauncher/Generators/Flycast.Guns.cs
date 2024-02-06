using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Management;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class FlycastGenerator
    {
        private void ConfigureFlycastGuns(IniFile ini, string mappingPath)
        {
            bool useOneGun = SystemConfig.getOptBoolean("one_gun");
            bool guninvert = SystemConfig.getOptBoolean("gun_invert");
            bool gunindexrevert = SystemConfig.getOptBoolean("gun_index_revert");
            bool multigun = false;
            if (SystemConfig["flycast_controller1"] == "7" && SystemConfig["flycast_controller2"] == "7")
                multigun = true;
            
            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();

            if (gunCount > 1 && guns.Length > 1 && !useOneGun)
                multigun = true;

            ini.WriteValue("input", "maple_sdl_mouse", "0");

            string mappingFile = Path.Combine(mappingPath, "SDL_Default Mouse.cfg");

            if (File.Exists(mappingFile))
                File.Delete(mappingFile);

            using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
            {
                ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:reload");
                ctrlini.WriteValue("digital", "bind1", guninvert ? "2:reload" : "2:btn_a");
                ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                ctrlini.WriteValue("emulator", "dead_zone", "10");
                ctrlini.WriteValue("emulator", "mapping_name", "Mouse");
                ctrlini.WriteValue("emulator", "rumble_power", "100");
                ctrlini.WriteValue("emulator", "version", "3");
                ctrlini.Save();
            }

            if (multigun)
            {
                if (gunCount <= 1) // If there's only one gun ( or just one sinden gun + one mouse ), then ignore multigun
                    return;

                RawLightgun lightgun1 = gunindexrevert ? guns[1] : guns[0];
                RawLightgun lightgun2 = gunindexrevert ? guns[0] : guns[1];

                ini.WriteValue("input", "RawInput", "yes");
                ini.WriteValue("input", "maple_raw_keyboard_" + lightgun1.DevicePath.Substring(8), "0");
                ini.WriteValue("input", "maple_raw_keyboard_" + lightgun2.DevicePath.Substring(8), "1");
                ini.WriteValue("input", "maple_raw_mouse_" + lightgun1.DevicePath.Substring(8), "0");
                ini.WriteValue("input", "maple_raw_mouse_" + lightgun2.DevicePath.Substring(8), "1");
                ini.Remove("input", "maple_sdl_keyboard");
                ini.Remove("input", "maple_sdl_mouse");

                string cleanPath1 = "";
                string cleanPath2 = "";

                string devicepathHID1 = lightgun1.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                if (devicepathHID1.Length > 39)
                    cleanPath1 = devicepathHID1.Substring(0, devicepathHID1.Length - 39);
                string devicepathHID2 = lightgun2.DevicePath.Substring(4).ToUpperInvariant().Replace("#", "\\");
                if (devicepathHID2.Length > 39)
                    cleanPath2 = devicepathHID2.Substring(0, devicepathHID2.Length - 39);

                Dictionary<ManagementObject, string> mouseList = new Dictionary<ManagementObject, string>();

                string query1 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath1 + "'").Replace("\\", "\\\\");
                ManagementObjectSearcher moSearch1 = new ManagementObjectSearcher(query1);
                ManagementObjectCollection moCollection1 = moSearch1.Get();
                foreach (ManagementObject mo in moCollection1)
                {
                    string desc1 = mo["Description"].ToString();
                    mouseList.Add(mo, desc1);
                }

                string query2 = ("SELECT * FROM Win32_PNPEntity" + " WHERE DeviceID = '" + cleanPath2 + "'").Replace("\\", "\\\\");
                ManagementObjectSearcher moSearch2 = new ManagementObjectSearcher(query2);
                ManagementObjectCollection moCollection2 = moSearch2.Get();
                foreach (ManagementObject mo in moCollection2)
                {
                    string desc2 = mo["Description"].ToString();
                    mouseList.Add(mo, desc2);
                }
                var mouseNameList = mouseList.Values.ToList();

                foreach (var mouse in mouseNameList)
                {
                    string mouseMapping = Path.Combine(mappingPath, "RAW_" + mouse + ".cfg");
                    
                    using (var ctrlini = new IniFile(mouseMapping, IniOptions.UseSpaces))
                    {
                        ctrlini.WriteValue("digital", "bind0", guninvert ? "1:btn_a" : "1:reload");
                        ctrlini.WriteValue("digital", "bind1", guninvert ? "2:reload" : "2:btn_a");
                        ctrlini.WriteValue("digital", "bind2", "3:btn_start");
                        ctrlini.WriteValue("emulator", "dead_zone", "10");
                        ctrlini.WriteValue("emulator", "mapping_name", "Mouse");
                        ctrlini.WriteValue("emulator", "rumble_power", "100");
                        ctrlini.WriteValue("emulator", "version", "3");
                    }
                }
            }

            if (SystemConfig.isOptSet("flycast_crosshair") && SystemConfig.getOptBoolean("flycast_crosshair"))
            {
                if (multigun)
                {
                    ini.WriteValue("config", "rend.CrossHairColor1","-1073675782");
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
}
