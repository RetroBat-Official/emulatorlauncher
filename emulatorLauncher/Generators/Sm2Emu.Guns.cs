using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
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
            ini.WriteValue("lightgun", "lightgun", useGuns ? "true" : "false");

            if (!useGuns)
            {
                ini.WriteValue("lightgun", "sinden_border", "false");
                ini.WriteValue("lightgun", "gun1_device", "");
                ini.WriteValue("lightgun", "gun2_device", "");
                return;
            }

            SimpleLogger.Instance.Info("[GUNS] Configuring light guns for sm2-emu.");

            BindBoolIniFeatureOn(ini, "lightgun", "lightgun_crosshair", "sm2emu_crosshair", "true", "false");
            BindBoolIniFeature(ini, "lightgun", "lightgun_hide_flash", "sm2emu_hide_flash", "true", "false");

            var guns = RawLightgun.GetRawLightguns();

            // Sinden: the camera needs the white border around the picture
            bool sinden = guns.Any(g => g.Type == RawLighGunType.SindenLightgun);
            if (sinden)
                Guns.StartSindenSoftware();

            ini.WriteValue("lightgun", "sinden_border", sinden ? "true" : "false");
            if (sinden)
                ini.WriteValue("lightgun", "sinden_border_thickness", SystemConfig.GetValueOrDefault("sm2emu_sinden_border", "12"));

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

                    ini.WriteValue("lightgun", prefix + role, code.ToString());
                }
            }

            WriteGunDevices(ini, guns);
        }

        /// <summary>
        /// gun1_device / gun2_device : the RetroBat player 1 and 2 guns. sm2-emu uses a pinned device as a gun
        /// even when its product name does not contain "gun" (DolphinBar, Blamcon...).
        /// </summary>
        private void WriteGunDevices(IniFile ini, RawLightgun[] guns)
        {
            // Plain mice are not pinned: without a gun, sm2-emu aims with the system mouse
            //var realGuns = guns.Where(g => g.Type != RawLighGunType.Mouse && !string.IsNullOrEmpty(g.DevicePath)).ToList();

            int count = SystemConfig.getOptBoolean("one_gun") ? 1 : 2;

            for (int player = 1; player <= 2; player++)
            {
                string device = "";
                if (player <= count && player <= guns.Length)
                {
                    device = GetGunDeviceId(guns[player - 1].DevicePath);
                    SimpleLogger.Instance.Info("[GUNS] Gun " + player + " : " + guns[player - 1].Name + " (" + guns[player - 1].Type + ")");
                }

                ini.WriteValue("lightgun", "gun" + player + "_device", device);
            }
        }

        /// <summary>
        /// \\?\HID#VID_16C0&PID_0F38&MI_00#8&2a8f...&0&0000#{378de44c-...} gives HID#VID_16C0&PID_0F38&MI_00#8&2a8f...&0&0000,
        /// a part of the Raw Input path unique to the device (sm2-emu searches it in the path, ignoring case).
        /// </summary>
        private static string GetGunDeviceId(string devicePath)
        {
            string id = devicePath;

            if (id.StartsWith(@"\\?\") || id.StartsWith(@"\??\"))
                id = id.Substring(4);

            int guid = id.LastIndexOf("#{", StringComparison.Ordinal);
            if (guid > 0)
                id = id.Substring(0, guid);

            return id;
        }
    }
}
