using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace emulatorLauncher
{
    class MessSystem
    {
        static MessSystem[] MessSystems = new MessSystem[]
            {                
                new MessSystem("bbcmicro"     ,"bbcb"     , new MessRomType[] 
                        { 
                            new MessRomType("flop1", new string[] { "adl", "ssd", "dsd", "ad", "img", "adf" }, "*cat\\n*exec !boot\\n", "3"),
                            new MessRomType("cass", null, "*tape\\nchain\\\"\\\"\\n", "2") 
                        }),
                new MessSystem("fmtowns"      ,"fmtownsux" , new MessRomType[] 
                        { 
                            new MessRomType("cdrom", new string[] { "iso", "cue" }),
                            new MessRomType("flop1")
                        }),                                        
                new MessSystem("fm7"          ,"fm7"      , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "t77", "wav" }, "LOADM\\\"\\\",,R\\n", "5"),
                            new MessRomType("flop1")
                        }),

                new MessSystem("camplynx"     ,"lynx48k"  , new MessRomType[] 
                        { 
                            new MessRomType("cass", null, "mload\\\"\\\"\\n") 
                        }),

                new MessSystem("lcdgames"     ,""         ,""      ),
                new MessSystem("gameandwatch" ,""         ,""      ),
                new MessSystem("cdi"          ,"cdimono1" ,"cdrm"  ),
                new MessSystem("advision"     ,"advision" ,"cart"  ),
                new MessSystem("tvgames"      ,""         ,""      ),
                new MessSystem("megaduck"     ,"megaduck" ,"cart"  ),
                new MessSystem("crvision"     ,"crvision" ,"cart"  ),
                new MessSystem("gamate"       ,"gamate"   ,"cart"  ),
                new MessSystem("pv1000"       ,"pv1000"   ,"cart"  ),
                new MessSystem("gamecom"      ,"gamecom"  ,"cart1" ),
                new MessSystem("xegs"         ,"xegs"     ,"cart"  ),
                new MessSystem("gamepock"     ,"gamepock" ,"cart"  ),
                new MessSystem("aarch"        ,"aa310"    ,"flop"  ),
                new MessSystem("atom"         ,"atom"     ,"cass"  ),
                new MessSystem("apfm1000"     ,"apfm1000" ,"cart"  ),
                new MessSystem("adam"         ,"adam"     ,"cass1" ),
                new MessSystem("arcadia"      ,"arcadia"  ,"cart"  ),
                new MessSystem("supracan"     ,"supracan" ,"cart"  ),
                new MessSystem("gmaster"      ,"gmaster"  ,"cart"  ),
                new MessSystem("astrocde"     ,"astrocde" ,"cart"  ),
                new MessSystem("ti99"         ,"ti99_4a"  ,"cart"  ),
                new MessSystem("tutor"        ,"tutor"    ,"cart"  ),
                new MessSystem("coco"         ,"coco"     ,"cart"  ),
                new MessSystem("socrates"     ,"socrates" ,"cart"  ),

            };


        public static MessSystem GetMessSystem(string system, string core = null)
        {
            MessSystem messMode = null;

            if (messMode == null && system != null)
                messMode = MessSystems.FirstOrDefault(m => system.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && system != null)
                messMode = MessSystems.FirstOrDefault(m => system.Equals(m.SysName, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && core != null)
                messMode = MessSystems.FirstOrDefault(m => core.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && core != null)
                messMode = MessSystems.FirstOrDefault(m => core.Equals(m.SysName, StringComparison.InvariantCultureIgnoreCase));

            return messMode;
        }

        public string GetMameCommandLineArguments(string system, string rom)
        {
            List<string> commandArray = new List<string>();

            commandArray.Add("-skip_gameinfo");

            // rompath
            commandArray.Add("-rompath");
            if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig["bios"]))
                commandArray.Add(AppConfig.GetFullPath("bios") + ";" + Path.GetDirectoryName(rom));
            else
                commandArray.Add(Path.GetDirectoryName(rom));

            // Alternate system for machines that have different configs (ie computers with different hardware)
            if (SystemConfig.isOptSet("altmodel"))
                commandArray.Insert(0, SystemConfig["altmodel"]);
            else
                commandArray.Insert(0, this.SysName);

            if (system == "bbcmicro")
            {
                // bbc has different boots for floppy & cassette, no special boot for carts
                if (SystemConfig.isOptSet("altromtype"))
                {
                    if (SystemConfig["altromtype"] == "cass")
                        commandArray.AddRange(new string[] { "-autoboot_delay", "2", "-autoboot_command", "*tape\\nchain\\\"\\\"\\n" });
                    else if (SystemConfig["altromtype"].StartsWith("flop"))
                        commandArray.AddRange(new string[] { "-autoboot_delay", "3", "-autoboot_command", "*cat\\n*exec !boot\\n" });
                }
                else
                {
                    var autoRunCommand = this.GetAutoBoot(rom);
                    if (autoRunCommand != null)
                        commandArray.AddRange(autoRunCommand.Arguments);
                }
            }
            else if (system == "fm7" && SystemConfig.isOptSet("altromtype") && SystemConfig["altromtype"] == "cass")
            {
                // fm7 boots floppies, needs cassette loading
                commandArray.AddRange(new string[] { "-autoboot_delay", "5", "-autoboot_command", "LOADM”“,,R\\n" });
            }
            else
            {
                // Autostart computer games where applicable
                // Generic boot if only one type is available
                var autoRunCommand = this.GetAutoBoot(rom);
                if (autoRunCommand != null)
                    commandArray.AddRange(autoRunCommand.Arguments);
            }
            /*
          if (system == "ti99")
          {
              commandArray.Add("-ioport");
              commandArray.Add("peb");
              
              commandArray.Add("-ioport:peb:slot3");
              commandArray.Add("speech");

              commandArray.Add(Path.GetFileNameWithoutExtension(rom));
          }*/

            // Alternate ROM type for systems with mutiple media (ie cassette & floppy)
            if (SystemConfig.isOptSet("altromtype"))
                commandArray.Add("-" + SystemConfig["altromtype"]);
            else
            {
                if (Directory.Exists(rom))
                {
                    var cueFile = Directory.GetFiles(rom, "*.cue").FirstOrDefault();
                    if (!string.IsNullOrEmpty(cueFile))
                        rom = cueFile;
                }

                var romType = this.GetRomType(rom);
                if (!string.IsNullOrEmpty(romType))
                    commandArray.Add("-" + romType);
            }

            if (system == "ti99")
                commandArray.Add(Path.GetFileNameWithoutExtension(rom));
            else
            {
                // Use the full filename for MESS ROMs
                commandArray.Add(rom);
            }

            return string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());
        }

        private MessSystem(string name, string sysName = "", string romType = "")
        {
            Name = name;
            SysName = sysName;
            RomTypes = new MessRomType[] { new MessRomType(romType) };
        }

        private MessSystem(string name, string sysName, MessRomType[] romType)
        {
            Name = name;
            SysName = sysName;
            RomTypes = romType;
        }

        public string Name { get; private set; }
        public string SysName { get; private set; }
        public MessRomType[] RomTypes { get; private set; }

        string GetRomType(string rom)
        {
            var ext = Path.GetExtension(rom).ToLowerInvariant();
            if (ext.Length > 0)
                ext = ext.Substring(1);

            if (ext == "zip")
            {
                var e = Tools.Misc.GetZipEntries(rom);
                if (e.Length == 1)
                    ext = Path.GetExtension(e[0]).ToLowerInvariant().Substring(1);
            }

            var ret = RomTypes.Where(t => t.Extensions != null && t.Extensions.Contains(ext)).Select(t => t.Type).FirstOrDefault();
            if (ret != null)
                return ret;

            return RomTypes.Where(t => t.Extensions == null).Select(t => t.Type).FirstOrDefault();
        }

        MessAutoBoot GetAutoBoot(string rom)
        {
            var ext = Path.GetExtension(rom).ToLowerInvariant();
            if (ext.Length > 0)
                ext = ext.Substring(1);

            if (ext == "zip")
            {
                var e = Tools.Misc.GetZipEntries(rom);
                if (e.Length == 1)
                    ext = Path.GetExtension(e[0]).ToLowerInvariant().Substring(1);
            }

            var ret = RomTypes.Where(t => t.Extensions != null && t.Extensions.Contains(ext)).Select(t => t.AutoBoot).FirstOrDefault();
            if (ret != null)
                return ret;

            return RomTypes.Where(t => t.Extensions == null).Select(t => t.AutoBoot).FirstOrDefault();
        }

        ConfigFile AppConfig { get { return Program.AppConfig; } }
        ConfigFile SystemConfig { get { return Program.SystemConfig; } }
    };

    class MessRomType
    {
        public MessRomType(string type, string[] extensions = null, string autoRun = null, string autoRunDelay = "3")
        {
            Type = type;
            Extensions = extensions;
            
            if (autoRun != null)
                AutoBoot = new MessAutoBoot(autoRun, autoRunDelay);
        }

        public string[] Extensions { get; set; }
        public string Type { get; set; }
        public MessAutoBoot AutoBoot { get; set; }
    }

    class MessAutoBoot
    {
        public MessAutoBoot(string command, string delay)
        {
            AutoRunCommand = command;
            AutoBootDelay = delay;
        }

        public string AutoRunCommand { get; set; }
        public string AutoBootDelay { get; set; }

        public string[] Arguments
        {
            get
            {
                return new string[] { "-autoboot_delay", AutoBootDelay, "-autoboot_command", AutoRunCommand };
            }
        }
    }
}
