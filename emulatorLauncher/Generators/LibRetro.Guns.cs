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
        // List of cores with dedicated configuration
        // To be updated each time a core is correctly / succesfully configured
        static List<string> coreGunConfig = new List<string>()
        {
            "4do",
            "bsnes",
            "bsnes_hd_beta",
            "cap32",
            "fceumm",
            "flycast",
            "genesis_plus_gx",
            "genesis_plus_gx_wide",
            "kronos",
            "mednafen_psx",
            "mednafen_psx_hw",
            "mednafen_saturn",
            "mesen",
            "mesen-s",
            "nestopia",
            "opera",
            "pcsx_rearmed",
            "snes9x",
            "snes9x_next",
            "stella",
            "swanstation",          // Has some issues, works with the right combination of video driver and aspect ratio
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

            // Used in some specific cases to invert trigger and reload buttons (for example with wiizapper)
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");

            // Force to use only one gun even when multiple gun devices / mouses are connected
            bool useOneGun = SystemConfig.isOptSet("one_gun") && SystemConfig.getOptBoolean("one_gun");

            int gunCount = RawLightgun.GetUsableLightGunCount();
            var guns = RawLightgun.GetRawLightguns();

            // Set multigun to true in some cases
            // Case 1 = multiple guns are connected, playerindex is 1 and user did not force 'one gun only'
            if (gunCount > 1 && guns.Length > 1 && playerIndex == 1 && !useOneGun)
                multigun = true;

            // Single player - assign buttons of joystick linked with playerIndex to gun buttons
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

            // Multigun case
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

            // Clean up unused mapping after last gun used
            if (useOneGun)
            {
                // If playerindex is 2, nullify player 1 gun buttons
                if (playerIndex == 2)
                {
                    foreach (string cfg in gunButtons)
                        retroarchConfig["input_player1" + cfg] = "nul";
                }

                // Nullify all buttons after playerindex
                if (guns.Length <= 16)
                {
                    for (int i = playerIndex + 1; i == 16; i++)
                    {
                        foreach (string cfg in gunButtons)
                            retroarchConfig["input_player" + i + cfg] = "nul";
                    }
                }
            }
            else
            {
                // Nullify all buttons after guns.length
                if (guns.Length <= 16)
                {
                    for (int i = guns.Length + 1; i == 16; i++)
                    {
                        foreach (string cfg in gunButtons)
                            retroarchConfig["input_player" + i + cfg] = "nul";
                    }
                }
            }


            // Set additional buttons gun mapping default ...
            if (!coreGunConfig.Contains(core))
                ConfigureLightgunKeyboardActions(retroarchConfig, playerIndex);
            
            // ... or configure core specific mappings            
            else
                ConfigureGunsCore(retroarchConfig, playerIndex, core, deviceType, multigun, guninvert, useOneGun);
        }

        /// <summary>
        /// Injects keyboard actions for lightgun games
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="playerIndex"></param>
        private void ConfigureLightgunKeyboardActions(ConfigFile retroarchConfig, int playerIndex)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

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

        /// <summary>
        /// Dedicated core mappings for lightgun games
        private void ConfigureGunsCore(ConfigFile retroarchConfig, int playerIndex, string core, string deviceType, bool multigun = false, bool guninvert = false, bool useOneGun = false)
        {
            // Some systems offer multiple type of guns (justifier, guncon...). Option must be available in es_features.cfg
            if (SystemConfig.isOptSet("gun_type") && !string.IsNullOrEmpty(SystemConfig["gun_type"]))
                deviceType = SystemConfig["gun_type"];

            var guns = RawLightgun.GetRawLightguns();
            if (guns.Length == 0)
                return;

            // If option in ES is forced to use one gun, only one gun will be configured on the playerIndex defined for the core
            if (useOneGun || playerIndex == 2)
            {
                // First gun will be used
                int deviceIndex = guns[0].Index;

                SimpleLogger.Instance.Debug("[LightGun] Assigned player " + playerIndex + " to -> " + guns[0].ToString());

                // Set deviceType and DeviceIndex
                retroarchConfig["input_libretro_device_p" + playerIndex] = deviceType;
                retroarchConfig["input_player" + playerIndex + "_mouse_index"] = deviceIndex.ToString();

                // Set mouse buttons (mouse only has 3 buttons, that can be mapped differently for each core)
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

                // For first player (or player 2 if playerindex is 2 and player 1 has a gamepad), we set keyboard keys to auxiliary gun buttons
                if (playerIndex == 1 || ctrl != null)
                {
                    // Start always set to enter and backspace
                    retroarchConfig["input_player" + playerIndex + "_gun_start"] = "enter";
                    retroarchConfig["input_player" + playerIndex + "_gun_select"] = "backspace";

                    // Auxiliary buttons can be set to directions if using a wiimote
                    if (SystemConfig.isOptSet("gun_ab") && SystemConfig["gun_ab"] == "directions")
                    {
                        retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = "left";
                        retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = "right";
                        retroarchConfig["input_player" + playerIndex + "_gun_aux_c"] = "up";
                    }
                    // By default auxiliary buttons are set to keys defined in es_input for the keyboard
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

            // Multigun case
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

        // List of retroarch.cfg gun input lines (used to clean up)
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

        // Rule to get mouse button assignment for each core, based on dictionaries
        // 2 type of cases : 
        // 1 - Classic case ==> mouse left = trigger
        // 2 - reverse case ==> mouse right = trigger
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
            { "4do",                    "2" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "2" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "2" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "2" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "2" },
            { "mesen",                  "2" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "2" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "2" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseAuxAButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "2" },
            { "bsnes_hd_beta",          "2" },
            { "cap32",                  "nul" },
            { "fbneo",                  "2" },
            { "fceumm",                 "nul" },
            { "flycast",                "2" },
            { "genesis_plus_gx",        "2" },
            { "genesis_plus_gx_wide",   "2" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "2" },
            { "mednafen_psx_hw",        "2" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "2" },
            { "nestopia",               "nul" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "2" },
            { "snes9x",                 "2" },
            { "snes9x_next",            "2" },
            { "stella",                 "nul" },
            { "swanstation",            "2" }
        };

        static Dictionary<string, string> coreChangeReloadMouseAuxAButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "3" },
            { "bsnes_hd_beta",          "3" },
            { "cap32",                  "nul" },
            { "fbneo",                  "3" },
            { "fceumm",                 "nul" },
            { "flycast",                "3" },
            { "genesis_plus_gx",        "3" },
            { "genesis_plus_gx_wide",   "3" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "3" },
            { "mednafen_psx_hw",        "3" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "3" },
            { "nestopia",               "nul" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "3" },
            { "snes9x",                 "3" },
            { "snes9x_next",            "3" },
            { "stella",                 "nul" },
            { "swanstation",            "3" }
        };

        static Dictionary<string, string> coreDefaultMouseAuxBButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "3" },
            { "bsnes_hd_beta",          "3" },
            { "cap32",                  "nul" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "nul" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "3" },
            { "genesis_plus_gx_wide",   "3" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "3" },
            { "mednafen_psx_hw",        "3" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "3" },
            { "nestopia",               "2" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "3" },
            { "snes9x",                 "3" },
            { "snes9x_next",            "3" },
            { "stella",                 "nul" },
            { "swanstation",            "3" }
        };

        static Dictionary<string, string> coreChangeReloadMouseAuxBButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "nul" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "nul" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "nul" },
            { "nestopia",               "3" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "nul" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseAuxCButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "nul" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "nul" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "nul" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreChangeReloadMouseAuxCButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "nul" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "nul" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "nul" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseStartButton = new Dictionary<string, string>()
        {
            { "4do",                    "3" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "3" },
            { "fbneo",                  "3" },
            { "fceumm",                 "3" },
            { "flycast",                "3" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "3" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "3" },
            { "mesen",                  "3" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "3" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "3" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreChangeReloadMouseStartButton = new Dictionary<string, string>()
        {
            { "4do",                    "3" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "3" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "3" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "3" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "3" },
            { "mesen",                  "3" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "3" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "3" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreDefaultMouseSelectButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "nul" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "nul" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "nul" },
            { "swanstation",            "nul" }
        };

        static Dictionary<string, string> coreChangeReloadMouseSelectButton = new Dictionary<string, string>()
        {
            { "4do",                    "nul" },
            { "bsnes",                  "nul" },
            { "bsnes_hd_beta",          "nul" },
            { "cap32",                  "nul" },
            { "fbneo",                  "nul" },
            { "fceumm",                 "nul" },
            { "flycast",                "nul" },
            { "genesis_plus_gx",        "nul" },
            { "genesis_plus_gx_wide",   "nul" },
            { "kronos",                 "nul" },
            { "mednafen_psx",           "nul" },
            { "mednafen_psx_hw",        "nul" },
            { "mednafen_saturn",        "nul" },
            { "mesen",                  "nul" },
            { "mesen-s",                "nul" },
            { "nestopia",               "nul" },
            { "opera",                  "nul" },
            { "pcsx_rearmed",           "nul" },
            { "snes9x",                 "nul" },
            { "snes9x_next",            "nul" },
            { "stella",                 "nul" },
            { "swanstation",            "nul" }
        };
    }
}