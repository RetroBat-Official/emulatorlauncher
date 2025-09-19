﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;

namespace EmulatorLauncher
{
    class Vita3kGenerator : Generator
    {
        private string _prefPath = "";

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("vita3k");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "vita3k.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (!GetVita3kPrefPath(path))
                _prefPath = Path.Combine(AppConfig.GetFullPath("saves"), "psvita", "vita3k");

            SimpleLogger.Instance.Info("[Generator] Setting '" + _prefPath + "' as content path for the emulator");

            // Check if firmware is intalled
            string firmware = Path.Combine(_prefPath, "vs0", "vsh", "initialsetup");
            if (!Directory.Exists(firmware))
                throw new ApplicationException("PSVita firmware is not installed in Vita3K emulator, launch the emulator and install the firware.");

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
            if (fullscreen)
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
                commandArray.Add("-path " + "\"" + rom + "\"");     //path used to install the game

            if (!string.IsNullOrEmpty(gameID) && (Directory.Exists(gamepath) || ext == ".m3u"))
                commandArray.Add("-r " + gameID);                    //r used to run installed games

            string args = string.Join(" ", commandArray);

            //setup config.ini file
            SetupConfiguration(path);

            //Start emulator
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,                
            };
        }

        //Configure config.yml file
        private void SetupConfiguration(string path)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));
            
            //First tackle the GUI stuff
            yml["initial-setup"] = "true";
            yml["user-auto-connect"] = "true";
            yml["show-welcome"] = "false";

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
            BindBoolFeature(yml, "show-gui", "vita3k_gui", "true", "false");
            BindBoolFeature(yml, "show-compile-shaders", "vita3k_showShaderCompile", "true", "false");
            yml["check-for-updates"] = "false";

            //Performance overlay options
            if (SystemConfig.isOptSet("performance-overlay") && SystemConfig["performance-overlay"] != "false")
            {
                yml["performance-overlay"] = "true";
                yml["perfomance-overlay-detail"] = SystemConfig["performance-overlay"];
            }
            else
                yml["performance-overlay"] = "false";

            //write pref-path with emulator path
            yml["pref-path"] = _prefPath;

            //Add modules if user has set option to manage from RETROBAT
            if (SystemConfig.isOptSet("modules") && SystemConfig["modules"] == "1")
            {
                yml["modules-mode"] = "1";
                var lleModules = yml.GetOrCreateContainer("lle-modules");
                
                //clear existing list of modules and let EL add modules
                lleModules.Elements.Clear();

                //Start adding modules
                
                //libhttp
                if (SystemConfig.isOptSet("libhttp") && SystemConfig.getOptBoolean("libhttp"))
                    lleModules.Elements.Add(new YmlElement() { Value = "- libhttp" });
                
                //libscemp4
                if (SystemConfig.isOptSet("libscemp4") && SystemConfig.getOptBoolean("libscemp4"))
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

            // Controls
            var buttonMap = yml.GetOrCreateContainer("controller-binds");
            buttonMap.Elements.Clear();
            
            var c1 = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            if (c1 != null && c1.VendorID == Common.Joysticks.USB_VENDOR.NINTENDO)
            {
                buttonMap.Elements.Add(new YmlElement() { Value = "- 1" });
                buttonMap.Elements.Add(new YmlElement() { Value = "- 0" });
                buttonMap.Elements.Add(new YmlElement() { Value = "- 3" });
                buttonMap.Elements.Add(new YmlElement() { Value = "- 2" });
            }
            else
            {
                buttonMap.Elements.Add(new YmlElement() { Value = "- 0" });
                buttonMap.Elements.Add(new YmlElement() { Value = "- 1" });
                buttonMap.Elements.Add(new YmlElement() { Value = "- 2" });
                buttonMap.Elements.Add(new YmlElement() { Value = "- 3" });
            }

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
            buttonMap.Elements.Add(new YmlElement() { Value = "- 14" });

            //save config file
            yml.Save();
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
                { "pl", "16" }
            };

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return "11";

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
            string configFilePath = Path.Combine(path, "config.yml");
            if (!File.Exists(configFilePath))
                return false;

            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));
            if (yml == null)
                return false;

            if (yml["pref-path"] == null)
                return false;

            string prefPath = yml["pref-path"];

            if (string.IsNullOrEmpty(prefPath))
                return false;

            if (!Directory.Exists(prefPath) || prefPath.StartsWith(".") || prefPath.StartsWith("/") || prefPath.StartsWith("\\"))
                return false;
            else
            {
                _prefPath = prefPath;
                return true;
            }
        }
    }
}
