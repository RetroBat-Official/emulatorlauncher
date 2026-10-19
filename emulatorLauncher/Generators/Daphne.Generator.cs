using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class HypseusGenerator : DaphneGenerator
    {
        public HypseusGenerator() { _executableName = "hypseus"; DependsOnDesktopResolution = true; }

    }

    partial class DaphneGenerator : Generator
    {
        protected override bool UseGenericScreenPlacement { get { return true; } }

        public DaphneGenerator()
        {
            _executableName = "daphne";
        }

		protected virtual void UpdateCommandline(List<string> commandArray)
        {
            if (SystemConfig.isOptSet("fastboot") && !SystemConfig.getOptBoolean("fastboot"))
                commandArray.Remove("-fastboot");

            if (_executableName == "daphne")
            {
                if (SystemConfig["ratio"] == "16/9")
                    commandArray.Add("-ignore_aspect_ratio");

                return;
            }

            // hypseus
            if (_executableName == "hypseus")
            {
                if (SystemConfig.isOptSet("smooth") && !SystemConfig.getOptBoolean("smooth"))
                    commandArray.Add("-nolinear_scale");

                if (SystemConfig.getOptBoolean("ignore_ratio"))
                    commandArray.Add("-ignore_aspect_ratio");
                else
                    commandArray.Add("-force_aspect_ratio");

                if (SystemConfig.isOptSet("hypseus_scalefactor") && !string.IsNullOrEmpty(SystemConfig["hypseus_scalefactor"]) && SystemConfig["hypseus_scalefactor"].Substring(0, 3) != "100")
                {
                    string xString = SystemConfig["hypseus_scalefactor"].ToIntegerString();
                    commandArray.Add("-scalefactor");
                    commandArray.Add(xString);
                }

                if (Program.IsMonitorIndexSet)
                {
                    // Hypseus uses SDL : -screen expects a 0-based SDL index.
                    commandArray.Add("-screen");
                    commandArray.Add(Displays.IndexOf(Program.TargetScreen, MonitorOrder.SdlLike).ToString());
                }

                if (SystemConfig.getOptBoolean("hypseus_stretch"))
                {
                    var resolution = ScreenResolution.FromScreen(Program.TargetScreen);
                    commandArray.Add("-x");
                    commandArray.Add(resolution.Width.ToString());
                    commandArray.Add("-y");
                    commandArray.Add(resolution.Height.ToString());

                    if (!commandArray.Contains("-ignore_aspect_ratio"))
                        commandArray.Add("-ignore_aspect_ratio");
                }

                if (SystemConfig.isOptSet("hypseus_renderer") && SystemConfig["hypseus_renderer"] == "vulkan")
                {
                    commandArray.Remove("-opengl");
                    commandArray.Add("-vulkan");
                }

                if (SystemConfig.isOptSet("hypseus_crosshair") && !SystemConfig.getOptBoolean("hypseus_crosshair"))
                    commandArray.Add("-nocrosshair");

                return;
            }
        }

        protected string _executableName;
        private string _daphneHomedir;
        private string _daphnePath;

        static string FindFile(string dir, string pattern, Predicate<string> predicate)
        {
            try
            {
                foreach (string f in Directory.GetFiles(dir, pattern))
                    if (predicate(f))
                        return f;

                foreach (string d in Directory.GetDirectories(dir))
                {
                    string ret = FindFile(d, pattern, predicate);
                    if (ret != null)
                        return ret;
                }                
            }
            catch { }

            return null;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            // Manage new launch from zip files with hypseus (do not unzip these)
            string zipSinge = null;
            bool zipHypseus = false;
            bool oneGun = SystemConfig.getOptBoolean("one_gun");
            if (Path.GetDirectoryName(rom).EndsWith("roms"))
            {
                zipSinge = rom;
                zipHypseus = true;
                _executableName = "hypseus";
            }
            else
                rom = this.TryUnZipGameIfNeeded(system, rom);

            string romName = Path.GetFileNameWithoutExtension(rom);
            bool useAlt = false;
            
            string singeFile = null;
            string frameFile = null;

            string romPath = Path.GetDirectoryName(rom);
            string systemRomBasePath = Path.Combine(AppConfig.GetFullPath("roms"), system);
            string[] romPathdirectories = romPath.Split(Path.DirectorySeparatorChar);
            if (romPathdirectories.Length >= 2)
            {
                string secondFromRight = romPathdirectories[romPathdirectories.Length - 2];
                if (secondFromRight == "vldp" && (Path.GetExtension(rom) == ".actionmax" || Path.GetExtension(rom) == ".singe" || Path.GetExtension(rom) == ".altgame"))
                {
                    string romNameAlt = Path.GetFileNameWithoutExtension(romPath);
                    zipSinge = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(romPath)), "roms", romNameAlt + ".zip");
                    if (zipSinge != null && File.Exists(zipSinge))
                    {
                        useAlt = true;
                        _executableName = "hypseus";
                    }
                }
            }

            // Special Treatment for actionmax games
            var ext = Path.GetExtension(rom).Replace(".", "").ToLower();
            if ((ext == "actionmax" || ext == "altgame") && !useAlt)
            {
                string expectedSingeFile = Path.Combine(Path.GetDirectoryName(rom), ext + ".hypseus", romName + ".singe");
                if (!File.Exists(expectedSingeFile))
                    throw new Exception("[ERROR] Unable to find singe file for actionmax.");

                rom = Path.Combine(Path.GetDirectoryName(rom), ext + ".hypseus");
            }

            string commandsFile = romPath + "\\" + romName + ".commands";

            if (!useAlt && !zipHypseus)
            {
                singeFile = rom + "\\" + romName + ".singe";
                if (!File.Exists(singeFile))
                    singeFile = FindFile(rom, "*.singe", f => Path.GetFileNameWithoutExtension(f).StartsWith(romName));

                frameFile = rom + "\\" + romName + ".txt";
                if (File.Exists(singeFile))
                {
                    _executableName = "hypseus";
                    if (!File.Exists(frameFile))
                    {
                        frameFile = FindFile(rom, "*.txt", f => Path.GetFileNameWithoutExtension(f).StartsWith(romName));
                        if (frameFile == null)
                            frameFile = FindFile(rom, "*.mp4", f => Path.GetFileNameWithoutExtension(f).StartsWith(romName));
                    }
                }
            }
            else
            {
                string basePath = Path.GetDirectoryName(romPath);

                if (zipHypseus)
                {
                    frameFile = Path.Combine(basePath, "vldp", romName, romName + ".txt");
                }
                else
                {
                    singeFile = null;
                    frameFile = rom.Replace(".singe", ".txt").Replace(".actionmax", ".txt").Replace(".altgame", ".txt");
                }

                if (!File.Exists(frameFile))
                    throw new Exception("[ERROR] Framefile (.txt) does not exist in vldp/<game> subfolder.");
            }

            string emulatorPath = AppConfig.GetFullPath(_executableName);
            string exe = Path.Combine(emulatorPath, _executableName + ".exe");
            _daphnePath = AppConfig.GetFullPath("daphne");

            if (!File.Exists(exe))
            {
                ExitCode = ExitCodes.EmulatorNotInstalled;
                return null;
            }

            if (emulator == "hypseus")
                CreateControllerConfiguration(emulatorPath, (singeFile != null && File.Exists(singeFile)) || useAlt || zipHypseus);

            List<string> commandArray = new List<string>();

            // extension used .daphne and the file to start the game is in the folder .daphne with the extension .txt
        
            string daphneDatadir = emulatorPath;
            _daphneHomedir = Path.GetDirectoryName(rom);

            if ((File.Exists(singeFile) || useAlt || zipHypseus) && _executableName == "hypseus")
            {
                _daphneHomedir = emulatorPath;

                if (zipHypseus)
                {
                    commandArray.AddRange(new string[]
                    {
                        "singe",
                        "vldp",
                        //"-retropath", // Requires the CreateSymbolicLink
                        "-singedir", systemRomBasePath,
                        "-zlua", zipSinge,
                        "-framefile", frameFile,
                        oneGun? "" : "-manymouse",
                        "-datadir", _daphneHomedir,
                        "-homedir", _daphneHomedir
                    });
                }
                else if (useAlt)
                {
                    commandArray.AddRange(new string[]
                    {
                        "singe",
                        "vldp",
                        //"-retropath", // Requires the CreateSymbolicLink
                        "-singedir", systemRomBasePath,
                        "-zlua", zipSinge,
                        "-usealt", romName,
                        "-framefile", frameFile,
                        oneGun? "" : "-manymouse",
                        "-datadir", _daphneHomedir,
                        "-homedir", _daphneHomedir
                    });
                }
                else
                {
                    commandArray.AddRange(new string[]
                    {
                        "singe",
                        "vldp",
                        //"-retropath", // Requires the CreateSymbolicLink
                        "-singedir", romPath,
                        "-framefile", frameFile,
                        "-script", singeFile,
                        oneGun? "" : "-manymouse",
                        "-datadir", _daphneHomedir,
                        "-homedir", _daphneHomedir
                    });
                }

                if (RawLightgun.GetRawLightguns().Any(gun => gun.Type == RawLighGunType.SindenLightgun))
                {
                    commandArray.AddRange(new string[] { "-sinden", "2", "w" });
                    Guns.StartSindenSoftware();
                }
            }
            else
            {
                if (emulator == "daphne")
                {
                    commandArray.AddRange(new string[]
                    {
                        romName,
                        "vldp",
                        "-framefile", frameFile,
                        "-useoverlaysb", "2",
                        "-homedir", _daphneHomedir
                    });
                }
                else                 {
                    commandArray.AddRange(new string[]
                    {
                        romName,
                        "vldp",
                        "-framefile", frameFile,
                        "-homedir", _daphneHomedir
                    });
                }
            }
            
            bool fullscreen = ShouldRunFullscreen();

            if (fullscreen)
                commandArray.Add("-fullscreen");

            /* 
            In future we can use -gamepad to use SDL codes for controller settings, however this does not currently allow to specify the controller index...
            It will also need modification of daphne.controllers.cs with SDL codes instead of dinput numbers
            if (this.Controllers.Any(c => !c.IsKeyboard))
                commandArray.Add("-gamepad");
            */

            if (emulator == "daphne")
            {
                commandArray.Add("-x");
                commandArray.Add((resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString());

                commandArray.Add("-y");
                commandArray.Add((resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString());
            }

            commandArray.Add("-opengl");            
            commandArray.Add("-fastboot");

            if (emulator == "hypseus" && SystemConfig.isOptSet("daphne_vsync") && !SystemConfig.getOptBoolean("daphne_vsync"))
                commandArray.Add("-novsync");

            UpdateCommandline(commandArray);       
            
            // The folder may have a file with the game name and .commands with extra arguments to run the game.
            if (File.Exists(commandsFile))
            {
                string[] file = File.ReadAllText(commandsFile).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < file.Length; i++)
                {
                    string s = file[i];

                    if (_executableName == "daphne")
                    {
                        if (s == "singe" && commandArray[0] != "singe")
                        {
                            commandArray[0] = "singe";
                            continue;
                        }

                        if (s == romName || s == "singe" || s == "vdlp" || s == "-fullscreen" ||
                            s == "-opengl" || s == "-vulkan" || s == "-fastboot" || s == "-retropath" || s == "-manymouse" 
                            || s == "-ignore_aspect_ratio")
                            continue;

                        if (s == "-x" || s == "-y" || s == "-framefile" || s == "-script" || s == "script" || s == "-useoverlaysb" || s == "-homedir" || s == "-datadir")
                        {
                            i++;
                            continue;
                        }

                        commandArray.Add(s);
                    }
                    
                    else if (_executableName == "hypseus")
                    {
                        if (s == "singe" && commandArray[0] != "singe")
                        {
                            commandArray[0] = "singe";
                            continue;
                        }

                        if (s == romName || s == "singe" || s == "vdlp" || s == "-fullscreen" ||
                            s == "-opengl" || s == "-vulkan" || s == "-fastboot" || s == "-retropath" || s == "-manymouse" || s == "force_aspect_ratio" || 
                            s == "-ignore_aspect_ratio" || s == " - novsync" || s == "-nolinear_scale" || s == "-nocrosshair")
                            continue;

                        if (s == "-framefile" || s == "-script" || s == "script" || s == "-useoverlaysb" || s == "-homedir" ||
                            s == "-datadir" || s == "-scalefactor" || s == "singedir" || s == "-bezel")
                        {
                            i++;
                            continue;
                        }

                        if (s == "-sinden")
                        {
                            i++;
                            i++;
                            continue;
                        }

                        commandArray.Add(s);
                    }
                }
            }

            if (emulator == "daphne")
            {
                if (SystemConfig["ratio"] == "16/9")
                    SystemConfig["bezel"] = "none";

                ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadeManager.GetPlatformFromFile(exe), system, rom, emulatorPath, resolution, emulator);
            }
            else if (emulator == "hypseus")
            {
                string gameDataPath = !string.IsNullOrEmpty(frameFile) ? Path.GetDirectoryName(frameFile) : rom;
                GetBezelToApply(system, rom, romName, gameDataPath, resolution, emulator, out string bezelName);

                if (!string.IsNullOrEmpty(bezelName))
                {
                    commandArray.Add("-bezel");
                    commandArray.Add("\"" + bezelName + "\"");
                }

                return new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = string.Join(" ", commandArray),
                    UseShellExecute = false, CreateNoWindow = true, // Hides console
                    WorkingDirectory = emulatorPath,
                };
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(" ", commandArray),
                WorkingDirectory = emulatorPath,
            };
        }

        private void GetBezelToApply(string system, string rom, string romName, string gameDataPath, ScreenResolution resolution, string emulator, out string bezelName)
        {
            bezelName = null;

            string hypseusBezelPath = Path.Combine(AppConfig.GetFullPath("hypseus"), "bezels");

            // 1. User-selected bezel file (explicit emulator option, always wins)
            string userBezel = SystemConfig["hypseus_bezel"];
            if (!string.IsNullOrEmpty(userBezel) && File.Exists(userBezel))
            {
                string target = Path.Combine(hypseusBezelPath, Path.GetFileName(userBezel));
                if (CopyBezelFile(userBezel, target))
                {
                    bezelName = Path.GetFileName(target);
                    SimpleLogger.Instance.Info("[Bezel] Using user-selected bezel file: " + userBezel);
                    return;
                }
            }

            if (SystemConfig["bezel"] == "none")
                return;

            // Game-specific bezels take priority over RetroBat generic (system) bezels, only with default decoration setting
            bool defaultDecoration = string.IsNullOrEmpty(SystemConfig["bezel"]);

            if (defaultDecoration)
            {
                // 2. Hypseus native bezel named after the game
                if (File.Exists(Path.Combine(hypseusBezelPath, romName + ".png")))
                {
                    bezelName = romName + ".png";
                    SimpleLogger.Instance.Info("[Bezel] Using bezel located in emulator bezel folder: " + romName);
                    return;
                }

                // 3. Find a bezel near the game data if any exists (for example, in the vldp folder)
                if (!string.IsNullOrEmpty(gameDataPath) && Directory.Exists(gameDataPath))
                {
                    string gameBezel = null;
                    try
                    {
                        string romNameLower = romName.ToLowerInvariant();

                        // Any png file with "bezel" in its name, files also containing the game name first, then alphabetical (deterministic)
                        gameBezel = Directory.GetFiles(gameDataPath, "*.png", SearchOption.TopDirectoryOnly)
                            .Where(f =>
                            {
                                string name = Path.GetFileName(f).ToLowerInvariant();
                                return name.EndsWith(".png") && name.Contains("bezel");
                            })
                            .OrderByDescending(f => Path.GetFileName(f).ToLowerInvariant().Contains(romNameLower))
                            .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
                    }
                    catch { }

                    if (gameBezel != null)
                    {
                        // Fixed target name to never overwrite a native hypseus bezel (<romName>.png)
                        string target = Path.Combine(hypseusBezelPath, "retrobatGameBezel.png");
                        if (CopyBezelFile(gameBezel, target))
                        {
                            bezelName = Path.GetFileName(target);
                            SimpleLogger.Instance.Info("[Bezel] Using bezel found in game folder: " + gameBezel);
                            return;
                        }
                    }
                }
            }

            // 4. RetroBat bezel (decoration system, may fall back to system bezel)
            var bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            if (bezelFileInfo != null && !string.IsNullOrEmpty(bezelFileInfo.PngFile) && File.Exists(bezelFileInfo.PngFile))
            {
                string target = Path.Combine(hypseusBezelPath, "retrobatBezel.png");
                if (CopyBezelFile(bezelFileInfo.PngFile, target))
                {
                    bezelName = Path.GetFileName(target);
                    SimpleLogger.Instance.Info("[Bezel] Using RetroBat bezel: " + bezelFileInfo.PngFile);
                }
            }
        }

        private static bool CopyBezelFile(string source, string target)
        {
            try
            {
                // Source already in hypseus bezels folder: nothing to copy (and never delete it)
                if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                    return true;

                string dir = Path.GetDirectoryName(target);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.Copy(source, target, true);
                return File.Exists(target);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Unable to copy bezel file for hypseus: " + ex.Message);
                return false;
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();

            try
            {
                string ram = Path.Combine(_daphneHomedir, "ram");
                if (Directory.Exists(ram))
                    new DirectoryInfo(ram).Delete(true);

                string frameFile = Path.Combine(_daphneHomedir, "framefile");
                if (Directory.Exists(frameFile))
                    new DirectoryInfo(frameFile).Delete(true);     

                if (_executableName == "daphne")
                    ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, _daphnePath);
            }
            catch { }
        }
    }
}