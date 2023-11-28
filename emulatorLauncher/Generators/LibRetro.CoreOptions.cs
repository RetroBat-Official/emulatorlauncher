using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Libretro
{
    partial class LibRetroGenerator : Generator
    {
        public static string GetCoreName(string core)
        {
            var coreNames = new Dictionary<string, string>()
            {
                { "2048", "2048" },
                { "3dengine", "3DEngine" },
                { "4do", "4DO" },
                { "81", "EightyOne" },
                { "a5200", "a5200" },
                { "advanced_tests", "Advanced Test" },
                { "arduous", "Arduous" },
                { "atari800", "Atari800" },
                { "bk", "bk" },
                { "blastem", "BlastEm" },
                { "bluemsx", "blueMSX" },
                { "bnes", "bnes/higan" },
                { "boom3", "boom3" },
                { "boom3_xp", "boom3_xp" },
                { "bsnes2014_accuracy", "bsnes 2014 Accuracy" },
                { "bsnes2014_balanced", "bsnes 2014 Balanced" },
                { "bsnes2014_performance", "bsnes 2014 Performance" },
                { "bsnes_cplusplus98", "bsnes C++98 (v085)" },
                { "bsnes_hd_beta", "bsnes-hd beta" },
                { "bsnes", "bsnes" },
                { "bsnes_mercury_accuracy", "bsnes-mercury Accuracy" },
                { "bsnes_mercury_balanced", "bsnes-mercury Balanced" },
                { "bsnes_mercury_performance", "bsnes-mercury Performance" },
                { "cannonball", "Cannonball" },
                { "cap32", "cap32" },
                { "cdi2015", "Philips CDi 2015" },
                { "chailove", "ChaiLove" },
                { "citra2018", "Citra 2018" },
                { "citra_canary", "Citra Canary/Experimental" },
                { "citra", "Citra" },
                { "craft", "Craft" },
                { "crocods", "CrocoDS" },
                { "cruzes", "Cruzes" },
                { "daphne", "Daphne" },
                { "desmume2015", "DeSmuME 2015" },
                { "desmume", "DeSmuME" },
                { "dinothawr", "Dinothawr" },
                { "directxbox", "DirectXBox" },
                { "dolphin_launcher", "Dolphin Launcher" },
                { "dolphin", "dolphin-emu" },
                { "dosbox_core", "DOSBox-core" },
                { "dosbox", "DOSBox" },
                { "dosbox_pure", "DOSBox-pure" },
                { "dosbox_svn_ce", "DOSBox-SVN CE" },
                { "dosbox_svn", "DOSBox-SVN" },
                { "duckstation", "DuckStation" },
                { "easyrpg", "EasyRPG Player" },
                { "ecwolf", "ECWolf" },
                { "emuscv", "Libretro-EmuSCV" },
                { "emux_chip8", "Emux CHIP-8" },
                { "emux_gb", "Emux GB" },
                { "emux_nes", "Emux NES" },
                { "emux_sms", "Emux SMS" },
                { "fbalpha2012_cps1", "FB Alpha 2012 CPS-1" },
                { "fbalpha2012_cps2", "FB Alpha 2012 CPS-2" },
                { "fbalpha2012_cps3", "FB Alpha 2012 CPS-3" },
                { "fbalpha2012", "FB Alpha 2012" },
                { "fbalpha2012_neogeo", "FB Alpha 2012 Neo Geo" },
                { "fbalpha", "FB Alpha" },
                { "fbneo", "FinalBurn Neo" },
                { "fceumm", "FCEUmm" },
                { "ffmpeg", "FFmpeg" },
                { "fixgb", "fixGB" },
                { "fixnes", "fixNES" },
                { "flycast_gles2", "Flycast GLES2" },
                { "flycast", "Flycast" },
                { "fmsx", "fMSX" },
                { "freechaf", "FreeChaF" },
                { "freeintv", "FreeIntv" },
                { "freej2me", "FreeJ2ME" },
                { "frodo", "Frodo" },
                { "fsuae", "FS-UAE" },
                { "fuse", "Fuse" },
                { "gambatte", "Gambatte" },
                { "gearboy", "Gearboy" },
                { "gearcoleco", "Gearcoleco" },
                { "gearsystem", "Gearsystem" },
                { "genesis_plus_gx", "Genesis Plus GX" },
                { "genesis_plus_gx_wide", "Genesis Plus GX Wide" },
                { "gme", "Game Music Emu" },
                { "gong", "Gong" },
                { "gpsp", "gpSP" },
                { "gw", "Game & Watch" },
                { "handy", "Handy" },
                { "hatari", "Hatari" },
                { "hatarib", "Hatarib" },
                { "hbmame", "HBMAME (Git)" },
                { "higan_sfc_balanced", "nSide (Super Famicom Balanced)" },
                { "higan_sfc", "nSide (Super Famicom Accuracy)" },
                { "imageviewer", "image display" },
                { "ishiiruka", "Ishiiruka" },
                { "jaxe", "JAXE" },
                { "jumpnbump", "jumpnbump" },
                { "kronos", "Kronos" },
                { "lowresnx", "LowRes NX" },
                { "lutro", "Lutro" },
                { "mame2000", "MAME 2000" },
                { "mame2003", "MAME 2003 (0.78)" },
                { "mame2003_midway", "MAME 2003 Midway (0.78)" },
                { "mame2003_plus", "MAME 2003-Plus" },
                { "mame2009", "MAME 2009 (0.135u4)" },
                { "mame2010", "MAME 2010" },
                { "mame2014", "MAME 2014" },
                { "mame2015", "MAME 2015 (0.160)" },
                { "mame2016", "MAME 2016" },
                { "mamearcade", "MAME (Git)" },
                { "mame", "MAME" },
                { "mednafen_gba", "Beetle GBA" },
                { "mednafen_lynx", "Beetle Lynx" },
                { "mednafen_ngp", "Beetle NeoPop" },
                { "mednafen_pce_fast", "Beetle PCE Fast" },
                { "mednafen_pce", "Beetle PCE" },
                { "mednafen_pcfx", "Beetle PC-FX" },
                { "mednafen_psx_hw", "Beetle PSX HW" },
                { "mednafen_psx", "Beetle PSX" },
                { "mednafen_saturn", "Beetle Saturn" },
                { "mednafen_snes", "Mednafen bSNES" },
                { "mednafen_supafaust", "Beetle Supafaust" },
                { "mednafen_supergrafx", "Beetle SuperGrafx" },
                { "mednafen_vb", "Beetle VB" },
                { "mednafen_wswan", "Beetle WonderSwan" },
                { "melonds", "melonDS" },
                { "mesen-s", "Mesen-S" },
                { "mesen", "Mesen" },
                { "mess2015", "MESS 2015 (0.160)" },
                { "meteor", "Meteor" },
                { "mgba", "mGBA" },
                { "minivmac", "MinivmacII" },
                { "moonlight", "Moonlight" },
                { "mpv", "MPV" },
                { "mrboom", "Mr.Boom" },
                { "mupen64plus_next_develop", "Mupen64Plus-Next" },
                { "mupen64plus_next_gles2", "Mupen64Plus-Next" },
                { "mupen64plus_next_gles3", "Mupen64Plus-Next" },
                { "mupen64plus_next", "Mupen64Plus-Next" },
                { "mu", "Mu" },
                { "nekop2", "Neko Project II" },
                { "neocd", "NeoCD" },
                { "nestopia", "Nestopia" },
                { "np2kai", "Neko Project II Kai" },
                { "nxengine", "NXEngine" },
                { "o2em", "O2EM" },
                { "oberon", "Oberon" },
                { "open-source-notices", "" },
                { "openlara", "OpenLara" },
                { "opentyrian", "OpenTyrian" },
                { "opera", "Opera" },
                { "parallel_n64_debug", "ParaLLEl (Debug)" },
                { "parallel_n64", "ParaLLEl N64" },
                { "pascal_pong", "PascalPong" },
                { "pcem", "PCem" },
                { "pcsx1", "PCSX1" },
                { "pcsx2", "LRPS2 (alpha)" },
                { "pcsx_rearmed_interpreter", "PCSX ReARMed [Interpreter]" },
                { "pcsx_rearmed", "PCSX-ReARMed" },
                { "pcsx_rearmed_neon", "PCSX ReARMed [NEON]" },
                { "picodrive", "PicoDrive" },
                { "play", "Play!" },
                { "pocketcdg", "PocketCDG" },
                { "pokemini", "PokeMini" },
                { "potator", "Potator" },
                { "ppsspp", "PPSSPP" },
                { "prboom", "PrBoom" },
                { "prosystem", "ProSystem" },
                { "puae2021", "PUAE 2021" },
                { "puae", "PUAE" },
                { "px68k", "PX68k" },
                { "quasi88", "QUASI88" },
                { "quicknes", "QuickNES" },
                { "race", "RACE" },
                { "redbook", "Redbook" },
                { "reminiscence", "REminiscence" },
                { "remotejoy", "RemoteJoy" },
                { "retro8", "retro-8 (alpha)" },
                { "retrodream", "RetroDream" },
                { "rustation", "Rustation" },
                { "sameboy", "SameBoy" },
                { "sameduck", "SameDuck" },
                { "same_cdi", "SAME_CDI" },
                { "scummvm", "ScummVM" },
                { "simcp", "SimCoupe" },
                { "smsplus", "SMS Plus GX" },
                { "snes9x2002", "Snes9x 2002" },
                { "snes9x2005", "Snes9x 2005" },
                { "snes9x2005_plus", "Snes9x 2005 Plus" },
                { "snes9x2010", "Snes9x 2010" },
                { "snes9x", "Snes9x" },
                { "squirreljme", "SquirrelJME" },
                { "stella2014", "Stella 2014" },
                { "stella", "Stella" },
                { "stonesoup", "Dungeon Crawl Stone Soup" },
                { "superbroswar", "Super Bros War" },
                { "swanstation", "SwanStation" },
                { "tempgba", "TempGBA" },
                { "testaudio_callback", "TestAudio Callback" },
                { "testaudio_no_callback", "TestAudio NoCallback" },
                { "testaudio_playback_wav", "TestAudio Playback Wav" },
                { "testgl_compute_shaders", "TestGL ComputeShaders" },
                { "testgl_ff", "TestGL (FF)" },
                { "testgl", "TestGL" },
                { "testinput_buttontest", "Button Test" },
                { "testretroluxury", "Test RetroLuxury" },
                { "testsw", "TestSW" },
                { "testsw_vram", "TestSW VRAM" },
                { "testvulkan_async_compute", "TestVulkan AsyncCompute" },
                { "testvulkan", "TestVulkan" },
                { "test", "Test" },
                { "test_netplay", "netplay-test" },
                { "tgbdual", "TGB Dual" },
                { "theodore", "theodore" },
                { "thepowdertoy", "ThePowderToy" },
                { "tic80", "TIC-80" },
                { "tyrquake", "TyrQuake" },
                { "uae4arm", "UAE4ARM" },
                { "ume2015", "UME 2015 (0.160)" },
                { "uzem", "uzem" },
                { "vaporspec", "VaporSpec" },
                { "vbam", "VBA-M" },
                { "vba_next", "VBA Next" },
                { "vecx", "vecx" },
                { "vemulator", "VeMUlator" },
                { "vice_x128", "VICE x128" },
                { "vice_x64sc", "VICE x64sc" },
                { "vice_x64", "VICE x64" },
                { "vice_xcbm2", "VICE xcbm2" },
                { "vice_xcbm5x0", "VICE xcbm5x0" },
                { "vice_xpet", "VICE xpet" },
                { "vice_xplus4", "VICE xplus4" },
                { "vice_xscpu64", "VICE xscpu64" },
                { "vice_xvic", "VICE xvic" },
                { "virtualjaguar", "Virtual Jaguar" },
                { "vitaquake2-rogue", "vitaQuake 2 [Rogue]" },
                { "vitaquake2-xatrix", "vitaQuake 2 [Xatrix]" },
                { "vitaquake2-zaero", "vitaQuake 2 [Zaero]" },
                { "vitaquake2", "vitaQuakeII" },
                { "vitaquake3", "vitaQuake 3" },
                { "vitavoyager", "vitaVoyager" },
                { "wasm4", "WASM-4" },
                { "x1", "x1" },
                { "x64sdl", "VICE SDL" },
                { "xrick", "XRick" },
                { "yabasanshiro", "YabaSanshiro" },
                { "yabause", "Yabause" }
            };

            string ret;
            if (coreNames.TryGetValue(core, out ret))
                return ret;

            var sb = new StringBuilder(core.Replace("_", " "));

            bool space = true;

            for (int i = 0; i < sb.Length; i++)
            {
                sb[i] = space ? char.ToUpperInvariant(sb[i]) : char.ToLowerInvariant(sb[i]);
                space = (sb[i] == ' ');
            }

            return sb.ToString();
        }


        private bool _isWidescreen;

        private void ConfigureCoreOptions(ConfigFile retroarchConfig, string system, string core)
        {
            InputRemap = new Dictionary<string, string>();

            // ratio is widescreen ?
            int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
            if (idx == 1 || idx == 2 || idx == 4 || idx == 6 || idx == 7 || idx == 9 || idx == 14 || idx == 16 || idx == 18 || idx == 19 || idx == 24)
                _isWidescreen = true;

            var coreSettings = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), new ConfigFileOptions() { CaseSensitive = true });

            Configure4Do(retroarchConfig, coreSettings, system, core);
            Configure81(retroarchConfig, coreSettings, system, core);
            Configurea5200(retroarchConfig, coreSettings, system, core);
            ConfigureAtari800(retroarchConfig, coreSettings, system, core);
            ConfigureBoom3(retroarchConfig, coreSettings, system, core);
            ConfigureBlueMsx(retroarchConfig, coreSettings, system, core);
            Configurebsnes(retroarchConfig, coreSettings, system, core);
            ConfigureCap32(retroarchConfig, coreSettings, system, core);
            ConfigureCitra(retroarchConfig, coreSettings, system, core);
            ConfigureCraft(retroarchConfig, coreSettings, system, core);
            ConfigureCrocoDS(retroarchConfig, coreSettings, system, core);
            ConfigureDesmume(retroarchConfig, coreSettings, system, core);
            ConfigureDolphin(retroarchConfig, coreSettings, system, core);
            ConfigureDosboxPure(retroarchConfig, coreSettings, system, core);
            Configureecwolf(retroarchConfig, coreSettings, system, core);
            ConfigureEmuscv(retroarchConfig, coreSettings, system, core);
            ConfigureFbalpha(retroarchConfig, coreSettings, system, core);
            ConfigureFbalpha2012(retroarchConfig, coreSettings, system, core);
            ConfigureFbalphaCPS1(retroarchConfig, coreSettings, system, core);
            ConfigureFbalphaCPS2(retroarchConfig, coreSettings, system, core);
            ConfigureFbalphaCPS3(retroarchConfig, coreSettings, system, core);
            ConfigureFbneo(retroarchConfig, coreSettings, system, core);
            ConfigureFCEumm(retroarchConfig, coreSettings, system, core);
            ConfigureFlycast(retroarchConfig, coreSettings, system, core);
            ConfigureFrodo(retroarchConfig, coreSettings, system, core);
            ConfigureFuse(retroarchConfig, coreSettings, system, core);
            ConfigureGambatte(retroarchConfig, coreSettings, system, core);
            ConfigureGenesisPlusGX(retroarchConfig, coreSettings, system, core);
            ConfigureGenesisPlusGXWide(retroarchConfig, coreSettings, system, core);
            ConfigureGong(retroarchConfig, coreSettings, system, core);
            ConfigureHandy(retroarchConfig, coreSettings, system, core);
            ConfigureHatari(retroarchConfig, coreSettings, system, core);
            ConfigureHatariB(retroarchConfig, coreSettings, system, core);
            ConfigureKronos(retroarchConfig, coreSettings, system, core);
            ConfigureMame(retroarchConfig, coreSettings, system, core);
            ConfigureMame2000(retroarchConfig, coreSettings, system, core);
            ConfigureMame2003(retroarchConfig, coreSettings, system, core);
            ConfigureMame2003Plus(retroarchConfig, coreSettings, system, core);
            ConfigureMame2010(retroarchConfig, coreSettings, system, core);
            ConfigureMame2014(retroarchConfig, coreSettings, system, core);
            ConfigureMame2016(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPCFX(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPce(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPceFast(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenPsxHW(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenSaturn(retroarchConfig, coreSettings, system, core);
            ConfigureMednafenSuperGrafx(retroarchConfig, coreSettings, system, core);
            ConfigureMesen(retroarchConfig, coreSettings, system, core);
            ConfigureMesenS(retroarchConfig, coreSettings, system, core);
            ConfigureMupen64(retroarchConfig, coreSettings, system, core);
            ConfigureMelonDS(retroarchConfig, coreSettings, system, core);
            ConfiguremGBA(retroarchConfig, coreSettings, system, core);
            ConfigureMrBoom(retroarchConfig, coreSettings, system, core);
            ConfigureNeocd(retroarchConfig, coreSettings, system, core);
            ConfigureNestopia(retroarchConfig, coreSettings, system, core);
            ConfigureO2em(retroarchConfig, coreSettings, system, core);
            ConfigureOpenLara(retroarchConfig, coreSettings, system, core);
            ConfigureOpera(retroarchConfig, coreSettings, system, core);
            ConfigureParallelN64(retroarchConfig, coreSettings, system, core);
            ConfigurePcsx2(retroarchConfig, coreSettings, system, core);
            ConfigurePcsxRearmed(retroarchConfig, coreSettings, system, core);
            ConfigurePicodrive(retroarchConfig, coreSettings, system, core);
            ConfigurePokeMini(retroarchConfig, coreSettings, system, core);
            ConfigurePotator(retroarchConfig, coreSettings, system, core);
            ConfigurePpsspp(retroarchConfig, coreSettings, system, core);
            ConfigurePrBoom(retroarchConfig, coreSettings, system, core);
            ConfigureProSystem(retroarchConfig, coreSettings, system, core);
            ConfigurePuae(retroarchConfig, coreSettings, system, core);
            ConfigurePX68k(retroarchConfig, coreSettings, system, core);
            ConfigureQuasi88(retroarchConfig, coreSettings, system, core);
            ConfigureRace(retroarchConfig, coreSettings, system, core);
            ConfigureSameBoy(retroarchConfig, coreSettings, system, core);
            ConfigureSameCDI(retroarchConfig, coreSettings, system, core);
            ConfigureSameDuck(retroarchConfig, coreSettings, system, core);
            ConfigureScummVM(retroarchConfig, coreSettings, system, core);
            ConfigureSNes9x(retroarchConfig, coreSettings, system, core);
            ConfigureStella(retroarchConfig, coreSettings, system, core);
            ConfigureStella2014(retroarchConfig, coreSettings, system, core);
            ConfigureSwanStation(retroarchConfig, coreSettings, system, core);
            ConfigureTGBDual(retroarchConfig, coreSettings, system, core);
            ConfigureTheodore(retroarchConfig, coreSettings, system, core);
            ConfigureTyrquake(retroarchConfig, coreSettings, system, core);
            ConfigureVecx(retroarchConfig, coreSettings, system, core);
            Configurevice(retroarchConfig, coreSettings, system, core);
            ConfigureVirtualJaguar(retroarchConfig, coreSettings, system, core);
            ConfigureVitaquake2 (retroarchConfig, coreSettings, system, core);
            Configurex1(retroarchConfig, coreSettings, system, core);

            if (coreSettings.IsDirty)
                coreSettings.Save(Path.Combine(RetroarchPath, "retroarch-core-options.cfg"), true);

            // Disable Bezel as default if a widescreen ratio is set. Can be manually set.
            if (SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
            {
                if (_isWidescreen)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";

                }
            }

            // Inject custom input_libretro_device_pXX values into remap file, as it's no longer supported in retroarch.cfg file
            if (InputRemap != null && InputRemap.Count == 0 && Program.SystemConfig["disableautocontrollers"] != "1")
            {
                for (int i = 1; i <= 8; i++)
                {
                    var dev = retroarchConfig["input_libretro_device_p" + i];
                    if (string.IsNullOrEmpty(dev))
                        continue;

                    InputRemap["input_libretro_device_p" + i] = dev;

                    var mode = retroarchConfig["input_player" + i + "_analog_dpad_mode"];
                    if (!string.IsNullOrEmpty(mode))
                        InputRemap["input_player" + i + "_analog_dpad_mode"] = mode;

                    var index = i - 1;
                    InputRemap["input_remap_port_p" + i] = index.ToString();
                }
            }

            // Injects cores input remaps
            if (InputRemap.Count > 0)
            {
                CreateInputRemap(GetCoreName(core), cfg =>
                {
                    foreach (var remap in InputRemap)
                        cfg[remap.Key] = remap.Value;
                });
            }
            else
                DeleteInputRemap(GetCoreName(core));
        }

        #region Core configuration
        private void Configure4Do(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "4do")
                return;

            BindFeature(coreSettings, "4do_high_resolution", "high_resolution", "enabled");
            BindFeature(coreSettings, "4do_cpu_overclock", "cpu_overclock", "1.0x (12.50Mhz)");
            BindFeature(coreSettings, "4do_bios", "4do_bios", "Panasonic FZ-1");
            BindFeature(coreSettings, "4do_region", "4do_region", "ntsc");

            // Game hacks
            string rom = SystemConfig["rom"].AsIndexedRomName();
            foreach (var hackName in operaHacks.Select(h => h.Value).Distinct())
                coreSettings["4do_" + hackName] = operaHacks.Any(h => h.Value == hackName && rom.Contains(h.Key)) ? "enabled" : "disabled";

            // If ROM includes the word 'Disc', assume it's a multi disc game, and enable shared nvram if the option isn't set.
            if (Features.IsSupported("4do_nvram_storage"))
            {
                if (SystemConfig.isOptSet("nvram_storage"))
                    coreSettings["4do_nvram_storage"] = SystemConfig["nvram_storage"];
                else if (!string.IsNullOrEmpty(SystemConfig["rom"]) && SystemConfig["rom"].ToLower().Contains("disc"))
                    coreSettings["4do_nvram_storage"] = "shared";
                else
                    coreSettings["4do_nvram_storage"] = "per game";
            }

            // Controls
            BindFeature(coreSettings, "4do_active_devices", "active_devices", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p1", "4do_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "4do_controller2", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p3", "4do_controller3", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p4", "4do_controller4", "1");

            // Lightgun
            SetupLightGuns(retroarchConfig, "260", core);
        }

        private void Configure81(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "81")
                return;

            BindFeature(coreSettings, "81_hide_border", "81_hide_border", "disabled");
            BindFeature(coreSettings, "81_highres", "81_highres", "auto");
            BindFeature(coreSettings, "81_chroma_81", "81_chroma_81", "auto");
            BindFeature(coreSettings, "81_video_presets", "81_video_presets", "clean");

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "zx81_controller1", "257");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "zx81_controller2", "259");
        }

        private void Configurea5200(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "a5200")
                return;

            BindFeature(coreSettings, "a5200_mix_frames", "a5200_mix_frames", "disabled");

            // Audio Filter
            if (Features.IsSupported("a5200_low_pass_filter"))
            {
                if (SystemConfig.isOptSet("a5200_low_pass_filter") && SystemConfig["a5200_low_pass_filter"] != "0")
                {
                    coreSettings["a5200_low_pass_filter"] = "enabled";
                    coreSettings["a5200_low_pass_range"] = SystemConfig["a5200_low_pass_filter"];
                }
                else
                {
                    coreSettings["a5200_low_pass_filter"] = "disabled";
                    coreSettings["a5200_low_pass_range"] = "60";
                }
            }

            // Controls
            BindFeature(coreSettings, "a5200_input_hack", "a5200_input_hack", "disabled");
            BindFeature(retroarchConfig, "input_libretro_device_p1", "a5200_controller1", "1");
        }

        private void ConfigureAtari800(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "atari800")
                return;

            bool atari800 = (system == "atari800");
            bool atariXE = !atari800 && system.IndexOf("xe", StringComparison.InvariantCultureIgnoreCase) >= 0;

            BindFeature(coreSettings, "atari800_artifacting", "atari800_artifacting", "disabled");
            BindFeature(coreSettings, "atari800_ntscpal", "atari800_ntscpal", "NTSC");
            BindFeature(coreSettings, "atari800_resolution", "atari800_resolution", "336x240");

            if (atari800)
            {
                var romExt = Path.GetExtension(Program.SystemConfig["rom"]).ToLower();

                coreSettings["atari800_internalbasic"] = (romExt == ".bas" ? "enabled" : "disabled");
                coreSettings["atari800_cassboot"] = (romExt == ".cas" ? "enabled" : "disabled");
                coreSettings["atari800_opt1"] = "disabled"; // detect card type

                BindFeature(coreSettings, "atari800_system", "atari800_system", "800XL (64K)", true);
                BindFeature(coreSettings, "atari800_sioaccel", "atari800_sioaccel", "enabled");
            }
            else if (atariXE)
            {
                coreSettings["atari800_system"] = "130XE (128K)";
                coreSettings["atari800_internalbasic"] = "disabled";
                coreSettings["atari800_opt1"] = "enabled";
                coreSettings["atari800_cassboot"] = "disabled";

                BindFeature(coreSettings, "atari800_sioaccel", "atari800_sioaccel", "enabled");

            }
            else // Atari 5200
            {
                coreSettings["atari800_system"] = "5200";
                coreSettings["atari800_opt1"] = "enabled"; // detect card type
                coreSettings["atari800_cassboot"] = "disabled";

                BindFeature(coreSettings, "atari800_opt2", "atari800_opt2", "disabled");    // Robotron joystick hack
            }

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "a800_controller1", "513");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "a800_controller2", "513");

            if (string.IsNullOrEmpty(AppConfig["bios"]))
                return;

            var atariCfg = ConfigFile.FromFile(Path.Combine(RetroarchPath, ".atari800.cfg"), new ConfigFileOptions() { CaseSensitive = true, KeepEmptyValues = true, KeepEmptyLines = true });
            if (!atariCfg.Any())
                atariCfg.AppendLine("Atari 800 Emulator, Version 3.1.0");

            string biosPath = AppConfig.GetFullPath("bios");
            atariCfg["ROM_OS_A_PAL"] = Path.Combine(biosPath, "ATARIOSA.ROM");
            atariCfg["ROM_OS_BB01R2"] = Path.Combine(biosPath, "ATARIXL.ROM");
            atariCfg["ROM_BASIC_C"] = Path.Combine(biosPath, "ATARIBAS.ROM");
            atariCfg["ROM_400/800_CUSTOM"] = Path.Combine(biosPath, "ATARIOSB.ROM");
            atariCfg["ROM_XL/XE_CUSTOM"] = Path.Combine(biosPath, "ATARIXL.ROM");
            atariCfg["ROM_5200"] = Path.Combine(biosPath, "5200.ROM");
            atariCfg["ROM_5200_CUSTOM"] = Path.Combine(biosPath, "atari5200.ROM");

            atariCfg["OS_XL/XE_VERSION"] = "AUTO";
            atariCfg["OS_5200_VERSION"] = "AUTO";
            atariCfg["BASIC_VERSION"] = "AUTO";
            atariCfg["XEGS_GAME_VERSION"] = "AUTO";
            atariCfg["OS_400/800_VERSION"] = "AUTO";

            atariCfg["CASSETTE_FILENAME"] = null;
            atariCfg["CASSETTE_LOADED"] = "0";
            atariCfg["CARTRIDGE_FILENAME"] = null;
            atariCfg["CARTRIDGE_TYPE"] = "0";

            if (atari800)
            {
                atariCfg["MACHINE_TYPE"] = "Atari XL/XE";
                atariCfg["RAM_SIZE"] = "64";
                atariCfg["DISABLE_BASIC"] = "0";
            }
            else if (atariXE)
            {
                atariCfg["MACHINE_TYPE"] = "Atari XL/XE";
                atariCfg["RAM_SIZE"] = "128";
                atariCfg["DISABLE_BASIC"] = "1";

                var rom = Program.SystemConfig["rom"];
                if (File.Exists(rom))
                {
                    atariCfg["CARTRIDGE_FILENAME"] = rom;

                    try
                    {
                        var ln = new FileInfo(rom).Length;
                        if (ln == 131072)
                            atariCfg["CARTRIDGE_TYPE"] = "14";
                        else if (ln == 65536)
                            atariCfg["CARTRIDGE_TYPE"] = "13";
                    }
                    catch { }
                }
            }
            else // Atari 5200
            {
                atariCfg["ROM_OS_A_PAL"] = "";
                atariCfg["ROM_OS_BB01R2"] = "";
                atariCfg["ROM_BASIC_C"] = "";
                atariCfg["ROM_400/800_CUSTOM"] = "";

                atariCfg["MACHINE_TYPE"] = "Atari 5200";
                atariCfg["RAM_SIZE"] = "16";
                atariCfg["DISABLE_BASIC"] = "1";
            }

            atariCfg.Save(Path.Combine(RetroarchPath, ".atari800.cfg"), false);
        }

        private void ConfigureBoom3(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "boom3" && core != "boom3_xp")
                return;

            BindFeature(retroarchConfig, "input_libretro_device_p1", "Doom3ControllerP1", "1");
        }

        private void ConfigureBlueMsx(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "bluemsx")
                return;

            coreSettings["bluemsx_overscan"] = (system == "msx2" || system == "msx2+" || system == "msxturbor") ? "MSX2" : "enabled";

            if (system == "spectravideo")
                coreSettings["bluemsx_msxtype"] = "SVI - Spectravideo SVI-328 MK2";
            else if (system == "colecovision")
                coreSettings["bluemsx_msxtype"] = "ColecoVision";
            else if (system == "msx1")
                coreSettings["bluemsx_msxtype"] = "MSX";
            else if (system == "msx2")
                coreSettings["bluemsx_msxtype"] = "MSX2";
            else if (system == "msx2+")
                coreSettings["bluemsx_msxtype"] = "MSX2+";
            else if (system == "msxturbor")
                coreSettings["bluemsx_msxtype"] = "MSXturboR";
            else
                coreSettings["bluemsx_msxtype"] = "Auto";

            BindFeature(coreSettings, "bluemsx_vdp_synctype", "bluemsx_vdp_synctype", "Auto");
            BindFeature(coreSettings, "bluemsx_nospritelimits", "bluemsx_nospritelimits", "OFF");

            // Controls (257 does not exist for BlueMSX core, it's either Retropad "1" or RetroKeyboard "3"
            /*var sysDevices = new Dictionary<string, string>() { { "msx", "257" }, { "msx1", "257" }, { "msx2", "257" }, { "colecovision", "1" } };

            if (sysDevices.ContainsKey(system))
                retroarchConfig["input_libretro_device_p1"] = sysDevices[system];

            if (sysDevices.ContainsKey(system))
                retroarchConfig["input_libretro_device_p2"] = sysDevices[system];*/

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "bluemsx_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "bluemsx_controller2", "1");
        }

        private void Configurebsnes(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "bsnes_hd_beta" && core != "bsnes")
                return;

            BindFeature(coreSettings, "bsnes_mode7_scale", "bsnes_mode7_scale", "2x");
            BindFeature(coreSettings, "bsnes_mode7_perspective", "bsnes_mode7_perspective", core == "bsnes" ? "ON" : "auto (wide)");
            BindFeature(coreSettings, "bsnes_mode7_supersample", "bsnes_mode7_supersample", core == "bsnes" ? "OFF" : "none");
            BindFeature(coreSettings, "bsnes_ppu_show_overscan", "bsnes_ppu_show_overscan", "OFF");
            BindFeature(coreSettings, "bsnes_blur_emulation", "bsnes_blur_emulation", "OFF");
            BindFeature(coreSettings, "bsnes_hotfixes", "bsnes_hotfixes", "OFF");
            BindFeature(coreSettings, "bsnes_cpu_fastmath", "bsnes_cpu_fastmath", "OFF");
            BindFeature(coreSettings, "bsnes_run_ahead_frames", "bsnes_run_ahead_frames", "OFF");
            BindBoolFeature(coreSettings, "bsnes_ppu_no_sprite_limit", "bsnes_ppu_no_sprite_limit", "ON", "OFF");

            // Overclock (1 setting for all)
            BindFeature(coreSettings, "bsnes_cpu_overclock", "bsnes_overclock", "100");
            BindFeature(coreSettings, "bsnes_cpu_sa1_overclock", "bsnes_overclock", "100");
            BindFeature(coreSettings, "bsnes_cpu_sfx_overclock", "bsnes_overclock", "100");

            // bsnes only features
            if (core == "bsnes")
            {
                BindFeature(coreSettings, "bsnes_aspect_ratio", "bsnes_aspect_ratio", "Auto");
                BindFeature(coreSettings, "bsnes_video_filter", "bsnes_video_filter", "None");
            }

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "SnesControllerP1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "SnesControllerP2", "1");

            // Gun configuration only for bsnes as bsnes_hd_beta does not have lightgun
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                string gunId = "260";

                var gunInfo = Program.GunGames.FindGame(system, SystemConfig["rom"]);
                if (gunInfo != null && gunInfo.GunType == "justifier")
                    gunId = "516";

                if (core == "bsnes")
                {
                    coreSettings["bsnes_touchscreen_lightgun_superscope_reverse"] = (gunInfo != null && gunInfo.ReversedButtons ? "ON" : "OFF");
                    coreSettings["bsnes_touchscreen_lightgun"] = "ON";
                }

                SetupLightGuns(retroarchConfig, gunId, core, 2);
            }
        }

        private void ConfigureCap32(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "cap32")
                return;

            coreSettings["cap32_autorun"] = "enabled";

            // Virtual Keyboard by default (select+start) change to (start+Y)
            coreSettings["cap32_combokey"] = "y";

            //  Auto Select Model
            if (system == "gx4000")
                coreSettings["cap32_model"] = "6128+ (experimental)";
            else
                BindFeature(coreSettings, "cap32_model", "cap32_model", "6128");

            BindFeature(coreSettings, "cap32_lang_layout", "cap32_lang_layout", "english");
            BindFeature(coreSettings, "cap32_ram", "cap32_ram", "128");
            BindFeature(coreSettings, "cap32_floppy_sound", "cap32_floppy_sound", "enabled");
            BindFeature(coreSettings, "cap32_gfx_colors", "cap32_gfx_colors", "16bit");
            BindFeature(coreSettings, "cap32_scr_tube", "cap32_scr_tube", "color");
            BindFeature(coreSettings, "cap32_scr_intensity", "cap32_scr_intensity", "8");

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "cap32_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "cap32_controller2", "1");

            BindFeature(coreSettings, "cap32_lightgun_input", "cap32_lightgun_input", "disabled");
            BindFeature(coreSettings, "cap32_lightgun_show", "cap32_lightgun_show", "disabled");

            SetupLightGuns(retroarchConfig, "260", core, 1);
        }

        private void ConfigureCitra(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "citra")
                return;

            coreSettings["citra_use_libretro_save_path"] = "LibRetro Default";
            coreSettings["citra_is_new_3ds"] = "New 3DS";

            if (SystemConfig.isOptSet("citra_layout_option"))
            {
                coreSettings["citra_layout_option"] = SystemConfig["citra_layout_option"];
                if ((SystemConfig["citra_layout_option"] == "Large Screen, Small Screen") && !SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
                {
                    retroarchConfig["aspect_ratio_index"] = "1";
                    SystemConfig["bezel"] = "none";
                }
                else if ((SystemConfig["citra_layout_option"] == "Single Screen Only") && !SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
                {
                    retroarchConfig["aspect_ratio_index"] = "2";
                    SystemConfig["bezel"] = "none";
                }
                else if ((SystemConfig["citra_layout_option"] == "Side by Side") && !SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
                {
                    retroarchConfig["aspect_ratio_index"] = "4";
                    SystemConfig["bezel"] = "none";
                }
                else
                    SystemConfig["bezel"] = SystemConfig["bezel"];
            }
            else
                coreSettings["citra_layout_option"] = "Default Top-Bottom Screen";

            BindFeature(coreSettings, "citra_region_value", "citra_region_value", "Auto");
            BindFeature(coreSettings, "citra_language", "citra_language", "English");
            BindFeature(coreSettings, "citra_resolution_factor", "citra_resolution_factor", "1x (Native)");
            BindFeature(coreSettings, "citra_swap_screen", "citra_swap_screen", "Top");
            BindFeature(coreSettings, "citra_custom_textures", "citra_custom_textures", "disabled");

            BindFeature(coreSettings, "citra_analog_function", "citra_analog_function", "C-Stick and Touchscreen Pointer");
            BindFeature(coreSettings, "citra_mouse_touchscreen", "citra_mouse_touchscreen", "enabled");
            BindFeature(coreSettings, "citra_render_touchscreen", "citra_render_touchscreen", "disabled");
        }

        private void ConfigureCraft(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "craft")
                return;

            BindFeature(coreSettings, "craft_resolution", "craft_resolution", "640x480");
            BindFeature(coreSettings, "craft_show_info_text", "craft_show_info_text", "disabled");
            BindFeature(coreSettings, "craft_inverted_aim", "craft_inverted_aim", "disabled");
            BindFeature(coreSettings, "craft_draw_distance", "craft_draw_distance", "10");
            BindFeature(coreSettings, "craft_field_of_view", "craft_field_of_view", "65");
        }

        private void ConfigureCrocoDS(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "crocods")
                return;

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "crocods_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "crocods_controller2", "1");
        }


        private void ConfigureDesmume(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "desmume" && core != "desmume2015")
                return;

            coreSettings["desmume_pointer_mouse"] = "enabled";
            coreSettings["desmume_pointer_type"] = "mouse";

            BindFeature(coreSettings, "desmume_cpu_mode", "desmume_cpu_mode", "interpreter");
            BindFeature(coreSettings, "desmume_pointer_device_r", "desmume_rightanalog", "emulated");
            BindFeature(coreSettings, "desmume_internal_resolution", "desmume_internal_resolution", "256x192");
            BindFeature(coreSettings, "desmume_screens_layout", "desmume_screens_layout", "top/bottom");
            BindFeature(coreSettings, "desmume_firmware_language", "desmume_firmware_language", "Auto");

            if (core == "desmume")
            {
                BindFeature(coreSettings, "desmume_use_external_bios", "desmume_use_external_bios", "disabled");
                BindFeature(coreSettings, "desmume_boot_into_bios", "desmume_boot_into_bios", "disabled");
                
                // Force interpreter if boot to bios is active
                if (SystemConfig.isOptSet("desmume_boot_into_bios") && SystemConfig["desmume_boot_into_bios"] == "enabled")
                    coreSettings["desmume_cpu_mode"] = "interpreter";

                // OpenGL options
                BindFeature(coreSettings, "desmume_opengl_mode", "desmume_opengl_mode", "disabled");
                BindFeature(coreSettings, "desmume_color_depth", "desmume_color_depth", "16-bit");
                BindFeature(coreSettings, "desmume_gfx_multisampling", "desmume_gfx_multisampling", "disabled");
                BindFeature(coreSettings, "desmume_gfx_texture_smoothing", "desmume_gfx_texture_smoothing", "disabled");
            }
        }

        private void ConfigureDolphin(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "dolphin")
                return;

            if (!SystemConfig.isOptSet("input_driver"))
                retroarchConfig["input_driver"] = "xinput";

            retroarchConfig["driver_switch_enable"] = "false";

            coreSettings["dolphin_renderer"] = "Hardware";
            coreSettings["dolphin_cpu_core"] = "JIT64";
            coreSettings["dolphin_dsp_hle"] = "enabled";
            coreSettings["dolphin_dsp_jit"] = "enabled";
            coreSettings["dolphin_widescreen_hack"] = "disabled";

            BindFeature(coreSettings, "dolphin_efb_scale", "dolphin_efb_scale", "x1 (640 x 528)");
            BindFeature(coreSettings, "dolphin_max_anisotropy", "dolphin_max_anisotropy", "1x");
            BindFeature(coreSettings, "dolphin_shader_compilation_mode", "dolphin_shader_compilation_mode", "sync");
            BindFeature(coreSettings, "dolphin_wait_for_shaders", "dolphin_wait_for_shaders", "disabled");
            BindFeature(coreSettings, "dolphin_load_custom_textures", "dolphin_load_custom_textures", "disabled");
            BindFeature(coreSettings, "dolphin_cache_custom_textures", "dolphin_cache_custom_textures", "disabled");
            BindFeature(coreSettings, "dolphin_enable_rumble", "dolphin_enable_rumble", "enabled");
            BindFeature(coreSettings, "dolphin_osd_enabled", "dolphin_osd_enabled", "disabled");
            BindFeature(coreSettings, "dolphin_cheats_enabled", "dolphin_cheats_enabled", "disabled");
            BindFeature(coreSettings, "dolphin_force_texture_filtering", "dolphin_force_texture_filtering", "disabled");
            BindFeature(coreSettings, "dolphin_language", "dolphin_language", "English");
            BindFeature(coreSettings, "dolphin_pal60", "dolphin_pal60", "disabled");
            BindFeature(coreSettings, "dolphin_progressive_scan", "dolphin_progressive_scan", "enabled");

            // Wii Controllers
            BindFeature(retroarchConfig, "input_libretro_device_p1", "dolphin_p1_controller", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "dolphin_p2_controller", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p3", "dolphin_p3_controller", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p4", "dolphin_p4_controller", "1");

            // gamecube
            if (system == "gamecube" || system == "gc")
            {
                BindFeature(coreSettings, "dolphin_widescreen_hack", "dolphin_widescreen_hack", "disabled");

                try
                {
                    // use Dolphin.ini for options not available in retroarch-core-options.cfg
                    string iniPath = Path.Combine(AppConfig.GetFullPath("saves"), "gamecube", "User", "Config", "Dolphin.ini");
                    if (File.Exists(iniPath))
                    {
                        using (var ini = new IniFile(iniPath, IniOptions.UseSpaces))
                        {
                            // Skip BIOS or not (IPL.bin required in saves\gamecube\User\GC\<EUR, JAP or USA>)
                            if (SystemConfig.isOptSet("skip_bios"))
                                ini.WriteValue("Core", "SkipIPL", SystemConfig["skip_bios"]);
                            else
                                ini.WriteValue("Core", "SkipIPL", "True");
                        }
                    }
                }
                catch { }

            }

            // wii
            if (system == "wii")
            {
                if (_isWidescreen || !SystemConfig.isOptSet("ratio"))
                    coreSettings["dolphin_widescreen"] = "enabled";
                else
                    coreSettings["dolphin_widescreen"] = "disabled";

                BindFeature(coreSettings, "dolphin_sensor_bar_position", "dolphin_sensor_bar_position", "Bottom");
            }
        }

        private void ConfigureDosboxPure(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "dosbox_pure")
                return;

            coreSettings["dosbox_pure_advanced"] = "true";
            coreSettings["dosbox_pure_savestate"] = "on";
            retroarchConfig["video_font_enable"] = "false"; // Disable OSD for dosbox_pure

            BindFeature(coreSettings, "dosbox_pure_aspect_correction", "ratio", "true");
            BindFeature(coreSettings, "dosbox_pure_cga", "cga", "early_auto");
            BindFeature(coreSettings, "dosbox_pure_cpu_core", "cpu_core", "auto");
            BindFeature(coreSettings, "dosbox_pure_cpu_type", "cpu_type", "auto");
            BindFeature(coreSettings, "dosbox_pure_cycles", "cycles", "auto");
            BindFeature(coreSettings, "dosbox_pure_gus", "gus", "false");
            BindFeature(coreSettings, "dosbox_pure_hercules", "hercules", "white");
            BindFeature(coreSettings, "dosbox_pure_machine", "machine", "svga");
            BindFeature(coreSettings, "dosbox_pure_memory_size", "memory_size", "16");
            BindFeature(coreSettings, "dosbox_pure_menu_time", "menu_time", "5");
            BindFeature(coreSettings, "dosbox_pure_midi", "midi", "scummvm/extra/Roland_SC-55.sf2");
            BindFeature(coreSettings, "dosbox_pure_on_screen_keyboard", "on_screen_keyboard", "true");
            BindFeature(coreSettings, "dosbox_pure_sblaster_adlib_emu", "sblaster_adlib_emu", "default");
            BindFeature(coreSettings, "dosbox_pure_sblaster_adlib_mode", "sblaster_adlib_mode", "auto");
            BindFeature(coreSettings, "dosbox_pure_sblaster_conf", "sblaster_conf", "A220 I7 D1 H5");
            BindFeature(coreSettings, "dosbox_pure_sblaster_type", "sblaster_type", "sb16");
            BindFeature(coreSettings, "dosbox_pure_svga", "svga", "vesa_nolfb");
            BindFeature(coreSettings, "dosbox_pure_keyboard_layout", "keyboard_layout", "us");
            BindFeature(coreSettings, "dosbox_pure_force60fps", "dosbox_pure_force60fps", "false");
            BindFeature(coreSettings, "dosbox_pure_perfstats", "dosbox_pure_perfstats", "none");
            BindFeature(coreSettings, "dosbox_pure_conf", "dosbox_pure_conf", "false");
            BindFeature(coreSettings, "dosbox_pure_voodoo", "dosbox_pure_voodoo", "off");
            BindFeature(coreSettings, "dosbox_pure_voodoo_perf", "dosbox_pure_voodoo_perf", "1");
            BindFeature(coreSettings, "dosbox_pure_bootos_ramdisk", "dosbox_pure_bootos_ramdisk", "false");
            BindFeature(coreSettings, "dosbox_pure_bootos_forcenormal", "dosbox_pure_bootos_forcenormal", "false");
            BindFeature(coreSettings, "dosbox_pure_auto_mapping", "dosbox_pure_auto_mapping", "false");
            BindFeature(coreSettings, "dosbox_pure_bind_unused", "dosbox_pure_bind_unused", "false");

            // Controller type
            BindFeature(retroarchConfig, "input_libretro_device_p1", "dos_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "dos_controller2", "1");
        }

        private void Configureecwolf(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "ecwolf")
                return;

            BindFeature(coreSettings, "ecwolf-resolution", "ecwolf_resolution", "320x200");
            BindFeature(coreSettings, "ecwolf-fps", "ecwolf_fps", "35");
            BindFeature(coreSettings, "ecwolf-palette", "ecwolf_palette", "rgb565");
            BindFeature(coreSettings, "ecwolf-aspect", "ecwolf_ratio", "auto");
            BindFeature(coreSettings, "ecwolf-analog-deadzone", "ecwolf_analog_deadzone", "15%");
            BindFeature(coreSettings, "ecwolf-analog-move-sensitivity", "ecwolf_analog_sensitivity", "10");
            BindFeature(coreSettings, "ecwolf-analog-turn-sensitivity", "ecwolf_analog_sensitivity", "10");
            BindBoolFeature(coreSettings, "ecwolf-alwaysrun", "ecwolf_run", "enabled", "disabled");

        }

        private void ConfigureEmuscv(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "emuscv")
                return;

            BindFeature(coreSettings, "emuscv_checkbios", "emuscv_checkbios", "AUTO");
            BindFeature(coreSettings, "emuscv_console", "emuscv_console", "AUTO");
            BindFeature(coreSettings, "emuscv_display", "emuscv_display", "AUTO");
            BindFeature(coreSettings, "emuscv_fps", "emuscv_fps", "AUTO");
            BindFeature(coreSettings, "emuscv_langage", "emuscv_langage", "AUTO");
            BindFeature(coreSettings, "emuscv_palette", "emuscv_palette", "AUTO");
            BindFeature(coreSettings, "emuscv_pixelaspect", "emuscv_pixelaspect", "AUTO");
            BindFeature(coreSettings, "emuscv_resolution", "emuscv_resolution", "AUTO");
        }

        private void ConfigureFbalpha(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha")
                return;

            BindFeature(coreSettings, "fba-vertical-mode", "fba_vertical_mode", "disabled");

            // Controls
            if (SystemConfig.isOptSet("fba_controller") && !string.IsNullOrEmpty(SystemConfig["fba_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["fba_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }
        }

        private void ConfigureFbalpha2012(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012")
                return;

            BindFeature(coreSettings, "fbneo-vertical-mode", "fba2012_vertical_mode", "disabled");

            // Controllers
            if (SystemConfig.isOptSet("fba2012_controller") && !string.IsNullOrEmpty(SystemConfig["fba2012_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["fba2012_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }
        }

        private void ConfigureFbalphaCPS1(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps1")
                return;

            coreSettings["fba2012cps1_frameskip"] = "disabled";
            coreSettings["fba2012cps1_aspect"] = "DAR";

            BindFeature(coreSettings, "fba2012cps1_auto_rotate", "fba2012cps1_auto_rotate", "enabled");
            BindFeature(coreSettings, "fba2012cps1_cpu_speed_adjust", "fba2012cps1_cpu_speed_adjust", "100");
            BindFeature(coreSettings, "fba2012cps1_hiscores", "fba2012cps1_hiscores", "enabled");

            if (Features.IsSupported("fba2012cps1_lowpass_range"))
            {
                if (SystemConfig.isOptSet("fba2012cps1_lowpass_range") && SystemConfig["fba2012cps1_lowpass_range"] != "0")
                {
                    coreSettings["fba2012cps1_lowpass_filter"] = "enabled";
                    coreSettings["fba2012cps1_lowpass_range"] = SystemConfig["fba2012cps1_lowpass_range"];
                }
                else
                {
                    coreSettings["fba2012cps1_lowpass_filter"] = "disabled";
                    coreSettings["fba2012cps1_lowpass_range"] = "60";
                }
            }
        }

        private void ConfigureFbalphaCPS2(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps2")
                return;

            coreSettings["fba2012cps2_frameskip"] = "disabled";
            coreSettings["fba2012cps2_aspect"] = "DAR";

            BindFeature(coreSettings, "fba2012cps2_auto_rotate", "fba2012cps2_auto_rotate", "enabled");
            BindFeature(coreSettings, "fba2012cps2_cpu_speed_adjust", "fba2012cps2_cpu_speed_adjust", "100");
            BindFeature(coreSettings, "fba2012cps2_hiscores", "fba2012cps2_hiscores", "enabled");

            // Audio Filter
            if (Features.IsSupported("fba2012cps2_lowpass_range"))
            {
                if (SystemConfig.isOptSet("fba2012cps2_lowpass_range") && SystemConfig["fba2012cps2_lowpass_range"] != "0")
                {
                    coreSettings["fba2012cps2_lowpass_filter"] = "enabled";
                    coreSettings["fba2012cps2_lowpass_range"] = SystemConfig["fba2012cps2_lowpass_range"];
                }
                else
                {
                    coreSettings["fba2012cps2_lowpass_filter"] = "disabled";
                    coreSettings["fba2012cps2_lowpass_range"] = "60";
                }
            }

            BindFeature(coreSettings, "fba2012cps2_controls", "fba2012cps2_controls", "gamepad");
        }

        private void ConfigureFbalphaCPS3(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbalpha2012_cps3")
                return;

            coreSettings["fbalpha2012_cps3_frameskip"] = "0";
            coreSettings["fbalpha2012_cps3_aspect"] = "DAR";

            BindFeature(coreSettings, "fbalpha2012_cps3_cpu_speed_adjust", "fbalpha2012_cps3_cpu_speed_adjust", "100");
            BindFeature(coreSettings, "fbalpha2012_cps3_hiscores", "fbalpha2012_cps3_hiscores", "enabled");
            BindFeature(coreSettings, "fbalpha2012_cps3_controls_p1", "fbalpha2012_cps3_controls_p1", "gamepad");
            BindFeature(coreSettings, "fbalpha2012_cps3_controls_p2", "fbalpha2012_cps3_controls_p2", "gamepad");
            BindFeature(coreSettings, "fbalpha2012_cps3_lr_controls_p1", "fbalpha2012_cps3_lr_controls_p1", "normal");
            BindFeature(coreSettings, "fbalpha2012_cps3_lr_controls_p2", "fbalpha2012_cps3_lr_controls_p2", "normal");
        }

        private void ConfigureFbneo(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fbneo")
                return;

            coreSettings["fbneo-allow-depth-32"] = "enabled";

            if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements") && SystemConfig.getOptBoolean("retroachievements.hardcore"))
                coreSettings["fbneo-allow-patched-romsets"] = "disabled";
            else
                coreSettings["fbneo-allow-patched-romsets"] = "enabled";

            coreSettings["fbneo-memcard-mode"] = "per-game";
            coreSettings["fbneo-hiscores"] = "enabled";
            coreSettings["fbneo-load-subsystem-from-parent"] = "enabled";
            coreSettings["fbneo-fm-interpolation"] = "4-point 3rd order";
            coreSettings["fbneo-sample-interpolation"] = "4-point 3rd order";

            BindFeature(coreSettings, "fbneo-neogeo-mode", "fbneo-neogeo-mode", "UNIBIOS");
            BindFeature(coreSettings, "fbneo-vertical-mode", "fbneo-vertical-mode", "disabled");

            if (SystemConfig.isOptSet("fbneo-lightgun-hide-crosshair") && SystemConfig["fbneo-lightgun-hide-crosshair"] == "disabled")
            {
                coreSettings["fbneo-lightgun-crosshair-emulation"] = "always show";
                coreSettings["fbneo-lightgun-hide-crosshair"] = "disabled";
            }
            else
            {
                coreSettings["fbneo-lightgun-crosshair-emulation"] = "always hide";
                coreSettings["fbneo-lightgun-hide-crosshair"] = "enabled";
            }

            // Controls
            if (SystemConfig.isOptSet("fbneo_controller") && !string.IsNullOrEmpty(SystemConfig["fbneo_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["fbneo_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            SetupLightGuns(retroarchConfig, "4", core);
        }

        private void ConfigureFCEumm(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fceumm")
                return;

            if (Features.IsSupported("fceumm_cropoverscan"))
            {
                if (SystemConfig.isOptSet("fceumm_cropoverscan") && SystemConfig["fceumm_cropoverscan"] == "none")
                {
                    coreSettings["fceumm_overscan_h"] = "disabled";
                    coreSettings["fceumm_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("fceumm_cropoverscan") && SystemConfig["fceumm_cropoverscan"] == "h")
                {
                    coreSettings["fceumm_overscan_h"] = "enabled";
                    coreSettings["fceumm_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("fceumm_cropoverscan") && SystemConfig["fceumm_cropoverscan"] == "v")
                {
                    coreSettings["fceumm_overscan_h"] = "disabled";
                    coreSettings["fceumm_overscan_v"] = "enabled";
                }
                else
                {
                    coreSettings["fceumm_overscan_h"] = "enabled";
                    coreSettings["fceumm_overscan_v"] = "enabled";
                }
            }

            BindFeature(coreSettings, "fceumm_palette", "fceumm_palette", "default");
            BindFeature(coreSettings, "fceumm_ntsc_filter", "fceumm_ntsc_filter", "disabled");
            BindFeature(coreSettings, "fceumm_sndquality", "fceumm_sndquality", "Low");
            BindFeature(coreSettings, "fceumm_overclocking", "fceumm_overclocking", "disabled");
            BindFeature(coreSettings, "fceumm_nospritelimit", "fceumm_nospritelimit", "enabled");
            BindFeature(coreSettings, "fceumm_show_crosshair", "fceumm_show_crosshair", "enabled");
            BindFeature(coreSettings, "fceumm_zapper_mode", "gun_input", "clightgun");

            // MULTI-TAP for 4 players
            if (SystemConfig.isOptSet("fceumm_multitap") && SystemConfig.getOptBoolean("fceumm_multitap"))
            {
                retroarchConfig["input_libretro_device_p3"] = "513";
                retroarchConfig["input_libretro_device_p4"] = "513";
                retroarchConfig["input_libretro_device_p5"] = "769";
            }

            SetupLightGuns(retroarchConfig, "258", core, 2);
        }

        private void ConfigureFlycast(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "flycast")
                return;

            coreSettings["reicast_system"] = "auto";
            coreSettings["reicast_show_lightgun_settings"] = "enabled";
            coreSettings["reicast_threaded_rendering"] = "enabled";
            coreSettings["reicast_enable_purupuru"] = "enabled"; // Enable controller force feedback

            BindFeature(coreSettings, "reicast_widescreen_hack", "widescreen_hack", "disabled");
            BindFeature(coreSettings, "reicast_widescreen_cheats", "widescreen_cheats", "disabled");
            BindFeature(coreSettings, "reicast_screen_rotation", "reicast_screen_rotation", "horizontal");

            if (SystemConfig["widescreen_hack"] == "enabled")
            {
                retroarchConfig["aspect_ratio_index"] = "1";
                SystemConfig["bezel"] = "none";
            }

            BindFeature(coreSettings, "reicast_texture_filtering", "reicast_texture_filtering", "0");
            BindFeature(coreSettings, "reicast_anisotropic_filtering", "anisotropic_filtering", "off");
            BindFeature(coreSettings, "reicast_texupscale", "texture_upscaling", "1");
            BindFeature(coreSettings, "reicast_render_to_texture_upscaling", "render_to_texture_upscaling", "1x");
            BindFeature(coreSettings, "reicast_force_wince", "force_wince", "disabled");
            BindFeature(coreSettings, "reicast_cable_type", "cable_type", "TV (RGB)");
            BindFeature(coreSettings, "reicast_broadcast", "reicast_broadcast", "Default");
            BindFeature(coreSettings, "reicast_internal_resolution", "internal_resolution", "640x480");
            BindFeature(coreSettings, "reicast_force_freeplay", "reicast_force_freeplay", "disabled");
            BindFeature(coreSettings, "reicast_allow_service_buttons", "reicast_allow_service_buttons", "disabled");
            BindFeature(coreSettings, "reicast_boot_to_bios", "reicast_boot_to_bios", "disabled");
            BindFeature(coreSettings, "reicast_hle_bios", "reicast_hle_bios", "disabled");
            BindFeature(coreSettings, "reicast_per_content_vmus", "reicast_per_content_vmus", "disabled");
            BindFeature(coreSettings, "reicast_language", "reicast_language", "English");
            BindFeature(coreSettings, "reicast_region", "reicast_region", "Japan");
            BindFeature(coreSettings, "reicast_dump_textures", "reicast_dump_textures", "disabled");
            BindFeature(coreSettings, "reicast_custom_textures", "reicast_custom_textures", "disabled");
            BindFeature(coreSettings, "reicast_alpha_sorting", "reicast_alpha_sorting", "per-triangle (normal)");
            BindFeature(coreSettings, "reicast_enable_rttb", "reicast_enable_rttb", "disabled");
            BindFeature(coreSettings, "reicast_mipmapping", "reicast_mipmapping", "disabled");
            BindFeature(coreSettings, "reicast_enable_dsp", "reicast_enable_dsp", "disabled");
            BindFeature(coreSettings, "reicast_pvr2_filtering", "reicast_pvr2_filtering", "disabled");
            BindFeature(coreSettings, "reicast_fog", "reicast_fog", "enabled");
            BindBoolFeature(coreSettings, "reicast_digital_triggers", "reicast_digital_triggers", "enabled", "disabled");
            BindFeature(coreSettings, "reicast_threaded_rendering", "reicast_threaded_rendering", "enabled");

            if (SystemConfig.isOptSet("reicast_frame_skipping") && SystemConfig["reicast_frame_skipping"] != "disabled")
            {
                coreSettings["reicast_frame_skipping"] = SystemConfig["reicast_frame_skipping"];
                coreSettings["reicast_threaded_rendering"] = "enabled";
            }
            else
                coreSettings["reicast_frame_skipping"] = "disabled";

            // Controls
            BindFeature(coreSettings, "reicast_trigger_deadzone", "reicast_trigger_deadzone", "0%");
            BindFeature(coreSettings, "reicast_analog_stick_deadzone", "reicast_analog_stick_deadzone", "15%");

            if (SystemConfig.isOptSet("flycast_controller") && !string.IsNullOrEmpty(SystemConfig["flycast_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["flycast_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            BindFeature(coreSettings, "reicast_lightgun1_crosshair", "reicast_lightgun1_crosshair", "disabled");
            BindFeature(coreSettings, "reicast_lightgun2_crosshair", "reicast_lightgun2_crosshair", "disabled");

            SetupLightGuns(retroarchConfig, "4", core);

            // Disable "enter" for player 2 in case of guns and 1 joystick connected (as this generate a conflict on ENTER key between player 2 and gun action)
            if (Program.Controllers.Count > 1 && Program.Controllers[1].IsKeyboard && (SystemConfig.getOptBoolean("use_guns") || SystemConfig["flycast_controller1"] == "4") && SystemConfig["flycast_controller2"] != "4")
            {
                retroarchConfig["input_player2_start"] = "";
            }
        }

        private void ConfigureFrodo(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "frodo")
                return;

            BindFeature(coreSettings, "frodo_resolution", "frodo_resolution", "384x288");
        }

        private void ConfigureFuse(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "fuse")
                return;

            coreSettings["fuse_auto_load"] = "enabled";
            coreSettings["fuse_fast_load"] = "enabled";

            BindFeature(coreSettings, "fuse_machine", "fuse_machine", "Spectrum 128K");

            // Player 1 controller - sinclair 1 controller used as default as used by most games
            BindFeature(retroarchConfig, "input_libretro_device_p1", "zx_controller1", "513");

            // Player 2 controller - sinclair 2 as default
            BindFeature(retroarchConfig, "input_libretro_device_p2", "zx_controller2", "513");

            // If using keyboard only option, disable controllers and use keyboard as device_p3 as stated in libretro core documentation
            // 3 options : keyboard only (disables joysticks), joysticks only (disables keyboard) or keyboard + joysticks (add keyboard as p3)
            if (Features.IsSupported("zx_control_type"))
            {
                switch (SystemConfig["zx_control_type"])
                {
                    case "1": // Joystick only
                        retroarchConfig["input_libretro_device_p3"] = "0";
                        break;
                    case "2": // Keyboard only
                        retroarchConfig["input_libretro_device_p1"] = "0";
                        retroarchConfig["input_libretro_device_p2"] = "0";
                        retroarchConfig["input_libretro_device_p3"] = "259";
                        break;
                    default: // Keyboard + joysticks
                        retroarchConfig["input_libretro_device_p3"] = "259";
                        break;
                }
            }
        }

        private void ConfigureGambatte(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "gambatte")
                return;

            coreSettings["gambatte_gb_bootloader"] = "enabled";
            coreSettings["gambatte_gbc_color_correction_mode"] = "accurate";
            coreSettings["gambatte_gbc_color_correction"] = "GBC only";
            coreSettings["gambatte_up_down_allowed"] = "disabled";

            BindFeature(coreSettings, "gambatte_gb_hwmode", "gambatte_gb_hwmode", "Auto");
            BindFeature(coreSettings, "gambatte_mix_frames", "gambatte_mix_frames", "lcd_ghosting");
            BindFeature(coreSettings, "gambatte_gb_internal_palette", "gambatte_gb_internal_palette", "GB - DMG");
            BindFeature(coreSettings, "gambatte_gb_colorization", "gambatte_gb_colorization", "auto");
        }

        private void ConfigureGenesisPlusGX(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "genesis_plus_gx")
                return;

            coreSettings["genesis_plus_gx_bram"] = "per game";

            BindFeature(coreSettings, "genesis_plus_gx_ym2413", "ym2413", "auto");
            BindFeature(coreSettings, "genesis_plus_gx_addr_error", "addr_error", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_lock_on", "lock_on", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_ym2612", "ym2612", "mame (ym2612)");
            BindFeature(coreSettings, "genesis_plus_gx_blargg_ntsc_filter", "ntsc_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_lcd_filter", "lcd_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_overscan", "overscan", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_render", "render", "single field");
            BindFeature(coreSettings, "genesis_plus_gx_force_dtack", "genesis_plus_gx_force_dtack", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_overclock", "genesis_plus_gx_overclock", "100%");
            BindFeature(coreSettings, "genesis_plus_gx_no_sprite_limit", "genesis_plus_gx_no_sprite_limit", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_bios", "genesis_plus_gx_bios", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_add_on", "genesis_plus_gx_add_on", "auto");
            BindFeature(coreSettings, "genesis_plus_gx_left_border", "genesis_plus_gx_left_border", "disabled");

            // Audio Filter
            if (Features.IsSupported("audio_filter"))
            {
                if (SystemConfig.isOptSet("audio_filter") && SystemConfig["audio_filter"] != "0")
                {
                    coreSettings["genesis_plus_gx_audio_filter"] = "low-pass";
                    coreSettings["genesis_plus_gx_lowpass_range"] = SystemConfig["audio_filter"];
                }
                else
                {
                    coreSettings["genesis_plus_gx_audio_filter"] = "disabled";
                    coreSettings["genesis_plus_gx_lowpass_range"] = "60";
                }
            }

            // Controls
            if (SystemConfig.isOptSet("genesis_plus_gx_controller") && !string.IsNullOrEmpty(SystemConfig["genesis_plus_gx_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["genesis_plus_gx_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            BindFeature(coreSettings, "genesis_plus_gx_gun_cursor", "gun_cursor", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_gun_input", "gun_input", "lightgun");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                if (system == "mastersystem")
                    SetupLightGuns(retroarchConfig, "260", core);
                else
                {
                    var gunId = "516";

                    var gunInfo = Program.GunGames.FindGame(system, SystemConfig["rom"]);
                    if (gunInfo != null && gunInfo.GunType == "justifier")
                        gunId = "772";

                    SetupLightGuns(retroarchConfig, gunId, core, 2);
                }
            }
        }

        private void ConfigureGenesisPlusGXWide(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "genesis_plus_gx_wide")
                return;

            if (SystemConfig.isOptSet("ratio") && !SystemConfig.isOptSet("bezel"))
            {
                int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                if (idx == 1 || idx == 2 || idx == 4 || idx == 6 || idx == 7 || idx == 9 || idx == 14 || idx == 16 || idx == 18 || idx == 19)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                }
            }
            else
            {
                retroarchConfig["aspect_ratio_index"] = "1";
                retroarchConfig["video_aspect_ratio_auto"] = "false";
                SystemConfig["bezel"] = "none";
            }

            coreSettings["genesis_plus_gx_wide_bram"] = "per game";

            BindFeature(coreSettings, "genesis_plus_gx_wide_ym2413", "ym2413", "auto");
            BindFeature(coreSettings, "genesis_plus_gx_wide_addr_error", "addr_error", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_lock_on", "lock_on", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_ym2612", "ym2612", "mame (ym2612)");
            BindFeature(coreSettings, "genesis_plus_gx_wide_blargg_ntsc_filter", "ntsc_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_lcd_filter", "lcd_filter", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_overscan", "overscan", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_render", "render", "single field");
            BindFeature(coreSettings, "genesis_plus_gx_wide_force_dtack", "genesis_plus_gx_force_dtack", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_overclock", "genesis_plus_gx_overclock", "100%");
            BindFeature(coreSettings, "genesis_plus_gx_wide_no_sprite_limit", "genesis_plus_gx_no_sprite_limit", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_bios", "genesis_plus_gx_bios", "disabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_add_on", "genesis_plus_gx_add_on", "auto");
            BindFeature(coreSettings, "genesis_plus_gx_wide_h40_extra_columns", "h40_extra_columns", "10");
            BindFeature(coreSettings, "genesis_plus_gx_left_border", "genesis_plus_gx_left_border", "disabled");

            // Audio Filter
            if (Features.IsSupported("audio_filter"))
            {
                if (SystemConfig.isOptSet("audio_filter") && SystemConfig["audio_filter"] != "0")
                {
                    coreSettings["genesis_plus_gx_wide_audio_filter"] = "low-pass";
                    coreSettings["genesis_plus_gx_wide_lowpass_range"] = SystemConfig["audio_filter"];
                }
                else
                {
                    coreSettings["genesis_plus_gx_wide_audio_filter"] = "disabled";
                    coreSettings["genesis_plus_gx_wide_lowpass_range"] = "60";
                }
            }

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "genesis_plus_gx_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "genesis_plus_gx_controller2", "1");

            BindFeature(coreSettings, "genesis_plus_gx_wide_gun_cursor", "gun_cursor", "enabled");
            BindFeature(coreSettings, "genesis_plus_gx_wide_gun_input", "gun_input", "lightgun");

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                if (system == "mastersystem")
                    SetupLightGuns(retroarchConfig, "260", core);
                else
                {
                    var gunId = "516";

                    var gunInfo = Program.GunGames.FindGame(system, SystemConfig["rom"]);
                    if (gunInfo != null && gunInfo.GunType == "justifier")
                        gunId = "772";

                    SetupLightGuns(retroarchConfig, gunId, core, 2);
                }
            }
        }

        private void ConfigureGong(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "gong")
                return;

            BindFeature(coreSettings, "gong_player2", "gong_player2", "CPU");
        }

        private void ConfigureHandy(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "handy")
                return;

            BindFeature(coreSettings, "handy_rot", "handy_rot", "None");
            BindFeature(coreSettings, "handy_gfx_colors", "handy_gfx_colors", "16bit");
            BindFeature(coreSettings, "handy_lcd_ghosting", "handy_lcd_ghosting", "disabled");
        }

        private void ConfigureHatari(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "hatari")
                return;

            if (SystemConfig.isOptSet("hatari_tos") && !string.IsNullOrEmpty(SystemConfig["hatari_tos"]))
                coreSettings["hatari_tosimage"] = SystemConfig["hatari_tos"];
            else
                coreSettings["hatari_tosimage"] = "default";

            BindFeature(coreSettings, "hatari_machinetype", "hatari_machinetype", "st");
            BindFeature(coreSettings, "hatari_ramsize", "hatari_ramsize", "1");

            BindBoolFeature(coreSettings, "hatari_video_crop_overscan", "hatari_video_crop_overscan", "false", "true");
            BindBoolFeature(coreSettings, "hatari_fastboot", "hatari_fastboot", "true", "false");
            BindBoolFeature(coreSettings, "hatari_twojoy", "hatari_twojoy", "false", "true");
            BindBoolFeature(coreSettings, "hatari_led_status_display", "hatari_led_status_display", "false", "true");
        }

        private void ConfigureHatariB(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "hatarib")
                return;

            if (SystemConfig.isOptSet("hatarib_tos") && !string.IsNullOrEmpty(SystemConfig["hatarib_tos"]))
            {
                if (SystemConfig["hatarib_tos"] == "tosimg")
                {
                    string tosImg = Path.Combine(AppConfig.GetFullPath("bios"));
                    if (File.Exists(tosImg))
                        coreSettings["hatarib_tos"] = "<tos.img>";
                }

                else if (SystemConfig["hatarib_tos"].StartsWith("etos"))
                {
                    coreSettings["hatarib_tos"] = "<" + SystemConfig["hatarib_tos"] + ">";
                }

                else
                {
                    string tosFile = "hatarib/" + SystemConfig["hatarib_tos"];
                    string tosPath = Path.Combine(AppConfig.GetFullPath("bios"), "hatarib", SystemConfig["hatarib_tos"]);

                    if (!File.Exists(tosPath))
                        coreSettings["hatarib_tos"] = tosFile;
                }
            }

            else if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "tos.img")))
                coreSettings["hatarib_tos"] = "<tos.img>";

            else
                coreSettings["hatarib_tos"] = "<etos1024k>";

            BindFeature(coreSettings, "hatarib_aspect", "hatarib_aspect", "0");
            BindFeature(coreSettings, "hatarib_monitor", "hatarib_monitor", "1");
            BindFeature(coreSettings, "hatarib_samplerate", "hatarib_samplerate", "48000");
            BindFeature(coreSettings, "hatarib_lpf", "hatarib_lpf", "3");
            BindFeature(coreSettings, "hatarib_machine", "hatarib_machine", "0");
            BindFeature(coreSettings, "hatarib_memory", "hatarib_memory", "1024");
            BindFeature(coreSettings, "hatarib_cpu_clock", "hatarib_cpu_clock", "-1");
            BindFeature(coreSettings, "hatarib_fast_floppy", "hatarib_fast_floppy", "1");
            BindFeature(coreSettings, "hatarib_cycle_exact", "hatarib_cycle_exact", "1");
            BindFeature(coreSettings, "hatarib_mouse_port", "hatarib_mouse_port", "1");
            BindFeature(coreSettings, "hatarib_statusbar", "hatarib_statusbar", "1");
            BindFeature(coreSettings, "hatarib_emutos_framerate", "hatarib_emutos_framerate", "-1");
            BindFeature(coreSettings, "hatarib_emutos_region", "hatarib_emutos_region", "-1");
        }

        private void ConfigureKronos(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "kronos")
                return;

            coreSettings["kronos_use_beetle_saves"] = "enabled";
            coreSettings["kronos_multitap_port2"] = "disabled";
            coreSettings["kronos_sh2coretype"] = "kronos";

            BindFeature(coreSettings, "kronos_addon_cartridge", "addon_cartridge", "512K_backup_ram");
            BindFeature(coreSettings, "kronos_force_downsampling", "force_downsampling", "disabled");
            BindFeature(coreSettings, "kronos_language_id", "language_id", "English");
            BindFeature(coreSettings, "kronos_meshmode", "meshmode", "disabled");
            BindFeature(coreSettings, "kronos_multitap_port1", "multitap_port1", "disabled");
            BindFeature(coreSettings, "kronos_polygon_mode", "polygon_mode", "cpu_tesselation");
            BindFeature(coreSettings, "kronos_resolution_mode", "resolution_mode", "original");
            BindFeature(coreSettings, "kronos_use_cs", "use_cs", "disabled");
            BindFeature(coreSettings, "kronos_videocoretype", "videocoretype", "opengl");
            BindFeature(coreSettings, "kronos_videoformattype", "videoformattype", "auto");

            if (system == "segastv")
            {
                BindFeature(coreSettings, "kronos_stv_favorite_region", "kronos_stv_favorite_region", "EU");
                BindFeature(coreSettings, "kronos_service_enabled", "kronos_service_enabled", "disabled");
            }

            // Controls
            if (SystemConfig.isOptSet("kronos_controller") && !string.IsNullOrEmpty(SystemConfig["kronos_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["kronos_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            SetupLightGuns(retroarchConfig, "2", core);     // Kronos does not have device 260, only mouse "2"

            // Disable multitap if guns are enabled
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                coreSettings["kronos_multitap_port1"] = "disabled";
                coreSettings["kronos_multitap_port2"] = "disabled";
            }
        }

        private void ConfigureMame(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame")
                return;

            string softLists = "enabled";

            MessSystem messSystem = MessSystem.GetMessSystem(system, SystemConfig["subcore"]);
            if (messSystem != null)
            {
                CleanupMameMessConfigFiles(messSystem);

                // If we have a know system name, disable softlists as we run with CLI
                if (!string.IsNullOrEmpty(messSystem.MachineName))
                    softLists = "disabled";
            }

            coreSettings["mame_softlists_enable"] = softLists;
            coreSettings["mame_softlists_auto_media"] = softLists;
            coreSettings["mame_write_config"] = "disabled";
            coreSettings["mame_mouse_enable"] = "enabled";
            coreSettings["mame_mame_paths_enable"] = "disabled";

            BindFeature(coreSettings, "mame_buttons_profiles", "mame_buttons_profiles", "disabled");
            BindFeature(coreSettings, "mame_read_config", "mame_read_config", "disabled");
            BindFeature(coreSettings, "mame_alternate_renderer", "alternate_renderer", "disabled");
            BindFeature(coreSettings, "mame_altres", "internal_resolution", "640x480");
            BindFeature(coreSettings, "mame_cheats_enable", "cheats_enable", "disabled");
            BindFeature(coreSettings, "mame_mame_4way_enable", "4way_enable", "disabled");
            BindFeature(coreSettings, "mame_lightgun_mode", "lightgun_mode", "lightgun");
            BindFeature(coreSettings, "mame_rotation_mode", "mame_rotation_mode", "internal");

            BindFeature(coreSettings, "mame_boot_from_cli", "boot_from_cli", "enabled", true);
            BindFeature(coreSettings, "mame_boot_to_bios", "boot_to_bios", "disabled", true);
            BindFeature(coreSettings, "mame_boot_to_osd", "boot_to_osd", "disabled", true);

            // System specifics
            if (system == "fmtowns")
            {
                if (!SystemConfig.isOptSet("pause_on_disconnect"))
                    retroarchConfig["pause_on_disconnect"] = "false";

                if (!SystemConfig.isOptSet("alternate_renderer"))
                    coreSettings["mame_alternate_renderer"] = "enabled";
            }
        }

        private void CleanupMameMessConfigFiles(MessSystem messSystem)
        {
            try
            {
                // Remove image_directories node in cfg file
                string cfgPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "cfg", messSystem.MachineName + ".cfg");
                if (File.Exists(cfgPath))
                {
                    XDocument xml = XDocument.Load(cfgPath);

                    var image_directories = xml.Descendants().FirstOrDefault(d => d.Name == "image_directories");
                    if (image_directories != null)
                    {
                        image_directories.Remove();
                        xml.Save(cfgPath);
                    }
                }
            }
            catch { }

            try
            {
                // Remove medias declared in ini file
                string iniPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "ini", messSystem.MachineName + ".ini");
                if (File.Exists(iniPath))
                {
                    var lines = File.ReadAllLines(iniPath);
                    var newLines = lines.Where(l =>
                        !l.StartsWith("cartridge") && !l.StartsWith("floppydisk") &&
                        !l.StartsWith("cassette") && !l.StartsWith("cdrom") &&
                        !l.StartsWith("romimage") && !l.StartsWith("memcard") &&
                        !l.StartsWith("quickload") && !l.StartsWith("harddisk") &&
                        !l.StartsWith("autoboot_command") && !l.StartsWith("autoboot_delay") && !l.StartsWith("autoboot_script") &&
                        !l.StartsWith("printout")
                        ).ToArray();

                    if (lines.Length != newLines.Length)
                        File.WriteAllLines(iniPath, newLines);
                }
            }
            catch { }
        }

        private void ConfigureMame2000(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame2000")
                return;

            coreSettings["mame2000-skip_disclaimer"] = "enabled";
        }

        private void ConfigureMame2003(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame2003")
                return;

            coreSettings["mame2003_skip_disclaimer"] = "enabled";
            coreSettings["mame2003_skip_warnings"] = "enabled";
            coreSettings["mame2003_mouse_device"] = "mouse";
            BindFeature(coreSettings, "mame2003_tate_mode", "mame2003_tate_mode", "disabled");
            BindFeature(coreSettings, "mame2003_input_interface", "mame2003_input_interface", "retropad");
            BindFeature(coreSettings, "mame2003_four_way_emulation", "mame2003_four_way_emulation", "disabled");
        }

        private void ConfigureMame2003Plus(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame078plus" && core != "mame2003_plus")
                return;

            coreSettings["mame2003-plus_skip_disclaimer"] = "enabled";
            coreSettings["mame2003-plus_skip_warnings"] = "enabled";
            coreSettings["mame2003-plus_mouse_device"] = "mouse";
            coreSettings["mame2003-plus_xy_device"] = "lightgun";

            BindFeature(coreSettings, "mame2003-plus_analog", "mame2003-plus_analog", "digital");
            BindFeature(coreSettings, "mame2003-plus_frameskip", "mame2003-plus_frameskip", "disabled");
            BindFeature(coreSettings, "mame2003-plus_input_interface", "mame2003-plus_input_interface", "retropad");
            BindFeature(coreSettings, "mame2003-plus_neogeo_bios", "mame2003-plus_neogeo_bios", "unibios33");
            BindFeature(coreSettings, "mame2003-plus_tate_mode", "mame2003-plus_tate_mode", "disabled");
            BindFeature(coreSettings, "mame2003-plus_four_way_emulation", "mame2003-plus_four_way_emulation", "disabled");

            // Controller type
            BindFeature(retroarchConfig, "input_libretro_device_p1", "mame_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "mame_controller2", "1");

            // Lightguns
            coreSettings["mame2003-plus_xy_device"] = HasMultipleGuns() ? "mouse" : "lightgun";
        }

        private void ConfigureMame2010(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame2010")
                return;

            BindFeature(coreSettings, "mame_current_xy_type", "mame_xy_type", HasMultipleGuns() ? "mouse" : "lightgun");
        }

        private void ConfigureMame2014(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame2014")
                return;

            coreSettings["mame2014_mouse_enable"] = "enabled";
        }

        private void ConfigureMame2016(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mame2016")
                return;

            coreSettings["mame2016_mouse_enable"] = "enabled";
        }

        private void ConfigureMednafenPCFX(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_pcfx")
                return;

            BindFeature(retroarchConfig, "input_libretro_device_p1", "pcfx_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "pcfx_controller2", "1");
        }

        private void ConfigureMednafenPce(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_pce")
                return;

            coreSettings["pce_show_advanced_input_settings"] = "enabled";

            BindFeature(coreSettings, "pce_psgrevision", "pce_psgrevision", "auto");
            BindFeature(coreSettings, "pce_resamp_quality", "pce_resamp_quality", "3");
            BindFeature(coreSettings, "pce_ocmultiplier", "pce_ocmultiplier", "1");
            BindFeature(coreSettings, "pce_nospritelimit", "pce_nospritelimit", "disabled");
            BindFeature(coreSettings, "pce_cdimagecache", "pce_cdimagecache", "disabled");
            BindFeature(coreSettings, "pce_cdbios", "pce_cdbios", "System Card 3");
            BindFeature(coreSettings, "pce_cdspeed", "pce_cdspeed", "1");
            BindFeature(coreSettings, "pce_palette", "pce_palette", "Composite");
            BindFeature(coreSettings, "pce_scaling", "pce_scaling", "auto");
            BindFeature(coreSettings, "pce_hires_blend", "pce_hires_blend", "disabled");
            BindFeature(coreSettings, "pce_h_overscan", "pce_h_overscan", "auto");
            BindFeature(coreSettings, "pce_adpcmextraprec", "pce_adpcmextraprec", "12-bit");
            BindFeature(coreSettings, "pce_adpcmvolume", "pcecdvolume", "100");
            BindFeature(coreSettings, "pce_cddavolume", "pcecdvolume", "100");
            BindFeature(coreSettings, "pce_cdpsgvolume", "pcecdvolume", "100");

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "pce_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "pce_controller2", "1");
        }

        private void ConfigureMednafenPceFast(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_pce_fast")
                return;

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "pce_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "pce_controller2", "1");
        }

        private void ConfigureMednafenPsxHW(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_psx_hw")
                return;

            // Multitap in case of multiplayer
            if (Controllers.Count > 5)
            {
                coreSettings["beetle_psx_hw_enable_multitap_port1"] = "enabled";
                coreSettings["beetle_psx_hw_enable_multitap_port2"] = "enabled";
            }
            else if (Controllers.Count > 2)
            {
                coreSettings["beetle_psx_hw_enable_multitap_port1"] = "enabled";
                coreSettings["beetle_psx_hw_enable_multitap_port2"] = "disabled";
            }
            else
            {
                coreSettings["beetle_psx_hw_enable_multitap_port1"] = "disabled";
                coreSettings["beetle_psx_hw_enable_multitap_port2"] = "disabled";
            }

            // widescreen
            BindFeature(coreSettings, "beetle_psx_hw_widescreen_hack", "widescreen_hack", "disabled");

            if (coreSettings["beetle_psx_hw_widescreen_hack"] == "enabled")
            {
                int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                if (idx > 0)
                {
                    retroarchConfig["aspect_ratio_index"] = idx.ToString();
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                }
                else
                {
                    retroarchConfig["aspect_ratio_index"] = "1";
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    SystemConfig["bezel"] = "none";
                    coreSettings["beetle_psx_hw_widescreen_hack_aspect_ratio"] = "16:9";
                }
            }

            // PGXP
            if (SystemConfig.isOptSet("pgxp") && SystemConfig.getOptBoolean("pgxp"))
            {
                coreSettings["beetle_psx_hw_pgxp_mode"] = "memory only";
                coreSettings["beetle_psx_hw_pgxp_texture"] = "enabled";
                coreSettings["beetle_psx_hw_pgxp_vertex"] = "enabled";
            }
            else
            {
                coreSettings["beetle_psx_hw_pgxp_mode"] = "disabled";
                coreSettings["beetle_psx_hw_pgxp_texture"] = "disabled";
                coreSettings["beetle_psx_hw_pgxp_vertex"] = "disabled";
            }

            //Custom textures
            if (SystemConfig.isOptSet("mednafen_texture_replacement") && (SystemConfig["mednafen_texture_replacement"] == "enabled"))
            {
                coreSettings["beetle_psx_hw_replace_textures"] = "enabled";
                coreSettings["beetle_psx_hw_track_textures"] = "enabled";
            }
            else
            {
                coreSettings["beetle_psx_hw_replace_textures"] = "disabled";
                coreSettings["beetle_psx_hw_track_textures"] = "disabled";
            }

            BindFeature(coreSettings, "beetle_psx_hw_internal_resolution", "internal_resolution", "1x(native)");
            BindFeature(coreSettings, "beetle_psx_hw_filter", "texture_filtering", "nearest");
            BindFeature(coreSettings, "beetle_psx_hw_dither_mode", "dither_mode", "disabled");
            BindFeature(coreSettings, "beetle_psx_hw_msaa", "msaa", "1x");
            BindFeature(coreSettings, "beetle_psx_hw_analog_toggle", "analog_toggle", "enabled");
            BindFeature(coreSettings, "beetle_psx_hw_widescreen_hack_aspect_ratio", "widescreen_hack_aspect_ratio", "16:9");
            BindFeature(coreSettings, "beetle_psx_hw_pal_video_timing_override", "pal_video_timing_override", "disabled");
            BindFeature(coreSettings, "beetle_psx_hw_skip_bios", "skip_bios", "enabled");
            BindFeature(coreSettings, "beetle_psx_hw_renderer", "mednafen_psx_renderer", "hardware");
            BindFeature(coreSettings, "beetle_psx_hw_gte_overclock", "beetle_psx_hw_gte_overclock", "disabled");
            BindFeature(coreSettings, "beetle_psx_hw_cpu_freq_scale", "beetle_psx_hw_cpu_freq_scale", "100%(native)");

            // Controls
            if (SystemConfig.isOptSet("mednafen_controller") && !string.IsNullOrEmpty(SystemConfig["mednafen_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["mednafen_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }
            BindFeature(coreSettings, "beetle_psx_hw_gun_input_mode", "gun_input_mode", "lightgun");
            BindFeature(coreSettings, "beetle_psx_hw_gun_cursor", "gun_cursor", "cross");
            BindFeature(coreSettings, "beetle_psx_hw_analog_toggle_combo", "beetle_psx_hw_analog_toggle_combo", "l1+r1+start");

            // If lightgun is enabled, multitaps are disabled
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                coreSettings["beetle_psx_hw_renderer"] = "software";                // Lightgun only works with software renderer
                coreSettings["beetle_psx_hw_enable_multitap_port1"] = "disabled";
                coreSettings["beetle_psx_hw_enable_multitap_port2"] = "disabled";
            }


            // Some games require a controller in port 1 and lightgun in port 2
            if (SystemConfig.isOptSet("psx_gunport2") && SystemConfig.getOptBoolean("psx_gunport2"))
                SetupLightGuns(retroarchConfig, "260", core, 2);
            else
                SetupLightGuns(retroarchConfig, "260", core, 1);
        }

        private void ConfigureMednafenSaturn(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_saturn")
                return;

            coreSettings["beetle_saturn_autortc"] = "enabled";
            coreSettings["beetle_saturn_shared_ext"] = "enabled";
            coreSettings["beetle_saturn_shared_int"] = "enabled";

            BindFeature(coreSettings, "beetle_saturn_autortc_lang", "beetle_saturn_autortc_lang", "english");
            BindFeature(coreSettings, "beetle_saturn_cart", "beetle_saturn_cart", "Auto Detect");
            BindFeature(coreSettings, "beetle_saturn_cdimagecache", "beetle_saturn_cdimagecache", "disabled");
            BindFeature(coreSettings, "beetle_saturn_midsync", "beetle_saturn_midsync", "disabled");
            BindFeature(coreSettings, "beetle_saturn_multitap_port1", "beetle_saturn_multitap_port1", "disabled");
            BindFeature(coreSettings, "beetle_saturn_multitap_port2", "beetle_saturn_multitap_port2", "disabled");
            BindFeature(coreSettings, "beetle_saturn_region", "beetle_saturn_region", "Auto Detect");

            // NEW
            BindFeature(coreSettings, "beetle_saturn_virtuagun_crosshair", "beetle_saturn_virtuagun_crosshair", "Cross", true);
            BindFeature(coreSettings, "beetle_saturn_mouse_sensitivity", "beetle_saturn_mouse_sensitivity", "100%");

            // Controls
            if (SystemConfig.isOptSet("mednafen_saturn_controller") && !string.IsNullOrEmpty(SystemConfig["mednafen_saturn_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["mednafen_saturn_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            coreSettings["beetle_saturn_virtuagun_input"] = "Lightgun";
            SetupLightGuns(retroarchConfig, "260", core);

            // Disable multitap if lightgun is enabled
            if (SystemConfig.getOptBoolean("use_guns"))
            {
                coreSettings["beetle_saturn_multitap_port1"] = "disabled";
                coreSettings["beetle_saturn_multitap_port2"] = "disabled";
            }
        }

        private void ConfigureMednafenSuperGrafx(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mednafen_supergrafx")
                return;

            BindFeature(retroarchConfig, "input_libretro_device_p1", "supergrafx_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "supergrafx_controller2", "1");
        }

        private void ConfigureMesen(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mesen")
                return;

            coreSettings["mesen_aspect_ratio"] = "Auto";

            BindFeature(coreSettings, "mesen_hdpacks", "hd_packs", "disabled");
            BindFeature(coreSettings, "mesen_ntsc_filter", "ntsc_filter", "Disabled");
            BindFeature(coreSettings, "mesen_palette", "palette", "Default");
            BindFeature(coreSettings, "mesen_shift_buttons_clockwise", "shift_buttons", "disabled");
            BindFeature(coreSettings, "mesen_fake_stereo", "fake_stereo", "disabled");
            BindBoolFeature(coreSettings, "mesen_nospritelimit", "mesen_nospritelimit", "enabled", "disabled");
            BindFeature(coreSettings, "mesen_overclock", "mesen_overclock", "None");
            BindBoolFeature(coreSettings, "mesen_fdsautoinsertdisk", "mesen_fdsautoinsertdisk", "enabled", "disabled");
            BindBoolFeature(coreSettings, "mesen_fdsfastforwardload", "mesen_fdsfastforwardload", "enabled", "disabled");

            bool overscan = SystemConfig.isOptSet("mesen_overscan_pixels") && !string.IsNullOrEmpty(SystemConfig["mesen_overscan_pixels"]);

            if (overscan && SystemConfig.isOptSet("mesen_crop_area") && !string.IsNullOrEmpty(SystemConfig["mesen_crop_area"]) && SystemConfig["mesen_crop_area"] != "none")
            {
                string overscanArea = SystemConfig["mesen_crop_area"];
                bool cropLimitHorizontal = (SystemConfig["mesen_overscan_pixels"] == "20px" || SystemConfig["mesen_overscan_pixels"] == "24px");

                switch (overscanArea)
                {
                    case "all":
                        coreSettings["mesen_overscan_down"] = SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_left"] = cropLimitHorizontal ? "16px" : SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_right"] = cropLimitHorizontal ? "16px" : SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_up"] = SystemConfig["mesen_overscan_pixels"];
                        break;
                    case "topbottom":
                        coreSettings["mesen_overscan_down"] = SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_up"] = SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_right"] = "None";
                        coreSettings["mesen_overscan_left"] = "None";
                        break;
                    case "leftright":
                        coreSettings["mesen_overscan_right"] = cropLimitHorizontal ? "16px" : SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_left"] = cropLimitHorizontal ? "16px" : SystemConfig["mesen_overscan_pixels"];
                        coreSettings["mesen_overscan_down"] = "None";
                        coreSettings["mesen_overscan_up"] = "None";
                        break;
                }
            }
            else
            {
                coreSettings["mesen_overscan_down"] = "None";
                coreSettings["mesen_overscan_left"] = "None";
                coreSettings["mesen_overscan_right"] = "None";
                coreSettings["mesen_overscan_up"] = "None";
            }

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "mesen_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "mesen_controller2", "1");
            BindFeature(retroarchConfig, "input_overlay_show_mouse_cursor", "ShowCursor", "false");

            SetupLightGuns(retroarchConfig, "262", core, 2);
        }

        private void ConfigureMesenS(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mesen-s")
                return;

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "mesen_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "mesen_controller2", "1");

            SetupLightGuns(retroarchConfig, "262", core, 2);
        }

        private void ConfigureMupen64(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mupen64plus_next" && core != "mupen64plus_next_gles3")
                return;

            if (system == "n64dd")
            {

                // Nintendo 64DD IPL bios selection workaround
                // mupen64plus doesn't allow multiple bios selection and looks only for a IPL.n64 file in bios\mupen64plus
                string biosPath = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus");
                if (!string.IsNullOrEmpty(biosPath))
                {
                    string biosFileTarget = Path.Combine(biosPath, "IPL.n64");
                    string biosFileSource = Path.Combine(biosPath, SystemConfig["ipl_bios"]);

                    if (Features.IsSupported("ipl_bios") && SystemConfig.isOptSet("ipl_bios"))
                    {
                        if (File.Exists(biosFileTarget))
                            File.Delete(biosFileTarget);

                        if (File.Exists(biosFileSource))
                            File.Copy(biosFileSource, biosFileTarget);

                    }

                }

            }

            coreSettings["mupen64plus-rsp-plugin"] = "hle";
            coreSettings["mupen64plus-EnableLODEmulation"] = "True";
            coreSettings["mupen64plus-EnableCopyAuxToRDRAM"] = "True";
            coreSettings["mupen64plus-EnableHWLighting"] = "True";
            coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "True";
            coreSettings["mupen64plus-GLideN64IniBehaviour"] = "early";
            coreSettings["mupen64plus-parallel-rdp-native-tex-rect"] = "True";
            coreSettings["mupen64plus-parallel-rdp-synchronous"] = "True";

            BindFeature(coreSettings, "mupen64plus-cpucore", "mupen64plus-cpucore", "pure_interpreter"); // CPU core
            BindFeature(coreSettings, "mupen64plus-rdp-plugin", "RDP_Plugin", "gliden64"); // Plugin selection
            BindFeature(coreSettings, "mupen64plus-Framerate", "mupen64plus_framerate", "Original");

            // Set RSP plugin: HLE for Glide, LLE for Parallel
            if (SystemConfig.isOptSet("RDP_Plugin") && coreSettings["mupen64plus-rdp-plugin"] == "parallel")
                coreSettings["mupen64plus-rsp-plugin"] = "parallel";
            else
                coreSettings["mupen64plus-rsp-plugin"] = "hle";

            // Overscan (Glide)
            if (SystemConfig.isOptSet("CropOverscan") && SystemConfig.getOptBoolean("CropOverscan"))
            {
                coreSettings["mupen64plus-OverscanBottom"] = "0";
                coreSettings["mupen64plus-OverscanLeft"] = "0";
                coreSettings["mupen64plus-OverscanRight"] = "0";
                coreSettings["mupen64plus-OverscanTop"] = "0";
            }
            else
            {
                coreSettings["mupen64plus-OverscanBottom"] = "15";
                coreSettings["mupen64plus-OverscanLeft"] = "18";
                coreSettings["mupen64plus-OverscanRight"] = "13";
                coreSettings["mupen64plus-OverscanTop"] = "12";
            }

            // Performance presets
            if (SystemConfig.isOptSet("PerformanceMode") && SystemConfig.getOptBoolean("PerformanceMode"))
            {
                coreSettings["mupen64plus-EnableCopyColorToRDRAM"] = "Off";
                coreSettings["mupen64plus-EnableCopyDepthToRDRAM"] = "Off";
                coreSettings["mupen64plus-EnableFBEmulation"] = "False";
                coreSettings["mupen64plus-ThreadedRenderer"] = "False";
                coreSettings["mupen64plus-HybridFilter"] = "False";
                coreSettings["mupen64plus-BackgroundMode"] = "OnePiece";
                coreSettings["mupen64plus-EnableLegacyBlending"] = "True";
                coreSettings["mupen64plus-txFilterIgnoreBG"] = "True";
            }
            else
            {
                coreSettings["mupen64plus-EnableCopyColorToRDRAM"] = "TripleBuffer";
                coreSettings["mupen64plus-EnableCopyDepthToRDRAM"] = "Software";
                coreSettings["mupen64plus-EnableFBEmulation"] = "True";
                coreSettings["mupen64plus-ThreadedRenderer"] = "True";
                coreSettings["mupen64plus-HybridFilter"] = "True";
                coreSettings["mupen64plus-BackgroundMode"] = "Stripped";
                coreSettings["mupen64plus-EnableLegacyBlending"] = "False";
                coreSettings["mupen64plus-txFilterIgnoreBG"] = "False";

            }

            // Hi Res textures methods
            if (SystemConfig.isOptSet("TexturesPack"))
            {
                if (SystemConfig["TexturesPack"] == "legacy")
                {
                    coreSettings["mupen64plus-EnableTextureCache"] = "True";
                    coreSettings["mupen64plus-txHiresEnable"] = "True";
                    coreSettings["mupen64plus-txCacheCompression"] = "True";
                    coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "False";
                    coreSettings["mupen64plus-EnableEnhancedTextureStorage"] = "False";
                    coreSettings["mupen64plus-EnableEnhancedHighResStorage"] = "False";
                }
                else if (SystemConfig["TexturesPack"] == "cache")
                {
                    coreSettings["mupen64plus-EnableTextureCache"] = "True";
                    coreSettings["mupen64plus-txHiresEnable"] = "True";
                    coreSettings["mupen64plus-txCacheCompression"] = "True";
                    coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "True";
                    coreSettings["mupen64plus-EnableEnhancedTextureStorage"] = "True";
                    coreSettings["mupen64plus-EnableEnhancedHighResStorage"] = "True";
                }
            }
            else
            {
                coreSettings["mupen64plus-EnableTextureCache"] = "False";
                coreSettings["mupen64plus-txHiresEnable"] = "False";
                coreSettings["mupen64plus-txCacheCompression"] = "False";
                coreSettings["mupen64plus-txHiresFullAlphaChannel"] = "False";
                coreSettings["mupen64plus-EnableEnhancedTextureStorage"] = "False";
                coreSettings["mupen64plus-EnableEnhancedHighResStorage"] = "False";
            }

            // Widescreen (Glide)
            if (SystemConfig.isOptSet("Widescreen") && SystemConfig.getOptBoolean("Widescreen"))
            {
                coreSettings["mupen64plus-aspect"] = "16:9 adjusted";
                retroarchConfig["aspect_ratio_index"] = "1";
                SystemConfig["bezel"] = "none";
            }
            else
                coreSettings["mupen64plus-aspect"] = "4/3";

            // Player packs
            BindFeature(coreSettings, "mupen64plus-pak1", "mupen64plus-pak1", "memory");
            BindFeature(coreSettings, "mupen64plus-pak2", "mupen64plus-pak2", "none");
            BindFeature(coreSettings, "mupen64plus-pak3", "mupen64plus-pak3", "none");
            BindFeature(coreSettings, "mupen64plus-pak4", "mupen64plus-pak4", "none");

            // Glide
            BindFeature(coreSettings, "mupen64plus-txEnhancementMode", "Texture_Enhancement", "As Is");
            BindFeature(coreSettings, "mupen64plus-43screensize", "43screensize", "640x480");
            BindFeature(coreSettings, "mupen64plus-169screensize", "169screensize", "960x540");
            BindFeature(coreSettings, "mupen64plus-BilinearMode", "BilinearMode", "3point");
            BindFeature(coreSettings, "mupen64plus-MultiSampling", "MultiSampling", "0");
            BindFeature(coreSettings, "mupen64plus-txFilterMode", "Texture_filter", "None");

            // Parallel
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-deinterlace-method", "mupen64plus-parallel-rdp-deinterlace-method", "Bob");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-dither-filter", "mupen64plus-parallel-rdp-dither-filter", "True");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-divot-filter", "mupen64plus-parallel-rdp-divot-filter", "True");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-downscaling", "mupen64plus-parallel-rdp-downscaling", "disable");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-gamma-dither", "mupen64plus-parallel-rdp-gamma-dither", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-native-texture-lod", "mupen64plus-parallel-rdp-native-texture-lod", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-overscan", "mupen64plus-parallel-rdp-overscan", "16");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-super-sampled-read-back", "mupen64plus-parallel-rdp-super-sampled-read-back", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-super-sampled-read-back-dither", "mupen64plus-parallel-rdp-super-sampled-read-back-dither", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-upscaling", "mupen64plus-parallel-rdp-upscaling", "1x");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-vi-aa", "mupen64plus-parallel-rdp-vi-aa", "False");
            BindFeature(coreSettings, "mupen64plus-parallel-rdp-vi-bilinear", "mupen64plus-parallel-rdp-vi-bilinear", "False");
        }

        private void ConfigureMelonDS(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "melonds")
                return;

            BindFeature(coreSettings, "melonds_boot_directly", "nds_boot", "enabled");
            BindFeature(coreSettings, "melonds_console_mode", "nds_console", "DS");

            if (SystemConfig.isOptSet("melonds_screen_layout") && SystemConfig["melonds_screen_layout"] == "duplicate")
            {
                coreSettings["melonds_screen_layout"] = "Hybrid Top";
                coreSettings["melonds_hybrid_small_screen"] = "Duplicate";
            }
            else
            {
                BindFeature(coreSettings, "melonds_screen_layout", "melonds_screen_layout", "Top/Bottom");
                coreSettings["melonds_hybrid_small_screen"] = "Bottom";
            }

            BindFeature(coreSettings, "melonds_touch_mode", "melonds_touch_mode", "Joystick");

            // Boot to firmware directly if a .bin file is loaded
            string rom = SystemConfig["rom"];

            if (Path.GetExtension(rom) == ".bin")
            {
                coreSettings["melonds_boot_directly"] = "disabled";
                coreSettings["melonds_console_mode"] = "DSi";

                // Copy the loaded nand to the bios folder before loading, so that multiple nand files can be used.
                string biosPath = Path.Combine(AppConfig.GetFullPath("bios"));
                if (!string.IsNullOrEmpty(biosPath))
                {
                    string nandFileTarget = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_nand.bin");
                    string nandFileSource = rom;

                    if (File.Exists(nandFileTarget) && File.Exists(nandFileSource))
                        File.Delete(nandFileTarget);

                    if (File.Exists(nandFileSource))
                        File.Copy(nandFileSource, nandFileTarget);

                }
            }
        }

        private void ConfiguremGBA(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mgba")
                return;

            BindFeature(coreSettings, "mgba_gb_model", "mgba_gb_model", "Autodetect");
            BindFeature(coreSettings, "mgba_skip_bios", "mgba_skip_bios", "OFF");
            BindFeature(coreSettings, "mgba_force_gbp", "mgba_force_gbp", "OFF");
            BindFeature(coreSettings, "mgba_gb_colors", "mgba_gb_colors", "Grayscale");
            BindFeature(coreSettings, "mgba_interframe_blending", "mgba_interframe_blending", "OFF");

            if (system == "gba" || system == "gba2players" || system == "gbc" || system == "gbc2players")
                BindFeature(coreSettings, "mgba_color_correction", "mgba_color_correction", "OFF");

            if (system == "sgb")
                BindFeature(coreSettings, "mgba_sgb_borders", "mgba_sgb_borders", "ON");

            // Audio Filter
            if (Features.IsSupported("mgba_audio_low_pass_filter"))
            {
                if (SystemConfig.isOptSet("mgba_audio_low_pass_filter") && SystemConfig["mgba_audio_low_pass_filter"] != "0")
                {
                    coreSettings["mgba_audio_low_pass_filter"] = "enabled";
                    coreSettings["mgba_audio_low_pass_range"] = SystemConfig["mgba_audio_low_pass_filter"];
                }
                else
                {
                    coreSettings["mgba_audio_low_pass_filter"] = "disabled";
                    coreSettings["mgba_audio_low_pass_range"] = "60";
                }
            }
        }

        private void ConfigureMrBoom(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "mrboom")
                return;

            BindFeature(coreSettings, "mrboom-aspect", "mrboom_aspect", "Native");
            BindFeature(coreSettings, "mrboom-levelselect", "mrboom_levelselect", "Normal");
            BindFeature(coreSettings, "mrboom-nomonster", "mrboom_nomonster", "ON");
            BindFeature(coreSettings, "mrboom-teammode", "mrboom_teammode", "Selfie");
        }

        private void ConfigureNeocd(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "neocd")
                return;

            coreSettings["neocd_per_content_saves"] = "On";

            BindFeature(coreSettings, "neocd_bios", "neocd_bios", "neocd_z.rom (CDZ)");
            BindFeature(coreSettings, "neocd_cdspeedhack", "neocd_cdspeedhack", "Off");
            BindFeature(coreSettings, "neocd_loadskip", "neocd_loadskip", "On");
            BindFeature(coreSettings, "neocd_region", "neocd_region", "USA");
        }

        private void ConfigureNestopia(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "nestopia")
                return;

            if (Features.IsSupported("nestopia_cropoverscan"))
            {
                if (SystemConfig.isOptSet("nestopia_cropoverscan") && SystemConfig["nestopia_cropoverscan"] == "none")
                {
                    coreSettings["nestopia_overscan_h"] = "disabled";
                    coreSettings["nestopia_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("nestopia_cropoverscan") && SystemConfig["nestopia_cropoverscan"] == "h")
                {
                    coreSettings["nestopia_overscan_h"] = "enabled";
                    coreSettings["nestopia_overscan_v"] = "disabled";
                }
                else if (SystemConfig.isOptSet("nestopia_cropoverscan") && SystemConfig["nestopia_cropoverscan"] == "v")
                {
                    coreSettings["nestopia_overscan_h"] = "disabled";
                    coreSettings["nestopia_overscan_v"] = "enabled";
                }
                else
                {
                    coreSettings["nestopia_overscan_h"] = "enabled";
                    coreSettings["nestopia_overscan_v"] = "enabled";
                }
            }

            BindFeature(coreSettings, "nestopia_nospritelimit", "nestopia_nospritelimit", "disabled");
            BindFeature(coreSettings, "nestopia_palette", "nestopia_palette", "consumer");
            BindFeature(coreSettings, "nestopia_blargg_ntsc_filter", "nestopia_blargg_ntsc_filter", "disabled");
            BindFeature(coreSettings, "nestopia_overclock", "nestopia_overclock", "1x");
            BindFeature(coreSettings, "nestopia_select_adapter", "nestopia_select_adapter", "auto");
            BindFeature(coreSettings, "nestopia_show_crosshair", "nestopia_show_crosshair", "disabled");
            BindFeature(coreSettings, "nestopia_favored_system", "nestopia_favored_system", "auto");
            BindFeature(coreSettings, "nestopia_button_shift", "nestopia_button_shift", "disabled");

            // Controls
            BindFeature(retroarchConfig, "input_libretro_device_p1", "nestopia_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "nestopia_controller2", "1");

            coreSettings["nestopia_zapper_device"] = "lightgun";
            SetupLightGuns(retroarchConfig, "262", core, 2);
        }

        private void ConfigureO2em(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "o2em")
                return;

            coreSettings["o2em_vkbd_transparency"] = "25";

            // Emulated Hardware
            if (Features.IsSupported("o2em_bios"))
            {
                if (SystemConfig.isOptSet("o2em_bios"))
                    coreSettings["o2em_bios"] = SystemConfig["o2em_bios"];
                else if (system == "videopacplus")
                    coreSettings["o2em_bios"] = "g7400.bin";
                else
                    coreSettings["o2em_bios"] = "o2rom.bin";
            }

            BindFeature(coreSettings, "o2em_region", "o2em_region", "auto");
            BindFeature(coreSettings, "o2em_swap_gamepads", "o2em_swap_gamepads", "disabled");
            BindFeature(coreSettings, "o2em_crop_overscan", "o2em_crop_overscan", "enabled");
            BindFeature(coreSettings, "o2em_mix_frames", "o2em_mix_frames", "disabled");

            // Audio Filter
            if (Features.IsSupported("o2em_low_pass_range"))
            {
                if (SystemConfig.isOptSet("o2em_low_pass_range") && SystemConfig["o2em_low_pass_range"] != "0")
                {
                    coreSettings["o2em_low_pass_filter"] = "enabled";
                    coreSettings["o2em_low_pass_range"] = SystemConfig["o2em_low_pass_range"];
                }
                else
                {
                    coreSettings["o2em_low_pass_filter"] = "disabled";
                    coreSettings["o2em_low_pass_range"] = "60";
                }
            }
        }

        private void ConfigureOpenLara(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "openlara")
                return;

            BindFeature(coreSettings, "openlara_framerate", "openlara_framerate", "60fps");
            BindFeature(coreSettings, "openlara_resolution", "openlara_resolution", "320x240");
        }

        static List<KeyValuePair<string, string>> operaHacks = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("crashnburn", "hack_timing_1"),
                new KeyValuePair<string, string>("dinopark tycoon", "hack_timing_3"),
                new KeyValuePair<string, string>("microcosm", "hack_timing_5"),
                new KeyValuePair<string, string>("aloneinthedark", "hack_timing_6"),
                new KeyValuePair<string, string>("samuraishowdown", "hack_graphics_step_y")
            };

        private void ConfigureOpera(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "opera")
                return;

            coreSettings["opera_dsp_threaded"] = "enabled";

            BindFeature(coreSettings, "opera_high_resolution", "high_resolution", "enabled");
            BindFeature(coreSettings, "opera_cpu_overclock", "cpu_overclock", "1.0x (12.50Mhz)");
            BindFeature(coreSettings, "opera_bios", "opera_bios", "Panasonic FZ-1 (U)");
            BindFeature(coreSettings, "opera_region", "opera_region", "ntsc");

            // Game hacks
            string rom = SystemConfig["rom"].AsIndexedRomName();
            foreach (var hackName in operaHacks.Select(h => h.Value).Distinct())
                coreSettings["opera_" + hackName] = operaHacks.Any(h => h.Value == hackName && rom.Contains(h.Key)) ? "enabled" : "disabled";

            // If ROM includes the word 'Disc', assume it's a multi disc game, and enable shared nvram if the option isn't set.
            if (Features.IsSupported("opera_nvram_storage"))
            {
                if (SystemConfig.isOptSet("nvram_storage"))
                    coreSettings["opera_nvram_storage"] = SystemConfig["nvram_storage"];
                else if (!string.IsNullOrEmpty(SystemConfig["rom"]) && SystemConfig["rom"].ToLower().Contains("disc"))
                    coreSettings["opera_nvram_storage"] = "shared";
                else
                    coreSettings["opera_nvram_storage"] = "per game";
            }

            // Controls
            BindFeature(coreSettings, "opera_active_devices", "active_devices", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p1", "opera_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "opera_controller2", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p3", "opera_controller3", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p4", "opera_controller4", "1");

            // Lightgun
            SetupLightGuns(retroarchConfig, "260", core);
        }

        private void ConfigureParallelN64(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "parallel_n64")
                return;

            if (system == "n64dd")
            {
                coreSettings["parallel-n64-64dd-hardware"] = "enabled";

                // Nintendo 64DD IPL bios selection workaround
                // Parallel core doesn't allow multiple bios selection and looks only for a 64DD_IPL.bin file in \bios folder
                string biosPath = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus");
                if (!string.IsNullOrEmpty(biosPath))
                {
                    string biosFileTarget = Path.Combine(AppConfig.GetFullPath("bios"), "64DD_IPL.bin");
                    string biosFileSource = Path.Combine(biosPath, SystemConfig["ipl_bios"]);

                    if (Features.IsSupported("ipl_bios") && SystemConfig.isOptSet("ipl_bios"))
                    {
                        if (File.Exists(biosFileTarget))
                            File.Delete(biosFileTarget);

                        if (File.Exists(biosFileSource))
                            File.Copy(biosFileSource, biosFileTarget);

                    }

                }
            }
            else
                coreSettings["parallel-n64-64dd-hardware"] = "disabled";

            BindFeature(coreSettings, "parallel-n64-screensize", "parallel_resolution", "640x480");
            BindFeature(coreSettings, "parallel-n64-aspectratiohint", "parallel_aspect", "normal");
            BindFeature(coreSettings, "parallel-n64-framerate", "parallel_framerate", "original");
            BindFeature(coreSettings, "parallel-n64-cpucore", "parallel_cpucore", "dynamic_recompiler");
            BindFeature(coreSettings, "parallel-n64-gfxplugin-accuracy", "parallel_gfx_accuracy", "veryhigh");
            BindFeature(coreSettings, "parallel-n64-gfxplugin", "parallel_gfx_plugin", "auto");
            coreSettings["parallel-n64-rspplugin"] = "auto";

            // Set RSP plugin: HLE for Glide, LLE for Parallel and cxd4 for angrylion
            if (SystemConfig.isOptSet("parallel_gfx_plugin") && SystemConfig["parallel_gfx_plugin"] == "parallel")
                coreSettings["parallel-n64-rspplugin"] = "parallel";
            else if (SystemConfig.isOptSet("parallel_gfx_plugin") && SystemConfig["parallel_gfx_plugin"] == "angrylion")
                coreSettings["parallel-n64-rspplugin"] = "cxd4";
            else if (SystemConfig.isOptSet("parallel_gfx_plugin"))
                coreSettings["parallel-n64-rspplugin"] = "hle";
            else
                coreSettings["parallel-n64-rspplugin"] = "auto";

            // Parallel options
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-downscaling", "parallel_downsampling", "disable");
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-upscaling", "parallel_upscaling", "1x");
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-gamma-dither", "parallel_gamma_dither", "enabled");
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-divot-filter", "parallel_divot_filter", "enabled");
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-vi-aa", "parallel_vi_aa", "enabled");
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-vi-bilinear", "parallel_vi_bilinear", "enabled");
            BindFeature(coreSettings, "parallel-n64-parallel-rdp-dither-filter", "parallel_rdp_dither", "enabled");

            if (SystemConfig["parallel_gfx_plugin"] != "parallel")
            {
                coreSettings["parallel-n64-parallel-rdp-downscaling"] = "disable";
                coreSettings["parallel-n64-parallel-rdp-upscaling"] = "1x";
                coreSettings["parallel-n64-parallel-rdp-gamma-dither"] = "enabled";
                coreSettings["parallel-n64-parallel-rdp-divot-filter"] = "enabled";
                coreSettings["parallel-n64-parallel-rdp-vi-aa"] = "enabled";
                coreSettings["parallel-n64-parallel-rdp-vi-bilinear"] = "enabled";
                coreSettings["parallel-n64-parallel-rdp-dither-filter"] = "enabled";
            }

            // Glide64 options
            BindFeature(coreSettings, "parallel-n64-filtering", "parallel_filtering", "automatic");

            if (SystemConfig["parallel_gfx_plugin"] != "glide64")
            {
                coreSettings["parallel-n64-filtering"] = "automatic";
            }

            // Angrylion options
            BindFeature(coreSettings, "parallel-n64-dithering", "parallel_dithering", "enabled");

            if (SystemConfig["parallel_gfx_plugin"] != "angrylion")
            {
                coreSettings["parallel-n64-dithering"] = "enabled";
            }

            // Controls
            BindFeature(coreSettings, "parallel-n64-astick-deadzone", "parallel_stick_deadzone", "15");
            BindFeature(coreSettings, "parallel-n64-astick-sensitivity", "parallel_stick_sensitivity", "100");
            BindFeature(coreSettings, "parallel-n64-pak1", "parallel_pak1", "none");
            BindFeature(coreSettings, "parallel-n64-pak2", "parallel_pak2", "none");
            BindFeature(coreSettings, "parallel-n64-pak3", "parallel_pak3", "none");
            BindFeature(coreSettings, "parallel-n64-pak4", "parallel_pak4", "none");
        }

        private void ConfigurePcsx2(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "pcsx2")
                return;

            coreSettings["pcsx2_memcard_slot_1"] = "shared32";
            coreSettings["pcsx2_memcard_slot_2"] = "shared32";

            BindFeature(coreSettings, "pcsx2_upscale_multiplier", "pcsx2_upscale_multiplier", "1");
            BindFeature(coreSettings, "pcsx2_aspect_ratio", "pcsx2_aspect_ratio", "0");
            BindBoolFeature(coreSettings, "pcsx2_enable_widescreen_patches", "pcsx2_enable_widescreen_patches", "enabled", "disabled");
            BindFeature(coreSettings, "pcsx2_renderer", "pcsx2_renderer", "Auto");
            BindBoolFeature(coreSettings, "pcsx2_fxaa", "pcsx2_fxaa", "1", "0");
            BindFeature(coreSettings, "pcsx2_anisotropic_filter", "pcsx2_anisotropic_filter", "0");
            BindFeature(coreSettings, "pcsx2_dithering", "pcsx2_dithering", "2");
            BindFeature(coreSettings, "pcsx2_texture_filtering", "pcsx2_texture_filtering", "2");
            BindFeature(coreSettings, "pcsx2_deinterlace_mode", "pcsx2_deinterlace_mode", "7");
            BindFeature(coreSettings, "pcsx2_system_language", "pcsx2_system_language", "English");
            BindBoolFeature(coreSettings, "pcsx2_fastboot", "pcsx2_fastboot", "disabled", "enabled");
            BindBoolFeature(coreSettings, "pcsx2_boot_bios", "pcsx2_boot_bios", "enabled", "disabled");
            BindBoolFeature(coreSettings, "pcsx2_enable_60fps_patches", "pcsx2_enable_60fps_patches", "enabled", "disabled");
            BindBoolFeature(coreSettings, "pcsx2_enable_cheats", "pcsx2_enable_cheats", "enabled", "disabled");
            BindFeature(coreSettings, "pcsx2_speedhacks_presets", "pcsx2_speedhacks_presets", "1");
            BindFeature(coreSettings, "pcsx2_rumble_enable", "pcsx2_rumble_enable", "enabled");
            BindFeature(coreSettings, "pcsx2_gamepad_l_deadzone", "pcsx2_deadzone", "5");
            BindFeature(coreSettings, "pcsx2_gamepad_r_deadzone", "pcsx2_deadzone", "5");
        }

        private void ConfigurePcsxRearmed(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "pcsx_rearmed")
                return;

            if (Features.IsSupported("neon_enhancement"))
            {
                switch (SystemConfig["neon_enhancement"])
                {
                    case "enabled":
                        coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "enabled";
                        coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "disabled";
                        break;
                    case "enabled_with_speedhack":
                        coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "enabled";
                        coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "enabled";
                        break;
                    default:
                        coreSettings["pcsx_rearmed_neon_enhancement_enable"] = "disabled";
                        coreSettings["pcsx_rearmed_neon_enhancement_no_main"] = "disabled";
                        break;
                }
            }

            BindFeature(coreSettings, "pcsx_rearmed_display_internal_fps", "display_internal_fps", "disabled");
            BindFeature(coreSettings, "pcsx_rearmed_dithering", "pcsx_rearmed_dithering", "enabled");
            BindFeature(coreSettings, "pcsx_rearmed_psxclock", "pcsx_rearmed_psxclock", "57");
            BindFeature(coreSettings, "pcsx_rearmed_region", "pcsx_rearmed_region", "auto");
            BindFeature(coreSettings, "pcsx_rearmed_show_bios_bootlogo", "pcsx_rearmed_show_bios_bootlogo", "disabled");
            BindFeature(coreSettings, "pcsx_rearmed_spu_interpolation", "pcsx_rearmed_spu_interpolation", "simple");
            BindFeature(coreSettings, "pcsx_rearmed_icache_emulation", "pcsx_rearmed_icache_emulation", "disabled");

            // Game fixes

            coreSettings["pcsx_rearmed_idiablofix"] = "disabled";
            coreSettings["pcsx_rearmed_pe2_fix"] = "disabled";
            coreSettings["pcsx_rearmed_inuyasha_fix"] = "disabled";
            coreSettings["pcsx_rearmed_gpu_peops_odd_even_bit"] = "disabled";
            coreSettings["pcsx_rearmed_gpu_peops_expand_screen_width"] = "disabled";
            coreSettings["pcsx_rearmed_gpu_peops_ignore_brightness"] = "disabled";
            coreSettings["pcsx_rearmed_gpu_peops_lazy_screen_update"] = "disabled";
            coreSettings["pcsx_rearmed_gpu_peops_repeated_triangles"] = "disabled";

            if (SystemConfig.isOptSet("pcsx_game_fixes") && SystemConfig["pcsx_game_fixes"] != "disabled")
            {
                var patchValue = SystemConfig["pcsx_game_fixes"];
                coreSettings[patchValue] = "enabled";
            }

            // Controls
            BindFeature(coreSettings, "pcsx_rearmed_vibration", "pcsx_rearmed_vibration", "disabled");

            if (SystemConfig.isOptSet("pcsx_controller") && !string.IsNullOrEmpty(SystemConfig["pcsx_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["pcsx_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            if (Controllers.Count > 5)
                coreSettings["pcsx_rearmed_multitap"] = "both";
            else if (Controllers.Count > 2)
                coreSettings["pcsx_rearmed_multitap"] = "port 1 only";
            else
                coreSettings["pcsx_rearmed_multitap"] = "disabled";

            BindFeature(coreSettings, "pcsx_rearmed_crosshair1", "pcsx_rearmed_crosshair1", "disabled");
            BindFeature(coreSettings, "pcsx_rearmed_crosshair2", "pcsx_rearmed_crosshair2", "disabled");

            if (SystemConfig.isOptSet("psx_gunport2") && SystemConfig.getOptBoolean("psx_gunport2"))
                SetupLightGuns(retroarchConfig, "260", core, 2);
            else
                SetupLightGuns(retroarchConfig, "260", core);

            // Disable multitap for multiple guns
            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
                coreSettings["pcsx_rearmed_multitap"] = "disabled";
        }

        private void ConfigurePicodrive(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "picodrive")
                return;

            coreSettings["picodrive_ramcart"] = "disabled";

            BindFeature(coreSettings, "picodrive_aspect", "core_aspect", "PAR");
            BindFeature(coreSettings, "picodrive_overclk68k", "overclk68k", "disabled");
            BindFeature(coreSettings, "picodrive_overscan", "overscan", "disabled");
            BindFeature(coreSettings, "picodrive_region", "region", "Auto");
            BindFeature(coreSettings, "picodrive_renderer", "renderer", "accurate");
            BindFeature(coreSettings, "picodrive_drc", "dynamic_recompiler", "disabled");
            BindFeature(coreSettings, "picodrive_input1", "picodrive_input1", "3 button pad");
            BindFeature(coreSettings, "picodrive_input2", "picodrive_input2", "3 button pad");
            BindFeature(coreSettings, "picodrive_smsfm", "picodrive_smsfm", "off");
            BindFeature(coreSettings, "picodrive_smsmapper", "picodrive_smsmapper", "Auto");
            BindBoolFeature(coreSettings, "picodrive_sprlim", "picodrive_nospritelimit", "enabled", "disabled");

            if (system == "mastersystem")
                coreSettings["picodrive_smstype"] = "Master System";
            else if (system == "gamegear")
                coreSettings["picodrive_smstype"] = "Game Gear";
            else
                coreSettings["picodrive_smstype"] = "Auto";

            // Audio Filter
            if (Features.IsSupported("audio_filter"))
            {
                if (SystemConfig.isOptSet("audio_filter") && SystemConfig["audio_filter"] != "0")
                {
                    coreSettings["picodrive_audio_filter"] = "low-pass";
                    coreSettings["picodrive_lowpass_range"] = SystemConfig["audio_filter"];
                }
                else
                {
                    coreSettings["picodrive_audio_filter"] = "disabled";
                    coreSettings["picodrive_lowpass_range"] = "60";
                }
            }
        }

        private void ConfigurePokeMini(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "pokemini")
                return;

            BindFeature(coreSettings, "pokemini_video_scale", "pokemini_video_scale", "4x");
            BindFeature(coreSettings, "pokemini_palette", "pokemini_palette", "Default");
            BindFeature(coreSettings, "pokemini_lcdcontrast", "pokemini_lcdcontrast", "64");
            BindFeature(coreSettings, "pokemini_lcdbright", "pokemini_lcdbright", "0");
            BindFeature(coreSettings, "pokemini_60hz_mode", "pokemini_60hz_mode", "disabled");

            // Audio Filter
            if (Features.IsSupported("pokemini_lowpass_filter"))
            {
                if (SystemConfig.isOptSet("pokemini_lowpass_filter") && SystemConfig["pokemini_lowpass_filter"] != "0")
                {
                    coreSettings["pokemini_lowpass_filter"] = "enabled";
                    coreSettings["pokemini_lowpass_range"] = SystemConfig["pokemini_lowpass_filter"];
                }
                else
                {
                    coreSettings["pokemini_lowpass_filter"] = "disabled";
                    coreSettings["pokemini_lowpass_range"] = "60";
                }
            }

            // Rumble and screen shaking setting
            if (Features.IsSupported("pokemini_rumble"))
            {
                switch (SystemConfig["pokemini_rumble"])
                {
                    case "all_off":
                        coreSettings["pokemini_rumble_lv"] = "0";
                        coreSettings["pokemini_screen_shake_lv"] = "0";
                        break;
                    case "no_rumble_low":
                        coreSettings["pokemini_rumble_lv"] = "0";
                        coreSettings["pokemini_screen_shake_lv"] = "1";
                        break;
                    case "no_rumble_high":
                        coreSettings["pokemini_rumble_lv"] = "0";
                        coreSettings["pokemini_screen_shake_lv"] = "3";
                        break;
                    case "rumble_low":
                        coreSettings["pokemini_rumble_lv"] = "2";
                        coreSettings["pokemini_screen_shake_lv"] = "0";
                        break;
                    case "rumble_medium":
                        coreSettings["pokemini_rumble_lv"] = "6";
                        coreSettings["pokemini_screen_shake_lv"] = "0";
                        break;
                    case "rumble_high":
                        coreSettings["pokemini_rumble_lv"] = "10";
                        coreSettings["pokemini_screen_shake_lv"] = "0";
                        break;
                    default:
                        coreSettings["pokemini_rumble_lv"] = "10";
                        coreSettings["pokemini_screen_shake_lv"] = "3";
                        break;
                }
            }
        }

        private void ConfigurePotator(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "potator")
                return;

            BindFeature(coreSettings, "potator_lcd_ghosting", "potator_ghosting", "0");
            BindFeature(coreSettings, "potator_palette", "potator_palette", "default");
        }

        private void ConfigurePpsspp(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "ppsspp")
                return;

            coreSettings["ppsspp_cpu_core"] = "jit";
            coreSettings["ppsspp_auto_frameskip"] = "disabled";
            coreSettings["ppsspp_frameskip"] = "Off";
            coreSettings["ppsspp_frameskiptype"] = "number of frames";
            coreSettings["ppsspp_rendering_mode"] = "Buffered";
            coreSettings["ppsspp_locked_cpu_speed"] = "off";

            if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements") && SystemConfig.getOptBoolean("retroachievements.hardcore"))
                coreSettings["ppsspp_cheats"] = "disabled";
            else
                coreSettings["ppsspp_cheats"] = "enabled";

            switch (SystemConfig["PerformanceMode"])
            {
                case "Fast":
                    coreSettings["ppsspp_block_transfer_gpu"] = "disabled";
                    coreSettings["ppsspp_spline_quality"] = "Low";
                    coreSettings["ppsspp_software_skinning"] = "enabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "enabled";
                    coreSettings["ppsspp_vertex_cache"] = "enabled";
                    coreSettings["ppsspp_fast_memory"] = "enabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "enabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "enabled";
                    coreSettings["ppsspp_force_lag_sync"] = "disabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "enabled";
                    break;
                case "Balanced":
                    coreSettings["ppsspp_block_transfer_gpu"] = "enabled";
                    coreSettings["ppsspp_spline_quality"] = "Medium";
                    coreSettings["ppsspp_software_skinning"] = "disabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "enabled";
                    coreSettings["ppsspp_vertex_cache"] = "enabled";
                    coreSettings["ppsspp_fast_memory"] = "enabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "disabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "disabled";
                    coreSettings["ppsspp_force_lag_sync"] = "disabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "disabled";
                    break;
                case "Accurate":
                    coreSettings["ppsspp_block_transfer_gpu"] = "enabled";
                    coreSettings["ppsspp_spline_quality"] = "High";
                    coreSettings["ppsspp_software_skinning"] = "disabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "disabled";
                    coreSettings["ppsspp_vertex_cache"] = "disabled";
                    coreSettings["ppsspp_fast_memory"] = "disabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "disabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "disabled";
                    coreSettings["ppsspp_force_lag_sync"] = "enabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "disabled";
                    break;
                default:
                    coreSettings["ppsspp_block_transfer_gpu"] = "enabled";
                    coreSettings["ppsspp_spline_quality"] = "Medium";
                    coreSettings["ppsspp_software_skinning"] = "enabled";
                    coreSettings["ppsspp_gpu_hardware_transform"] = "enabled";
                    coreSettings["ppsspp_vertex_cache"] = "disabled";
                    coreSettings["ppsspp_fast_memory"] = "enabled";
                    coreSettings["ppsspp_lazy_texture_caching"] = "disabled";
                    coreSettings["ppsspp_retain_changed_textures"] = "disabled";
                    coreSettings["ppsspp_force_lag_sync"] = "disabled";
                    coreSettings["ppsspp_disable_slow_framebuffer_effects"] = "disabled";
                    break;
            }

            BindFeature(coreSettings, "ppsspp_internal_resolution", "ppsspp_internal_resolution", "1440x816");
            BindFeature(coreSettings, "ppsspp_texture_anisotropic_filtering", "ppsspp_texture_anisotropic_filtering", "off");
            BindFeature(coreSettings, "ppsspp_texture_filtering", "ppsspp_texture_filtering", "Auto");
            BindFeature(coreSettings, "ppsspp_texture_scaling_type", "ppsspp_texture_scaling_type", "xbrz");
            BindFeature(coreSettings, "ppsspp_texture_scaling_level", "ppsspp_texture_scaling_level", "Off");
            BindFeature(coreSettings, "ppsspp_texture_deposterize", "ppsspp_texture_deposterize", "disabled");
            BindFeature(coreSettings, "ppsspp_language", "ppsspp_language", "Automatic");
            BindFeature(coreSettings, "ppsspp_io_timing_method", "ppsspp_io_timing_method", "Fast");
            BindFeature(coreSettings, "ppsspp_ignore_bad_memory_access", "ppsspp_ignore_bad_memory_access", "enabled");
            BindFeature(coreSettings, "ppsspp_texture_replacement", "ppsspp_texture_replacement", "disabled");
            BindFeature(coreSettings, "ppsspp_button_preference", "ppsspp_button_preference", "Cross");
            BindFeature(coreSettings, "ppsspp_mulitsample_level", "ppsspp_mulitsample_level", "Disabled");
        }

        private void ConfigurePrBoom(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "prboom")
                return;

            BindFeature(retroarchConfig, "input_libretro_device_p1", "DoomControllerP1", "1");
            BindFeature(coreSettings, "prboom-resolution", "prboom_resolution", "320x200");
            BindFeature(coreSettings, "prboom-mouse_on", "prboom_mouse", "disabled");
            BindFeature(coreSettings, "prboom-find_recursive_on", "prboom_recursive", "enabled");
        }

        private void ConfigureProSystem(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "prosystem")
                return;

            BindFeature(coreSettings, "prosystem_color_depth", "prosystem_color_depth", "16bit");

            // Audio Filter
            if (Features.IsSupported("prosystem_low_pass_filter"))
            {
                if (SystemConfig.isOptSet("prosystem_low_pass_filter") && SystemConfig["prosystem_low_pass_filter"] != "0")
                {
                    coreSettings["prosystem_low_pass_filter"] = "enabled";
                    coreSettings["prosystem_low_pass_range"] = SystemConfig["prosystem_low_pass_filter"];
                }
                else
                {
                    coreSettings["prosystem_low_pass_filter"] = "disabled";
                    coreSettings["prosystem_low_pass_range"] = "60";
                }
            }

            BindFeature(coreSettings, "prosystem_gamepad_dual_stick_hack", "dual_stick_hack", "disabled");
        }

        private void ConfigurePuae(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "puae")
                return;

            coreSettings["puae_video_options_display"] = "enabled";
            coreSettings["puae_audio_options_display"] = "enabled";
            coreSettings["puae_use_whdload"] = "hdfs";

            // System options
            BindFeature(coreSettings, "puae_model", "model", "auto");
            BindFeature(coreSettings, "puae_cpu_compatibility", "cpu_compatibility", "normal");
            BindFeature(coreSettings, "puae_cpu_multiplier", "cpu_multiplier", "0");
            BindFeature(coreSettings, "puae_kickstart", "puae_kickstart", "auto");
            BindFeature(coreSettings, "puae_use_whdload_prefs", "whdload", "config");
            BindFeature(coreSettings, "puae_floppy_speed", "floppy_speed", "100");
            BindFeature(coreSettings, "puae_floppy_sound", "floppy_sound", "75");
            BindFeature(coreSettings, "puae_cd_speed", "puae_cd_speed", "100");
            BindFeature(coreSettings, "puae_cd_startup_delayed_insert", "puae_cd_delay", "disabled");

            // Video options
            BindFeature(coreSettings, "puae_video_resolution", "video_resolution", "auto");
            BindFeature(coreSettings, "puae_video_standard", "video_standard", "PAL auto");
            BindFeature(coreSettings, "puae_crop", "puae_crop", "auto");
            BindFeature(coreSettings, "puae_crop_mode", "puae_crop_mode", "both");
            BindFeature(coreSettings, "puae_gfx_colors", "puae_gfx_colors", "16bit");

            // Control options
            if (system == "amigacd32")
                BindFeature(coreSettings, "puae_cd32pad_options", "pad32_options", "disabled");
            else
                BindFeature(coreSettings, "puae_retropad_options", "pad_options", "disabled");

            BindFeature(retroarchConfig, "input_libretro_device_p1", "puae_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "puae_controller2", "1");
            BindFeature(coreSettings, "puae_analogmouse", "puae_analogmouse", "both");
            BindFeature(coreSettings, "puae_mouse_speed", "puae_mouse_speed", "100");
            BindFeature(coreSettings, "puae_physical_keyboard_pass_through", "puae_keyboard_pass_through", "disabled");

            /*Deprecated options
            BindFeature(coreSettings, "puae_zoom_mode", "zoom_mode", "auto");
            */
        }

        private void ConfigurePX68k(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "px68k")
                return;

            BindFeature(coreSettings, "px68k_cpuspeed", "px68k_cpuspeed", "10Mhz");
            BindFeature(coreSettings, "px68k_ramsize", "px68k_ramsize", "2MB");
            BindFeature(coreSettings, "px68k_frameskip", "px68k_frameskip", "Full Frame");
            BindFeature(coreSettings, "px68k_joytype1", "px68k_joytype", "Default (2 Buttons)");
            BindFeature(coreSettings, "px68k_joytype2", "px68k_joytype", "Default (2 Buttons)");
            BindFeature(coreSettings, "px68k_joy1_select", "px68k_joy1_select", "Default");
            BindFeature(coreSettings, "px68k_joy_mouse", "px68k_joy_mouse", "Mouse");
        }

        private void ConfigureQuasi88(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "quasi88")
                return;

            BindFeature(coreSettings, "q88_basic_mode", "q88_basic_mode", "N88 V2");
            BindFeature(coreSettings, "q88_cpu_clock", "q88_cpu_clock", "4");
            BindFeature(coreSettings, "q88_pcg-8100", "q88_pcg-8100", "disabled");
            BindFeature(coreSettings, "q88_sound_board", "q88_sound_board", "OPNA");

            // Controller type
            BindFeature(retroarchConfig, "input_libretro_device_p1", "pc88_controller1", "1");
        }

        private void ConfigureRace(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "race")
                return;

            BindFeature(coreSettings, "race_language", "race_language", "english");
        }

        private void ConfigureSameBoy(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "sameboy")
                return;

            BindFeature(coreSettings, "sameboy_rtc", "sameboy_rtc", "sync to system clock");

            bool multiplayer = (system == "gb2players" || system == "gbc2players");

            if (multiplayer)
            {
                coreSettings["sameboy_link"] = "enabled";
                coreSettings["sameboy_dual"] = "enabled";
                coreSettings["sameboy_rtc"] = "sync to system clock";
                BindFeature(coreSettings, "sameboy_screen_layout", "sameboy_screen_layout", "left-right");
                BindFeature(coreSettings, "sameboy_audio_output", "sameboy_audio_output", "Game Boy #1");

                if (system == "gb2players")
                {
                    BindFeature(coreSettings, "sameboy_mono_palette", "sameboy_mono_palette", "greyscale");
                    BindFeature(coreSettings, "sameboy_mono_palette_1", "sameboy_mono_palette", "greyscale");
                    BindFeature(coreSettings, "sameboy_mono_palette_2", "sameboy_mono_palette", "greyscale");
                }

                else if (system == "gbc2players")
                {
                    BindFeature(coreSettings, "sameboy_color_correction_mode", "sameboy_color_correction_mode", "emulate hardware");
                    BindFeature(coreSettings, "sameboy_color_correction_mode_1", "sameboy_color_correction_mode", "emulate hardware");
                    BindFeature(coreSettings, "sameboy_color_correction_mode_2", "sameboy_color_correction_mode", "emulate hardware");
                }
            }

            else
            {
                coreSettings["sameboy_link"] = "disabled";
                coreSettings["sameboy_dual"] = "disabled";

                if (system == "gb")
                {
                    BindFeature(coreSettings, "sameboy_mono_palette", "sameboy_mono_palette", "greyscale");
                }

                else if (system == "gbc")
                {
                    BindFeature(coreSettings, "sameboy_color_correction_mode", "sameboy_color_correction_mode", "emulate hardware");
                }
            }
        }

        private void ConfigureSameCDI(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "same_cdi")
                return;

            BindFeature(coreSettings, "same_cdi_altres", "samecdi_resolution", "640x480");
            BindFeature(coreSettings, "same_cdi_throttle", "samecdi_throttle", "disabled");
        }

        private void ConfigureSameDuck(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "sameduck")
                return;

            BindFeature(coreSettings, "sameduck_color_correction_mode", "sameduck_colorcorrect", "emulate hardware");
            BindFeature(coreSettings, "sameduck_rumble", "sameduck_rumble", "all games");
        }

        private void ConfigureScummVM(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "scummvm")
                return;

            BindFeature(coreSettings, "scummvm_analog_response", "scummvm_analog_response", "linear");

            try
            {
                // use scummvm.ini for options not available in retroarch-core-options.cfg
                string iniPath = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm.ini");
                if (File.Exists(iniPath))
                {
                    using (var ini = new IniFile(iniPath))
                    {

                        ini.WriteValue("scummvm", "extrapath", Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra"));
                        ini.WriteValue("scummvm", "themepath", Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "theme"));
                        ini.WriteValue("scummvm", "savepath", Path.Combine(AppConfig.GetFullPath("saves"), "scummvm"));

                        if (SystemConfig.isOptSet("ratio"))
                            ini.WriteValue("scummvm", "aspect_ratio", "true");

                        // Sound Mode
                        if (SystemConfig.isOptSet("SoundMode"))
                        {
                            if (SystemConfig["SoundMode"] == "fluidsynth")
                            {
                                ini.WriteValue("scummvm", "music_driver", "fluidsynth");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "fluidsynth");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "emulated_mt32")
                            {
                                ini.WriteValue("scummvm", "music_driver", "mt32");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "mt32");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "true_mt32")
                            {
                                ini.WriteValue("scummvm", "music_driver", "mt32");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "mt32");
                                ini.WriteValue("scummvm", "native_mt32", "true");
                            }
                            else if (SystemConfig["SoundMode"] == "adlib_mame")
                            {
                                ini.WriteValue("scummvm", "music_driver", "adlib");
                                ini.WriteValue("scummvm", "opl_driver", "mame");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "adlib_dosbox")
                            {
                                ini.WriteValue("scummvm", "music_driver", "adlib");
                                ini.WriteValue("scummvm", "opl_driver", "db");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "adlib_nuked")
                            {
                                ini.WriteValue("scummvm", "music_driver", "adlib");
                                ini.WriteValue("scummvm", "opl_driver", "nuked");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "pcspeaker")
                            {
                                ini.WriteValue("scummvm", "music_driver", "pcspk");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "pcjr")
                            {
                                ini.WriteValue("scummvm", "music_driver", "pcjr");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "cms")
                            {
                                ini.WriteValue("scummvm", "music_driver", "cms");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }
                            else if (SystemConfig["SoundMode"] == "disabled")
                            {
                                ini.WriteValue("scummvm", "music_driver", "null");
                                ini.WriteValue("scummvm", "opl_driver", "auto");
                                ini.WriteValue("scummvm", "gm_device", "auto");
                                ini.WriteValue("scummvm", "mt32_device", "auto");
                                ini.WriteValue("scummvm", "native_mt32", "false");
                            }

                        }
                        else
                        {
                            ini.WriteValue("scummvm", "music_driver", "auto");
                            ini.WriteValue("scummvm", "opl_driver", "auto");
                            ini.WriteValue("scummvm", "gm_device", "auto");
                            ini.WriteValue("scummvm", "mt32_device", "auto");
                            ini.WriteValue("scummvm", "native_mt32", "false");
                        }

                        // Mixed Adlib/Midi
                        if (SystemConfig.isOptSet("multi_midi"))
                            ini.WriteValue("scummvm", "multi_midi", SystemConfig["multi_midi"]);
                        else
                            ini.WriteValue("scummvm", "multi_midi", "true");

                        // Sound Font
                        if (SystemConfig.isOptSet("soundfont"))
                            ini.WriteValue("scummvm", "soundfont", Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", SystemConfig["soundfont"]));

                        // Text and Speech (enable subtitles, speech or both)
                        if (SystemConfig.isOptSet("TextSpeech"))
                        {
                            if (SystemConfig["TextSpeech"] == "Speech")
                            {
                                ini.WriteValue("scummvm", "speech_mute", "false");
                                ini.WriteValue("scummvm", "subtitles", "false");
                            }
                            else if (SystemConfig["TextSpeech"] == "Subtitles")
                            {
                                ini.WriteValue("scummvm", "speech_mute", "true");
                                ini.WriteValue("scummvm", "subtitles", "true");
                            }
                            else if (SystemConfig["TextSpeech"] == "Both")
                            {
                                ini.WriteValue("scummvm", "speech_mute", "false");
                                ini.WriteValue("scummvm", "subtitles", "true");
                            }

                        }
                        else
                        {
                            ini.WriteValue("scummvm", "speech_mute", "false");
                            ini.WriteValue("scummvm", "subtitles", "true");
                        }

                        /* Render mode options through scummvm menu seems having no effects for now.
                         * May be will be usefull later.
                         
                        if (SystemConfig.isOptSet("render_mode"))
                            ini.WriteValue("scummvm", "render_mode", SystemConfig["render_mode"]);
                        else
                            ini.Remove("scummvm", "render_mode");
                        */
                    }
                }
            }
            catch { }
        }

        private void ConfigureSNes9x(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "snes9x" && core != "snes9x_next")
                return;

            coreSettings["snes9x_show_advanced_av_settings"] = "enabled";

            BindFeature(coreSettings, "snes9x_blargg", "snes9x_blargg", "disabled"); // Emulated video signal
            BindFeature(coreSettings, "snes9x_overscan", "snes9x_overscan", "auto"); // Overscan
            BindFeature(coreSettings, "snes9x_region", "snes9x_region", "auto"); // Region
            BindFeature(coreSettings, "snes9x_hires_blend", "snes9x_hires_blend", "disabled"); // Pixel blending
            BindFeature(coreSettings, "snes9x_audio_interpolation", "snes9x_audio_interpolation", "none"); // Audio interpolation
            BindFeature(coreSettings, "snes9x_overclock_superfx", "snes9x_overclock_superfx", "100%"); // SuperFX overclock
            BindFeature(coreSettings, "snes9x_block_invalid_vram_access", "snes9x_block_invalid_vram_access", "enabled"); // Block invalid VRAM access

            // Unsafe hacks (config must be done in Core options)
            if (SystemConfig.isOptSet("SnesUnsafeHacks") && SystemConfig["SnesUnsafeHacks"] == "config")
            {
                coreSettings["snes9x_echo_buffer_hack"] = "enabled";
                coreSettings["snes9x_overclock_cycles"] = "enabled";
                coreSettings["snes9x_randomize_memory"] = "enabled";
                coreSettings["snes9x_reduce_sprite_flicker"] = "enabled";
            }
            else
            {
                coreSettings["snes9x_echo_buffer_hack"] = "disabled";
                coreSettings["snes9x_overclock_cycles"] = "disabled";
                coreSettings["snes9x_randomize_memory"] = "disabled";
                coreSettings["snes9x_reduce_sprite_flicker"] = "disabled";
            }

            // Advanced video options (config must be done in Core options menu)
            if (SystemConfig.isOptSet("SnesAdvancedVideoOptions") && SystemConfig["SnesAdvancedVideoOptions"] == "config")
            {
                coreSettings["snes9x_layer_1"] = "disabled";
                coreSettings["snes9x_layer_2"] = "disabled";
                coreSettings["snes9x_layer_3"] = "disabled";
                coreSettings["snes9x_layer_4"] = "disabled";
                coreSettings["snes9x_layer_5"] = "disabled";
                coreSettings["snes9x_gfx_clip"] = "disabled";
                coreSettings["snes9x_gfx_transp"] = "disabled";
            }
            else
            {
                coreSettings["snes9x_layer_1"] = "enabled";
                coreSettings["snes9x_layer_2"] = "enabled";
                coreSettings["snes9x_layer_3"] = "enabled";
                coreSettings["snes9x_layer_4"] = "enabled";
                coreSettings["snes9x_layer_5"] = "enabled";
                coreSettings["snes9x_gfx_clip"] = "enabled";
                coreSettings["snes9x_gfx_transp"] = "enabled";
            }

            // Advanced audio options (config must be done in Core options menu)
            if (SystemConfig.isOptSet("SnesAdvancedAudioOptions") && SystemConfig["SnesAdvancedAudioOptions"] == "config")
            {
                coreSettings["snes9x_sndchan_1"] = "disabled";
                coreSettings["snes9x_sndchan_2"] = "disabled";
                coreSettings["snes9x_sndchan_3"] = "disabled";
                coreSettings["snes9x_sndchan_4"] = "disabled";
                coreSettings["snes9x_sndchan_5"] = "disabled";
                coreSettings["snes9x_sndchan_6"] = "disabled";
                coreSettings["snes9x_sndchan_7"] = "disabled";
                coreSettings["snes9x_sndchan_8"] = "disabled";
            }
            else
            {
                coreSettings["snes9x_sndchan_1"] = "enabled";
                coreSettings["snes9x_sndchan_2"] = "enabled";
                coreSettings["snes9x_sndchan_3"] = "enabled";
                coreSettings["snes9x_sndchan_4"] = "enabled";
                coreSettings["snes9x_sndchan_5"] = "enabled";
                coreSettings["snes9x_sndchan_6"] = "enabled";
                coreSettings["snes9x_sndchan_7"] = "enabled";
                coreSettings["snes9x_sndchan_8"] = "enabled";
            }

            BindFeature(retroarchConfig, "input_libretro_device_p1", "SnesControllerP1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "SnesControllerP2", "1");

            if (Controllers.Count > 2)
            {
                retroarchConfig["input_libretro_device_p2"] = "257";
                retroarchConfig["input_libretro_device_p3"] = "1";
                retroarchConfig["input_libretro_device_p4"] = "1";
                retroarchConfig["input_libretro_device_p5"] = "1";
            }

            coreSettings["snes9x_show_lightgun_settings"] = "enabled";
            BindFeature(coreSettings, "snes9x_lightgun_mode", "snes9x_lightgun_mode", "Lightgun"); // Lightgun mode

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                string gunId = "260";

                var gunInfo = Program.GunGames.FindGame(system, SystemConfig["rom"]);
                if (gunInfo != null && gunInfo.GunType == "justifier")
                    gunId = "516";

                coreSettings["snes9x_superscope_reverse_buttons"] = (gunInfo != null && gunInfo.ReversedButtons ? "enabled" : "disabled");

                SetupLightGuns(retroarchConfig, gunId, core, 2);
            }
        }

        private void ConfigureStella(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "stella")
                return;

            BindFeature(coreSettings, "stella_console", "stella_console", "auto");
            BindFeature(coreSettings, "stella_palette", "stella_palette", "standard");
            BindFeature(coreSettings, "stella_filter", "stella_filter", "disabled");
            BindFeature(coreSettings, "stella_crop_hoverscan", "stella_crop_hoverscan", "disabled");
            BindFeature(coreSettings, "stella_phosphor", "stella_phosphor", "auto");

            // Lightgun
            SetupLightGuns(retroarchConfig, "4", core);
        }

        private void ConfigureStella2014(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "stella2014")
                return;

            BindFeature(coreSettings, "stella2014_color_depth", "stella2014_color_depth", "16bit");
            BindFeature(coreSettings, "stella2014_mix_frames", "stella2014_mix_frames", "disabled");

            // Audio Filter
            if (Features.IsSupported("stella2014_low_pass_filter"))
            {
                if (SystemConfig.isOptSet("stella2014_low_pass_filter") && SystemConfig["stella2014_low_pass_filter"] != "0")
                {
                    coreSettings["stella2014_low_pass_filter"] = "enabled";
                    coreSettings["stella2014_low_pass_range"] = SystemConfig["stella2014_low_pass_filter"];
                }
                else
                {
                    coreSettings["stella2014_low_pass_filter"] = "disabled";
                    coreSettings["stella2014_low_pass_range"] = "60";
                }
            }
        }

        private void ConfigureSwanStation(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "swanstation")
                return;

            BindFeature(coreSettings, "swanstation_Console_Region", "swanstation_region", "Auto");
            BindFeature(coreSettings, "swanstation_GPU_Renderer", "swanstation_GPU", "Auto");
            BindFeature(coreSettings, "swanstation_GPU_TextureFilter", "swanstation_texturefilter", "Nearest");
            BindFeature(coreSettings, "swanstation_Display_AspectRatio", "swanstation_aspectratio", "Auto");
            BindFeature(coreSettings, "swanstation_Display_CropMode", "swanstation_cropmode", "Overscan");
            BindFeature(coreSettings, "swanstation_GPU_ResolutionScale", "internal_resolution", "1");
            BindFeature(coreSettings, "swanstation_GPU_ForceNTSCTimings", "force_ntsc_timings", "false");
            BindFeature(coreSettings, "swanstation_GPU_WidescreenHack", "widescreen_hack", "false");
            BindFeature(coreSettings, "swanstation_GPU_MSAA", "msaa", "1");
            BindFeature(coreSettings, "swanstation_GPU_ScaledDithering", "scaled_dithering", "true");
            BindFeature(coreSettings, "swanstation_GPU_TrueColor", "truecolor", "false");
            BindFeature(coreSettings, "swanstation_BIOS_PatchFastBoot", "skip_bios", "true");

            // Controls
            if (SystemConfig.isOptSet("swanstation_controller") && !string.IsNullOrEmpty(SystemConfig["swanstation_controller"]))
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = SystemConfig["swanstation_controller"];
                }
            }
            else
            {
                for (int i = 1; i < 9; i++)
                {
                    retroarchConfig["input_libretro_device_p" + i] = "1";
                }
            }

            BindFeature(retroarchConfig, "swanstation_Controller_AnalogCombo", "swanstation_Controller_AnalogCombo", "4");

            if (Controllers.Count > 5)
                coreSettings["swanstation_ControllerPorts_MultitapMode"] = "BothPorts";
            else if (Controllers.Count > 2)
                coreSettings["swanstation_ControllerPorts_MultitapMode"] = "Port1Only";
            else
                coreSettings["swanstation_ControllerPorts_MultitapMode"] = "Disabled";

            if (SystemConfig.isOptSet("psx_gunport2") && SystemConfig.getOptBoolean("psx_gunport2"))
                SetupLightGuns(retroarchConfig, "260", core, 2);
            else
                SetupLightGuns(retroarchConfig, "260", core);

            // Disable multitap in case of lightgun
            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
                coreSettings["swanstation_ControllerPorts_MultitapMode"] = "Disabled";
        }

        private void ConfigureTGBDual(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "tgbdual")
                return;

            if (system == "gb2players" || system == "gbc2players")
            {
                coreSettings["tgbdual_gblink_enable"] = "enabled";
                BindFeature(coreSettings, "tgbdual_screen_placement", "tgbdual_screen_placement", "left-right");
            }
            else if (system != "gb2players" && system != "gbc2players")
                coreSettings["tgbdual_gblink_enable"] = "disabled";
        }

        private void ConfigureTheodore(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "theodore")
                return;

            coreSettings["theodore_autorun"] = "enabled";
        }

        private void ConfigureTyrquake(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "tyrquake")
                return;

            BindFeature(retroarchConfig, "input_libretro_device_p1", "quake_device_type", "1");
            BindFeature(coreSettings, "tyrquake_analog_deadzone", "quake_analog_deadzone", "15");
            BindFeature(coreSettings, "tyrquake_invert_y_axis", "quake_invert_y_axis", "disabled");
            BindFeature(coreSettings, "tyrquake_rumble", "quake_rumble", "disabled");
            BindFeature(coreSettings, "tyrquake_resolution", "quake_resolution", "320x200");
        }

        private void ConfigureVecx(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "vecx")
                return;

            BindFeature(coreSettings, "vecx_res_multi", "vecx_res_multi", "1");
        }

        private void Configurevice(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "vice_x64" && core != "vice_xvic" && core != "vice_xplus4" && core != "vice_x128" && core != "vice_x64sc" && core != "vice_xpet")
                return;

            // Common Vice features
            coreSettings["vice_audio_options_display"] = "enabled";
            BindFeature(coreSettings, "vice_warp_boost", "warp_boost", "enabled");
            BindFeature(coreSettings, "vice_aspect_ratio", "vice_aspect_ratio", "auto");
            BindFeature(coreSettings, "vice_crop", "vice_crop", "disabled");
            BindFeature(coreSettings, "vice_crop_mode", "vice_crop_mode", "both");
            BindFeature(coreSettings, "vice_gfx_colors", "vice_gfx_colors", "16bit");

            // vice_x64 specific features
            if (core == "vice_x64" || core == "vice_x64sc")
            {
                BindFeature(coreSettings, "vice_c64_model", "c64_model", "C64 PAL auto");
                BindFeature(coreSettings, "vice_ram_expansion_unit", "vice_ram_expansion_unit", "none");
                BindFeature(coreSettings, "vice_external_palette", "vice_external_palette", "colodore");
            }

            // vice_xvic specific features
            else if (core == "vice_xvic")
            {
                BindFeature(coreSettings, "vice_vic20_model", "vic20_model", "VIC20 PAL auto");
                BindFeature(coreSettings, "vice_vic20_memory_expansions", "vic20_memexpansion", "none");
                BindFeature(coreSettings, "vice_vic20_external_palette", "vic20_palette", "colodore_vic");
            }

            // vice_xplus4 specific features
            else if (core == "vice_xplus4")
            {
                BindFeature(coreSettings, "vice_plus4_model", "vice_plus4_model", "PLUS4 PAL");
                BindFeature(coreSettings, "vice_plus4_external_palette", "vice_plus4_external_palette", "colodore_ted");
            }

            // vice_x128 specific features
            else if (core == "vice_x128")
            {
                BindFeature(coreSettings, "vice_c128_model", "vice_c128_model", "C128 PAL");
                BindFeature(coreSettings, "vice_c128_ram_expansion_unit", "vice_c128_ram_expansion_unit", "none");
                BindFeature(coreSettings, "vice_external_palette", "vice_external_palette", "colodore");
            }

            // vice_xpet specific features
            else if (core == "vice_xpet")
            {
                BindFeature(coreSettings, "vice_pet_model", "vice_pet_model", "8032");
                BindFeature(coreSettings, "vice_pet_external_palette", "vice_pet_external_palette", "default");
            }

            // Controls
            BindFeature(coreSettings, "vice_retropad_options", "vice_retropad_options", "disabled");
            BindFeature(coreSettings, "vice_joyport", "vice_joyport", "2");
            BindFeature(retroarchConfig, "input_libretro_device_p1", "vice_controller1", "1");
            BindFeature(retroarchConfig, "input_libretro_device_p2", "vice_controller2", "1");
        }

        private void ConfigureVirtualJaguar(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "virtualjaguar")
                return;

            BindFeature(coreSettings, "virtualjaguar_usefastblitter", "usefastblitter", "disabled");
            BindFeature(coreSettings, "virtualjaguar_bios", "bios_vj", "enabled");
            BindFeature(coreSettings, "virtualjaguar_doom_res_hack", "doom_res_hack", "disabled");
            BindFeature(coreSettings, "virtualjaguar_pal", "vj_pal", "disabled");
        }

        private void ConfigureVitaquake2(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "vitaquake2")
                return;

            // Video settings
            BindFeature(coreSettings, "vitaquakeii_resolution", "vitaquakeii_resolution", "960x544");
            BindFeature(coreSettings, "vitaquakeii_renderer", "vitaquakeii_renderer", "opengl");
            BindFeature(coreSettings, "vitaquakeii_gl_shadows", "vitaquakeii_gl_shadows", "disabled");
            BindFeature(coreSettings, "vitaquakeii_gl_texture_filtering", "vitaquakeii_gl_texture_filtering", "nearest_hq");
            BindFeature(coreSettings, "vitaquakeii_hand", "vitaquakeii_hand", "right");
            BindFeature(coreSettings, "vitaquakeii_xhair", "vitaquakeii_xhair", "cross");

            // user interface
            BindFeature(coreSettings, "vitaquakeii_fps", "vitaquakeii_fps", "disabled");

            // Controls
            BindFeature(coreSettings, "vitaquakeii_invert_y_axis", "vitaquakeii_invert_y_axis", "enabled");
            BindFeature(coreSettings, "vitaquakeii_analog_deadzone", "vitaquakeii_analog_deadzone", "15");
            BindFeature(coreSettings, "vitaquakeii_rumble", "vitaquakeii_rumble", "disabled");
            BindFeature(coreSettings, "vitaquakeii_aimfix", "vitaquakeii_aimfix", "disabled");
            BindFeature(coreSettings, "vitaquakeii_mouse_sensitivity", "vitaquakeii_mouse_sensitivity", "3.0");
            BindFeature(retroarchConfig, "input_libretro_device_p1", "quake2_device_type", "1");
        }

        private void Configurex1(ConfigFile retroarchConfig, ConfigFile coreSettings, string system, string core)
        {
            if (core != "x1")
                return;

            BindFeature(coreSettings, "X1_RESOLUTE", "x1_resolute", "LOW");
        }
        #endregion

        #region Input remaps
        private Dictionary<string, string> InputRemap = new Dictionary<string, string>();

        private void CreateInputRemap(string cleanSystemName, Action<ConfigFile> createRemap)
        {
            if (string.IsNullOrEmpty(cleanSystemName))
                return;

            DeleteInputRemap(cleanSystemName);
            if (createRemap == null)
                return;

            string dir = Path.Combine(RetroarchPath, "config", "remaps", cleanSystemName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, cleanSystemName + ".rmp");

            this.AddFileForRestoration(path);

            var cfg = ConfigFile.FromFile(path, new ConfigFileOptions() { CaseSensitive = true });
            createRemap(cfg);
            cfg.Save(path, true);
        }

        private void DeleteInputRemap(string cleanSystemName)
        {
            if (string.IsNullOrEmpty(cleanSystemName))
                return;

            string dir = Path.Combine(RetroarchPath, "config", "remaps", cleanSystemName);
            string path = Path.Combine(dir, cleanSystemName + ".rmp");

            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch { }
        }
        #endregion
    }
}