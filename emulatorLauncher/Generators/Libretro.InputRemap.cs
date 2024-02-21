using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;

namespace EmulatorLauncher.Libretro
{
    partial class LibRetroGenerator : Generator
    {
        public static void GenerateCoreInputRemap(string system, string core, Dictionary<string, string> inputremap)
        {
            int playerCount = Program.Controllers.Count;

            if (playerCount == 0)
                return;

            for (int i = 1; i <= playerCount; i++)
            {
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
                        return;

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

            return;
        }



        private enum mame_remap
        {
            L3 = 14,
            R3 = 15,
        };

        private enum atari800_remap
        {
            FIRE1 = 0,
            FIRE2 = 8,
            NUMPAD_DIESE = 1,
            NUMPAD_STAR = 9,
        };

        private enum dolphin_gamecube_remap
        {
            X = 9,
            A = 8,
            Y = 1,
            B = 0,
            LEFT_ANALOG = 14,
            RIGHT_ANALOG = 15,
            EMPTY = -1,
        };
    }
}