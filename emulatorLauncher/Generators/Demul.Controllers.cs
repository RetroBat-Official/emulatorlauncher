using System;
using System.Collections.Generic;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class DemulGenerator : Generator
    {
        private bool _isArcade;
        private static readonly List<string> nonArcadeSystems = new List<string> { "dreamcast" };

        private void SetupControllers(string path, IniFile ini, string system)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            _isArcade = !nonArcadeSystems.Contains(system);

            if (!this.Controllers.Any(c => !c.IsKeyboard))
                return;

            string ctrlrIniFile = Path.Combine(path, "padDemul.ini");

            using (var ctrlIni = IniFile.FromFile(ctrlrIniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                try
                {
                    CleanupExistingMappings(ctrlIni, system);

                    foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                        ConfigureInput(controller, ctrlIni, ini, system);
                }
                catch { }
            }
        }

        private void ConfigureInput(Controller controller, IniFile ini, IniFile ctrlIni, string system)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(controller, ctrlIni, ini, system);
        }

        private void ConfigureJoystick(Controller controller, IniFile ini, IniFile ctrlIni, string system)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            // Initializing controller information
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid = (controller.Guid.ToString()).Substring(0, 27) + "00000";
            SdlToDirectInput sdlCtrl = null;
            int index = controller.DirectInput != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex;

            // Index is directinput input but 2 lists depending on Xinput or DirectInput device
            var dinputControllers = this.Controllers.Where(c => !c.IsXInputDevice).ToList().OrderBy(d => d.DirectInput.DeviceIndex);
            var xinputControllers = this.Controllers.Where(c => c.IsXInputDevice).ToList().OrderBy(d => d.DirectInput.DeviceIndex);

            index = controller.IsXInputDevice ? xinputControllers.OrderBy(d => d.DirectInput.DeviceIndex).ToList().IndexOf(controller) : dinputControllers.OrderBy(d => d.DirectInput.DeviceIndex).ToList().IndexOf(controller);

            bool isXInput = controller.IsXInputDevice;

            // Get SDL controller mapping in gamecontrollerDB file
            if (!isXInput)
            {
                if (!File.Exists(gamecontrollerDB))
                {
                    SimpleLogger.Instance.Info("[CONTROLLERS] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                    gamecontrollerDB = null;
                    return;
                }
                else
                    SimpleLogger.Instance.Info("[CONTROLLERS] Player " + controller.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

                sdlCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

                if (sdlCtrl == null)
                {
                    SimpleLogger.Instance.Info("[CONTROLLERS] Player " + controller.PlayerIndex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid);
                    return;
                }
                else
                    SimpleLogger.Instance.Info("[CONTROLLERS] Player " + controller.PlayerIndex + ": " + guid + " found in gamecontrollerDB file.");

                if (sdlCtrl.ButtonMappings == null)
                {
                    SimpleLogger.Instance.Info("[CONTROLLERS] No mapping found for the controller." + guid);
                    return;
                }
            }

            // Write in demul.ini file
            ini.WriteValue("MAIN", "DEVICE_API", "1");

            string vmsPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "demul", "memsaves");
            if (!Directory.Exists(vmsPath))
                try { Directory.CreateDirectory(vmsPath); }
                catch { }

            string vmsFile = Path.Combine(vmsPath, "vms" + (controller.PlayerIndex - 1).ToString() + "0.bin");

            if (controller.PlayerIndex < 5)
            {
                ini.WriteValue("VMS", vmuPort[controller.PlayerIndex], vmsFile);
                ini.WriteValue(maplePorts[controller.PlayerIndex], "device", "16777216");
                ini.WriteValue(maplePorts[controller.PlayerIndex], "port0", "234881024");
                
                if (SystemConfig.isOptSet("demul_extension" + controller.PlayerIndex) && !string.IsNullOrEmpty(SystemConfig["demul_extension" + controller.PlayerIndex]))
                {
                    string extension = SystemConfig["demul_extension" + controller.PlayerIndex];
                    if (extension != null)
                        ini.WriteValue("VMS", maplePorts[controller.PlayerIndex] + "port1", extension);
                }

                ini.WriteValue(maplePorts[controller.PlayerIndex], "port2", "-1");
                ini.WriteValue(maplePorts[controller.PlayerIndex], "port3", "-1");
                ini.WriteValue(maplePorts[controller.PlayerIndex], "port4", "-1");
            }

            if (SystemConfig.isOptSet("demuldeadzone") && !string.IsNullOrEmpty(SystemConfig["demuldeadzone"]))
            {
                string deadzone = SystemConfig["demuldeadzone"];
                if (deadzone != null)
                    ctrlIni.WriteValue("GLOBAL0", "DEADZONE", deadzone);
            }

            // Write controller mapping in padDemul.ini file
            if (_isArcade)
            {
                string iniSection = "JAMMA0_" + (controller.PlayerIndex - 1).ToString();

                ctrlIni.WriteValue(iniSection, "PUSH1", isXInput ? GetXInputCode(controller, InputKey.a, index) : GetDInputCode(controller, InputKey.a, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "PUSH2", isXInput ? GetXInputCode(controller, InputKey.b, index) : GetDInputCode(controller, InputKey.b, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "PUSH3", isXInput ? GetXInputCode(controller, InputKey.y, index) : GetDInputCode(controller, InputKey.y, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "PUSH4", isXInput ? GetXInputCode(controller, InputKey.x, index) : GetDInputCode(controller, InputKey.x, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "PUSH5", isXInput ? GetXInputCode(controller, InputKey.pageup, index) : GetDInputCode(controller, InputKey.pageup, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "PUSH6", isXInput ? GetXInputCode(controller, InputKey.pagedown, index) : GetDInputCode(controller, InputKey.pagedown, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "PUSH7", isXInput ? GetXInputCode(controller, InputKey.l2, index) : GetDInputCode(controller, InputKey.l2, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "PUSH8", isXInput ? GetXInputCode(controller, InputKey.r2, index) : GetDInputCode(controller, InputKey.r2, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "SERVICE", isXInput ? GetXInputCode(controller, InputKey.l3, index) : GetDInputCode(controller, InputKey.l3, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "START", isXInput ? GetXInputCode(controller, InputKey.start, index) : GetDInputCode(controller, InputKey.start, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "COIN", isXInput ? GetXInputCode(controller, InputKey.select, index) : GetDInputCode(controller, InputKey.select, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "DIGITALUP", isXInput ? GetXInputCode(controller, InputKey.up, index) : GetDInputCode(controller, InputKey.up, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "DIGITALDOWN", isXInput ? GetXInputCode(controller, InputKey.down, index) : GetDInputCode(controller, InputKey.down, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "DIGITALLEFT", isXInput ? GetXInputCode(controller, InputKey.left, index) : GetDInputCode(controller, InputKey.left, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "DIGITALRIGHT", isXInput ? GetXInputCode(controller, InputKey.right, index) : GetDInputCode(controller, InputKey.right, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "ANALOGUP", isXInput ? GetXInputCode(controller, InputKey.joystick1up, index) : GetDInputCode(controller, InputKey.joystick1up, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGDOWN", isXInput ? GetXInputCode(controller, InputKey.joystick1down, index) : GetDInputCode(controller, InputKey.joystick1down, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGLEFT", isXInput ? GetXInputCode(controller, InputKey.joystick1left, index) : GetDInputCode(controller, InputKey.joystick1left, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGRIGHT", isXInput ? GetXInputCode(controller, InputKey.joystick1right, index) : GetDInputCode(controller, InputKey.joystick1right, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGUP2", isXInput ? GetXInputCode(controller, InputKey.joystick2up, index) : GetDInputCode(controller, InputKey.joystick2up, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGDOWN2", isXInput ? GetXInputCode(controller, InputKey.joystick2down, index) : GetDInputCode(controller, InputKey.joystick2down, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGLEFT2", isXInput ? GetXInputCode(controller, InputKey.joystick2left, index) : GetDInputCode(controller, InputKey.joystick2left, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "ANALOGRIGHT2", isXInput ? GetXInputCode(controller, InputKey.joystick2right, index) : GetDInputCode(controller, InputKey.joystick2right, sdlCtrl, index));

                ctrlIni.WriteValue("GLOBAL0", "TEST", isXInput ? GetXInputCode(controller, InputKey.r3, index) : GetDInputCode(controller, InputKey.r3, sdlCtrl, index));
                ctrlIni.WriteValue("GLOBAL0", "SERVICE", isXInput ? GetXInputCode(controller, InputKey.l3, index) : GetDInputCode(controller, InputKey.l3, sdlCtrl, index));
            }

            else
            {
                string iniSection = "JOY0_" + (controller.PlayerIndex - 1).ToString();

                ctrlIni.WriteValue(iniSection, "UP", isXInput ? GetXInputCode(controller, InputKey.up, index) : GetDInputCode(controller, InputKey.up, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "DOWN", isXInput ? GetXInputCode(controller, InputKey.down, index) : GetDInputCode(controller, InputKey.down, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "LEFT", isXInput ? GetXInputCode(controller, InputKey.left, index) : GetDInputCode(controller, InputKey.left, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "RIGHT", isXInput ? GetXInputCode(controller, InputKey.right, index) : GetDInputCode(controller, InputKey.right, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "A", isXInput ? GetXInputCode(controller, InputKey.a, index) : GetDInputCode(controller, InputKey.a, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "B", isXInput ? GetXInputCode(controller, InputKey.b, index) : GetDInputCode(controller, InputKey.b, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "X", isXInput ? GetXInputCode(controller, InputKey.y, index) : GetDInputCode(controller, InputKey.y, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "Y", isXInput ? GetXInputCode(controller, InputKey.x, index) : GetDInputCode(controller, InputKey.x, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "LTRIG", isXInput ? GetXInputCode(controller, InputKey.l2, index) : GetDInputCode(controller, InputKey.l2, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "RTRIG", isXInput ? GetXInputCode(controller, InputKey.r2, index) : GetDInputCode(controller, InputKey.r2, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "START", isXInput ? GetXInputCode(controller, InputKey.start, index) : GetDInputCode(controller, InputKey.start, sdlCtrl, index));

                ctrlIni.WriteValue(iniSection, "S1UP", isXInput ? GetXInputCode(controller, InputKey.joystick1up, index) : GetDInputCode(controller, InputKey.joystick1up, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S1DOWN", isXInput ? GetXInputCode(controller, InputKey.joystick1down, index) : GetDInputCode(controller, InputKey.joystick1down, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S1LEFT", isXInput ? GetXInputCode(controller, InputKey.joystick1left, index) : GetDInputCode(controller, InputKey.joystick1left, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S1RIGHT", isXInput ? GetXInputCode(controller, InputKey.joystick1right, index) : GetDInputCode(controller, InputKey.joystick1right, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S2UP", isXInput ? GetXInputCode(controller, InputKey.joystick2up, index) : GetDInputCode(controller, InputKey.joystick2up, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S2DOWN", isXInput ? GetXInputCode(controller, InputKey.joystick2down, index) : GetDInputCode(controller, InputKey.joystick2down, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S2LEFT", isXInput ? GetXInputCode(controller, InputKey.joystick2left, index) : GetDInputCode(controller, InputKey.joystick2left, sdlCtrl, index));
                ctrlIni.WriteValue(iniSection, "S2RIGHT", isXInput ? GetXInputCode(controller, InputKey.joystick2right, index) : GetDInputCode(controller, InputKey.joystick2right, sdlCtrl, index));
            }
        }

        private static string GetDInputCode(Controller c, InputKey key, SdlToDirectInput ctrl, int index, bool trigger = false)
        {
            long hatStart = 67108864;
            long buttonStart = 16777216;
            long axisStart = 33554688;
            long revertAxisStart = axisStart - 256;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            string esName = (c.Config[key].Name).ToString();

            if (esName == null || !esToDinput.ContainsKey(esName))
                return "0";

            string dinputName = esToDinput[esName];
            if (dinputName == null)
                return "0";

            if (!ctrl.ButtonMappings.ContainsKey(dinputName))
                return "0";

            string button = ctrl.ButtonMappings[dinputName];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1: return (hatStart + (index * 65536)).ToString();
                    case 2: return ((hatStart + 256) + (index * 65536)).ToString();
                    case 4: return ((hatStart + (256 * 2)) + (index * 65536)).ToString();
                    case 8: return ((hatStart + (256 * 3)) + (index * 65536)).ToString();
                };
            }

            else if (button.StartsWith("b"))
            {
                int buttonID = button.Substring(1).ToInteger();
                return ((buttonStart + buttonID) + (index * 65536)).ToString();
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                if (revertAxis || trigger) return ((revertAxisStart + axisID) + (index * 65536)).ToString();
                else return ((axisStart + axisID) + (index * 65536)).ToString();

            }

            return "0";
        }

        private static string GetXInputCode(Controller c, InputKey key, int index)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return (805306380 + (index * 65536)).ToString();
                        case 1: return (805306381 + (index * 65536)).ToString();
                        case 2: return (805306382 + (index * 65536)).ToString();
                        case 3: return (805306383 + (index * 65536)).ToString();
                        case 4: return (805306376 + (index * 65536)).ToString();
                        case 5: return (805306377 + (index * 65536)).ToString();
                        case 6: return (805306373 + (index * 65536)).ToString();
                        case 7: return (805306372 + (index * 65536)).ToString();
                        case 8: return (805306374 + (index * 65536)).ToString();
                        case 9: return (805306375 + (index * 65536)).ToString();
                        case 10: return "0";
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return (-1879047936 + (index * 65536)).ToString();
                            else return (-1879048192 + (index * 65536)).ToString();
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return (-1879047424 + (index * 65536)).ToString();
                            else return (-1879047680 + (index * 65536)).ToString();
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return (-1879046912 + (index * 65536)).ToString();
                            else return (-1879047168 + (index * 65536)).ToString();
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return (-1879046400 + (index * 65536)).ToString();
                            else return (-1879046656 + (index * 65536)).ToString();
                        case 4: return (1342177280 + (index * 65536)).ToString();
                        case 5: return (1342177536 + (index * 65536)).ToString();
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return (805306368 + (index * 65536)).ToString();
                        case 2: return (805306371 + (index * 65536)).ToString();
                        case 4: return (805306369 + (index * 65536)).ToString();
                        case 8: return (805306370 + (index * 65536)).ToString();
                    }
                }
            }
            return "0";
        }

        private void CleanupExistingMappings(IniFile ini, string system)
        {
            for (int i = 0; i < 2; i++)
            {
                string joySection = "JOY" + i + "_";
                string jammaSection = "JAMMA" + i + "_";
                string globalSection = "GLOBAL" + i;

                for (int j = 0; j < 4; j++)
                {
                    string joySubSection = joySection + j;

                    foreach (var button in dreamcastButtons)
                        ini.WriteValue(joySubSection, button, "0");

                    string jammaSubSection = jammaSection + j;

                    foreach (var button in jammaButtons)
                        ini.WriteValue(jammaSubSection, button, "0");
                }

                foreach (var button in globalButtons)
                    ini.WriteValue(globalSection, button, "0");
            }

            ini.WriteValue("MAIN", "DEVICE_API", "0");
        }


        private static readonly List<string> dreamcastButtons = new List<string>() { 
            "UP", "DOWN", "LEFT", "RIGHT", "UP2", "DOWN2", "LEFT2", "RIGHT2", "A", "B", "C", "D", "X", "Y", "Z", "LTRIG", "RTRIG", "START", "S1UP", "S1DOWN", "S1LEFT", "S1RIGHT", "S2UP", "S2DOWN", "S2LEFT", "S2RIGHT" };

        private static readonly List<string> jammaButtons = new List<string>() {
            "PUSH1", "PUSH2", "PUSH3", "PUSH4 ", "PUSH5", "PUSH6", "PUSH7", "PUSH8", "SERVICE", "START", "COIN", "DIGITALUP", "DIGITALDOWN", "DIGITALLEFT", "DIGITALRIGHT", "ANALOGUP", "ANALOGDOWN", "ANALOGLEFT", "ANALOGRIGHT", "ANALOGUP2", "ANALOGDOWN2", "ANALOGLEFT2", "ANALOGRIGHT2" };

        private static readonly List<string> globalButtons = new List<string>() {
            "TEST", "TEST2", "SERVICE", "SAVESTATE", "LOADSTATE", "NEXTSTATE", "PREVSTATE", "DEADZONE" };

        private static Dictionary<string, string> esToDinput = new Dictionary<string, string>()
        {
            { "a", "a" },
            { "b", "b" },
            { "x", "y" },
            { "y", "x" },
            { "select", "back" },
            { "start", "start" },
            { "joystick1left", "leftx" },
            { "leftanalogleft", "leftx" },
            { "joystick1up", "lefty" },
            { "leftanalogup", "lefty" },
            { "joystick2left", "rightx" },
            { "rightanalogleft", "rightx" },
            { "joystick2up", "righty" },
            { "rightanalogup", "righty" },
            { "up", "dpup" },
            { "down", "dpdown" },
            { "left", "dpleft" },
            { "right", "dpright" },
            { "l2", "lefttrigger" },
            { "l3", "leftstick" },
            { "pagedown", "rightshoulder" },
            { "pageup", "leftshoulder" },
            { "r2", "righttrigger" },
            { "r3", "rightstick" },
            { "leftthumb", "lefttrigger" },
            { "rightthumb", "righttrigger" },
            { "l1", "leftshoulder" },
            { "r1", "rightshoulder" },
            { "lefttrigger", "leftstick" },
            { "righttrigger", "rightstick" },
        };

        private static Dictionary<int, string> maplePorts = new Dictionary<int, string>()
        {
            { 1, "PORTA" },
            { 2, "PORTB" },
            { 3, "PORTC" },
            { 4, "PORTD" },
        };

        private static Dictionary<int, string> vmuPort = new Dictionary<int, string>()
        {
            { 1, "VMSA0" },
            { 2, "VMSB0" },
            { 3, "VMSC0" },
            { 4, "VMSD0" },
        };
    }
}
