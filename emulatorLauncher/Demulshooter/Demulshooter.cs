using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    class Demulshooter
    {
        private static Version _dsVersion;
        private static bool GetDemulshooterExecutable(string emulator, string rom, out string executable, out string path)
        {
            executable = "DemulShooter.exe";
            path = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "demulshooter");

            if (!Directory.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] Demulshooter not available.");
                return false;
            }

            if (emulator == "teknoparrot")
            {
                string romName = Path.GetFileNameWithoutExtension(rom);
                if (teknoParrotGames.TryGetValue(romName, out var game))
                    executable = game.Architecture == "x64" ? "DemulShooterX64.exe" : "DemulShooter.exe";
            }
            else if (emulator == "exelauncher" || emulator == "windows")
            {
                string romName = Path.GetFileNameWithoutExtension(rom).Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (exeLauncherGames.TryGetValue(romName, out var game))
                    executable = game.Architecture == "x64" ? "DemulShooterX64.exe" : "DemulShooter.exe";
            }
            else if (emulator == "flycast" || emulator == "rpcs3")
                executable = "DemulShooterX64.exe";

            return true;
        }

        private static readonly List<string> chihiroRoms = new List<string>
        { "vcop3", "virtuacop", "cop", "virtualcop", "virtua cop", "virtual cop", "vc3" };

        private static readonly List<string> chihiroDSRoms = new List<string>
        { "vcop3", "virtuacop", "cop", "virtualcop", "virtua cop", "virtual cop", "vc3", "vsg", "gs", "hod3xb", "hotd3" };

        private static readonly List<string> demulRoms = new List<string>
        {
            "braveff", "claychal", "confmiss", "deathcox", "deathcoxo", "hotd2", "hotd2o", "hotd2p", "lupinsho", "manicpnc", "mok",
            "ninjaslt", "ninjaslta", "ninjasltj", "ninjasltu", "pokasuka", "rangrmsn", "sprtshot", "xtrmhunt", "xtrmhnt2"
        };

        private readonly List<string> model2Roms = new List<string>
        { "bel", "gunblade", "hotd", "rchase2", "vcop", "vcop2" };

        private static readonly List<string> flycastRoms = new List<string>
        { "confmiss", "deathcox", "hotd2", "hotd2o", "hotd2p", "lupinsho", "mok", "ninjaslt", "ninjaslta", "ninjasltj", "ninjasltu" };

        private readonly List<string> rpcs3Roms = new List<string>
        { "de4d", "deadstorm", "sailorz" };

        public static void StartDemulshooter(string emulator, string system, string rom, RawLightgun gun1, RawLightgun gun2 = null, RawLightgun gun3 = null, RawLightgun gun4 = null)
        {
            // Kill MameHook if option is not enabled
            if (!Program.SystemConfig.isOptSet("use_mamehooker") || !Program.SystemConfig.getOptBoolean("use_mamehooker"))
            {
                SimpleLogger.Instance.Info("[GUNS] MameHook option not enabled, killing any running instance");
                MameHooker.KillMameHooker();
            }
            // Start MameHooker if enabled
            else if (Program.SystemConfig.getOptBoolean("use_mamehooker"))
            {
                SimpleLogger.Instance.Info("[GUNS] Starting MameHook before DemulShooter");
                
                // Configure specific settings if needed
                if (emulator == "m2emulator")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom).ToLower();
                    MameHooker.Model2.ConfigureModel2(romName);
                }
                else if (emulator == "teknoparrot")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom);
                    MameHooker.Teknoparrot.ConfigureTeknoparrot(romName);
                }
                else if (emulator == "exelauncher" || emulator == "windows")
                {
                    MameHooker.ExeLauncher.ConfigureExeLauncher(rom);
                }
                else if (emulator == "demul")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom);
                    MameHooker.Demul.ConfigureDemul(romName);
                }
                else if (emulator == "flycast")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom);
                    MameHooker.Flycast.ConfigureFlycast(romName);
                }

                Process mameHookProcess = MameHooker.StartMameHooker();
                
                if (mameHookProcess != null)
                {
                    // Wait for MameHook to start
                    SimpleLogger.Instance.Info("[GUNS] Waiting for MameHook to initialize");
                    mameHookProcess.WaitForInputIdle(2000); // Wait up to 2 seconds for the process to be ready
                    System.Threading.Thread.Sleep(2000); // Additional wait to ensure full initialization
                }
            }

            string iniFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "demulshooter", "config.ini");

            if (iniFile != null)
                SimpleLogger.Instance.Info("[GUNS] Writing in DemulShooter ini file: " + iniFile);
            
            using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                for (int i = 1; i <= 5; i++)
                {
                    string mode = "P" + i + "Mode";
                    ini.WriteValue("", mode, "RAWINPUT");
                    string devName = "P" + i + "DeviceName";
                    ini.AppendValue("", devName, string.Empty);
                }
                
                if (gun1 != null)
                    ini.WriteValue("", "P1DeviceName", gun1.DevicePath);
                if (gun2 != null)
                    ini.WriteValue("", "P2DeviceName", gun2.DevicePath);
                if (gun3 != null)
                    ini.WriteValue("", "P3DeviceName", gun3.DevicePath);
                if (gun4 != null)
                    ini.WriteValue("", "P4DeviceName", gun4.DevicePath);

                if (Program.SystemConfig.isOptSet("ds_output") && !string.IsNullOrEmpty(Program.SystemConfig["ds_output"]))
                {
                    string outputType = Program.SystemConfig["ds_output"];
                    ini.WriteValue("", "OutputEnabled", "True");

                    SimpleLogger.Instance.Info("[GUNS] Writing DemulShooter outputs.");

                    switch (outputType)
                    {
                        case "win":
                            ini.WriteValue("", "WM_OutputsEnabled", "True");
                            ini.WriteValue("", "Net_OutputsEnabled", "False");
                            break;
                        case "net":
                            ini.WriteValue("", "Net_OutputsEnabled", "True");
                            ini.WriteValue("", "WM_OutputsEnabled", "False");
                            break;
                        case "both":
                            ini.WriteValue("", "Net_OutputsEnabled", "True");
                            ini.WriteValue("", "WM_OutputsEnabled", "True");
                            break;
                    }
                }
                else
                    ini.WriteValue("", "OutputEnabled", "False");

                ini.Save();
            }
            // Write code to start demulshooter
            // DemulShooter.exe -target=demul07a -rom=confmiss -noresize -widescreen
            // DemulShooterX64.exe -target=seganu -rom=lma

            if (GetDemulshooterExecutable(emulator, rom, out string executable, out string dsPath))
            {
                string exe = Path.Combine(dsPath, executable);

                if (!File.Exists(exe))
                {
                    SimpleLogger.Instance.Warning("[WARNING] Demulshooter exe does not exist.");
                    return;
                }

                // Get DSVersion
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exe);
                    string version = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
                    SimpleLogger.Instance.Info("[INFO] DemulShooter version: " + version);
                    Version ver = new Version();
                    if (Version.TryParse(version, out ver))
                        _dsVersion = ver;
                }
                catch { SimpleLogger.Instance.Warning("[WARNING] Failed to get DemulShooter version."); }
                    
                var commandArray = new List<string>();

                if (GetDemulshooterTarget(emulator, rom, out string target))
                    commandArray.Add("-target=" + target);
                else
                {
                    SimpleLogger.Instance.Warning("[WARNING] Failed to launch DemulShooter, no target.");
                    return;
                }

                if (emulator != "dolphin")
                {
                    if (GetDemulshooterRom(rom, emulator, _dsVersion, out string dsRom))
                        commandArray.Add("-rom=" + dsRom);
                    else
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Failed to launch DemulShooter, no rom.");
                        return;
                    }
                }

                if (emulator == "teknoparrot")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom);
                    if (teknoParrotGames.TryGetValue(romName, out var game))
                    {
                        commandArray.Add("-noinput");

                        if (!string.IsNullOrEmpty(game.ExtraArgs))
                            commandArray.Add(game.ExtraArgs);

                        // Add optional arguments based on configuration
                        if (Program.SystemConfig.getOptBoolean("tp_nocrosshair"))
                            commandArray.Add("-nocrosshair");
                    }
                }
                else if (emulator == "demul")
                {
                    if (Program.SystemConfig.getOptBoolean("demul_noresize"))
                        commandArray.Add("-noresize");
                }
                else if (emulator == "windows" || emulator == "exelauncher")
                {
                    if (Program.SystemConfig.getOptBoolean("ds_nocrosshair"))
                        commandArray.Add("-nocrosshair");
                }

                // Global verbose mode for all emulators
                if (Program.SystemConfig.getOptBoolean("ds_verbose"))
                    commandArray.Add("-v");

                string args = string.Join(" ", commandArray);

                var p = new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = dsPath,
                    Arguments = args,
                };
                
                using (Process process = new Process())
                {
                    SimpleLogger.Instance.Info("[INFO] Running Demulshooter: " + exe + " " + args);
                    process.StartInfo = p;
                    process.Start();
                }
            }
            else
            {
                SimpleLogger.Instance.Warning("[WARNING] Failed to launch DemulShooter.");
                return;
            }
        }

        public static void KillDemulShooter()
        {
            SimpleLogger.Instance.Info("[CLEANUP] Check if demulshooter needs closing.");

            string[] processNames = { "DemulShooter", "DemulShooterX64" };

            int i = 0;
            for (i = 0; i < 11; i++)
            {
                foreach (string processName in processNames)
                {
                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0 && i != 10)
                    {
                        System.Threading.Thread.Sleep(500);
                        i++;
                    }
                    else if (processes.Length > 0 && i == 10)
                    {
                        foreach (Process process in processes)
                        {
                            SimpleLogger.Instance.Info("[INFO] Force-killing DemulShooter.");
                            try { process.Kill(); }
                            catch { SimpleLogger.Instance.Info("[WARNING] Failed to terminate DemulShooter."); }
                        }
                    }
                }
            }
        }

        private static bool GetDemulshooterTarget(string emulator, string rom, out string target)
        {
            target = null;
            bool ret = false;

            SimpleLogger.Instance.Info("[GUNS] Fetching Demulshooter -target argument.");

            if (emulator == "m2emulator")
            {
                target = "model2";
                ret = true;
            }
            else if (emulator == "demul")
            {
                target = "demul07a";
                ret = true;
            }
            else if (emulator == "flycast")
            {
                target = "flycast";
                ret = true;
            }
            else if (emulator == "chihiro" || emulator == "chihiro-ds")
            {
                target = "chihiro";
                ret = true;
            }
            else if (emulator == "teknoparrot")
            {
                string romName = Path.GetFileNameWithoutExtension(rom);
                if (teknoParrotGames.TryGetValue(romName, out var game))
                {
                    target = game.Target;
                    ret = true;
                }
            }
            else if (emulator == "exelauncher")
            {
                string romName = Path.GetFileNameWithoutExtension(rom);
                if (exeLauncherGames.TryGetValue(romName, out var game))
                {
                    target = game.Target;
                    ret = true;
                }
            }

            return ret;
        }

        private static bool GetDemulshooterRom(string rom, string emulator, Version dsVersion, out string dsRom)
        {
            dsRom = null;
            bool ret = false;

            SimpleLogger.Instance.Info("[GUNS] Fetching Demulshooter -rom argument.");

            if (emulator == "m2emulator")
            {
                dsRom = Path.GetFileNameWithoutExtension(rom);
                ret = true;
            }
            else if (emulator == "demul")
            {
                string romName = Path.GetFileNameWithoutExtension(rom).ToLower();
                if (demulRoms.Contains(romName) || chihiroRoms.Contains(romName))
                {
                    dsRom = romName;
                    ret = true;
                }
            }
            else if (emulator == "flycast")
            {
                string romName = Path.GetFileNameWithoutExtension(rom).ToLower();
                if (flycastRoms.Contains(romName))
                {
                    dsRom = romName;
                    ret = true;
                }
            }
            else if (emulator == "teknoparrot")
            {
                string romName = Path.GetFileNameWithoutExtension(rom);
                if (teknoParrotGames.TryGetValue(romName, out var game))
                {
                    dsRom = game.RomName;
                    ret = true;
                }
            }
            else if (emulator == "exelauncher")
            {
                string romName = Path.GetFileNameWithoutExtension(rom);
                if (exeLauncherGames.TryGetValue(romName, out var game))
                {
                    dsRom = game.RomName;
                    ret = true;
                }
            }
            else if (emulator == "chihiro")
            {
                if (chihiroRoms.Any(r => rom.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try
                    {
                        string dsVersionNew = "15.5.0.0";
                        Version dsNewVersion = new Version();
                        if (Version.TryParse(dsVersionNew, out dsNewVersion))
                        {
                            if (dsVersion >= dsNewVersion)
                                dsRom = "vcop3_old";
                            else
                                dsRom = "vcop3";

                            ret = true;
                        }
                        else
                        {
                            dsRom = "vcop3_old";
                            ret = true;
                        }
                    }
                    catch 
                    {
                        dsRom = "vcop3_old";
                        ret = true;
                    }
                }
            }

            else if (emulator == "chihiro-ds")
            {
                if (chihiroDSRoms.Any(r => rom.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    // Chihiro-ds version - specific ROM mappings for gun games
                    string romLower = rom.ToLowerInvariant();
                    if (romLower.Contains("vc3") || romLower.Contains("cop"))
                    {
                        dsRom = "vcop3";
                        ret = true;
                    }
                    else if (romLower.Contains("vsg") || romLower == "gs")
                    {
                        dsRom = "gsquad";
                        ret = true;
                    }
                    else if (romLower.Contains("hod3xb") || romLower.Contains("hotd3"))
                    {
                        dsRom = "hod3";
                        ret = true;
                    }
                    else
                        ret = false;
                }
            }
            return ret;
        }

        internal class TeknoParrotGame
        {
            public string XmlName { get; set; }
            public string RomName { get; set; }
            public string Architecture { get; set; }
            public string Target { get; set; }
            public string ExtraArgs { get; set; }
        }

        internal static readonly Dictionary<string, TeknoParrotGame> teknoParrotGames = new Dictionary<string, TeknoParrotGame>
        {
            { "2Spicy", new TeknoParrotGame { XmlName = "2Spicy", RomName = "2spicy", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "AfterDark2", new TeknoParrotGame { XmlName = "AfterDark2", RomName = "nha", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "Akuma", new TeknoParrotGame { XmlName = "Akuma", RomName = "akuma", Architecture = "x64", Target = "gamewax", ExtraArgs = "" } },
            { "AKB48", new TeknoParrotGame { XmlName = "AKB48", RomName = "sailorz", Architecture = "x64", Target = "rpcs3", ExtraArgs = "" } },
            { "AliensArmageddon", new TeknoParrotGame { XmlName = "AliensArmageddon", RomName = "aa", Architecture = "x32", Target = "rawthrill", ExtraArgs = "" } },
            { "AliensExtermination", new TeknoParrotGame { XmlName = "AliensExtermination", RomName = "aliens", Architecture = "x32", Target = "globalvr", ExtraArgs = "" } },
            { "BlockKing", new TeknoParrotGame { XmlName = "BlockKing", RomName = "bkbs", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "BugBusters", new TeknoParrotGame { XmlName = "BugBusters", RomName = "bugbust", Architecture = "x32", Target = "windows", ExtraArgs = "" } },
            { "ByonByon", new TeknoParrotGame { XmlName = "ByonByon", RomName = "mgungun2", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "Castlevania", new TeknoParrotGame { XmlName = "Castlevania", RomName = "hcv", Architecture = "x32", Target = "konami", ExtraArgs = "" } },         
            { "DarkEscape4D", new TeknoParrotGame { XmlName = "DarkEscape4D", RomName = "de4d", Architecture = "x64", Target = "rpcs3", ExtraArgs = "" } },
            { "Drakons", new TeknoParrotGame { XmlName = "Drakons", RomName = "drk", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "DSPS", new TeknoParrotGame { XmlName = "DSPS", RomName = "deadstorm", Architecture = "x64", Target = "rpcs3", ExtraArgs = "" } },
            { "EADP", new TeknoParrotGame { XmlName = "EADP", RomName = "eapd", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "ElevatorActionInvasion", new TeknoParrotGame { XmlName = "ElevatorActionInvasion", RomName = "eai", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "FarCryParadiseLost", new TeknoParrotGame { XmlName = "FarCryParadiseLost", RomName = "farcry", Architecture = "x32", Target = "globalvr", ExtraArgs = "" } },
            { "FearLand", new TeknoParrotGame { XmlName = "FearLand", RomName = "fearland", Architecture = "x32", Target = "globalvr", ExtraArgs = "" } },
            { "Friction", new TeknoParrotGame { XmlName = "Friction", RomName = "friction", Architecture = "x32", Target = "windows", ExtraArgs = "" } },
            { "GG", new TeknoParrotGame { XmlName = "GG", RomName = "sgg", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "GaiaAttack4", new TeknoParrotGame { XmlName = "GaiaAttack4", RomName = "gattack4", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "GhostBusters", new TeknoParrotGame { XmlName = "GhostBusters", RomName = "gbusters", Architecture = "x32", Target = "arcadepc", ExtraArgs = "" } },
            { "GSEVO", new TeknoParrotGame { XmlName = "GSEVO", RomName = "gsquad", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "GSEVOELF2", new TeknoParrotGame { XmlName = "GSEVOELF2", RomName = "gsquad", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "GundamSpiritsOfZeon", new TeknoParrotGame { XmlName = "GundamSpiritsOfZeon", RomName = "gsoz", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "GundamSpiritsOfZeon2p", new TeknoParrotGame { XmlName = "GundamSpiritsOfZeon2p", RomName = "gsoz2p", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "HauntedMuseum", new TeknoParrotGame { XmlName = "HauntedMuseum", RomName = "hmuseum", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "HauntedMuseumII", new TeknoParrotGame { XmlName = "HauntedMuseumII", RomName = "hmuseum2", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "HOTD4", new TeknoParrotGame { XmlName = "HOTD4", RomName = "hotd4", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "HOTD4SP", new TeknoParrotGame { XmlName = "HOTD4SP", RomName = "hotd4sp", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "HOTD4ELF2", new TeknoParrotGame { XmlName = "HOTD4ELF2", RomName = "hotd4", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "HOTD4SPELF2", new TeknoParrotGame { XmlName = "HOTD4SPELF2", RomName = "hotd4sp", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "HOTDEX", new TeknoParrotGame { XmlName = "HOTDEX", RomName = "hotdex", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "HOTDSD", new TeknoParrotGame { XmlName = "HOTDSD", RomName = "hodsd", Architecture = "x64", Target = "alls", ExtraArgs = "-noinput" } },
            { "JurassicPark", new TeknoParrotGame { XmlName = "JurassicPark", RomName = "jp", Architecture = "x32", Target = "rawthrill", ExtraArgs = "" } },
            { "LethalEnforcers3", new TeknoParrotGame { XmlName = "LethalEnforcers3", RomName = "le3", Architecture = "x32", Target = "konami", ExtraArgs = "" } },
            { "LGI", new TeknoParrotGame { XmlName = "LGI", RomName = "lgi", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "LGI3D", new TeknoParrotGame { XmlName = "LGI3D", RomName = "lgi3D", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "LGJ", new TeknoParrotGame { XmlName = "LGJ", RomName = "lgj", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "LGJS", new TeknoParrotGame { XmlName = "LGJS", RomName = "lgjsp", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },
            { "LuigisMansion", new TeknoParrotGame { XmlName = "LuigisMansion", RomName = "lma", Architecture = "x64", Target = "seganu", ExtraArgs = "" } },
            { "MedaruNoGunman", new TeknoParrotGame { XmlName = "MedaruNoGunman", RomName = "mng", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "MissionImpossible", new TeknoParrotGame { XmlName = "MissionImpossible", RomName = "misimp", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "MusicGunGun2", new TeknoParrotGame { XmlName = "MusicGunGun2", RomName = "mgungun2", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "OG", new TeknoParrotGame { XmlName = "OG", RomName = "og", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "PointBlankX", new TeknoParrotGame { XmlName = "PointBlankX", RomName = "pblankx", Architecture = "x32", Target = "es4", ExtraArgs = "" } },
            { "PoliceTrainer2", new TeknoParrotGame { XmlName = "PoliceTrainer2", RomName = "policetr2", Architecture = "x32", Target = "ppmarket", ExtraArgs = "" } },
            { "RabbidsHollywood", new TeknoParrotGame { XmlName = "RabbidsHollywood", RomName = "rha", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "RaccoonRampage", new TeknoParrotGame { XmlName = "raccoonrampage", RomName = "racramp", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "Rambo", new TeknoParrotGame { XmlName = "Rambo", RomName = "rambo", Architecture = "x32", Target = "lindbergh", ExtraArgs = "" } },            
            { "RazingStorm", new TeknoParrotGame { XmlName = "RazingStorm", RomName = "razstorm", Architecture = "x64", Target = "rpcs3", ExtraArgs = "" } },
            { "SDR", new TeknoParrotGame { XmlName = "SDR", RomName = "sdr", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "SilentHill", new TeknoParrotGame { XmlName = "SilentHill", RomName = "sha", Architecture = "x32", Target = "ttx", ExtraArgs = "" } },
            { "TargetTerrorGold", new TeknoParrotGame { XmlName = "TargetTerrorGold", RomName = "ttg", Architecture = "x32", Target = "rawthrill", ExtraArgs = "" } },
            { "TC5", new TeknoParrotGame { XmlName = "TC5", RomName = "tc5", Architecture = "x64", Target = "es3", ExtraArgs = "" } },
            { "Terminator", new TeknoParrotGame { XmlName = "Terminator", RomName = "ts", Architecture = "x32", Target = "rawthrill", ExtraArgs = "" } },
            { "TombRaider", new TeknoParrotGame { XmlName = "TombRaider", RomName = "tra", Architecture = "x64", Target = "arcadepc", ExtraArgs = "" } },
            { "Transformers", new TeknoParrotGame { XmlName = "Transformers", RomName = "tha", Architecture = "x32", Target = "ringwide", ExtraArgs = "" } },
            { "TransformersShadowsRising", new TeknoParrotGame { XmlName = "TransformersShadowsRising", RomName = "tsr", Architecture = "x32", Target = "ringedge2", ExtraArgs = "" } },
            { "WalkingDead", new TeknoParrotGame { XmlName = "WalkingDead", RomName = "wd", Architecture = "x32", Target = "rawthrill", ExtraArgs = "" } },
            { "WartranTroopers", new TeknoParrotGame { XmlName = "WartranTroopers", RomName = "wartran", Architecture = "x32", Target = "konami", ExtraArgs = "" } },
            { "WildWestShootout", new TeknoParrotGame { XmlName = "WildWestShootout", RomName = "wws", Architecture = "x32", Target = "arcadepc", ExtraArgs = "" } }
        };

        internal class ExeLauncherGame
        {
            public string RomName { get; set; }
            public string Architecture { get; set; }
            public string Target { get; set; }
        }

        internal static readonly Dictionary<string, ExeLauncherGame> exeLauncherGames = new Dictionary<string, ExeLauncherGame>
        {
           	{ "akuma", new ExeLauncherGame { RomName = "akuma", Architecture = "x64", Target = "gamewax" } },
            { "aliensextermination", new ExeLauncherGame { RomName = "aliens", Architecture = "x32", Target = "globalvr" } },
            { "aliendiscosafari", new ExeLauncherGame { RomName = "ads", Architecture = "x32", Target = "windows" } },
            { "artisdead", new ExeLauncherGame { RomName = "artdead", Architecture = "x32", Target = "windows" } },
            { "bigbuckhunterultimatetrophy", new ExeLauncherGame { RomName = "bbhut", Architecture = "x64", Target = "windows" } },
            { "blockking", new ExeLauncherGame { RomName = "bkbs", Architecture = "x32", Target = "ttx" } },
            { "castlevaniaarcade", new ExeLauncherGame { RomName = "hcv", Architecture = "x32", Target = "konami" } },
            { "dcop", new ExeLauncherGame { RomName = "dcop", Architecture = "x64", Target = "windows" } },
            { "eadp", new ExeLauncherGame { RomName = "eadp", Architecture = "x32", Target = "ttx" } },
            { "elevatoractioninvasion", new ExeLauncherGame { RomName = "eai", Architecture = "x64", Target = "arcadepc" } },
            { "fearland", new ExeLauncherGame { RomName = "fearland", Architecture = "x32", Target = "globalvr" } },
            { "friction", new ExeLauncherGame { RomName = "friction", Architecture = "x32", Target = "windows" } },
            { "gaiaattack4", new ExeLauncherGame { RomName = "gattack4", Architecture = "x32", Target = "ttx" } },
            { "gundamspiritsofzeon", new ExeLauncherGame { RomName = "gsoz", Architecture = "x32", Target = "ttx" } },
            { "gundamspiritsofzeon2p", new ExeLauncherGame { RomName = "gsoz2p", Architecture = "x32", Target = "ttx" } },
            { "hauntedmuseum", new ExeLauncherGame { RomName = "hmuseum", Architecture = "x32", Target = "ttx" } },
            { "hauntedmuseum2", new ExeLauncherGame { RomName = "hmuseum2", Architecture = "x32", Target = "ttx" } },
            { "heavyfireafghanistan", new ExeLauncherGame { RomName = "hfa", Architecture = "x32", Target = "windows" } },
            { "heavyfireafghanistan2p", new ExeLauncherGame { RomName = "hfa2p", Architecture = "x32", Target = "windows" } },
            { "heavyfireshaterredspear", new ExeLauncherGame { RomName = "hfss", Architecture = "x32", Target = "windows" } },
            { "heavyfireshaterredspear2p", new ExeLauncherGame { RomName = "hfss2p", Architecture = "x32", Target = "windows" } },
            { "laststand", new ExeLauncherGame { RomName = "pvz", Architecture = "x32", Target = "arcadepc" } },
            { "lethalenforcers3", new ExeLauncherGame { RomName = "le3", Architecture = "x32", Target = "konami" } },
            { "letsgoisland", new ExeLauncherGame { RomName = "lgi", Architecture = "x32", Target = "ringwide" } },
            { "letsgoisland3d", new ExeLauncherGame { RomName = "lgi3D", Architecture = "x32", Target = "ringwide" } },
            { "luigismansionarcade", new ExeLauncherGame { RomName = "lma", Architecture = "x64", Target = "seganu" } },
            { "madbullets", new ExeLauncherGame { RomName = "madbul", Architecture = "x32", Target = "windows" } },
            { "marssortie", new ExeLauncherGame { RomName = "marss", Architecture = "x32", Target = "arcadepc" } },
            { "medalofvalormedalofhonorarcade", new ExeLauncherGame { RomName = "mng", Architecture = "x32", Target = "ringwide" } },
            { "missionimpossiblearcade", new ExeLauncherGame { RomName = "misimp", Architecture = "x64", Target = "arcadepc" } },
            { "musicgungun2", new ExeLauncherGame { RomName = "mgungun2", Architecture = "x32", Target = "ttx" } },
            { "nerfarcade", new ExeLauncherGame { RomName = "nerfa", Architecture = "x64", Target = "rawthrill" } },
            { "nighthunterafterdarkchapterii", new ExeLauncherGame { RomName = "nha", Architecture = "x64", Target = "arcadepc" } },
            { "operationghost", new ExeLauncherGame { RomName = "og", Architecture = "x32", Target = "ringwide" } },
            { "operationwolfreturnsfirstmission", new ExeLauncherGame { RomName = "opwolfr", Architecture = "x64", Target = "windows" } },
            { "pointblankx", new ExeLauncherGame { RomName = "pblankx", Architecture = "x32", Target = "es4" } },
            { "rabbidshollywood", new ExeLauncherGame { RomName = "rha", Architecture = "x64", Target = "arcadepc" } },
            { "raccoonrampage", new ExeLauncherGame { RomName = "racramp", Architecture = "x64", Target = "arcadepc" } },
            { "reload", new ExeLauncherGame { RomName = "reload", Architecture = "x32", Target = "windows" } },
            { "segadreamraiders", new ExeLauncherGame { RomName = "sdr", Architecture = "x32", Target = "ringwide" } },
            { "silenthillthearcade", new ExeLauncherGame { RomName = "sha", Architecture = "x32", Target = "ttx" } },
            { "thehouseofthedead2", new ExeLauncherGame { RomName = "hod2pc", Architecture = "x32", Target = "windows" } },
            { "thehouseofthedead3", new ExeLauncherGame { RomName = "hod3pc", Architecture = "x32", Target = "windows" } },
            { "thehouseofthedeadoverkill", new ExeLauncherGame { RomName = "hodo", Architecture = "x32", Target = "windows" } },
            { "thehouseofthedeadremake", new ExeLauncherGame { RomName = "hotdra", Architecture = "x64", Target = "windows" } },
            { "thehouseofthedeadscarletdawn", new ExeLauncherGame { RomName = "hodsd", Architecture = "x64", Target = "alls" } },
            { "tombraiderarcade", new ExeLauncherGame { RomName = "tra", Architecture = "x64", Target = "arcadepc" } },
            { "transformershumanalliance", new ExeLauncherGame { RomName = "tha", Architecture = "x32", Target = "ringwide" } },
            { "transformershumanalliancetc5", new ExeLauncherGame { RomName = "tc5", Architecture = "x64", Target = "es3" } },
            { "transformersshadowsrising", new ExeLauncherGame { RomName = "tsr", Architecture = "x32", Target = "ringedge2" } },
            { "wildwestshootout", new ExeLauncherGame { RomName = "wws", Architecture = "x32", Target = "arcadepc" } } 
        };
    }
}
