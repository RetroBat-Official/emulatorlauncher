using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class RyujinxGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("ryujinx");

            string exe = Path.Combine(path, "Ryujinx.exe");
            if (!File.Exists(exe))
                return null;

            SetupConfiguration(path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }

        private string GetDefaultswitchLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "jp", "Japanese" },
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

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return "Taiwanese";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }
            return "AmericanEnglish";
        }

        //Manage Config.json file settings
        private void SetupConfiguration(string path)
        {
            var json = DynamicJson.Load(Path.Combine(path, "portable", "Config.json"));

            //Perform conroller configuration
            CreateControllerConfiguration(json);

            //Set fullscreen
            json["start_fullscreen"] = "true";

            //General Settings
            json["check_updates_on_start"] = "false";
            json["show_confirm_exit"] = "false";
            json["show_console"] = "false";

            //Input
            json["docked_mode"] = "true";

            //System
            json["system_language"] = GetDefaultswitchLanguage();
            BindFeature(json, "enable_vsync", "vsync", "true");
            BindFeature(json, "enable_ptc", "enable_ptc", "true");
            BindFeature(json, "enable_fs_integrity_checks", "enable_fs_integrity_checks", "true");
            BindFeature(json, "audio_backend", "audio_backend", "SDL2");
            BindFeature(json, "memory_manager_mode", "memory_manager_mode", "HostMappedUnsafe");
            BindFeature(json, "expand_ram", "expand_ram", "false");
            BindFeature(json, "ignore_missing_services", "ignore_missing_services", "false");
            BindFeature(json, "system_region", "system_region", "USA");

            //Graphics Settings
            BindFeature(json, "backend_threading", "backend_threading", "Auto");
            BindFeature(json, "graphics_backend", "backend", "Vulkan");
            BindFeature(json, "enable_shader_cache", "enable_shader_cache", "true");
            BindFeature(json, "enable_texture_recompression", "enable_texture_recompression", "false");
            BindFeature(json, "res_scale", "res_scale", "1");
            BindFeature(json, "max_anisotropy", "max_anisotropy", "-1");
            BindFeature(json, "aspect_ratio", "aspect_ratio", "Fixed16x9");

            //save config file
            json.Save();
        }
    }
}
