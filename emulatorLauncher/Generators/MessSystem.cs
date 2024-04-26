using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    class MessSystem
    {
        // mame -listfull nom*
        // mame -listmedias nom*

        static readonly MessSystem[] MessSystems = new MessSystem[]
            {
                // IN RETROBAT

                // ADAM
                new MessSystem("adam"         ,"adam"     , new MessRomType[]
                        {
                            new MessRomType("cart1", new string[] { "bin", "rom", "col" } ),
                            new MessRomType("cass1", new string[] { "wav", "ddp" } ),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk */ )
                        }),

                // Apple II
                new MessSystem("apple2"       ,"apple2ee"      , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav" } ),
                            new MessRomType("flop1" /* .mfi  .dfi  .dsk  .do   .po   .rti  .edd  .woz  .nib */ ),
                        }),

                new MessSystem("apple2e"       ,"apple2e"      , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav" } ),
                            new MessRomType("flop1" /* .mfi  .dfi  .dsk  .do   .po   .rti  .edd  .woz  .nib */ ),
                        }),

                new MessSystem("apple2p"       ,"apple2p"      , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav" } ),
                            new MessRomType("flop1" /* .mfi  .dfi  .dsk  .do   .po   .rti  .edd  .woz  .nib */ ),
                        }),

                // Apple 2 GS
                new MessSystem("apple2gs"     ,"apple2gs" ,new MessRomType[]
                    {
                        new MessRomType("flop3", new string[] { "hfe", "mfm", "td0", "imd", "d77", "d88", "1dd", "cqm", "cqi", "ima", "img", "ufi", "360", "ipf", "dc42", "moof", "2mg", "woz" } ),
                        new MessRomType("flop1" /* .mfi  .dfi  .dsk  .do   .po   .rti  .edd  .nib */ ),
                    }),

                // Atom;atom;flop1;'*DOS\n*DIR\n*CAT\n*RUN"'
                new MessSystem("atom"         ,"atom"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "tap", "cdw", "uef" } ),
                            new MessRomType("cart", new string[] { "bin", "rom" } ),
                            new MessRomType("quik", new string[] { "atm" } ),
                            new MessRomType("flop1", null, "*DOS\\n*DIR\\n*CAT\\n" ),  // *CAT\\n - "*DOS\\n*DIR\\n*CAT\\n*RUN\\\"runme\\\"\\n"
                        }),

                // BBC MICRO
                new MessSystem("bbcmicro"     ,"bbcb"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "csw", "uef" }, "*tape\\nchain\\\"\\\"\\n", "2"),
                            new MessRomType("rom1", new string[] { "rom", "bin" }),
                            new MessRomType("flop1", null, "*cat\\n*exec !boot\\n", "3" )
                        }),

                new MessSystem("bbcm"     ,"bbcm"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "csw", "uef" }, "*tape\\nchain\\\"\\\"\\n", "2"),
                            new MessRomType("cart1", new string[] { "rom", "bin" }),
                            new MessRomType("flop1", null, "*cat\\n*exec !boot\\n", "3" )
                        }),

                new MessSystem("bbcm512"     ,"bbcm512"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "csw", "uef" }, "*tape\\nchain\\\"\\\"\\n", "2"),
                            new MessRomType("cart1", new string[] { "rom", "bin" }),
                            new MessRomType("flop1", null, "*cat\\n*exec !boot\\n", "3" )
                        }),

                new MessSystem("bbcmc"     ,"bbcmc"     , new MessRomType[]
                        {
                            new MessRomType("rom3", new string[] { "rom", "bin" }),
                            new MessRomType("flop1", null, "*cat\\n*exec !boot\\n", "3" )
                        }),

                // Camputers LYNX "mload\\\"\\\"\\n" (MLOAD"gamename")
                new MessSystem("camplynx"     ,"lynx48k"  , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "tap" })
                        }),

                new MessSystem("lynx96k"     ,"lynx96k"  , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "tap" }),
                            new MessRomType("flop1" )
                        }),

                new MessSystem("lynx128k"     ,"lynx128k"  , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "tap" }),
                            new MessRomType("flop1" )
                        }),

                // Color Computer (default to coco3)
                new MessSystem("coco"         ,"coco3"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

                new MessSystem("cocoh"         ,"cocoh"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

                new MessSystem("coco1"        ,"coco"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

               new MessSystem("coco2b"        ,"coco2b"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

               new MessSystem("coco2bh"        ,"coco2bh"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

                new MessSystem("coco3p"         ,"coco3p"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

                new MessSystem("coco3h"         ,"coco3h"     , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav", "cas", "flac" } ),
                            new MessRomType("cart", new string[] { "ccc", "rom" } ),
                            new MessRomType("hard1", new string[] { "vhd" } ),
                            new MessRomType("flop1" ),
                        }),

                // CreatiVision
                new MessSystem("crvision"     ,"crvision" ,new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav" }),
                            new MessRomType("cart" /* .bin  .rom */)
                        }),

                // Fujitsu FM-7
                new MessSystem("fm7"          ,"fm7"      , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "t77", "wav" }, "LOADM\\\"\\\",,R\\n", "5"),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk */ )
                        }),

                new MessSystem("fm77av"          ,"fm77av"      , new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "t77", "wav" }, "LOADM\\\"\\\",,R\\n", "5"),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk */ )
                        }),

                // Fujitsu FM-TOWNS
                new MessSystem("fmtowns"      ,"fmtowns" , new MessRomType[]
                        {
                            new MessRomType("cdrm", new string[] { "iso", "cue", "chd", "toc", "nrg", "gdi", "cdr" }),
                            new MessRomType("memc", new string[] { "icm" }),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk  .bin */ )
                        }) { InGameMouse = true },

                new MessSystem("fmtownsux"      ,"fmtownsux" , new MessRomType[]
                        {
                            new MessRomType("cdrm", new string[] { "iso", "cue", "chd", "toc", "nrg", "gdi", "cdr" }),
                            new MessRomType("hard1", new string[] { "hd", "hdv", "2mg", "hdi" }),
                            new MessRomType("memc", new string[] { "icm" }),
                            new MessRomType("flop1" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk  .bin */ )
                        }) { InGameMouse = true },

                new MessSystem("fmtmarty"     ,"fmtmarty"      , new MessRomType[]  // Fujitsu FM Towns Marty
                        {
                            new MessRomType("cdrm", new string[] { "chd", "cue", "toc", "nrg", "gdi", "iso", "cdr" }),
                            new MessRomType("memc", new string[] { "icm" }),
                            new MessRomType("flop" /* .mfi  .dfi  .hfe  .mfm  .td0  .imd  .d77  .d88  .1dd  .cqm  .cqi  .dsk  .bin */ )
                        }) { InGameMouse = true },

                // Oric (Tangerine)
                new MessSystem("oric"         ,"orica"     , new MessRomType[]
                        {
                            new MessRomType("cass", null, "CLOAD\\\"\\\"\\n" ),
                        }),

                // TI-99
                new MessSystem("ti99"         ,"ti99_4a"  , new MessRomType[]
                        {
                            new MessRomType("cass1", new string[] { "wav" } ),
                            new MessRomType("cart", new string[] { "rpk" } )
                        }) { UseFileNameWithoutExtension = true },

                // TUTOR
                new MessSystem("tutor"        ,"tutor"    ,new MessRomType[]
                        {
                            new MessRomType("cass", new string[] { "wav" }),
                            new MessRomType("cart", new string[] { "bin" })
                        }),

                // XEGS (ATARI)
                new MessSystem("xegs"         ,"xegs", new MessRomType[]
                        {
                            new MessRomType("flop1", new string[] { "atr", "dsk", "xfd" } ),
                            new MessRomType("cart")
                        }){ UseFileNameWithoutExtension = true },

                // Archimedes
                new MessSystem("archimedes"         ,"aa4401"     , "flop"),
                new MessSystem("aa305"        ,"aa305"    ,"flop"  ),
                new MessSystem("aa440"        ,"aa440"    ,"flop"  ),
                new MessSystem("aa310"        ,"aa310"    ,"flop"  ),
                new MessSystem("aa4101"        ,"aa4101"    ,"flop"  ),
                new MessSystem("aa4201"        ,"aa4201"    ,"flop"  ),
                new MessSystem("aa3000"        ,"aa3000"    ,"flop"  ),
                new MessSystem("aa540"        ,"aa540"    ,"flop"  ),
                new MessSystem("aa5000"        ,"aa5000"    ,new MessRomType[]
                        {
                            new MessRomType("rom", new string[] { "bin", "rom" }),
                            new MessRomType("hard", new string[] { "chd", "hd", "hdv", "2mg", "hdi" }),
                            new MessRomType("flop")
                        }),
                new MessSystem("aa3010"        ,"aa3010"    ,"flop"  ),
                new MessSystem("aa3020"        ,"aa3020"    ,new MessRomType[]
                        {
                            new MessRomType("hard", new string[] { "chd", "hd", "hdv", "2mg", "hdi" }),
                            new MessRomType("flop")
                        }),
                new MessSystem("aa4000"        ,"aa4000"    ,new MessRomType[]
                        {
                            new MessRomType("hard", new string[] { "chd", "hd", "hdv", "2mg", "hdi" }),
                            new MessRomType("flop")
                        }),

                new MessSystem("advision"     ,"advision" ,"cart"  ),           // Adventure Vision
                new MessSystem("scv"          ,"scv"      ,"cart"  ),           // Super Cassette Vision
                new MessSystem("astrocde"     ,"astrocde" ,"cart"  ),           // Bally Astrocade
                new MessSystem("astrocade"    ,"astrocde" ,"cart"  ),           // Bally Astrocade
                new MessSystem("casloopy"     ,"casloopy" ,"cart"  ),           // casio Loopy
                new MessSystem("pv1000"       ,"pv1000"   ,"cart"  ),           // Casio PV-1000
                new MessSystem("gamecom"      ,"gamecom"  ,"cart1" ),           // GameCom
                new MessSystem("gp32"         ,"gp32"     ,"memc"  ),           // GamePark 32             
                new MessSystem("vsmile"       ,"vsmile"   ,"cart"  ),           // VSMILE
                new MessSystem("vsmilem"      ,"vsmilem"  ,"cart"  ),           // VSMILE
                new MessSystem("vsmilpro"     ,"vsmilpro" ,"cdrm"  ),           // VSMILE
                new MessSystem("supracan"     ,"supracan" ,"cart"  ),           // Supracan
                new MessSystem("megaduck"     ,"megaduck" ,"cart"  ),           // Megaduck
                new MessSystem("gamate"       ,"gamate"   ,"cart"  ),           // Gamate
                new MessSystem("gamepock"     ,"gamepock" ,"cart"  ),           // Game Pocket
                new MessSystem("apfm1000"     ,"apfm1000" ,"cart"  ),           // APF M-1000
                new MessSystem("arcadia"      ,"arcadia"  ,"cart"  ),           // Arcadia 2001
                new MessSystem("gmaster"      ,"gmaster"  ,"cart"  ),           // Game Master
                new MessSystem("vc4000"       ,"vc4000"        , "cart"  ),     // Interton VC 4000
                new MessSystem("pegasus"      ,"pegasus"       , "rom1"  ),     // Amber Pegasus
                new MessSystem("pegasusm"      ,"pegasusm"     , "rom1"  ),     // Amber Pegasus (memory expansion)

                // NOT IN RETROBAT
                new MessSystem("x1"           ,"x1" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "bin", "rom" } ), 
                            new MessRomType("cass", new string[] { "wav", "tap" } ), 
                            new MessRomType("flop1")
                        }),

                new MessSystem("dragon32"     ,"dragon32" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "ccc", "rom" } ), 
                            new MessRomType("cass", new string[] { "wav", "cas" }, "CLOADM\\nEXEC\\n" ), 
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
                            new MessRomType("cass", new string[] { "wav", "csw", "uef" }, "*T.\\nCH.\\\"\\\"\\n" ), 
                            new MessRomType("flop")
                        }),

                new MessSystem("c64"          ,"c64" , new MessRomType[] 
                        { 
                            new MessRomType("cart", new string[] { "80", "a0", "e0", "crt" } ), 
                            new MessRomType("cass", new string[] { "wav", "tap" } ), 
                            new MessRomType("quik", new string[] { "p00", "prg", "t64" } ), 
                            new MessRomType("flop")
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

                new MessSystem("macintosh"       ,"maclc3", new MessRomType[] 
                        { 
                            new MessRomType("cdrm", new string[] { "chd", "cue", "toc", "nrg", "gdi", "iso", "cdr" }), 
                            new MessRomType("hard", new string[] { "hd", "hdv", "2mg", "hdi" }), 
                            new MessRomType("flop")
                        }),
    
                new MessSystem("einstein"     ,"einstein"      , "flop1"  ), // Tatung Einstein TC-01
                new MessSystem("pmd85"        ,"pmd85"         , "cass"  ), // Tesla PMD 85
                new MessSystem("laser200"     ,"laser200"      , "dump" ), // VTech Laser 200
                new MessSystem("mpu2000"      ,"vc4000"        , "cart"  ),  // Acetronic MPU 2000
                new MessSystem("mpt05"        ,"vc4000"        , "cart"  ),  // ITMC MPT-05
                new MessSystem("tcs"          ,"vc4000"        , "cart"  ),  // Rowtron Television Computer System
                new MessSystem("cpc6128p"     ,"cpc6128p"      , "flop1"  ), // Amstrad CPC Plus
                new MessSystem("apogee"       ,"apogee"        , "cass"  ),  // Apogee BK-01
                new MessSystem("apple2gs"     ,"apple2gsr1"    , "flop3"  ), // Apple II GS
                new MessSystem("sv8000"       ,"sv8000"        , "cart"  ),  // Bandai Super Vision 8000
                new MessSystem("pv2000"       ,"pv2000"        , "cart"  ),  // Casio PV-2000
                new MessSystem("vic10"        ,"vic10"         , "cart"  ),  // Commodore MAX Machine
                new MessSystem("cgenie"       ,"cgenie"        , "cass"  ), // EACA EG2000 Colour Genie                
                new MessSystem("bk001001"     ,"bk001001"      , "cass"  ), // Electronika BK       
                new MessSystem("spc4000"      ,"vc4000"        , "cart"  ), // Grundig Super Play Computer 4000
                new MessSystem("hmg2650"      ,"arcadia"       , "cart"  ), // Hanimex HMG 2650
                new MessSystem("interact"     ,"interact"      , "cass"  ), // Interact Home Computer
                new MessSystem("abc80"        ,"abc80"         , "flop1"  ), // Luxor ABC 80
                new MessSystem("aquarius"     ,"aquarius"      , "cart1"  ), // Mattel Aquarius
                new MessSystem("samcoupe"     ,"samcoupe"      , "flop1"  ), // MGT Sam Coupe
                new MessSystem("microvsn"     ,"microvsn"      , "cart"  ), // Milton Bradley Microvision
                new MessSystem("pc6001"       ,"pc6001mk2"     , "cart2"  ), // NEC PC-6001
                new MessSystem("p2000t"       ,"p2000t"     , "cass"  ), // Philips P2000T
                new MessSystem("vg5k"         ,"vg5k"     , "cass"  ), // Philips VG 5000
                new MessSystem("radio86"      ,"radio86"     , "cass"  ), // Radio-86RK Partner-01.01
                new MessSystem("studio2"      ,"studio2"     , "cart"  ), // RCA Studio II
                new MessSystem("svmu"         ,"svmu"     , "quik"  ), // Sega Visual Memory Unit
                new MessSystem("mz2500"       ,"mz2500"     , "flop1"  ), // Sharp MZ-2500
                new MessSystem("mz700"        ,"mz700"     , "cass"  ), // Sharp MZ-700
                new MessSystem("pockstat"     ,"pockstat"     , "cart"  ), // Sony PocketStation
                new MessSystem("m5"           ,"m5"     , "cart1"  ), // Sord M5
                new MessSystem("sf7000"       ,"sf7000"     , "flop"  ), // Super Control Station SF-7000                
                new MessSystem("supervision"  ,"svision"     , "cart"  ), // Supervision

                new MessSystem("pecom64"      ,"pecom64"     , new MessRomType[]   // Pecom 64
                        {                            
                            new MessRomType("cass", null, "PLOAD\\nRUN\\n" ), 
                        }),

                new MessSystem("ep64"     ,"ep128", new MessRomType[]  // Enterprise Sixty Four
                        {                            
                            new MessRomType("cass", new string[] { "wav" }), 
                            new MessRomType("cart")
                        }),
                   
                new MessSystem("exl100"     ,"exl100", new MessRomType[]  // Exelvision EXL 100                
                        {                            
                            new MessRomType("cass", new string[] { "wav" }), 
                            new MessRomType("cart")
                        }),
                   
                new MessSystem("mikrosha"     ,"mikrosha", new MessRomType[]  // Mikrosha
                        {                            
                            new MessRomType("cass", new string[] { "wav", "rkm" }), 
                            new MessRomType("cart")
                        }),

                new MessSystem("mtx"      ,"mtx512", new MessRomType[]  // Memotech MTX
                        { 
                            new MessRomType("dump", new string[] { "mtx" }), 
                            new MessRomType("quik", new string[] { "run" }), 
                            new MessRomType("cass", new string[] { "wav" }), 
                            new MessRomType("cart1")
                        }),

                new MessSystem("hector"      ,"hec2hrx", new MessRomType[]  // Hector HRX
                        { 
                            new MessRomType("cass", new string[] { "wav", "k7", "cin", "for" }), 
                            new MessRomType("flop1")
                        }),

                new MessSystem("jupace"      ,"jupace", new MessRomType[]  // Jupiter ACE
                        { 
                            new MessRomType("dump", new string[] { "ace" }), 
                            new MessRomType("cass")
                        }),

                new MessSystem("galaxyp"    ,"galaxyp", new MessRomType[]  // Galaksija Plus
                        { 
                            new MessRomType("dump", new string[] { "gal" }), 
                            new MessRomType("cass")
                        }),

                new MessSystem("sorcerer"     ,"sorcerer", new MessRomType[]  // Exidy Sorcerer
                        { 
                            new MessRomType("quik", new string[] { "snp" }), 
                            new MessRomType("cass1", new string[] { "wav", "tape" }, "LOG\\n" ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("vic20"        ,"vic20", new MessRomType[]  // Commodore VIC-20
                        { 
                            new MessRomType("flop", new string[] { "mfi", "dfi", "d64", "g64", "g41", "g71" }), 
                            new MessRomType("cass", new string[] { "wav", "tap" }), 
                            new MessRomType("cart")
                        }),

                new MessSystem("vector06"       ,"vector06", new MessRomType[] // Vector-06C
                        { 
                            new MessRomType("cart", new string[] { "bin", "emr" }), 
                            new MessRomType("cass", new string[] { "wav" }), 
                            new MessRomType("flop1")
                        }),
                        
                new MessSystem("tvc64"       ,"tvc64", new MessRomType[] // Videoton TV 64
                        { 
                            new MessRomType("cass", new string[] { "wav", "cas" }, "LOAD\\n" ), 
                            new MessRomType("cart")
                        }),

                new MessSystem("cdi"          ,"cdimono1" ,"cdrm"  ) { InGameMouse = true },        
                new MessSystem("attache"      ,"attache"  ,"flop1"  ),
                new MessSystem("ampro"        ,"ampro"    ,"flop1"  ),
                new MessSystem("apc"          ,"apc"      ,"flop1"  ),
                new MessSystem("gw"           ,""         ,""      ),
                new MessSystem("gameandwatch" ,""         ,""      ),
                new MessSystem("lcdgames"     ,"%romname%",""      ),
                new MessSystem("mame"         ,"%romname%"     ,""      ),
                new MessSystem("hbmame"       ,"hbmame"   ,""      ),
                new MessSystem("cave"         ,"%romname%",""      ),
                new MessSystem("tvgames"      ,""         ,""      ),
                new MessSystem("socrates"     ,"socrates" ,"cart"  ),
                new MessSystem("a2600"        ,"a2600"    ,"cart"  ),
                new MessSystem("nes"          ,"nes"      ,"cart"  ),
                new MessSystem("snes"         ,"snes"     ,"cart"  ),
                new MessSystem("gbcolor"      ,"gbcolor"  ,"cart"  ),
                new MessSystem("gameboy"      ,"gameboy"  ,"cart"  ),
                new MessSystem("bk0010"       ,"bk001001" ,"cass"  ),                
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

                if (messMode == null && !string.IsNullOrEmpty(system))
                    messMode = MessSystems.FirstOrDefault(m => system.Equals(altModel, StringComparison.InvariantCultureIgnoreCase));

                if (messMode == null && !string.IsNullOrEmpty(system))
                    messMode = MessSystems.FirstOrDefault(m => system.Equals(altModel, StringComparison.InvariantCultureIgnoreCase));                
            }

            if (messMode == null && !string.IsNullOrEmpty(system))
                messMode = MessSystems.FirstOrDefault(m => system.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && !string.IsNullOrEmpty(system))
                messMode = MessSystems.FirstOrDefault(m => system.Equals(m.MachineName, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && !string.IsNullOrEmpty(core))
                messMode = MessSystems.FirstOrDefault(m => core.Equals(m.Name, StringComparison.InvariantCultureIgnoreCase));

            if (messMode == null && !string.IsNullOrEmpty(core))
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
        
        public List<string> GetMameCommandLineArguments(string system, string rom, bool standalone = false)
        {
            bool useSoftList = SystemConfig.isOptSet("force_softlist") && SystemConfig["force_softlist"] != "none";

            List<string> commandArray = new List<string>();

            // Alternate system for machines that have different configs (ie computers with different hardware)
            string messModel = "";

            if (SystemConfig.isOptSet("altmodel"))
            {
                commandArray.Add(SystemConfig["altmodel"]);
                messModel = SystemConfig["altmodel"];
            }
            else if (MachineName == "%romname%")
            {
                commandArray.Add(Path.GetFileNameWithoutExtension(rom));
                messModel = Path.GetFileNameWithoutExtension(rom);
            }
            else if (!string.IsNullOrEmpty(this.MachineName) && this.MachineName != "%romname%")
            {
                commandArray.Add(MachineName);
                messModel = MachineName;
            }

            if (standalone)
                commandArray.Add("-skip_gameinfo");

            // Cleanup previous ini file
            // This is required, else there might be multiple image devices listed and MAME might autoload the wrong one
            string iniFileName = "";
            if (SystemConfig.isOptSet("altmodel"))
                iniFileName = SystemConfig["altmodel"];
            else if (MachineName == "%romname%")
                iniFileName = Path.GetFileNameWithoutExtension(rom);
            else if (!string.IsNullOrEmpty(this.MachineName) && this.MachineName != "%romname%")
                iniFileName = MachineName;

            var bios = AppConfig.GetFullPath("bios");
            var saves = AppConfig.GetFullPath("saves");

            string inipath = Path.Combine(bios, "mame", "ini", iniFileName + ".ini");
            if (File.Exists(inipath))
                File.Delete(inipath);

            // rompath
            commandArray.Add("-rompath");
            if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig.GetFullPath("bios")))
            {
                if (Directory.Exists(Path.Combine(bios, "mess")))
                    commandArray.Add(Path.Combine(bios, "mess") + ";" + bios + ";" + Path.GetDirectoryName(rom));
                else
                    commandArray.Add(bios + ";" + Path.GetDirectoryName(rom));
            }

            else
                commandArray.Add(Path.GetDirectoryName(rom));

            // Cheats
            string cheatPath = Path.Combine(AppConfig.GetFullPath("cheats"), "mame");
            if (!string.IsNullOrEmpty(cheatPath) && Directory.Exists(cheatPath))
            {
                commandArray.Add("-cheatpath");
                commandArray.Add(cheatPath);
            }

            List<string> pluginList = new List<string>();
            if (SystemConfig.isOptSet("cheats_enable") && SystemConfig.getOptBoolean("cheats_enable"))
                pluginList.Add("cheat");
            if (SystemConfig.isOptSet("mame_hiscore") && SystemConfig.getOptBoolean("mame_hiscore"))
                pluginList.Add("hiscore");

            if (pluginList.Count > 0)
            {
                string pluginJoin = string.Join<string>(",", pluginList);
                commandArray.Add("-plugin");
                commandArray.Add(pluginJoin);
            }

            if (standalone)
            {
                commandArray.Add("-cfg_directory");
                commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "cfg")));

                commandArray.Add("-inipath");
                commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "ini")));

                commandArray.Add("-hashpath");
                commandArray.Add(EnsureDirectoryExists(Path.Combine(bios, "mame", "hash")));

                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    commandArray.Add("-snapshot_directory");
                    commandArray.Add(AppConfig.GetFullPath("screenshots"));
                }
            }

            // Artwork path
            commandArray.Add("-artpath");

            string artwork = Path.Combine(Path.GetDirectoryName(rom), "artwork");
            if (Directory.Exists(artwork))
            { 
                artwork = Path.Combine(Path.GetDirectoryName(rom), ".artwork");
                commandArray.Add(artwork + ";" + EnsureDirectoryExists(Path.Combine(saves, "mame", "artwork")));
            }   
            else
                commandArray.Add(EnsureDirectoryExists(Path.Combine(saves, "mame", "artwork")));

            // Specific modules for some systems (manage system slots)
            
            Action<string, string> addSlot = (v, w) =>
            {
                if (SystemConfig.isOptSet(v) && SystemConfig.getOptBoolean(v))
                {
                    commandArray.Add(w);
                    commandArray.Add(v);
                }
            };

            // Adam
            if (system == "adam")
            {
                if (SystemConfig.isOptSet("adam_joy") && SystemConfig["adam_joy"] != "none")
                {
                    commandArray.Add("-joy1");
                    commandArray.Add(SystemConfig["adam_joy"]);
                    commandArray.Add("-joy2");
                    commandArray.Add(SystemConfig["adam_joy"]);
                }
            }

            // Apple 2
            if (system == "apple2" || system == "apple2gs")
            {
                if (SystemConfig.isOptSet("gameio") && SystemConfig["gameio"] != "none")
                {
                    if (SystemConfig["gameio"] == "joyport" && messModel != "apple2p")
                        throw new ApplicationException(" Joyport only compatible with Apple II +");
                    else
                    {
                        commandArray.Add("-gameio");
                        commandArray.Add(SystemConfig["gameio"]);
                    }
                }

                addSlot("4play", "-sl1");
                addSlot("midi", "-sl2");

                if (system == "apple2gs")
                    addSlot("mockingboard", "-sl4");
            }

            //Astrocade
            if (system == "astrocade")
            {
                if (SystemConfig.isOptSet("astrocde_addjoy") && SystemConfig.getOptBoolean("astrocde_addjoy"))
                {
                    commandArray.Add("-ctrl2");
                    commandArray.Add("joy");
                    commandArray.Add("-ctrl3");
                    commandArray.Add("joy");
                    commandArray.Add("-ctrl4");
                    commandArray.Add("joy");
                }
            }

            //BBC Micro Joystick
            if (system == "bbcmicro")
            {
                if (SystemConfig.isOptSet("bbc_sticktype") && SystemConfig["bbc_sticktype"] != "none" && messModel != "bbcmc")
                {
                    commandArray.Add("-analogue");
                    commandArray.Add(SystemConfig["bbc_sticktype"]);
                }
                if (messModel == "bbcmc")
                {
                    addSlot("joystick", "-joyport");
                }
            }

            //FM-Towns
            if (system == "fmtowns")
            {
                if (SystemConfig.isOptSet("fmtowns_joytype") && SystemConfig["fmtowns_joytype"] != "none")
                {
                    commandArray.Add("-pad1");
                    commandArray.Add(SystemConfig["fmtowns_joytype"]);
                    commandArray.Add("-pad2");
                    commandArray.Add(SystemConfig["fmtowns_joytype"]);
                }
            }

            // TI99
            if (system == "ti99")
            {
                commandArray.Add("-ioport");
                commandArray.Add("peb");
                if (!SystemConfig.isOptSet("ti99_32kram") || SystemConfig.getOptBoolean("ti99_32kram"))
                { 
                    commandArray.Add("-ioport:peb:slot2");
                    commandArray.Add("32kmem");
                }
                if (!SystemConfig.isOptSet("ti99_speech") || SystemConfig.getOptBoolean("ti99_speech"))
                {
                    commandArray.Add("-ioport:peb:slot3");
                    commandArray.Add("speech");
                }
            }

            //V-SMILE
            if (system == "vsmile")
            {
                commandArray.Add("-ctrl2");
                commandArray.Add("joy");
            }

            // Ram size
            if (SystemConfig.isOptSet("ramsize") && !string.IsNullOrEmpty(SystemConfig["ramsize"]))
            {
                commandArray.Add("-ramsize");
                commandArray.Add(SystemConfig["ramsize"]);
            }

            // Autostart computer games where applicable
            // User autostart if autorun file exists
            var romname = Path.GetFileNameWithoutExtension(rom);
            string autorunFile = Path.Combine(Path.GetDirectoryName(rom), romname + ".autorun");
            if (File.Exists(autorunFile))
            {
                if (File.ReadAllText(autorunFile) != null)
                {
                    commandArray.Add("-autoboot_delay");
                    commandArray.Add("3");
                    commandArray.Add("-autoboot_command");
                    commandArray.Add(File.ReadAllText(autorunFile));
                }
            }

            //Specific autostart for Camputers lynx based on hashfile (for now only for MAME standalone)
            else if (standalone && system == "camplynx" && SystemConfig.isOptSet("force_softlist") && !string.IsNullOrEmpty(SystemConfig["force_softlist"]))
            {
                string hashfile = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "hash", SystemConfig["force_softlist"] + ".xml");
                if (File.Exists(hashfile))
                {
                    XDocument doc = XDocument.Load(hashfile);
                    string idToFind = romname;
                    XElement selectedElement = doc.Descendants()
                            .Where(x => (string)x.Attribute("name") == idToFind).FirstOrDefault();

                    if (selectedElement != null)
                    {
                        XElement commandElement = selectedElement.Descendants()
                            .Where(x => (string)x.Attribute("name") == "usage").FirstOrDefault();

                        if (commandElement != null)
                        {
                            string command = commandElement.Attribute("value").Value + "\\n";
                            command = command.Replace("\"", "\\\"");
                            commandArray.Add("-autoboot_delay");
                            commandArray.Add("3");
                            commandArray.Add("-autoboot_command");
                            commandArray.Add(command);
                        }
                    }
                }
            }

            // Generic boot if only one type is available
            else
            {
                var autoRunCommand = SystemConfig.isOptSet("altromtype") ? GetAutoBootForRomType(SystemConfig["altromtype"]) : GetAutoBoot(rom);
                if (autoRunCommand != null)
                    commandArray.AddRange(autoRunCommand.Arguments);
            }

            // Additional disks if required
            if (SystemConfig.isOptSet("addblankdisk") && !string.IsNullOrEmpty(SystemConfig["addblankdisk"]))
            {
                // FMTOWNS (blankdisk or system disk to mount with cdrom - system disk must be placed in saves folder and have the same name as the cd rom game name, extension is .hdm)
                if (system == "fmtowns")
                {
                    {
                        bool blank = SystemConfig["addblankdisk"] == "blank";
                        string MessRomType = this.GetRomType(rom);
                        string diskPath = Path.Combine(EnsureDirectoryExists(Path.Combine(saves, "mame", system)));
                        string blankDisk = Path.Combine(diskPath, "blank.fmtowns");
                        string targetdisk = blank? Path.Combine(diskPath, Path.GetFileNameWithoutExtension(rom) + ".fmtowns") : Path.Combine(diskPath, Path.GetFileNameWithoutExtension(rom) + ".hdm");
                        
                        if (!File.Exists(targetdisk) && File.Exists(blankDisk))
                            File.Copy(blankDisk, targetdisk);

                        if (File.Exists(targetdisk))
                        {
                            if (messModel == "fmtmarty")
                            {
                                commandArray.Add("-flop");
                                commandArray.Add(targetdisk);
                            }
                            else if ((SystemConfig.isOptSet("altromtype") && SystemConfig["altromtype"] == "flop1") || MessRomType == "flop1")
                            {
                                commandArray.Add("-flop2");
                                commandArray.Add(targetdisk);
                            }
                            else
                            {
                                commandArray.Add("-flop1");
                                commandArray.Add(targetdisk);
                            }
                        }
                    }
                }
            }

            // Alternate ROM type for systems with mutiple media (ie cassette & floppy) / only if softlist not set
            if (!useSoftList)
            {
                if (SystemConfig.isOptSet("altromtype") && Path.GetExtension(rom).ToLower() != ".m3u")
                    commandArray.Add("-" + SystemConfig["altromtype"]);

                else if (Path.GetExtension(rom).ToLower() != ".m3u")
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
            }

            // Specific cases for some systems
            // Disable softlist for .rpk extension with ti99
            if (system == "ti99" && rom.EndsWith(".rpk"))
                UseFileNameWithoutExtension = false;


            // Specific Managements for multi-disc roms using m3u
            // Go through the .m3u file and assign each line to a floppy drive
            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                List<string> disks = new List<string>();
                string dskPath = Path.GetDirectoryName(rom);

                foreach (var line in File.ReadAllLines(rom))
                {
                    string dsk = Path.Combine(dskPath, line);
                    if (File.Exists(dsk))
                        disks.Add(dsk);
                    else
                        throw new ApplicationException("File specified in .m3u does not exist");
                }

                if (disks.Count == 0)
                    throw new ApplicationException(".m3u file is empty");

                else if (disks.Count == 1)
                {
                    var romType = this.GetRomType(disks[0]);

                    if (SystemConfig.isOptSet("altromtype") && !string.IsNullOrEmpty(SystemConfig["altromtype"]))
                        romType = SystemConfig["altromtype"];

                    if (!string.IsNullOrEmpty(romType))
                        commandArray.Add("-" + romType);
                    commandArray.Add(disks[0]);
                }

                else if (disks.Count > 1 && system == "apple2gs")
                {
                    var romType = this.GetRomType(disks[0]);
                    if (SystemConfig.isOptSet("altromtype") && !string.IsNullOrEmpty(SystemConfig["altromtype"]))
                        romType = SystemConfig["altromtype"];
                    
                    if (romType == "flop3" || romType == "flop4")
                    {
                        commandArray.Add("-flop3");
                        commandArray.Add(disks[0]);
                        commandArray.Add("-flop4");
                        commandArray.Add(disks[1]);
                    }
                    else if (romType == "flop1" || romType == "flop2")
                    {
                        commandArray.Add("-flop1");
                        commandArray.Add(disks[0]);
                        commandArray.Add("-flop2");
                        commandArray.Add(disks[1]);
                    }
                }

                else if (disks.Count > 1 && system == "apple2")
                {
                    commandArray.Add("-flop1");
                    commandArray.Add(disks[0]);
                    commandArray.Add("-flop2");
                    commandArray.Add(disks[1]);
                }
            }

            // Specific Managements to enable or disable softlist
            // When using softlist, the rom name must match exactly the hash file and be passed to command line without path or extension
            else if (SystemConfig.isOptSet("force_softlist") && SystemConfig["force_softlist"] != "none")
            {
                string softlist = SystemConfig["force_softlist"];
                rom = softlist + ":" + Path.GetFileNameWithoutExtension(rom);
                commandArray.Add(rom);
            }

            // Generic case: add rom to command line
            else if (MachineName != "%romname%")
                commandArray.Add(this.UseFileNameWithoutExtension ? Path.GetFileNameWithoutExtension(rom) : rom);

            // Add an argument to mame core to fix saving of remap file naming
            if (system == "mame" && MachineName == "%romname%")
            {
                commandArray.Add("-comment_directory");
                commandArray.Add(rom);
            }
                return commandArray;
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

            if (ext == "zip" || ext == "7z")
            {
                var e = Zip.ListEntries(rom).Where(f => !f.IsDirectory).Select(f => f.Filename).ToArray();
                if (e.Length == 1 && !string.IsNullOrEmpty(Path.GetExtension(e[0])))
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

            if (ext == "zip" || ext == "7z")
            {
                var e = Zip.ListEntries(rom).Where(f => !f.IsDirectory).Select(f => f.Filename).ToArray(); 
                if (e.Length == 1 && !string.IsNullOrEmpty(Path.GetExtension(e[0])))
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
