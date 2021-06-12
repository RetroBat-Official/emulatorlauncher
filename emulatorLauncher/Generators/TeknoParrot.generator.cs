using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using TeknoParrotUi.Common;
using emulatorLauncher.Tools;
using emulatorLauncher.PadToKeyboard;
using System.Management;
using System.Text.RegularExpressions;
using VPinballLauncher;

namespace emulatorLauncher
{
    class TeknoParrotGenerator : Generator
    {        
        static Dictionary<string, string> executables = new Dictionary<string, string>()
        {                        
            { "Batman",                          @"Batman\ZeusSP\sdaemon.exe" },
            { "BBCF",                            @"Blazblue - Central Fiction\game.exe" },
            { "BBCP",                            @"Blazblue Chronophantasma\Game.exe" },
            { "BlazBlueContinuumShift2",         @"Blazblue Continuum Shift II\game.exe" },
            { "CC",                              @"Chaos Code\game.exe" },
            { "ChaosCodeNSOC103",                @"Chaos Code - New Sign of Catastrophe for NesicaxLive\game.exe" },
            { "CrimzonClover",                   @"Crimzon Clover for NesicaxLive\Game.exe" },
            { "Daytona3",                        @"Daytona 3\Daytona\Daytona.exe" },
            { "DirtyDrivin",                     @"Dirty Drivin\sdaemon.exe" },
            { "DOA5",                            @"Dead or Alive 5\DOA5A.exe" },
            { "FightingClimax",                  @"FightingClimax\RingGame.exe" },
            { "FightingClimaxIgnition",          @"Fighting Climax Ignition [SDCS]\Game\RingGame.exe" },
            { "FNFSB2",                          @"Super Bikes 2 - Raw Thrills\rawart\sdaemon.exe" },
            { "FR",                              @"Ford Racing\fordracing.exe" },
            { "GG",                              @"Sega Golden Gun\exe\RingGunR_RingWide.exe" },
            { "GGXrd",                           @"Guilty Gear Xrd Rev 2\Binaries\Win32\GuiltyGearXrd.exe" },
            { "GGXrdSIGN",                       @"Guilty Gear Xrd Sign (RingEdge 2)\Binaries\Win32\GuiltyGearXrd.exe" },
            { "GHA",                             @"Guitar Hero Arcade EDITION\GHA.exe" },
            { "H2Overdrive",                     @"H2Overdrive\sdaemon.exe" },
            { "Homura",                          @"Homura (Type X)\Game.exe" },
            { "HOTD4",                           @"hotd4 (lindbergh)\disk0\elf\hod4M.elf" },
            { "HyperStreetFighterII",            @"Hyper Street Fighter II - The Anniversary Edition for NesicaxLive\game.exe" },
            { "ID6",                             @"Initial D arcade stage 6 AA\id6_dump_.exe" },
            { "ID7",                             @"Initial D arcade stage 7 AAX\InitialD7_GLW_RE_SBYD_dumped_.exe" },
            { "ID8",                             @"Initial D Arcade Stage 8 Infinity\InitialD8_GLW_RE_SBZZ_redumped_.exe" },
            { "Ikaruga",                         @"Ikaruga for NesicaxLive\game.exe" },
            { "LGI",                             @"Sega Lets Go Island\LGI_RingW_F_safe.exe" },
            { "LuigisMansion",                   @"Luigi's Mansion Arcade\exe\x64\VACUUM.exe" },
            { "MB",                              @"Melty Blood Actress Again Current Code\MBAA.exe" },
            { "MKDX",                            @"Mario kart dx\MK_AGP3_FINAL.exe" },
            { "MS",                              @"Sonic Storm aka Mach Storm\src\game\ACE7_WIN\ACE7_WIN_10.exe" },
            { "OG",                              @"Operation G.H.O.S.T\gs2.exe" },
            { "or2spdlx",                        @"Outrun 2 SP SDX\Jennifer\Jennifer" },
            { "Persona4U",                       @"Persona 4 - The Ultimax Ultra Suplex Hold\game.exe" },
            { "PuzzleBobble",                    @"Puzzle Bobble for NesicaxLive\game.exe" },
            { "RaidenIIINesica",                 @"Raiden III for NesicaxLive\game.exe" },
            { "RaidenIV",                        @"Raiden IV (type x)\game.exe" },
            { "Rambo",                           @"Rambo (Lindbergh)\disk0\elf\ramboD.elf" },
            { "RastanSaga",                      @"Rastan Saga  for NesicaxLive\game.exe" },
            { "RumbleFish2Nesica",               @"Rumble Fish 2 for NesicaxLive\Release\game\Game.exe" },
            { "SamuraiSpiritsSen",               @"Samurai Shodown - Edge of Destiny\game.exe" },
            { "SDR",                             @"Sega Dream Raiders\prg\game.exe" },
            { "segartv",                         @"Sega Race TV\drive.elf" },
            { "ShiningForceCrossRaid",           @"Shining Force Cross Raid\project_f-ringedge-release.exe" },
            { "SpicaAdventure",                  @"Spica Adventure\game.exe" },
            { "SR3",                             @"Sega Rally 3 (Europa-R)\Rally\Rally.exe" },
            { "SSASR",                           @"Sonic Sega All Stars Racing Arcade\game.exe" },
            { "StarWars",                        @"Star Wars - Battle Pod\Launcher\RSLauncher.exe" },
            { "StreetFighterIII3rdStrike",       @"Street Fighter III 3rd Strike - Fight for the Future\game.exe" },
            { "StreetFighterZero3",              @"Street Fighter Zero 3 for NesicaxLive\game.exe" },
            { "Transformers",                    @"transformers_final\exe\TF_Gun_R_Ring_dumped.exe" },
            { "UDX",                             @"Under Defeat HD+\UDX_RINGEDGE.exe" },
            { "VampireSavior",                   @"Vampire Savior - The Lord of Vampire for NesicaxLive\game.exe" },
            { "VF5B",                            @"Virtua Fighter 5 Rev B\vf5" },
            { "VF5C",                            @"Virtua Fighter 5 Rev C\vf5" },
            { "VT3",                             @"Virtua Tennis 3\vt3_Lindbergh\vt3_Lindbergh_FULLHD" },
            { "VT4",                             @"Virtua Tennis 4 (Ring Edge)\VT4_RING_r.exe" },            

            { "2Spicy",                          @"Too Spicy\elf\apacheM_HD.elf" },
            { "abc",                             @"Afterburner Climax\abc 1080p" },
            { "AkaiKatanaShinNesica",            @"Akai Katana Shin for NesicaxLive\Game.exe" },
            { "AquapazzaAquaplusDreamMatch",     @"Aquapazza Aquaplus Dream Match for NesicaxLive\Game.exe" },
            { "ArcadeLove",                      @"Arcade Love with Pengo\gl.exe" },
            { "ArcanaHeart2Nesica",              @"Arcana Heart 2 for NesicaxLive\Game.exe" },
            { "BattleGear4",                     @"Battle Gear 4\game.exe" },
            { "BattleGear4Tuned",                @"Battle Gear 4 Tuned\game.exe" },
            { "BladeArcus",                      @"Blade Arcus from Shining\Game\mxWritePrerenderedFrames.exe" },
            { "BladeStrangers",                  @"Blade Strangers\Game\himekaku.exe" },
            { "BorderBreakScramble",             @"Border Break Scramble\Border Break Scramble\nrs.exe" },
            { "CaladriusAC",                     @"CaladriusAC\SILVER_AMP_R_ROM.exe" },
            { "ChaseHQ2",                        @"Chase H.Q. 2\game.exe" },
            { "DariusBurst",                     @"Daruis Brust Another Chronicle\GameFiles\game.exe" },
            { "DoNotFallRunforYourDrink",        @"Do Not Fall Run For Your Drink\game.exe" },
            { "ElevatorAction",                  @"Elevator Action for NesicaxLive\game.exe" },
            { "EnEinsPerfektewelt",              @"En-Eins Perfektewelt\game.exe" },
            { "FNF",                             @"Fast and Furious\sdaemon.exe" },
            { "FNFDrift",                        @"Fast and Furious the  Drift\rawart\sdaemon.exe" },
            { "FNFSB",                           @"Fast and Furious Superbikes\sdaemon.exe" },
            { "FNFSC",                           @"Fast and Furious Supercars\sdaemon.exe" },
            { "GGXX",                            @"Guilty Gear XX\GGXXACP_RE2.exe" },
            { "Goketsuji",                       @"Goketsuji Ichizoku - Senzo Kuyou for NesicaxLive\game.exe" },
            { "GRID",                            @"Race Driver - GRID\Sega\Grid\GRID.exe" },
            { "GtiClub3",                        @"gticlub3 - ok\gti3.exe" },
            { "ID4Exp",                          @"Id4exp\disk0\id4.elf" },
            { "ID4Jap",                          @"Id4\id4\disk0\id4.elf" },
            { "ID5",                             @"Initial D 5 Export\id5.elf" },
            { "JusticeLeague",                   @"Justice League - Heroes United\JLA.exe" },
            { "KODrive",                         @"K.O. Drive\exe\M-DriveR_RingWide.exe" },
            { "Koihime",                         @"Koihime\Game\koi_systemAPM.exe" },
            { "LGI3D",                           @"Sega Lets Go Island 3D\LGI.exe" },
            { "LGJ",                             @"lets go jungle\disk0\lgj1920" },
            { "LGJS - Copie",                    @"Lets Go Jungle Special\lgjsp_app" },
            { "LGJS",                            @"Lets Go Jungle Special\lgjsp_app" },
            { "LGS",                             @"Let's Go Safari\GameSampR_RingWide.exe" },
            { "MeltyBloodRE2",                   @"Melty Blood AACC\Game\MBAA_RWMasterBuild.exe" },
            { "PhantomBreaker",                  @"Phantom Breaker Another Code\Game\pbac_ringedge2.exe" },
            { "PokkenTournament",                @"Pokemon\ferrum_app.exe" },
            { "PowerInstinctV",                  @"Goketsuji Ichizoku - Matsuri Senzo Kuyo\game.exe" },
            { "PPQ",                             @"puyoquest\bin\Pj24App.exe" },
            { "R-Tuned",                         @"r-tuned\R-Tuned Ultimate Street Racing\dsr_HD" },
            { "SchoolOfRagnarok",                @"School of Ragnarok\TieProduction\Binaries\Win64\TieProduction.exe" },
            { "SenkoNoRondeDuo",                 @"Senko No Ronde - Duo - Dis-United Order\game.exe" },
            { "Shigami3",                        @"Shikigami No Shiro III\bin\game.exe" },
            { "ShiningForceCrossElysion",        @"Shining Force - Cross Elysion\project_f-ringedge-release.exe" },
            { "SnoCross",                        @"Winter X Games Snocross\sdaemon.exe" },
            { "SpaceInvaders",                   @"Space Invaders\Game.exe" },
            { "SRC",                             @"Sega racing\Sega_Racing_Classic_RingWide - ok\d1a.exe" },
            { "StraniaTheStellaMachina",         @"Strania The Stella Machina\game.exe" },
            { "TaisenHotGimmick5",               @"Taisen Hot Gimmick 5\Game.exe" },
            { "Theatrhythm",                     @"Theatrhythm Final Fantasy All Star Carnival\game.exe" },
            { "TokyoCop",                        @"Tokyo Cop\home\joc2001\joc2001\Sources\gameport\linux\gameport" },
            { "UltraStreetFighterIV",            @"Ultra Street Fighter 4 for NesicaxLive\game.exe" },
            { "UnderNightInBirthExeLatest",      @"Under Night In-Birth ExeLate[st]\Game\RingGame.exe" },
            { "VirtuaRLimit",                    @"Valve Limit R\launcher.exe" },
            { "WackyRaces",                      @"Wacky Race\Launcher.exe" },
            { "WMMT5",                           @"Wangan Midnight Maximum Tune 5\wmn5r.exe" },
            { "YugiohDT6U",                      @"Yu\exe\game.exe" },
            { "Tekken7",                         @"Tekken 7 Fated Retribution\TekkenGame\Binaries\Win64\TekkenGame-Win64-Shipping.exe" },
            { "Tekken7FR",                       @"Tekken7FR\TekkenGame\Binaries\Win64\TekkenGame-Win64-Shipping.exe" },

            { "AliensExtermination",             @"aliens\DATA\aliens dehasped.exe" },

            { "KingofFightersSkyStage",          @"KOF SkyStage\Game.exe" },
            { "KingofFightersXII",               @"King of Fighters XII\game.exe" },
            { "KingofFightersXIII",              @"King of Fighters XIII\game.exe" },
            { "KingofFightersXIIIClimax",        @"King of Fighters XIII Climax\game.exe" },
            { "KingofFightersXIIIClimaxNesica",  @"King of Fighters XIII Climax for NesicaxLive\game.exe" },

            { "SuperStreetFighterIVArcadeEdition", @"Super Street Fighter IV Arcade Edition\game.exe" },            
            { "TetrisTheGrandMaster3TerrorInstinct", @"Tetris The Grand Master 3 Terror Instinct\Gamehd.exe" },
            { "KingofFighters98UltimateMatchFinalEditionNesica", @"The King of Fighters '98 Ultimate Match Final Edition for NesicaxLive\game.exe" },
            { "KingofFightersMaximumImpactRegulationA", @"King of Fighters Maximum Impact Regulation A\game.exe" },

        };

