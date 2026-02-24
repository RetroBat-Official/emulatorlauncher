using EmulatorLauncher;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EmulatorLauncher
{
    partial class MesenGenerator : Generator
    {
        private void SetupControllers(JObject pref, JObject systemSection, string mesenSystem)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Mesen");

            // clear existing mapping sections of json file
            var portList = nesPorts;
            if (mesenSystem == "Snes")
                portList = snesPorts;
            else if (mesenSystem == "GameBoy")
                portList = gbPorts;
            else if (mesenSystem == "Gba")
                portList = gbaPorts;
            else if (mesenSystem == "PcEngine")
                portList = pcePorts;
            else if (mesenSystem == "Sms")
                portList = smsPorts;
            else if (mesenSystem == "Cv")
                portList = cvPorts;

            foreach (string port in portList)
            {
                var portSection = GetOrCreateContainer(systemSection, port);
                if (portSection != null)
                {
                    portSection["Type"] = "None";
                    portSection["TurboSpeed"] = 0;
                    for (int i = 1; i < 5; i++)
                    {
                        var mappingSection = GetOrCreateContainer(portSection, "Mapping" + i);
                        if (mappingSection != null)
                        {
                            foreach (string button in mesenButtons)
                                mappingSection[button] = 0;

                            if (mesenSystem == "Cv")
                            {
                                mappingSection["ColecoVisionControllerButtons"] = JValue.CreateNull();
                            }
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

        private void ConfigureInput(JObject pref, JObject systemSection, Controller controller, string mesenSystem)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard && !Controllers.Any(c => !c.IsKeyboard))
                ConfigureKeyboard(pref, systemSection, controller.Config, mesenSystem, controller.PlayerIndex);
            else if (!controller.IsKeyboard)
                ConfigureJoystick(pref, systemSection, controller, mesenSystem);
        }

        private void ConfigureJoystick(JObject pref, JObject systemSection, Controller ctrl, string mesenSystem)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            bool revertButtons = SystemConfig.isOptSet("rotate_buttons") && SystemConfig.getOptBoolean("rotate_buttons");
            if (mesenSystem == "Nes")
                revertButtons = !SystemConfig.getOptBoolean("rotate_buttons");

            bool isNintendo = ctrl.VendorID == Common.Joysticks.USB_VENDOR.NINTENDO;
            bool isXInput = ctrl.IsXInputDevice;
            int index = isXInput ? ctrl.XInput.DeviceIndex : (ctrl.DirectInput != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex);
            int playerIndex = ctrl.PlayerIndex;

            // Define port to use
            string portSection = DefinePortToUse(playerIndex, mesenSystem);

            if (portSection == null)
                return;

            var port = GetOrCreateContainer(systemSection, portSection);
            var mapping = GetOrCreateContainer(port, "Mapping1");
            var inputKeyMapping = inputKeyMappingDefault;
            if (isNintendo)
            {
                inputKeyMapping[InputKey.b] = "South";
                inputKeyMapping[InputKey.a] = "West";
                inputKeyMapping[InputKey.y] = "East";
                inputKeyMapping[InputKey.x] = "North";
            }

            string controllerType = "None";
            if (SystemConfig.isOptSet("mesen_controllertype" + playerIndex) && !string.IsNullOrEmpty(SystemConfig["mesen_controllertype" + playerIndex]))
                controllerType = SystemConfig["mesen_controllertype" + playerIndex];
            else if (systemDefaultController.ContainsKey(mesenSystem))
                controllerType = systemDefaultController[mesenSystem];

            port["Type"] = controllerType;

            if (mesenSystem == "Nes" || mesenSystem == "Sms")
            {
                if (portSection == "Port1A")
                {
                    port = GetOrCreateContainer(systemSection, "Port1");
                    mapping = GetOrCreateContainer(port, "Mapping1");
                }
                if (revertButtons)
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                }
                else
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                }

                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right]));

                if (SystemConfig.getOptBoolean("nes_turbo_enable"))
                {
                    if (revertButtons)
                    {
                        mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                        mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                    }
                    else
                    {
                        mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                        mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                    }
                }
                else
                {
                    mapping["TurboA"] = 0;
                    mapping["TurboB"] = 0;
                }
            }

            else if (mesenSystem == "Gameboy")
            {
                if (portSection == "Port1A")
                {
                    port = GetOrCreateContainer(systemSection, "Port1");
                    mapping = GetOrCreateContainer(port, "Mapping1");
                }
                if (SystemConfig.getOptBoolean("buttonsInvert"))
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                }
                else
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                }
                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right]));

                if (SystemConfig.getOptBoolean("buttonsInvert"))
                {
                    mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                    mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                }
                else
                {
                    mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                    mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                }
            }

            else if (mesenSystem == "Gba")
            {
                mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                mapping["L"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup]));
                mapping["R"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown]));
                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right]));
                mapping["TurboA"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                mapping["TurboB"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                mapping["TurboL"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.l2])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.l2]));
                mapping["TurboR"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.r2])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.r2]));
            }

            else if (mesenSystem == "Snes")
            {
                if (portSection == "Port1A")
                {
                    port = GetOrCreateContainer(systemSection,"Port1");
                    mapping = GetOrCreateContainer(port, "Mapping1");
                }
                if (controllerType == "SnesMouse")
                {
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping["MouseButtons"] = new JArray(mouseID);
                }
                if (SystemConfig.getOptBoolean("buttonsInvert"))
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                    mapping["X"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                    mapping["Y"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                }
                else
                {
                    mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                    mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                    mapping["X"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                    mapping["Y"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                }
                mapping["L"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup]));
                mapping["R"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown]));
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right]));
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
            }

            else if (mesenSystem == "PcEngine")
            {
                if (portSection == "Port1A")
                {
                    port = GetOrCreateContainer(systemSection,"Port1");
                    mapping = GetOrCreateContainer(port, "Mapping1");
                }
                mapping["A"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                mapping["B"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                mapping["X"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                mapping["Y"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                mapping["L"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup]));
                mapping["R"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown]));
                mapping["Up"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                mapping["Down"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                mapping["Left"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                mapping["Right"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right]));
                mapping["Start"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                mapping["Select"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
            }
            
            else if (mesenSystem == "Cv")
            {
                List<int> cvbuttons = new List<int>();
                if (isXInput)
                {
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pageup]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.l2]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.r2]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogup]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogdown]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogleft]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogright]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                    cvbuttons.Add(4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                }
                else
                {
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pageup]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.l2]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.pagedown]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.r2]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogup]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogdown]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogleft]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.rightanalogright]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x]));
                    cvbuttons.Add(8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b]));
                }

                mapping["ColecoVisionControllerButtons"] = new JArray(cvbuttons);
            }

            if (playerIndex == 1)
                WriteHotkeys(pref, index, isXInput, true, inputKeyMapping, mesenSystem);

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private void ConfigureKeyboard(JObject pref, JObject systemSection, InputConfig keyboard, string mesenSystem, int playerIndex)
        {
            if (keyboard == null)
                return;

            // Define port to use
            string portSection = DefineKBPortToUse(mesenSystem);

            if (portSection == null)
                return;

            var port = GetOrCreateContainer(systemSection, portSection);
            var mapping = GetOrCreateContainer(port, "Mapping2");

            string controllerType = "None";
            if (SystemConfig.isOptSet("mesen_controllertype1") && !string.IsNullOrEmpty(SystemConfig["mesen_controllertype1"]))
                controllerType = SystemConfig["mesen_controllertype1"];
            else if (systemDefaultController.ContainsKey(mesenSystem))
                controllerType = systemDefaultController[mesenSystem];

            port["Type"] = controllerType;

            Action<JObject, string, InputKey> WriteKeyboardMapping = (v, w, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    v[w] = SdlToKeyCode(a.Id).ToString().ToInteger();
                }
                else
                    v[w] = "Unbound";
            };

            if (mesenSystem == "Nes" || mesenSystem == "Gameboy")
            {
                if (portSection == "Port1A")
                {
                    port = GetOrCreateContainer(systemSection, "Port1");
                    mapping = GetOrCreateContainer(port, "Mapping2");
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

            else if (mesenSystem == "Gba")
            {
                WriteKeyboardMapping(mapping, "A", InputKey.b);
                WriteKeyboardMapping(mapping, "B", InputKey.a);
                WriteKeyboardMapping(mapping, "L", InputKey.pageup);
                WriteKeyboardMapping(mapping, "R", InputKey.pagedown);
                WriteKeyboardMapping(mapping, "Up", InputKey.up);
                WriteKeyboardMapping(mapping, "Down", InputKey.down);
                WriteKeyboardMapping(mapping, "Left", InputKey.left);
                WriteKeyboardMapping(mapping, "Right", InputKey.right);
                WriteKeyboardMapping(mapping, "Select", InputKey.select);
                WriteKeyboardMapping(mapping, "Start", InputKey.start);
                WriteKeyboardMapping(mapping, "TurboA", InputKey.x);
                WriteKeyboardMapping(mapping, "TurboB", InputKey.y);
            }

            else if (mesenSystem == "Snes")
            {
                if (portSection == "Port1A")
                {
                    port = GetOrCreateContainer(systemSection, "Port1");
                    mapping = GetOrCreateContainer(port, "Mapping2");
                }
                if (controllerType == "SnesMouse")
                {
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping["MouseButtons"] = new JArray(mouseID);
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
                    port = GetOrCreateContainer(systemSection, "Port1");
                    mapping = GetOrCreateContainer(port, "Mapping2");
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

            else if (mesenSystem == "Cv")
            {
                List<int> cvbuttons = new List<int>
                {
                    24, 26, 23, 25, 44, 62, 35, 36, 37, 38, 39, 40, 41, 42, 43, 34, 60, 66
                };
                mapping["ColecoVisionControllerButtons"] = new JArray(cvbuttons);
            }

            if (playerIndex == 1)
            {
                WriteHotkeys(pref, 0, false, false, inputKeyMappingDefault, mesenSystem);
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
            else if (mesenSystem == "Gameboy" || mesenSystem == "Gba")
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
            else if (mesenSystem == "Cv")
            {
                switch (playerIndex)
                {
                    case 1:
                        return "Port1";
                    case 2:
                        return "Port2";
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
            else if (mesenSystem == "Gameboy" || mesenSystem == "Gba")
                return "Controller";
            else if (mesenSystem == "Sms")
                return "Port1";
            else if (mesenSystem == "Cv")
                return "Port1";

            return null;
        }

        private void ConfigureMultitap(JObject systemSection, string mesenSystem)
        {
            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_nes_multitap") && SystemConfig["mesen_nes_multitap"] == "dual")
                {
                    var port1 = GetOrCreateContainer(systemSection, "Port1");
                    port1["Type"] = "FourScore";
                    var expport = GetOrCreateContainer(systemSection, "ExpPort");
                    expport["Type"] = "FourPlayerAdapter";
                }
                else if (SystemConfig.isOptSet("mesen_nes_multitap") && SystemConfig["mesen_nes_multitap"] == "single")
                {
                    var port1 = GetOrCreateContainer(systemSection, "Port1");
                    port1["Type"] = "FourScore";
                }
            }

            else if (mesenSystem == "Snes")
            {
                if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "dual")
                {
                    var port1 = GetOrCreateContainer(systemSection, "Port1");
                    port1["Type"] = "Multitap";
                    var port2 = GetOrCreateContainer(systemSection, "Port2");
                    port2["Type"] = "Multitap";
                }
                else if (SystemConfig.isOptSet("mesen_snes_multitap") && SystemConfig["mesen_snes_multitap"] == "single")
                {
                    var port1 = GetOrCreateContainer(systemSection,"Port1");
                    port1["Type"] = "Multitap";
                }
            }

            else if (mesenSystem == "PcEngine")
            {
                if (SystemConfig.isOptSet("mesen_pce_multitap") && SystemConfig.getOptBoolean("mesen_pce_multitap"))
                {
                    var port1 = GetOrCreateContainer(systemSection, "Port1");
                    port1["Type"] = "PceTurboTap";
                }
            }
        }

        static private Dictionary<string, KeyValuePair<int, int>> defaultKBHotkeys = new Dictionary<string, KeyValuePair<int, int>>()
        {
            { "ToggleFastForward", new KeyValuePair<int, int>(18, 0) },
            { "FastForward", new KeyValuePair<int, int>(55, 0) },
            { "Rewind", new KeyValuePair<int, int>(2, 0) },
            { "TakeScreenshot", new KeyValuePair<int, int>(97, 0) },
            { "Pause", new KeyValuePair<int, int>(59, 0) },
            { "MoveToNextStateSlot", new KeyValuePair<int, int>(96, 0) },
            { "MoveToPreviousStateSlot", new KeyValuePair<int, int>(95, 0) },
            { "SaveState", new KeyValuePair<int, int>(91, 0) },
            { "LoadState", new KeyValuePair<int, int>(93, 0) },
            { "Exit", new KeyValuePair<int, int>(13, 0) },
            { "ToggleFullscreen", new KeyValuePair<int, int>(49, 0) }
        };
        static private Dictionary<string, InputKey> defaultJoyHotkeys = new Dictionary<string, InputKey>()
        {
            { "ToggleFastForward", InputKey.right },
            { "FastForward", InputKey.right },
            { "Rewind", InputKey.left },
            { "TakeScreenshot", InputKey.r3 },
            { "Pause", InputKey.a },
            { "MoveToNextStateSlot", InputKey.up },
            { "MoveToPreviousStateSlot", InputKey.down },
            { "SaveState", InputKey.y },
            { "LoadState", InputKey.x },
            { "Exit", InputKey.start },
            { "ToggleFullscreen", InputKey.l3 }
        };

        private void WriteHotkeys(JObject pref, int index, bool isXInput, bool joypad, Dictionary<InputKey, string> inputKeyMapping, string mesenSystem)
        {
            Dictionary<string, KeyValuePair<int, int>> targetHK = defaultKBHotkeys;
            Dictionary<string, InputKey> targetPadHK = defaultJoyHotkeys;

            bool custoHK = EmulatorLauncher.Hotkeys.GetHotKeysFromFile("mesen", mesenSystem, out Dictionary<string, HotkeyResult> customhotkeys);
            bool custojoyHK = EmulatorLauncher.Hotkeys.GetPadHKFromFile("mesen", mesenSystem, out Dictionary<string, string> customjoyhotkeys);
            if (custojoyHK && !customjoyhotkeys.ContainsKey("ToggleFastForward") && customjoyhotkeys.ContainsKey("FastForward"))
                customjoyhotkeys["ToggleFastForward"] = customjoyhotkeys["FastForward"];

            if (custojoyHK && customjoyhotkeys.Count > 0)
            {
                Dictionary<string, InputKey> customPadHK = new Dictionary<string, InputKey>();
                foreach (var cjk in customjoyhotkeys)
                {
                    if (Enum.TryParse<InputKey>(cjk.Value, out InputKey ik))
                    {
                        customPadHK.Add(cjk.Key, ik);
                    }
                }
                targetPadHK = customPadHK;
            }

            if (custoHK && customhotkeys.Count > 0)
            {
                Dictionary<string, KeyValuePair<int, int>> customTargetHK = new Dictionary<string, KeyValuePair<int, int>>();
                foreach (var chk in customhotkeys)
                {
                    int value = chk.Value.EmulatorValue.ToInteger();
                    customTargetHK.Add(chk.Value.EmulatorKey, new KeyValuePair<int, int>(value, 0));
                }
                targetHK = customTargetHK;
            }

            pref.Remove("ShortcutKeys");
            JArray shortcuts = new JArray();
            bool toggleff = SystemConfig.getOptBoolean("fastforward_toggle");

            foreach (var hk in targetHK)
            {
                JObject shortcut = new JObject();
                string sname = hk.Key;
                shortcut["Shortcut"] = sname;
                var kbKeys = GetOrCreateContainer(shortcut, "KeyCombination");
                kbKeys["Key1"] = hk.Value.Key;
                kbKeys["Key2"] = hk.Value.Value;
                kbKeys["Key3"] = 0;

                if (joypad && targetPadHK.ContainsKey(sname))
                {
                    if (toggleff && (sname == "FastForward"))
                    {
                        shortcuts.Add(shortcut);
                        continue;
                    }
                    else if (!toggleff && (sname == "ToggleFastForward"))
                    {
                        shortcuts.Add(shortcut);
                        continue;
                    }
                    else
                    {
                        var keysjoy = GetOrCreateContainer(shortcut, "KeyCombination2");
                        keysjoy["Key1"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select]));
                        keysjoy["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[targetPadHK[sname]])) : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[targetPadHK[sname]]));
                        keysjoy["Key3"] = 0;
                    }
                }

                shortcuts.Add(shortcut);
            }

            // Other hotkeys
            for (int i = 0; i < 10; i++)
            {
                JObject selectStateSlotShortcut = new JObject();
                selectStateSlotShortcut["Shortcut"] = i == 0 ? "SelectSaveSlot1" + i.ToString() : "SelectSaveSlot" + i.ToString();
                var selectSlotkeys = GetOrCreateContainer(selectStateSlotShortcut, "KeyCombination");
                selectSlotkeys["Key1"] = 34 + i;
                selectSlotkeys["Key2"] = 0;
                selectSlotkeys["Key3"] = 0;
                shortcuts.Add(selectStateSlotShortcut);
            }

            for (int i = 1; i <= 10; i++)
            {
                JObject saveStateSlotShortcut = new JObject();
                saveStateSlotShortcut["Shortcut"] = "SaveStateSlot" + i.ToString();
                var saveSlotkeys = GetOrCreateContainer(saveStateSlotShortcut, "KeyCombination");
                saveSlotkeys["Key1"] = 89 + i;
                saveSlotkeys["Key2"] = 116;
                saveSlotkeys["Key3"] = 0;
                shortcuts.Add(saveStateSlotShortcut);
            }

            for (int i = 1; i <= 10; i++)
            {
                JObject stateSlotShortcut = new JObject();
                stateSlotShortcut["Shortcut"] = "LoadStateSlot" + i.ToString();
                var slotkeys = GetOrCreateContainer(stateSlotShortcut, "KeyCombination");
                slotkeys["Key1"] = 89 + i;
                slotkeys["Key2"] = 118;
                slotkeys["Key3"] = 0;
                shortcuts.Add(stateSlotShortcut);
            }

            JObject stateSlotautoShortcut = new JObject();
            stateSlotautoShortcut["Shortcut"] = "LoadStateSlotAuto";
            var slotautokeys = GetOrCreateContainer(stateSlotautoShortcut, "KeyCombination");
            slotautokeys["Key1"] = 100;
            slotautokeys["Key2"] = 118;
            slotautokeys["Key3"] = 0;
            shortcuts.Add(stateSlotautoShortcut);

            pref["ShortcutKeys"] = shortcuts;
        }

        static readonly List<string> xbuttonNames = new List<string>() { "Up", "Down", "Left", "Right", "Start", "Select", "L3", "R3", "L1", "R1", "?", "?", "South", "East", "West", "North", "L2", "R2", "RT Up", "RT Down", "RT Left", "RT Right", "LT Up", "LT Down", "LT Left", "LT Right" };
        static readonly List<string> dibuttonNames = new List<string>() { "LT Up", "LT Down", "LT Left", "LT Right", "RT Up", "RT Down", "RT Left", "RT Right", "Z+", "Z-", "Z2+", "Z2-", "Up", "Down", "Right", "Left", "West", "South", "East", "North", "L1", "R1", "L2", "R2", "Select", "Start", "L3", "R3", "Guide" };
        static readonly List<string> mesenButtons = new List<string>() { "A", "B", "X", "Y", "L", "R", "Up", "Down", "Left", "Right", "Start", "Select", "U", "D", "TurboA", "TurboB", "TurboX", "TurboY", "TurboL", "TurboR", "TurboSelect", "TurboStart", "GenericKey1" };

        static readonly List<string> nesPorts = new List<string>() { "Port1", "Port2", "ExpPort", "Port1A", "Port1B", "Port1C", "Port1D", "ExpPortA", "ExpPortB", "ExpPortC", "ExpPortD", "MapperInput" };
        static readonly List<string> snesPorts = new List<string>() { "Port1", "Port2", "Port1A", "Port1B", "Port1C", "Port1D", "Port2A", "Port2B", "Port2C", "Port2D" };
        static readonly List<string> gbPorts = new List<string>() { "Controller" };
        static readonly List<string> gbaPorts = new List<string>() { "Controller" };
        static readonly List<string> cvPorts = new List<string>() { "Port1", "Port2" };
        static readonly List<string> pcePorts = new List<string>() { "Port1", "Port1A", "Port1B", "Port1C", "Port1D", "Port1E" };
        static readonly List<string> smsPorts = new List<string>() { "Port1", "Port2" };

        static readonly Dictionary<InputKey, string> inputKeyMappingDefault = new Dictionary<InputKey, string>()
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
            { "Cv", 2 },
            { "Nes", 8 },
            { "Snes", 8 },
            { "Gameboy", 1 },
            { "Gba", 1 },
            { "PcEngine", 5 },
            { "Sms", 2 }
        };

        static readonly Dictionary<string, string> systemDefaultController = new Dictionary<string, string>()
        {
            { "Nes", "NesController" },
            { "Snes", "SnesController" },
            { "Gameboy", "GameboyController" },
            { "Gba", "GbaController" },
            { "PcEngine", "PceController" },
            { "Sms", "SmsController" },
            { "Cv", "ColecoVisionController" }
        };

        private static string SdlToKeyCode(long sdlCode)
        {

            //The following list of keys has been verified
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
                case 0x40000047: return "115";        // Scrolllock
                case 0x40000048: return "7";        // Pause
                case 0x40000049: return "31";       // Insert
                case 0x4000004A: return "22";       // Home
                case 0x4000004B: return "19";       // PageUp
                case 0x4000004D: return "21";       // End
                case 0x4000004E: return "20";       // PageDown
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
