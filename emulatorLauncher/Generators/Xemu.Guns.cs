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
        /// <summary>
        /// CHIHIRO: bind one pointer device per player for the gun games (The House
        /// of the Dead 3, Virtua Cop 3, Ghost Squad). The fork reads each mouse or
        /// light gun on its own through Raw Input, so two players can aim with
        /// their own device instead of sharing the system cursor.
        /// </summary>
        private void ConfigureChihiroGuns(IniTomlFile ini)
        {
            // Default: the system cursor aims, which is what the emulator already
            // does out of the box (chihiro.jvs.pointer_device defaults to 'mouse').
            if (!SystemConfig.getOptBoolean("use_guns"))
            {
                ini.WriteValue("chihiro.settings", "pointer_devices", "false");
                ini.WriteValue("chihiro.settings", "lightgun_mode", "false");
                ini.WriteValue("chihiro.jvs", "pointer_device", "'mouse'");
                ini.WriteValue("chihiro.jvs_p2", "pointer_device", "''");
                return;
            }

            // Already sorted: known light guns first, integrated touchpads last.
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

            ini.WriteValue("chihiro.jvs", "start", KEY_1.ToString());
            ini.WriteValue("chihiro.jvs", "coin", KEY_5.ToString());
            ini.WriteValue("chihiro.jvs_p2", "start", KEY_2.ToString());
            ini.WriteValue("chihiro.jvs_p2", "coin", KEY_6.ToString());

            // Hides the cursor and hands the mouse buttons to the gun instead of
            // the emulator UI while a gun game runs.
            ini.WriteValue("chihiro.settings", "lightgun_mode", "true");

            ini.WriteValue("chihiro.settings", "pointer_devices", "true");
            ini.WriteValue("chihiro.settings", "pointer_grab", "false");

            ini.WriteValue("chihiro.jvs", "pointer_device", "'" + GetPointerIdentity(guns, guns[0]) + "'");
            SimpleLogger.Instance.Info("[LightGun] P1 -> " + guns[0].ToString());

            if (!useOneGun && guns.Length > 1 && gunCount > 1)
            {
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

            ConfigureChihiroGunButtons(ini);
        }

        /// <summary>
        /// The identity the fork gives a pointer device (xemu-pointer-rawinput.c):
        /// "vidpid:vvvv:pppp" in lower case when the Raw Input device path carries a
        /// VID and a PID, else "path:" + that path, stripped of its "\\?\" prefix
        /// and of its trailing class GUID, keeping the last 56 characters.
        /// When two connected devices share one VID:PID, the fork keeps the VID:PID
        /// identity for the first one Raw Input enumerates and registers the others
        /// under their path, so the same split is applied here.
        /// </summary>
        private static string GetPointerIdentity(RawLightgun[] guns, RawLightgun gun)
        {
            string path = gun.DevicePath ?? string.Empty;

            // The enums are built from a signed short (VidPid.Parse), so a VID above
            // 0x7FFF - an Aimtrak is VID_D209 - is stored negative: mask it back to
            // 16 bits, or the identity would come out as "ffffd209".
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
        /// Same normalization as device_path() and path_identity() in the fork.
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

        private void ConfigureChihiroGunButtons(IniTomlFile ini)
        {
            bool invert = SystemConfig.getOptBoolean("gun_invert");
            bool reloadButton = SystemConfig.getOptBoolean("gun_reload_button");

            // Trigger and grip. Inverting swaps them, for a WiiZapper held the other
            // way round.
            int trigger = invert ? POINTER_RIGHT : POINTER_LEFT;
            int grip = invert ? POINTER_LEFT : POINTER_RIGHT;

            // Reloading is done by shooting off the picture, as on the cabinet. A gun
            // that cannot shoot off screen gets the middle button instead, and the
            // secondary action falls back to the keyboard so nothing is doubled up.
            int reload = reloadButton ? POINTER_MIDDLE : 0;
            int secondary = reloadButton ? KEY_SPACE : POINTER_MIDDLE;

            // The House of the Dead 3: trigger and grip only, no reload key - the
            // shotgun is reloaded by pumping off the picture.
            ini.WriteValue("chihiro.jvs.hotd3", "trigger", trigger.ToString());
            ini.WriteValue("chihiro.jvs.hotd3", "body_button", grip.ToString());

            // Virtua Cop 3: the pedal takes cover
            ini.WriteValue("chihiro.jvs.vc3", "trigger", trigger.ToString());
            ini.WriteValue("chihiro.jvs.vc3", "body_button", grip.ToString());
            ini.WriteValue("chihiro.jvs.vc3", "pedal", secondary.ToString());
            ini.WriteValue("chihiro.jvs.vc3", "reload", reload.ToString());

            // Ghost Squad: 'change' switches weapon
            ini.WriteValue("chihiro.jvs.gs", "trigger", trigger.ToString());
            ini.WriteValue("chihiro.jvs.gs", "body_button", grip.ToString());
            ini.WriteValue("chihiro.jvs.gs", "change", secondary.ToString());
            ini.WriteValue("chihiro.jvs.gs", "reload", reload.ToString());
        }
    }
}