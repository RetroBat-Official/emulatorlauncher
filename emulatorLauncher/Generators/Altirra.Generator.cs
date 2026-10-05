using System;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Collections.Generic;
using System.Diagnostics;

namespace EmulatorLauncher
{
    partial class AltirraGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _machine;
        private string _config;
        private AltirraProfile _profile;

        // Altirra.exe /portable /f /hardware 5200 "<path to rom>"
        //  /w /f /[no]borderless     Enable/disable borderless full-screen mode
        //  /pal /secam /ntsc /[no]fastboot /[no]casautoboot /[no]vsync
        //  /kernel:default|osa|osb|xl|lle|llexl|5200|5200lle
        //  /hardware:800|800xl|5200
        //  /memsize:8K|16K|24K|32K|48K|52K|64K|128K|320K|320KCOMPY|576K|576KCOMPY|1088K
        //  /axlonmemsize:none|64K|128K|256K|512K|1024K|2048K|4096K
        //  /diskemu:generic|generic56k|fastest|810|1050|xf551|usdoubler|speedy1050|indusgt|happy810|happy1050|1050turbo Set standard disk emulation mode
        //  /d3d9 /d3d11
        //  /[no]hdpath|hdpathrw <path> - mount H: device
        //  /cartmapper <mapper> - set cartridge mapper for untagged image
        //  /portablealt:<file>
        //  /cart /tape /disk
        //  /[no]autoprofile    Automatically select default profile for image type
        //  /[no] soundboard:d2c0|d500|d600
        //  /skipsetup
        //  /defprofile:800|xl|xegs|1200xl|5200

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "Altirra64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "Altirra.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = ShouldRunFullscreen();

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            if (SystemConfig.isOptSet("altirra_machine") && !string.IsNullOrEmpty(SystemConfig["altirra_machine"]))
                _machine = SystemConfig["altirra_machine"];
            else
            {
                if (atariProfileMap.ContainsKey(system))
                    _machine = atariProfileMap[system];
                else
                    _machine = system.Replace("atari", "").ToUpperInvariant();
            }

            _resolution = resolution;

            // Configuration file
            string configFile = Path.Combine(path, "Altirra.ini");
            if (!File.Exists(configFile))
            {
                string templateCfgFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", emulator, "Altirra.ini");
                if (File.Exists(templateCfgFile))
                    try { File.Copy(templateCfgFile, configFile); } catch { }
            }

            SetupAltirraConfiguration(configFile, system);

            _config = configFile;

            // Command line arguments
            List<string> commandArray = new List<string>();

            commandArray.Add("/portable");
            commandArray.Add("/skipsetup");

            if (fullscreen)
            {
                commandArray.Add("/f");
                commandArray.Add("/borderless");
            }

            if (SystemConfig.isOptSet("altirra_renderer") && !string.IsNullOrEmpty(SystemConfig["altirra_renderer"]))
                commandArray.Add("/" + SystemConfig["altirra_renderer"]);
            else
                commandArray.Add("/d3d11");

            if (SystemConfig.getOptBoolean("altirra_autoprofile"))
                commandArray.Add("/autoprofile");

            if (SystemConfig.isOptSet("altirra_cartmapper") && !string.IsNullOrEmpty(SystemConfig["altirra_cartmapper"]))
            {
                commandArray.Add("/cartmapper");
                commandArray.Add(SystemConfig["altirra_cartmapper"]);
            }
            else if (_profile != null && (_profile.Machine == "5200" || _profile.Machine == "XEGS"))
            {
                string mapper = GetDefaultCartMapper(rom, _profile.Machine);
                if (mapper != null)
                {
                    commandArray.Add("/cartmapper");
                    commandArray.Add(mapper);
                }
            }

            commandArray.Add("\"" + rom + "\"");
            
            string args = string.Join(" ", commandArray);

