using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using System.Configuration;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class DuckstationGenerator : Generator
    {
        private bool _sindenSoft = false;
        private bool _multigun = false;

        private void CreateGunConfiguration(IniFile ini)
        {
            bool gun = SystemConfig.getOptBoolean("use_guns");


            if (!gun)
                return;

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
                _sindenSoft = true;
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
            string tech1 = "";
            string tech2 = "";
            bool gamepad1 = false;
            bool gamepad2 = false;
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");

            if (ctrl1 != null)
            {
                if (!ctrl1.IsKeyboard && ctrl1.IsXInputDevice && !_forceSDL)
                {
                    techPadNumber1 = "XInput-" + ctrl1.XInput.DeviceIndex + "/";
                    tech1 = "XInput";
                    gamepad1 = true;
                }
                else if (!ctrl1.IsKeyboard)
                {
                    techPadNumber1 = "SDL-" + (ctrl1.SdlController == null ? ctrl1.DeviceIndex : ctrl1.SdlController.Index) + "/";
                    tech1 = "SDL";
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
                    tech2 = "XInput";
                    gamepad2 = true;
                }
                else if (!ctrl2.IsKeyboard)
                {
                    techPadNumber2 = "SDL-" + (ctrl2.SdlController == null ? ctrl2.DeviceIndex : ctrl2.SdlController.Index) + "/";
                    tech2 = "SDL";
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

            // Define mapping for A and B buttons (default is mouse right click and middle click)
            if (SystemConfig.isOptSet("duck_gun_ab") && !string.IsNullOrEmpty(SystemConfig["duck_gun_ab"]) && SystemConfig["duck_gun_ab"] == "controller_1")
            {
                if (gamepad1)
                {
                    ini.WriteValue(padNumber1, "A", techPadNumber1 + GetInputKeyName(ctrl1, InputKey.a, tech1));
                    ini.WriteValue(padNumber1, "B", techPadNumber1 + GetInputKeyName(ctrl1, InputKey.b, tech1));
                    ini.WriteValue(padNumber1, "Start", techPadNumber1 + GetInputKeyName(ctrl1, InputKey.start, tech1));
                    ini.WriteValue(padNumber1, "Back", techPadNumber1 + GetInputKeyName(ctrl1, InputKey.select, tech1));
                    ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
                }
                else
                {
                    ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/PageUp");
                    ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/PageDown");
                    ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
                }
            }
            else if (SystemConfig["duck_gun_ab"] == "key_1")
            {
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/PageUp");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/PageDown");
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_2")
            {
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/K");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/L");
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_3")
            {
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/Left");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/Right");
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_4")
            {
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/Left");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/Return");
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_5")
            {
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/VolumeUp");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/VolumeDown");
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
            }
            else if (SystemConfig["duck_gun_ab"] == "key_6")
            {
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", "Keyboard/1");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/5");
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
            }
            else if(SystemConfig.isOptSet("gun_reload_button") && SystemConfig.getOptBoolean("gun_reload_button"))
            {
                ini.WriteValue(padNumber1, "ShootOffscreen", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", pointer1 + "/MiddleButton");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", "Keyboard/1");
            }
            else
            {
                ini.WriteValue(padNumber1, "ShootOffscreen", "Keyboard/1");
                ini.WriteValue(padNumber1, justifier1 ? "Start" : "A", guninvert ? pointer1 + "/LeftButton" : pointer1 + "/RightButton");
                ini.WriteValue(padNumber1, justifier1 ? "Back" : "B", pointer1 + "/MiddleButton");
            }

            string crosshairSize = "0.500000";
            if (SystemConfig.isOptSet("duck_crosshair") && !string.IsNullOrEmpty(SystemConfig["duck_crosshair"]))
                crosshairSize = SystemConfig["duck_crosshair"].Substring(0, SystemConfig["duck_crosshair"].Length - 4);
            ini.WriteValue(padNumber1, "CrosshairScale", crosshairSize);
            
            ini.WriteValue(padNumber1, "XScale", "0.930000");    // Adjust Xscale for mouse calibration

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

                // Define mapping for A and B buttons (default is mouse right click and middle click)
                if (SystemConfig.isOptSet("duck_gun_ab") && !string.IsNullOrEmpty(SystemConfig["duck_gun_ab"]) && SystemConfig["duck_gun_ab"] == "controller_1")
                {
                    if (gamepad2)
                    {
                        ini.WriteValue(padNumber2, "A", techPadNumber2 + GetInputKeyName(ctrl2, InputKey.a, tech2));
                        ini.WriteValue(padNumber2, "B", techPadNumber2 + GetInputKeyName(ctrl2, InputKey.b, tech2));
                        ini.WriteValue(padNumber2, "Start", techPadNumber2 + GetInputKeyName(ctrl2, InputKey.start, tech2));
                        ini.WriteValue(padNumber2, "Back", techPadNumber2 + GetInputKeyName(ctrl2, InputKey.select, tech2));
                        ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                    }
                    else
                    {
                        ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/PageUp");
                        ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/PageDown");
                        ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                    }
                }
                else if (SystemConfig["duck_gun_ab"] == "key_1")
                {
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/PageUp");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/PageDown");
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                }
                else if (SystemConfig["duck_gun_ab"] == "key_2")
                {
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/K");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/L");
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                }
                else if (SystemConfig["duck_gun_ab"] == "key_3")
                {
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/Left");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/Right");
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                }
                else if (SystemConfig["duck_gun_ab"] == "key_4")
                {
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/Left");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/Return");
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                }
                else if (SystemConfig["duck_gun_ab"] == "key_5")
                {
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/VolumeUp");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/VolumeDown");
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                }
                else if (SystemConfig["duck_gun_ab"] == "key_6")
                {
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", "Keyboard/2");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/6");
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                }
                else if (SystemConfig.isOptSet("gun_reload_button") && SystemConfig.getOptBoolean("gun_reload_button"))
                {
                    ini.WriteValue(padNumber2, "ShootOffscreen", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", pointer2 + "/MiddleButton");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", "Keyboard/2");
                }
                else
                {
                    ini.WriteValue(padNumber2, "ShootOffscreen", "Keyboard/2");
                    ini.WriteValue(padNumber2, justifier2 ? "Start" : "A", guninvert ? pointer2 + "/LeftButton" : pointer2 + "/RightButton");
                    ini.WriteValue(padNumber2, justifier2 ? "Back" : "B", pointer2 + "/MiddleButton");
                }

                string crosshairSize2 = "0.500000";
                if (SystemConfig.isOptSet("duck_crosshair") && !string.IsNullOrEmpty(SystemConfig["duck_crosshair"]))
                    crosshairSize2 = SystemConfig["duck_crosshair"].Substring(0, SystemConfig["duck_crosshair"].Length - 4);
                ini.WriteValue(padNumber2, "CrosshairScale", crosshairSize2);

                ini.WriteValue(padNumber2, "XScale", "0.930000");    // Adjust Xscale for mouse calibration

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
    }
}