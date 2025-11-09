using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using Steam_Library_Manager.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace es_update
{
    public class RetroBatIni
    {
        public static string GetDefaultIniContent(string rootPath)
        {
            string templateFile = Path.Combine(rootPath, "system", "resources", "retrobat_template.ini");

            if (File.Exists(templateFile))
                return File.ReadAllText(templateFile);
            else
                return @"; RETROBAT GLOBAL CONFIG FILE

[RetroBat]

; At startup RetroBat will detect or not the language used in Windows to set automatically the same language in the frontend and RetroArch emulator.
LanguageDetection=0

; At startup RetroBat will reset the default config files options of emulationstation and retrobat.ini.
; Use at your own risk.	
ResetConfigMode=0

; Run automatically RetroBat at Windows startup (0=NO 1=STARTUP 2=REGISTRY).
Autostart=0

; Set the Start Delay for RetroBat to start automatically at startup (in milliseconds).
AutoStartDelay=0

; Run WiimoteGun at RetroBat's startup. You can use your wiimote as a gun and navigate through EmulationStation.
WiimoteGun=0

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

        public static void UpgradeRBIniFile(string iniPath, string rootPath)
        {
            SimpleLogger.Instance.Info("[INFO] Updating retrobat.ini file.");

            string newIniContent = GetDefaultIniContent(rootPath);
            Dictionary<string, Dictionary<string, string>> iniValues = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                using (var ini = new IniFile(iniPath, IniOptions.KeepEmptyLines | IniOptions.KeepEmptyValues))
                {
                    // Reading existing values
                    var sections = ini.EnumerateSections();

                    foreach (var section in sections)
                    {
                        var keyValuePairs = ini.EnumerateValues(section);
                        Dictionary<string, string> sectionValues = new Dictionary<string, string>();
                        foreach (var keyValue in keyValuePairs)
                            sectionValues.Add(keyValue.Key, keyValue.Value);
                        
                        iniValues.Add(section, sectionValues);
                    }
                    
                    ini.Save();
                }

                try
                {
                    string iniBak = iniPath.Replace(".ini", ".ini.bak");
                    File.Copy(iniPath, iniBak);
                    File.Delete(iniPath);
                }
                catch { }

                File.WriteAllText(iniPath, newIniContent);

                try
                {
                    using (var ini = new IniFile(iniPath, IniOptions.KeepEmptyLines | IniOptions.KeepEmptyValues))
                    {
                        foreach (var s in iniValues)
                        {
                            if (s.Value.Count == 0)
                                continue;

                            string section = s.Key;

                            foreach (var keyValue in s.Value)
                                ini.WriteValue(section, keyValue.Key, keyValue.Value);
                        }
                    }
                }
                catch { }

            }
            catch { SimpleLogger.Instance.Warning("[WARNING] Failed to Update retrobat.ini file."); }
        }
    }
}
