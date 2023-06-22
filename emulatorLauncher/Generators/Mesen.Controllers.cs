using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using emulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using System.Threading;
using System.Xml.Linq;
using System.Drawing;
using System.Collections;
using emulatorLauncher.Tools;
using System.Runtime.InteropServices.ComTypes;
using SharpDX.XInput;

namespace emulatorLauncher
{
    partial class MesenGenerator : Generator
    {
        private void SetupControllers(XElement xdoc)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (this.Controllers.Count == 1 && this.Controllers[0].IsKeyboard)
                return;

            // clear existing mapping sections of xml file
            var inputInfo = xdoc.GetOrCreateElement("InputInfo");
            var inputDevices = inputInfo.Descendants("InputDevice").ToList();
            for (int i = 0; i < inputDevices.Count() ; i++)
            {
                var inputDevice = inputDevices[i];
                var keys = inputDevice.GetOrCreateElement("Keys");
                var keyMappings = keys.Descendants("KeyMappings").ToList();
                for (int j = 0; j < keyMappings.Count(); j++)
                {
                    {
                        var keymapping = keyMappings[j];
                        keymapping.Remove();
                    }
                }
            }

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                ConfigureInput(inputDevices, controller);
        }

        private void ConfigureInput(List<XElement> inputDevices, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(inputDevices, controller);
        }

            private void ConfigureJoystick(List<XElement> inputDevices, Controller ctrl)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            bool revertButtons = SystemConfig.isOptSet("mesen_revertbuttons") && SystemConfig.getOptBoolean("mesen_revertbuttons");
            bool isXInput = ctrl.IsXInputDevice;
            int index = isXInput ? ctrl.XInput.DeviceIndex: ctrl.DirectInput.DeviceIndex;
            int playerIndex = ctrl.PlayerIndex - 1;

            var keys = inputDevices[playerIndex].GetOrCreateElement("Keys");
            var keyMapping = keys.GetOrCreateElement("KeyMappings");

            BindFeature(inputDevices[playerIndex], "ControllerType", "mesen_controller" + playerIndex, "StandardController");

            if (revertButtons)
            {
                keyMapping.SetElementValue("A", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString());
                keyMapping.SetElementValue("B", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString());
            }
            else
            {
                keyMapping.SetElementValue("A", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString());
                keyMapping.SetElementValue("B", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.a])).ToString());
            }
            
            keyMapping.SetElementValue("Select", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.select])).ToString());
            keyMapping.SetElementValue("Start", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.start])).ToString());
            keyMapping.SetElementValue("Up", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.up])).ToString());
            keyMapping.SetElementValue("Down", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.down])).ToString());
            keyMapping.SetElementValue("Left", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.left])).ToString());
            keyMapping.SetElementValue("Right", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.right])).ToString());

            if (revertButtons)
            {
                keyMapping.SetElementValue("TurboA", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.b])).ToString());
                keyMapping.SetElementValue("TurboB", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString());
            }
            else
            {
                keyMapping.SetElementValue("TurboA", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.x])).ToString());
                keyMapping.SetElementValue("TurboB", isXInput ? (65536 + index * 256 + xbuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString() : (69632 + index * 256 + dibuttonNames.IndexOf(inputKeyMapping[InputKey.y])).ToString());
            }
        }

        static List<string> xbuttonNames = new List<string>() { "Up", "Down", "Left", "Right", "Start", "Select", "L3", "R3", "L1", "R1", "?", "?", "South", "East", "West", "North", "L2", "R2", "RT Up", "RT Down", "RT Left", "RT Right", "LT Up", "LT Down", "LT Left", "LT Right" };
        static List<string> dibuttonNames = new List<string>() { "LT Up", "LT Down", "LT Left", "LT Right", "RT Up", "RT Down", "RT Left", "RT Right", "Z+", "Z-", "Z2+", "Z2-", "Up", "Down", "Right", "Left", "South", "East", "West", "North", "L1", "R1", "L2", "R2", "Select", "Start", "L3", "R3", "Guide" };

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
    }
}
