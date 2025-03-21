﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class ForceEngineGenerator : Generator
    {
        public ForceEngineGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("theforceengine");

            string exe = Path.Combine(path, "TheForceEngine.exe");
            if (!File.Exists(exe))
                return null;

            // Get mod to apply if any (must be put in rom file first line)
            string mod = null;
            var lines = File.ReadAllLines(rom);
            if (lines.Length > 0)
                mod = lines[0];

            SetupForceEngine(rom, path);

            var commandArray = new List<string>();

            if (SystemConfig.isOptSet("forceengine_cutscenes") && SystemConfig.getOptBoolean("forceengine_cutscenes"))
                commandArray.Add("-c0");
            else
                commandArray.Add("-c1");

            if (mod != null)
                commandArray.Add("-u" + mod);

            commandArray.Add("-gdark");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }
        private void SetupForceEngine(string rom, string path)
        {
            string settings = Path.Combine(path, "settings.ini");

            if (!File.Exists(settings))
                try { File.WriteAllText(settings, ""); }
                catch { return; }

            using (var ini = IniFile.FromFile(settings))
            {
                // Game options
                string romPath = Path.GetDirectoryName(rom).Replace("\\", "/");
                ini.WriteValue("Dark_Forces", "sourcePath", "\"" + romPath + "\"");

                BindBoolIniFeature(ini, "Dark_Forces", "enableAutoaim", "forceengine_autoaim", "false", "true");

                // Graphics
                bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
                if (fullscreen)
                    ini.WriteValue("Window", "fullscreen", "true");
                else
                    ini.WriteValue("Window", "fullscreen", "false");

                BindBoolIniFeatureOn(ini, "Graphics", "vsync", "vsync", "true", "false");
                BindBoolIniFeatureOn(ini, "Graphics", "widescreen", "forceengine_widescreen", "true", "false");
                BindBoolIniFeature(ini, "Graphics", "show_fps", "forceengine_fps", "true", "false");
                BindBoolIniFeatureOn(ini, "Graphics", "useBilinear", "forceengine_smooth", "true", "false");
                BindBoolIniFeatureOn(ini, "Graphics", "useMipmapping", "forceengine_mipmap", "true", "false");
                BindBoolIniFeature(ini, "Graphics", "colorCorrection", "forceengine_color_correction", "true", "false");
                BindBoolIniFeatureOn(ini, "Graphics", "bloomEnabled", "forceengine_bloom", "true", "false");
                BindIniFeature(ini, "Graphics", "renderer", "forceengine_renderer", "1");
                BindIniFeature(ini, "Graphics", "colorMode", "forceengine_color_mode", "2");
                BindIniFeature(ini, "Graphics", "skyMode", "forceengine_sky_mode", "1");
                BindBoolIniFeature(ini, "Graphics", "reticleEnable", "forceengine_crosshair", "false", "true");

                if (SystemConfig.isOptSet("forceengine_internal_resolution") && !string.IsNullOrEmpty(SystemConfig["forceengine_internal_resolution"]))
                {
                    string res = SystemConfig["forceengine_internal_resolution"];
                    string[] parts = res.Split('x');
                    string width = parts[0];
                    string height = parts[1];
                    ini.WriteValue("Graphics", "gameHeight", height);
                    ini.WriteValue("Graphics", "gameWidth", width);
                }
                else
                {
                    var res = ScreenResolution.CurrentResolution;
                    ini.WriteValue("Graphics", "gameHeight", res.Height.ToString());
                    ini.WriteValue("Graphics", "gameWidth", res.Width.ToString());
                }

                // System
                ini.WriteValue("System", "gameExitsToMenu", "false");
                ini.WriteValue("System", "returnToModLoader", "false");

                // A11y
                if (SystemConfig.isOptSet("forceengine_subtitles") && !string.IsNullOrEmpty(SystemConfig["forceengine_subtitles"]))
                {
                    if (SystemConfig["forceengine_subtitles"] == "cutscenes")
                    {
                        ini.WriteValue("A11y", "showCutsceneSubtitles", "true");
                        ini.WriteValue("A11y", "showGameplaySubtitles", "false");
                    }
                    else if (SystemConfig["forceengine_subtitles"] == "gameplay")
                    {
                        ini.WriteValue("A11y", "showCutsceneSubtitles", "false");
                        ini.WriteValue("A11y", "showGameplaySubtitles", "true");
                    }
                    else if (SystemConfig["forceengine_subtitles"] == "both")
                    {
                        ini.WriteValue("A11y", "showCutsceneSubtitles", "true");
                        ini.WriteValue("A11y", "showGameplaySubtitles", "true");
                    }
                }
                else
                {
                    ini.WriteValue("A11y", "showCutsceneSubtitles", "false");
                    ini.WriteValue("A11y", "showGameplaySubtitles", "false");
                }

                ini.Save();
            }
        }
    }
}
