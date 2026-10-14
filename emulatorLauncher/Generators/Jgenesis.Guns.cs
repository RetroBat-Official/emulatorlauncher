using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private void SetupGuns(IniFileJGenesis ini, string jgenSystem)
        {
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
            }

            if (!SystemConfig.getOptBoolean("use_guns"))
            {
                ini.WriteValue("input.snes", "p2_type", "\"" + "Gamepad" + "\"");
                ini.WriteValue("input.nes", "p2_type", "\"" + "Gamepad" + "\"");
                return;
            }

            if (jgenSystem == "snes")
            {
                // jgenesis 0.14+ : super scope buttons belong to the player 2 mapping.
                // The old "super_scope" table is only migrated once, from a config older than 0.14.0
                ini.DeleteSection("input.snes.mapping_1.super_scope");
                ini.DeleteSection("[[input.snes.mapping_2.super_scope]]");
                foreach (string button in new[] { "fire", "cursor", "pause", "turbo_toggle" })
                    ini.DeleteSection("[[input.snes.mapping_1.super_scope." + button + "]]");

                ini.WriteValue("input.snes", "p2_type", "\"" + "SuperScope" + "\"");

                WriteJgenInput(ini, "[[input.snes.mapping_1.p2.super_scope_fire]]", "Mouse", "button", "Left");
                WriteJgenInput(ini, "[[input.snes.mapping_1.p2.super_scope_cursor]]", "Mouse", "button", "Right");
                WriteJgenInput(ini, "[[input.snes.mapping_1.p2.super_scope_pause]]", "Mouse", "button", "Middle");
                return;
            }

            if (jgenSystem == "nes")
            {
                ini.DeleteSection("input.nes.mapping_1.zapper");

                ini.WriteValue("input.nes", "p2_type", "\"" + "Zapper" + "\"");

                ini.WriteValue("[[input.nes.mapping_1.zapper.fire]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.nes.mapping_1.zapper.fire]]", "button", "\"" + "Left" + "\"");
                ini.WriteValue("[[input.nes.mapping_1.zapper.force_offscreen]]", "type", "\"" + "Mouse" + "\"");
                ini.WriteValue("[[input.nes.mapping_1.zapper.force_offscreen]]", "button", "\"" + "Right" + "\"");
                return;
            }
        }

        /// <summary>
        /// DolphinBar Wiimote (mode 1 or 2) : the D-pad sends the arrow keys, added as second mapping (mapping_2).
        /// jgenesis reads one mouse, so only one gun is used.
        /// Must be called after SetupControllers, which cleans the pads mapping_2.
        /// </summary>
        private void SetupDolphinBarKeys(IniFileJGenesis ini, string jgenSystem)
        {
            string[] sections;

            if (jgenSystem == "nes")
                sections = new[] { "[[input.nes.mapping_2.p1.start]]", "[[input.nes.mapping_2.p1.select]]" };
            else if (jgenSystem == "snes")
                sections = new[] { "[[input.snes.mapping_2.p2.super_scope_pause]]", "[[input.snes.mapping_2.p2.super_scope_turbo_toggle]]" };
            else
                return;

            // Always cleaned, so the keys do not stay when the Wiimote is removed
            foreach (string section in sections)
                ini.DeleteSection(section);

            if (!SystemConfig.getOptBoolean("use_guns") || !RawLightgun.GetRawLightguns().Any(g => g.Type == RawLighGunType.MayFlashWiimote))
                return;

            // NES : zapper has only a trigger, start / select are on the player 1 pad
            // SNES : D-pad Right = super scope pause (start), D-pad Down = turbo switch
            WriteJgenInput(ini, sections[0], "Keyboard", "key", "Right");
            WriteJgenInput(ini, sections[1], "Keyboard", "key", jgenSystem == "nes" ? "Left" : "Down");
        }

        private static void WriteJgenInput(IniFileJGenesis ini, string section, string type, string field, string value)
        {
            ini.WriteValue(section, "type", "\"" + type + "\"");
            ini.WriteValue(section, field, "\"" + value + "\"");
        }
    }
}
