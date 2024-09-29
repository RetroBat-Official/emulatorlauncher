using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class RyujinxGenerator : Generator
    {
        public RyujinxGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private SdlVersion _sdlVersion = SdlVersion.SDL2_26;
        private string _emulatorPath;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("ryujinx");
            if (!Directory.Exists(path))
                return null;

            _emulatorPath = path;

            string exe = Path.Combine(path, "Ryujinx.exe");
            if (!File.Exists(exe))
                return null;

            string setupPath = Path.Combine(path, "portable");

            string portablePath = Path.Combine(AppConfig.GetFullPath("saves"), "switch", "ryujinx", "portable");
            if (Directory.Exists(portablePath))
                setupPath = portablePath;

            SimpleLogger.Instance.Info("[Generator] Setting '" + setupPath + "' as content path for the emulator");

            SetupConfiguration(setupPath);

            var commandArray = new List<string>();

            if (Directory.Exists(portablePath))
            {
                commandArray.Add("-r");
                commandArray.Add("\"" + portablePath + "\"");
            }
            
            commandArray.Add("\"" + rom + "\"");
            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                WindowStyle = ProcessWindowStyle.Minimized,
            };
        }

        private string GetDefaultswitchLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "jp", "Japanese" },
                { "ja", "Japanese" },
                { "en", "AmericanEnglish" },
                { "fr", "French" },
                { "de", "German" },
                { "it", "Italian" },
                { "es", "Spanish" },
                { "zh", "Chinese" },
                { "ko", "Korean" },
                { "nl", "Dutch" },
                { "pt", "Portuguese" },
                { "ru", "Russian" },
                { "tw", "Taiwanese" }
            };

            // Special case for some variances
            if (SystemConfig["Language"] == "zh_TW")
                return "Taiwanese";
            else if (SystemConfig["Language"] == "pt_BR")
                return "BrazilianPortuguese";
            else if (SystemConfig["Language"] == "en_GB")
                return "BritishEnglish";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }
            return "AmericanEnglish";
        }

        //Manage Config.json file settings
        private void SetupConfiguration(string setupPath)
        {
            if (SystemConfig.isOptSet("disableautoconfig") && SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            var json = DynamicJson.Load(Path.Combine(setupPath, "Config.json"));

            //Set fullscreen
            json["start_fullscreen"] = fullscreen ? "true" : "false";

            // Folder
            List<string> paths = new List<string>();
            string romPath = Path.Combine(AppConfig.GetFullPath("roms"), "switch");
            if (Directory.Exists(romPath))
            {
                paths.Add(romPath);
                json.SetObject("game_dirs", paths);
            }

            //General Settings
            json["check_updates_on_start"] = "false";
            json["show_confirm_exit"] = "false";
            json["show_console"] = "false";

            //Input
            BindBoolFeature(json, "docked_mode", "ryujinx_undock", "false", "true");
            json["hide_cursor"] = "2";

            // Discord
            BindBoolFeature(json, "enable_discord_integration", "discord", "true", "false");

            //System
            BindFeature(json, "system_language", "switch_language", GetDefaultswitchLanguage());
            BindBoolFeatureOn(json, "enable_vsync", "vsync", "true", "false");
            BindBoolFeatureOn(json, "enable_ptc", "enable_ptc", "true", "false");

            // internet access
            if (SystemConfig.isOptSet("ryujinx_network") && SystemConfig["ryujinx_network"] == "internet")
                json["enable_internet_access"] = "true";
            else
                json["enable_internet_access"] = "false";

            BindBoolFeatureOn(json, "enable_fs_integrity_checks", "enable_fs_integrity_checks", "true", "false");
            BindFeature(json, "audio_backend", "audio_backend", "SDL2");
            BindFeature(json, "memory_manager_mode", "memory_manager_mode", "HostMappedUnsafe");
            BindBoolFeature(json, "expand_ram", "expand_ram", "true", "false");
            BindBoolFeature(json, "ignore_missing_services", "ignore_missing_services", "true", "false");
            BindFeature(json, "system_region", "system_region", "USA");

            //Graphics Settings
            BindBoolFeatureAuto(json, "backend_threading", "backend_threading", "On", "Off", "Auto");
            
            BindBoolFeatureOn(json, "enable_shader_cache", "enable_shader_cache", "true", "false");
            BindBoolFeature(json, "enable_texture_recompression", "enable_texture_recompression", "true", "false");

            // Resolution
            string res;
            if (SystemConfig.isOptSet("res_scale") && !string.IsNullOrEmpty(SystemConfig["res_scale"]))
            {
                res = SystemConfig["res_scale"];

                if (res.StartsWith("0."))
                {
                    json["res_scale"] = "-1";
                    json["res_scale_custom"] = res;
                }
                else
                {
                    json["res_scale"] = res;
                }
            }

            else
                json["res_scale"] = "1";

            BindFeature(json, "max_anisotropy", "max_anisotropy", "-1");
            BindFeature(json, "aspect_ratio", "aspect_ratio", "Fixed16x9");

            //Perform conroller configuration
            CreateControllerConfiguration(json);

            BindFeature(json, "graphics_backend", "backend", "Vulkan");

            // Networking
            if (SystemConfig.isOptSet("ryujinx_network") && !string.IsNullOrEmpty(SystemConfig["ryujinx_network"]))
            {
                string network = SystemConfig["ryujinx_network"];

                switch(network)
                {
                    case "no":
                        json["multiplayer_mode"] = "0";
                        break;
                    case "local":
                    case "internet":
                        json["multiplayer_mode"] = "1";
                        break;
                }
            }
            else
                json["multiplayer_mode"] = "0";

            //save config file
            json.Save();
        }
    }
}
