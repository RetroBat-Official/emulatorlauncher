using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class HypseusGenerator : DaphneGenerator
    {
        public HypseusGenerator() { _executableName = "hypseus"; }
    }

    class DaphneGenerator : Generator
    {
        public DaphneGenerator()
        {
            _executableName = "daphne";
        }

        protected virtual void UpdateCommandline(List<string> commandArray)
        {
            if (_executableName == "daphne")
            {
                if (!SystemConfig.isOptSet("smooth"))
                    commandArray.Add("-nolinear_scale");

                if (SystemConfig["ratio"] != "4/3")
                    commandArray.Add("-ignore_aspect_ratio");

                return;
            }

            // hypseus
            if (SystemConfig["ratio"] == "16/9")
                commandArray.Add("-ignore_aspect_ratio");
            else
                commandArray.Add("-force_aspect_ratio");
        }

        protected string _executableName;
        private string _daphneHomedir;
        private string _symLink;

        static string FindFile(string dir, string pattern, Predicate<string> predicate)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir, pattern))
                    if (predicate(f))
                        return f;

                foreach (string d in Directory.GetDirectories(dir))
                {
                    string ret = FindFile(d, pattern, predicate);
                    if (ret != null)
                        return ret;
                }                
            }
            catch
            {
                
            }

            return null;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            rom = this.TryUnZipGameIfNeeded(system, rom);

            string romName = Path.GetFileNameWithoutExtension(rom);

            string commandsFile = rom + "/" + romName + ".commands";

            string singeFile = rom + "/" + romName + ".singe";
            if (!File.Exists(singeFile))
                singeFile = FindFile(rom, "*.singe", f => Path.GetFileNameWithoutExtension(f).StartsWith(romName));

            string frameFile = rom + "/" + romName + ".txt";
            if (File.Exists(singeFile))
            {
                _executableName = "hypseus";
                if (!File.Exists(frameFile))
                {
                    frameFile = FindFile(rom, "*.txt", f => Path.GetFileNameWithoutExtension(f).StartsWith(romName));
                    if (frameFile == null)
                        frameFile = FindFile(rom, "*.mp4", f => Path.GetFileNameWithoutExtension(f).StartsWith(romName));
                }
            }                        

            string emulatorPath = AppConfig.GetFullPath(_executableName);
            string exe = Path.Combine(emulatorPath, _executableName + ".exe");
            if (!File.Exists(exe))
            {
                ExitCode = ExitCodes.EmulatorNotInstalled;
                return null;
            }

            List<string> commandArray = new List<string>();

            // extension used .daphne and the file to start the game is in the folder .daphne with the extension .txt
        
            string daphneDatadir = emulatorPath;
            _daphneHomedir = Path.GetDirectoryName(rom);

            if (File.Exists(singeFile) && _executableName == "hypseus")
            {
                commandArray.AddRange(new string[]                       
                   {                        
                        "singe", 
                        "vldp", 
                        "-retropath", // Requires the CreateSymbolicLink
                        "-framefile", frameFile, 
                        "-script", singeFile, 
                        "-manymouse", 
                        "-homedir", _daphneHomedir
                    });
                
                string directoryName = Path.GetFileName(rom);

                _symLink = Path.Combine(emulatorPath, directoryName);

                try
                {
                    if (Directory.Exists(_symLink))
                        Directory.Delete(_symLink);
                }
                catch { }

                FileTools.CreateSymlink(_symLink, rom, true);              
            }
            else
            {
                 commandArray.AddRange(new string[]                       
                   {                                
                       romName, 
                       "vldp", 
                        "-framefile", frameFile, 
                        "-useoverlaysb", "2", 
                        "-homedir", _daphneHomedir
                   });
            }

            if (Features.IsSupported("overlay") && SystemConfig.getOptBoolean("overlay"))
            {
                commandArray.Add("-useoverlaysb");
                commandArray.Add("2");
            }

            commandArray.Add("-x");
            commandArray.Add((resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString());

            commandArray.Add("-y");
            commandArray.Add((resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString());

            commandArray.Add("-fullscreen");
            commandArray.Add("-opengl");            
            commandArray.Add("-fastboot");
            
            UpdateCommandline(commandArray);       
            
            // The folder may have a file with the game name and .commands with extra arguments to run the game.
            if (_executableName == "daphne" && File.Exists(commandsFile))
            {
                string[] file = File.ReadAllText(commandsFile).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < file.Length; i++)
                {
                    string s = file[i];

                    if (s == "singe" && commandArray[0] != "singe")
                    {
                        commandArray[0] = "singe";
                        continue;
                    }

                    if (s == romName || s == "singe" || s == "vdlp" || s == "-fullscreen" || 
                        s == "-opengl" || s == "-fastboot" || s == "-retropath" || s == "-manymouse")
                        continue;

                    if (s == "-x" || s == "-y" || s == "-framefile" || s == "-script" || s == "script" || s == "-useoverlaysb" || s == "-homedir" || s == "-datadir")
                    {
                        i++;
                        continue;
                    }

                    commandArray.Add(s);
                }
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = emulatorPath,
            };
        }

        public override void Cleanup()
        {
            base.Cleanup();

            try
            {
                if (!string.IsNullOrEmpty(_symLink) && Directory.Exists(_symLink))
                    Directory.Delete(_symLink);

                string ram = Path.Combine(_daphneHomedir, "ram");
                if (Directory.Exists(ram))
                    new DirectoryInfo(ram).Delete(true);

                string frameFile = Path.Combine(_daphneHomedir, "framefile");
                if (Directory.Exists(frameFile))
                    new DirectoryInfo(frameFile).Delete(true);     
            }
            catch { }
        }
    }
}