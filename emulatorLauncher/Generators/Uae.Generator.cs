using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class UaeGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("winuae");

            string exe = Path.Combine(path, "winuae64.exe");
            if (!File.Exists(exe))
                return null;


            var disks = DetectDiscs(rom);
            if (disks.Count == 0)
                return null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("use_gui=no");

            sb.AppendLine("gfx_fullscreen_amiga=fullwindow");
            sb.AppendLine("gfx_width_fullscreen=1920");
            sb.AppendLine("gfx_height_fullscreen=1080");
            sb.AppendLine("gfx_filter_aspect_ratio=16:9");

            string disk = rom;
            if (system == "amigacd32" || core == "cd32")
            {
                disk = disks.First();

                sb.AppendLine("kickstart_rom_file=" + Path.Combine(AppConfig.GetFullPath("bios"), "kick40060.CD32"));
                sb.AppendLine("kickstart_ext_rom_file=" + Path.Combine(AppConfig.GetFullPath("bios"), "kick40060.CD32.ext"));
                sb.AppendLine("flash_file=.\\cd32.nvr");

                sb.AppendLine("cdimage0=" + disk);
                sb.AppendLine("cd_speed=0");
                sb.AppendLine("chipset_compatible=CD32");
                sb.AppendLine("chipset=aga");
                sb.AppendLine("chipset_refreshrate=49.920410");
                sb.AppendLine("bogomem_size=2");
                sb.AppendLine("chipmem_size=4");

                sb.AppendLine("floppy0type=-1");
                sb.AppendLine("floppy1type=-1");

                sb.AppendLine("cd32cd=true");
                sb.AppendLine("cd32c2p=true");
                sb.AppendLine("cd32nvram=true");
                sb.AppendLine("nr_floppies=0");
                sb.AppendLine("cpu_type=68ec020");
                sb.AppendLine("cpu_model=68020");
                sb.AppendLine("cpu_compatible=true");
                sb.AppendLine("cpu_24bit_addressing=true");
            }
            else if (Path.GetExtension(disk).ToLower() == ".lha")
            {
                string bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v3.1 r40.68 (1993)(Commodore)(A1200)[!].rom");
                sb.AppendLine("kickstart_rom_file=" + bios);

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
            else
            {
                string bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v1.3 r34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom");
                if (!File.Exists(bios))
                    bios = Path.Combine(AppConfig.GetFullPath("bios"), "Kickstart v1.3 r34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[o].rom");
                if (!File.Exists(bios))
                    bios = Path.Combine(AppConfig.GetFullPath("bios"), "KICK13.ROM");
                
                sb.AppendLine("kickstart_rom_file=" + bios);

                for (int i = 0; i < disks.Count; i++)
                {
                    sb.AppendLine("floppy" + i + "=" + disks[i]);
                    sb.AppendLine("floppy" + i + "type=0");
                }

                sb.AppendLine("nr_floppies=" + disks.Count);
                sb.AppendLine("floppy_speed=0");
            }

            sb.AppendLine("cpu_speed=max");
            
            if (SystemConfig.isOptSet("cycleexact") && SystemConfig.getOptBoolean("cycleexact"))
                sb.AppendLine("cycle_exact=true");

            string fn = Path.Combine(path, "game.uae");
            File.WriteAllText(fn, sb.ToString());
        
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
    }
}
