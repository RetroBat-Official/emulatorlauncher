using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class ZincGenerator : Generator
    {
        private ScreenResolution _resolution;

        public ZincGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("zinc");

            string exe = Path.Combine(path, "ZiNc.exe");
            if (!File.Exists(exe))
                return null;

            _resolution = resolution;

            List<string> commandArray = new List<string>();

            // Get Game number for command line
            string romName = Path.GetFileNameWithoutExtension(rom);
            int romNumber = 0;
            
            if (zincGameNumber.ContainsKey(romName))
                romNumber = zincGameNumber[romName];

            if (romNumber != 0)
                commandArray.Add(romNumber.ToString());
            else
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfiguration(path, rom);
            SetupRendererCfg(path, rom, resolution, fullscreen);

            // Add config path command line
            string cfgFile = "--use-config-file=" + "\"" + Path.Combine(path, "zinc.cfg") + "\"";
            commandArray.Add(cfgFile);

            // Add controller config path command line
            string controllerCfgFile = Path.Combine(path, "cfg", zincControllerCfgFiles[romNumber]);
            string oldControllerCfgFile = Path.Combine(path, "controller.cfg");

            if (zincControllerCfgFiles.ContainsKey(romNumber) && File.Exists(controllerCfgFile) && SystemConfig["zinc_controller_config"] != "none")
            {
                if (SystemConfig.isOptSet("zinc_controller_config") && SystemConfig["zinc_controller_config"] == "autoconfig")
                {
                    ConfigureControllers(oldControllerCfgFile, path);
                    string controllerCfgCommand = "--use-controller-cfg-file=" + "\"" + oldControllerCfgFile + "\"";
                    commandArray.Add("--controller=.\\controller.znc");
                    commandArray.Add(controllerCfgCommand);
                }
                else
                {
                    controllerCfgFile = Path.Combine(path, "cfg", zincControllerCfgFiles[romNumber]);
                    string controllerCfgCommand = "--use-controller-cfg-file=" + "\"" + controllerCfgFile + "\"";
                    string outputFile = Path.Combine(path, "wberror.txt");
                    using (var ini = IniFile.FromFile(controllerCfgFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
                    {
                        ini.WriteValue("General", "output", outputFile);
                        ini.WriteValue("General", "NOERROR", "1");
                        ini.Save();
                    }

                    if (File.Exists(oldControllerCfgFile))
                        File.Delete(oldControllerCfgFile);
                    if (File.Exists(controllerCfgFile))
                        File.Copy(controllerCfgFile, oldControllerCfgFile);

                    commandArray.Add("--controller=.\\controller.znc");
                    commandArray.Add(controllerCfgCommand);
                }
            }

            // Renderer choice
            string rendererFile = "--use-renderer-cfg-file=" + "\"" + Path.Combine(path, "renderer.cfg") + "\"";
            commandArray.Add(rendererFile);

            string renderer = "ogl";
            if (SystemConfig.isOptSet("zinc_renderer") && !string.IsNullOrEmpty(SystemConfig["zinc_renderer"]))
                renderer = SystemConfig["zinc_renderer"];
            commandArray.Add("--renderer=.\\" + renderer + "_renderer.znc");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfiguration(string path, string rom)
        {
            // Zinc.cfg file
            string cfg = Path.Combine(path, "zinc.cfg");

            string romPath = Path.GetDirectoryName(rom);

            var cfgFile = ConfigFile.FromFile(cfg, new ConfigFileOptions() { UseSpaces = false });
            cfgFile["--roms-directory"] = romPath;
            
            cfgFile.Save(cfg, false);
        }

        private void SetupRendererCfg(string path, string rom, ScreenResolution resolution = null, bool fullscreen = true)
        {
            // renderer.cfg file
            string cfg = Path.Combine(path, "renderer.cfg");

            var cfgFile = ConfigFile.FromFile(cfg, new ConfigFileOptions() { UseSpaces = true, KeepComments = true });
            cfgFile["XSize"] = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString();
            cfgFile["YSize"] = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString();
            cfgFile["FullScreen"] = fullscreen ? "1" : "0";
            
            BindFeature(cfgFile, "ColorDepth", "zinc_colordepth", "32");
            BindFeature(cfgFile, "ScanLines", "zinc_scanlines", "0");
            BindFeature(cfgFile, "ShowFPS", "zinc_fps", "0");

            cfgFile.Save(cfg, false);
        }

        static Dictionary<string, int> zincGameNumber = new Dictionary<string, int>()
        {
            { "starglad",   1 },
            { "sfex",       2 },
            { "sfexj",      3 },
            { "sfexa",      4 },
            { "sfexp",      5 },
            { "sfexpu1",    6 },
            { "sfexpj",     7 },
            { "sfex2",      8 },
            { "sfex2j",     9 },
            { "sfex2p",     10 },
            { "sfex2pj",    11 },
            { "sfex2pa",    12 },
            { "plsmaswd",   13 },
            { "stargld2",   14 },
            { "rvschola",   15 },
            { "jgakuen",    16 },
            { "rvschool",   17 },
            { "shiryu2",    18 },
            { "strider2",   19 },
            { "kikaioh",    20 },
            { "techromn",   21 },
            { "ts2",        22 },
            { "ts2j",       23 },
            { "tgmj",       24 },
            { "sncwgltd",   25 },
            { "beastrzb",   26 },
            { "beastrzr",   27 },
            { "bldyror2",   28 },
            { "brvblade",   29 },
            { "psyforcj",   30 },
            { "psyforce",   31 },
            { "psyfrcex",   32 },
            { "mgcldtex",   33 },
            { "raystorj",   34 },
            { "raystorm",   35 },
            { "ftimpcta",   36 },
            { "gdarius",    37 },
            { "gdarius2",   38 },
            { "danceyes",   39 },
            { "xevi3dg",    40 },
            { "starswep",   41 },
            { "myangel3",   42 },
            { "tekkenb",    43 },
            { "tekkena",    44 },
            { "tekken",     45 },
            { "tekken2a",   46 },
            { "tekken2b",   47 },
            { "tekken2",    48 },
            { "souledga",   49 },
            { "souledgb",   50 },
            { "souledge",   51 },
            { "dunkmnia",   52 },
            { "dunkmnic",   53 },
            { "primglex",   54 },
            { "weddingr",   55 },
            { "hyperath",   56 },
            { "pbball96",   57 },
            { "susume",     58 },
            { "fgtlayer",   59 },
            { "ehrgeiz",    60 },
            { "tekken3",    61 },
            { "mrdrillr",   62 },
            { "aquarush",   63 },
            { "pacapp",     64 },
            { "glpracr3",   65 },
            { "shngmtkb",   66 },
            { "cbaj",       67 },
            { "doapp",      68 },
            { "tondemo",    69 },
            { "mfjump",     70 },
            { "hvnsgate",   71 }
        };

        static Dictionary<int, string> zincControllerCfgFiles = new Dictionary<int, string>()
        {
            { 1,  "1 Star Gladiator (US 960627)_controller.cfg" },
            { 2,  "2 Street Fighter EX (US 961219)_controller.cfg" },
            { 3,  "3 Street Fighter EX (JP 961130)_controller.cfg" },
            { 4,  "4 Street Fighter EX (ASIA 961219)_controller.cfg" },
            { 5,  "5 Street Fighter EX Plus (US 970407)_controller.cfg" },
            { 6,  "6 Street Fighter EX Plus (US 970311)_controller.cfg" },
            { 7,  "7 Street Fighter EX Plus (JP 970311)_controller.cfg" },
            { 8,  "8 Street Fighter EX 2 (US 980526)_controller.cfg" },
            { 9,  "9 Street Fighter EX 2 (JP 980312)_controller.cfg" },
            { 10, "10 Street Fighter EX 2 PLUS (US 990611)_controller.cfg" },
            { 11, "11 Street Fighter EX 2 PLUS (JP 990611)_controller.cfg" },
            { 12, "12 Street Fighter EX 2 PLUS (ASIA 990611)_controller.cfg" },
            { 13, "13 Plasma Sword (US 980316)_controller.cfg" },
            { 14, "14 Star Gladiator 2 (JP 980316)_controller.cfg" },
            { 15, "15 Rival Schools (ASIA 971117)_controller.cfg" },
            { 16, "16 Justice Gakuen (JP 991117)_controller.cfg" },
            { 17, "17 Rival Schools (US 971117)_controller.cfg" },
            { 18, "18 Strider Hiryu 2 (JP 991213)_controller.cfg" },
            { 19, "19 Strider 2 (ASIA 991213)_controller.cfg" },
            { 20, "20 Kikaioh (JP 980914)_controller.cfg" },
            { 21, "21 Tech Romancer (US 980914)_controller.cfg" },
            { 22, "22 Battle Arena Toshinden 2 (US 951124)_controller.cfg" },
            { 23, "23 Battle Arena Toshinden 2 (JP 951124)_controller.cfg" },
            { 24, "24 Tetris The Grand Master (JP 980710)_controller.cfg" },
            { 25, "25 Sonic Wings Limited (JP)_controller.cfg" },
            { 26, "26 Beastorizer (US) bootleg_controller.cfg" },
            { 27, "27 Beastorizer (US)_controller.cfg" },
            { 28, "28 Bloody Roar 2 (JP)_controller.cfg" },
            { 29, "29 Brave Blade (JP)_controller.cfg" },
            { 30, "30 Psychic Force (JP 2.4J)_controller.cfg" },
            { 31, "31 Psychic Force (World 2.4O)_controller.cfg" },
            { 32, "32 Psychic Force EX (JP 2.0J)_controller.cfg" },
            { 33, "33 Magical Date EX - sotsugyou kokuhaku daisakusen (JP 2.01J)_controller.cfg" },
            { 34, "34 Raystorm (JP 2.05J)_controller.cfg" },
            { 35, "35 Raystorm (US 2.06A)_controller.cfg" },
            { 36, "36 Fighters Impact A (JP 2.00J)_controller.cfg" },
            { 37, "37 G-Darius (JP 2.01J)_controller.cfg" },
            { 38, "38 G-Darius Ver.2 (JP 2.03J)_controller.cfg" },
            { 39, "39 Dancing Eyes (JP) Ver. A_controller.cfg" },
            { 40, "40 Xevious 3DG (JP) Ver. A_controller.cfg" },
            { 41, "41 Star Sweep (JP) Ver. A_controller.cfg" },
            { 42, "42 Kosodate Quiz My Angel 3 (JP) Ver. A_controller.cfg" },
            { 43, "43 Tekken (JP) Ver. B_controller.cfg" },
            { 44, "44 Tekken (WORLD) Ver. B_controller.cfg" },
            { 45, "45 Tekken (WORLD) Ver. C_controller.cfg" },
            { 46, "46 Tekken 2 (JP) Ver. B_controller.cfg" },
            { 47, "47 Tekken 2 (World) Ver. A_controller.cfg" },
            { 48, "48 Tekken 2 (World) Ver. B_controller.cfg" },
            { 49, "49 Soul Edge (JP) SO3 Ver. A_controller.cfg" },
            { 50, "50 Soul Edge (JP) SO1 Ver. A_controller.cfg" },
            { 51, "51 Soul Edge Ver. II (JP) SO4 Ver. C_controller.cfg" },
            { 52, "52 Dunk Mania (US) DM2 Ver. C_controller.cfg" },
            { 53, "53 Dunk Mania (JP) DM1 Ver. C_controller.cfg" },
            { 54, "54 Prime Goal EX (JP) Ver. A_controller.cfg" },
            { 55, "55 Wedding Rhapsody (JP) Ver. JAA_controller.cfg" },
            { 56, "56 Hyper Athlete (JP) Ver. 1.00_controller.cfg" },
            { 57, "57 Powerful Baseball 96 (JP) Ver. 1.03_controller.cfg" },
            { 58, "58 Susume! Taisen Puzzle-Dama (JP) Ver. 1.20_controller.cfg" },
            { 59, "59 Fighting Layer (JP) Ver. B_controller.cfg" },
            { 60, "60 Ehrgeiz (US) Ver. A_controller.cfg" },
            { 61, "61 Tekken 3 (JP) Ver. A_controller.cfg" },
            { 62, "62 Mr Driller (JP) Ver. A_controller.cfg" },
            { 63, "63 Aqua Rush (JP) Ver. A_controller.cfg" },
            { 64, "64 Paca Paca Passion (JP) Ver. A_controller.cfg" },
            { 65, "65 Gallop Racer 3 (JP)_controller.cfg" },
            { 66, "66 Shanghai Matekibuyuu (JP)_controller.cfg" },
            { 67, "67 Cool Boarders Arcade Jam (US)_controller.cfg" },
            { 68, "68 Dead or Alive++_controller.cfg" },
            { 69, "69 Tondemo Crisis_controller.cfg" },
            { 70, "70 Monster Farm Jump (JP)_controller.cfg" },
            { 71, "71 Heaven's Gate_controller.cfg" }
        };
    }
}
