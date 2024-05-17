using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System;

namespace EmulatorLauncher
{
    partial class MesenGenerator : Generator
    {
        private void SetupControllers(DynamicJson pref, DynamicJson systemSection, string mesenSystem)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // clear existing mapping sections of json file
            var portList = nesPorts;
            if (mesenSystem == "Snes")
                portList = snesPorts;
            else if (mesenSystem == "GameBoy")
                portList = gbPorts;
            else if (mesenSystem == "PcEngine")
                portList = pcePorts;
            else if (mesenSystem == "Sms")
                portList = smsPorts;

            foreach (string port in portList)
            {
                var portSection = systemSection.GetOrCreateContainer(port);
                if (portSection != null)
                {
                    portSection["Type"] = "None";
                    portSection["TurboSpeed"] = "0";
                    for (int i = 1; i < 5; i++)
                    {
                        var mappingSection = portSection.GetOrCreateContainer("Mapping" + i);
                        if (mappingSection != null)
                        {
                            foreach (string button in mesenButtons)
                                mappingSection[button] = "0";
                        }
                    }
                }
            }

            int maxPad = 1;
            if (systemMaxPad.ContainsKey(mesenSystem))
                maxPad = systemMaxPad[mesenSystem];

            // Configure multitap usage
            ConfigureMultitap(systemSection, mesenSystem);

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(pref, systemSection, controller, mesenSystem);
        }

