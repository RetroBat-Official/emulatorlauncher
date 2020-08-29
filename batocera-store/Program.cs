using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace batocera_store
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "install")
            {
                Thread.Sleep(1000);
                Console.WriteLine("OK");
            }
            else if (args.Length > 0 && args[0] == "list")
                Console.WriteLine(Properties.Resources.batocera_store);

            return 0;
        }
    }
}
