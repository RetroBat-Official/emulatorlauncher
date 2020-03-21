using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using emulatorLauncher.libRetro;
using System.IO;
using System.Diagnostics;

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
            { "ps2", () => new Pcsx2Generator() },  
            { "fpinball", () => new FpinballGenerator() },
            
    /*,
            'moonlight': MoonlightGenerator(),
            'scummvm': ScummVMGenerator(),
            'dosbox': DosBoxGenerator(),
            'mupen64plus': MupenGenerator(),
            'vice': ViceGenerator(),
            'fsuae': FsuaeGenerator(),
            'amiberry': AmiberryGenerator(),
            'reicast': ReicastGenerator(),
            'dolphin': DolphinGenerator(),
            'pcsx2': Pcsx2Generator(),
            'ppsspp': PPSSPPGenerator(),
            'citra' : CitraGenerator()*/
        };

        public static ConfigFile AppConfig { get; private set; }
        public static string LocalPath { get; private set; }
        public static ConfigFile SystemConfig { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
           // MessageBox.Show("attach");

            LocalPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            AppConfig = ConfigFile.FromFile(Path.Combine(LocalPath, "emulatorLauncher.cfg"));

            SystemConfig = ConfigFile.LoadEmulationStationSettings(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_settings.cfg"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll("global"));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"]));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"] + "[\"" + Path.GetFileNameWithoutExtension(SystemConfig["rom"]) + "\"]"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));

            ImportShaderOverrides();

            if (!SystemConfig.isOptSet("rom"))
            {
                SimpleLogger.Instance.Error("rom not set");
                return;
            }

            if (!File.Exists(SystemConfig["rom"]))
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
            if (generator == null)
                generator = generators.Where(g => g.Key == SystemConfig["system"]).Select(g => g.Value()).FirstOrDefault();

            if (generator == null && !string.IsNullOrEmpty(SystemConfig["emulator"]) && SystemConfig["emulator"].StartsWith("lr-"))
                generator = new LibRetroGenerator();

            if (generator != null)
            {
                string videoMode = SystemConfig["videomode"];
                ProcessStartInfo path = generator.Generate(SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"], SystemConfig["rom"], null, videoMode);
                if (path != null)
                {
                    path.UseShellExecute = true;

                    Cursor.Position = new System.Drawing.Point(Screen.PrimaryScreen.Bounds.Right, Screen.PrimaryScreen.Bounds.Bottom / 2);

                    try { Process.Start(path).WaitForExit(); }
                    catch { }

                    generator.Cleanup();
                }              
                else
                    SimpleLogger.Instance.Error("generator failed");
            }
            else
                SimpleLogger.Instance.Error("cant find generator");
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
}
