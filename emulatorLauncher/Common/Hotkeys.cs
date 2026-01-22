using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using SharpDX.DirectInput;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    public partial class Hotkeys
    {
        /// <summary>
        /// Method to get keyboard hotkeys and for emulators using pad2key, generate a padToKey.xml file to load
        /// </summary>
        /// <param name="emulator"></param>
        /// <param name="core"></param>
        /// <param name="hotkeys"></param>
        public static bool GetHotKeysFromFile(string emulator, string core, out Dictionary<string, HotkeyResult> hotkeys, string exeName = null)
        {
            hotkeys = new Dictionary<string, HotkeyResult>();
            
            string file = emulator + "_kb_hotkeys.yml";
            string kbHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", file);
            if (!File.Exists(kbHotkeyFile))
                kbHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", file);
            if (!File.Exists(kbHotkeyFile))
                kbHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "kb_hotkeys.yml");
            if (!File.Exists(kbHotkeyFile))
                kbHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "kb_hotkeys.yml");
            if (!File.Exists(kbHotkeyFile))
                return false;
            
            string dicPath = Path.Combine(Program.AppConfig.GetFullPath("emulationstation"), "resources");
            bool emuDicExists = GetEmulatorDic(dicPath, emulator, out var hotkeyDic);

            if (emulator != "retroarch" && !emuDicExists)
            {
                SimpleLogger.Instance.Info("[GENERATOR] No hotkey dictionary found for emulator : " + emulator);
                return false;
            }

            try
            {
                YmlFile ymlFile = YmlFile.Load(kbHotkeyFile);

                YmlContainer kbHotkeyList = ymlFile.Elements.Where(c => c.Name == core).FirstOrDefault() as YmlContainer;

                if (kbHotkeyList == null)
                    kbHotkeyList = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

                if (kbHotkeyList == null)
                    return false;

                SimpleLogger.Instance.Info("[GENERATOR] Overwriting keyboard hotkeys with values from : " + kbHotkeyFile);

                foreach (var element in kbHotkeyList.Elements.OfType<YmlElement>())
                {
                    if (!string.IsNullOrEmpty(element.Name))
                    {
                        if (emulator == "retroarch")
                        {
                            hotkeys[element.Name] = new HotkeyResult
                            {
                                RetroArchValue = element.Value,
                                EmulatorKey = element.Name,
                                EmulatorValue = element.Value
                            };
                        }
                        else if (hotkeyDic.Count > 0)
                        {
                            var emulatorHotkey = GetEmulatorHotkey(emulator);
                            if (emulatorHotkey == null)
                                break;

                            var hkInfo = emulatorHotkey.EmulatorHotkeys.FirstOrDefault(h => h.RetroArchHK.Equals(element.Name, StringComparison.OrdinalIgnoreCase));
                            var hkDic = hotkeyDic;

                            if (hkInfo != null)
                            {
                                string value = hkInfo.DefaultValue;
                                string sourceKey = element.Value.ToLowerInvariant();

                                if (hkDic.ContainsKey(sourceKey))
                                    value = hkDic[sourceKey];

                                hotkeys[element.Name] = new HotkeyResult
                                {
                                    RetroArchValue = element.Value,
                                    EmulatorKey = hkInfo.EmulatorHK,
                                    EmulatorValue = value
                                };
                            }
                        }
                    }
                }

                if (hotkeys.Count > 0)
                {
                    WriteXMLPad2KeyFile(emulator, core, hotkeys, exeName);
                    return true;
                }
                
                return false;
            }
            catch
            {
                hotkeys.Clear();
                return false;
            }
        }

        /// <summary>
        /// Method to get controller hotkeys for emulators allowing controller combos for hotkeys
        /// </summary>
        /// <param name="emulator"></param>
        /// <param name="core"></param>
        /// <param name="padHKDic"></param>
        public static bool GetPadHKFromFile(string emulator, string core, out Dictionary<string, string> padHKDic)
        {
            padHKDic = new Dictionary<string, string>();

            try
            {
                string file = emulator + "_controller_hotkeys.yml";
                string cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", file);
                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", file);
                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "controller_hotkeys.yml");
                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "controller_hotkeys.yml");

                if (File.Exists(cHotkeyFile))
                {
                    YmlFile ymlFile = YmlFile.Load(cHotkeyFile);
                    if (ymlFile != null)
                    {
                        YmlContainer cHotkeyList = ymlFile.Elements.Where(c => c.Name == core).FirstOrDefault() as YmlContainer;

                        if (cHotkeyList == null)
                            cHotkeyList = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

                        if (cHotkeyList != null)
                        {
                            SimpleLogger.Instance.Info("[GENERATOR] Overwriting controller hotkeys with values from : " + cHotkeyFile);

                            var cHotkeys = cHotkeyList.Elements;

                            if (cHotkeys != null & cHotkeys.Count > 0)
                            {
                                var emuHotkey = GetEmulatorHotkey(emulator);
                                if (emuHotkey == null)
                                    return false;

                                foreach (var cHotkey in cHotkeys)
                                {
                                    YmlElement hotkey = cHotkey as YmlElement;
                                    if (hotkey.Name == "noHotkey")
                                        continue;

                                    // b is used for control center, so cannot be mapped as hotkey anymore
                                    if (hotkey.Name == "b")
                                        continue;

                                    // Replace menu_toggle by input_pause_toggle if menu_toggle is not supported by the emulator
                                    if (hotkey.Value == "menu_toggle" && !emuHotkey.EmulatorHotkeys.Any(h => h.RetroArchHK == "input_menu_toggle"))
                                        hotkey.Value = "pause_toggle";

                                    string key = hotkey.Value;
                                    string value = hotkey.Name;
                                    string dicKey = "input_" + key;

                                    var emuhkInfo = emuHotkey.EmulatorHotkeys.FirstOrDefault(h => h.RetroArchHK.Equals(dicKey, StringComparison.OrdinalIgnoreCase));
                                    if (emuhkInfo != null)
                                        padHKDic.Add(emuhkInfo.EmulatorHK, value);
                                }
                                
                                if (padHKDic.Count > 0)
                                    return true;
                                else
                                    return false;
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static void WriteXMLPad2KeyFile(string emulator, string core, Dictionary<string, HotkeyResult> hotkeys, string exeName)
        {
            try
            {
                if (GetPad2KeyHKFromFile(emulator, core, out Dictionary<string, string> padHKDic))
                    padHotkey = padHKDic;

                var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null));
                var root = new XElement("padToKey");
                doc.Add(root);

                var app = new XElement("app", new XAttribute("name", emulator));
                if (emulatorAppName.ContainsKey(emulator))
                    app.SetAttributeValue("name", emulatorAppName[emulator]);
                if (!string.IsNullOrEmpty(exeName))
                    app.SetAttributeValue("name", exeName);

                root.Add(app);

                void AddInput(XElement appElement, string name, string code)
                {
                    appElement.Add(
                        new XElement("input",
                            new XAttribute("name", name),
                            new XAttribute("code", code)
                        )
                    );
                }

                void AddKey(XElement appElement, string name, string code)
                {
                    appElement.Add(
                        new XElement("input",
                            new XAttribute("name", name),
                            new XAttribute("key", code)
                        )
                    );
                }

                bool replaceMenuWithPause = false;
                if (hotkeys.Any(h => h.Key == "input_pause_toggle") && !hotkeys.Any(h => h.Key == "input_menu_toggle"))
                    replaceMenuWithPause = true;

                foreach (var mapping in hotkeys)
                {
                    string orgKey = mapping.Key;
                    if (replaceMenuWithPause && orgKey == "input_pause_toggle")
                        orgKey = "input_menu_toggle";

                    if (Program.SystemConfig.getOptBoolean("fastforward_toggle") && orgKey == "input_hold_fast_forward" && hotkeys.Any(h => h.Key == "input_toggle_fast_forward"))
                        continue;

                    if (padHotkey.ContainsKey(orgKey))
                    {
                        string padInput = "hotkey " + padHotkey[orgKey];
                        string rakey = mapping.Value.RetroArchValue;
                        string key = "KEY_" + rakey.ToUpperInvariant();

                        if (raStringToPad2KeyString.ContainsKey(rakey.ToUpperInvariant()))
                        {
                            key = "KEY_" + raStringToPad2KeyString[rakey.ToUpperInvariant()];
                        }

                        AddInput(app, padInput, key);
                    }
                }

                if (!hotkeys.Any(h => h.Key == "input_exit_emulator"))
                {
                    AddKey(app, "hotkey start", "(%{CLOSE})");
                }

                if (emulator == "melonds")
                {
                    AddInput(app, "hotkey x", "KEY_F1");
                    AddKey(app, "hotkey y", "(+{F1})");
                }

                string path = Path.Combine(Path.GetTempPath(), "padToKey.xml");

                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    doc.Save(writer);
                }
            }

            catch { }
        }

        private static bool GetPad2KeyHKFromFile(string emulator, string core, out Dictionary<string, string> padHKDic)
        {
            padHKDic = new Dictionary<string, string>();

            try
            {
                string file = emulator + "_controller_hotkeys.yml";
                var emuHotkey = GetEmulatorHotkey(emulator);

                string cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", file);

                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", file);
                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "user", "inputmapping", "controller_hotkeys.yml");
                if (!File.Exists(cHotkeyFile))
                    cHotkeyFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "controller_hotkeys.yml");

                if (File.Exists(cHotkeyFile))
                {
                    YmlFile ymlFile = YmlFile.Load(cHotkeyFile);
                    if (ymlFile != null)
                    {
                        YmlContainer cHotkeyList = ymlFile.Elements.Where(c => c.Name == core).FirstOrDefault() as YmlContainer;

                        if (cHotkeyList == null)
                            cHotkeyList = ymlFile.Elements.Where(c => c.Name == "default").FirstOrDefault() as YmlContainer;

                        if (cHotkeyList != null)
                        {
                            SimpleLogger.Instance.Info("[GENERATOR] Overwriting controller hotkeys with values from : " + cHotkeyFile);

                            var cHotkeys = cHotkeyList.Elements;

                            if (cHotkeys != null & cHotkeys.Count > 0)
                            {
                                foreach (var cHotkey in cHotkeys)
                                {
                                    YmlElement hotkey = cHotkey as YmlElement;
                                    if (hotkey.Name == "noHotkey")
                                        continue;

                                    string key = hotkey.Value;
                                    string value = hotkey.Name;
                                    string dicKey = "input_" + key;

                                    if (Program.SystemConfig.getOptBoolean("fastforward_toggle") && dicKey == "input_hold_fast_forward" && emuHotkey.EmulatorHotkeys.Any(h => h.RetroArchHK == "input_toggle_fast_forward"))
                                    {
                                        dicKey = "input_toggle_fast_forward";
                                    }

                                    padHKDic.Add(dicKey, value);
                                }
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static EmulatorHotkey GetEmulatorHotkey(string emulator)
        {
            return EmulatorHotkeys.FirstOrDefault(e => e.Emulator.Equals(emulator, StringComparison.OrdinalIgnoreCase));
        }
    }
}
