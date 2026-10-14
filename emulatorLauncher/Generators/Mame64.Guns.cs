using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    partial class Mame64Generator : Generator
    {
        private bool _multigun = false;
        private bool _mouseGun = false;

        /// <summary>
        /// Guns: multigun, device remapping (GUNCODE_n / MOUSECODE_n) and gun index of each player (p*_gunIndex overrides).
        /// Returns the index of players 1 to 4, also used by the pad mappings.
        /// </summary>
        private string[] ConfigureMameGunDevices(XElement input, RawLightgun[] guns, int gunCount)
        {
            _multigun = gunCount > 1 && !SystemConfig.getOptBoolean("mame_forceOneGun");

            if (gunCount > 0)
                ConfigureGunRemap(input, guns, _multigun);

            return new[]
            {
                GetGunIndexOrDefault("p1_gunIndex", "1", 1),
                GetGunIndexOrDefault("p2_gunIndex", "2", 2),
                GetGunIndexOrDefault("p3_gunIndex", "3", 3),
                GetGunIndexOrDefault("p4_gunIndex", "4", 4)
            };
        }

        private void ConfigureLightguns(XElement guninput, RawLightgun[] guns, string index1, string index2, string index3, string index4, int gunNb, bool hbmame = false)
        {
            // DolphinBar (Mayflash Wiimote): start/coin on the D-pad, same keys in DolphinBar modes 1 and 2.
            // MAME reads every keyboard as one here, so the first Wiimote uses D-pad Right/Left and the second one Up/Down.
            string[] indexes = { null, index1, index2, index3, index4 };
            var wiiStart = new string[5];
            var wiiCoin = new string[5];
            var wiiButton3 = new string[5];
            int players = _multigun ? Math.Min(gunNb, 4) : 1;
            int wiimotes = 0;

            for (int p = 1; p <= players; p++)
            {
                var gun = GetMappedGun(guns, indexes[p]);
                if (gun == null || gun.Type != RawLighGunType.MayFlashWiimote)
                    continue;

                wiimotes++;
                if (wiimotes == 1)
                {
                    wiiStart[p] = " OR KEYCODE_RIGHT";
                    wiiCoin[p] = " OR KEYCODE_LEFT";
                }
                else if (wiimotes == 2)
                {
                    wiiStart[p] = " OR KEYCODE_UP";
                    wiiCoin[p] = " OR KEYCODE_DOWN";
                }
                else
                    SimpleLogger.Instance.Info("[GUNS] P" + p + " DolphinBar: no D-pad direction left for start/coin, keyboard only.");
            }

            // Single Wiimote: D-pad Down is free and replaces the missing middle click (button 3)
            if (wiimotes == 1)
            {
                for (int p = 1; p <= players; p++)
                {
                    if (wiiStart[p] != null)
                        wiiButton3[p] = " OR KEYCODE_DOWN";
                }
            }

            AddPort(guninput, hbmame ? "UI_CONFIGURE" : "UI_MENU", standard: "KEYCODE_TAB");

            AddPort(guninput, "UI_SELECT", standard: "GUNCODE_" + index1 + "_BUTTON1 OR KEYCODE_ENTER OR MOUSECODE_" + index1 + "_BUTTON1");
            AddPort(guninput, "UI_BACK", standard: "GUNCODE_" + index1 + "_BUTTON3 OR MOUSECODE_" + index1 + "_BUTTON3 OR KEYCODE_ESC");
            AddPort(guninput, "UI_CANCEL", standard: "KEYCODE_ESC");
            AddPort(guninput, "UI_UP", standard: "KEYCODE_UP");
            AddPort(guninput, "UI_DOWN", standard: "KEYCODE_DOWN");
            AddPort(guninput, "UI_LEFT", standard: "KEYCODE_LEFT");
            AddPort(guninput, "UI_RIGHT", standard: "KEYCODE_RIGHT");
            AddPort(guninput, "UI_PAUSE", standard: "KEYCODE_P");
            AddPort(guninput, "UI_REWIND_SINGLE", standard: "KEYCODE_BACKSPACE");
            AddPort(guninput, "UI_FAST_FORWARD", standard: "KEYCODE_SPACE");
            AddPort(guninput, "UI_SAVE_STATE", standard: "KEYCODE_F2");
            AddPort(guninput, "UI_LOAD_STATE", standard: "KEYCODE_F4");
            AddPort(guninput, "SERVICE", standard: "KEYCODE_0");
            AddPort(guninput, "SERVICE1", standard: "KEYCODE_9");

            // Joystick directions (with a DolphinBar the arrows are start/coin: cleared, MAME would apply its default arrows otherwise)
            AddPort(guninput, "P1_JOYSTICK_UP", standard: wiimotes > 0 ? "NONE" : "KEYCODE_UP");
            AddPort(guninput, "P1_JOYSTICK_DOWN", standard: wiimotes > 0 ? "NONE" : "KEYCODE_DOWN");
            AddPort(guninput, "P1_JOYSTICK_LEFT", standard: wiimotes > 0 ? "NONE" : "KEYCODE_LEFT");
            AddPort(guninput, "P1_JOYSTICK_RIGHT", standard: wiimotes > 0 ? "NONE" : "KEYCODE_RIGHT");

            // Buttons
            AddPort(guninput, "P1_BUTTON1", standard: "GUNCODE_" + index1 + "_BUTTON1 OR MOUSECODE_" + index1 + "_BUTTON1");
            AddPort(guninput, "P1_BUTTON2", standard: "GUNCODE_" + index1 + "_BUTTON2 OR MOUSECODE_" + index1 + "_BUTTON2");
            AddPort(guninput, "P1_BUTTON3", standard: "GUNCODE_" + index1 + "_BUTTON3 OR MOUSECODE_" + index1 + "_BUTTON3" + wiiButton3[1]);

            // Start & Select
            AddPort(guninput, "P1_START", standard: "KEYCODE_1" + wiiStart[1]);
            AddPort(guninput, "P1_SELECT", standard: "KEYCODE_5" + wiiCoin[1]);

            // Start & Coin aliases
            AddPort(guninput, "START1", standard: "KEYCODE_1" + wiiStart[1]);
            AddPort(guninput, "COIN1", standard: "KEYCODE_5" + wiiCoin[1]);

            // Analog / Lightgun / Mouse axes
            AddPort(guninput, "P1_AD_STICK_X", standard: "GUNCODE_" + index1 + "_XAXIS OR MOUSECODE_" + index1 + "_XAXIS");
            AddPort(guninput, "P1_AD_STICK_Y", standard: "GUNCODE_" + index1 + "_YAXIS OR MOUSECODE_" + index1 + "_YAXIS");
            AddPort(guninput, "P1_LIGHTGUN_X", standard: "GUNCODE_" + index1 + "_XAXIS OR MOUSECODE_" + index1 + "_XAXIS");
            AddPort(guninput, "P1_LIGHTGUN_Y", standard: "GUNCODE_" + index1 + "_YAXIS OR MOUSECODE_" + index1 + "_YAXIS");
            AddPort(guninput, "P1_MOUSE_X", standard: "GUNCODE_" + index1 + "_XAXIS OR MOUSECODE_" + index1 + "_XAXIS");
            AddPort(guninput, "P1_MOUSE_Y", standard: "GUNCODE_" + index1 + "_YAXIS OR MOUSECODE_" + index1 + "_YAXIS");

            if (_multigun)
            {
                // Player 2
                AddPort(guninput, "P2_JOYSTICK_UP", "GUNCODE_" + index2 + "_BUTTON7");
                AddPort(guninput, "P2_JOYSTICK_DOWN", "GUNCODE_" + index2 + "_BUTTON9");
                AddPort(guninput, "P2_JOYSTICK_LEFT", "GUNCODE_" + index2 + "_BUTTON10");
                AddPort(guninput, "P2_JOYSTICK_RIGHT", "GUNCODE_" + index2 + "_BUTTON8");
                AddPort(guninput, "P2_BUTTON1", "GUNCODE_" + index2 + "_BUTTON1 OR MOUSECODE_" + index2 + "_BUTTON1");
                AddPort(guninput, "P2_BUTTON2", "GUNCODE_" + index2 + "_BUTTON2 OR MOUSECODE_" + index2 + "_BUTTON2");
                AddPort(guninput, "P2_BUTTON3", "GUNCODE_" + index2 + "_BUTTON3 OR MOUSECODE_" + index2 + "_BUTTON3" + wiiButton3[2]);
                AddPort(guninput, "P2_START", "KEYCODE_2" + wiiStart[2]);
                AddPort(guninput, "P2_SELECT", "KEYCODE_6" + wiiCoin[2]);
                AddPort(guninput, "START2", "KEYCODE_2" + wiiStart[2]);
                AddPort(guninput, "COIN2", "KEYCODE_6" + wiiCoin[2]);
                AddPort(guninput, "P2_AD_STICK_X", "GUNCODE_" + index2 + "_XAXIS OR MOUSECODE_" + index2 + "_XAXIS");
                AddPort(guninput, "P2_AD_STICK_Y", "GUNCODE_" + index2 + "_YAXIS OR MOUSECODE_" + index2 + "_YAXIS");
                AddPort(guninput, "P2_LIGHTGUN_X", "GUNCODE_" + index2 + "_XAXIS OR MOUSECODE_" + index2 + "_XAXIS");
                AddPort(guninput, "P2_LIGHTGUN_Y", "GUNCODE_" + index2 + "_YAXIS OR MOUSECODE_" + index2 + "_YAXIS");
                AddPort(guninput, "P2_MOUSE_X", "GUNCODE_" + index2 + "_XAXIS OR MOUSECODE_" + index2 + "_XAXIS");
                AddPort(guninput, "P2_MOUSE_Y", "GUNCODE_" + index2 + "_YAXIS OR MOUSECODE_" + index2 + "_YAXIS");

                if (gunNb > 2) // Player 3
                    for (int p = 3; p <= Math.Min(gunNb, 4); p++)
                    {
                        string idx = p == 3 ? index3 : index4;
                        AddPort(guninput, $"P{p}_JOYSTICK_UP", "GUNCODE_" + idx + "_BUTTON7");
                        AddPort(guninput, $"P{p}_JOYSTICK_DOWN", "GUNCODE_" + idx + "_BUTTON9");
                        AddPort(guninput, $"P{p}_JOYSTICK_LEFT", "GUNCODE_" + idx + "_BUTTON10");
                        AddPort(guninput, $"P{p}_JOYSTICK_RIGHT", "GUNCODE_" + idx + "_BUTTON8");
                        AddPort(guninput, $"P{p}_BUTTON1", "GUNCODE_" + idx + "_BUTTON1 OR MOUSECODE_" + idx + "_BUTTON1");
                        AddPort(guninput, $"P{p}_BUTTON2", "GUNCODE_" + idx + "_BUTTON2 OR MOUSECODE_" + idx + "_BUTTON2");
                        AddPort(guninput, $"P{p}_BUTTON3", "GUNCODE_" + idx + "_BUTTON3 OR MOUSECODE_" + idx + "_BUTTON3" + wiiButton3[p]);
                        AddPort(guninput, $"P{p}_START", "KEYCODE_" + p + wiiStart[p]);
                        AddPort(guninput, $"P{p}_SELECT", "KEYCODE_" + (p + 4) + wiiCoin[p]);
                        AddPort(guninput, $"START{p}", "KEYCODE_" + p + wiiStart[p]);
                        AddPort(guninput, $"COIN{p}", "KEYCODE_" + (p + 4) + wiiCoin[p]);
                        AddPort(guninput, $"P{p}_AD_STICK_X", "GUNCODE_" + idx + "_XAXIS OR MOUSECODE_" + idx + "_XAXIS");
                        AddPort(guninput, $"P{p}_AD_STICK_Y", "GUNCODE_" + idx + "_YAXIS OR MOUSECODE_" + idx + "_YAXIS");
                        AddPort(guninput, $"P{p}_LIGHTGUN_X", "GUNCODE_" + idx + "_XAXIS OR MOUSECODE_" + idx + "_XAXIS");
                        AddPort(guninput, $"P{p}_LIGHTGUN_Y", "GUNCODE_" + idx + "_YAXIS OR MOUSECODE_" + idx + "_YAXIS");
                        AddPort(guninput, $"P{p}_MOUSE_X", "GUNCODE_" + idx + "_XAXIS OR MOUSECODE_" + idx + "_XAXIS");
                        AddPort(guninput, $"P{p}_MOUSE_Y", "GUNCODE_" + idx + "_YAXIS OR MOUSECODE_" + idx + "_YAXIS");
                    }
            }
        }

        private RawLightgun GetMappedGun(RawLightgun[] guns, string index)
        {
            if (guns == null || !int.TryParse(index, out int target))
                return null;

            Regex regex = new Regex(@"HID#(?:\{([^}]+)\}|([^#]+))");
            int i = 1;
            int mouseCount = 0;

            foreach (RawLightgun gun in guns)
            {
                if (!regex.IsMatch(gun.DevicePath ?? ""))
                    continue;

                if (gun.Type == RawLighGunType.Mouse || _mouseGun)
                {
                    if (mouseCount > 0)
                        continue;
                    mouseCount++;
                }

                if (i == target)
                    return gun;
                i++;
            }

            return null;
        }

        private void ConfigureGunRemap(XElement guninput, RawLightgun[] guns, bool multi = false)
        {
            Regex regex = new Regex(@"HID#(?:\{([^}]+)\}|([^#]+))");
            int i = 1;
            int mouseCount = 0;
            foreach (RawLightgun gun in guns)
            {
                var type = gun.Type;
                Match match = regex.Match(gun.DevicePath);

                if (match.Success)
                {
                    string result = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                    result.Replace("&", "&amp;");
                    string target;

                    if (gun.Type == RawLighGunType.Mouse || _mouseGun)
                    {
                        if (mouseCount > 0)
                            continue;
                        target = "MOUSECODE_" + i;
                        guninput.Add(new XElement("mapdevice", new XAttribute("device", result), new XAttribute("controller", target)));
                        mouseCount++;
                        i++;
                    }
                    else
                    {
                        target = "GUNCODE_" + i;
                        guninput.Add(new XElement("mapdevice", new XAttribute("device", result), new XAttribute("controller", target)));
                        i++;
                    }
                }
            }
        }

        private string GetGunIndexOrDefault(string key, string defaultValue, int playerNumber)
        {
            if (SystemConfig.isOptSet(key))
            {
                string value = SystemConfig[key];
                if (!string.IsNullOrEmpty(value))
                {
                    SimpleLogger.Instance.Info($"[GUNS] Overwriting Gun {playerNumber} index : {value}");
                    return value;
                }
            }

            return defaultValue;
        }
    }
}
