using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{   
    partial class DaphneGenerator : Generator
    {
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

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
                    if (SystemConfig.isOptSet("hypseus_flight") && SystemConfig.getOptBoolean("hypseus_flight"))
                    {
                        ini.WriteValue("KEYBOARD", "KEY_UP", "SDLK_DOWN 0 0 " + GetInputKeyName(ctrl, InputKey.joystick1down));
                        ini.WriteValue("KEYBOARD", "KEY_DOWN", "SDLK_UP 0 0 " + GetInputKeyName(ctrl, InputKey.joystick1up));
                    }
                    else
                    {
                        ini.WriteValue("KEYBOARD", "KEY_UP", "SDLK_UP 0 0 " + GetInputKeyName(ctrl, InputKey.joystick1up));
                        ini.WriteValue("KEYBOARD", "KEY_DOWN", "SDLK_DOWN 0 0 " + GetInputKeyName(ctrl, InputKey.joystick1down));
                    }
                    ini.WriteValue("KEYBOARD", "KEY_LEFT", "SDLK_LEFT 0 0 "+ GetInputKeyName(ctrl, InputKey.joystick1left));
                    ini.WriteValue("KEYBOARD", "KEY_RIGHT", "SDLK_RIGHT 0 0 " + GetInputKeyName(ctrl, InputKey.joystick1right));
                    ini.WriteValue("KEYBOARD", "KEY_COIN1", "SDLK_5 0 " + GetInputKeyName(ctrl, InputKey.select));
                    ini.WriteValue("KEYBOARD", "KEY_COIN2", "SDLK_6 0 0");
                    ini.WriteValue("KEYBOARD", "KEY_START1", "SDLK_1 0 " + GetInputKeyName(ctrl, InputKey.start));
                    ini.WriteValue("KEYBOARD", "KEY_START2", "SDLK_2 0 0");
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
                    ini.Save();
                }
                catch { }
            }

            string lastLine = File.ReadLines(iniFile).LastOrDefault();

            if (lastLine != null && lastLine.Trim().Equals("END"))
                return;

            using (StreamWriter sw = File.AppendText(iniFile))
            {
                sw.WriteLine("END");
            }

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