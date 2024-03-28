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

        private void CreateControllerConfiguration(string path, string rom)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string padConfigFolder = Path.Combine(path, "config", "games");
            if (!Directory.Exists(padConfigFolder)) try { Directory.CreateDirectory(padConfigFolder); }
                catch { }

            string cfgFile = Path.Combine(padConfigFolder, Path.GetFileNameWithoutExtension(rom) + ".ini");

            var cfg = FbneoConfigFile.FromFile(cfgFile);

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(controller, cfg, cfgFile);
        }

        private void ConfigureInput(Controller controller, FbneoConfigFile cfg, string cfgFile)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard && this.Controllers.Count(i => !i.IsKeyboard) == 0)
                ConfigureKeyboard(controller, cfg, cfgFile);
            else
            {
                ConfigureJoystick(controller, cfg);
                cfg.Save();
            }
        }

        private void ConfigureJoystick(Controller controller, FbneoConfigFile cfg)
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

            // Get game mapping yml database
            YmlContainer game = null;
            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            string fbneoMapping = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "fbneo.yml");

            if (File.Exists(fbneoMapping))
            {
                YmlFile ymlFile = YmlFile.Load(fbneoMapping);

                game = ymlFile.Elements.Where(c => c.Name == _romName).FirstOrDefault() as YmlContainer;

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

            // Define index
            int index = controller.dinputCtrl != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex;

            string joy = "0x4" + index.ToString();

            cfg["version"] = "0x100003";
            cfg["analog"] = "0x0100";
            cfg["cpu"] = "0x0100";

            foreach (var button in gameMapping.Values.FirstOrDefault())
            {
                if (testStrings.Contains(button.Key))
                    cfg["input  " + "\"" + button.Key + "\""] = "switch " + joy + GetDinputMapping(dinputCtrl, button.Value);
                else
                    cfg["input  " + "\"P" + controller.PlayerIndex + " " + button.Key + "\""] = "switch " + joy + GetDinputMapping(dinputCtrl, button.Value);
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

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey)
        {
            int direction = 1;

            if (c == null)
                return "00";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "00";
            }

            if (buttonkey.Contains("_"))
            {
                string[] buttonDirection = buttonkey.Split('_');
                buttonkey = buttonDirection[0];

                if (buttonDirection[1] == "up" || buttonDirection[1] == "left")
                    direction = -1;
                else if (buttonDirection[1] == "down" || buttonDirection[1] == "right")
                    direction = 1;
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "00";
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
                        return "8A";
                    case 11:
                        return "8B";
                    case 12:
                        return "8C";
                    case 13:
                        return "8D";
                    case 14:
                        return "8E";
                    case 15:
                        return "8F";
                    default:
                        return "8" + buttonID;
                }
                
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "12";
                    case 2:
                        return "11";
                    case 4:
                        return "13";
                    case 8:
                        return "10";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    direction = -1;
                }

                else if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    direction = 1;
                }

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                switch (axisID)
                {
                    case 0:
                        return "0" + (direction == 1 ? "1" : "0");
                    case 1:
                        return "0" + (direction == 1 ? "3" : "2");
                    case 2:
                        return "0" + (direction == 1 ? "5" : "4");
                    case 3:
                        return "0" + (direction == 1 ? "7" : "6");
                    case 4:
                        return "0" + (direction == 1 ? "9" : "8");
                    case 5:
                        return "0" + (direction == 1 ? "B" : "A");
                }
            }

            return "00";
        }

        private static List<string> testStrings = new List<string>() 
        { "Diagnostic", "Debug Dip 1", "Debug Dip 2", "Dip 1", "Dip 2", "Dip A", "Dip B", "Dip C", "Fake Dip", "Region", "Reset", "Service", "System", "Slots",  "Test" };
    }
}