            // Launch emulator
            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        /// <summary>
        /// Setup Altirra.ini file
        /// </summary>
        /// <param name="conf"></param>
        /// <param name="system"></param>
        private void SetupAltirraConfiguration(string conf, string system)
        {
            AltirraProfile p = altirraProfiles.FirstOrDefault(a => a.Machine == _machine);

            if (p != null)
                _profile = p;

            using (var ini = IniFile.FromFile(conf, IniOptions.UseSpaces | IniOptions.ManageKeysWithQuotes))
            {
                ini.WriteValue("User\\Software\\virtualdub.org\\Altirra\\Settings", "Display: Borderless mode", SystemConfig.getOptBoolean("exclusivefs") ? "0" : "1");

                ini.WriteValue("User\\Software\\virtualdub.org\\Altirra", "ShownSetupWizard", "1");
                ini.WriteValue("User\\Software\\virtualdub.org\\Altirra\\DialogDefaults", "DiscardMemory", "\"ok\"");

                // Environment and acceleration categories are read from the global profile (machine profiles only hold hardware/firmware)
                string globalProfileSection = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000";
                ini.Remove("", "Pause when inactive");  // stray root key written by older versions
                ini.WriteValue(globalProfileSection, "Pause when inactive", "1");
                ini.WriteValue(globalProfileSection, "Cassette: Auto-boot enabled", "1");
                ini.WriteValue(globalProfileSection, "Cassette: Auto-rewind enabled", "1");

                if (p != null)
                    ini.WriteValue("User\\Software\\virtualdub.org\\Altirra\\Profiles", "Current profile", p.ProfileCode);

                string defaultsProfileSection = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\Defaults";
                string profileSection = "";

                if (p != null)
                {
                    profileSection = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\" + p.ProfileSectionCode;

                    ini.WriteValue(profileSection, "_Name", "\"" + p.Name + "\"");
                    ini.WriteValue(profileSection, "_Visible", "1");
                    ini.WriteValue(profileSection, "_Category Mask", "\"hardware,firmware\"");
                    ini.WriteValue(profileSection, "_Saved Category Mask", "\"hardware,firmware\"");

                    // String values must be quoted, otherwise Altirra silently ignores them
                    if (SystemConfig.isOptSet("altirra_kernel") && SystemConfig["altirra_kernel"] == "internal")
                        ini.WriteValue(profileSection, "Kernel path", "\"" + p.InternalKernel + "\"");
                    else
                        ini.WriteValue(profileSection, "Kernel path", "\"\"");

                    ini.WriteValue(defaultsProfileSection, _machine, p.ProfileCode);

                    if (SystemConfig.isOptSet("altirra_videostandard") && !string.IsNullOrEmpty(SystemConfig["altirra_videostandard"]))
                    {
                        string standard = SystemConfig["altirra_videostandard"];

                        switch (standard)
                        {
                            case "ntsc":
                                ini.WriteValue(profileSection, "PAL mode", "0");
                                ini.WriteValue(profileSection, "SECAM mode", "0");
                                ini.WriteValue(profileSection, "Mixed video mode", "0");
                                break;
                            case "pal":
                                ini.WriteValue(profileSection, "PAL mode", "1");
                                ini.WriteValue(profileSection, "SECAM mode", "0");
                                ini.WriteValue(profileSection, "Mixed video mode", "0");
                                break;
                            case "secam":
                                ini.WriteValue(profileSection, "PAL mode", "1");
                                ini.WriteValue(profileSection, "SECAM mode", "1");
                                ini.WriteValue(profileSection, "Mixed video mode", "0");
                                break;
                            case "ntsc50":
                                ini.WriteValue(profileSection, "PAL mode", "1");
                                ini.WriteValue(profileSection, "SECAM mode", "0");
                                ini.WriteValue(profileSection, "Mixed video mode", "1");
                                break;
                            case "pal60":
                                ini.WriteValue(profileSection, "PAL mode", "0");
                                ini.WriteValue(profileSection, "SECAM mode", "0");
                                ini.WriteValue(profileSection, "Mixed video mode", "1");
                                break;
                        }
                    }
                    else
                    {
                        // Default: NTSC (Altirra default)
                        ini.WriteValue(profileSection, "PAL mode", "0");
                        ini.WriteValue(profileSection, "SECAM mode", "0");
                        ini.WriteValue(profileSection, "Mixed video mode", "0");
                    }

                    BindBoolIniFeature(ini, profileSection, "GTIA: CTIA mode", "altirra_ctia", "1", "0");
                }

                ConfigureControllers(ini, system, p);

                ini.Save();
            }
        }

