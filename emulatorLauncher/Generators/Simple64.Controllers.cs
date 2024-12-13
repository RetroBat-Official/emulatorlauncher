using System.IO;
using System.Linq;
using EmulatorLauncher.Common;
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
        /*private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Simple64");

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

            // override devicename
            string newNamePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
            if (File.Exists(newNamePath))
            {
                string newName = SdlJoystickGuid.GetNameFromFile(newNamePath, controller.Guid, "simple64");

                if (newName != null)
                    devicename = newName;
            }

            int index = controller.SdlController != null ? controller.SdlController.Index : controller.DeviceIndex;
            bool revertbuttons = controller.VendorID == USB_VENDOR.NINTENDO;
            bool zAsRightTrigger = SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_stick_zl");
            bool xboxLayout = SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && SystemConfig["mupen64_inputprofile" + playerIndex] == "xbox";
            string guid = controller.SdlController != null ? controller.SdlController.Guid.ToString().ToLower() : controller.Guid.ToString().ToLower();
            string n64guid = controller.Guid.ToLowerInvariant();

            string iniSection = "RetroBatAuto-" + playerIndex;

            // Get default sensitivity & deadzone
            string sensitivity = "100";
            string deadzone = "15";
            if (SystemConfig.isOptSet("mupen64_sensitivity") && !string.IsNullOrEmpty(SystemConfig["mupen64_sensitivity"]))
                sensitivity = SystemConfig["mupen64_sensitivity"].ToIntegerString();

            if (SystemConfig.isOptSet("mupen64_deadzone") && !string.IsNullOrEmpty(SystemConfig["mupen64_deadzone"]))
                deadzone = SystemConfig["mupen64_deadzone"].ToIntegerString();

            // ButtonID (SDL)
            // 3 = hat / 4 = button / 5 = axis / 1 or -1 = axis direction (if axis)

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
                        N64Controller n64Gamepad = N64Controller.GetN64Controller("simple64", n64guid, n64Controllers);

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
                                    devicename = n64Gamepad.ControllerInfo["deviceName"];
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + n64Gamepad.Name);

                            ConfigureN64Controller(profileIni, iniSection, n64Gamepad);

                            profileIni.WriteValue(iniSection, "Deadzone", deadzone);
                            profileIni.WriteValue(iniSection, "Sensitivity", sensitivity);

                            settingsIni.WriteValue("Controller" + playerIndex, "Profile", iniSection);

                            string gamepadN64 = index + ":" + devicename;
                            settingsIni.WriteValue("Controller" + playerIndex, "Gamepad", gamepadN64);

                            string pakDevice64 = "simple64_pak" + playerIndex;
                            if (SystemConfig.isOptSet(pakDevice64) && !string.IsNullOrEmpty(SystemConfig[pakDevice64]))
                                settingsIni.WriteValue("Controller" + playerIndex, "Pak", SystemConfig[pakDevice64]);
                            else
                                settingsIni.WriteValue("Controller" + playerIndex, "Pak", "Memory");

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

            if (SystemConfig.isOptSet("mupen64_inputprofile" + playerIndex) && (SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face" || SystemConfig["mupen64_inputprofile" + playerIndex] == "c_face_zl"))
            {
                if (controller.IsXInputDevice)
                {
                    profileIni.WriteValue(iniSection, "A", zAsRightTrigger ? "\"" + "4,4" + "\"" : "\"" + "5,4" + "\"");
                    profileIni.WriteValue(iniSection, "B", zAsRightTrigger ? "\"" + "4,5,1" + "\"" : "\"" + "5,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "Z", zAsRightTrigger ? "\"" + "5,5,1" + "\"" : "\"" + "4,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "L", "\"" + "6,4" + "\"");
                    profileIni.WriteValue(iniSection, "Start", "\"" + "7,4" + "\"");
                    profileIni.WriteValue(iniSection, "R", zAsRightTrigger ? "\"" + "5,4" + "\"" : "\"" + "4,4" + "\"");
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
                    profileIni.WriteValue(iniSection, "A", zAsRightTrigger ? "\"" + "9,4" + "\"" : "\"" + "10,4" + "\"");
                    profileIni.WriteValue(iniSection, "B", zAsRightTrigger ? "\"" + "4,5,1" + "\"" : "\"" + "5,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "Z", zAsRightTrigger ? "\"" + "5,5,1" + "\"" : "\"" + "4,5,1" + "\"");
                    profileIni.WriteValue(iniSection, "L", "\"" + "4,4" + "\"");
                    profileIni.WriteValue(iniSection, "Start", "\"" + "6,4" + "\"");
                    profileIni.WriteValue(iniSection, "R", zAsRightTrigger ? "\"" + "10,4" + "\"" : "\"" + "9,4" + "\"");
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
                    if (xboxLayout)
                    {
                        profileIni.WriteValue(iniSection, "A", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                        profileIni.WriteValue(iniSection, "B", revertbuttons ? "\"" + "0,4" + "\"" : "\"" + "1,4" + "\"");
                    }
                    else
                    {
                        profileIni.WriteValue(iniSection, "A", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                        profileIni.WriteValue(iniSection, "B", revertbuttons ? "\"" + "3,4" + "\"" : "\"" + "2,4" + "\"");
                    }
                    profileIni.WriteValue(iniSection, "Z", zAsRightTrigger ? "\"" + "5,5,1" + "\"" : "\"" + "4,5,1" + "\"");
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
                    if (xboxLayout)
                    {
                        profileIni.WriteValue(iniSection, "A", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                        profileIni.WriteValue(iniSection, "B", revertbuttons ? "\"" + "0,4" + "\"" : "\"" + "1,4" + "\"");
                    }
                    else
                    {
                        profileIni.WriteValue(iniSection, "A", revertbuttons ? "\"" + "1,4" + "\"" : "\"" + "0,4" + "\"");
                        profileIni.WriteValue(iniSection, "B", revertbuttons ? "\"" + "3,4" + "\"" : "\"" + "2,4" + "\"");
                    }
                    profileIni.WriteValue(iniSection, "Z", zAsRightTrigger ? "\"" + "5,5,1" + "\"" : "\"" + "4,5,1" + "\"");
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

            string pakDevice = "simple64_pak" + playerIndex;
            if (SystemConfig.isOptSet(pakDevice) && !string.IsNullOrEmpty(SystemConfig[pakDevice]))
                settingsIni.WriteValue("Controller" + playerIndex, "Pak", SystemConfig[pakDevice]);
            else
                settingsIni.WriteValue("Controller" + playerIndex, "Pak", "Memory");

            /*if (playerIndex == 1)
                ConfigureHotkeys(controller, ini, iniSection);*/

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
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

        private void ConfigureN64Controller(IniFile profileIni, string iniSection, N64Controller gamepad)
        {
            if (gamepad.Mapping == null)
            {
                SimpleLogger.Instance.Info("[Controller] Missing mapping for N64 controller.");
                return;
            }

            foreach (var button in gamepad.Mapping)
                profileIni.WriteValue(iniSection, button.Key, button.Value);
        }

        // Controller hotkeys are not available in Simple64 yet
        /*private void ConfigureHotkeys(Controller controller, IniFile ini, string iniSection)
        {
           //TBD
        }*/
    }
}
