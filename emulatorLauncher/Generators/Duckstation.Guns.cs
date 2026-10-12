using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class DuckstationGenerator : Generator
    {
        private bool _multigun = false;

        private void CreateGunConfiguration(IniFile ini)
        {
            bool gun = SystemConfig.getOptBoolean("use_guns");

            if (!gun)
                return;

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                UpdateSdlControllersWithHints();
                try
                {
                    Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "1", EnvironmentVariableTarget.Process);
                }
                catch { }
            }

                Controller ctrl1 = null;
            Controller ctrl2 = null;

            if (Program.Controllers.Count >= 2)
            {
                ctrl1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                ctrl2 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
            }
            else if (Program.Controllers.Count >= 1)
                ctrl1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            else
                return;

            InputConfig joy1 = null;
            InputConfig joy2 = null;

            if (ctrl1 != null)
                joy1 = ctrl1.Config;

            if (ctrl2 != null)
                joy2 = ctrl2.Config;

            // Start Sinden Soft if required
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
            }

            string pointer1 = "Pointer-0";
            string pointer2 = "";

            if (guns.Length > 1)
            {
                int gun2Index = guns[1].Index;
                int gun1Index = guns[0].Index;
                pointer1 = "Pointer-" + gun1Index;
                pointer2 = "Pointer-" + gun2Index;
                _multigun = true;
                ini.WriteValue("InputSources", "RawInput", "true");

                if (SystemConfig.getOptBoolean("duck_gun_switch"))
                {
                    pointer1 = "Pointer-" + gun2Index;
                    pointer2 = "Pointer-" + gun1Index;
                }
            }
            else if (guns.Length > 0)
            {
                if (SystemConfig.getOptBoolean("duck_forceraw"))
                {
                    int gun1Index = guns[0].Index;
                    pointer1 = "Pointer-" + gun1Index;
                    ini.WriteValue("InputSources", "RawInput", "true");
                }
                else
                    ini.WriteValue("InputSources", "RawInput", "false");
            }
            else
                return;

            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                _forceSDL = true;

            string techPadNumber1 = null;
            string techPadNumber2 = null;
            bool gamepad1 = false;
            bool gamepad2 = false;
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");

            if (ctrl1 != null)
            {
                if (!ctrl1.IsKeyboard && ctrl1.IsXInputDevice && !_forceSDL)
                {
                    techPadNumber1 = "XInput-" + ctrl1.XInput.DeviceIndex + "/";
                    gamepad1 = true;
                }
                else if (!ctrl1.IsKeyboard)
                {
                    techPadNumber1 = "SDL-" + (ctrl1.SdlController == null ? ctrl1.DeviceIndex : ctrl1.SdlController.Index) + "/";
                    gamepad1 = true;
                }
                else
                    techPadNumber1 = "Keyboard/";
            }

            if (ctrl2 != null)
            {
                if (!ctrl2.IsKeyboard && ctrl2.IsXInputDevice && !_forceSDL)
                {
                    techPadNumber2 = "XInput-" + ctrl2.XInput.DeviceIndex + "/";
                    gamepad2 = true;
                }
                else if (!ctrl2.IsKeyboard)
                {
                    techPadNumber2 = "SDL-" + (ctrl2.SdlController == null ? ctrl2.DeviceIndex : ctrl2.SdlController.Index) + "/";
                    gamepad2 = true;
                }
                else
                    techPadNumber2 = "Keyboard/";
            }

            string padNumber1 = "Pad1";
            string padNumber2 = "Pad2";

            if (SystemConfig.isOptSet("duck_gunp2") && SystemConfig.getOptBoolean("duck_gunp2"))
            {
                padNumber1 = "Pad2";
                _multigun = false;
            }

            ini.WriteValue("ControllerPorts", "MultitapMode", "Disabled");

            // Guncon configuration
            // Player 1
            if (SystemConfig.isOptSet("duck_gunindex1") && !string.IsNullOrEmpty(SystemConfig["duck_gunindex1"]))
                pointer1 = "Pointer-" + SystemConfig["duck_gunindex1"];

            string gunType1 = "GunCon";
            bool justifier1 = false;
            if (SystemConfig["duck_controller1"] == "Justifier")
            {
                gunType1 = "Justifier";
                justifier1 = true;
            }

            ini.WriteValue(padNumber1, "Type", gunType1);
            ini.WriteValue(padNumber1, "Pointer", pointer1);
            ini.WriteValue(padNumber1, "Trigger", guninvert ? pointer1 + "/RightButton" : pointer1 + "/LeftButton");

            ConfigureGunButtons(ini, padNumber1, pointer1, 1, justifier1, guninvert, GetGunFromPointer(guns, pointer1), ctrl1, techPadNumber1, gamepad1);

            string crosshairSize = "0.500000";
            if (SystemConfig.isOptSet("duck_crosshair") && !string.IsNullOrEmpty(SystemConfig["duck_crosshair"]))
            {
                if (float.TryParse(SystemConfig["duck_crosshair"], NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                    crosshairSize = scale.ToString("0.000000", CultureInfo.InvariantCulture);
            }
            ini.WriteValue(padNumber1, "CrosshairScale", crosshairSize);
            BindIniFeature(ini, padNumber1, "CrosshairColor", "duck_crosshair_color1", "#00ff00");

            BindIniFeatureSlider(ini, padNumber1, "XScale", "duck_gun_xscale", "0.930000", 6);

            // Crosshair
            ini.Remove(padNumber1, "CrosshairImagePath");
            string crosshairPath = Path.Combine(Path.Combine(AppConfig.GetFullPath("duckstation"), "cross"));
            if (!Directory.Exists(crosshairPath)) try { Directory.CreateDirectory(crosshairPath); }
                catch { }
            string crosshairFile = Path.Combine(crosshairPath, "crosshair.png");

            if (!File.Exists(crosshairFile))
                SimpleLogger.Instance.Info("[GUNS] No crosshair file found in " + crosshairFile);

            if (SystemConfig.isOptSet("duck_custom_crosshair") && SystemConfig.getOptBoolean("duck_custom_crosshair") && File.Exists(crosshairFile))
            {
                ini.WriteValue(padNumber1, "CrosshairImagePath", crosshairFile);
                ini.WriteValue("Main", "HideCursorInFullscreen", "true");
            }

            // Gun 2
            if (_multigun)
            {
                string gunType2 = "GunCon";
                bool justifier2 = false;
                if (SystemConfig["duck_controller2"] == "Justifier")
                {
                    gunType2 = "Justifier";
                    justifier2 = true;
                }

                if (SystemConfig.isOptSet("duck_gunindex2") && !string.IsNullOrEmpty(SystemConfig["duck_gunindex2"]))
                    pointer2 = "Pointer-" + SystemConfig["duck_gunindex2"];

                ini.WriteValue(padNumber2, "Type", gunType2);
                ini.WriteValue(padNumber2, "Pointer", pointer2);
                ini.WriteValue(padNumber2, "Trigger", guninvert ? pointer2 + "/RightButton" : pointer2 + "/LeftButton");

                ConfigureGunButtons(ini, padNumber2, pointer2, 2, justifier2, guninvert, GetGunFromPointer(guns, pointer2), ctrl2, techPadNumber2, gamepad2);

                string crosshairSize2 = "0.500000";
                if (SystemConfig.isOptSet("duck_crosshair") && !string.IsNullOrEmpty(SystemConfig["duck_crosshair"]))
                {
                    if (float.TryParse(SystemConfig["duck_crosshair"], NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                        crosshairSize2 = scale.ToString("0.000000", CultureInfo.InvariantCulture);
                }
                ini.WriteValue(padNumber2, "CrosshairScale", crosshairSize2);

                BindIniFeatureSlider(ini, padNumber2, "XScale", "duck_gun_xscale", "0.930000", 6);
                BindIniFeature(ini, padNumber2, "CrosshairColor", "duck_crosshair_color2", "#00ff00");

                // Crosshair
                ini.Remove(padNumber2, "CrosshairImagePath");
                
                if (!File.Exists(crosshairFile))
                    SimpleLogger.Instance.Info("[GUNS] No crosshair file found in " + crosshairFile);

                if (SystemConfig.isOptSet("duck_custom_crosshair") && SystemConfig.getOptBoolean("duck_custom_crosshair") && File.Exists(crosshairFile))
                {
                    ini.WriteValue(padNumber2, "CrosshairImagePath", crosshairFile);
                    ini.WriteValue("Main", "HideCursorInFullscreen", "true");
                }
            }
        }

        /// <summary>
        /// GunCon A/B (Justifier Start/Back) and offscreen shot mapping: the "duck_gun_ab" option has priority, then the DolphinBar layout, then mouse buttons.
        /// </summary>
        private void ConfigureGunButtons(IniFile ini, string pad, string pointer, int player, bool justifier, bool guninvert, RawLightgun gun, Controller ctrl, string techPad, bool gamepad)
        {
            string buttonA = justifier ? "Start" : "A";
            string buttonB = justifier ? "Back" : "B";
            string reloadClick = guninvert ? pointer + "/LeftButton" : pointer + "/RightButton";
            string gunAB = SystemConfig["duck_gun_ab"];

            if (gunAB == "controller_1")
            {
                if (gamepad)
                {
                    ini.WriteValue(pad, "A", techPad + GetInputKeyName(ctrl, InputKey.a));
                    ini.WriteValue(pad, "B", techPad + GetInputKeyName(ctrl, InputKey.b));
                    ini.WriteValue(pad, "Start", techPad + GetInputKeyName(ctrl, InputKey.start));
                    ini.WriteValue(pad, "Back", techPad + GetInputKeyName(ctrl, InputKey.select));
                }
                else
                {
                    ini.WriteValue(pad, buttonA, "Keyboard/PageUp");
                    ini.WriteValue(pad, buttonB, "Keyboard/PageDown");
                }
                ini.WriteValue(pad, "ShootOffscreen", reloadClick);
            }
            else if (gunAB == "key_2")
            {
                ini.WriteValue(pad, buttonA, "Keyboard/K");
                ini.WriteValue(pad, buttonB, "Keyboard/L");
                ini.WriteValue(pad, "ShootOffscreen", reloadClick);
            }
            else if (gunAB == "key_6")
            {
                // Wiimote4Guns: each player keyboard sends 1/5, 2/6...
                ini.WriteValue(pad, buttonA, "Keyboard/" + player);
                ini.WriteValue(pad, buttonB, "Keyboard/" + (player + 4));
                ini.WriteValue(pad, "ShootOffscreen", reloadClick);
            }
            else if (gun != null && gun.Type == RawLighGunType.MayFlashWiimote)
            {
                // DolphinBar: A = reload, D-pad Right/Left = A/B (Start/Back), same keys in DolphinBar modes 1 and 2
                // DuckStation does not tell keyboards apart (host keyboard keys only), so player 2 uses D-pad Up/Down
                ini.WriteValue(pad, "ShootOffscreen", reloadClick);
                ini.WriteValue(pad, buttonA, player == 1 ? "Keyboard/Right" : "Keyboard/Up");
                ini.WriteValue(pad, buttonB, player == 1 ? "Keyboard/Left" : "Keyboard/Down");
            }
            else if (SystemConfig.getOptBoolean("gun_reload_button"))
            {
                ini.WriteValue(pad, "ShootOffscreen", reloadClick);
                ini.WriteValue(pad, buttonA, pointer + "/MiddleButton");
                ini.WriteValue(pad, buttonB, "Keyboard/" + player);
            }
            else
            {
                ini.WriteValue(pad, "ShootOffscreen", "Keyboard/" + player);
                ini.WriteValue(pad, buttonA, reloadClick);
                ini.WriteValue(pad, buttonB, pointer + "/MiddleButton");
            }
        }

        // Gun behind a DuckStation pointer ("Pointer-N" = gun index N), or the only connected gun
        private static RawLightgun GetGunFromPointer(RawLightgun[] guns, string pointer)
        {
            if (guns.Length == 1)
                return guns[0];

            if (pointer != null && pointer.StartsWith("Pointer-") && int.TryParse(pointer.Substring(8), out int index))
                return guns.FirstOrDefault(g => g.Index == index);

            return null;
        }
    }
}