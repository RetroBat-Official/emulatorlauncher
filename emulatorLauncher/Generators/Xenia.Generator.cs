using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    class XeniaGenerator : Generator
    {
        public XeniaGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private bool _canary = false;
        private bool _edge = false;
        private bool _xeniaManagerConfig = false;
        private string _xeniaManagerConfigFile = null;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string folderName = emulator;

            string exeName = null;
            
            switch (emulator)
            {
                case "xenia":
                    exeName = "xenia.exe";
                    break;
                case "xenia-canary":
                    exeName = "xenia_canary.exe";
                    break;
                case "xenia-edge":
                    exeName = "xenia_edge.exe";
                    break;
                case "xenia-manager":
                    exeName = "xenia_canary.exe";
                    break;
            }

            string path = AppConfig.GetFullPath(folderName);
            if (folderName == "xenia-manager")
            {
                string managerEmuFolder = "Xenia Canary";
                switch (core)
                {
                    case "canary":
                        managerEmuFolder = "Xenia Canary";
                        exeName = "xenia_canary.exe";
                        break;
                    case "mousehook":
                        managerEmuFolder = "Xenia Mousehook";
                        exeName = "xenia_canary_mousehook.exe";
                        break;
                    case "netplay":
                        managerEmuFolder = "Xenia Netplay";
                        exeName = "xenia_canary_netplay.exe";
                        break;
                    }
                path = Path.Combine(path, "Emulators", managerEmuFolder);
            }

            string exe = Path.Combine(path, exeName);
            if (!File.Exists(exe))
                return null;
            
            _canary = exeName.StartsWith("xenia_canary");
            _edge = exeName == "xenia_edge.exe";

            // Create portable file if not exists
            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            // Manage case where rom is a folder
            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.*", SearchOption.AllDirectories)
                   .FirstOrDefault(f =>
                       string.Equals(Path.GetExtension(f), ".iso", StringComparison.OrdinalIgnoreCase));

                if (rom == null)
                {
                    throw new ApplicationException("Unable to find any iso file in the folder");
                }
            }

            string romdir = Path.GetDirectoryName(rom);
            
            if (Path.GetExtension(rom).ToLower() == ".m3u" || Path.GetExtension(rom).ToLower() == ".xbox360")
            {
                SimpleLogger.Instance.Info("[INFO] game file is .m3u, reading content of file.");
                var lines = File.ReadAllLines(rom);

                if (lines.Length > 0)
                {
                    string romExt = FileTools.ReadFirstValidLine(rom);

                    if (romExt.StartsWith("/") || romExt.StartsWith("\\") || romExt.StartsWith("#"))
                        romExt = romExt.Substring(1);
                    else if (romExt.StartsWith(".\\") || romExt.StartsWith("./"))
                        romExt = romExt.Substring(2);

                    rom = Path.Combine(romdir, romExt);
                    SimpleLogger.Instance.Info("[INFO] path to rom : " + (rom != null ? rom : "null"));
                }
            }

            if (useXeniaManagerConfig(AppConfig.GetFullPath(folderName), rom))
            {
                _xeniaManagerConfig = true;
            }

            bool fullscreen = ShouldRunFullscreen();

            if (!_xeniaManagerConfig)
                SetupConfiguration(path, emulator);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("--fullscreen");

            if (_xeniaManagerConfig)
            {
                SimpleLogger.Instance.Info("[INFO] Using Xenia Manager Configuration file, RetroBat will not append configuration.");
                commandArray.Add("--config");
                commandArray.Add(StringExtensions.QuoteString(_xeniaManagerConfigFile));
            }

            if (_edge && SystemConfig.getOptBoolean("edge_ignore_optimized_settings"))
            {
                SimpleLogger.Instance.Info("[INFO] Ignoring optimized settings for Xenia Edge.");
                commandArray.Add("--config");
                commandArray.Add("\"xenia-edge.config.toml\"");
            }

            commandArray.Add(StringExtensions.QuoteString(rom));

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
            SimpleLogger.Instance.Info("[Generator] Writing RetroBat configuration to .toml config file.");

            if (_canary)
            {
                SetupCanary(path);
                return;
            }
            else if (_edge)
            {
                SetupEdge(path);
                return;
            }

            try
            {
                string iniFile = "xenia.config.toml";

                using (IniFile ini = new IniFile(Path.Combine(path, iniFile), IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
                {
                    //APU section
                    string audio_driver = StringExtensions.QuoteString(SystemConfig["apu"], true);
                    if (SystemConfig.isOptSet("apu") && !string.IsNullOrEmpty(SystemConfig["apu"]))
                        ini.AppendValue("APU", "apu", audio_driver);
                    else if (Features.IsSupported("apu"))
                        ini.AppendValue("APU", "apu", "any".QuoteString(true));

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

                    // Display section
                    if (SystemConfig.isOptSet("postprocess_antialiasing") && !string.IsNullOrEmpty(SystemConfig["postprocess_antialiasing"]))
                        ini.AppendValue("Display", "postprocess_antialiasing", SystemConfig["postprocess_antialiasing"].QuoteString(true));
                    else if (Features.IsSupported("postprocess_antialiasing"))
                        ini.AppendValue("Display", "postprocess_antialiasing", "off".QuoteString(true));

                    // Scaling filter
                    if (SystemConfig.isOptSet("postprocess_scaling_and_sharpening") && !string.IsNullOrEmpty(SystemConfig["postprocess_scaling_and_sharpening"]))
                        ini.AppendValue("Display", "postprocess_scaling_and_sharpening", SystemConfig["postprocess_scaling_and_sharpening"].QuoteString(true));
                    else if (Features.IsSupported("postprocess_scaling_and_sharpening"))
                        ini.AppendValue("Display", "postprocess_scaling_and_sharpening", "\"\"");

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

                    //CPU section
                    if (SystemConfig.isOptSet("break_on_unimplemented_instructions") && SystemConfig.getOptBoolean("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "true");
                    else if (Features.IsSupported("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "false");

                    //GPU section
                    string video_driver = StringExtensions.QuoteString(SystemConfig["gpu"], true);
                    if (SystemConfig.isOptSet("gpu") && !string.IsNullOrEmpty(SystemConfig["gpu"]))
                        ini.AppendValue("GPU", "gpu", video_driver);
                    else if (Features.IsSupported("gpu"))
                        ini.AppendValue("GPU", "gpu", "any".QuoteString(true));

                    if (SystemConfig.isOptSet("render_target_path") && (SystemConfig["render_target_path"] == "performance"))
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", StringExtensions.QuoteString("rtv", true));
                        ini.AppendValue("GPU", "render_target_path_vulkan", StringExtensions.QuoteString("fbo", true));
                    }
                    else if (SystemConfig.isOptSet("render_target_path") && (SystemConfig["render_target_path"] == "accuracy"))
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", StringExtensions.QuoteString("rov", true));
                        ini.AppendValue("GPU", "render_target_path_vulkan", StringExtensions.QuoteString("fsi", true));
                    }
                    else
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", "\"any\"");
                        ini.AppendValue("GPU", "render_target_path_vulkan", "\"any\"");
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
                    string contentPath = Path.Combine(AppConfig.GetFullPath("saves"), "xbox360", "xenia", "content");
                    if (Directory.Exists(contentPath))
                    {
                        ini.AppendValue("Storage", "content_root", StringExtensions.QuoteString(contentPath.Replace("\\", "/"), true));
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }
                    else
                    {
                        contentPath = Path.Combine(path, "content");
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }

                    if (SystemConfig.isOptSet("mount_cache") && SystemConfig.getOptBoolean("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "true");
                    else if (Features.IsSupported("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "false");

                    // Controllers section (HID)
                    if (SystemConfig.isOptSet("xenia_hid") && !string.IsNullOrEmpty(SystemConfig["xenia_hid"]))
                        ini.AppendValue("HID", "hid", StringExtensions.QuoteString(SystemConfig["xenia_hid"], true));
                    else if (Features.IsSupported("xenia_hid"))
                        ini.AppendValue("HID", "hid", "\"sdl\"");

                    // Console language
                    if (SystemConfig.isOptSet("xenia_lang") && !string.IsNullOrEmpty(SystemConfig["xenia_lang"]))
                        ini.AppendValue("XConfig", "user_language", SystemConfig["xenia_lang"]);
                    else if (Features.IsSupported("xenia_lang"))
                        ini.AppendValue("XConfig", "user_language", GetXboxLangFromEnvironment());
                }
            }
            catch { }
         }

        private void SetupCanary(string path)
        {
            SetupXConfig(path);

            try
            {
                string iniFile = "xenia-canary.config.toml";

                using (IniFile ini = new IniFile(Path.Combine(path, iniFile), IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
                {
                    //APU section
                    string audio_driver = StringExtensions.QuoteString(SystemConfig["apu"], true);
                    if (SystemConfig.isOptSet("apu") && !string.IsNullOrEmpty(SystemConfig["apu"]))
                        ini.AppendValue("APU", "apu", audio_driver);
                    else if (Features.IsSupported("apu"))
                        ini.AppendValue("APU", "apu", "any".QuoteString(true));

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

                    if (SystemConfig.isOptSet("xenia_patches") && SystemConfig.getOptBoolean("xenia_patches"))
                        ini.AppendValue("General", "apply_patches", "true");
                    else
                        ini.AppendValue("General", "apply_patches", "false");

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

                    if (SystemConfig.isOptSet("readback_resolve") && !string.IsNullOrEmpty(SystemConfig["readback_resolve"]))
                        ini.AppendValue("GPU", "readback_resolve", SystemConfig["readback_resolve"].QuoteString(true));
                    else if (Features.IsSupported("readback_resolve"))
                        ini.AppendValue("GPU", "readback_resolve", "fast".QuoteString(true));

                    if (SystemConfig.isOptSet("xenia_queue_priority") && !string.IsNullOrEmpty(SystemConfig["xenia_queue_priority"]))
                        ini.AppendValue("D3D12", "d3d12_queue_priority", SystemConfig["xenia_queue_priority"]);
                    else if (Features.IsSupported("xenia_queue_priority"))
                        ini.AppendValue("D3D12", "d3d12_queue_priority", "0");

                    if (SystemConfig.isOptSet("xenia_d3d12_debug") && SystemConfig.getOptBoolean("xenia_d3d12_debug"))
                        ini.AppendValue("D3D12", "d3d12_debug", "true");
                    else if (Features.IsSupported("xenia_d3d12_debug"))
                        ini.AppendValue("D3D12", "d3d12_debug", "false");

                    // Display section
                    if (SystemConfig.isOptSet("postprocess_antialiasing") && !string.IsNullOrEmpty(SystemConfig["postprocess_antialiasing"]))
                        ini.AppendValue("Display", "postprocess_antialiasing", SystemConfig["postprocess_antialiasing"].QuoteString(true));
                    else if (Features.IsSupported("postprocess_antialiasing"))
                        ini.AppendValue("Display", "postprocess_antialiasing", "off".QuoteString(true));

                    // Scaling filter
                    if (SystemConfig.isOptSet("postprocess_scaling_and_sharpening") && !string.IsNullOrEmpty(SystemConfig["postprocess_scaling_and_sharpening"]))
                        ini.AppendValue("Display", "postprocess_scaling_and_sharpening", SystemConfig["postprocess_scaling_and_sharpening"].QuoteString(true));
                    else if (Features.IsSupported("postprocess_scaling_and_sharpening"))
                        ini.AppendValue("Display", "postprocess_scaling_and_sharpening", "\"\"");

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

                    //CPU section
                    if (SystemConfig.isOptSet("break_on_unimplemented_instructions") && SystemConfig.getOptBoolean("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "true");
                    else if (Features.IsSupported("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "false");

                    //GPU section
                    string video_driver = StringExtensions.QuoteString(SystemConfig["gpu"], true);
                    if (SystemConfig.isOptSet("gpu") && !string.IsNullOrEmpty(SystemConfig["gpu"]))
                        ini.AppendValue("GPU", "gpu", video_driver);
                    else if (Features.IsSupported("gpu"))
                        ini.AppendValue("GPU", "gpu", "any".QuoteString(true));

                    if (SystemConfig.isOptSet("render_target_path") && (SystemConfig["render_target_path"] == "performance"))
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", StringExtensions.QuoteString("rtv", true));
                        ini.AppendValue("GPU", "render_target_path_vulkan", StringExtensions.QuoteString("fbo", true));
                    }
                    else if (SystemConfig.isOptSet("render_target_path") && (SystemConfig["render_target_path"] == "accuracy"))
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", StringExtensions.QuoteString("rov", true));
                        ini.AppendValue("GPU", "render_target_path_vulkan", StringExtensions.QuoteString("fsi", true));
                    }
                    else
                    {
                        ini.AppendValue("GPU", "render_target_path_d3d12", "\"any\"");
                        ini.AppendValue("GPU", "render_target_path_vulkan", "\"any\"");
                    }

                    if (SystemConfig.isOptSet("gpu_allow_invalid_fetch_constants") && SystemConfig.getOptBoolean("gpu_allow_invalid_fetch_constants"))
                        ini.AppendValue("GPU", "gpu_allow_invalid_fetch_constants", "true");
                    else if (Features.IsSupported("gpu_allow_invalid_fetch_constants"))
                        ini.AppendValue("GPU", "gpu_allow_invalid_fetch_constants", "false");

                    if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                        ini.AppendValue("GPU", "vsync", "false");
                    else if (Features.IsSupported("vsync"))
                        ini.AppendValue("GPU", "vsync", "true");

                    if (SystemConfig.isOptSet("occlusion_query") && !string.IsNullOrEmpty(SystemConfig["occlusion_query"]))
                        ini.AppendValue("GPU", "occlusion_query", StringExtensions.QuoteString(SystemConfig["occlusion_query"], true));
                    else if (Features.IsSupported("occlusion_query"))
                        ini.AppendValue("GPU", "occlusion_query", "fake".QuoteString(true));

                    if (SystemConfig.isOptSet("xenia_clear_memory_page_state") && SystemConfig.getOptBoolean("xenia_clear_memory_page_state"))
                        ini.AppendValue("GPU", "clear_memory_page_state", "true");
                    else if (Features.IsSupported("xenia_clear_memory_page_state"))
                        ini.AppendValue("GPU", "clear_memory_page_state", "false");

                    if (SystemConfig.isOptSet("xenia_framerate_limit") && !string.IsNullOrEmpty(SystemConfig["xenia_framerate_limit"]))
                        ini.AppendValue("GPU", "framerate_limit", SystemConfig["xenia_framerate_limit"]);
                    else if (Features.IsSupported("xenia_framerate_limit"))
                        ini.AppendValue("GPU", "framerate_limit", "60");

                    if (SystemConfig.isOptSet("xenia_anisotropic_override") && !string.IsNullOrEmpty(SystemConfig["xenia_anisotropic_override"]))
                        ini.AppendValue("GPU", "anisotropic_override", SystemConfig["xenia_anisotropic_override"]);
                    else if (Features.IsSupported("xenia_anisotropic_override"))
                        ini.AppendValue("GPU", "anisotropic_override", "-1");

                    if (SystemConfig.isOptSet("xenia_async_shader_compilation") && !SystemConfig.getOptBoolean("xenia_async_shader_compilation"))
                        ini.AppendValue("GPU", "async_shader_compilation", "false");
                    else
                        ini.AppendValue("GPU", "async_shader_compilation", "true");

                    // Video section
                    if (SystemConfig.isOptSet("xenia_avpack") && !string.IsNullOrEmpty(SystemConfig["xenia_avpack"]))
                        ini.AppendValue("Video", "avpack", SystemConfig["xenia_avpack"]);
                    else if (Features.IsSupported("xenia_avpack"))
                        ini.AppendValue("Video", "avpack", "8");

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
                    string contentPath = Path.Combine(AppConfig.GetFullPath("saves"), "xbox360", "xenia", "content");
                    if (Directory.Exists(contentPath))
                    {
                        ini.AppendValue("Storage", "content_root", StringExtensions.QuoteString(contentPath.Replace("\\", "/"), true));
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }
                    else
                    {
                        contentPath = Path.Combine(path, "content");
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }

                    if (SystemConfig.isOptSet("mount_cache") && SystemConfig.getOptBoolean("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "true");
                    else if (Features.IsSupported("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "false");

                    // Controllers section (HID)
                    if (SystemConfig.isOptSet("xenia_hid") && !string.IsNullOrEmpty(SystemConfig["xenia_hid"]))
                        ini.AppendValue("HID", "hid", StringExtensions.QuoteString(SystemConfig["xenia_hid"], true));
                    else if (Features.IsSupported("xenia_hid"))
                        ini.AppendValue("HID", "hid", "\"sdl\"");

                    // Profiles
                    for (int i = 1; i < 4; i++)
                    {
                        string profileHint = "xenia_profile" + i;
                        if (!SystemConfig.isOptSet(profileHint) || string.IsNullOrEmpty(SystemConfig[profileHint]))
                            continue;
                        string profileFolder = SystemConfig[profileHint];
                        string profile = Path.GetFileNameWithoutExtension(profileFolder);

                        if (!profileFolder.Contains(contentPath.Replace("\\", "/")))
                        {
                            SimpleLogger.Instance.Info("[Warning] Profile " + profile + " selected for Player " + i + " not in content path: " + contentPath);
                            continue;
                        }


                        string setting = "logged_profile_slot_" + (i - 1) + "_xuid";
                        if (SystemConfig.isOptSet(profileHint) && !string.IsNullOrEmpty(SystemConfig[profileHint]))
                            ini.AppendValue("Profiles", setting, StringExtensions.QuoteString(profile, true));
                    }
                }
            }
            catch { }
        }

        private void SetupEdge(string path)
        {
            try
            {
                string iniFile = "xenia-edge.config.toml";

                using (IniFile ini = new IniFile(Path.Combine(path, iniFile), IniOptions.KeepEmptyLines | IniOptions.UseSpaces))
                {
                    //APU section
                    string audio_driver = StringExtensions.QuoteString(SystemConfig["apu"], true);
                    if (SystemConfig.isOptSet("apu") && !string.IsNullOrEmpty(SystemConfig["apu"]))
                        ini.AppendValue("APU", "apu", audio_driver);
                    else if (Features.IsSupported("apu"))
                        ini.AppendValue("APU", "apu", "any".QuoteString(true));

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

                    if (SystemConfig.isOptSet("xenia_patches") && SystemConfig.getOptBoolean("xenia_patches"))
                        ini.AppendValue("General", "apply_patches", "true");
                    else
                        ini.AppendValue("General", "apply_patches", "false");

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

                    if (SystemConfig.isOptSet("readback_resolve") && !string.IsNullOrEmpty(SystemConfig["readback_resolve"]))
                        ini.AppendValue("GPU", "readback_resolve", SystemConfig["readback_resolve"].QuoteString(true));
                    else if (Features.IsSupported("readback_resolve"))
                        ini.AppendValue("GPU", "readback_resolve", "fast".QuoteString(true));

                    if (SystemConfig.isOptSet("xenia_queue_priority") && !string.IsNullOrEmpty(SystemConfig["xenia_queue_priority"]))
                        ini.AppendValue("D3D12", "d3d12_queue_priority", SystemConfig["xenia_queue_priority"]);
                    else if (Features.IsSupported("xenia_queue_priority"))
                        ini.AppendValue("D3D12", "d3d12_queue_priority", "0");

                    if (SystemConfig.isOptSet("xenia_d3d12_debug") && SystemConfig.getOptBoolean("xenia_d3d12_debug"))
                        ini.AppendValue("D3D12", "d3d12_debug", "true");
                    else if (Features.IsSupported("xenia_d3d12_debug"))
                        ini.AppendValue("D3D12", "d3d12_debug", "false");

                    // Display section
                    if (SystemConfig.isOptSet("postprocess_antialiasing") && !string.IsNullOrEmpty(SystemConfig["postprocess_antialiasing"]))
                        ini.AppendValue("Display", "postprocess_antialiasing", SystemConfig["postprocess_antialiasing"].QuoteString(true));
                    else if (Features.IsSupported("postprocess_antialiasing"))
                        ini.AppendValue("Display", "postprocess_antialiasing", "off".QuoteString(true));

                    // Scaling filter
                    if (SystemConfig.isOptSet("postprocess_scaling_and_sharpening") && !string.IsNullOrEmpty(SystemConfig["postprocess_scaling_and_sharpening"]))
                        ini.AppendValue("Display", "postprocess_scaling_and_sharpening", SystemConfig["postprocess_scaling_and_sharpening"].QuoteString(true));
                    else if (Features.IsSupported("postprocess_scaling_and_sharpening"))
                        ini.AppendValue("Display", "postprocess_scaling_and_sharpening", "\"\"");

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

                    if (SystemConfig.isOptSet("xenia_internal_display_resolution") && !string.IsNullOrEmpty(SystemConfig["xenia_internal_display_resolution"]))
                        ini.AppendValue("Console", "internal_display_resolution", SystemConfig["xenia_internal_display_resolution"]);
                    else if (Features.IsSupported("xenia_internal_display_resolution"))
                        ini.AppendValue("Console", "internal_display_resolution", "8");

                    //CPU section
                    if (SystemConfig.isOptSet("break_on_unimplemented_instructions") && SystemConfig.getOptBoolean("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "true");
                    else if (Features.IsSupported("break_on_unimplemented_instructions"))
                        ini.AppendValue("CPU", "break_on_unimplemented_instructions", "false");

                    //GPU section
                    string video_driver = StringExtensions.QuoteString(SystemConfig["gpu"], true);
                    if (SystemConfig.isOptSet("gpu") && !string.IsNullOrEmpty(SystemConfig["gpu"]))
                        ini.AppendValue("GPU", "gpu", video_driver);
                    else if (Features.IsSupported("gpu"))
                        ini.AppendValue("GPU", "gpu", "any".QuoteString(true));

                    if (SystemConfig.isOptSet("render_target_path") && (!string.IsNullOrEmpty(SystemConfig["render_target_path"])))
                        ini.AppendValue("GPU", "render_target_path", StringExtensions.QuoteString(SystemConfig["render_target_path"], true));
                    else
                        ini.AppendValue("GPU", "render_target_path", "performance".QuoteString(true));

                    if (SystemConfig.isOptSet("gpu_allow_invalid_fetch_constants") && SystemConfig.getOptBoolean("gpu_allow_invalid_fetch_constants"))
                        ini.AppendValue("GPU", "gpu_allow_invalid_fetch_constants", "true");
                    else if (Features.IsSupported("gpu_allow_invalid_fetch_constants"))
                        ini.AppendValue("GPU", "gpu_allow_invalid_fetch_constants", "false");

                    if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                        ini.AppendValue("GPU", "guest_display_refresh_cap", "false");
                    else if (Features.IsSupported("vsync"))
                        ini.AppendValue("GPU", "guest_display_refresh_cap", "true");

                    if (SystemConfig.isOptSet("occlusion_query") && !string.IsNullOrEmpty(SystemConfig["occlusion_query"]))
                        ini.AppendValue("GPU", "occlusion_query", StringExtensions.QuoteString(SystemConfig["occlusion_query"], true));
                    else if (Features.IsSupported("occlusion_query"))
                        ini.AppendValue("GPU", "occlusion_query", "fake".QuoteString(true));

                    if (SystemConfig.isOptSet("xenia_clear_memory_page_state") && SystemConfig.getOptBoolean("xenia_clear_memory_page_state"))
                        ini.AppendValue("GPU", "clear_memory_page_state", "true");
                    else if (Features.IsSupported("xenia_clear_memory_page_state"))
                        ini.AppendValue("GPU", "clear_memory_page_state", "false");

                    if (SystemConfig.isOptSet("xenia_framerate_limit") && !string.IsNullOrEmpty(SystemConfig["xenia_framerate_limit"]))
                        ini.AppendValue("GPU", "framerate_limit", SystemConfig["xenia_framerate_limit"]);
                    else if (Features.IsSupported("xenia_framerate_limit"))
                        ini.AppendValue("GPU", "framerate_limit", "60");

                    if (SystemConfig.isOptSet("xenia_async_shader_compilation") && !SystemConfig.getOptBoolean("xenia_async_shader_compilation"))
                        ini.AppendValue("GPU", "async_shader_compilation", "false");
                    else
                        ini.AppendValue("GPU", "async_shader_compilation", "true");

                    if (SystemConfig.isOptSet("xenia_clear_memory_page_state") && SystemConfig.getOptBoolean("xenia_clear_memory_page_state"))
                        ini.AppendValue("GPU", "clear_memory_page_state", "true");
                    else if (Features.IsSupported("xenia_clear_memory_page_state"))
                        ini.AppendValue("GPU", "clear_memory_page_state", "false");

                    // Video section
                    if (SystemConfig.isOptSet("xenia_video_standard") && !string.IsNullOrEmpty(SystemConfig["xenia_video_standard"]))
                        ini.AppendValue("Console", "video_standard", SystemConfig["xenia_video_standard"]);
                    else if (Features.IsSupported("xenia_video_standard"))
                        ini.AppendValue("Console", "video_standard", "1");

                    if (SystemConfig.isOptSet("xenia_avpack") && !string.IsNullOrEmpty(SystemConfig["xenia_avpack"]))
                        ini.AppendValue("Video", "avpack", SystemConfig["xenia_avpack"]);
                    else if (Features.IsSupported("xenia_avpack"))
                        ini.AppendValue("Video", "avpack", "8");

                    if (SystemConfig.isOptSet("xenia_widescreen") && !string.IsNullOrEmpty(SystemConfig["xenia_widescreen"]))
                        ini.AppendValue("Console", "widescreen", SystemConfig["xenia_widescreen"]);
                    else if (Features.IsSupported("xenia_widescreen"))
                        ini.AppendValue("Console", "widescreen", "true");

                    if (SystemConfig.isOptSet("xenia_pal50") && SystemConfig.getOptBoolean("xenia_pal50"))
                        ini.AppendValue("Console", "use_50Hz_mode", "true");
                    else if (Features.IsSupported("xenia_pal50"))
                        ini.AppendValue("Console", "use_50Hz_mode", "false");

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
                    string contentPath = Path.Combine(AppConfig.GetFullPath("saves"), "xbox360", "xenia", "content");
                    if (Directory.Exists(contentPath))
                    {
                        ini.AppendValue("Storage", "content_root", StringExtensions.QuoteString(contentPath.Replace("\\", "/"), true));
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }
                    else
                    {
                        contentPath = Path.Combine(path, "content");
                        SimpleLogger.Instance.Info("[Generator] Setting '" + contentPath + "' as content path for the emulator");
                    }

                    if (SystemConfig.isOptSet("mount_cache") && SystemConfig.getOptBoolean("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "true");
                    else if (Features.IsSupported("mount_cache"))
                        ini.AppendValue("Storage", "mount_cache", "false");

                    // Controllers section (HID)
                    if (SystemConfig.isOptSet("xenia_hid") && !string.IsNullOrEmpty(SystemConfig["xenia_hid"]))
                        ini.AppendValue("HID", "hid", StringExtensions.QuoteString(SystemConfig["xenia_hid"], true));
                    else if (Features.IsSupported("xenia_hid"))
                        ini.AppendValue("HID", "hid", "\"sdl\"");

                    // Console language
                    if (SystemConfig.isOptSet("xenia_lang") && !string.IsNullOrEmpty(SystemConfig["xenia_lang"]))
                        ini.AppendValue("Console", "user_language", SystemConfig["xenia_lang"]);
                    else if (Features.IsSupported("xenia_lang"))
                        ini.AppendValue("Console", "user_language", GetXboxLangFromEnvironment());

                    // Profiles
                    for (int i = 1; i < 4; i++)
                    {
                        string profileHint = "xenia_profile" + i;
                        if (!SystemConfig.isOptSet(profileHint) || string.IsNullOrEmpty(SystemConfig[profileHint]))
                            continue;
                        string profileFolder = SystemConfig[profileHint];
                        string profile = Path.GetFileNameWithoutExtension(profileFolder);

                        if (!profileFolder.Contains(contentPath.Replace("\\", "/")))
                        {
                            SimpleLogger.Instance.Info("[Warning] Profile " + profile + " selected for Player " + i + " not in content path: " + contentPath);
                            continue;
                        }


                        string setting = "logged_profile_slot_" + (i - 1) + "_xuid";
                        if (SystemConfig.isOptSet(profileHint) && !string.IsNullOrEmpty(SystemConfig[profileHint]))
                            ini.AppendValue("Profiles", setting, StringExtensions.QuoteString(profile, true));
                    }
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
            {
                SimpleLogger.Instance.Info("[WARNING] Config file reference not found in games.json.");
                return false;
            }

            if (!File.Exists(cfgFile))
                cfgFile = Path.Combine(path, cfgFile);
            if (!File.Exists(cfgFile))
            {
                SimpleLogger.Instance.Info("[WARNING] Config file does not exist in location: " + cfgFile);
                return false;
            }

            _xeniaManagerConfigFile = cfgFile;
            SimpleLogger.Instance.Info("[INFO] Using config file: " + cfgFile);
            return true;
        }

        private void SetupXConfig(string xeniaPath)
        {
            string xconfigPath = Path.Combine(xeniaPath, "xconfig.settings");

            byte[] data;
            if (File.Exists(xconfigPath))
            {
                data = File.ReadAllBytes(xconfigPath);
                if (data.Length != 0x1A18)
                    data = CreateDefaultXConfig();
            }
            else
            {
                string dir = Path.GetDirectoryName(xconfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                data = CreateDefaultXConfig();
            }

            const int UserBase = 0x08E6;
            const int SecuredBase = 0x06E6;

            // --- Widescreen ---
            bool widescreen = !SystemConfig.isOptSet("xenia_widescreen") || SystemConfig.getOptBoolean("xenia_widescreen");
            uint videoFlags = widescreen ? 0x00010000u : 0x00000000u;

            // --- PAL / PAL50 / NTSC ---
            uint avRegion;
            int videoStandard = SystemConfig.isOptSet("xenia_video_standard") ? SystemConfig["xenia_video_standard"].ToInteger() : 1;

            switch (videoStandard)
            {
                case 2: // NTSC-J
                    avRegion = 0x00400200u;
                    WriteUInt32BE(data, UserBase + 0x164, 0x02D001E0u); // VGA 720x480
                    break;
                case 3: // PAL
                    avRegion = 0x00400400u;
                    WriteUInt32BE(data, UserBase + 0x164, 0x02D00240u); // VGA 720x576
                    break;
                case 4: // PAL50
                    avRegion = 0x00800300u;
                    videoFlags |= 0x00000002u;
                    WriteUInt32BE(data, UserBase + 0x164, 0x02D00240u); // VGA 720x576
                    break;
                default: // NTSC (1)
                    avRegion = 0x00400100u;
                    WriteUInt32BE(data, UserBase + 0x164, 0x02D001E0u); // VGA 720x480
                    break;
            }

            WriteUInt32BE(data, SecuredBase + 0x28, avRegion);
            WriteUInt32BE(data, UserBase + 0x030, videoFlags);

            // --- Resolution ---
            uint resolution = GetXConfigResolution();
            WriteUInt32BE(data, UserBase + 0x15C, resolution); // Composite/HDMI
            WriteUInt32BE(data, UserBase + 0x160, resolution); // Component

            // --- Langue ---
            int language = GetXboxLangFromEnvironment().ToInteger();
            if (SystemConfig.isOptSet("xenia_lang") && !string.IsNullOrEmpty(SystemConfig["xenia_lang"]))
                language = SystemConfig["xenia_lang"].ToInteger();

            WriteUInt32BE(data, UserBase + 0x02C, ((uint)language));

            File.WriteAllBytes(xconfigPath, data);
        }

        private static void WriteUInt32BE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)(value);
        }

        private uint GetXConfigResolution()
        {
            if (!SystemConfig.isOptSet("xenia_internal_display_resolution"))
                return 0x050002D0u; // 1280x720 par défaut

            switch (SystemConfig["xenia_internal_display_resolution"].ToInteger())
            {
                case 0: return 0x028001E0u; // 640x480
                case 1: return 0x02800240u; // 640x576
                case 2: return 0x02D001E0u; // 720x480
                case 3: return 0x02D00240u; // 720x576
                case 4: return 0x03200258u; // 800x600
                case 5: return 0x035001E0u; // 848x480
                case 6: return 0x04000300u; // 1024x768
                case 7: return 0x04800360u; // 1152x864
                case 8: return 0x050002D0u; // 1280x720
                case 9: return 0x05000300u; // 1280x768
                case 10: return 0x050003C0u; // 1280x960
                case 11: return 0x05000400u; // 1280x1024
                case 12: return 0x05500300u; // 1360x768
                case 13: return 0x05A00384u; // 1440x900
                case 14: return 0x0690041Au; // 1680x1050
                case 15: return 0x0780021Cu; // 1920x540
                case 16: return 0x07800438u; // 1920x1080
                default: return 0x050002D0u; // 1280x720 par défaut
            }
        }

        private static byte[] CreateDefaultXConfig()
        {
            byte[] data = new byte[0x1A18];
            const int SecuredBase = 0x06E6;
            const int UserBase = 0x08E6;

            // Secured: AvRegion = NtscM
            WriteUInt32BE(data, SecuredBase + 0x28, 0x00400100u);

            // User: VideoFlags = RatioNormal
            WriteUInt32BE(data, UserBase + 0x030, 0u);

            // User: Language = English
            WriteUInt32BE(data, UserBase + 0x02C, 1u);

            // User: Country = UnitedStates
            data[UserBase + 0x040] = 103;

            // User: AudioFlags = DolbyDigital | DolbyProLogic
            WriteUInt32BE(data, UserBase + 0x034, 0x00010001u);

            // User: résolutions par défaut 1280x720
            WriteUInt32BE(data, UserBase + 0x15C, 0x050002D0u);
            WriteUInt32BE(data, UserBase + 0x160, 0x050002D0u);

            // User: VGA 720x480
            WriteUInt32BE(data, UserBase + 0x164, 0x02D001E0u);

            // User: RetailFlags = DashboardInitialized
            WriteUInt32BE(data, UserBase + 0x038, 0x00000040u);

            // User: PcFlags = XBLAllowed | XBLMembershipCreationAllowed
            data[UserBase + 0x041] = 0x03;

            // User: PcGame = NoGameRestrictions
            WriteUInt32BE(data, UserBase + 0x168, 0x000000FFu);

            // User: MusicVolume = 0.7f
            WriteUInt32BE(data, UserBase + 0x1C1, 0x3F333333u);

            return data;
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

    public class XeniaManagerFileLocations
    {
        [JsonProperty("game")]
        public string GameLocation { get; set; }

        [JsonProperty("patch")]
        public string PatchLocation { get; set; }

        [JsonProperty("config")]
        public string ConfigLocation { get; set; }

        [JsonProperty("custom_emulator_executable")]
        public string EmulatorExecutableLocation { get; set; }
    }

    public class XeniaManagerCompat
    {
        [JsonProperty("url")]
        public string CompatURL { get; set; }

        [JsonProperty("rating")]
        public string CompatRating { get; set; }
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

        [JsonProperty("xenia_version")]
        public string EmulatorVersion { get; set; }

        [JsonProperty("playtime")]
        public double? Playtime { get; set; }

        [JsonProperty("artwork")]
        public XeniaManagerArtwork Artwork { get; set; }

        [JsonProperty("compatibility")]
        public XeniaManagerCompat Compatibility { get; set; }

        [JsonProperty("file_locations")]
        public XeniaManagerFileLocations FileLocations { get; set; }
    }
}
