using System;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class MelonDSGenerator
    {
        /// <summary>
        /// Create controller configuration
        /// </summary>
        /// <param name="ini"></param>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (Program.Controllers.Count == 0)
                return;

            var ctrl = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

            if (ctrl.IsKeyboard)
                WriteKeyboardMapping(ini, ctrl);
            else
                WriteJoystickMapping(ini, ctrl);
        }

        /// <summary>
        /// Gamepad
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="c1"></param>
        // All mappings generated here are detailed in the wiki, this is the most balanced mapping after intensive testing, if users need different mapping they might disable autoconfiguration
        private void WriteJoystickMapping(IniFile ini, Controller ctrl)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            int index = (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index);

            ini.WriteValue("", "JoystickID", index.ToString());
            ini.WriteValue("", "Joy_A", GetInputKeyName(ctrl, InputKey.b));
            ini.WriteValue("", "Joy_B", GetInputKeyName(ctrl, InputKey.a));
            ini.WriteValue("", "Joy_Select", GetInputKeyName(ctrl, InputKey.select));
            ini.WriteValue("", "Joy_Start", GetInputKeyName(ctrl, InputKey.start));
            
            if (SystemConfig.isOptSet("melonds_leftstick") && SystemConfig.getOptBoolean("melonds_leftstick"))
            {
                ini.WriteValue("", "Joy_Right", GetInputKeyName(ctrl, InputKey.leftanalogright));
                ini.WriteValue("", "Joy_Left", GetInputKeyName(ctrl, InputKey.leftanalogleft));
                ini.WriteValue("", "Joy_Up", GetInputKeyName(ctrl, InputKey.leftanalogup));
                ini.WriteValue("", "Joy_Down", GetInputKeyName(ctrl, InputKey.leftanalogdown));
            }
            else
            {
                ini.WriteValue("", "Joy_Right", GetInputKeyName(ctrl, InputKey.right));
                ini.WriteValue("", "Joy_Left", GetInputKeyName(ctrl, InputKey.left));
                ini.WriteValue("", "Joy_Up", GetInputKeyName(ctrl, InputKey.up));
                ini.WriteValue("", "Joy_Down", GetInputKeyName(ctrl, InputKey.down));
            }

            ini.WriteValue("", "Joy_R", GetInputKeyName(ctrl, InputKey.pagedown));
            ini.WriteValue("", "Joy_L", GetInputKeyName(ctrl, InputKey.pageup));
            ini.WriteValue("", "Joy_X", GetInputKeyName(ctrl, InputKey.x));
            ini.WriteValue("", "Joy_Y", GetInputKeyName(ctrl, InputKey.y));

        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                    return input.Id.ToString();

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return (131071 + (0 << 20) + (pid << 24)).ToString();
                            else return (131071 + (1 << 20) + (pid << 24)).ToString();
                        case 4:
                        case 5: return (131071 + (2 << 20) + (pid << 24)).ToString();
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value + 256;
                    return pid.ToString();    
                }
            }
            return "-1";
        }

        /// <summary>
        /// Keyboard
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="ctrl"></param>
        private void WriteKeyboardMapping(IniFile ini, Controller ctrl)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            InputConfig keyboard = ctrl.Config;

            Action<string, InputKey> WriteKeyboardMapping = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                    ini.WriteValue("", v, SdlToQTCode(a.Id));
            };

            WriteKeyboardMapping("Key_A", InputKey.b);
            WriteKeyboardMapping("Key_B", InputKey.a);
            WriteKeyboardMapping("Key_Select", InputKey.select);
            WriteKeyboardMapping("Key_Start", InputKey.start);
            WriteKeyboardMapping("Key_Right", InputKey.right);
            WriteKeyboardMapping("Key_Left", InputKey.left);
            WriteKeyboardMapping("Key_Up", InputKey.up);
            WriteKeyboardMapping("Key_Down", InputKey.down);
            WriteKeyboardMapping("Key_R", InputKey.pagedown);
            WriteKeyboardMapping("Key_L", InputKey.pageup);
            WriteKeyboardMapping("Key_X", InputKey.x);
            WriteKeyboardMapping("Key_Y", InputKey.y);
        }

        private static string SdlToQTCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x00: return "-1";         // Unknown
                case 0x08: return "16777219";   // Backspace
                case 0x09: return "16777217";   // Tab
                case 0x0D: return "16777220";   // Return
                case 0x1B: return "16777216";   // Esc
                case 0x20: return "32";         // Space
                case 0x21: return "33";         // Exclam !
                case 0x22: return "34";         // double quote "
                case 0x23: return "35";         // Hash #
                case 0x24: return "36";
                case 0x25: return "37";
                case 0x26: return "38";
                case 0x27: return "39";
                case 0x28: return "40";
                case 0x29: return "41";
                case 0x2A: return "42";
                case 0x2B: return "43";
                case 0x2C: return "44";
                case 0x2D: return "45";
                case 0x2E: return "46";
                case 0x2F: return "47";
                case 0x30: return "48";
                case 0x31: return "49";
                case 0x32: return "50";
                case 0x33: return "51";
                case 0x34: return "52";
                case 0x35: return "53";
                case 0x36: return "54";
                case 0x37: return "55";
                case 0x38: return "56";
                case 0x39: return "57";
                case 0x3A: return "58";         // Colon
                case 0x3B: return "59";
                case 0x3C: return "60";
                case 0x3D: return "61";
                case 0x3E: return "62";
                case 0x3F: return "63";
                case 0x40: return "64";
                case 0x5B: return "91";
                case 0x5C: return "92";
                case 0x5D: return "93";
                case 0x5E: return "94";
                case 0x5F: return "95";
                case 0x60: return "96";
                case 0x61: return "65";
                case 0x62: return "66";
                case 0x63: return "67";
                case 0x64: return "68";
                case 0x65: return "69";
                case 0x66: return "70";
                case 0x67: return "71";
                case 0x68: return "72";
                case 0x69: return "73";
                case 0x6A: return "74";
                case 0x6B: return "75";
                case 0x6C: return "76";
                case 0x6D: return "77";
                case 0x6E: return "78";
                case 0x6F: return "79";
                case 0x70: return "80";
                case 0x71: return "81";
                case 0x72: return "82";
                case 0x73: return "83";
                case 0x74: return "84";
                case 0x75: return "85";
                case 0x76: return "86";
                case 0x77: return "87";
                case 0x78: return "88";
                case 0x79: return "89";
                case 0x7A: return "90";
                case 0x7F: return "16777223";
                case 0x40000039: return "16777252";
                case 0x4000003A: return "16777264";
                case 0x4000003B: return "16777265";
                case 0x4000003C: return "16777266";
                case 0x4000003D: return "16777267";
                case 0x4000003E: return "16777268";
                case 0x4000003F: return "16777269";
                case 0x40000040: return "16777270";
                case 0x40000041: return "16777271";
                case 0x40000042: return "16777272";
                case 0x40000043: return "16777273";
                case 0x40000044: return "16777274";
                case 0x40000045: return "16777275";
                case 0x40000046: return "16777225";
                case 0x40000047: return "16777254";
                case 0x40000048: return "16777224";
                case 0x40000049: return "16777222";
                case 0x4000004A: return "16777232";
                case 0x4000004B: return "16777238";
                case 0x4000004D: return "16777233";
                case 0x4000004E: return "16777239";
                case 0x4000004F: return "16777236";
                case 0x40000050: return "16777234";
                case 0x40000051: return "16777237";
                case 0x40000052: return "16777235";
                case 0x40000053: return "553648165";
                case 0x40000054: return "536870959";
                case 0x40000055: return "536870954";
                case 0x40000056: return "536870957";
                case 0x40000057: return "536870955";
                case 0x40000058: return "553648133";
                case 0x40000059: return "536870961";
                case 0x4000005A: return "536870962";
                case 0x4000005B: return "536870963";
                case 0x4000005C: return "536870964";
                case 0x4000005D: return "536870965";
                case 0x4000005E: return "536870966";
                case 0x4000005F: return "536870967";
                case 0x40000060: return "536870968";
                case 0x40000061: return "536870969";
                case 0x40000062: return "536870960";
                case 0x40000063: return "536870958";
                case 0x40000067: return "-1";       // NUM EQUAL
                case 0x40000068: return "16777276";
                case 0x40000069: return "16777277";
                case 0x4000006A: return "16777278";
                case 0x4000006B: return "16777279";
                case 0x4000006C: return "16777280";
                case 0x4000006D: return "16777281";
                case 0x4000006E: return "16777282";
                case 0x4000006F: return "16777283";
                case 0x40000070: return "16777284";
                case 0x40000071: return "16777285";
                case 0x40000072: return "16777286";
                case 0x40000073: return "16777287";
                case 0x40000075: return "16777304";
                case 0x40000076: return "16777301";
                case 0x400000E0: return "16777249";
                case 0x400000E1: return "16777248";
                case 0x400000E2: return "16777251";
                case 0x400000E4: return "2130706399";
                case 0x400000E5: return "2130706400";
                case 0x400000E6: return "16777249";
            }
            return "-1";
        }
    }
}
