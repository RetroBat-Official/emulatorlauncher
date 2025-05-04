using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System;

namespace EmulatorLauncher
{
    class SolarusGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator);

            string exe = Path.Combine(path, "solarus-run.exe");
            if (!File.Exists(exe))
                return null;

            string savesFileDir = null;
            if (GetSavesFolderName(rom, out string optionDir))
            {
                savesFileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".solarus", optionDir);
            }

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            var commandArray = new List<string>();
            
            if (fullscreen)
                commandArray.Add("-fullscreen=yes");
            else
                commandArray.Add("-fullscreen=no");

            commandArray.Add("\"" + rom + "\"");

            

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
            {
                ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);
                return 0;
            }
            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);
            return ret;
        }

        private bool GetSavesFolderName(string rom, out string dir)
        {
            dir = string.Empty;
            using (var archive = Zip.Open(rom))
            {
                var entries = archive.Entries.ToList();
                string tempDirectory = Path.Combine(Path.GetTempPath(), "rb_solarus");

                // Find quest.dat File
                var questDat = entries.FirstOrDefault(e => e.Filename == "quest.dat");
                if (questDat != null)
                {
                    questDat.Extract(tempDirectory);

                    // Process upgrade actions
                    string questTempFile = Path.Combine(tempDirectory, "quest.dat");

                    if (File.Exists(questTempFile))
                    {
                        string content = File.ReadAllText(questTempFile);

                        Match match = Regex.Match(content, @"write_dir\s*=\s*""([^""]*)""");

                        if (match.Success)
                        {
                            dir = match.Groups[1].Value;
                            try { File.Delete(questTempFile); }
                            catch { }
                            return true;
                        }
                        else
                        {
                            try { File.Delete(questTempFile); }
                            catch { }
                            return false;
                        }
                    }

                    try { File.Delete(questTempFile); }
                    catch { }
                }
            }

            return false;
        }
    }
}
