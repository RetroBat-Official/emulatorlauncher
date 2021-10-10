using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace emulatorLauncher
{
    class XEmuGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("xemu");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "xemuw.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "xemu.exe");
            
            if (!File.Exists(exe))
                return null;

            try
            {
                using (IniFile ini = new IniFile(Path.Combine(path, "xemu.ini"), true))
                {
                    if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig.GetFullPath("saves")))
                    {
                        string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                        if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                            catch { }

                        if (!File.Exists(Path.Combine(savePath, "eeprom.bin")))
                            File.WriteAllBytes(Path.Combine(savePath, "eeprom.bin"), Properties.Resources.eeprom);

                        if (!File.Exists(Path.Combine(savePath, "xbox_hdd.qcow2")))
                        {
                            string zipFile = Path.Combine(savePath, "xbox_hdd.qcow2.zip");
                            File.WriteAllBytes(zipFile, Properties.Resources.xbox_hdd_qcow2);

                            string unzip = Path.Combine(Path.GetDirectoryName(typeof(XEmuGenerator).Assembly.Location), "unzip.exe");
                            if (File.Exists(unzip))
                            {
                                Process.Start(new ProcessStartInfo()
                                {
                                    FileName = unzip,
                                    Arguments = "-o \"" + zipFile + "\" -d \"" + savePath + "\"",
                                    WorkingDirectory = savePath,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    UseShellExecute = true
                                })
                                .WaitForExit();
                            }

                            File.Delete(zipFile);
                        }

                        if (File.Exists(Path.Combine(savePath, "eeprom.bin")))
                            ini.WriteValue("system", "eeprom_path", Path.Combine(savePath, "eeprom.bin"));

                        if (File.Exists(Path.Combine(savePath, "xbox_hdd.qcow2")))
                            ini.WriteValue("system", "hdd_path", Path.Combine(savePath, "xbox_hdd.qcow2"));
                    }

                    if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig.GetFullPath("bios")))
                    {
                        if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "Complex_4627.bin")))
                            ini.WriteValue("system", "flash_path", Path.Combine(AppConfig.GetFullPath("bios"), "Complex_4627.bin"));

                        if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "mcpx_1.0.bin")))
                            ini.WriteValue("system", "bootrom_path", Path.Combine(AppConfig.GetFullPath("bios"), "mcpx_1.0.bin"));
                    }

                    //ini.WriteValue("system", "memory", "128");
                    ini.WriteValue("system", "shortanim", "true");
                    ini.WriteValue("system", "dvd_path", rom);
                    ini.WriteValue("display", "scale", "scale");                    
                    ini.Save();

                    if (SystemConfig.isOptSet("render_scale") && !string.IsNullOrEmpty(SystemConfig["render_scale"]))
                        ini.WriteValue("display", "render_scale", SystemConfig["render_scale"]);
                    else
                        ini.WriteValue("display", "render_scale", "1");

                    if (SystemConfig.isOptSet("scale") && !string.IsNullOrEmpty(SystemConfig["scale"]))
                        ini.WriteValue("display", "scale", SystemConfig["scale"]);
                    else
                        ini.WriteValue("display", "scale", "scale");

                    if (SystemConfig.isOptSet("system_memory") && !string.IsNullOrEmpty(SystemConfig["system_memory"]))
                        ini.WriteValue("system", "memory", SystemConfig["system_memory"]);
                    else
                        ini.WriteValue("system", "memory", "128");

                }
            }
            catch { }

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-full-screen -dvd_path \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }

    }
}
