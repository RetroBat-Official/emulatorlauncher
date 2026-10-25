using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{   
    partial class DaphneGenerator : Generator
    {
        private void CreateControllerConfiguration(string path, bool singe)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            //if (!this.Controllers.Any(c => !c.IsKeyboard))
            //    return;

            string iniFile = Path.Combine(path, "hypinput.ini");

            if (!File.Exists(iniFile))
                File.WriteAllText(iniFile, "");

            Controller ctrl = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                try
                {
                    // Hypseus takes one joystick button and one axis per direction. Writing only the
                    // stick's axis leaves a pad whose d-pad is reported as buttons (DualSense: 12-15)
                    // without any d-pad, so the d-pad button goes in the button slot.
                    string dpadUp = GetDirectionButton(ctrl, InputKey.up), dpadDown = GetDirectionButton(ctrl, InputKey.down);
                    string dpadLeft = GetDirectionButton(ctrl, InputKey.left), dpadRight = GetDirectionButton(ctrl, InputKey.right);
                    if (SystemConfig.isOptSet("hypseus_flight") && SystemConfig.getOptBoolean("hypseus_flight"))
                    {
                        ini.WriteValue("KEYBOARD", "KEY_UP", "SDLK_DOWN 0 " + dpadDown + " " + GetInputKeyName(ctrl, InputKey.joystick1down));
                        ini.WriteValue("KEYBOARD", "KEY_DOWN", "SDLK_UP 0 " + dpadUp + " " + GetInputKeyName(ctrl, InputKey.joystick1up));
                    }
                    else
                    {
                        ini.WriteValue("KEYBOARD", "KEY_UP", "SDLK_UP 0 " + dpadUp + " " + GetInputKeyName(ctrl, InputKey.joystick1up));
                        ini.WriteValue("KEYBOARD", "KEY_DOWN", "SDLK_DOWN 0 " + dpadDown + " " + GetInputKeyName(ctrl, InputKey.joystick1down));
                    }
                    ini.WriteValue("KEYBOARD", "KEY_LEFT", "SDLK_LEFT 0 " + dpadLeft + " " + GetInputKeyName(ctrl, InputKey.joystick1left));
                    ini.WriteValue("KEYBOARD", "KEY_RIGHT", "SDLK_RIGHT 0 " + dpadRight + " " + GetInputKeyName(ctrl, InputKey.joystick1right));
                    SimpleLogger.Instance.Info("[CONTROLLERS] Hypseus directions: d-pad buttons up/down/left/right = " + dpadUp + "/" + dpadDown + "/" + dpadLeft + "/" + dpadRight + " (0 = not a button on this pad), stick axes as before");

                    // DolphinBar Wiimotes (mode 1 or 2) send keyboard keys. Hypseus reads all keyboards as one,
                    // so the second Wiimote uses other directions (second key slot of each action, Singe games only)
                    int wiimotes = singe ? RawLightgun.GetRawLightguns().Count(g => g.Type == RawLighGunType.MayFlashWiimote) : 0;

                    ini.WriteValue("KEYBOARD", "KEY_COIN1", "SDLK_5 " + (wiimotes > 0 ? "SDLK_LEFT" : "0") + " " + GetInputKeyName(ctrl, InputKey.select));
                    ini.WriteValue("KEYBOARD", "KEY_COIN2", "SDLK_6 " + (wiimotes > 1 ? "SDLK_DOWN" : "0") + " 0");
                    ini.WriteValue("KEYBOARD", "KEY_START1", "SDLK_1 " + (wiimotes > 0 ? "SDLK_RIGHT" : "0") + " " + GetInputKeyName(ctrl, InputKey.start));
                    ini.WriteValue("KEYBOARD", "KEY_START2", "SDLK_2 " + (wiimotes > 1 ? "SDLK_UP" : "0") + " 0");
                    ini.WriteValue("KEYBOARD", "KEY_BUTTON1", "SDLK_LCTRL 0 " + GetInputKeyName(ctrl, InputKey.a));
                    ini.WriteValue("KEYBOARD", "KEY_BUTTON2", "SDLK_LALT 0 " + GetInputKeyName(ctrl, InputKey.b));
                    ini.WriteValue("KEYBOARD", "KEY_BUTTON3", "SDLK_SPACE 0 " + GetInputKeyName(ctrl, InputKey.pagedown));
                    ini.WriteValue("KEYBOARD", "KEY_SKILL1", "SDLK_LSHIFT 0 " + GetInputKeyName(ctrl, InputKey.y));
                    ini.WriteValue("KEYBOARD", "KEY_SKILL2", "SDLK_z 0 " + GetInputKeyName(ctrl, InputKey.x));
                    ini.WriteValue("KEYBOARD", "KEY_SKILL3", "SDLK_x 0 " + GetInputKeyName(ctrl, InputKey.pageup));
                    ini.WriteValue("KEYBOARD", "KEY_SERVICE", "SDLK_9 0 " + GetInputKeyName(ctrl, InputKey.l3));
                    ini.WriteValue("KEYBOARD", "KEY_TEST", "SDLK_F2 SDLK_F4 " + GetInputKeyName(ctrl, InputKey.r3));
                    ini.WriteValue("KEYBOARD", "KEY_RESET", "SDLK_0 0 0");
                    ini.WriteValue("KEYBOARD", "KEY_SCREENSHOT", "SDLK_F12 0 0");
                    ini.WriteValue("KEYBOARD", "KEY_QUIT", "SDLK_ESCAPE 0 0");
                    ini.WriteValue("KEYBOARD", "KEY_PAUSE", "SDLK_p 0 0");
                    ini.WriteValue("KEYBOARD", "KEY_CONSOLE", "SDLK_BACKSLASH 0 0");
                    ini.WriteValue("KEYBOARD", "KEY_TILT", "SDLK_t 0 0");

                    foreach (string key in new[] { "SCREEN", "FULLSCREEN", "MANYMOUSE", "LINEARSCALE", "GRAPHICAPI" })
                        ini.Remove("GLOBAL", key);
                    ini.WriteValue("GLOBAL", "GAMEPAD", "FALSE");

                    ini.Save();
                }
                catch { }
            }

            EnsureSectionEndMarkers(iniFile);
        }

        // Hypseus reads [KEYBOARD], [MOUSE] and [GLOBAL] up to an "END" line. IniFile drops these value-less
        // lines on save: restore one after the last entry of every section, otherwise Hypseus 3.x aborts
        // the [KEYBOARD] parsing on the next section header ("Corrupt config file").
        private static void EnsureSectionEndMarkers(string iniFile)
        {
            try
            {
                var lines = File.ReadAllLines(iniFile)
                    .Where(l => !l.Trim().Equals("END", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                bool inSection = false;
                int lastContent = -1;

                for (int i = 0; i <= lines.Count; i++)
                {
                    // A virtual header after the last line closes the last section
                    string trimmed = i < lines.Count ? lines[i].Trim() : "[";

                    if (trimmed.StartsWith("["))
                    {
                        if (inSection)
                        {
                            lines.Insert(lastContent + 1, "END");
                            i++;
                        }

                        if (i >= lines.Count)
                            break;

                        inSection = true;
                        lastContent = i;
                        continue;
                    }

                    if (trimmed.Length > 0 && !trimmed.StartsWith("#") && !trimmed.StartsWith(";"))
                        lastContent = i;
                }

                File.WriteAllLines(iniFile, lines);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Unable to write END markers in hypinput.ini: " + ex.Message);
            }
        }

        // The d-pad direction as a Hypseus joystick-button code, or "0" when the pad reports
        // it as a hat or nothing (Hypseus handles hats on its own, see -openhat).
        private static string GetDirectionButton(Controller c, InputKey key)
        {
            var input = c == null ? null : c.Config[key];
            if (input == null || input.Type != "button")
                return "0";
            return GetInputKeyName(c, key).Trim();
        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            key = key.GetRevertedAxis(out bool revertAxis);

            int index = c.SdlController != null ? c.SdlController.Index : c.DeviceIndex;

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button" && input.Id < 9)
                    return index + "0" + (input.Id + 1).ToString();
                
                else if (input.Type == "button")
                    return index + (input.Id + 1).ToString();
                
                else if (input.Type == "axis")
                {
                    if (revertAxis)
                        return "+" + index + "0" + (input.Id + 1).ToString();
                    else
                        return "-" + index + "0" + (input.Id + 1).ToString();
                }

                else if(input.Type == "hat")
                    return " 0";
            }

            return " 0";
        }
    }
}