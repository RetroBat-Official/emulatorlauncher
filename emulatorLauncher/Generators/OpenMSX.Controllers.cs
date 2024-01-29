using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class OpenMSXGenerator : Generator
    {
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private List<string> configureControllers(string path)
        {
            var retList = new List<string>();
            bool useMouse = SystemConfig.isOptSet("msx_mouse") && SystemConfig.getOptBoolean("msx_mouse");

            retList.AddRange(ConfigureKeyboardHotkeys(path));

            if (useMouse && Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                string userScript = Path.Combine(path, "plugmouse.tcl");
                if (File.Exists(userScript))
                {
                    retList.Add("-script");
                    retList.Add("\"" + userScript + "\"");
                    return retList;
                }
                else
                {
                    return retList;
                }
            }

            else if (SystemConfig.isOptSet("msx_custom_gamepad") && !string.IsNullOrEmpty(SystemConfig["msx_custom_gamepad"]))
            {
                string userScript = Path.Combine(path, SystemConfig["msx_custom_gamepad"] + ".tcl");
                if (!File.Exists(userScript))
                    throw new ApplicationException("User script does not exist in 'emulators\\openmsx\\share\\scripts' path.");
                else
                { 
                    retList.Add("-script");
                    retList.Add("\"" + userScript + "\"");
                    return retList;
                }
            }

            else if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                string disableAllJoysticks = Path.Combine(path, "removealljoysticks.tcl");
                retList.Add("-script");
                retList.Add("\"" + disableAllJoysticks + "\"");
                return retList;
            }

            else
            {
                // UpdateSdlControllersWithHints();                     No hints in openMSX code

                // Inject controllers                
                
                string retrobatJoyScipt = Path.Combine(path, "retrobatJoystick.tcl");

                if (Program.Controllers.Count == 1)
                {
                    var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                    using (StreamWriter joyScript = new StreamWriter(retrobatJoyScipt, false))
                        ConfigureJoystick(joyScript, c1);
                }

                else if (Program.Controllers.Count > 1)
                {
                    var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                    var c2 = Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                    using (StreamWriter joyScript = new StreamWriter(retrobatJoyScipt, false))
                        ConfigureJoystick(joyScript, c1, c2);
                }

                retList.Add("-script");
                retList.Add("\"" + retrobatJoyScipt + "\"");
                return retList;
            } 
        }

        private void ConfigureJoystick(StreamWriter sw, Controller c1, Controller c2 = null)
        {
            bool useMouse = SystemConfig.isOptSet("msx_mouse") && SystemConfig.getOptBoolean("msx_mouse");
            var retList = new List<string>();

            if (c1 == null || c1.Config == null)
                return;

            if (c1.IsKeyboard)
            {
                if (useMouse)
                {
                    sw.WriteLine("plug joyportb mouse");
                    sw.WriteLine("set grabinput on");
                }
                else
                {
                    sw.WriteLine("set grabinput off");
                }
                return;
            }

            else
            {
                int index1 = c1.SdlController == null ? c1.DeviceIndex + 1 : c1.SdlController.Index + 1;
                int index2 = -1;
                
                if (c2 != null)
                    index2 = c2.SdlController == null ? c2.DeviceIndex + 1 : c2.SdlController.Index + 1;

                if (c2 == null)
                {
                    sw.WriteLine("unplug joyporta");
                    sw.WriteLine("unplug joyportb");
                    sw.WriteLine("plug joyporta joystick" + index1);
                    if (useMouse)
                    {
                        sw.WriteLine("plug joyportb mouse");
                        sw.WriteLine("set grabinput on");
                    }
                    else
                    {
                        sw.WriteLine("set grabinput off");
                    }
                }
                    
                else
                {
                    sw.WriteLine("unplug joyporta");
                    sw.WriteLine("unplug joyportb");
                    sw.WriteLine("plug joyporta joystick" + index1);
                    sw.WriteLine("plug joyportb joystick" + index2);
                    sw.WriteLine("set grabinput off");
                }

                // Add hotkeys bind for joystick 1

                /// SELECT + R1 = Fast forward
                // unbind F9
                // bind F9 "set fastforward off"
                // bind F9, release "set fastforward on"

                string shortJoy = "joy" + index1;
                string longJoy = "joystick" + index1;
                string tech1 = c1.IsXInputDevice ? "XInput" : "SDL";

                if (tech1 == "XInput")
                {
                    sw.WriteLine("bind \"" + shortJoy + " button6 down\" \"bind \\\"" + shortJoy + " button0 down\\\" main_menu_toggle ; bind \\\"" + shortJoy + " button1 down\\\" \\\"toggle pause\\\" ; bind \\\"" + shortJoy + " button4 down\\\" \\\"reverse goback 2\\\"  ; bind \\\"" + shortJoy + " button5 down\\\" \\\"toggle fastforward\\\"\"");
                    sw.WriteLine("bind \"" + shortJoy + " button6 up\" \"unbind \\\"" + shortJoy + " button0 down\\\" ; unbind \\\"" + shortJoy + " button1 down\\\" ; unbind \\\"" + shortJoy + " button4 down\\\"  ; bind \\\"" + shortJoy + " button5 down\\\" \\\"toggle fastforward\\\"\"");
                    sw.WriteLine("dict set " + longJoy + "_config A button0");
                    sw.WriteLine("dict set " + longJoy + "_config B button1");
                    sw.WriteLine("dict set " + longJoy + "_config LEFT {-axis0 L_hat0}");
                    sw.WriteLine("dict set " + longJoy + "_config RIGHT {+axis0 R_hat0}");
                    sw.WriteLine("dict set " + longJoy + "_config UP {-axis1 U_hat0}");
                    sw.WriteLine("dict set " + longJoy + "_config DOWN {+axis1 D_hat0}");
                }

                else if (tech1 == "SDL")
                {
                    sw.WriteLine("bind \"" + shortJoy + " button4 down\" \"bind \\\"" + shortJoy + " button0 down\\\" main_menu_toggle ; bind \\\"" + shortJoy + " button1 down\\\" \\\"toggle pause\\\" ; bind \\\"" + shortJoy + " button9 down\\\" \\\"reverse goback 2\\\"  ; bind \\\"" + shortJoy + " button10 down\\\" \\\"toggle fastforward\\\"\"");
                    sw.WriteLine("bind \"" + shortJoy + " button4 up\" \"unbind \\\"" + shortJoy + " button0 down\\\" ; unbind \\\"" + shortJoy + " button1 down\\\" ; unbind \\\"" + shortJoy + " button9 down\\\"  ; bind \\\"" + shortJoy + " button10 down\\\" \\\"toggle fastforward\\\"\"");
                    sw.WriteLine("dict set "+ longJoy + "_config A button0");
                    sw.WriteLine("dict set " + longJoy + "_config B button1");
                    sw.WriteLine("dict set " + longJoy + "_config LEFT {-axis0 button13}");
                    sw.WriteLine("dict set " + longJoy + "_config RIGHT {+axis0 button14}");
                    sw.WriteLine("dict set " + longJoy + "_config UP {-axis1 button11}");
                    sw.WriteLine("dict set " + longJoy + "_config DOWN {+axis1 button12}");
                }

                if (c2 != null)
                {
                    string tech2 = c2.IsXInputDevice ? "XInput" : "SDL";
                    string longJoy2 = "joystick" + index2;
                    if (tech2 == "SDL")
                    {
                        sw.WriteLine("dict set " + longJoy2 + "_config A button0");
                        sw.WriteLine("dict set " + longJoy2 + "_config B button1");
                        sw.WriteLine("dict set " + longJoy2 + "_config LEFT {-axis0 button13}");
                        sw.WriteLine("dict set " + longJoy2 + "_config RIGHT {+axis0 button14}");
                        sw.WriteLine("dict set " + longJoy2 + "_config UP {-axis1 button11}");
                        sw.WriteLine("dict set " + longJoy2 + "_config DOWN {+axis1 button12}");
                    }
                    else
                    {
                        sw.WriteLine("dict set " + longJoy2 + "_config A button0");
                        sw.WriteLine("dict set " + longJoy2 + "_config B button1");
                        sw.WriteLine("dict set " + longJoy2 + "_config LEFT {-axis0 L_hat0}");
                        sw.WriteLine("dict set " + longJoy2 + "_config RIGHT {+axis0 R_hat0}");
                        sw.WriteLine("dict set " + longJoy2 + "_config UP {-axis1 U_hat0}");
                        sw.WriteLine("dict set " + longJoy2 + "_config DOWN {+axis1 D_hat0}");
                    }
                }

                sw.Close();
                return;
            }
        }

        private List<string> ConfigureKeyboardHotkeys(string path)
        {
            var retList = new List<string>();
            
            string kbHotkeyScript = Path.Combine(path, "kbhotkeys.tcl");

            using (StreamWriter kbhotkeys = new StreamWriter(kbHotkeyScript, false))
            {
                kbhotkeys.WriteLine(@"bind ALT+F8 {savestate [guess_title]}");
                kbhotkeys.WriteLine(@"bind ALT+F7 {loadstate [guess_title]}");
                kbhotkeys.Close();
            }

            retList.Add("-script");
            retList.Add("\"" + kbHotkeyScript + "\"");

            return retList;
        }
    }
}
