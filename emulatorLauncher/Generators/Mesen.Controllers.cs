using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class MesenGenerator : Generator
    {
        private void SetupControllers(DynamicJson pref, DynamicJson systemSection, string mesenSystem)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (this.Controllers.Count == 1 && this.Controllers[0].IsKeyboard)
                return;

            // clear existing mapping sections of json file
            var portList = nesPorts;
            if (mesenSystem == "Snes")
                portList = snesPorts;
            else if (mesenSystem == "GameBoy")
                portList = gbPorts;
            else if (mesenSystem == "PcEngine")
                portList = pcePorts;

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
                return;
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

            if (mesenSystem == "Nes" || mesenSystem == "Gameboy")
            {
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
                if (controllerType == "SnesMouse")
                {
                    List<int> mouseID = new List<int>();
                    mouseID.Add(512);
                    mouseID.Add(513);
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

            if (playerIndex == 1)
                WriteHotkeys(pref, index, isXInput);
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

            return null;
        }

        private void ConfigureMultitap(DynamicJson systemSection, string mesenSystem)
        {
            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_nes_multitap") &&  SystemConfig["mesen_nes_multitap"] == "dual")
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
            shotkeys["Key2"] = isXInput ? (4096 + index * 256 + 1 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.r2])).ToString() : (8192 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.r2])).ToString();
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

        static List<string> xbuttonNames = new List<string>() { "Up", "Down", "Left", "Right", "Start", "Select", "L3", "R3", "L1", "R1", "?", "?", "South", "East", "West", "North", "L2", "R2", "RT Up", "RT Down", "RT Left", "RT Right", "LT Up", "LT Down", "LT Left", "LT Right" };
        static List<string> dibuttonNames = new List<string>() { "LT Up", "LT Down", "LT Left", "LT Right", "RT Up", "RT Down", "RT Left", "RT Right", "Z+", "Z-", "Z2+", "Z2-", "Up", "Down", "Right", "Left", "West", "South", "East", "North", "L1", "R1", "L2", "R2", "Select", "Start", "L3", "R3", "Guide" };
        static List<string> mesenButtons = new List<string>() { "A", "B", "X", "Y", "L", "R", "Up", "Down", "Left", "Right", "Start", "Select", "TurboA", "TurboB", "TurboX", "TurboY", "TurboL", "TurboR", "TurboSelect", "TurboStart" };

        static List<string> nesPorts = new List<string>() { "Port1", "Port2", "ExpPort", "Port1A", "Port1B", "Port1C", "Port1D", "ExpPortA", "ExpPortB", "ExpPortC", "ExpPortD", "MapperInput" };
        static List<string> snesPorts = new List<string>() { "Port1", "Port2", "Port1A", "Port1B", "Port1C", "Port1D", "Port2A", "Port2B", "Port2C", "Port2D" };
        static List<string> gbPorts = new List<string>() { "Controller" };
        static List<string> pcePorts = new List<string>() { "Port1", "Port1A", "Port1B", "Port1C", "Port1D", "Port1E" };

        static Dictionary<InputKey, string> inputKeyMapping = new Dictionary<InputKey, string>()
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

        static Dictionary<string, int> systemMaxPad = new Dictionary<string, int>()
        {
            { "Nes", 8 },
            { "Snes", 8 },
            { "Gameboy", 1 },
            { "PcEngine", 5 }
        };

        static Dictionary<string, string> systemDefaultController = new Dictionary<string, string>()
        {
            { "Nes", "NesController" },
            { "Snes", "SnesController" },
            { "Gameboy", "GameboyController" },
            { "PcEngine", "PceController" }
        };
    }
}
