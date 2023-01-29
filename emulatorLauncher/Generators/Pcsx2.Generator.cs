using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Management;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class Pcsx2Generator : Generator
    {
        public Pcsx2Generator()
        {
            DependsOnDesktopResolution = true;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            return ret;
        }
      
        private string _path;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        private bool _isPcsx17;
        private bool _isPcsxqt;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            //Define paths, search first based on emulator name
            string folderName = emulator;

            _path = AppConfig.GetFullPath(folderName);

            //If path does not exist try pcsx2 1.6 path
            if (string.IsNullOrEmpty(_path))
                _path = AppConfig.GetFullPath("pcsx2-16");

            //search first for qt version .exe, if found also set bool _isPcsxqt to true for later steps
            string exe = Path.Combine(_path, "pcsx2-qtx64.exe"); // v1.7qt filename
            if (File.Exists(exe))
            {
                _isPcsxqt = true;

                if (core == "pcsx2-avx2" || core == "avx2")
                {
                    string avx2 = Path.Combine(_path, "pcsx2-qtx64-avx2.exe");
                    if (File.Exists(avx2))
                        exe = avx2;
                }
            }
            else if (!File.Exists(exe))
            {
                exe = Path.Combine(_path, "pcsx2x64.exe"); // v1.7 filename 
                if (!File.Exists(exe))
                    exe = Path.Combine(_path, "pcsx2.exe"); // v1.6 filename            
            }

            // v1.7.0 ???
            Version version = new Version();
            if (!_isPcsxqt && Version.TryParse(FileVersionInfo.GetVersionInfo(exe).ProductVersion, out version))
                _isPcsx17 = version >= new Version(1, 7, 0, 0);

            // Select avx2 build for 1.7 non-qt version
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

            // Configuration files
            // QT version has now only 1 ini file versus multiple for wxwidgets version
            if (_isPcsxqt)
                SetupConfigurationQT(path);
            else
            {
                SetupPaths(emulator, core);
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

                if (SystemConfig.isOptSet("bigpicture") && SystemConfig.getOptBoolean("bigpicture"))
                {
                    commandArray.Add("-fullscreen");
                    commandArray.Add("-bigpicture");
                }

                if (SystemConfig.isOptSet("fullboot") && SystemConfig.getOptBoolean("fullboot"))
                    commandArray.Add("-slowboot");
            }
            else 
            {
                commandArray.Add("--portable");
                commandArray.Add("--fullscreen");
                commandArray.Add("--nogui");

                if (SystemConfig.isOptSet("fullboot") && SystemConfig.getOptBoolean("fullboot"))
                    commandArray.Add("--fullboot");
            }

            string args = string.Join(" ", commandArray);

            //start emulator
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args + " \"" + rom + "\"",
            };
        }

        #region wxwidgets version
        private void SetupPaths(string emulator, string core)
        {
            var biosList = new string[] { 
                            "SCPH30004R.bin", "SCPH30004R.MEC", "scph39001.bin", "scph39001.MEC", 
                            "SCPH-39004_BIOS_V7_EUR_160.BIN", "SCPH-39001_BIOS_V7_USA_160.BIN", "SCPH-70000_BIOS_V12_JAP_200.BIN" };

            string iniFile = Path.Combine(_path, "inis", "PCSX2_ui.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    string biosPath = AppConfig.GetFullPath("bios");
                    if (!string.IsNullOrEmpty(biosPath))
                    {
                        ini.WriteValue("Folders", "UseDefaultBios", "disabled");

                        if (biosList.Any(b => File.Exists(Path.Combine(biosPath, "pcsx2", "bios", b))))
                            ini.WriteValue("Folders", "Bios", Path.Combine(biosPath, "pcsx2", "bios").Replace("\\", "\\\\"));
                        else
                            ini.WriteValue("Folders", "Bios", biosPath.Replace("\\", "\\\\"));

                        ini.WriteValue("Folders", "UseDefaultCheats", "disabled");
                        ini.WriteValue("Folders", "Cheats", Path.Combine(biosPath, "pcsx2", "cheats").Replace("\\", "\\\\"));
                        ini.WriteValue("Folders", "UseDefaultCheatsWS", "disabled");
                        ini.WriteValue("Folders", "CheatsWS", Path.Combine(biosPath, "pcsx2", "cheats_ws").Replace("\\", "\\\\"));
                    }

                    string savesPath = AppConfig.GetFullPath("saves");
                    if (!string.IsNullOrEmpty(savesPath))
                    {
                        savesPath = Path.Combine(savesPath, "ps2", Path.GetFileName(_path));

                        if (!Directory.Exists(savesPath))
                            try { Directory.CreateDirectory(savesPath); }
                            catch { }

                        ini.WriteValue("Folders", "UseDefaultSavestates", "disabled");
                        ini.WriteValue("Folders", "UseDefaultMemoryCards", "disabled");
                        ini.WriteValue("Folders", "Savestates", savesPath.Replace("\\", "\\\\") + "\\\\" + "sstates");
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
                    ini.WriteValue("GSWindow", "IsFullscreen", "enabled");

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

                        if (SystemConfig.isOptSet("Notifications") && SystemConfig.getOptBoolean("Notifications"))
                            ini.WriteValue("Settings", "OsdShowMessages", "1");
                        else
                            ini.WriteValue("Settings", "OsdShowMessages", "0");
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
        private void SetupConfigurationQT(string path)
        {
            var biosList = new string[] {
                            "SCPH30004R.bin", "SCPH30004R.MEC", "scph39001.bin", "scph39001.MEC",
                            "SCPH-39004_BIOS_V7_EUR_160.BIN", "SCPH-39001_BIOS_V7_USA_160.BIN", "SCPH-70000_BIOS_V12_JAP_200.BIN" };

            string conf = Path.Combine(_path, "inis", "PCSX2.ini");

            using (var ini = IniFile.FromFile(conf, IniOptions.UseSpaces))
            {
                CreateControllerConfiguration(ini);

                //fullscreen
                ini.WriteValue("UI", "StartFullscreen", "true");

                //Enable cheevos is needed
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

                //Define paths

                //BIOS path
                string biosPath = AppConfig.GetFullPath("bios");

                if (biosList.Any(b => File.Exists(Path.Combine(biosPath, "pcsx2", "bios", b))))
                    ini.WriteValue("Folders", "Bios", Path.Combine(biosPath, "pcsx2", "bios"));
                else
                    ini.WriteValue("Folders", "Bios", biosPath);

                string biosPcsx2Path = Path.Combine(biosPath, "pcsx2");

                if (!Directory.Exists(biosPcsx2Path))
                    try { Directory.CreateDirectory(biosPcsx2Path); }
                    catch { }

                ini.WriteValue("Folders", "Cheats", Path.Combine(biosPcsx2Path, "cheats"));
                ini.WriteValue("Folders", "CheatsWS", Path.Combine(biosPcsx2Path, "cheats_ws"));
                ini.WriteValue("Folders", "CheatsNI", Path.Combine(biosPcsx2Path, "cheats_ni"));

                //precise bios to use

                string biosFile = biosList.FirstOrDefault(b => File.Exists(Path.Combine(biosPcsx2Path, "bios", b)));
                if (string.IsNullOrEmpty(biosFile))
                    biosFile = biosList.FirstOrDefault(b => File.Exists(Path.Combine(biosPath, b)));
                else
                    biosFile = "SCPH30004R.bin";

                ini.WriteValue("Filenames", "BIOS", biosFile);

                //Snapshots path
                string screenShotsPath = AppConfig.GetFullPath("screenshots");
                if (!string.IsNullOrEmpty(screenShotsPath))
                    ini.WriteValue("Folders", "Snapshots", screenShotsPath + "\\" + "pcsx2");

                //Savestates path
                string savesPath = AppConfig.GetFullPath("saves");
                string memcardsPath = AppConfig.GetFullPath("saves");
                if (!string.IsNullOrEmpty(savesPath))
                {
                    savesPath = Path.Combine(savesPath, "ps2", "pcsx2", "sstates");
                    memcardsPath = Path.Combine(memcardsPath, "ps2", "pcsx2", "memcards");

                    if (!Directory.Exists(savesPath))
                        try { Directory.CreateDirectory(savesPath); }
                        catch { }

                    if (!Directory.Exists(memcardsPath))
                        try { Directory.CreateDirectory(memcardsPath); }
                        catch { }

                    ini.WriteValue("Folders", "Savestates", savesPath);
                    ini.WriteValue("Folders", "MemoryCards", memcardsPath);
                }

                //Custom textures path
                string texturePath = AppConfig.GetFullPath("bios");
                if (!string.IsNullOrEmpty(texturePath))
                {
                    texturePath = Path.Combine(texturePath, "pcsx2", "textures");

                    if (!Directory.Exists(texturePath))
                        try { Directory.CreateDirectory(texturePath); }
                        catch { }

                    ini.WriteValue("Folders", "Textures", texturePath);
                }

                //UI section
                ini.WriteValue("UI", "ConfirmShutdown", "false");
                
                //Enable cheats automatically on load if Retroachievements is not set only
                if (SystemConfig.isOptSet("enable_cheats") && !SystemConfig.getOptBoolean("retroachievements") && !string.IsNullOrEmpty(SystemConfig["enable_cheats"]))
                    ini.WriteValue("EmuCore", "EnableCheats", SystemConfig["enable_cheats"]);
                else if (Features.IsSupported("enable_cheats"))
                    ini.WriteValue("EmuCore", "EnableCheats", "false");

                //Graphics - EmuCore/GS
                if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                    ini.WriteValue("EmuCore/GS", "AspectRatio", SystemConfig["ratio"]);
                else
                    ini.WriteValue("EmuCore/GS", "AspectRatio", "Auto 4:3/3:2");

                if (SystemConfig.isOptSet("fmv_ratio") && !string.IsNullOrEmpty(SystemConfig["fmv_ratio"]))
                    ini.WriteValue("EmuCore/GS", "FMVAspectRatioSwitch", SystemConfig["fmv_ratio"]);
                else if (Features.IsSupported("fmv_ratio"))
                    ini.WriteValue("EmuCore/GS", "FMVAspectRatioSwitch", "Off");

                if (SystemConfig.isOptSet("renderer") && !string.IsNullOrEmpty(SystemConfig["renderer"]))
                    ini.WriteValue("EmuCore/GS", "Renderer", SystemConfig["renderer"]);
                else
                    ini.WriteValue("EmuCore/GS", "Renderer", "-1");

                if (SystemConfig.isOptSet("interlace") && !string.IsNullOrEmpty(SystemConfig["interlace"]))
                    ini.WriteValue("EmuCore/GS", "deinterlace", SystemConfig["interlace"]);
                else if (Features.IsSupported("interlace"))
                    ini.WriteValue("EmuCore/GS", "deinterlace", "7");

                if (SystemConfig.isOptSet("bilinear_filtering") && !string.IsNullOrEmpty(SystemConfig["bilinear_filtering"]))
                    ini.WriteValue("EmuCore/GS", "linear_present", SystemConfig["bilinear_filtering"]);
                else if (Features.IsSupported("bilinear_filtering"))
                    ini.WriteValue("EmuCore/GS", "linear_present", "true");

                //Vsync
                if (SystemConfig.isOptSet("VSync") && !string.IsNullOrEmpty(SystemConfig["VSync"]))
                    ini.WriteValue("EmuCore/GS", "VsyncEnable", SystemConfig["VSync"]);
                else
                    ini.WriteValue("EmuCore/GS", "VsyncEnable", "0");

                if (SystemConfig.isOptSet("pcrtc_offsets") && !string.IsNullOrEmpty(SystemConfig["pcrtc_offsets"]))
                    ini.WriteValue("EmuCore/GS", "pcrtc_offsets", SystemConfig["pcrtc_offsets"]);
                else
                    ini.WriteValue("EmuCore/GS", "pcrtc_offsets", "false");

                if (SystemConfig.isOptSet("pcrtc_antiblur") && !string.IsNullOrEmpty(SystemConfig["pcrtc_antiblur"]))
                    ini.WriteValue("EmuCore/GS", "pcrtc_antiblur", SystemConfig["pcrtc_antiblur"]);
                else
                    ini.WriteValue("EmuCore/GS", "pcrtc_antiblur", "true");

                //Resolution
                if (SystemConfig.isOptSet("internalresolution") && !string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                    ini.WriteValue("EmuCore/GS", "upscale_multiplier", SystemConfig["internalresolution"]);
                else
                    ini.WriteValue("EmuCore/GS", "upscale_multiplier", "1");

                if (SystemConfig.isOptSet("mipmap") && !string.IsNullOrEmpty(SystemConfig["mipmap"]))
                    ini.WriteValue("EmuCore/GS", "mipmap_hw", SystemConfig["mipmap"]);
                else
                    ini.WriteValue("EmuCore/GS", "mipmap_hw", "-1");

                if (SystemConfig.isOptSet("texture_filtering") && !string.IsNullOrEmpty(SystemConfig["texture_filtering"]))
                    ini.WriteValue("EmuCore/GS", "filter", SystemConfig["texture_filtering"]);
                else
                    ini.WriteValue("EmuCore/GS", "filter", "2");

                if (SystemConfig.isOptSet("trilinear_filtering") && !string.IsNullOrEmpty(SystemConfig["trilinear_filtering"]))
                    ini.WriteValue("EmuCore/GS", "TriFilter", SystemConfig["trilinear_filtering"]);
                else
                    ini.WriteValue("EmuCore/GS", "TriFilter", "-1");

                if (SystemConfig.isOptSet("anisotropic_filtering") && !string.IsNullOrEmpty(SystemConfig["anisotropic_filtering"]))
                    ini.WriteValue("EmuCore/GS", "MaxAnisotropy", SystemConfig["anisotropic_filtering"]);
                else
                    ini.WriteValue("EmuCore/GS", "MaxAnisotropy", "0");

                if (SystemConfig.isOptSet("dithering") && !string.IsNullOrEmpty(SystemConfig["dithering"]))
                    ini.WriteValue("EmuCore/GS", "dithering_ps2", SystemConfig["dithering"]);
                else
                    ini.WriteValue("EmuCore/GS", "dithering_ps2", "2");

                if (SystemConfig.isOptSet("crc_hack_level") && !string.IsNullOrEmpty(SystemConfig["crc_hack_level"]))
                    ini.WriteValue("EmuCore/GS", "crc_hack_level", SystemConfig["crc_hack_level"]);
                else if (Features.IsSupported("crc_hack_level"))
                    ini.WriteValue("EmuCore/GS", "crc_hack_level", "-1");

                if (SystemConfig.isOptSet("blending_accuracy") && !string.IsNullOrEmpty(SystemConfig["blending_accuracy"]))
                    ini.WriteValue("EmuCore/GS", "accurate_blending_unit", SystemConfig["blending_accuracy"]);
                else if (Features.IsSupported("blending_accuracy"))
                    ini.WriteValue("EmuCore/GS", "accurate_blending_unit", "1");

                if (SystemConfig.isOptSet("texture_preloading") && !string.IsNullOrEmpty(SystemConfig["texture_preloading"]))
                    ini.WriteValue("EmuCore/GS", "texture_preloading", SystemConfig["texture_preloading"]);
                else if (Features.IsSupported("texture_preloading"))
                    ini.WriteValue("EmuCore/GS", "texture_preloading", "2");

                //User hacks
                if ((SystemConfig.isOptSet("UserHacks") && !string.IsNullOrEmpty(SystemConfig["UserHacks"])))
                    ini.WriteValue("EmuCore/GS", "UserHacks", SystemConfig["UserHacks"]);
                else if (Features.IsSupported("UserHacks"))
                    ini.WriteValue("EmuCore/GS", "UserHacks", "false");

                //User hack half screen fix
                if (SystemConfig.isOptSet("UserHacks_Half_Bottom_Override") && !string.IsNullOrEmpty(SystemConfig["UserHacks_Half_Bottom_Override"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_Half_Bottom_Override", SystemConfig["UserHacks_Half_Bottom_Override"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("Offset"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_Half_Bottom_Override", "-1");

                //User hacks Skipdraw range
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
                else if (SystemConfig.isOptSet("skipdraw") && (SystemConfig["skipdraw"] == "bully"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "6");
                    ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "6");
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("skipdraw"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_Start", "0");
                    ini.WriteValue("EmuCore/GS", "UserHacks_SkipDraw_End", "0");
                }
                
                //User hack safe features
                if (SystemConfig.isOptSet("UserHacks_Disable_Safe_Features") && !string.IsNullOrEmpty(SystemConfig["UserHacks_Disable_Safe_Features"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_Disable_Safe_Features", SystemConfig["UserHacks_Disable_Safe_Features"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("UserHacks_Disable_Safe_Features"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_Disable_Safe_Features", "false");

                //User hacks Half Pixel Offset
                if (SystemConfig.isOptSet("UserHacks_HalfPixelOffset") && !string.IsNullOrEmpty(SystemConfig["UserHacks_HalfPixelOffset"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_HalfPixelOffset", SystemConfig["UserHacks_HalfPixelOffset"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("Offset"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_HalfPixelOffset", "0");

                //User hacks Round sprite
                if (SystemConfig.isOptSet("UserHacks_round_sprite_offset") && !string.IsNullOrEmpty(SystemConfig["UserHacks_round_sprite_offset"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_round_sprite_offset", SystemConfig["UserHacks_round_sprite_offset"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("Offset"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_round_sprite_offset", "0");

                //User hacks Align sprite
                if (SystemConfig.isOptSet("align_sprite") && !string.IsNullOrEmpty(SystemConfig["align_sprite"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_align_sprite_X", SystemConfig["align_sprite"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("align_sprite"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_align_sprite_X", "false");

                //User hacks Merge sprite
                if (SystemConfig.isOptSet("UserHacks_merge_pp_sprite") && !string.IsNullOrEmpty(SystemConfig["UserHacks_merge_pp_sprite"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_merge_pp_sprite", SystemConfig["UserHacks_merge_pp_sprite"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("align_sprite"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_merge_pp_sprite", "false");

                //User hacks Wild Arms offset
                if (SystemConfig.isOptSet("UserHacks_WildHack") && !string.IsNullOrEmpty(SystemConfig["UserHacks_WildHack"]))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_WildHack", SystemConfig["UserHacks_WildHack"]);
                    ini.WriteValue("EmuCore/GS", "UserHacks", "true");
                }
                else if (Features.IsSupported("UserHacks_WildHack"))
                    ini.WriteValue("EmuCore/GS", "UserHacks_WildHack", "false");

                //Custom textures
                if (SystemConfig.isOptSet("hires_textures") && SystemConfig.getOptBoolean("hires_textures"))
                {
                    ini.WriteValue("EmuCore/GS", "LoadTextureReplacements", "true");
                    ini.WriteValue("EmuCore/GS", "PrecacheTextureReplacements", "true");
                }
                else
                {
                    ini.WriteValue("EmuCore/GS", "LoadTextureReplacements", "false");
                    ini.WriteValue("EmuCore/GS", "PrecacheTextureReplacements", "false");
                }
                
                //OSD information
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

                //texture offset
                if (SystemConfig.isOptSet("TextureOffsets") && (SystemConfig["TextureOffsets"] == "1"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetX", "500");
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetY", "500");
                    ini.WriteValue("EmuCore/GS", "UserHacks", "1");
                }
                else if (SystemConfig.isOptSet("TextureOffsets") && (SystemConfig["TextureOffsets"] == "2"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetX", "0");
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetY", "1000");
                    ini.WriteValue("EmuCore/GS", "UserHacks", "1");
                }
                else if (Features.IsSupported("TextureOffsets"))
                {
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetX", "0");
                    ini.WriteValue("EmuCore/GS", "UserHacks_TCOffsetY", "0");
                }

                //Show OSD notifications
                if (SystemConfig.isOptSet("Notifications") && !string.IsNullOrEmpty(SystemConfig["Notifications"]))
                    ini.WriteValue("EmuCore/GS", "OsdShowMessages", SystemConfig["Notifications"]);
                else
                    ini.WriteValue("EmuCore/GS", "OsdShowMessages", "false");

                //FXAA
                if (SystemConfig.isOptSet("fxaa") && !string.IsNullOrEmpty(SystemConfig["fxaa"]))
                    ini.WriteValue("EmuCore/GS", "fxaa", SystemConfig["fxaa"]);
                else if (Features.IsSupported("fxaa"))
                    ini.WriteValue("EmuCore/GS", "fxaa", "false");

                //TVShader
                if (SystemConfig.isOptSet("TVShader") && !string.IsNullOrEmpty(SystemConfig["TVShader"]))
                    ini.WriteValue("EmuCore/GS", "TVShader", SystemConfig["TVShader"]);
                else if (Features.IsSupported("TVShader"))
                    ini.WriteValue("EmuCore/GS", "TVShader", "0");

                //AUDIO section
                if (SystemConfig.isOptSet("apu") && !string.IsNullOrEmpty(SystemConfig["apu"]))
                    ini.WriteValue("SPU2/Output", "OutputModule", SystemConfig["apu"]);
                else if (Features.IsSupported("apu"))
                    ini.WriteValue("SPU2/Output", "OutputModule", "cubeb");

                //Game fixes
                if (SystemConfig.isOptSet("FpuNegDivHack") && !string.IsNullOrEmpty(SystemConfig["FpuNegDivHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "FpuNegDivHack", SystemConfig["FpuNegDivHack"]);
                else if (Features.IsSupported("FpuNegDivHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "FpuNegDivHack", "false");

                if (SystemConfig.isOptSet("FpuMulHack") && !string.IsNullOrEmpty(SystemConfig["FpuMulHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "FpuMulHack", SystemConfig["FpuMulHack"]);
                else if (Features.IsSupported("FpuMulHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "FpuMulHack", "false");

                if (SystemConfig.isOptSet("SoftwareRendererFMVHack") && !string.IsNullOrEmpty(SystemConfig["SoftwareRendererFMVHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "SoftwareRendererFMVHack", SystemConfig["SoftwareRendererFMVHack"]);
                else if (Features.IsSupported("SoftwareRendererFMVHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "SoftwareRendererFMVHack", "false");

                if (SystemConfig.isOptSet("SkipMPEGHack") && !string.IsNullOrEmpty(SystemConfig["SkipMPEGHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "SkipMPEGHack", SystemConfig["SkipMPEGHack"]);
                else if (Features.IsSupported("SkipMPEGHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "SkipMPEGHack", "false");

                if (SystemConfig.isOptSet("GoemonTlbHack") && !string.IsNullOrEmpty(SystemConfig["GoemonTlbHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "GoemonTlbHack", SystemConfig["GoemonTlbHack"]);
                else if (Features.IsSupported("GoemonTlbHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "GoemonTlbHack", "false");

                if (SystemConfig.isOptSet("EETimingHack") && !string.IsNullOrEmpty(SystemConfig["EETimingHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "EETimingHack", SystemConfig["EETimingHack"]);
                else if (Features.IsSupported("EETimingHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "EETimingHack", "false");

                if (SystemConfig.isOptSet("InstantDMAHack") && !string.IsNullOrEmpty(SystemConfig["InstantDMAHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "InstantDMAHack", SystemConfig["InstantDMAHack"]);
                else if (Features.IsSupported("InstantDMAHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "InstantDMAHack", "false");

                if (SystemConfig.isOptSet("OPHFlagHack") && !string.IsNullOrEmpty(SystemConfig["OPHFlagHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "OPHFlagHack", SystemConfig["OPHFlagHack"]);
                else if (Features.IsSupported("OPHFlagHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "OPHFlagHack", "false");

                if (SystemConfig.isOptSet("GIFFIFOHack") && !string.IsNullOrEmpty(SystemConfig["GIFFIFOHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "GIFFIFOHack", SystemConfig["GIFFIFOHack"]);
                else if (Features.IsSupported("GIFFIFOHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "GIFFIFOHack", "false");

                if (SystemConfig.isOptSet("DMABusyHack") && !string.IsNullOrEmpty(SystemConfig["DMABusyHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "DMABusyHack", SystemConfig["DMABusyHack"]);
                else if (Features.IsSupported("DMABusyHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "DMABusyHack", "false");

                if (SystemConfig.isOptSet("VIF1StallHack") && !string.IsNullOrEmpty(SystemConfig["VIF1StallHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "VIF1StallHack", SystemConfig["VIF1StallHack"]);
                else if (Features.IsSupported("VIF1StallHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "VIF1StallHack", "false");

                if (SystemConfig.isOptSet("VIFFIFOHack") && !string.IsNullOrEmpty(SystemConfig["VIFFIFOHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "VIFFIFOHack", SystemConfig["VIFFIFOHack"]);
                else if (Features.IsSupported("VIFFIFOHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "VIFFIFOHack", "false");

                if (SystemConfig.isOptSet("IbitHack") && !string.IsNullOrEmpty(SystemConfig["IbitHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "IbitHack", SystemConfig["IbitHack"]);
                else if (Features.IsSupported("IbitHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "IbitHack", "false");

                if (SystemConfig.isOptSet("VuAddSubHack") && !string.IsNullOrEmpty(SystemConfig["VuAddSubHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "VuAddSubHack", SystemConfig["VuAddSubHack"]);
                else if (Features.IsSupported("VuAddSubHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "VuAddSubHack", "false");

                if (SystemConfig.isOptSet("VUOverflowHack") && !string.IsNullOrEmpty(SystemConfig["VUOverflowHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "VUOverflowHack", SystemConfig["VUOverflowHack"]);
                else if (Features.IsSupported("VUOverflowHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "VUOverflowHack", "false");

                if (SystemConfig.isOptSet("VUSyncHack") && !string.IsNullOrEmpty(SystemConfig["VUSyncHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "VUSyncHack", SystemConfig["VUSyncHack"]);
                else if (Features.IsSupported("VUSyncHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "VUSyncHack", "false");

                if (SystemConfig.isOptSet("XgKickHack") && !string.IsNullOrEmpty(SystemConfig["XgKickHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "XgKickHack", SystemConfig["XgKickHack"]);
                else if (Features.IsSupported("XgKickHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "XgKickHack", "false");

                if (SystemConfig.isOptSet("BlitInternalFPSHack") && !string.IsNullOrEmpty(SystemConfig["BlitInternalFPSHack"]))
                    ini.WriteValue("EmuCore/Gamefixes", "BlitInternalFPSHack", SystemConfig["BlitInternalFPSHack"]);
                else if (Features.IsSupported("BlitInternalFPSHack"))
                    ini.WriteValue("EmuCore/Gamefixes", "BlitInternalFPSHack", "false");

            }
        }
        #endregion
    }
}
