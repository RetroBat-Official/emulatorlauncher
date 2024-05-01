using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class JgenesisGenerator : Generator
    {
        private void SetupControllers(string jgenSystem)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (jgenSystem == "sega_cd")
                jgenSystem = "genesis";

            int maxPad = 1;
            if (systemMaxPad.ContainsKey(jgenSystem))
                maxPad = systemMaxPad[jgenSystem];

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(controller, controller.PlayerIndex, jgenSystem);
        }

        private void ConfigureInput(Controller controller, int playerIndex, string jgenSystem)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(controller.Config, jgenSystem);
            else
                ConfigureJoystick(controller, playerIndex, jgenSystem);
        }

        private void ConfigureJoystick(Controller ctrl, int playerIndex, string jgenSystem)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;

            bool revertButtons = SystemConfig.isOptSet("jgen_revertbuttons") && SystemConfig.getOptBoolean("jgen_revertbuttons");
            bool isXInput = ctrl.IsXInputDevice;

            if (playerIndex == 1)
                WriteHotkeys(index, isXInput);
        }

        private void ConfigureKeyboard(InputConfig keyboard, string jgenSystem)
        {
            if (keyboard == null)
                return;

        }

        private void WriteHotkeys(int index, bool isXInput)
        {

        }

        static readonly Dictionary<string, int> systemMaxPad = new Dictionary<string, int>()
        {
            { "smsgg", 2 },
            { "genesis", 2 },
            { "game_boy", 1 },
            { "nes", 2 },
            { "snes", 2 }
        };

        private static string SdlToKeyCode(long sdlCode)
        {

            //The following list of keys has been verified, ryujinx will not allow wrong string so do not add a key until the description has been tested in the emulator first
            switch (sdlCode)
            {
                case 0x0D: return "6";      // ENTER
                case 0x08: return "2";      // Backspace
                case 0x09: return "3";      // TAB
                case 0x20: return "18";     // SPACE
                case 0x2B: return "0";      // Plus
                case 0x2C: return "142";    // Comma
                case 0x2D: return "0";      // Minus
                case 0x2E: return "144";    // Period
                case 0x2F: return "145";    // Slash
                case 0x30: return "34";     // Number 0
                case 0x31: return "35";
                case 0x32: return "36";
                case 0x33: return "37";
                case 0x34: return "38";
                case 0x35: return "39";
                case 0x36: return "40";
                case 0x37: return "41";
                case 0x38: return "42";
                case 0x39: return "43";     // Number 9
                case 0x3B: return "140";    // Semi column
                case 0x61: return "44";     // A
                case 0x62: return "45";
                case 0x63: return "46";
                case 0x64: return "47";
                case 0x65: return "48";
                case 0x66: return "49";
                case 0x67: return "50";
                case 0x68: return "51";
                case 0x69: return "52";
                case 0x6A: return "53";
                case 0x6B: return "54";
                case 0x6C: return "55";
                case 0x6D: return "56";
                case 0x6E: return "57";
                case 0x6F: return "58";
                case 0x70: return "59";
                case 0x71: return "60";
                case 0x72: return "61";
                case 0x73: return "62";
                case 0x74: return "63";
                case 0x75: return "64";
                case 0x76: return "65";
                case 0x77: return "66";
                case 0x78: return "67";
                case 0x79: return "68";
                case 0x7A: return "69";             // Z
                case 0x7F: return "32";             // Delete
                case 0x4000003A: return "90";       // F1
                case 0x4000003B: return "91";
                case 0x4000003C: return "92";
                case 0x4000003D: return "93";
                case 0x4000003E: return "94";
                case 0x4000003F: return "95";
                case 0x40000040: return "96";
                case 0x40000041: return "97";
                case 0x40000042: return "98";
                case 0x40000043: return "99";
                case 0x40000044: return "100";
                case 0x40000045: return "101";      // F12
                case 0x40000047: return "0";        // Scrolllock
                case 0x40000048: return "0";        // Pause
                case 0x40000049: return "31";       // Insert
                case 0x4000004A: return "22";       // Home
                case 0x4000004B: return "19";       // PageUp
                case 0x4000004D: return "21";       // End
                case 0x4000004E: return "20";       // Page Down
                case 0x4000004F: return "25";       // Right  
                case 0x40000050: return "23";       // Left
                case 0x40000051: return "26";       // Down
                case 0x40000052: return "24";       // Up
                case 0x40000053: return "114";      // Numlock  
                case 0x40000054: return "89";       // KeypadDivide
                case 0x40000055: return "84";       // KeypadMultiply
                case 0x40000056: return "87";       // KeypadSubtract
                case 0x40000057: return "85";       // KeypadAdd
                case 0x40000058: return "6";        // Enter
                case 0x40000059: return "75";       // Numpad 1
                case 0x4000005A: return "76";
                case 0x4000005B: return "77";
                case 0x4000005C: return "78";
                case 0x4000005D: return "79";
                case 0x4000005E: return "80";
                case 0x4000005F: return "81";
                case 0x40000060: return "82";
                case 0x40000061: return "83";
                case 0x40000062: return "74";       // Numpad 0
                case 0x40000063: return "88";       // KeypadDecimal
                case 0x40000085: return "88";
                case 0x400000E0: return "118";      // Left control
                case 0x400000E1: return "116";      // Left shift
                case 0x400000E2: return "120";      // Left ALT
                case 0x400000E4: return "119";      // Right control
                case 0x400000E5: return "117";      // Right shift
            }
            return "0";
        }
    }
}
