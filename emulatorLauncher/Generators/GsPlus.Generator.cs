using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class GsPlusGenerator : Generator
    {
        public GsPlusGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        private static readonly string[] _romNames = new string[] { "ROM", "ROM.01", "ROM.03", "APPLE2GS.ROM" };

        private static readonly string[] _diskExtensions = new string[] { ".2mg", ".po", ".do", ".dsk", ".nib", ".hdv", ".img", ".gz" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "gsplus.exe"))
                return null;

            bool fullscreen = ShouldRunFullscreen();

            // GSplus does not read archives
            string romExtension = Path.GetExtension(rom).ToLowerInvariant();
            if (romExtension == ".zip" || romExtension == ".7z" || romExtension == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories)
                        .OrderBy(file => Array.IndexOf(_diskExtensions, Path.GetExtension(file).ToLowerInvariant()))
                        .ToArray();

                    rom = romFiles.FirstOrDefault(file => _diskExtensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            var disks = GetGameDiskFiles(rom);
            if (disks.Count == 0)
                throw new ApplicationException("No Apple IIgs disk image found in " + Path.GetFileName(rom));

            foreach (var disk in disks.Where(d => !File.Exists(d)))
                throw new ApplicationException("File '" + disk + "' does not exist");

            if (disks.Any(d => Path.GetExtension(d).Equals(".woz", StringComparison.OrdinalIgnoreCase)))
                SimpleLogger.Instance.Warning("[Generator] GSplus 0.14 has no WOZ support, the image will most likely not boot.");

            // Working copies : GSplus writes back to the mounted image when it is not read-only
            for (int i = 0; i < disks.Count; i++)
            {
                bool diskWritable;
                disks[i] = ResolveDiskWorkingCopy(system, emulator, disks[i], out diskWritable, true);
            }

            SetupConfiguration(path, disks);

            if (!fullscreen)
                SystemConfig["bezel"] = "none";

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            _resolution = resolution;

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-fullscreen");
            else
                commandArray.Add("-borderless");

            // SDL2 joystick index
            var controller = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.PlayerIndex).FirstOrDefault();
            if (controller != null)
            {
                commandArray.Add("-joy");
                commandArray.Add(controller.GetSdlControllerIndex().ToString());
            }

            if (SystemConfig.isOptSet("gsplus_scanline") && !string.IsNullOrEmpty(SystemConfig["gsplus_scanline"]))
            {
                commandArray.Add("-scanline");
                commandArray.Add(SystemConfig["gsplus_scanline"]);
            }

            if (SystemConfig.getOptBoolean("gsplus_novsync"))
                commandArray.Add("-novsync");

            if (SystemConfig.getOptBoolean("gsplus_noaspect") && _bezelFileInfo == null)
                commandArray.Add("-noaspect");

            if (SystemConfig.getOptBoolean("gsplus_bw"))
                commandArray.Add("-bw");

            if (SystemConfig.getOptBoolean("gsplus_dhr140"))
                commandArray.Add("-dhr140");

            var targetResolution = resolution ?? GetTargetScreenResolution();
            commandArray.Add("-sw");
            commandArray.Add(targetResolution.Width.ToString());
            commandArray.Add("-sh");
            commandArray.Add(targetResolution.Height.ToString());

            string screenshots = AppConfig.GetFullPath("screenshots");
            if (!string.IsNullOrEmpty(screenshots) && Directory.Exists(screenshots))
            {
                commandArray.Add("-ssdir");
                commandArray.Add(screenshots);
            }

            string args = string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                // gsplus.exe opens a console window next to its window.
                // Starting minimized keeps that console out of the way.
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (_bezelFileInfo != null)
                ret.EnvironmentVariables["SDL_ALLOW_TOPMOST"] = "0";

            return ret;
        }

        /// <summary>
        /// Write config.txt. GSplus searches for "config.txt", "config.gsp" then ".config.gsp"
        /// </summary>
        private void SetupConfiguration(string path, List<string> disks)
        {
            string cfgFile = Path.Combine(path, "config.txt");

            try
            {
                using (var conf = new IniFile(cfgFile, IniOptions.UseSpaces))
                {
                    // A previous game must never be left mounted
                    conf.WriteValue(null, "s5d1", "");
                    conf.WriteValue(null, "s5d2", "");
                    conf.WriteValue(null, "s6d1", "");
                    conf.WriteValue(null, "s6d2", "");
                    for (int i = 1; i <= 12; i++)
                        conf.WriteValue(null, "s7d" + i, "");

                    // ROM. GSplus finds ROM / ROM.01 / ROM.03 on its own next to the executable,
                    // so only override when one of the names it accepts is in the bios folder :
                    // pointing the key at a missing file would break that search.
                    string bios = AppConfig.GetFullPath("bios");
                    if (!string.IsNullOrEmpty(bios) && Directory.Exists(bios))
                    {
                        string romFile = _romNames
                            .Select(n => Path.Combine(bios, n))
                            .FirstOrDefault(File.Exists);

                        if (romFile != null)
                            conf.WriteValue(null, "g_cfg_rom_path", romFile);
                        else
                            SimpleLogger.Instance.Warning("[Generator] No Apple IIgs ROM found in the bios folder (ROM, ROM.01, ROM.03 or APPLE2GS.ROM).");
                    }

                    // Mount each image in the slot its size calls for
                    int s5 = 1, s6 = 1, s7 = 1;
                    foreach (var disk in disks)
                    {
                        int slot = GetDiskSlot(disk);
                        string key;

                        if (slot == 6 && s6 <= 2)
                            key = "s6d" + (s6++);
                        else if (slot == 5 && s5 <= 2)
                            key = "s5d" + (s5++);
                        else if (s7 <= 12)
                            key = "s7d" + (s7++);
                        else
                        {
                            SimpleLogger.Instance.Warning("[Generator] No free drive left for " + Path.GetFileName(disk));
                            continue;
                        }

                        conf.WriteValue(null, key, disk);
                        SimpleLogger.Instance.Info("[Generator] " + key + " = " + disk);
                    }

                    // Paddles
                    BindIniFeature(conf, null, "g_swap_paddles", "gsplus_swap_paddles", "0");
                    BindIniFeature(conf, null, "g_invert_paddles", "gsplus_invert_paddles", "0");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Cannot write GSplus config.txt : " + ex.Message);
            }
        }

        /// <summary>
        /// Picks the slot a disk image is mounted in.
        ///   slot 7 : SmartPort, ProDOS block devices - 3.5" 800K and hard disk images
        ///   slot 6 : Disk II controller, the only one able to read 5.25" and nibble images
        ///   slot 5 : 3.5" IWM drive - mounts correctly but never boots, manual choice only
        /// </summary>
        private int GetDiskSlot(string file)
        {
            if (SystemConfig.isOptSet("gsplus_slot"))
            {
                int forced = SystemConfig["gsplus_slot"].ToInteger();
                if (forced >= 5 && forced <= 7)
                    return forced;
            }

            // Nibble images are a raw 5.25" flux stream, a SmartPort cannot read them.
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".nib" || ext == ".d13")
                return 6;

            try
            {
                // 5.25" images hold 140K of data, plus an optional 2IMG (64 bytes) or
                // MacBinary (128 bytes) header. Anything larger is a block device.
                if (new FileInfo(file).Length <= 140 * 1024 + 256)
                    return 6;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Cannot read the size of " + Path.GetFileName(file) + " : " + ex.Message);
            }

            return 7;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            // GSplus always returns 1
            if (ret == 1)
                return 0;

            return ret;
        }
    }
}