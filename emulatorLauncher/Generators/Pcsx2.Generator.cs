using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Management;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class Pcsx2Generator : Generator
    {
        public Pcsx2Generator()
        {
            DependsOnDesktopResolution = true;
        }

        private SaveStatesWatcher _saveStatesWatcher;
        private string _path;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _isPcsx17;
        private bool _isPcsxqt;

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            //Define paths, search first based on emulator name
            string folderName = emulator;

            _path = AppConfig.GetFullPath(folderName);

            //If path does not exist try pcsx2 1.6 path
            if (string.IsNullOrEmpty(_path))
                _path = AppConfig.GetFullPath("pcsx2-16");

            //search first for qt version .exe, if found also set bool _isPcsxqt to true for later steps
            string sse4exe = Path.Combine(_path, "pcsx2-qtx64.exe"); // v1.7 SSE4-QT filename
            string avx2exe = Path.Combine(_path, "pcsx2-qtx64-avx2.exe"); // v1.7 AVX2-QT filename
            string exe = Path.Combine(_path, "pcsx2-qt.exe"); // v1.7 new-QT filename
            
            // Define QT executable file to use (default is the new pcsx2-QT executable, but not everybody might have upgraded
            if (File.Exists(exe) || File.Exists(sse4exe) || File.Exists(avx2exe))
            {
                _isPcsxqt = true;

                // If new pcsx2-QT.exe does not exist, default to SSE4
                if (!File.Exists(exe) || core == "pcsx2-sse4" || core == "sse4")
                {
                    if (File.Exists(sse4exe))
                        exe = sse4exe;
                }

                // AVX2 version when AVX2 core is forced
                if (core == "pcsx2-avx2" || core == "avx2")
                {
                    if (File.Exists(avx2exe))
                        exe = avx2exe;
                }
            }

            // For these still with wxwidgets version
            else if (!File.Exists(exe))
            {
                exe = Path.Combine(_path, "pcsx2x64.exe"); // v1.7 filename 
                if (!File.Exists(exe))
                    exe = Path.Combine(_path, "pcsx2.exe"); // v1.6 filename            
            }

            // v1.7.0 wxwidgets ?
            Version version = new Version();
            if (!_isPcsxqt && Version.TryParse(FileVersionInfo.GetVersionInfo(exe).ProductVersion, out version))
                _isPcsx17 = version >= new Version(1, 7, 0, 0);

            // Select avx2 build for 1.7 wxwidgets version
            if (!_isPcsxqt && _isPcsx17 && (core == "pcsx2-avx2" || core == "avx2"))
            {
                string avx2 = Path.Combine(_path, "pcsx2x64-avx2.exe");

                if (!File.Exists(avx2))
                    avx2 = Path.Combine(_path, "pcsx2-avx2.exe");

                if (File.Exists(avx2))
                {
                    exe = avx2;

                    if (Version.TryParse(FileVersionInfo.GetVersionInfo(exe).ProductVersion, out version))
                        _isPcsx17 = version >= new Version(1, 7, 0, 0);
                }
            }

            if (!File.Exists(exe))
                return null;
            
            String path = AppConfig.GetFullPath(emulator);

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // Configuration files
            // QT version has now only 1 ini file versus multiple for wxwidgets version
            if (_isPcsxqt)
                SetupConfigurationQT(path, rom, system, fullscreen);

            else
            {
                SetupPaths(system, emulator, core, fullscreen);
                SetupVM();
                SetupLilyPad();
                SetupGSDx(resolution);
            }

            File.WriteAllText(Path.Combine(_path, "portable.ini"), "RunWizard=0");

            //Applying bezels
            if (!SystemConfig.isOptSet("ratio") || SystemConfig["ratio"] == "4:3")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            //setting up command line parameters
            var commandArray = new List<string>();

            if (_isPcsxqt)
            {
                commandArray.Add("-batch");
                commandArray.Add("-nogui");

                if (SystemConfig.isOptSet("pcsx2_startbios") && SystemConfig.getOptBoolean("pcsx2_startbios"))
                {
                    commandArray.Add("-bios");
                    string argsBios = string.Join(" ", commandArray);
                    return new ProcessStartInfo()
                    {
                        FileName = exe,
                        WorkingDirectory = _path,
                        Arguments = argsBios,
                    };
                }

                if (SystemConfig.isOptSet("bigpicture") && SystemConfig.getOptBoolean("bigpicture"))
                {
                    if (!SystemConfig.getOptBoolean("disable_fullscreen") || SystemConfig.getOptBoolean("forcefullscreen"))
                        commandArray.Add("-fullscreen");
                    commandArray.Add("-bigpicture");
                }

                if (SystemConfig.isOptSet("fullboot") && SystemConfig.getOptBoolean("fullboot"))
                    commandArray.Add("-slowboot");
            }
            else 
            {
                commandArray.Add("--portable");

                if (fullscreen)
                    commandArray.Add("--fullscreen");

                commandArray.Add("--nogui");

                if (SystemConfig.isOptSet("fullboot") && SystemConfig.getOptBoolean("fullboot"))
                    commandArray.Add("--fullboot");
            }

            commandArray.Add("\"" + rom + "\"");

            if (File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("-statefile");
                commandArray.Add("\"" + Path.GetFullPath(SystemConfig["state_file"]) + "\"");
            }

            string args = string.Join(" ", commandArray);

            //start emulator
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args,
            };
        }

        #region wxwidgets version
        private void SetupPaths(string system, string emulator, string core, bool fullscreen)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            var biosList = new string[] { 
                            "SCPH30004R.bin", "SCPH30004R.MEC", "scph39001.bin", "scph39001.MEC", 
                            "SCPH-39004_BIOS_V7_EUR_160.BIN", "SCPH-39001_BIOS_V7_USA_160.BIN", "SCPH-70000_BIOS_V12_JAP_200.BIN" };

            string iniFile = Path.Combine(_path, "inis", "PCSX2_ui.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    string biosPath = AppConfig.GetFullPath("bios");
                    string cheatsPath = AppConfig.GetFullPath("cheats");
                    if (!string.IsNullOrEmpty(biosPath))
                    {
                        ini.WriteValue("Folders", "UseDefaultBios", "disabled");

                        if (biosList.Any(b => File.Exists(Path.Combine(biosPath, "pcsx2", "bios", b))))
                            ini.WriteValue("Folders", "Bios", Path.Combine(biosPath, "pcsx2", "bios").Replace("\\", "\\\\"));
                        else
                            ini.WriteValue("Folders", "Bios", biosPath.Replace("\\", "\\\\"));

                        ini.WriteValue("Folders", "UseDefaultCheats", "disabled");
                        ini.WriteValue("Folders", "Cheats", Path.Combine(cheatsPath, "pcsx2", "cheats").Replace("\\", "\\\\"));
                        ini.WriteValue("Folders", "UseDefaultCheatsWS", "disabled");
                        ini.WriteValue("Folders", "CheatsWS", Path.Combine(cheatsPath, "pcsx2", "cheats_ws").Replace("\\", "\\\\"));
                    }

                    string savesPath = AppConfig.GetFullPath("saves");
                    if (!string.IsNullOrEmpty(savesPath))
                    {
                        savesPath = Path.Combine(savesPath, "ps2", Path.GetFileName(_path));

                        if (!Directory.Exists(savesPath))
                            try { Directory.CreateDirectory(savesPath); }
                            catch { }

                        ini.WriteValue("Folders", "UseDefaultSavestates", "disabled");
                        ini.WriteValue("Folders", "Savestates", savesPath.Replace("\\", "\\\\") + "\\\\" + "sstates");

                        ini.WriteValue("Folders", "UseDefaultMemoryCards", "disabled");
                        ini.WriteValue("Folders", "MemoryCards", savesPath.Replace("\\", "\\\\") + "\\\\" + "memcards");
                    }

                    string screenShotsPath = AppConfig.GetFullPath("screenshots");
                    if (!string.IsNullOrEmpty(screenShotsPath))
                    {

                        ini.WriteValue("Folders", "UseDefaultSnapshots", "disabled");
                        ini.WriteValue("Folders", "Snapshots", screenShotsPath.Replace("\\", "\\\\") + "\\\\" + "pcsx2");
                    }

                    if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                        ini.WriteValue("GSWindow", "AspectRatio", SystemConfig["ratio"]);
                    else
                        ini.WriteValue("GSWindow", "AspectRatio", "4:3");

                    if (SystemConfig.isOptSet("fmv_ratio") && !string.IsNullOrEmpty(SystemConfig["fmv_ratio"]))
                        ini.WriteValue("GSWindow", "FMVAspectRatioSwitch", SystemConfig["fmv_ratio"]);
                    else if (Features.IsSupported("fmv_ratio"))
                        ini.WriteValue("GSWindow", "FMVAspectRatioSwitch", "Off");

                    ini.WriteValue("ProgramLog", "Visible", "disabled");
                    ini.WriteValue("GSWindow", "IsFullscreen", fullscreen ? "enabled" : "disabled");

                    if (Features.IsSupported("negdivhack") && SystemConfig.isOptSet("negdivhack") && SystemConfig.getOptBoolean("negdivhack"))
                        ini.WriteValue(null, "EnablePresets", "disabled");

                    if (!_isPcsx17)
                    {
                        ini.WriteValue("Filenames", "PAD", "LilyPad.dll");

                        // Enabled for <= 1.6.0
                        if (Features.IsSupported("gs_plugin"))
                        {
                            if (SystemConfig.isOptSet("gs_plugin") && !string.IsNullOrEmpty(SystemConfig["gs_plugin"]))
                                ini.WriteValue("Filenames", "GS", SystemConfig["gs_plugin"]);
                            else
                                ini.WriteValue("Filenames", "GS", "GSdx32-SSE2.dll");
                        }

                        foreach (var key in new string[] { "SPU2", "CDVD", "USB", "FW", "DEV9", "BIOS"})
                        {
                            string value = ini.GetValue("Filenames", key);
                            if (value == null || value == "Please Configure")
                            {
                                switch (key)
                                {
                                    case "SPU2":
                                        value = "Spu2-X.dll";
                                        break;
                                    case "CDVD":
                                        value = "cdvdGigaherz.dll";
                                        break;
                                    case "USB":
                                        value = "USBnull.dll";
                                        break;
                                    case "FW":
                                        value = "FWnull.dll";
                                        break;
                                    case "DEV9":
                                        value = "DEV9null.dll";
                                        break;
                                    case "BIOS":

                                        var biosFile = biosList.FirstOrDefault(b => File.Exists(Path.Combine(biosPath, "pcsx2", "bios", b)));
                                        if (!string.IsNullOrEmpty(biosFile))
                                            value = biosFile;
                                        else
                                            value = "SCPH30004R.bin";

                                        break;
                                }

                                ini.WriteValue("Filenames", key, value);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void SetupLilyPad()
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            if (_isPcsx17)
                return; // Keyboard Mode 1 is not supported anymore

            string iniFile = Path.Combine(_path, "inis", _isPcsx17 ? "PAD.ini" : "LilyPad.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                    ini.WriteValue("General Settings", "Keyboard Mode", "1");
            }
            catch { }
        }

        //Setup PCSX2_vm.ini file (both 1.6 & 1.7)
        private void SetupVM()
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            string iniFile = Path.Combine(_path, "inis", "PCSX2_vm.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    if (_isPcsx17)
                        ini.WriteValue("EmuCore", "EnableRecordingTools", "disabled");

                    if (!string.IsNullOrEmpty(SystemConfig["VSync"]))
                        ini.WriteValue("EmuCore/GS", "VsyncEnable", SystemConfig["VSync"]);
                    else
                        ini.WriteValue("EmuCore/GS", "VsyncEnable", "1");

                    if (Features.IsSupported("negdivhack"))
                    {
                        string negdivhack = SystemConfig.isOptSet("negdivhack") && SystemConfig.getOptBoolean("negdivhack") ? "enabled" : "disabled";

                        ini.WriteValue("EmuCore/Speedhacks", "vuThread", negdivhack);

                        ini.WriteValue("EmuCore/CPU/Recompiler", "vuExtraOverflow", negdivhack);
                        ini.WriteValue("EmuCore/CPU/Recompiler", "vuSignOverflow", negdivhack);
                        ini.WriteValue("EmuCore/CPU/Recompiler", "fpuExtraOverflow", negdivhack);
                        ini.WriteValue("EmuCore/CPU/Recompiler", "fpuFullMode", negdivhack);

                        ini.WriteValue("EmuCore/Gamefixes", "VuClipFlagHack", negdivhack);
                        ini.WriteValue("EmuCore/Gamefixes", "FpuNegDivHack", negdivhack);
                    }
                }
            }
            catch { }
        }

        //Setup GS.ini (v1.7) and GSdx.ini (v1.6) 
        private void SetupGSDx(ScreenResolution resolution)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            string iniFile = Path.Combine(_path, "inis", _isPcsx17 ? "GS.ini" : "GSdx.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {                
                    //Activate user hacks - valid for 1.6 and 1.7 - default activation if a hack is enabled later  
                    if ((SystemConfig.isOptSet("UserHacks") && !string.IsNullOrEmpty(SystemConfig["UserHacks"])))
                        ini.WriteValue("Settings", "UserHacks", SystemConfig["UserHacks"]);
                    else if (_isPcsx17)
                        ini.WriteValue("Settings", "UserHacks", "0");

                    //Resolution upscale (both 1.6 & 1.7)
                    if (!string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                        ini.WriteValue("Settings", "upscale_multiplier", SystemConfig["internalresolution"]);
                    else
                        ini.WriteValue("Settings", "upscale_multiplier", "1");

                    if (string.IsNullOrEmpty(SystemConfig["internalresolution"]) || SystemConfig["internalresolution"] == "0")
                    {
                        if (resolution != null)
                        {
                            ini.WriteValue("Settings", "resx", resolution.Width.ToString());
                            ini.WriteValue("Settings", "resy", (resolution.Height * 2).ToString());
                        }
                        else
                        {
                            ini.WriteValue("Settings", "resx", Screen.PrimaryScreen.Bounds.Width.ToString());
                            ini.WriteValue("Settings", "resy", (Screen.PrimaryScreen.Bounds.Height * 2).ToString());
                        }
                    }

                    //Enable External Shader (both 1.6 & 1.7)
                    ini.WriteValue("Settings", "shaderfx", "1");

                    //TVShader (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("TVShader") && !string.IsNullOrEmpty(SystemConfig["TVShader"]))
                        ini.WriteValue("Settings", "TVShader", SystemConfig["TVShader"]);
                    else if (Features.IsSupported("TVShader"))
                        ini.WriteValue("Settings", "TVShader", "0");

                    //Wild Arms offset (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("Offset") && !string.IsNullOrEmpty(SystemConfig["Offset"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_WildHack", SystemConfig["Offset"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("Offset"))
                        ini.WriteValue("Settings", "UserHacks_WildHack", "0");

                    //Half Pixel Offset (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("UserHacks_HalfPixelOffset") && !string.IsNullOrEmpty(SystemConfig["UserHacks_HalfPixelOffset"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_HalfPixelOffset", SystemConfig["UserHacks_HalfPixelOffset"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("Offset"))
                        ini.WriteValue("Settings", "UserHacks_HalfPixelOffset", "0");

                    //Half-screen fix (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("UserHacks_Half_Bottom_Override") && !string.IsNullOrEmpty(SystemConfig["UserHacks_Half_Bottom_Override"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_Half_Bottom_Override", SystemConfig["UserHacks_Half_Bottom_Override"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("Offset"))
                        ini.WriteValue("Settings", "UserHacks_Half_Bottom_Override", "-1");

                    //Round sprite (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("UserHacks_round_sprite_offset") && !string.IsNullOrEmpty(SystemConfig["UserHacks_round_sprite_offset"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_round_sprite_offset", SystemConfig["UserHacks_round_sprite_offset"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("Offset"))
                        ini.WriteValue("Settings", "UserHacks_round_sprite_offset", "0");

                    //Shader - Texture filtering of display (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("bilinear_filtering") && !string.IsNullOrEmpty(SystemConfig["bilinear_filtering"]))
                        ini.WriteValue("Settings", "linear_present", SystemConfig["bilinear_filtering"]);
                    else if (Features.IsSupported("bilinear_filtering"))
                        ini.WriteValue("Settings", "linear_present", "0");

                    //Shader - FXAA Shader (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("fxaa") && !string.IsNullOrEmpty(SystemConfig["fxaa"]))
                        ini.WriteValue("Settings", "fxaa", SystemConfig["fxaa"]);
                    else if (Features.IsSupported("fxaa"))
                        ini.WriteValue("Settings", "fxaa", "0");

                    //Renderer
                    if (SystemConfig.isOptSet("renderer") && !string.IsNullOrEmpty(SystemConfig["renderer"]))
                        ini.WriteValue("Settings", "Renderer", SystemConfig["renderer"]);
                    else if (Features.IsSupported("renderer"))
                        ini.WriteValue("Settings", "Renderer", "12");

                    //Deinterlacing : automatic or NONE options (1.6 & 1.7)
                    if (SystemConfig.isOptSet("interlace") && !string.IsNullOrEmpty(SystemConfig["interlace"]))
                        ini.WriteValue("Settings", "interlace", SystemConfig["interlace"]);
                    else if (Features.IsSupported("interlace"))
                        ini.WriteValue("Settings", "interlace", "7");

                    //Anisotropic filtering (1.6 & 1.7)
                    if (SystemConfig.isOptSet("anisotropic_filtering") && !string.IsNullOrEmpty(SystemConfig["anisotropic_filtering"]))
                        ini.WriteValue("Settings", "MaxAnisotropy", SystemConfig["anisotropic_filtering"]);
                    else if (Features.IsSupported("anisotropic_filtering"))
                        ini.WriteValue("Settings", "MaxAnisotropy", "0");

                    //Align sprite (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("align_sprite") && !string.IsNullOrEmpty(SystemConfig["align_sprite"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_align_sprite_X", SystemConfig["align_sprite"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("align_sprite"))
                        ini.WriteValue("Settings", "UserHacks_align_sprite_X", "0");

                    //Merge sprite (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("UserHacks_merge_pp_sprite") && !string.IsNullOrEmpty(SystemConfig["UserHacks_merge_pp_sprite"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_merge_pp_sprite", SystemConfig["UserHacks_merge_pp_sprite"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("align_sprite"))
                        ini.WriteValue("Settings", "UserHacks_merge_pp_sprite", "0");

                    //Disable safe features (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("UserHacks_Disable_Safe_Features") && !string.IsNullOrEmpty(SystemConfig["UserHacks_Disable_Safe_Features"]))
                    {
                        ini.WriteValue("Settings", "UserHacks_Disable_Safe_Features", SystemConfig["UserHacks_Disable_Safe_Features"]);
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("UserHacks_Disable_Safe_Features"))
                        ini.WriteValue("Settings", "UserHacks_Disable_Safe_Features", "0");

                    //Texture Offsets (both 1.6 & 1.7)
                    if (SystemConfig.isOptSet("TextureOffsets") && (SystemConfig["TextureOffsets"] == "1"))
                    {
                        ini.WriteValue("Settings", "UserHacks_TCOffsetX", "500");
                        ini.WriteValue("Settings", "UserHacks_TCOffsetY", "500");
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (SystemConfig.isOptSet("TextureOffsets") && (SystemConfig["TextureOffsets"] == "2"))
                    {
                        ini.WriteValue("Settings", "UserHacks_TCOffsetX", "0");
                        ini.WriteValue("Settings", "UserHacks_TCOffsetY", "1000");
                        ini.WriteValue("Settings", "UserHacks", "1");
                    }
                    else if (Features.IsSupported("TextureOffsets"))
                    {
                        ini.WriteValue("Settings", "UserHacks_TCOffsetX", "0");
                        ini.WriteValue("Settings", "UserHacks_TCOffsetY", "0");
                    }

                    //Skipdraw Range (ini has different values between 1.6 - gsdx.ini & 1.7 - gs.ini)
                    if (!_isPcsx17)
                    {
                        if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "1"))
                        {
                            ini.WriteValue("Settings", "UserHacks_SkipDraw_Offset", "1");
                            ini.WriteValue("Settings", "UserHacks_SkipDraw", "1");
                            ini.WriteValue("Settings", "UserHacks", "1");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "2"))
                        {
                            ini.WriteValue("Settings", "UserHacks_SkipDraw_Offset", "1");
                            ini.WriteValue("Settings", "UserHacks_SkipDraw", "2");
                            ini.WriteValue("Settings", "UserHacks", "1");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "3"))
                        {
                            ini.WriteValue("Settings", "UserHacks_SkipDraw_Offset", "1");
                            ini.WriteValue("Settings", "UserHacks_SkipDraw", "3");
                            ini.WriteValue("Settings", "UserHacks", "1");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "4"))
                        {
                            ini.WriteValue("Settings", "UserHacks_SkipDraw_Offset", "1");
                            ini.WriteValue("Settings", "UserHacks_SkipDraw", "4");
                            ini.WriteValue("Settings", "UserHacks", "1");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "5"))
                        {
                            ini.WriteValue("Settings", "UserHacks_SkipDraw_Offset", "1");
                            ini.WriteValue("Settings", "UserHacks_SkipDraw", "5");
                            ini.WriteValue("Settings", "UserHacks", "1");
                        }
                        else if (Features.IsSupported("skipdraw"))
                        {
                            ini.WriteValue("Settings", "UserHacks_SkipDraw_Offset", "0");
                            ini.WriteValue("Settings", "UserHacks_SkipDraw", "0");
                        }
                    }
                    else
                    {
                        if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "1"))
                        {
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "1");
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "1");
                            ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "2"))
                        {
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "1");
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "2");
                            ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "3"))
                        {
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "1");
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "3");
                            ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "4"))
                        {
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "1");
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "4");
                            ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                        }
                        else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "5"))
                        {
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "1");
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "5");
                            ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                        }
                        else if (Features.IsSupported("skipdraw"))
                        {
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "0");
                            ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "0");
                        }
                    }

                    //CRC Hack Level
                    if (SystemConfig.isOptSet("crc_hack_level") && !string.IsNullOrEmpty(SystemConfig["crc_hack_level"]))
                        ini.WriteValue("Settings", "crc_hack_level", SystemConfig["crc_hack_level"]);
                    else if (Features.IsSupported("crc_hack_level"))
                        ini.WriteValue("Settings", "crc_hack_level", "-1");

                    //Custom textures
                    if (SystemConfig.isOptSet("hires_textures") && SystemConfig.getOptBoolean("hires_textures"))
                    {
                        ini.WriteValue("Settings", "LoadTextureReplacements", "1");
                        ini.WriteValue("Settings", "PrecacheTextureReplacements", "1");
                    }
                    else
                    {
                        ini.WriteValue("Settings", "LoadTextureReplacements", "0");
                        ini.WriteValue("Settings", "PrecacheTextureReplacements", "0");
                    }

                    if (!_isPcsx17)
                    {
                        if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                        {
                            ini.WriteValue("Settings", "osd_monitor_enabled", "1");
                            ini.WriteValue("Settings", "osd_indicator_enabled", "1");
                        }
                        else
                        {
                            ini.WriteValue("Settings", "osd_monitor_enabled", "0");
                            ini.WriteValue("Settings", "osd_indicator_enabled", "0");
                        }

                        if (SystemConfig.isOptSet("Notifications") && SystemConfig.getOptBoolean("Notifications"))
                            ini.WriteValue("Settings", "osd_log_enabled", "1");
                        else
                            ini.WriteValue("Settings", "osd_log_enabled", "0");

                    }
                    else
                    {
                        if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                        {
                            ini.WriteValue("Settings", "OsdShowCPU", "1");
                            ini.WriteValue("Settings", "OsdShowFPS", "1");
                            ini.WriteValue("Settings", "OsdShowGPU", "1");                            
                            ini.WriteValue("Settings", "OsdShowResolution", "1");
                            ini.WriteValue("Settings", "OsdShowSpeed", "1");
                        }
                        else
                        {
                            ini.WriteValue("Settings", "OsdShowCPU", "0");
                            ini.WriteValue("Settings", "OsdShowFPS", "0");
                            ini.WriteValue("Settings", "OsdShowGPU", "0");
                            ini.WriteValue("Settings", "OsdShowResolution", "0");
                            ini.WriteValue("Settings", "OsdShowSpeed", "0");
                        }

                        if (SystemConfig.isOptSet("Notifications") && !SystemConfig.getOptBoolean("Notifications"))
                            ini.WriteValue("Settings", "OsdShowMessages", "0");
                        else
                            ini.WriteValue("Settings", "OsdShowMessages", "1");
                    }

                }

            }
            catch { }
        }
        #endregion

        #region QT version
        /// <summary>
        /// Setup Configuration of PCSX2.ini file for New PCSX2 QT version
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfigurationQT(string path, string rom, string system, bool fullscreen)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            var biosList = new string[] {
                            "SCPH30004R.bin", "SCPH30004R.MEC", "scph39001.bin", "scph39001.MEC",
                            "SCPH-39004_BIOS_V7_EUR_160.BIN", "SCPH-39001_BIOS_V7_USA_160.BIN", "SCPH-70000_BIOS_V12_JAP_200.BIN" };

            string conf = Path.Combine(_path, "inis", "PCSX2.ini");

            using (var ini = IniFile.FromFile(conf, IniOptions.UseSpaces | IniOptions.AllowDuplicateValues))
            {
                ini.WriteValue("UI", "HideMouseCursor", "true");
                CreateControllerConfiguration(ini);
                SetupGunQT(ini, path);

                // Disable auto-update
                ini.WriteValue("AutoUpdater", "CheckAtStartup", "false");

                // Enable cheevos is needed
                if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
                {
                    ini.WriteValue("Achievements", "Enabled", "true");
                    ini.WriteValue("Achievements", "TestMode", "false");
                    ini.WriteValue("Achievements", "UnofficialTestMode", "false");
                    ini.WriteValue("Achievements", "SoundEffects", "true");
                    ini.WriteValue("Achievements", "RichPresence", SystemConfig.getOptBoolean("retroachievements.richpresence") ? "true" : "false");
                    ini.WriteValue("Achievements", "PrimedIndicators", SystemConfig.getOptBoolean("retroachievements.challenge_indicators") ? "true" : "false");
                    ini.WriteValue("Achievements", "Leaderboards", SystemConfig.getOptBoolean("retroachievements.leaderboards") ? "true" : "false");
                    ini.WriteValue("Achievements", "ChallengeMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "true" : "false");
                    
                    // Inject credentials
                    if (SystemConfig.isOptSet("retroachievements.username") && SystemConfig.isOptSet("retroachievements.token"))
                    {
                        ini.WriteValue("Achievements", "Username", SystemConfig["retroachievements.username"]);
                        ini.WriteValue("Achievements", "Token", SystemConfig["retroachievements.token"]);
                        
                        if (string.IsNullOrEmpty(ini.GetValue("Achievements", "Token")))
                            ini.WriteValue("Achievements", "LoginTimestamp", Convert.ToString((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds));
                    }
                }
                else
                {
                    ini.WriteValue("Achievements", "Enabled", "false");
                    ini.WriteValue("Achievements", "ChallengeMode", "false");
                }

                // Define paths
                
                // Add rom path to RecursivePaths
                AddPathToRecursivePaths(Path.GetFullPath(Path.GetDirectoryName(rom)), ini);

                // BIOS path
                string biosPath = AppConfig.GetFullPath("bios");

                if (biosList.Any(b => File.Exists(Path.Combine(biosPath, "pcsx2", "bios", b))))
                    ini.WriteValue("Folders", "Bios", Path.Combine(biosPath, "pcsx2", "bios"));
                else
                    ini.WriteValue("Folders", "Bios", biosPath);

                string biosPcsx2Path = Path.Combine(biosPath, "pcsx2");
                if (!Directory.Exists(biosPcsx2Path))
                    try { Directory.CreateDirectory(biosPcsx2Path); }
                    catch { }

                // Precise bios to use
                string biosFile = biosList.FirstOrDefault(b => File.Exists(Path.Combine(biosPcsx2Path, "bios", b)));
                if (string.IsNullOrEmpty(biosFile))
                    biosFile = biosList.FirstOrDefault(b => File.Exists(Path.Combine(biosPath, b)));
                else
                    biosFile = "SCPH30004R.bin";

                ini.WriteValue("Filenames", "BIOS", biosFile);

                // Cheats Path
                string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "pcsx2", "cheats");
                string cheatswsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "pcsx2", "cheats_ws");
                string cheatsniPath = Path.Combine(AppConfig.GetFullPath("cheats"), "pcsx2", "cheats_ni");
                SetIniPath(ini, "Folders", "Cheats", cheatsPath);
                SetIniPath(ini, "Folders", "CheatsWS", cheatswsPath);
                SetIniPath(ini, "Folders", "CheatsNI", cheatsniPath);

                // Snapshots path
                string screenShotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "pcsx2");
                SetIniPath(ini, "Folders", "Snapshots", screenShotsPath);

                // Memory cards path
                string memcardsPath = Path.Combine(AppConfig.GetFullPath("saves"), "ps2", "pcsx2", "memcards");                
                SetIniPath(ini, "Folders", "MemoryCards", memcardsPath);

                bool newSaveStates = Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported("pcsx2");

                // SaveStates path
                string savesPath = newSaveStates ?
                    Program.EsSaveStates.GetSavePath(system, "pcsx2", "pcsx2") :
                    Path.Combine(AppConfig.GetFullPath("saves"), system, "pcsx2", "sstates");

                if (!string.IsNullOrEmpty(savesPath))
                {
                    if (newSaveStates)
                    {
                        // Keep the original folder, we'll listen to it, and inject in our custom folder
                        ini.WriteValue("Folders", "Savestates", "sstates");

                        _saveStatesWatcher = new Pcsx2SaveStatesMonitor(rom, Path.Combine(path, "sstates"), savesPath);
                        _saveStatesWatcher.PrepareEmulatorRepository();
                    }
                    else
                    {
                        FileTools.TryCreateDirectory(savesPath);
                        ini.WriteValue("Folders", "Savestates", savesPath);
                    }
                }

                // autosave
                if (_saveStatesWatcher != null)
                    ini.WriteValue("EmuCore", "SaveStateOnShutdown", _saveStatesWatcher.IsLaunchingAutoSave() || SystemConfig.getOptBoolean("autosave") ? "true" : "false");
                else
                    ini.WriteValue("EmuCore", "SaveStateOnShutdown", "false");

                //Custom textures path
                string texturePath = Path.Combine(AppConfig.GetFullPath("bios"), "pcsx2", "textures");
                SetIniPath(ini, "Folders", "Textures", texturePath);

                // UI section
                ini.WriteValue("UI", "ConfirmShutdown", "false");

                // fullscreen management
                
                if (SystemConfig.getOptBoolean("forcefullscreen"))
                    ini.WriteValue("UI", "StartFullscreen", "true");
                else if (SystemConfig.getOptBoolean("disable_fullscreen"))
                    ini.WriteValue("UI", "StartFullscreen", "false");
                else if (fullscreen)
                    ini.WriteValue("UI", "StartFullscreen", "true");
                else
                    ini.WriteValue("UI", "StartFullscreen", "false");

                ini.Remove("UI", "MainWindowGeometry");
                ini.Remove("UI", "MainWindowState");
                ini.Remove("UI", "DisplayWindowGeometry");

                // Emucore section
                ini.WriteValue("EmuCore", "SavestateZstdCompression", "true");
                ini.WriteValue("EmuCore", "SaveStateOnShutdown", "false");

                //Enable cheats automatically on load if Retroachievements-hardcore is not set only
                if (SystemConfig.isOptSet("enable_cheats") && !SystemConfig.getOptBoolean("retroachievements.hardcore") && !string.IsNullOrEmpty(SystemConfig["enable_cheats"]))
                    ini.WriteValue("EmuCore", "EnableCheats", SystemConfig["enable_cheats"]);
                else if (Features.IsSupported("enable_cheats"))
                    ini.WriteValue("EmuCore", "EnableCheats", "false");

                BindBoolIniFeature(ini, "EmuCore", "EnableDiscordPresence", "discord", "true", "false");
                BindBoolIniFeature(ini, "EmuCore", "EnableWideScreenPatches", "widescreen_patch", "true", "false");
                BindBoolIniFeature(ini, "EmuCore", "EnableNoInterlacingPatches", "interlacing_patch", "true", "false");

                // EmuCore/GS
                BindBoolIniFeature(ini, "EmuCore/GS", "IntegerScaling", "integerscale", "true", "false");
                BindIniFeature(ini, "EmuCore/GS", "AspectRatio", "ratio", "Auto 4:3/3:2");
                BindIniFeature(ini, "EmuCore/GS", "FMVAspectRatioSwitch", "fmv_ratio", "Off");
                BindIniFeature(ini, "EmuCore/GS", "Renderer", "renderer", "-1");
                BindIniFeature(ini, "EmuCore/GS", "deinterlace_mode", "interlace", "0");
                BindIniFeature(ini, "EmuCore/GS", "VsyncEnable", "VSync", "1");
                BindBoolIniFeature(ini, "EmuCore/GS", "pcrtc_offsets", "pcrtc_offsets", "true", "false");
                BindIniFeature(ini, "EmuCore/GS", "pcrtc_antiblur", "pcrtc_antiblur", "true");
                BindIniFeature(ini, "EmuCore/GS", "upscale_multiplier", "internalresolution", "1");
                BindIniFeature(ini, "EmuCore/GS", "mipmap_hw", "mipmap", "-1");
                BindIniFeature(ini, "EmuCore/GS", "filter", "texture_filtering", "2");
                BindIniFeature(ini, "EmuCore/GS", "TriFilter", "trilinear_filtering", "-1");
                BindIniFeature(ini, "EmuCore/GS", "MaxAnisotropy", "anisotropic_filtering", "0");
                BindIniFeature(ini, "EmuCore/GS", "dithering_ps2", "dithering", "2");
                BindIniFeature(ini, "EmuCore/GS", "accurate_blending_unit", "blending_accuracy", "1");
                BindBoolIniFeature(ini, "EmuCore/GS", "fxaa", "fxaa", "true", "false");
                BindIniFeature(ini, "EmuCore/GS", "TVShader", "TVShader", "0");

                if (SystemConfig.isOptSet("bilinear_filtering") && SystemConfig["bilinear_filtering"] == "0")
                {
                    ini.WriteValue("EmuCore/GS", "linear_present", "false");
                    ini.WriteValue("EmuCore/GS", "linear_present_mode", "0");
                } 
                else if (SystemConfig.isOptSet("bilinear_filtering") && SystemConfig["bilinear_filtering"] == "2")
                {
                    ini.WriteValue("EmuCore/GS", "linear_present", "true");
                    ini.WriteValue("EmuCore/GS", "linear_present_mode", "2");
                }
                else if (Features.IsSupported("bilinear_filtering"))
                {
                    ini.WriteValue("EmuCore/GS", "linear_present", "true");
                    ini.WriteValue("EmuCore/GS", "linear_present_mode", "1");
                }

                BindIniFeature(ini, "EmuCore/GS", "texture_preloading", "texture_preloading", "2");

                // User hacks
                BindBoolIniFeature(ini, "EmuCore/GS", "UserHacks", "UserHacks", "true", "false");

                // User hacks Skipdraw range
                if (SystemConfig.isOptSet("skipdraw") && !string.IsNullOrEmpty(SystemConfig["skipdraw"]))
                {
                    Action<string, string> skipdrawWrite = (s, e) =>
                    {
                        ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", s);
                        ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", e);
                        ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                    };

                    switch (SystemConfig["skipdraw"])
                    {
                        case "1":
                            skipdrawWrite("1", "1");
                            break;
                        case "2":
                            skipdrawWrite("1", "2");
                            break;
                        case "3":
                            skipdrawWrite("1", "3");
                            break;
                        case "4":
                            skipdrawWrite("1", "4");
                            break;
                        case "5":
                            skipdrawWrite("1", "5");
                            break;
                        case "bully":
                            skipdrawWrite("1", "6");
                            break;
                    }
                }
                else if (Features.IsSupported("skipdraw"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "0");
                    ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "0");
                }

                // User hack safe features
                if (SystemConfig.isOptSet("UserHacks_Disable_Safe_Features") && !string.IsNullOrEmpty(SystemConfig["UserHacks_Disable_Safe_Features"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_Disable_Safe_Features", SystemConfig["UserHacks_Disable_Safe_Features"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("UserHacks_Disable_Safe_Features"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_Disable_Safe_Features", "false");

                // User hacks Half Pixel Offset
                if (SystemConfig.isOptSet("UserHacks_HalfPixelOffset") && !string.IsNullOrEmpty(SystemConfig["UserHacks_HalfPixelOffset"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_HalfPixelOffset", SystemConfig["UserHacks_HalfPixelOffset"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("Offset"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_HalfPixelOffset", "0");

                // User hacks Round sprite
                if (SystemConfig.isOptSet("UserHacks_round_sprite_offset") && !string.IsNullOrEmpty(SystemConfig["UserHacks_round_sprite_offset"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_round_sprite_offset", SystemConfig["UserHacks_round_sprite_offset"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("Offset"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_round_sprite_offset", "0");

                // User hacks Align sprite
                if (SystemConfig.isOptSet("align_sprite") && !string.IsNullOrEmpty(SystemConfig["align_sprite"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_align_sprite_X", SystemConfig["align_sprite"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("align_sprite"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_align_sprite_X", "false");

                // User hacks Merge sprite
                if (SystemConfig.isOptSet("UserHacks_merge_pp_sprite") && !string.IsNullOrEmpty(SystemConfig["UserHacks_merge_pp_sprite"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_merge_pp_sprite", SystemConfig["UserHacks_merge_pp_sprite"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("align_sprite"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_merge_pp_sprite", "false");

                // User hacks Wild Arms offset
                if (SystemConfig.isOptSet("UserHacks_WildHack") && !string.IsNullOrEmpty(SystemConfig["UserHacks_WildHack"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_WildHack", SystemConfig["UserHacks_WildHack"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("UserHacks_WildHack"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_WildHack", "false");

                //texture offset
                if (SystemConfig.isOptSet("TextureOffsets") && !string.IsNullOrEmpty(SystemConfig["TextureOffsets"]))
                {
                    Action<string, string> textureOffsetsWrite = (x, y) =>
                    {
                        ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetX", x);
                        ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetY", y);
                        ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                    };

                    switch (SystemConfig["TextureOffsets"])
                    {
                        case "1":
                            textureOffsetsWrite("500", "500");
                            break;
                        case "2":
                            textureOffsetsWrite("0", "1000");
                            break;
                    }
                }
                else if (Features.IsSupported("TextureOffsets"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetX", "0");
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetY", "0");
                }

                // Custom textures
                if (SystemConfig.isOptSet("hires_textures") && SystemConfig["hires_textures"] == "1")
                {
                    ini.WriteValue("EmuCore/GS", "LoadTextureReplacements", "true");
                    ini.WriteValue("EmuCore/GS", "PrecacheTextureReplacements", "true");
                }
                else if (SystemConfig.isOptSet("hires_textures") && SystemConfig["hires_textures"] == "2")
                {
                    ini.WriteValue("EmuCore/GS", "LoadTextureReplacements", "true");
                    ini.WriteValue("EmuCore/GS", "PrecacheTextureReplacements", "false");
                }
                else
                {
                    ini.WriteValue("EmuCore/GS", "LoadTextureReplacements", "false");
                    ini.WriteValue("EmuCore/GS", "PrecacheTextureReplacements", "false");
                }

                // OSD information
                BindIniFeature(ini, "EmuCore/GS", "OsdShowMessages", "Notifications", "true");

                if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                {
                    ini.WriteValue("EmuCore/GS", "OsdShowCPU", "true");
                    ini.WriteValue("EmuCore/GS", "OsdShowFPS", "true");
                    ini.WriteValue("EmuCore/GS", "OsdShowGPU", "true");
                    ini.WriteValue("EmuCore/GS", "OsdShowResolution", "true");
                    ini.WriteValue("EmuCore/GS", "OsdShowSpeed", "true");
                }
                else
                {
                    ini.WriteValue("EmuCore/GS", "OsdShowCPU", "false");
                    ini.WriteValue("EmuCore/GS", "OsdShowFPS", "false");
                    ini.WriteValue("EmuCore/GS", "OsdShowGPU", "false");
                    ini.WriteValue("EmuCore/GS", "OsdShowResolution", "false");
                    ini.WriteValue("EmuCore/GS", "OsdShowSpeed", "false");
                }

                // AUDIO section
                BindIniFeature(ini, "SPU2/Output", "OutputModule", "apu", "cubeb");

                // Game fixes
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "FpuNegDivHack", "FpuNegDivHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "FpuMulHack", "FpuMulHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "SoftwareRendererFMVHack", "SoftwareRendererFMVHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "SkipMPEGHack", "SkipMPEGHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "GoemonTlbHack", "GoemonTlbHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "EETimingHack", "EETimingHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "InstantDMAHack", "InstantDMAHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "OPHFlagHack", "OPHFlagHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "GIFFIFOHack", "GIFFIFOHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "DMABusyHack", "DMABusyHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "VIF1StallHack", "VIF1StallHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "VIFFIFOHack", "VIFFIFOHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "IbitHack", "IbitHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "VuAddSubHack", "VuAddSubHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "VUOverflowHack", "VUOverflowHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "VUSyncHack", "VUSyncHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "XgKickHack", "XgKickHack", "true", "false");
                BindBoolIniFeature(ini, "EmuCore/Gamefixes", "BlitInternalFPSHack", "BlitInternalFPSHack", "true", "false");
            }
        }

        private static void AddPathToRecursivePaths(string romPath, IniFile ini)
        {
            var recursivePaths = ini.EnumerateValues("GameList")
                .Where(e => e.Key == "RecursivePaths")
                .Select(e => Path.GetFullPath(e.Value))
                .ToList();

            if (!recursivePaths.Contains(romPath))
                ini.AppendValue("GameList", "RecursivePaths", romPath);
        }
        #endregion
        
        public override int RunAndWait(ProcessStartInfo path)
        {
            int ret = 0;
            int monitorIndex = Math.Max(0, SystemConfig["MonitorIndex"].ToInteger() - 1);
            
            if (_bezelFileInfo != null)
            {               
                var bezel = _bezelFileInfo.ShowFakeBezel(_resolution, true, monitorIndex);
                if (bezel != null)
                {
                    RECT rc = bezel.ViewPort;

                    if (rc.bottom - rc.top == (_resolution ?? ScreenResolution.CurrentResolution).Height)
                        rc.bottom--;

                    var process = StartProcessAndMoveItsWindowTo(path, rc);
                    if (process != null)
                    {
                        while (!process.WaitForExit(50))
                            Application.DoEvents();

                        try { ret = process.ExitCode; }
                        catch { }
                    }

                    if (bezel != null)
                        bezel.Dispose();

                    return ret;
                }
            }

            if (monitorIndex >= 0 && Screen.AllScreens.Length > 1 && monitorIndex < Screen.AllScreens.Length)
            {
                var process = StartProcessAndMoveItsWindowTo(path, Screen.AllScreens[monitorIndex].Bounds);
                if (process != null)
                {
                    process.WaitForExit();

                    try { ret = process.ExitCode; }
                    catch { }
                }

                return ret;
            }

            return base.RunAndWait(path);
        }

        private Process StartProcessAndMoveItsWindowTo(ProcessStartInfo path, RECT rc)
        {
            int retryCount = 0;
            var process = Process.Start(path);

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                retryCount++;

                // If it's longer than 10 seconds, then exit loop
                if (retryCount > 10000 / 50)
                    break;

                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;

                if (!User32.IsWindowVisible(hWnd))
                    continue;

                User32.SetWindowPos(hWnd, IntPtr.Zero, rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top, SWP.ASYNCWINDOWPOS);
                break;
            }
            return process;
        }
      
    }
}
