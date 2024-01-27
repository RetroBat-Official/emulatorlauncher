using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Rpcs3Generator
    {
        /// <summary>
        /// Setup config.yml file for guns
        /// </summary>
        /// <param name="path"></param>
        private void SetupGuns(YmlFile yml, YmlContainer vulkan)
        {
            if (!Program.SystemConfig.getOptBoolean("rpcs3_guns"))
                return;

            // set borderless window mode for guns
            vulkan["Exclusive Fullscreen Mode"] = "Disable";

            //
            var io = yml.GetOrCreateContainer("Input/Output");
            io["Keyboard"] = "\"Null\"";
            io["Mouse"] = "\"Null\"";
            io["Camera"] = "Fake";
            io["Camera type"] = "PS Eye";
            io["Move"] = "Mouse";

            BindBoolFeature(io, "Show move cursor", "rpcs3_mouse_cursor", "true", "false");
        }
    }
}
