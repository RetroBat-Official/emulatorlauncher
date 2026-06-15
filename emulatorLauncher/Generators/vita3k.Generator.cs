using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static EmulatorLauncher.Common.KeyboardInterceptor;

namespace EmulatorLauncher
{
    class Vita3kGenerator : Generator
    {
        private string _prefPath = "";
        private bool _fullscreen = true;
        Process _vita3kProcess = null;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("vita3k");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "Vita3K.exe");
            if (!File.Exists(exe))
                return null;

            _fullscreen = ShouldRunFullscreen();

            if (!GetVita3kPrefPath(path))
                _prefPath = Path.Combine(AppConfig.GetFullPath("saves"), "psvita", "vita3k");

            SimpleLogger.Instance.Info("[Generator] Setting '" + _prefPath + "' as content path for the emulator");

            //Check extension of rom
            /*
             * .m3u is used for games already installed in vita3k emulator
             * .psvita for folders that need to be installed a first time
             * .vpk for packages that need to be installed a first time
             */
            string ext = Path.GetExtension(rom);
            
            //get gameID for later check whether the game is already installed or not
            string gameID = "";
            
            if (ext == ".m3u")
                gameID = File.ReadAllText(rom);
            
            if (ext == ".vpk" || ext == ".psvita")
            {
                if (rom.Contains('['))
                    gameID = rom.Substring(rom.IndexOf('[') + 1, rom.IndexOf(']') - rom.IndexOf('[') - 1);
            }

            if (string.IsNullOrEmpty(gameID))
                SimpleLogger.Instance.Warning("[WARNING] No game ID specified, running vita3k without game.");

            List<string> commandArray = new List<string>();

            //-w, -f is used to avoid vita3k regenerating the config file as it is very fussy with it !
            //-c to specify the configfile to use
            //-r for game rom/ID
            if (_fullscreen)
                commandArray.Add("-F");

            commandArray.Add("-w");
            commandArray.Add("-f");
            
            //get the configfile to specify it in the command line arguments
            String configfile = Path.Combine(path, "config.yml");
            commandArray.Add("-c " + "\"" + configfile + "\"");

            //check if game is already installed or not (if installed game folder with gameID name exists in ux0\app\<gameID> folder)
            //if game is not yet installed ==> install it
            //if game is installed ==> run it
            string gamepath = Path.Combine(_prefPath, "ux0", "app", gameID);
            
            if (!Directory.Exists(gamepath) && (ext == ".vpk" || ext == ".psvita"))
                commandArray.Add("-path " + "\"" + rom + "\"");

            if (!string.IsNullOrEmpty(gameID) && (Directory.Exists(gamepath) || ext == ".m3u"))
                commandArray.Add("-r " + gameID);

            string args = string.Join(" ", commandArray);

            //setup config.ini file
            SetupConfiguration(configfile);
            SetupGUIConfiguration(path);

