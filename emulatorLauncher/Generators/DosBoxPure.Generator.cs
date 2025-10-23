using EmulatorLauncher.Common;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EmulatorLauncher
{
    class DosBoxPureGenerator : Generator
    {
        public DosBoxPureGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("dosbox-pure");
            if (string.IsNullOrEmpty(path))
                return null;
                        
            string exe = Path.Combine(path, "DOSBoxPure.exe");
            if (!File.Exists(exe))
                return null;

            SetupConfiguration(path);
            SearchAndCopySoundFiles(path);

            List<string> commandArray = new List<string>
            {
                StringExtensions.QuoteString(rom)
            };

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfiguration(string path)
        {
            try
            {
                bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

                string dosBoxcfg = Path.Combine(path, "DOSBoxPure.cfg");
                JObject root;

                if (File.Exists(dosBoxcfg))
                {
                    string jsonText = File.ReadAllText(dosBoxcfg);
                    root = JObject.Parse(jsonText);
                }
                else
                {
                    root = new JObject { };
                }

                root["screen_fullscreen"] = fullscreen ? "true" : "false";

                // EMULATION
                SetOrRemove(root, "dosbox_pure_conf", "dosbox_pure_conf", "false", "false");
                SetOrRemove(root, "dosbox_pure_cycles", "dosbox_pure_cycles", "", "");
                SetOrRemove(root, "dosbox_pure_perfstats", "dosbox_pure_perfstats", "none", "none");
                SetOrRemove(root, "dosbox_pure_memory_size", "dosbox_pure_memory_size", "16", "16");
                SetOrRemove(root, "dosbox_pure_cpu_type", "dosbox_pure_cpu_type", "", "");
                SetOrRemove(root, "dosbox_pure_cpu_core", "dosbox_pure_cpu_core", "", "");
                SetOrRemove(root, "dosbox_pure_bootos_ramdisk", "dosbox_pure_bootos_ramdisk", "false", "false");
                if (SystemConfig.getOptBoolean("dosbox_pure_bootos_forcenormal"))
                    root["dosbox_pure_bootos_forcenormal"] = "true";
                else
                    root.Remove("dosbox_pure_bootos_forcenormal");
                
                // CONTROLS
                if (SystemConfig.getOptBoolean("fastforward_toggle"))
                    root.Remove("interface_speedtoggle");
                else
                    root["interface_speedtoggle"] = "hold";

                SetOrRemove(root, "dosbox_pure_mouse_input", "dosbox_pure_mouse_input", "", "");
                SetOrRemove(root, "dosbox_pure_auto_mapping", "dosbox_pure_auto_mapping", "true", "true");
                SetOrRemove(root, "dosbox_pure_keyboard_layout", "dosbox_pure_keyboard_layout", "us", "us");

                // VIDEO
                SetOrRemove(root, "dosbox_pure_machine", "dosbox_pure_machine", "svga", "svga");
                SetOrRemove(root, "dosbox_pure_svga", "dosbox_pure_svga", "svga_s3", "svga_s3");
                SetOrRemove(root, "dosbox_pure_svgamem", "dosbox_pure_svgamem", "2", "2");
                SetOrRemove(root, "dosbox_pure_voodoo", "dosbox_pure_voodoo", "8mb", "8mb");
                SetOrRemove(root, "dosbox_pure_voodoo_perf", "dosbox_pure_voodoo_perf", "4", "4");
                SetOrRemove(root, "dosbox_pure_voodoo_scale", "dosbox_pure_voodoo_scale", "1", "1");
                SetOrRemove(root, "interface_scaling", "dosbox_interface_scaling", "sharp", "sharp");
                SetOrRemove(root, "interface_crtfilter", "dosbox_interface_crtfilter", "0", "0");
                SetOrRemove(root, "dosbox_pure_aspect_correction", "dosbox_pure_aspect_correction", "false", "false");
                SetOrRemove(root, "dosbox_pure_overscan", "dosbox_pure_overscan", "0", "0");

                // AUDIO
                SetOrRemove(root, "dosbox_pure_sblaster_conf", "dosbox_sblaster_conf", "A220 I7 D1 H5", "A220 I7 D1 H5");
                SetOrRemove(root, "dosbox_pure_midi", "dosbox_midi", "", "");
                SetOrRemove(root, "dosbox_pure_sblaster_type", "dosbox_pure_sblaster_type", "sb16", "sb16");
                SetOrRemove(root, "dosbox_pure_sblaster_adlib_mode", "dosbox_pure_sblaster_adlib_mode", "", "");
                SetOrRemove(root, "dosbox_pure_sblaster_adlib_emu", "dosbox_pure_sblaster_adlib_emu", "default", "default");
                
                if (SystemConfig.getOptBoolean("dosbox_pure_gus"))
                    root["dosbox_pure_gus"] = "true";
                else
                    root.Remove("dosbox_pure_gus");

                if (SystemConfig.getOptBoolean("dosbox_pure_tandysound"))
                    root["dosbox_pure_tandysound"] = "on";
                else
                    root.Remove("dosbox_pure_tandysound");

                string jsonString = root.ToString(Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(dosBoxcfg, jsonString);
            }
            catch
            {
                SimpleLogger.Instance.Warning("[WARNING] Failed to update DosBox Pure configuration.");
            }
        }

        private void SearchAndCopySoundFiles(string path)
        {
            string targetPath = Path.Combine(path, "system");
            FileTools.EnsureDirectoryExists(targetPath);

            try
            {
                string mtcontrolsource = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "MT32_CONTROL.ROM");
                string mtcontroltarget = Path.Combine(targetPath, "MT32_CONTROL.ROM");
                if (!File.Exists(mtcontroltarget) && File.Exists(mtcontrolsource))
                    try { File.Copy(mtcontrolsource, mtcontroltarget); } catch { }

                string mtpcmsource = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "MT32_PCM.ROM");
                string mtpcmtarget = Path.Combine(targetPath, "MT32_PCM.ROM");
                if (!File.Exists(mtpcmtarget) && File.Exists(mtpcmsource))
                    try { File.Copy(mtpcmsource, mtpcmtarget); } catch { }

                string roland55source = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "Roland_SC-55.sf2");
                string roland55target = Path.Combine(targetPath, "Roland_SC-55.sf2");
                if (!File.Exists(roland55target) && File.Exists(roland55source))
                    try { File.Copy(roland55source, roland55target); } catch { }
                
                string roland88source = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "Roland_SC-88.sf2");
                string roland88target = Path.Combine(targetPath, "Roland_SC-88.sf2");
                if (!File.Exists(roland88target) && File.Exists(roland88source))
                    try { File.Copy(roland88source, roland88target); } catch { }
            }
            catch
            {
                SimpleLogger.Instance.Warning("[WARNING] Failed to copy MIDI sound file for DosBox Pure.");
            }
        }

        private void SetOrRemove(JObject root, string setting, string feature, string emptyValue, string defaultValue)
        {
            if (SystemConfig.isOptSet(feature) && !string.IsNullOrEmpty(SystemConfig[feature]))
            {
                if (SystemConfig[feature] == emptyValue)
                    root.Remove(setting);
                else
                    root[setting] = SystemConfig[feature];
            }
            else if (defaultValue != emptyValue)
                root[setting] = defaultValue;
            else
                root.Remove(setting);
        }
    }
}
