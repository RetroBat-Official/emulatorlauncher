using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using SharpDX.XInput;

namespace emulatorLauncher
{
    partial class Model2Generator : Generator
    {
        private void ConfigureControllers(byte[] bytes)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (Program.Controllers.Count > 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                var c2 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                if (c1.IsKeyboard)
                    WriteKeyboardMapping(bytes, c1);
                else
                    WriteJoystickMapping(bytes, c1, c2);
            }
            else if (Program.Controllers.Count == 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                if (c1.IsKeyboard)
                    WriteKeyboardMapping(bytes, c1);
                else
                    WriteJoystickMapping(bytes, c1);
            }
            else if (Program.Controllers.Count == 0)
                return;
        }

        private void WriteJoystickMapping(byte[] bytes, Controller c1, Controller c2 = null)
        {
            if (c1 == null || c1.Config == null)
                return;

            //initialize controller index, supermodel uses directinput controller index (+1)
            //only index of player 1 is initialized as there might be only 1 controller at that point
            int j2index = -1;
            int j1index = c1.DirectInput != null ? c1.DirectInput.DeviceIndex + 1 : c1.DeviceIndex + 1;

            //If a secod controller is connected, get controller index of player 2, if there is no 2nd controller, just increment the index
            if (c2 != null && c2.Config != null)
                j2index = c2.DirectInput != null ? c2.DirectInput.DeviceIndex + 1 : c2.DeviceIndex + 1;
            else
                j2index = j1index + 1;
        }

        private void WriteKeyboardMapping(byte[] bytes, Controller c)
        {
            if (c == null)
                return;

            InputConfig keyboard = c.Config;
            if (keyboard == null)
                return;
        }
    }
}
