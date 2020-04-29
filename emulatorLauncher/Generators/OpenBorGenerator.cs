using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class OpenBorGenerator : Generator
    {        
        private string destFile;
        
        public static int JoystickValue(InputKey key, Controller c)
        {
            var a = c.Input[key];
            if (a == null)
                return 0;

            int JOY_MAX_INPUTS = 64;

            int value = 0;

            if (a.Type == "button")
                value = 1 + (c.Index - 1) * JOY_MAX_INPUTS + (int)a.Id;
            else if (a.Type == "hat")
            {
                int hatfirst = 1 + (c.Index - 1) * JOY_MAX_INPUTS + c.Buttons + 2 * c.Axes + 4 * (int)a.Id;
                if (a.Value == 2) // SDL_HAT_RIGHT
                    hatfirst += 1;
                else if (a.Value == 4) // SDL_HAT_DOWN
                    hatfirst += 2;
                else if (a.Value == 8) // SDL_HAT_LEFT
                    hatfirst += 3;

                value = hatfirst;
            }
            else if (a.Type == "axis")
            {
                int axisfirst = 1 + (c.Index - 1) * JOY_MAX_INPUTS + c.Buttons + 2 * (int)a.Id;
                if (a.Value > 0) axisfirst++;
                value = axisfirst;
            }

            if (c.Input.Type != "keyboard")
                value += 600;

            return value;
        }
        
        public static int KeyboardValue(InputKey key, Controller c)
        {
            var a = c.Input[key];
            if (a == null)
                return 0;

            var mapped = SDL.SDL_default_keymap.Select(k => Convert.ToInt32(k)).ToList().IndexOf((int)a.Id);
            if (mapped >= 0)
                return mapped;

            return 0;
        }

        private void setupControllers(ConfigFile ini)
        {
            foreach (var c in Controllers)
            {
                if (c.Input == null)
                    continue;

                int idx = c.Index - 1;

                if (c.Input.Type == "keyboard")
                {
                    ini["keys." + idx + ".0"] = OpenBorGenerator.KeyboardValue(InputKey.up, c).ToString();
                    ini["keys." + idx + ".1"] = OpenBorGenerator.KeyboardValue(InputKey.down, c).ToString();
                    ini["keys." + idx + ".2"] = OpenBorGenerator.KeyboardValue(InputKey.left, c).ToString();
                    ini["keys." + idx + ".3"] = OpenBorGenerator.KeyboardValue(InputKey.right, c).ToString();
                    ini["keys." + idx + ".4"] = OpenBorGenerator.KeyboardValue(InputKey.a, c).ToString(); // ATTACK
                    ini["keys." + idx + ".5"] = OpenBorGenerator.KeyboardValue(InputKey.b, c).ToString();
                    ini["keys." + idx + ".6"] = OpenBorGenerator.KeyboardValue(InputKey.y, c).ToString();
                    ini["keys." + idx + ".7"] = OpenBorGenerator.KeyboardValue(InputKey.rightshoulder, c).ToString(); // ATTACK4
                    ini["keys." + idx + ".8"] = OpenBorGenerator.KeyboardValue(InputKey.x, c).ToString(); // JUMP
                    ini["keys." + idx + ".9"] = OpenBorGenerator.KeyboardValue(InputKey.select, c).ToString();
                    ini["keys." + idx + ".10"] = OpenBorGenerator.KeyboardValue(InputKey.start, c).ToString();
                    //ini["keys." + idx + ".11"] = OpenBorGenerator.KeyboardValue(InputKey.up, c).ToString();
                    //ini["keys." + idx + ".12"] = OpenBorGenerator.KeyboardValue(InputKey.hotkey, c).ToString();
                    continue;
                }
                
                ini["keys." + idx + ".0"] = OpenBorGenerator.JoystickValue(InputKey.up, c).ToString();
                ini["keys." + idx + ".1"] = OpenBorGenerator.JoystickValue(InputKey.down, c).ToString();
                ini["keys." + idx + ".2"] = OpenBorGenerator.JoystickValue(InputKey.left, c).ToString();
                ini["keys." + idx + ".3"] = OpenBorGenerator.JoystickValue(InputKey.right, c).ToString();
                ini["keys." + idx + ".4"] = OpenBorGenerator.JoystickValue(InputKey.a, c).ToString(); // ATTACK
                ini["keys." + idx + ".5"] = OpenBorGenerator.JoystickValue(InputKey.b, c).ToString();
                ini["keys." + idx + ".6"] = OpenBorGenerator.JoystickValue(InputKey.y, c).ToString();
                ini["keys." + idx + ".7"] = OpenBorGenerator.JoystickValue(InputKey.rightshoulder, c).ToString(); // ATTACK4
                ini["keys." + idx + ".8"] = OpenBorGenerator.JoystickValue(InputKey.x, c).ToString(); // JUMP
                ini["keys." + idx + ".9"] = OpenBorGenerator.JoystickValue(InputKey.select, c).ToString();
                ini["keys." + idx + ".10"] = OpenBorGenerator.JoystickValue(InputKey.start, c).ToString();
                //ini["keys." + idx + ".11"] = OpenBorGenerator.ControllerValue(InputKey.up, c).ToString(); // screenshot
                // ini["keys." + idx + ".12"] = OpenBorGenerator.JoystickValue(InputKey.hotkey, c).ToString(); // esc
            }
        }


        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("openbor");

            string exe = Path.Combine(path, "OpenBOR.exe");
            if (!File.Exists(exe))
                return null;

            if (setupConfig(path))
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = "\""+rom+"\"",
                    WorkingDirectory = path
                };
            }

            string pakDir = Path.Combine(path, "Paks");
            foreach (var file in Directory.GetFiles(pakDir))
            {
                if (Path.GetFileName(file) == Path.GetFileName(rom))
                    continue;

                File.Delete(file);
            }

            destFile = Path.Combine(pakDir, Path.GetFileName(rom));
            if (!File.Exists(destFile))
                File.Copy(rom, destFile);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path
            };
        }

        public override void Cleanup()
        {
            if (destFile != null && File.Exists(destFile))
                File.Delete(destFile);
        }

        private bool setupConfig(string path)
        {
            string ini = Path.Combine(path, "config.ini");
            var conf = ConfigFile.FromFile(ini);
            if (conf == null)
                return false;

            setupControllers(conf);

            conf["fullscreen"] = "1";
            conf["vsync"] = "1";
            conf["usegl"] = "1";

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig["screenshots"]))
            {
                string dir = AppConfig.GetFullPath("screenshots");

                Uri relRoot = new Uri(path, UriKind.Absolute);
                string relPath = relRoot.MakeRelativeUri(new Uri(dir, UriKind.Absolute)).ToString().Replace("/", "\\");

                conf["screenShotsDir"] = ".\\" + relPath;
            }

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
            {
                string dir = Path.Combine(AppConfig.GetFullPath("saves"), "openbor");
              
                Uri relRoot = new Uri(path, UriKind.Absolute);
                string relPath = relRoot.MakeRelativeUri(new Uri(dir, UriKind.Absolute)).ToString().Replace("/", "\\");
             
                conf["savesDir"] = ".\\"+relPath;
            }

            conf.Save(ini, false);
            return true;
        }
    }
}
