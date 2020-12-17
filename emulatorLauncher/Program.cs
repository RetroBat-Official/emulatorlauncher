using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using emulatorLauncher.libRetro;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Text.RegularExpressions;
using System.Management;
using System.Globalization;
using emulatorLauncher.PadToKeyboard;
using System.Runtime.InteropServices;

// XBox
// -p1index 0 -p1guid 030000005e040000ea02000000007801 -p1name "XBox One S Controller" -p1nbbuttons 11 -p1nbhats 1 -p1nbaxes 6 -system pcengine -emulator libretro -core mednafen_supergrafx -rom "H:\[Emulz]\roms\pcengine\1941 Counter Attack.pce"
// 8bitdo
// -p1index 0 -p1guid 030000004c050000c405000000006800 -p1name "PS4 Controller" -p1nbbuttons 16 -p1nbhats 0 -p1nbaxes 6  -system pcengine -emulator libretro -core mednafen_supergrafx -rom "H:\[Emulz]\roms\pcengine\1941 Counter Attack.pce"

// fbinball 8bitdo
// -p1index 0 -p1guid 030000005e040000e002000000007801 -p1name "Xbox One S Controller" -p1nbbuttons 11 -p1nbhats 1 -p1nbaxes 6  -system fpinball -emulator fpinball -core fpinball -rom "H:\[Emulz]\roms\fpinball\Big Shot (Gottlieb 1973).fpt"

// -p1index 0 -p1guid 030000001008000001e5000000000000 -p1name "usb gamepad           " -p1nbbuttons 10 -p1nbhats 0 -p1nbaxes 2  -system pcengine -emulator libretro -core mednafen_supergrafx -rom "H:\[Emulz]\roms\pcengine\1941 Counter Attack.pce"

/// -p1index 0 -p1guid 030000005e040000e002000000007801 -p1name "Xbox One S Controller" -p1nbbuttons 11 -p1nbhats 1 -p1nbaxes 6  -system gamecube -emulator dolphin -core  -rom "H:\[Emulz]\roms\gamecube\Mario Kart Double Dash.gcz"

