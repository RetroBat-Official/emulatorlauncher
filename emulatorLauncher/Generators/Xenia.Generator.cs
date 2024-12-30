using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using Newtonsoft.Json;

namespace EmulatorLauncher
{
    class XeniaGenerator : Generator
    {
        public XeniaGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private bool _canary = false;
        private bool _xeniaManagerConfig = false;
        private string _xeniaManagerConfigFile = null;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string folderName = emulator;

            string path = AppConfig.GetFullPath(folderName);
            if (folderName == "xenia-manager")
                path = Path.Combine(path, "Emulators", "Xenia Canary");

            string exeName = null;
            switch (emulator)
            {
                case "xenia":
                    exeName = "xenia.exe";
                    break;
                case "xenia-canary":
                    exeName = "xenia_canary.exe";
                    break;
                case "xenia-manager":
                    exeName = "xenia_canary.exe";
                    break;
            }

            string exe = Path.Combine(path, exeName);
            if (!File.Exists(exe))
                return null;

            if (useXeniaManagerConfig(AppConfig.GetFullPath(folderName), rom))
            {
                _xeniaManagerConfig = true;
            }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string romdir = Path.GetDirectoryName(rom);
			
			if (Path.GetExtension(rom).ToLower() == ".m3u" || Path.GetExtension(rom).ToLower() == ".xbox360")
            {
                SimpleLogger.Instance.Info("[INFO] game file is .m3u, reading content of file.");
                rom = File.ReadAllText(rom);
                rom = Path.Combine(romdir, rom.Substring(1));
                SimpleLogger.Instance.Info("[INFO] path to rom : " + (rom != null ? rom : "null"));
            }

            if (!_xeniaManagerConfig)
                SetupConfiguration(path, emulator);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("--fullscreen");

            if (_xeniaManagerConfig)
            {
                SimpleLogger.Instance.Info("[INFO] Using Xenia Manager Configuration file, RetroBat will not append configuration.");
                commandArray.Add("--config");
                commandArray.Add("\"" + _xeniaManagerConfigFile + "\"");
            }

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        //Setup toml configuration file (using AppendValue because config file is very sensitive to values that do not exist and both emulators are still under heavy development)
        private void SetupConfiguration(string path, string emulator)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            SimpleLogger.Instance.Info("[Generator] Writing RetroBat configuration to .toml config file.");

