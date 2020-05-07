using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class Model3Generator : Generator
    {        
        private string destFile;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("supermodel");

            string exe = Path.Combine(path, "supermodel.exe");            
            if (!File.Exists(exe))
                return null;

            List<string> args = new List<string>();

            if (resolution != null)
                args.Add("-res=" + resolution.Width + "," + resolution.Height);
              
            args.Add("-fullscreen");
            args.Add("-wide-screen");

            // if (SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "1")
                args.Add("-stretch");
            
            args.Add("-vsync");
            args.Add("\""+rom+"\"");

            // -res=1920,1080 -fullscreen -wide-screen -stretch -vsync %ROM%

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(" ", args),
                WorkingDirectory = path,                
            };            
        }

        public override void Cleanup()
        {
            if (destFile != null && File.Exists(destFile))
                File.Delete(destFile);
        }
    }
}