        private string _exename;
        private GameProfile _gameProfile;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("TeknoParrot");
            string exe = Path.Combine(path, "TeknoParrotUi.exe");
            if (!File.Exists(exe))
                return null;

            // ExtractUserProfiles("h:\\");

            string gameName = Path.GetFileNameWithoutExtension(rom);

            GameProfile profile = FindGameProfile(path, rom, gameName);
            if (profile == null)
            {
                SimpleLogger.Instance.Error("Unable to find gameprofile for " + rom);
                return new ProcessStartInfo() { FileName = "WARNING", Arguments = "Unable to find game profile.\r\nPlease make sure the game folder is named like the xml file in emulators/teknoparrot/GameProfiles folder or like the <GameName> element in the xml" };
            }

            GameProfile userProfile = null;

            var userProfilePath = Path.Combine(Path.Combine(path, "UserProfiles", Path.GetFileName(profile.FileName)));
            if (File.Exists(userProfilePath))
                userProfile = JoystickHelper.DeSerializeGameProfile(userProfilePath, true);
            else
            {
                JoystickHelper.SerializeGameProfile(profile, userProfilePath);
                userProfile = JoystickHelper.DeSerializeGameProfile(userProfilePath, true);
            }

            if (userProfile == null)
            {
                SimpleLogger.Instance.Error("Unable create userprofile for " + rom);
                return new ProcessStartInfo() { FileName = "WARNING", Arguments = "Unable to create userprofile" };
            }
            
