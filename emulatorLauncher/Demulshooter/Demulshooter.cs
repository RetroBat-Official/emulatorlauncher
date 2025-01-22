using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class Demulshooter
    {
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

            else if (emulator == "flycast" || emulator == "rpcs3")
                executable = "DemulShooterX64.exe";

            return true;
        }

        private static readonly List<string> chihiroRoms = new List<string>
        { "vcop3" };

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
            // Start MameHooker first if enabled
            if (Program.SystemConfig.getOptBoolean("use_mamehooker"))
            {
                SimpleLogger.Instance.Info("[INFO] Starting MameHook before DemulShooter");
                Process mameHookProcess = MameHooker.StartMameHooker();
                
                if (mameHookProcess != null)
                {
                    // Wait for MameHook to start
                    SimpleLogger.Instance.Info("[INFO] Waiting for MameHook to initialize");
                    mameHookProcess.WaitForInputIdle(2000); // Wait up to 2 seconds for the process to be ready
                    System.Threading.Thread.Sleep(2000); // Additional wait to ensure full initialization
                }
            }

            string iniFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "demulshooter", "config.ini");

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
                    if (GetDemulshooterRom(rom, emulator, out string dsRom))
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
            else if (emulator == "teknoparrot")
            {
                string romName = Path.GetFileNameWithoutExtension(rom);
                if (teknoParrotGames.TryGetValue(romName, out var game))
                {
                    target = game.Target;
                    ret = true;
                }
            }

            return ret;
        }

        private static bool GetDemulshooterRom(string rom, string emulator, out string dsRom)
        {
            dsRom = null;
            bool ret = false;

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
            // aagames
            { "RabbidsHollywood", new TeknoParrotGame { XmlName = "RabbidsHollywood", RomName = "rha", Architecture = "x64", Target = "aagames", ExtraArgs = "-noinput" } },
            { "TombRaider", new TeknoParrotGame { XmlName = "TombRaider", RomName = "tra", Architecture = "x64", Target = "aagames", ExtraArgs = "-noinput" } },
            { "Drakons", new TeknoParrotGame { XmlName = "Drakons", RomName = "drk", Architecture = "x64", Target = "aagames", ExtraArgs = "-noinput" } },

            // alls, es3, es4
            { "HOTDSD", new TeknoParrotGame { XmlName = "HOTDSD", RomName = "hodsd", Architecture = "x64", Target = "alls", ExtraArgs = "-noinput" } },
            { "TC5", new TeknoParrotGame { XmlName = "TC5", RomName = "tc5", Architecture = "x64", Target = "es3", ExtraArgs = "-noinput" } },
            { "PointBlankX", new TeknoParrotGame { XmlName = "PointBlankX", RomName = "pblankx", Architecture = "x32", Target = "es4", ExtraArgs = "-noinput" } },

            // coastal
            { "WildWestShootout", new TeknoParrotGame { XmlName = "WildWestShootout", RomName = "wws", Architecture = "x32", Target = "coastal", ExtraArgs = "-noinput" } },

            // gamewax
            { "Akuma", new TeknoParrotGame { XmlName = "Akuma", RomName = "akuma", Architecture = "x64", Target = "gamewax", ExtraArgs = "-noinput" } },

            // globalvr
            { "AliensExtermination", new TeknoParrotGame { XmlName = "AliensExtermination", RomName = "aliens", Architecture = "x32", Target = "globalvr", ExtraArgs = "-noinput" } },
            { "FarCryParadiseLost", new TeknoParrotGame { XmlName = "FarCryParadiseLost", RomName = "farcry", Architecture = "x32", Target = "globalvr", ExtraArgs = "" } },
            { "FearLand", new TeknoParrotGame { XmlName = "FearLand", RomName = "fearland", Architecture = "x32", Target = "globalvr", ExtraArgs = "-noinput" } },

            // ice, konami
            { "GhostBusters", new TeknoParrotGame { XmlName = "GhostBusters", RomName = "gbusters", Architecture = "x32", Target = "ice", ExtraArgs = "-noinput" } },
            { "WartranTroopers", new TeknoParrotGame { XmlName = "WartranTroopers", RomName = "wartran", Architecture = "x32", Target = "konami", ExtraArgs = "-noinput" } },
            { "LethalEnforcers3", new TeknoParrotGame { XmlName = "LethalEnforcers3", RomName = "le3", Architecture = "x32", Target = "konami", ExtraArgs = "-noinput" } },
            { "Castlevania", new TeknoParrotGame { XmlName = "Castlevania", RomName = "hcv", Architecture = "x32", Target = "konami", ExtraArgs = "-noinput" } },

            // lindbergh
            { "2Spicy", new TeknoParrotGame { XmlName = "2Spicy", RomName = "2spicy", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "HOTD4", new TeknoParrotGame { XmlName = "HOTD4", RomName = "hotd4", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "HOTD4SP", new TeknoParrotGame { XmlName = "HOTD4SP", RomName = "hotd4sp", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "LGJ", new TeknoParrotGame { XmlName = "LGJ", RomName = "lgj", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "LGJS", new TeknoParrotGame { XmlName = "LGJS", RomName = "lgjsp", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "Rambo", new TeknoParrotGame { XmlName = "Rambo", RomName = "rambo", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "GSEVO", new TeknoParrotGame { XmlName = "GSEVO", RomName = "gsquad", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "GSEVOELF2", new TeknoParrotGame { XmlName = "GSEVOELF2", RomName = "gsquad", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "HOTD4ELF2", new TeknoParrotGame { XmlName = "HOTD4ELF2", RomName = "hotd4", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "HOTD4SPELF2", new TeknoParrotGame { XmlName = "HOTD4SPELF2", RomName = "hotd4sp", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },
            { "HOTDEX", new TeknoParrotGame { XmlName = "HOTDEX", RomName = "hotdex", Architecture = "x32", Target = "lindbergh", ExtraArgs = "-noinput" } },

            // ppmarket
            { "PoliceTrainer2", new TeknoParrotGame { XmlName = "PoliceTrainer2", RomName = "policetr2", Architecture = "x32", Target = "ppmarket", ExtraArgs = "-noinput" } },

            // rawthrill
            { "AliensArmageddon", new TeknoParrotGame { XmlName = "AliensArmageddon", RomName = "aa", Architecture = "x32", Target = "rawthrill", ExtraArgs = "-noinput" } },
            { "JurassicPark", new TeknoParrotGame { XmlName = "JurassicPark", RomName = "jp", Architecture = "x32", Target = "rawthrill", ExtraArgs = "-noinput" } },
            { "Terminator", new TeknoParrotGame { XmlName = "Terminator", RomName = "ts", Architecture = "x32", Target = "rawthrill", ExtraArgs = "-noinput" } },
            { "WalkingDead", new TeknoParrotGame { XmlName = "WalkingDead", RomName = "wd", Architecture = "x32", Target = "rawthrill", ExtraArgs = "-noinput" } },
            { "TargetTerrorGold", new TeknoParrotGame { XmlName = "TargetTerrorGold", RomName = "ttg", Architecture = "x32", Target = "rawthrill", ExtraArgs = "-noinput" } },

            // ringwide, ringedge2
            { "LGI", new TeknoParrotGame { XmlName = "LGI", RomName = "lgi", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "LGI3D", new TeknoParrotGame { XmlName = "LGI3D", RomName = "lgi3D", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "OG", new TeknoParrotGame { XmlName = "OG", RomName = "og", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "SDR", new TeknoParrotGame { XmlName = "SDR", RomName = "sdr", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "GG", new TeknoParrotGame { XmlName = "GG", RomName = "sgg", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "Transformers", new TeknoParrotGame { XmlName = "Transformers", RomName = "tha", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "MedaruNoGunman", new TeknoParrotGame { XmlName = "MedaruNoGunman", RomName = "mng", Architecture = "x32", Target = "ringwide", ExtraArgs = "-noinput" } },
            { "TransformersShadowsRising", new TeknoParrotGame { XmlName = "TransformersShadowsRising", RomName = "tsr", Architecture = "x32", Target = "ringedge2", ExtraArgs = "-noinput" } },

            // seganu
            { "LuigisMansion", new TeknoParrotGame { XmlName = "LuigisMansion", RomName = "lma", Architecture = "x64", Target = "seganu", ExtraArgs = "-noinput" } },

            // ttx
            { "BlockKing", new TeknoParrotGame { XmlName = "BlockKing", RomName = "bkbs", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "ByonByon", new TeknoParrotGame { XmlName = "ByonByon", RomName = "mgungun2", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "EADP", new TeknoParrotGame { XmlName = "EADP", RomName = "eapd", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "GaiaAttack4", new TeknoParrotGame { XmlName = "GaiaAttack4", RomName = "gattack4", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "GundamSpiritsOfZeon", new TeknoParrotGame { XmlName = "GundamSpiritsOfZeon", RomName = "gsoz", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "GundamSpiritsOfZeon2p", new TeknoParrotGame { XmlName = "GundamSpiritsOfZeon2p", RomName = "gsoz2p", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "HauntedMuseum", new TeknoParrotGame { XmlName = "HauntedMuseum", RomName = "hmuseum", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "HauntedMuseumII", new TeknoParrotGame { XmlName = "HauntedMuseumII", RomName = "hmuseum2", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "MusicGunGun2", new TeknoParrotGame { XmlName = "MusicGunGun2", RomName = "mgungun2", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },
            { "SilentHill", new TeknoParrotGame { XmlName = "SilentHill", RomName = "sha", Architecture = "x32", Target = "ttx", ExtraArgs = "-noinput" } },

            // unis
            { "ElevatorActionInvasion", new TeknoParrotGame { XmlName = "ElevatorActionInvasion", RomName = "eai", Architecture = "x64", Target = "unis", ExtraArgs = "-noinput" } },
            { "AfterDark2", new TeknoParrotGame { XmlName = "AfterDark2", RomName = "nha", Architecture = "x64", Target = "unis", ExtraArgs = "-noinput" } },
            { "RaccoonRampage", new TeknoParrotGame { XmlName = "raccoonrampage", RomName = "racramp", Architecture = "x64", Target = "unis", ExtraArgs = "-noinput" } },

            // windows (for try compatibility)
            { "BugBusters", new TeknoParrotGame { XmlName = "BugBusters", RomName = "bugbust", Architecture = "x32", Target = "windows", ExtraArgs = "-noinput" } },
            { "Friction", new TeknoParrotGame { XmlName = "Friction", RomName = "friction", Architecture = "x32", Target = "windows", ExtraArgs = "-noinput" } }
        };
    }
}
