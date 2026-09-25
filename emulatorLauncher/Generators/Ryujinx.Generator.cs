using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmulatorLauncher
{
    partial class RyujinxGenerator : Generator
    {
        public RyujinxGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _emulatorPath;
        private bool _sdl3 = false;
        private string _confPath;
        private JArray _savedGameDirs = new JArray();

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("ryujinx");
            if (!Directory.Exists(path))
                throw new ApplicationException("Emulator path doesn't exist: " + path);

            _emulatorPath = path;

            string exe = Path.Combine(path, "Ryujinx.exe");
            if (!File.Exists(exe))
                throw new ApplicationException("Emulator executable doesn't exist: " + exe);

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exe);
                if (versionInfo != null)
                {
                    string version = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
                    if (!string.IsNullOrEmpty(version))
                    {
                        SimpleLogger.Instance.Info("[Generator] " + emulator + " version: " + version);

                        string checkversion = "1.2.28.0";
                        if (Version.TryParse(checkversion, out Version tocheck))
                        {
                            if (Version.TryParse(version, out Version current))
                            {
                                if (current <= tocheck)
                                {
                                    SystemConfig["ryujinx_sdlguid"] = "true";
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            string sdl3dll = Path.Combine(path, "SDL3.dll");
            if (File.Exists(sdl3dll))
                _sdl3 = true;

            string setupPath = Path.Combine(path, "portable");

            string portablePath = Path.Combine(AppConfig.GetFullPath("saves"), "switch", "ryujinx", "portable");
            if (Directory.Exists(portablePath))
                setupPath = portablePath;

            SimpleLogger.Instance.Info("[Generator] Setting '" + setupPath + "' as content path for the emulator");

            // First of all delete the Config.json file near the executable if it exists
            string jsonFiletoDelete = Path.Combine(path, "Config.json");
            if (File.Exists(jsonFiletoDelete))
                try { File.Delete(jsonFiletoDelete); } catch { }

            SetupConfiguration(setupPath, path);

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
                Arguments = args
            };
        }

        public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
        {
            Process.Start(path);
            Thread.Sleep(2000);
            Process ryujinx = null;
            var timeout = DateTime.UtcNow.AddSeconds(15);

            while (DateTime.UtcNow < timeout)
            {
                ryujinx = Process.GetProcessesByName("Ryujinx")
                                 .FirstOrDefault(p => !p.HasExited);

                if (ryujinx != null)
                {
                    var hwndTimeout = DateTime.UtcNow.AddSeconds(10);

                    IntPtr hwnd = IntPtr.Zero;
                    while (DateTime.UtcNow < hwndTimeout)
                    {
                        hwnd = User32.FindHwnds(ryujinx.Id).FirstOrDefault();
                        if (hwnd != IntPtr.Zero)
                            break;
                        Thread.Sleep(200);
                    }

                    if (hwnd != IntPtr.Zero)
                        User32.ForceForegroundWindow(hwnd);

                    break;
                }
                Thread.Sleep(500);
            }

            ryujinx?.WaitForExit();
            return 0;
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
        private void SetupConfiguration(string setupPath, string path)
        {
            bool fullscreen = ShouldRunFullscreen();

            // Read and parse JSON
            string filePath = Path.Combine(setupPath, "Config.json");
            _confPath = filePath;

            if (!File.Exists(filePath))
            {
                string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "ryujinx", "portable", "Config.json");
                if (File.Exists(templateFile))
                    try { File.Copy(templateFile, filePath); } catch { }
            }

            if (!File.Exists(filePath))
                return;

            string jsonConfig = File.ReadAllText(filePath);
            dynamic json = JsonConvert.DeserializeObject(jsonConfig);

            JObject dynRoot = (JObject)json;

            if (dynRoot["hotkeys"] == null)
                dynRoot["hotkeys"] = new JObject();

            JObject hotkeysObj = (JObject)dynRoot["hotkeys"];

            hotkeysObj["screenshot"] = "F8";
            hotkeysObj["show_ui"] = "F1";
            hotkeysObj["pause"] = "P";
            if (hotkeysObj["toggle_vsync_mode"] != null && hotkeysObj["toggle_vsync_mode"].ToString() == "F1")
                hotkeysObj["toggle_vsync_mode"] = "F3";

            //Set fullscreen
            json.start_fullscreen = fullscreen ? true : false;

            // Folder
            _savedGameDirs = dynRoot["game_dirs"] as JArray ?? new JArray();
            json.game_dirs = new JArray(); // clear temporarely (will be restored in cleanup)

            // General Settings
            json.check_updates_on_start = false;
            json.update_checker_type = "Off";
            json.show_confirm_exit = false;
            json.show_console = SystemConfig.getOptBoolean("ryujinx_showconsole") ? true : false;
            json.focus_lost_action_type = SystemConfig.isOptSet("nopauseonlostfocus") && !SystemConfig.getOptBoolean("nopauseonlostfocus") ? "PauseEmulation" : "DoNothing";

            // Input
            if (SystemConfig.getOptBoolean("ryujinx_undock"))
                json.docked_mode = false;
            else
                json.docked_mode = true;

            json.hide_cursor = 2;

            // Discord
            BindBoolFeatureDefaultFalse(json, "enable_discord_integration", "discord");

            // System
            BindFeature(json, "system_language", "switch_language", GetDefaultswitchLanguage());
            BindFeatureInt(json, "vsync_mode", "ryujinx_vsync", 0);
            if (SystemConfig.isOptSet("ryujinx_vsync") && SystemConfig["ryujinx_vsync"] == "2")
                json.enable_custom_vsync_interval = true;
            else
                json.enable_custom_vsync_interval = false;

            BindBoolFeatureDefaultTrue(json, "enable_ptc", "enable_ptc");
            BindBoolFeatureDefaultFalse(json, "ignore_applet", "ryujinx_controller_applet");

            // internet access
            if (SystemConfig.isOptSet("ryujinx_network") && SystemConfig["ryujinx_network"] == "internet")
                json.enable_internet_access = true;
            else
                json.enable_internet_access = false;

            BindBoolFeatureDefaultTrue(json, "enable_fs_integrity_checks", "enable_fs_integrity_checks");
            BindFeature(json, "audio_backend", "audio_backend", _sdl3 ? "SDL3" : "SDL2");
            BindFeature(json, "memory_manager_mode", "memory_manager_mode", "HostMappedUnsafe");
            BindBoolFeatureDefaultFalse(json, "ignore_missing_services", "ignore_missing_services");
            BindFeature(json, "system_region", "system_region", "USA");

            // Graphics Settings
            BindBoolFeatureAuto(json, "backend_threading", "backend_threading", "On", "Off", "Auto");
            BindFeature(json, "anti_aliasing", "ryujinx_antialiasing", "None");
            BindFeature(json, "scaling_filter", "ryujinx_scaling_filter", "Bilinear");
            BindBoolFeatureDefaultTrue(json, "enable_shader_cache", "enable_shader_cache");
            BindBoolFeatureDefaultFalse(json, "enable_texture_recompression", "enable_texture_recompression");

            // Resolution
            double res;
            if (SystemConfig.isOptSet("res_scale") && !string.IsNullOrEmpty(SystemConfig["res_scale"]))
            {
                res = SystemConfig["res_scale"].ToDouble();

                if (res < 1)
                {
                    json.res_scale = -1;
                    json.res_scale_custom = res;
                }
                else
                {
                    json.res_scale = (int)res;
                }
            }

            else
                json.res_scale = 1;

            BindFeatureInt(json, "max_anisotropy", "max_anisotropy", -1);
            BindFeature(json, "aspect_ratio", "aspect_ratio", "Fixed16x9");

            // Perform conroller configuration
            CreateControllerConfiguration(json);

            BindFeature(json, "graphics_backend", "backend", "Vulkan");

            // Networking
            if (SystemConfig.isOptSet("ryujinx_network") && !string.IsNullOrEmpty(SystemConfig["ryujinx_network"]))
            {
                string network = SystemConfig["ryujinx_network"];

                switch(network)
                {
                    case "no":
                        json.multiplayer_mode = 0;
                        break;
                    case "local":
                    case "internet":
                        json.multiplayer_mode = 1;
                        break;
                }
            }
            else
                json.multiplayer_mode = 0;

            // save config file
            string updatedJson = JsonConvert.SerializeObject(json, Formatting.Indented);
            File.WriteAllText(filePath, updatedJson);

            // Also write the config file near the emulator in case it could not be deleted
            string jsonNearExe = Path.Combine(path, "Config.json");
            if (File.Exists(jsonNearExe))
                File.WriteAllText(jsonNearExe, updatedJson);

            string jsonNearExePortable = Path.Combine(path, "portable", "Config.json");
            if (File.Exists(jsonNearExePortable))
                File.WriteAllText(jsonNearExePortable, updatedJson);
        }

        public override void Cleanup()
        {
            base.Cleanup();

            // Restore game directories size in case it was changed by the user
            if (!string.IsNullOrEmpty(_confPath) && File.Exists(_confPath))
            {
                string jsonConfig = File.ReadAllText(_confPath);
                dynamic json = JsonConvert.DeserializeObject(jsonConfig);
                JObject dynRoot = (JObject)json;
                    json.game_dirs = _savedGameDirs;
                
                // save config file
                string updatedJson = JsonConvert.SerializeObject(json, Formatting.Indented);
                File.WriteAllText(_confPath, updatedJson);
            }
        }
    }
}
