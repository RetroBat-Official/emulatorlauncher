using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class Vita3kGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            //get emulator path based on emulator name
            string path = AppConfig.GetFullPath("vita3k");

            //get the executable file
            string exe = Path.Combine(path, "vita3k.exe");
            if (!File.Exists(exe))
                return null;

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
                gameID = rom.Substring(rom.IndexOf('[') + 1, rom.IndexOf(']') - rom.IndexOf('[') - 1);
            }

            //Define command-line arguments
            List<string> commandArray = new List<string>();

            //-w, -f is used to avoid vita3k regenerating the config file as it is very fussy with it !
            //-c to specify the configfile to use
            //-r for game rom/ID
            commandArray.Add("-F");
            commandArray.Add("-w");
            commandArray.Add("-f");
            
            //get the configfile to specify it in the command line arguments
            String configfile = Path.Combine(path, "config.yml");
            commandArray.Add("-c " + "\"" + configfile + "\"");

            //check if game is already installed or not (if installed game folder with gameID name exists in ux0\app\<gameID> folder)
            //if game is not yet installed ==> install it
            //if game is installed ==> run it
            string gamepath = Path.Combine(path, "ux0", "app", gameID);
            
            if (!Directory.Exists(gamepath) && (ext == ".vpk" || ext == ".psvita"))
            {
                commandArray.Add("-path " + "\"" + rom + "\"");     //path used to install the game
            }

            if (Directory.Exists(gamepath) || ext == "m3u")
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
                { "tw", "11" },
                { "pl", "16" }
            };

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return "11";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }

            return "1";
        }

        //Configure config.yml file
        private void SetupConfiguration(string path)
        {
            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));
            
            //First tackle the GUI stuff
            yml["initial-setup"] = "true";
            yml["user-auto-connect"] = "true";
            yml["show-welcome"] = "false";

            //System language
            BindFeature(yml, "sys-lang", "psvita_language", GetDefaultvitaLanguage());

            //Then the emulator options
            BindFeature(yml, "backend-renderer", "backend-renderer", "Vulkan");
            BindFeature(yml, "resolution-multiplier", "resolution-multiplier", "1");
            
            //Automatically disable surface sync when upscaling (as per standard emulator beahviour)
            if (SystemConfig.isOptSet("resolution-multiplier") && SystemConfig["resolution-multiplier"] != "1")
                yml["disable-surface-sync"] = "true";
            else
                yml["disable-surface-sync"] = "false";
            BindFeature(yml, "enable-fxaa", "enable-fxaa", "false");
            BindFeature(yml, "v-sync", "v-sync", "false");
            BindFeature(yml, "anisotropic-filtering", "anisotropic-filtering", "1");
            BindFeature(yml, "cpu-opt", "cpu-opt", "true");
            BindFeature(yml, "shader-cache", "shader-cache", "true");
            BindFeature(yml, "texture-cache", "texture-cache", "true");
            BindFeature(yml, "performance-overlay", "performance-overlay", "false");
            
            //Performance overlay options
            if (SystemConfig.isOptSet("performance-overlay") && SystemConfig["performance-overlay"] != "false")
            {
                yml["performance-overlay"] = "true";
                yml["perfomance-overlay-detail"] = SystemConfig["performance-overlay"];
            }
            else
                yml["performance-overlay"] = "false";

            //write pref-path with emulator path
            string vita_emulator_path = AppConfig.GetFullPath("vita3k");
            yml["pref-path"] = vita_emulator_path;

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
            //else don't touch the modules container
                

            //save config file
            yml.Save();           
        }

    }
}
