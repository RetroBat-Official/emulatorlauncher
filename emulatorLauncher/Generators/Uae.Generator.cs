using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace EmulatorLauncher
{
    partial class UaeGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("winuae");

            string exe = Path.Combine(path, "winuae64.exe");
            if (!File.Exists(exe))
                return null;

            if (Path.GetExtension(rom).ToLower() == ".uae")
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = "\"" + rom + "\"",
                    WorkingDirectory = path,
                };
            }
            else if (Path.GetExtension(rom).ToLower() == ".ags")
            {
                SimpleLogger.Instance.Info("AGS file, reading for path to Amiga Game Selector.");
                var lines = File.ReadAllLines(rom);
                if (lines.Length > 0)
                {
                    string selectorPath = lines[0].Trim();
                    if (Directory.Exists(selectorPath))
                    {
                        SimpleLogger.Instance.Info("Launching Amiga Game Selector from: " + selectorPath);
                        
                        string selectorExe = Path.Combine(selectorPath, "winuae64.exe");

                        if (!File.Exists(selectorExe))
                        {
                            SimpleLogger.Instance.Error("Amiga Game Selector WinUAE executable not found at: " + selectorExe);
                            return null;
                        }

                        var uaeFile = Directory.EnumerateFiles(selectorPath, "*.uae", SearchOption.TopDirectoryOnly).FirstOrDefault();

                        if (uaeFile != null)
                        {
                            return new ProcessStartInfo()
                            {
                                FileName = selectorExe,
                                Arguments = "-f \"" + uaeFile + "\"",
                                WorkingDirectory = selectorPath,
                            };
                        }
                    }
                }
            }

            var disks = DetectDiscs(rom);
            if (disks.Count == 0)
                return null;

            bool fullscreen = ShouldRunFullscreen();

            WinUAEConfigureIni(path, system);
            string fn = WriteGameUaeFile(system, path, rom, disks, fullscreen, resolution);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = fn,
                WorkingDirectory = path,
            };
        }

        private static List<string> DetectDiscs(string disk)
        {
            string dskPath = Path.GetDirectoryName(disk);

            List<string> disks = new List<string>();

            if (Path.GetExtension(disk).ToLower() == ".m3u")
            {
                foreach (var line in File.ReadAllLines(disk))
                {
                    string dsk = Path.Combine(dskPath, line);
                    if (File.Exists(dsk))
                        disks.Add(dsk);
                }

                return disks;
            }

            disks.Add(disk);

            string dskName = Path.GetFileNameWithoutExtension(disk);

            for (int i = 2; i < 10; i++)
            {
                string dskI = dskName.Replace("-1", "-" + i);
                if (dskI == dskName)
                    dskI = dskName.Replace("_1", "_" + i);

                if (dskI == dskName)
                    dskI = dskName.Replace("Disk 1", "Disk " + i);

                if (dskI == dskName)
                    dskI = dskName.Replace("Disk1", "Disk" + i);

                if (dskI == dskName && dskI.EndsWith("1"))
                    dskI = dskName.Substring(0, dskName.Length - 1) + i;

                if (dskI == dskName)
                    break;

                string dsk = Path.Combine(dskPath, dskI + ".adf");
                if (!File.Exists(dsk))
                    dsk = Path.Combine(dskPath, dskI + ".adz");

                if (!File.Exists(dsk))
                    dsk = Path.Combine(dskPath, dskI + ".ad_");

                if (!File.Exists(dsk))
                    dsk = Path.Combine(dskPath, dskI + ".ad" + i);

                if (!File.Exists(dsk))
                    break;

                disks.Add(dsk);
            }

            return disks;
        }

        private void WinUAEConfigureIni(string path, string system)
        {
            string settingsFile = Path.Combine(path, "winuae.ini");

            using (IniFile ini = new IniFile(settingsFile))
            {
                // Write paths
                string biosPath = AppConfig.GetFullPath("bios");
                ini.WriteValue("WinUAE", "KickstartPath", biosPath);
                ini.WriteValue("WinUAE", "PathMode", "WinUAE");
                ini.WriteValue("WinUAE", "SaveImageOriginalPath", "0");
                ini.WriteValue("WinUAE", "RecursiveROMScan", "0");
                ini.WriteValue("WinUAE", "RelativePaths", "0");

                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "winuae");
                FileTools.EnsureDirectoryExists(screenshotPath);
                ini.WriteValue("WinUAE", "ScreenshotPath", screenshotPath);

                string savestatePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "winuae");
                FileTools.EnsureDirectoryExists(savestatePath);
                ini.WriteValue("WinUAE", "StatefilePath", savestatePath);

                string videoPath = Path.Combine(AppConfig.GetFullPath("records"), "winuae");
                FileTools.EnsureDirectoryExists(videoPath);
                ini.WriteValue("WinUAE", "VideoPath", videoPath);

                string nvramPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "winuae", "nvram");
                FileTools.EnsureDirectoryExists(nvramPath);
                ini.WriteValue("WinUAE", "NVRAMPath", nvramPath);

                string saveimagePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "winuae", "saveimages");
                FileTools.EnsureDirectoryExists(saveimagePath);
                ini.WriteValue("WinUAE", "SaveimagePath", saveimagePath);

                string ripPath = Path.Combine(AppConfig.GetFullPath("records"), "winuae", "rips");
                FileTools.EnsureDirectoryExists(ripPath);
                ini.WriteValue("WinUAE", "RipperPath", ripPath);

                string configPath = Path.Combine(path, "Configurations");
                FileTools.EnsureDirectoryExists(configPath);
                ini.WriteValue("WinUAE", "ConfigurationPath", configPath);
            }
        }

        private void WriteKickstartRom(StringBuilder sb, string system)
        {
            bool a500Plus = SystemConfig.isOptSet("a500_machine") && SystemConfig["a500_machine"] == "amiga500+";
            string key = (system == "amiga500" && a500Plus) ? "amiga500+" : system;

            AmigaBios.Candidate candidate;
            if (!AmigaBios.BySystem.TryGetValue(key, out candidate))
            {
                SimpleLogger.Instance.Warning("[WinUAE] Système inconnu pour le Kickstart : " + system);
                return;
            }

            string bios = AmigaBios.Find(candidate.Kickstart);
            if (bios == null || !File.Exists(bios))
                throw new ApplicationException("Kickstart introuvable pour " + key + ". Placez l'un de ces fichiers dans \\bios : " + string.Join(", ", candidate.Kickstart));
            sb.AppendLine("kickstart_rom_file=" + bios);

            if (candidate.KickstartExt != null)
            {
                string ext = AmigaBios.Find(candidate.KickstartExt);
                if (ext == null || !File.Exists(ext))
                    throw new ApplicationException("ROM étendu introuvable pour " + key + ". Placez l'un de ces fichiers dans \\bios : " + string.Join(", ", candidate.KickstartExt));
                sb.AppendLine("kickstart_ext_rom_file=" + ext);
            }
        }

        private string WriteGameUaeFile(string system, string path, string rom, List<string> disks, bool fullscreen, ScreenResolution resolution)
        {
            StringBuilder sb = new StringBuilder();

            // GUI options
            sb.AppendLine(@"; common");
            sb.AppendLine("use_gui=no");
            sb.AppendLine("use_debugger=false");
            sb.AppendLine("kickshifter=false");
            sb.AppendLine("scsidevice_disable=false");
            sb.AppendLine("cd_speed=100");
            sb.AppendLine("parallel_on_demand=false");
            sb.AppendLine("serial_on_demand=false");
            sb.AppendLine("serial_hardware_ctsrts=true");
            sb.AppendLine("serial_direct=false");
            sb.AppendLine("scsi=false");
            sb.AppendLine("uaeserial=false");
            sb.AppendLine("sana2=false");

            // Sound options
            sb.AppendLine("sound_output=exact");
            sb.AppendLine("sound_channels=stereo");
            sb.AppendLine("sound_stereo_separation=7");
            sb.AppendLine("sound_stereo_mixing_delay=0");
            sb.AppendLine("sound_max_buff=16384");
            sb.AppendLine("sound_frequency=44100");
            sb.AppendLine("sound_interpol=anti");
            sb.AppendLine("sound_filter=emulated");

            if (system == "amiga500" || system == "amigacdtv")
                sb.AppendLine("sound_filter_type=standard");
            else
                sb.AppendLine("sound_filter_type=enhanced");

            sb.AppendLine("sound_auto=true");

            // GFX options
            int gfxWidth = resolution != null ? resolution.Width : 1920;
            int gfxHeight = resolution != null ? resolution.Height : 1080;

            if (fullscreen)
            {
                sb.AppendLine("gfx_fullscreen_amiga=fullwindow");
                sb.AppendLine("gfx_fullscreen_picasso=fullwindow");
                sb.AppendLine("gfx_width_fullscreen=" + gfxWidth);
                sb.AppendLine("gfx_height_fullscreen=" + gfxHeight);
            }
            else
            {
                sb.AppendLine("gfx_fullscreen_amiga=false");
                sb.AppendLine("gfx_fullscreen_picasso=false");
                sb.AppendLine("gfx_width=" + gfxWidth);
                sb.AppendLine("gfx_height=" + gfxHeight);
            }

            sb.AppendLine("gfx_filter_aspect_ratio=16:9");
            sb.AppendLine("gfx_colour_mode=32bit");

            if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
            {
                sb.AppendLine("gfx_vsync=false");
                sb.AppendLine("gfx_vsyncmode=normal");
            }
            else if (Features.IsSupported("vsync"))
            {
                sb.AppendLine("gfx_vsync=true");
                sb.AppendLine("gfx_vsyncmode=normal");
            }

            sb.AppendLine("gfx_lores=false");
            sb.AppendLine("gfx_resolution=hires");
            sb.AppendLine("gfx_lores_mode=normal");

            if (SystemConfig.isOptSet("winuae_gfxrenderer") && !string.IsNullOrEmpty(SystemConfig["winuae_gfxrenderer"]) && SystemConfig["winuae_gfxrenderer"] == "direct3d11_soft")
            {
                sb.AppendLine("gfx_api=direct3d11");
                sb.AppendLine("gfx_api_options=software");
            }
            else if (SystemConfig.isOptSet("winuae_gfxrenderer") && !string.IsNullOrEmpty(SystemConfig["winuae_gfxrenderer"]) && SystemConfig["winuae_gfxrenderer"] != "direct3d11_soft")
            {
                sb.AppendLine("gfx_api=" + SystemConfig["winuae_gfxrenderer"]);
                sb.AppendLine("gfx_api_options=hardware");
            }
            else
            {
                sb.AppendLine("gfx_api=direct3d11");
                sb.AppendLine("gfx_api_options=hardware");
            }

            sb.AppendLine("gfx_blacker_than_black=false");
            sb.AppendLine("collision_level=playfields");
            sb.AppendLine("gfxcard_hardware_vblank=false");
            sb.AppendLine("gfxcard_hardware_sprite=true");
            sb.AppendLine("gfxcard_multithread=false");

            // CPU options
            sb.AppendLine("cpu_throttle=0.0");

            if (SystemConfig.isOptSet("cycleexact") && SystemConfig.getOptBoolean("cycleexact"))
            {
                sb.AppendLine("cpu_speed=real");
                sb.AppendLine("cpu_cycle_exact=true");
                sb.AppendLine("cpu_memory_cycle_exact=true");
                sb.AppendLine("blitter_cycle_exact=true");
                sb.AppendLine("cycle_exact=true");
            }
            else if (Features.IsSupported("cycleexact"))
            {
                sb.AppendLine("cpu_speed=max");
                sb.AppendLine("cpu_cycle_exact=false");
                sb.AppendLine("cpu_memory_cycle_exact=false");
                sb.AppendLine("blitter_cycle_exact=false");
                sb.AppendLine("cycle_exact=false");
            }

            // Memory
            sb.AppendLine("debugmem_start=0x0");
            sb.AppendLine("debugmem_size=0");
            sb.AppendLine("mem25bit_size=0");
            sb.AppendLine("mbresmem_size=0");
            sb.AppendLine("z3mem_size=0");
            sb.AppendLine("z3mem_start=0x10000000");

            sb.AppendLine("resetwarning=false");

            sb.AppendLine("cpu_data_cache=false");
            sb.AppendLine("rtg_nocustom=true");
            sb.AppendLine("rtg_modes=0x212");
            sb.AppendLine("debug_mem=false");
            sb.AppendLine("log_illegal_mem=false");

            string disk = rom;
            bool isWhdload = Path.GetExtension(disk).ToLower() == ".lha";

            WriteKickstartRom(sb, system);

            // Material profile
            if (system == "amigacd32")
                WriteCd32Profile(sb, disks, isWhdload);
            else if (system == "amigacdtv")
                WriteCdtvProfile(sb, disks, isWhdload);
            else if (system == "amiga500")
                WriteAmiga500Profile(sb, disks, isWhdload);
            else if (system == "amiga1200")
                WriteAmiga1200Profile(sb, disks, isWhdload);
            else if (system == "amiga4000")
                WriteAmiga4000Profile(sb, disks, isWhdload);

            // WHDLoad
            if (isWhdload)
            {
                string whdBoot = Path.Combine(path, "WHDBoot");
                EnsureWhdBootEnvironment(path);

                string savesDir = Path.Combine(AppConfig.GetFullPath("saves"), system, "winuae", "whdload", Path.GetFileNameWithoutExtension(disk));
                FileTools.EnsureDirectoryExists(savesDir);

                sb.AppendLine("cpu_type=68ec020");
                sb.AppendLine("cpu_model=68020");
                sb.AppendLine("cpu_compatible=true");
                //sb.AppendLine("cpu_24bit_addressing=true");

                // DH0 = WHDBoot boot environment (bootpri=0)
                sb.AppendLine(@"filesystem2=rw,DH0:WHDBoot:" + whdBoot + ",0");
                sb.AppendLine(@"uaehf0=dir,rw,DH0:WHDBoot:" + whdBoot + ",0");

                // DH1 = the game (contents of the .lha). RW and not ro: WHDLoad needs to write
                // (cache/saves) even when SAVEPATH is provided. bootpri=-128 = mounts
                sb.AppendLine(@"filesystem2=rw,DH1:" + Path.GetFileName(disk) + ":" + disk + ",-128");
                sb.AppendLine(@"uaehf1=dir,rw,DH1:" + Path.GetFileName(disk) + ":" + disk + ",-128");

                // DH2 = saves, real and persistent folder, dedicated to this game
                sb.AppendLine(@"filesystem2=rw,DH2:Saves:" + savesDir + ",-128");
                sb.AppendLine(@"uaehf2=dir,rw,DH2:Saves:" + savesDir + ",-128");

                sb.AppendLine("nr_floppies=0");
            }

            // Controllers
            ConfigureControls(sb, system);

            string gameUae = Path.Combine(path, "game.uae");
            File.WriteAllText(gameUae, sb.ToString());

            return gameUae;
        }

        private void WriteCd32Profile(StringBuilder sb, List<string> disks, bool isWhdload)
        {
            sb.AppendLine("flash_file=.\\cd32.nvr");
            sb.AppendLine("chipset=aga");
            sb.AppendLine("chipset_compatible=CD32");
            sb.AppendLine("rtc=none");
            sb.AppendLine("ksmirror_e0=false");
            sb.AppendLine("ksmirror_a8=true");
            sb.AppendLine("cd32cd=true");
            sb.AppendLine("cd32c2p=true");
            sb.AppendLine("cd32nvram=true");
            sb.AppendLine("unmapped_address_space=zero");

            bool fastRam = SystemConfig.getOptBoolean("winuae_cd32_fastram");
            if (isWhdload)
                sb.AppendLine("fastmem_size=" + (fastRam ? "8" : "4"));
            else
                sb.AppendLine("fastmem_size=" + (fastRam ? "8" : "0"));

            sb.AppendLine("a3000mem_size=0");
            sb.AppendLine("bogomem_size=0");
            sb.AppendLine("chipmem_size=4");
            sb.AppendLine("cpu_data_cache=false");
            sb.AppendLine("cpu_multiplier=4");
            sb.AppendLine("floppy0type=-1");
            sb.AppendLine("floppy1type=-1");
            sb.AppendLine("nr_floppies=0");

            if (isWhdload)
            {
                sb.AppendLine("cpu_24bit_addressing=true");
                return;
            }

            sb.AppendLine("cpu_model=68020");
            sb.AppendLine("cpu_compatible=true");

            if (SystemConfig.isOptSet("amiga_jit") && SystemConfig.getOptBoolean("amiga_jit"))
            {
                sb.AppendLine("cpu_24bit_addressing=false");

                if (SystemConfig.isOptSet("amiga_fpu") && SystemConfig.getOptBoolean("amiga_fpu"))
                {
                    sb.AppendLine("cpu_type=68020/68881");
                    sb.AppendLine("fpu_model=68882");
                    sb.AppendLine("fpu_strict=true");
                }
                else
                {
                    sb.AppendLine("cpu_type=68020");
                    sb.AppendLine("fpu_strict=false");
                }

                sb.AppendLine("cachesize=16384");
                sb.AppendLine("cpu_speed=max");
                sb.AppendLine("cpu_cycle_exact=false");
                sb.AppendLine("cpu_memory_cycle_exact=false");
                sb.AppendLine("blitter_cycle_exact=false");
                sb.AppendLine("cycle_exact=false");
                sb.AppendLine("comp_trustbyte=indirect");
                sb.AppendLine("comp_trustword=indirect");
                sb.AppendLine("comp_trustlong=indirect");
                sb.AppendLine("comp_trustnaddr=indirect");
                sb.AppendLine("comp_nf=true");
                sb.AppendLine("comp_constjump=true");
                sb.AppendLine("comp_flushmode=soft");
                sb.AppendLine("compfpu=true");
                sb.AppendLine("comp_catchfault=true");
            }
            else
            {
                sb.AppendLine("cpu_24bit_addressing=true");

                if (SystemConfig.isOptSet("amiga_fpu") && SystemConfig.getOptBoolean("amiga_fpu"))
                {
                    sb.AppendLine("cpu_type=68ec020/68881");
                    sb.AppendLine("fpu_model=68882");
                    sb.AppendLine("fpu_strict=true");
                }
                else
                {
                    sb.AppendLine("cpu_type=68ec020");
                    sb.AppendLine("fpu_strict=false");
                }

                sb.AppendLine("cachesize=0");
            }

            for (int i = 0; i < disks.Count; i++)
                sb.AppendLine("cdimage" + i + "=" + disks[i]);
        }

        private void WriteCdtvProfile(StringBuilder sb, List<string> disks, bool isWhdload)
        {
            sb.AppendLine("cdtvsram_rom_file=:ENABLED");
            sb.AppendLine("cdtvdmac_rom_file=:ENABLED");
            sb.AppendLine("flash_file=.\\cdtv.nvr");
            sb.AppendLine("chipset_compatible=CDTV");
            sb.AppendLine("chipset=ecs_agnus");
            sb.AppendLine("rtc=MSM6242B");
            sb.AppendLine("ksmirror_e0=false");
            sb.AppendLine("cdtvcd=true");
            sb.AppendLine("cdtvram=true");

            if (isWhdload)
                sb.AppendLine("fastmem_size=8");
            else
                sb.AppendLine("fastmem_size=0");

            sb.AppendLine("a3000mem_size=0");
            sb.AppendLine("bogomem_size=0");
            sb.AppendLine("chipmem_size=2");
            sb.AppendLine("cpu_multiplier=2");
            sb.AppendLine("floppy0type=-1");
            sb.AppendLine("floppy1type=-1");
            sb.AppendLine("nr_floppies=0");

            if (isWhdload)
            {
                sb.AppendLine("cpu_24bit_addressing=true");
                return;
            }

            sb.AppendLine("cpu_type=68000");
            sb.AppendLine("cpu_model=68000");
            sb.AppendLine("cpu_compatible=true");
            sb.AppendLine("cachesize=0");

            for (int i = 0; i < disks.Count; i++)
                sb.AppendLine("cdimage" + i + "=" + disks[i]);
        }

        private void WriteAmiga500Profile(StringBuilder sb, List<string> disks, bool isWhdload)
        {
            bool plus = SystemConfig.isOptSet("a500_machine") && SystemConfig["a500_machine"] == "amiga500+";

            if (isWhdload)
            {
                sb.AppendLine("chipset=ecs");
                sb.AppendLine("chipset_compatible=A500+");
                sb.AppendLine("fastmem_size=8");
                sb.AppendLine("a3000mem_size=0");
                sb.AppendLine("bogomem_size=0");
                sb.AppendLine("chipmem_size=4");
                sb.AppendLine("cpu_24bit_addressing=true");
            }
            else if (plus)
            {
                sb.AppendLine("chipset=ecs");
                sb.AppendLine("chipset_compatible=A500+");
                sb.AppendLine("fastmem_size=4");
                sb.AppendLine("a3000mem_size=8");
                sb.AppendLine("bogomem_size=0");
                sb.AppendLine("cpuboardmem1_size=256");
                sb.AppendLine("chipmem_size=2");
            }
            else
            {
                sb.AppendLine("chipset=ocs");
                sb.AppendLine("chipset_compatible=A500");
                sb.AppendLine("fastmem_size=0");
                sb.AppendLine("a3000mem_size=0");
                sb.AppendLine("bogomem_size=0");
                sb.AppendLine("chipmem_size=1");
            }

            sb.AppendLine("rtc=MSM6242B");
            sb.AppendLine("cia_todbug=true");
            sb.AppendLine("cpu_multiplier=2");

            if (isWhdload)
                return;

            sb.AppendLine("cpu_type=68000");
            sb.AppendLine("cpu_model=68000");
            sb.AppendLine("cpu_compatible=true");
            sb.AppendLine("cpu_24bit_addressing=true");
            sb.AppendLine("cachesize=0");

            WriteFloppyOrVolumeMedia(sb, disks);
        }

        private void WriteAmiga1200Profile(StringBuilder sb, List<string> disks, bool isWhdload)
        {
            sb.AppendLine("pcmcia_mb_rom_file=:ENABLED");
            sb.AppendLine("ide_mb_rom_file=:ENABLED");
            sb.AppendLine("chipset=aga");
            sb.AppendLine("chipset_compatible=A1200");
            sb.AppendLine("rtc=MSM6242B");
            sb.AppendLine("ksmirror_a8=true");
            sb.AppendLine("pcmcia=true");
            sb.AppendLine("ide=a600/a1200");

            if (isWhdload)
            {
                bool fastRam = !SystemConfig.isOptSet("winuae_a1200_fastram") || SystemConfig.getOptBoolean("winuae_a1200_fastram");
                sb.AppendLine("fastmem_size=" + (fastRam ? "8" : "4"));
                sb.AppendLine("z3mem_size=" + (fastRam ? "16" : "0"));
                sb.AppendLine("cpu_24bit_addressing=false");
            }
            else
            {
                bool fastRam = !SystemConfig.isOptSet("winuae_a1200_fastram") || SystemConfig.getOptBoolean("winuae_a1200_fastram");
                sb.AppendLine("fastmem_size=" + (fastRam ? "4" : "0"));
                sb.AppendLine("cpu_24bit_addressing=true");
            }
            
            sb.AppendLine("a3000mem_size=0");
            sb.AppendLine("bogomem_size=0");
            sb.AppendLine("chipmem_size=4");

            if (isWhdload)
                return;

            if (SystemConfig.isOptSet("amiga_jit") && SystemConfig.getOptBoolean("amiga_jit"))
            {
                sb.AppendLine("cpu_24bit_addressing=false");

                if (SystemConfig.isOptSet("amiga_fpu") && SystemConfig.getOptBoolean("amiga_fpu"))
                {
                    sb.AppendLine("cpu_type=68020/68881");
                    sb.AppendLine("fpu_model=68882");
                    sb.AppendLine("fpu_strict=true");
                }
                else
                {
                    sb.AppendLine("cpu_type=68020");
                    sb.AppendLine("fpu_strict=false");
                }

                sb.AppendLine("cachesize=16384");
                sb.AppendLine("cpu_speed=max");
                sb.AppendLine("cpu_cycle_exact=false");
                sb.AppendLine("cpu_memory_cycle_exact=false");
                sb.AppendLine("blitter_cycle_exact=false");
                sb.AppendLine("cycle_exact=false");
                sb.AppendLine("comp_trustbyte=indirect");
                sb.AppendLine("comp_trustword=indirect");
                sb.AppendLine("comp_trustlong=indirect");
                sb.AppendLine("comp_trustnaddr=indirect");
                sb.AppendLine("comp_nf=true");
                sb.AppendLine("comp_constjump=true");
                sb.AppendLine("comp_flushmode=soft");
                sb.AppendLine("compfpu=true");
                sb.AppendLine("comp_catchfault=true");
            }
            else
            {
                sb.AppendLine("cpu_24bit_addressing=true");

                if (SystemConfig.isOptSet("amiga_fpu") && SystemConfig.getOptBoolean("amiga_fpu"))
                {
                    sb.AppendLine("cpu_type=68ec020/68881");
                    sb.AppendLine("fpu_model=68882");
                    sb.AppendLine("fpu_strict=true");
                }
                else
                {
                    sb.AppendLine("cpu_type=68ec020");
                    sb.AppendLine("fpu_strict=false");
                }

                sb.AppendLine("cachesize=0");
            }

            sb.AppendLine("cpu_model=68020");
            sb.AppendLine("cpu_compatible=true");
            sb.AppendLine("cpu_multiplier=4");

            WriteFloppyOrVolumeMedia(sb, disks);
        }

        private void WriteAmiga4000Profile(StringBuilder sb, List<string> disks, bool isWhdload)
        {
            sb.AppendLine("ide_mb_rom_file=:ENABLED");
            sb.AppendLine("chipset=aga");
            sb.AppendLine("chipset_compatible=A4000");
            sb.AppendLine("ciaatod=50hz");
            sb.AppendLine("ksmirror_e0=false");
            sb.AppendLine("fatgary=0");
            sb.AppendLine("ramsey=15");
            sb.AppendLine("z3_autoconfig=true");
            sb.AppendLine("unmapped_address_space=zero");
            sb.AppendLine("ide=a4000");
            sb.AppendLine("fastmem_size=0");
            sb.AppendLine("a3000mem_size=8");
            sb.AppendLine("bogomem_size=0");
            sb.AppendLine("chipmem_size=4");
            sb.AppendLine("cpu_24bit_addressing=true");

            if (isWhdload)
                return;

            string cpu4000 = SystemConfig.isOptSet("winuae_4000_cpu") ? SystemConfig["winuae_4000_cpu"] : "68040";
            sb.AppendLine("cpu_type=" + cpu4000);
            sb.AppendLine("cpu_model=" + cpu4000);

            if (SystemConfig.isOptSet("amiga_fpu") && SystemConfig.getOptBoolean("amiga_fpu"))
            {
                sb.AppendLine("fpu_model=" + (cpu4000 == "68030" ? "68882" : "68040"));
                sb.AppendLine("fpu_strict=true");
            }
            else
                sb.AppendLine("fpu_strict=false");

            sb.AppendLine("cpu_compatible=false");

            if (SystemConfig.isOptSet("amiga_jit") && SystemConfig.getOptBoolean("amiga_jit"))
            {
                sb.AppendLine("cpu_24bit_addressing=false");
                sb.AppendLine("cachesize=16384");
                sb.AppendLine("cpu_speed=max");
                sb.AppendLine("cpu_cycle_exact=false");
                sb.AppendLine("cpu_memory_cycle_exact=false");
                sb.AppendLine("blitter_cycle_exact=false");
                sb.AppendLine("cycle_exact=false");
                sb.AppendLine("comp_trustbyte=indirect");
                sb.AppendLine("comp_trustword=indirect");
                sb.AppendLine("comp_trustlong=indirect");
                sb.AppendLine("comp_trustnaddr=indirect");
                sb.AppendLine("comp_nf=true");
                sb.AppendLine("comp_constjump=true");
                sb.AppendLine("comp_flushmode=soft");
                sb.AppendLine("compfpu=true");
                sb.AppendLine("comp_catchfault=true");
            }
            else
            {
                sb.AppendLine("cpu_24bit_addressing=true");
                sb.AppendLine("cachesize=0");
            }

            for (int i = 0; i < disks.Count; i++)
            {
                sb.AppendLine("floppy" + i + "=" + disks[i]);
                sb.AppendLine("floppy" + i + "type=0");
            }

            sb.AppendLine("nr_floppies=" + disks.Count);
        }

        public static void UpdateAGS(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                SimpleLogger.Instance.Error("[AGS] Invalid AGS path.");
                return;
            }

            string uaePath = Path.GetDirectoryName(path);
            string executable = Path.Combine(uaePath, "winuae64.exe");
            var uaeFile = Directory.EnumerateFiles(uaePath, "*.uae", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (uaeFile != null && File.Exists(executable))
            {
                string romPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "roms", "amiga500");

                if (Directory.Exists(romPath))
                {
                    string agsFile = Path.Combine(romPath, "Amiga Game Selector.ags");
                    File.WriteAllText(agsFile, uaePath);
                }
            }
        }

        private static readonly string[] _hdfExtensions = { ".hdf", ".vhd", ".hdz" };

        private void WriteFloppyOrVolumeMedia(StringBuilder sb, List<string> disks)
        {
            string first = disks.FirstOrDefault();

            if (first != null && _hdfExtensions.Contains(Path.GetExtension(first).ToLowerInvariant()))
            {
                sb.AppendLine("nr_floppies=1");
                sb.AppendLine("floppy0type=0");
                sb.AppendLine("hardfile2=" + BuildHardfileEntry(first));
                return;
            }

            if (first != null && Directory.Exists(first))
            {
                string volume = Path.GetFileNameWithoutExtension(first.TrimEnd(Path.DirectorySeparatorChar));
                sb.AppendLine("nr_floppies=1");
                sb.AppendLine("floppy0type=0");
                sb.AppendLine(@"filesystem2=rw,DH0:" + volume + ":" + first + ",0");
                sb.AppendLine(@"uaehf0=dir,rw,DH0:" + volume + ":" + first + ",0");
                return;
            }

            for (int i = 0; i < disks.Count; i++)
            {
                sb.AppendLine("floppy" + i + "=" + disks[i]);
                sb.AppendLine("floppy" + i + "type=0");
            }

            sb.AppendLine("nr_floppies=" + disks.Count);
            sb.AppendLine("floppy_speed=" + (SystemConfig.isOptSet("winuae_floppy_speed") ? SystemConfig["winuae_floppy_speed"] : "100"));
        }

        private static string BuildHardfileEntry(string hdf)
        {
            if (IsRdbHardfile(hdf))
                return "rw,DH0:" + hdf + ",0,0,0,512,0,,uae0";

            long size = new FileInfo(hdf).Length;
            int sectors, surfaces;
            if (TryDetectHdfGeometry(hdf, size, out sectors, out surfaces))
                return "rw,DH0:" + hdf + "," + sectors + "," + surfaces + ",2,512,0,,uae0";

            throw new ApplicationException(
                "Unable to determine automatically the geometry of the hard disk \"" + Path.GetFileName(hdf) + "\" : " +
                "it does not have an RDB partition table, and no standard geometry allows to find a valid AmigaDOS root block. " +
                "Open this file in WinUAE (Hard drives > Add Hardfile, geometry detection button) to check if it is " +
                "actually usable, or get a version of this disk already formatted with an RDB table.");
        }

        private static bool TryDetectHdfGeometry(string hdf, long fileSize, out int sectors, out int surfaces)
        {
            sectors = 0;
            surfaces = 0;

            try
            {
                using (var fs = new FileStream(hdf, FileMode.Open, FileAccess.Read))
                {
                    var block0 = new byte[512];
                    if (fs.Read(block0, 0, 512) != 512)
                        return false;

                    // Only a file system with a standard boot block ("DOS...") can be
                    // validated this way - otherwise we can't verify anything, so we don't guess.
                    if (block0[0] != (byte)'D' || block0[1] != (byte)'O' || block0[2] != (byte)'S')
                        return false;

                    // "Modern" geometries tried internally by WinUAE itself
                    // (getchsgeometry_hdf / getchsgeometry2, cf. §15).
                    for (int mode = 0; mode < 2; mode++)
                    {
                        int cyl, head, spt;
                        GetChsGeometry2(fileSize, mode, out cyl, out head, out spt);
                        if (TryValidateRootBlock(fs, fileSize, cyl, head, spt, out sectors, out surfaces))
                            return true;
                    }

                    // Extended fallback
                    int[] extHeads = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 32, 64, 128, 255 };
                    int[] extSpt = { 9, 11, 17, 18, 20, 25, 26, 27, 32, 33, 34, 38, 39, 45, 51, 52, 56, 63, 85, 127, 255 };

                    long total = fileSize / 512;

                    foreach (int head in extHeads)
                    {
                        foreach (int spt in extSpt)
                        {
                            int cyl = (int)(total / (head * spt));
                            if (cyl <= 0)
                                continue;

                            if (TryValidateRootBlock(fs, fileSize, cyl, head, spt, out sectors, out surfaces))
                                return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool TryValidateRootBlock(FileStream fs, long fileSize, int cyl, int head, int spt, out int sectors, out int surfaces)
        {
            sectors = 0;
            surfaces = 0;

            long rootblock = (2L + ((long)cyl * head * spt - 1)) / 2L;
            long offset = rootblock * 512L;
            if (offset < 0 || offset + 512 > fileSize)
                return false;

            var block = new byte[512];
            fs.Seek(offset, SeekOrigin.Begin);
            if (fs.Read(block, 0, 512) != 512)
                return false;

            uint checksum = 0;
            for (int i = 0; i < 512; i += 4)
                checksum += (uint)((block[i] << 24) | (block[i + 1] << 16) | (block[i + 2] << 8) | block[i + 3]);

            bool valid = checksum == 0
                && block[0] == 0 && block[1] == 0 && block[2] == 0 && block[3] == 2   // T_HEADER
                && block[4] == 0 && block[5] == 0 && block[6] == 0 && block[7] == 0
                && block[8] == 0 && block[9] == 0 && block[10] == 0 && block[11] == 0
                && block[508] == 0 && block[509] == 0 && block[510] == 0 && block[511] == 1; // ST_ROOT

            if (!valid)
                return false;

            sectors = spt;
            surfaces = head;
            return true;
        }

        private static void GetChsGeometry2(long size, int mode, out int pcyl, out int phead, out int psectorspertrack)
        {
            long total = size / 512;
            int spt = 0, head = 0, cyl = 0;

            if (mode == 1)
            {
                // Géométrie "à l'ancienne" : 1 tête, 32 secteurs/piste.
                head = 1;
                spt = 32;
                cyl = (int)(total / (head * spt));
            }
            else
            {
                int[] sptt = { 63, 127, 255 };
                for (int i = 0; i < sptt.Length; i++)
                {
                    int maxhead = sptt[i] < 255 ? 16 : 255;
                    spt = sptt[i];
                    for (head = 4; head <= maxhead; head++)
                    {
                        cyl = (int)(total / (head * spt));
                        bool stop;
                        if (size <= 512L * 1024 * 1024)
                            stop = cyl <= 1023;
                        else
                            stop = (cyl < 16383) || (cyl < 32767 && head >= 5) || (cyl <= 65535);

                        if (stop)
                            break;

                        if (maxhead > 16)
                        {
                            head *= 2;
                            head--;
                        }
                    }
                    if (head <= 16)
                        break;
                }
            }

            if (head > 16)
                head--;

            pcyl = cyl;
            phead = head;
            psectorspertrack = spt;
        }

        private static bool IsRdbHardfile(string hdf)
        {
            try
            {
                using (var fs = new FileStream(hdf, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[4];
                    for (int block = 0; block < 16; block++)
                    {
                        fs.Seek(block * 512L, SeekOrigin.Begin);
                        if (fs.Read(buffer, 0, 4) != 4)
                            break;
                        if (buffer[0] == 'R' && buffer[1] == 'D' && buffer[2] == 'S' && buffer[3] == 'K')
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static void EnsureWhdBootEnvironment(string winuaePath)
        {
            string whdBoot = Path.Combine(winuaePath, "WHDBoot");

            if (!Directory.Exists(whdBoot))
            {
                string zip = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "templates", "winuae", "WHDBoot.zip");
                if (!File.Exists(zip))
                    throw new ApplicationException(
                        "WHDBoot environment missing, and " + zip + " not found either. " +
                        "Cannot set up WHDLoad boot environment.");

                try { Zip.Extract(zip, whdBoot); }
                catch (Exception ex)
                {
                    throw new ApplicationException(
                        "Failed to extract WHDBoot environment from " + zip + " : " + ex.Message);
                }
            }

            string kickstartsDir = Path.Combine(whdBoot, "Devs", "Kickstarts");
            FileTools.EnsureDirectoryExists(kickstartsDir);

            foreach (var entry in _whdBootKickstarts)
            {
                string source = AmigaBios.Find(entry.Candidates);
                if (source != null && File.Exists(source))
                {
                    string dest = Path.Combine(kickstartsDir, entry.TargetName);
                    try { File.Copy(source, dest, true); }
                    catch {  }
                }
            }
        }

        private class WhdBootKickstartEntry
        {
            public string TargetName;
            public string[] Candidates;

            public WhdBootKickstartEntry(string targetName, string[] candidates)
            {
                TargetName = targetName;
                Candidates = candidates;
            }
        }

        private static readonly WhdBootKickstartEntry[] _whdBootKickstarts = new[]
        {
            new WhdBootKickstartEntry("kick34005.A500", AmigaBios.BySystem["amiga500"].Kickstart),
            new WhdBootKickstartEntry("kick40063.A600", AmigaBios.BySystem["amiga600"].Kickstart),
            new WhdBootKickstartEntry("kick40068.A1200", AmigaBios.BySystem["amiga1200"].Kickstart),
            new WhdBootKickstartEntry("kick40068.A4000", AmigaBios.BySystem["amiga4000"].Kickstart),
        };
    }

    internal static class AmigaBios
    {
        public class Candidate
        {
            public string[] Kickstart;
            public string[] KickstartExt;
        }

        public static readonly Dictionary<string, Candidate> BySystem = new Dictionary<string, Candidate>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "amiga500", new Candidate { Kickstart = new[] {
                "kick34005.A500", "kick34005.A500.rom", "amiga-os-130.rom",
                "Kickstart v1.3 r34.005 (1987-12)(Commodore)(A500-A1000-A2000-CDTV)[!].rom",
                "Kickstart v1.3 r34.005 (1987-12)(Commodore)(A500-A1000-A2000-CDTV)[o].rom",
                "Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom" } } },

            { "amiga600", new Candidate { Kickstart = new[] {
                "kick40063.A600", "kick40063.A600.rom", "amiga-os-310-a600.rom",
                "Kickstart v3.1 r40.063 (1993-10)(Commodore)(A600HD)[!].rom" } } },

            { "amiga500+", new Candidate { Kickstart = new[] {
                "kick37175.A500", "kick37175.A500.rom", "amiga-os-204.rom",
                "Kickstart v2.04 r37.175 (1991-05)(Commodore)(A500+)[!].rom",
                "Kickstart v2.04 rev 37.175 (1991)(Commodore)(A500+).rom" } } },

            { "amiga1200", new Candidate { Kickstart = new[] {
                "kick40068.A1200", "kick40068.A1200.rom", "amiga-os-310-a1200.rom",
                "Kickstart v3.1 r40.068 (1993-12)(Commodore)(A1200)[!].rom",
                "Kickstart - 391774-01 (USA, Europe) (v3.1 Rev 40.068) (A1200).rom" } } },

            { "amiga4000", new Candidate { Kickstart = new[] {
                "kick40068.A4000", "kick40068.A4000.rom", "amiga-os-310-a4000.rom",
                "Kickstart v3.1 r40.068 (1993-12)(Commodore)(A4000)[!].rom" } } },

            { "amigacd32", new Candidate {
                Kickstart = new[] {
                    "kick40060.CD32", "kick40060.CD32.rom", "amiga-os-310-cd32.rom",
                    "Kickstart v3.1 r40.060 (1993-05)(Commodore)(CD32)[!].rom",
                    "Kickstart v3.1 r40.60 (1993)(Commodore)(CD32).rom" },
                KickstartExt = new[] {
                    "kick40060.CD32.ext", "kick40060.CD32.ext.rom", "amiga-os-310-cd32-ext.rom",
                    "CD32 Extended-ROM r40.60 (1993)(Commodore)(CD32).rom" } } },

            { "amigacdtv", new Candidate {
                Kickstart = new[] {
                    "kick34005.A500", "kick34005.A500.rom", "amiga-os-130.rom",
                    "Kickstart v1.3 r34.005 (1987-12)(Commodore)(A500-A1000-A2000-CDTV)[!].rom",
                    "Kickstart v1.3 r34.005 (1987-12)(Commodore)(A500-A1000-A2000-CDTV)[o].rom" },
                KickstartExt = new[] {
                    "kick34005.CDTV", "kick34005.CDTV.rom", "amiga-os-130-cdtv-ext.rom",
                    "CDTV Extended-ROM v1.0 (1991)(Commodore)(CDTV)[!].rom",
                    "CDTV Extended-ROM v1.0 (1992)(Commodore)(CDTV).rom" } } },
        };


        public static string Find(string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return null;

            string biosPath = Program.AppConfig.GetFullPath("bios");
            if (string.IsNullOrEmpty(biosPath))
                return Path.Combine(biosPath ?? "", candidates[0]);

            foreach (var dir in new[] { biosPath, Path.Combine(biosPath, "amiga"), Path.Combine(biosPath, "Amiga") })
            {
                if (!Directory.Exists(dir))
                    continue;

                foreach (var name in candidates)
                {
                    string full = Path.Combine(dir, name);
                    if (File.Exists(full))
                        return full;
                }
            }

            return Path.Combine(biosPath, candidates[0]);
        }
    }
}