            //Start emulator
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                UseShellExecute = false,
            };
        }

        //Configure config.yml file
        private void SetupConfiguration(string configPpath)
        {
            if (!File.Exists(configPpath))
            {
                string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "vita3k", "config.yml");

                if (File.Exists(templateFile))
                {
                    try
                    {
                        File.Copy(templateFile, configPpath);
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Error("Error copying config template file: " + ex.Message);
                    }
                }
            }
            
            if (!File.Exists(configPpath))
            {
                SimpleLogger.Instance.Error("Config file not found and template file not found, cannot create config file for vita3k.");
                return;
            }

            var yml = YmlFile.Load(Path.Combine(configPpath));

            //write pref-path with emulator path
            yml["pref-path"] = _prefPath;

            //First tackle the GUI stuff
            yml["initial-setup"] = "true";
            yml["user-auto-connect"] = "true";
            yml["show-welcome"] = "false";
            yml["boot-apps-full-screen"] = _fullscreen ? "true" : "false";

            // Discord
            if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                yml["discord-rich-presence"] = "true";
            else
                yml["discord-rich-presence"] = "false";

            //System language
            BindFeature(yml, "sys-lang", "psvita_language", GetDefaultvitaLanguage());

            //Then the emulator options
            BindFeature(yml, "backend-renderer", "backend-renderer", "Vulkan");
            BindFeature(yml, "resolution-multiplier", "resolution_multiplier", "1.00");
            BindBoolFeature(yml, "disable-surface-sync", "disable_surfacesync", "true", "false");
            BindFeature(yml, "screen-filter", "vita_screenfilter", "Bilinear");
            BindBoolFeatureOn(yml, "v-sync", "vita_vsync", "true", "false");
            BindFeature(yml, "anisotropic-filtering", "anisotropic-filtering", "1");
            BindBoolFeatureOn(yml, "cpu-opt", "cpu_opt", "true", "false");
            BindBoolFeatureOn(yml, "async-pipeline-compilation", "async_pipeline_compilation", "true", "false");
            BindBoolFeatureOn(yml, "shader-cache", "shader_cache", "true", "false");
            BindBoolFeatureOn(yml, "texture-cache", "texture_cache", "true", "false");
            BindFeature(yml, "performance-overlay", "performance-overlay", "false");
            BindFeature(yml, "high-accuracy", "vita3k_high_accuracy", "true");
            BindBoolFeature(yml, "fps-hack", "vita3k_fpshack", "true", "false");
            BindBoolFeature(yml, "show-compile-shaders", "vita3k_showShaderCompile", "true", "false");
            yml["check-for-updates-mode"] = "0";

            //Performance overlay options
            if (SystemConfig.isOptSet("performance-overlay") && SystemConfig["performance-overlay"] != "false")
            {
                yml["performance-overlay"] = "true";
                yml["perfomance-overlay-detail"] = SystemConfig["performance-overlay"];
            }
            else
                yml["performance-overlay"] = "false";

            //Add modules if user has set option to manage from RETROBAT
            if (SystemConfig.isOptSet("modules") && SystemConfig["modules"] == "1")
            {
                yml["modules-mode"] = "1";
                var lleModules = yml.GetOrCreateContainer("lle-modules");
                
                //clear existing list of modules and let EL add modules
                lleModules.Elements.Clear();

                //Start adding modules
                
                //libhttp
                if (SystemConfig.getOptBoolean("libhttp"))
                    lleModules.Elements.Add(new YmlElement() { Value = "- libhttp" });
                
                //libscemp4
                if (SystemConfig.getOptBoolean("libscemp4"))
                    lleModules.Elements.Add(new YmlElement() { Value = "- libscemp4" });

                //Add more modules in the future based on user feedback, tests and games requiring specific modules
            }

            // If user has set feature to AUTOMATIC IN VITA, clear list of modules and set mode to auto
            else if (SystemConfig.isOptSet("modules") && SystemConfig["modules"] == "0")
            {
                yml["modules-mode"] = "0";
                var lleModules = yml.GetOrCreateContainer("lle-modules");
                lleModules.Elements.Clear();
            }

            // Custom textures
            if (SystemConfig.getOptBoolean("vita_custom_textures"))
                yml["import-textures"] = "true";
            else
                yml["import-textures"] = "false";

            // Controls
            var buttonMap = yml.GetOrCreateContainer("controller-binds");
            buttonMap.Elements.Clear();
            
            /*var c1 = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            buttonMap.Elements.Add(new YmlElement() { Value = "- 0" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 1" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 2" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 3" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 4" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 5" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 6" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 7" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 8" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 9" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 10" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 11" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 12" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 13" });
            buttonMap.Elements.Add(new YmlElement() { Value = "- 14" });*/

            //save config file
            yml.Save();
        }

        private void SetupGUIConfiguration(string path)
        {
            string guiSettingsPath = Path.Combine(path, "gui-configs");
            if (!Directory.Exists(guiSettingsPath))
            {
                try
                {
                    Directory.CreateDirectory(guiSettingsPath);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error("Error creating gui-configs directory: " + ex.Message);
                    return;
                }
            }

            string guiConfigFile = Path.Combine(guiSettingsPath, "CurrentSettings.ini");

            using (var ini = new IniFile(guiConfigFile))
            {
                ini.WriteValue("MainWindow", "confirmExitApp", "false");
            }
        }

        /// <summary>
        /// UI - console language
        /// Japanese = 0
        /// English = 1
        /// French = 2
        /// Spain = 3
        /// German = 4
        /// Italian = 5
        /// Dutch = 6
        /// Portuguese = 7
        /// Russian = 8
        /// Korean = 9
        /// Chinese = 10
        /// Taiwanese = 11
        /// Polish = 16
        /// </summary>
        /// <returns></returns>
        private string GetDefaultvitaLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "jp", "0" },
                { "ja", "0" },
                { "en", "1" },
                { "fr", "2" },
                { "es", "3" },
                { "de", "4" },
                { "it", "5" },
                { "nl", "6" },
                { "pt", "7" },
                { "ru", "8" },
                { "ko", "9" },
                { "zh", "10" },
                { "cn", "11" },
                { "fi", "12" },
                { "sv", "13" },
                { "da", "14" },
                { "nn", "15" },
                { "pl", "16" }
            };

            // Special cases
            if (SystemConfig["Language"] == "zh_TW")
                return "11";
            if (SystemConfig["Language"] == "zh_CN")
                return "11";
            if (SystemConfig["Language"] == "pt_BR")
                return "17";
            if (SystemConfig["Language"] == "en_GB")
                return "18";
            if (SystemConfig["Language"] == "nb")
                return "15";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }

            return "1";
        }

        private bool GetVita3kPrefPath(string path)
        {
            if (SystemConfig.isOptSet("vita3k_pref_path") && !string.IsNullOrEmpty(SystemConfig["vita3k_pref_path"]))
            {
                _prefPath = SystemConfig["vita3k_pref_path"].Replace("/", "\\");
                return true;
            }
            else
                return false;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "vita3k", InputKey.hotkey | InputKey.start,
                action: () =>
                {
                    var p = Process.GetProcessesByName("Vita3K").FirstOrDefault();
                    if (p == null)
                        return;

                    try
                    {
                        if (!p.HasExited)
                        {
                            p.CloseMainWindow();
                            p.WaitForExit(3000);
                        }
                    }
                    catch { }

                    try
                    {
                        if (!p.HasExited)
                            p.Kill();
                    }
                    catch { }
                });
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            try
            {
                var px = Process.Start(path);

                if (px == null)
                    return 0;

                using (var escHook = new KeyboardInterceptor(px, new KeyTrigger(Keys.Escape)))
                {
                    px.WaitForExit();
                    SimpleLogger.Instance.Info("[Generator] Process exited with code " + px.ExitCode);
                    int exitCode = px.ExitCode;

                    foreach (var p in Process.GetProcessesByName("Vita3K").Where(p => !p.HasExited))
                        try { p.CloseMainWindow(); } catch { }

                    foreach (var p in Process.GetProcessesByName("Vita3K").Where(p => !p.HasExited))
                        try { p.WaitForExit(2000); } catch { }

                    foreach (var p in Process.GetProcessesByName("Vita3K").Where(p => !p.HasExited))
                        try { if (!p.HasExited) p.Kill(); } catch { }

                    return 0;
                }
            }
            catch { }

            return 0;
        }
    }
}
