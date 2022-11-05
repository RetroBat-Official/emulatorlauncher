using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class Rpcs3Generator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("rpcs3");

            string exe = Path.Combine(path, "rpcs3.exe");
            if (!File.Exists(exe))
                return null;

            rom = this.TryUnZipGameIfNeeded(system, rom);

            if (Directory.Exists(rom))
            {
                string eboot = Path.Combine(rom, "PS3_GAME\\USRDIR\\EBOOT.BIN");
                if (!File.Exists(eboot))
                    eboot = Path.Combine(rom, "USRDIR\\EBOOT.BIN");

                if (!File.Exists(eboot))
                    throw new ApplicationException("Unable to find any game in the provided folder");

                rom = eboot;
            }
            else if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                string romPath = Path.GetDirectoryName(rom);
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(romPath, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            List<string> commandArray = new List<string>();
            commandArray.Add("\"" + rom + "\"");

            if (SystemConfig.isOptSet("gui") && !SystemConfig.getOptBoolean("gui"))
                commandArray.Add("--no-gui");

            string args = string.Join(" ", commandArray);
            
            // If game was uncompressed, say we are going to launch, so the deletion will not be silent
            ValidateUncompressedGame();

            SetupGuiConfiguration(path);
            SetupConfiguration(path);

            //Check if firmware is installed in emulator, if not and if firmware is available in \bios path then install it instead of running the game
            string firmware = Path.Combine(path, "dev_flash", "vsh", "etc", "version.txt");
            string biosPath = AppConfig.GetFullPath("bios");
            string biosPs3 = Path.Combine(biosPath, "PS3UPDAT.PUP");
            if (!File.Exists(firmware) && File.Exists(biosPs3))
            {
                List<string> commandArrayfirmware = new List<string>();
                commandArrayfirmware.Add("--installfw");
                commandArrayfirmware.Add(biosPs3);
                string argsfirmware = string.Join(" ", commandArrayfirmware);
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = argsfirmware,
                };
            }
            else
                return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,                
                WindowStyle = ProcessWindowStyle.Minimized
            };
        }
        //Set 6 options in rpcs3 GUI settings to disable prompts (updates, exit, launching game...)
        private void SetupGuiConfiguration(string path)
        {
            string guiSettings = Path.Combine(path, "GuiConfigs", "CurrentSettings.ini");
            using (var ini = new IniFile(guiSettings))
            {
                ini.WriteValue("main_window", "confirmationBoxExitGame", "false");
                ini.WriteValue("main_window", "infoBoxEnabledInstallPUP", "false");
                ini.WriteValue("main_window", "infoBoxEnabledWelcome", "false");
                ini.WriteValue("main_window", "confirmationBoxBootGame", "false");
                ini.WriteValue("main_window", "infoBoxEnabledInstallPKG", "false");
                ini.WriteValue("Meta", "checkUpdateStart", "false");
            }
        }
        //Setup config.yml file
        private void SetupConfiguration(string path)
        {
            String configFile = Path.Combine(path, "config.yml");
            var config = configFile;
            var yml = YmlFile.Load(config);
            //Handle Core part of yml file
            var core = yml.GetOrCreateContainer("Core");
            
            if (SystemConfig.isOptSet("ppudecoder") && !string.IsNullOrEmpty(SystemConfig["ppudecoder"]))
                core["PPU Decoder"] = SystemConfig["ppudecoder"];
            else
                core["PPU Decoder"] = "Recompiler (LLVM)";

            if (SystemConfig.isOptSet("lvmprecomp") && !string.IsNullOrEmpty(SystemConfig["lvmprecomp"]))
                core["PPU LLVM Precompilation"] = SystemConfig["lvmprecomp"];
            else
                core["PPU LLVM Precompilation"] = "true";

            if (SystemConfig.isOptSet("spudecoder") && !string.IsNullOrEmpty(SystemConfig["spudecoder"]))
                core["SPU Decoder"] = SystemConfig["spudecoder"];
            else
                core["SPU Decoder"] = "Recompiler (LLVM)";

            if (SystemConfig.isOptSet("lowerspuprio") && !string.IsNullOrEmpty(SystemConfig["lowerspuprio"]))
                core["Lower SPU thread priority"] = SystemConfig["lowerspuprio"];
            else
                core["Lower SPU thread priority"] = "false";

            if (SystemConfig.isOptSet("sputhreads") && !string.IsNullOrEmpty(SystemConfig["sputhreads"]))
                core["Preferred SPU Threads"] = SystemConfig["sputhreads"];
            else
                core["Preferred SPU Threads"] = "0";

            if (SystemConfig.isOptSet("spuloopdetect") && !string.IsNullOrEmpty(SystemConfig["spuloopdetect"]))
                core["SPU loop detection"] = SystemConfig["spuloopdetect"];
            else
                core["SPU loop detection"] = "false";

            if (SystemConfig.isOptSet("spublocksize") && !string.IsNullOrEmpty(SystemConfig["spublocksize"]))
                core["SPU Block Size"] = SystemConfig["spublocksize"];
            else
                core["SPU Block Size"] = "Safe";

            if (SystemConfig.isOptSet("accuratersx") && !string.IsNullOrEmpty(SystemConfig["accuratersx"]))
                core["Accurate RSX reservation access"] = SystemConfig["accuratersx"];
            else
                core["Accurate RSX reservation access"] = "false";

            if (SystemConfig.isOptSet("accuratexfloat") && !string.IsNullOrEmpty(SystemConfig["accuratexfloat"]))
                core["Accurate xfloat"] = SystemConfig["accuratexfloat"];
            else
                core["Accurate xfloat"] = "false";

            if (SystemConfig.isOptSet("vectornan") && !string.IsNullOrEmpty(SystemConfig["vectornan"]))
                core["PPU LLVM Accurate Vector NaN values"] = SystemConfig["vectornan"];
            else
                core["PPU LLVM Accurate Vector NaN values"] = "false";

            if (SystemConfig.isOptSet("fullavx") && !string.IsNullOrEmpty(SystemConfig["fullavx"]))
                core["Full Width AVX-512"] = SystemConfig["fullavx"];
            else
                core["Full Width AVX-512"] = "false";

            //Handle Video part of yml file
            var video = yml.GetOrCreateContainer("Video");

            if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                video["Renderer"] = SystemConfig["gfxbackend"];
            else
                video["Renderer"] = "Vulkan";

            if (SystemConfig.isOptSet("rpcs3_internal_resolution") && !string.IsNullOrEmpty(SystemConfig["rpcs3_internal_resolution"]))
                video["Resolution"] = SystemConfig["rpcs3_internal_resolution"];
            else
                video["Resolution"] = "1280x720";

            if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                video["Aspect ratio"] = SystemConfig["ratio"];
            else
                video["Aspect ratio"] = "16:9";

            if (SystemConfig.isOptSet("framelimit") && !string.IsNullOrEmpty(SystemConfig["framelimit"]))
                video["Frame limit"] = SystemConfig["framelimit"];
            else
                video["Frame limit"] = "Auto";

            if (SystemConfig.isOptSet("msaa") && !string.IsNullOrEmpty(SystemConfig["msaa"]))
                video["MSAA"] = SystemConfig["msaa"];
            else
                video["MSAA"] = "Auto";

            if (SystemConfig.isOptSet("shadermode") && !string.IsNullOrEmpty(SystemConfig["shadermode"]))
                video["Shader Mode"] = SystemConfig["shadermode"];
            else
                video["Shader Mode"] = "Async Shader Recompiler";

            if (SystemConfig.isOptSet("writecolorbuffers") && !string.IsNullOrEmpty(SystemConfig["writecolorbuffers"]))
                video["Write Color Buffers"] = SystemConfig["writecolorbuffers"];
            else
                video["Write Color Buffers"] = "false";

            if (SystemConfig.isOptSet("writedepthbuffers") && !string.IsNullOrEmpty(SystemConfig["writedepthbuffers"]))
                video["Write Depth Buffer"] = SystemConfig["writedepthbuffers"];
            else
                video["Write Depth Buffer"] = "false";

            if (SystemConfig.isOptSet("readcolorbuffers") && !string.IsNullOrEmpty(SystemConfig["readcolorbuffers"]))
                video["Read Color Buffers"] = SystemConfig["readcolorbuffers"];
            else
                video["Read Color Buffers"] = "false";

            if (SystemConfig.isOptSet("readdepthbuffers") && !string.IsNullOrEmpty(SystemConfig["readdepthbuffers"]))
                video["Read Depth Buffer"] = SystemConfig["readdepthbuffers"];
            else
                video["Read Depth Buffer"] = "false";

            if (SystemConfig.isOptSet("vsync") && !string.IsNullOrEmpty(SystemConfig["vsync"]))
                video["VSync"] = SystemConfig["vsync"];
            else
                video["VSync"] = "false";

            if (SystemConfig.isOptSet("stretchtodisplay") && !string.IsNullOrEmpty(SystemConfig["stretchtodisplay"]))
                video["Stretch To Display Area"] = SystemConfig["stretchtodisplay"];
            else
                video["Stretch To Display Area"] = "false";

            if (SystemConfig.isOptSet("strict_rendering") && !string.IsNullOrEmpty(SystemConfig["strict_rendering"]))
                video["Strict Rendering Mode"] = SystemConfig["strict_rendering"];
            else
                video["Strict Rendering Mode"] = "false";

            if (SystemConfig.isOptSet("disablevertex") && !string.IsNullOrEmpty(SystemConfig["disablevertex"]))
                video["Disable Vertex Cache"] = SystemConfig["disablevertex"];
            else
                video["Disable Vertex Cache"] = "false";

            if (SystemConfig.isOptSet("multithreadedrsx") && !string.IsNullOrEmpty(SystemConfig["multithreadedrsx"]))
                video["Multithreaded RSX"] = SystemConfig["multithreadedrsx"];
            else
                video["Multithreaded RSX"] = "false";

            if (SystemConfig.isOptSet("enable3d") && !string.IsNullOrEmpty(SystemConfig["enable3d"]))
                video["Enable 3D"] = SystemConfig["enable3d"];
            else
                video["Enable 3D"] = "false";

            if (SystemConfig.isOptSet("anisotropicfilter") && !string.IsNullOrEmpty(SystemConfig["anisotropicfilter"]))
                video["Anisotropic Filter Override"] = SystemConfig["anisotropicfilter"];
            else
                video["Anisotropic Filter Override"] = "0";

            //Handle Vulkan part of yml file
            var vulkan = video.GetOrCreateContainer("Vulkan");

            if (SystemConfig.isOptSet("asynctexturestream") && !string.IsNullOrEmpty(SystemConfig["asynctexturestream"]))
                vulkan["Asynchronous Texture Streaming 2"] = SystemConfig["asynctexturestream"];
            else
                vulkan["Asynchronous Texture Streaming 2"] = "false";

            //Handle Audio part of yml file
            var audio = yml.GetOrCreateContainer("Audio");

            if (SystemConfig.isOptSet("audiobackend") && !string.IsNullOrEmpty(SystemConfig["audiobackend"]))
                audio["Renderer"] = SystemConfig["audiobackend"];
            else
                audio["Renderer"] = "XAudio2";

            if (SystemConfig.isOptSet("audiochannels") && !string.IsNullOrEmpty(SystemConfig["audiochannels"]))
                audio["Audio Format"] = SystemConfig["audiochannels"];
            else
                audio["Audio Format"] = "Downmix to Stereo";

            //Handle Miscellaneous part of yml file
            var misc = yml.GetOrCreateContainer("Miscellaneous");

            if (SystemConfig.isOptSet("startfullscreen") && !string.IsNullOrEmpty(SystemConfig["startfullscreen"]))
                misc["Start games in fullscreen mode"] = SystemConfig["startfullscreen"];
            else
                misc["Start games in fullscreen mode"] = "true";

            //save to yml file
            yml.Save();
        }
    }
}
