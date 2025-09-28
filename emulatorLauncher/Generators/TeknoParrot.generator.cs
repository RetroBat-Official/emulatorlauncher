﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
using TeknoParrotUi.Common;
using EmulatorLauncher.PadToKeyboard;
using System.Management;
using System.Text.RegularExpressions;
using EmulatorLauncher.VPinballLauncher;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _triforce = false;
        private bool _namco2x6 = false;
        private bool _namco3xx = false;

        static readonly Dictionary<string, string> executables = new Dictionary<string, string>()
        {
            { "2Spicy",                          @"Too Spicy\elf\apacheM_HD.elf" },
            { "abc",                             @"Afterburner Climax\abc 1080p" },
            { "AkaiKatanaShinNesica",            @"Akai Katana Shin for NesicaxLive\Game.exe" },
            { "AliensExtermination",             @"Aliens Extermination\aliens\DATA\aliens dehasped.exe" },
            { "AquapazzaAquaplusDreamMatch",     @"Aquapazza Aquaplus Dream Match for NesicaxLive\Game.exe" },
            { "ArcadeLove",                      @"Arcade Love with Pengo\gl.exe" },
            { "ArcanaHeart2Nesica",              @"Arcana Heart 2 for NesicaxLive\Game.exe" },
            { "Batman",                          @"Batman\ZeusSP\sdaemon.exe" },
            { "BattleGear4",                     @"Battle Gear 4\game.exe" },
            { "BattleGear4Tuned",                @"Battle Gear 4 Tuned\game.exe" },
            { "BBCF",                            @"Blazblue - Central Fiction\game.exe" },
            { "BBCP",                            @"Blazblue Chronophantasma\Game.exe" },
            { "BBBHome",                         @"Big Buck Hunter Pro Home\game" },
            { "BladeArcus",                      @"Blade Arcus from Shining\Game\mxWritePrerenderedFrames.exe" },
            { "BladeStrangers",                  @"Blade Strangers\Game\himekaku.exe" },
            { "BlazBlueContinuumShift2",         @"Blazblue Continuum Shift II\game.exe" },
            { "BorderBreakScramble",             @"Border Break Scramble\Border Break Scramble\nrs.exe" },
            { "CaladriusAC",                     @"CaladriusAC\SILVER_AMP_R_ROM.exe" },
            { "CC",                              @"Chaos Code\game.exe" },
            { "ChaosCodeNSOC103",                @"Chaos Code - New Sign of Catastrophe for NesicaxLive\game.exe" },
            { "ChaseHQ2",                        @"Chase H.Q. 2\game.exe" },
            { "CrimzonClover",                   @"Crimzon Clover for NesicaxLive\Game.exe" },
            { "DariusBurst",                     @"Daruis Brust Another Chronicle\game.exe" },
            { "Daytona3",                        @"Daytona 3\Daytona\Daytona.exe" },
            { "DirtyDrivin",                     @"Dirty Drivin\sdaemon.exe" },
            { "DOA5",                            @"Dead or Alive 5\DOA5A.exe" },
            { "DoNotFallRunforYourDrink",        @"Do Not Fall Run For Your Drink\game.exe" },
            { "ElevatorAction",                  @"Elevator Action for NesicaxLive\game.exe" },
            { "EnEinsPerfektewelt",              @"En-Eins Perfektewelt\game.exe" },
            { "FightingClimax",                  @"FightingClimax\RingGame.exe" },
            { "FightingClimaxIgnition",          @"Fighting Climax Ignition [SDCS]\Game\RingGame.exe" },
            { "FNF",                             @"Fast and Furious\sdaemon.exe" },
            { "FNFDrift",                        @"Fast and Furious the  Drift\rawart\sdaemon.exe" },
            { "FNFSB",                           @"Fast and Furious Superbikes\sdaemon.exe" },
            { "FNFSB2",                          @"Super Bikes 2 - Raw Thrills\rawart\sdaemon.exe" },
            { "FNFSC",                           @"Fast and Furious Supercars\sdaemon.exe" },
            { "FR",                              @"Ford Racing\fordracing.exe" },
            { "GG",                              @"Sega Golden Gun\exe\RingGunR_RingWide.exe" },
            { "GGXrd",                           @"Guilty Gear Xrd Rev 2\Binaries\Win32\GuiltyGearXrd.exe" },
            { "GGXrdSIGN",                       @"Guilty Gear Xrd Sign (RingEdge 2)\Binaries\Win32\GuiltyGearXrd.exe" },
            { "GGXX",                            @"Guilty Gear XX\GGXXACP_RE2.exe" },
            { "GHA",                             @"Guitar Hero Arcade EDITION\GHA.exe" },
            { "Goketsuji",                       @"Goketsuji Ichizoku - Senzo Kuyou for NesicaxLive\game.exe" },
            { "GRID",                            @"Race Driver - GRID\Sega\Grid\GRID.exe" },
            { "GtiClub3",                        @"gticlub3 - ok\gti3.exe" },
            { "H2Overdrive",                     @"H2Overdrive\sdaemon.exe" },
            { "Homura",                          @"Homura (Type X)\Game.exe" },
            { "HOTD4",                           @"hotd4 (lindbergh)\disk0\elf\hod4M.elf" },
            { "HOTD4SP",                         @"House of The Dead 4: Special\disk0\hod4-sp\elf\hod4M.elf" },
            { "HOTDEX",                          @"House of The Dead EX\disk0\elf\hodexRI.elf" },
            { "HyperStreetFighterII",            @"Hyper Street Fighter II - The Anniversary Edition for NesicaxLive\game.exe" },
            { "ID4Exp",                          @"Id4exp\disk0\id4.elf" },
            { "ID4Jap",                          @"Id4\id4\disk0\id4.elf" },
            { "ID5",                             @"Initial D 5 Export\id5.elf" },
            { "ID6",                             @"Initial D arcade stage 6 AA\id6_dump_.exe" },
            { "ID7",                             @"Initial D arcade stage 7 AAX\InitialD7_GLW_RE_SBYD_dumped_.exe" },
            { "ID8",                             @"Initial D Arcade Stage 8 Infinity\InitialD8_GLW_RE_SBZZ_redumped_.exe" },
            { "IDZv2TP",                         @"Initial D Arcade Stage Zero Ver.2\package\InitialD0_DX11_Nu.exe" },
            { "Ikaruga",                         @"Ikaruga for NesicaxLive\game.exe" },
            { "JurassicPark",                    @"Jurassic Park Arcade\Game" },
            { "JusticeLeague",                   @"Justice League - Heroes United\JLA.exe" },
            { "KingofFightersSkyStage",          @"KOF SkyStage\Game.exe" },
            { "KingofFightersXII",               @"King of Fighters XII\game.exe" },
            { "KingofFightersXIII",              @"King of Fighters XIII\game.exe" },
            { "KingofFightersXIIIClimax",        @"King of Fighters XIII Climax\game.exe" },
            { "KingofFightersXIIIClimaxNesica",  @"King of Fighters XIII Climax for NesicaxLive\game.exe" },
            { "KingofFighters98UltimateMatchFinalEditionNesica", @"The King of Fighters '98 Ultimate Match Final Edition for NesicaxLive\game.exe" },
            { "KingofFightersMaximumImpactRegulationA", @"King of Fighters Maximum Impact Regulation A\game.exe" },
            { "KODrive",                         @"K.O. Drive\exe\M-DriveR_RingWide.exe" },
            { "Koihime",                         @"Koihime\Game\koi_systemAPM.exe" },
            { "LGI",                             @"Sega Lets Go Island\LGI_RingW_F_safe.exe" },
            { "LGI3D",                           @"Sega Lets Go Island 3D\LGI.exe" },
            { "LGJ",                             @"lets go jungle\disk0\lgj1920" },
            { "LGJS - Copie",                    @"Lets Go Jungle Special\lgjsp_app" },
            { "LGJS",                            @"Lets Go Jungle Special\lgjsp_app" },
            { "LGS",                             @"Let's Go Safari\GameSampR_RingWide.exe" },
            { "LuigisMansion",                   @"Luigi's Mansion Arcade\exe\x64\VACUUM.exe" },
            { "MB",                              @"Melty Blood Actress Again Current Code\MBAA.exe" },
            { "MeltyBloodRE2",                   @"Melty Blood AACC\Game\MBAA_RWMasterBuild.exe" },
            { "MKDX",                            @"Mario kart dx\MK_AGP3_FINAL.exe" },
            { "MS",                              @"Sonic Storm aka Mach Storm\src\game\ACE7_WIN\ACE7_WIN_10.exe" },
            { "OG",                              @"Operation G.H.O.S.T\gs2.exe" },
            { "or2spdlx",                        @"Outrun 2 SP SDX\Jennifer\Jennifer" },
            { "Persona4U",                       @"Persona 4 - The Ultimax Ultra Suplex Hold\game.exe" },
            { "PhantomBreaker",                  @"Phantom Breaker Another Code\Game\pbac_ringedge2.exe" },
            { "PokkenTournament",                @"Pokemon\ferrum_app.exe" },
            { "PowerInstinctV",                  @"Goketsuji Ichizoku - Matsuri Senzo Kuyo\game.exe" },
            { "PPQ",                             @"puyoquest\bin\Pj24App.exe" },
            { "PuzzleBobble",                    @"Puzzle Bobble for NesicaxLive\game.exe" },
            { "RaidenIIINesica",                 @"Raiden III for NesicaxLive\game.exe" },
            { "RaidenIV",                        @"Raiden IV (type x)\game.exe" },
            { "Rambo",                           @"Rambo (Lindbergh)\disk0\elf\ramboD.elf" },
            { "RastanSaga",                      @"Rastan Saga  for NesicaxLive\game.exe" },
            { "RumbleFish2Nesica",               @"Rumble Fish 2 for NesicaxLive\Release\game\Game.exe" },
            { "R-Tuned",                         @"r-tuned\R-Tuned Ultimate Street Racing\dsr_HD" },
            { "SamuraiSpiritsSen",               @"Samurai Shodown - Edge of Destiny\game.exe" },
            { "SchoolOfRagnarok",                @"School of Ragnarok\TieProduction\Binaries\Win64\TieProduction.exe" },
            { "SDR",                             @"Sega Dream Raiders\prg\game.exe" },
            { "segartv",                         @"Sega Race TV\drive.elf" },
            { "SenkoNoRondeDuo",                 @"Senko No Ronde - Duo - Dis-United Order\game.exe" },
            { "Shigami3",                        @"Shikigami No Shiro III\bin\game.exe" },
            { "ShiningForceCrossElysion",        @"Shining Force - Cross Elysion\project_f-ringedge-release.exe" },
            { "ShiningForceCrossRaid",           @"Shining Force Cross Raid\project_f-ringedge-release.exe" },
            { "SnoCross",                        @"Winter X Games Snocross\sdaemon.exe" },
            { "SpaceInvaders",                   @"Space Invaders\Game.exe" },
            { "SpicaAdventure",                  @"Spica Adventure\game.exe" },
            { "SR3",                             @"Sega Rally 3 (Europa-R)\Rally\Rally.exe" },
            { "SRC",                             @"Sega racing\Sega_Racing_Classic_RingWide - ok\d1a.exe" },
            { "SSASR",                           @"Sonic Sega All Stars Racing Arcade\game.exe" },
            { "StarWars",                        @"Star Wars - Battle Pod\Launcher\RSLauncher.exe" },
            { "StraniaTheStellaMachina",         @"Strania The Stella Machina\game.exe" },
            { "StreetFighterIII3rdStrike",       @"Street Fighter III 3rd Strike - Fight for the Future\game.exe" },
            { "StreetFighterVTypeArcade",        @"Street Fighter V Type Arcade\game\WindowsNoEditor\StreetFighterV.exe" },
            { "StreetFighterZero3",              @"Street Fighter Zero 3 for NesicaxLive\game.exe" },
            { "SuperStreetFighterIVArcadeEdition", @"Super Street Fighter IV Arcade Edition\game.exe" },
            { "TaisenHotGimmick5",               @"Taisen Hot Gimmick 5\Game.exe" },
            { "TargetTerrorGold",                @"Target Terror: Gold\game" },
            { "Tekken7",                         @"Tekken 7 Fated Retribution\TekkenGame\Binaries\Win64\TekkenGame-Win64-Shipping.exe" },
            { "Tekken7FR",                       @"Tekken7FR\TekkenGame\Binaries\Win64\TekkenGame-Win64-Shipping.exe" },
            { "TetrisTheGrandMaster3TerrorInstinct", @"Tetris The Grand Master 3 Terror Instinct\Gamehd.exe" },
            { "Theatrhythm",                     @"Theatrhythm Final Fantasy All Star Carnival\game.exe" },
            { "TokyoCop",                        @"Tokyo Cop\home\joc2001\joc2001\Sources\gameport\linux\gameport" },
            { "Transformers",                    @"transformers_final\exe\TF_Gun_R_Ring_dumped.exe" },
            { "TransformersShadowsRising",       @"Transformers: Shadows Rising\Sega\Transformers2\Transformers2.exe" },
            { "UDX",                             @"Under Defeat HD+\UDX_RINGEDGE.exe" },
            { "UltraStreetFighterIV",            @"Ultra Street Fighter 4 for NesicaxLive\game.exe"},
            { "UnderNightInBirthExeLatest",      @"Under Night In-Birth ExeLate[st]\Game\RingGame.exe" },
            { "VampireSavior",                   @"Vampire Savior - The Lord of Vampire for NesicaxLive\game.exe" },
            { "VF5B",                            @"Virtua Fighter 5 Rev B\vf5" },
            { "VF5C",                            @"Virtua Fighter 5 Rev C\vf5" },
            { "VirtuaRLimit",                    @"Valve Limit R\launcher.exe" },
            { "VT3",                             @"Virtua Tennis 3\vt3_Lindbergh\vt3_Lindbergh_FULLHD" },
            { "VT4",                             @"Virtua Tennis 4 (Ring Edge)\VT4_RING_r.exe" },
            { "WackyRaces",                      @"Wacky Race\Launcher.exe" },
            { "WMMT5",                           @"Wangan Midnight Maximum Tune 5\wmn5r.exe" },
            { "YugiohDT6U",                      @"Yu\exe\game.exe" },
        };

        private string _exename;
        private GameProfile _gameProfile;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("TeknoParrot");
            string exe = Path.Combine(path, "TeknoParrotUi.exe");
            if (!File.Exists(exe))
                return null;

            if (core == "triforce")
                _triforce = true;
            else if (core == "namco2x6")
                _namco2x6 = true;
            else if (core == "namco3xx")
                _namco3xx = true;

            if (!_namco2x6)
                rom = this.TryUnZipGameIfNeeded(system, rom);

            string gameName = Path.GetFileNameWithoutExtension(rom);
            SimpleLogger.Instance.Info("[INFO] Game name : " + gameName);

            GameProfile profile = FindGameProfile(path, rom, gameName, _triforce, _namco2x6, _namco3xx);
            if (profile == null)
            {
                SimpleLogger.Instance.Error("[TeknoParrotGenerator] Unable to find gameprofile for " + rom);
                return new ProcessStartInfo() { FileName = "WARNING", Arguments = "Unable to find game profile.\r\nPlease make sure the game folder is named like the GameProfile file in emulators/teknoparrot/GameProfiles folder." };
            }

            SetupParrotData(path);

            GameProfile userProfile = null;

            SimpleLogger.Instance.Info("[INFO] Checking if userprofile exists.");
            var userProfilePath = Path.Combine(Path.Combine(path, "UserProfiles", Path.GetFileName(profile.FileName)));
            if (File.Exists(userProfilePath))
            {
                SimpleLogger.Instance.Info("[INFO] UserProfile already exists.");
                userProfile = JoystickHelper.DeSerializeGameProfile(userProfilePath, true);
            }
            else
            {
                SimpleLogger.Instance.Info("[INFO] Generating user profile.");
                JoystickHelper.SerializeGameProfile(profile, userProfilePath);
                userProfile = JoystickHelper.DeSerializeGameProfile(userProfilePath, true);
            }

            userProfile.ProfileName = Path.GetFileNameWithoutExtension(userProfilePath);

            if (userProfile == null)
            {
                SimpleLogger.Instance.Error("[TeknoParrotGenerator] Unable create userprofile for " + rom);
                return new ProcessStartInfo() { FileName = "WARNING", Arguments = "Unable to create userprofile" };
            }

            bool multiExe = false;

            // Triforce case : put iso path in GamePath tag
            if (_triforce || _namco2x6)
            {
                userProfile.GamePath = rom;
            }

            else if (_namco3xx)
            {
                string gamePath = Directory.GetFiles(rom, userProfile.ExecutableName, SearchOption.AllDirectories).FirstOrDefault();
                if (gamePath != null)
                {
                    userProfile.GamePath = gamePath;
                }
            }

            if (userProfile.GamePath == null || !File.Exists(userProfile.GamePath))
            {
                SimpleLogger.Instance.Info("[INFO] Searching for Game executable.");
                if (userProfile.ExecutableName != null && userProfile.ExecutableName.Contains(";"))
                {
                    var split = userProfile.ExecutableName.Split(';');
                    if (split.Length > 1)
                    {
                        userProfile.ExecutableName = split[0];
                        multiExe = true;
                    }
                }

            RetryWithSecondExe:
                userProfile.GamePath = FindExecutable(rom, Path.GetFileNameWithoutExtension(userProfile.FileName));

                if (userProfile.ExecutableName == "game")
                {
                    if (userProfile.EmulatorType != null)
                    {
                        string exeLoaderPath = Path.Combine(path, userProfile.EmulatorType);
                        if (Directory.Exists(exeLoaderPath))
                        {
                            string[] exeFiles = Directory.GetFiles(exeLoaderPath, "*.exe");
                            if (exeFiles.Length > 0)
                                _exename = exeFiles[0];
                        }
                    }
                }

                string tempPath = userProfile.GamePath;
                if (string.IsNullOrEmpty(tempPath) && userProfile.ExecutableName == "game")
                {
                    userProfile.GamePath = File.Exists(Path.Combine(rom, "game")) ? Path.Combine(rom, "game") : null;
                }

                tempPath = userProfile.GamePath;
                if (string.IsNullOrEmpty(tempPath) && userProfile.ExecutableName != null)
                    userProfile.GamePath = FindBestExecutable(rom, userProfile.ExecutableName);
                else if (string.IsNullOrEmpty(tempPath) && userProfile.ExecutableName == null)
                {
                    if (executables.ContainsKey(Path.GetFileNameWithoutExtension(userProfile.FileName)))
                    {
                        try
                        {
                            string tempExe = Path.GetFileName(executables[Path.GetFileNameWithoutExtension(userProfile.FileName)]);
                            if (tempExe != null)
                                userProfile.GamePath = FindBestExecutable(rom, tempExe);
                        }
                        catch { }
                    }
                }

                if (userProfile.GamePath == null)
                {
                    if (multiExe)
                    {
                        var split = profile.ExecutableName.Split(';');
                        if (split.Length > 1)
                            userProfile.ExecutableName = split[1];

                        SimpleLogger.Instance.Info("[INFO] Searching for Game executable with second executable.");

                        goto RetryWithSecondExe;
                    }

                    // Final search in yml file used for reshade
                    string ExecutableYml = null;
                    try
                    {
                        if (GetYmlExeInfo(gameName, out ExecutableYml))
                        {
                            string exeFile = Directory.GetFiles(rom, ExecutableYml, SearchOption.AllDirectories).FirstOrDefault();
                            if (exeFile != null)
                                userProfile.GamePath = exeFile;
                        }
                    }
                    catch { }

                    if (userProfile.GamePath == null)
                    {
                        SimpleLogger.Instance.Error("[TeknoParrotGenerator] Unable to find Game executable for " + rom);
                        return new ProcessStartInfo() { FileName = "WARNING", Arguments = "Unable to find game executable" };
                    }
                }
            }

            if (profile.ExecutableName2 != null)
            {
                if (userProfile.GamePath2 == null || !File.Exists(userProfile.GamePath2))
                {
                    if (userProfile.ExecutableName2.Contains(";"))
                    {
                        var split = userProfile.ExecutableName2.Split(';');
                        if (split.Length > 1)
                            userProfile.ExecutableName2 = split[0];
                    }

                    userProfile.GamePath2 = FindBestExecutable(rom, userProfile.ExecutableName2);
                }
            }

            // Manage fullscreen
            var windowed = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Windowed");
            if (windowed != null && SystemConfig.isOptSet("tp_fsmode") && (SystemConfig["tp_fsmode"] == "1" || SystemConfig["tp_fsmode"] == "2"))
                windowed.FieldValue = "1";
            else if (windowed != null)
                windowed.FieldValue = "0";

            var displaymode = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "DisplayMode");
            if (displaymode != null && SystemConfig.isOptSet("tp_fsmode") && !string.IsNullOrEmpty(SystemConfig["tp_fsmode"]))
            {
                string fs_mode = SystemConfig["tp_fsmode"];
                switch (fs_mode)
                {
                    case "0":
                        if (displaymode.FieldOptions != null && displaymode.FieldOptions.Any(f => f == "Windowed"))
                            displaymode.FieldValue = "Windowed";
                        break;
                    case "1":
                        if (displaymode.FieldOptions != null && displaymode.FieldOptions.Any(f => f == "Fullscreen Windowed"))
                            displaymode.FieldValue = "Fullscreen Windowed";
                        break;
                    case "2":
                        if (displaymode.FieldOptions != null && displaymode.FieldOptions.Any(f => f == "Fullscreen"))
                            displaymode.FieldValue = "Fullscreen";
                        break;
                }
            }

            var hideCursor = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "HideCursor");
            if (hideCursor != null)
            {
                if (SystemConfig.isOptSet("tp_display_cursor") && SystemConfig.getOptBoolean("tp_display_cursor"))
                    hideCursor.FieldValue = "0";
                else
                    hideCursor.FieldValue = "1";
            }

            var customResolution = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "CustomResolution");
            var resolutionWidth = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "ResolutionWidth");
            var resolutionHeight = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "ResolutionHeight");
            if (customResolution != null && resolutionWidth != null && resolutionHeight != null)
            {
                int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
                int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

                customResolution.FieldValue = "1";
                resolutionWidth.FieldValue = resX.ToString();
                resolutionHeight.FieldValue = resY.ToString();
            }

            // Option to disable RequiresAdmin tag
            var requiresadmin = profile.RequiresAdmin;
            
            if (SystemConfig.isOptSet("requires_admin") && SystemConfig.getOptBoolean("requires_admin"))
                userProfile.RequiresAdmin = false;
            else
                userProfile.RequiresAdmin = requiresadmin;

            // APM3ID - for online gaming
            var apm3id = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "APM3ID");
            if (apm3id != null && SystemConfig.isOptSet("apm3id") && !string.IsNullOrEmpty(SystemConfig["apm3id"]))
            {
                string rbapm3id = SystemConfig["apm3id"].ToUpperInvariant();
                string tpapm3id = apm3id.FieldValue.ToUpperInvariant();
                if (string.IsNullOrEmpty(tpapm3id) || rbapm3id != tpapm3id)
                    apm3id.FieldValue = rbapm3id;
            }

            ConfigureControllers(userProfile, rom);

            if (_namco2x6)
                ConfigurePlay(userProfile);
            if (_namco3xx)
                ConfigureRpcs3(userProfile, path);

            JoystickHelper.SerializeGameProfile(userProfile, userProfilePath);

            Thread.Sleep(500);

            string profileName = Path.GetFileName(userProfile.FileName);

            // Apply reshade bezels
            string reshadeExe;
            string reshadeExecutablePath;
            ReshadeBezelType reshadeType = ReshadeBezelType.opengl;
            ReshadePlatform reshadePlatform = EmulatorLauncher.ReshadePlatform.x86;

            GetReshadeInfo(gameName, out reshadeExe, out reshadeType, out reshadePlatform);

            if (reshadeExe != null)
            {
                if (reshadeExe.Contains(';'))
                {
                    string[] reshadeExes = reshadeExe.Split(';');
                    reshadeExecutablePath = FindReshadeFolder(reshadeExes[0], rom);

                    if (reshadeExes.Length > 1 && reshadeExecutablePath == null)
                        reshadeExecutablePath = FindReshadeFolder(reshadeExes[1], rom);
                }
                else
                    reshadeExecutablePath = FindReshadeFolder(reshadeExe, rom);

                if (reshadeExecutablePath != null)
                {
                    SimpleLogger.Instance.Info("[INFO] Applying Reshade.");
                    if (!ReshadeManager.Setup(reshadeType, reshadePlatform, system, rom, reshadeExecutablePath, resolution, emulator))
                        _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                }
            }

            _resolution = resolution;

            if (_exename == null)
                _exename = Path.GetFileNameWithoutExtension(userProfile.GamePath);
            
            _gameProfile = userProfile;

            List<string> commandArray = new List<string>
            {
                "--profile=" + profileName
            };

            if (!SystemConfig.isOptSet("tp_minimize") || SystemConfig.getOptBoolean("tp_minimize"))
                commandArray.Add("--startMinimized");
            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Verb = userProfile.RequiresAdmin ? "runas" : null,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private static void SetupParrotData(string path)
        {
            string parrotData = Path.Combine(path, "ParrotData.xml");

            if (File.Exists(parrotData))
            {
                SimpleLogger.Instance.Info("[INFO] Setting up ParrotData.xml");

                XElement xdoc = XElement.Load(parrotData);

                xdoc.SetElementValue("SilentMode", true);
                xdoc.SetElementValue("ConfirmExit", false);
                xdoc.SetElementValue("HideVanguardWarning", true);
                xdoc.SetElementValue("DisableAnalytics", true);
                xdoc.SetElementValue("FirstTimeSetupComplete", true);
                xdoc.SetElementValue("HideDolphinGUI", true);

                if (Program.SystemConfig.isOptSet("tp_stooz") && !string.IsNullOrEmpty(Program.SystemConfig["tp_stooz"]))
                {
                    xdoc.SetElementValue("UseSto0ZDrivingHack", true);
                    string stooz = Program.SystemConfig["tp_stooz"].ToIntegerString();
                    xdoc.SetElementValue("StoozPercent", stooz.ToInteger());
                }
                else
                {
                    xdoc.SetElementValue("UseSto0ZDrivingHack", false);
                    xdoc.SetElementValue("StoozPercent", 0);
                }
                
                if (Program.SystemConfig.isOptSet("discord") && Program.SystemConfig.getOptBoolean("discord"))
                    xdoc.SetElementValue("UseDiscordRPC", true);
                else
                    xdoc.SetElementValue("UseDiscordRPC", false);

                xdoc.Save(parrotData);
            }

            else
            {
                ParrotData data = new ParrotData();

                if (!data.SilentMode || data.ConfirmExit)
                {
                    data.SilentMode = true;
                    data.ConfirmExit = false;
                }

                if (Program.SystemConfig.isOptSet("tp_stooz") && !string.IsNullOrEmpty(Program.SystemConfig["tp_stooz"]))
                {
                    data.UseSto0ZDrivingHack = true;
                    string stooz = Program.SystemConfig["tp_stooz"].ToIntegerString();
                    data.StoozPercent = stooz.ToInteger();
                }
                else
                {
                    data.UseSto0ZDrivingHack = false;
                    data.StoozPercent = 0;
                }

                if (Program.SystemConfig.isOptSet("discord") && Program.SystemConfig.getOptBoolean("discord"))
                    data.UseDiscordRPC = true;
                else
                    data.UseDiscordRPC = false;

                data.HideVanguardWarning = true;
                data.DisableAnalytics = true;

                File.WriteAllText(parrotData, data.ToXml());
            }
        }

        private static void ConfigurePlay(GameProfile userProfile)
        {
            var graphicsBackend = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Graphics Backend");
            if (graphicsBackend != null && Program.SystemConfig.isOptSet("tp_play_gpuapi") && !string.IsNullOrEmpty(Program.SystemConfig["tp_play_gpuapi"]))
                graphicsBackend.FieldValue = Program.SystemConfig["tp_play_gpuapi"];
            else
                graphicsBackend.FieldValue = "Vulkan";

            var resolution = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Resolution");
            if (resolution != null && Program.SystemConfig.isOptSet("tp_play_resolution") && !string.IsNullOrEmpty(Program.SystemConfig["tp_play_resolution"]))
                resolution.FieldValue = Program.SystemConfig["tp_play_resolution"];
            else
                resolution.FieldValue = "480p";
        }

        private static void ConfigureRpcs3(GameProfile userProfile, string emuPath)
        {
            var graphicsBackend = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Graphics Backend");
            if (graphicsBackend != null && Program.SystemConfig.isOptSet("tp_rpcs3_gpuapi") && !string.IsNullOrEmpty(Program.SystemConfig["tp_rpcs3_gpuapi"]))
                graphicsBackend.FieldValue = Program.SystemConfig["tp_rpcs3_gpuapi"];
            else if (graphicsBackend != null)
                graphicsBackend.FieldValue = "Vulkan";

            var resolution = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Resolution Scale");
            if (resolution != null && Program.SystemConfig.isOptSet("tp_rpcs3_resolution") && !string.IsNullOrEmpty(Program.SystemConfig["tp_rpcs3_resolution"]))
            {
                string resolutionValue = Program.SystemConfig["tp_rpcs3_resolution"].ToIntegerString();
                resolution.FieldValue = resolutionValue;
            }
            else if (resolution != null)
                resolution.FieldValue = "100";

            var rotaryDigital = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Use Buttons For Rotary Encoders");
            if (rotaryDigital != null && Program.SystemConfig.getOptBoolean("tp_digitalrotary"))
            {
                rotaryDigital.FieldValue = "1";
            }
            else if (rotaryDigital != null)
                rotaryDigital.FieldValue = "0";

            string rpcs3Config = Path.Combine(emuPath, "RPCS3", "config", "config.yml");

            if (File.Exists(rpcs3Config))
            {
                YmlFile yml = YmlFile.Load(rpcs3Config);
                YmlContainer misc = yml.GetOrCreateContainer("Miscellaneous");
                YmlContainer video = yml.GetOrCreateContainer("Video");

                misc["Automatically start games after boot"] = "true";
                misc["Exit RPCS3 when process finishes"] = "true";
                misc["Prevent display sleep while running games"] = "true";
                if (Program.SystemConfig.isOptSet("tp_rpcs3_writecolorbuffers") && !string.IsNullOrEmpty(Program.SystemConfig["tp_rpcs3_writecolorbuffers"]))
                    video["Write Color Buffers"] = Program.SystemConfig.getOptBoolean("tp_rpcs3_writecolorbuffers") ? "true" : "false";
                else
                    video["Write Color Buffers"] = "false";

                yml.Save();
            }

            string rpcs3guiConfig = Path.Combine(emuPath, "RPCS3", "GuiConfigs", "CurrentSettings.ini");

            if (File.Exists(rpcs3guiConfig))
            {
                using (var ini = new IniFile(rpcs3guiConfig))
                {
                    ini.WriteValue("main_window", "confirmationBoxExitGame", "false");
                    ini.WriteValue("main_window", "infoBoxEnabledInstallPUP", "false");
                    ini.WriteValue("main_window", "infoBoxEnabledWelcome", "false");
                    ini.WriteValue("main_window", "confirmationBoxBootGame", "false");
                    ini.WriteValue("main_window", "infoBoxEnabledInstallPKG", "false");
                    ini.WriteValue("Meta", "checkUpdateStart", "false");

                    ini.Save();
                }
            }
        }

        private static string RootDirectory(string path)
        {
            string tmp = Path.GetDirectoryName(path);
            
            string ret = string.Empty;
            while (!string.IsNullOrEmpty(tmp))
            {
                ret = tmp;
                tmp = Path.GetDirectoryName(tmp);
            }

            return ret;
        }

        private static string RelativeDirectory(string path)
        {
            int l = RootDirectory(path).Length;
            if (l > 0)
                return path.Substring(l + 1);

            return path;
        }

        private static GameProfile FindGameProfile(string path, string romPath, string gameName, bool triforce = false, bool namco2x6 = false, bool namco3xx = false)
        {
            string currentFolderName = Path.GetFileNameWithoutExtension(romPath);

            if (GetProfileFromYml(gameName, out string ymlProfileName))
            {
                var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine(path, "GameProfiles", ymlProfileName + ".xml"), false);
                if (profile != null)
                    return profile;
            }

            if (namco2x6)
            {
                if (File.Exists(Path.Combine(path, "GameProfiles", gameName + ".xml")))
                {
                    var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine(path, "GameProfiles", gameName + ".xml"), false);
                    if (profile != null)
                        return profile;
                }
            }

            if (triforce)
            {
                string directoryPath = Path.GetDirectoryName(romPath);
                string lastFolderName = Path.GetFileName(directoryPath);

                if (File.Exists(Path.Combine(path, "GameProfiles", lastFolderName + ".xml")))
                {
                    var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine(path, "GameProfiles", lastFolderName + ".xml"), false);
                    if (profile != null)
                        return profile;
                }
            }

            if (namco3xx)
            {
                string profileFile = Directory.GetFiles(romPath, "*.tprofile", SearchOption.TopDirectoryOnly).FirstOrDefault();

                if (File.Exists(profileFile))
                {
                    string profileName = Path.GetFileNameWithoutExtension(profileFile);
                    
                    SimpleLogger.Instance.Info("[INFO] Profile file found: " + profileFile);
                    
                    if (File.Exists(Path.Combine(path, "GameProfiles", profileName + ".xml")))
                    {
                        var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine(path, "GameProfiles", profileName + ".xml"), false);
                        if (profile != null)
                            return profile;
                    }
                }

                if (!File.Exists(profileFile))
                {
                    string profileName = Path.GetFileNameWithoutExtension(romPath);
                    SimpleLogger.Instance.Info("[INFO] Using folder name to find Game Profile.");

                    var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine(path, "GameProfiles", profileName + ".xml"), false);
                    if (profile != null)
                        return profile;
                }
            }

            foreach (var exe in executables)
            {                
                var root = RootDirectory(exe.Value);
                if (root == currentFolderName)
                {
                    gameName = exe.Key;
                    break;
                }
            }

            if (File.Exists(Path.Combine(path, "GameProfiles", gameName + ".xml")))
            {
                var profile = JoystickHelper.DeSerializeGameProfile(Path.Combine(path, "GameProfiles", gameName + ".xml"), false);
                if (profile != null)
                    return profile;
            }

            var profiles = new List<GameProfile>();

            foreach (var file in Directory.GetFiles(Path.Combine(path, "GameProfiles")))
            {
                SimpleLogger.Instance.Info("[WARNING] Game Profile not found, trying to deserialize all profile files: " + Path.GetFileNameWithoutExtension(file));
                var profile = JoystickHelper.DeSerializeGameProfile(file, false);
                if (profile == null)
                    continue;

                if (string.IsNullOrEmpty(profile.GameName))
                {
                    try
                    {
                        string json = Path.Combine(path, "Metadata", Path.GetFileNameWithoutExtension(file) + ".json");
                        if (File.Exists(json))
                        {
                            var js = DynamicJson.Load(json);
                            profile.GameName = js["game_name"];
                        }
                    }
                    catch { }
                }

                if (gameName.Equals(profile.GameName, StringComparison.InvariantCultureIgnoreCase))
                    return profile;

                if (CleanupGameName(gameName).Equals(CleanupGameName(profile.GameName), StringComparison.InvariantCultureIgnoreCase))
                    return profile;

                if (!string.IsNullOrEmpty(profile.ExecutableName))
                    profiles.Add(profile);
            }


            var uniqueExes = executables
                .GroupBy(x => RelativeDirectory(x.Value))
                .Where(x => x.Count() == 1)
                .Select(y => y.FirstOrDefault())
                .ToDictionary(y => y.Key, y => y.Value);

            foreach (var profile in profiles)
            {
                var profilName = Path.GetFileNameWithoutExtension(profile.FileName);
                if (!uniqueExes.ContainsKey(profilName))
                    continue;

                var root = RelativeDirectory(uniqueExes[profilName]);
                if (File.Exists(Path.Combine(romPath, root)))
                    return profile;
            }
         
            return null;
        }

        private static string CleanupGameName(string name)
        {
            if (name == null)
                return string.Empty;

            if (name.Contains(":"))
                name = name.Replace(":", "").Replace("  ", " ").Trim();

            if (name.Contains("-"))
                name = name.Replace("-", "").Replace("  ", " ").Trim();

            if (name.Contains("."))
                name = name.Replace(".", "");

            if (name.Contains("'"))
                name = name.Replace("'", "");

            return name.Trim();
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (string.IsNullOrEmpty(_exename))
                return mapping;

            mapping.ForceApplyToProcess = "TeknoParrotUI";

            mapping = PadToKey.AddOrUpdateKeyMapping(mapping, "TeknoParrotUI", InputKey.hotkey | InputKey.start, "(%{KILL})");
            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

        private static void KillProcessTree(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return;

            foreach(var process in processes)
                KillProcessAndChildren(process.Id);
        }

        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));

            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        private void KillIDZ()
        {
            if (_gameProfile == null || _gameProfile.EmulationProfile != "SegaToolsIDZ")
                return;

            Regex regex = new Regex(@"amdaemon.*");

            foreach (Process p in Process.GetProcesses("."))
            {
                if (regex.Match(p.ProcessName).Success)
                {
                    p.Kill();
                    Console.WriteLine("killed amdaemon!");
                }
            }

            regex = new Regex(@"InitialD0.*");

            foreach (Process p in Process.GetProcesses("."))
            {
                if (regex.Match(p.ProcessName).Success)
                {
                    p.Kill();
                    Console.WriteLine("killed game process!");
                }
            }

            regex = new Regex(@"ServerBoxD8.*");

            foreach (Process p in Process.GetProcesses("."))
            {
                if (regex.Match(p.ProcessName).Success)
                {
                    p.Kill();
                    Console.WriteLine("killed serverbox!");
                }
            }

            regex = new Regex(@"inject.*");

            foreach (Process p in Process.GetProcesses("."))
            {
                if (regex.Match(p.ProcessName).Success)
                {
                    p.Kill();
                    Console.WriteLine("killed inject.exe!");
                }
            }

            regex = new Regex(@"node.*");

            foreach (Process p in Process.GetProcesses("."))
            {
                if (regex.Match(p.ProcessName).Success)
                {
                    p.Kill();
                    Console.WriteLine("killed nodeJS! (if you were running node, you may want to restart it)");
                }
            }
        }

        private static string FindExecutable(string path, string profileName)
        {
            SimpleLogger.Instance.Info("[INFO] Searching in internal Database.");
            if (!executables.ContainsKey(profileName))
                return null;

            var root = RelativeDirectory(executables[profileName]);
            if (File.Exists(Path.Combine(path, root)))
                return Path.Combine(path, root);

            return null;
        }

        private static string FindBestExecutable(string path, string executableName)
        {
            SimpleLogger.Instance.Info("[INFO] Searching more widely.");
            try
            {
                // Search for the file in the current directory and all subdirectories
                foreach (string file in Directory.GetFiles(path, executableName, SearchOption.AllDirectories))
                {
                    return file;
                }
            }
            catch { }

            return null;
        }

        private bool GetReshadeInfo(string game, out string reshadeExe, out ReshadeBezelType type, out ReshadePlatform platform)
        {
            reshadeExe = null;
            type = ReshadeBezelType.opengl;
            platform = ReshadePlatform.x86;

            string reshadeInfoFile = Path.Combine(Program.AppConfig.GetFullPath("tools"), "teknoparrotReshade.yml");

            try
            {
                var yml = YmlFile.Load(reshadeInfoFile);
                if (yml != null)
                {
                    var gameReshade = yml.GetContainer(game);
                    if (gameReshade != null)
                    {
                        foreach (var infoLine in gameReshade.Elements)
                        {
                            YmlElement info = infoLine as YmlElement;
                            if (info.Name == "path")
                            {
                                reshadeExe = info.Value;
                                continue;
                            }

                            else if (info.Name == "platform")
                            {
                                bool platformExists = Enum.TryParse(info.Value, out platform);
                            }

                            else if (info.Name == "type")
                            {
                                bool typeExists = Enum.TryParse(info.Value, out type);
                            }
                        }
                        SimpleLogger.Instance.Info("[INFO] Information for Reshade is found.");
                        return true;
                    }
                    else return false;
                }
                else return false;
            }
            catch { return false; }
        }

        private bool GetYmlExeInfo(string game, out string YmlExe)
        {
            YmlExe = null;

            string ExeInfoFile = Path.Combine(Program.AppConfig.GetFullPath("tools"), "teknoparrotInfo.yml");

            try
            {
                var yml = YmlFile.Load(ExeInfoFile);
                if (yml != null)
                {
                    var gameInfo = yml.GetContainer(game);
                    if (gameInfo != null)
                    {
                        foreach (var infoLine in gameInfo.Elements)
                        {
                            YmlElement info = infoLine as YmlElement;
                            if (info.Name == "executable")
                            {
                                YmlExe = info.Value;
                                continue;
                            }
                        }
                        return true;
                    }
                    else return false;
                }
                else return false;
            }
            catch { return false; }
        }

        private static bool GetProfileFromYml(string game, out string ProfileName)
        {
            ProfileName = null;

            string ProfileInfoFile = Path.Combine(Program.AppConfig.GetFullPath("tools"), "teknoparrotInfo.yml");

            try
            {
                var yml = YmlFile.Load(ProfileInfoFile);
                if (yml != null)
                {
                    var gameInfo = yml.GetContainer(game);
                    if (gameInfo != null)
                    {
                        foreach (var infoLine in gameInfo.Elements)
                        {
                            YmlElement info = infoLine as YmlElement;
                            if (info.Name == "profile")
                            {
                                ProfileName = info.Value;
                                continue;
                            }
                        }
                        return true;
                    }
                    else return false;
                }
                else return false;
            }
            catch { return false; }
        }

        private string FindReshadeFolder(string executable, string rom)
        {
            string ret = null;

            if (executable == null)
                return null;

            SimpleLogger.Instance.Info("[INFO] Searching folder for Reshade based on : " + executable);

            switch (executable)
            {
                case "tp_budgie":
                    ret = Path.Combine(AppConfig.GetFullPath("teknoparrot"), "TeknoParrot");
                    break;
                case "elf_budgie":
                    ret = Path.Combine(AppConfig.GetFullPath("teknoparrot"), "ElfLdr2");
                    break;
                default:
                    string exeLocation = Directory.GetFiles(rom, executable, SearchOption.AllDirectories).FirstOrDefault();
                    if (exeLocation != null)
                        ret = Path.GetDirectoryName(exeLocation);
                    break;
            }
            return ret;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            if (path.FileName == "WARNING")
            {
                using (LoadingForm frm = new LoadingForm())
                {
                    frm.WarningText = path.Arguments;
                    frm.Show();

                    int ticks = Environment.TickCount;
                    
                    while(Environment.TickCount - ticks < 4000)
                    {
                        Application.DoEvents();
                        Thread.Sleep(1);
                    }
                }

                return 0;
            }

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            KillProcessTree("TeknoParrotUI");
            KillProcessTree(_exename);

            if (Process.GetProcessesByName("OpenParrotLoader").Length > 0 || Process.GetProcessesByName("OpenParrotLoader64").Length > 0)
            {
                Thread.Sleep(1000);

                KillProcessTree("OpenParrotLoader");
                KillProcessTree("OpenParrotLoader64");
            }

            KillProcessTree("BudgieLoader");
            KillProcessTree("OpenParrotKonamiLoader");
            KillIDZ();

            if (_sindenSoft)
                Guns.KillSindenSoftware();

            if (_demulshooter)
                Demulshooter.KillDemulShooter();

            if (_triforce)
                KillProcessTree("Dolphin");

            if (_namco3xx)
                KillProcessTree("rpcs3");

            return 0;
        }
    }

    public class ParrotData
    {
        public ParrotData()
        {
            ExitGameKey = "0x1B";
            PauseGameKey = "0x13";
            ScoreCollapseGUIKey = "0x79";
            CheckForUpdates = true;
            ConfirmExit = true;
            DownloadIcons = true;
            UiDisableHardwareAcceleration = false;
            HideVanguardWarning = true;
            UiColour = "lightblue";
            UiDarkMode = false;
            UiHolidayThemes = true;
            HasReadPolicies = false;
            DisableAnalytics = true;
            FirstTimeSetupComplete = true;
            HideDolphinGUI = true;
        }

        public bool UseSto0ZDrivingHack { get; set; }
        public int StoozPercent { get; set; }
        public bool FullAxisGas { get; set; }
        public bool FullAxisBrake { get; set; }
        public bool ReverseAxisGas { get; set; }
        public bool ReverseAxisBrake { get; set; }
        public string LastPlayed { get; set; }
        public string ExitGameKey { get; set; }
        public string PauseGameKey { get; set; }
        public string ScoreSubmissionID { get; set; }
        public string ScoreCollapseGUIKey { get; set; }
        public bool SaveLastPlayed { get; set; }
        public bool UseDiscordRPC { get; set; }
        public bool SilentMode { get; set; }
        public bool CheckForUpdates { get; set; }
        public bool ConfirmExit { get; set; }
        public bool DownloadIcons { get; set; }
        public bool UiDisableHardwareAcceleration { get; set; }
        public bool HideVanguardWarning { get; set; }
        public string UiColour { get; set; }
        public bool UiDarkMode { get; set; }
        public bool UiHolidayThemes { get; set; }
        public bool HasReadPolicies { get; set; }
        public bool DisableAnalytics { get; set; }
        public bool FirstTimeSetupComplete { get; set; }
        public bool HideDolphinGUI { get; set; }
    }
}
