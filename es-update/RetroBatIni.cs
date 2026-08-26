using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace es_update
{
    public class RetroBatIni
    {
        #region Template

        /// <summary>
        /// Returns the reference content: the template file if it is present and valid,
        /// the built-in copy otherwise. Both must stay in sync (see AssertTemplateMatchesFallback).
        /// </summary>
        public static string GetDefaultIniContent(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                rootPath = Path.GetDirectoryName(typeof(RetroBatIni).Assembly.Location);

            string templateFile = Path.Combine(rootPath, "system", "resources", "retrobat_template.ini");

            try
            {
                if (File.Exists(templateFile))
                {
                    string content = File.ReadAllText(templateFile);

                    if (!string.IsNullOrWhiteSpace(content) && content.Contains("[RetroBat]"))
                        return content;

                    SimpleLogger.Instance.Warning("[WARNING] retrobat_template.ini is empty or invalid, using built-in defaults.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Cannot read retrobat_template.ini (" + ex.Message + "), using built-in defaults.");
            }

            return GetBuiltInIniContent();
        }

        /// <summary>
        /// Built-in fallback. Keep this ASCII-only: the file may be rewritten using the
        /// user's original ANSI codepage, which cannot represent arbitrary characters.
        /// </summary>
        private static string GetBuiltInIniContent()
        {
            return @"; RETROBAT GLOBAL CONFIG FILE

[RetroBat]

; At startup RetroBat will detect or not the language used in Windows to set automatically the same language in the frontend and RetroArch emulator.
LanguageDetection=1

; At startup RetroBat will reset the default config files options of emulationstation and retrobat.ini.
; Use at your own risk.	
ResetConfigMode=0

; Run automatically RetroBat at Windows startup (0=NO 1=STARTUP 2=REGISTRY).
Autostart=0

; Set the Start Delay for RetroBat to start automatically at startup (in milliseconds).
AutoStartDelay=0

; Run WiimoteGun at RetroBat's startup. You can use your wiimote as a gun and navigate through EmulationStation.
WiimoteGun=0

; Path to an additional application to launch in parallel with RetroBat (.exe, .bat, .cmd...). Leave empty to disable. Use quotes if the path contains spaces.
; Add -nowindow at the end to start it without a visible window (e.g. AppLauncher=""C:\tools\app.exe"" -nowindow). Without -nowindow, it starts normally.
; To launch more than one application, add AppLauncher2, AppLauncher3, etc. with the same syntax.
AppLauncher=

[SplashScreen]

; Set if video introduction is played before running the interface.
EnableIntro=1

; The name of the video file to play. RandomVideo must be set on 0 to take effect.
FileName=""retrobat-neon.mp4""

; If 'default' is set, RetroBat will use the default video path where video files are stored.
; Enter a full path to use a custom directory for video files.
FilePath=""default""

; Play video files randomly when RetroBat starts.
RandomVideo=1

; Set the delay between the start of the video and the start of the interface.
; Setting a longer delay can help if the video is not displayed in the foreground
VideoDelay=1000

; By default RetroBat loads EmulationStation in parallel of the intro video, setting this to '1' tells RetroBat to wait for the video to finish before loading ES
WaitForVideoEnd=1

; Set this to stop when video automatically when the interface has loaded
KillVideoWhenESReady=0

; Allow killing intro video with Gamepad press (this only works with XInput controllers)
GamepadVideoKill=1

[EmulationStation]

; Start the frontend in fullscreen or in windowed mode.
Fullscreen=1

; Borderless Fullscreen
FullscreenBorderless=1

; Force the fullscreen resolution with the parameters set at WindowXSize and WindowYSize.
ForceFullscreenRes=0

; Select EmulationStation theme randomly.
RandomTheme=0

; Force to retry to get focus after a certain amount of time (milliseconds).
FocusDelay=2000

; The frontend will parse only the gamelist.xml files in roms directories to display available games.
; If files are added when this option is enabled, they will not appear in the gamelists of the frontend. The option must be enabled again to display new entries properly.
GameListOnly=0
 
; 0 = run the frontend normally.
; 1 = run the frontend in kiosk mode.
; 2 = run the frontend in kid mode.
InterfaceMode=0

; Set to which monitor index the frontend will be displayed.
MonitorIndex=0

; Disable to disable VSync in RetroBat interface.
VSync=1

; Set if the option to quit the frontend is displayed or not when the full menu is enabled.
NoExitMenu=0

; Set if you are using an old GPU not compatible with newest OpenGL version.
OpenGL2_1=0

; Set the windows width of the frontend.
WindowXSize=1280

; Set the windows height of the frontend.
WindowYSize=720

; Draw framerate in EmulationStation.
DrawFramerate=0";
        }

        #endregion

        #region Upgrade

        /// <summary>
        /// Rewrites retrobat.ini from the current template while keeping the user's values.
        ///
        /// Reading is done with a raw parser rather than IniFile, because IniFile treats any ';'
        /// inside a value as the start of a comment and truncates the value (see IniFile ctor).
        /// Writing goes through IniFile, whose WriteValue updates keys in place and therefore
        /// preserves the template's comments, ordering and blank lines.
        /// </summary>
        /// <param name="dropObsoleteKeys">
        /// When true, user keys absent from the template are discarded. Default is false:
        /// keys not listed in the template may still be read by RetroBat batch scripts.
        /// </param>
        public static bool UpgradeRBIniFile(string iniPath, string rootPath, bool dropObsoleteKeys = false)
        {
            SimpleLogger.Instance.Info("[INFO] Updating retrobat.ini file.");

            if (string.IsNullOrEmpty(iniPath))
            {
                SimpleLogger.Instance.Warning("[WARNING] retrobat.ini path is empty, update aborted.");
                return false;
            }

            string newIniContent = GetDefaultIniContent(rootPath);

            // Nothing to migrate: use template with defaults
            if (!File.Exists(iniPath))
            {
                try
                {
                    File.WriteAllText(iniPath, newIniContent, new UTF8Encoding(false));
                    SimpleLogger.Instance.Info("[INFO] retrobat.ini created from template.");
                    return true;
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[WARNING] Cannot create retrobat.ini: " + ex.Message);
                    return false;
                }
            }

            // Read the user's values
            Encoding userEncoding = DetectEncoding(iniPath);
            Dictionary<string, Dictionary<string, string>> iniValues;

            try
            {
                iniValues = ParseIniEntries(File.ReadAllText(iniPath, userEncoding));
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Cannot read retrobat.ini (" + ex.Message + "), update aborted.");
                return false;
            }

            // Back up current file before overwriting it
            string iniBak = iniPath + ".bak";

            try
            {
                File.Copy(iniPath, iniBak, true);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Cannot backup retrobat.ini (" + ex.Message + "), update aborted.");
                return false;
            }

            // Lay down the template
            try
            {
                File.WriteAllText(iniPath, newIniContent, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Cannot write retrobat.ini (" + ex.Message + "), restoring backup.");
                RestoreBackup(iniBak, iniPath);
                return false;
            }

            // Re-apply the user's values
            try
            {
                var templateEntries = ParseIniEntries(newIniContent);

                using (var ini = new IniFile(iniPath, IniOptions.KeepEmptyLines | IniOptions.KeepEmptyValues))
                {
                    foreach (var userSection in iniValues)
                    {
                        if (userSection.Value.Count == 0)
                            continue;

                        string section = userSection.Key;

                        Dictionary<string, string> templateSection;
                        if (!templateEntries.TryGetValue(section, out templateSection))
                            templateSection = null;

                        foreach (var keyValue in userSection.Value)
                        {
                            bool inTemplate = false;

                            if (templateSection != null)
                            {
                                string templateValue;
                                inTemplate = templateSection.TryGetValue(keyValue.Key, out templateValue);
                            }

                            if (!inTemplate)
                            {
                                // AppLauncher2, AppLauncher3... are user-extensible keys
                                if (IsIndexedVariant(templateSection, keyValue.Key))
                                {
                                    ini.WriteValue(section, keyValue.Key, keyValue.Value);
                                    continue;
                                }

                                SimpleLogger.Instance.Info("[INFO] Key not in template: [" + section + "] " + keyValue.Key
                                    + (dropObsoleteKeys ? " (dropped)" : " (kept)"));

                                if (dropObsoleteKeys)
                                    continue;
                            }

                            ini.WriteValue(section, keyValue.Key, keyValue.Value);
                        }
                    }

                    ini.Save();
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Failed to restore user settings (" + ex.Message + "), restoring backup.");
                RestoreBackup(iniBak, iniPath);
                return false;
            }

            // Restore the original encoding
            RestoreEncoding(iniPath, userEncoding);

            SimpleLogger.Instance.Info("[INFO] retrobat.ini updated successfully (backup: " + Path.GetFileName(iniBak) + ").");
            return true;
        }

        private static void RestoreBackup(string iniBak, string iniPath)
        {
            try
            {
                File.Copy(iniBak, iniPath, true);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[ERROR] Cannot restore retrobat.ini backup: " + ex.Message, ex);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Raw INI parser. Read-only by design, it does not treat ';' inside
        /// a value as a comment delimiter, so values such as paths are preserved verbatim.
        /// </summary>
        private static Dictionary<string, Dictionary<string, string>> ParseIniEntries(string content)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string section = string.Empty;

            if (string.IsNullOrEmpty(content))
                return result;

            foreach (var raw in content.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                string key = line.Substring(0, idx).Trim();
                if (key.Length == 0)
                    continue;

                Dictionary<string, string> sectionValues;
                if (!result.TryGetValue(section, out sectionValues))
                {
                    sectionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    result[section] = sectionValues;
                }

                sectionValues[key] = line.Substring(idx + 1).Trim();
            }

            return result;
        }

        /// <summary>
        /// True when the key is an indexed variant of a template key (AppLauncher2, AppLauncher3...).
        /// </summary>
        private static bool IsIndexedVariant(Dictionary<string, string> templateSection, string key)
        {
            if (templateSection == null || string.IsNullOrEmpty(key))
                return false;

            int i = key.Length;
            while (i > 0 && char.IsDigit(key[i - 1]))
                i--;

            if (i == key.Length || i == 0)
                return false;

            return templateSection.ContainsKey(key.Substring(0, i));
        }

        private static Encoding DetectEncoding(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);

                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    return new UTF8Encoding(true);
                if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
                    return new UTF32Encoding(false, true);
                if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                    return Encoding.Unicode;
                if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                    return Encoding.BigEndianUnicode;

                try
                {
                    new UTF8Encoding(false, true).GetString(bytes);
                    return new UTF8Encoding(false);
                }
                catch (DecoderFallbackException)
                {
                    SimpleLogger.Instance.Info("[INFO] retrobat.ini is not valid UTF-8, reading it as ANSI (codepage " + Encoding.Default.CodePage + ").");
                    return Encoding.Default;
                }
            }
            catch
            {
                return new UTF8Encoding(false);
            }
        }

        private static void RestoreEncoding(string iniPath, Encoding userEncoding)
        {
            var utf8NoBom = new UTF8Encoding(false);

            if (userEncoding.CodePage == utf8NoBom.CodePage && userEncoding.GetPreamble().Length == 0)
                return;

            try
            {
                string content = File.ReadAllText(iniPath, utf8NoBom);

                string roundTrip = userEncoding.GetString(userEncoding.GetBytes(content));
                if (roundTrip != content)
                {
                    SimpleLogger.Instance.Warning("[WARNING] retrobat.ini contains characters that codepage "
                        + userEncoding.CodePage + " cannot represent, file kept as UTF-8.");
                    return;
                }

                File.WriteAllText(iniPath, content, userEncoding);
                SimpleLogger.Instance.Info("[INFO] retrobat.ini rewritten using its original encoding (codepage " + userEncoding.CodePage + ").");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] Cannot restore retrobat.ini original encoding (" + ex.Message + "), file kept as UTF-8.");
            }
        }

        #endregion

#if DEBUG
        /// <summary>
        /// Guards against drift between retrobat_template.ini and the built-in fallback.
        /// Debug only: a mismatch produces configurations that differ between installs.
        /// </summary>
        public static void AssertTemplateMatchesFallback(string rootPath)
        {
            string templateFile = Path.Combine(rootPath, "system", "resources", "retrobat_template.ini");
            if (!File.Exists(templateFile))
                return;

            var onDisk = ParseIniEntries(File.ReadAllText(templateFile));
            var builtIn = ParseIniEntries(GetBuiltInIniContent());

            foreach (var section in builtIn)
            {
                Dictionary<string, string> diskSection;
                if (!onDisk.TryGetValue(section.Key, out diskSection))
                {
                    SimpleLogger.Instance.Warning("[DEBUG] Section missing from template file: [" + section.Key + "]");
                    continue;
                }

                foreach (var kv in section.Value)
                {
                    string diskValue;
                    if (!diskSection.TryGetValue(kv.Key, out diskValue))
                        SimpleLogger.Instance.Warning("[DEBUG] Key missing from template file: [" + section.Key + "] " + kv.Key);
                    else if (diskValue != kv.Value)
                        SimpleLogger.Instance.Warning("[DEBUG] Default mismatch: [" + section.Key + "] " + kv.Key
                            + " template=" + diskValue + " builtin=" + kv.Value);
                }
            }
        }
#endif
    }
}