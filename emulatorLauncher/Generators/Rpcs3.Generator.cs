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

            // Check if firmware is installed in emulator, if not and if firmware is available in \bios path then install it instead of running the game
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
          
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,                
                WindowStyle = ProcessWindowStyle.Minimized
            };
        }

        /// <summary>
        /// Set 6 options in rpcs3 GUI settings to disable prompts (updates, exit, launching game...)
        /// </summary>
        /// <param name="path"></param>
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

        /// <summary>
        /// Setup config.yml file
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfiguration(string path)
        {
            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));

            // Handle Core part of yml file
            var core = yml.GetOrCreateContainer("Core");
            BindFeature(core, "PPU Decoder", "ppudecoder", "Recompiler (LLVM)");
            BindFeature(core, "PPU LLVM Precompilation", "lvmprecomp", "true");
            BindFeature(core, "SPU Decoder", "spudecoder", "Recompiler (LLVM)");
            BindFeature(core, "Lower SPU thread priority", "lowerspuprio", "false");
            BindFeature(core, "Preferred SPU Threads", "sputhreads", "0");
            BindFeature(core, "SPU loop detection", "spuloopdetect", "false");
            BindFeature(core, "SPU Block Size", "spublocksize", "Safe");
            BindFeature(core, "Accurate RSX reservation access", "accuratersx", "false");
            BindFeature(core, "Accurate xfloat", "accuratexfloat", "false");
            BindFeature(core, "PPU LLVM Accurate Vector NaN values", "vectornan", "false");
            BindFeature(core, "Full Width AVX-512", "fullavx", "false");
            
            // Handle Video part of yml file
            var video = yml.GetOrCreateContainer("Video");
            BindFeature(video, "Renderer", "gfxbackend", "Vulkan");
            BindFeature(video, "Resolution", "rpcs3_internal_resolution", "1280x720");
            BindFeature(video, "Aspect ratio", "ratio", "16:9");
            BindFeature(video, "Frame limit", "framelimit", "Auto");
            BindFeature(video, "MSAA", "msaa", "Auto");
            BindFeature(video, "Shader Mode", "shadermode", "Async Shader Recompiler");
            BindFeature(video, "Write Color Buffers", "writecolorbuffers", "false");
            BindFeature(video, "Write Depth Buffer", "writedepthbuffers", "false");
            BindFeature(video, "Read Color Buffers", "readcolorbuffers", "false");
            BindFeature(video, "Read Depth Buffer", "readdepthbuffers", "false");
            BindFeature(video, "VSync", "vsync", "false");
            BindFeature(video, "Stretch To Display Area", "stretchtodisplay", "false");
            BindFeature(video, "Strict Rendering Mode", "strict_rendering", "false");
            BindFeature(video, "Disable Vertex Cache", "disablevertex", "false");
            BindFeature(video, "Multithreaded RSX", "multithreadedrsx", "false");
            BindFeature(video, "Enable 3D", "enable3d", "false");
            BindFeature(video, "Anisotropic Filter Override", "anisotropicfilter", "0");
            
            // Handle Vulkan part of yml file
            var vulkan = video.GetOrCreateContainer("Vulkan");
            BindFeature(vulkan, "Asynchronous Texture Streaming 2", "asynctexturestream", "false");
            
            // Handle Audio part of yml file
            var audio = yml.GetOrCreateContainer("Audio");
            BindFeature(audio, "Renderer", "audiobackend", "XAudio2");
            BindFeature(audio, "Audio Format", "audiochannels", "Downmix to Stereo");
            
            // Handle Miscellaneous part of yml file
            var misc = yml.GetOrCreateContainer("Miscellaneous");
            BindFeature(misc, "Start games in fullscreen mode", "startfullscreen", "true");

            // Save to yml file
            yml.Save();
        }
    }
}
