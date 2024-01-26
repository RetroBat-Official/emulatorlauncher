using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

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

            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                _forceSDL = true;

            string techPadNumber = null;
            string tech = "";
            bool guninvert = SystemConfig.isOptSet("gun_invert") && SystemConfig.getOptBoolean("gun_invert");
            
            if (!ctrl.IsKeyboard && ctrl.IsXInputDevice && !_forceSDL)
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
                pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", techPadNumber + GetInputKeyName(ctrl, InputKey.pagedown, tech));
            else
                pcsx2ini.WriteValue(usbSection, "guncon2_Recalibrate", "Pointer-0/MiddleButton");

            if (SystemConfig.isOptSet("pcsx2_gunmapping") && SystemConfig["pcsx2_gunmapping"] == "controller")
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
            pcsx2ini.WriteValue(usbSection, "guncon2_cursor_scale", "1.0");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeUp");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeDown");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeLeft");
            pcsx2ini.Remove(usbSection, "guncon2_RelativeRight");
        }
    }
}