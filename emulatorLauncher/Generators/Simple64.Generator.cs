using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    // Generator for Simple64
    partial class Simple64Generator : Generator
    {
        public Simple64Generator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("simple64");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "simple64-gui.exe");
            if (!File.Exists(exe))
                return null;

            // Configure the emulator
            SetupGUIConfig(path, rom);
            SetupConfiguration(path, system);
            CreateControllerConfiguration(path);

            List<string> commandArray = new List<string>();

            if (!SystemConfig.isOptSet("show_gui") || !SystemConfig.getOptBoolean("show_gui"))
                commandArray.Add("--nogui");

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupGUIConfig(string path, string rom)
        {
            string guiConf = Path.Combine(path, "simple64-gui.ini");
            using (var ini = IniFile.FromFile(guiConf))
            {
                BindIniFeature(ini, "General", "inputPlugin", "simple64_inputplugin", "simple64-input-qt.dll");

                ini.WriteValue("General", "configDirPath", path.Replace("\\", "/"));

                string romPath = Path.Combine(Path.GetDirectoryName(rom));
                ini.WriteValue("General", "ROMdir", romPath.Replace("\\", "/"));
            }
        }

        private void SetupConfiguration(string path, string system)
        {
            string conf = Path.Combine(path, "mupen64plus.cfg");

            using (var ini = IniFile.FromFile(conf, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                // Paths
                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "Simple64");
                if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                    catch { }
                ini.WriteValue("Core", "ScreenshotPath", screenshotPath.Replace("\\", "/"));

                string saveStatePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "state");
                if (!Directory.Exists(saveStatePath)) try { Directory.CreateDirectory(saveStatePath); }
                    catch { }
                ini.WriteValue("Core", "SaveStatePath", saveStatePath.Replace("\\", "/"));

                string saveSRAMPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "sram");
                if (!Directory.Exists(saveSRAMPath)) try { Directory.CreateDirectory(saveSRAMPath); }
                    catch { }
                ini.WriteValue("Core", "SaveSRAMPath", saveSRAMPath.Replace("\\", "/"));

                // Default settings
                BindBoolIniFeature(ini, "Core", "DisableExtraMem", "n64_mempack", "True", "False");
                ini.WriteValue("Core", "AutoStateSlotIncrement", "True");

                // Parallel options
                bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
                ini.WriteValue("Video-Parallel", "Fullscreen", fullscreen ? "True" : "False");

                // Vsync
                if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                    ini.WriteValue("Video-Parallel", "VSync", "0");
                else
                    ini.WriteValue("Video-Parallel", "VSync", "1");

                // Widescreen
                BindBoolIniFeature(ini, "Video-Parallel", "WidescreenStretch", "n64_widescreen", "True", "False");

                // Anti-aliasing
                BindBoolIniFeature(ini, "Video-Parallel", "VIAA", "antialiasing", "True", "False");

                // Upscaling
                if (SystemConfig.isOptSet("parallel_upscaling") && !string.IsNullOrEmpty(SystemConfig["parallel_upscaling"]))
                    ini.WriteValue("Video-Parallel", "Upscaling", SystemConfig["parallel_upscaling"]);
                else
                    ini.WriteValue("Video-Parallel", "Upscaling", "1");
            }
        }
    }
}
