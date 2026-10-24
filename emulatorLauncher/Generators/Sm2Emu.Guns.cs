using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Sm2EmuGenerator
    {
        // Mouse buttons as sm2-emu reads them on Windows (evdev codes): left, right, middle
        private const int GUN_BUTTON_LEFT = 272;
        private const int GUN_BUTTON_RIGHT = 273;

        private static readonly string[] _gunRoles = new string[] { "trigger", "reload", "coin", "start", "hat_up", "hat_down", "hat_left", "hat_right" };

        /// <summary>
        /// Light gun mode. sm2-emu reads the guns as Raw Input mice: trigger = left button, reload = right button.
        /// Start and coin stay on the keyboard (1 / 5 for player 1, 2 / 6 for player 2), like the gun software defaults.
        /// </summary>
        private void SetupGuns(IniFile ini)
        {
            bool useGuns = SystemConfig.getOptBoolean("use_guns");
            ini.WriteValue("", "lightgun", useGuns ? "true" : "false");

            if (!useGuns)
            {
                ini.WriteValue("", "sinden_border", "false");
                return;
            }

            SimpleLogger.Instance.Info("[GUNS] Configuring light guns for sm2-emu.");

            BindBoolIniFeatureOn(ini, "", "lightgun_crosshair", "sm2emu_crosshair", "true", "false");
            BindBoolIniFeature(ini, "", "lightgun_hide_flash", "sm2emu_hide_flash", "true", "false");

            var guns = RawLightgun.GetRawLightguns();

            // Sinden: the camera needs the white border around the picture
            bool sinden = guns.Any(g => g.Type == RawLighGunType.SindenLightgun);
            if (sinden)
                Guns.StartSindenSoftware();

            ini.WriteValue("", "sinden_border", sinden ? "true" : "false");
            if (sinden)
                ini.WriteValue("", "sinden_border_thickness", SystemConfig.GetValueOrDefault("sm2emu_sinden_border", "12"));

            bool invert = SystemConfig.getOptBoolean("gun_invert");
            for (int player = 1; player <= 2; player++)
            {
                string prefix = "gun" + player + "_button_";
                foreach (string role in _gunRoles)
                {
                    int code = 0;
                    if (role == "trigger")
                        code = invert ? GUN_BUTTON_RIGHT : GUN_BUTTON_LEFT;
                    else if (role == "reload")
                        code = invert ? GUN_BUTTON_LEFT : GUN_BUTTON_RIGHT;

                    ini.WriteValue("", prefix + role, code.ToString());
                }
            }
        }
    }
}
