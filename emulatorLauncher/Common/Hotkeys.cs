using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
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
        public static bool GetHotKeysFromFile(string emulator, string core, out Dictionary<string, HotkeyResult> hotkeys)
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
                        switch (emulator)
                        {
                            case "retroarch":
                                hotkeys[element.Name] = new HotkeyResult
                                {
                                    RetroArchValue = element.Value,
                                    EmulatorKey = element.Name,
                                    EmulatorValue = element.Value
                                };
                                break;
                            case "ares":
                                {
                                    var emulatorHotkey = GetEmulatorHotkey("ares");
                                    if (emulatorHotkey == null)
                                        break;

                                    var hkInfo = emulatorHotkey.EmulatorHotkeys.FirstOrDefault(h =>h.RetroArchHK.Equals(element.Name, StringComparison.OrdinalIgnoreCase));

                                    if (hkInfo != null)
                                    {
                                        string value = hkInfo.DefaultValue;
                                        string sourceKey = element.Value.ToLowerInvariant();

                                        if (AresKeyEnum.ContainsKey(sourceKey))
                                        {
                                            value = AresKeyEnum[sourceKey];
                                        }

                                        hotkeys[element.Name] = new HotkeyResult
                                        {
                                            RetroArchValue = element.Value,
                                            EmulatorKey = hkInfo.EmulatorHK,
                                            EmulatorValue = value
                                        };
                                    }
                                    break;
                                }
                            case "bigpemu":
                                {
                                    var emulatorHotkey = GetEmulatorHotkey("bigpemu");
                                    if (emulatorHotkey == null)
                                        break;

                                    var hkInfo = emulatorHotkey.EmulatorHotkeys.FirstOrDefault(h =>h.RetroArchHK.Equals(element.Name, StringComparison.OrdinalIgnoreCase));

                                    if (hkInfo != null)
                                    {
                                        string value = hkInfo.DefaultValue;
                                        string sourceKey = element.Value.ToLowerInvariant();

                                        if (sdlKeyCodeEnum.ContainsKey(sourceKey))
                                        {
                                            value = sdlKeyCodeEnum[sourceKey];
                                        }

                                        hotkeys[element.Name] = new HotkeyResult
                                        {
                                            RetroArchValue = element.Value,
                                            EmulatorKey = hkInfo.EmulatorHK,
                                            EmulatorValue = value
                                        };
                                    }
                                    break;
                                }
                            case "flycast":
                                {
                                    var emulatorHotkey = GetEmulatorHotkey("flycast");
                                    if (emulatorHotkey == null)
                                        break;

                                    var hkInfo = emulatorHotkey.EmulatorHotkeys.FirstOrDefault(h => h.RetroArchHK.Equals(element.Name, StringComparison.OrdinalIgnoreCase));

                                    if (hkInfo != null)
                                    {
                                        string value = hkInfo.DefaultValue;
                                        string sourceKey = element.Value.ToLowerInvariant();

                                        if (sdlKeycodeToHID.ContainsKey(sourceKey))
                                        {
                                            value = sdlKeycodeToHID[sourceKey];
                                        }

                                        hotkeys[element.Name] = new HotkeyResult
                                        {
                                            RetroArchValue = element.Value,
                                            EmulatorKey = hkInfo.EmulatorHK,
                                            EmulatorValue = value
                                        };
                                    }
                                    break;
                                }
                        }
                    }
                }

                if (hotkeys.Count > 0)
                {
                    WriteXMLPad2KeyFile(emulator, core, hotkeys);
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
                                foreach (var cHotkey in cHotkeys)
                                {
                                    YmlElement hotkey = cHotkey as YmlElement;
                                    if (hotkey.Name == "noHotkey")
                                        continue;

                                    string key = hotkey.Value;
                                    string value = hotkey.Name;
                                    string dicKey = "input_" + key;

                                    switch (emulator)
                                    {
                                        case "bigpemu":
                                            var bigpemuHotkey = GetEmulatorHotkey("bigpemu");

                                            if (bigpemuHotkey == null)
                                                break;

                                            var bigpemuhkInfo = bigpemuHotkey.EmulatorHotkeys.FirstOrDefault(h => h.RetroArchHK.Equals(dicKey, StringComparison.OrdinalIgnoreCase));

                                            if (bigpemuhkInfo != null)
                                                padHKDic.Add(bigpemuhkInfo.EmulatorHK, value);

                                            break;
                                        
                                        case "flycast":
                                            var flycastHotkey = GetEmulatorHotkey("flycast");

                                            if (flycastHotkey == null)
                                                break;

                                            var flycasthkInfo = flycastHotkey.EmulatorHotkeys.FirstOrDefault(h => h.RetroArchHK.Equals(dicKey, StringComparison.OrdinalIgnoreCase));

                                            if (flycasthkInfo != null)
                                                padHKDic.Add(flycasthkInfo.EmulatorHK, value);

                                            break;
                                    }
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

        private static void WriteXMLPad2KeyFile(string emulator, string core, Dictionary<string, HotkeyResult> hotkeys)
        {
            try
            {
                if (GetPad2KeyHKFromFile(emulator, core, out Dictionary<string, string> padHKDic))
                    padHotkey = padHKDic;

                var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null));
                var root = new XElement("padToKey");
                doc.Add(root);
                var app = new XElement("app", new XAttribute("name", emulator));
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

                foreach (var mapping in hotkeys)
                {
                    string orgKey = mapping.Key;
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
