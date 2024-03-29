using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class FbneoGenerator : Generator
    {
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

                game = ymlFile.Elements.Where(g => g.Name == _romName).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ymlFile.Elements.Where(g => _romName.StartsWith(g.Name)).FirstOrDefault() as YmlContainer;

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
                        var button = buttonEntry as YmlElement;
                        if (button != null)
                        {
                            buttonMap.Add(button.Name, button.Value);
                        }
                    }

                    if (buttonMap.Count == 0)
                        return;

                    gameMapping.Add(gameName, buttonMap);
                }
            }

            if (gameMapping == null)
                return;

            // Define number of players
            int players = 2;

            if (gameMapping.Values.FirstOrDefault().ContainsKey("players"))
            {
                if (gameMapping.Values.FirstOrDefault()["players"].ToInteger() > 0)
                    players = gameMapping.Values.FirstOrDefault()["players"].ToInteger();
            }

            var cfg = FbneoConfigFile.FromFile(cfgFile);

            if (!Controllers.Any(c => !c.IsKeyboard))
            {
                var controller = Controllers.FirstOrDefault(c => c.IsKeyboard);
                if (controller != null)
                    ConfigureKeyboard(controller, cfg, cfgFile);
            }

            else
            {
                foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(players))
                    ConfigureJoystick(controller, cfg, system, gameMapping);

                cfg.Save();
            }
        }

        private void ConfigureJoystick(Controller controller, FbneoConfigFile cfg, string system, Dictionary<string, Dictionary<string,string>> gameMapping)
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
            string guid1 = (controller.Guid.ToString()).Substring(0, 27) + "00000";
            SdlToDirectInput dinputCtrl = null;

            SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

            try { dinputCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1); }
            catch { }

            if (dinputCtrl == null)
                return;

            // Define index
            int index = controller.dinputCtrl != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex;

            string joy = "0x4" + index.ToString();

            cfg["version"] = "0x100003";
            cfg["analog"] = "0x0100";
            cfg["cpu"] = "0x0100";

            foreach (var button in gameMapping.Values.FirstOrDefault())
            {
                if (p1strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 1)
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, button.Value, joy, index);
                }

                else if (p2strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 2)
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, button.Value, joy, index);
                }

                else if (p3strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 3)
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, button.Value, joy, index);
                }

                else if (p4strings.Contains(button.Key))
                {
                    if (controller.PlayerIndex == 4)
                        cfg["input  " + "\"" + button.Key + "\""] = GetDinputMapping(dinputCtrl, button.Value, joy, index);
                }

                else if (button.Key == "players")
                    continue;

                else
                    cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key + "\""] = GetDinputMapping(dinputCtrl, button.Value, joy, index);
            }
        }

        private static void ConfigureKeyboard(Controller controller, FbneoConfigFile cfg, string cfgFile)
        {
            if (controller == null)
                return;

            InputConfig keyboard = controller.Config;
            if (keyboard == null)
                return;

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

        private static List<string> p1strings = new List<string>() 
        { "Coin 1", "Diagnostic", "Debug Dip 1", "Debug Dip 2", "Dip 1", "Dip 2", "Dip A", "Dip B", "Dip C", "Fake Dip", "Region", "Reset", "Service", "Service 1", "Service Mode", "Start 1", "System", "Slots", "Test", "Tilt" };

        private static List<string> p2strings = new List<string>()
        { "Coin 2", "Service 2", "Start 2" };

        private static List<string> p3strings = new List<string>()
        { "Coin 3", "Service 3", "Start 3" };

        private static List<string> p4strings = new List<string>()
        { "Coin 4", "Service 4", "Start 4" };
    }
}