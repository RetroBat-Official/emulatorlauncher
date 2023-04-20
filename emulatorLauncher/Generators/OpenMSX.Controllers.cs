using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using emulatorLauncher.Tools;

namespace emulatorLauncher
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

            retList.AddRange(ConfigureKeyboardHotkeys(path));

            if (SystemConfig.isOptSet("msx_mouse") && SystemConfig.getOptBoolean("msx_mouse"))
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

                    if (c1.IsKeyboard)
                        return retList;

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
            var retList = new List<string>();

            if (c1 == null || c1.Config == null)
                return;

            if (c1.IsKeyboard)
                return;
            
            else
            {
                int index1 = c1.SdlController == null ? c1.DeviceIndex + 1 : c1.SdlController.Index + 1;
                int index2 = -1;
                
                if (c2 != null)
                    index2 = c2.SdlController == null ? c2.DeviceIndex + 1 : c2.SdlController.Index + 1;

                if (c2 == null)
                {
                    sw.WriteLine("unplug joyporta");
                    sw.WriteLine("plug joyporta joystick" + index1);
                }
                    
                else
                {
                    sw.WriteLine("unplug joyporta");
                    sw.WriteLine("unplug joyportb");
                    sw.WriteLine("plug joyporta joystick" + index1);
                    sw.WriteLine("plug joyportb joystick" + index2);
                }

                // Add hotkeys bind for joystick 1
                string shortJoy = "joy" + index1;
                string tech = c1.IsXInputDevice ? "XInput" : "SDL";

                if (tech == "XInput")
                {
                    sw.WriteLine("bind \"" + shortJoy + " button6 down\" \"main_menu_toggle\"");
                }

                else if (tech == "SDL")
                {
                    sw.WriteLine("bind \"" + shortJoy + " button4 down\" \"main_menu_toggle\"");
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
