using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class HypseusGenerator : DaphneGeneratorBase
    {
        protected override string ExecutableName { get { return "hypseus"; } }

        protected override void UpdateCommandline(List<string> commandArray)
        {
            if (!SystemConfig.isOptSet("smooth"))
                commandArray.Add("-nolinear_scale");
            
            if (SystemConfig["ratio"] != "16/9")
                commandArray.Add("-force_aspect_ratio");
        }
    }

    class DaphneGenerator : DaphneGeneratorBase
    {
        protected override string ExecutableName { get { return "daphne"; } }

        protected override void UpdateCommandline(List<string> commandArray)
        {
            if (SystemConfig["ratio"] == "16/9")
                commandArray.Add("-ignore_aspect_ratio");                
        }
    }

    abstract class DaphneGeneratorBase : Generator
    {
        protected abstract string ExecutableName { get; }
        protected abstract void UpdateCommandline(List<string> commandArray);

        private string _daphneHomedir;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string emulatorPath = AppConfig.GetFullPath(ExecutableName);

            string exe = Path.Combine(emulatorPath, ExecutableName + ".exe");
            if (!File.Exists(exe))
                return null;

            List<string> commandArray = new List<string>();

            // extension used .daphne and the file to start the game is in the folder .daphne with the extension .txt
            string romName = Path.GetFileNameWithoutExtension(rom);
            string frameFile = rom + "/" + romName + ".txt";
            string commandsFile = rom + "/" + romName + ".commands";

            string daphneDatadir = emulatorPath;
            _daphneHomedir = Path.GetDirectoryName(rom);
                                  
            commandArray.Add(romName);
            commandArray.Add("vldp");
            commandArray.Add("-framefile");
            commandArray.Add(frameFile);

            if (SystemConfig["overlay"] == "1")
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
//            commandArray.Add("-datadir");
//            commandArray.Add(daphneDatadir);
            commandArray.Add("-homedir");
            commandArray.Add(_daphneHomedir);

            UpdateCommandline(commandArray);       
            
            // The folder may have a file with the game name and .commands with extra arguments to run the game.
            if (File.Exists(commandsFile))
            {
                string[] file = File.ReadAllText(commandsFile).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < file.Length; i++)
                {
                    string s = file[i];
                    if (s == "-x" || s == "-y")
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