using EmulatorLauncher.Common;
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

            SimpleLogger.Instance.Info("[GENERATOR] Setting up guns.");

            // set borderless window mode for guns
            vulkan["Exclusive Fullscreen Mode"] = "Disable";
            var io = yml.GetOrCreateContainer("Input/Output");

            if (Program.SystemConfig["rpcs3_guns"] == "pseye")
            {
                io["Camera"] = "Fake";
                io["Camera type"] = "PS Eye";
                io["Move"] = "Mouse";

                BindBoolFeature(io, "Show move cursor", "rpcs3_mouse_cursor", "true", "false");
            }
        }
    }
}
