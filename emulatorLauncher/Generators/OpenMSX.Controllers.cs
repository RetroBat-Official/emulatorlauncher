using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class OpenMSXGenerator : Generator
    {
        /*private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private List<string> ConfigureControllers(string path, XElement settings, XElement bindings)
        {
            if (!SystemConfig.isOptSet("disableautocontrollers") || !SystemConfig.getOptBoolean("disableautocontrollers"))
                bindings.RemoveAll();
            
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
                        ConfigureJoystick(settings, bindings, joyScript, c1);
                }

                else if (Program.Controllers.Count > 1)
                {
                    var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                    var c2 = Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                    using (StreamWriter joyScript = new StreamWriter(retrobatJoyScipt, false))
                        ConfigureJoystick(settings, bindings, joyScript, c1, c2);
                }

                retList.Add("-script");
                retList.Add("\"" + retrobatJoyScipt + "\"");
                return retList;
            } 
        }

        private Dictionary<InputKey, string> joymegaMapping = new Dictionary<InputKey, string>()
        {
            { InputKey.up,                  "UP"},
            { InputKey.down,                "DOWN"},
            { InputKey.left,                "LEFT" },
            { InputKey.right,               "RIGHT"},
            { InputKey.a,                   "A" },
            { InputKey.b,                   "B" },
            { InputKey.pagedown,            "C" },
            { InputKey.y,                   "X" },
            { InputKey.x,                   "Y" },
            { InputKey.pageup,              "Z" },
            { InputKey.select,              "SELECT" },
            { InputKey.start,               "START" }
        };

        private Dictionary<InputKey, string> msxjoystickMapping = new Dictionary<InputKey, string>()
        {
            { InputKey.up,                  "UP"},
            { InputKey.down,                "DOWN"},
            { InputKey.left,                "LEFT" },
            { InputKey.right,               "RIGHT"},
            { InputKey.a,                   "A" },
            { InputKey.b,                   "B" }
        };

        private static Dictionary<InputKey, string> hotkeyMapping = new Dictionary<InputKey, string>()
        {
            { InputKey.b,                   "toggle pause"},
            { InputKey.pageup,              "reverse goback 2"},
            { InputKey.pagedown,            "toggle fastforward" },
            { InputKey.x,                   "loadstate \\[guess_title\\]" },
            { InputKey.y,                   "savestate \\[guess_title\\]" }
        };

        private List<string> directions = new List<string>() { "UP", "DOWN", "RIGHT", "LEFT" };

        private void ConfigureJoystick(XElement settings, XElement bindings, StreamWriter sw, Controller c1, Controller c2 = null)
        {
            Dictionary<InputKey, string> joyMapping = msxjoystickMapping;
            bool useMouse = SystemConfig.isOptSet("msx_mouse") && SystemConfig.getOptBoolean("msx_mouse");
            string joyType = "msxjoystick";
            if (SystemConfig.isOptSet("msx_joytype") && !string.IsNullOrEmpty(SystemConfig["msx_joytype"]))
            {
                joyType = SystemConfig["msx_joytype"];
                joyMapping = joymegaMapping;
            }

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
                bool useStick = SystemConfig.getOptBoolean("msx_useStick");
                int index1 = c1.SdlController == null ? c1.DeviceIndex + 1 : c1.SdlController.Index + 1;
                int index2 = -1;

                if (c2 != null)
                    index2 = c2.SdlController == null ? c2.DeviceIndex + 1 : c2.SdlController.Index + 1;

                if (c2 == null)
                {
                    sw.WriteLine("unplug joyporta");
                    sw.WriteLine("unplug joyportb");
                    sw.WriteLine("plug joyporta " + joyType + "1");
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
                    sw.WriteLine("plug joyporta " + joyType + "1");
                    sw.WriteLine("plug joyportb " + joyType + "2");
                    sw.WriteLine("set grabinput off");
                }

                sw.Close();

                // Add controller mapping (in settings section)
                string shortJoy = "joy" + index1;
                string longJoy = "joystick" + index1;
                string tech1 = c1.IsXInputDevice ? "XInput" : "SDL";
                var p1Mapping = new List<string>();

                XElement joy1ConfigSetting;

                if (joyType == "msxjoystick")
                    joy1ConfigSetting = settings.Descendants("setting").FirstOrDefault(e => e.Attribute("id")?.Value == "msxjoystick1_config");
                else
                    joy1ConfigSetting = settings.Descendants("setting").FirstOrDefault(e => e.Attribute("id")?.Value == "joymega1_config");

                foreach (var button in joyMapping)
                {
                    string mapping = GetInputKeyName(c1, button.Key);

                    if (mapping == null) continue;
                    else if (useStick && directions.Contains(button.Value))
                    {
                        string addAxis;
                        switch (button.Value)
                        {
                            case "UP":
                                addAxis = "{" + shortJoy + " " + GetInputKeyName(c1, InputKey.leftanalogup) + "}";
                                p1Mapping.Add(button.Value + " " + "{{" + shortJoy + " " + mapping + "} " + addAxis + "}");
                                break;
                            case "DOWN":
                                addAxis = "{" + shortJoy + " " + GetInputKeyName(c1, InputKey.leftanalogdown) + "}";
                                p1Mapping.Add(button.Value + " " + "{{" + shortJoy + " " + mapping + "} " + addAxis + "}");
                                break;
                            case "LEFT":
                                addAxis = "{" + shortJoy + " " + GetInputKeyName(c1, InputKey.leftanalogleft) + "}";
                                p1Mapping.Add(button.Value + " " + "{{" + shortJoy + " " + mapping + "} " + addAxis + "}");
                                break;
                            case "RIGHT":
                                addAxis = "{" + shortJoy + " " + GetInputKeyName(c1, InputKey.leftanalogright) + "}";
                                p1Mapping.Add(button.Value + " " + "{{" + shortJoy + " " + mapping + "} " + addAxis + "}");
                                break;
                        }
                    }
                    else
                    {
                        p1Mapping.Add(button.Value + " " + "{{" + shortJoy + " " + mapping + "}}");
                    }
                }

                string p1MappingString = string.Join(" ", p1Mapping);

                if (joy1ConfigSetting != null)
                {
                    joy1ConfigSetting.Value = p1MappingString;
                }
                else
                {
                    var newSetting = new XElement("setting", new XAttribute("id", joyType + "1_config"), p1MappingString);
                    settings.Add(newSetting);
                }

                // Add hotkeys bind for joystick 1 (in bindings section)
                WriteHotkeyBindings(c1, shortJoy, bindings);

                // Player 2
                if (c2 != null)
                {
                    string tech2 = c2.IsXInputDevice ? "XInput" : "SDL";
                    string longJoy2 = "joystick" + index2;
                    string shortJoy2 = "joy" + index2;
                    var p2Mapping = new List<string>();

                    XElement joy2ConfigSetting;

                    if (joyType == "msxjoystick")
                        joy2ConfigSetting = settings.Descendants("setting").FirstOrDefault(e => e.Attribute("id")?.Value == "msxjoystick2_config");
                    else
                        joy2ConfigSetting = settings.Descendants("setting").FirstOrDefault(e => e.Attribute("id")?.Value == "joymega2_config");

                    foreach (var button in joyMapping)
                    {
                        string mapping = GetInputKeyName(c2, button.Key);

                        if (mapping == null) continue;
                        else if (useStick && directions.Contains(button.Value))
                        {
                            string addAxis;
                            switch (button.Value)
                            {
                                case "UP":
                                    addAxis = "{" + shortJoy2 + " " + GetInputKeyName(c2, InputKey.leftanalogup) + "}";
                                    p2Mapping.Add(button.Value + " " + "{{" + shortJoy2 + " " + mapping + "} " + addAxis + "}");
                                    break;
                                case "DOWN":
                                    addAxis = "{" + shortJoy2 + " " + GetInputKeyName(c2, InputKey.leftanalogdown) + "}";
                                    p2Mapping.Add(button.Value + " " + "{{" + shortJoy2 + " " + mapping + "} " + addAxis + "}");
                                    break;
                                case "LEFT":
                                    addAxis = "{" + shortJoy2 + " " + GetInputKeyName(c2, InputKey.leftanalogleft) + "}";
                                    p2Mapping.Add(button.Value + " " + "{{" + shortJoy2 + " " + mapping + "} " + addAxis + "}");
                                    break;
                                case "RIGHT":
                                    addAxis = "{" + shortJoy2 + " " + GetInputKeyName(c2, InputKey.leftanalogright) + "}";
                                    p2Mapping.Add(button.Value + " " + "{{" + shortJoy2 + " " + mapping + "} " + addAxis + "}");
                                    break;
                            }
                        }
                        else
                        {
                            p2Mapping.Add(button.Value + " " + "{{" + shortJoy2 + " " + mapping + "}}");
                        }
                    }

                    string p2MappingString = string.Join(" ", p2Mapping);

                    if (joy2ConfigSetting != null)
                    {
                        joy2ConfigSetting.Value = p2MappingString;
                    }
                    else
                    {
                        var newSetting2 = new XElement("setting", new XAttribute("id", joyType + "2_config"), p2MappingString);
                        settings.Add(newSetting2);
                    }
                }
                return;
            }
        }
        private List<string> ConfigureKeyboardHotkeys(string path)
        {
            var retList = new List<string>();
            
            string kbHotkeyScript = Path.Combine(path, "kbhotkeys.tcl");

            using (StreamWriter kbhotkeys = new StreamWriter(kbHotkeyScript, false))
            {
                kbhotkeys.WriteLine(@"bind ALT+F8 {savestate [guess_title]_" + _saveStateSlot + "}");
                kbhotkeys.WriteLine(@"bind ALT+F7 {loadstate [guess_title]_" + _saveStateSlot + "}");
                kbhotkeys.Close();
            }

            retList.Add("-script");
            retList.Add("\"" + kbHotkeyScript + "\"");

            return retList;
        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            Int64 pid;
            Int64 pval;
            string ret;

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                 ret = "button" + input.Id;
                 return ret;
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+axis" + pid;
                        else return "-axis" + pid;
                }

                if (input.Type == "hat")
                {
                    pid = input.Id;
                    pval = input.Value;
                    switch (pval)
                    {
                        case 1: return "hat" + pid + " up";
                        case 2: return "hat" + pid + " right";
                        case 4: return "hat" + pid + " down";
                        case 8: return "hat" + pid + " left";
                    }
                }
            }
            return null;
        }

        private static void WriteHotkeyBindings(Controller c1, string shortJoy, XElement bindings)
        {
            var hotkey = GetInputKeyName(c1, InputKey.hotkey);
            if (hotkey != null)
            {
                // Build Bind & Unbind string
                var hkMappingBind = new List<string>();
                var hkMappingUnbind = new List<string>();

                foreach (var hk in hotkeyMapping)
                {
                    string mapping = GetInputKeyName(c1, hk.Key);
                    string value = hk.Value;

                    if (mapping == null) continue;
                    else
                    {
                        if (value.StartsWith("loadstate") || value.StartsWith("savestate"))
                            value += "_" + _saveStateSlot + " ";

                        string toAddBind = "bind " + "\"" + shortJoy + " " + mapping + " down\"" + " " + value;
                        string toAddUnbind = "unbind " + "\"" + shortJoy + " " + mapping + " down\"";
                        hkMappingBind.Add(toAddBind);
                        hkMappingUnbind.Add(toAddUnbind);
                    }
                }
                
                string hotkeyBindString = string.Join(" ; ", hkMappingBind);
                string hotkeyunbindString = string.Join(" ; ", hkMappingUnbind);

                // Write first line to bind hotkey combos in bindings xml
                string bindHkXml = shortJoy + " " + hotkey + " down";
                XElement hotkeyBind = bindings.Descendants("bind").FirstOrDefault(e => e.Attribute("key")?.Value == bindHkXml);
                
                if (hotkeyBind != null)
                {
                    hotkeyBind.Value = hotkeyBindString;
                }
                else
                {
                    var newSettingBind = new XElement("bind", new XAttribute("key", bindHkXml), hotkeyBindString);
                    bindings.Add(newSettingBind);
                }

                // Write second line to unbind hotkey combos in bindings xml
                string unbindHkXml = shortJoy + " " + hotkey + " up";
                XElement hotkeyUnbind = bindings.Descendants("bind").FirstOrDefault(e => e.Attribute("key")?.Value == unbindHkXml);
                
                if (hotkeyUnbind != null)
                {
                    hotkeyUnbind.Value = hotkeyunbindString;
                }
                else
                {
                    var newSettingUnBind = new XElement("bind", new XAttribute("key", unbindHkXml), hotkeyunbindString);
                    bindings.Add(newSettingUnBind);
                }
            }
        }
    }
}
