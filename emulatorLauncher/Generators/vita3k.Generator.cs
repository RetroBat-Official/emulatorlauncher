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

            //rom needs to be installed in the emulator first, as the argument for command-line can only be the game code, not possible to put a path to a file
            //Using a m3u file with the game code in it in the retrobat rom folder after having installed the game in the emulator
            rom = File.ReadAllText(rom);

            //Define command-line arguments
            List<string> commandArray = new List<string>();
            
            //-w, -f is used to avoid vita3k regenerating the config file as it is very fussy with it !
            //-c to specify the configfile to use
            //-r for game rom/ID
            commandArray.Add("--fullscreen");
            commandArray.Add("-w");
            commandArray.Add("-f");
            
            //get the configfile to specify it in the command line arguments
            String configfile = Path.Combine(path, "config.yml");
            commandArray.Add("-c" + configfile);
            commandArray.Add("-r" + rom);

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
            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));
            
            //First tackle the GUI stuff
            yml["initial-setup"] = "true";
            yml["user-auto-connect"] = "true";
            yml["show-welcome"] = "false";

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
            string pref_path = (vita_emulator_path + "/");
            yml["pref-path"] = pref_path;

            //save config file
            yml.Save();           
        }

    }
}
