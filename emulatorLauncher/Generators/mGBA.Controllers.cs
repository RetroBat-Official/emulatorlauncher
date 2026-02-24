using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EmulatorLauncher
{
    partial class MGBAGenerator : Generator
    {
        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for mGBA");

            if (this.Controllers.Count == 0)
                return;

            var c1 = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            if (c1 == null)
                return;

            if (c1.Config == null)
                return;

            bool invertButtons = Program.SystemConfig.getOptBoolean("buttonsInvert");
            
            // Clearing
            ini.ClearSection("gba.input.QT_K");
            ini.ClearSection("gba.input.SDLB");

            foreach (var s in ini.EnumerateSections().Where(s => s.StartsWith("gba.input-profile.")))
                ini.ClearSection(s);

            // Joystick
            string guid = c1.GetSdlGuid(SdlVersion.SDL2_26, true).ToLowerInvariant();
            string section = "gba.input.SDLB";

            ini.WriteValue(section, "device0", guid);

            WriteKeyConfig(c1, InputKey.up, ini, section, "keyUp", "axisUpAxis", "hat0Up", "axisUpValue");
            WriteKeyConfig(c1, InputKey.down, ini, section, "keyDown", "axisDownAxis", "hat0Down", "axisDownValue");
            WriteKeyConfig(c1, InputKey.left, ini, section, "keyLeft", "axisLeftAxis", "hat0Left", "axisLeftValue");
            WriteKeyConfig(c1, InputKey.right, ini, section, "keyRight", "axisRightAxis", "hat0Right", "axisRightValue");
            WriteKeyConfig(c1, InputKey.leftanalogup, ini, section, "keyUp", "axisUpAxis", "hat0Up", "axisUpValue");
            WriteKeyConfig(c1, InputKey.leftanalogdown, ini, section, "keyDown", "axisDownAxis", "hat0Down", "axisDownValue");
            WriteKeyConfig(c1, InputKey.leftanalogleft, ini, section, "keyLeft", "axisLeftAxis", "hat0Left", "axisLeftValue");
            WriteKeyConfig(c1, InputKey.leftanalogright, ini, section, "keyRight", "axisRightAxis", "hat0Right", "axisRightValue");

            WriteKeyConfig(c1, InputKey.pageup, ini, section, "keyL", "axisLAxis", null, "axisLValue");
            WriteKeyConfig(c1, InputKey.pagedown, ini, section, "keyR", "axisRAxis", null, "axisRValue");

            WriteKeyConfig(c1, InputKey.start, ini, section, "keyStart", "axisStartAxis", null, "axisStartValue");
            WriteKeyConfig(c1, InputKey.select, ini, section, "keySelect", "axisSelectAxis", null, "axisSelectValue");

            if (invertButtons)
            {
                WriteKeyConfig(c1, InputKey.b, ini, section, "keyB", "axisBAxis", null, "axisBValue");
                WriteKeyConfig(c1, InputKey.a, ini, section, "keyA", "axisAAxis", null, "axisAValue");
            }
            else
            {
                WriteKeyConfig(c1, InputKey.a, ini, section, "keyB", "axisBAxis", null, "axisBValue");
                WriteKeyConfig(c1, InputKey.b, ini, section, "keyA", "axisAAxis", null, "axisAValue");
            }

            ini.WriteValue(section, "tiltAxisY", "3");
            ini.WriteValue(section, "gyroAxisX", "0");
            ini.WriteValue(section, "gyroAxisZ", "-1");
            ini.WriteValue(section, "gyroSensitivity", "2.2e+09");
            ini.WriteValue(section, "tiltAxisX", "2");
            ini.WriteValue(section, "gyroAxisY", "1");

            //Keyboard
            ConfigureKeyboard(ini);
        }

        private void ConfigureKeyboard(IniFile ini)
        {
            string section = "gba.input.QT_K";

            ini.WriteValue(section, "keyRight", "16777236");
            ini.WriteValue(section, "keyDown", "16777237");
            ini.WriteValue(section, "keyUp", "16777235");
            ini.WriteValue(section, "keySelect", "16777219");
            ini.WriteValue(section, "keyLeft", "16777234");
            ini.WriteValue(section, "keyStart", "16777220");

            List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
            {
                ini.WriteValue(section, "keyR", "65");  //A
                ini.WriteValue(section, "keyL", "90");  //Z
                ini.WriteValue(section, "keyB", "81");  //Q
                ini.WriteValue(section, "keyA", "83");  //S
            }
            else
            {
                ini.WriteValue(section, "keyR", "87");  //W
                ini.WriteValue(section, "keyL", "81");  //Q
                ini.WriteValue(section, "keyB", "65");  //A
                ini.WriteValue(section, "keyA", "83");  //S
            }
        }

        private bool GetInputType(Controller c, InputKey key, out string type)
        {
            key = key.GetRevertedAxis(out bool revertAxis);

            type = "button";
            var input = c.Config[key];
            if (input == null)
                return false;

            if (input.Type == "button")
            {
                type = "button";
                return true;
            }

            else if (input.Type == "hat")
            {
                type = "hat";
                return true;
            }

            else if (input.Type == "axis")
            {
                type = "axis";
                return true;
            }

            return false;
        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            Int64 pid;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                    return input.Id.ToString(); 

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) 
                        return "+" + pid.ToString();
                    else 
                        return "-" + pid.ToString();
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "6";
                        case 2: return "4";
                        case 4: return "7";
                        case 8: return "5";
                    }
                }
            }
            return "";
        }

        private void WriteKeyConfig(Controller c, InputKey key, IniFile ini, string section, string iniButtonKey = null, string iniAxisKey = null, string iniHatKey = null, string iniAxisValue = null)
        {
            if (GetInputType(c, key, out string type))
            {
                string value = GetInputKeyName(c, key);

                if (value != null)
                {
                    switch (type)
                    {
                        case "button":
                            if (iniButtonKey != null)
                                ini.WriteValue(section, iniButtonKey, value);
                            break;
                        case "axis":
                            if (iniAxisKey != null)
                                ini.WriteValue(section, iniAxisKey, value);
                            string sign = value.Substring(0, 1);
                            if (iniAxisValue != null)
                                ini.WriteValue(section, iniAxisValue, sign + "12288");
                            break;
                        case "hat":
                            if (iniHatKey != null)
                                ini.WriteValue(section, iniHatKey, value);
                            break;
                    }
                }
            }
        }
    }
}
