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
using System.Net;
using System.ComponentModel;
using Microsoft.Win32;
using System.Text;

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
            { "retrobat", () => new RetrobatLauncherGenerator() },
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
            { "ps2", () => new Pcsx2Generator() },  { "pcsx2", () => new Pcsx2Generator() }, { "pcsx2-16", () => new Pcsx2Generator() },
            { "fpinball", () => new FpinballGenerator() }, { "bam", () => new FpinballGenerator() },
            { "vpinball", () => new VPinballGenerator() },
            { "dosbox", () => new DosBoxGenerator() },
            { "ppsspp", () => new PpssppGenerator() },
			{ "project64", () => new Project64Generator() },
            { "dolphin", () => new DolphinGenerator() }, { "triforce", () => new DolphinGenerator() },
            { "cemu", () => new CemuGenerator() },  { "wiiu", () => new CemuGenerator() },  
            { "winuae", () => new UaeGenerator() },
            { "applewin", () => new AppleWinGenerator() }, { "apple2", () => new AppleWinGenerator() },
            { "gsplus", () => new GsPlusGenerator() }, { "apple2gs", () => new GsPlusGenerator() },
            { "simcoupe", () => new SimCoupeGenerator() },               
            { "cxbx", () => new CxbxGenerator() }, { "chihiro", () => new CxbxGenerator() }, { "xbox", () => new CxbxGenerator() },               
            { "redream", () => new RedreamGenerator() },                  
            { "mugen", () => new ExeLauncherGenerator() }, { "windows", () => new ExeLauncherGenerator() }, 
            { "demul", () => new DemulGenerator() }, { "demul-old", () => new DemulGenerator() }, 
            { "mednafen", () => new MednafenGenerator() },
            { "daphne", () => new DaphneGenerator() },
            { "hypseus", () => new HypseusGenerator() },            
			{ "raine", () => new RaineGenerator() },
			{ "snes9x", () => new Snes9xGenerator() },
			{ "citra", () => new CitraGenerator() },
			{ "pico8", () => new Pico8Generator() },
            { "xenia", () => new XeniaGenerator() },
            { "mame64", () => new Mame64Generator() },
            { "oricutron", () => new OricutronGenerator() },
            { "switch", () => new YuzuGenerator() }, { "yuzu", () => new YuzuGenerator() }, { "yuzu-early-access", () => new YuzuGenerator() },
            { "ryujinx", () => new RyujinxGenerator() },
            { "teknoparrot", () => new TeknoParrotGenerator() },    
            { "easyrpg", () => new EasyRpgGenerator() },                
            { "tsugaru", () => new TsugaruGenerator() },
			{ "love", () => new LoveGenerator() },
			{ "xemu", () => new XEmuGenerator() },
            { "scummvm", () => new ScummVmGenerator() },            
            { "arcadeflashweb", () => new ArcadeFlashWebGenerator() },			
            { "solarus", () => new SolarusGenerator() },
            { "eka2l1", () => new Eka2l1Generator() }, 
            { "n-gage", () => new Eka2l1Generator() },             
			{ "pinballfx3", () => new PinballFX3Generator() }			
        };

        public static ConfigFile AppConfig { get; private set; }
        public static string LocalPath { get; private set; }
        public static ConfigFile SystemConfig { get; private set; }
        public static List<Controller> Controllers { get; private set; }
        public static EsFeatures Features { get; private set; }
        public static Game CurrentGame { get; private set; }

        public static bool EnableHotKeyStart
        {
            get
            {
                return Process.GetProcessesByName("JoyToKey").Length == 0;
            }
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        public static string EscapeXml(this string s)
        {
            string toxml = s;
            if (!string.IsNullOrEmpty(toxml))
            {
                // replace literal values with entities
                toxml = toxml.Replace("&", "&amp;");
                toxml = toxml.Replace("'", "&apos;");
                toxml = toxml.Replace("\"", "&quot;");
                toxml = toxml.Replace(">", "&gt;");
                toxml = toxml.Replace("<", "&lt;");
            }
            return toxml;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            /*
            StringBuilder sb = new StringBuilder();
            var mi = IniFile.FromFile(@"H:\[Emulz]\system\emulators\mupen64\mupen64plus.ini");
            foreach (var ss in mi.EnumerateSections())
            {
                string goodName = mi.GetValue(ss, "GoodName");
                goodName = EscapeXml(goodName);
                sb.AppendLine("<rom hash=\""+ss+"\" name=\"" + goodName + "\"/>");
            }

            File.WriteAllText("c:\\temp\\mupen.xml", sb.ToString());

            */

            RegisterShellExtensions();

            if (args.Length == 0)
                return;

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

            if (!SystemConfig.isOptSet("use_guns") && args.Any(a => a == "-lightgun"))
                SystemConfig["use_guns"] = "true";

            LoadControllerConfiguration(args);
            ImportShaderOverrides();

            if (args.Any(a => "-extract".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                string source = SystemConfig["extract"];
                if (!Zip.IsCompressedFile(source))
                    return;

                using (var progress = new ProgressInformation("Extraction..."))
                {
                    string extractionPath = Path.ChangeExtension(source, ".game");

                    try { Directory.Delete(extractionPath, true); }
                    catch { }

                    Zip.Extract(source, extractionPath, null, (o, e) => progress.SetText("Extraction... " + e.ProgressPercentage + "%"));
                    Zip.CleanupUncompressedWSquashFS(source, extractionPath);
                    return;
                }
            }

            if (args.Any(a => "-makeiso".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                IsoFile.ConvertToIso(SystemConfig["makeiso"]);
                return;
            }

            if (args.Any(a => "-updatepo".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                EsFeaturesPoBuilder.Process();
                return;
            }

            if (args.Any(a => "-updateall".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                using (var frm = new InstallerFrm())
                    frm.UpdateAll();

                return;
            }

            if (args.Any(a => "-collectversions".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (args.Any(a => "-online".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
                    Installer.InstallAllAndCollect(Path.Combine(Path.GetTempPath(), "emulators"));
                else
                    Installer.CollectVersions();

                return;
            }

            if (!SystemConfig.isOptSet("rom"))
            {
                SimpleLogger.Instance.Error("rom not set");
                Environment.ExitCode = (int) ExitCodes.BadCommandLine;
                return;
            }

            if (!File.Exists(SystemConfig.GetFullPath("rom")) && !Directory.Exists(SystemConfig.GetFullPath("rom")))
            {
                SimpleLogger.Instance.Error("rom does not exist");
                Environment.ExitCode = (int)ExitCodes.BadCommandLine;
                return;
            }

            if (!SystemConfig.isOptSet("system"))
            {
                SimpleLogger.Instance.Error("system not set");
                Environment.ExitCode = (int)ExitCodes.BadCommandLine;
                return;
            }

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemDefaults.GetDefaultEmulator(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["core"]))
                SystemConfig["core"] = SystemDefaults.GetDefaultCore(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemConfig["system"];

            if (SystemConfig.isOptSet("gameinfo") && File.Exists(SystemConfig.GetFullPath("gameinfo")))
            {
                var gamelist = GameList.Load(SystemConfig.GetFullPath("gameinfo"));
                if (gamelist != null)
                {
                    CurrentGame = gamelist.Games.FirstOrDefault();
                    if (CurrentGame != null)                        
                        SimpleLogger.Instance.Info("Game : " + CurrentGame.Name);
                }
            }

            if (CurrentGame == null)
            {
                CurrentGame = new Game()
                {
                    path = SystemConfig.GetFullPath("rom"),
                    Name = Path.GetFileNameWithoutExtension(SystemConfig["rom"])
                };
            }

            // Check if installed. Download & Install it if necessary.
            Installer installer = Installer.GetInstaller();
            if (installer != null)
            {
                bool updatesEnabled = !SystemConfig.isOptSet("updates.enabled") || SystemConfig.getOptBoolean("updates.enabled");
                if ((!installer.IsInstalled() || (updatesEnabled && installer.HasUpdateAvailable())) && installer.CanInstall())
                {
                    using (InstallerFrm frm = new InstallerFrm(installer))
                        if (frm.ShowDialog() != DialogResult.OK)
                            return;
                }
            }
            
            Generator generator = generators.Where(g => g.Key == SystemConfig["emulator"]).Select(g => g.Value()).FirstOrDefault();
            if (generator == null && !string.IsNullOrEmpty(SystemConfig["emulator"]) && SystemConfig["emulator"].StartsWith("lr-"))
                generator = new LibRetroGenerator();
            if (generator == null)
                generator = generators.Where(g => g.Key == SystemConfig["system"]).Select(g => g.Value()).FirstOrDefault();

            if (generator != null)
            {
                SimpleLogger.Instance.Info("Generator : " + generator.GetType().Name);

                try
                {
                    Features = EsFeatures.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_features.cfg"));
                }
                catch (Exception ex)
                {                    
                    WriteCustomErrorFile("Error : es_features.cfg is invalid :\r\n" + ex.Message); // Delete custom err
                    Environment.ExitCode = (int)ExitCodes.CustomError;
                    return;
                }

                Features.SetFeaturesContext(SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"]);

                using (var screenResolution = ScreenResolution.Parse(SystemConfig["videomode"]))
                {
                    ProcessStartInfo path = null;

                    try
                    {
                        path = generator.Generate(SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"], SystemConfig["rom"], null, screenResolution);
                    }
                    catch (Exception ex)
                    {
                        generator.Cleanup();

                        SimpleLogger.Instance.Error(ex.Message);
                        Program.WriteCustomErrorFile(ex.Message);
                        Environment.ExitCode = (int) ExitCodes.CustomError;
                        SimpleLogger.Instance.Error("Generator exception : " + ex.Message);
                        return;
                    }

                    if (path != null)
                    {
                        path.UseShellExecute = true;

                        if (screenResolution != null && generator.DependsOnDesktopResolution)
                            screenResolution.Apply();

                        Cursor.Position = new System.Drawing.Point(Screen.PrimaryScreen.Bounds.Right, Screen.PrimaryScreen.Bounds.Bottom / 2);

                        PadToKey mapping = null;
                        if (generator.UseEsPadToKey)
                            mapping = PadToKey.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_padtokey.cfg"));

                        mapping = LoadGamePadToKeyMapping(path, mapping);
                        mapping = generator.SetupCustomPadToKeyMapping(mapping);

                        if (path.Arguments != null)
                            SimpleLogger.Instance.Info("Running : " + path.FileName + " " + path.Arguments);
                        else
                            SimpleLogger.Instance.Info("Running :  " + path.FileName);

                        using (new HighPerformancePowerScheme())
                        using (var joy = new JoystickListener(Controllers.Where(c => c.Config.DeviceName != "Keyboard").ToArray(), mapping))
                        {
                            int exitCode = generator.RunAndWait(path);
                            if (exitCode != 0 && !joy.ProcessKilled)
                                Environment.ExitCode = (int)ExitCodes.EmulatorExitedUnexpectedly;
                        }

                        generator.RestoreFiles();
                    }
                    else
                    {
                        SimpleLogger.Instance.Error("generator failed");
                        Environment.ExitCode = (int) generator.ExitCode;
                    }
                }

                generator.Cleanup();
            }
            else
            {
                SimpleLogger.Instance.Error("Can't find generator");
                Environment.ExitCode = (int)ExitCodes.UnknownEmulator;
            }

            if (Environment.ExitCode != 0)
                SimpleLogger.Instance.Error("Exit code : " + Environment.ExitCode);
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

            if (gameMapping == null || gameMapping.All(c => c == null))
                return mapping;

            PadToKeyApp app = new PadToKeyApp();
            app.Name = Path.GetFileNameWithoutExtension(path.FileName).ToLower();

            int controllerIndex = 0;

            foreach (var player in gameMapping)
            {
                if (player == null)
                {
                    controllerIndex++;
                    continue;
                }

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
                    input.ControllerIndex = controllerIndex;

                    bool custom = false;

                    foreach (var target in action.Targets)
                    {
                        if (target == "(%{KILL})" || target == "%{KILL}")
                        {
                            custom = true;
                            input.Key = "(%{KILL})";
                            continue;
                        }

                        if (target == "(%{CLOSE})" || target == "%{CLOSE}")
                        {
                            custom = true;
                            input.Key = "(%{CLOSE})";
                            continue;
                        }

                        if (target == "(%{F4})" || target == "%{F4}")
                        {
                            custom = true;
                            input.Key = "(%{F4})";
                            continue;
                        }

                        LinuxScanCode sc;
                        if (!Enum.TryParse<LinuxScanCode>(target.ToUpper(), out sc))
                            continue;

                        input.SetScanCode((uint)sc);
                    }

                    if (input.ScanCodes.Length > 0 || custom)
                        app.Input.Add(input);
                }

                controllerIndex++;
            }

            if (app.Input.Count > 0)
            {
                if (mapping == null)
                    mapping = new PadToKey();

                var existingApp = mapping.Applications.FirstOrDefault(a => a.Name.Equals(app.Name, StringComparison.InvariantCultureIgnoreCase));
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

                    Controller player = Controllers.FirstOrDefault(c => c.PlayerIndex == playerId);
                    if (player == null)
                    {
                        player = new Controller() { PlayerIndex = playerId };
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
                        case "index": player.DeviceIndex = val.ToInteger(); break;
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
                var inputConfig = EsInput.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_input.cfg"));
                if (inputConfig != null)
                {
                    foreach (var pi in Controllers)
                    {
                        pi.Config = inputConfig.FirstOrDefault(c => c.DeviceGUID.ToUpper() == pi.Guid && c.DeviceName == pi.Name);
                        if (pi.Config == null)
                            pi.Config = inputConfig.FirstOrDefault(c => c.DeviceGUID.ToUpper() == pi.Guid);
                        if (pi.Config == null)
                            pi.Config = inputConfig.FirstOrDefault(c => c.DeviceName == pi.Name);
                        if (pi.Config == null)
                            pi.Config = inputConfig.FirstOrDefault(c => c.DeviceName == "Keyboard");
                    }

                    Controllers.RemoveAll(c => c.Config == null);

                    if (!Controllers.Any() || SystemConfig.getOptBoolean("use_guns") || HasWiimoteGun())
                    {
                        var keyb = new Controller() { PlayerIndex = Controllers.Count + 1 };
                        keyb.Config = inputConfig.FirstOrDefault(c => c.DeviceName == "Keyboard");
                        if (keyb.Config != null)
                        {
                            keyb.Name = "Keyboard";
                            keyb.Guid = keyb.Config.ProductGuid.ToString();
                            Controllers.Add(keyb);
                        }
                    }
                }

                return inputConfig;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Detects if WiimoteGun is running in gamepad mode
        /// </summary>
        /// <returns></returns>
        public static bool HasWiimoteGun(WiiModeGunMode mode = WiiModeGunMode.Any)
        {
            IntPtr hWndWiimoteGun = User32.FindWindow("WiimoteGun", null);
            if (hWndWiimoteGun != IntPtr.Zero)
            {
                if (mode == WiiModeGunMode.Any)
                    return true;

                int wndMode = (int)User32.GetProp(hWndWiimoteGun, "mode");
                return wndMode == (int)mode;
            }

            return false;
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


        /// <summary>
        /// To use with Environment.ExitCode = (int)ExitCodes.CustomError;
        /// Deletes the file if message == null
        /// </summary>
        /// <param name="message"></param>
        public static void WriteCustomErrorFile(string message)
        {
            SimpleLogger.Instance.Error(message);

            string fn = Path.Combine(Path.GetTempPath(), "emulationstation.tmp", "launch_error.log");

            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    if (File.Exists(fn))
                        File.Delete(fn);
                }
                else
                    File.WriteAllText(fn, message);
            }
            catch { }
        }


        public static void RegisterShellExtensions()
        {
            try
            {
                if (!File.Exists(Zip.GetRdSquashFSPath()))
                    return;

                RegisterConvertToIso(".squashfs");
                RegisterConvertToIso(".wsquashfs");
                RegisterExtractAsFolder(".squashfs");
                RegisterExtractAsFolder(".wsquashfs");
            }
            catch { }
        }

        private static void RegisterConvertToIso(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return;

            RegistryKey key = Registry.ClassesRoot.CreateSubKey(extension);
            if (key == null)
                return;

            RegistryKey shellKey = key.CreateSubKey("Shell");
            if (shellKey == null)
                return;

            var openWith = typeof(Program).Assembly.Location;
            shellKey.CreateSubKey("Convert to ISO").CreateSubKey("command").SetValue("", "\"" + openWith + "\"" + " -makeiso \"%1\"");
            shellKey.Close();

            key.Close();
        }


        private static void RegisterExtractAsFolder(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return;

            RegistryKey key = Registry.ClassesRoot.CreateSubKey(extension);
            if (key == null)
                return;

            RegistryKey shellKey = key.CreateSubKey("Shell");
            if (shellKey == null)
                return;

            var openWith = typeof(Program).Assembly.Location;
            shellKey.CreateSubKey("Extract as folder").CreateSubKey("command").SetValue("", "\"" + openWith + "\"" + " -extract \"%1\"");
            shellKey.Close();

            key.Close();
        }
    }

    class Controller
    {
        public Controller()
        {
            DeviceIndex = -1;
        }

        public int PlayerIndex { get; set; }
        public int DeviceIndex { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public int NbButtons { get; set; }
        public int NbHats { get; set; }
        public int NbAxes { get; set; }

        public InputConfig Config { get; set; }

        public override string ToString() { return Name + " (" + PlayerIndex.ToString()+")"; }
    }

    enum WiiModeGunMode : int
    {
        Any = 0,
        Mouse = 1,
        Gamepad = 2
    }

}
