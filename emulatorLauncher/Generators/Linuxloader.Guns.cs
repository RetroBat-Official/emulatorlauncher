using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class LinuxloaderGenerator
    {
        private bool _demulshooter = false;

        private void ConfigureLindberghGuns(IniFile ini, string system, bool autoconfOff = false)
        {
            bool configGuns = SystemConfig.getOptBoolean("use_guns");
            bool gunindexrevert = SystemConfig.getOptBoolean("gun_index_revert");

            var guns = RawLightgun.GetRawLightguns();

            if (configGuns)
                ConfigureGunButtons(ini, guns, autoconfOff);

            if (guns.Length < 1)
                return;
            else
                SimpleLogger.Instance.Info("[GUNS] Found " + guns.Length + " usable guns.");

            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
            }

            // If DemulShooter is enabled, configure it
            if (SystemConfig.getOptBoolean("use_demulshooter"))
            {
                _demulshooter = true;
                SimpleLogger.Instance.Info("[INFO] Configuring DemulShooter");
                var gun1 = guns.Length > 0 ? guns[0] : null;
                var gun2 = guns.Length > 1 ? guns[1] : null;
                var gun3 = guns.Length > 2 ? guns[2] : null;
                var gun4 = guns.Length > 3 ? guns[3] : null;

                if (gunindexrevert)
                {
                    if (guns.Length > 1)
                    {
                        if (guns.Length >= 3)
                        {
                            gun1 = guns[1];
                            gun2 = guns[2];
                            gun3 = guns[0];
                            gun4 = guns.Length > 3 ? guns[3] : null;
                        }
                        else
                        {
                            gun1 = guns[1];
                            gun2 = guns[0];
                        }
                    }
                }

                Demulshooter.StartDemulshooter("linuxloader", system, _romName, gun1, gun2, gun3, gun4);
                return;
            }
        }

        private static readonly List<string> buttonsToMap = new List<string> { "ll_gun_reload", "ll_gun_button", "ll_gun_action" };

        private void ConfigureGunButtons(IniFile ini, RawLightgun[] guns, bool autoconfOff = false)
        {
            // Common section
            if (autoconfOff)
            {
                ini.WriteValue("Common", "ExitGame", "KEY_Escape");
                ini.WriteValue("Common", "P1_Coin", "KEY_5");
                ini.WriteValue("Common", "P2_Coin", "KEY_6");
                ini.WriteValue("Common", "P1_Start", "KEY_1");
                ini.WriteValue("Common", "P2_Start", "KEY_2");
            }

            string triggerButton = "MOUSE_LEFT_BUTTON";
            string reloadButton = "MOUSE_RIGHT_BUTTON";
            string gunButton = "MOUSE_MIDDLE_BUTTON";
            string actionButton = "KEY_1";
            string pedalLeft = "KEY_Left";
            string pedalRight = "KEY_Right";

            foreach (string button in buttonsToMap)
            {
                if (SystemConfig.isOptSet(button) && !string.IsNullOrEmpty(SystemConfig[button]))
                {
                    string mappedButton = SystemConfig[button];
                    switch (button)
                    {
                        case "ll_gun_reload":
                            reloadButton = mappedButton;
                            break;
                        case "ll_gun_button":
                            gunButton = mappedButton;
                            break;
                        case "ll_gun_action":
                            actionButton = mappedButton;
                            break;
                    }
                }
            }

            // Swap left and right clicks (e.g. Wiimote with Nunchuk or WiiZapper on a DolphinBar)
            if (SystemConfig.getOptBoolean("gun_invert"))
            {
                triggerButton = "MOUSE_RIGHT_BUTTON";
                reloadButton = SwapMouseButtons(reloadButton);
                gunButton = SwapMouseButtons(gunButton);
                actionButton = SwapMouseButtons(actionButton);
            }

            // DolphinBar Wiimotes (mode 1 or 2) send keyboard keys. Linuxloader reads all keyboards as one,
            // so player 2 uses other directions. Pedals are moved away from Left/Right (used by start/coin).
            int wiimotes = guns.Count(g => g.Type == RawLighGunType.MayFlashWiimote);
            if (wiimotes > 0)
            {
                bool wiimoteNormal = SystemConfig["WiimoteMode"] == "normal";
                string extraKey = wiimoteNormal ? "KEY_PageUp" : "KEY_Return";
                bool gunButtonDefault = !SystemConfig.isOptSet("ll_gun_button") || string.IsNullOrEmpty(SystemConfig["ll_gun_button"]);

                AppendInput(ini, "Common", "P1_Start", "KEY_Right");
                AppendInput(ini, "Common", "P1_Coin", "KEY_Left");

                // Action button follows start when it is mapped on start key
                if (actionButton == "KEY_1")
                    actionButton = "KEY_1, KEY_Right";

                if (wiimotes == 1)
                {
                    if (gunButtonDefault)
                        gunButton = "KEY_Down";

                    pedalLeft = "KEY_Up";
                    pedalRight = extraKey;
                }
                else
                {
                    AppendInput(ini, "Common", "P2_Start", "KEY_Up");
                    AppendInput(ini, "Common", "P2_Coin", "KEY_Down");

                    if (gunButtonDefault)
                        gunButton = extraKey;

                    pedalLeft = wiimoteNormal ? "KEY_PageDown" : "";
                    pedalRight = "";
                }
            }

            // Optional auto-centering of the gun aiming axis (useful when using a gamepad instead of a lightgun)
            if (SystemConfig.isOptSet("ll_gun_centering") && !string.IsNullOrEmpty(SystemConfig["ll_gun_centering"]))
            {
                string centering = SystemConfig.getOptBoolean("ll_gun_centering") ? "1" : "0";
                ini.WriteValue("Config", "GunX_Centering", centering);
                ini.WriteValue("Config", "GunY_Centering", centering);
            }

            if (SystemConfig.isOptSet("ll_gunaxis_invert") && !string.IsNullOrEmpty(SystemConfig["ll_gunaxis_invert"]))
            {
                string invertAxis = SystemConfig["ll_gunaxis_invert"].ToLower();
                if (invertAxis == "x")
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X_INVERTED");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
                }
                else if (invertAxis == "y")
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y_INVERTED");
                }
                else if (invertAxis == "both")
                {
                    ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X_INVERTED");
                    ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y_INVERTED");
                }
            }
            else
            {
                ini.WriteValue("Shooting", "P1_GunX", "MOUSE_AXIS_X");
                ini.WriteValue("Shooting", "P1_GunY", "MOUSE_AXIS_Y");
            }

            ini.WriteValue("Shooting", "P1_Trigger", triggerButton);
            ini.WriteValue("Shooting", "P1_Reload", reloadButton);
            ini.WriteValue("Shooting", "P1_GunButton", gunButton);
            ini.WriteValue("Shooting", "P1_ActionButton", actionButton);
            ini.WriteValue("Shooting", "P1_PedalLeft", pedalLeft);
            ini.WriteValue("Shooting", "P1_PedalRight", pedalRight);
        }

        private static string SwapMouseButtons(string input)
        {
            if (input == "MOUSE_LEFT_BUTTON")
                return "MOUSE_RIGHT_BUTTON";
            if (input == "MOUSE_RIGHT_BUTTON")
                return "MOUSE_LEFT_BUTTON";
            return input;
        }

        /// <summary>
        /// Adds an input to a comma separated binding list (controls.ini), without duplicates.
        /// </summary>
        private static void AppendInput(IniFile ini, string section, string key, string input)
        {
            string value = ini.GetValue(section, key);
            if (string.IsNullOrEmpty(value))
            {
                ini.WriteValue(section, key, input);
                return;
            }

            var inputs = value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
            if (!inputs.Contains(input))
                inputs.Add(input);

            ini.WriteValue(section, key, string.Join(", ", inputs));
        }
    }
}