/// -p1index 0 -p1guid 03000000b50700000399000000000000 -p1name "2 axis 12 bouton boÃ®tier de commande" -p1nbbuttons 12 -p1nbhats 0 -p1nbaxes 2  -system atari2600 -emulator libretro -core stella -rom "H:\[Emulz]\roms\atari2600\Asteroids (USA).7z"
/// 
namespace emulatorLauncher
{
    static class Program
    {
        static Dictionary<string, Func<Generator>> generators = new Dictionary<string, Func<Generator>>
        {
            { "libretro", () => new LibRetroGenerator() }, { "angle", () => new LibRetroGenerator() },
			{ "amigaforever", () => new AmigaForeverGenerator() },
			{ "duckstation", () => new DuckstationGenerator() },
			{ "kega-fusion", () => new KegaFusionGenerator() },
			{ "mesen", () => new MesenGenerator() },
			{ "mgba", () => new mGBAGenerator() },			
            { "model2", () => new Model2Generator() },
            { "model3", () => new Model3Generator() }, { "supermodel", () => new Model3Generator() },
            { "openbor", () => new OpenBorGenerator() },
            { "ps3", () => new Rpcs3Generator() },  { "rpcs3", () => new Rpcs3Generator() },  
            { "ps2", () => new Pcsx2Generator() },  { "pcsx2", () => new Pcsx2Generator() },  
            { "fpinball", () => new FpinballGenerator() }, { "bam", () => new FpinballGenerator() },
            { "vpinball", () => new VPinballGenerator() },
            { "dosbox", () => new DosBoxGenerator() },
            { "ppsspp", () => new PpssppGenerator() },
			{ "project64", () => new Project64Generator() },
            { "dolphin", () => new DolphinGenerator() },
            { "cemu", () => new CemuGenerator() },  { "wiiu", () => new CemuGenerator() },  
            { "winuae", () => new UaeGenerator() },
            { "applewin", () => new AppleWinGenerator() }, { "apple2", () => new AppleWinGenerator() },
            { "simcoupe", () => new SimCoupeGenerator() },               
            { "cxbx", () => new CxbxGenerator() }, { "chihiro", () => new CxbxGenerator() }, { "xbox", () => new CxbxGenerator() },               
            { "redream", () => new RedreamGenerator() },                  
            { "mugen", () => new ExeLauncherGenerator() }, { "windows", () => new ExeLauncherGenerator() }, 
            { "demul", () => new DemulGenerator() }, { "demul-old", () => new DemulGenerator() }, 
            { "mednafen", () => new MednafenGenerator() },
            { "daphne", () => new DaphneGenerator() },
			{ "raine", () => new RaineGenerator() },
			{ "snes9x", () => new Snes9xGenerator() },
			{ "citra", () => new CitraGenerator() },
            { "xenia", () => new XeniaGenerator() },
            { "mame64", () => new Mame64Generator() },
            { "oricutron", () => new OricutronGenerator() },
            { "switch", () => new YuzuGenerator() }, { "yuzu", () => new YuzuGenerator() },
            { "ryujinx", () => new RyujinxGenerator() },
            { "teknoparrot", () => new TeknoParrotGenerator() },            
            { "solarus", () => new SolarusGenerator() }            
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

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info(Environment.CommandLine);

            try { SetProcessDPIAware(); }
            catch { }

            LocalPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            AppConfig = ConfigFile.FromFile(Path.Combine(LocalPath, "emulatorLauncher.cfg"));
            AppConfig.ImportOverrides(ConfigFile.FromArguments(args));

            SystemConfig = ConfigFile.LoadEmulationStationSettings(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_settings.cfg"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll("global"));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"]));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"] + "[\"" + Path.GetFileName(SystemConfig["rom"]) + "\"]"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));

            var inputConfig = LoadControllerConfiguration(args);
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

                        PadToKey mapping = null;
                        if (generator.UsePadToKey)
                            mapping = PadToKey.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_padtokey.cfg"));

                        mapping = LoadGamePadToKeyMapping(path, mapping);
                        mapping = generator.SetupCustomPadToKeyMapping(mapping);

                        using (new HighPerformancePowerScheme())
                        using (new JoystickListener(inputConfig, mapping))
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

        private static PadToKey LoadGamePadToKeyMapping(ProcessStartInfo path, PadToKey mapping)
        {
            string filePath = SystemConfig["rom"] + (Directory.Exists(SystemConfig["rom"]) ? "\\padto.keys" : ".keys");

            EvMapyKeysFile gameMapping = EvMapyKeysFile.TryLoad(filePath);
            if (gameMapping == null && SystemConfig["system"] != null)
            {
                var systemMapping = Path.Combine(Program.LocalPath, ".emulationstation", "padtokey", SystemConfig["system"] + ".keys");
                if (!File.Exists(systemMapping))
                    systemMapping = Path.Combine(Program.AppConfig.GetFullPath("padtokey"), SystemConfig["system"] + ".keys");

                if (File.Exists(systemMapping))
                    gameMapping = EvMapyKeysFile.TryLoad(systemMapping);
            }

            if (gameMapping == null || gameMapping.Count == 0)
                return mapping;

            PadToKeyApp app = new PadToKeyApp();
            app.Name = Path.GetFileNameWithoutExtension(path.FileName).ToLower();

            foreach (var player in gameMapping)
            {
                foreach (var action in player)
                {
                    if (action.type == "mouse")
                    {
                        if (action.Triggers == null || action.Triggers.Length == 0)
                            continue;

                        if (action.Triggers.FirstOrDefault() == "joystick1")
                        {
                            PadToKeyInput mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.leftanalogleft;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "X";
                            app.Input.Add(mouseInput);

                            mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.leftanalogup;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "Y";
                            app.Input.Add(mouseInput);
                        }
                        else if (action.Triggers.FirstOrDefault() == "joystick2")
                        {
                            PadToKeyInput mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.rightanalogleft;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "X";
                            app.Input.Add(mouseInput);

                            mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.rightanalogup;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "Y";
                            app.Input.Add(mouseInput);
                        }
                        
                        continue;
                    }

                    if (action.type != "key")
                        continue;

                    InputKey k;
                    if (!Enum.TryParse<InputKey>(string.Join(", ", action.Triggers.ToArray()).ToLower(), out k))
                        continue;

                    PadToKeyInput input = new PadToKeyInput();
                    input.Name = k;

                    foreach (var target in action.Targets)
                    {
                        LinuxScanCode sc;
                        if (!Enum.TryParse<LinuxScanCode>(target.ToUpper(), out sc))
                            continue;

                        input.SetScanCode((uint)sc);
                    }

                    if (input.ScanCodes.Length > 0)
                        app.Input.Add(input);
                }
            }

            if (app.Input.Count > 0)
            {
                if (mapping == null)
                    mapping = new PadToKey();

                var existingApp = mapping.Applications.FirstOrDefault(a => a.Name == app.Name);
                if (existingApp != null)
                {
                    // Merge with existing by replacing inputs
                    foreach (var input in app.Input)
                    {
                        existingApp.Input.RemoveAll(i => i.Name == input.Name);
                        existingApp.Input.Add(input);
                    }
                }
                else
                    mapping.Applications.Add(app);
            }

            return mapping;
        }

        private static InputConfig[] LoadControllerConfiguration(string[] args)
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
                        case "guid": player.Guid = val.ToUpper(); break;
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
                            pi.Input = inputConfig.FirstOrDefault(c => c.DeviceGUID.ToUpper() == pi.Guid && c.DeviceName == pi.Name);
                            if (pi.Input == null)
                                pi.Input = inputConfig.FirstOrDefault(c => c.DeviceGUID.ToUpper() == pi.Guid);
                            if (pi.Input == null)
                                pi.Input = inputConfig.FirstOrDefault(c => c.DeviceName == pi.Name);
                            if (pi.Input == null)
                                pi.Input = inputConfig.FirstOrDefault(c => c.DeviceName == "Keyboard");
                        }
                    }

                          
                }

                return inputConfig;
            }
            catch { }

            return null;
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
