using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Rpcs3Generator
    {
        private bool _sindenSoft = false;
        
        // Setup config.yml file for guns
        
        private void SetupGuns(string path, YmlFile yml, YmlContainer vulkan)
        {
            if (!Program.SystemConfig.isOptSet("rpcs3_guns") || Program.SystemConfig["rpcs3_guns"] == "none")
                return;

            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }

            SimpleLogger.Instance.Info("[GENERATOR] Setting up guns.");

            // set borderless window mode for guns
            vulkan["Exclusive Fullscreen Mode"] = "Disable";

            var io = yml.GetOrCreateContainer("Input/Output");
            BindBoolFeature(io, "Show move cursor", "rpcs3_mouse_cursor", "true", "false");

            if (Program.SystemConfig["rpcs3_guns"] == "pseye")
            {
                io["Camera"] = "Fake";
                io["Camera type"] = "PS Eye";
                io["Move"] = "Mouse";
            }

            else
            {
                io["Mouse"] = "Raw";
                string rawMouseConf = Path.Combine(path, "config", "raw_mouse.yml");
                ConfigureRawMouse(rawMouseConf, guns);
                string guncon3conf = Path.Combine(path, "config", "guncon3.yml");
                ConfigureGuncon3(guncon3conf, guns);
            }
        }

        private void ConfigureRawMouse(string conf, RawLightgun[] guns)
        {
            YmlFile yml = YmlFile.Load(conf);
            int i = 3;

            if (SystemConfig.getOptBoolean("rpcs3_guns_start1"))
                i = 1;

            for (int p = 1; p <= 4; p++)
            {
                var px = yml.GetOrCreateContainer("Player " + p);
                px["Device"] = "\"\"";
                px["Mouse Acceleration"] = "100";
                px["Button 1"] = "Button 1";
                px["Button 2"] = "Button 2";
                px["Button 3"] = "Button 3";
                px["Button 4"] = "Button 4";
                px["Button 5"] = "Button 5";
                px["Button 6"] = "\"\"";
                px["Button 7"] = "\"\"";
                px["Button 8"] = "\"\"";
            }

            foreach (var gun in guns.Take(2))
            {
                var px = yml.GetOrCreateContainer("Player " + i);

                px["Device"] = "\"" + gun.DevicePath.Replace("\\", "\\\\") + "\"";
                i++;
            }

            yml.Save();
        }

        private void ConfigureGuncon3(string conf, RawLightgun[] guns)
        {
            YmlFile yml = YmlFile.Load(conf);
            int i = 3;

            if (SystemConfig.getOptBoolean("rpcs3_guns_start1"))
                i = 1;

            foreach (var gun in guns.Take(2))
            {
                var px = yml.GetOrCreateContainer("Player " + i);

                px["Trigger"] = "Mouse Button 1";
                i++;
            }

            yml.Save();
        }
    }
}
