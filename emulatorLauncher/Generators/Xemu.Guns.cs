using System;
using System.IO;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class XEmuGenerator
    {
        private const int POINTER_LEFT = 1001;
        private const int POINTER_MIDDLE = 1002;
        private const int POINTER_RIGHT = 1003;
        private const int KEY_SPACE = 44;
        private const int KEY_Q = 20;
        private const int KEY_S = 22;
        private const int KEY_RIGHT = 79;
        private const int KEY_LEFT = 80;
        private const int KEY_DOWN = 81;
        private const int KEY_UP = 82;

        private bool _multigun = false;
        /// <summary>
        /// CHIHIRO: bind one pointer device per player for the gun games (The House
        /// of the Dead 3, Virtua Cop 3, Ghost Squad). The fork reads each mouse or
        /// light gun on its own through Raw Input.
        /// </summary>
        private void ConfigureChihiroGuns(IniTomlFile ini)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
            {
                ini.WriteValue("chihiro.settings", "pointer_devices", "false");
                ini.WriteValue("chihiro.settings", "lightgun_mode", "false");
                ini.WriteValue("chihiro.jvs", "pointer_device", "'mouse'");
                ini.WriteValue("chihiro.jvs_p2", "pointer_device", "''");
                return;
            }

            var guns = RawLightgun.GetRawLightguns();
            int gunCount = RawLightgun.GetUsableLightGunCount();
            bool useOneGun = SystemConfig.getOptBoolean("one_gun");

            SimpleLogger.Instance.Info("[LightGun] Found " + gunCount + " usable guns.");

            if (guns.Length == 0)
            {
                SimpleLogger.Instance.Warning("[LightGun] No pointer device found, falling back to the system cursor.");
                ini.WriteValue("chihiro.settings", "pointer_devices", "false");
                ini.WriteValue("chihiro.settings", "lightgun_mode", "true");
                ini.WriteValue("chihiro.jvs", "pointer_device", "'mouse'");
                ini.WriteValue("chihiro.jvs_p2", "pointer_device", "''");
                return;
            }

            // Hides the cursor and hands the mouse buttons to the gun instead of
            // the emulator UI while a gun game runs.
            ini.WriteValue("chihiro.settings", "lightgun_mode", "true");

            ini.WriteValue("chihiro.settings", "pointer_devices", "true");
            ini.WriteValue("chihiro.settings", "pointer_grab", "false");

            ini.WriteValue("chihiro.jvs", "pointer_device", "'" + GetPointerIdentity(guns, guns[0]) + "'");
            SimpleLogger.Instance.Info("[LightGun] P1 -> " + guns[0].ToString());

            if (!useOneGun && guns.Length > 1 && gunCount > 1)
            {
                _multigun = true;
                ini.WriteValue("chihiro.jvs_p2", "pointer_device", "'" + GetPointerIdentity(guns, guns[1]) + "'");
                SimpleLogger.Instance.Info("[LightGun] P2 -> " + guns[1].ToString());
            }
            else
                ini.WriteValue("chihiro.jvs_p2", "pointer_device", "''");

            // Sinden: start the software, and let the emulator draw the border it
            // needs (the fork draws it itself, around the game picture).
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();

                ini.WriteValue("chihiro.settings", "sinden_border", "true");

                if (SystemConfig.isOptSet("chihiro_sinden_style") && !string.IsNullOrEmpty(SystemConfig["chihiro_sinden_style"]))
                    ini.WriteValue("chihiro.settings", "sinden_border_style", "'" + SystemConfig["chihiro_sinden_style"] + "'");
                else
                    ini.WriteValue("chihiro.settings", "sinden_border_style", "'game'");

                BindIniFeature(ini, "chihiro.settings", "sinden_border_size", "chihiro_sinden_size", "2");
            }
            else
                ini.WriteValue("chihiro.settings", "sinden_border", "false");

            // Crosshair, drawn by the emulator over the game picture
            if (SystemConfig.isOptSet("chihiro_crosshair") && SystemConfig["chihiro_crosshair"] == "custom"
                && SystemConfig.isOptSet("chihiro_crosshairpath") && File.Exists(SystemConfig["chihiro_crosshairpath"]))
            {
                ini.WriteValue("chihiro.jvs", "crosshair_path", "'" + SystemConfig["chihiro_crosshairpath"] + "'");
                ini.WriteValue("chihiro.jvs_p2", "crosshair_path", "'" + SystemConfig["chihiro_crosshairpath"] + "'");
                BindIniFeature(ini, "chihiro.jvs", "crosshair_scale", "chihiro_crosshair_scale", "100");
                BindIniFeature(ini, "chihiro.jvs_p2", "crosshair_scale", "chihiro_crosshair_scale", "100");
            }
            else
            {
                ini.Remove("chihiro.jvs", "crosshair_path");
                ini.Remove("chihiro.jvs_p2", "crosshair_path");
            }

            ConfigureChihiroGunButtons(ini, guns);
        }

        private static string GetPointerIdentity(RawLightgun[] guns, RawLightgun gun)
        {
            string path = gun.DevicePath ?? string.Empty;

            int vid = (int)gun.VendorId & 0xFFFF;
            int pid = (int)gun.ProductId & 0xFFFF;

            bool hasVidPid = vid != 0 && pid != 0 &&
                             path.IndexOf("VID_", StringComparison.InvariantCultureIgnoreCase) >= 0;

            if (hasVidPid && guns.Any(g => g != gun && g.Index < gun.Index &&
                                           ((int)g.VendorId & 0xFFFF) == vid &&
                                           ((int)g.ProductId & 0xFFFF) == pid))
                hasVidPid = false;

            if (hasVidPid)
                return string.Format("vidpid:{0:x4}:{1:x4}", vid, pid);

            return "path:" + NormalizePointerPath(path);
        }

        /// <summary>
        /// Same normalization as device_path() and path_identity() in xemu-chihiro.
        /// </summary>
        private static string NormalizePointerPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\"))
                path = path.Substring(4);

            int guid = path.IndexOf("#{");
            if (guid >= 0)
                path = path.Substring(0, guid);

            if (path.Length > 56)
                path = path.Substring(path.Length - 56);

            return path;
        }

        private void ConfigureChihiroGunButtons(IniTomlFile ini, RawLightgun[] guns)
        {
            bool invert = SystemConfig.getOptBoolean("gun_invert");
            bool reloadButton = SystemConfig.getOptBoolean("gun_reload_button");

            int trigger = invert ? POINTER_RIGHT : POINTER_LEFT;
            int grip = invert ? POINTER_LEFT : POINTER_RIGHT;

            // Reloading is done by shooting off the picture, as on the cabinet. A gun
            // that cannot shoot off screen gets the middle button instead.
            int reload = reloadButton ? POINTER_MIDDLE : 0;
            int secondary1 = reloadButton ? KEY_Q : POINTER_MIDDLE;
            int secondary2 = reloadButton ? KEY_S : POINTER_MIDDLE;

            // DolphinBar (Mayflash Wiimote), detected for each player
            bool p1Wiimote = guns[0].Type == RawLighGunType.MayFlashWiimote;
            bool p2Wiimote = _multigun && guns.Length > 1 && guns[1].Type == RawLighGunType.MayFlashWiimote;
            bool twoWiimotes = p1Wiimote && p2Wiimote;

            var p1 = GetChihiroGunKeys(1, p1Wiimote, true, twoWiimotes, grip, reload, secondary1);
            var p2 = GetChihiroGunKeys(2, p2Wiimote, !p1Wiimote, twoWiimotes, grip, reload, secondary2);

            ini.WriteValue("chihiro.jvs", "start", p1.Start.ToString());
            ini.WriteValue("chihiro.jvs", "coin", p1.Coin.ToString());
            ini.WriteValue("chihiro.jvs_p2", "start", p2.Start.ToString());
            ini.WriteValue("chihiro.jvs_p2", "coin", p2.Coin.ToString());

            // The House of the Dead 3 (no reload input: aim off the picture)
            ini.WriteValue("chihiro.jvs.hotd3", "trigger", trigger.ToString());
            ini.WriteValue("chihiro.jvs.hotd3", "body_button", p1.Hotd3Body.ToString());

            // Virtua Cop 3: the pedal takes cover
            ini.WriteValue("chihiro.jvs.vc3", "trigger", trigger.ToString());
            ini.WriteValue("chihiro.jvs.vc3", "body_button", p1.Body.ToString());
            ini.WriteValue("chihiro.jvs.vc3", "pedal", p1.Extra.ToString());
            ini.WriteValue("chihiro.jvs.vc3", "reload", p1.Reload.ToString());

            // Ghost Squad: 'change' switches weapon
            ini.WriteValue("chihiro.jvs.gs", "trigger", trigger.ToString());
            ini.WriteValue("chihiro.jvs.gs", "body_button", p1.Body.ToString());
            ini.WriteValue("chihiro.jvs.gs", "change", p1.Extra.ToString());
            ini.WriteValue("chihiro.jvs.gs", "reload", p1.Reload.ToString());

            // Multigun is written in chihiro.jvs_p2 section
            if (_multigun)
            {
                ini.WriteValue("chihiro.jvs_p2", "gs_trigger", trigger.ToString());
                ini.WriteValue("chihiro.jvs_p2", "gs_body_button", p2.Body.ToString());
                ini.WriteValue("chihiro.jvs_p2", "gs_change", p2.Extra.ToString());
                ini.WriteValue("chihiro.jvs_p2", "gs_reload", p2.Reload.ToString());

                ini.WriteValue("chihiro.jvs_p2", "hotd3_trigger", trigger.ToString());
                ini.WriteValue("chihiro.jvs_p2", "hotd3_body_button", p2.Hotd3Body.ToString());

                ini.WriteValue("chihiro.jvs_p2", "vc3_trigger", trigger.ToString());
                ini.WriteValue("chihiro.jvs_p2", "vc3_body_button", p2.Body.ToString());
                ini.WriteValue("chihiro.jvs_p2", "vc3_pedal", p2.Extra.ToString());
                ini.WriteValue("chihiro.jvs_p2", "vc3_reload", p2.Reload.ToString());
            }

            SimpleLogger.Instance.Info("[LightGun] DolphinBar P1: " + p1Wiimote + ", P2: " + p2Wiimote);
        }

        // Keys of one player for the gun games (SDL scancodes, 1001+ = buttons of the player's pointer device)
        private class ChihiroGunKeys
        {
            public int Start, Coin, Reload, Hotd3Body, Body, Extra;
        }

        /// <summary>
        /// DolphinBar: start/coin on the D-pad (same keys in DolphinBar modes 1 and 2). xemu-chihiro reads every keyboard
        /// as one (SDL keyboard state), so a second Wiimote uses other directions; pointer buttons are read per device.
        /// </summary>
        private static ChihiroGunKeys GetChihiroGunKeys(int player, bool wiimote, bool firstWiimote, bool twoWiimotes, int grip, int reload, int secondary)
        {
            var keys = new ChihiroGunKeys
            {
                Start = player == 1 ? KEY_1 : KEY_2,
                Coin = player == 1 ? KEY_5 : KEY_6,
                Reload = reload,
                Hotd3Body = grip,
                Body = grip,
                Extra = secondary
            };

            if (!wiimote)
                return keys;

            keys.Start = firstWiimote ? KEY_RIGHT : KEY_UP;
            keys.Coin = firstWiimote ? KEY_LEFT : KEY_DOWN;

            if (twoWiimotes)
            {
                // Every D-pad direction is taken by start/coin: A takes the pedal / weapon change, reload by aiming off the picture
                keys.Extra = grip;
                keys.Reload = 0;
                keys.Body = 0;
            }
            else
            {
                // A = reload, D-pad Down = pedal / weapon change, D-pad Up = body button
                keys.Reload = grip;
                keys.Extra = KEY_DOWN;
                keys.Body = KEY_UP;
            }

            return keys;
        }
    }
}