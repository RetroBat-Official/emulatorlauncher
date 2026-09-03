using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace EmulatorLauncher
{
    partial class UaeGenerator : Generator
    {
        /// <summary>
        /// Use NAME (joyportfriendlyname), not index —
        /// WinUAE gets name with inputdevice_joyport_config()
        /// Verified in the source code of tonioni/WinUAE :
        /// joyport0/1, joyport0mode/1mode, joyportfriendlyname0/1 are the actual keys).
        /// </summary>
        private void ConfigureControls(StringBuilder sb, string system)
        {
            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig.getOptBoolean("disableautocontrollers"))
                return;

            var pads = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.PlayerIndex).ToList();

            // Port 0 = mouse (port 1 on the machine), port 1 = joystick (port 2) : Amiga convention
            bool swapPorts = SystemConfig.getOptBoolean("winuae_swapports");
            int joystickPort = swapPorts ? 0 : 1;
            int mousePort = swapPorts ? 1 : 0;

            string mode = system == "amigacd32" ? "cd32joy" : "djoy";
            if (SystemConfig.isOptSet("winuae_padmode") && !string.IsNullOrEmpty(SystemConfig["winuae_padmode"]))
                mode = SystemConfig["winuae_padmode"];

            var pad1 = pads.FirstOrDefault();
            WriteJoyport(sb, joystickPort, pad1, mode);

            var pad2 = pads.Skip(1).FirstOrDefault();
            bool secondPortIsJoystick = pad2 != null && SystemConfig.isOptSet("winuae_port1") && SystemConfig["winuae_port1"] == "joystick";

            if (secondPortIsJoystick)
            {
                WriteJoyport(sb, mousePort, pad2, mode);
            }
            else
            {
                sb.AppendLine("joyport" + mousePort + "=mousedefault");
                sb.AppendLine("joyport" + mousePort + "mode=mouse");
            }
        }

        private static void WriteJoyport(StringBuilder sb, int port, Controller controller, string mode)
        {
            if (controller == null)
            {
                // Backup keyboard: arrows + right Ctrl fires
                sb.AppendLine("joyport" + port + "=kbd2");
                sb.AppendLine("joyport" + port + "mode=" + mode);
                return;
            }

            string name = controller.IsXInputDevice
                ? "XInput Controller"
                : (controller.Sdl3Controller != null ? controller.Sdl3Controller.Name : controller.Name);

            if (!string.IsNullOrEmpty(name))
                sb.AppendLine("joyportfriendlyname" + port + "=" + name);
            else
                sb.AppendLine("joyport" + port + "=joydefault");

            sb.AppendLine("joyport" + port + "mode=" + mode);
        }
    }
}