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
            { "rpcs3", () => new Rpcs3Generator() },  
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
            SystemConfig.ImportOverrides(SystemConfig.LoadAll("global"));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"]));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"] + "[\"" + Path.GetFileNameWithoutExtension(SystemConfig["rom"]) + "\"]"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));

            if (!SystemConfig.isOptSet("rom"))
                return;

            if (!File.Exists(SystemConfig["rom"]))
                return;

            if (!SystemConfig.isOptSet("system"))
                return;

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemDefaults.GetDefaultEmulator(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["core"]))
                SystemConfig["core"] = SystemDefaults.GetDefaultCore(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemConfig["system"];

            Generator generator = generators.Where(g => g.Key == SystemConfig["emulator"]).Select(g => g.Value()).FirstOrDefault();
            if (generator == null)
                generator = generators.Where(g => g.Key == SystemConfig["system"]).Select(g => g.Value()).FirstOrDefault();

            if (generator != null)
            {
                ProcessStartInfo path = generator.Generate(SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"], SystemConfig["rom"], null, null);
                if (path != null)
                {
                    path.UseShellExecute = true;                    

                    try { Process.Start(path).WaitForExit(); }
                    catch { }

                    generator.Cleanup();
                }              
            }
        }
    }
}
