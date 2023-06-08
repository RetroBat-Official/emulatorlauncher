using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Windows.Forms;


namespace emulatorLauncher
{
    class ZincGenerator : Generator
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

            string romName = Path.GetFileNameWithoutExtension(rom);
            string romNumber = zincGameNumber[romName];

            if (romNumber != null)
                commandArray.Add(romNumber);
            else
                return null;

            SetupConfiguration(path, rom);
            SetupRendererCfg(path, rom, resolution);

            string cfgFile = "--use-config-file=" + "\"" + Path.Combine(path, "zinc.cfg") + "\"";
            commandArray.Add(cfgFile);

            string rendererFile = "--use-renderer-cfg-file=" + "\"" + Path.Combine(path, "renderer.cfg") + "\"";
            commandArray.Add(rendererFile);

            string renderer = "ogl";
            if (SystemConfig.isOptSet("zinc_renderer") && !string.IsNullOrEmpty(SystemConfig["zinc_renderer"]))
                renderer = SystemConfig["zinc_renderer"];
            commandArray.Add("--renderer=" + renderer + "_renderer.znc");

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

        private void SetupRendererCfg(string path, string rom, ScreenResolution resolution = null)
        {
            // renderer.cfg file
            string cfg = Path.Combine(path, "renderer.cfg");

            var cfgFile = ConfigFile.FromFile(cfg, new ConfigFileOptions() { UseSpaces = true, KeepComments = true });
            cfgFile["XSize"] = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString();
            cfgFile["YSize"] = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString();
            cfgFile["FullScreen"] = "1";
            
            BindFeature(cfgFile, "ColorDepth", "zinc_colordepth", "32");
            BindFeature(cfgFile, "ScanLines", "zinc_scanlines", "0");
            BindFeature(cfgFile, "ShowFPS", "zinc_fps", "0");

            cfgFile.Save(cfg, false);
        }

        static Dictionary<string, string> zincGameNumber = new Dictionary<string, string>()
        {
            { "starglad",   "1" },
            { "sfex",       "2" },
            { "sfexj",      "3" },
            { "sfexa",      "4" },
            { "sfexp",      "5" },
            { "sfexpu1",    "6" },
            { "sfexpj",     "7" },
            { "sfex2",      "8" },
            { "sfex2j",     "9" },
            { "sfex2p",     "10" },
            { "sfex2pj",    "11" },
            { "sfex2pa",    "12" },
            { "plsmaswd",   "13" },
            { "stargld2",   "14" },
            { "rvschola",   "15" },
            { "jgakuen",    "16" },
            { "rvschool",   "17" },
            { "shiryu2",    "18" },
            { "strider2",   "19" },
            { "kikaioh",    "20" },
            { "techromn",   "21" },
            { "ts2",        "22" },
            { "ts2j",       "23" },
            { "tgmj",       "24" },
            { "sncwgltd",   "25" },
            { "beastrzb",   "26" },
            { "beastrzr",   "27" },
            { "bldyror2",   "28" },
            { "brvblade",   "29" },
            { "psyforcj",   "30" },
            { "psyforce",   "31" },
            { "psyfrcex",   "32" },
            { "mgcldtex",   "33" },
            { "raystorj",   "34" },
            { "raystorm",   "35" },
            { "ftimpcta",   "36" },
            { "gdarius",    "37" },
            { "gdarius2",   "38" },
            { "danceyes",   "39" },
            { "xevi3dg",    "40" },
            { "starswep",   "41" },
            { "myangel3",   "42" },
            { "tekkenb",    "43" },
            { "tekkena",    "44" },
            { "tekken",     "45" },
            { "tekken2a",   "46" },
            { "tekken2b",   "47" },
            { "tekken2",    "48" },
            { "souledga",   "49" },
            { "souledgb",   "50" },
            { "souledge",   "51" },
            { "dunkmnia",   "52" },
            { "dunkmnic",   "53" },
            { "primglex",   "54" },
            { "weddingr",   "55" },
            { "hyperath",   "56" },
            { "pbball96",   "57" },
            { "susume",     "58" },
            { "fgtlayer",   "59" },
            { "ehrgeiz",    "60" },
            { "tekken3",    "61" },
            { "mrdrillr",   "62" },
            { "aquarush",   "63" },
            { "pacapp",     "64" },
            { "glpracr3",   "65" },
            { "shngmtkb",   "66" },
            { "cbaj",       "67" },
            { "doapp",      "68" },
            { "tondemo",    "69" },
            { "mfjump",     "70" },
            { "hvnsgate",   "71" }
        };
    }

}
