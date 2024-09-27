using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using ValveKeyValue;

namespace EmulatorLauncher
{
    partial class Pcsx2Generator : Generator
    {
        private void SetupGunQT(IniFile pcsx2ini, string path)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            SimpleLogger.Instance.Info("[GUNS] Configuring gun.");

            Controller ctrl = null;

            if (Program.Controllers.Count >= 1)
                ctrl = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            else
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            // Initialize USB sections
            string usbSection = "USB1";
            if (SystemConfig.isOptSet("pcsx2_gun") && SystemConfig["pcsx2_gun"] == "USB2")
                usbSection = "USB2";
            
            pcsx2ini.ClearSection("USB1");
            pcsx2ini.ClearSection("USB2");

            _forceSDL = Program.SystemConfig.isOptSet("pcsx2_input_driver_force") && Program.SystemConfig.getOptBoolean("pcsx2_input_driver_force");
            _forceDInput = Program.SystemConfig.isOptSet("pcsx2_input_driver_force") && Program.SystemConfig["pcsx2_input_driver_force"] == "dinput";

            SdlToDirectInput dinputController = null;
            string techPadNumber = null;
            string tech = "";
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");
            
            if (!ctrl.IsKeyboard && _forceDInput)
            {
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
                if (!File.Exists(gamecontrollerDB))
                {
                    SimpleLogger.Instance.Info("[WHEELS] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                    gamecontrollerDB = null;
                }
                string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
                SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

                try { dinputController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid); }
                catch { }

                if (dinputController.ButtonMappings == null)
                {
                    SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". No button mapping in gamescontrollerDB file for : " + guid);
                    dinputController = null;
                }
                techPadNumber = "DInput-" + ctrl.DirectInput.DeviceIndex + "/";
                tech = "DInput";

                if (dinputController == null)
                {
                    tech = ctrl.IsXInputDevice ? "XInput" : "SDL";

                    if (ctrl.IsXInputDevice && !_forceSDL)
                        techPadNumber = "XInput-" + ctrl.XInput.DeviceIndex + "/";
                    else
                        techPadNumber = "SDL-" + (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index) + "/";
                }
            }
            else if (!ctrl.IsKeyboard && ctrl.IsXInputDevice && !_forceSDL)
            {
                techPadNumber = "XInput-" + ctrl.XInput.DeviceIndex + "/";
                tech = "XInput";
            }
            else if (!ctrl.IsKeyboard)
            {
                techPadNumber = "SDL-" + (ctrl.SdlController == null ? ctrl.DeviceIndex : ctrl.SdlController.Index) + "/";
                tech = "SDL";
            }
            else
                techPadNumber = "Keyboard/";

            // Configure gun for player 1 if option is set in es_features
            pcsx2ini.WriteValue(usbSection, "Type", "guncon2");
            pcsx2ini.WriteValue(usbSection, "guncon2_Trigger", guninvert ? "Pointer-0/RightButton" : "Pointer-0/LeftButton");
            
