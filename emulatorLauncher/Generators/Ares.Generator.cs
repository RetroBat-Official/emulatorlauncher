using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class AresGenerator : Generator
    {
        public AresGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("ares");

            string exe = Path.Combine(path, "ares.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed(out _);

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            List<string> commandArray = new List<string>();
            
            commandArray.Add("--system");
            commandArray.Add(core);
            commandArray.Add("\"" + rom + "\"");

            if (fullscreen)
                commandArray.Add("--fullscreen");

            string args = string.Join(" ", commandArray);

            var bml = BmlFile.Load(Path.Combine(path, "settings.bml"));
            SetupConfiguration(bml, path, system, core, rom);
            WriteKeyboardHotkeys(bml, path);
            CreateControllerConfiguration(bml, path);

            bml.Save();

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,                
            };
        }

        /// <summary>
        /// Setup settings.bml file
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfiguration(BmlFile bml, string path, string system, string core, string rom)
        {
            // Set driver for input
            var input = bml.GetOrCreateContainer("Input");
            input["Driver"] = "SDL";

            // Video
            var video = bml.GetOrCreateContainer("Video");
            BindFeature(video, "Driver", "ares_renderer", "OpenGL 3.2");

            // Audio
            var audio = bml.GetOrCreateContainer("Audio");
            BindFeature(audio, "Driver", "ares_audio_renderer", "WASAPI");

            // General Settings
            var general = bml.GetOrCreateContainer("General");
            BindBoolFeature(general, "Rewind", "rewind", "true", "false");
            BindBoolFeature(general, "RunAhead", "ares_runahead", "true", "false");
            BindBoolFeature(general, "AutoSaveMemory", "autosave", "true", "false");

            // Paths
            var paths = bml.GetOrCreateContainer("Paths");
            
            string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "ares");
            if (!Directory.Exists(screenshotsPath)) try { Directory.CreateDirectory(screenshotsPath); }
                catch { }
            string aresScreenshotsPath = screenshotsPath + "/";
            
            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "ares");
            if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                catch { }
            string aresSavesPath = savesPath + "/";
            
            paths["Screenshots"] = aresScreenshotsPath.Replace("\\", "/");
            paths["Saves"] = aresSavesPath.Replace("\\", "/");

            // Current rom path
            var aresCore = bml.GetOrCreateContainer(core);
            aresCore["Path"] = Path.GetDirectoryName(rom).Replace("\\", "/") + "/";
        }

        private void WriteKeyboardHotkeys(BmlFile bml, string path)
        {
            // Use padtokey mapping to map these keys to controllers as Ares does not allow combos
            var hotkey = bml.GetOrCreateContainer("Hotkey");
            hotkey["ToggleFullscreen"] = "0x1/0/90;;";      // TAB
            hotkey["FastForward"] = "0x1/0/9;;";            // F9
            hotkey["Rewind"] = "0x1/0/8;;";                 // F8
            hotkey["ToggleFastForward"] = "0x1/0/10;;";     // F10
            hotkey["FrameAdvance"] = "0x1/0/11;;";          // F11
            hotkey["CaptureScreenshot"] = "0x1/0/5;;";      // F5
            hotkey["SaveState"] = "0x1/0/1;;";              // F1
            hotkey["LoadState"] = "0x1/0/2;;";              // F2
            hotkey["DecrementStateSlot"] = "0x1/0/3;;";     // F3
            hotkey["IncrementStateSlot"] = "0x1/0/4;;";     // F4
            hotkey["PauseEmulation"] = "0x1/0/6;;";         // F6
            hotkey["QuitEmulator"] = "0x1/0/12;;";          // F12
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
