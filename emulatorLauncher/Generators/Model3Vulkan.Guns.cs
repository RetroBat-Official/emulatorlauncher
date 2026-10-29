using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Model3VulkanGenerator
    {
        private string _mouse1 = "MOUSE";
        private string _mouse2 = "MOUSE";
        private bool _multigun = false;
        private RawLightgun[] _guns = new RawLightgun[0];

        /// <summary>
        /// Model3-Vulkan reads each mouse separately (Raw Input, PlatformInputSDL3.cpp) : MOUSEn is the n-th mouse of
        /// SDL_GetMice, which follows GetRawInputDeviceList like RawLightgun.
        /// MOUSE without number is the system cursor (any mouse), used with one gun.
        /// </summary>
        private void DefineGuns(IniFile ini)
        {
            _mouse1 = _mouse2 = "MOUSE";
            _multigun = false;

            bool useGuns = SystemConfig.getOptBoolean("use_guns");
            bool realGuns = false;

            if (useGuns)
            {
                _guns = RawLightgun.GetRawLightguns();

                int gunCount = RawLightgun.GetUsableLightGunCount();
                if (SystemConfig.getOptBoolean("one_gun"))
                    gunCount = Math.Min(gunCount, 1);

                realGuns = _guns.Take(Math.Max(gunCount, 1)).Any(g => g.Type != RawLighGunType.Mouse);

                if (gunCount > 1 && _guns.Length > 1)
                {
                    _multigun = true;
                    _mouse1 = "MOUSE" + (_guns[0].Index + 1);
                    _mouse2 = "MOUSE" + (_guns[1].Index + 1);
                }

                // Forced mouse numbers
                if (SystemConfig.isOptSet("supermodel_gun1") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun1"]))
                    _mouse1 = "MOUSE" + SystemConfig["supermodel_gun1"].ToInteger();
                if (SystemConfig.isOptSet("supermodel_gun2") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun2"]))
                {
                    _mouse2 = "MOUSE" + SystemConfig["supermodel_gun2"].ToInteger();
                    _multigun = true;
                    if (_mouse1 == "MOUSE")
                        _mouse1 = "MOUSE1";
                }

                for (int i = 0; i < _guns.Length && i < (_multigun ? 2 : 1); i++)
                    SimpleLogger.Instance.Info("[GUNS] Gun " + (i + 1) + " : " + _guns[i].Name + " (" + _guns[i].Type + ") => " + (i == 0 ? _mouse1 : _mouse2));

                // Sinden : white border drawn by the emulator (Vulkan2D) around the picture
                bool sinden = _guns.Take(_multigun ? 2 : 1).Any(g => g.Type == RawLighGunType.SindenLightgun);
                if (sinden)
                    Guns.StartSindenSoftware();

                ini.WriteValue(GlobalSection, "SindenBorder", sinden ? "1" : "0");
                if (sinden)
                    ini.WriteValue(GlobalSection, "SindenBorderWidth", SystemConfig.GetValueOrDefault("m3v_sinden_border", "16"));
            }
            else
                ini.WriteValue(GlobalSection, "SindenBorder", "0");

            // Crosshairs : 0 none, 1 player 1, 2 player 2, 3 both. Light guns aim by themselves, a mouse needs the crosshair.
            if (SystemConfig.isOptSet("crosshairs") && !string.IsNullOrEmpty(SystemConfig["crosshairs"]))
                ini.WriteValue(GlobalSection, "Crosshairs", SystemConfig["crosshairs"]);
            else if (realGuns)
                ini.WriteValue(GlobalSection, "Crosshairs", "0");
            else
                ini.WriteValue(GlobalSection, "Crosshairs", _multigun ? "3" : "1");

            BindIniFeature(ini, GlobalSection, "CrosshairStyle", "m3v_crosshairstyle", "vector");
        }

        /// <summary>
        /// Light gun mode : trigger = left button, reload (off-screen) = right button, swapped with gun_invert (Wiimote with Nunchuk, WiiZapper).
        /// Start and coin stay on the keyboard (1 / 5, 2 / 6), like the light gun software defaults.
        /// </summary>
        private void ConfigureGuns(IniFile ini)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            bool invert = SystemConfig.getOptBoolean("gun_invert");
            string trigger = invert ? "_RIGHT_BUTTON" : "_LEFT_BUTTON";
            string reload = invert ? "_LEFT_BUTTON" : "_RIGHT_BUTTON";

            ini.WriteValue(GlobalSection, "InputTrigger", "\"" + _mouse1 + trigger + "\"");
            ini.WriteValue(GlobalSection, "InputOffscreen", "\"" + _mouse1 + reload + "\"");
            ini.WriteValue(GlobalSection, "InputAnalogTriggerLeft", "\"" + _mouse1 + trigger + "\"");
            ini.WriteValue(GlobalSection, "InputAnalogTriggerRight", "\"" + _mouse1 + reload + "\"");

            if (_multigun)
            {
                ini.WriteValue(GlobalSection, "InputTrigger2", "\"" + _mouse2 + trigger + "\"");
                ini.WriteValue(GlobalSection, "InputOffscreen2", "\"" + _mouse2 + reload + "\"");
                ini.WriteValue(GlobalSection, "InputAnalogTriggerLeft2", "\"" + _mouse2 + trigger + "\"");
                ini.WriteValue(GlobalSection, "InputAnalogTriggerRight2", "\"" + _mouse2 + reload + "\"");
            }

            ConfigureDolphinBar(ini);
        }

        /// <summary>
        /// DolphinBar (Mayflash Wiimote, modes 1 and 2) : D-pad Left = coin, D-pad Right = start (arrow keys of the DolphinBar keyboard).
        /// The SDL3 build only reads one merged keyboard (CSDLInputSystem::GetNumKeyboards) : the arrows cannot be told apart
        /// between two DolphinBars, so they are only added for player 1 when player 2 is not a DolphinBar.
        /// </summary>
        private void ConfigureDolphinBar(IniFile ini)
        {
            var playerGuns = _guns.Take(_multigun ? 2 : 1).ToArray();
            if (playerGuns.Length == 0 || playerGuns[0].Type != RawLighGunType.MayFlashWiimote)
                return;

            if (playerGuns.Length > 1 && playerGuns[1].Type == RawLighGunType.MayFlashWiimote)
            {
                SimpleLogger.Instance.Info("[GUNS] Two DolphinBars : start and coin stay on the keys 1 / 5 and 2 / 6 (one merged keyboard).");
                return;
            }

            AppendInput(ini, "InputStart1", "KEY_RIGHT");
            AppendInput(ini, "InputCoin1", "KEY_LEFT");
            SimpleLogger.Instance.Info("[GUNS] Player 1 DolphinBar start / coin : KEY_RIGHT / KEY_LEFT");
        }

        private static void AppendInput(IniFile ini, string key, string toAdd)
        {
            string value = (ini.GetValue(GlobalSection, key) ?? "").Trim('"');
            var keys = value.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0 && k != "NONE").ToList();

            if (!keys.Contains(toAdd))
                keys.Add(toAdd);

            ini.WriteValue(GlobalSection, key, "\"" + string.Join(",", keys) + "\"");
        }
    }
}
