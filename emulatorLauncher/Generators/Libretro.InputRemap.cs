using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace EmulatorLauncher.Libretro
{
    partial class LibRetroGenerator : Generator
    {
        // Used to get user specific remap files from inputmapping yml file
        // Used to managed Retroarch remaps and align controls between several cores (Retrobat default remaps)
        // Used for options to invert buttons, etc.


        static readonly List<string> systemButtonInvert = new List<string>() { "snes", "snes-msu", "superfamicom", "sattelaview", "sufami", "sgb", "gb-msu", "sfc" };
        static readonly List<string> systemButtonRotate = new List<string>() { "famicom", "nes", "fds", "mastersystem" };
        static readonly List<string> systemMegadrive = new List<string>() { "genesis", "genesis-msu", "megadrive", "megacd", "megadrive-msu", "sega32x", "segacd" };
        static readonly List<string> systemNES = new List<string>() { "nes", "fds", "famicom" };
        static readonly List<string> systemN64 = new List<string>() { "n64", "n64dd" };
        static readonly List<string> systemFBneo = new List<string>() { "cave", "cps1", "cps2", "cps3", "fbneo", "neogeo" };
        static readonly List<string> megadrive3ButtonsList = new List<string>() { "2", "257", "1025", "1537", "773" };
        static readonly List<string> coreNoRemap = new List<string>() { "mednafen_snes" };
        static readonly Dictionary<string,string> messFiles = new Dictionary<string,string>()
            {
                { "advision", "advision"},
                { "apfm1000", "apfm1000"},
                { "arcadia", "arcadia"},
                { "astrocade", "astrocde"},
                { "casloopy", "casloopy"},
                { "crvision", "crvision"},
                { "gamecom", "gamecom"},
                { "supracan", "supracan"}
            };

        private static int _playerCount = 1;
        private static int _maxCount = 2;
        private static string _gameRemapName = null;
        private bool _noRemap = false;
        private static List<string> _cfgFilesToRestore = new List<string>();
        private static string _inputRemapSave = null;

        public static void GenerateCoreInputRemap(string system, string core, Dictionary<string, string> inputremap, ConfigFile coreSettings, bool mameAuto = false)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (_noCoreRemap)
                return;

            _playerCount = Program.Controllers.Count;
            _maxCount = _playerCount + 2;

            if (Program.SystemConfig.getOptBoolean("force1pOnly"))
            {
                if (coreToP2Device.ContainsKey(core))
                {
                    for (int i = 3; i < _maxCount + 1; i++)
                        inputremap["input_libretro_device_p" + i] = "0";
                }
                else
                {
                    for (int i = 2; i < _maxCount + 1; i++)
                        inputremap["input_libretro_device_p" + i] = "0";
                }
            }

            if (_playerCount == 0)
                return;

            string romName = null;
            string rom = Program.SystemConfig["rom"];
            if (!string.IsNullOrEmpty(rom) && File.Exists(rom))
                romName = System.IO.Path.GetFileNameWithoutExtension(rom);

            if (core == "mame")
            {
                // default
                string defaultcfgFile = Path.Combine(Program.AppConfig.GetFullPath("saves"), "mame", "cfg", "default.cfg");
                string defaultctrlFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "lr-mame", "default.cfg");
                if (File.Exists(defaultcfgFile))
                    DeleteDefaultInputincfgFile(defaultcfgFile, defaultctrlFile);
                else if (File.Exists(defaultctrlFile) && !File.Exists(defaultcfgFile) && !mameAuto)
                    try { File.Copy(defaultctrlFile, defaultcfgFile); } catch { }

                // Per system/game
                string cfgFile;
                string ctrlFile;

                if (messFiles.ContainsKey(system))
                {
                    string messcfgFile = messFiles[system] + ".cfg";
                    cfgFile = Path.Combine(Program.AppConfig.GetFullPath("saves"), "mame", "cfg", messcfgFile);
                    ctrlFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "lr-mame", messcfgFile);
                }

                else
                {
                    cfgFile = Path.Combine(Program.AppConfig.GetFullPath("saves"), "mame", "cfg", romName + ".cfg");
                    ctrlFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "lr-mame", romName + ".cfg");
                    if (!File.Exists(ctrlFile))
                    {
                        string cfgDir = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "lr-mame");
                        if (Directory.Exists(cfgDir))
                        {
                            string[] cfgFiles = Directory.GetFiles(cfgDir, "*.cfg", SearchOption.TopDirectoryOnly);
                            ctrlFile = cfgFiles.Where(c => romName.StartsWith(Path.GetFileNameWithoutExtension(c))).OrderByDescending(c => Path.GetFileNameWithoutExtension(c).Length).FirstOrDefault();
                        }
                    }
                }

                if (File.Exists(cfgFile))
                    DeleteInputincfgFile(cfgFile, ctrlFile, defaultcfgFile);
                else if (File.Exists(ctrlFile) && !File.Exists(cfgFile) && !mameAuto)
                    try { File.Copy(ctrlFile, cfgFile); } catch { }
            }

            bool remapFromFile = SetupCoreGameRemaps(system, core, romName, inputremap, coreSettings, mameAuto);
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
                        case "genesis_plus_gx_expanded":
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
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yb")
                            {
                                inputremap["input_player" + i + "_btn_b"] = "1";
                                inputremap["input_player" + i + "_btn_l"] = "9";
                                inputremap["input_player" + i + "_btn_r"] = "0";
                                inputremap["input_player" + i + "_btn_x"] = "11";
                                inputremap["input_player" + i + "_btn_y"] = "10";
                            }
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_by")
                            {
                                inputremap["input_player" + i + "_btn_b"] = "1";
                                inputremap["input_player" + i + "_btn_l"] = "0";
                                inputremap["input_player" + i + "_btn_r"] = "9";
                                inputremap["input_player" + i + "_btn_x"] = "11";
                                inputremap["input_player" + i + "_btn_y"] = "10";
                            }
                            break;
                        case "fbneo":
                            if (Program.SystemConfig["megadrive_control_layout"] == "lr_zc")
                            {
                                break;
                            }
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yz")
                            {
                                inputremap["input_player" + i + "_btn_a"] = "11";
                                inputremap["input_player" + i + "_btn_b"] = "8";
                                inputremap["input_player" + i + "_btn_l"] = "9";
                                inputremap["input_player" + i + "_btn_r"] = "10";
                                inputremap["input_player" + i + "_btn_x"] = "1";
                                inputremap["input_player" + i + "_btn_y"] = "0";
                            }
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_yb")
                            {
                                inputremap["input_player" + i + "_btn_a"] = "11";
                                inputremap["input_player" + i + "_btn_l"] = "9";
                                inputremap["input_player" + i + "_btn_r"] = "8";
                                inputremap["input_player" + i + "_btn_x"] = "10";
                            }
                            else if (Program.SystemConfig["megadrive_control_layout"] == "lr_by")
                            {
                                inputremap["input_player" + i + "_btn_a"] = "11";
                                inputremap["input_player" + i + "_btn_l"] = "8";
                                inputremap["input_player" + i + "_btn_r"] = "9";
                                inputremap["input_player" + i + "_btn_x"] = "10";
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

                #region fbneo
                if (systemFBneo.Contains(system))
                {
                    if (core == "fbneo" && Program.SystemConfig.isOptSet("fbneo_controller") && !string.IsNullOrEmpty(Program.SystemConfig["fbneo_controller"]))
                    {
                        string layout = Program.SystemConfig["fbneo_controller"];
                        if (layout.StartsWith("custom_"))
                        {
                            switch (layout)
                            {
                                case "custom_4bmini":
                                    inputremap["input_player" + i + "_btn_a"] = "9";
                                    inputremap["input_player" + i + "_btn_b"] = "8";
                                    inputremap["input_player" + i + "_btn_x"] = "0";
                                    inputremap["input_player" + i + "_btn_y"] = "1";
                                    inputremap["input_player" + i + "_btn_l"] = "10";
                                    inputremap["input_player" + i + "_btn_l2"] = "11";
                                    inputremap["input_player" + i + "_btn_r"] = "12";
                                    inputremap["input_player" + i + "_btn_r2"] = "13";
                                    break;
                                case "custom_6b":
                                    inputremap["input_player" + i + "_btn_a"] = "11";
                                    inputremap["input_player" + i + "_btn_b"] = "8";
                                    inputremap["input_player" + i + "_btn_x"] = "1";
                                    inputremap["input_player" + i + "_btn_y"] = "9";
                                    inputremap["input_player" + i + "_btn_l"] = "13";
                                    inputremap["input_player" + i + "_btn_l2"] = "12";
                                    inputremap["input_player" + i + "_btn_r"] = "0";
                                    inputremap["input_player" + i + "_btn_r2"] = "10";
                                    break;
                                case "custom_8b_top":
                                    inputremap["input_player" + i + "_btn_a"] = "8";
                                    inputremap["input_player" + i + "_btn_b"] = "0";
                                    inputremap["input_player" + i + "_btn_x"] = "1";
                                    inputremap["input_player" + i + "_btn_y"] = "9";
                                    inputremap["input_player" + i + "_btn_l"] = "10";
                                    inputremap["input_player" + i + "_btn_l2"] = "11";
                                    inputremap["input_player" + i + "_btn_r"] = "12";
                                    inputremap["input_player" + i + "_btn_r2"] = "13";
                                    break;
                                case "custom_8b_bottom":
                                    inputremap["input_player" + i + "_btn_a"] = "10";
                                    inputremap["input_player" + i + "_btn_b"] = "11";
                                    inputremap["input_player" + i + "_btn_x"] = "12";
                                    inputremap["input_player" + i + "_btn_y"] = "13";
                                    inputremap["input_player" + i + "_btn_l"] = "8";
                                    inputremap["input_player" + i + "_btn_l2"] = "0";
                                    inputremap["input_player" + i + "_btn_r"] = "1";
                                    inputremap["input_player" + i + "_btn_r2"] = "9";
                                    break;
                                case "custom_8b_mvs2":
                                    inputremap["input_player" + i + "_btn_a"] = "8";
                                    inputremap["input_player" + i + "_btn_b"] = "10";
                                    inputremap["input_player" + i + "_btn_x"] = "1";
                                    inputremap["input_player" + i + "_btn_y"] = "9";
                                    inputremap["input_player" + i + "_btn_l"] = "11";
                                    inputremap["input_player" + i + "_btn_l2"] = "0";
                                    inputremap["input_player" + i + "_btn_r"] = "12";
                                    inputremap["input_player" + i + "_btn_r2"] = "13";
                                    break;
                            }
                        }
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
                    if (core == "fceumm")
                    {
                        if (Program.SystemConfig.getOptBoolean("xbox_layout"))
                        {
                            if (Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                            {
                                inputremap["input_player" + i + "_btn_y"] = "9";
                                inputremap["input_player" + i + "_btn_x"] = "1";
                            }
                            else
                            {
                                inputremap["input_player" + i + "_btn_y"] = "-1";
                                inputremap["input_player" + i + "_btn_x"] = "-1";
                            }

                            inputremap["input_player" + i + "_btn_a"] = "0";
                            inputremap["input_player" + i + "_btn_b"] = "8";
                        }
                        else if (!rotateButtons)
                        {
                            if (Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                            {
                                inputremap["input_player" + i + "_btn_a"] = "9";
                                inputremap["input_player" + i + "_btn_x"] = "1";
                            }
                            else
                            {
                                inputremap["input_player" + i + "_btn_a"] = "-1";
                                inputremap["input_player" + i + "_btn_x"] = "-1";
                            }

                            inputremap["input_player" + i + "_btn_b"] = "8";
                            inputremap["input_player" + i + "_btn_y"] = "0";
                        }
                        else if (!Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                        {
                            inputremap["input_player" + i + "_btn_x"] = "-1";
                            inputremap["input_player" + i + "_btn_y"] = "-1";
                        }


                    }

                    if (core == "nestopia")
                    {
                        if (!Program.SystemConfig.getOptBoolean("nes_turbo_enable"))
                        {
                            if (Program.SystemConfig.getOptBoolean("xbox_layout"))
                            {
                                if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                                {
                                    inputremap["input_player" + i + "_btn_x"] = "-1";
                                    inputremap["input_player" + i + "_btn_y"] = "-1";
                                    inputremap["input_player" + i + "_btn_a"] = "0";
                                    inputremap["input_player" + i + "_btn_b"] = "8";
                                }
                                else
                                {
                                    inputremap["input_player" + i + "_btn_a"] = "1";
                                    inputremap["input_player" + i + "_btn_x"] = "-1";
                                    inputremap["input_player" + i + "_btn_y"] = "-1";
                                }
                            }
                            else
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
                        else if (Program.SystemConfig.getOptBoolean("xbox_layout"))
                        {
                            if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                            {
                                inputremap["input_player" + i + "_btn_x"] = "1";
                                inputremap["input_player" + i + "_btn_y"] = "9";
                                inputremap["input_player" + i + "_btn_a"] = "0";
                                inputremap["input_player" + i + "_btn_b"] = "8";
                            }
                            else
                            {
                                inputremap["input_player" + i + "_btn_a"] = "1";
                                inputremap["input_player" + i + "_btn_y"] = "8";
                            }
                        }
                    }

                    if (core == "mesen")
                    {
                        if (Program.SystemConfig.getOptBoolean("xbox_layout"))
                        {
                            if (Program.SystemConfig.getOptBoolean("rotate_buttons"))
                            {
                                inputremap["input_player" + i + "_btn_a"] = "0";
                                inputremap["input_player" + i + "_btn_b"] = "8";
                                inputremap["input_player" + i + "_btn_x"] = "1";
                                inputremap["input_player" + i + "_btn_y"] = "9";
                            }
                            else
                            {
                                inputremap["input_player" + i + "_btn_a"] = "1";
                                inputremap["input_player" + i + "_btn_y"] = "8";
                            }
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

                #region supergrafx
                if (system == "supergrafx")
                {
                    if (core == "geargrafx")
                    {
                        inputremap["input_player" + i + "_btn_l"] = "12";
                        inputremap["input_player" + i + "_btn_l2"] = "-1";
                        inputremap["input_player" + i + "_btn_r"] = "13";
                        inputremap["input_player" + i + "_btn_r2"] = "-1";
                    }
                }
                #endregion
            }

            return;
        }

        private static bool SetupCoreGameRemaps(string system, string core, string romName, Dictionary<string, string> inputremap, ConfigFile coreSettings, bool mameAuto = false)
        {
            if (core == null || system == null || romName == null || mameAuto)
                return false;

            YmlContainer game = null;
            string coreMapping = null;

            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{core}", core)
                    .Replace("{system}", system)
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "user");

                coreMapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(coreMapping))
                    break;
            }

            if (coreMapping == null)
                return false;

            YmlFile ymlFile = YmlFile.Load(coreMapping);

            if (ymlFile == null)
                return false;

            string controLayout = "";
            if (Program.SystemConfig.isOptSet("controller_layout") && !string.IsNullOrEmpty(Program.SystemConfig["controller_layout"]))
                controLayout = Program.SystemConfig["controller_layout"];

            string searchYml = romName;

            // Loop through yml elements
            // example for mame sf2:
            // sf2_gamepad, sf2_8button...
            // If option is set in ES to use 8button layout for mame, search for:
            // 1) sf2_8button
            // 2) sf2
            // 3) Any container beginning with s or sf and ending with 8button
            // 4) Any container beginning with s or sf
            // 5) default_8button
            // 6) default
            YmlContainer gameLayout = null;
            game = ymlFile.Elements.Where(c => c.Name == searchYml).FirstOrDefault() as YmlContainer;
            if (game != null)
            {
                string searchYmlLayout = searchYml + "_" + controLayout;
                gameLayout = ymlFile.Elements.Where(c => c.Name == searchYmlLayout).FirstOrDefault() as YmlContainer;
                if (gameLayout != null)
                    game = gameLayout;
            }

            else if (game == null)
            {
                game = ymlFile.Elements.Where(c => romName.StartsWith(c.Name)).OrderByDescending(c => c.Name.Length).FirstOrDefault() as YmlContainer;
                if (game != null)
                {
                    string searchYmlLayout = game.Name + "_" + controLayout;
                    gameLayout = ymlFile.Elements.Where(c => c.Name == searchYmlLayout).FirstOrDefault() as YmlContainer;
                    if (gameLayout != null)
                        game = gameLayout;
                }
            }

            string defsearch = "default";
            if (!string.IsNullOrEmpty(controLayout))
                defsearch = defsearch + "_" + controLayout;

            if (game == null)
                game = ymlFile.Elements.Where(c => c.Name == defsearch).FirstOrDefault() as YmlContainer;

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

            if (buttonMap.Count == 0)
                return false;

            for (int i = 1; i <= _playerCount; i++)
            {
                foreach (var button in buttonMap)
                    inputremap["input_player" + i + "_" + button.Key] = button.Value;
                
            }
            _gameRemapName = romName;
            SimpleLogger.Instance.Info("[INFO] Generated controller configuration based on inputmapping file.");

            return true;
        }

        private Dictionary<string, string> InputRemap = new Dictionary<string, string>();

        private void CreateInputRemap(string cleanSystemName, Action<ConfigFile> createRemap)
        {
            if (string.IsNullOrEmpty(cleanSystemName))
                return;
            
            // Don't delete remap file is controller autoconfig is off
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig.getOptBoolean("disableautocontrollers"))
                return;

            DeleteInputRemap(cleanSystemName);
            if (createRemap == null || _noRemap)
                return;

            string dir = Path.Combine(RetroarchPath, "config", "remaps", cleanSystemName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string remapFilePath = Path.Combine(dir, cleanSystemName + ".rmp");

            var cfg = ConfigFile.FromFile(remapFilePath, new ConfigFileOptions() { CaseSensitive = true });
            createRemap(cfg);
            cfg.Save(remapFilePath, true);
        }

        private void DeleteInputRemap(string cleanSystemName)
        {
            if (string.IsNullOrEmpty(cleanSystemName))
                return;

            // Don't delete remap file is controller autoconfig is off
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig.getOptBoolean("disableautocontrollers"))
                return;

            string dir = Path.Combine(RetroarchPath, "config", "remaps", cleanSystemName);
            string path = Path.Combine(dir, cleanSystemName + ".rmp");
            
            try
            {
                if (File.Exists(path))
                {
                    try { File.Copy(path, path + ".backup", true); } catch { }
                    AddFileForRestoration(path);
                    File.Delete(path);
                }

                if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch { }
        }

        private static void DeleteInputincfgFile(string cfgFile, string ctrlFile, string defaultcfgFile = null)
        {
            // Backup cfgfile
            string backup = cfgFile + ".backup";
            try
            {
                File.Copy(cfgFile, backup, true);
                _cfgFilesToRestore.Add(cfgFile);
            }
            catch { }

            string controLayout = "";
            if (Program.SystemConfig.isOptSet("controller_layout") && !string.IsNullOrEmpty(Program.SystemConfig["controller_layout"]))
                controLayout = Program.SystemConfig["controller_layout"];

            string searchPattern = "_" + controLayout;

            try
            {
                XDocument doc = XDocument.Load(cfgFile);
                XElement gameSystemElement = doc.Root?.Element("system");

                // Keep only DIPSWITCH ports from the existing input
                XElement inputElement = gameSystemElement?.Element("input");

                if (inputElement != null)
                {
                    inputElement.Elements()
                        .Where(p => (string)p.Attribute("type") != "DIPSWITCH")
                        .Remove();
                }

                List<XElement> allPorts = new List<XElement>();
                List<XElement> keyboards = new List<XElement>();

                if (ctrlFile != null && File.Exists(ctrlFile))
                {
                    XDocument ctrldoc = XDocument.Load(ctrlFile);

                    XElement ctrlSystemElement = null;

                    if (ctrldoc.Root != null)
                    {
                        var systems = ctrldoc.Root.Elements("system");

                        ctrlSystemElement =
                            systems.FirstOrDefault(s =>
                            {
                                var nameAttr = s.Attribute("name");
                                return nameAttr != null &&
                                       nameAttr.Value.EndsWith(searchPattern);
                            })
                            ?? systems.FirstOrDefault();
                    }

                    XElement ctrlinputElement = null;

                    if (ctrlSystemElement != null)
                        ctrlinputElement = ctrlSystemElement.Element("input");

                    if (ctrlinputElement != null && gameSystemElement != null)
                    {
                        allPorts = ctrlinputElement.Elements("port").ToList();
                        keyboards = ctrlinputElement.Elements("keyboard").ToList();

                        var uiPorts = allPorts.Where(p => (string)p.Attribute("type") != null && ((string)p.Attribute("type")).StartsWith("UI"));
                        var nonUIPorts = allPorts.Where(p => (string)p.Attribute("type") != null && !((string)p.Attribute("type")).StartsWith("UI"));

                        if (nonUIPorts.Any())
                        {
                            XElement newInput = new XElement("input", nonUIPorts);
                            gameSystemElement.Add(newInput);

                            if (keyboards.Any())
                            {
                                foreach (var keyboard in keyboards)
                                    newInput.Add(keyboard);
                            }
                        }

                        if (uiPorts.Any() && defaultcfgFile != null && File.Exists(defaultcfgFile))
                        {
                            XDocument defaultdoc = XDocument.Load(defaultcfgFile);
                            XElement defaultSystemElement = defaultdoc.Root?.Element("system");

                            bool menuPort = uiPorts.Any(p => (string)p.Attribute("type") == "UI_MENU");

                            if (defaultSystemElement != null)
                            {
                                XElement defaultInput = defaultSystemElement?.Descendants("input").FirstOrDefault();

                                if (defaultInput != null)
                                {
                                    defaultInput.Elements("port").Where(p => (string)p.Attribute("type") == "UI_MENU").Remove();
                                    defaultInput.Add(uiPorts);
                                }
                                else
                                {
                                    defaultInput = new XElement("input", uiPorts);
                                    defaultSystemElement.Add(defaultInput);
                                }
                            }
                            else
                            {
                                defaultSystemElement = new XElement("system", new XAttribute("name", "default"));
                                XElement defaultInput = new XElement("input", uiPorts);
                                defaultSystemElement.Add(defaultInput);
                                defaultdoc.Root?.Add(defaultSystemElement);
                            }

                            defaultdoc.Save(defaultcfgFile);
                        }
                    }
                }

                doc.Save(cfgFile);
            }
            catch { }
        }

        private static void DeleteDefaultInputincfgFile(string cfgFile, string ctrlFile)
        {
            // Backup cfgfile
            string backupDefault = cfgFile + ".backup";
            try 
            { 
                File.Copy(cfgFile, backupDefault, true);
                _cfgFilesToRestore.Add(cfgFile);
            } 
            catch { }

            // Modify cfgfile
            try
            {
                XDocument doc = XDocument.Load(cfgFile);
                XElement gameSystemElement = doc.Root?.Element("system");
                gameSystemElement?.Element("input")?.Remove();

                List<XElement> allPorts = new List<XElement>();

                if (File.Exists(ctrlFile))
                {
                    XDocument ctrldoc = XDocument.Load(ctrlFile);

                    XElement ctrlinputElement = ctrldoc.Root?
                    .Element("system")?
                    .Element("input");

                    if (ctrlinputElement != null && gameSystemElement != null)
                    {
                        allPorts = ctrlinputElement.Elements("port").ToList();

                        var uiPorts = allPorts.Where(p => (string)p.Attribute("type") != null && ((string)p.Attribute("type")).StartsWith("UI"));
                        var nonUIPorts = allPorts.Where(p => (string)p.Attribute("type") != null && !((string)p.Attribute("type")).StartsWith("UI"));

                        if (uiPorts.Any())
                        {
                            XElement newInput = new XElement("input", uiPorts);
                            gameSystemElement.Add(newInput);
                        }
                    }
                }

                doc.Save(cfgFile);
            }
            catch { }
        }

        static string[] mappingPaths =
        {            
            // User specific
            "{userpath}\\inputmapping\\libretro_{core}_{system}.yml",
            "{userpath}\\inputmapping\\libretro_{core}.yml",
            "{userpath}\\inputmapping\\libretro.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\libretro_{core}_{system}.yml",
            "{systempath}\\resources\\inputmapping\\libretro_{core}.yml",
            "{systempath}\\resources\\inputmapping\\libretro.yml"
        };

        // RETROARCH/MAME correspondance (example for sf2 with snes controls):
        // A(EAST) => L
        // B(SOUTH) => X
        // X(NORTH) => A
        // Y(WEST) => B
        // L => Y
        // R => R
        private enum Mame_remap
        {
            L2 = 12,
            L = 10,
            SELECT = 2,
            START = 3,
            R2 = 13,
            R = 11,
            L3 = 14,
            R3 = 15,
            A = 8,
            B = 0,
            X = 9,
            Y = 1,
            JOY1DOWN = 18,
            JOY1LEFT = 17,
            JOY1UP = 19,
            JOY1RIGHT = 16,
            DPADRIGHT = 7,
            DPADLEFT = 6,
            DPADUP = 4,
            DPADDOWN = 5,
            JOY2DOWN = 22,
            JOY2LEFT = 21,
            JOY2UP = 23,
            JOY2RIGHT = 20
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