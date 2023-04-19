using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;

namespace emulatorLauncher
{
    partial class OpenMSXGenerator : Generator
    {
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
                return retList;
        }

        private List<string> ConfigureKeyboardHotkeys(string path)
        {
            var retList = new List<string>();
            
            string kbHotkeyScript = Path.Combine(path, "kb hotkeys.tcl");
            if (!File.Exists(kbHotkeyScript)) try { File.CreateText(kbHotkeyScript); }
                catch { }

            using (StreamWriter kbhotkeys = new StreamWriter(kbHotkeyScript, false))
            {
                kbhotkeys.WriteLine(@"bind ALT+F8 {savestate [guess_title]}");
                kbhotkeys.WriteLine(@"bind ALT+F7 {loadstate [guess_title]}");
            }

            retList.Add("-script");
            retList.Add("\"" + kbHotkeyScript + "\"");

            return retList;
        }
    }
}