            if (SystemConfig["pcsx2_gunmapping"] == "keyboard_middle")
                pcsx2ini.WriteValue(usbSection, "guncon2_ShootOffscreen", "Keyboard/1");
            else
                pcsx2ini.WriteValue(usbSection, "guncon2_ShootOffscreen", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");

            if (SystemConfig.isOptSet("gun_calibrate") && SystemConfig["gun_calibrate"] == "trigger")
                pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", guninvert ? "Pointer-0/RightButton" : "Pointer-0/LeftButton");
            else if (SystemConfig.isOptSet("gun_calibrate") && SystemConfig["gun_calibrate"] == "leftshift")
                pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", "Keyboard/Shift");
            else if (SystemConfig.isOptSet("gun_calibrate") && SystemConfig["gun_calibrate"] == "gamepadr")
            {
                if (tech == "DInput")
                    pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", techPadNumber + GetDInputKeyName(dinputController, "rightshoulder"));
                else
                    pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", techPadNumber + GetInputKeyName(ctrl, InputKey.pagedown, tech));
            }
            else
                pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", "Pointer-0/MiddleButton");

            if (SystemConfig.isOptSet("pcsx2_gunmapping") && SystemConfig["pcsx2_gunmapping"] == "controller")
            {
                if (tech == "DInput")
                {
                    pcsx2ini.WriteValue(usbSection, "guncon2_Up", techPadNumber + GetDInputKeyName(dinputController, "buttonup"));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Down", techPadNumber + GetDInputKeyName(dinputController, "buttondown"));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Left", techPadNumber + GetDInputKeyName(dinputController, "buttonleft"));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Right", techPadNumber + GetDInputKeyName(dinputController, "buttonright"));
                    pcsx2ini.WriteValue(usbSection, "guncon2_A", techPadNumber + GetDInputKeyName(dinputController, "a"));          // Cross
                    pcsx2ini.WriteValue(usbSection, "guncon2_B", techPadNumber + GetDInputKeyName(dinputController, "b"));          // Circle
                    pcsx2ini.WriteValue(usbSection, "guncon2_C", techPadNumber + GetDInputKeyName(dinputController, "x"));          // Square
                    pcsx2ini.WriteValue(usbSection, "guncon2_Select", techPadNumber + GetDInputKeyName(dinputController, "back"));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Start", techPadNumber + GetDInputKeyName(dinputController, "start"));
                }
                else
                {
                    pcsx2ini.WriteValue(usbSection, "guncon2_Up", techPadNumber + GetInputKeyName(ctrl, InputKey.up, tech));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Down", techPadNumber + GetInputKeyName(ctrl, InputKey.down, tech));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Left", techPadNumber + GetInputKeyName(ctrl, InputKey.left, tech));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Right", techPadNumber + GetInputKeyName(ctrl, InputKey.right, tech));
                    pcsx2ini.WriteValue(usbSection, "guncon2_A", techPadNumber + GetInputKeyName(ctrl, InputKey.a, tech));          // Cross
                    pcsx2ini.WriteValue(usbSection, "guncon2_B", techPadNumber + GetInputKeyName(ctrl, InputKey.b, tech));          // Circle
                    pcsx2ini.WriteValue(usbSection, "guncon2_C", techPadNumber + GetInputKeyName(ctrl, InputKey.y, tech));          // Square
                    pcsx2ini.WriteValue(usbSection, "guncon2_Select", techPadNumber + GetInputKeyName(ctrl, InputKey.select, tech));
                    pcsx2ini.WriteValue(usbSection, "guncon2_Start", techPadNumber + GetInputKeyName(ctrl, InputKey.start, tech));
                }
            }
            else
            {
                pcsx2ini.WriteValue(usbSection, "guncon2_Up", "Keyboard/Up");
                pcsx2ini.WriteValue(usbSection, "guncon2_Down", "Keyboard/Down");
                pcsx2ini.WriteValue(usbSection, "guncon2_Left", "Keyboard/Left");
                pcsx2ini.WriteValue(usbSection, "guncon2_Right", "Keyboard/Right");
                
                if (SystemConfig.isOptSet("pcsx2_gunmapping") && SystemConfig["pcsx2_gunmapping"] == "keyboard_volume")
                {
                    pcsx2ini.WriteValue(usbSection, "guncon2_A", "Keyboard/VolumeUp");
                    pcsx2ini.WriteValue(usbSection, "guncon2_B", "Keyboard/VolumeDown");
                }
                else
                {
                    pcsx2ini.WriteValue(usbSection, "guncon2_A", "Keyboard/1");
                    pcsx2ini.WriteValue(usbSection, "guncon2_B", "Keyboard/2");
                }

                pcsx2ini.WriteValue(usbSection, "guncon2_C", "Keyboard/3");
                pcsx2ini.WriteValue(usbSection, "guncon2_Select", "Keyboard/Backspace");

                if (SystemConfig.isOptSet("pcsx2_gunmapping") && SystemConfig["pcsx2_gunmapping"] == "keyboard_middle")
                {
                    pcsx2ini.WriteValue(usbSection, "guncon2_A", guninvert ? "Pointer-0/LeftButton" : "Pointer-0/RightButton");
                    pcsx2ini.WriteValue(usbSection, "guncon2_Start", "Pointer-0/MiddleButton");
                }
                else
                    pcsx2ini.WriteValue(usbSection, "guncon2_Start", "Keyboard/Return");
            }

            // Crosshair
            SimpleLogger.Instance.Info("[GUNS] Configuring crosshair.");
            pcsx2ini.Remove(usbSection, "guncon2_cursor_path");
            string crosshairPath = Path.Combine(path, "cross");
            if (!Directory.Exists(crosshairPath)) try { Directory.CreateDirectory(crosshairPath); }
                catch { }

            string crosshairFile = Path.Combine(crosshairPath, "crosshair.png");

            if (!File.Exists(crosshairFile))
                SimpleLogger.Instance.Info("[GUNS] No crosshair file found in " + crosshairFile);
            
            if (SystemConfig.isOptSet("pcsx2_crosshair") && SystemConfig["pcsx2_crosshair"] == "custom" && File.Exists(crosshairFile))
            {
                pcsx2ini.WriteValue(usbSection, "guncon2_cursor_path", crosshairFile);
                pcsx2ini.WriteValue("UI", "HideMouseCursor", "true");
            }
            else if (SystemConfig.isOptSet("pcsx2_crosshair") && SystemConfig["pcsx2_crosshair"] == "mouse")
            {
                pcsx2ini.Remove(usbSection, "guncon2_cursor_path");
                pcsx2ini.WriteValue("UI", "HideMouseCursor", "false");
            }
            else
            {
                pcsx2ini.Remove(usbSection, "guncon2_cursor_path");
                pcsx2ini.WriteValue("UI", "HideMouseCursor", "true");
            }

            pcsx2ini.WriteValue(usbSection, "guncon2_custom_config", "false");
            pcsx2ini.WriteValue(usbSection, "guncon2_cursor_color", "#ffffff");

            // crosshair size
            string crosshairSize = "1.0";
            if (SystemConfig.isOptSet("pcsx2_crosshair_size") && !string.IsNullOrEmpty(SystemConfig["pcsx2_crosshair_size"]))
                crosshairSize = SystemConfig["pcsx2_crosshair_size"].Substring(0, SystemConfig["pcsx2_crosshair_size"].Length - 4);
            pcsx2ini.WriteValue(usbSection, "guncon2_cursor_scale", crosshairSize);
            pcsx2ini.Remove(usbSection, "guncon2_RelativeUp");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeDown");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeLeft");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeRight");
        }
    }
}