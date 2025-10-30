using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class DolphinControllers
    {
        private static void GenerateControllerConfig_wiilightgun(string path, bool sindenSoft)
        {
            string iniFile = Path.Combine(path, "User", "Config", "WiimoteNew.ini");

            SimpleLogger.Instance.Info("[INFO] Configuring Lightgun buttons in : " + iniFile);

            string rom = Program.SystemConfig["rom"];
            var mappingToUse = defaultGunMapping;

            var guns = RawLightgun.GetRawLightguns();

            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                sindenSoft = true;
            }

            // Search gun game in gamesDB.xml file
            var game = Program.GunGames.FindGunGame("wii", rom);
            if (game == null || game.Gun == null)
            {
                SimpleLogger.Instance.Info("[GUNS] No gun configuration found for this game.");
            }

            Dictionary<string, string> metadata = new Dictionary<string, string>();
            if (game != null && game.Gun != null)
            {
                metadata["vertical_offset"] = game.Gun.VerticalOffset.ToString();
                metadata["yaw"] = game.Gun.Yaw.ToString();
                metadata["pitch"] = game.Gun.Pitch.ToString();
                metadata["action"] = game.Gun.Action;
                metadata["start"] = game.Gun.Start;
                metadata["select"] = game.Gun.Select;
                metadata["trigger"] = game.Gun.Trigger;
                metadata["ir_down"] = game.Gun.IrDown;
                metadata["ir_right"] = game.Gun.IrRight;
                metadata["ir_up"] = game.Gun.IrUp;
                metadata["ir_left"] = game.Gun.IrLeft;
                metadata["sub1"] = game.Gun.Sub1;
            }

            if (metadata == null)
                return;

            // Modify mapping based on gamesDB.xml file
            foreach (var x in metadata)
            {
                if (x.Value != null && newGunMapping.ContainsKey(x.Value))
                {
                    string toReplace = newGunMapping[x.Value];
                    string newValue = defaultGunMapping[gunMapping[x.Key]];
                    mappingToUse[toReplace] = newValue;
                    if (toReplace == "Shake/X")
                    {
                        mappingToUse["Shake/Y"] = newValue;
                        mappingToUse["Shake/Z"] = newValue;
                    }
                }
            }

            // Remove original button mapping
            foreach (var x in metadata)
            {
                if (x.Value != null && gunMapping.ContainsKey(x.Key))
                    mappingToUse[gunMapping[x.Key]] = "";
            }

            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                for (int i = 1; i < 5; i++)
                {
                    ini.ClearSection("Wiimote" + i.ToString());
                }
                string wiimote = "Wiimote1";

                foreach (var x in mappingToUse)
                {
                    ini.WriteValue(wiimote, x.Key, x.Value);
                }

                // Write specials
                foreach (var x in metadata)
                {
                    if (x.Value != null && x.Value != "" && gunSpecials.ContainsKey(x.Key))
                        ini.WriteValue(wiimote, gunSpecials[x.Key], x.Value);
                }

                ini.Save();
            }

            // Reset hotkeys
            string hotkeyini = Path.Combine(path, "User", "Config", "Hotkeys.ini");
            ResetHotkeysToDefault(hotkeyini);
        }

        static readonly Dictionary<string, string> defaultGunMapping = new Dictionary<string, string>
        {
            { "Device", "DInput/0/Keyboard Mouse" },
            { "Source", "1" },
            { "Buttons/X", "Q" },
            { "Buttons/B", "`Click 0`" },
            { "Buttons/Y", "S" },
            { "Buttons/A", "`Click 1`" },
            { "Buttons/-", "BACK" },
            { "Buttons/+", "RETURN" },
            { "Main Stick/Up", "UP" },
            { "Main Stick/Down", "DOWN" },
            { "Main Stick/Left", "LEFT" },
            { "Main Stick/Right", "RIGHT" },
            { "Tilt/Modifier/Range", "50." },
            { "Nunchuk/Stick/Modifier/Range", "50." },
            { "Nunchuk/Tilt/Modifier/Range", "50." },
            { "uDraw/Stylus/Modifier/Range", "50." },
            { "Drawsome/Stylus/Modifier/Range", "50." },
            { "Buttons/1", "`1`" },
            { "Buttons/2", "`5`" },
            { "D-Pad/Up", "UP" },
            { "D-Pad/Down", "DOWN" },
            { "D-Pad/Left", "LEFT" },
            { "D-Pad/Right", "RIGHT" },
            { "IR/Up", "`Cursor Y-`" },
            { "IR/Down", "`Cursor Y+`" },
            { "IR/Left", "`Cursor X-`" },
            { "IR/Right", "`Cursor X+`" },
            { "Shake/X", "`Click 2`" },
            { "Shake/Y", "`Click 2`" },
            { "Shake/Z", "`Click 2`" },
            { "Extension", "Nunchuk" },
            { "Nunchuk/Buttons/C", "LCONTROL" },
            { "Nunchuk/Buttons/Z", "LSHIFT" },
            { "Nunchuk/Stick/Up", "W" },
            { "Nunchuk/Stick/Down", "S" },
            { "Nunchuk/Stick/Left", "A" },
            { "Nunchuk/Stick/Right", "D" },
            { "Nunchuk/Stick/Calibration", "100.00 141.42 100.00 141.42 100.00 141.42 100.00 141.42" },
            //{ "Nunchuk/Shake/X", "`Click 2`" },
            //{ "Nunchuk/Shake/Y", "`Click 2`" },
            //{ "Nunchuk/Shake/Z", "`Click 2`" }
        };

        static readonly Dictionary<string, string> gunMapping = new Dictionary<string, string>
        {
            { "action", "Buttons/A" },
            { "trigger", "Buttons/B" },
            { "sub3", "Buttons/Home" },
            { "select", "Buttons/-" },
            { "sub1", "Buttons/1" },
            { "sub2", "Buttons/2" },
            { "start", "Buttons/+" },
            { "up", "D-Pad/Up" },
            { "down", "D-Pad/Down" },
            { "left", "D-Pad/Left" },
            { "right", "D-Pad/Right" },
            { "c", "Nunchuk/Buttons/C" },
            { "z", "Nunchuk/Buttons/Z" }
        };

        static readonly Dictionary<string, string> newGunMapping = new Dictionary<string, string>
        {
            { "a", "Buttons/A" },
            { "b", "Buttons/B" },
            { "home", "Buttons/Home" },
            { "-", "Buttons/-" },
            { "1", "Buttons/1" },
            { "2", "Buttons/2" },
            { "+", "Buttons/+" },
            { "up", "D-Pad/Up" },
            { "down", "D-Pad/Down" },
            { "left", "D-Pad/Left" },
            { "right", "D-Pad/Right" },
            { "tiltforward", "Tilt/Forward" },
            { "tiltbackward", "Tilt/Backward" },
            { "tiltleft", "Tilt/Left" },
            { "tiltright", "Tilt/Right" },
            { "shake", "Shake/X" },
            { "c", "Nunchuk/Buttons/C" },
            { "z", "Nunchuk/Buttons/Z" }
        };

        static readonly Dictionary<string, string> gunSpecials = new Dictionary<string, string>
        {
            { "vertical_offset", "IR/Vertical Offset" },
            { "yaw", "IR/Total Yaw" },
            { "pitch", "IR/Total Pitch" },
            { "ir_down", "IR/Down" },
            { "ir_up", "IR/Up" },
            { "ir_left", "IR/Left" },
            { "ir_right", "IR/Right" }
        };
    }
}