        private void ConfigureInput(DynamicJson pref, DynamicJson systemSection, Controller controller, string mesenSystem)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(pref, systemSection, controller.Config, mesenSystem);
            else
                ConfigureJoystick(pref, systemSection, controller, mesenSystem);
        }

        private void ConfigureJoystick(DynamicJson pref, DynamicJson systemSection, Controller ctrl, string mesenSystem)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            bool revertButtons = SystemConfig.isOptSet("mesen_revertbuttons") && SystemConfig.getOptBoolean("mesen_revertbuttons");
            bool isXInput = ctrl.IsXInputDevice;
            int index = isXInput ? ctrl.XInput.DeviceIndex : (ctrl.DirectInput != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex);
            int playerIndex = ctrl.PlayerIndex;

            // Define port to use
            string portSection = DefinePortToUse(playerIndex, mesenSystem);

            if (portSection == null)
                return;

            var port = systemSection.GetOrCreateContainer(portSection);
            var mapping = port.GetOrCreateContainer("Mapping1");

            string controllerType = "None";
            if (SystemConfig.isOptSet("mesen_controller" + playerIndex) && !string.IsNullOrEmpty(SystemConfig["mesen_controller" + playerIndex]))
                controllerType = SystemConfig["mesen_controller" + playerIndex];
            else if (systemDefaultController.ContainsKey(mesenSystem))
                controllerType = systemDefaultController[mesenSystem];

            port["Type"] = controllerType;

            if (mesenSystem == "Nes" || mesenSystem == "Gameboy" || mesenSystem == "Sms")
            {
                if (portSection == "Port1A")
                {
                    port = systemSection.GetOrCreateContainer("Port1");
                    mapping = port.GetOrCreateContainer("Mapping1");
                }
                if (revertButtons)
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString();
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString();
                }
                else
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString();
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString();
                }

                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString();
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString();
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString();
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString();
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString();

                if (revertButtons)
                {
                    mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString();
                    mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString();
                }
                else
                {
                    mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString();
                    mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString();
                }
            }

            else if (mesenSystem == "Snes")
            {
                if (portSection == "Port1A")
                {
                    port = systemSection.GetOrCreateContainer("Port1");
                    mapping = port.GetOrCreateContainer("Mapping1");
                }
                if (controllerType == "SnesMouse")
                {
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping.SetObject("MouseButtons", mouseID);
                }
                mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString();
                mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString();
                mapping["X"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString();
                mapping["Y"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString();
                mapping["L"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])).ToString();
                mapping["R"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])).ToString();
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString();
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString();
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString();
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString();
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString();
                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            }

            else if (mesenSystem == "PcEngine")
            {
                if (portSection == "Port1A")
                {
                    port = systemSection.GetOrCreateContainer("Port1");
                    mapping = port.GetOrCreateContainer("Mapping1");
                }
                mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString();
                mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString();
                mapping["X"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString();
                mapping["Y"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString();
                mapping["L"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])).ToString();
                mapping["R"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])).ToString();
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString();
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString();
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString();
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString();
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString();
                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            }

            if (playerIndex == 1)
                WriteHotkeys(pref, index, isXInput);
        }

        private void ConfigureKeyboard(DynamicJson pref, DynamicJson systemSection, InputConfig keyboard, string mesenSystem)
        {
            if (keyboard == null)
                return;

            // Define port to use
            string portSection = DefineKBPortToUse(mesenSystem);

            if (portSection == null)
                return;

            var port = systemSection.GetOrCreateContainer(portSection);
            var mapping = port.GetOrCreateContainer("Mapping2");

            string controllerType = "None";
            if (SystemConfig.isOptSet("mesen_controller1") && !string.IsNullOrEmpty(SystemConfig["mesen_controller1"]))
                controllerType = SystemConfig["mesen_controller1"];
            else if (systemDefaultController.ContainsKey(mesenSystem))
                controllerType = systemDefaultController[mesenSystem];

            port["Type"] = controllerType;

            Action<DynamicJson, string, InputKey> WriteKeyboardMapping = (v, w, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    v[w] = SdlToKeyCode(a.Id).ToString();
                }
                else
                    v[w] = "Unbound";
            };

            if (mesenSystem == "Nes" || mesenSystem == "Gameboy")
            {
                if (portSection == "Port1A")
                {
                    port = systemSection.GetOrCreateContainer("Port1");
                    mapping = port.GetOrCreateContainer("Mapping2");
                }
                WriteKeyboardMapping(mapping, "A", InputKey.b);
                WriteKeyboardMapping(mapping, "B", InputKey.a);
                WriteKeyboardMapping(mapping, "Select", InputKey.select);
                WriteKeyboardMapping(mapping, "Start", InputKey.start);
                WriteKeyboardMapping(mapping, "Up", InputKey.up);
                WriteKeyboardMapping(mapping, "Down", InputKey.down);
                WriteKeyboardMapping(mapping, "Left", InputKey.left);
                WriteKeyboardMapping(mapping, "Right", InputKey.right);
                WriteKeyboardMapping(mapping, "TurboA", InputKey.x);
                WriteKeyboardMapping(mapping, "TurboB", InputKey.y);
            }

            else if (mesenSystem == "Snes")
            {
                if (portSection == "Port1A")
                {
                    port = systemSection.GetOrCreateContainer("Port1");
                    mapping = port.GetOrCreateContainer("Mapping2");
                }
                if (controllerType == "SnesMouse")
                {
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping.SetObject("MouseButtons", mouseID);
                }
                WriteKeyboardMapping(mapping, "A", InputKey.b);
                WriteKeyboardMapping(mapping, "B", InputKey.a);
                WriteKeyboardMapping(mapping, "X", InputKey.x);
                WriteKeyboardMapping(mapping, "Y", InputKey.y);
                WriteKeyboardMapping(mapping, "L", InputKey.pageup);
                WriteKeyboardMapping(mapping, "R", InputKey.pagedown);
                WriteKeyboardMapping(mapping, "Up", InputKey.up);
                WriteKeyboardMapping(mapping, "Down", InputKey.down);
                WriteKeyboardMapping(mapping, "Left", InputKey.left);
                WriteKeyboardMapping(mapping, "Right", InputKey.right);
                WriteKeyboardMapping(mapping, "Select", InputKey.select);
                WriteKeyboardMapping(mapping, "Start", InputKey.start);
            }

            else if (mesenSystem == "PcEngine")
            {
                if (portSection == "Port1A")
                {
                    port = systemSection.GetOrCreateContainer("Port1");
                    mapping = port.GetOrCreateContainer("Mapping2");
                }
                WriteKeyboardMapping(mapping, "A", InputKey.b);
                WriteKeyboardMapping(mapping, "B", InputKey.a);
                WriteKeyboardMapping(mapping, "X", InputKey.x);
                WriteKeyboardMapping(mapping, "Y", InputKey.y);
                WriteKeyboardMapping(mapping, "L", InputKey.pageup);
                WriteKeyboardMapping(mapping, "R", InputKey.pagedown);
                WriteKeyboardMapping(mapping, "Up", InputKey.up);
                WriteKeyboardMapping(mapping, "Down", InputKey.down);
                WriteKeyboardMapping(mapping, "Left", InputKey.left);
                WriteKeyboardMapping(mapping, "Right", InputKey.right);
                WriteKeyboardMapping(mapping, "Select", InputKey.select);
                WriteKeyboardMapping(mapping, "Start", InputKey.start);
            }

            else if (mesenSystem == "Sms")
            {
                WriteKeyboardMapping(mapping, "A", InputKey.b);
                WriteKeyboardMapping(mapping, "B", InputKey.a);
                WriteKeyboardMapping(mapping, "Up", InputKey.up);
                WriteKeyboardMapping(mapping, "Down", InputKey.down);
                WriteKeyboardMapping(mapping, "Left", InputKey.left);
                WriteKeyboardMapping(mapping, "Right", InputKey.right);
                WriteKeyboardMapping(mapping, "Select", InputKey.select);
                WriteKeyboardMapping(mapping, "Start", InputKey.start);
                WriteKeyboardMapping(mapping, "TurboA", InputKey.x);
                WriteKeyboardMapping(mapping, "TurboB", InputKey.y);
            }
        }

        private string DefinePortToUse(int playerIndex, string mesenSystem)
        {
            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_nes_multitap") && (!string.IsNullOrEmpty(SystemConfig["mesen_nes_multitap"]) || SystemConfig["mesen_nes_multitap"] != "none"))
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1A";
                        case 2:
                            return "Port1B";
                        case 3:
                            return "Port1C";
                        case 4:
                            return "Port1D";
                        case 5:
                            return "ExpPortA";
                        case 6:
                            return "ExpPortB";
                        case 7:
                            return "ExpPortC";
                        case 8:
                            return "ExpPortD";
                    }
                }
                else
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1";
                        case 2:
                            return "Port2";
                        case 3:
                            return null;
                        case 4:
                            return null;
                        case 5:
                            return null;
                        case 6:
                            return null;
                        case 7:
                            return null;
                        case 8:
                            return null;
                    }
                }
            }
            else if (mesenSystem == "Snes")
            {
                if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "dual")
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1A";
                        case 2:
                            return "Port1B";
                        case 3:
                            return "Port1C";
                        case 4:
                            return "Port1D";
                        case 5:
                            return "Port2A";
                        case 6:
                            return "Port2B";
                        case 7:
                            return "Port2C";
                        case 8:
                            return "Port2D";
                    }
                }
                else if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "single")
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1A";
                        case 2:
                            return "Port1B";
                        case 3:
                            return "Port1C";
                        case 4:
                            return "Port1D";
                        case 5:
                            return "Port2";
                        case 6:
                            return null;
                        case 7:
                            return null;
                        case 8:
                            return null;
                    }
                }
                else
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1";
                        case 2:
                            return "Port2";
                        case 3:
                            return null;
                        case 4:
                            return null;
                        case 5:
                            return null;
                        case 6:
                            return null;
                        case 7:
                            return null;
                        case 8:
                            return null;
                    }
                }
            }
            else if (mesenSystem == "PcEngine")
            {
                if (SystemConfig.isOptSet("mesen_pce_multitap") && SystemConfig.getOptBoolean("mesen_pce_multitap"))
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1A";
                        case 2:
                            return "Port1B";
                        case 3:
                            return "Port1C";
                        case 4:
                            return "Port1D";
                        case 5:
                            return "Port1E";
                    }
                }
                else
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1";
                        case 2:
                            return null;
                        case 3:
                            return null;
                        case 4:
                            return null;
                        case 5:
                            return null;
                        case 6:
                            return null;
                        case 7:
                            return null;
                        case 8:
                            return null;
                    }
                }
            }
            else if (mesenSystem == "Gameboy")
            {
                return "Controller";
            }
            else if (mesenSystem == "Sms")
            {
                if (SystemConfig.getOptBoolean("mesen_sms_guncontroller") && SystemConfig["mesen_zapper"] == "Port1")
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port2";
                        case 2:
                            return null;
                    }
                }
                else
                {
                    switch (playerIndex)
                    {
                        case 1:
                            return "Port1";
                        case 2:
                            return "Port2";
                    }
                }
            }

            return null;
        }

        private string DefineKBPortToUse(string mesenSystem)
        {
            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_nes_multitap") && (!string.IsNullOrEmpty(SystemConfig["mesen_nes_multitap"]) || SystemConfig["mesen_nes_multitap"] != "none"))
                    return "Port1A";
                else
                    return "Port1";

            }
            else if (mesenSystem == "Snes")
            {
                if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "dual")
                    return "Port1A";
                else if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "single")
                    return "Port1A";
                else
                    return "Port1";
            }
            else if (mesenSystem == "PcEngine")
            {
                if (SystemConfig.isOptSet("mesen_pce_multitap") && SystemConfig.getOptBoolean("mesen_pce_multitap"))
                    return "Port1A";
                else
                    return "Port1";
            }
            else if (mesenSystem == "Gameboy")
                return "Controller";
            else if (mesenSystem == "Sms")
                return "Port1";

            return null;
        }

        private void ConfigureMultitap(DynamicJson systemSection, string mesenSystem)
        {
            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_nes_multitap") && SystemConfig["mesen_nes_multitap"] == "dual")
                {
                    var port1 = systemSection.GetOrCreateContainer("Port1");
                    port1["Type"] = "FourScore";
                    var expport = systemSection.GetOrCreateContainer("ExpPort");
                    expport["Type"] = "FourPlayerAdapter";
                }
                else if (SystemConfig.isOptSet("mesen_nes_multitap") && SystemConfig["mesen_nes_multitap"] == "single")
                {
                    var port1 = systemSection.GetOrCreateContainer("Port1");
                    port1["Type"] = "FourScore";
                }
            }

            else if (mesenSystem == "Snes")
            {
                if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "dual")
                {
                    var port1 = systemSection.GetOrCreateContainer("Port1");
                    port1["Type"] = "Multitap";
                    var port2 = systemSection.GetOrCreateContainer("Port2");
                    port2["Type"] = "Multitap";
                }
                else if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "single")
                {
                    var port1 = systemSection.GetOrCreateContainer("Port1");
                    port1["Type"] = "Multitap";
                }
            }

            else if (mesenSystem == "PcEngine")
            {
                if (SystemConfig.isOptSet("mesen_pce_multitap") && SystemConfig.getOptBoolean("mesen_pce_multitap"))
                {
                    var port1 = systemSection.GetOrCreateContainer("Port1");
                    port1["Type"] = "PceTurboTap";
                }
            }
        }

        private void WriteHotkeys(DynamicJson pref, int index, bool isXInput)
        {
            pref.Remove("ShortcutKeys");
            var shortcuts = new List<DynamicJson>();
            
            var ffshortcut = new DynamicJson();
            ffshortcut["Shortcut"] = "FastForward";
            var ffkeys = ffshortcut.GetOrCreateContainer("KeyCombination2");
            ffkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            ffkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString();
            ffkeys["Key3"] = "0";
            shortcuts.Add(ffshortcut);

            var rewshortcut = new DynamicJson();
            rewshortcut["Shortcut"] = "Rewind";
            var rewkeys = rewshortcut.GetOrCreateContainer("KeyCombination2");
            rewkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            rewkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString();
            rewkeys["Key3"] = "0";
            shortcuts.Add(rewshortcut);

            var shotshortcut = new DynamicJson();
            shotshortcut["Shortcut"] = "TakeScreenshot";
            var shotkeys = shotshortcut.GetOrCreateContainer("KeyCombination2");
            shotkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            shotkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.r3])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.r3])).ToString();
            shotkeys["Key3"] = "0";
            shortcuts.Add(shotshortcut);

            var pauseshortcut = new DynamicJson();
            pauseshortcut["Shortcut"] = "Pause";
            var pausekeys = pauseshortcut.GetOrCreateContainer("KeyCombination2");
            pausekeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            pausekeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString();
            pausekeys["Key3"] = "0";
            shortcuts.Add(pauseshortcut);

            var nextslotshortcut = new DynamicJson();
            nextslotshortcut["Shortcut"] = "MoveToNextStateSlot";
            var nextslotkeys = nextslotshortcut.GetOrCreateContainer("KeyCombination2");
            nextslotkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            nextslotkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString();
            nextslotkeys["Key3"] = "0";
            shortcuts.Add(nextslotshortcut);

            var prevslotshortcut = new DynamicJson();
            prevslotshortcut["Shortcut"] = "MoveToPreviousStateSlot";
            var prevslotkeys = prevslotshortcut.GetOrCreateContainer("KeyCombination2");
            prevslotkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            prevslotkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString();
            prevslotkeys["Key3"] = "0";
            shortcuts.Add(prevslotshortcut);

            var savestateshortcut = new DynamicJson();
            savestateshortcut["Shortcut"] = "SaveState";
            var savekeys = savestateshortcut.GetOrCreateContainer("KeyCombination2");
            savekeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            savekeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString();
            savekeys["Key3"] = "0";
            shortcuts.Add(savestateshortcut);

            var loadstateshortcut = new DynamicJson();
            loadstateshortcut["Shortcut"] = "LoadState";
            var loadkeys = loadstateshortcut.GetOrCreateContainer("KeyCombination2");
            loadkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            loadkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString();
            loadkeys["Key3"] = "0";
            shortcuts.Add(loadstateshortcut);

            var toggleffshortcut = new DynamicJson();
            toggleffshortcut["Shortcut"] = "ToggleFastForward";
            var fftogglekeys = toggleffshortcut.GetOrCreateContainer("KeyCombination2");
            fftogglekeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            fftogglekeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])).ToString();
            fftogglekeys["Key3"] = "0";
            shortcuts.Add(toggleffshortcut);

            var togglerewshortcut = new DynamicJson();
            togglerewshortcut["Shortcut"] = "ToggleRewind";
            var rewtogglekeys = togglerewshortcut.GetOrCreateContainer("KeyCombination2");
            rewtogglekeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            rewtogglekeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])).ToString();
            rewtogglekeys["Key3"] = "0";
            shortcuts.Add(togglerewshortcut);

            var exitshortcut = new DynamicJson();
            exitshortcut["Shortcut"] = "Exit";
            var exitkeys = exitshortcut.GetOrCreateContainer("KeyCombination2");
            exitkeys["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString();
            exitkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString();
            exitkeys["Key3"] = "0";
            shortcuts.Add(exitshortcut);

            pref.SetObject("ShortcutKeys", shortcuts);
        }

        static readonly List<string> xbuttonNames = new List<string>() { "Up", "Down", "Left", "Right", "Start", "Select", "L3", "R3", "L1", "R1", "?", "?", "South", "East", "West", "North", "L2", "R2", "RT Up", "RT Down", "RT Left", "RT Right", "LT Up", "LT Down", "LT Left", "LT Right" };
        static readonly List<string> dibuttonNames = new List<string>() { "LT Up", "LT Down", "LT Left", "LT Right", "RT Up", "RT Down", "RT Left", "RT Right", "Z+", "Z-", "Z2+", "Z2-", "Up", "Down", "Right", "Left", "West", "South", "East", "North", "L1", "R1", "L2", "R2", "Select", "Start", "L3", "R3", "Guide" };
        static readonly List<string> mesenButtons = new List<string>() { "A", "B", "X", "Y", "L", "R", "Up", "Down", "Left", "Right", "Start", "Select", "TurboA", "TurboB", "TurboX", "TurboY", "TurboL", "TurboR", "TurboSelect", "TurboStart" };

        static readonly List<string> nesPorts = new List<string>() { "Port1", "Port2", "ExpPort", "Port1A", "Port1B", "Port1C", "Port1D", "ExpPortA", "ExpPortB", "ExpPortC", "ExpPortD", "MapperInput" };
        static readonly List<string> snesPorts = new List<string>() { "Port1", "Port2", "Port1A", "Port1B", "Port1C", "Port1D", "Port2A", "Port2B", "Port2C", "Port2D" };
        static readonly List<string> gbPorts = new List<string>() { "Controller" };
        static readonly List<string> pcePorts = new List<string>() { "Port1", "Port1A", "Port1B", "Port1C", "Port1D", "Port1E" };
        static readonly List<string> smsPorts = new List<string>() { "Port1", "Port2" };

        static readonly Dictionary<InputKey, string> inputKeyMapping = new Dictionary<InputKey, string>()
        {
            { InputKey.b, "East" },
            { InputKey.a, "South" },
            { InputKey.y, "West" },
            { InputKey.x, "North" },
            { InputKey.up, "Up" },
            { InputKey.down, "Down" },
            { InputKey.left, "Left" },
            { InputKey.right, "Right" },
            { InputKey.pageup, "L1" },
            { InputKey.pagedown, "R1" },
            { InputKey.l2, "L2" },
            { InputKey.r2, "R2" },
            { InputKey.l3, "L3" },
            { InputKey.r3, "R3" },
            { InputKey.select, "Select" },
            { InputKey.start, "Start" },
            { InputKey.leftanalogup, "LT Up" },
            { InputKey.leftanalogdown, "LT Down" },
            { InputKey.leftanalogleft, "LT Left" },
            { InputKey.leftanalogright, "LT Right" },
            { InputKey.rightanalogup, "RT Up" },
            { InputKey.rightanalogdown, "RT Down" },
            { InputKey.rightanalogleft, "RT Left" },
            { InputKey.rightanalogright, "RT Right" },
        };

        static readonly Dictionary<string, int> systemMaxPad = new Dictionary<string, int>()
        {
            { "Nes", 8 },
            { "Snes", 8 },
            { "Gameboy", 1 },
            { "PcEngine", 5 },
            { "Sms", 2 }
        };

        static readonly Dictionary<string, string> systemDefaultController = new Dictionary<string, string>()
        {
            { "Nes", "NesController" },
            { "Snes", "SnesController" },
            { "Gameboy", "GameboyController" },
            { "PcEngine", "PceController" },
            { "Sms", "SmsController" }
        };

        private static string SdlToKeyCode(long sdlCode)
        {

            //The following list of keys has been verified, ryujinx will not allow wrong string so do not add a key until the description has been tested in the emulator first
            switch (sdlCode)
            {
                case 0x0D: return "6";      // ENTER
                case 0x08: return "2";      // Backspace
                case 0x09: return "3";      // TAB
                case 0x20: return "18";     // SPACE
                case 0x2B: return "0";      // Plus
                case 0x2C: return "142";    // Comma
                case 0x2D: return "0";      // Minus
                case 0x2E: return "144";    // Period
                case 0x2F: return "145";    // Slash
                case 0x30: return "34";     // Number 0
                case 0x31: return "35";
                case 0x32: return "36";
                case 0x33: return "37";
                case 0x34: return "38";
                case 0x35: return "39";
                case 0x36: return "40";
                case 0x37: return "41";
                case 0x38: return "42";
                case 0x39: return "43";     // Number 9
                case 0x3B: return "140";    // Semi column
                case 0x61: return "44";     // A
                case 0x62: return "45";
                case 0x63: return "46";
                case 0x64: return "47";
                case 0x65: return "48";
                case 0x66: return "49";
                case 0x67: return "50";
                case 0x68: return "51";
                case 0x69: return "52";
                case 0x6A: return "53";
                case 0x6B: return "54";
                case 0x6C: return "55";
                case 0x6D: return "56";
                case 0x6E: return "57";
                case 0x6F: return "58";
                case 0x70: return "59";
                case 0x71: return "60";
                case 0x72: return "61";
                case 0x73: return "62";
                case 0x74: return "63";
                case 0x75: return "64";
                case 0x76: return "65";
                case 0x77: return "66";
                case 0x78: return "67";
                case 0x79: return "68";
                case 0x7A: return "69";             // Z
                case 0x7F: return "32";             // Delete
                case 0x4000003A: return "90";       // F1
                case 0x4000003B: return "91";
                case 0x4000003C: return "92";
                case 0x4000003D: return "93";
                case 0x4000003E: return "94";
                case 0x4000003F: return "95";
                case 0x40000040: return "96";
                case 0x40000041: return "97";
                case 0x40000042: return "98";
                case 0x40000043: return "99";
                case 0x40000044: return "100";
                case 0x40000045: return "101";      // F12
                case 0x40000047: return "0";        // Scrolllock
                case 0x40000048: return "0";        // Pause
                case 0x40000049: return "31";       // Insert
                case 0x4000004A: return "22";       // Home
                case 0x4000004B: return "19";       // PageUp
                case 0x4000004D: return "21";       // End
                case 0x4000004E: return "20";       // Page Down
                case 0x4000004F: return "25";       // Right  
                case 0x40000050: return "23";       // Left
                case 0x40000051: return "26";       // Down
                case 0x40000052: return "24";       // Up
                case 0x40000053: return "114";      // Numlock  
                case 0x40000054: return "89";       // KeypadDivide
                case 0x40000055: return "84";       // KeypadMultiply
                case 0x40000056: return "87";       // KeypadSubtract
                case 0x40000057: return "85";       // KeypadAdd
                case 0x40000058: return "6";        // Enter
                case 0x40000059: return "75";       // Numpad 1
                case 0x4000005A: return "76";
                case 0x4000005B: return "77";
                case 0x4000005C: return "78";
                case 0x4000005D: return "79";
                case 0x4000005E: return "80";
                case 0x4000005F: return "81";
                case 0x40000060: return "82";
                case 0x40000061: return "83";
                case 0x40000062: return "74";       // Numpad 0
                case 0x40000063: return "88";       // KeypadDecimal
                case 0x40000085: return "88";
                case 0x400000E0: return "118";      // Left control
                case 0x400000E1: return "116";      // Left shift
                case 0x400000E2: return "120";      // Left ALT
                case 0x400000E4: return "119";      // Right control
                case 0x400000E5: return "117";      // Right shift
            }
            return "0";
        }
    }
}
