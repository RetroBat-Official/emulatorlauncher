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
                            new MessRomType("cass", new string[] { "wav", "csw", "uef" }, "*tape\\nchain\\\"\\\"\\n", "2"),
                            new MessRomType("rom1", new string[] { "rom", "bin" }),
                            new MessRomType("flop1", null, "*cat\\n*exec !boot\\n", "3" )
                        }),

                new MessSystem("fmtowns"      ,"fmtownsux" , new MessRomType[] 
                        { 
                            new MessRomType("cdrom", new string[] { "iso", "cue", "chd", "toc", "nrg", "gdi", "cdr" }),
                            new MessRomType("hard1", new string[] { "hd", "hdv", "2mg", "hdi" }),
                            new MessRomType("memc", new string[] { "icm" }),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk  .bin */ )
                        }) { InGameMouse = true },         
                               
                new MessSystem("fm7"          ,"fm7"      , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "t77", "wav" }, "LOADM\\\"\\\",,R\\n", "5"),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk */ )
                        }),

                new MessSystem("adam"         ,"adam"     , new MessRomType[] 
                        { 
                            new MessRomType("cart1", new string[] { "bin", "rom", ".col" } ),
                            new MessRomType("cass1", new string[] { "wav", "ddp" } ),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk */ )
                        }),

                new MessSystem("coco"         ,"coco"     , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("cart" )
                        }),
                                        
               new MessSystem("coco2"         ,"coco2"     , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),  
                            new MessRomType("hard1", new string[] { "vhd" } ),  
                            new MessRomType("flop1" ),  
                        }),

               new MessSystem("coco3"         ,"coco3"     , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),  
                            new MessRomType("hard1", new string[] { "vhd" } ),  
                            new MessRomType("flop1" ),  
                        }),
                                        
                new MessSystem("ti99"         ,"ti99_4a"  , new MessRomType[] 
                        { 
                            new MessRomType("cass1", new string[] { "wav" } ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("atom"         ,"atom"     , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "tap", "cdw", "uef" } ), 
                            new MessRomType("cart", new string[] { "bin", "rom" } ),  
                            new MessRomType("quik", new string[] { "atm" } ),  
                            new MessRomType("flop1" ),  
                        }),

                new MessSystem("camplynx"     ,"lynx48k"  , new MessRomType[] 
                        { 
                            new MessRomType("cass", null, "mload\\\"\\\"\\n") 
                        }),
                                        
                new MessSystem("x1"           ,"x1" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "bin", "rom" } ), 
                            new MessRomType("cass", new string[] { "wav", "tap" } ), 
                            new MessRomType("flop1")
                        }),

                new MessSystem("dragon32"     ,"dragon32" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "ccc", "rom" } ), 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("flop1")
                        }),

                new MessSystem("dragon64"     ,"dragon64" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "ccc", "rom" } ), 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("flop1")
                        }),

                new MessSystem("electron"     ,"electron" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "bin", "rom" } ), 
                            new MessRomType("cass", new string[] { "wav", "csw", "uef" } ), 
                            new MessRomType("flop")
                        }),

                new MessSystem("c64"          ,"c64" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "80", "a0", "e0", "crt" } ), 
                            new MessRomType("cass", new string[] { "wav", "tap" } ), 
                            new MessRomType("quik", new string[] { "p00", "prg", "t64" } ), 
                            new MessRomType("flop")
                        }),
                          
                new MessSystem("xegs"         ,"xegs", new MessRomType[] 
                        { 
                            new MessRomType("flop1", new string[] { "atr", "dsk", "xfd" } ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("alice32"      ,"alice32", new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas", "c10", "k7" } ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("alice90"      ,"alice90", new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas", "c10", "k7" } ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("xegs"         ,"xegs", new MessRomType[] 
                        { 
                            new MessRomType("flop1", new string[] { "atr", "dsk", "xfd" } ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("aquarius"    ,"aquarius", new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "caq" } ), 
                            new MessRomType("cart1")
                        }),

                new MessSystem("tg16"         ,"tg16", new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "pce", "bin" } ), 
                            new MessRomType("cdrm")
                        }),

                new MessSystem("scv"          ,"scv"      ,"cart"  ),
                new MessSystem("cdi"          ,"cdimono1" ,"cdrm"  ) { InGameMouse = true },        
                new MessSystem("advision"     ,"advision" ,"cart"  ),
                new MessSystem("attache"      ,"attache"  , "flop1"  ),
                new MessSystem("ampro"        ,"ampro"    ,"flop1"  ),
                new MessSystem("apc"          ,"apc"      ,"flop1"  ),
                new MessSystem("pv1000"       ,"pv1000"   ,"cart"  ),
                new MessSystem("gamecom"      ,"gamecom"  ,"cart1" ),
                new MessSystem("astrocde"     ,"astrocde" ,"cart"  ),
                new MessSystem("astrocade"    ,"astrocde" ,"cart"  ),
                new MessSystem("vsmile"       ,"vsmile"   ,"cart"  ),
                new MessSystem("gameandwatch" ,""         ,""      ),
                new MessSystem("lcdgames"     ,""         ,""      ),
                new MessSystem("tvgames"      ,""         ,""      ),
                new MessSystem("megaduck"     ,"megaduck" ,"cart"  ),
                new MessSystem("crvision"     ,"crvision" ,"cart"  ),
                new MessSystem("gamate"       ,"gamate"   ,"cart"  ),              
                new MessSystem("gamepock"     ,"gamepock" ,"cart"  ),
                new MessSystem("aarch"        ,"aa310"    ,"flop"  ),
                new MessSystem("apfm1000"     ,"apfm1000" ,"cart"  ),                
                new MessSystem("arcadia"      ,"arcadia"  ,"cart"  ),
                new MessSystem("supracan"     ,"supracan" ,"cart"  ),
                new MessSystem("gmaster"      ,"gmaster"  ,"cart"  ),
                new MessSystem("tutor"        ,"tutor"    ,"cart"  ),               
                new MessSystem("socrates"     ,"socrates" ,"cart"  ),
                new MessSystem("a2600"        ,"a2600"    ,"cart"  ),
                new MessSystem("nes"          ,"nes"      ,"cart"  ),
                new MessSystem("snes"         ,"snes"     ,"cart"  ),
                new MessSystem("gbcolor"      ,"gbcolor"  ,"cart"  ),
                new MessSystem("gameboy"      ,"gameboy"  ,"cart"  ),
                new MessSystem("apple2gs"     ,"apple2gs" ,"flop1"  ),

            };

        public string Name { get; private set; }
        public string SysName { get; private set; }
        public MessRomType[] RomTypes { get; private set; }
        public bool InGameMouse { get; set; }

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
            {
                commandArray.Add(AppConfig.GetFullPath("bios") + ";" + Path.GetDirectoryName(rom));

                commandArray.Add("-cfg_directory");
                commandArray.Add(Path.Combine(AppConfig.GetFullPath("bios"), "mame", "cfg"));
            }
            else
                commandArray.Add(Path.GetDirectoryName(rom));

            // Alternate system for machines that have different configs (ie computers with different hardware)
            if (SystemConfig.isOptSet("altmodel"))
                commandArray.Insert(0, SystemConfig["altmodel"]);
            else if (!string.IsNullOrEmpty(this.SysName))
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
