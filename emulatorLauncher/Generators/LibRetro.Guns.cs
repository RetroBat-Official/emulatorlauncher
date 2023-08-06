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

            retroarchConfig["input_libretro_device_p" + playerIndex] = deviceType;
            retroarchConfig["input_player" + playerIndex + "_mouse_index"] = "0";
            retroarchConfig["input_player" + playerIndex + "_gun_trigger_mbtn"] = "1";
            retroarchConfig["input_player" + playerIndex + "_gun_offscreen_shot_mbtn"] = "2";
            retroarchConfig["input_player" + playerIndex + "_gun_start_mbtn"] = "3";

            retroarchConfig["input_player" + playerIndex + "_analog_dpad_mode"] = "0";
            retroarchConfig["input_player" + playerIndex + "_joypad_index"] = "0";

            if (playerIndex > 1)
                return; // If device has to be set as player 2, then exit, there's no multigun support

            ConfigureLightgunKeyboardActions(retroarchConfig, playerIndex, core);

            int gunCount = RawLightgun.GetUsableLightGunCount();
            if (gunCount <= 1) // If there's only one gun ( or just one sinden gun + one mouse ), then ignore multigun
                return;

            var guns = RawLightgun.GetRawLightguns();
            if (guns.Length <= 1)
                return;

            // DirectInput does not differenciate mouse indexes. We have to use "Raw" with multiple guns
            retroarchConfig["input_driver"] = "raw";

            for (int i = 1; i <= guns.Length; i++)
            {
                int deviceIndex = guns[i - 1].Index; // i-1;

                SimpleLogger.Instance.Debug("[LightGun] Assigned player " + i + " to -> " + guns[i - 1].ToString());

                retroarchConfig["input_libretro_device_p" + i] = deviceType;
                retroarchConfig["input_player" + i + "_mouse_index"] = deviceIndex.ToString();
                retroarchConfig["input_player" + i + "_gun_trigger_mbtn"] = "1";
                retroarchConfig["input_player" + i + "_gun_offscreen_shot_mbtn"] = "2";
                retroarchConfig["input_player" + i + "_gun_start_mbtn"] = "3";

                retroarchConfig["input_player" + i + "_analog_dpad_mode"] = "0";
                retroarchConfig["input_player" + i + "_joypad_index"] = deviceIndex.ToString();
            }
        }

        /// <summary>
        /// Injects keyboard actions for lightgun games
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="playerId"></param>
        private void ConfigureLightgunKeyboardActions(ConfigFile retroarchConfig, int playerIndex, string core)
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
                retroarchConfig["input_player" + playerIndex + "_gun_select"] = "space";
                retroarchConfig["input_player" + playerIndex + "_gun_aux_a"] = "w";
                retroarchConfig["input_player" + playerIndex + "_gun_aux_b"] = "x";
                retroarchConfig["input_player" + playerIndex + "_gun_aux_c"] = "s";
                retroarchConfig["input_player" + playerIndex + "_gun_dpad_up"] = "up";
                retroarchConfig["input_player" + playerIndex + "_gun_dpad_down"] = "down";
                retroarchConfig["input_player" + playerIndex + "_gun_dpad_left"] = "left";
                retroarchConfig["input_player" + playerIndex + "_gun_dpad_right"] = "right";
            }

            // Configure core specific mappings
            ConfigureGunsMednafenPSX(retroarchConfig, playerIndex, core);
        }

        private void ConfigureGunsMednafenPSX(ConfigFile retroarchConfig, int playerIndex, string core)
        {
            if (core != "mednafen_psx_hw")
                return;
        }
    }
}