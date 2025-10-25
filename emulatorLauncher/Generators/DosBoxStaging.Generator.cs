using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static EmulatorLauncher.PadToKeyboard.SendKey;

namespace EmulatorLauncher
{
    class DosBoxStagingGenerator : Generator
    {
        public DosBoxStagingGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string path = AppConfig.GetFullPath("dosbox-staging");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "dosbox.exe");
            if (!File.Exists(exe))
                return null;

            if (fullscreen)
            {
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                _resolution = resolution;

                if (_bezelFileInfo != null && _bezelFileInfo.PngFile != null)
                    SimpleLogger.Instance.Info("[INFO] Bezel file selected : " + _bezelFileInfo.PngFile);
            }

            bool zip_autoboot = false;
            string autoboot = "";
            if ((Path.GetExtension(rom).ToLowerInvariant() == ".7z" || Path.GetExtension(rom).ToLowerInvariant() == ".squashfs") || Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                string[] romExtensions = new string[] { ".dbp" };
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    rom = uncompressedRomPath;
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*").OrderBy(file => Array.IndexOf(romExtensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    if (romFiles.Length > 0)
                    {
                        string autobootFile = romFiles[0];
                        autoboot = FileTools.ReadFirstValidLine(autobootFile);
                        zip_autoboot = true;
                    }
                    else
                        SimpleLogger.Instance.Info("[DosBoxStaging] No autostart file found after uncompressing the archive.");
                    
                    ValidateUncompressedGame();
                }
            }

            string confFile = Path.Combine(path, "dosbox-staging.conf");
            if (!File.Exists(confFile))
                File.WriteAllText(confFile, "");

            SearchAndCopySoundFiles(path);
            ConfigureDosBox(confFile, fullscreen);

            if (Directory.Exists(rom) && File.Exists(Path.Combine(rom, "dosbox.conf")))
                confFile = Path.Combine(rom, "dosbox.conf");

            string ext = Path.GetExtension(rom).ToLowerInvariant();

            string batFile = null;
            if (ext == ".bat")
                batFile = rom;
            else if (Directory.Exists(rom))
            {
                batFile = Path.Combine(rom, "dosbox.bat");
                if (!File.Exists(batFile))
                    batFile = Path.Combine(rom, "run.bat");
                if (!File.Exists(batFile))
                    batFile = Path.Combine(rom, "autoexec.bat");
            }

            if (File.Exists(batFile) && !SystemConfig.getOptBoolean("dosbox_ignorebat"))
            {
                List<string> commandArrayBat = new List<string>();

                commandArrayBat.Add("-conf");
                commandArrayBat.Add("\"" + confFile + "\"");
                if (fullscreen)
                    commandArrayBat.Add("-fullscreen");
                if (!SystemConfig.getOptBoolean("dosbox_showconsole"))
                    commandArrayBat.Add("-noconsole");
                if (!SystemConfig.getOptBoolean("dosbox_noexit"))
                    commandArrayBat.Add("-exit");
                commandArrayBat.Add("\"" + batFile + "\"");

                string argsBat = string.Join(" ", commandArrayBat);

                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = argsBat
                };
            }

            List<string> commandArray = new List<string>();

            if (File.Exists(confFile))
            {
                commandArray.Add("-conf");
                commandArray.Add("\"" + confFile + "\"");
            }

            if (fullscreen)
                commandArray.Add("-fullscreen");

            if (!SystemConfig.getOptBoolean("dosbox_showconsole"))
                commandArray.Add("-noconsole");

            if (!SystemConfig.getOptBoolean("dosbox_noexit"))
                commandArray.Add("-exit");

            if (Directory.Exists(rom))
            {
                if (SystemConfig.getOptBoolean("dosbox_runsetup"))
                {
                    string setupcommand = "SETUP.EXE";

                    if (SystemConfig.isOptSet("dosbox_setupcommand"))
                    {
                        string target = SystemConfig["dosbox_setupcommand"];
                        setupcommand = Path.GetFileName(target);
                    }

                    string foundFile = Directory.EnumerateFiles(rom)
                            .FirstOrDefault(f => Path.GetFileName(f)
                            .Equals(setupcommand, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(foundFile))
                        commandArray.Add("-c \"" + setupcommand + "\"");
                    else
                        SimpleLogger.Instance.Info("[DosBoxStaging] Setup command file not found: " + setupcommand);
                }

                else if (SystemConfig.isOptSet("dosbox_exerun"))
                {
                    string autoexeFile = SystemConfig["dosbox_exerun"];
                    string autoEXE = Path.GetFileName(autoexeFile);

                    string foundFileEXE = Directory.EnumerateFiles(rom)
                            .FirstOrDefault(f => Path.GetFileName(f)
                            .Equals(autoEXE, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(foundFileEXE))
                        commandArray.Add("-c \"" + autoEXE + "\"");
                }
            }

            if (zip_autoboot && !string.IsNullOrEmpty(autoboot))
            {
                if (Path.IsPathRooted(autoboot))
                {
                    string fullPath = autoboot;
                    string directory = Path.GetDirectoryName(fullPath);
                    string fileName = Path.GetFileName(fullPath);

                    commandArray.Add("-c \"cd " + directory + "\"");
                    commandArray.Add("-c \"" + fileName + "\"");
                }
            }

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void ConfigureDosBox(string confFile, bool fullscreen)
        {
            try
            {
                using (IniFile ini = new IniFile(confFile, IniOptions.KeepEmptyLines | IniOptions.UseSpaces | IniOptions.KeepEmptyValues))
                {
                    // sdl section
                    ini.AppendValue("sdl", "fullscreen", fullscreen ? "true" : "false");
                    BindIniFeature(ini, "sdl", "output", "dbs_output", "opengl");
                    BindIniFeature(ini, "sdl", "texture_renderer", "dbs_texture_renderer", "auto");
                    BindIniFeature(ini, "sdl", "display", "MonitorIndex", "0");
                    ini.AppendValue("sdl", "fullresolution", "desktop");
                    BindIniFeature(ini, "sdl", "host_rate", "dbs_host_rate", "auto");
                    BindIniFeature(ini, "sdl", "vsync", "dbs_vsync", "auto");
                    BindIniFeature(ini, "sdl", "presentation_mode", "dbs_presentation_mode", "auto");

                    // dosbox section
                    BindIniFeature(ini, "dosbox", "machine", "dbs_machine", "svga_s3");
                    BindIniFeature(ini, "dosbox", "memsize", "dbs_memsize", "16");
                    BindIniFeature(ini, "dosbox", "vmemsize", "dbs_vmemsize", "auto");
                    BindBoolIniFeatureOn(ini, "dosbox", "vga_render_per_scanline", "dbs_vga_render_per_scanline", "true", "false");
                    BindBoolIniFeatureOn(ini, "dosbox", "automount", "dbs_automount", "true", "false");
                    BindIniFeature(ini, "dosbox", "startup_verbosity", "dbs_startup_verbosity", "auto");

                    // render section
                    BindIniFeature(ini, "render", "glshader", "dbs_glshader", "crt-auto");
                    BindIniFeature(ini, "render", "aspect", "dbs_aspect", "auto");
                    BindIniFeature(ini, "render", "integer_scaling", "dbs_integer_scaling", "auto");

                    // cpu section
                    BindIniFeature(ini, "cpu", "core", "dbs_core", "auto");
                    BindIniFeature(ini, "cpu", "cputype", "dbs_cputype", "auto");
                    BindIniFeature(ini, "cpu", "cpu_cycles", "dbs_cpu_cycles", "3000");
                    BindIniFeature(ini, "cpu", "cpu_cycles_protected", "dbs_cpu_cycles_protected", "60000");
                    BindBoolIniFeature(ini, "cpu", "cpu_throttle", "dbs_cpu_throttle", "true", "false");

                    // voodoo section
                    BindBoolIniFeatureOn(ini, "voodoo", "voodoo", "dbs_voodoo", "true", "false");
                    BindIniFeature(ini, "voodoo", "voodoo_memsize", "dbs_voodoo_memsize", "4");
                    BindBoolIniFeatureOn(ini, "voodoo", "voodoo_bilinear_filtering", "dbs_voodoo_bilinear_filtering", "true", "false");

                    // midi section
                    if (SystemConfig.isOptSet("dosbox_midi") && !string.IsNullOrEmpty(SystemConfig["dosbox_midi"]))
                    {
                        string midiDevice = SystemConfig["dosbox_midi"];

                        switch (midiDevice)
                        {
                            case "system":
                                ini.AppendValue("midi", "mididevice", "win32");
                                break;
                            case "disabled":
                                ini.AppendValue("midi", "mididevice", "none");
                                break;
                            case "Roland_SC-55.sf2":
                                ini.AppendValue("midi", "mididevice", "fluidsynth");
                                ini.AppendValue("fluidsynth", "soundfont", "Roland_SC-55.sf2");
                                break;
                            case "Roland_SC-88.sf2":
                                ini.AppendValue("midi", "mididevice", "fluidsynth");
                                ini.AppendValue("fluidsynth", "soundfont", "Roland_SC-88.sf2");
                                break;
                            case "MT32_CONTROL.ROM":
                                ini.AppendValue("midi", "mididevice", "mt32");
                                ini.AppendValue("mt32", "romdir", "");
                                break;
                        }
                    }
                    else
                        ini.AppendValue("midi", "mididevice", "auto");

                    // sblaster section
                    BindIniFeature(ini, "sblaster", "sbtype", "dbs_sbtype", "sb16");
                    BindIniFeature(ini, "sblaster", "oplmode", "dbs_oplmode", "auto");
                    BindIniFeature(ini, "sblaster", "cms", "dbs_cms", "auto");

                    if (SystemConfig.isOptSet("dosbox_sblaster_conf") && !string.IsNullOrEmpty(SystemConfig["dosbox_sblaster_conf"]))
                    {
                        string sblasterConf = SystemConfig["dosbox_sblaster_conf"];

                        string[] parts = sblasterConf.Split(' ');

                        string sbbase = parts.FirstOrDefault(p => p.StartsWith("A"));
                        ini.AppendValue("sblaster", "sbbase", sbbase != null ? sbbase.Substring(1) : "220");
                        string irq = parts.FirstOrDefault(p => p.StartsWith("I"));
                        ini.AppendValue("sblaster", "irq", irq != null ? irq.Substring(1) : "7");
                        string dma = parts.FirstOrDefault(p => p.StartsWith("D"));
                        ini.AppendValue("sblaster", "dma", dma != null ? dma.Substring(1) : "1");
                        string hdma = parts.FirstOrDefault(p => p.StartsWith("H"));
                        ini.AppendValue("sblaster", "hdma", hdma != null ? hdma.Substring(1) : "5");

                    }
                    else
                    {
                        ini.AppendValue("sblaster", "sbbase", "220");
                        ini.AppendValue("sblaster", "irq", "7");
                        ini.AppendValue("sblaster", "dma", "1");
                        ini.AppendValue("sblaster", "hdma", "5");
                    }

                    // gus and imfc section
                    BindBoolIniFeature(ini, "gus", "gus", "dbs_gus", "true", "false");
                    BindBoolIniFeature(ini, "imfc", "imfc", "dbs_imfc", "true", "false");

                    // speaker section
                    BindIniFeature(ini, "speaker", "tandy", "dbs_tandy", "auto");
                    BindIniFeature(ini, "speaker", "lpt_dac", "dbs_lpt_dac", "none");
                    BindBoolIniFeature(ini, "speaker", "ps1audio", "dbs_ps1audio", "true", "false");

                    // realmagic section
                    BindIniFeature(ini, "reelmagic", "reelmagic", "dbs_reelmagic", "off");

                    // joystick section
                    BindIniFeature(ini, "joystick", "joysticktype", "dbs_joysticktype", "auto");
                    BindBoolIniFeatureOn(ini, "joystick", "timed", "dbs_timed", "true", "false");
                    BindBoolIniFeature(ini, "reelmagic", "autofire", "dbs_autofire", "true", "false");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[DosBoxStaging] Error configuring DosBox: " + ex.Message);
            }
        }

        private void SearchAndCopySoundFiles(string path)
        {
            string targetPathFluidSynth = Path.Combine(path, "soundfonts");
            FileTools.EnsureDirectoryExists(targetPathFluidSynth);

            string targetPathmt32 = Path.Combine(path, "mt32-roms");
            FileTools.EnsureDirectoryExists(targetPathmt32);

            try
            {
                string mtcontrolsource = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "MT32_CONTROL.ROM");
                string mtcontroltarget = Path.Combine(targetPathmt32, "MT32_CONTROL.ROM");
                if (!File.Exists(mtcontroltarget) && File.Exists(mtcontrolsource))
                    try { File.Copy(mtcontrolsource, mtcontroltarget); } catch { }

                string mtpcmsource = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "MT32_PCM.ROM");
                string mtpcmtarget = Path.Combine(targetPathmt32, "MT32_PCM.ROM");
                if (!File.Exists(mtpcmtarget) && File.Exists(mtpcmsource))
                    try { File.Copy(mtpcmsource, mtpcmtarget); } catch { }

                string roland55source = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "Roland_SC-55.sf2");
                string roland55target = Path.Combine(targetPathFluidSynth, "Roland_SC-55.sf2");
                if (!File.Exists(roland55target) && File.Exists(roland55source))
                    try { File.Copy(roland55source, roland55target); } catch { }

                string roland88source = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "Roland_SC-88.sf2");
                string roland88target = Path.Combine(targetPathFluidSynth, "Roland_SC-88.sf2");
                if (!File.Exists(roland88target) && File.Exists(roland88source))
                    try { File.Copy(roland88source, roland88target); } catch { }
            }
            catch
            {
                SimpleLogger.Instance.Warning("[WARNING] Failed to copy MIDI sound file for DosBox Pure.");
            }
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
    }
}
