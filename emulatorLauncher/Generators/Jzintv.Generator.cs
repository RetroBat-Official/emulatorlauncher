using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class JZintvGenerator : Generator
    {
        public JZintvGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "jzintv.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            List<string> commandArray = new List<string>();

            // exec.bin and grom.bin specifications
            string gromPath = Path.Combine(AppConfig.GetFullPath("bios"), "grom.bin");
            string execPath = Path.Combine(AppConfig.GetFullPath("bios"), "exec.bin");

            if (!File.Exists(gromPath) || !File.Exists(execPath))
                throw new ApplicationException("grom.bin or exec.bin not found in bios folder!");

            commandArray.Add("-e");
            commandArray.Add("\"" + execPath + "\"");
            commandArray.Add("-g");
            commandArray.Add("\"" + gromPath + "\"");

            // Command file management (overrides other commands)
            string commandFile = rom.Replace(Path.GetExtension(rom), ".commands");

            if (!File.Exists(commandFile))
                commandFile = Path.Combine(AppConfig.GetFullPath("roms"),"intellivision", "default.commands");

            if (File.Exists(commandFile))
            {
                var lines = File.ReadAllLines(commandFile);

                if (lines.Length > 0)
                {
                    foreach (var line in lines)
                        line.Trim();
                }
            }
            else
            {
                // ECS option
                if (SystemConfig.getOptBoolean("jzintv_useECS"))
                {
                    string ecsPath = Path.Combine(AppConfig.GetFullPath("bios"), "ecs.bin");

                    if (File.Exists(ecsPath))
                    {
                        commandArray.Add("-E");
                        commandArray.Add("\"" + ecsPath + "\"");
                        commandArray.Add("-s1");
                    }

                    else
                    {
                        SimpleLogger.Instance.Warning("[WARN] ECS option enabled but ecs.bin not found in bios folder!");
                        commandArray.Add("-s0");
                    }
                }
                else
                    commandArray.Add("-s0");

                // PAL option
                if (SystemConfig.getOptBoolean("jzintv_usePAL"))
                    commandArray.Add("-p");

                // Resolution
                if (SystemConfig.isOptSet("jzintv_internal_resolution") && !string.IsNullOrEmpty(SystemConfig["jzintv_internal_resolution"]))
                    commandArray.Add("-z" + SystemConfig["jzintv_internal_resolution"]);
                else if (resolution != null)
                {
                    if (SystemConfig.isOptSet("jzintv_bitdepth") && !string.IsNullOrEmpty(SystemConfig["jzintv_bitdepth"]))
                        commandArray.Add("-z" + resolution.Width + "x" + resolution.Height + "," + SystemConfig["jzintv_bitdepth"]);
                    else
                        commandArray.Add("-z" + resolution.Width + "x" + resolution.Height);
                }
                else
                {
                    if (SystemConfig.isOptSet("jzintv_bitdepth") && !string.IsNullOrEmpty(SystemConfig["jzintv_bitdepth"]))
                        commandArray.Add("-z" + ScreenResolution.CurrentResolution.Width + "x" + ScreenResolution.CurrentResolution.Height + "," + SystemConfig["jzintv_bitdepth"]);
                    else
                        commandArray.Add("-z" + ScreenResolution.CurrentResolution.Width + "x" + ScreenResolution.CurrentResolution.Height);
                }

                // GRAM size
                if (SystemConfig.isOptSet("jzintv_gram_size") && !string.IsNullOrEmpty(SystemConfig["jzintv_gram_size"]))
                    commandArray.Add("-G" + SystemConfig["jzintv_gram_size"]);
            }

            // Fullscreen
            if (fullscreen)
                commandArray.Add("-f1");
            else
                commandArray.Add("-f0");

            // Controls configuration
            if (SystemConfig.isOptSet("jzintv_inputfile") && !string.IsNullOrEmpty("jzintv_inputfile"))
            {
                SimpleLogger.Instance.Info("[INFO] Using user KeyboardHack file.");
                commandArray.Add("--kbdhackfile=\"" + SystemConfig["jzintv_inputfile"] + "\"");
            }
            else
            {
                ConfigureControllers(commandArray, path);
            }

            // rom
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int ret = base.RunAndWait(path);

            if (ret == 1)
            {
                return 0;
            }
            return ret;
        }
    }
}
