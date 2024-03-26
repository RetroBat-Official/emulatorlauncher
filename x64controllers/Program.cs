using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Security;

namespace x64controllers
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string version = null;
            string hints = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-version" && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    version = args[i + 1];
                    i++;
                }
                else if (args[i] == "-hints" && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    hints = args[i + 1];
                    if (string.IsNullOrEmpty(hints))
                        hints = null;

                    i++;
                }
            }

            SdlVersion sdlVersion = SdlVersion.Unknown;
            if (!Enum.TryParse<SdlVersion>(version, out sdlVersion))
                return;

            if (sdlVersion == SdlVersion.Unknown || sdlVersion == SdlVersion.SDL2_0_X)
                return;

            var ss = SdlDllControllersMapping.FromSdlVersion(sdlVersion, true, hints);
            if (ss == null || ss.Mapping == null)
                return;

            if (ss.Mapping.Any())
            {
                Console.WriteLine("<mappings>");
                
                foreach (var item in ss.Mapping)
                    Console.WriteLine("<mapping path=\"" + SecurityElement.Escape(item.Key) + "\" guid=\"" + item.Value.ToString() + "\"/>");

                Console.WriteLine("</mappings>");
            }
        }
    }
}