            if (userProfile.GamePath == null || !File.Exists(userProfile.GamePath))
            {
                userProfile.GamePath = FindExecutable(rom, Path.GetFileNameWithoutExtension(userProfile.FileName));    
            
                if (userProfile.GamePath == null)
                    userProfile.GamePath = FindBestExecutable(rom, userProfile.ExecutableName);

                if (userProfile.GamePath == null)
                {
                    SimpleLogger.Instance.Error("Unable to find Game executable for " + rom);
                    return new ProcessStartInfo() { FileName = "WARNING", Arguments = "Unable to find game executable" };
                }
            }

            var windowed = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Windowed");
            if (windowed != null)
                windowed.FieldValue = "0";

            var hideCursor = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "HideCursor");
            if (hideCursor != null)
                hideCursor.FieldValue = "1";

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

            ConfigureControllers(userProfile);

            JoystickHelper.SerializeGameProfile(userProfile, userProfilePath);

            string profileName = Path.GetFileName(userProfile.FileName);

            _exename = Path.GetFileNameWithoutExtension(userProfile.GamePath);
            _gameProfile = userProfile;

            return new ProcessStartInfo()
            {
                FileName = exe,
                Verb = userProfile.RequiresAdmin ? "runas" : null,
                WorkingDirectory = path,
                Arguments = "--profile=" + profileName // + " --startMinimized",
            };
        }

        private static void ExtractUserProfiles(string path)
        {

            StringBuilder sb = new StringBuilder();

            foreach (var file in Directory.GetFiles(Path.Combine(path, "UserProfiles")))
            {
                var zzz = JoystickHelper.DeSerializeGameProfile(file, false);
                if (zzz == null)
                    continue;

                string code = Path.GetFileNameWithoutExtension(zzz.FileName);
                if (executables.ContainsKey(code))
                    continue;

                string codeStr = "{ \"" + code + "\",";
                sb.Append(codeStr);
                sb.Append(new string(' ', Math.Max(1, 32 - code.Length)));
                sb.Append("@\"");
                sb.Append(zzz.GamePath);
                sb.AppendLine("\" },");
            }

            var str = sb.ToString();
        }

        // <string name="teknoparrot.disableautocontrollers" value="1" />

        private static void ConfigureControllers(GameProfile userProfile)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            bool xInput = Program.Controllers.All(c => c.Config != null && c.Config.IsXInputDevice());

            var inputAPI = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Input API");
            if (inputAPI != null)
            {
                if (xInput && inputAPI.FieldOptions != null && inputAPI.FieldOptions.Any(f => f == "XInput"))
                    inputAPI.FieldValue = "XInput";
                else if (inputAPI.FieldOptions != null && inputAPI.FieldOptions.Any(f => f == "DirectInput"))
                    inputAPI.FieldValue = "DirectInput";
            }

            foreach (var c in Program.Controllers)
            {
                if (c.Config == null || c.Config.Type == "key")
                    continue;

                if (xInput)
                {
        //            foreach (var btn in userProfile.JoystickButtons)
        //                btn.XInputButton = null;

                    if (userProfile.EmulationProfile == "NamcoMachStorm")
                    {
                        ImportXInputButton(userProfile, c.Config, InputKey.select, InputMapping.Service1);

                        ImportXInputButton(userProfile, c.Config, InputKey.leftanalogup, InputMapping.Analog6);
                        ImportXInputButton(userProfile, c.Config, InputKey.leftanalogleft, InputMapping.Analog4);
                        ImportXInputButton(userProfile, c.Config, InputKey.rightthumb, InputMapping.Analog2);

                        ImportXInputButton(userProfile, c.Config, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp);
                        ImportXInputButton(userProfile, c.Config, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown);

                        ImportXInputButton(userProfile, c.Config, InputKey.a, InputMapping.JvsTwoP1Button1, InputMapping.P1Button1);
                        ImportXInputButton(userProfile, c.Config, InputKey.b, InputMapping.ExtensionOne12);
                        ImportXInputButton(userProfile, c.Config, InputKey.x, InputMapping.ExtensionOne11);
                    }
                    else
                    {
                        if (c.Config[InputKey.leftanalogleft] != null)
                        {
                            if (userProfile.HasAnyXInputButton(InputMapping.Analog0))
                            {
                                ImportXInputButton(userProfile, c.Config, InputKey.left, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft);
                                ImportXInputButton(userProfile, c.Config, InputKey.right, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight);
                                ImportXInputButton(userProfile, c.Config, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp);
                                ImportXInputButton(userProfile, c.Config, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown);

                                // Wheel
                                ImportXInputButton(userProfile, c.Config, InputKey.leftanalogleft, InputMapping.Analog0);
                                ImportXInputButton(userProfile, c.Config, InputKey.leftanalogup, InputMapping.Analog1);

                                // Gas
                                ImportXInputButton(userProfile, c.Config, InputKey.rightthumb, InputMapping.Analog2);

                                // Brake
                                ImportXInputButton(userProfile, c.Config, InputKey.leftthumb, InputMapping.Analog4);
                            }
                            else
                            {
                                ImportXInputButton(userProfile, c.Config, InputKey.leftanalogleft, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft);
                                ImportXInputButton(userProfile, c.Config, InputKey.leftanalogright, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight);
                                ImportXInputButton(userProfile, c.Config, InputKey.leftanalogup, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp);
                                ImportXInputButton(userProfile, c.Config, InputKey.leftanalogdown, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown);
                            }
                        }
                        else
                        {
                            ImportXInputButton(userProfile, c.Config, InputKey.left, InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft);
                            ImportXInputButton(userProfile, c.Config, InputKey.right, InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight);
                            ImportXInputButton(userProfile, c.Config, InputKey.up, InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp);
                            ImportXInputButton(userProfile, c.Config, InputKey.down, InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown);
                        }

                        if (userProfile.HasAnyXInputButton(InputMapping.JvsTwoP1ButtonStart, InputMapping.P1ButtonStart, InputMapping.JvsTwoCoin1, InputMapping.Coin1))
                        {
                            ImportXInputButton(userProfile, c.Config, InputKey.start, InputMapping.JvsTwoP1ButtonStart, InputMapping.P1ButtonStart);
                            ImportXInputButton(userProfile, c.Config, InputKey.select, InputMapping.JvsTwoCoin1, InputMapping.Coin1);
                        }
                        else
                            ImportXInputButton(userProfile, c.Config, InputKey.start, InputMapping.Service1, InputMapping.JvsTwoService1);

                        ImportXInputButton(userProfile, c.Config, InputKey.a, InputMapping.JvsTwoP1Button1, InputMapping.P1Button1);
                        ImportXInputButton(userProfile, c.Config, InputKey.b, InputMapping.JvsTwoP1Button2, InputMapping.P1Button2);
                        ImportXInputButton(userProfile, c.Config, InputKey.x, InputMapping.JvsTwoP1Button3, InputMapping.P1Button3);
                        ImportXInputButton(userProfile, c.Config, InputKey.y, InputMapping.JvsTwoP1Button4, InputMapping.P1Button4);

                        ImportXInputButton(userProfile, c.Config, InputKey.leftshoulder, InputMapping.JvsTwoP1Button5, InputMapping.P1Button5);
                        ImportXInputButton(userProfile, c.Config, InputKey.rightshoulder, InputMapping.JvsTwoP1Button6, InputMapping.P1Button6);

                        if (userProfile.HasAnyXInputButton(InputMapping.ExtensionOne2) && !userProfile.HasAnyXInputButton(InputMapping.P1Button2))
                            ImportXInputButton(userProfile, c.Config, InputKey.b, InputMapping.ExtensionOne2);
                        else
                            ImportXInputButton(userProfile, c.Config, InputKey.b, InputMapping.JvsTwoP1Button2, InputMapping.P1Button2);
                    }
                }
                else
                {
           //         foreach (var btn in userProfile.JoystickButtons)
          //              btn.DirectInputButton = null;

                    if (c.Config[InputKey.leftanalogleft] != null)
                    {
                        ImportDirectInputButton(userProfile, c.Config, InputKey.leftanalogup, new InputMapping[] { InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp });
                        ImportDirectInputButton(userProfile, c.Config, InputKey.leftanalogleft, new InputMapping[] { InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft });
                        ImportDirectInputButton(userProfile, c.Config, InputKey.leftanalogdown, new InputMapping[] { InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown });
                        ImportDirectInputButton(userProfile, c.Config, InputKey.leftanalogright, new InputMapping[] { InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight });
                    }
                    else
                    {
                        ImportDirectInputButton(userProfile, c.Config, InputKey.up, new InputMapping[] { InputMapping.JvsTwoP1ButtonUp, InputMapping.P1ButtonUp });
                        ImportDirectInputButton(userProfile, c.Config, InputKey.left, new InputMapping[] { InputMapping.JvsTwoP1ButtonLeft, InputMapping.P1ButtonLeft });
                        ImportDirectInputButton(userProfile, c.Config, InputKey.down, new InputMapping[] { InputMapping.JvsTwoP1ButtonDown, InputMapping.P1ButtonDown });
                        ImportDirectInputButton(userProfile, c.Config, InputKey.right, new InputMapping[] { InputMapping.JvsTwoP1ButtonRight, InputMapping.P1ButtonRight });
                    }

                    ImportDirectInputButton(userProfile, c.Config, InputKey.select, new InputMapping[] { InputMapping.JvsTwoCoin1, InputMapping.Coin1 });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.start, new InputMapping[] { InputMapping.JvsTwoP1ButtonStart, InputMapping.P1ButtonStart });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.a, new InputMapping[] { InputMapping.JvsTwoP1Button1, InputMapping.P1Button1 });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.b, new InputMapping[] { InputMapping.JvsTwoP1Button2, InputMapping.P1Button2 });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.x, new InputMapping[] { InputMapping.JvsTwoP1Button3, InputMapping.P1Button3 });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.y, new InputMapping[] { InputMapping.JvsTwoP1Button4, InputMapping.P1Button4 });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.lefttrigger, new InputMapping[] { InputMapping.JvsTwoP1Button5, InputMapping.P1Button5 });
                    ImportDirectInputButton(userProfile, c.Config, InputKey.righttrigger, new InputMapping[] { InputMapping.JvsTwoP1Button6, InputMapping.P1Button6 });

                }

                break;
            }
        }

        private static void ImportXInputButton(GameProfile userProfile, InputConfig c, InputKey key, params InputMapping[] mapping)
        {
            var start = userProfile.JoystickButtons.FirstOrDefault(j => !j.HideWithXInput && mapping.Contains(j.InputMapping));


            bool reverseAxis = false;

            if (c[key] == null && key == InputKey.leftanalogdown)
            {
                reverseAxis = true;
                key = InputKey.leftanalogup;
            }
            if (c[key] == null && key == InputKey.leftanalogright)
            {
                reverseAxis = true;
                key = InputKey.leftanalogleft;
            }


            if (start != null && c[key] != null)
            {
                start.XInputButton = new XInputButton();

                GamepadButtonFlags result;

                if (c[key].Type == "axis")
                {
                    start.XInputButton.IsLeftThumbX = false;
                    start.XInputButton.IsRightThumbX = false;
                    start.XInputButton.IsLeftThumbY = false;
                    start.XInputButton.IsRightThumbY = false;
                    start.XInputButton.IsAxisMinus = false;
                    start.XInputButton.IsLeftTrigger = false;
                    start.XInputButton.IsRightTrigger = false;
                    start.XInputButton.XInputIndex = 0;
                    start.XInputButton.ButtonIndex = 0;
                    start.XInputButton.IsButton = false;
                    start.XInputButton.ButtonCode = 0;

                    start.BindNameXi = "Input Device 0 " + c.GetXInputMapping(key, reverseAxis).ToString();
                    start.BindName = "Input Device 0 " + c.GetXInputMapping(key, reverseAxis).ToString();

                    switch (c.GetXInputMapping(key, reverseAxis))
                    {
                        case XINPUTMAPPING.LEFTANALOG_LEFT:
                            start.XInputButton.IsLeftThumbX = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.LEFTANALOG_RIGHT:
                            start.XInputButton.IsLeftThumbX = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.LEFTANALOG_UP:
                            start.XInputButton.IsLeftThumbY = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.LEFTANALOG_DOWN:
                            start.XInputButton.IsLeftThumbY = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_LEFT:
                            start.XInputButton.IsRightThumbX = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_RIGHT:
                            start.XInputButton.IsRightThumbX = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_UP:
                            start.XInputButton.IsRightThumbY = true;
                            start.XInputButton.IsAxisMinus = false;
                            break;
                        case XINPUTMAPPING.RIGHTANALOG_DOWN:
                            start.XInputButton.IsRightThumbY = true;
                            start.XInputButton.IsAxisMinus = true;
                            break;
                        case XINPUTMAPPING.LEFTTRIGGER:
                            start.XInputButton.IsLeftTrigger = true;
                            break;
                        case XINPUTMAPPING.RIGHTTRIGGER:
                            start.XInputButton.IsRightTrigger = true;
                            break;

                    }
                }
                else if (c[key].Type == "button" || c[key].Type == "hat")
                {
                    if (Enum.TryParse<GamepadButtonFlags>(c.GetXInputMapping(key).ToString(), out result))
                    {
                        start.XInputButton.IsLeftThumbX = false;
                        start.XInputButton.IsRightThumbX = false;
                        start.XInputButton.IsLeftThumbY = false;
                        start.XInputButton.IsRightThumbY = false;
                        start.XInputButton.IsAxisMinus = false;
                        start.XInputButton.IsLeftTrigger = false;
                        start.XInputButton.IsRightTrigger = false;
                        start.XInputButton.XInputIndex = 0;
                        start.XInputButton.ButtonIndex = 0;
                        start.XInputButton.IsButton = true;
                        start.XInputButton.ButtonCode = (short)result;

                        start.BindNameXi = "Input Device 0 " + c.GetXInputMapping(key).ToString();
                        start.BindName = "Input Device 0 " + c.GetXInputMapping(key).ToString();

                    }
                }
            }
        }

        private static void ImportDirectInputButton(GameProfile userProfile, InputConfig c, InputKey key, InputMapping[] mapping)
        {
            var start = userProfile.JoystickButtons.FirstOrDefault(j => !j.HideWithDirectInput && mapping.Contains(j.InputMapping));

            bool reverseAxis = false;

            if (c[key] == null && key == InputKey.leftanalogdown)
            {
                reverseAxis = true;
                key = InputKey.leftanalogup;
            }
            if (c[key] == null && key == InputKey.leftanalogright)
            {
                reverseAxis = true;
                key = InputKey.leftanalogleft;
            }

            var info = c.GetDirectInputInfo();

            if (start != null && c[key] != null && info != null)
            {
                var ss = c[key];

                start.DirectInputButton = new JoystickButton();
                start.DirectInputButton.JoystickGuid = info.InstanceGuid;
                start.DirectInputButton.IsAxis = false;
                start.DirectInputButton.IsAxisMinus = false;
                start.DirectInputButton.IsFullAxis = false;
                start.DirectInputButton.IsReverseAxis = false;
                start.DirectInputButton.PovDirection = 0;
                start.DirectInputButton.IsReverseAxis = false;
                start.DirectInputButton.Button = 0;

                if (ss.Type == "button")
                    start.DirectInputButton.Button = (int)ss.Id + 48;
                else if (ss.Type == "hat")
                {
                    start.DirectInputButton.Button = 32;
                    if (ss.Value == 1) // Top
                        start.DirectInputButton.PovDirection = 0;
                    else if (ss.Value == 4) // Down
                        start.DirectInputButton.PovDirection = 18000;
                    else if (ss.Value == 8) // Left
                        start.DirectInputButton.PovDirection = 27000;
                    else if (ss.Value == 2) // Right
                        start.DirectInputButton.PovDirection = 9000;
                }
                else if (ss.Type == "axis")
                {
                    start.DirectInputButton.Button = (int) ss.Id * 4;
                    start.DirectInputButton.IsAxis = true;
                    start.DirectInputButton.IsAxisMinus = reverseAxis ? ss.Value > 0 : ss.Value < 0;
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

        private static GameProfile FindGameProfile(string path, string romPath, string gameName)
        {
            string currentFolderName = Path.GetFileNameWithoutExtension(romPath);

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

            List<GameProfile> profiles = new List<GameProfile>();

            foreach (var file in Directory.GetFiles(Path.Combine(path, "GameProfiles")))
            {
                var profile = JoystickHelper.DeSerializeGameProfile(file, false);
                if (profile == null)
                    continue;

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

            return name.Trim();
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (string.IsNullOrEmpty(_exename))
                return mapping;

            if (Program.Controllers.Count(c => c.Config != null && c.Config.DeviceName != "Keyboard") == 0)
                return mapping;

            if (mapping == null)
                mapping = new PadToKeyboard.PadToKey();

            var app = new PadToKeyApp();
            app.Name = _exename;

            PadToKeyInput mouseInput = new PadToKeyInput();
            mouseInput.Name = InputKey.hotkey | InputKey.start;
            mouseInput.Type = PadToKeyType.Keyboard;
            mouseInput.Key = "(%{KILL})";
            app.Input.Add(mouseInput);
            mapping.Applications.Add(app);

            return mapping;
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

        private void killIDZ()
        {
            if (_gameProfile == null || _gameProfile.EmulationProfile != "SegaToolsIDZ")
                return;

            var currentId = Process.GetCurrentProcess().Id;
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

        private int GetParentProcess(int Id)
        {
            int parentPid = 0;
            using (ManagementObject mo = new ManagementObject("win32_process.handle='" + Id.ToString() + "'"))
            {
                mo.Get();
                parentPid = Convert.ToInt32(mo["ParentProcessId"]);
            }
            return parentPid;
        }

        private static string FindExecutable(string path, string profileName)
        {
            if (!executables.ContainsKey(profileName))
                return null;

            var root = RelativeDirectory(executables[profileName]);
            if (File.Exists(Path.Combine(path, root)))
                return Path.Combine(path, root);

            return null;
        }

        private static string FindBestExecutable(string path, string executableName, bool childs = true)
        {
            foreach (var file in Directory.GetFiles(path, "*.exe"))
            {
                if (file.IndexOf("AmAut", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    continue;

                if (file.IndexOf("GetHwInfo", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    continue;

                if (file.IndexOf("Game Loader", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    continue;

                if (executableName == null || executableName.Equals(Path.GetFileName(file), StringComparison.InvariantCultureIgnoreCase))
                    return file;
            }

            if (childs)
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var ret = FindBestExecutable(dir, executableName, true);
                    if (ret != null)
                        return ret;
                }
            }

            return null;
        }

        public override void RunAndWait(ProcessStartInfo path)
        {
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

                return;
            }

            base.RunAndWait(path);

            KillProcessTree("TeknoParrotUI");

            if (Process.GetProcessesByName("OpenParrotLoader").Length > 0 || Process.GetProcessesByName("OpenParrotLoader64").Length > 0)
            {
                Thread.Sleep(1000);

                KillProcessTree("OpenParrotLoader");
                KillProcessTree("OpenParrotLoader64");
            }

            KillProcessTree("BudgieLoader");
            KillProcessTree("OpenParrotKonamiLoader");
            killIDZ();
        }
        
    }

   
    public static class Exts
    {
        public static bool HasAnyXInputButton(this GameProfile pthi, params InputMapping[] lists)
        {
            return pthi.JoystickButtons.Any(j => !j.HideWithXInput && lists.Contains(j.InputMapping));
        }
    }
}