            try
            {
                string iniFile = "xenia-canary.config.toml";
                if (emulator == "xenia")
                    iniFile = "xenia.config.toml";
                
                using (IniFile ini = new IniFile(Path.Combine(path, iniFile), IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
                {
                    //APU section
                    string audio_driver = "\"" + SystemConfig["apu"] + "\"";
                    if (SystemConfig.isOptSet("apu") && !string.IsNullOrEmpty(SystemConfig["apu"]))
                        ini.AppendValue("APU", "apu", audio_driver);
                    else if (Features.IsSupported("apu"))
                        ini.AppendValue("APU", "apu", "\"any\"");

                    //Content section
                    if (SystemConfig.isOptSet("license_mask") && !string.IsNullOrEmpty(SystemConfig["license_mask"]))
                        ini.AppendValue("Content", "license_mask", SystemConfig["license_mask"]);
                    else if (Features.IsSupported("license_mask"))
                        ini.AppendValue("Content", "license_mask", "1");

                    //General section
                    if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                        ini.AppendValue("General", "discord", "true");
                    else
                        ini.AppendValue("General", "discord", "false");

                    if (_canary)
                    {
                        if (SystemConfig.isOptSet("xenia_patches") && SystemConfig.getOptBoolean("xenia_patches"))
                            ini.AppendValue("General", "apply_patches", "true");
                        else
                            ini.AppendValue("General", "apply_patches", "false");
                    }

                    //D3D12 section
                    if (SystemConfig.isOptSet("xenia_allow_variable_refresh_rate_and_tearing") && !SystemConfig.getOptBoolean("xenia_allow_variable_refresh_rate_and_tearing"))
                    {
                        ini.AppendValue("D3D12", "d3d12_allow_variable_refresh_rate_and_tearing", "false");
                        ini.AppendValue("Vulkan", "vulkan_allow_present_mode_immediate", "false");
                    }
                    else if (Features.IsSupported("xenia_allow_variable_refresh_rate_and_tearing"))
                    {
                        ini.AppendValue("D3D12", "d3d12_allow_variable_refresh_rate_and_tearing", "true");
                        ini.AppendValue("Vulkan", "vulkan_allow_present_mode_immediate", "true");
                    }

                    if (SystemConfig.isOptSet("d3d12_readback_resolve") && SystemConfig.getOptBoolean("d3d12_readback_resolve"))
                        ini.AppendValue("D3D12", "d3d12_readback_resolve", "true");
                    else if (Features.IsSupported("d3d12_readback_resolve"))
                        ini.AppendValue("D3D12", "d3d12_readback_resolve", "false");

                    if (SystemConfig.isOptSet("xenia_queue_priority") && !string.IsNullOrEmpty(SystemConfig["xenia_queue_priority"]))
                        ini.AppendValue("D3D12", "d3d12_queue_priority", SystemConfig["xenia_queue_priority"]);
                    else if (Features.IsSupported("xenia_queue_priority"))
                        ini.AppendValue("D3D12", "d3d12_queue_priority", "0");

                    if (SystemConfig.isOptSet("xenia_d3d12_debug") && SystemConfig.getOptBoolean("xenia_d3d12_debug"))
                        ini.AppendValue("D3D12", "d3d12_debug", "true");
                    else if (Features.IsSupported("xenia_d3d12_debug"))
                        ini.AppendValue("D3D12", "d3d12_debug", "false");

                    //Display section
                    string fxaa = "\"" + SystemConfig["postprocess_antialiasing"] + "\"";
                    if (SystemConfig.isOptSet("postprocess_antialiasing") && !string.IsNullOrEmpty(SystemConfig["postprocess_antialiasing"]))
                        ini.AppendValue("Display", "postprocess_antialiasing", fxaa);
                    else if (Features.IsSupported("postprocess_antialiasing"))
                        ini.AppendValue("Display", "postprocess_antialiasing", "\"\"");

                    // Resolution
                    if (SystemConfig.isOptSet("xenia_resolution") && !string.IsNullOrEmpty(SystemConfig["xenia_resolution"]))
                    {
                        string[] res = SystemConfig["xenia_resolution"].Split('_');
                        ini.AppendValue("GPU", "draw_resolution_scale_x", res[0]);
                        ini.AppendValue("GPU", "draw_resolution_scale_y", res[1]);
                    }
                    else
                    {
                        ini.AppendValue("GPU", "draw_resolution_scale_x", "1");
                        ini.AppendValue("GPU", "draw_resolution_scale_y", "1");
                    }
                    
                    if (_canary)
                    {
                        if (SystemConfig.isOptSet("xenia_internal_display_resolution") && !string.IsNullOrEmpty(SystemConfig["xenia_internal_display_resolution"]))
                            ini.AppendValue("Video", "internal_display_resolution", SystemConfig["xenia_internal_display_resolution"]);
                        else if (Features.IsSupported("xenia_internal_display_resolution"))
                            ini.AppendValue("Video", "internal_display_resolution", "8");
                    }

                    //CPU section
                    if (SystemConfig.isOptSet("break_on_unimplemented_instructions") && SystemConfig.getOptBoolean("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "true");
                    else if (Features.IsSupported("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "false");

                    //GPU section
                    string video_driver = "\"" + SystemConfig["gpu"] + "\"";
                    if (SystemConfig.isOptSet("gpu") && !string.IsNullOrEmpty(SystemConfig["gpu"]))
                        ini.AppendValue("GPU", "gpu", video_driver);
                    else if (Features.IsSupported("gpu"))
                        ini.AppendValue("GPU", "gpu", "\"any\"");

                    if (SystemConfig.isOptSet("render_target_path") && (SystemConfig["render_target_path"] == "rtv"))
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", "\"rtv\"");
                        ini.AppendValue("GPU", "render_target_path_vulkan", "\"fbo\"");
                    }
                    else if (SystemConfig.isOptSet("render_target_path") && (SystemConfig["render_target_path"] == "rov"))
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", "\"rov\"");
                        ini.AppendValue("GPU", "render_target_path_vulkan", "\"fsi\"");
                    }
                    else
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", "\"\"");
                        ini.AppendValue("GPU", "render_target_path_vulkan", "\"\"");
                    }

                    if (SystemConfig.isOptSet("gpu_allow_invalid_fetch_constants") && SystemConfig.getOptBoolean("gpu_allow_invalid_fetch_constants"))
                        ini.AppendValue("GPU", "gpu_allow_invalid_fetch_constants", "true");
                    else if (Features.IsSupported("gpu_allow_invalid_fetch_constants"))
                        ini.AppendValue("GPU", "gpu_allow_invalid_fetch_constants", "false");

                    if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                        ini.AppendValue("GPU", "vsync", "false");
                    else if (Features.IsSupported("vsync"))
                        ini.AppendValue("GPU", "vsync", "true");

                    if (SystemConfig.isOptSet("query_occlusion_fake_sample_count") && !string.IsNullOrEmpty(SystemConfig["query_occlusion_fake_sample_count"]))
                        ini.AppendValue("GPU", "query_occlusion_fake_sample_count", SystemConfig["query_occlusion_fake_sample_count"]);
                    else if (Features.IsSupported("query_occlusion_fake_sample_count"))
                        ini.AppendValue("GPU", "query_occlusion_fake_sample_count", "1000");

                    if (_canary)
                    {
                        if (SystemConfig.isOptSet("xenia_clear_memory_page_state") && SystemConfig.getOptBoolean("xenia_clear_memory_page_state"))
                            ini.AppendValue("GPU", "clear_memory_page_state", "true");
                        else if (Features.IsSupported("xenia_clear_memory_page_state"))
                            ini.AppendValue("GPU", "clear_memory_page_state", "false");

                        if (SystemConfig.isOptSet("xenia_framerate_limit") && !string.IsNullOrEmpty(SystemConfig["xenia_framerate_limit"]))
                            ini.AppendValue("GPU", "framerate_limit", SystemConfig["xenia_framerate_limit"]);
                        else if (Features.IsSupported("xenia_framerate_limit"))
                            ini.AppendValue("GPU", "framerate_limit", "60");
                    }

                    // Video section (canary only)
                    if (_canary)
                    {
                        if (SystemConfig.isOptSet("xenia_video_standard") && !string.IsNullOrEmpty(SystemConfig["xenia_video_standard"]))
                            ini.AppendValue("Video", "video_standard", SystemConfig["xenia_video_standard"]);
                        else if (Features.IsSupported("xenia_video_standard"))
                            ini.AppendValue("Video", "video_standard", "1");

                        if (SystemConfig.isOptSet("xenia_avpack") && !string.IsNullOrEmpty(SystemConfig["xenia_avpack"]))
                            ini.AppendValue("Video", "avpack", SystemConfig["xenia_avpack"]);
                        else if (Features.IsSupported("xenia_avpack"))
                            ini.AppendValue("Video", "avpack", "8");

                        if (SystemConfig.isOptSet("xenia_widescreen") && !string.IsNullOrEmpty(SystemConfig["xenia_widescreen"]))
                            ini.AppendValue("Video", "widescreen", SystemConfig["xenia_widescreen"]);
                        else if (Features.IsSupported("xenia_widescreen"))
                            ini.AppendValue("Video", "widescreen", "true");

                        if (SystemConfig.isOptSet("xenia_pal50") && SystemConfig.getOptBoolean("xenia_pal50"))
                            ini.AppendValue("Video", "use_50Hz_mode", "true");
                        else if (Features.IsSupported("xenia_pal50"))
                            ini.AppendValue("Video", "use_50Hz_mode", "false");
                    }

                    // Memory section
                    if (SystemConfig.isOptSet("scribble_heap") && SystemConfig.getOptBoolean("scribble_heap"))
                        ini.AppendValue("Memory", "scribble_heap", "true");
                    else if (Features.IsSupported("scribble_heap"))
                        ini.AppendValue("Memory", "scribble_heap", "false");

                    if (SystemConfig.isOptSet("protect_zero") && !SystemConfig.getOptBoolean("protect_zero"))
                        ini.AppendValue("Memory", "protect_zero", "false");
                    else if (Features.IsSupported("protect_zero"))
                        ini.AppendValue("Memory", "protect_zero", "true");

                    // Storage section
                    string contentPath = Path.Combine(AppConfig.GetFullPath("saves"), "xbox360", "xenia");
                    if (Directory.Exists(contentPath))
                    {
                        ini.AppendValue("Storage", "content_root", "\"" + contentPath.Replace("\\", "/") + "\"");
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }
                    else
                        SimpleLogger.Instance.Info("[Generator] Setting '" + path + "' as content path for the emulator");

                    if (SystemConfig.isOptSet("mount_cache") && SystemConfig.getOptBoolean("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "true");
                    else if (Features.IsSupported("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "false");

                    // Controllers section (HID)
                    if (SystemConfig.isOptSet("xenia_hid") && !string.IsNullOrEmpty(SystemConfig["xenia_hid"]))
                        ini.AppendValue("HID", "hid", "\"" + SystemConfig["xenia_hid"] + "\"");
                    else if (Features.IsSupported("xenia_hid"))
                        ini.AppendValue("HID", "hid", "\"any\"");

                    // Console language
                    if (SystemConfig.isOptSet("xenia_lang") && !string.IsNullOrEmpty(SystemConfig["xenia_lang"]))
                        ini.AppendValue("XConfig", "user_language", SystemConfig["xenia_lang"]);
                    else if (Features.IsSupported("xenia_lang"))
                        ini.AppendValue("XConfig", "user_language", GetXboxLangFromEnvironment());
                }
            }
            catch { }
         }

        private string GetXboxLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, string>()
            {
                { "en", "1" },
                { "jp", "2" },
                { "ja", "2" },
                { "de", "3" },
                { "fr", "4" },
                { "es", "5" },
                { "it", "6" },
                { "ko", "7" },
                { "zh", "8" },
                { "pt", "9" },
                { "pl", "11" },
                { "ru", "12" },
                { "sv", "13" },
                { "tr", "14" },
                { "nl", "16" }
            };

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return "17";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }

            return "1";
        }

        private bool useXeniaManagerConfig(string path, string rom)
        {
            string gameFile = Directory.GetFiles(path, "games.json", SearchOption.AllDirectories).FirstOrDefault();

            if (!File.Exists(gameFile))
                return false;

            SimpleLogger.Instance.Info("[INFO] Searching if game config file exists.");

            string json = File.ReadAllText(gameFile);
            List<XeniaManagerGame> games = JsonConvert.DeserializeObject<List<XeniaManagerGame>>(json);
            string searchLocation = Path.GetFileName(rom);

            XeniaManagerGame foundGame = games.Find(game => game.FileLocations.GameLocation.EndsWith(searchLocation));

            if (foundGame == null)
                return false;

            string cfgFile = foundGame.FileLocations.ConfigLocation;

            if (cfgFile == null)
                return false;
            
            if (!File.Exists(cfgFile))
                return false;

            _xeniaManagerConfigFile = cfgFile;
            SimpleLogger.Instance.Info("[INFO] Using config file: " + cfgFile);
            return true;
        }
    }

