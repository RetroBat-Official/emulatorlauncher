using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class XEmuGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("xemu");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "xemu.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "xemuw.exe");
            
            if (!File.Exists(exe))
                return null;

            try
            {
                string eepromPath = null;
                string hddPath = null;
                string flashPath = null;
                string bootRom = null;

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
                        eepromPath = Path.Combine(savePath, "eeprom.bin");

                    if (File.Exists(Path.Combine(savePath, "xbox_hdd.qcow2")))
                        hddPath = Path.Combine(savePath, "xbox_hdd.qcow2");
                }

                if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig.GetFullPath("bios")))
                {
                    if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "Complex_4627.bin")))
                        flashPath = Path.Combine(AppConfig.GetFullPath("bios"), "Complex_4627.bin");

                    if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "mcpx_1.0.bin")))
                        bootRom = Path.Combine(AppConfig.GetFullPath("bios"), "mcpx_1.0.bin");
                }

                // Save to old INI format
                using (IniFile ini = new IniFile(Path.Combine(path, "xemu.ini"), IniOptions.UseSpaces))
                {
                    if (!string.IsNullOrEmpty(eepromPath))
                        ini.WriteValue("system", "eeprom_path", eepromPath);

                    if (!string.IsNullOrEmpty(hddPath))
                        ini.WriteValue("system", "hdd_path", hddPath);

                    if (!string.IsNullOrEmpty(flashPath))
                        ini.WriteValue("system", "flash_path", flashPath);

                    if (!string.IsNullOrEmpty(bootRom))
                        ini.WriteValue("system", "bootrom_path", bootRom);

                    ini.WriteValue("system", "shortanim", "true");
                    ini.WriteValue("system", "dvd_path", rom);
                    ini.WriteValue("display", "scale", "scale");

                    if (SystemConfig.isOptSet("render_scale") && !string.IsNullOrEmpty(SystemConfig["render_scale"]))
                        ini.WriteValue("display", "render_scale", SystemConfig["render_scale"]);
                    else if (Features.IsSupported("render_scale"))
                        ini.WriteValue("display", "render_scale", "1");

                    if (SystemConfig.isOptSet("scale") && !string.IsNullOrEmpty(SystemConfig["scale"]))
                        ini.WriteValue("display", "scale", SystemConfig["scale"]);
                    else if (Features.IsSupported("scale"))
                        ini.WriteValue("display", "scale", "scale");

                    if (SystemConfig.isOptSet("system_memory") && !string.IsNullOrEmpty(SystemConfig["system_memory"]))
                        ini.WriteValue("system", "memory", SystemConfig["system_memory"]);
                    else
                        ini.WriteValue("system", "memory", "128");
                }

                // Save to new TOML format
                using (IniFile ini = new IniFile(Path.Combine(path, "xemu.toml"), IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
                {
                    ini.WriteValue("general", "show_welcome", "false");
                    ini.WriteValue("general", "skip_boot_anim", "true");
                    ini.WriteValue("general.updates", "check", "false");

                    if (!SystemConfig.getOptBoolean("disableautocontrollers"))
                    {
                        for (int i = 0; i < 16; i++)
                            ini.Remove("input.bindings", "port" + i);

                        int port = 1;
                        foreach (var ctl in Controllers)
                        {
                            if (ctl.Name == "Keyboard")
                                ini.WriteValue("input.bindings", "port" + port, "'keyboard'");
                            else if (ctl.Config != null && ctl.XInput != null)
                                ini.WriteValue("input.bindings", "port" + port, "'" + ctl.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant() + "'");

                            port++;
                        }
                    }

                    if (SystemConfig.isOptSet("render_scale") && !string.IsNullOrEmpty(SystemConfig["render_scale"]))
                        ini.WriteValue("display.quality", "surface_scale", SystemConfig["render_scale"]);
                    else if (Features.IsSupported("render_scale"))
                        ini.WriteValue("display.quality", "surface_scale", "1");
                    
                    if (SystemConfig.isOptSet("scale") && !string.IsNullOrEmpty(SystemConfig["scale"]))
                        ini.WriteValue("display.ui", "fit", "'" + SystemConfig["scale"] + "'");
                    else if (Features.IsSupported("scale"))
                        ini.WriteValue("display.ui", "fit", "'scale'");
                    
                    if (SystemConfig.isOptSet("system_memory") && !string.IsNullOrEmpty(SystemConfig["system_memory"]))
                        ini.WriteValue("sys", "mem_limit", "'" + SystemConfig["system_memory"] + "'");
                    else
                        ini.WriteValue("sys", "mem_limit", "'128'");

                    if (!string.IsNullOrEmpty(eepromPath))
                        ini.WriteValue("sys.files", "eeprom_path", "'" + eepromPath + "'");

                    if (!string.IsNullOrEmpty(hddPath))
                        ini.WriteValue("sys.files", "hdd_path", "'" + hddPath + "'");

                    if (!string.IsNullOrEmpty(flashPath))
                        ini.WriteValue("sys.files", "flashrom_path", "'" + flashPath + "'");

                    if (!string.IsNullOrEmpty(bootRom))
                        ini.WriteValue("sys.files", "bootrom_path", "'" + bootRom + "'");

                    ini.WriteValue("sys.files", "dvd_path", "'" + rom + "'");
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

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "xemu", InputKey.hotkey | InputKey.start, "(%{KILL})");
        }
    }
}
