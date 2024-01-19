using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class UaeGenerator : Generator
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

            var disks = DetectDiscs(rom);
            if (disks.Count == 0)
                return null;

            winUAEConfigureIni(path, rom, system);
            string fn = WriteGameUaeFile(system, path, rom, disks);

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

        private void winUAEConfigureIni(string path, string rom, string system)
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
                ini.WriteValue("WinUAE", "ScreenshotPath", screenshotPath);

                string savestatePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "winuae");
                ini.WriteValue("WinUAE", "StatefilePath", savestatePath);

                string videoPath = Path.Combine(AppConfig.GetFullPath("records"), "winuae");
                ini.WriteValue("WinUAE", "VideoPath", videoPath);
            }
        }

        private void WriteKickstartRom(StringBuilder sb, string system)
        {
            string bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick40068.A1200");

            if (SystemConfig.isOptSet("a500_machine") && SystemConfig["a500_machine"] == "amiga500+")
            {
                bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v2.04 r37.175 (1991-05)(Commodore)(A500+)[!].rom");
                if (!File.Exists(bios))
                    bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick37175.A500");
                sb.AppendLine("kickstart_rom_file=" + bios);
            }

            else
            {
                switch (system)
                {
                    case "amiga500":
                        bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v2.04 r37.175 (1991-05)(Commodore)(A500+)[!].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick37175.A500");
                        sb.AppendLine("kickstart_rom_file=" + bios);
                        break;
                    case "amiga1200":
                        bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v3.1 r40.068 (1993-12)(Commodore)(A1200)[!].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart - 391774-01 (USA, Europe) (v3.1 Rev 40.068) (A1200).rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick40068.A1200");
                        sb.AppendLine("kickstart_rom_file=" + bios);
                        break;
                    case "amiga4000":
                        bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v3.1 r40.068 (1993-12)(Commodore)(A4000)[!].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick40068.A4000");
                        sb.AppendLine("kickstart_rom_file=" + bios);
                        break;
                    case "amigacd32":
                        bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v3.1 r40.060 (1993-05)(Commodore)(CD32)[!].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v3.1 r40.60 (1993)(Commodore)(CD32).rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick40060.CD32");
                        sb.AppendLine("kickstart_rom_file=" + bios);
                        break;
                    case "amigacdtv":
                        bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v1.3 r34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v1.3 r34.005 (1987-12)(Commodore)(A500-A1000-A2000-CDTV)[!].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v1.3 r34.005 (1987-12)(Commodore)(A500-A1000-A2000-CDTV)[o].rom");
                        if (!File.Exists(bios))
                            bios = Path.Combine(AppConfig.GetFullPath("bios"), "kick34005.CDTV");
                        sb.AppendLine("kickstart_rom_file=" + bios);
                        break;
                }
            }
        }

        private string WriteGameUaeFile(string system, string path, string rom, List<string> disks)
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
            sb.AppendLine("gfx_fullscreen_amiga=fullwindow");
            sb.AppendLine("gfx_width_fullscreen=1920");
            sb.AppendLine("gfx_height_fullscreen=1080");
            sb.AppendLine("gfx_filter_aspect_ratio=16:9");
            sb.AppendLine("gfx_colour_mode=32bit");

            if (SystemConfig.isOptSet("vsync") && !string.IsNullOrEmpty(SystemConfig["vsync"]))
            {
                sb.AppendLine("gfx_vsync=" + SystemConfig["vsync"]);
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

            WriteKickstartRom(sb, system);

            if (Path.GetExtension(disk).ToLower() == ".lha")
            {
                string WHDLoad = Path.Combine(path, "WHDLoad");

                sb.AppendLine("cpu_type=68ec020");
                sb.AppendLine("cpu_model=68020");

                sb.AppendLine(@"filesystem2=ro,DH0:boot:" + WHDLoad + @"\DH0,0");
                sb.AppendLine(@"uaehf0=dir,ro,DH0:boot:" + WHDLoad + @"\DH0,0");

                sb.AppendLine(@"filesystem2=ro,DH1:" + Path.GetFileName(disk) + ":" + disk + ",0");
                sb.AppendLine(@"uaehf1=dir,ro,DH1:" + Path.GetFileName(disk) + ":" + disk + ",0");

                sb.AppendLine(@"filesystem2=rw,DH2:boot:" + WHDLoad + @"\DH2,0");
                sb.AppendLine(@"uaehf2=dir,rw,DH2:boot:" + WHDLoad + @"\DH2,0");

                sb.AppendLine("cpu_compatible=true");
                sb.AppendLine("cpu_24bit_addressing=true");
            }

            else if (system == "amigacd32")
            {
                disk = disks.First();

                string extension = Path.Combine(AppConfig.GetFullPath("bios"), "CD32 Extended-ROM r40.60 (1993)(Commodore)(CD32).rom");
                if (!File.Exists(extension))
                    extension = Path.Combine(AppConfig.GetFullPath("bios"), "kick40060.CD32.ext");
                sb.AppendLine("kickstart_ext_rom_file=" + extension);

                sb.AppendLine("flash_file=.\\cd32.nvr");

                sb.AppendLine("cdimage0=" + disk);
                sb.AppendLine("chipset=aga");
                sb.AppendLine("chipset_compatible=CD32");
                sb.AppendLine("rtc=none");
                sb.AppendLine("ksmirror_e0=false");
                sb.AppendLine("ksmirror_a8=true");
                sb.AppendLine("cd32cd=true");
                sb.AppendLine("cd32c2p=true");
                sb.AppendLine("cd32nvram=true");
                sb.AppendLine("unmapped_address_space=zero");
                sb.AppendLine("fastmem_size=0");
                sb.AppendLine("a3000mem_size=0");
                sb.AppendLine("bogomem_size=0");
                sb.AppendLine("chipmem_size=4");
                sb.AppendLine("cpu_model=68020");
                sb.AppendLine("cpu_compatible=true");
                sb.AppendLine("cpu_data_cache=false");
                sb.AppendLine("cpu_multiplier=4");

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

                sb.AppendLine("floppy0type=-1");
                sb.AppendLine("floppy1type=-1");
                sb.AppendLine("nr_floppies=0");
            }

            else if (system == "amigacdtv")
            {
                disk = disks.First();

                string extension = Path.Combine(AppConfig.GetFullPath("bios"), "CDTV Extended-ROM v1.0 (1991)(Commodore)(CDTV)[!].rom");
                if (!File.Exists(extension))
                    extension = Path.Combine(AppConfig.GetFullPath("bios"), "CDTV Extended-ROM v2.7 (1992)(Commodore)(CDTV).rom");
                if (!File.Exists(extension))
                    extension = Path.Combine(AppConfig.GetFullPath("bios"), "kick40060.CD32.ext");
                sb.AppendLine("kickstart_ext_rom_file=" + extension);

                sb.AppendLine("cdtvsram_rom_file=:ENABLED");
                sb.AppendLine("cdtvdmac_rom_file=:ENABLED");
                sb.AppendLine("flash_file=.\\cdtv.nvr");
                sb.AppendLine("cdimage0=" + disk);
                sb.AppendLine("chipset_compatible=CDTV");
                sb.AppendLine("chipset=ecs_agnus");
                sb.AppendLine("rtc=MSM6242B");
                sb.AppendLine("ksmirror_e0=false");
                sb.AppendLine("cdtvcd=true");
                sb.AppendLine("cdtvram=true");
                sb.AppendLine("fastmem_size=0");
                sb.AppendLine("a3000mem_size=0");
                sb.AppendLine("bogomem_size=0");
                sb.AppendLine("chipmem_size=2");
                sb.AppendLine("cpu_type=68000");
                sb.AppendLine("cpu_model=68000");
                sb.AppendLine("cpu_compatible=true");
                sb.AppendLine("cpu_24bit_addressing=true");
                sb.AppendLine("cpu_multiplier=2");
                sb.AppendLine("cachesize=0");

                sb.AppendLine("floppy0type=-1");
                sb.AppendLine("floppy1type=-1");
                sb.AppendLine("nr_floppies=0");
            }

            else if (system == "amiga500")
            {
                for (int i = 0; i < disks.Count; i++)
                {
                    sb.AppendLine("floppy" + i + "=" + disks[i]);
                    sb.AppendLine("floppy" + i + "type=0");
                }

                sb.AppendLine("nr_floppies=" + disks.Count);
                sb.AppendLine("floppy_speed=0");

                if (SystemConfig.isOptSet("a500_machine") && SystemConfig["a500_machine"] == "amiga500+")
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
                    sb.AppendLine("bogomem_size=2");
                    sb.AppendLine("chipmem_size=1");
                }

                sb.AppendLine("rtc=MSM6242B");
                sb.AppendLine("cia_todbug=true");
                sb.AppendLine("cpu_type=68000");
                sb.AppendLine("cpu_model=68000");
                sb.AppendLine("cpu_compatible=true");
                sb.AppendLine("cpu_24bit_addressing=true");
                sb.AppendLine("cpu_multiplier=2");
                sb.AppendLine("cachesize=0");
            }

            else if (system == "amiga1200")
            {
                sb.AppendLine("pcmcia_mb_rom_file=:ENABLED");
                sb.AppendLine("ide_mb_rom_file=:ENABLED");

                for (int i = 0; i < disks.Count; i++)
                {
                    sb.AppendLine("floppy" + i + "=" + disks[i]);
                    sb.AppendLine("floppy" + i + "type=0");
                }

                sb.AppendLine("nr_floppies=" + disks.Count);
                sb.AppendLine("floppy_speed=0");
                sb.AppendLine("chipset=aga");
                sb.AppendLine("chipset_compatible=A1200");
                sb.AppendLine("rtc=MSM6242B");
                sb.AppendLine("ksmirror_a8=true");
                sb.AppendLine("pcmcia=true");
                sb.AppendLine("ide=a600/a1200");
                sb.AppendLine("fastmem_size=4");
                sb.AppendLine("a3000mem_size=0");
                sb.AppendLine("bogomem_size=0");
                sb.AppendLine("chipmem_size=4");

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
            }

            else if (system == "amiga4000")
            {
                sb.AppendLine("ide_mb_rom_file=:ENABLED");

                for (int i = 0; i < disks.Count; i++)
                {
                    sb.AppendLine("floppy" + i + "=" + disks[i]);
                    sb.AppendLine("floppy" + i + "type=0");
                }

                sb.AppendLine("nr_floppies=" + disks.Count);
                sb.AppendLine("floppy_speed=0");
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
                sb.AppendLine("cpu_type=68040");
                sb.AppendLine("cpu_model=68040");

                if (SystemConfig.isOptSet("amiga_fpu") && SystemConfig.getOptBoolean("amiga_fpu"))
                {
                    sb.AppendLine("fpu_model=68040");
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
            }

            string gameUae = Path.Combine(path, "game.uae");
            File.WriteAllText(gameUae, sb.ToString());

            return gameUae;
        }
    }
}
