using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class NosGbaGenerator : Generator
    {
        private string _emuBatteryPath;
        private string _emuSlotPath;
        private string _savesPath;
        private string _statesPath;
        private string _romName;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("nosgba");
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("no$gba");

            string exe = Path.Combine(path, "no$gba.exe");
            if (!File.Exists(exe))
                return null;

            // NO$GBA cannot read archives : extract the game to the temporary uncompressed folder
            if (Zip.IsCompressedFile(rom))
            {
                string[] romExtensions = new string[] { ".gba", ".gb", ".gbc", ".nds", ".srl" };

                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, true, false);
                if (!Directory.Exists(uncompressedRomPath))
                {
                    SetCustomError("Unable to extract the archive.");
                    return null;
                }

                string uncompressedRom = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories)
                    .OrderBy(file => Array.IndexOf(romExtensions, Path.GetExtension(file).ToLowerInvariant()))
                    .FirstOrDefault(file => romExtensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));

                if (string.IsNullOrEmpty(uncompressedRom))
                {
                    SetCustomError("No rom file found in the archive.");
                    return null;
                }

                rom = uncompressedRom;
                ValidateUncompressedGame();
            }

            bool fullscreen = ShouldRunFullscreen();

            SetupConfiguration(path, system);
            SyncSavesIn(path, system, rom);

            var commandArray = new List<string>();
            
            if (fullscreen)    
                commandArray.Add("/f");

            if (system == "gba2players" && (!SystemConfig.isOptSet("gba_nbplayers") || SystemConfig["gba_nbplayers"] == "-Two Machines"))
                    commandArray.Add("/2");

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfiguration(string path, string system)
        {
            string conf = Path.Combine(path, "NO$GBA.INI");
            if (!File.Exists(conf))
                return;

            using (var ini = IniFile.FromFile(conf, IniOptions.KeepEmptyLines | IniOptions.KeepEmptyValues | IniOptions.UseDoubleEqual | IniOptions.UseSpaces))
            {
                ini.WriteValue("", "SAV/SNA File Format", "Raw");

                // Link cable setup for gba2players
                if (system == "gba2players")
                {
                    if (SystemConfig.isOptSet("gba_nbplayers") && !string.IsNullOrEmpty(SystemConfig["gba_nbplayers"]))
                        ini.WriteValue("", "Number of Emulated Gameboys", SystemConfig["gba_nbplayers"]);
                    else
                        ini.WriteValue("", "Number of Emulated Gameboys", "-Two Machines");

                    ini.WriteValue("", "Link Cable Type", "-Automatic");
                }
                    
                else if (SystemConfig.isOptSet("gba_nbplayers") && !string.IsNullOrEmpty(SystemConfig["gba_nbplayers"]))
                {
                    ini.WriteValue("", "Number of Emulated Gameboys", SystemConfig["gba_nbplayers"]);
                    ini.WriteValue("", "Link Cable Type", "-Automatic");
                }
                
                else
                {
                    ini.WriteValue("", "Link Cable Type", "= None");
                    ini.WriteValue("", "Number of Emulated Gameboys", "-Single Machine");
                }

                if (SystemConfig.isOptSet("gba_colors") && !string.IsNullOrEmpty(SystemConfig["gba_colors"]))
                    ini.WriteValue("", "GBA Mode/Colors", SystemConfig["gba_colors"]);
                else if (Features.IsSupported("gba_colors"))
                    ini.WriteValue("", "GBA Mode/Colors", "GBA SP (backlight)");

                if (SystemConfig.isOptSet("gba_video_output") && !string.IsNullOrEmpty(SystemConfig["gba_video_output"]))
                    ini.WriteValue("", "Video Output", SystemConfig["gba_video_output"]);
                else if (Features.IsSupported("gba_video_output"))
                    ini.WriteValue("", "Video Output", "24bit True Color");

                if (SystemConfig.isOptSet("gba_video_renderer") && !string.IsNullOrEmpty(SystemConfig["gba_video_renderer"]))
                    ini.WriteValue("", "3D Renderer", SystemConfig["gba_video_renderer"]);
                else if (Features.IsSupported("gba_video_renderer"))
                    ini.WriteValue("", "3D Renderer", "nocash");
            }
        }

        private void SyncSavesIn(string path, string system, string rom)
        {
            try
            {
                _romName = Path.GetFileNameWithoutExtension(rom);
                _emuBatteryPath = Path.Combine(path, "BATTERY");
                _emuSlotPath = Path.Combine(path, "SLOT");

                // Same folder as mGBA battery saves (saves\<system>\<rom>.sav), so saves are shared
                _savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                _statesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "nosgba", "sstates");

                if (!Directory.Exists(_savesPath)) try { Directory.CreateDirectory(_savesPath); } catch { }
                if (!Directory.Exists(_statesPath)) try { Directory.CreateDirectory(_statesPath); } catch { }
                if (!Directory.Exists(_emuBatteryPath)) try { Directory.CreateDirectory(_emuBatteryPath); } catch { }
                if (!Directory.Exists(_emuSlotPath)) try { Directory.CreateDirectory(_emuSlotPath); } catch { }

                // Battery save of the launched game only
                CopyIfNewer(Path.Combine(_savesPath, _romName + ".sav"), Path.Combine(_emuBatteryPath, _romName + ".SAV"), false);

                // Save states : whole folder (states are specific to NO$GBA)
                foreach (var file in Directory.GetFiles(_statesPath))
                    CopyIfNewer(file, Path.Combine(_emuSlotPath, Path.GetFileName(file)), false);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[NosGbaGenerator] Failed to copy saves to emulator folder : " + ex.Message, ex);
                _savesPath = null;
            }
        }

        private void SyncSavesOut()
        {
            if (string.IsNullOrEmpty(_savesPath) || string.IsNullOrEmpty(_romName))
                return;

            try
            {
                CopyIfNewer(Path.Combine(_emuBatteryPath, _romName + ".SAV"), Path.Combine(_savesPath, _romName + ".sav"), true);

                if (Directory.Exists(_emuSlotPath))
                {
                    foreach (var file in Directory.GetFiles(_emuSlotPath))
                        CopyIfNewer(file, Path.Combine(_statesPath, Path.GetFileName(file)), false);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[NosGbaGenerator] Failed to copy saves back to RetroBat saves folder : " + ex.Message, ex);
            }
        }

        private static void CopyIfNewer(string source, string target, bool rawOnly)
        {
            if (!File.Exists(source))
                return;

            if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source))
                return;

            // Never export a NO$GBA compressed battery file to the shared saves folder : other emulators can't read it
            if (rawOnly && IsNocashCompressed(source))
            {
                SimpleLogger.Instance.Warning("[NosGbaGenerator] Compressed NO$GBA save not exported : " + source);
                return;
            }

            File.Copy(source, target, true);
            SimpleLogger.Instance.Info("[NosGbaGenerator] Copied " + source + " to " + target);
        }

        private static bool IsNocashCompressed(string file)
        {
            try
            {
                byte[] header = new byte[15];
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Read(header, 0, header.Length) != header.Length)
                        return false;
                }

                return System.Text.Encoding.ASCII.GetString(header) == "NocashGbaBackup";
            }
            catch { return false; }
        }

        public override void Cleanup()
        {
            SyncSavesOut();
            base.Cleanup();
        }
    }
}
