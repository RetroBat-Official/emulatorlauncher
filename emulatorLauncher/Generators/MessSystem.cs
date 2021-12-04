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
                        
                // generic 'coco' is defaulted to coco3 machine
                new MessSystem("coco"         ,"coco3"     , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),  
                            new MessRomType("hard1", new string[] { "vhd" } ),  
                            new MessRomType("flop1" ),  
                        }),

                new MessSystem("coco1"        ,"coco"     , new MessRomType[] 
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
                        
               new MessSystem("coco2b"        ,"coco2b"     , new MessRomType[] 
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

                new MessSystem("coco3p"         ,"coco3p"     , new MessRomType[] 
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
                        }) { UseFileNameWithoutExtension = true },

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

                new MessSystem("mx1600"      ,"mx1600" , new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" } ), 
                            new MessRomType("cart")
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

                new MessSystem("spectravideo"       ,"svi328", new MessRomType[] 
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" }, "CLOAD\\nRUN\\n", "6"), 
                            new MessRomType("cart")
                        }),

                new MessSystem("scv"          ,"scv"      ,"cart"  ),
                new MessSystem("cdi"          ,"cdimono1" ,"cdrm"  ) { InGameMouse = true },        
                new MessSystem("advision"     ,"advision" ,"cart"  ),
                new MessSystem("attache"      ,"attache"  ,"flop1"  ),
                new MessSystem("ampro"        ,"ampro"    ,"flop1"  ),
                new MessSystem("apc"          ,"apc"      ,"flop1"  ),
                new MessSystem("pv1000"       ,"pv1000"   ,"cart"  ),
                new MessSystem("gamecom"      ,"gamecom"  ,"cart1" ),
                new MessSystem("astrocde"     ,"astrocde" ,"cart"  ),
                new MessSystem("astrocade"    ,"astrocde" ,"cart"  ),
                new MessSystem("vsmile"       ,"vsmile"   ,"cart"  ),
                new MessSystem("gw"           ,""         ,""      ),
                new MessSystem("gameandwatch" ,""         ,""      ),
                new MessSystem("lcdgames"     ,"%romname%",""      ),
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
                new MessSystem("apple2gs"     ,"apple2gs" ,"flop1" ),
                new MessSystem("bk0010"       ,"bk0010"   ,"cass"  )
            };

        public string Name { get; private set; }
        public string MachineName { get; private set; }
        public MessRomType[] RomTypes { get; private set; }
        public bool InGameMouse { get; private set; }
        public bool UseFileNameWithoutExtension { get; private set; }

        #region Public Methods
        public static MessSystem GetMessSystem(string system, string core = null)
        {
            MessSystem messMode = null;

            if (SystemConfig.isOptSet("altmodel") && !string.IsNullOrEmpty(SystemConfig["altmodel"]))
            {
                string altModel = SystemConfig["altmodel"];

                if (messMode == null && system != null)
                    messMode = MessSystems.FirstOrDefault(m => system.Equals(altModel, StringComparison.InvariantCultureIgnoreCase));

                if (messMode == null && system != null)
                    messMode = MessSystems.FirstOrDefault(m => system.Equals(altModel, StringComparison.InvariantCultureIgnoreCase));                
            }

            if (messMode == null && system != null)
                messMode = MessSystems.FirstOrDefault(m => system.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && system != null)
                messMode = MessSystems.FirstOrDefault(m => system.Equals(m.MachineName, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && core != null)
                messMode = MessSystems.FirstOrDefault(m => core.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && core != null)
                messMode = MessSystems.FirstOrDefault(m => core.Equals(m.MachineName, StringComparison.InvariantCultureIgnoreCase));

            return messMode;
        }

        private string EnsureDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch { }

            return path;
        }

        public string GetMameCommandLineArguments(string system, string rom)
        {
            List<string> commandArray = new List<string>();

            // Alternate system for machines that have different configs (ie computers with different hardware)
            if (SystemConfig.isOptSet("altmodel"))
                commandArray.Add(SystemConfig["altmodel"]);
            else if (MachineName == "%romname%")
                commandArray.Add(Path.GetFileNameWithoutExtension(rom));
            else if (!string.IsNullOrEmpty(this.MachineName) && this.MachineName != "%romname%")
                commandArray.Add(MachineName);

            commandArray.Add("-skip_gameinfo");

            
            // rompath
            commandArray.Add("-rompath");
            if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig.GetFullPath("bios")))
            {
                var bios = AppConfig.GetFullPath("bios");

                commandArray.Add(bios + ";" + Path.GetDirectoryName(rom));

                commandArray.Add("-cfg_directory");
                commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "cfg")));

                commandArray.Add("-inipath");
                commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "ini")));

                commandArray.Add("-hashpath");
                commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "hash")));

                commandArray.Add("-artpath");

                string artwork = Path.Combine(Path.GetDirectoryName(rom), "artwork");
                if (Directory.Exists(artwork))
                    artwork = Path.Combine(Path.GetDirectoryName(rom), ".artwork");

                if (Directory.Exists(artwork))
                    commandArray.Add(artwork + ";" + EnsureDirectoryExists(Path.Combine(bios, "mame", "artwork")));
                else
                    commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "artwork")));
            }
            else
                commandArray.Add(Path.GetDirectoryName(rom));
            
            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
            {
                commandArray.Add("-snapshot_directory");
                commandArray.Add(AppConfig.GetFullPath("screenshots"));
            }
             
            // Autostart computer games where applicable
            // Generic boot if only one type is available
            var autoRunCommand = SystemConfig.isOptSet("altromtype") ? GetAutoBootForRomType(SystemConfig["altromtype"]) : GetAutoBoot(rom);
            if (autoRunCommand != null)
                commandArray.AddRange(autoRunCommand.Arguments);
            
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

            if (MachineName != "%romname%")
                commandArray.Add(this.UseFileNameWithoutExtension ? Path.GetFileNameWithoutExtension(rom) : rom);

            return string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());
        }
        #endregion

        #region Private methods
        private MessSystem(string name, string sysName = "", string romType = "")
        {
            Name = name;
            MachineName = sysName;
            RomTypes = new MessRomType[] { new MessRomType(romType) };
        }

        private MessSystem(string name, string sysName, MessRomType[] romType)
        {
            Name = name;
            MachineName = sysName;
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

        MessAutoBoot GetAutoBootForRomType(string romType)
        {
            if (string.IsNullOrEmpty(romType))
                return null;

            return RomTypes.Where(t => romType.Equals(t.Type, StringComparison.InvariantCultureIgnoreCase)).Select(t => t.AutoBoot).FirstOrDefault();         
        }

        static ConfigFile AppConfig { get { return Program.AppConfig; } }
        static ConfigFile SystemConfig { get { return Program.SystemConfig; } }
        #endregion
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

        public string[] Extensions { get; private set; }
        public string Type { get; private set; }
        public MessAutoBoot AutoBoot { get; private set; }
    }

    class MessAutoBoot
    {
        public MessAutoBoot(string command, string delay)
        {
            AutoRunCommand = command;
            AutoBootDelay = delay;
        }

        public string AutoRunCommand { get; private set; }
        public string AutoBootDelay { get; private set; }

        public string[] Arguments
        {
            get
            {
                List<string> ret = new List<string>();

                if (!string.IsNullOrEmpty(AutoBootDelay))
                    ret.AddRange(new string[] { "-autoboot_delay", AutoBootDelay });

                if (!string.IsNullOrEmpty(AutoRunCommand))
                    ret.AddRange(new string[] { "-autoboot_command", AutoRunCommand });

                return ret.ToArray();
            }
        }
    }
}
