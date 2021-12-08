using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using emulatorLauncher;
using System.IO;

namespace batocera_store
{
    class Program
    {
        static int Main(string[] args)
        {
            AppConfig = ConfigFile.FromFile(Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "emulatorLauncher.cfg"));
            AppConfig.ImportOverrides(ConfigFile.FromArguments(args));

            if (args.Length > 0 && args[0] == "install")
            {
                Thread.Sleep(1000);
                Console.WriteLine("OK");
            }
            else if (args.Length > 0 && args[0] == "list")
                Console.WriteLine(Properties.Resources.batocera_store);

            return 0;
        }

        public static ConfigFile AppConfig { get; private set; }
    }
}
