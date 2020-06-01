using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class Model3Generator : Generator
    {        
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("supermodel");

            string exe = Path.Combine(path, "supermodel.exe");            
            if (!File.Exists(exe))
                return null;

            List<string> args = new List<string>();

            if (resolution != null)
                args.Add("-res=" + resolution.Width + "," + resolution.Height);
            else
                args.Add("-res=" + Screen.PrimaryScreen.Bounds.Width + "," + Screen.PrimaryScreen.Bounds.Height);
              
            args.Add("-fullscreen");
            args.Add("-wide-screen");

            if (!SystemConfig.isOptSet("ratio") || SystemConfig["ratio"] != "4/3")
                args.Add("-stretch");
                            
            if (SystemConfig["VSync"] != "false")
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
    }
}
