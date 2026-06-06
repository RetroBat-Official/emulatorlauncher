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

        /// <summary>
        /// If controller autoconfig is off but use_guns is on.
        /// </summary>
        /// <param name="cfgPath"></param>
        /// <param name="system"></param>
        private void ConfigureLindberghGunsAutoOff(string cfgPath, string system)
        {
            SimpleLogger.Instance.Info("[INFO] Configuring guns.");

            using (var ini = new IniFile(cfgPath, IniOptions.UseSpaces))
            {
                ConfigureLindberghGuns(ini, "lindbergh", true);
                
                ini.Save();
            }
        }

        private void ConfigureLindberghGuns(IniFile ini, string system, bool autoconfOff = false)
        {
            bool configGuns = SystemConfig.getOptBoolean("use_guns");
            bool guninvert = SystemConfig.getOptBoolean("gun_invert");
            bool gunindexrevert = SystemConfig.getOptBoolean("gun_index_revert");

            if (configGuns)
                ConfigureGunButtons(ini, autoconfOff);

            var guns = RawLightgun.GetRawLightguns();

            if (guns.Length < 1)
                return;
            else
                SimpleLogger.Instance.Info("[GUNS] Found " + guns.Length + " usable guns.");

            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
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

        private void ConfigureGunButtons(IniFile ini, bool autoconfOff = false)
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

            string reloadButton = "MOUSE_RIGHT_BUTTON";
            string gunButton = "MOUSE_MIDDLE_BUTTON";
            string actionButton = "KEY_1";

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

            ini.WriteValue("Shooting", "P1_Trigger", "MOUSE_LEFT_BUTTON");
            ini.WriteValue("Shooting", "P1_Reload", reloadButton);
            ini.WriteValue("Shooting", "P1_GunButton", gunButton);
            ini.WriteValue("Shooting", "P1_ActionButton", actionButton);
            ini.WriteValue("Shooting", "P1_PedalLeft", "KEY_Left");
            ini.WriteValue("Shooting", "P1_PedalRight", "KEY_Right");
        }
    }
}