    public class XeniaManagerArtwork
    {
        [JsonProperty("background")]
        public string Background { get; set; }

        [JsonProperty("boxart")]
        public string Boxart { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }

    public class XeniaManagerArtworkCache
    {
        [JsonProperty("background")]
        public string Background { get; set; }

        [JsonProperty("boxart")]
        public string Boxart { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }
    }

    public class XeniaManagerFileLocations
    {
        [JsonProperty("game_location")]
        public string GameLocation { get; set; }

        [JsonProperty("patch_location")]
        public string PatchLocation { get; set; }

        [JsonProperty("config_location")]
        public string ConfigLocation { get; set; }

        [JsonProperty("emulator_executable_location")]
        public string EmulatorExecutableLocation { get; set; }
    }

    public class XeniaManagerGame
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("game_id")]
        public string GameId { get; set; }

        [JsonProperty("alternative_id")]
        public List<string> AlternativeId { get; set; }

        [JsonProperty("media_id")]
        public string MediaId { get; set; }

        [JsonProperty("emulator_version")]
        public string EmulatorVersion { get; set; }

        [JsonProperty("playtime")]
        public double? Playtime { get; set; }

        [JsonProperty("gamecompatibility_url")]
        public string GameCompatibilityUrl { get; set; }

        [JsonProperty("compatibility_rating")]
        public string CompatibilityRating { get; set; }

        [JsonProperty("artwork")]
        public XeniaManagerArtwork Artwork { get; set; }

        [JsonProperty("artwork_cache")]
        public XeniaManagerArtworkCache ArtworkCache { get; set; }

        [JsonProperty("file_locations")]
        public XeniaManagerFileLocations FileLocations { get; set; }
    }
}
