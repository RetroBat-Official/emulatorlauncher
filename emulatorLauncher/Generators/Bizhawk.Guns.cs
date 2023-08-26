using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        // List of cores with dedicated configuration
        // To be updated each time a core is correctly / succesfully configured
        static List<string> coreGunConfig = new List<string>()
        {
            "xxx",
        };

        /// <summary>
        /// Injects guns settings
        /// </summary>
        /// <param name="config"></param>
        /// <param name="deviceType"></param>
        /// <param name="playerIndex"></param>
        private void SetupLightGuns(ConfigFile config, string deviceType, string core, int playerIndex = 1)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;
        }

        /// <summary>
        /// Dedicated core mappings for lightgun games
        private void ConfigureGunsCore(ConfigFile config, int playerIndex, string core, string deviceType, bool multigun = false, bool guninvert = false, bool useOneGun = false)
        {
         
        }

        private string GetcoreMouseButton (string core, bool guninvert, string mbtn)
        {
            string ret = "nul";

            return ret;
        }
    }
}