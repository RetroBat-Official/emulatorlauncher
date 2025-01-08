using System.Linq;
using TeknoParrotUi.Common;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private static bool ConfigureTPGuns(GameProfile userProfile)
        {
            if (!Program.SystemConfig.getOptBoolean("use_gun"))
                return false;

            if (!userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("Gun") || j.ButtonName.Contains("GUN")))
                return false;

            SimpleLogger.Instance.Info("[GUNS] Configuring Gun.");

            // Variables
            int playerNumber = 1;
            YmlContainer game = null;
            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            string tpMappingyml = null;
            var buttonMap = new Dictionary<string, string>();
            string gameName = null;
            string tpGameName = Path.GetFileNameWithoutExtension(userProfile.FileName).ToLowerInvariant();

            // Look for number of players based on string search in userprofile file (Px, Player x)
            if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P6") || j.ButtonName.Contains("Player 6")))
                playerNumber = 6;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P5") || j.ButtonName.Contains("Player 5")))
                playerNumber = 5;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P4") || j.ButtonName.Contains("Player 4")))
                playerNumber = 4;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P3") || j.ButtonName.Contains("Player 3")))
                playerNumber = 3;
            else if (userProfile.JoystickButtons.Any(j => j.ButtonName.Contains("P2") || j.ButtonName.Contains("Player 2")))
                playerNumber = 2;

            // Search mapping in yml file
            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "inputmapping");

                tpMappingyml = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(tpMappingyml))
                    break;
            }

            if (File.Exists(tpMappingyml))
            {
                YmlFile ymlFile = YmlFile.Load(tpMappingyml);
                game = ymlFile.Elements.Where(g => g.Name == tpGameName).FirstOrDefault() as YmlContainer;
                if (game != null)
                {
                    gameName = game.Name;
                    foreach (var buttonEntry in game.Elements)
                    {
                        YmlElement button = buttonEntry as YmlElement;
                        if (button.Name == "Players")
                        {
                            playerNumber = button.Value.ToInteger();
                        }

                        else if (button != null && button.Name != "Players")
                        {
                            buttonMap.Add(button.Name, button.Value);
                        }
                    }

                    gameMapping.Add(gameName, buttonMap);
                    SimpleLogger.Instance.Info("[INFO] Performing mapping based on teknoparrot.yml file");
                }
                else
                {
                    SimpleLogger.Instance.Warning("[GUNS] Game mapping not listed in teknoparrot.yml file.");
                    return false;
                }
            }
            else
            {
                SimpleLogger.Instance.Warning("[GUNS] File teknoparrot.yml does not exist.");
                return false;
            }

            return false;
        }
    }
}
