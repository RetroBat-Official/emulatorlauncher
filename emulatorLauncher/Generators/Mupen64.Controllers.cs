using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class Mupen64Generator
    {
        /// <summary>
        /// Cf. https://github.com/Rosalie241/RMG/tree/master/Source/RMG-Input/Utilities
        /// </summary>
        /// <param name="mupen64plus.cfg"></param>
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // UpdateSdlControllersWithHints();     // No hints found in emulator code

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(controller, ini);
        }

        private void ConfigureInput(Controller controller, IniFile ini)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(controller, ini, controller.PlayerIndex);
        }

        private void ConfigureJoystick(Controller controller, IniFile ini, int playerIndex)
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

            string iniSection = "Rosalie's Mupen GUI - Input Plugin Profile " + (playerIndex - 1);

            // Get default sensitivity & deadzone
            string sensitivity = "100";
            string deadzone = "15";
            if (SystemConfig.isOptSet("mupen64_sensitivity") && !string.IsNullOrEmpty(SystemConfig["mupen64_sensitivity"]))
                sensitivity = SystemConfig["mupen64_sensitivity"];

            if (SystemConfig.isOptSet("mupen64_deadzone") && !string.IsNullOrEmpty(SystemConfig["mupen64_deadzone"]))
                deadzone = SystemConfig["mupen64_deadzone"];

            ini.WriteValue(iniSection, "PluggedIn", "True");
            ini.WriteValue(iniSection, "DeviceName", devicename);
            ini.WriteValue(iniSection, "DeviceNum", index.ToString());
            ini.WriteValue(iniSection, "Deadzone", deadzone);
            ini.WriteValue(iniSection, "Sensitivity", sensitivity);
            ini.WriteValue(iniSection, "Pak", "0");
            ini.WriteValue(iniSection, "RemoveDuplicateMappings", "True");
            ini.WriteValue(iniSection, "FilterEventsForButtons", "True");
            ini.WriteValue(iniSection, "FilterEventsForAxis", "True");

            if (SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl"))
            {
                ini.WriteValue(iniSection, "A_InputType", "0");
                ini.WriteValue(iniSection, "A_Name", zAsLeftTrigger ? "rightshoulder" : "leftshoulder");
                ini.WriteValue(iniSection, "A_Data", zAsLeftTrigger ? "10" : "9");
                ini.WriteValue(iniSection, "A_ExtraData", "0");
                ini.WriteValue(iniSection, "B_InputType", "1");
                ini.WriteValue(iniSection, "B_Name", zAsLeftTrigger ? "righttrigger+" : "lefttrigger+");
                ini.WriteValue(iniSection, "B_Data", zAsLeftTrigger ? "5" : "4");
                ini.WriteValue(iniSection, "B_ExtraData", "1");
                ini.WriteValue(iniSection, "Start_InputType", "0");
                ini.WriteValue(iniSection, "Start_Name", "start");
                ini.WriteValue(iniSection, "Start_Data", "6");
                ini.WriteValue(iniSection, "Start_ExtraData", "0");

                ini.WriteValue(iniSection, "DpadUp_InputType", "0");
                ini.WriteValue(iniSection, "DpadUp_Name", "dpup");
                ini.WriteValue(iniSection, "DpadUp_Data", "11");
                ini.WriteValue(iniSection, "DpadUp_ExtraData", "0");
                ini.WriteValue(iniSection, "DpadDown_InputType", "0");
                ini.WriteValue(iniSection, "DpadDown_Name", "dpdown");
                ini.WriteValue(iniSection, "DpadDown_Data", "12");
                ini.WriteValue(iniSection, "DpadDown_ExtraData", "0");
                ini.WriteValue(iniSection, "DpadLeft_InputType", "0");
                ini.WriteValue(iniSection, "DpadLeft_Name", "dpleft");
                ini.WriteValue(iniSection, "DpadLeft_Data", "13");
                ini.WriteValue(iniSection, "DpadLeft_ExtraData", "0");
                ini.WriteValue(iniSection, "DpadRight_InputType", "0");
                ini.WriteValue(iniSection, "DpadRight_Name", "dpright");
                ini.WriteValue(iniSection, "DpadRight_Data", "14");
                ini.WriteValue(iniSection, "DpadRight_ExtraData", "0");

                ini.WriteValue(iniSection, "CButtonUp_InputType", "0");
                ini.WriteValue(iniSection, "CButtonUp_Name", revertbuttons ? "x" : "y");
                ini.WriteValue(iniSection, "CButtonUp_Data", revertbuttons ? "2" : "3");
                ini.WriteValue(iniSection, "CButtonUp_ExtraData", "0");
                ini.WriteValue(iniSection, "CButtonDown_InputType", "0");
                ini.WriteValue(iniSection, "CButtonDown_Name", revertbuttons ? "b" : "a");
                ini.WriteValue(iniSection, "CButtonDown_Data", revertbuttons ? "1" : "0");
                ini.WriteValue(iniSection, "CButtonDown_ExtraData", "0");
                ini.WriteValue(iniSection, "CButtonLeft_InputType", "0");
                ini.WriteValue(iniSection, "CButtonLeft_Name", revertbuttons ? "y" : "x");
                ini.WriteValue(iniSection, "CButtonLeft_Data", revertbuttons ? "3" : "2");
                ini.WriteValue(iniSection, "CButtonLeft_ExtraData", "0");
                ini.WriteValue(iniSection, "CButtonRight_InputType", "0");
                ini.WriteValue(iniSection, "CButtonRight_Name", revertbuttons ? "a" : "b");
                ini.WriteValue(iniSection, "CButtonRight_Data", revertbuttons ? "0" : "1");
                ini.WriteValue(iniSection, "CButtonRight_ExtraData", "0");

                ini.WriteValue(iniSection, "LeftTrigger_InputType", "0");
                ini.WriteValue(iniSection, "LeftTrigger_Name", "back");
                ini.WriteValue(iniSection, "LeftTrigger_Data", "4");
                ini.WriteValue(iniSection, "LeftTrigger_ExtraData", "0");
                ini.WriteValue(iniSection, "RightTrigger_InputType", "0");
                ini.WriteValue(iniSection, "RightTrigger_Name", zAsLeftTrigger ? "leftshoulder" : "rightshoulder");
                ini.WriteValue(iniSection, "RightTrigger_Data", zAsLeftTrigger ? "9" : "10");
                ini.WriteValue(iniSection, "RightTrigger_ExtraData", "0");
                ini.WriteValue(iniSection, "ZTrigger_InputType", "1");
                ini.WriteValue(iniSection, "ZTrigger_Name", zAsLeftTrigger ? "lefttrigger+" : "righttrigger+");
                ini.WriteValue(iniSection, "ZTrigger_Data", zAsLeftTrigger ? "4" : "5");
                ini.WriteValue(iniSection, "ZTrigger_ExtraData", "1");

                ini.WriteValue(iniSection, "AnalogStickUp_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickUp_Name", "lefty-");
                ini.WriteValue(iniSection, "AnalogStickUp_Data", "1");
                ini.WriteValue(iniSection, "AnalogStickUp_ExtraData", "0");
                ini.WriteValue(iniSection, "AnalogStickDown_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickDown_Name", "lefty+");
                ini.WriteValue(iniSection, "AnalogStickDown_Data", "1");
                ini.WriteValue(iniSection, "AnalogStickDown_ExtraData", "1");
                ini.WriteValue(iniSection, "AnalogStickLeft_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickLeft_Name", "leftx-");
                ini.WriteValue(iniSection, "AnalogStickLeft_Data", "0");
                ini.WriteValue(iniSection, "AnalogStickLeft_ExtraData", "0");
                ini.WriteValue(iniSection, "AnalogStickRight_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickRight_Name", "leftx+");
                ini.WriteValue(iniSection, "AnalogStickRight_Data", "0");
                ini.WriteValue(iniSection, "AnalogStickRight_ExtraData", "1");
            }

            else
            {
                ini.WriteValue(iniSection, "A_InputType", "0");
                ini.WriteValue(iniSection, "A_Name", revertbuttons ? "b" : "a");
                ini.WriteValue(iniSection, "A_Data", revertbuttons ? "1" : "0");
                ini.WriteValue(iniSection, "A_ExtraData", "0");
                ini.WriteValue(iniSection, "B_InputType", "0");
                ini.WriteValue(iniSection, "B_Name", revertbuttons ? "y" : "x");
                ini.WriteValue(iniSection, "B_Data", revertbuttons ? "3" : "2");
                ini.WriteValue(iniSection, "B_ExtraData", "0");
                ini.WriteValue(iniSection, "Start_InputType", "0");
                ini.WriteValue(iniSection, "Start_Name", "start");
                ini.WriteValue(iniSection, "Start_Data", "6");
                ini.WriteValue(iniSection, "Start_ExtraData", "0");

                ini.WriteValue(iniSection, "DpadUp_InputType", "0");
                ini.WriteValue(iniSection, "DpadUp_Name", "dpup");
                ini.WriteValue(iniSection, "DpadUp_Data", "11");
                ini.WriteValue(iniSection, "DpadUp_ExtraData", "0");
                ini.WriteValue(iniSection, "DpadDown_InputType", "0");
                ini.WriteValue(iniSection, "DpadDown_Name", "dpdown");
                ini.WriteValue(iniSection, "DpadDown_Data", "12");
                ini.WriteValue(iniSection, "DpadDown_ExtraData", "0");
                ini.WriteValue(iniSection, "DpadLeft_InputType", "0");
                ini.WriteValue(iniSection, "DpadLeft_Name", "dpleft");
                ini.WriteValue(iniSection, "DpadLeft_Data", "13");
                ini.WriteValue(iniSection, "DpadLeft_ExtraData", "0");
                ini.WriteValue(iniSection, "DpadRight_InputType", "0");
                ini.WriteValue(iniSection, "DpadRight_Name", "dpright");
                ini.WriteValue(iniSection, "DpadRight_Data", "14");
                ini.WriteValue(iniSection, "DpadRight_ExtraData", "0");

                ini.WriteValue(iniSection, "CButtonUp_InputType", "1");
                ini.WriteValue(iniSection, "CButtonUp_Name", "righty-");
                ini.WriteValue(iniSection, "CButtonUp_Data", "3");
                ini.WriteValue(iniSection, "CButtonUp_ExtraData", "0");
                ini.WriteValue(iniSection, "CButtonDown_InputType", "1");
                ini.WriteValue(iniSection, "CButtonDown_Name", "righty+");
                ini.WriteValue(iniSection, "CButtonDown_Data", "3");
                ini.WriteValue(iniSection, "CButtonDown_ExtraData", "1");
                ini.WriteValue(iniSection, "CButtonLeft_InputType", "1");
                ini.WriteValue(iniSection, "CButtonLeft_Name", "rightx-");
                ini.WriteValue(iniSection, "CButtonLeft_Data", "2");
                ini.WriteValue(iniSection, "CButtonLeft_ExtraData", "0");
                ini.WriteValue(iniSection, "CButtonRight_InputType", "1");
                ini.WriteValue(iniSection, "CButtonRight_Name", "rightx+");
                ini.WriteValue(iniSection, "CButtonRight_Data", "2");
                ini.WriteValue(iniSection, "CButtonRight_ExtraData", "1");

                ini.WriteValue(iniSection, "LeftTrigger_InputType", "0");
                ini.WriteValue(iniSection, "LeftTrigger_Name", "leftshoulder");
                ini.WriteValue(iniSection, "LeftTrigger_Data", "9");
                ini.WriteValue(iniSection, "LeftTrigger_ExtraData", "0");
                ini.WriteValue(iniSection, "RightTrigger_InputType", "0");
                ini.WriteValue(iniSection, "RightTrigger_Name", "rightshoulder");
                ini.WriteValue(iniSection, "RightTrigger_Data", "10");
                ini.WriteValue(iniSection, "RightTrigger_ExtraData", "0");
                ini.WriteValue(iniSection, "ZTrigger_InputType", "1");
                ini.WriteValue(iniSection, "ZTrigger_Name", zAsLeftTrigger ? "lefttrigger+" : "righttrigger+");
                ini.WriteValue(iniSection, "ZTrigger_Data", zAsLeftTrigger ? "4" : "5");
                ini.WriteValue(iniSection, "ZTrigger_ExtraData", "1");

                ini.WriteValue(iniSection, "AnalogStickUp_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickUp_Name", "lefty-");
                ini.WriteValue(iniSection, "AnalogStickUp_Data", "1");
                ini.WriteValue(iniSection, "AnalogStickUp_ExtraData", "0");
                ini.WriteValue(iniSection, "AnalogStickDown_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickDown_Name", "lefty+");
                ini.WriteValue(iniSection, "AnalogStickDown_Data", "1");
                ini.WriteValue(iniSection, "AnalogStickDown_ExtraData", "1");
                ini.WriteValue(iniSection, "AnalogStickLeft_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickLeft_Name", "leftx-");
                ini.WriteValue(iniSection, "AnalogStickLeft_Data", "0");
                ini.WriteValue(iniSection, "AnalogStickLeft_ExtraData", "0");
                ini.WriteValue(iniSection, "AnalogStickRight_InputType", "1");
                ini.WriteValue(iniSection, "AnalogStickRight_Name", "leftx+");
                ini.WriteValue(iniSection, "AnalogStickRight_Data", "0");
                ini.WriteValue(iniSection, "AnalogStickRight_ExtraData", "1");
            }

            ini.WriteValue(iniSection, "UseProfile", "");
            ini.WriteValue(iniSection, "GameboyRom", "");
            ini.WriteValue(iniSection, "GameboySave", "");
        }
    }
}