        /// <summary>
        /// Altirra applies /cartmapper to any headerless image regardless of its size (image is truncated or padded),
        /// so the mapper must match the image size. Returns null when no safe default exists.
        /// </summary>
        private static string GetDefaultCartMapper(string rom, string machine)
        {
            string ext = Path.GetExtension(rom).ToLowerInvariant();
            if (ext != ".a52" && ext != ".bin" && ext != ".rom")
                return null;

            long size;
            try { size = new FileInfo(rom).Length; }
            catch { return null; }

            if (machine == "5200")
            {
                switch (size)
                {
                    case 4096: return "20";     // 5200 4K
                    case 8192: return "19";     // 5200 8K
                    case 16384: return "6";     // 5200 16K (two chips)
                    case 32768: return "4";     // 5200 32K
                    case 40960: return "7";     // Bounty Bob (5200)
                }
            }
            else if (machine == "XEGS")
            {
                if (size <= 8192)
                    return "1";                 // 8K (2K/4K images are mirrored)

                switch (size)
                {
                    case 16384: return "2";     // 16K
                    case 32768: return "12";    // XEGS 32K
                    case 65536: return "13";    // XEGS 64K
                    case 131072: return "14";   // XEGS 128K
                    case 262144: return "23";   // XEGS 256K
                    case 524288: return "24";   // XEGS 512K
                    case 1048576: return "25";  // XEGS 1M
                }
            }

            return null;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            using (var ini = IniFile.FromFile(_config, IniOptions.UseSpaces | IniOptions.ManageKeysWithQuotes))
            {
                ini.ClearSection("User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000\\Mounted Images");

                if (_profile != null)
                {
                    string sectionToClear = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\" + _profile.ProfileSectionCode + "\\Mounted Images";
                    ini.ClearSection(sectionToClear);
                }

                if (_inputMaps != null)
                {
                    foreach (var map in _inputMaps)
                    {
                        ini.WriteValue("User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000\\Input maps", map.Key, map.Value);
                    }
                }

                foreach (var p in altirraProfiles)
                {
                    if (p.ProfileSectionCode != null)
                    {
                        string section = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\" + p.ProfileSectionCode;
                        ini.WriteValue(section, "_Category Mask", "\"hardware,firmware,inputMaps\"");
                    }
                }
            }
        }

        internal class AltirraProfile
        {
            public string Machine { get; set; }
            public string ProfileCode { get; set; }
            public string ProfileSectionCode { get; set; }
            public string Name { get; set; }
            public string InternalKernel { get; set; }
        }

        static readonly AltirraProfile[] altirraProfiles = new AltirraProfile[]
        {
            new AltirraProfile { Machine = "800", ProfileCode = "1791275519", ProfileSectionCode = "6AC4B1FF", Name = "400/800 Computer", InternalKernel = "internal:00000002" },
            new AltirraProfile { Machine = "5200", ProfileCode = "1134732972", ProfileSectionCode = "43A2A6AC", Name = "5200 Console", InternalKernel = "internal:00000006" },
            new AltirraProfile { Machine = "XEGS", ProfileCode = "670525974", ProfileSectionCode = "27F76A16", Name = "XEGS Console", InternalKernel = "internal:00000003" },
            new AltirraProfile { Machine = "1200XL", ProfileCode = "859958396", ProfileSectionCode = "3341EC7C", Name = "1200XL Computer", InternalKernel = "internal:00000003" },
            new AltirraProfile { Machine = "XL", ProfileCode = "964089481", ProfileSectionCode = "3976D689", Name = "XL/XE Computer", InternalKernel = "internal:00000003" }
        };

        static readonly Dictionary<string, string> atariProfileMap = new Dictionary<string, string>
        {
            { "atari800", "800" },
            { "atari5200", "5200" },
            { "xegs", "XEGS" }
        };
    }
}
