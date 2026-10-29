using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    /// <summary>
    /// POM2 - Apple II family emulator (habib256/pom2), audited against v1.0.0.
    ///
    ///  - POM2 has no portable mode : ResourcePaths.cpp userConfigDir() reads %APPDATA% first, then
    ///    %LOCALAPPDATA%, with no override hook. APPDATA is redirected on the child process so
    ///    state.cfg lands in '<emulator>\POM2\state.cfg'. Inside POM2, APPDATA is read only there.
    ///
    ///  - '--kiosk' is the frontend mode : exclusive fullscreen, no menus, and setKioskMode() calls
    ///    settings->setReadOnly(true). POM2 reads state.cfg but never writes it back, so the file
    ///    this generator produces is authoritative.
    ///
    ///  - state.cfg is a flat, section-less "key=value" store, '#' comments on their own line only.
    ///    ConfigFile produces exactly that. Values are never paths here : Settings.cpp unescapeValue()
    ///    decodes \n \r \s \t \\, so a raw Windows path would be corrupted.
    ///
    ///  - Controllers : POM2 identifies pads by GLFW slot index (JoystickInput.h, kHostCount = 16),
    ///    not by SDL index nor by GUID - glfwGetJoystickGUID is never called. The RetroBat player
    ///    order cannot be mapped onto it, so the slot is left to the user (pom2_joystick), with the
    ///    emulator's own auto-bind as the default. There is deliberately no Pom2.Controllers.cs.
    ///
    ///  - No savestates : POM2 has no snapshot hotkey, '--snapshot-save' is a boot-time action only.
    /// </summary>
    class Pom2Generator : Generator
    {
        public Pom2Generator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        /// <summary>Image types POM2 can classify and boot (DiskImage.cpp classifyDiskForSlot).</summary>
        private static readonly string[] _diskExtensions = new string[] { ".woz", ".dsk", ".do", ".po", ".nib", ".hdv", ".2mg", ".d13" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "POM2.exe"))
                return null;

            bool fullscreen = ShouldRunFullscreen();

            // POM2 does not read archives, it classifies images by extension and content
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

            // POM2 mounts a single positional image : CliDispatcher.cpp rejects a second one
            var disks = GetGameDiskFiles(rom);
            if (disks.Count == 0)
                throw new ApplicationException("No Apple II disk image found in " + Path.GetFileName(rom));

            int discNumber = 0;
            if (SystemConfig.isOptSet("pom2_discnumber") && !string.IsNullOrEmpty(SystemConfig["pom2_discnumber"]))
                discNumber = SystemConfig["pom2_discnumber"].ToInteger();

            if (discNumber < 0 || discNumber >= disks.Count)
                discNumber = 0;

            rom = disks[discNumber];

            if (!File.Exists(rom))
                throw new ApplicationException("File '" + rom + "' does not exist");

            if (disks.Count > 1)
                SimpleLogger.Instance.Info("[Generator] POM2 mounts one image at a time, booting '" + Path.GetFileName(rom) + "' - swap from the in-game disk menu.");

            // POM2 reads the file's read-only attribute as the write-protect notch (MediaNotch.h),
            // so the working copy carries the protection, exactly like AppleWin.
            bool diskWritable;
            rom = ResolveDiskWorkingCopy(system, emulator, rom, out diskWritable, true);

            // Portable configuration : mirrors ResourcePaths.cpp userConfigDir(), <APPDATA>\POM2
            string configPath = Path.Combine(path, "POM2");
            if (!Directory.Exists(configPath))
            {
                try { Directory.CreateDirectory(configPath); }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[Generator] Cannot create " + configPath + " : " + ex.Message); }
            }

            SetupConfiguration(configPath);

            var commandArray = new List<string>();

            // Exclusive fullscreen, no menus, and settings become read-only
            if (fullscreen)
                commandArray.Add("--kiosk");

            // The positional image must stay last : the first non-flag argument is the boot disk
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                // Needed so ResourcePaths finds roms\ and fonts\ beside POM2.exe, and so the
                // HOME-less imgui.ini fallback stays inside the emulator folder
                WorkingDirectory = path,
                UseShellExecute = false
            };

            // Redirect the per-user settings directory into the emulator folder
            ret.EnvironmentVariables["APPDATA"] = path;

            // Safety net : if the working copy could not be made we are mounting the user's own file,
            // and this is the only lever that protects it (MediaWritePolicy.h). AppleWin has no
            // equivalent, which is why it always gets a copy.
            if (!diskWritable)
                ret.EnvironmentVariables["POM2_MEDIA_WRITE_DEFAULT"] = "protected";

            return ret;
        }

        /// <summary>
        /// Write POM2's state.cfg. Flat "key=value" store, no sections (Settings.cpp load/save).
        /// Unknown keys already in the file are preserved.
        /// </summary>
        private void SetupConfiguration(string configPath)
        {
            string cfgFile = Path.Combine(configPath, "state.cfg");

            try
            {
                var cfg = ConfigFile.FromFile(cfgFile, new ConfigFileOptions() { CaseSensitive = true, KeepEmptyValues = true });

                // Machine. POM2's own fresh-install default is "iie-pal" (//e Enhanced PAL, 50Hz,
                // MainWindow.cpp:853) - deliberately not used : almost every Apple II title is NTSC,
                // and "iie" also matches the ROM auto-probe, which avoids a second cold reset.
                BindFeature(cfg, "system_profile", "pom2_machine", "iie");
                BindFeature(cfg, "char_rom_locale", "pom2_charrom", "default");
                BindFeature(cfg, "ramworks_banks", "pom2_ramworks", "1");

                // Video
                BindFeature(cfg, "hi_res_mode", "pom2_video", "ColorCompositeOE");
                BindFeature(cfg, "aspect_mode", "pom2_aspect", "square");
                BindBoolFeatureOn(cfg, "crt_effects_enabled", "pom2_crteffects", "true", "false");

                // Disks
                BindBoolFeature(cfg, "disk_turbo", "pom2_diskturbo", "true", "false");

                // Slot 4 sound card. Ignored on the //c profiles, which have no expansion slots.
                BindFeature(cfg, "slot_4_card", "pom2_slot4", "mockingboard");

                // Audio
                BindBoolFeature(cfg, "floppy_sound_muted", "pom2_nofloppysound", "true", "false");

                // Game port. "-2" is the "key absent" sentinel POM2 uses to keep its own auto-bind
                // (MainWindow.cpp:538) - writing it is how AUTOMATIC stays automatic.
                BindFeature(cfg, "joystick_host", "pom2_joystick", "-2");
                BindFeature(cfg, "joystick_deadzone", "pom2_deadzone", "0.00");

                // Mouse as paddles 0/1 (PaddleMouse.h). Off by default, like POM2 itself : it
                // replaces the pad's paddles, so forcing it on would break joystick games.
                BindBoolFeature(cfg, "paddle_mouse", "pom2_paddlemouse", "true", "false");
                BindFeature(cfg, "paddle_mouse_range", "pom2_paddlemouse_range", "100");

                // The AI control server binds an HTTP port. Off by default, but a value persisted by
                // a windowed session would survive : force it.
                cfg["ai_control_enable"] = "false";

                // POM2 remounts the media named by these keys at startup. Clear them so a launch
                // boots the requested image and nothing else.
                cfg["disk_path"] = "";
                cfg["hdv_path"] = "";

                cfg.Save(cfgFile, false);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Cannot write POM2 state.cfg : " + ex.Message);
            }
        }
    }
}