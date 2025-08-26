﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common.Compression.Wrappers;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher.Libretro
{
    // -system nes -emulator libretro -core fceumm -rom "H:\[Emulz]\roms\nes\Arkanoid.nes" -state_slot 1 -autosave 1
    // -system nes -emulator libretro -core fceumm -rom "H:\[Emulz]\roms\nes\Arkanoid.nes" -state_slot 1 -state_file "H:/[Emulz]/saves/nes/Arkanoid.state"

    partial class LibRetroGenerator : Generator
    {
        public string RetroarchPath { get; set; }
        public string RetroarchCorePath { get; set; }
        public string CurrentHomeDirectory { get; set; }
        public LibRetroGenerator()
        {
            RetroarchPath = AppConfig.GetFullPath("retroarch");

            RetroarchCorePath = AppConfig.GetFullPath("retroarch.cores");
            if (string.IsNullOrEmpty(RetroarchCorePath))
                RetroarchCorePath = Path.Combine(RetroarchPath, "cores");
        }

        const string RetroArchNetPlayPatchedName = "RETROBAT";
        private LibRetroStateFileManager _stateFileManager;
        private ScreenShotsWatcher _screenShotWatcher;
        private bool _noHotkey = false;
        private string _video_driver;
        private string _dosBoxTempRom;
        private bool _bias = true;

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (string.IsNullOrEmpty(RetroarchPath))
                return null;

            string subCore = null;
            string romName = Path.GetFileNameWithoutExtension(rom);

            if (!string.IsNullOrEmpty(core))
            {
                int split = core.IndexOfAny(new char[] { ':', '/' });
                if (split >= 0)
                {
                    subCore = core.Substring(split + 1);
                    core = core.Substring(0, split);

                    SystemConfig["subcore"] = subCore;
                }
            }

            // Detect best core for MAME games ( If not overridden by the user )
            if (GetBestMameCore(system, subCore, core, emulator, rom, out string newCore))
                core = newCore;

            // specific management for some extensions
            if (Path.GetExtension(rom).ToLowerInvariant() == ".game")
                core = Path.GetFileNameWithoutExtension(rom);
            else if (Path.GetExtension(rom).ToLowerInvariant() == ".libretro")
            {
                core = Path.GetFileNameWithoutExtension(rom);

                if (core == "xrick")
                    rom = Path.Combine(Path.GetDirectoryName(rom), "xrick", "data.zip");
                else if (core == "dinothawr")
                    rom = Path.Combine(Path.GetDirectoryName(rom), "dinothawr", "dinothawr.game");
                else
                    rom = null;
            }
            else if (Path.GetExtension(rom).ToLowerInvariant() == ".croft")
            {
                string[] croftSubFile = File.ReadAllLines(rom);
                string croftSubPath = croftSubFile[0];
                rom = Path.Combine(Path.GetDirectoryName(rom), croftSubPath);
            }
            else if (core == "boom3")
            {
                if (Path.GetExtension(rom).ToLowerInvariant() == ".boom3" || Path.GetExtension(rom).ToLowerInvariant() == ".game")
                {
                    string[] pakFile = File.ReadAllLines(rom);
                    string pakSubPath = pakFile[0];
                    if (pakSubPath.StartsWith("d3xp"))
                        core = "boom3_xp";
                    rom = Path.Combine(Path.GetDirectoryName(rom), pakSubPath);
                }
            }
            else if (core == "vitaquake2")
            {
                string pakPath = Path.GetDirectoryName(rom);
                int index = pakPath.IndexOf("vitaquake2");
                if (index != -1)
                {
                    string endOfPakPath = pakPath.Substring(index + 10);
                    if (endOfPakPath.StartsWith("\\"))
                        endOfPakPath = endOfPakPath.Substring(1);

                    if (endOfPakPath.Contains("rogue"))
                        core = "vitaquake2-rogue";
                    else if (endOfPakPath.Contains("xatrix"))
                        core = "vitaquake2-xatrix";
                    else if (endOfPakPath.Contains("zaero"))
                        core = "vitaquake2-zaero";
                }
            }
            else if (core == "geolith" && Path.GetExtension(rom).ToLower() == ".zip")
            {
                using (var zip = Zip.Open(rom))
                {
                    var entries = zip.Entries.ToList();

                    bool neoFileExists = entries.Any(z => z.Filename.EndsWith(".neo", StringComparison.OrdinalIgnoreCase));

                    if (!neoFileExists)
                        throw new ApplicationException("[ERROR] Geolith core requires a .neo file in the zip archive.");
                }
            }

            // Exit if no core is provided
            if (string.IsNullOrEmpty(core))
            {
                ExitCode = ExitCodes.MissingCore;
                SimpleLogger.Instance.Error("[LibretroGenerator] Core was not provided");
                return null;
            }
            else
            {
                bool updatesEnabled = !SystemConfig.isOptSet("updates.enabled") || SystemConfig.getOptBoolean("updates.enabled");
                string corePath = Path.Combine(RetroarchCorePath, core + "_libretro.dll");
                if (File.Exists(corePath) && updatesEnabled)
                {
                    SimpleLogger.Instance.Info("[INFO] Checking core update availability");
                    var date = File.GetLastWriteTime(corePath).ToUniversalTime().ToString("0.yy.MM.dd");
                    var versionInfo = FileVersionInfo.GetVersionInfo(corePath);
                    string version = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;

                    if (Installer.CoreHasUpdateAvailable(core, date, version, out string ServerVersion))
                    {
                        SimpleLogger.Instance.Info("[INFO] Core update available, downloading");

                        try
                        {

                            string url = Installer.GetUpdateUrl("cores/" + core + "_libretro.dll.zip");
                            if (!WebTools.UrlExists(url))
                            {
                                // Automatic install of missing core
                                var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"));

                                url = retroarchConfig["core_updater_buildbot_cores_url"];
                                if (!string.IsNullOrEmpty(url))
                                    url += core + "_libretro.dll.zip";
                            }

                            if (WebTools.UrlExists(url))
                            {
                                using (var frm = new InstallerFrm(core, url, RetroarchCorePath))
                                {
                                    frm.SetLabel(string.Format(Properties.Resources.UpdateAvailable, "libretro-" + core, ServerVersion, date));
                                    frm.ShowDialog();
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (!File.Exists(corePath))
                {
                    try
                    {

                        string url = Installer.GetUpdateUrl("cores/" + core + "_libretro.dll.zip");
                        if (!WebTools.UrlExists(url))
                        {
                            // Automatic install of missing core
                            var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"));

                            url = retroarchConfig["core_updater_buildbot_cores_url"];
                            if (!string.IsNullOrEmpty(url))
                                url += core + "_libretro.dll.zip";
                        }

                        if (WebTools.UrlExists(url))
                        {
                            using (var frm = new InstallerFrm(core, url, RetroarchCorePath))
                                frm.ShowDialog();
                        }
                    }
                    catch { }

                    if (!File.Exists(corePath))
                    {
                        SimpleLogger.Instance.Error("[LibretroGenerator] Core is not installed");
                        ExitCode = ExitCodes.MissingCore;
                        return null;
                    }
                }
            }

            // For j2me, check if java is installed
            if (core != null && core == "freej2me")
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = "-version",
                        RedirectStandardError = true, // Java outputs version info to stderr
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process process = Process.Start(psi))
                    {
                        using (System.IO.StreamReader reader = process.StandardError)
                        {
                            string output = reader.ReadToEnd();

                            if (!string.IsNullOrEmpty(output))
                            {
                                string version = "nul";
                                Match match = Regex.Match(output, @"\b(\d+\.\d+\.\d+(_\d+)?)\b");

                                if (match != null)
                                    version = match.Value;
                                
                                SimpleLogger.Instance.Info("[INFO] Java is installed, version is: " + version);
                            }
                            else
                            {
                                throw new ApplicationException("[ERROR] Java is not installed.");
                            }
                        }
                    }
                }
                catch { }
            }

            // Extension used by hypseus .daphne but lr-daphne starts with .zip
            if (system == "daphne" || core == "daphne")
            {
                string datadir = Path.GetDirectoryName(rom);

                //romName = os.path.splitext(os.path.basename(rom))[0]
                rom = Path.GetFullPath(datadir + "/roms/" + romName + ".zip");
            }

            // unzip 7z for some cores
            if (rom != null && core!=null && Path.GetExtension(rom).ToLower() == ".7z")
            {
                string newRom = GetUnzippedRomForSystem(rom, core, system);

                if (newRom != null)
                    rom = newRom;
            }

            // m3u management in some cases
            if (core == "mednafen_pce" || core == "mednafen_pce_fast")
            {
                if (Path.GetExtension(rom).ToLower() == ".m3u")
                {
                    string tempRom = File.ReadLines(rom).FirstOrDefault();
                    if (File.Exists(tempRom))
                        rom = tempRom;
                    else
                        rom = Path.Combine(Path.GetDirectoryName(rom), tempRom);
                }
            }

            // dosbox core specifics
            if (core != null && core.IndexOf("dosbox", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                string bat = Path.Combine(rom, "dosbox.bat");
                if (File.Exists(bat))
                    rom = bat;
                else
                {
                    string ext = Path.GetExtension(rom).ToLower();
                    if ((ext == ".dosbox" || ext == ".dos" || ext == ".pc") && File.Exists(rom))
                    {
                        string tempRom = Path.Combine(Path.GetDirectoryName(rom), "dosbox.conf");
                        if (File.Exists(tempRom) && !new FileInfo(tempRom).Attributes.HasFlag(FileAttributes.Hidden))
                            rom = tempRom;
                        else
                        {
                            try
                            {
                                if (File.Exists(tempRom))
                                    File.Delete(tempRom);
                            }
                            catch { }

                            try
                            {
                                File.Copy(rom, tempRom);
                                new FileInfo(tempRom).Attributes |= FileAttributes.Hidden;
                                rom = tempRom;
                                _dosBoxTempRom = tempRom;
                            }
                            catch { }
                        }
                    }
                }
            }

            // When using .ipf extension, ensure capsimg.dll is present in RetroArch folder
            if (core != null && rom != null && capsimgCore.Contains(core) && Path.GetExtension(rom).ToLowerInvariant() == ".ipf")
            {
                string sourceDll = Path.Combine(AppConfig.GetFullPath("bios"), "capsimg.dll");
                string targetDll = Path.Combine(AppConfig.GetFullPath("retroarch"), "capsimg.dll");
                if (!File.Exists(targetDll) && File.Exists(sourceDll))
                {
                    try { File.Copy(sourceDll, targetDll); }
                    catch { }
                }
            }

            Configure(system, core, rom, resolution);

            List<string> commandArray = new List<string>();

            string subSystem = SubSystem.GetSubSystem(core, system);
            if (!string.IsNullOrEmpty(subSystem))
            {
                commandArray.Add("--subsystem");
                commandArray.Add(subSystem);
            }

            // Add subsystem for sameboy core multiplayer
            bool multiplayer = (system == "gb2players" || system == "gbc2players");
            if (core == "sameboy" && multiplayer)
            {
                // Case for different game cartridges (like Pokemon) - usage of m3u
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
                            throw new ApplicationException("File '" + Path.Combine(dskPath, line) + "' does not exist");
                    }

                    if (disks.Count == 0)       // Empty m3u
                        return null;

                    else if (disks.Count == 1)  // Only 1 game in m3u file, just use this file as rom
                        rom = disks[0];

                    else
                    {
                        rom = disks[0];
                        commandArray.Add("--subsystem");
                        commandArray.Add("gb_link_2p");
                        commandArray.Add("\"" + disks[1] + "\"");
                    }
                }
                // Case for same game cartridge
                else
                {
                    commandArray.Add("--subsystem");
                    commandArray.Add("gb_link_2p");
                    commandArray.Add("\"" + rom + "\"");
                }
            }

            // Netplay mode
            if (!string.IsNullOrEmpty(SystemConfig["netplaymode"]))
            {
                // Netplay mode
                if (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator")
                    commandArray.Add("--host");
                else if (SystemConfig["netplaymode"] == "client" || SystemConfig["netplaymode"] == "spectator")
                {
                    commandArray.Add("--connect " + SystemConfig["netplayip"]);
                    commandArray.Add("--port " + SystemConfig["netplayport"]);
                }

                if (!string.IsNullOrEmpty(SystemConfig["netplaysession"]))
                {
                    // Suported with retroarch 1.17+ only
                    if (IsVersionAtLeast(new Version(1, 17, 0, 0)))
                        commandArray.Add("--mitm-session " + SystemConfig["netplaysession"]);
                }
            }

            // RetroArch 1.7.8 requires the shaders to be passed as command line argument      
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shader") && SystemConfig["shader"] != "None")
            {
                string videoDriver = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"))["video_driver"];
                bool isOpenGL = (emulator != "angle") && (videoDriver == "gl") && (!coreNoGL.Contains(core));

                string path = Path.Combine(AppConfig.GetFullPath("shaders"), "configs", SystemConfig["shaderset"], "rendering-defaults.yml");
                if (File.Exists(path))
                {
                    string renderconfig = SystemShaders.GetShader(File.ReadAllText(path), SystemConfig["system"], SystemConfig["emulator"], SystemConfig["core"], isOpenGL);
                    if (!string.IsNullOrEmpty(renderconfig))
                        SystemConfig["shader"] = renderconfig;
                }

                string shaderFilename = SystemConfig["shader"] + (isOpenGL ? ".glslp" : ".slangp");

                string videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), shaderFilename).Replace("/", "\\");
                if (!File.Exists(videoShader))
                    videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), isOpenGL ? "shaders_glsl" : "shaders_slang", shaderFilename).Replace("/", "\\");

                if (!File.Exists(videoShader))
                    videoShader = Path.Combine(AppConfig.GetFullPath("shaders"), isOpenGL ? "glsl" : "slang", shaderFilename).Replace("/", "\\");

                if (!File.Exists(videoShader))
                    videoShader = Path.Combine(RetroarchPath, "shaders", isOpenGL ? "shaders_glsl" : "shaders_slang", shaderFilename).Replace("/", "\\");

                if (!File.Exists(videoShader) && !isOpenGL && shaderFilename.Contains("zfast-"))
                    videoShader = Path.Combine(RetroarchPath, "shaders", isOpenGL ? "shaders_glsl" : "shaders_slang", "crt/crt-geom.slangp").Replace("/", "\\");

                if (File.Exists(videoShader))
                {
                    commandArray.Add("--set-shader");
                    commandArray.Add("\"" + videoShader + "\"");
                }
            }

            string args = string.Join(" ", commandArray);

            // Manage patches
            string ipsPattern = "*.ips";
            string upsPattern = "*.ups";
            string bpsPattern = "*.bps";
            List<string> ipsFiles = new List<string>();
            List<string> upsFiles = new List<string>();
            List<string> bpsFiles = new List<string>();

            List<string> patchArgs = new List<string>();
            if (Features.IsSupported("softpatch") && SystemConfig.isOptSet("applyPatch") && !string.IsNullOrEmpty(SystemConfig["applyPatch"]))
            {
                string patchMethod = SystemConfig["applyPatch"];
                switch (patchMethod)
                {
                    case "none":
                        patchArgs.Add("--no-patch");
                        break;
                    case "patchFolder":
                        string patchFolder = Path.Combine(Path.GetDirectoryName(rom), "patches");
                        if (!Directory.Exists(patchFolder))
                            break;
                        ipsFiles = Directory.GetFiles(patchFolder, ipsPattern)
                            .Where(file => Path.GetFileNameWithoutExtension(file).Equals(romName, StringComparison.OrdinalIgnoreCase)).ToList();
                        upsFiles = Directory.GetFiles(patchFolder, upsPattern)
                            .Where(file => Path.GetFileNameWithoutExtension(file).Equals(romName, StringComparison.OrdinalIgnoreCase)).ToList();
                        bpsFiles = Directory.GetFiles(patchFolder, bpsPattern)
                            .Where(file => Path.GetFileNameWithoutExtension(file).Equals(romName, StringComparison.OrdinalIgnoreCase)).ToList();
                        
                        if (ipsFiles.Count > 0)
                        {
                            patchArgs.Add("--ips");
                            patchArgs.Add("\"" + ipsFiles.FirstOrDefault() + "\"");
                        }
                        if (upsFiles.Count > 0)
                        {
                            patchArgs.Add("--ups");
                            patchArgs.Add("\"" + upsFiles.FirstOrDefault() + "\"");
                        }
                        if (bpsFiles.Count > 0)
                        {
                            patchArgs.Add("--bps");
                            patchArgs.Add("\"" + bpsFiles.FirstOrDefault() + "\"");
                        }
                        break;
                    case "subFolder":
                        string subFolder = Path.Combine(Path.GetDirectoryName(rom), romName + "-patches");
                        if (!Directory.Exists(subFolder))
                            break;
                        ipsFiles = Directory.GetFiles(subFolder, ipsPattern).ToList();
                        upsFiles = Directory.GetFiles(subFolder, upsPattern).ToList();
                        bpsFiles = Directory.GetFiles(subFolder, bpsPattern).ToList();

                        if (ipsFiles.Count > 0)
                        {
                            patchArgs.Add("--ips");
                            patchArgs.Add("\"" + ipsFiles.FirstOrDefault() + "\"");
                        }
                        if (upsFiles.Count > 0)
                        {
                            patchArgs.Add("--ups");
                            patchArgs.Add("\"" + upsFiles.FirstOrDefault() + "\"");
                        }
                        if (bpsFiles.Count > 0)
                        {
                            patchArgs.Add("--bps");
                            patchArgs.Add("\"" + bpsFiles.FirstOrDefault() + "\"");
                        }
                        break;
                }
            }
            string patchArg = string.Join(" ", patchArgs);

            // Special case : .atari800.cfg is loaded from path in 'HOME' environment variable
            if (core == "atari800")
            {
                CurrentHomeDirectory = Environment.GetEnvironmentVariable("HOME");
                Environment.SetEnvironmentVariable("HOME", RetroarchPath);
            }

            // manage MESS systems (MAME core)
            MessSystem messSystem = core == "mame" ? MessSystem.GetMessSystem(system, subCore) : null;
            if (messSystem != null && !string.IsNullOrEmpty(messSystem.MachineName))
            {
                var messArgs = messSystem.GetMameCommandLineArguments(system, rom).JoinArguments();
                messArgs = messArgs.Replace("\\\"", "\"");
                messArgs = "\"" + messArgs.Replace("\"", "\\\"") + "\"";
                messArgs = (messArgs + " " + args).Trim();

                return new ProcessStartInfo()
                {
                    FileName = Path.Combine(RetroarchPath, emulator == "angle" ? "retroarch_angle.exe" : "retroarch.exe"),
                    WorkingDirectory = RetroarchPath,
                    Arguments = ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" " + messArgs).Trim()
                };
            }

            // Run MelonDS firmware, use the first .nds file found in the folder as rom argument (the core does not have an argument to boot to firmware)
            if (core == "melonds" && Path.GetExtension(rom) == ".bin")
            {
                string romPath = Path.GetDirectoryName(rom);
                var romToLaunch = Directory.EnumerateFiles(romPath, "*.nds")
                    .FirstOrDefault();

                if (romToLaunch == null)
                    throw new ApplicationException("Libretro:melonDS requires a '.nds' game file to load a nand file.");

                rom = romToLaunch;
            }

            string retroarch = Path.Combine(RetroarchPath, emulator == "angle" ? "retroarch_angle.exe" : "retroarch.exe");
            if (emulator != "angle" && SystemConfig["netplay"] == "true" && (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator"))
                retroarch = GetNetPlayPatchedRetroarch();

            string finalArgs;

            if (patchArgs.Count > 0)
            {
                if (string.IsNullOrEmpty(rom))
                    finalArgs = (patchArg + " -L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" " + args).Trim();
                else
                    finalArgs = (patchArg + " -L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" \"" + rom + "\" " + args).Trim();
            }
            else
            {
                if (string.IsNullOrEmpty(rom))
                    finalArgs = ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" " + args).Trim();
                else
                    finalArgs = ("-L \"" + Path.Combine(RetroarchCorePath, core + "_libretro.dll") + "\" \"" + rom + "\" " + args).Trim();
            }

            return new ProcessStartInfo()
            {
                FileName = retroarch,
                WorkingDirectory = RetroarchPath,
                Arguments = finalArgs,
            };
        }

        private bool GetBestMameCore(string system, string subCore, string core, string emulator, string rom, out string newCore)
        {
            newCore = null;

            if (system == "mame" && subCore == null && core != null && core.StartsWith("mame"))
            {
                if (string.IsNullOrEmpty(Program.SystemConfig[system + ".core"]) && string.IsNullOrEmpty(Program.CurrentGame.Core))
                {
                    string[] supportedCores = null;

                    // Load supported core list from es_systems.cfg
                    var esSystems = Program.EsSystems;
                    if (esSystems != null)
                    {
                        supportedCores = esSystems.Systems
                            .Where(sys => sys.Name == system)
                            .SelectMany(sys => sys.Emulators)
                            .Where(emul => emul.Name == emulator)
                            .SelectMany(emul => emul.Cores)
                            .Select(cr => cr.Name)
                            .ToArray();

                        if (supportedCores.Length == 0)
                            supportedCores = null;
                    }

                    var compatibleCores = MameVersionDetector.FindCompatibleMameCores(rom, supportedCores).Select(c => c.Name.Replace("-", "_")).ToList();
                    var bestCore = compatibleCores.FirstOrDefault();
                    if (!string.IsNullOrEmpty(bestCore))
                    {
                        newCore = bestCore;

                        if (SystemConfig.getOptBoolean("use_guns") && core == "mame2003" && compatibleCores.Contains("mame2003_plus"))
                        {
                            // mame2003 is not working fine with lightgun games -> prefer mame2003_plus if it's compatible
                            newCore = "mame2003_plus";
                        }

                        SimpleLogger.Instance.Info("[FindBestMameCore] Detected compatible mame core : " + newCore);
                        return true;
                    }
                    else
                        SimpleLogger.Instance.Info("[FindBestMameCore] No detected compatible mame core. Using current default core : " + core);

                    return false;
                }

                return false;
            }

            return false;
        }

        private void Configure(string system, string core, string rom, ScreenResolution resolution)
        {
            var retroarchConfig = ConfigFile.FromFile(Path.Combine(RetroarchPath, "retroarch.cfg"), new ConfigFileOptions() { CaseSensitive = true });

            retroarchConfig["global_core_options"] = "true";
            retroarchConfig["core_options_path"] = ""; //',             '"/userdata/system/configs/retroarch/cores/retroarch-core-options.cfg"')          
            retroarchConfig["menu_driver"] = "ozone";
            retroarchConfig["ui_menubar_enable"] = "false";
            retroarchConfig["menu_framebuffer_opacity"] = "0.900000";
            retroarchConfig["video_fullscreen"] = "true";
            retroarchConfig["video_window_save_positions"] = "false";
            retroarchConfig.DisableAll("video_viewport_bias_x");
            retroarchConfig.DisableAll("video_viewport_bias_y");
            retroarchConfig["video_allow_rotate "] = "true";
            retroarchConfig["menu_show_load_content_animation"] = "false";
            retroarchConfig["notification_show_autoconfig"] = "false";
            retroarchConfig["notification_show_config_override_load"] = "false";            
            retroarchConfig["notification_show_remap_load"] = "false";
            retroarchConfig["notification_show_cheats_applied"] = "true";
            retroarchConfig["notification_show_patch_applied"] = "true";
            retroarchConfig["notification_show_fast_forward"] = "true";
            retroarchConfig["notification_show_disk_control"] = "true";
            retroarchConfig["notification_show_save_state"] = "true";
            retroarchConfig["notification_show_screenshot"] = "true";
            retroarchConfig["notification_show_when_menu_is_alive"] = "false";
            retroarchConfig["driver_switch_enable"] = "true";
            retroarchConfig["rgui_extended_ascii"] = "true";
            retroarchConfig["rgui_show_start_screen"] = "false";
            retroarchConfig["rgui_browser_directory"] = AppConfig.GetFullPath("roms") ?? "default";
            retroarchConfig["input_overlay_enable_autopreferred"] = "false";
            if (CoreSaveSort.Contains(core))
                retroarchConfig["sort_savefiles_enable"] = "true";
            else
                retroarchConfig["sort_savefiles_enable"] = "false";
            retroarchConfig["sort_savefiles_by_content_enable"] = "false";

            // input driver set to raw if multigun is enabled
            if (SystemConfig.getOptBoolean("use_guns") && !SystemConfig.getOptBoolean("one_gun"))
            {
                int gunCount = RawLightgun.GetUsableLightGunCount();
                var guns = RawLightgun.GetRawLightguns();

                if (gunCount > 1 && guns.Length > 1)
                    retroarchConfig["input_driver"] = "raw";
                else
                    retroarchConfig["input_driver"] = "dinput";
            }
            else
                retroarchConfig["input_driver"] = "dinput";

            BindBoolFeature(retroarchConfig, "game_specific_options", "game_specific_options", "true", "false");
            BindBoolFeature(retroarchConfig, "pause_on_disconnect", "pause_on_disconnect", "true", "false");
            BindBoolFeature(retroarchConfig, "pause_nonactive", "use_guns", "true", "false", true); // Pause when calibrating gun...
            BindBoolFeature(retroarchConfig, "input_autodetect_enable", "disableautocontrollers", "true", "false", true);
            BindFeature(retroarchConfig, "input_analog_deadzone", "analog_deadzone", "0.000000");
            BindFeature(retroarchConfig, "input_analog_sensitivity", "analog_sensitivity", "1.000000");
            BindFeatureSlider(retroarchConfig, "fastforward_ratio", "fastforward_ratio", "0.000000");
            retroarchConfig["input_remap_binds_enable"] = "true";
            retroarchConfig["input_remap_sort_by_controller_enable"] = "false";
            retroarchConfig["input_remapping_directory"] = ":\\config\\remaps";

            SetupUIMode(retroarchConfig);

            // Resolution & monitor
            bool forcefs = SystemConfig.getOptBoolean("forcefullscreen");
            int test = Screen.AllScreens.Length;
            if (Features.IsSupported("MonitorIndex"))
            {
                if (SystemConfig.isOptSet("MonitorIndex"))
                {
                    int monitorId;
                    if (int.TryParse(SystemConfig["MonitorIndex"], out monitorId) && monitorId <= Screen.AllScreens.Length)
                        retroarchConfig["video_monitor_index"] = (monitorId).ToString();
                }
                else
                {
                    retroarchConfig["video_monitor_index"] = "0";
                }                   
            }

            if (resolution == null)
            {
                var res = ScreenResolution.CurrentResolution;
                retroarchConfig["video_fullscreen_x"] = res.Width.ToString();
                retroarchConfig["video_fullscreen_y"] = res.Height.ToString();
                retroarchConfig["video_refresh_rate"] = res.DisplayFrequency.ToString("N6", System.Globalization.CultureInfo.InvariantCulture);

                if (!SystemConfig.isOptSet("MonitorIndex"))
                {
                    Rectangle emulationStationBounds;
                    if (IsEmulationStationWindowed(out emulationStationBounds) && !forcefs)
                    {
                        int width = emulationStationBounds.Width;
                        int height = emulationStationBounds.Height;
                        
                        if (emulationStationBounds.Left == 0 && emulationStationBounds.Top == 0)
                        {
                            emulationStationBounds.X = (res.Width - width) / 2 - SystemInformation.FrameBorderSize.Width;
                            emulationStationBounds.Y = (res.Height - height - SystemInformation.CaptionHeight - SystemInformation.MenuHeight) / 2 - SystemInformation.FrameBorderSize.Height;
                        }

                        retroarchConfig["video_windowed_position_x"] = emulationStationBounds.X.ToString();
                        retroarchConfig["video_windowed_position_y"] = emulationStationBounds.Y.ToString();
                        retroarchConfig["video_windowed_position_width"] = width.ToString();
                        retroarchConfig["video_windowed_position_height"] = height.ToString();
                        retroarchConfig["video_fullscreen"] = "false";
                        retroarchConfig["video_window_save_positions"] = "true";

                        resolution = ScreenResolution.FromSize(width, height); // For bezels
                    }
                    else
                        retroarchConfig["video_windowed_fullscreen"] = "true";
                }
                else
                    retroarchConfig["video_windowed_fullscreen"] = "true";
            }
            else if (!forcefs)
            {
                retroarchConfig["video_fullscreen_x"] = resolution.Width.ToString();
                retroarchConfig["video_fullscreen_y"] = resolution.Height.ToString();
                retroarchConfig["video_refresh_rate"] = resolution.DisplayFrequency.ToString("N6", System.Globalization.CultureInfo.InvariantCulture);
                retroarchConfig["video_windowed_fullscreen"] = "false";
            }

            else
            {
                retroarchConfig["video_fullscreen_x"] = resolution.Width.ToString();
                retroarchConfig["video_fullscreen_y"] = resolution.Height.ToString();
                retroarchConfig["video_refresh_rate"] = resolution.DisplayFrequency.ToString("N6", System.Globalization.CultureInfo.InvariantCulture);
                retroarchConfig["video_windowed_fullscreen"] = "true";
            }

            if (resolution == null && retroarchConfig["video_monitor_index"] != "0")
                resolution = ScreenResolution.FromScreenIndex(retroarchConfig["video_monitor_index"].ToInteger() - 1);

            // Folders
            if (!string.IsNullOrEmpty(AppConfig["bios"]))
            {
                if (Directory.Exists(AppConfig["bios"]))
                    retroarchConfig["system_directory"] = AppConfig.GetFullPath("bios");
                else if (retroarchConfig["system_directory"] != @":\system" && !Directory.Exists(retroarchConfig["system_directory"]))
                    retroarchConfig["system_directory"] = @":\system";
            }

            if (!string.IsNullOrEmpty(AppConfig["thumbnails"]))
            {
                if (Directory.Exists(AppConfig["thumbnails"]))
                    retroarchConfig["thumbnails_directory"] = AppConfig.GetFullPath("thumbnails");
                else if (retroarchConfig["thumbnails_directory"] != @":\thumbnails" && !Directory.Exists(retroarchConfig["thumbnails_directory"]))
                    retroarchConfig["thumbnails_directory"] = @":\thumbnails";
            }

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]))
            {
                if (Directory.Exists(AppConfig["screenshots"]))
                {
                    retroarchConfig["screenshot_directory"] = AppConfig.GetFullPath("screenshots");
                    _screenShotWatcher = new ScreenShotsWatcher(AppConfig.GetFullPath("screenshots"), SystemConfig["system"], SystemConfig["rom"]);
                }
                else if (retroarchConfig["screenshot_directory"] != @":\screenshots" && !Directory.Exists(retroarchConfig["screenshot_directory"]))
                    retroarchConfig["screenshot_directory"] = @":\screenshots";
            }

            // Save Files
            string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
            
            if (core == "mame")
                savePath = Path.Combine(AppConfig.GetFullPath("saves"));

            //if (core == "dolphin" && system == "wii")
            //    savePath = Path.Combine(AppConfig.GetFullPath("saves"), "dolphin");

            FileTools.TryCreateDirectory(savePath);                
            retroarchConfig["savefile_directory"] = savePath;
            retroarchConfig["savefiles_in_content_dir"] = "false";

            // Cheats folder
            string cheatspath = Path.Combine(AppConfig.GetFullPath("cheats"), "retroarch");
            if (!string.IsNullOrEmpty(cheatspath))
            {
                if (Directory.Exists(cheatspath))
                    retroarchConfig["cheat_database_path"] = cheatspath;
                else if (retroarchConfig["cheat_database_path"] != @":\cheats" && !Directory.Exists(retroarchConfig["cheat_database_path"]))
                    retroarchConfig["cheat_database_path"] = @":\cheats";
            }

            // Records path
            string recordconfigpath = Path.Combine(AppConfig.GetFullPath("records"), "config");
            if (!string.IsNullOrEmpty(recordconfigpath))
            {
                if (Directory.Exists(recordconfigpath))
                    retroarchConfig["recording_config_directory"] = recordconfigpath;
                else if (retroarchConfig["recording_config_directory"] != @":\records\config" && !Directory.Exists(retroarchConfig["recording_config_directory"]))
                    retroarchConfig["recording_config_directory"] = @":\records\config";
            }

            string recordoutputpath = Path.Combine(AppConfig.GetFullPath("records"), "output");
            if (!string.IsNullOrEmpty(recordoutputpath))
            {
                if (Directory.Exists(recordoutputpath))
                    retroarchConfig["recording_output_directory"] = recordoutputpath;
                else if (retroarchConfig["recording_output_directory"] != @":\records\output" && !Directory.Exists(retroarchConfig["recording_output_directory"]))
                    retroarchConfig["recording_output_directory"] = @":\records\output";
            }

            // Cache
            string cacheDirectory = Path.Combine(Path.GetTempPath(), "retroarch");
            FileTools.TryCreateDirectory(cacheDirectory);                
            retroarchConfig["cache_directory"] = cacheDirectory;

            // Savestates
            var saveStatePath = Program.EsSaveStates.GetSavePath(system, "libretro", core);
            if (!string.IsNullOrEmpty(saveStatePath))
            {
                FileTools.TryCreateDirectory(saveStatePath);

                retroarchConfig["savestate_directory"] = saveStatePath;
                retroarchConfig["savestate_thumbnail_enable"] = "true";
                retroarchConfig["savestates_in_content_dir"] = "false";
                retroarchConfig["sort_savestates_enable"] = "false";
                retroarchConfig["sort_savestates_by_content_enable"] = "false";

            }

            if (SystemConfig.isOptSet("incrementalsavestates") && !SystemConfig.getOptBoolean("incrementalsavestates"))
            {
                retroarchConfig["savestate_auto_index"] = "false";
                retroarchConfig["savestate_max_keep"] = "50";
            }
            else
            {
                retroarchConfig["savestate_auto_index"] = "true";
                retroarchConfig["savestate_max_keep"] = "0";
            }

            _stateFileManager = LibRetroStateFileManager.FromSaveStateFile(SystemConfig["state_file"]);
            if (_stateFileManager != null)
            {
                retroarchConfig["savestate_auto_load"] = "true";
                retroarchConfig["savestate_auto_save"] = _stateFileManager.IsAutoFile ? "true" : "false";
            }
            else
            {
                BindBoolFeature(retroarchConfig, "savestate_auto_load", "autosave", "true", "false");
                BindBoolFeature(retroarchConfig, "savestate_auto_save", "autosave", "true", "false");
            }

            BindFeature(retroarchConfig, "state_slot", "state_slot", "0");

            // Shaders
            if (AppConfig.isOptSet("shaders") && SystemConfig.isOptSet("shader") && SystemConfig["shader"] != "None")
                retroarchConfig["video_shader_enable"] = "true";
            else if (Features.IsSupported("shaderset"))
                retroarchConfig["video_shader_enable"] = "false";

            // Video filters
            string videoFiltersPath = Path.Combine(RetroarchPath, "filters", "video");
            if (Directory.Exists(videoFiltersPath))
                retroarchConfig["video_filter_dir"] = videoFiltersPath;
            
            if (SystemConfig.isOptSet("videofilters") && !string.IsNullOrEmpty(SystemConfig["videofilters"]) && SystemConfig["videofilters"] != "None")
            {
                string videofilter = SystemConfig["videofilters"] + ".filt";
                retroarchConfig["video_filter"] = Path.Combine(videoFiltersPath, videofilter);
            }
            else if (Features.IsSupported("videofilters"))
            {
                retroarchConfig["video_filter_dir"] = ":\\filters\\video";
                retroarchConfig["video_filter"] = "";
            }

            // Aspect ratio
            if (SystemConfig.isOptSet("ratio"))
            {
                if (SystemConfig["ratio"] == "custom")
                {
                    retroarchConfig["video_aspect_ratio_auto"] = "false";
                    retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("custom").ToString();
                    retroarchConfig["video_viewport_bias_x"] = "0.000000";
                    retroarchConfig["video_viewport_bias_y"] = "0.000000";
                }
                else
                {
                    int idx = ratioIndexes.IndexOf(SystemConfig["ratio"]);
                    if (idx >= 0)
                    {
                        retroarchConfig["aspect_ratio_index"] = idx.ToString();
                        retroarchConfig["video_aspect_ratio_auto"] = "false";
                    }
                    else
                    {
                        retroarchConfig["video_aspect_ratio_auto"] = "true";
                        retroarchConfig["aspect_ratio_index"] = "";
                    }
                }
            }
            else if (SystemConfig["shader"].Contains("Mega_Bezel"))
            {
                retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("full").ToString();
            }
            else if (core == "tgbdual" || system == "wii" || system == "fbneo" || system == "nds" || system == "mame")
            {
                retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString();
            }
            else
                retroarchConfig["aspect_ratio_index"] = "";
            
            // Rewind
            if (!SystemConfig.isOptSet("rewind"))
                retroarchConfig["rewind_enable"] = systemNoRewind.Contains(system) ? "false" : "true"; // AUTO
            else if (SystemConfig.getOptBoolean("rewind"))
                retroarchConfig["rewind_enable"] = "true";
            else
                retroarchConfig["rewind_enable"] = "false";

            // Audio
            BindFeature(retroarchConfig, "audio_driver", "audio_driver", "xaudio"); // Audio driver
            BindFeature(retroarchConfig, "audio_resampler", "audio_resampler", "sinc");
            BindFeature(retroarchConfig, "audio_resampler_quality", "audio_resampler_quality", "3");
            BindFeature(retroarchConfig, "audio_volume", "audio_volume", "0.000000");
            BindFeature(retroarchConfig, "audio_mixer_volume", "audio_mixer_volume", "0.000000");
            
            if (SystemConfig["audio_dsp_plugin"] == "none")
                retroarchConfig["audio_dsp_plugin"] = "";
            else
                BindFeature(retroarchConfig, "audio_dsp_plugin", "audio_dsp_plugin", "");

            if (SystemConfig.isOptSet("audio_sync") && !SystemConfig.getOptBoolean("audio_sync"))
                retroarchConfig["audio_sync"] = "false";
            else
                retroarchConfig["audio_sync"] = "true";

            // Misc
            BindBoolFeature(retroarchConfig, "video_smooth", "smooth", "true", "false");
            BindBoolFeature(retroarchConfig, "video_scale_integer", "integerscale", "true", "false");
            BindBoolFeature(retroarchConfig, "video_threaded", "video_threaded", "true", "false");
            BindBoolFeature(retroarchConfig, "fps_show", "showFPS", "true", "false");
            BindBoolFeature(retroarchConfig, "video_frame_delay_auto", "video_frame_delay_auto", "true", "false"); // Auto frame delay (input delay reduction via frame timing)
            BindBoolFeature(retroarchConfig, "quit_press_twice", "PressTwice", "true", "false"); // Press hotkeys twice to exit

            BindBoolFeatureOn(retroarchConfig, "video_font_enable", "OnScreenMsg", "true", "false"); // OSD notifications
            BindFeature(retroarchConfig, "video_rotation", "RotateVideo", "0"); // video rotation
            BindFeature(retroarchConfig, "screen_orientation", "RotateScreen", "0"); // screen orientation
            BindFeature(retroarchConfig, "crt_switch_resolution", "CRTSwitch", "0"); // CRT Switch
            BindFeature(retroarchConfig, "crt_switch_resolution_super", "CRTSuperRes", "0"); // CRT Resolution

            BindFeature(retroarchConfig, "input_poll_type_behavior", "input_poll_type_behavior", "2");


            if (SystemConfig.getOptBoolean("GameFocus"))
                retroarchConfig["input_auto_game_focus"] = "1";
            else
                retroarchConfig["input_auto_game_focus"] = "0";

            // Discord presence
            if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                retroarchConfig["discord_allow"] = "true";
            else
                retroarchConfig["discord_allow"] = "false";

            // Stats
            if (SystemConfig.isOptSet("DrawStats"))
            {
                if (SystemConfig["DrawStats"] == "fps_only")
                {
                    retroarchConfig["fps_show"] = "true";
                    retroarchConfig["memory_show"] = "false";
                    retroarchConfig["statistics_show"] = "false";
                }
                else if (SystemConfig["DrawStats"] == "mem_only")
                {
                    retroarchConfig["fps_show"] = "false";
                    retroarchConfig["memory_show"] = "true";
                    retroarchConfig["statistics_show"] = "false";
                }
                else if (SystemConfig["DrawStats"] == "fps_mem")
                {
                    retroarchConfig["fps_show"] = "true";
                    retroarchConfig["memory_show"] = "true";
                    retroarchConfig["statistics_show"] = "false";
                }
                else if (SystemConfig["DrawStats"] == "tech_stats")
                {
                    retroarchConfig["fps_show"] = "false";
                    retroarchConfig["memory_show"] = "false";
                    retroarchConfig["statistics_show"] = "true";
                }
            }
            else
            {
                retroarchConfig["fps_show"] = "false";
                retroarchConfig["memory_show"] = "false";
                retroarchConfig["statistics_show"] = "false";
            }

            // Default controllers
            retroarchConfig.DisableAll("input_libretro_device_p");

            retroarchConfig["input_libretro_device_p1"] = coreToP1Device.ContainsKey(core) ? coreToP1Device[core] : "1";
            retroarchConfig["input_libretro_device_p2"] = coreToP2Device.ContainsKey(core) ? coreToP2Device[core] : "1";

            if (LibretroControllers.WriteControllersConfig(retroarchConfig, system, core))
                UseEsPadToKey = false;

            // If no hotkey if configured, add pad2key to exit retroarch
            if (retroarchConfig["input_enable_hotkey"] == "nul" && retroarchConfig["input_enable_hotkey_btn"] == "nul" && retroarchConfig["input_enable_hotkey_axis"] == "nul" && retroarchConfig["input_enable_hotkey_mbtn"] == "nul")
            {
                if (Controllers.Any(c => !c.IsKeyboard))
                    _noHotkey = true;
            }

            // Core, services & bezel configs
            ConfigureRetroachievements(retroarchConfig);
            ConfigureNetPlay(retroarchConfig);
            ConfigureAIService(retroarchConfig);
            ConfigureRunahead(system, core, retroarchConfig);
            ConfigureCoreOptions(retroarchConfig, system, core);
            
            // Video driver
            ConfigureVideoDriver(core, retroarchConfig);
            ConfigureGPUIndex(retroarchConfig);
            ConfigureVSync(retroarchConfig);

            // Bezels
            ConfigureBezels(retroarchConfig, system, rom, core, resolution);

            // Language
            SetLanguage(retroarchConfig);

            // Force raw input
            if (SystemConfig.isOptSet("libretro_rawinput") && SystemConfig.getOptBoolean("libretro_rawinput"))
                retroarchConfig["input_driver"] = "raw";

            if (hdrCompatibleVideoDrivers.Contains(_video_driver))
                BindBoolFeature(retroarchConfig, "video_hdr_enable", "enable_hdr", "true", "false");
            else
                retroarchConfig["video_hdr_enable"] = "false";

            // Custom overrides : allow the user to configure directly retroarch.cfg via batocera.conf via lines like : snes.retroarch.menu_driver=rgui
            foreach (var user_config in SystemConfig)
                if (user_config.Name.StartsWith("retroarch."))
                    retroarchConfig[user_config.Name.Substring("retroarch.".Length)] = user_config.Value;
                
            if (retroarchConfig.IsDirty)
                retroarchConfig.Save(Path.Combine(RetroarchPath, "retroarch.cfg"), true);
        }

        private void ConfigureRunahead(string system, string core, ConfigFile retroarchConfig)
        {
            if (coreNoPreemptiveFrames.Contains(core) && SystemConfig.isOptSet("preemptive_frames") && SystemConfig.getOptBoolean("preemptive_frames"))
                SimpleLogger.Instance.Info("[INFO] Core not compatible with preemptive frames");

            if (systemNoRunahead.Contains(system) && SystemConfig.isOptSet("runahead") && SystemConfig["runahead"].ToIntegerString().ToInteger() > 0)
                SimpleLogger.Instance.Info("[INFO] System not compatible with run-ahead");

            if (SystemConfig.isOptSet("runahead") && SystemConfig["runahead"].ToIntegerString().ToInteger() > 0 && SystemConfig.isOptSet("preemptive_frames") && SystemConfig.getOptBoolean("preemptive_frames") && !coreNoPreemptiveFrames.Contains(core))
            {
                retroarchConfig["run_ahead_enabled"] = "false";
                retroarchConfig["run_ahead_frames"] = SystemConfig["runahead"].ToIntegerString();
                retroarchConfig["run_ahead_secondary_instance"] = "false";
                retroarchConfig["preemptive_frames_enable"] = "true";
            }

            else if (SystemConfig.isOptSet("runahead") && SystemConfig["runahead"].ToIntegerString().ToInteger() > 0 && !systemNoRunahead.Contains(system))
            {
                retroarchConfig["run_ahead_enabled"] = "true";
                retroarchConfig["run_ahead_frames"] = SystemConfig["runahead"].ToIntegerString();
                retroarchConfig["preemptive_frames_enable"] = "false";

                if (SystemConfig.isOptSet("secondinstance") && SystemConfig.getOptBoolean("secondinstance"))
                    retroarchConfig["run_ahead_secondary_instance"] = "true";
                else
                    retroarchConfig["run_ahead_secondary_instance"] = "false";
            }

            else
            {
                retroarchConfig["run_ahead_enabled"] = "false";
                retroarchConfig["run_ahead_frames"] = "0";
                retroarchConfig["run_ahead_secondary_instance"] = "false";
                retroarchConfig["preemptive_frames_enable"] = "false";
            }
        }

        private void ConfigureVideoDriver(string core, ConfigFile retroarchConfig)
        {
            if (!Features.IsSupported("video_driver"))
                return;
            
            _video_driver = retroarchConfig["video_driver"];

            // Return if driver was forced in core settings
            if (_coreVideoDriverForce)
            {
                _video_driver = retroarchConfig["video_driver"];
                return;
            }

            // general, assigned selected core
            if (SystemConfig.isOptSet("video_driver"))
            {
                _video_driver = SystemConfig["video_driver"];
                retroarchConfig["video_driver"] = SystemConfig["video_driver"];
            }
            
            // core stuff
            if (core == "dolphin" && retroarchConfig["video_driver"] != "d3d11" && retroarchConfig["video_driver"] != "vulkan" && retroarchConfig["video_driver"] != "gl" && retroarchConfig["video_driver"] != "glcore")
            {
                _video_driver = "d3d11";
                retroarchConfig["video_driver"] = "d3d11";
                return;
            }
            if (core.StartsWith("mupen64") && SystemConfig["RDP_Plugin"] == "parallel")
            {
                _video_driver = "vulkan";
                retroarchConfig["video_driver"] = "vulkan";
                return;
            }
            if (core == "pcsx2" && retroarchConfig["video_driver"] == "gl")
            {
                _video_driver = "glcore";
                retroarchConfig["video_driver"] = "glcore";
                return;
            }
            if (core == "scummvm" && (retroarchConfig["video_driver"] != "gl" && retroarchConfig["video_driver"] != "glcore"))
            {
                _video_driver = "glcore";
                retroarchConfig["video_driver"] = "glcore";
                return;
            }

            // Set default video driver per core
            if (!SystemConfig.isOptSet("video_driver") && defaultVideoDriver.ContainsKey(core))
            {
                _video_driver = defaultVideoDriver[core];
                retroarchConfig["video_driver"] = defaultVideoDriver[core];
            }

            if (SystemConfig["ratio"] == "custom")
            {
                if (_forcenobias)
                    retroarchConfig["video_viewport_bias_y"] = "0.000000";
                else if (_forceBias || driverYBias.Contains(SystemConfig["video_driver"]))
                    retroarchConfig["video_viewport_bias_y"] = "1.000000";
                else
                    retroarchConfig["video_viewport_bias_y"] = "0.000000";
            }
        }

        /// <summary>
        /// Configure GPU index
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureGPUIndex(ConfigFile retroarchConfig)
        {
            if (!Features.IsSupported("GPUIndex"))
                return;

            if (SystemConfig.isOptSet("GPUIndex"))
            {
                if (retroarchConfig["video_driver"] == "d3d10")
                    retroarchConfig["d3d10_gpu_index"] = SystemConfig["GPUIndex"];

                if (retroarchConfig["video_driver"] == "d3d11")
                    retroarchConfig["d3d11_gpu_index"] = SystemConfig["GPUIndex"];

                if (retroarchConfig["video_driver"] == "d3d12")
                    retroarchConfig["d3d12_gpu_index"] = SystemConfig["GPUIndex"];

                if (retroarchConfig["video_driver"] == "vulkan")
                    retroarchConfig["vulkan_gpu_index"] = SystemConfig["GPUIndex"];
            }
            else
            {
                retroarchConfig["d3d10_gpu_index"] = "0";
                retroarchConfig["d3d11_gpu_index"] = "0";
                retroarchConfig["d3d12_gpu_index"] = "0";
                retroarchConfig["vulkan_gpu_index"] = "0";
            }            
        }

        /// <summary>
        /// Synchronization options
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureVSync(ConfigFile retroarchConfig)
        {
            if (Features.IsSupported("video_hard_sync"))
            {
                if (SystemConfig.isOptSet("video_hard_sync"))
                {
                    if (SystemConfig["video_hard_sync"] != "false")
                    {
                        retroarchConfig["video_hard_sync"] = "true";
                        retroarchConfig["video_hard_sync_frames"] = SystemConfig["video_hard_sync"];
                    }
                    else
                    {
                        retroarchConfig["video_hard_sync"] = "false";
                        retroarchConfig["video_hard_sync_frames"] = "0";
                    }
                }
                else
                {
                    retroarchConfig["video_hard_sync"] = "false";
                    retroarchConfig["video_hard_sync_frames"] = "0";
                }
            }

            if (SystemConfig.isOptSet("video_swap_interval") && !string.IsNullOrEmpty(SystemConfig["video_swap_interval"]))
                retroarchConfig["video_swap_interval"] = SystemConfig["video_swap_interval"].ToIntegerString();
            else
                retroarchConfig["video_swap_interval"] = "0";

            if (SystemConfig.isOptSet("video_black_frame_insertion") && !string.IsNullOrEmpty(SystemConfig["video_black_frame_insertion"]))
                retroarchConfig["video_black_frame_insertion"] = SystemConfig["video_black_frame_insertion"].ToIntegerString();
            else
                retroarchConfig["video_black_frame_insertion"] = "0";

            BindBoolFeature(retroarchConfig, "vrr_runloop_enable", "vrr_runloop_enable", "true", "false");

            if (Features.IsSupported("video_vsync"))
            {
                if (SystemConfig.isOptSet("video_vsync"))
                {
                    if (SystemConfig["video_vsync"] != "adaptative")
                    {
                        retroarchConfig["video_vsync"] = SystemConfig["video_vsync"];
                        retroarchConfig["video_adaptive_vsync"] = "false";
                    }
                    else
                    {
                        retroarchConfig["video_vsync"] = "true";
                        retroarchConfig["video_adaptive_vsync"] = "true";
                    }
                }
                else if (SystemConfig.isOptSet("VSync") && !SystemConfig.getOptBoolean("VSync"))
                {
                    retroarchConfig["video_vsync"] = "false";
                    retroarchConfig["video_adaptive_vsync"] = "false";
                }
                else
                {
                    retroarchConfig["video_vsync"] = "true";
                    retroarchConfig["video_adaptive_vsync"] = "false";
                }
            }
            else if (SystemConfig.isOptSet("VSync") && !SystemConfig.getOptBoolean("VSync"))
            {
                retroarchConfig["video_vsync"] = "false";
                retroarchConfig["video_adaptive_vsync"] = "false";
            }           
        }

        /// <summary>
        /// AI service for game translations
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureAIService(ConfigFile retroarchConfig)
        {
            // if (!Features.IsSupported("ai_service_enabled"))
            //    return;

            if (SystemConfig.isOptSet("ai_service_enabled") && SystemConfig.getOptBoolean("ai_service_enabled"))
            {
                retroarchConfig["ai_service_enable"] = "true";
                retroarchConfig["ai_service_mode"] = "0";
                retroarchConfig["ai_service_source_lang"] = "0";

                if (!string.IsNullOrEmpty(SystemConfig["ai_service_url"]))
                    retroarchConfig["ai_service_url"] = SystemConfig["ai_service_url"] + "&mode=Fast&output=png&target_lang=" + SystemConfig["ai_target_lang"];
                else
                    retroarchConfig["ai_service_url"] = "http://" + "ztranslate.net/service?api_key=BATOCERA&mode=Fast&output=png&target_lang=" + SystemConfig["ai_target_lang"];

                BindBoolFeature(retroarchConfig, "ai_service_pause", "ai_service_pause", "true", "false");
            }
            else
                retroarchConfig["ai_service_enable"] = "false";
        }

        /// <summary>
        ///  Netplay management : netplaymode client -netplayport " + std::to_string(options.port) + " -netplayip
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureNetPlay(ConfigFile retroarchConfig)
        {
            retroarchConfig["netplay_mode"] = "false";

            if (SystemConfig["netplay"] == "true" && !string.IsNullOrEmpty(SystemConfig["netplaymode"]))
            {
                // Security : hardcore mode disables save states, which would kill netplay
                retroarchConfig["cheevos_hardcore_mode_enable"] = "false";

                retroarchConfig["netplay_ip_port"] = SystemConfig["netplay.port"]; // netplayport
                retroarchConfig["netplay_nickname"] = SystemConfig["netplay.nickname"];

                retroarchConfig["netplay_mitm_server"] = SystemConfig["netplay.relay"];
                retroarchConfig["netplay_use_mitm_server"] = string.IsNullOrEmpty(SystemConfig["netplay.relay"]) ? "false" : "true";

                retroarchConfig["netplay_client_swap_input"] = "false";

                if (SystemConfig["netplaymode"] == "client" || SystemConfig["netplaymode"] == "spectator")
                {
                    retroarchConfig["netplay_mode"] = "true";
                    retroarchConfig["netplay_ip_address"] = SystemConfig["netplayip"];
                    retroarchConfig["netplay_ip_port"] = SystemConfig["netplayport"];
                    retroarchConfig["netplay_client_swap_input"] = "true";
                }

                // connect as client
                if (SystemConfig["netplaymode"] == "client")
                {
                    if (SystemConfig.isOptSet("netplaypass"))
                        retroarchConfig["netplay_password"] = SystemConfig["netplaypass"];
                    else
                        retroarchConfig.DisableAll("netplay_password");
                }

                // connect as spectator
                if (SystemConfig["netplaymode"] == "spectator")
                {
                    retroarchConfig["netplay_spectator_mode_enable"] = "true";
                    retroarchConfig["netplay_start_as_spectator"] = "true";

                    if (SystemConfig.isOptSet("netplaypass"))
                        retroarchConfig["netplay_spectate_password"] = SystemConfig["netplaypass"];
                    else
                        retroarchConfig.DisableAll("netplay_spectate_password");
                }
                else if (base.SystemConfig["netplaymode"] == "host-spectator")
                {
                    retroarchConfig["netplay_spectator_mode_enable"] = "true";
                    retroarchConfig["netplay_start_as_spectator"] = "true";
                    retroarchConfig["netplay_mode"] = "false";
                }
                else
                {
                    if (SystemConfig["netplaymode"] != "host")
                        retroarchConfig["netplay_spectator_mode_enable"] = "false";

                    retroarchConfig["netplay_start_as_spectator"] = "false";
                }

                // Netplay host passwords
                if (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator")
                {
                    if (SystemConfig["netplaymode"] == "host")
                        retroarchConfig["netplay_spectator_mode_enable"] = SystemConfig.getOptBoolean("netplay.spectator") ? "true" : "false";

                    retroarchConfig["netplay_password"] = SystemConfig["netplay.password"];
                    retroarchConfig["netplay_spectate_password"] = SystemConfig["netplay.spectatepassword"];
                }

                // Netplay hide the gameplay
                retroarchConfig["netplay_public_announce"] = string.IsNullOrEmpty(SystemConfig["netplay_public_announce"]) ? "true" : SystemConfig["netplay_public_announce"];
                
                // When hosting, if not public announcing, make sure you don't use a mitm server -> It's LAN only
                if (retroarchConfig["netplay_public_announce"] == "false" && (SystemConfig["netplaymode"] == "host" || SystemConfig["netplaymode"] == "host-spectator"))
                    retroarchConfig["netplay_use_mitm_server"] = "false";

                // custom relay server
                if (SystemConfig["netplay.relay"] == "custom" && SystemConfig.isOptSet("netplay.customserver") && !string.IsNullOrEmpty(SystemConfig["netplay.customserver"]))
                    retroarchConfig["netplay_custom_mitm_server"] = SystemConfig["netplay.customserver"];
                else
                    retroarchConfig["netplay_custom_mitm_server"] = "";
            }

            BindBoolFeature(retroarchConfig, "content_show_netplay", "netplay", "true", "false");
        }

        /// <summary>
        /// Retroachievements / Cheevos
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void ConfigureRetroachievements(ConfigFile retroarchConfig)
        {
            if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
            {
                // Since 1.10, token is stored & password is reset
                retroarchConfig.DisableAll("cheevos_token");

                retroarchConfig["cheevos_enable"] = "true";
                retroarchConfig["cheevos_username"] = SystemConfig["retroachievements.username"];
                retroarchConfig["cheevos_password"] = SystemConfig["retroachievements.password"];
                retroarchConfig["cheevos_hardcore_mode_enable"] = SystemConfig.getOptBoolean("retroachievements.hardcore") && _stateFileManager == null ? "true" : "false";
                retroarchConfig["cheevos_leaderboards_enable"] = SystemConfig["retroachievements.leaderboards"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_verbose_enable"] = SystemConfig["retroachievements.verbose"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_auto_screenshot"] = SystemConfig["retroachievements.screenshot"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_challenge_indicators"] = SystemConfig["retroachievements.challenge_indicators"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_start_active"] = SystemConfig["retroachievements.encore"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_richpresence_enable"] = SystemConfig["retroachievements.richpresence"] == "true" ? "true" : "false";
                retroarchConfig["cheevos_test_unofficial"] = SystemConfig["retroachievements.unofficial"] == "true" ? "true" : "false";

                // Unlock sound
                if (AppConfig.isOptSet("retroachievementsounds") && SystemConfig.isOptSet("retroachievements.sound") && !string.IsNullOrEmpty(SystemConfig["retroachievements.sound"]))
                {
                    if (SystemConfig["retroachievements.sound"] != "none")
                    {
                        retroarchConfig["cheevos_unlock_sound_enable"] = "true";
                        string targetSoundPath = Path.Combine(RetroarchPath, "assets", "sounds");
                        if (!string.IsNullOrEmpty(targetSoundPath))
                        {
                            string targetSoundFile = Path.Combine(targetSoundPath, "unlock.ogg");
                            string sourceSoundFile = Path.Combine(AppConfig.GetFullPath("retroachievementsounds"), SystemConfig["retroachievements.sound"] + ".ogg");

                            if (File.Exists(targetSoundFile))
                                File.Delete(targetSoundFile);

                            if (File.Exists(sourceSoundFile))
                                File.Copy(sourceSoundFile, targetSoundFile);
                        }
                    }
                    else
                        retroarchConfig["cheevos_unlock_sound_enable"] = "false";
                }

                else
                    retroarchConfig["cheevos_unlock_sound_enable"] = "false";
            }
            else
                retroarchConfig["cheevos_enable"] = "false";
        }

        /// <summary>
        /// Language
        /// </summary>
        /// <param name="retroarchConfig"></param>
        private void SetLanguage(ConfigFile retroarchConfig)
        {
            Func<string, string> shortLang = new Func<string, string>(s =>
            {
                s = s.ToLowerInvariant();

                int cut = s.IndexOf("_");
                if (cut >= 0)
                    return s.Substring(0, cut);

                return s;
            });

            var lang = SystemConfig["Language"];
            if (string.IsNullOrEmpty(lang))
                lang = "en";
            bool foundLang = false;

            retro_language rl = (retro_language)9999999;
            if (Languages.TryGetValue(lang, out rl))
                foundLang = true;
            else
            {
                lang = shortLang(lang);

                foundLang = Languages.TryGetValue(lang, out rl);
                if (!foundLang)
                {
                    var ret = Languages.Where(l => shortLang(l.Key) == lang).ToList();
                    if (ret.Any())
                    {
                        foundLang = true;
                        rl = ret.First().Value;
                    }
                }
            }

            if (foundLang)
                retroarchConfig["user_language"] = ((int)rl).ToString();
        }

        /// <summary>
        /// Patch Retroarch to display @RETROBAT in netplay architecture
        /// </summary>
        /// <returns></returns>
        private string GetNetPlayPatchedRetroarch()
        {
            string fn = Path.Combine(RetroarchPath, "retroarch.exe");
            if (!File.Exists(fn))
                return fn;

            string patched = Path.Combine(RetroarchPath, "retroarch.patched." + RetroArchNetPlayPatchedName + ".exe");
            if (File.Exists(patched) && new FileInfo(fn).Length == new FileInfo(patched).Length)
                return patched;

            try { File.Delete(patched); }
            catch { }

            var bytes = File.ReadAllBytes(fn);

            var toFind = "username=%s&core_name=%s&core_version=%s&game_name=%s&game_crc=%08lX".Select(c => (byte)c)
                .ToArray();

            int start = bytes.IndexOf(toFind);
            if (start < 0)
                return fn;

            int end = bytes.IndexOf(new byte[] { 0 }, start + toFind.Length);
            if (end < 0)
                return fn;

            byte[] extractedBytes = new byte[end - start]; // Array to hold the extracted bytes
            Array.Copy(bytes, start, extractedBytes, 0, extractedBytes.Length);

            var fullstr = Encoding.UTF8.GetString(extractedBytes);

            toFind = extractedBytes;

            var toSet = toFind.ToArray();
            var toSubst = "&subsystem_name=%s".Select(c => (byte)c).ToArray();
            int idx = toFind.IndexOf(toSubst);
            if (idx < 0)
                return fn;

            int index = bytes.IndexOf(toFind);
            if (index < 0)
                return fn;

            string patchString = "@" + RetroArchNetPlayPatchedName + "&s";

            var toPatch = patchString.Select(c => (byte)c).ToArray();
            for (int i = 0; i < patchString.Length; i++)
                toSet[idx + i] = toPatch[i];

            for (int i = 0; i < toSet.Length; i++)
                bytes[index + i] = toSet[i];

            File.WriteAllBytes(patched, bytes);
            return patched;
        }

        /// <summary>
        /// Bezels
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="systemName"></param>
        /// <param name="rom"></param>
        /// <param name="resolution"></param>
        private void ConfigureBezels(ConfigFile retroarchConfig, string systemName, string rom, string core, ScreenResolution resolution)
        {
            retroarchConfig["input_overlay_hide_in_menu"] = "false";
            retroarchConfig["input_overlay_enable"] = "false";
            retroarchConfig["video_message_pos_x"] = "0.05";
            retroarchConfig["video_message_pos_y"] = "0.05";

            if (systemName == "wii" && (!SystemConfig.isOptSet("ratio")))
                return;

            bool animatedBezel = SystemConfig["bezel"] == "animated";
            var bezelInfo = BezelFiles.GetBezelFiles(systemName, rom, resolution, "libretro");
            if (bezelInfo == null && !animatedBezel)
                return;

            string overlay_png_file = bezelInfo.PngFile;

            Size imageSize;

            try
            {
                imageSize = GetImageSize(overlay_png_file);
            }
            catch
            {
                return;
            }

            BezelInfo infos = bezelInfo.BezelInfos;

            // if image is not at the correct size, find the correct size
            bool bezelNeedAdaptation = false;
            bool viewPortUsed = true;

            if (!infos.IsValid())
                viewPortUsed = false;

            // for testing ->   
            //resolution = ScreenResolution.Parse("2280x1080x32x60");
            //resolution = ScreenResolution.Parse("3840x2160x32x60");                    

            int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
            int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

            float screenRatio = (float)resX / (float)resY;
            float bezelRatio = (float)imageSize.Width / (float)imageSize.Height;

            if (viewPortUsed)
            {
                if (resX != infos.width.GetValueOrDefault() || resY != infos.height.GetValueOrDefault())
                {
                    if (screenRatio < 1.6) // use bezels only for 16:10, 5:3, 16:9 and wider aspect ratios
                        return;
                    else
                        bezelNeedAdaptation = true;
                }

                if (!SystemConfig.isOptSet("ratio"))
                {
                    if (systemName == "mame" || systemName == "fbneo" || infos.IsEstimated)
                        retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString();
                    else
                        retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("custom").ToString(); // overwritten from the beginning of this file                
                }
            }
            else
            {
                // when there is no information about width and height in the .info, assume that the tv is HD 16/9 and infos are core provided
                if (screenRatio < 1.6) // use bezels only for 16:10, 5:3, 16:9 and wider aspect ratios
                    return;

                infos.width = imageSize.Width;
                infos.height = imageSize.Height;
                bezelNeedAdaptation = true;

                if (!SystemConfig.isOptSet("ratio"))
                    retroarchConfig["aspect_ratio_index"] = ratioIndexes.IndexOf("core").ToString(); // overwritten from the beginning of this file
            }

            string animatedBezelPath = null;

            if (animatedBezel)
            {
                animatedBezelPath = Path.Combine(AppConfig.GetFullPath("decorations"), "animated", "systems", systemName, systemName + ".cfg");

                if (!File.Exists(animatedBezelPath))
                {
                    animatedBezelPath = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "decorations", "animated", "systems", systemName, systemName + ".cfg");
                    if (!File.Exists(animatedBezelPath))
                        animatedBezel = false;
                }
            }

            string overlay_cfg_file = Path.Combine(RetroarchPath, "custom-overlay.cfg");

            retroarchConfig["input_overlay_enable"] = "true";
            retroarchConfig["input_overlay_scale_landscape"] = "1.0";
            retroarchConfig["input_overlay_scale_portrait"] = "1.0";
            retroarchConfig["input_overlay"] = animatedBezel ? animatedBezelPath : overlay_cfg_file;
            retroarchConfig["input_overlay_hide_in_menu"] = "true";

            if (!infos.opacity.HasValue)
                infos.opacity = 1.0f;
            if (!infos.messagex.HasValue)
                infos.messagex = 0.0f;
            if (!infos.messagey.HasValue)
                infos.messagey = 0.0f;

            retroarchConfig["input_overlay_opacity"] = infos.opacity.ToString().Replace(",", "."); // "1.0";
            // for testing : retroarchConfig["input_overlay_opacity"] = "0.5";

            if (bezelNeedAdaptation)
            {
                float wratio = resX / (float)infos.width;
                float hratio = resY / (float)infos.height;

                int xoffset = resX - infos.width.Value;
                int yoffset = resY - infos.height.Value;

                bool stretchImage = false;

                if (resX < infos.width || resY < infos.height) // If width or height < original, can't add black borders. Just stretch
                    stretchImage = true;
                else if (Math.Abs(screenRatio - bezelRatio) < 0.2) // FCA : About the same ratio ? Just stretch
                    stretchImage = true;

                if (viewPortUsed)
                {
                    if (stretchImage)
                    {
                        retroarchConfig["custom_viewport_x"] = ((int)(infos.left * wratio)).ToString();
                        retroarchConfig["custom_viewport_y"] = ((int)(infos.top * hratio)).ToString();
                        retroarchConfig["custom_viewport_width"] = ((int)((infos.width - infos.left - infos.right) * wratio)).ToString();
                        retroarchConfig["custom_viewport_height"] = ((int)((infos.height - infos.top - infos.bottom) * hratio)).ToString();
                        retroarchConfig["video_message_pos_x"] = (infos.messagex.Value * wratio).ToString(CultureInfo.InvariantCulture);
                        retroarchConfig["video_message_pos_y"] = (infos.messagey.Value * hratio).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        retroarchConfig["custom_viewport_x"] = ((int)(infos.left + xoffset / 2)).ToString();
                        retroarchConfig["custom_viewport_y"] = ((int)(infos.top + yoffset / 2)).ToString();
                        retroarchConfig["custom_viewport_width"] = ((int)((infos.width - infos.left - infos.right))).ToString();
                        retroarchConfig["custom_viewport_height"] = ((int)((infos.height - infos.top - infos.bottom))).ToString();
                        retroarchConfig["video_message_pos_x"] = (infos.messagex.Value + xoffset / 2).ToString(CultureInfo.InvariantCulture);
                        retroarchConfig["video_message_pos_y"] = (infos.messagey.Value + yoffset / 2).ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (!stretchImage)
                    overlay_png_file = BezelFiles.GetStretchedBezel(overlay_png_file, resX, resY);
            }
            else
            {
                if (viewPortUsed)
                {
                    retroarchConfig["custom_viewport_x"] = infos.left.Value.ToString();
                    retroarchConfig["custom_viewport_y"] = infos.top.Value.ToString();
                    retroarchConfig["custom_viewport_width"] = (infos.width.Value - infos.left.Value - infos.right.Value).ToString();
                    retroarchConfig["custom_viewport_height"] = (infos.height.Value - infos.top.Value - infos.bottom.Value).ToString();
                }

                retroarchConfig["video_message_pos_x"] = infos.messagex.Value.ToString(CultureInfo.InvariantCulture);
                retroarchConfig["video_message_pos_y"] = infos.messagey.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (retroarchConfig["video_fullscreen"] != "true")
                retroarchConfig["input_overlay_show_mouse_cursor"] = "true";
            else
                retroarchConfig["input_overlay_show_mouse_cursor"] = "false";

            StringBuilder fd = new StringBuilder();
            fd.AppendLine("overlays = 1");
            fd.AppendLine("overlay0_overlay = \"" + overlay_png_file + "\"");
            fd.AppendLine("overlay0_full_screen = true");
            fd.AppendLine("overlay0_descs = 0");
            File.WriteAllText(overlay_cfg_file, fd.ToString());

            if (retroarchConfig["aspect_ratio_index"] == ratioIndexes.IndexOf("custom").ToString())
                _bias = false;

            if (_bias)
            {
                retroarchConfig["video_viewport_bias_x"] = "0.500000";
                retroarchConfig["video_viewport_bias_y"] = "0.500000";
            }
            else
            {
                retroarchConfig["video_viewport_bias_x"] = "0.000000";

                if (_forcenobias)
                    retroarchConfig["video_viewport_bias_y"] = "0.000000";
                else if (driverYBias.Contains(_video_driver) || _forceBias)
                    retroarchConfig["video_viewport_bias_y"] = "1.000000";
                else
                    retroarchConfig["video_viewport_bias_y"] = "0.000000";
            }
        }

        private static Size GetImageSize(string file)
        {
            using (Image img = Image.FromFile(file))
                return img.Size;
        }

        private static bool IsVersionAtLeast(Version ver)
        {
            var ist = Installer.GetInstaller("libretro");
            if (ist != null)
            {
                var local = ist.GetInstalledVersion();
                return (!string.IsNullOrEmpty(local) && Version.Parse(local) >= ver);
            }

            return false;
        }
        
        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_noHotkey)
            {
                SimpleLogger.Instance.Info("[GENERATOR] No hotkey defined, adding select + start to exit in padtokey.");
                return PadToKey.AddOrUpdateKeyMapping(mapping, "retroarch", InputKey.select | InputKey.start, "(%{CLOSE})");
            }
            else
                return mapping;
        }

        public override void Cleanup()
        {
            if (SystemConfig["core"] == "atari800")
                Environment.SetEnvironmentVariable("HOME", CurrentHomeDirectory);

            if (_dosBoxTempRom != null && File.Exists(_dosBoxTempRom))
                File.Delete(_dosBoxTempRom);

            if (_screenShotWatcher != null)
            {
                _screenShotWatcher.Dispose();
                _screenShotWatcher = null;
            }

            if (_stateFileManager != null)
            {
                _stateFileManager.Dispose();
                _stateFileManager = null;
            }

            if (_sindenSoft)
                Guns.KillSindenSoftware();

            // Kill java processes as there is a bug where sound continues even when retroarch is closed
            if (SystemConfig["core"] == "freej2me")
            {
                var px = Process.GetProcessesByName("javaw");
                foreach (var p in px)
                {
                    try
                    {
                        p.Kill();
                    }
                    catch { }
                }
            }

            base.Cleanup();
        }

        class UIModeSetting
        {
            public UIModeSetting(string name, string minimal, string recommanded, string full)
            {
                Name = name;
                Minimal = minimal;
                Recommanded = recommanded;
                Full = full;
            }

            public string Name { get; private set; }
            public string Minimal { get; private set; }
            public string Recommanded { get; private set; }
            public string Full { get; private set; }

            public string GetValue(UIModeType type)
            {
                if (type == UIModeType.Minimal)
                    return Minimal;

                if (type == UIModeType.Recommanded)
                    return Recommanded;

                return Full;
            }
        }

        enum UIModeType
        {
            Minimal,
            Recommanded,
            Full
        }

        static UIModeSetting[] UIModes = new UIModeSetting[]
        {
            new UIModeSetting("desktop_menu_enable", "false", "false", "true"),
            new UIModeSetting("content_show_add", "false", "false", "true"),
            new UIModeSetting("content_show_contentless_cores", "0", "0", "1"),
            new UIModeSetting("content_show_explore", "false", "false", "true"),
            new UIModeSetting("content_show_favorites", "false", "false", "true"),
            new UIModeSetting("content_show_history", "false", "true", "true"),
            new UIModeSetting("content_show_images", "false", "false", "true"),
            new UIModeSetting("content_show_music", "false", "false", "true"),
            new UIModeSetting("content_show_netplay", "false", "true", "true"),
            new UIModeSetting("content_show_playlists", "false", "false", "true"),
            new UIModeSetting("content_show_video", "false", "false", "true"),
            new UIModeSetting("menu_show_advanced_settings", "false", "false", "true"),
            new UIModeSetting("menu_show_configurations", "false", "false", "true"),
            new UIModeSetting("menu_show_core_updater", "false", "false", "true"),
            new UIModeSetting("menu_show_dump_disc", "false", "false", "true"),
            new UIModeSetting("menu_show_help", "false", "true", "true"),
            new UIModeSetting("menu_show_information", "false", "true", "true"),
            new UIModeSetting("menu_show_latency", "false", "true", "true"),
            new UIModeSetting("menu_show_legacy_thumbnail_updater", "false", "false", "true"),
            new UIModeSetting("menu_show_load_content", "false", "false", "true"),
            new UIModeSetting("menu_show_load_core", "false", "false", "true"),
            new UIModeSetting("menu_show_load_disc", "false", "false", "true"),
            new UIModeSetting("menu_show_online_updater", "false", "true", "true"),
            new UIModeSetting("menu_show_overlays", "false", "false", "true"),
            new UIModeSetting("menu_show_reboot", "false", "true", "true"),
            new UIModeSetting("menu_show_restart_retroarch", "false", "false", "true"),
            new UIModeSetting("menu_show_rewind", "false", "true", "true"),
            new UIModeSetting("menu_show_shutdown", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_add_to_favorites", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_cheats", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_close_content", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_controls", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_core_options_flush", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_download_thumbnails", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_options", "false", "true", "true"),          
            new UIModeSetting("quick_menu_show_reset_core_association", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_restart_content", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_save_content_dir_overrides", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_save_core_overrides", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_save_game_overrides", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_set_core_association", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_shaders", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_start_recording", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_start_streaming", "false", "false", "true"),
            new UIModeSetting("quick_menu_show_take_screenshot", "false", "true", "true"),
            new UIModeSetting("quick_menu_show_undo_save_load_state", "false", "false", "true"),
            new UIModeSetting("settings_show_ai_service", "false", "true", "true"),
            new UIModeSetting("settings_show_audio", "false", "true", "true"),
            new UIModeSetting("settings_show_configuration", "false", "true", "true"),
            new UIModeSetting("settings_show_directory", "false", "false", "true"),
            new UIModeSetting("settings_show_drivers", "false", "true", "true"),
            new UIModeSetting("settings_show_file_browser", "false", "false", "true"),
            new UIModeSetting("settings_show_frame_throttle", "false", "true", "true"),
            new UIModeSetting("settings_show_input", "false", "true", "true"),
            new UIModeSetting("settings_show_latency", "false", "true", "true"),
            new UIModeSetting("settings_show_logging", "false", "true", "true"),
            new UIModeSetting("settings_show_network", "false", "true", "true"),
            new UIModeSetting("settings_show_onscreen_display", "false", "true", "true"),
            new UIModeSetting("settings_show_playlists", "false", "false", "true"),
            new UIModeSetting("settings_show_power_management", "false", "true", "true"),
            new UIModeSetting("settings_show_recording", "false", "true", "true"),
            new UIModeSetting("settings_show_saving", "false", "true", "true"),
            new UIModeSetting("settings_show_user", "false", "true", "true"),
            new UIModeSetting("settings_show_user_interface", "false", "true", "true"),
            new UIModeSetting("settings_show_video", "false", "true", "true"),
            new UIModeSetting("kiosk_mode_enable", "true", "false", "false")
        };

        // Retroarch menu : different level of options appearing or not in the retroarch menu
        private void SetupUIMode(ConfigFile retroarchConfig)
        {
            UIModeType type = UIModeType.Recommanded;

            if (SystemConfig["UIMode"] == "Kid" || SystemConfig["UIMode"] == "Kiosk" || SystemConfig["OptionsMenu"] == "minimal")
                type = UIModeType.Minimal;
            else if (SystemConfig["OptionsMenu"] == "full")
                type = UIModeType.Full;

            foreach(var item in UIModes)
                retroarchConfig[item.Name] = item.GetValue(type);
        }

        // List and dictionaries
        static List<string> ratioIndexes = new List<string> { "4/3", "16/9", "16/10", "16/15", "21/9", "1/1", "2/1", "3/2", "3/4", "4/1", "4/4", "5/4", "6/5", "7/9", "8/3",
                "8/7", "19/12", "19/14", "30/17", "32/9", "config", "squarepixel", "core", "custom", "full" };
        static List<string> systemNoRewind = new List<string>() { "dice", "nds", "3ds", "sega32x", "wii", "gamecube", "gc", "psx", "zxspectrum", "odyssey2", "n64", "dreamcast", "atomiswave", "naomi", "naomi2", "neogeocd", "saturn", "mame", "hbmame", "fbneo", "dos", "scummvm", "psp" };
        static List<string> systemNoRunahead = new List<string>() { "dice", "nds", "3ds", "sega32x", "wii", "gamecube", "n64", "dreamcast", "atomiswave", "naomi", "naomi2", "neogeocd", "saturn" };
        static List<string> coreNoPreemptiveFrames = new List<string>() { "2048", "4do", "81", "atari800", "bluemsx", "bsnes", "bsnes-jg", "bsnes_hd_beta", "cannonball", "cap32", "citra", "craft", "crocods", "desmume", "desmume2015", "dice", "dolphin", "dosbox_pure", "easyrpg", "fbalpha2012_cps1", "fbalpha2012_cps2", "fbalpha2012_cps3", "flycast", "frodo", "gw", "handy", "hatari", "hatarib", "imageviewer", "kronos", "lutro", "mame2000", "mame2003", "mame2003_plus", "mame2003_midway", "mame2010", "mame2014", "mame2016", "mednafen_psx_hw", "mednafen_snes", "mupen64plus_next", "nekop2", "nestopia", "np2kai", "nxengine", "o2em", "opera", "parallel_n64", "pcsx2", "ppsspp", "prboom", "prosystem", "puae", "px68k", "race", "retro8", "sameduck", "same_cdi", "scummvm", "swanstation", "theodore", "tic80", "tyrquake", "vice_x128", "vice_x64", "vice_x64sc", "vice_xpet", "vice_xplus4", "vice_xvic", "vecx", "virtualjaguar" };
        static List<string> capsimgCore = new List<string>() { "hatari", "hatarib", "puae" };
        static List<string> hdrCompatibleVideoDrivers = new List<string>() { "d3d12", "d3d11", "vulkan" };
        static List<string> coreNoGL = new List<string>() { "citra", "kronos", "mednafen_psx", "mednafen_psx_hw", "pcsx2", "swanstation" };
        static List<string> driverYBias = new List<string>() { "gl", "glcore" };
        static List<string> CoreSaveSort = new List<string>() { "dolphin" };
        static Dictionary<string, string> coreToP1Device = new Dictionary<string, string>() { { "atari800", "513" }, { "cap32", "513" }, { "fuse", "513" } };
        static Dictionary<string, string> coreToP2Device = new Dictionary<string, string>() { { "atari800", "513" }, { "fuse", "513" } };
        static Dictionary<string, string> defaultVideoDriver = new Dictionary<string, string>() 
        { 
            { "flycast", "vulkan" },
            { "melondsds", "glcore" },
            { "pcsx2", "glcore" }
        };
        static Dictionary<string, retro_language> Languages = new Dictionary<string, retro_language>()
        {
            {"en", retro_language.RETRO_LANGUAGE_ENGLISH},
            {"ja", retro_language.RETRO_LANGUAGE_JAPANESE},
            {"fr", retro_language.RETRO_LANGUAGE_FRENCH},
            {"es", retro_language.RETRO_LANGUAGE_SPANISH},
            {"de", retro_language.RETRO_LANGUAGE_GERMAN},
            {"it", retro_language.RETRO_LANGUAGE_ITALIAN},
            {"nl", retro_language.RETRO_LANGUAGE_DUTCH},
            {"pt_BR", retro_language.RETRO_LANGUAGE_PORTUGUESE_BRAZIL},
            {"pt_PT", retro_language.RETRO_LANGUAGE_PORTUGUESE_PORTUGAL},
            {"pt", retro_language.RETRO_LANGUAGE_PORTUGUESE_PORTUGAL},
            {"ru", retro_language.RETRO_LANGUAGE_RUSSIAN},
            {"ko", retro_language.RETRO_LANGUAGE_KOREAN},
            {"zh_CN", retro_language.RETRO_LANGUAGE_CHINESE_SIMPLIFIED},
            {"zh_SG", retro_language.RETRO_LANGUAGE_CHINESE_SIMPLIFIED},
            {"zh_HK", retro_language.RETRO_LANGUAGE_CHINESE_TRADITIONAL},
            {"zh_TW", retro_language.RETRO_LANGUAGE_CHINESE_TRADITIONAL},
            {"zh", retro_language.RETRO_LANGUAGE_CHINESE_SIMPLIFIED},
            {"eo", retro_language.RETRO_LANGUAGE_ESPERANTO},
            {"pl", retro_language.RETRO_LANGUAGE_POLISH},
            {"vi", retro_language.RETRO_LANGUAGE_VIETNAMESE},
            {"ar", retro_language.RETRO_LANGUAGE_ARABIC},
            {"el", retro_language.RETRO_LANGUAGE_GREEK},
            {"ca", retro_language.RETRO_LANGUAGE_CATALAN},
            {"cs", retro_language.RETRO_LANGUAGE_CZECH},
            {"en_GB", retro_language.RETRO_LANGUAGE_BRITISH_ENGLISH},
            {"fi", retro_language.RETRO_LANGUAGE_FINNISH},
            {"hu", retro_language.RETRO_LANGUAGE_HUNGARIAN},
            {"he", retro_language.RETRO_LANGUAGE_HEBREW},
            {"id", retro_language.RETRO_LANGUAGE_INDONESIAN},
            {"sk", retro_language.RETRO_LANGUAGE_SLOVAK},
            {"sv", retro_language.RETRO_LANGUAGE_SWEDISH},
            {"tr", retro_language.RETRO_LANGUAGE_TURKISH},
            {"uk_UA", retro_language.RETRO_LANGUAGE_UKRAINIAN}
        };
    }

    // https://github.com/libretro/RetroArch/blob/master/libretro-common/include/libretro.h#L260
    enum retro_language
    {
        RETRO_LANGUAGE_ENGLISH = 0,
        RETRO_LANGUAGE_JAPANESE = 1,
        RETRO_LANGUAGE_FRENCH = 2,
        RETRO_LANGUAGE_SPANISH = 3,
        RETRO_LANGUAGE_GERMAN = 4,
        RETRO_LANGUAGE_ITALIAN = 5,
        RETRO_LANGUAGE_DUTCH = 6,
        RETRO_LANGUAGE_PORTUGUESE_BRAZIL = 7,
        RETRO_LANGUAGE_PORTUGUESE_PORTUGAL = 8,
        RETRO_LANGUAGE_RUSSIAN = 9,
        RETRO_LANGUAGE_KOREAN = 10,
        RETRO_LANGUAGE_CHINESE_TRADITIONAL = 11,
        RETRO_LANGUAGE_CHINESE_SIMPLIFIED = 12,
        RETRO_LANGUAGE_ESPERANTO = 13,
        RETRO_LANGUAGE_POLISH = 14,
        RETRO_LANGUAGE_VIETNAMESE = 15,
        RETRO_LANGUAGE_ARABIC = 16,
        RETRO_LANGUAGE_GREEK = 17,
        RETRO_LANGUAGE_TURKISH = 18,
        RETRO_LANGUAGE_SLOVAK = 19,
        RETRO_LANGUAGE_PERSIAN = 20,
        RETRO_LANGUAGE_HEBREW = 21,
        RETRO_LANGUAGE_ASTURIAN = 22,
        RETRO_LANGUAGE_FINNISH = 23,
        RETRO_LANGUAGE_INDONESIAN = 24,
        RETRO_LANGUAGE_SWEDISH = 25,
        RETRO_LANGUAGE_UKRAINIAN = 26,
        RETRO_LANGUAGE_CZECH = 27,
        RETRO_LANGUAGE_CATALAN_VALENCIA = 28,
        RETRO_LANGUAGE_CATALAN = 29,
        RETRO_LANGUAGE_BRITISH_ENGLISH = 30,
        RETRO_LANGUAGE_HUNGARIAN = 31//,
        //      RETRO_LANGUAGE_LAST,

        /* Ensure sizeof(enum) == sizeof(int) */
        //        RETRO_LANGUAGE_DUMMY = INT_MAX
    };

    class SubSystem
    {
        static public List<SubSystem> subSystems = new List<SubSystem>()
        {
            new SubSystem("fbneo", "colecovision", "cv"),

            new SubSystem("fbneo", "msx", "msx"),                        
            new SubSystem("fbneo", "msx1", "msx"),

            new SubSystem("fbneo", "supergrafx", "sgx"),
            new SubSystem("fbneo", "pcengine", "pce"),
            new SubSystem("fbneo", "pcenginecd", "pce"),

            new SubSystem("fbneo", "turbografx", "tg"),
            new SubSystem("fbneo", "turbografx16", "tg"),
            
            new SubSystem("fbneo", "gamegear", "gg"),
            new SubSystem("fbneo", "mastersystem", "sms"),
            new SubSystem("fbneo", "megadrive", "md"),

            new SubSystem("fbneo", "sg1000", "sg1k"),
            new SubSystem("fbneo", "sg-1000", "sg1k"),
            
            new SubSystem("fbneo", "zxspectrum", "spec"),

            new SubSystem("fbneo", "neogeocd", "neocd")            
        };

        public static string GetSubSystem(string core, string system)
        {
            var sub = subSystems.FirstOrDefault(s => s.Core.Equals(core, StringComparison.InvariantCultureIgnoreCase) && s.System.Equals(system, StringComparison.InvariantCultureIgnoreCase));
            if (sub != null)
                return sub.SubSystemId;

            return null;
        }

        public SubSystem(string core, string system, string subSystem)
        {
            System = system;
            Core = core;
            SubSystemId = subSystem;
        }

        public string System { get; set; }
        public string Core { get; set; }
        public string SubSystemId { get; set; }
    }
}
