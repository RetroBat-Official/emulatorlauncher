using EmulatorLauncher.Common.FileFormats;

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
            if (!Program.SystemConfig.isOptSet("rpcs3_guns") || Program.SystemConfig["rpcs3_guns"] == "none")
                return;

            // set borderless window mode for guns
            vulkan["Exclusive Fullscreen Mode"] = "Disable";
            var io = yml.GetOrCreateContainer("Input/Output");

            if (Program.SystemConfig["rpcs3_guns"] == "pseye")
            {
                io["Keyboard"] = "\"Null\"";
                io["Mouse"] = "\"Null\"";
                io["Camera"] = "Fake";
                io["Camera type"] = "PS Eye";
                io["Move"] = "Mouse";

                BindBoolFeature(io, "Show move cursor", "rpcs3_mouse_cursor", "true", "false");
            }
            if (Program.SystemConfig["rpcs3_guns"] == "1")
                io["GunCon3 emulated controller"] = "1 controller";
            else if (Program.SystemConfig["rpcs3_guns"] == "2")
                io["GunCon3 emulated controller"] = "2 controllers";
        }
    }
}
