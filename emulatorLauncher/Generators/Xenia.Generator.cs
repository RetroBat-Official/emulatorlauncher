using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class XeniaGenerator : Generator
    {
        public XeniaGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private bool _canary = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string folderName = (emulator == "xenia-canary" || core == "xenia-canary") ? "xenia-canary" : "xenia";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("xenia");

            string exe = Path.Combine(path, "xenia.exe");
            if (!File.Exists(exe))
            {
                _canary = true;
                exe = Path.Combine(path, "xenia-canary.exe");

                if (!File.Exists(exe))
                    exe = Path.Combine(path, "xenia_canary.exe");
            }

            if (!File.Exists(exe))
                return null;
			
			string romdir = Path.GetDirectoryName(rom);
			
			if (Path.GetExtension(rom).ToLower() == ".m3u" || Path.GetExtension(rom).ToLower() == ".xbox360")
            {
                SimpleLogger.Instance.Info("[INFO] game file is .m3u, reading content of file.");
                rom = File.ReadAllText(rom);
                rom = Path.Combine(romdir, rom.Substring(1));
                SimpleLogger.Instance.Info("[INFO] path to rom : " + (rom != null ? rom : "null"));
            }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfiguration(path);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("--fullscreen");

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
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

        //Setup toml configuration file (using AppendValue because config file is very sensitive to values that do not exist and both emulators are still under heavy development)
        private void SetupConfiguration(string path)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            SimpleLogger.Instance.Info("[Generator] Writing RetroBat configuration to .toml config file.");

            try
            {
                using (IniFile ini = new IniFile(Path.Combine(path, _canary ? "xenia-canary.config.toml" : "xenia.config.toml"), IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
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

                    if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean(SystemConfig["vsync"]))
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
    }
}
