using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class DosBoxGenerator : Generator
    {
        public DosBoxGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string batFile = Path.Combine(rom, "dosbox.bat");

            string gameConfFile =  Path.Combine(rom, "dosbox.cfg");

            string ext = Path.GetExtension(rom).ToLower();
            if ((ext == ".dosbox" || ext == ".dos" || ext == ".pc" || ext == ".conf") && File.Exists(rom))
                gameConfFile = rom;

            string exeFile = Path.Combine(rom, "dosbox.exe");
            if (File.Exists(exeFile))
            {
                return new ProcessStartInfo()
                {
                    FileName = exeFile,
                    WorkingDirectory = rom,
                    Arguments = "-noconsole -exit"
                };                
            }

            string path = AppConfig.GetFullPath("dosbox");
            if (string.IsNullOrEmpty(path))
                return null;
                        
            string exe = Path.Combine(path, "dosbox.exe");
            if (!File.Exists(exe))
                return null;

            List<string> commandArray = new List<string>
            {
                "\"" + batFile + "\""
            };

            if (File.Exists(gameConfFile))
            {
                commandArray.Add("-conf");
                commandArray.Add("\"" + gameConfFile + "\"");
            }
            else
            {
                commandArray.Add("-conf");
                commandArray.Add("\"" + Path.Combine(path, "dosbox.conf") + "\"");
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args + " -fullscreen -noconsole -exit -c \"set ROOT=" + rom + "\" ",
            };
            /*
            commandArray = [batoceraFiles.batoceraBins[system.config['emulator']], 
			    "-userconf", 
			    "-exit", 
			    """{}""".format(batFile),
			    "-c", """set ROOT={}""".format(gameDir)]
             * 
            if os.path.isfile(gameConfFile):
                commandArray.append("-conf")
                commandArray.append("""{}""".format(gameConfFile))
            else:
                commandArray.append("-conf")
                commandArray.append("""{}""".format(batoceraFiles.dosboxConfig))

            return Command.Command(array=commandArray, env={"SDL_VIDEO_GL_DRIVER":"/usr/lib/libGLESv2.so"})*/
        }
    }
}
