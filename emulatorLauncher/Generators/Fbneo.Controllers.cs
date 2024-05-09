using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class FbneoGenerator : Generator
    {
        private bool _noPlayer = false;
        private int _nbGamepads;
        private int _players = 2;

        private void CreateControllerConfiguration(string path, string rom, string system)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string padConfigFolder = Path.Combine(path, "config", "games");
            if (!Directory.Exists(padConfigFolder)) try { Directory.CreateDirectory(padConfigFolder); }
                catch { }

            string cfgFile = Path.Combine(padConfigFolder, Path.GetFileNameWithoutExtension(rom) + ".ini");

            // Get game mapping yml database
            YmlContainer game = null;
            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            string fbneoMapping = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "fbneo.yml");

            if (File.Exists(fbneoMapping))
            {
                YmlFile ymlFile = YmlFile.Load(fbneoMapping);

                if (decocassGames.Contains(_romName))
                    game = ymlFile.Elements.Where(g => g.Name == "decocass").FirstOrDefault() as YmlContainer;
                else if(decomlcGames.Contains(_romName))
                    game = ymlFile.Elements.Where(g => g.Name == "decomlc").FirstOrDefault() as YmlContainer;
                else
                    game = ymlFile.Elements.Where(g => g.Name == _romName).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => _romName.StartsWith(g.Name) && g.Name != "").FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => g.Name == "default_" + system).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;

                if (game != null)
                {
                    var gameName = game.Name;
                    var buttonMap = new Dictionary<string, string>();

                    foreach (var buttonEntry in game.Elements)
                    {
                        YmlElement button = buttonEntry as YmlElement;
                        if (button != null)
                            buttonMap.Add(button.Name, button.Value);
                    }

                    if (buttonMap.Count == 0)
                        return;

                    gameMapping.Add(gameName, buttonMap);
                }
                else
                    SimpleLogger.Instance.Info("[INFO] Game not found in mapping file : " + _romName);
            }

            if (gameMapping == null || gameMapping.Count == 0)
            {
                SimpleLogger.Instance.Info("[INFO] Game not found in fbneo.yml mapping file : " + _romName + " . No automatic mapping can be performed.");
                return;
            }

            // Define number of players
            if (gameMapping.Values.FirstOrDefault().ContainsKey("players"))
            {
                if (gameMapping.Values.FirstOrDefault()["players"].ToInteger() > 0)
                    _players = gameMapping.Values.FirstOrDefault()["players"].ToInteger();
            }

            if (gameMapping.Values.FirstOrDefault().ContainsKey("noplayer"))
            {
                if (gameMapping.Values.FirstOrDefault()["noplayer"].ToInteger() == 1)
                    _noPlayer = true;
            }

            if (noPlayerRom.Contains(_romName))
                _noPlayer = true;

            var cfg = FbneoConfigFile.FromFile(cfgFile);

            if (!Controllers.Any(c => !c.IsKeyboard))
            {
                ConfigureKeyboard(cfgFile);
                return;
            }

            else
            {
                _nbGamepads = this.Controllers.Where(c => !c.IsKeyboard).Count();
                foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(_players))
                    ConfigureJoystick(controller, cfg, gameMapping);

                cfg.Save();
            }
        }

        private void ConfigureJoystick(Controller controller, FbneoConfigFile cfg, Dictionary<string, Dictionary<string,string>> gameMapping)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            // Fbneo uses dinput plugin
            if (controller.DirectInput == null)
                return;

            // Get gamecontrollerdb buttonmapping for the controller
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid1 = (controller.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput dinputCtrl = null;

            SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

            try { dinputCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1); }
            catch { }

            if (dinputCtrl == null)
                return;

            // Define index
            int dinputIndex = controller.DirectInput != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex;
            int index = dinputIndex;

            if (SystemConfig.isOptSet("fbneo_dinput_index") && SystemConfig.getOptBoolean("fbneo_dinput_index") && dinputIndex > 0)
                index = dinputIndex - 1;
            
            SimpleLogger.Instance.Info("[INFO] index for player " + controller.PlayerIndex + ": " + index);

            string joy = "0x4" + index.ToString();

            cfg["version"] = "0x100003";
            cfg["analog"] = "0x0100";
            cfg["cpu"] = "0x0100";

            foreach (var button in gameMapping.Values.FirstOrDefault())
            {
                string updatedValue = null;

                if (button.Value.Contains("_or_"))
                {
                    string[] buttons = button.Value.Split(new string[] { "_or_" }, System.StringSplitOptions.None);
                    if (controller.PlayerIndex == 1)
                        updatedValue = buttons[0];
                    else
                        updatedValue = buttons[1];
                }
                else if (button.Value.StartsWith("noplayer_"))
                {
                    updatedValue = button.Value.Substring(9);
                }
                
                // Specific cases
                if (button.Key == "players")
                    continue;
                if (button.Key == "noplayer")
                    continue;

                if (_romName == "kenseim")
                {
                    if (button.Key == "Coin")
                    {
                        if (controller.PlayerIndex == 1)
                            cfg["input  " + "\"Coin\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    }
                    if (button.Key == "Ryu Start")
                    {
                        if (controller.PlayerIndex == 1)
                            cfg["input  " + "\"Ryu Start\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    }
                    else if (button.Key == "Chun-Li Start")
                    {
                        if (controller.PlayerIndex == 2)
                            cfg["input  " + "\"Chun-Li Start\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    }
                    else if (button.Key == "Service")
                    {
                        if (controller.PlayerIndex == 1)
                            cfg["input  " + "\"Service\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    }
                    else if (controller.PlayerIndex == 1)
                        cfg["input  " + "\"Mole A" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    else
                        cfg["input  " + "\"Mole B" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    continue;
                }

                if (_romName.StartsWith("sidepckt"))
                {
                    if (controller.PlayerIndex == 1)
                    {
                        if (button.Key == "Coin" || button.Key == "Start")
                            cfg["input  " + "\"" + button.Key + " 1\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        else if (button.Key == "Service")
                            cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        else if (button.Key.EndsWith("(Cocktail)"))
                            cfg["input  " + "\"" + button.Key.Replace("_(Cocktail)", "") + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    }

                    else if (controller.PlayerIndex == 2)
                    {
                        if (button.Key == "Coin" || button.Key == "Start")
                            cfg["input  " + "\"" + button.Key + " 2\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        else
                            cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    }
                    continue;
                }

                if (button.Value.StartsWith("noplayer_"))
                {
                    if (controller.PlayerIndex == 1)
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    continue;
                }

                if (p1strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 1)
                    {
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        continue;
                    }
                }

                else if (p2strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 2)
                    {
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        continue;
                    }
                }

                else if (p3strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 3)
                    {
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        continue;
                    }
                }

                else if (p4strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 4)
                    {
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        continue;
                    }
                }

                if (gunValues.Contains(button.Value))
                {
                    if (controller.PlayerIndex == 1 && _noPlayer)
                        cfg["input  " + "\"" + button.Key + "\""] = updatedValue ?? button.Value;
                    else if (controller.PlayerIndex == 1)
                        cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key + "\""] = updatedValue ?? button.Value;
                    else
                        continue;

                    continue;
                }

                if (button.Key.EndsWith("(Cocktail)") && controller.PlayerIndex == 1)
                {
                    if (_romName.StartsWith("birdtry") && button.Key == "Fire 1 (Cocktail)")
                        cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key.Replace("(Cocktail)", "(Hit)") + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    else if (_romName.StartsWith("birdtry") && button.Key == "Fire 2 (Cocktail)")
                        cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key.Replace("(Cocktail)", "(Select)") + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    else if (!_noPlayer)
                        cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key.Replace(" (Cocktail)", "") + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    else
                    {
                        cfg["input  " + "\"" + button.Key.Replace(" (Cocktail)", "") + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                        cfg["input  " + "\"" + button.Key + "\""] = "constant 0x00";
                    }
                    continue;
                }

                else if (button.Key.EndsWith("(Cocktail)") && controller.PlayerIndex == 2 && _noPlayer)
                {
                    cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    continue;
                }

                if (_noPlayer)
                {
                    cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
                    continue;
                }                

                cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key + "\""] = GetDinputMapping(dinputCtrl, updatedValue ?? button.Value, joy, index);
            }
        }

        private static void ConfigureKeyboard(string cfgFile)
        {
            string backupFile = cfgFile + ".bak";

            if (File.Exists(backupFile))
                try { File.Delete(backupFile); }
                catch { }

            if (File.Exists(cfgFile))
            {
                try
                {
                    File.Copy(cfgFile, backupFile);
                    File.Delete(cfgFile);
                }
                catch { }
            }
        }

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey, string joy, int index)
        {
            int direction = 1;

            if (c == null)
                return "undefined";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "undefined";
            }

            if (buttonkey.Contains("_"))
            {
                string[] buttonDirection = buttonkey.Split('_');
                buttonkey = buttonDirection[0];

                if (buttonDirection[1] == "up" || buttonDirection[1] == "left")
                    direction = -1;
                else if (buttonDirection[1] == "down" || buttonDirection[1] == "right")
                    direction = 1;
                else if (buttonDirection[1] == "axis")
                    direction = 0;
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "undefined";
            }

            if (buttonkey.StartsWith("-"))
            {
                buttonkey = buttonkey.Substring(1);
                direction = -1;
            }
            else if (buttonkey.StartsWith("+"))
            {
                buttonkey = buttonkey.Substring(1);
            }

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());

                switch (buttonID)
                {
                    case 10:
                        return "switch " + joy + "8A";
                    case 11:
                        return "switch " + joy + "8B";
                    case 12:
                        return "switch " + joy + "8C";
                    case 13:
                        return "switch " + joy + "8D";
                    case 14:
                        return "switch " + joy + "8E";
                    case 15:
                        return "switch " + joy + "8F";
                    default:
                        return "switch " + joy + "8" + buttonID;
                }
                
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "switch " + joy + "12";
                    case 2:
                        return "switch " + joy + "11";
                    case 4:
                        return "switch " + joy + "13";
                    case 8:
                        return "switch " + joy + "10";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                }

                else if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                }

                else if (button.StartsWith("a"))
                {
                    axisID = button.Substring(1).ToInteger();
                }

                if (direction == 0)
                    return "joyaxis " + index.ToString() + " " + axisID.ToString();

                else
                {
                    switch (axisID)
                    {
                        case 0:
                            return "switch " + joy + "0" + (direction == 1 ? "1" : "0");
                        case 1:
                            return "switch " + joy + "0" + (direction == 1 ? "3" : "2");
                        case 2:
                            return "switch " + joy + "0" + (direction == 1 ? "5" : "4");
                        case 3:
                            return "switch " + joy + "0" + (direction == 1 ? "7" : "6");
                        case 4:
                            return "switch " + joy + "0" + (direction == 1 ? "9" : "8");
                        case 5:
                            return "switch " + joy + "0" + (direction == 1 ? "B" : "A");
                    }
                }
            }

            return "undefined";
        }

        private readonly static List<string> p1strings = new List<string>() 
        { "Coin 1", "Diagnostic", "Debug Dip 1", "Debug Dip 2", "Dip 1", "Dip 2", "Dip A", "Dip B", "Dip C", "Fake Dip", "Left Switch", "Pay Switch", "Region", "Reset", "Right Switch",
            "S3 Test (Jamma)", "S3 Test", "Service", "Service 1", "Service Mode", "Show Switch", "Start 1", "Start 1 / Fire 1", "Start 1 / Fire 2", "Start 1/P1 Fire 1", "Start 2/P1 Fire 2", "System", "Slots", "Test", "Tilt" };

        private readonly static List<string> p2strings = new List<string>()
        { "Coin 2", "Service 2", "Start 2", "Start 2 / Fire 2" };

        private readonly static List<string> p3strings = new List<string>()
        { "Coin 3", "Service 3", "Start 3" };

        private readonly static List<string> p4strings = new List<string>()
        { "Coin 4", "Service 4", "Start 4" };

        private readonly static List<string> noPlayerRom = new List<string>()
        { "4in1", "800fath", "800fatha", "ad2083", "amidar", "amidar1", "amidaru", "amidaro", "amidarb", "amigo", "amidars", "anteater", "anteaterg", "anteateruk", "aracnis", "armorcar", "armorcar2", "asideral", "astrians", "atlantis", "atlantis2", "atlantisb", "azurian", 
            "bagmanmc", "bagmanm2", "batman2", "billiard", "blkhole", "bomber", "bongo", "catacomb", "cavelon", "checkman", "checkmanj", "chewing", "ckongg", "ckongmc", "ckongs", "conquer", "crusherm", 
            "dambustr", "dambustra", "dambustruk", "darkplnt", "devilfsh", "devilfshg", "devilfshgb", "dingo", "dingoe", "dkongjrm", "donight", "drivfrcg", "drivfrct", "drivfrcb", 
            "eagle", "eagle2", "eagle3", "exodus", "explorer", "fantastc", "fantazia", "frogf", "frogg", "frogger", "froggereb", "froggermc", "froggers1", "froggers2", "froggers3", "froggert", "froggers", "froggrs", 
            "galactica2", "galaktron", "galap1", "galap2", "galap4", "galapx", "galaxbsf", "galaxbsf2", "galaxcirsa", "galaxian", "galaxianbl", "galaxianbl2", "galaxianbl3", "galaxianem", "galaxianiii", "galaxrcgg", 
            "galaxianrp", "galaxrf", "galaxrfgg", "galaxyx", "galemp", "galkamika", "galturbo", "gmgalax", "gteikoku", "gteikokub", "gteikokub2", "gteikokub3",
            "hexpool", "hexpoola", "hncholms", "hotshock", "hotshockb", "hunchbkg", "hunchbks", "hustler", "hustlerb", "jumpbug", "jumpbugb", 
            "kamakazi3", "kamikazesp", "kingball", "kingballj", "knockout", "kong", "korokoro", "ladybugg", "losttomb", "losttombh", "luctoday",
            "mariner", "mimonkey", "mimonsco", "mimonscr", "mimonscra", "mltiwars", "moonal2", "moonal2b", "moonaln", "mooncmw", "mooncrecm", "mooncreg", "mooncreg2", "mooncrgx", 
            "mooncrsl", "mooncrs2", "mooncrs3", "mooncrs4", "mooncrs5", "mooncrsb", "mooncrst", "mooncrstg", "mooncrsto", "mooncrstu", "mooncrstuk", "mooncrstuku", "mooncrstuu", "mouncrst", "moonwar", "moonwara", "moonqsr",
            "mrkougar", "mrkougar2", "mrkougb", "mrkougb2", "mshuttle", "mshuttle2", "mshuttlea", "mshuttlej", "mshuttlej2",
            "namenayo", "newsin7", "offensiv", "olibug", "omegab", "omni", "orbitron", "pacmanbl", "pacmanbla", "pacmanblc", "pacmanblv", "pajaroes", "phoenxp2", "pisces", "piscesb", "porter", "racknrol", "redufo", "redufob", "redufob2", "redufob3", 
            "scorpion", "scorpiona", "scorpionb", "scorpionmc", "scramb2", "scramblb", "scramble", "scramblebb", "scramblebf", "scrambler", "scrambles", "scrambp", "scramce", "scrampt", "scramrf", "skybase", "skyraidr", 
            "spacbat2", "spacbatt", "spacempr", "spcmission", "spctbird", "spctrek", "sstarcrs", "starfght", "starfgmc", "strfbomb", "superg", "supergx", "supershp", "swarm", 
            "tjumpman", "triplep", "triplepa", "tst_galx", "uniwars", "uniwarsa", "vpool", "vueloesp", "zerotime", "zerotimed", "zerotimemc", "zerotimeu" };

        private readonly static List<string> gunValues = new List<string>()
        { "mouseaxis 0", "mouseaxis 1" };

        private readonly static List<string> decocassGames = new List<string>()
        {
            "chwy", "cmanhat", "cterrani", "castfant", "cnebula", "csuperas", "cocean1a", "cocean6b", "clocknch", "clocknchj", "cfboy0a1", "cprogolf", "cprogolfj", "cprogolf18", "cluckypo", "ctisland", "ctisland2",
            "ctisland3", "cexplore", "cdiscon1", "csweetht", "ctornado", "cmissnx", "cptennis", "cptennisj", "cadanglr", "cfishing", "cbtime", "chamburger", "cburnrub", "cburnrub2", "cbnj", "cgraplop", "cgraplop2",
            "clapapa", "clapapa2", "cskater", "cprobowl", "cnightst", "cnightst2", "cpsoccer", "cpsoccerj", "csdtenis", "czeroize", "cppicf", "cppicf2", "cfghtice", "cscrtry", "cscrtry2", "coozumou", "cbdash",
            "cflyball", "decomult"
        };

        private readonly static List<string> decomlcGames = new List<string>()
        {
            "avengrgs", "avengrgsj", "avengrgsbh", "skullfng", "skullfngj", "skullfnga", "stadhr96", "stadhr96u", "stadhr96j", "stadhr96j2", "stadhr96k", "hoops96", "hoops95", "ddream95"
        };
    }
}