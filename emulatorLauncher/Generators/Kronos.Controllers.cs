using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Globalization;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using System.IO;

namespace EmulatorLauncher
{
    partial class KronosGenerator
    {
        /// <summary>
        /// Cf. https://github.com/PCSX2/pcsx2/blob/master/pcsx2/Frontend/SDLInputSource.cpp#L211
        /// </summary>
        /// <param name="pcsx2ini"></param>
        /*private void UpdateSdlControllersWithHints(IniFile ini)
        {
            var hints = new List<string>();

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }*/

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // UpdateSdlControllersWithHints(ini);

            if (SystemConfig.isOptSet("kronos_multitap") && !string.IsNullOrEmpty(SystemConfig["kronos_multitap"]))
                _multitap = SystemConfig["kronos_multitap"];
            else if (Controllers.Count > 7)
                _multitap = "both";
            else if (Controllers.Count > 2)
                _multitap = "2";
            else
                _multitap = "0";

            // clear existing pad sections of ini file
            for (int i = 1; i < 7; i++)
            {
                string port1Type = "Input\\Port\\1\\Id\\" + i.ToString() + "\\Type";
                string port2Type = "Input\\Port\\2\\Id\\" + i.ToString() + "\\Type";

                ini.WriteValue("1.0", port1Type, "0");
                ini.WriteValue("1.0", port2Type, "0");
            }

            ini.WriteValue("1.0", "Input\\PerCore", "3");

            // Configure hotkeys
            ResetHotkeysToDefault(ini);

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(ini, controller, controller.PlayerIndex);
        }

