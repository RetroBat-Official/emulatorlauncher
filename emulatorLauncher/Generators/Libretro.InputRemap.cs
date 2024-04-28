using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;

namespace EmulatorLauncher.Libretro
{
    partial class LibRetroGenerator : Generator
    {
        static readonly List<string> systemButtonInvert = new List<string>() { "snes", "snes-msu", "sattelaview", "sufami", "sgb" };
        static readonly List<string> systemButtonRotate = new List<string>() { "nes", "fds" };
        static readonly List<string> coreNoRemap = new List<string>() { "mednafen_snes" };

        private static int _playerCount = 1;

        public static void GenerateCoreInputRemap(string system, string core, Dictionary<string, string> inputremap)
        {
            _playerCount = Program.Controllers.Count;

            if (_playerCount == 0)
                return;

            string romName = null;
            string rom = Program.SystemConfig["rom"];
            if (!string.IsNullOrEmpty(rom) && File.Exists(rom))
                romName = System.IO.Path.GetFileNameWithoutExtension(rom);

            bool invertButtons = systemButtonInvert.Contains(system) && Program.Features.IsSupported("buttonsInvert") && Program.SystemConfig.getOptBoolean("buttonsInvert");
            bool rotateButtons = systemButtonRotate.Contains(system) && Program.Features.IsSupported("shift_buttons") && Program.SystemConfig.getOptBoolean("shift_buttons");

            for (int i = 1; i <= _playerCount; i++)
            {
                if (invertButtons && !coreNoRemap.Contains(core))
                {
                    inputremap["input_player" + i + "_btn_a"] = "0";
                    inputremap["input_player" + i + "_btn_b"] = "8";
                    inputremap["input_player" + i + "_btn_x"] = "1";
                    inputremap["input_player" + i + "_btn_y"] = "9";
                }

                if (rotateButtons && !coreNoRemap.Contains(core))
                {
                    inputremap["input_player" + i + "_btn_a"] = "9";
                    inputremap["input_player" + i + "_btn_b"] = "8";
                    inputremap["input_player" + i + "_btn_x"] = "1";
                    inputremap["input_player" + i + "_btn_y"] = "0";
                }

                if (core == "atari800")
                {
                    inputremap["input_player" + i + "_btn_a"] = "0";
                    inputremap["input_player" + i + "_btn_b"] = "8";

                    if (system == "atari5200")
                    {
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "9";
                    }
                }

                if (system == "gamecube")
                {
                    bool revertall = Program.Features.IsSupported("gamepadbuttons") && Program.SystemConfig.isOptSet("gamepadbuttons") && Program.SystemConfig["gamepadbuttons"] == "reverse_all";
                    bool revertAB = Program.Features.IsSupported("gamepadbuttons") && Program.SystemConfig.isOptSet("gamepadbuttons") && Program.SystemConfig["gamepadbuttons"] == "reverse_ab";
                    bool xboxPositions = Program.Features.IsSupported("gamepadbuttons") && Program.SystemConfig.isOptSet("gamepadbuttons") && Program.SystemConfig["gamepadbuttons"] == "xbox";
                    bool analogTriggers = Program.Features.IsSupported("gamepadanalogtriggers") && Program.SystemConfig.isOptSet("gamepadanalogtriggers") && Program.SystemConfig["gamepadanalogtriggers"] == "true";

                    if (analogTriggers)
                    {
                        inputremap["input_player" + i + "_btn_l2"] = "14";
                        inputremap["input_player" + i + "_btn_r2"] = "15";
                        inputremap["input_player" + i + "_btn_l3"] = "-1";
                        inputremap["input_player" + i + "_btn_r3"] = "-1";
                    }

                    if (revertall)
                        continue;

                    if (xboxPositions)
                    {
                        inputremap["input_player" + i + "_btn_a"] = "0";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "9";
                    }

                    else if (revertAB)
                    {
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "9";
                    }

                    else
                    {
                        inputremap["input_player" + i + "_btn_a"] = "9";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "0";
                    }
                }
            }
            SetupCoreGameRemaps(system, core, romName, inputremap);
            return;
        }

        private static void SetupCoreGameRemaps(string system, string core, string romName, Dictionary<string, string> inputremap)
        {
            if (core == null || system == null || romName == null)
                return;

            YmlContainer game = null;

            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            string coreMapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "libretro_" + core + "_" + system + ".yml");

            if (!File.Exists(coreMapping))
                return;

            YmlFile ymlFile = YmlFile.Load(coreMapping);

            if (ymlFile == null)
                return;

            game = ymlFile.Elements.Where(c => c.Name == romName).FirstOrDefault() as YmlContainer;

            if (game == null)
                game = ymlFile.Elements.Where(c => romName.StartsWith(c.Name)).FirstOrDefault() as YmlContainer;

            if (game == null)
                game = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

            if (game == null)
                return;

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
            gameMapping.Add(gameName, buttonMap);

            if (buttonMap.Count == 0)
                return;

            for (int i = 1; i <= _playerCount; i++)
            {
                foreach (var button in buttonMap)
                    inputremap["input_player" + i + "_" + button.Key] = button.Value;
            }
        }

        private enum Mame_remap
        {
            L3 = 14,
            R3 = 15,
        };

        private enum Atari800_remap
        {
            FIRE1 = 0,
            FIRE2 = 8,
            NUMPAD_DIESE = 1,
            NUMPAD_STAR = 9,
        };

        private enum Dolphin_gamecube_remap
        {
            X = 9,
            A = 8,
            Y = 1,
            B = 0,
            LEFT_ANALOG = 14,
            RIGHT_ANALOG = 15,
            EMPTY = -1,
        };

        private enum Snes_remap
        {
            X = 9,
            A = 8,
            Y = 1,
            B = 0,
        };

        private enum Nes_remap
        {
            TURBO_A = 9,
            A = 8,
            TURBO_B = 1,
            B = 0,
        };

        private enum Flycast_remap
        {
            LP = 0,
            BLOW_OFF = 1,
            COIN = 2,
            START = 3,
            DPAD_UP = 4,
            DPAD_DOWN = 5,
            DPAD_LEFT = 6,
            DPAD_RIGHT = 7,
            SP = 8,
            LK = 9,
            SK = 11,
            TEST = 14,
            SERVICE = 15,
            NON_ASSIGNED = -1,
        };
    }
}