using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher.Libretro
{
    partial class LibRetroGenerator : Generator
    {
        // Used to get user specific remap files from inputmapping yml file
        // Used to managed Retroarch remaps and align controls between several cores (Retrobat default remaps)
        // Used for options to invert buttons, etc.


        static readonly List<string> systemButtonInvert = new List<string>() { "snes", "snes-msu", "sattelaview", "sufami", "sgb", "gb-msu" };
        static readonly List<string> systemButtonRotate = new List<string>() { "nes", "fds", "mastersystem" };
        static readonly List<string> systemMegadrive = new List<string>() { "genesis", "megadrive", "megadrive-msu", "sega32x", "segacd" };
        static readonly List<string> systemNES = new List<string>() { "nes", "fds" };
        static readonly List<string> systemN64 = new List<string>() { "n64", "n64dd" };
        static readonly List<string> megadrive3ButtonsList = new List<string>() { "2", "257", "1025", "1537", "773" };
        static readonly List<string> coreNoRemap = new List<string>() { "mednafen_snes" };

        private static int _playerCount = 1;

        public static void GenerateCoreInputRemap(string system, string core, Dictionary<string, string> inputremap)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            _playerCount = Program.Controllers.Count;

            if (_playerCount == 0)
                return;

            string romName = null;
            string rom = Program.SystemConfig["rom"];
            if (!string.IsNullOrEmpty(rom) && File.Exists(rom))
                romName = System.IO.Path.GetFileNameWithoutExtension(rom);

            bool remapFromFile = SetupCoreGameRemaps(system, core, romName, inputremap);
            if (remapFromFile)
                return;

            bool invertButtons = systemButtonInvert.Contains(system) && Program.Features.IsSupported("buttonsInvert") && Program.SystemConfig.getOptBoolean("buttonsInvert");
            bool rotateButtons = systemButtonRotate.Contains(system) && Program.Features.IsSupported("rotate_buttons") && Program.SystemConfig.getOptBoolean("rotate_buttons");

            for (int i = 1; i <= _playerCount; i++)
            {
                if (invertButtons && !coreNoRemap.Contains(core))
                {
                    inputremap["input_player" + i + "_btn_a"] = "0";
                    inputremap["input_player" + i + "_btn_b"] = "8";
                    inputremap["input_player" + i + "_btn_x"] = "1";
                    inputremap["input_player" + i + "_btn_y"] = "9";
                }
                
                #region atari800
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
                #endregion

                #region 3do
                if (system == "3do")
                {
                    inputremap["input_player" + i + "_btn_x"] = "-1";
                }
                #endregion

                #region 3ds
                if (system == "3ds")
                {
                    inputremap["input_player" + i + "_btn_l3"] = "15";
                    inputremap["input_player" + i + "_btn_r3"] = "-1";

                    if (Program.SystemConfig.getOptBoolean("gamepadbuttons"))
                    {
                        inputremap["input_player" + i + "_btn_a"] = "0";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "9";
                    }
                }
                #endregion

                #region dreamcast
                if (system == "dreamcast")
                {
                    if (Program.SystemConfig.getOptBoolean("dreamcast_use_shoulders"))
                    {
                        inputremap["input_player" + i + "_btn_l"] = "12";
                        inputremap["input_player" + i + "_btn_l2"] = "-1";
                        inputremap["input_player" + i + "_btn_r"] = "13";
                        inputremap["input_player" + i + "_btn_r2"] = "-1";
                    }
                }
                #endregion

                #region gamecube
                if (system == "gamecube")
                {
                    bool positional = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "position";
                    bool revertAB = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "reverse_ab";
                    bool xboxPositions = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "xbox";
                    bool digitalTriggers = Program.Features.IsSupported("gamepaddigitaltriggers") && Program.SystemConfig.isOptSet("gamepaddigitaltriggers") && Program.SystemConfig.getOptBoolean("gamepaddigitaltriggers");

                    inputremap["input_player" + i + "_btn_l3"] = "-1";
                    inputremap["input_player" + i + "_btn_r3"] = "-1";

                    if (positional)
                    {
                        inputremap["input_player" + i + "_btn_a"] = "9";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "0";
                        if (!digitalTriggers)
                        {
                            inputremap["input_player" + i + "_btn_l2"] = "14";
                            inputremap["input_player" + i + "_btn_r2"] = "15";
                        }
                    }

                    if (xboxPositions)
                    {
                        inputremap["input_player" + i + "_btn_a"] = "0";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "9";
                        if (!digitalTriggers)
                        {
                            inputremap["input_player" + i + "_btn_l2"] = "14";
                            inputremap["input_player" + i + "_btn_r2"] = "15";
                        }
                    }

                    else if (revertAB)
                    {
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "9";
                        if (!digitalTriggers)
                        {
                            inputremap["input_player" + i + "_btn_l2"] = "14";
                            inputremap["input_player" + i + "_btn_r2"] = "15";
                        }
                    }

                    else
                    {
                        if (!digitalTriggers)
                        {
                            inputremap["input_player" + i + "_btn_l2"] = "14";
                            inputremap["input_player" + i + "_btn_r2"] = "15";
                        }
                        inputremap["input_player" + i + "_btn_l3"] = "-1";
                        inputremap["input_player" + i + "_btn_r3"] = "-1";
                    }
                }
                #endregion

                #region gamegear
                if (system == "gamegear")
                {
                    if (core == "fbneo")
                    {
                        inputremap["input_player" + i + "_btn_a"] = "0";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                    }
                }
                #endregion

                #region mastersystem
                if (system == "mastersystem" && rotateButtons)
                {
                    if (core == "fbneo")
                    {
                        inputremap["input_player" + i + "_btn_a"] = "-1";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_y"] = "0";
                    }
                    else
                    {
                        inputremap["input_player" + i + "_btn_a"] = "-1";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "-1";
                        inputremap["input_player" + i + "_btn_y"] = "0";
                    }
                }
                #endregion

                #region megadrive
                if (systemMegadrive.Contains(system) && !megadrive3ButtonsList.Contains(Program.SystemConfig["genesis_plus_gx_controller"]))
                {
                    switch (core)
                    {
                        case "genesis_plus_gx":
                        case "genesis_plus_gx_wide":
                        case "picodrive":
                            if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                            {
                                inputremap["input_player" + i + "_btn_a"] = "0";
                                inputremap["input_player" + i + "_btn_b"] = "1";
                                inputremap["input_player" + i + "_btn_l"] = "11";
                                inputremap["input_player" + i + "_btn_r"] = "8";
                                inputremap["input_player" + i + "_btn_y"] = "10";
                            }
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                            {
                                inputremap["input_player" + i + "_btn_l"] = "9";
                                inputremap["input_player" + i + "_btn_x"] = "10";
                            }
                            break;
                        case "fbneo":
                            if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                            {
                                inputremap["input_player" + i + "_btn_a"] = "0";
                                inputremap["input_player" + i + "_btn_b"] = "1";
                                inputremap["input_player" + i + "_btn_r"] = "8";
                                inputremap["input_player" + i + "_btn_x"] = "11";
                                inputremap["input_player" + i + "_btn_y"] = "9";
                            }
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                            {
                                inputremap["input_player" + i + "_btn_l"] = "11";
                                inputremap["input_player" + i + "_btn_r"] = "10";
                            }
                            else
                            {
                                inputremap["input_player" + i + "_btn_l"] = "9";
                                inputremap["input_player" + i + "_btn_r"] = "10";
                                inputremap["input_player" + i + "_btn_x"] = "11";
                            }
                            break;
                    }
                }
                else if (systemMegadrive.Contains(system))
                {
                    if (core == "fbneo")
                    {
                        inputremap["input_player" + i + "_btn_l"] = "9";
                        inputremap["input_player" + i + "_btn_r"] = "10";
                        inputremap["input_player" + i + "_btn_x"] = "11";
                    }
                }
                #endregion

                #region neogeocd
                if (system == "neogeocd")
                {
                    if (core == "neocd")
                    {
                        inputremap["input_player" + i + "_btn_l"] = "14";
                        inputremap["input_player" + i + "_btn_l2"] = "15";
                        inputremap["input_player" + i + "_btn_l3"] = "-1";
                        inputremap["input_player" + i + "_btn_r"] = "12";
                        inputremap["input_player" + i + "_btn_r3"] = "-1";
                    }
                }
                #endregion

                #region N64
                if (systemN64.Contains(system))
                {
                    if (Program.SystemConfig.isOptSet("lr_n64_buttons") && Program.SystemConfig["lr_n64_buttons"] == "xbox")
                    {
                        inputremap["input_player" + i + "_btn_a"] = "1";
                        inputremap["input_player" + i + "_btn_r2"] = "-1";
                        inputremap["input_player" + i + "_btn_x"] = "-1";
                        inputremap["input_player" + i + "_btn_y"] = "-1";
                    }
                }
                #endregion

                #region NES
                if (systemNES.Contains(system))
                {
                    if (core == "fceumm" && !rotateButtons)
                    {
                        inputremap["input_player" + i + "_btn_a"] = "9";
                        inputremap["input_player" + i + "_btn_b"] = "8";
                        inputremap["input_player" + i + "_btn_x"] = "1";
                        inputremap["input_player" + i + "_btn_y"] = "0";
                    }

                    if (core == "nestopia" && !Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                    {
                        if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                        {
                            inputremap["input_player" + i + "_btn_x"] = "-1";
                            inputremap["input_player" + i + "_btn_y"] = "-1";
                        }
                        else
                        {
                            inputremap["input_player" + i + "_btn_x"] = "-1";
                            inputremap["input_player" + i + "_btn_a"] = "-1";
                        }
                    }
                }
                #endregion

                #region psx
                if (system == "psx" && Program.SystemConfig.getOptBoolean("psx_triggerswap"))
                {
                    inputremap["input_player" + i + "_btn_l2"] = "22";
                    inputremap["input_player" + i + "_btn_r2"] = "23";
                }
                #endregion

                #region saturn
                if (system == "saturn")
                {
                    bool switchTriggers = Program.SystemConfig.getOptBoolean("saturn_invert_triggers");
                    if (Program.SystemConfig.isOptSet("saturn_padlayout") && !string.IsNullOrEmpty(Program.SystemConfig["saturn_padlayout"]))
                    {
                        if (core == "yabasanshiro")
                        {
                            switch (Program.SystemConfig["saturn_padlayout"])
                            {
                                case "lr_yz":
                                    if (switchTriggers)
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "10";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "12";
                                        inputremap["input_player" + i + "_btn_r"] = "13";
                                        inputremap["input_player" + i + "_btn_x"] = "1";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                        inputremap["input_player" + i + "_btn_l2"] = "9";
                                        inputremap["input_player" + i + "_btn_r2"] = "11";
                                    }
                                    else
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "10";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "9";
                                        inputremap["input_player" + i + "_btn_x"] = "1";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                    }
                                    break;
                                case "lr_xz":
                                    if (switchTriggers)
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "10";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "12";
                                        inputremap["input_player" + i + "_btn_l2"] = "1";
                                        inputremap["input_player" + i + "_btn_r"] = "13";
                                        inputremap["input_player" + i + "_btn_r2"] = "11";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                    }
                                    else
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "10";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "1";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                    }
                                    break;
                                case "lr_zc":
                                    if (switchTriggers)
                                    {
                                        inputremap["input_player" + i + "_btn_l"] = "12";
                                        inputremap["input_player" + i + "_btn_l2"] = "11";
                                        inputremap["input_player" + i + "_btn_r"] = "13";
                                        inputremap["input_player" + i + "_btn_r2"] = "10";
                                    }
                                    break;
                            }
                        }

                        else
                        {
                            switch (Program.SystemConfig["saturn_padlayout"])
                            {
                                case "lr_yz":
                                    if (switchTriggers)
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "11";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "12";
                                        inputremap["input_player" + i + "_btn_r"] = "13";
                                        inputremap["input_player" + i + "_btn_x"] = "1";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                        inputremap["input_player" + i + "_btn_l2"] = "9";
                                        inputremap["input_player" + i + "_btn_r2"] = "10";
                                    }
                                    else
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "11";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "9";
                                        inputremap["input_player" + i + "_btn_r"] = "10";
                                        inputremap["input_player" + i + "_btn_x"] = "1";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                    }
                                    break;
                                case "lr_xz":
                                    if (switchTriggers)
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "11";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "12";
                                        inputremap["input_player" + i + "_btn_l2"] = "1";
                                        inputremap["input_player" + i + "_btn_r"] = "13";
                                        inputremap["input_player" + i + "_btn_r2"] = "10";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                    }
                                    else
                                    {
                                        inputremap["input_player" + i + "_btn_a"] = "11";
                                        inputremap["input_player" + i + "_btn_b"] = "8";
                                        inputremap["input_player" + i + "_btn_l"] = "1";
                                        inputremap["input_player" + i + "_btn_r"] = "10";
                                        inputremap["input_player" + i + "_btn_y"] = "0";
                                    }
                                    break;
                                case "lr_zc":
                                    if (switchTriggers)
                                    {
                                        inputremap["input_player" + i + "_btn_l"] = "12";
                                        inputremap["input_player" + i + "_btn_l2"] = "10";
                                        inputremap["input_player" + i + "_btn_r"] = "13";
                                        inputremap["input_player" + i + "_btn_r2"] = "11";
                                    }
                                    break;
                            }
                        }
                    }
                    else if (core == "yabasanshiro")
                    {
                        if (switchTriggers)
                        {
                            inputremap["input_player" + i + "_btn_l"] = "12";
                            inputremap["input_player" + i + "_btn_l2"] = "11";
                            inputremap["input_player" + i + "_btn_r"] = "13";
                            inputremap["input_player" + i + "_btn_r2"] = "10";
                        }
                        else
                        {
                            inputremap["input_player" + i + "_btn_l"] = "11";
                            inputremap["input_player" + i + "_btn_r"] = "10";
                        }
                    }
                }
                #endregion
            }
            
            return;
        }

        private static bool SetupCoreGameRemaps(string system, string core, string romName, Dictionary<string, string> inputremap)
        {
            if (core == null || system == null || romName == null)
                return false;

            Dictionary<string, Dictionary<string, string>> gameMapping = new Dictionary<string, Dictionary<string, string>>();
            YmlContainer game = null;
            string coreMapping = null;

            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{core}", core)
                    .Replace("{system}", system)
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "inputmapping");

                coreMapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(coreMapping))
                    break;
            }

            if (coreMapping == null)
                return false;

            YmlFile ymlFile = YmlFile.Load(coreMapping);

            if (ymlFile == null)
                return false;

            game = ymlFile.Elements.Where(c => c.Name == romName).FirstOrDefault() as YmlContainer;

            if (game == null)
                game = ymlFile.Elements.Where(c => romName.StartsWith(c.Name)).FirstOrDefault() as YmlContainer;

            if (game == null)
                game = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

            if (game == null)
                return false;

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
                return false;

            for (int i = 1; i <= _playerCount; i++)
            {
                foreach (var button in buttonMap)
                    inputremap["input_player" + i + "_" + button.Key] = button.Value;
            }
            return true;
        }

        static string[] mappingPaths =
        {            
            // User specific
            "{userpath}\\libretro_{core}_{system}.yml",
            "{userpath}\\libretro_{core}.yml",
            "{userpath}\\libretro.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\libretro_{core}_{system}.yml",
            "{systempath}\\resources\\inputmapping\\libretro_{core}.yml",
            "{systempath}\\resources\\inputmapping\\libretro.yml"
        };

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