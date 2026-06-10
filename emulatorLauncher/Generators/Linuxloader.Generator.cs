using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class LinuxloaderGenerator : Generator
    {
        public LinuxloaderGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _path;
        private bool _sindenSoft;
        private string _romName;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("linuxloader");
            _path = path;

            string exe = Path.Combine(path, "linuxloader.exe");
            if (!File.Exists(exe))
                return null;

            _romName = Path.GetFileNameWithoutExtension(rom);

            // Check existence of config file, if not create it
            string controlsFile = Path.Combine(path, "controls.ini");
            if (!File.Exists(controlsFile))
            {
                try
                {
                    var linuxloaderCreateControls = new ProcessStartInfo()
                    {
                        FileName = exe,
                        WorkingDirectory = path,
                        Arguments = "--create controls",
                    };

                    using (var process = new Process())
                    {
                        process.StartInfo = linuxloaderCreateControls;
                        process.Start();
                        process.WaitForExit(5000);
                    }
                }
                catch { SimpleLogger.Instance.Error("[ERROR] Unable to create controls.ini file."); }
            }

            // check existence of config file, if not create it
            string configFile = Path.Combine(path, "linuxloader.ini");
            if (!File.Exists(configFile))
            {
                try
                {
                    var linuxloaderCreateConfig = new ProcessStartInfo()
                    {
                        FileName = exe,
                        WorkingDirectory = path,
                        Arguments = "--create config",
                    };

                    using (var process = new Process())
                    {
                        process.StartInfo = linuxloaderCreateConfig;
                        process.Start();
                        process.WaitForExit(5000);
                    }
                }
                catch { SimpleLogger.Instance.Error("[ERROR] Unable to create linuxloader.ini file."); }
            }

            string gamePath = GetGamePath(rom);

            if (string.IsNullOrEmpty(gamePath))
            {
                throw new Exception("Unable to find game path for " + rom);
            }

            List<string> commandArray = new List<string>();

            bool fullscreen = ShouldRunFullscreen();

            // Bezels
            if (fullscreen)
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            _resolution = resolution;

            SetupConfiguration(configFile, commandArray, gamePath, fullscreen);
            CreateControllerConfiguration(controlsFile, gamePath);

            if (File.Exists(configFile))
            {
                commandArray.Add("-c");
                commandArray.Add("\"" + configFile + "\"");
            }

            if (File.Exists(controlsFile) && !Program.SystemConfig.getOptBoolean("disableautocontrollers"))
            {
                commandArray.Add("-o");
                commandArray.Add("\"" + controlsFile + "\"");
            }
            else if (SystemConfig.isOptSet("ll_controlconfig") && !string.IsNullOrEmpty(SystemConfig["ll_controlconfig"]))
            {
                string customControls = SystemConfig["ll_controlconfig"];
                if (File.Exists(customControls))
                {
                    commandArray.Add("-o");
                    commandArray.Add("\"" + customControls + "\"");
                }
            }

            string controllerDBFile = Path.Combine(path, "gamecontrollerdb.txt");
            if (File.Exists(controllerDBFile))
            {
                commandArray.Add("-d");
                commandArray.Add("\"" + controllerDBFile + "\"");
            }

            commandArray.Add("-g");
            commandArray.Add("\"" + gamePath + "\"");

            if (SystemConfig.getOptBoolean("ll_testmode"))
                commandArray.Add("-t");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private string GetGamePath(string rom)
        {
            if (File.Exists(rom))
                return Path.GetDirectoryName(rom);
            
            else if (Directory.Exists(rom))
            {
                string gamesConfig = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "tools", "linuxloaderconfig.yml");
                if (File.Exists(gamesConfig))
                {
                    try 
                    {
                        var knownGames = ParseLauncherPaths(gamesConfig);
                        string gameName = Path.GetFileNameWithoutExtension(rom);
                        string launcherPath = knownGames.TryGetValue(gameName, out var p) ? p : null;

                        if (string.IsNullOrEmpty(launcherPath))
                            return Path.Combine(rom, "disk0");
                        else
                            return Path.Combine(rom, "disk0", launcherPath);
                    }
                    catch 
                    { 
                        SimpleLogger.Instance.Error("[ERROR] Unable to parse linuxloaderconfig.yml file.");
                        return Path.Combine(rom, "disk0");
                    }
                }
                else
                    return Path.Combine(rom, "disk0");
            }
            else
                return string.Empty;
        }

        private void SetupConfiguration(string conf, List<string> commandArray, string gamePath, bool fullscreen)
        {
            try
            {
                string gameConfigFile = Path.Combine(gamePath, "linuxloader.ini");
                if (File.Exists(gameConfigFile))
                {
                    AddFileForRestoration(gameConfigFile);
                    try { File.Delete(gameConfigFile); }
                    catch { }
                }

                using (IniFile ini = new IniFile(conf, IniOptions.UseSpaces | IniOptions.KeepEmptyLines))
                {
                    ini.WriteValue("Display", "FULLSCREEN", fullscreen ? "true" : "false");

                    BindBoolIniFeatureOn(ini, "Display", "KEEP_ASPECT_RATIO", "ll_keepratio", "true", "false");
                    BindBoolIniFeatureOn(ini, "Display", "HIDE_CURSOR", "ll_hide_cursor", "true", "false");

                    if (SystemConfig.getOptBoolean("ll_sindenborder"))
                    {
                        ini.WriteValue("Display", "BORDER_ENABLED", "true");
                        ini.WriteValue("Display", "WHITE_BORDER_PERCENTAGE", "2");
                        ini.WriteValue("Display", "BLACK_BORDER_PERCENTAGE", "0");
                    }
                    else
                    {
                        ini.WriteValue("Display", "BORDER_ENABLED", "false");
                    }

                    ini.WriteValue("Input", "INPUT_MODE", "1");

                    BindIniFeature(ini, "Emulation", "REGION", "ll_region", "EX");
                    BindBoolIniFeature(ini, "Emulation", "FREEPLAY", "ll_freeplay", "true", "none");

                    if (SystemConfig.getOptBoolean("ll_crosshair"))
                    {
                        ini.WriteValue("CrossHairs", "ENABLE_CROSSHAIRS", "true");
                        ini.WriteValue("CrossHairs", "GSEVO_CROSSHAIR_ALWAYS_ON", "true");
                        ini.WriteValue("CrossHairs", "GSEVO_CROSSHAIR_ALWAYS_OFF", "false");
                    }
                    else
                    {
                        ini.WriteValue("CrossHairs", "ENABLE_CROSSHAIRS", "false");
                        ini.WriteValue("CrossHairs", "GSEVO_CROSSHAIR_ALWAYS_ON", "false");
                        ini.WriteValue("CrossHairs", "GSEVO_CROSSHAIR_ALWAYS_OFF", "true");
                    }

                    // crosshairs
                    string cross1Path = Path.Combine(_path, "cross", "cross1.png");
                    if (SystemConfig.isOptSet("ll_crosshair1") && !string.IsNullOrEmpty(SystemConfig["ll_crosshair1"]))
                        cross1Path = SystemConfig["ll_crosshair1"];                        

                    if (!File.Exists(cross1Path))
                    {
                        string templateCross1 = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "crosshairs", "cross1.png");
                        if (File.Exists(templateCross1))
                            try { File.Copy(templateCross1, cross1Path); } catch { }
                    }

                    if (SystemConfig.getOptBoolean("ll_crosshair"))
                        ini.WriteValue("CrossHairs", "P1_CROSSHAIR_PATH", "\"" + cross1Path + "\"");
                    else
                        ini.WriteValue("CrossHairs", "P1_CROSSHAIR_PATH", "\"\"");
                }
            }
            catch { SimpleLogger.Instance.Error("[ERROR] Unable to save config ini file."); }
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            PadToKey.AddOrUpdateKeyMapping(mapping, "linuxloader", InputKey.hotkey | InputKey.start, "(%{CLOSE})");

            return mapping;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            Process process = Process.Start(path);
            Job.Current.AddProcess(process);
            
            process.WaitForExit();

            try
            {
                bezel?.Dispose();
                ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, _path);
            }
            catch { }

            return 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            if (_demulshooter)
                Demulshooter.KillDemulShooter();

            if (_sindenSoft)
                Guns.KillSindenSoftware();
        }

        public static Dictionary<string, string> ParseLauncherPaths(string ymlPath)
        {
            var yml = YmlFile.Load(ymlPath);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in yml.Elements)
            {
                var container = element as YmlContainer;
                if (container == null)
                    continue;

                string launcherPath = container["launcher_path"]?.Trim('"') ?? "";

                if (string.IsNullOrEmpty(container.Name))
                    continue;

                result[container.Name] = launcherPath;
            }

            return result;
        }
    }
}
