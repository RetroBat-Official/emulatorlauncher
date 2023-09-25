using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using emulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using System.Threading;
using System.Xml.Linq;
using System.Drawing;

namespace emulatorLauncher
{
   partial class MesenGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mesen");

            string exe = Path.Combine(path, "Mesen.exe");
            if (!File.Exists(exe))
                return null;

            // settings (xml configuration)
            SetupConfiguration(path, rom);

            // command line parameters
            var commandArray = new List<string>();

            commandArray.Add("\"" + rom + "\"");
            commandArray.Add("/fullscreen");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfiguration(string path, string rom)
        {
            string settingsFile = Path.Combine(path, "settings.xml");

            var xdoc = File.Exists(settingsFile) ? XElement.Load(settingsFile) : new XElement("Configuration");
            BindFeature(xdoc, "Region", "mesen_region", "Auto");

            // Emulator preferences
            var preference = xdoc.GetOrCreateElement("PreferenceInfo");
            preference.SetElementValue("DisplayLanguage", "SystemDefault");
            preference.SetElementValue("SingleInstance", "true");
            preference.SetElementValue("PauseWhenInBackground", "true");
            preference.SetElementValue("PauseWhenInMenusAndConfig", "true");
            preference.SetElementValue("PauseWhenInDebuggingTools", "true");
            preference.SetElementValue("AutoLoadIpsPatches", "true");

            if (SystemConfig.isOptSet("mesen_autosave") && SystemConfig["mesen_autosave"] != "false")
            {
                preference.SetElementValue("AutoSave", "true");
                preference.SetElementValue("AutoSaveDelay", SystemConfig["mesen_autosave"]);
            }
            else
                preference.SetElementValue("AutoSave", "false");

            preference.SetElementValue("AutomaticallyCheckForUpdates", "false");

            if (SystemConfig.isOptSet("mesen_osd") && SystemConfig.getOptBoolean("mesen_osd"))
            {
                preference.SetElementValue("DisableOsd", "false");
                preference.SetElementValue("AutoSaveNotify", "true");
            }
            else
                preference.SetElementValue("DisableOsd", "true");

            BindBoolFeature(preference, "ShowGameTimer", "mesen_timecounter", "true", "false");
            preference.SetElementValue("ConfirmExitResetPower", "false");

            // define folders
            string recordsFolder = Path.Combine(AppConfig.GetFullPath("records"), "output", "mesen");
            if (!Directory.Exists(recordsFolder)) try { Directory.CreateDirectory(recordsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(recordsFolder) && Directory.Exists(recordsFolder))
            {
                preference.SetElementValue("OverrideAviFolder", "true");
                preference.SetElementValue("AviFolder", recordsFolder);
            }

            string savesFolder = Path.Combine(AppConfig.GetFullPath("saves"), "nes", "mesen");
            if (!Directory.Exists(savesFolder)) try { Directory.CreateDirectory(savesFolder); }
                catch { }
            if (!string.IsNullOrEmpty(savesFolder) && Directory.Exists(savesFolder))
            {
                preference.SetElementValue("OverrideSaveDataFolder", "true");
                preference.SetElementValue("SaveDataFolder", savesFolder);
            }

            string saveStateFolder = Path.Combine(AppConfig.GetFullPath("saves"), "nes", "mesen", "SaveStates");
            if (!Directory.Exists(saveStateFolder)) try { Directory.CreateDirectory(saveStateFolder); }
                catch { }
            if (!string.IsNullOrEmpty(saveStateFolder) && Directory.Exists(saveStateFolder))
            {
                preference.SetElementValue("OverrideSaveStateFolder", "true");
                preference.SetElementValue("SaveStateFolder", saveStateFolder);
            }

            string screenshotsFolder = Path.Combine(AppConfig.GetFullPath("screenshots"), "mesen");
            if (!Directory.Exists(screenshotsFolder)) try { Directory.CreateDirectory(screenshotsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(screenshotsFolder) && Directory.Exists(screenshotsFolder))
            {
                preference.SetElementValue("OverrideScreenshotFolder", "true");
                preference.SetElementValue("ScreenshotFolder", screenshotsFolder);
            }

            // Video menu
            var video = xdoc.GetOrCreateElement("VideoInfo");
            BindBoolFeature(video, "ShowFPS", "mesen_fps", "true", "false");
            BindFeature(video, "VideoFilter", "mesen_filter", "None");
            BindBoolFeature(video, "UseBilinearInterpolation", "bilinear_filtering", "true", "false");
            BindFeature(video, "AspectRatio", "mesen_ratio", "Auto");
            BindBoolFeature(video, "VerticalSync", "mesen_vsync", "false", "true");
            BindBoolFeature(video, "UseHdPacks", "mesen_customtextures", "true", "false");
            BindFeature(video, "ScanlineIntensity", "mesen_scanlines", "0");
            BindBoolFeature(video, "RemoveSpriteLimit", "mesen_spritelimit", "true", "false");
            BindBoolFeature(video, "FullscreenForceIntegerScale", "integerscale", "true", "false");

            // Emulation menu
            var emulation = xdoc.GetOrCreateElement("EmulationInfo");
            BindFeature(emulation, "RunAheadFrames", "mesen_runahead", "0");

            // Controllers configuration
            SetupControllers(xdoc);

            // Save xml file
            xdoc.Save(settingsFile);
        }
    }
}
