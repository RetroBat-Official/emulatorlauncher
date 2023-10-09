using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Simple64Generator
    {
        /// <summary>
        /// Cf. n/a
        /// </summary>
        /// <param name="input-profiles.ini"></param>
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // UpdateSdlControllersWithHints();     // No hints found in emulator code

            string inputProfileIni = Path.Combine(path, "input-profiles.ini");
            string inputSettingsIni = Path.Combine(path, "input-settings.ini");

            using (var profileIni = IniFile.FromFile(inputProfileIni))
            {
                using (var settingsIni = IniFile.FromFile(inputSettingsIni))
                {
                    ResetInputSettings(settingsIni);
                    
                    foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                        ConfigureInput(controller, profileIni, settingsIni);
                }
            }
        }

        private void ConfigureInput(Controller controller, IniFile profileIni, IniFile settingsIni)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(controller, profileIni, settingsIni, controller.PlayerIndex);
        }

        private void ConfigureJoystick(Controller controller, IniFile profileIni, IniFile settingsIni, int playerIndex)
        {
            if (controller == null)
                return;

            var joy = controller.Config;
            if (joy == null)
                return;

            string devicename = joy.DeviceName;
            int index = controller.SdlController.Index;
            bool revertbuttons = controller.VendorID == USB_VENDOR.NINTENDO;
            bool zAsLeftTrigger = SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_stick_zl";

            string iniSection = "RetroBatAuto-" + playerIndex;

            // Get default sensitivity & deadzone
            string sensitivity = "100";
            string deadzone = "15";
            if (SystemConfig.isOptSet("mupen64_sensitivity") && !string.IsNullOrEmpty(SystemConfig["mupen64_sensitivity"]))
                sensitivity = SystemConfig["mupen64_sensitivity"];

            if (SystemConfig.isOptSet("mupen64_deadzone") && !string.IsNullOrEmpty(SystemConfig["mupen64_deadzone"]))
                deadzone = SystemConfig["mupen64_deadzone"];

            // ButtonID (SDL)
            // 3 = hat / 4 = button / 5 = axis / 1 or -1 = axis direction (if axis)

            if (SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl"))
            {
                if (controller.IsXInputDevice)
                {
                    profileIni.WriteValue(iniSection, "A", zAsLeftTrigger ? "\"" + "5,4" + "\"" : "\"" + "4,4" + "\"");
                    profileIni.WriteValue(iniSection, "B", zAsLeftTrigger ? "\"" + "5,5,1" + "\"" : "\"" + "4,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "Z", zAsLeftTrigger ? "\"" + "4,5,1" + "\"" : "\"" + "5,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "L", "\"" + "6,4" + "\"");
                    profileIni.WriteValue(iniSection, "Start", "\"" + "7,4" + "\"");
                    profileIni.WriteValue(iniSection, "R", zAsLeftTrigger ? "\"" + "4,4" + "\"" : "\"" + "5,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadL", "\"" + "0,3,8" + "\"");
                    profileIni.WriteValue(iniSection, "DPadR", "\"" + "0,3,2" + "\"");
                    profileIni.WriteValue(iniSection, "DPadU", "\"" + "0,3,1" + "\"");
                    profileIni.WriteValue(iniSection, "DPadD", "\"" + "0,3,4" + "\"");
                    profileIni.WriteValue(iniSection, "CLeft", revertbuttons ? "\"" + "3,4" + "\"" : "\"" + "2,4" + "\"");
                    profileIni.WriteValue(iniSection, "CRight", revertbuttons ? "\"" + "0,4" + "\"" : "\"" + "1,4" + "\"");
                    profileIni.WriteValue(iniSection, "CUp", revertbuttons ? "\"" + "2,4" + "\"" : "\"" + "3,4" + "\"");
                    profileIni.WriteValue(iniSection, "CDown", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                    profileIni.WriteValue(iniSection, "AxisLeft", "\"" + "0,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisRight", "\"" + "0,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisUp", "\"" + "1,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisDown", "\"" + "1,5,1" + "\"");
                }
                else
                {
                    profileIni.WriteValue(iniSection, "A", zAsLeftTrigger ? "\"" + "10,4" + "\"" : "\"" + "9,4" + "\"");
                    profileIni.WriteValue(iniSection, "B", zAsLeftTrigger ? "\"" + "5,5,1" + "\"" : "\"" + "4,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "Z", zAsLeftTrigger ? "\"" + "4,5,1" + "\"" : "\"" + "5,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "L", "\"" + "4,4" + "\"");
                    profileIni.WriteValue(iniSection, "Start", "\"" + "6,4" + "\"");
                    profileIni.WriteValue(iniSection, "R", zAsLeftTrigger ? "\"" + "9,4" + "\"" : "\"" + "10,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadL", "\"" + "13,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadR", "\"" + "14,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadU", "\"" + "11,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadD", "\"" + "12,4" + "\"");
                    profileIni.WriteValue(iniSection, "CLeft", revertbuttons ? "\"" + "3,4" + "\"" : "\"" + "2,4" + "\"");
                    profileIni.WriteValue(iniSection, "CRight", revertbuttons ? "\"" + "0,4" + "\"" : "\"" + "1,4" + "\"");
                    profileIni.WriteValue(iniSection, "CUp", revertbuttons ? "\"" + "2,4" + "\"" : "\"" + "3,4" + "\"");
                    profileIni.WriteValue(iniSection, "CDown", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                    profileIni.WriteValue(iniSection, "AxisLeft", "\"" + "0,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisRight", "\"" + "0,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisUp", "\"" + "1,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisDown", "\"" + "1,5,1" + "\"");
                }
            }

            else
            {
                if (controller.IsXInputDevice)
                {
                    profileIni.WriteValue(iniSection, "A", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                    profileIni.WriteValue(iniSection, "B", revertbuttons ? "\"" + "3,4" + "\"" : "\"" + "2,4" + "\"");
                    profileIni.WriteValue(iniSection, "Z", zAsLeftTrigger ? "\"" + "4,5,1" + "\"" : "\"" + "5,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "Start", "\"" + "7,4" + "\"");
                    profileIni.WriteValue(iniSection, "L", "\"" + "4,4" + "\"");
                    profileIni.WriteValue(iniSection, "R", "\"" + "5,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadL", "\"" + "0,3,8" + "\"");
                    profileIni.WriteValue(iniSection, "DPadR", "\"" + "0,3,2" + "\"");
                    profileIni.WriteValue(iniSection, "DPadU", "\"" + "0,3,1" + "\"");
                    profileIni.WriteValue(iniSection, "DPadD", "\"" + "0,3,4" + "\"");
                    profileIni.WriteValue(iniSection, "CLeft", "\"" + "2,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "CRight", "\"" + "2,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "CUp", "\"" + "3,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "CDown", "\"" + "3,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisLeft", "\"" + "0,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisRight", "\"" + "0,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisUp", "\"" + "1,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisDown", "\"" + "1,5,1" + "\"");
                }
                else
                {
                    profileIni.WriteValue(iniSection, "A", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                    profileIni.WriteValue(iniSection, "B", revertbuttons ? "\"" + "3,4" + "\"" : "\"" + "2,4" + "\"");
                    profileIni.WriteValue(iniSection, "Z", zAsLeftTrigger ? "\"" + "4,5,1" + "\"" : "\"" + "5,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "Start", "\"" + "6,4" + "\"");
                    profileIni.WriteValue(iniSection, "L", "\"" + "9,4" + "\"");
                    profileIni.WriteValue(iniSection, "R", "\"" + "10,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadL", "\"" + "13,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadR", "\"" + "14,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadU", "\"" + "11,4" + "\"");
                    profileIni.WriteValue(iniSection, "DPadD", "\"" + "12,4" + "\"");
                    profileIni.WriteValue(iniSection, "CLeft", "\"" + "2,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "CRight", "\"" + "2,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "CUp", "\"" + "3,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "CDown", "\"" + "3,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisLeft", "\"" + "0,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisRight", "\"" + "0,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisUp", "\"" + "1,5,-1" + "\"");
                    profileIni.WriteValue(iniSection, "AxisDown", "\"" + "1,5,1" + "\"");
                }
            }

            profileIni.WriteValue(iniSection, "Deadzone", deadzone);
            profileIni.WriteValue(iniSection, "Sensitivity", sensitivity);

            settingsIni.WriteValue("Controller" + playerIndex, "Profile", iniSection);
            
            string gamepad  = index + ":" + devicename;
            settingsIni.WriteValue("Controller" + playerIndex, "Gamepad", gamepad);


            /*if (playerIndex == 1)
                ConfigureHotkeys(controller, ini, iniSection);*/
        }
        private void ResetInputSettings(IniFile ini)
        {
            for (int i = 1; i<5; i++)
            {
                string iniSection = "Controller" + i;
                ini.WriteValue(iniSection, "Profile", "Auto");
                ini.WriteValue(iniSection, "Gamepad", "Auto");
                ini.WriteValue(iniSection, "Pak", "Memory");
            }
        }

        // Controller hotkeys are not available in Simple64 yet
        private void ConfigureHotkeys(Controller controller, IniFile ini, string iniSection)
        {
           //TBD
        }
    }
}
