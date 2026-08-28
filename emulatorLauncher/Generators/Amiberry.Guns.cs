using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    /// <summary>
    ///  - The Amiga peripheral is a LIGHT PEN, not a light gun. A port is switched to
    ///    it with joyport{N}mode=lightpen (mode index 8 in joyportmodes[],
    ///    cfgfile.cpp:221).
    ///
    ///  - Absolute positioning requires absolute_mouse=tablet. 
    ///    Only "tablet" dispatches raw absolute window coordinates
    ///
    ///  - The hardware pen is read through port 2's POTGO lines on everything except
    ///    the A1000 (lightpen_port_number(), inputdevice.cpp:4010), so joyport1 is the
    ///    correct default.
    ///
    ///  - Two guns require input.multi_mouse=true, which makes Amiberry enumerate
    ///    physical mice through SDL_GetMice(). That ordering is not RetroBat's
    ///    RawInput ordering, so the second gun's slot is a guess; gun_index_revert
    ///    lets the user swap. Amiberry does guarantee that mouse index 0 is a real
    ///    (non-virtual) device, so the single gun case is safe.
    ///    
    ///  - Amiberry has no off-screen reload event and no crosshair rendering.
    /// </summary>
    partial class AmiberryGenerator : Generator
    {
        private bool _sindenSoft = false;

        private const string ABSOLUTE_MOUSE_TABLET = "tablet";

        private const string MODE_LIGHTPEN = "lightpen";
        private const string SUBMODE_TROJAN = "trojan";

        private void ConfigureGuns(Action<string, string> Set)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            var guns = RawLightgun.GetRawLightguns();
            if (guns == null || guns.Length < 1)
            {
                SimpleLogger.Instance.Warning("[GUNS] No usable light gun found, skipping Amiberry gun setup.");
                return;
            }

            SimpleLogger.Instance.Info("[GUNS] Found " + guns.Length + " usable gun(s), configuring Amiberry lightpen.");

            // Sinden soft
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }

            bool useOneGun = SystemConfig.getOptBoolean("one_gun");
            bool multigun = !useOneGun && guns.Length > 1;
            bool indexRevert = SystemConfig.getOptBoolean("gun_index_revert");

            // Without this the pen only receives relative deltas and cannot point.
            Set("absolute_mouse", ABSOLUTE_MOUSE_TABLET);

            // Do not grab the pointer: a gun reports absolute screen positions and
            // capturing would confine and warp it. This also removes the "click once
            // before it works" behaviour.
            Set("amiberry.active_capture_automatically", "false");
            Set("magic_mouse", "false");

            // 100% keeps host and Amiga coordinates aligned; anything else skews aim.
            Set("input.mouse_speed", "100");

            // Port and trigger
            int gunPort = 1; // port 2 on the machine
            if (SystemConfig.isOptSet("amiberry_gunport") && !string.IsNullOrEmpty(SystemConfig["amiberry_gunport"]))
                gunPort = SystemConfig["amiberry_gunport"].ToInteger();

            bool trojan = SystemConfig.isOptSet("amiberry_gun_type") && SystemConfig["amiberry_gun_type"] == "trojan";

            int firstMouse = indexRevert && multigun ? 1 : 0;

            BindLightpen(Set, gunPort, firstMouse, trojan);

            if (multigun)
            {
                int secondPort = gunPort == 1 ? 0 : 1;
                int secondMouse = indexRevert ? 0 : 1;

                // Opt-in physical mouse enumeration. Off by default because it relies
                // on device name filtering and can regress single-gun setups.
                Set("input.multi_mouse", "true");

                BindLightpen(Set, secondPort, secondMouse, trojan);

                SimpleLogger.Instance.Warning("[GUNS] Two guns configured. Amiberry enumerates mice through SDL, " + "which may not match RetroBat's order - use 'REVERSE GUN ORDER' if the guns are swapped.");
            }

            SimpleLogger.Instance.Info("[GUNS] Lightpen on port " + gunPort + (trojan ? " (Trojan Phazer trigger)" : " (standard lightpen trigger)") + ".");
        }

        private void BindLightpen(Action<string, string> Set, int port, int mouseIndex, bool trojan)
        {
            // Index only - Amiberry binds the pen to a mouse device, and
            // mice have no friendly-name matching path like joysticks do.
            Set("joyport" + port, mouseIndex == 0 ? "mouse0" : "mouse" + mouseIndex.ToString(CultureInfo.InvariantCulture));
            Set("joyport" + port + "mode", MODE_LIGHTPEN);

            if (trojan)
                Set("joyport" + port + "submode", SUBMODE_TROJAN);
        }
    }
}