        private void ConfigureInput(IniFile ini, Controller controller, int playerindex)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, playerindex);
            else
                ConfigureJoystick(ini, controller, playerindex);
        }


        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            string port = GetControllerPort(playerindex);
            string controllerId = GetControllerId(playerindex);
            string cType = "2";
            int index = ctrl.SdlController != null ? ctrl.SdlController.Index : ctrl.DeviceIndex;
            bool isXinput = ctrl.IsXInputDevice;

            // Special mapping for saturn-like controllers in json file
            string guid = (ctrl.Guid.ToString()).ToLowerInvariant();
            bool needSatActivationSwitch = false;
            bool sat_pad = Program.SystemConfig["saturn_pad_kronos"] == "1" || Program.SystemConfig["saturn_pad_kronos"] == "2";

            string saturnjson = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "saturnControllers.json");
            if (File.Exists(saturnjson))
            {
                try
                {
                    var saturnControllers = MegadriveController.LoadControllersFromJson(saturnjson);

                    if (saturnControllers != null)
                    {
                        MegadriveController saturnGamepad = MegadriveController.GetMDController("kronos", guid, saturnControllers);

                        if (saturnGamepad != null)
                        {
                            if (saturnGamepad.ControllerInfo != null)
                            {
                                if (saturnGamepad.ControllerInfo.ContainsKey("needActivationSwitch"))
                                    needSatActivationSwitch = saturnGamepad.ControllerInfo["needActivationSwitch"] == "yes";

                                if (needSatActivationSwitch && !sat_pad)
                                {
                                    SimpleLogger.Instance.Info("[Controller] Specific Saturn mapping needs to be activated for this controller.");
                                    goto BypassSATControllers;
                                }
                            }

                            SimpleLogger.Instance.Info("[Controller] Performing specific mapping for " + saturnGamepad.Name);

                            if (saturnGamepad.Mapping != null)
                            {
                                bool inputAnal = Program.SystemConfig["saturn_pad_kronos"] == "2";

                                // write mapping here
                                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Type", cType);

                                foreach (var button in saturnGamepad.Mapping)
                                {
                                    if (inputAnal && digitalKeys.Contains(button.Key))
                                        continue;
                                    else if (!inputAnal && analKeys.Contains(button.Key))
                                        continue;

                                    long.TryParse(button.Value, out long code);

                                    string key = button.Key;
                                    if (button.Key.StartsWith("anal_"))
                                        key = button.Key.Substring(5, 1);

                                    if (code >= 0)
                                    {
                                        code = (index << 18) + code;
                                        ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\" + key, code.ToString());
                                    }
                                }

                                SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());

                                return;
                            }
                            else
                                SimpleLogger.Instance.Info("[INFO] Missing mapping for Saturn Gamepad, falling back to standard mapping.");
                        }
                        else
                            SimpleLogger.Instance.Info("[Controller] No specific mapping found for Saturn controller.");
                    }
                    else
                        SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");
                }
                catch { }
            }

            BypassSATControllers:

            string padlayout = "lr_yz";
            if (SystemConfig.isOptSet("kronos_padlayout") && !string.IsNullOrEmpty(SystemConfig["kronos_padlayout"]))
                padlayout = SystemConfig["kronos_padlayout"];

            ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Type", cType);

            if (SystemConfig.isOptSet("yaba_leftstick") && SystemConfig.getOptBoolean("yaba_leftstick"))
            {
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\0", GetInputKeyName(ctrl, InputKey.leftanalogup, index));
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\1", GetInputKeyName(ctrl, InputKey.leftanalogright, index));
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\2", GetInputKeyName(ctrl, InputKey.leftanalogdown, index));
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\3", GetInputKeyName(ctrl, InputKey.leftanalogleft, index));
            }
            else
            {
                
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\0", GetInputKeyName(ctrl, InputKey.up, index));
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\1", GetInputKeyName(ctrl, InputKey.right, index));
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\2", GetInputKeyName(ctrl, InputKey.down, index));
                ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\3", GetInputKeyName(ctrl, InputKey.left, index));
            }

            ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\4", GetInputKeyName(ctrl, InputKey.r2, index, isXinput));
            ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\5", GetInputKeyName(ctrl, InputKey.l2, index, isXinput));
            ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\6", GetInputKeyName(ctrl, InputKey.start, index));

            if (padlayout == "lr_xz")
            {
                foreach (KeyValuePair<string, InputKey> pair in lr_xz_profile)
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\" + pair.Key, GetInputKeyName(ctrl, pair.Value, index));
            }

            else if (padlayout == "lr_zc")
            {
                foreach (KeyValuePair<string, InputKey> pair in lr_zc_profile)
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\" + pair.Key, GetInputKeyName(ctrl, pair.Value, index));
            }
            
            else
            {
                foreach (KeyValuePair<string, InputKey> pair in lr_yz_profile)
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\" + cType + "\\Key\\" + pair.Key, GetInputKeyName(ctrl, pair.Value, index));
            }
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            string port = GetControllerPort(playerindex);
            string controllerId = GetControllerId(playerindex);

            ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Type", "2");

            bool azerty = false;
            List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
                azerty = true;

            if (azerty)
            {
                foreach (KeyValuePair<string, string> pair in AzertyLayout)
                {
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\2" + "\\Key\\" + pair.Key, pair.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> pair in keyboardMapping)
                {
                    ini.WriteValue("1.0", "Input\\Port\\" + port + "\\Id\\" + controllerId + "\\Controller\\2" + "\\Key\\" + pair.Key, pair.Value);
                }
            }
        }

        private static string GetInputKeyName(Controller c, InputKey key, int index, bool isXinput = false)
        {
            key = key.GetRevertedAxis(out bool revertAxis);
            Int64 pid;
            string ret = "";

            var input = c.Config[key];

            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id + 1;
                    ret = ((index << 18) + pid).ToString();
                    return ret;
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;

                    if (isXinput && input.Id == 4)
                        pid = 2;

                    if (input.Value > 0 || revertAxis)
                        ret = ((index << 18) + 0x110000 + pid).ToString();
                    else
                        ret = ((index << 18) + 0x100000 + pid).ToString();

                    return ret;
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    ret = ((index << 18) + 0x200000 + (pid << 4)).ToString();
                    return ret;
                }
            }
            return ret;
        }

        private void ResetHotkeysToDefault(IniFile ini)
        {
            ini.WriteValue("1.0", "Shortcuts\\%26Quitter", "Esc");
            ini.WriteValue("1.0", "Shortcuts\\L%26ancer", "F1");
            ini.WriteValue("1.0", "Shortcuts\\%26Pause", "F2");
            ini.WriteValue("1.0", "Shortcuts\\Sc%26reenshot", "F8");
        }

        private string GetControllerPort(int playerindex)
        {
            if (_multitap == "both")
                return multitap_ports[playerindex];

            if (_multitap == "1")
                return multitap_port1[playerindex];

            if (_multitap == "2")
                return multitap_port2[playerindex];

            if (playerindex == 1)
                return "1";

            if (playerindex == 2)
                return "2";

            return "1";
        }

        private string GetControllerId(int playerindex)
        {
            if (playerindex < 13)
                return multitap_id[playerindex];

            return "1";
        }

        private static List<string> digitalKeys = new List<string> { "0", "1", "2", "3" };
        private static List<string> analKeys = new List<string> { "anal_0", "anal_1", "anal_2", "anal_3" };

        static readonly Dictionary<string, InputKey> lr_yz_profile = new Dictionary<string, InputKey>
        {
            { "7", InputKey.y },
            { "8", InputKey.a },
            { "9", InputKey.b },
            { "10", InputKey.x },
            { "11", InputKey.pageup },
            { "12", InputKey.pagedown },
        };

        static readonly Dictionary<string, InputKey> lr_zc_profile = new Dictionary<string, InputKey>
        {
            { "7", InputKey.a },
            { "8", InputKey.b },
            { "9", InputKey.pagedown },
            { "10", InputKey.y },
            { "11", InputKey.x },
            { "12", InputKey.pageup },
        };

        static readonly Dictionary<string, InputKey> lr_xz_profile = new Dictionary<string, InputKey>
        {
            { "7", InputKey.y },
            { "8", InputKey.a },
            { "9", InputKey.b },
            { "10", InputKey.pageup },
            { "11", InputKey.x },
            { "12", InputKey.pagedown },
        };

        static readonly Dictionary<string, string> keyboardMapping = new Dictionary<string, string>
        {
            { "0", "16777235" },            // up
            { "1", "16777236" },            // Right
            { "2", "16777237" },            // Down
            { "3", "16777234" },            // Left
            { "4", "69" },                  // Right trigger - E
            { "5", "81" },                  // Left trigger - Q
            { "6", "16777220" },            // Start - enter
            { "7", "90" },                  // Z
            { "8", "88" },                  // X
            { "9", "67" },                  // C
            { "10", "65" },                 // A
            { "11", "83" },                 // S
            { "12", "68" },                 // D
        };

        static readonly Dictionary<string, string> AzertyLayout = new Dictionary<string, string>
        {
            { "0", "16777235" },            // up
            { "1", "16777236" },            // Right
            { "2", "16777237" },            // Down
            { "3", "16777234" },            // Left
            { "4", "69" },                  // Right trigger - E
            { "5", "65" },                  // Left trigger - A
            { "6", "16777220" },            // Start - enter
            { "7", "87" },                  // W
            { "8", "88" },                  // X
            { "9", "67" },                  // C
            { "10", "81" },                 // Q
            { "11", "83" },                 // S
            { "12", "68" },                 // D
        };

        static readonly Dictionary<int, string> multitap_port1 = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "2" },
            { 3, "1" },
            { 4, "1" },
            { 5, "1" },
            { 6, "1" },
            { 7, "1" },
        };

        static readonly Dictionary<int, string> multitap_port2 = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "2" },
            { 3, "2" },
            { 4, "2" },
            { 5, "2" },
            { 6, "2" },
            { 7, "2" },
        };

        static readonly Dictionary<int, string> multitap_ports = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "2" },
            { 3, "1" },
            { 4, "1" },
            { 5, "1" },
            { 6, "1" },
            { 7, "1" },
            { 8, "2" },
            { 9, "2" },
            { 10, "2" },
            { 11, "2" },
            { 12, "2" },
        };

        static readonly Dictionary<int, string> multitap_id = new Dictionary<int, string>
        {
            { 1, "1" },
            { 2, "1" },
            { 3, "2" },
            { 4, "3" },
            { 5, "4" },
            { 6, "5" },
            { 7, "6" },
            { 8, "2" },
            { 9, "3" },
            { 10, "4" },
            { 11, "5" },
            { 12, "6" },
        };
    }
}
