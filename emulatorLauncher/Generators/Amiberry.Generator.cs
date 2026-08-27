using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class AmiberryGenerator : Generator
    {
        public AmiberryGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _emulatorPath;
        private string _amiberryStatesPath;
        private string _amiberryRomsPath;
        private bool _needRomRescan;
        private string _amiberryThumbnailsPath;
        private AmigaModel _model;
        private bool _fullscreen;
        private AmiberrySaveStatesMonitor _saveStatesWatcher;
        private List<string> _diskSwapper;

        #region Model presets

        /// <summary>
        ///   Hardware presets. Memory units follow the .uae convention:
        ///   ChipMem : units of 512 KB (1 = 512K, 2 = 1M, 4 = 2M, 8 = 4M)
        ///   BogoMem : units of 256 KB (2 = 512K, 4 = 1M)
        ///   FastMem / Z3Mem / MotherboardMem : megabytes
        /// </summary>
        private class AmigaModel
        {
            public string Id;
            public string Chipset;            // ocs | ecs_agnus | ecs_denise | ecs | aga | a1000
            public string ChipsetCompatible;  // A500 | A500+ | A600 | A1000 | A1200 | A2000 | A3000 | A4000 | CD32 | CDTV
            public int CpuModel = 68000;
            public int FpuModel = 0;
            public bool CpuCompatible = true;
            public string CpuSpeed = "real";  // real | max
            public int ChipMem = 1;
            public int BogoMem = 2;
            public int FastMem = 0;
            public int Z3Mem = 0;
            public int MotherboardMem = 0;    // a3000mem_size
            public int JitCache = 0;          // cachesize, 0 = JIT off
            public bool Address24Bit = true;
            public int Floppies = 1;   // every stock Amiga shipped with one internal drive
            public bool IsCd = false;
            public bool IsCd32 = false;
            public string[] Kickstart;        // candidate file names, first match wins
            public string[] KickstartExt;     // extended rom (CD32 / CDTV)
        }

        /// <summary>
        /// Kickstart candidate file names, looked up in RetroBat's \bios.
        /// Order: Amiga Forever "shared" name, Cloanto name, TOSEC name.
        /// </summary>
        private static readonly Dictionary<string, AmigaModel> _models = new Dictionary<string, AmigaModel>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "A500", new AmigaModel {
                Id = "A500", Chipset = "ocs", ChipsetCompatible = "A500",
                CpuModel = 68000, ChipMem = 1, BogoMem = 2, Floppies = 1,
                Kickstart = new[] { "kick34005.A500", "kick34005.A500.rom", "amiga-os-130.rom",
                    "Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom",
                    "Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV).rom" } } },

            { "A500OG", new AmigaModel {
                Id = "A500OG", Chipset = "ocs", ChipsetCompatible = "A500",
                CpuModel = 68000, ChipMem = 1, BogoMem = 0, Floppies = 1,
                Kickstart = new[] { "kick33180.A500", "kick33180.A500.rom", "amiga-os-120.rom",
                    "Kickstart v1.2 rev 33.180 (1986)(Commodore)(A500-A2000)[!].rom",
                    "Kickstart v1.2 rev 33.180 (1986)(Commodore)(A500-A1000-A2000).rom" } } },

            { "A500PLUS", new AmigaModel {
                Id = "A500PLUS", Chipset = "ecs", ChipsetCompatible = "A500+",
                CpuModel = 68000, ChipMem = 2, BogoMem = 0, Floppies = 1,
                Kickstart = new[] { "kick37175.A500", "kick37175.A500.rom", "amiga-os-204.rom",
                    "Kickstart v2.04 rev 37.175 (1991)(Commodore)(A500+)[!].rom",
                    "Kickstart v2.04 rev 37.175 (1991)(Commodore)(A500+).rom" } } },

            { "A600", new AmigaModel {
                Id = "A600", Chipset = "ecs", ChipsetCompatible = "A600",
                CpuModel = 68000, ChipMem = 4, BogoMem = 0, FastMem = 8, Floppies = 1,
                Kickstart = new[] { "kick40063.A600", "kick40063.A600.rom", "amiga-os-310-a600.rom",
                    "Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000)[!].rom",
                    "Kickstart v3.1 rev 40.63 (1993)(Commodore)(A500-A600-A2000).rom" } } },

            { "A1200OG", new AmigaModel {
                Id = "A1200OG", Chipset = "aga", ChipsetCompatible = "A1200",
                CpuModel = 68020, ChipMem = 4, BogoMem = 0, FastMem = 0, Floppies = 1,
                Address24Bit = true, CpuSpeed = "real",
                Kickstart = new[] { "kick40068.A1200", "kick40068.A1200.rom", "amiga-os-310-a1200.rom",
                    "Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200)[!].rom",
                    "Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200).rom" } } },

            { "A1200", new AmigaModel {
                Id = "A1200", Chipset = "aga", ChipsetCompatible = "A1200",
                CpuModel = 68020, ChipMem = 4, BogoMem = 0, FastMem = 8, Floppies = 1,
                Address24Bit = true, CpuSpeed = "real",
                Kickstart = new[] { "kick40068.A1200", "kick40068.A1200.rom", "amiga-os-310-a1200.rom",
                    "Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200)[!].rom",
                    "Kickstart v3.1 rev 40.68 (1993)(Commodore)(A1200).rom" } } },

            { "A4030", new AmigaModel {
                Id = "A4030", Chipset = "aga", ChipsetCompatible = "A4000",
                CpuModel = 68030, FpuModel = 68882, CpuCompatible = false, CpuSpeed = "max",
                ChipMem = 4, BogoMem = 0, MotherboardMem = 8, JitCache = 16384,
                Address24Bit = false, Floppies = 1,
                Kickstart = new[] { "kick40068.A4000", "kick40068.A4000.rom", "amiga-os-310-a4000.rom",
                    "Kickstart v3.1 rev 40.68 (1993)(Commodore)(A4000).rom" } } },

            { "A4040", new AmigaModel {
                Id = "A4040", Chipset = "aga", ChipsetCompatible = "A4000",
                CpuModel = 68040, FpuModel = 68040, CpuCompatible = false, CpuSpeed = "max",
                ChipMem = 4, BogoMem = 0, MotherboardMem = 8, JitCache = 16384,
                Address24Bit = false, Floppies = 1,
                Kickstart = new[] { "kick40068.A4000", "kick40068.A4000.rom", "amiga-os-310-a4000.rom",
                    "Kickstart v3.1 rev 40.68 (1993)(Commodore)(A4000).rom" } } },

            { "CD32", new AmigaModel {
                Id = "CD32", Chipset = "aga", ChipsetCompatible = "CD32",
                CpuModel = 68020, ChipMem = 4, BogoMem = 0, Floppies = 0,
                Address24Bit = true, IsCd = true, IsCd32 = true,
                Kickstart = new[] { "kick40060.CD32", "kick40060.CD32.rom", "amiga-os-310-cd32.rom",
                    "Kickstart v3.1 rev 40.60 (1993)(Commodore)(CD32).rom" },
                KickstartExt = new[] { "kick40060.CD32.ext", "kick40060.CD32.ext.rom", "amiga-os-310-cd32-ext.rom",
                    "CD32 Extended-ROM rev 40.60 (1993)(Commodore)(CD32).rom" } } },

            { "CD32FR", new AmigaModel {
                Id = "CD32FR", Chipset = "aga", ChipsetCompatible = "CD32",
                CpuModel = 68020, ChipMem = 4, BogoMem = 0, FastMem = 8, Floppies = 0,
                Address24Bit = true, IsCd = true, IsCd32 = true,
                Kickstart = new[] { "kick40060.CD32", "kick40060.CD32.rom", "amiga-os-310-cd32.rom",
                    "Kickstart v3.1 rev 40.60 (1993)(Commodore)(CD32).rom" },
                KickstartExt = new[] { "kick40060.CD32.ext", "kick40060.CD32.ext.rom", "amiga-os-310-cd32-ext.rom",
                    "CD32 Extended-ROM rev 40.60 (1993)(Commodore)(CD32).rom" } } },

            { "CDTV", new AmigaModel {
                Id = "CDTV", Chipset = "ecs_agnus", ChipsetCompatible = "CDTV",
                CpuModel = 68000, ChipMem = 2, BogoMem = 0, Floppies = 0, IsCd = true,
                Kickstart = new[] { "kick34005.A500", "kick34005.A500.rom", "amiga-os-130.rom",
                    "Kickstart v1.3 rev 34.5 (1987)(Commodore)(A500-A1000-A2000-CDTV)[!].rom" },
                KickstartExt = new[] { "kick34005.CDTV", "kick34005.CDTV.rom", "amiga-os-130-cdtv-ext.rom",
                    "CDTV Extended-ROM v1.0 (1991)(Commodore)(CDTV)[!].rom",
                    "CDTV Extended-ROM v1.0 (1992)(Commodore)(CDTV).rom" } } },
        };

        private static readonly Dictionary<string, string> _systemDefaultModel = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "amiga500",  "A500"   },
            { "amiga600",  "A600"   },
            { "amiga1200", "A1200"  },
            { "amiga4000", "A4040"  },
            { "amigacd32", "CD32"   },
            { "amigacdtv", "CDTV"   },
        };

        #endregion

        #region File type helpers

        private static readonly string[] _floppyExtensions = { ".adf", ".adz", ".dms", ".ipf", ".fdi", ".raw", ".st" };
        private static readonly string[] _cdExtensions = { ".cue", ".iso", ".chd", ".ccd", ".mds", ".nrg" };
        private static readonly string[] _hdfExtensions = { ".hdf", ".vhd", ".hdz" };

        private static bool HasExtension(string rom, string[] extensions)
        {
            string ext = Path.GetExtension(rom).ToLowerInvariant();
            return extensions.Contains(ext);
        }

        #endregion

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            rom = PathHelper.ResolvePath(rom);

            string path = AppConfig.GetFullPath("amiberry");
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "Amiberry.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "amiberry.exe");
            if (!File.Exists(exe))
            {
                SimpleLogger.Instance.Error("[Generator] Amiberry executable not found in " + path);
                return null;
            }

            _emulatorPath = path;
            _resolution = resolution;
            _fullscreen = ShouldRunFullscreen();

            if (_fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            EnsurePortableMode(path);

            // Point Amiberry's content folders at RetroBat's
            SetupAmiberrySettings(path, system);

            // Define the hardware model.
            _model = ResolveModel(system, rom);
            if (_model == null)
            {
                SimpleLogger.Instance.Error("[Generator] Unknown Amiga model for system " + system);
                return null;
            }

            // Save states, slots 1 to 9. Amiberry writes a real PNG thumbnail beside
            // each .uss, so every slot gets a proper screenshot.
            if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);

                _saveStatesWatcher = new AmiberrySaveStatesMonitor(rom, _amiberryStatesPath, localPath, _amiberryThumbnailsPath, Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png"));

                _saveStatesWatcher.PrepareEmulatorRepository();
            }
            else
                _saveStatesWatcher = null;

            var commandArray = new List<string>();

            // Prioritize user supplied .uae config
            if (Path.GetExtension(rom).ToLowerInvariant() == ".uae")
            {
                commandArray.Add("--config");
                commandArray.Add("\"" + rom + "\"");
            }
            else
            {
                string uaeConfig = Path.Combine(path, "Configurations", "retrobat.uae");
                WriteUaeConfig(uaeConfig, system, rom);

                commandArray.Add("--config");
                commandArray.Add("\"" + uaeConfig + "\"");

                if (Path.GetExtension(rom).ToLowerInvariant() == ".lha")
                {
                    commandArray.Add("--autoload");
                    commandArray.Add("\"" + rom + "\"");
                }
            }

            if (_saveStatesWatcher != null && SystemConfig.isOptSet("state_file") && !string.IsNullOrEmpty(SystemConfig["state_file"]))
            {
                string physical = GetPhysicalStateFile(rom);
                if (physical != null)
                {
                    commandArray.Add("--statefile");
                    commandArray.Add("\"" + physical + "\"");
                }
            }

            if (_needRomRescan)
                commandArray.Add("--rescan-roms");

            // -G : skip the GUI
            commandArray.Add("-G");

            if (SystemConfig.getOptBoolean("amiberry_log"))
                commandArray.Add("--log");

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(" ", commandArray),
                WorkingDirectory = path,
            };
        }

        #region Portable mode & amiberry.conf

        /// <summary>
        /// Creates the "amiberry.portable" file next to the executable.
        /// </summary>
        private static void EnsurePortableMode(string path)
        {
            string portableFile = Path.Combine(path, "amiberry.portable");
            if (File.Exists(portableFile))
                return;

            try
            {
                File.WriteAllText(portableFile, string.Empty);
                SimpleLogger.Instance.Info("[Amiberry] Created portable mode marker: " + portableFile);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Amiberry] Could not create the portable marker (" + ex.Message + "). Amiberry will write its data to %LOCALAPPDATA%\\Amiberry and %USERPROFILE%\\Amiberry.");
            }
        }

        private void SetupAmiberrySettings(string path, string system)
        {
            string settingsDir = Path.Combine(path, "Settings");
            string confFile = Path.Combine(settingsDir, "amiberry.conf");

            string savesPath = Path.Combine(PathHelper.ResolvePath(AppConfig.GetFullPath("saves")), system, "amiberry");
            _amiberryStatesPath = savesPath;
            _amiberryThumbnailsPath = Path.Combine(PathHelper.ResolvePath(AppConfig.GetFullPath("screenshots")), "amiberry");
            FileTools.TryCreateDirectory(_amiberryThumbnailsPath);

            var overrides = new Dictionary<string, string>();

            _amiberryRomsPath = Path.Combine(path, "ROMs");
            FileTools.TryCreateDirectory(_amiberryRomsPath);
            overrides["rom_path"] = _amiberryRomsPath;

            FileTools.TryCreateDirectory(savesPath);
            if (Directory.Exists(savesPath))
            {
                overrides["savestate_dir"] = savesPath;
                overrides["nvram_dir"] = savesPath;
                overrides["saveimage_dir"] = savesPath;
            }

            if (Directory.Exists(_amiberryThumbnailsPath))
                overrides["screenshot_dir"] = _amiberryThumbnailsPath;

            overrides["config_path"] = Path.Combine(path, "Configurations");
            overrides["controllers_path"] = Path.Combine(path, "Controllers");
            overrides["whdboot_path"] = Path.Combine(path, "WHDBoot");

            overrides["Quickstart"] = "0";        // never open the QuickStart panel
            overrides["default_fullscreen_mode"] = _fullscreen ? "2" : "0";
            overrides["write_logfile"] = SystemConfig.getOptBoolean("amiberry_log") ? "yes" : "no";
            overrides["update_check"] = "0";

            overrides["default_open_gui_key"] = SystemConfig.isOptSet("amiberry_guikey") ? SystemConfig["amiberry_guikey"] : "F12";
            overrides["default_fullscreen_toggle_key"] = SystemConfig.isOptSet("amiberry_fskey") ? SystemConfig["amiberry_fskey"] : "LAlt+Return";
            overrides["default_quit_key"] = "";

            foreach (var dir in new[] { overrides["config_path"], overrides["controllers_path"], overrides["whdboot_path"] })
                FileTools.TryCreateDirectory(dir);

            FileTools.TryCreateDirectory(settingsDir);
            MergeConfLines(confFile, overrides);
        }

        /// <summary>
        /// We rewrite only the keys send by RetroBat and append the missing ones.
        /// </summary>
        private static void MergeConfLines(string file, Dictionary<string, string> overrides)
        {
            try
            {
                var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string>();

                var written = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    int sep = line.IndexOf('=');
                    if (sep <= 0)
                        continue;

                    string key = line.Substring(0, sep).Trim();
                    if (!overrides.ContainsKey(key) || written.Contains(key))
                        continue;

                    lines[i] = key + "=" + overrides[key];
                    written.Add(key);
                }

                foreach (var kv in overrides)
                {
                    if (!written.Contains(kv.Key))
                        lines.Add(kv.Key + "=" + kv.Value);
                }

                File.WriteAllLines(file, lines);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Amiberry] Could not update amiberry.conf: " + ex.Message);
            }
        }

        /// <summary>
        /// Amiberry names its states after the inserted media, which is the rom itself
        /// except for a .m3u playlist: there DF0 holds disk 1, so the state is named
        /// after that disk.
        /// </summary>
        private string GetPhysicalStateFile(string rom)
        {
            string requested = SystemConfig["state_file"];
            if (string.IsNullOrEmpty(requested))
                return null;

            // The slot is the trailing "-N" of the RetroBat-side file name.
            var match = Regex.Match(Path.GetFileNameWithoutExtension(requested), @"-([0-9]{1,2})$");
            if (!match.Success)
                return null;

            string suffix = "-" + match.Groups[1].Value;

            string emulatorBase = Path.GetFileNameWithoutExtension(rom);
            string txt = Path.Combine(_saveStatesWatcher.SaveStatesPath, emulatorBase + ".txt");

            if (File.Exists(txt))
            {
                string recorded = File.ReadAllText(txt).Trim();
                if (!string.IsNullOrEmpty(recorded))
                    emulatorBase = recorded;
            }

            string candidate = Path.Combine(_amiberryStatesPath, emulatorBase + suffix + ".uss");
            if (File.Exists(candidate))
                return candidate;

            SimpleLogger.Instance.Warning("[Amiberry] Save state not found in the emulator folder: " + candidate);
            return null;
        }

        #endregion

        #region Model resolution

        private AmigaModel ResolveModel(string system, string rom)
        {
            string modelId = null;

            if (SystemConfig.isOptSet("amiberry_model") && !string.IsNullOrEmpty(SystemConfig["amiberry_model"]))
                modelId = SystemConfig["amiberry_model"];

            if (string.IsNullOrEmpty(modelId) || !_models.ContainsKey(modelId))
            {
                if (!_systemDefaultModel.TryGetValue(system, out modelId))
                    modelId = "A500";
            }

            var model = _models[modelId];

            // A CD image on a floppy-only model would never boot. Fall back to a machine
            // that can actually read it rather than failing silently.
            if (HasExtension(rom, _cdExtensions) && !model.IsCd)
            {
                SimpleLogger.Instance.Warning("[Amiberry] CD image on a non-CD model (" + modelId + "), switching to CD32.");
                model = _models["CD32"];
            }

            return model;
        }

        #endregion

        #region .uae config generation

        private void WriteUaeConfig(string file, string system, string rom)
        {
            FileTools.TryCreateDirectory(Path.GetDirectoryName(file));

            // The .uae format is an ordered flat list, and order matters
            var cfg = new List<string>();

            Action<string, string> Set = (key, value) =>
            {
                if (value != null)
                    cfg.Add(key + "=" + value);
            };

            Set("config_description", "RetroBat");
            Set("config_hardware", "true");
            Set("config_host", "true");
            Set("use_gui", "no");

            // Chipset / CPU
            Set("chipset_compatible", _model.ChipsetCompatible);
            Set("chipset", _model.Chipset);

            string cpuModel = _model.CpuModel.ToString(CultureInfo.InvariantCulture);
            if (SystemConfig.isOptSet("amiberry_cpu") && !string.IsNullOrEmpty(SystemConfig["amiberry_cpu"]))
                cpuModel = SystemConfig["amiberry_cpu"];
            Set("cpu_model", cpuModel);

            int fpu = _model.FpuModel;
            if (SystemConfig.isOptSet("amiberry_fpu") && !string.IsNullOrEmpty(SystemConfig["amiberry_fpu"]))
                fpu = SystemConfig["amiberry_fpu"].ToInteger();
            if (fpu > 0)
                Set("fpu_model", fpu.ToString(CultureInfo.InvariantCulture));

            Set("cpu_24bit_addressing", _model.Address24Bit ? "true" : "false");

            string accuracy = SystemConfig.isOptSet("amiberry_accuracy") ? SystemConfig["amiberry_accuracy"] : "compatible";
            switch (accuracy)
            {
                case "cycleexact":
                    Set("cpu_compatible", "true");
                    Set("cpu_cycle_exact", "true");
                    Set("cpu_memory_cycle_exact", "true");
                    Set("blitter_cycle_exact", "true");
                    Set("cpu_speed", "real");
                    break;
                case "fast":
                    Set("cpu_compatible", "false");
                    Set("cpu_cycle_exact", "false");
                    Set("blitter_cycle_exact", "false");
                    Set("cpu_speed", "max");
                    break;
                default: // most compatible
                    Set("cpu_compatible", _model.CpuCompatible ? "true" : "false");
                    Set("cpu_cycle_exact", "false");
                    Set("blitter_cycle_exact", "false");
                    Set("cpu_speed", _model.CpuSpeed);
                    break;
            }

            // JIT is incompatible with cycle exact, and Amiberry ignores it on 68000.
            int jit = _model.JitCache;
            if (SystemConfig.isOptSet("amiberry_jit"))
                jit = SystemConfig.getOptBoolean("amiberry_jit") ? 16384 : 0;
            if (accuracy == "cycleexact")
                jit = 0;
            Set("cachesize", jit.ToString(CultureInfo.InvariantCulture));

            // Memory
            Set("chipmem_size", GetMemoryOption("amiberry_chipmem", _model.ChipMem));
            Set("bogomem_size", GetMemoryOption("amiberry_bogomem", _model.BogoMem));
            Set("fastmem_size", GetMemoryOption("amiberry_fastmem", _model.FastMem));
            if (_model.Z3Mem > 0 || SystemConfig.isOptSet("amiberry_z3mem"))
                Set("z3mem_size", GetMemoryOption("amiberry_z3mem", _model.Z3Mem));
            if (_model.MotherboardMem > 0)
                Set("a3000mem_size", _model.MotherboardMem.ToString(CultureInfo.InvariantCulture));

            StageRomKey();

            string kickstart = StageKickstart(_model.Kickstart);
            if (kickstart != null)
                Set("kickstart_rom_file", kickstart);
            else
                SimpleLogger.Instance.Warning("[Amiberry] No Kickstart ROM found for " + _model.Id +
                    ". Expected one of: " + string.Join(", ", _model.Kickstart) + " in \\bios.");

            if (_model.KickstartExt != null)
            {
                string kickstartExt = StageKickstart(_model.KickstartExt);
                if (kickstartExt != null)
                    Set("kickstart_ext_rom_file", kickstartExt);
                else
                    SimpleLogger.Instance.Warning("[Amiberry] No extended ROM found for " + _model.Id +
                        ". Expected one of: " + string.Join(", ", _model.KickstartExt) + " in \\bios.");
            }

            if (_saveStatesWatcher != null)
            {
                string quickState = _saveStatesWatcher.GetQuickStateFilePath();
                if (quickState != null)
                {
                    Set("statefile_path", quickState);
                    SimpleLogger.Instance.Info("[Amiberry] Quick save/load target: " + quickState);
                }
            }

            // Media
            ConfigureMedia(Set, rom);

            // Video
            ConfigureVideo(Set);

            // Audio
            Set("sound_output", SystemConfig.isOptSet("amiberry_sound") ? SystemConfig["amiberry_sound"] : "exact");
            Set("sound_frequency", SystemConfig.isOptSet("amiberry_sound_freq") ? SystemConfig["amiberry_sound_freq"] : "44100");

            // 7 rather than the hardware's 10 for the default
            Set("sound_stereo_separation", SystemConfig.isOptSet("amiberry_stereo_separation") ? SystemConfig["amiberry_stereo_separation"] : "7");

            if (SystemConfig.isOptSet("amiberry_floppy_sound") && !string.IsNullOrEmpty(SystemConfig["amiberry_floppy_sound"]))
                Set("floppy_volume", SystemConfig["amiberry_floppy_sound"]);

            // WHDLoad
            if (Path.GetExtension(rom).ToLowerInvariant() == ".lha")
            {
                // Amiberry's "press fire before the game starts" prompt.
                // Off by default so the game boots straight away.
                Set("whdload_buttonwait", SystemConfig.getOptBoolean("amiberry_whd_buttonwait") ? "true" : "false");
                Set("whdload_showsplash", SystemConfig.getOptBoolean("amiberry_whd_splash") ? "true" : "false");
                Set("whdload_quit_on_exit", "true");
            }

            // Hotkeys and Amiberry specific keys
            // These are prefixed with the target name (TARGET_NAME = "amiberry").
            Set("amiberry.open_gui", SystemConfig.isOptSet("amiberry_guikey") ? SystemConfig["amiberry_guikey"] : "F12");
            Set("amiberry.quit_amiberry", "");
            Set("amiberry.alt_tab_release", "true");

            // Controls
            ConfigureControls(Set, system);


            // Light guns
            ConfigureGuns(Set);

            try
            {
                File.WriteAllLines(file, cfg);
                SimpleLogger.Instance.Info("[Amiberry] Wrote configuration: " + file);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Amiberry] Could not write " + file + ": " + ex.Message);
            }
        }

        private string GetMemoryOption(string feature, int defaultValue)
        {
            if (SystemConfig.isOptSet(feature) && !string.IsNullOrEmpty(SystemConfig[feature]))
                return SystemConfig[feature];
            return defaultValue.ToString(CultureInfo.InvariantCulture);
        }

        private void StageRomKey()
        {
            string source = FindBios(new[] { "rom.key" });
            if (source == null)
                return;

            string dest = Path.Combine(_amiberryRomsPath, "rom.key");

            try
            {
                if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(source).Length)
                {
                    File.Copy(source, dest, true);
                    _needRomRescan = true;
                    SimpleLogger.Instance.Info("[Amiberry] Staged rom.key.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Amiberry] Could not stage rom.key: " + ex.Message);
            }
        }

        private string StageKickstart(string[] candidates)
        {
            string source = FindBios(candidates);
            if (source == null)
                return null;

            FileTools.TryCreateDirectory(_amiberryRomsPath);
            string dest = Path.Combine(_amiberryRomsPath, candidates[0]);

            try
            {
                var src = new FileInfo(source);
                var dst = new FileInfo(dest);

                if (!dst.Exists || dst.Length != src.Length || dst.LastWriteTimeUtc < src.LastWriteTimeUtc)
                {
                    File.Copy(source, dest, true);
                    _needRomRescan = true;
                    SimpleLogger.Instance.Info("[Amiberry] Staged Kickstart: " + source + " -> " + dest);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Amiberry] Could not stage " + source + ": " + ex.Message);
                return File.Exists(dest) ? dest : null;
            }

            return dest;
        }

        /// <summary>
        /// Returns the first existing candidate in \bios (and \bios\amiga).
        /// Amiga Forever, Cloanto and TOSEC
        /// names are all accepted.
        /// </summary>
        private string FindBios(string[] candidates)
        {
            if (candidates == null)
                return null;

            string biosPath = AppConfig.GetFullPath("bios");
            if (string.IsNullOrEmpty(biosPath))
                return null;

            var searchDirs = new List<string> { biosPath, Path.Combine(biosPath, "amiga"), Path.Combine(biosPath, "Amiga") };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                foreach (var candidate in candidates)
                {
                    string full = Path.Combine(dir, candidate);
                    if (File.Exists(full))
                        return full;
                }
            }

            return null;
        }

        #endregion

        #region Media

        private void ConfigureMedia(Action<string, string> Set, string rom)
        {
            string ext = Path.GetExtension(rom).ToLowerInvariant();

            // CD based systems
            if (HasExtension(rom, _cdExtensions))
            {
                Set("cdimage0", rom + ",image");
                Set("nr_floppies", "0");
                Set("floppy0type", "-1");
                Set("floppy1type", "-1");
                return;
            }

            // Hard disk file
            if (HasExtension(rom, _hdfExtensions))
            {
                Set("nr_floppies", Math.Max(1, _model.Floppies).ToString(CultureInfo.InvariantCulture));
                Set("floppy0type", "0");
                Set("hardfile2", BuildHardfileEntry(rom));
                return;
            }

            // Directory mounted as an Amiga volume
            if (Directory.Exists(rom))
            {
                string volume = Path.GetFileNameWithoutExtension(rom.TrimEnd(Path.DirectorySeparatorChar));
                Set("nr_floppies", Math.Max(1, _model.Floppies).ToString(CultureInfo.InvariantCulture));
                Set("floppy0type", "0");
                Set("filesystem2", "rw,DH0:" + volume + ":" + rom + ",0");
                return;
            }

            // WHDLoad archive: the auto booter builds the whole hard drive setup itself.
            if (ext == ".lha")
            {
                Set("nr_floppies", "0");
                return;
            }

            // Floppies, either a single file or a .m3u playlist
            var disks = new List<string>();

            if (ext == ".m3u")
            {
                var m3u = MultiDiskImageFile.FromFile(rom);
                if (m3u.Files.Length == 0)
                    throw new ApplicationException("m3u file does not contain any game file.");
                disks.AddRange(m3u.Files.Where(File.Exists));
            }
            else
            {
                disks.Add(rom);
            }

            int drives = _model.Floppies > 0 ? _model.Floppies : 1;
            if (SystemConfig.isOptSet("amiberry_drives") && !string.IsNullOrEmpty(SystemConfig["amiberry_drives"]))
                drives = SystemConfig["amiberry_drives"].ToInteger();
            drives = Math.Max(1, Math.Min(4, drives));

            Set("nr_floppies", drives.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < 4; i++)
                Set("floppy" + i + "type", i < drives ? "0" : "-1");

            for (int i = 0; i < Math.Min(disks.Count, drives); i++)
                Set("floppy" + i, disks[i]);

            // Anything beyond the available drives goes to the disk swapper, which can
            // only be expressed on the command line (see RunAndWait).
            _diskSwapper = disks.Count > 1 ? disks : null;

            Set("floppy_speed", SystemConfig.isOptSet("amiberry_floppy_speed") ? SystemConfig["amiberry_floppy_speed"] : "100");
        }

        private static string BuildHardfileEntry(string hdf)
        {
            string geometry = IsRdbHardfile(hdf) ? "0,0,0,512" : "32,1,2,512";
            return "rw,DH0:" + hdf + "," + geometry + ",0,,uae0";
        }

        private static bool IsRdbHardfile(string hdf)
        {
            try
            {
                using (var fs = new FileStream(hdf, FileMode.Open, FileAccess.Read))
                {
                    // The RDB lives in one of the first 16 blocks of 512 bytes.
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

        #endregion

        #region Video

        private void ConfigureVideo(Action<string, string> Set)
        {
            string fsMode = _fullscreen ? "fullwindow" : "false";
            Set("gfx_fullscreen_amiga", fsMode);
            Set("gfx_fullscreen_picasso", fsMode);

            if (!_fullscreen && _resolution != null)
            {
                Set("gfx_width", _resolution.Width.ToString(CultureInfo.InvariantCulture));
                Set("gfx_height", _resolution.Height.ToString(CultureInfo.InvariantCulture));
            }

            Set("gfx_vsync", SystemConfig.getOptBoolean("amiberry_vsync") ? "true" : "false");
            Set("gfx_resolution", SystemConfig.isOptSet("amiberry_resolution") ? SystemConfig["amiberry_resolution"] : "hires");
            Set("gfx_linemode", SystemConfig.isOptSet("amiberry_linemode") ? SystemConfig["amiberry_linemode"] : "double");

            // When MonitorIndex is not configured we leave it alone entirely.
            if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
            {
                int monitorIndex = SystemConfig["MonitorIndex"].ToInteger();
                Set("gfx_display", (monitorIndex + 1).ToString(CultureInfo.InvariantCulture));
            }

            // scaling_method: -1 auto, 0 nearest, 1 linear, 2 integer, 3 stretch
            Set("amiberry.scaling_method", SystemConfig.isOptSet("amiberry_scaling") ? SystemConfig["amiberry_scaling"] : "-1");
            Set("amiberry.gfx_auto_crop", SystemConfig.getOptBoolean("amiberry_autocrop") ? "true" : "false");
            Set("amiberry.gfx_correct_aspect", SystemConfig.isOptSet("amiberry_aspect") ? SystemConfig["amiberry_aspect"] : "1");

            // Bezels are drawn by RetroBat (FakeBezel), so Amiberry's own must stay off.
            Set("amiberry.use_bezel", "false");
            Set("amiberry.use_custom_bezel", "false");
        }

        #endregion

        public override int RunAndWait(ProcessStartInfo path)
        {
            // The disk swapper cannot be expressed in the .uae file, only on the command
            // line, so it is appended here rather than in Generate().
            if (_diskSwapper != null && _diskSwapper.Count > 1)
            {
                var quoted = _diskSwapper.Select(d => d.Contains(",") ? "\"" + d + "\"" : d);
                path.Arguments += " -diskswapper=" + string.Join(",", quoted);
            }

            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            CleanupGuns();

            if (_saveStatesWatcher != null)
            {
                // Order matters: rescue first (it may create a new slot), prune second.
                _saveStatesWatcher.RescueDefaultSlot();
                _saveStatesWatcher.PruneOrphanThumbnails();
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            return ret;
        }
    }
}
