using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Security.Principal;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.PadToKeyboard;
using EmulatorLauncher.Libretro;
using EmulatorLauncher.Common.Compression.Wrappers;
using EmulatorLauncher.Common.Launchers;

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
namespace EmulatorLauncher
{
    static class Program
    {
        /// <summary>
        /// Link between emulator declared in es_systems.cfg and generator to use to launch emulator
        /// </summary>
        static Dictionary<string, Func<Generator>> generators = new Dictionary<string, Func<Generator>>
        {
            { "3dsen", () => new Nes3dGenerator() },
            { "altirra", () => new AltirraGenerator() },
            { "amigaforever", () => new AmigaForeverGenerator() },
            { "angle", () => new LibRetroGenerator() },
            { "apple2", () => new AppleWinGenerator() },
            { "apple2gs", () => new GsPlusGenerator() },
            { "applewin", () => new AppleWinGenerator() },
            { "arcadeflashweb", () => new ArcadeFlashWebGenerator() },
            { "ares", () => new AresGenerator() },
            { "azahar", () => new AzaharGenerator() },
            { "bam", () => new FpinballGenerator() },
            { "bigpemu", () => new BigPEmuGenerator() },
            { "bizhawk", () => new BizhawkGenerator() },
            { "capriceforever", () => new CapriceForeverGenerator() },
            { "cdogs", () => new CDogsGenerator() },
            { "cemu", () => new CemuGenerator() },
            { "cgenius", () => new CGeniusGenerator() },
            { "chihiro", () => new CxbxGenerator() },
            { "chihiro-gun", () => new CxbxGenerator() },
            { "citra", () => new CitraGenerator() },
            { "citra-canary", () => new CitraGenerator() },
            { "citron", () => new CitronGenerator() },
            { "corsixth", () => new CorsixTHGenerator() },
            { "cxbx", () => new CxbxGenerator() },
            { "daphne", () => new DaphneGenerator() },
            { "demul", () => new DemulGenerator() },
            { "demul-old", () => new DemulGenerator() },
            { "desmume", () => new DesmumeGenerator() },
            { "devilutionx", () => new DevilutionXGenerator() },
            { "dhewm3", () => new Dhewm3Generator() },
            { "dolphin", () => new DolphinGenerator() },
            { "dosbox", () => new DosBoxGenerator() },
            { "duckstation", () => new DuckstationGenerator() },
            { "easyrpg", () => new EasyRpgGenerator() },
            { "eden", () => new EdenGenerator() },
            { "eduke32", () => new EDukeGenerator() },
            { "eka2l1", () => new Eka2l1Generator() },
            { "fbneo", () => new FbneoGenerator() },
            { "flycast", () => new FlycastGenerator() },
            { "fpinball", () => new FpinballGenerator() },
            { "gemrb", () => new GemRBGenerator() },
            { "gopher64", () => new Gopher64Generator() },
            { "gsplus", () => new GsPlusGenerator() },
            { "gzdoom", () => new GZDoomGenerator() },
            { "hatari", () => new HatariGenerator() },
            { "hbmame", () => new Mame64Generator() },
            { "hypseus", () => new HypseusGenerator() },
            { "ikemen", () => new ExeLauncherGenerator() },
            { "jgenesis", () => new JgenesisGenerator() },
            { "jynx", () => new JynxGenerator() },
            { "kega-fusion", () => new KegaFusionGenerator() },
            { "kronos", () => new KronosGenerator() },
            { "libretro", () => new LibRetroGenerator() },
            { "lime3ds", () => new Lime3dsGenerator() },
            { "love", () => new LoveGenerator() },
            { "m2emulator", () => new Model2Generator() },
            { "magicengine", () => new MagicEngineGenerator() },
            { "mame64", () => new Mame64Generator() },
            { "mandarine", () => new MandarineGenerator() },
            { "mednafen", () => new MednafenGenerator() },
            { "melonds", () => new MelonDSGenerator() },
            { "mesen", () => new MesenGenerator() },
            { "mgba", () => new MGBAGenerator() },
            { "model2", () => new Model2Generator() },
            { "model3", () => new Model3Generator() },
            { "mugen", () => new ExeLauncherGenerator() },
            { "mupen64", () => new Mupen64Generator() },
            { "nosgba", () => new NosGbaGenerator() },
            { "no$gba", () => new NosGbaGenerator() },
            { "n-gage", () => new Eka2l1Generator() },
            { "openbor", () => new OpenBorGenerator() },
            { "opengoal", () => new OpenGoalGenerator() },
            { "openjazz", () => new OpenJazzGenerator() },
            { "openmsx", () => new OpenMSXGenerator() },
            { "oricutron", () => new OricutronGenerator() },
            { "pcsx2", () => new Pcsx2Generator() },
            { "pcsx2qt", () => new Pcsx2Generator() },
            { "pcsx2-16", () => new Pcsx2Generator() },
            { "pdark", () => new PDarkGenerator() },
            { "phoenix", () => new PhoenixGenerator() },
            { "pico8", () => new Pico8Generator() },
            { "pinballfx", () => new PinballFXGenerator() },
            { "play", () => new PlayGenerator() },
            { "ppsspp", () => new PpssppGenerator() },
            { "project64", () => new Project64Generator() },
            { "ps2", () => new Pcsx2Generator() },
            { "ps3", () => new Rpcs3Generator() },
            { "psvita", () => new Vita3kGenerator() },
            { "psxmame", () => new PSXMameGenerator() },
            { "raine", () => new RaineGenerator() },
            { "raze", () => new RazeGenerator() },
            { "redream", () => new RedreamGenerator() },
            { "retrobat", () => new RetrobatLauncherGenerator() },
            { "rpcs3", () => new Rpcs3Generator() },
            { "ruffle", () => new RuffleGenerator() },
            { "ryujinx", () => new RyujinxGenerator() },
            { "scummvm", () => new ScummVmGenerator() },
            { "shadps4", () => new ShadPS4Generator() },
            { "simcoupe", () => new SimCoupeGenerator() },
            { "simple64", () => new Simple64Generator() },
            { "singe2", () => new Singe2Generator() },
            { "snes9x", () => new Snes9xGenerator() },
            { "soh", () => new SohGenerator() },
            { "solarus", () => new SolarusGenerator() },
            { "solarus2", () => new SolarusGenerator() },
            { "sonic3air", () => new PortsLauncherGenerator() },
            { "sonicmania", () => new PortsLauncherGenerator() },
            { "sonicretro", () => new PortsLauncherGenerator() },
            { "sonicretrocd", () => new PortsLauncherGenerator() },
            { "ssf", () => new SSFGenerator() },
            { "starship", () => new StarshipGenerator() },
            { "stella", () => new StellaGenerator() },
            { "sudachi", () => new SudachiGenerator() },
            { "supermodel", () => new Model3Generator() },
            { "suyu", () => new SuyuGenerator() },
            { "teknoparrot", () => new TeknoParrotGenerator() },
            { "theforceengine", () => new ForceEngineGenerator() },
            { "triforce", () => new DolphinGenerator() },
            { "tsugaru", () => new TsugaruGenerator() },
            { "vita3k", () => new Vita3kGenerator() },
            { "vpinball", () => new VPinballGenerator() },
            { "wiiu", () => new CemuGenerator() },
            { "winarcadia", () => new WinArcadiaGenerator() },
            { "windows", () => new ExeLauncherGenerator() },
            { "winuae", () => new UaeGenerator() },
            { "xbox", () => new CxbxGenerator() },
            { "xemu", () => new XEmuGenerator() },
            { "xenia", () => new XeniaGenerator() },
            { "xenia-canary", () => new XeniaGenerator() },
            { "xenia-manager", () => new XeniaGenerator() },
            { "xm6pro", () => new Xm6proGenerator() },
            { "xroar", () => new XroarGenerator() },
            { "yabasanshiro", () => new YabasanshiroGenerator() },
            { "ymir", () => new YmirGenerator() },
            { "yuzu", () => new YuzuGenerator() },
            { "yuzu-early-access", () => new YuzuGenerator() },
            { "zaccariapinball", () => new ZaccariaPinballGenerator() },
            { "zesarux", () => new ZEsarUXGenerator() },
            { "zinc", () => new ZincGenerator() }
        };

