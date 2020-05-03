using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using emulatorLauncher.libRetro;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    static class Program
    {
        static Dictionary<string, Func<Generator>> generators = new Dictionary<string, Func<Generator>>
        {
            { "libretro", () => new LibRetroGenerator() },
            { "model2", () => new Model2Generator() },
            { "openbor", () => new OpenBorGenerator() },
            { "ps3", () => new Rpcs3Generator() },  
            { "ps2", () => new Pcsx2Generator() },  { "pcsx2", () => new Pcsx2Generator() },  
            { "fpinball", () => new FpinballGenerator() }, { "bam", () => new FpinballGenerator() },
            { "vpinball", () => new VPinballGenerator() },
            { "dosbox", () => new DosBoxGenerator() },
            { "ppsspp", () => new PpssppGenerator() },
            { "dolphin", () => new DolphinGenerator() },
            
    /*,
            'moonlight': MoonlightGenerator(),
            'scummvm': ScummVMGenerator(),
            'mupen64plus': MupenGenerator(),
            'vice': ViceGenerator(),
            'fsuae': FsuaeGenerator(),
            'amiberry': AmiberryGenerator(),
            'reicast': ReicastGenerator(),
            'citra' : CitraGenerator()*/
        };

        public static ConfigFile AppConfig { get; private set; }
        public static string LocalPath { get; private set; }
        public static ConfigFile SystemConfig { get; private set; }
        public static List<Controller> Controllers { get; set; }

        public static bool EnableHotKeyStart
        {
            get
            {
                return Process.GetProcessesByName("JoyToKey").Length == 0;
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info(Environment.CommandLine);

            LocalPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            AppConfig = ConfigFile.FromFile(Path.Combine(LocalPath, "emulatorLauncher.cfg"));

            SystemConfig = ConfigFile.LoadEmulationStationSettings(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_settings.cfg"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll("global"));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"]));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"] + "[\"" + Path.GetFileNameWithoutExtension(SystemConfig["rom"]) + "\"]"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));

            LoadControllerConfiguration(args);
            ImportShaderOverrides();

            if (!SystemConfig.isOptSet("rom"))
            {
                SimpleLogger.Instance.Error("rom not set");
                return;
            }

            if (!File.Exists(SystemConfig.GetFullPath("rom")) && !Directory.Exists(SystemConfig.GetFullPath("rom")))
            {
                SimpleLogger.Instance.Error("rom does not exist");
                return;
            }

            if (!SystemConfig.isOptSet("system"))
            {
                SimpleLogger.Instance.Error("system not set");
                return;
            }

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemDefaults.GetDefaultEmulator(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["core"]))
                SystemConfig["core"] = SystemDefaults.GetDefaultCore(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemConfig["system"];
            
            Generator generator = generators.Where(g => g.Key == SystemConfig["emulator"]).Select(g => g.Value()).FirstOrDefault();
            if (generator == null && !string.IsNullOrEmpty(SystemConfig["emulator"]) && SystemConfig["emulator"].StartsWith("lr-"))
                generator = new LibRetroGenerator();
            if (generator == null)
                generator = generators.Where(g => g.Key == SystemConfig["system"]).Select(g => g.Value()).FirstOrDefault();

            if (generator != null)
            {
                using (var screenResolution = ScreenResolution.Parse(SystemConfig["videomode"]))
                {
                    ProcessStartInfo path = generator.Generate(SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"], SystemConfig["rom"], null, screenResolution);
                    if (path != null)
                    {
                        if (path.Arguments != null)
                            SimpleLogger.Instance.Info("->  " + path.FileName + " " + path.Arguments);
                        else
                            SimpleLogger.Instance.Info("->  " + path.FileName);

                        path.UseShellExecute = true;

                        if (screenResolution != null && generator.DependsOnDesktopResolution)
                            screenResolution.Apply();

                        Cursor.Position = new System.Drawing.Point(Screen.PrimaryScreen.Bounds.Right, Screen.PrimaryScreen.Bounds.Bottom / 2);

                        generator.RunAndWait(path);
                    }
                    else
                        SimpleLogger.Instance.Error("generator failed");
                }

                generator.Cleanup();
            }
            else
                SimpleLogger.Instance.Error("cant find generator");
        }

        private static void LoadControllerConfiguration(string[] args)
        {
            Controllers = new List<Controller>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-p") && args[i].Length > 3)
                {
                    int playerId;
                    int.TryParse(args[i].Substring(2, 1), out playerId);

                    Controller player = Controllers.FirstOrDefault(c => c.Index == playerId);
                    if (player == null)
                    {
                        player = new Controller() { Index = playerId };
                        Controllers.Add(player);
                    }

                    if (args.Length < i + 1)
                        break;

                    string var = args[i].Substring(3);
                    string val = args[i + 1];
                    if (val.StartsWith("-"))
                        continue;

                    switch (var)
                    {
                        case "guid": player.Guid = val; break;
                        case "name": player.Name = val; break;
                        case "nbbuttons": player.NbButtons = val.ToInteger(); break;
                        case "nbhats": player.NbHats = val.ToInteger(); break;
                        case "nbaxes": player.NbAxes = val.ToInteger(); break;
                    }
                }
            }

            try
            {
                var inputConfig = InputList.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_input.cfg"));
                if (inputConfig != null)
                {
                    if (!Controllers.Any())
                    {
                        var pi = new Controller() { Index = 1 };
                        pi.Input = inputConfig.FirstOrDefault(c => c.DeviceName == "Keyboard");
                        if (pi.Input != null)
                            Controllers.Add(pi);
                    }
                    else
                    {
                        foreach (var pi in Controllers)
                        {
                            pi.Input = inputConfig.FirstOrDefault(c => c.DeviceGUID == pi.Guid && c.DeviceName == pi.Name);
                            if (pi.Input == null)
                                pi.Input = inputConfig.FirstOrDefault(c => c.DeviceName == "Keyboard");
                        }
                    }
                }
            }
            catch { }
        }

        private static void ImportShaderOverrides()
        {
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shaderset") && SystemConfig["shaderset"] != "none")
            {
                string path = Path.Combine(AppConfig.GetFullPath("shaders"), "configs", SystemConfig["shaderset"], "rendering-defaults.yml");
                if (File.Exists(path))
                {
                    string renderconfig = SystemShaders.GetShader(File.ReadAllText(path), SystemConfig["system"]);
                    if (!string.IsNullOrEmpty(renderconfig))
                        SystemConfig["shader"] = renderconfig;
                }
            }
        }
    }

    class Controller
    {
        public int Index { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public int NbButtons { get; set; }
        public int NbHats { get; set; }
        public int NbAxes { get; set; }

        public InputConfig Input { get; set; }

        public override string ToString() { return Name; }
    }

}
