using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using emulatorLauncher.Tools;

namespace emulatorLauncher.libRetro
{
    partial class LibRetroGenerator : Generator
    {
        static List<string> coreGunConfig = new List<string>() 
        { 
            "flycast",
            "mednafen_psx", 
            "mednafen_psx_hw",
            "pcsx_rearmed",
            "swanstation",
            "fbneo",
        };

        /// <summary>
        /// Injects guns settings
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="deviceType"></param>
        /// <param name="playerIndex"></param>
        private void SetupLightGuns(ConfigFile retroarchConfig, string deviceType, string core, int playerIndex = 1)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            bool multigun = false;
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");
            bool useOneGun = SystemConfig.isOptSet("one_gun") && SystemConfig.getOptBoolean("one_gun");

            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();
            if (gunCount > 1 && guns.Length > 1 && playerIndex == 1 && !useOneGun)
                multigun = true;

            if (!multigun)
            {
                // Get gamepad buttons to assign them so that controller buttons can be used along with gun
                string a_padbutton = retroarchConfig["input_player" + playerIndex + "_a_btn"];
                string b_padbutton = retroarchConfig["input_player" + playerIndex + "_b_btn"];
                string c_padbutton = retroarchConfig["input_player" + playerIndex + "_y_btn"];
                string start_padbutton = retroarchConfig["input_player" + playerIndex + "_start_btn"];
                string select_padbutton = retroarchConfig["input_player" + playerIndex + "_select_btn"];
                string up_padbutton = retroarchConfig["input_player" + playerIndex + "_up_btn"];
                string down_padbutton = retroarchConfig["input_player" + playerIndex + "_down_btn"];
                string left_padbutton = retroarchConfig["input_player" + playerIndex + "_left_btn"];
                string right_padbutton = retroarchConfig["input_player" + playerIndex + "_right_btn"];

                // Set mouse buttons for one player (default mapping)
                retroarchConfig["input_libretro_device_p" + playerIndex] = deviceType;
                retroarchConfig["input_player" + playerIndex + "_mouse_index"] = "0";
                retroarchConfig["input_player" + playerIndex + "_gun_trigger_mbtn"] = guninvert ? "2" : "1";
                retroarchConfig["input_player" + playerIndex + "_gun_offscreen_shot_mbtn"] = guninvert ? "1" : "2";
                retroarchConfig["input_player" + playerIndex + "_gun_start_mbtn"] = "3";

                // Assign gamepad buttons to gun buttons
                if (select_padbutton != "" && select_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_select_btn"] = select_padbutton;
                if (start_padbutton != "" && start_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_start_btn"] = start_padbutton;
                if (a_padbutton != "" && a_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_a_btn"] = a_padbutton;
                if (b_padbutton != "" && b_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_b_btn"] = b_padbutton;
                if (c_padbutton != "" && c_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_c_btn"] = c_padbutton;
                if (up_padbutton != "" && up_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_up_btn"] = up_padbutton;
                if (down_padbutton != "" && down_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_down_btn"] = down_padbutton;
                if (left_padbutton != "" && left_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_left_btn"] = left_padbutton;
                if (right_padbutton != "" && right_padbutton != null)
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_right_btn"] = right_padbutton;

                retroarchConfig["input_player" + playerIndex + "_analog_dpad_mode"] = "0";
                retroarchConfig["input_player" + playerIndex + "_joypad_index"] = "0";
            }
            else
            {
                // DirectInput does not differenciate mouse indexes. We have to use "Raw" with multiple guns
                retroarchConfig["input_driver"] = "raw";

                // Set mouse buttons for multigun
                for (int i = 1; i <= guns.Length; i++)
                {
                    // Get gamepad buttons to assign them so that controller buttons can be used along with gun
                    string a_padbutton = retroarchConfig["input_player" + i + "_a_btn"];
                    string b_padbutton = retroarchConfig["input_player" + i + "_b_btn"];
                    string c_padbutton = retroarchConfig["input_player" + i + "_y_btn"];
                    string start_padbutton = retroarchConfig["input_player" + i + "_start_btn"];
                    string select_padbutton = retroarchConfig["input_player" + i + "_select_btn"];
                    string up_padbutton = retroarchConfig["input_player" + i + "_up_btn"];
                    string down_padbutton = retroarchConfig["input_player" + i + "_down_btn"];
                    string left_padbutton = retroarchConfig["input_player" + i + "_left_btn"];
                    string right_padbutton = retroarchConfig["input_player" + i + "_right_btn"];

                    int deviceIndex = guns[i - 1].Index; // i-1;

                    SimpleLogger.Instance.Debug("[LightGun] Assigned player " + i + " to -> " + guns[i - 1].ToString());

                    retroarchConfig["input_libretro_device_p" + i] = deviceType;
                    retroarchConfig["input_player" + i + "_mouse_index"] = deviceIndex.ToString();
                    retroarchConfig["input_player" + i + "_gun_trigger_mbtn"] = guninvert ? "2" : "1";
                    retroarchConfig["input_player" + i + "_gun_offscreen_shot_mbtn"] = guninvert ? "1" : "2";
                    retroarchConfig["input_player" + i + "_gun_start_mbtn"] = "3";

                    // Assign gamepad buttons to gun buttons
                    if (select_padbutton != "" && select_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_select_btn"] = select_padbutton;
                    if (start_padbutton != "" && start_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_start_btn"] = start_padbutton;
                    if (a_padbutton != "" && a_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_aux_a_btn"] = a_padbutton;
                    if (b_padbutton != "" && b_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_aux_b_btn"] = b_padbutton;
                    if (c_padbutton != "" && c_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_aux_c_btn"] = c_padbutton;
                    if (up_padbutton != "" && up_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_dpad_up_btn"] = up_padbutton;
                    if (down_padbutton != "" && down_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_dpad_down_btn"] = down_padbutton;
                    if (left_padbutton != "" && left_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_dpad_left_btn"] = left_padbutton;
                    if (right_padbutton != "" && right_padbutton != null)
                        retroarchConfig["input_player" + i + "_gun_dpad_right_btn"] = right_padbutton;

                    retroarchConfig["input_player" + i + "_analog_dpad_mode"] = "0";
                    retroarchConfig["input_player" + i + "_joypad_index"] = deviceIndex.ToString();
                }
            }

            if (guns.Length <= 16)
            {
                for (int i = guns.Length + 1; i == 16; i++)
                {
                    foreach (string cfg in gunButtons)
                        retroarchConfig["input_player" + i + cfg] = "nul";
                }
            }

            // Set additional buttons gun mapping default
            ConfigureLightgunKeyboardActions(retroarchConfig, deviceType, playerIndex, core, multigun, guninvert, useOneGun);
        }

        /// <summary>
        /// Injects keyboard actions for lightgun games
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="playerId"></param>
        private void ConfigureLightgunKeyboardActions(ConfigFile retroarchConfig, string deviceType, int playerIndex, string core, bool multigun, bool guninvert, bool useOneGun)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            if (!coreGunConfig.Contains(core))
            {
                var keyb = Controllers.Where(c => c.Name == "Keyboard" && c.Config != null && c.Config.Input != null).Select(c => c.Config).FirstOrDefault();
                if (keyb != null)
                {
                    var start = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.start);
                    retroarchConfig["input_player" + playerIndex + "_gun_start"] = start == null ? "nul" : LibretroControllers.GetConfigValue(start);

                    var select = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.select);
                    retroarchConfig["input_player" + playerIndex + "_gun_select"] = select == null ? "nul" : LibretroControllers.GetConfigValue(select);

                    var aux_a = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.b);
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = aux_a == null ? "nul" : LibretroControllers.GetConfigValue(aux_a);

                    var aux_b = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.a);
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = aux_b == null ? "nul" : LibretroControllers.GetConfigValue(aux_b);

                    var aux_c = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.y);
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_c"] = aux_c == null ? "nul" : LibretroControllers.GetConfigValue(aux_c);

                    var dpad_up = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.up);
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_up"] = dpad_up == null ? "nul" : LibretroControllers.GetConfigValue(dpad_up);

                    var dpad_down = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.down);
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_down"] = dpad_down == null ? "nul" : LibretroControllers.GetConfigValue(dpad_down);

                    var dpad_left = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.left);
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_left"] = dpad_left == null ? "nul" : LibretroControllers.GetConfigValue(dpad_left);

                    var dpad_right = keyb.Input.FirstOrDefault(i => i.Name == Tools.InputKey.right);
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_right"] = dpad_right == null ? "nul" : LibretroControllers.GetConfigValue(dpad_right);
                }
                else
                {
                    retroarchConfig["input_player" + playerIndex + "_gun_start"] = "enter";
                    retroarchConfig["input_player" + playerIndex + "_gun_select"] = "backspace";
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = "w";
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = "x";
                    retroarchConfig["input_player" + playerIndex + "_gun_aux_c"] = "s";
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_up"] = "up";
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_down"] = "down";
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_left"] = "left";
                    retroarchConfig["input_player" + playerIndex + "_gun_dpad_right"] = "right";
                }
            }
            
            // Configure core specific mappings            
            else
                ConfigureGunsCore(retroarchConfig, playerIndex, core, deviceType, multigun, guninvert, useOneGun);
        }

        // Mednafen psx mapping
        // GunCon buttons : trigger, A, B (offscreen reload)

        private void ConfigureGunsCore(ConfigFile retroarchConfig, int playerIndex, string core, string deviceType, bool multigun = false, bool guninvert = false, bool useOneGun = false)
        {
            if (SystemConfig.isOptSet("gun_type") && !string.IsNullOrEmpty(SystemConfig["gun_type"]))
                deviceType = SystemConfig["gun_type"];

            var guns = RawLightgun.GetRawLightguns();
            if (guns.Length == 0)
                return;

            if (useOneGun)
            {
                int deviceIndex = guns[0].Index;

                SimpleLogger.Instance.Debug("[LightGun] Assigned player " + playerIndex + " to -> " + guns[0].ToString());

                retroarchConfig["input_libretro_device_p" + playerIndex] = deviceType;
                retroarchConfig["input_player" + playerIndex + "_mouse_index"] = deviceIndex.ToString();

                retroarchConfig["input_player" + playerIndex + "_gun_trigger_mbtn"] = guninvert ? "2" : "1";
                retroarchConfig["input_player" + playerIndex + "_gun_offscreen_shot_mbtn"] = GetcoreMouseButton(core, guninvert, "reload");
                retroarchConfig["input_player" + playerIndex + "_gun_aux_a_mbtn"] = GetcoreMouseButton(core, guninvert, "aux_a");
                retroarchConfig["input_player" + playerIndex + "_gun_aux_b_mbtn"] = GetcoreMouseButton(core, guninvert, "aux_b");
                retroarchConfig["input_player" + playerIndex + "_gun_aux_c_mbtn"] = GetcoreMouseButton(core, guninvert, "aux_c");
                retroarchConfig["input_player" + playerIndex + "_gun_start_mbtn"] = GetcoreMouseButton(core, guninvert, "start");
                retroarchConfig["input_player" + playerIndex + "_gun_select_mbtn"] = GetcoreMouseButton(core, guninvert, "select");

                retroarchConfig["input_player" + playerIndex + "_analog_dpad_mode"] = "0";
                retroarchConfig["input_player" + playerIndex + "_joypad_index"] = deviceIndex.ToString();

                var ctrl = Controllers.Where(c => c.Name != "Keyboard" && c.Config != null && c.Config.Input != null).Select(c => c.Config).FirstOrDefault();

                if (playerIndex == 1 || ctrl != null)
                {
                    retroarchConfig["input_player" + playerIndex + "_gun_start"] = "enter";
                    retroarchConfig["input_player" + playerIndex + "_gun_select"] = "backspace";

                    if (SystemConfig.isOptSet("gun_ab") && SystemConfig["gun_ab"] == "directions")
                    {
                        retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = "left";
                        retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = "right";
                        retroarchConfig["input_player" + playerIndex + "_gun_aux_c"] = "up";
                    }
                    else
                    {
                        var keyb = Controllers.Where(c => c.Name == "Keyboard" && c.Config != null && c.Config.Input != null).Select(c => c.Config).FirstOrDefault();
                        if (keyb != null)
                        {
                            var aux_a = keyb.Input.FirstOrDefault(k => k.Name == Tools.InputKey.b);
                            retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = aux_a == null ? "nul" : LibretroControllers.GetConfigValue(aux_a);

                            var aux_b = keyb.Input.FirstOrDefault(k => k.Name == Tools.InputKey.a);
                            retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = aux_b == null ? "nul" : LibretroControllers.GetConfigValue(aux_b);

                            var aux_c = keyb.Input.FirstOrDefault(k => k.Name == Tools.InputKey.y);
                            retroarchConfig["input_player" + playerIndex + "_gun_aux_c"] = aux_c == null ? "nul" : LibretroControllers.GetConfigValue(aux_c);
                        }
                        else
                        {
                            retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = "w";
                            retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = "x";
                            retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = "s";
                        }
                    }
                }
            }

            else
            {
                for (int i = 1; i <= guns.Length; i++)
                {
                    int deviceIndex = guns[i - 1].Index; // i-1;

                    SimpleLogger.Instance.Debug("[LightGun] Assigned player " + i + " to -> " + guns[i - 1].ToString());

                    retroarchConfig["input_libretro_device_p" + i] = deviceType;
                    retroarchConfig["input_player" + i + "_mouse_index"] = deviceIndex.ToString();

                    retroarchConfig["input_player" + i + "_gun_trigger_mbtn"] = guninvert ? "2" : "1";
                    retroarchConfig["input_player" + i + "_gun_offscreen_shot_mbtn"] = GetcoreMouseButton(core, guninvert, "reload");
                    retroarchConfig["input_player" + i + "_gun_aux_a_mbtn"] = GetcoreMouseButton(core, guninvert, "aux_a");
                    retroarchConfig["input_player" + i + "_gun_aux_b_mbtn"] = GetcoreMouseButton(core, guninvert, "aux_b");
                    retroarchConfig["input_player" + i + "_gun_aux_c_mbtn"] = GetcoreMouseButton(core, guninvert, "aux_c");
                    retroarchConfig["input_player" + i + "_gun_start_mbtn"] = GetcoreMouseButton(core, guninvert, "start");
                    retroarchConfig["input_player" + i + "_gun_select_mbtn"] = GetcoreMouseButton(core, guninvert, "select");

                    retroarchConfig["input_player" + i + "_analog_dpad_mode"] = "0";
                    retroarchConfig["input_player" + i + "_joypad_index"] = deviceIndex.ToString();

                    if (i == 1)
                    {
                        retroarchConfig["input_player" + i + "_gun_start"] = "enter";
                        retroarchConfig["input_player" + i + "_gun_select"] = "backspace";

                        if (SystemConfig.isOptSet("gun_ab") && SystemConfig["gun_ab"] == "directions")
                        {
                            retroarchConfig["input_player1_gun_aux_a"] = "left";
                            retroarchConfig["input_player1_gun_aux_b"] = "right";
                            retroarchConfig["input_player1_gun_aux_c"] = "up";
                        }
                        else
                        {
                            var keyb = Controllers.Where(c => c.Name == "Keyboard" && c.Config != null && c.Config.Input != null).Select(c => c.Config).FirstOrDefault();
                            if (keyb != null)
                            {
                                var aux_a = keyb.Input.FirstOrDefault(k => k.Name == Tools.InputKey.b);
                                retroarchConfig["input_player1_gun_aux_a"] = aux_a == null ? "nul" : LibretroControllers.GetConfigValue(aux_a);

                                var aux_b = keyb.Input.FirstOrDefault(k => k.Name == Tools.InputKey.a);
                                retroarchConfig["input_player1_gun_aux_b"] = aux_b == null ? "nul" : LibretroControllers.GetConfigValue(aux_b);

                                var aux_c = keyb.Input.FirstOrDefault(k => k.Name == Tools.InputKey.y);
                                retroarchConfig["input_player1_gun_aux_c"] = aux_c == null ? "nul" : LibretroControllers.GetConfigValue(aux_c);
                            }
                            else
                            {
                                retroarchConfig["input_player1_gun_aux_a"] = "w";
                                retroarchConfig["input_player1_gun_aux_b"] = "x";
                                retroarchConfig["input_player1_gun_aux_b"] = "s";
                            }
                        }
                    }
                }
            }
        }

        static List<string> gunButtons = new List<string>()
        {
            "_mouse_index",
            "_gun_aux_a",
            "_gun_aux_a_axis",
            "_gun_aux_a_btn",
            "_gun_aux_a_mbtn",
            "_gun_aux_b",
            "_gun_aux_b_axis",
            "_gun_aux_b_btn",
            "_gun_aux_b_mbtn",
            "_gun_aux_c",
            "_gun_aux_c_axis",
            "_gun_aux_c_btn",
            "_gun_aux_c_mbtn",
            "_gun_dpad_down",
            "_gun_dpad_down_axis",
            "_gun_dpad_down_btn",
            "_gun_dpad_down_mbtn",
            "_gun_dpad_left",
            "_gun_dpad_left_axis",
            "_gun_dpad_left_btn",
            "_gun_dpad_left_mbtn",
            "_gun_dpad_right",
            "_gun_dpad_right_axis",
            "_gun_dpad_right_btn",
            "_gun_dpad_right_mbtn",
            "_gun_dpad_up",
            "_gun_dpad_up_axis",
            "_gun_dpad_up_btn",
            "_gun_dpad_up_mbtn",
            "_gun_offscreen_shot",
            "_gun_offscreen_shot_axis",
            "_gun_offscreen_shot_btn",
            "_gun_offscreen_shot_mbtn",
            "_gun_select",
            "_gun_select_axis",
            "_gun_select_btn",
            "_gun_select_mbtn",
            "_gun_start",
            "_gun_start_axis",
            "_gun_start_btn",
            "_gun_start_mbtn",
            "_gun_trigger",
            "_gun_trigger_axis",
            "_gun_trigger_btn",
            "_gun_trigger_mbtn"
        };

        private string GetcoreMouseButton (string core, bool guninvert, string mbtn)
        {
            bool changeReload = SystemConfig.isOptSet("gun_reload_button") && SystemConfig.getOptBoolean("gun_reload_button");
            string ret = "nul";

            switch (mbtn)
            {
                case "reload":
                    if (coreDefaultMouseReloadButton.ContainsKey(core))
                        ret = changeReload ? "2" : coreDefaultMouseReloadButton[core];
                    else
                        ret = "2";
                    break;
                case "aux_a":
                    if (coreChangeReloadMouseAuxAButton.ContainsKey(core) && coreDefaultMouseAuxAButton.ContainsKey(core))
                        ret = changeReload ? coreChangeReloadMouseAuxAButton[core] : coreDefaultMouseAuxAButton[core];
                    else
                        ret = "3";
                    break;
                case "aux_b":
                    if (coreChangeReloadMouseAuxBButton.ContainsKey(core) && coreDefaultMouseAuxBButton.ContainsKey(core))
                        ret = changeReload ? coreChangeReloadMouseAuxBButton[core] : coreDefaultMouseAuxBButton[core];
                    else
                        ret = "nul";
                    break;
                case "aux_c":
                    if (coreChangeReloadMouseAuxCButton.ContainsKey(core) && coreDefaultMouseAuxCButton.ContainsKey(core))
                        ret = changeReload ? coreChangeReloadMouseAuxCButton[core] : coreDefaultMouseAuxCButton[core];
                    else
                        ret = "nul";
                    break;
                case "start":
                    if (coreChangeReloadMouseStartButton.ContainsKey(core) && coreDefaultMouseStartButton.ContainsKey(core))
                        ret = changeReload ? coreChangeReloadMouseStartButton[core] : coreDefaultMouseStartButton[core];
                    else
                        ret = "nul";
                    break;
                case "select":
                    if (coreChangeReloadMouseSelectButton.ContainsKey(core) && coreDefaultMouseSelectButton.ContainsKey(core))
                        ret = changeReload ? coreChangeReloadMouseSelectButton[core] : coreDefaultMouseSelectButton[core];
                    else
                        ret = "nul";
                    break;
            }

            if (ret == "2" && guninvert)
                return "1";
            else
                return ret;
        }

        // Set all dictionnaries for mouse buttons (2 dictionaries for each button, one for default value, one for value when reload is forced on mouse rightclick)
        static Dictionary<string, string> coreDefaultMouseReloadButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseAuxAButton = new Dictionary<string, string>()
        {
            { "fbneo",              "2" },
            { "flycast",            "2" },
            { "mednafen_psx",       "2" },
            { "mednafen_psx_hw",    "2" },
            { "pcsx_rearmed",       "2" },
            { "swanstation",        "2" }
        };

        static Dictionary<string, string> coreChangeReloadMouseAuxAButton = new Dictionary<string, string>()
        {
            { "fbneo",              "3" },
            { "flycast",            "3" },
            { "mednafen_psx",       "3" },
            { "mednafen_psx_hw",    "3" },
            { "pcsx_rearmed",       "3" },
            { "swanstation",        "3" }
        };

        static Dictionary<string, string> coreDefaultMouseAuxBButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "3" },
            { "mednafen_psx_hw",    "3" },
            { "pcsx_rearmed",       "3" },
            { "swanstation",        "3" }
        };

        static Dictionary<string, string> coreChangeReloadMouseAuxBButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseAuxCButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreChangeReloadMouseAuxCButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseStartButton = new Dictionary<string, string>()
        {
            { "fbneo",              "3" },
            { "flycast",            "3" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreChangeReloadMouseStartButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseSelectButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };

        static Dictionary<string, string> coreChangeReloadMouseSelectButton = new Dictionary<string, string>()
        {
            { "fbneo",              "nul" },
            { "flycast",            "nul" },
            { "mednafen_psx",       "nul" },
            { "mednafen_psx_hw",    "nul" },
            { "pcsx_rearmed",       "nul" },
            { "swanstation",        "nul" }
        };
    }
}