        public static ConfigFile AppConfig { get; private set; }
        public static string LocalPath { get; private set; }
        public static ConfigFile SystemConfig { get; private set; }
        public static List<Controller> Controllers { get; private set; }
        public static EsFeatures Features { get; private set; }
        public static Game CurrentGame { get; private set; }

        /// <summary>
        /// Import es_systems.cfg (and overrides) to retrieve emulators
        /// </summary>
        private static EsSystems _esSystems;

        public static EsSystems EsSystems
        {
            get
            {
                if (_esSystems == null)
                {
                    _esSystems = EsSystems.Load(Path.Combine(Program.LocalPath, ".emulationstation", "es_systems.cfg"));

                    if (_esSystems != null)
                    {
                        // Import emulator overrides from additional es_systems_*.cfg files
                        foreach (var file in Directory.GetFiles(Path.Combine(Program.LocalPath, ".emulationstation"), "es_systems_*.cfg"))
                        {
                            try
                            {
                                var esSystemsOverride = EsSystems.Load(file);
                                if (esSystemsOverride != null && esSystemsOverride.Systems != null)
                                {
                                    foreach (var ss in esSystemsOverride.Systems)
                                    {
                                        if (ss.Emulators == null || !ss.Emulators.Any())
                                            continue;

                                        var orgSys = _esSystems.Systems.FirstOrDefault(e => e.Name == ss.Name);
                                        if (orgSys != null)
                                            orgSys.Emulators = ss.Emulators;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                return _esSystems;
            }
        }

        /// <summary>
        /// Import es_savestates.cfg
        /// Used to monitor savestates
        /// </summary>
        private static EsSaveStates _esSaveStates;

        public static EsSaveStates EsSaveStates
        {
            get
            {
                if (_esSaveStates == null)
                    _esSaveStates = EsSaveStates.Load(Path.Combine(Program.LocalPath, ".emulationstation", "es_savestates.cfg"));

                return _esSaveStates;
            }
        }

        public static bool HasEsSaveStates
        {
            get
            {
                return File.Exists(Path.Combine(Program.LocalPath, ".emulationstation", "es_savestates.cfg"));
            }
        }

        /// <summary>
        /// Import gamesdb.xml
        /// Used to get information on game (gun, wheel, ...)
        /// </summary>
        private static GamesDB _gunGames;

        public static GamesDB GunGames
        {
            get
            {
                if (_gunGames == null)
                {
                    string gamesDb = Path.Combine(Program.AppConfig.GetFullPath("resources"), "gamesdb.xml");
                    if (File.Exists(gamesDb))
                        _gunGames = GamesDB.Load(gamesDb);
                    else
                    {
                        string gungamesDb = Path.Combine(Program.AppConfig.GetFullPath("resources"), "gungames.xml");
                        _gunGames = GamesDB.Load(gungamesDb);
                    }
                }

                return _gunGames;
            }
        }

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
        /// Method to show a splash video before a game starts
        /// </summary>
        static void ShowSplashVideo()
        {
            var loadingScreens = AppConfig.GetFullPath("loadingscreens");
            if (string.IsNullOrEmpty(loadingScreens))
                return;

            var system = SystemConfig["system"];
            if (string.IsNullOrEmpty(system))
                return;

            var rom = Path.GetFileNameWithoutExtension(SystemConfig["rom"]??"");

            var paths = new string[] 
            {
                "!screens!\\!system!\\!romname!.mp4",
                "!screens!\\!system!\\!system!.mp4",
                "!screens!\\!system!.mp4",
                "!screens!\\default.mp4"
            };

            var videoPath = paths
                .Select(path => path.Replace("!screens!", loadingScreens).Replace("!system!", system).Replace("!romname!", rom))
                .FirstOrDefault(path => File.Exists(path));

            if (string.IsNullOrEmpty(videoPath))
                return;

           SplashVideo.Start(videoPath, 5000);           
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            RegisterShellExtensions();

            if (args.Length == 0)
                return;


            // Used by XInputDevice.GetDevices
            if (args.Length == 2 && args[0] == "-queryxinputinfo")
            {
                var all = XInputDevice.GetDevices(true);
                File.WriteAllText(args[1], string.Join("\r\n", all.Where(d => d.Connected).Select(d => "<xinput index=\"" + d.DeviceIndex + "\" path=\"" + d.Path + "\"/>").ToArray()));
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info("[Startup] " + Environment.CommandLine);

            try { SetProcessDPIAware(); }
            catch { }

            SimpleLogger.Instance.Info("[Startup] Loading configuration.");
            LocalPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            AppConfig = ConfigFile.FromFile(Path.Combine(LocalPath, "emulatorLauncher.cfg"));
            AppConfig.ImportOverrides(ConfigFile.FromArguments(args));

            SimpleLogger.Instance.Info("[Startup] Loading ES settings.");
            SystemConfig = ConfigFile.LoadEmulationStationSettings(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_settings.cfg"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll("global"));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"]));
            SystemConfig.ImportOverrides(SystemConfig.LoadAll(SystemConfig["system"] + "[\"" + Path.GetFileName(SystemConfig["rom"]) + "\"]"));
            SystemConfig.ImportOverrides(ConfigFile.FromArguments(args));

            // Log Retrobat version && emulatorlauncher version
            string rbVersionPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "version.info");
            string emulatorlauncherExePath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "emulationstation", "emulatorlauncher.exe");

            if (File.Exists(rbVersionPath))
            {
                string rbVersion = File.ReadAllText(rbVersionPath).Trim();
                SimpleLogger.Instance.Info("[Startup] Retrobat version : " + rbVersion);
            }
            else
                SimpleLogger.Instance.Info("[Startup] Retrobat version : not found");

            if (File.Exists(emulatorlauncherExePath))
            {
                DateTime lastModifiedDate = File.GetLastWriteTime(emulatorlauncherExePath);
                if (lastModifiedDate != null)
                    SimpleLogger.Instance.Info("[Startup] EmulatorLauncher.exe version : " + lastModifiedDate.ToString());
            }

            // Automatically switch on lightgun if -lightgun is passed and not disabled in the config (except for wii where we do not want to switch on with real wiimote)
            if (!SystemConfig.isOptSet("use_guns") && args.Any(a => a == "-lightgun") && SystemConfig["system"] != "wii")
            {
                SystemConfig["use_guns"] = "true";
                SimpleLogger.Instance.Info("[GUNS] Lightgun game : setting default lightun value to true.");
            }

            /* for later wheels
            if (!SystemConfig.isOptSet("use_wheel") && args.Any(a => a == "-wheel"))
                SystemConfig["use_wheel"] = "true";*/

            // Get shaders to apply to the game
            ImportShaderOverrides();

            // Check consistance of path
            string rbPath = AppConfig.GetFullPath("retrobat");

            #region arguments
            if (args.Any(a => "-updatestores".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                GameStoresManager.UpdateGames();
                return;
            }

            if (args.Any(a => "-resetusbcontrollers".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                bool elevated = WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
                if (!elevated)
                {
                    MessageBox.Show("Process is not elevated");
                }
                else
                {
                    var dd = HidGameDevice.GetUsbGameDevices();
                    if (dd.Length > 1)
                    {
                        try
                        {
                            foreach (var dev in dd)
                                dev.Enable(false);

                            System.Threading.Thread.Sleep(200);

                            foreach (var dev in dd.OrderBy(dev => dev.PNPDeviceID))
                                dev.Enable(true);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }

                return;
            }

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

            if (args.Any(a => "-listmame".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                string mamePath = Path.Combine(AppConfig.GetFullPath("roms"), "mame");
                if (Directory.Exists(mamePath))
                {
                    string fn = Path.Combine(Path.GetTempPath(), "mameroms.txt");
                    FileTools.TryDeleteFile(fn);
                    File.WriteAllText(fn, MameVersionDetector.ListAllGames(mamePath, false));
                    Process.Start(fn);
                }
             
                return;
            }

            if (args.Any(a => "-checkmame".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                string mamePath = Path.Combine(AppConfig.GetFullPath("roms"), "mame");
                if (Directory.Exists(mamePath))
                {
                    string fn = Path.Combine(Path.GetTempPath(), "mame.txt");
                    FileTools.TryDeleteFile(fn);
                    File.WriteAllText(fn, MameVersionDetector.CheckMame(mamePath));
                    Process.Start(fn);
                }

                return;
            }

            if (args.Any(a => "-listfbneoinmame".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                string mamePath = Path.Combine(AppConfig.GetFullPath("roms"), "mame");
                if (Directory.Exists(mamePath))
                {
                    string fn = Path.Combine(Path.GetTempPath(), "fbneo.txt");
                    FileTools.TryDeleteFile(fn);
                    File.WriteAllText(fn, MameVersionDetector.ListAllGames(mamePath, true));
                    Process.Start(fn);
                }

                return;
            }

            if (args.Any(a => "-checkfbneo".Equals(a, StringComparison.InvariantCultureIgnoreCase)))
            {
                string mamePath = Path.Combine(AppConfig.GetFullPath("roms"), "fbneo");
                if (Directory.Exists(mamePath))
                {
                    string fn = Path.Combine(Path.GetTempPath(), "fbneo.txt");
                    FileTools.TryDeleteFile(fn);
                    File.WriteAllText(fn, MameVersionDetector.CheckFbNeo(mamePath));
                    Process.Start(fn);
                }

                return;
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
            #endregion

            // Check rom is defined and exists
            if (!SystemConfig.isOptSet("rom"))
            {
                SimpleLogger.Instance.Error("[Error] rom not set");
                Environment.ExitCode = (int) ExitCodes.BadCommandLine;
                return;
            }

            if (!File.Exists(SystemConfig.GetFullPath("rom")) && !Directory.Exists(SystemConfig.GetFullPath("rom")))
            {
                SimpleLogger.Instance.Error("[Error] rom does not exist");
                Environment.ExitCode = (int)ExitCodes.BadCommandLine;
                return;
            }

            // System, emulator and core
            if (!SystemConfig.isOptSet("system"))
            {
                SimpleLogger.Instance.Error("[Error] system not set");
                Environment.ExitCode = (int)ExitCodes.BadCommandLine;
                return;
            }

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemDefaults.GetDefaultEmulator(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["core"]))
                SystemConfig["core"] = SystemDefaults.GetDefaultCore(SystemConfig["system"]);

            if (string.IsNullOrEmpty(SystemConfig["emulator"]))
                SystemConfig["emulator"] = SystemConfig["system"];

            // Log local version
            try
            {
                Installer localInstaller = Installer.GetInstaller(null, true);
                if (localInstaller != null)
                {
                    string localVersion = localInstaller.GetInstalledVersion(true);

                    if (localVersion != null)
                        SimpleLogger.Instance.Info("[Startup] Emulator version: " + localVersion);
                }
            }
            catch { SimpleLogger.Instance.Error("[Startup] Error while getting local version"); }

            // Game info
            if (SystemConfig.isOptSet("gameinfo") && File.Exists(SystemConfig.GetFullPath("gameinfo")))
            {
                var gamelist = GameList.Load(SystemConfig.GetFullPath("gameinfo"));
                if (gamelist != null)
                {
                    CurrentGame = gamelist.Games.FirstOrDefault();
                    if (CurrentGame != null)                        
                        SimpleLogger.Instance.Info("[Game] " + CurrentGame.Name);
                }
            }

            if (CurrentGame == null)
            {
                var romPath = SystemConfig.GetFullPath("rom");
                var gamelistPath = Path.Combine(Path.GetDirectoryName(romPath), "gamelist.xml");
                if (File.Exists(gamelistPath))
                {
                    var gamelist = GameList.Load(gamelistPath);
                    if (gamelist != null && gamelist.Games != null)
                        CurrentGame = gamelist.Games.FirstOrDefault(g => g.GetRomFile() == romPath);
                }

                if (CurrentGame == null)
                {
                    CurrentGame = new Game()
                    {
                        Path = romPath,
                        Name = Path.GetFileNameWithoutExtension(romPath),
                        Tag = "missing"
                    };
                }
            }

            // Check and delete es-update.cmd and es-checkversion.cmd
            string esUpdateCmd = Path.Combine(Program.LocalPath, "es-update.cmd");
            string esCheckVersionCmd = Path.Combine(Program.LocalPath, "es-checkversion.cmd");

            if (File.Exists(esUpdateCmd))
            {
                SimpleLogger.Instance.Info("[Startup] Deleting " + esUpdateCmd);
                FileTools.TryDeleteFile(esUpdateCmd);
            }
            if (File.Exists(esCheckVersionCmd))
            {
                SimpleLogger.Instance.Info("[Startup] Deleting " + esCheckVersionCmd);
                FileTools.TryDeleteFile(esCheckVersionCmd);
            }

            // Get Generator to use based on emulator or system if none found
            Generator generator = generators.Where(g => g.Key == SystemConfig["emulator"]).Select(g => g.Value()).FirstOrDefault();
            if (generator == null && !string.IsNullOrEmpty(SystemConfig["emulator"]) && SystemConfig["emulator"].StartsWith("lr-"))
                generator = new LibRetroGenerator();
            if (generator == null)
                generator = generators.Where(g => g.Key == SystemConfig["system"]).Select(g => g.Value()).FirstOrDefault();

            // Load controller configuration from arguments passed by emulationstation
            LoadControllerConfiguration(args);

            // Splash video
            if (generator != null)
            {
                ShowSplashVideo();
                //System.Threading.Thread.Sleep(5000);
                //return;
            }

            // Check if emulator is installed. Download & Install it if necessary. Propose update if available.
            Installer installer = Installer.GetInstaller();
            if (installer != null)
            {
                bool updatesEnabled = !SystemConfig.isOptSet("updates.enabled") || SystemConfig.getOptBoolean("updates.enabled");

                if (!updatesEnabled)
                    SimpleLogger.Instance.Info("[Startup] Updates not enabled, not looking for updates.");

                if ((!installer.IsInstalled() || (updatesEnabled && installer.HasUpdateAvailable())) && installer.CanInstall())
                {
                    SimpleLogger.Instance.Info("[Startup] Emulator update found : proposing to update.");
                    using (InstallerFrm frm = new InstallerFrm(installer))
                        if (frm.ShowDialog() != DialogResult.OK)
                            return;
                }
            }

            // Load features, run the generator to configure and set up command lines, start emulator process and cleanup after emulator process ends
            if (generator != null)
            {
                SimpleLogger.Instance.Info("[Generator] Using " + generator.GetType().Name);

                try
                {
                    Features = EsFeatures.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_features.cfg"));
                }
                catch (Exception ex)
                {                    
                    WriteCustomErrorFile("[Error] es_features.cfg is invalid :\r\n" + ex.Message); // Delete custom err
                    Environment.ExitCode = (int)ExitCodes.CustomError;
                    return;
                }

                SimpleLogger.Instance.Info("[Generator] Loading features.");
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
                        
                        Program.WriteCustomErrorFile(ex.Message);
                        Environment.ExitCode = (int) ExitCodes.CustomError;
                        SimpleLogger.Instance.Error("[Generator] Exception : " + ex.Message, ex);
                        return;
                    }

                    if (path != null)
                    {
                        path.UseShellExecute = true;

                        if (screenResolution != null && generator.DependsOnDesktopResolution)
                            screenResolution.Apply();

                        if (!Program.SystemConfig.isOptSet("use_guns") || !Program.SystemConfig.getOptBoolean("use_guns"))
                            Cursor.Position = new System.Drawing.Point(Screen.PrimaryScreen.Bounds.Right, Screen.PrimaryScreen.Bounds.Bottom / 2);

                        PadToKey mapping = null;
                        if (generator.UseEsPadToKey)
                            mapping = PadToKey.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_padtokey.cfg"));

                        mapping = LoadGamePadToKeyMapping(path, mapping);
                        mapping = generator.SetupCustomPadToKeyMapping(mapping);

                        if (path.Arguments != null)
                            SimpleLogger.Instance.Info("[Running] " + path.FileName + " " + path.Arguments);
                        else
                            SimpleLogger.Instance.Info("[Running]  " + path.FileName);

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
                        SimpleLogger.Instance.Error("[Generator] Failed. path is null");
                        Environment.ExitCode = (int) generator.ExitCode;
                    }
                }

                generator.Cleanup();
            }
            else
            {
                SimpleLogger.Instance.Error("[Generator] Can't find generator");
                Environment.ExitCode = (int)ExitCodes.UnknownEmulator;
            }

            if (Environment.ExitCode != 0)
                SimpleLogger.Instance.Error("[Generator] Exit code " + Environment.ExitCode);
        }

        private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as System.Exception;
            if (ex == null)
                SimpleLogger.Instance.Error("[CurrentDomain] Unhandled exception");
            else
            {
                SimpleLogger.Instance.Error("[CurrentDomain] Unhandled exception : " + ex.Message, ex);

                if (e.IsTerminating)
                {
                    Program.WriteCustomErrorFile(ex.Message);
                    Environment.Exit((int)ExitCodes.CustomError);
                }
            }
        }

        private static PadToKey LoadGamePadToKeyMapping(ProcessStartInfo path, PadToKey mapping)
        {
            string filePath = SystemConfig["rom"] + (Directory.Exists(SystemConfig["rom"]) ? "\\padto.keys" : ".keys");

            EvMapyKeysFile gameMapping = EvMapyKeysFile.TryLoad(filePath);
            if (gameMapping == null && SystemConfig["system"] != null)
            {
                var core = SystemConfig["core"];
                var system = SystemConfig["system"];

                string systemMapping = "";

                if (!string.IsNullOrEmpty(core))
                {
                    systemMapping = Path.Combine(Program.LocalPath, ".emulationstation", "padtokey", system + "." + core + ".keys");

                    if (!File.Exists(systemMapping))
                        systemMapping = Path.Combine(Program.AppConfig.GetFullPath("padtokey"), system + "." + core + ".keys");
                }

                if (!File.Exists(systemMapping))
                    systemMapping = Path.Combine(Program.LocalPath, ".emulationstation", "padtokey", system + ".keys");

                if (!File.Exists(systemMapping))
                    systemMapping = Path.Combine(Program.AppConfig.GetFullPath("padtokey"), system + ".keys");

                if (File.Exists(systemMapping))
                    gameMapping = EvMapyKeysFile.TryLoad(systemMapping);
            }

            if (gameMapping == null || gameMapping.All(c => c == null))
                return mapping;

            PadToKeyApp app = new PadToKeyApp();
            app.Name = Path.GetFileNameWithoutExtension(path.FileName).ToLower();

            int playerIndex = 0;

            foreach (var player in gameMapping)
            {
                if (player == null)
                {
                    playerIndex++;
                    continue;
                }

                var controller = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == playerIndex + 1);

                foreach (var action in player)
                {
                    if (action.type == "mouse")
                    {
                        if (action.Triggers == null || action.Triggers.Length == 0)
                            continue;

                        if (action.Triggers.FirstOrDefault() == "joystick1")
                        {
                            PadToKeyInput mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.joystick1left;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "X";
                            app.Input.Add(mouseInput);

                            mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.joystick1up;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "Y";
                            app.Input.Add(mouseInput);
                        }
                        else if (action.Triggers.FirstOrDefault() == "joystick2")
                        {
                            PadToKeyInput mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.joystick2left;
                            mouseInput.Type = PadToKeyType.Mouse;
                            mouseInput.Code = "X";
                            app.Input.Add(mouseInput);

                            mouseInput = new PadToKeyInput();
                            mouseInput.Name = InputKey.joystick2up;
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

                    PadToKeyInput input = new PadToKeyInput
                    {
                        Name = k,
                        ControllerIndex = controller == null ? playerIndex : controller.DeviceIndex
                    };

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

                playerIndex++;
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
            SimpleLogger.Instance.Info("[Startup] Loading Controller configuration.");
            var controllers = new Dictionary<int, Controller>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-p") && args[i].Length > 3)
                {
                    int playerId = new string(args[i].Substring(2).TakeWhile(c => char.IsNumber(c)).ToArray()).ToInteger();

                    Controller player;
                    if (!controllers.TryGetValue(playerId, out player))
                    {
                        player = new Controller() { PlayerIndex = playerId };
                        controllers[playerId] = player;
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
                        case "guid": player.Guid = new SdlJoystickGuid(val); break;
                        case "path": player.DevicePath = val; break;
                        case "name": player.Name = val; break;
                        case "nbbuttons": player.NbButtons = val.ToInteger(); break;
                        case "nbhats": player.NbHats = val.ToInteger(); break;
                        case "nbaxes": player.NbAxes = val.ToInteger(); break;
                    }
                }
            }

            Controllers = controllers.Select(c => c.Value).OrderBy(c => c.PlayerIndex).ToList();

            try
            {
                var inputConfig = EsInput.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_input.cfg"));
                if (inputConfig != null)
                {
                    foreach (var pi in Controllers)
                    {
                        if (pi.IsKeyboard)
                        {
                            pi.Config = inputConfig.FirstOrDefault(c => "Keyboard".Equals(c.DeviceName, StringComparison.InvariantCultureIgnoreCase));
                            if (pi.Config != null)
                                continue;
                        }

                        pi.Config = inputConfig.FirstOrDefault(c => pi.CompatibleSdlGuids.Contains(c.DeviceGUID.ToLowerInvariant()) && c.DeviceName == pi.Name);
                        if (pi.Config == null)
                            pi.Config = inputConfig.FirstOrDefault(c => pi.CompatibleSdlGuids.Contains(c.DeviceGUID.ToLowerInvariant()));
                        if (pi.Config == null)
                            pi.Config = inputConfig.FirstOrDefault(c => c.DeviceName == pi.Name);
                    }

                    Controllers.RemoveAll(c => c.Config == null);

                    if (!Controllers.Any() || SystemConfig.getOptBoolean("use_guns") || Misc.HasWiimoteGun())
                    {
                        var keyb = new Controller() { PlayerIndex = Controllers.Count + 1 };
                        keyb.Config = inputConfig.FirstOrDefault(c => c.DeviceName == "Keyboard");
                        if (keyb.Config != null)
                        {
                            keyb.Name = "Keyboard";
                            keyb.Guid = new SdlJoystickGuid("00000000000000000000000000000000");
                            Controllers.Add(keyb);
                        }
                    }
                }

                return inputConfig;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[LoadControllerConfiguration] Failed " + ex.Message, ex);
            }

            return null;
        }
        
        private static void ImportShaderOverrides()
        {
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shaderset") && SystemConfig["shaderset"] != "none")
            {
                string path = Path.Combine(AppConfig.GetFullPath("shaders"), "configs", SystemConfig["shaderset"], "rendering-defaults.yml");
                if (File.Exists(path))
                {
                    string renderconfig = SystemShaders.GetShader(File.ReadAllText(path), SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"]);
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
            SimpleLogger.Instance.Error("[Error] " + message);

            string fn = Path.Combine(Installer.GetTempPath(), "launch_error.log");

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
                if (!SquashFsArchive.IsSquashFsAvailable)
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

        private static int ObscureCode(byte x, byte y)
        {
            return (x ^ y) + 0x80;
        }
    }
}
