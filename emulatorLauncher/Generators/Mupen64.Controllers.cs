using System.IO;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Mupen64Generator
    {
        /// <summary>
        /// Cf. https://github.com/Rosalie241/RMG/tree/master/Source/RMG-Input/Utilities
        /// </summary>
        /// <param name="mupen64plus.cfg"></param>
        /*private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for RMG Mupen64");

            // UpdateSdlControllersWithHints();     // No hints found in emulator code

            for (int i = 0; i < 4; i++)
            {
                string section = "Rosalie's Mupen GUI - Input Plugin Profile " + i;
                ini.ClearSection(section);
            }

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
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

            // override devicename
            string newNamePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
            if (File.Exists(newNamePath))
            {
                string newName = SdlJoystickGuid.GetNameFromFile(newNamePath, controller.Guid, "mupen64");

                if (newName != null)
                    devicename = newName;
            }

            int index = controller.SdlController != null ? controller.SdlController.Index : controller.DeviceIndex;
            bool revertbuttons = controller.VendorID == USB_VENDOR.NINTENDO;
            bool zAsRightTrigger = SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_stick");
            bool xboxLayout = SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && SystemConfig["mupen64_inputprofile" + playerIndex] == "xbox";
            string n64guid = controller.Guid.ToLowerInvariant();

            string iniSection = "Rosalie's Mupen GUI - Input Plugin Profile " + (playerIndex - 1);

            // Get default sensitivity & deadzone
            string sensitivity = "100";
            string deadzone = "15";
            if (SystemConfig.isOptSet("mupen64_sensitivity") && !string.IsNullOrEmpty(SystemConfig["mupen64_sensitivity"]))
                sensitivity = SystemConfig["mupen64_sensitivity"].ToIntegerString();

            if (SystemConfig.isOptSet("mupen64_deadzone") && !string.IsNullOrEmpty(SystemConfig["mupen64_deadzone"]))
                deadzone = SystemConfig["mupen64_deadzone"].ToIntegerString();

            ini.WriteValue(iniSection, "PluggedIn", "True");
            ini.WriteValue(iniSection, "DeviceName", devicename);
            ini.WriteValue(iniSection, "DeviceNum", index.ToString());
            ini.WriteValue(iniSection, "Deadzone", deadzone);
            ini.WriteValue(iniSection, "Sensitivity", sensitivity);
            ini.WriteValue(iniSection, "UseProfile", "");
            ini.WriteValue(iniSection, "GameboyRom", "");
            ini.WriteValue(iniSection, "GameboySave", "");

            if (SystemConfig.isOptSet("mupen64_pak" + playerIndex) && !string.IsNullOrEmpty(SystemConfig["mupen64_pak" + playerIndex]))
                ini.WriteValue(iniSection, "Pak", SystemConfig["mupen64_pak" + playerIndex]);
            else
                ini.WriteValue(iniSection, "Pak", "3");

            if (SystemConfig["mupen64_pak1"] == "2" && playerIndex == 1)
                SearchTransferPackFiles(ini, iniSection);

            ini.WriteValue(iniSection, "RemoveDuplicateMappings", "True");
            ini.WriteValue(iniSection, "FilterEventsForButtons", "True");
            ini.WriteValue(iniSection, "FilterEventsForAxis", "True");

            // Special mapping for n64 style controllers
            string n64json = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
            bool needActivationSwitch = false;
            bool n64_pad = Program.SystemConfig.getOptBoolean("n64_pad");

            if (File.Exists(n64json))
            {
                try
                {
                    var n64Controllers = N64Controller.LoadControllersFromJson(n64json);

                    if (n64Controllers != null)
                    {
                        N64Controller n64Gamepad = N64Controller.GetN64Controller("mupen64", n64guid, n64Controllers);

                        if (n64Gamepad != null)
                        {
                            if (n64Gamepad.ControllerInfo != null)
                            {
                                if (n64Gamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needActivationSwitch = n64Gamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needActivationSwitch && !n64_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific n64 mapping needs to be activated for this controller.");
                                    goto BypassSPecialControllers;
                                }

                                if (n64Gamepad.ControllerInfo.ContainsKey("deviceName") && !string.IsNullOrEmpty(n64Gamepad.ControllerInfo["deviceName"]))
                                    ini.WriteValue(iniSection, "DeviceName", n64Gamepad.ControllerInfo["deviceName"]);
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + n64Gamepad.Name);

                            ConfigureN64Controller(ini, iniSection, n64Gamepad);

                            if (playerIndex == 1)
                            {
                                ConfigureHotkeysN64Controllers(ini, iniSection, n64Gamepad);
                                ConfigureEmptyHotkeys(ini, iniSection);
                            }
                            return;
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] Gamepad not in JSON file.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            BypassSPecialControllers:

            // Default mapping
            if (SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl"))
            {
                ini.WriteValue(iniSection, "A_InputType", "0");
                ini.WriteValue(iniSection, "A_Name", zAsRightTrigger ? "leftshoulder" : "rightshoulder");
                ini.WriteValue(iniSection, "A_Data", zAsRightTrigger ? "9" : "10");
                ini.WriteValue(iniSection, "A_ExtraData", "0");
                ini.WriteValue(iniSection, "B_InputType", "1");
                ini.WriteValue(iniSection, "B_Name", zAsRightTrigger ? "lefttrigger+" : "righttrigger+");
                ini.WriteValue(iniSection, "B_Data", zAsRightTrigger ? "4" : "5");
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
                ini.WriteValue(iniSection, "RightTrigger_Name", zAsRightTrigger ? "rightshoulder" : "leftshoulder");
                ini.WriteValue(iniSection, "RightTrigger_Data", zAsRightTrigger ? "10" : "9");
                ini.WriteValue(iniSection, "RightTrigger_ExtraData", "0");
                ini.WriteValue(iniSection, "ZTrigger_InputType", "1");
                ini.WriteValue(iniSection, "ZTrigger_Name", zAsRightTrigger ? "righttrigger+" : "lefttrigger+");
                ini.WriteValue(iniSection, "ZTrigger_Data", zAsRightTrigger ? "5" : "4");
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
                if (xboxLayout)
                {
                    ini.WriteValue(iniSection, "A_InputType", "0");
                    ini.WriteValue(iniSection, "A_Name", revertbuttons ? "b" : "a");
                    ini.WriteValue(iniSection, "A_Data", revertbuttons ? "1" : "0");
                    ini.WriteValue(iniSection, "A_ExtraData", "0");
                    ini.WriteValue(iniSection, "B_InputType", "0");
                    ini.WriteValue(iniSection, "B_Name", revertbuttons ? "a" : "b");
                    ini.WriteValue(iniSection, "B_Data", revertbuttons ? "0" : "1");
                    ini.WriteValue(iniSection, "B_ExtraData", "0");
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
                }

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
                ini.WriteValue(iniSection, "ZTrigger_Name", zAsRightTrigger ? "righttrigger+" : "lefttrigger+");
                ini.WriteValue(iniSection, "ZTrigger_Data", zAsRightTrigger ? "5" : "4");
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

            if (playerIndex == 1)
            {
                ConfigureHotkeys(ini, iniSection);
                ConfigureEmptyHotkeys(ini, iniSection);
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
        }

        private void ConfigureHotkeys(IniFile ini, string iniSection)
        {
            ini.WriteValue(iniSection, "Hotkey_Exit_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_Exit_Name", "back;start");
            ini.WriteValue(iniSection, "Hotkey_Exit_Data", "4;6");
            ini.WriteValue(iniSection, "Hotkey_Exit_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_Resume_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_Resume_Name", "back;b");
            ini.WriteValue(iniSection, "Hotkey_Resume_Data", "4;1");
            ini.WriteValue(iniSection, "Hotkey_Resume_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_Screenshot_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_Screenshot_Name", "back;rightstick");
            ini.WriteValue(iniSection, "Hotkey_Screenshot_Data", "4;8");
            ini.WriteValue(iniSection, "Hotkey_Screenshot_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor50_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor50_Name", "back;dpleft");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor50_Data", "4;13");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor50_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor100_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor100_Name", "back;dpup");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor100_Data", "4;11");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor100_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor250_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor250_Name", "back;dpright");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor250_Data", "4;14");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor250_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SaveState_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_SaveState_Name", "back;x");
            ini.WriteValue(iniSection, "Hotkey_SaveState_Data", "4;2");
            ini.WriteValue(iniSection, "Hotkey_SaveState_ExtraData", "0;0");
            ini.WriteValue(iniSection, "Hotkey_LoadState_InputType", "0;0");
            ini.WriteValue(iniSection, "Hotkey_LoadState_Name", "back;y");
            ini.WriteValue(iniSection, "Hotkey_LoadState_Data", "4;3");
            ini.WriteValue(iniSection, "Hotkey_LoadState_ExtraData", "0;0");
        }

        private void ConfigureEmptyHotkeys(IniFile ini, string iniSection)
        {
            ini.WriteValue(iniSection, "Hotkey_Shutdown_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_Shutdown_Name", "");
            ini.WriteValue(iniSection, "Hotkey_Shutdown_Data", "");
            ini.WriteValue(iniSection, "Hotkey_Shutdown_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SoftReset_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SoftReset_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SoftReset_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SoftReset_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_HardReset_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_HardReset_Name", "");
            ini.WriteValue(iniSection, "Hotkey_HardReset_Data", "");
            ini.WriteValue(iniSection, "Hotkey_HardReset_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_LimitFPS_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_LimitFPS_Name", "");
            ini.WriteValue(iniSection, "Hotkey_LimitFPS_Data", "");
            ini.WriteValue(iniSection, "Hotkey_LimitFPS_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor25_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor25_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor25_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor25_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor75_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor75_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor75_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor75_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor125_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor125_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor125_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor125_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor150_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor150_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor150_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor150_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor175_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor175_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor175_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor175_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor200_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor200_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor200_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor200_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor225_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor225_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor225_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor225_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor275_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor275_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor275_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor275_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor300_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor300_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor300_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SpeedFactor300_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_GSButton_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_GSButton_Name", "");
            ini.WriteValue(iniSection, "Hotkey_GSButton_Data", "");
            ini.WriteValue(iniSection, "Hotkey_GSButton_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot0_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot0_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot0_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot0_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot1_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot1_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot1_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot1_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot2_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot2_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot2_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot2_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot3_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot3_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot3_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot3_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot4_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot4_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot4_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot4_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot5_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot5_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot5_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot5_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot6_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot6_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot6_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot6_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot7_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot7_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot7_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot7_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot8_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot8_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot8_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot8_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot9_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot9_Name", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot9_Data", "");
            ini.WriteValue(iniSection, "Hotkey_SaveStateSlot9_ExtraData", "");
            ini.WriteValue(iniSection, "Hotkey_Fullscreen_InputType", "");
            ini.WriteValue(iniSection, "Hotkey_Fullscreen_Name", "");
            ini.WriteValue(iniSection, "Hotkey_Fullscreen_Data", "");
            ini.WriteValue(iniSection, "Hotkey_Fullscreen_ExtraData", "");
        }

        private void SearchTransferPackFiles(IniFile ini, string iniSection)
        {
            string pakSystem = "gb";
            if (SystemConfig.isOptSet("mupen64_pak_system") && !string.IsNullOrEmpty(SystemConfig["mupen64_pak_system"]))
                pakSystem = SystemConfig["mupen64_pak_system"];

            string romFolder = Path.Combine(AppConfig.GetFullPath("roms"), pakSystem);
            string saveFolder = Path.Combine(AppConfig.GetFullPath("saves"), pakSystem);

            if (SystemConfig.isOptSet("mupen64_pak_rom") && !string.IsNullOrEmpty(SystemConfig["mupen64_pak_rom"]))
            {
                if (SystemConfig.isOptSet("mupen64_pak_save") && !string.IsNullOrEmpty(SystemConfig["mupen64_pak_save"]))
                {
                    string romStart = SystemConfig["mupen64_pak_rom"];
                    string saveStart = SystemConfig["mupen64_pak_save"];

                    if (Directory.Exists(romFolder) && Directory.Exists(saveFolder))
                    {
                        DirectoryInfo romDirectory = new DirectoryInfo(romFolder);
                        DirectoryInfo saveDirectory = new DirectoryInfo(saveFolder);

                        var romFileList = romDirectory.GetFiles(pakSystem == "gbc" ? "*.gbc" : "*.gb", SearchOption.AllDirectories).Where(x => x.Name.StartsWith(romStart));
                        var saveFileListsav = saveDirectory.GetFiles("*.sav", SearchOption.AllDirectories).Where(x => x.Name.StartsWith(saveStart));
                        var saveFileListram = saveDirectory.GetFiles("*.ram", SearchOption.AllDirectories).Where(x => x.Name.StartsWith(saveStart));

                        if (!romFileList.Any())
                            return;
                        if (!saveFileListsav.Any() && !saveFileListram.Any())
                            return;

                        string romFile = romFileList.FirstOrDefault().FullName;
                        string saveFile = null;
                        if (saveFileListsav.Any())
                            saveFile = saveFileListsav.FirstOrDefault().FullName;
                        else
                            saveFile = saveFileListram.FirstOrDefault().FullName;

                        ini.WriteValue(iniSection, "GameboyRom", "\"" + romFile.Replace("\\", "/") + "\"");
                        ini.WriteValue(iniSection, "GameboySave", "\"" + saveFile.Replace("\\", "/") + "\"");
                    }
                }
            }
        }

        private void ConfigureN64Controller(IniFile ini, string iniSection, N64Controller n64Gamepad)
        {
            if (n64Gamepad.Mapping == null)
            {
                SimpleLogger.Instance.Info("[Controller] Missing mapping for N64 controller.");
                return;
            }

            foreach (var button in n64Gamepad.Mapping)
                ini.WriteValue(iniSection, button.Key, button.Value);
        }

        private void ConfigureHotkeysN64Controllers(IniFile ini, string iniSection, N64Controller n64Gamepad)
        {
            if (n64Gamepad.HotKeyMapping == null)
            {
                SimpleLogger.Instance.Info("[Controller] Missing mapping for N64 controller hotkeys.");
                return;
            }

            foreach (var button in n64Gamepad.HotKeyMapping)
                ini.WriteValue(iniSection, button.Key, button.Value);
        }
    }
}
