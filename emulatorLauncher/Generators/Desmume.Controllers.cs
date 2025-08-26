using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace EmulatorLauncher
{
    partial class DesmumeGenerator
    {
        /// <summary>
        /// Create controller configuration
        /// </summary>
        /// <param name="ini"></param>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            if (Program.Controllers.Count == 0)
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Desmume");

            ini.ClearSection("Controls");

            var ctrl = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

            if (ctrl.IsKeyboard)
                WriteKeyboardMapping(ini, ctrl);
            else
                WriteJoystickMapping(ini, ctrl);

            WriteKeyboardHotkeys(ini);
        }

        /// <summary>
        /// Gamepad
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="c1"></param>
        private void WriteJoystickMapping(IniFile ini, Controller ctrl)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            bool invertFaceButtons = SystemConfig.getOptBoolean("desmume_gamepadButtons");

            int index = (ctrl.DirectInput != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex);

            if (ctrl.IsXInputDevice)
            {
                SimpleLogger.Instance.Info("[INFO] Configuring XInput controller with index: " + index.ToString());

                if (SystemConfig.getOptBoolean("desmume_analog"))
                {
                    ini.WriteValue("Controls", "Left", GetXInputCode(32768, index));
                    ini.WriteValue("Controls", "Right", GetXInputCode(32769, index));
                    ini.WriteValue("Controls", "Up", GetXInputCode(32770, index));
                    ini.WriteValue("Controls", "Down", GetXInputCode(32771, index));
                }
                else
                {
                    ini.WriteValue("Controls", "Left", GetXInputCode(32772, index));
                    ini.WriteValue("Controls", "Right", GetXInputCode(32773, index));
                    ini.WriteValue("Controls", "Up", GetXInputCode(32774, index));
                    ini.WriteValue("Controls", "Down", GetXInputCode(32775, index));
                }

                ini.WriteValue("Controls", "Left_Up", "27");
                ini.WriteValue("Controls", "Left_Down", "27");
                ini.WriteValue("Controls", "Right_Up", "27");
                ini.WriteValue("Controls", "Right_Down", "27");
                ini.WriteValue("Controls", "Start", GetXInputCode(32783, index));
                ini.WriteValue("Controls", "Select", GetXInputCode(32782, index));
                ini.WriteValue("Controls", "Lid", "0");
                ini.WriteValue("Controls", "Debug", "0");

                ini.WriteValue("Controls", "A", invertFaceButtons? GetXInputCode(32776, index) : GetXInputCode(32777, index));
                ini.WriteValue("Controls", "B", invertFaceButtons? GetXInputCode(32777, index) : GetXInputCode(32776, index));
                ini.WriteValue("Controls", "X", invertFaceButtons? GetXInputCode(32778, index) : GetXInputCode(32779, index));
                ini.WriteValue("Controls", "Y", invertFaceButtons? GetXInputCode(32779, index) : GetXInputCode(32778, index));
                ini.WriteValue("Controls", "L", GetXInputCode(32780, index));
                ini.WriteValue("Controls", "R", GetXInputCode(32781, index));

                return;
            }

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid1 = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput controller = null;

            SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

            try { controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1); }
            catch { }

            if (ctrl != null)
            {
                if (SystemConfig.getOptBoolean("desmume_analog"))
                {
                    ini.WriteValue("Controls", "Left", GetDInputKeyName(controller, "leftx", index, false));
                    ini.WriteValue("Controls", "Right", GetDInputKeyName(controller, "leftx", index, true));
                    ini.WriteValue("Controls", "Up", GetDInputKeyName(controller, "lefty", index, false));
                    ini.WriteValue("Controls", "Down", GetDInputKeyName(controller, "leftx", index, true));
                }
                else
                {
                    ini.WriteValue("Controls", "Left", GetDInputKeyName(controller, "dpleft", index, false));
                    ini.WriteValue("Controls", "Right", GetDInputKeyName(controller, "dpright", index, false));
                    ini.WriteValue("Controls", "Up", GetDInputKeyName(controller, "dpup", index, false));
                    ini.WriteValue("Controls", "Down", GetDInputKeyName(controller, "dpdown", index, false));
                }

                ini.WriteValue("Controls", "Left_Up", "27");
                ini.WriteValue("Controls", "Left_Down", "27");
                ini.WriteValue("Controls", "Right_Up", "27");
                ini.WriteValue("Controls", "Right_Down", "27");
                ini.WriteValue("Controls", "Start", GetDInputKeyName(controller, "start", index, false));
                ini.WriteValue("Controls", "Select", GetDInputKeyName(controller, "back", index, false));
                ini.WriteValue("Controls", "Lid", "0");
                ini.WriteValue("Controls", "Debug", "0");

                ini.WriteValue("Controls", "A", invertFaceButtons ? GetDInputKeyName(controller, "a", index, false) : GetDInputKeyName(controller, "b", index, false));
                ini.WriteValue("Controls", "B", invertFaceButtons ? GetDInputKeyName(controller, "b", index, false) : GetDInputKeyName(controller, "a", index, false));
                ini.WriteValue("Controls", "X", invertFaceButtons ? GetDInputKeyName(controller, "x", index, false) : GetDInputKeyName(controller, "y", index, false));
                ini.WriteValue("Controls", "Y", invertFaceButtons ? GetDInputKeyName(controller, "y", index, false) : GetDInputKeyName(controller, "x", index, false));
                ini.WriteValue("Controls", "L", GetDInputKeyName(controller, "leftshoulder", index, false));
                ini.WriteValue("Controls", "R", GetDInputKeyName(controller, "rightshoulder", index, false));
            }

            else
                SimpleLogger.Instance.Info("[WARNING] Controller cannot be automatically configured.");
        }

        private string GetXInputCode(int code, int index)
        {
            int ret = code + (index * 256);
            return ret.ToString();
        }
        private static string GetDInputKeyName(SdlToDirectInput ctrl, string key, int index, bool revert = false)
        {
            int ret = 27;

            if (!ctrl.ButtonMappings.ContainsKey(key))
                ret = defaultButton[key];

            string button = ctrl.ButtonMappings[key];

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());
                ret = 32776 + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        ret = 32774;
                        break;
                    case 2:
                        ret = 32773;
                        break;
                    case 4:
                        ret = 32775;
                        break;
                    case 8:
                        ret = 32772;
                        break;
                }
                ;
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();
                else
                {
                    if (button.StartsWith("-a"))
                        revert = true;
                    if (button.StartsWith("+a"))
                        revert = false;
                    
                    axisID = button.Substring(2).ToInteger();
                }

                switch (axisID)
                {
                    case 0:
                        if (revert) ret = 32769;
                        else ret = 32768;
                        break;
                    case 1:
                        if (revert) ret = 32771;
                        else ret = 32770;
                        break;
                    case 2:
                        if (revert) ret = 32810;
                        else ret = 32809;
                        break;
                    case 3:
                        if (revert) ret = 32822;
                        else ret = 32821;
                        break;
                    case 4:
                        if (revert) ret = 32824;
                        else ret = 32823;
                        break;
                    case 5:
                        if (revert) ret = 32826;
                        else ret = 32825;
                        break;
                }
                ;
            }

            int finalRet = ret + (index * 256);
            return finalRet.ToString();
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

            bool azerty = false;
            List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                azerty = true;

            ini.WriteValue("Controls", "Left", "37");
            ini.WriteValue("Controls", "Right", "39");
            ini.WriteValue("Controls", "Up", "38");
            ini.WriteValue("Controls", "Down", "40");
            ini.WriteValue("Controls", "Start", "13"); // Enter
            ini.WriteValue("Controls", "Select", "32"); // Space
            ini.WriteValue("Controls", "A", "88");
            ini.WriteValue("Controls", "B", azerty ? "87":"90");
            ini.WriteValue("Controls", "X", "83");
            ini.WriteValue("Controls", "Y", azerty? "81":"65");
            ini.WriteValue("Controls", "L", azerty? "65":"81");
            ini.WriteValue("Controls", "R", azerty? "90":"87");

        }

        private void WriteKeyboardHotkeys(IniFile ini)
        {
            for (int i = 0; i < 10; i++)
            {
                int j = 48;
                string slot = "SelectSlot" + i.ToString();
                string slotMod = slot + " MOD";
                ini.WriteValue("Hotkeys", slot, j.ToString());
                ini.WriteValue("Hotkeys", slotMod, "0");
                j++;
            }

            for (int i = 1; i < 10; i++)
            {
                int j = 112;
                string saveslot = "SaveToSlot" + i.ToString();
                string saveslotMod = saveslot + " MOD";
                ini.WriteValue("Hotkeys", saveslot, j.ToString());
                ini.WriteValue("Hotkeys", saveslotMod, "4");

                string loadslot = "LoadFromSlot" + i.ToString();
                string loadslotMod = loadslot + " MOD";
                ini.WriteValue("Hotkeys", loadslot, j.ToString());
                ini.WriteValue("Hotkeys", loadslotMod, "0");

                j++;
            }

            ini.WriteValue("Hotkeys", "SaveToSlot0", "121");
            ini.WriteValue("Hotkeys", "SaveToSlot0 MOD", "4");
            ini.WriteValue("Hotkeys", "LoadFromSlot0", "121");
            ini.WriteValue("Hotkeys", "LoadFromSlot0 MOD", "0");

            foreach (var h in hotkeys)
            {
                string mod = "0";
                string key = h.Key;
                string modKey = key + " MOD";
                string value = h.Value;

                if (h.Value.Contains("_"))
                {
                    string[] strings = h.Value.Split('_');
                    value = strings[0];
                    mod = strings[1];
                }

                ini.WriteValue("Hotkeys", key, value);
                ini.WriteValue("Hotkeys", modKey, mod);
            }
        }

        private Dictionary<string,string> hotkeys = new Dictionary<string, string>()
        {
            { "QuickSave","73" }, // I
            { "QuickLoad","79" }, // O
            { "NextSaveSlot","76" }, // L
            { "PreviousSaveSlot","75" }, // K
            { "Pause","80" }, // P
            { "FastForward","70" }, // F
            { "FastForwardToggle","84" }, // T
            { "QuickScreenshot","123" }, // F12
            { "FrameAdvance","78" }, // N
        };

        private static Dictionary<string, int> defaultButton = new Dictionary<string, int>()
        {
            { "QuickSave", 0 },
        };
    }
}
