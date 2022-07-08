using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class YuzuGenerator : Generator
    {
        public YuzuGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator.Replace("-", " "));

            string exe = Path.Combine(path, "yuzu.exe");
            if (!File.Exists(exe))
                return null;

            SetupConfiguration(path);
            
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-f -g \"" + rom + "\"",
            };
        }

        private void SetupConfiguration(string path)
        {
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");

            using (var ini = new IniFile(conf))
            {
                ini.WriteValue("UI", "fullscreen\\default", "false");
                ini.WriteValue("UI", "fullscreen", "true");

                //      CreateControllerConfiguration(ini);

                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as", "false");
                    ini.WriteValue("UI", "Screenshots\\screenshot_path", AppConfig.GetFullPath("screenshots"));
                }

                // backend
                if (SystemConfig.isOptSet("backend") && !string.IsNullOrEmpty(SystemConfig["backend"]) && SystemConfig["backend"] != "0")
                {
                    ini.WriteValue("Renderer", "backend\\default", "false");
                    ini.WriteValue("Renderer", "backend", SystemConfig["backend"]);
                }
                else if (Features.IsSupported("backend"))
                {
                    ini.WriteValue("Renderer", "backend\\default", "true");
                    ini.WriteValue("Renderer", "backend", "0");
                }

                // resolution_setup
                if (SystemConfig.isOptSet("resolution_setup") && !string.IsNullOrEmpty(SystemConfig["resolution_setup"]) && SystemConfig["resolution_setup"] != "2")
                {
                    ini.WriteValue("Renderer", "resolution_setup\\default", "false");
                    ini.WriteValue("Renderer", "resolution_setup", SystemConfig["resolution_setup"]);
                }
                else if (Features.IsSupported("resolution_setup"))
                {
                    ini.WriteValue("Renderer", "resolution_setup\\default", "true");
                    ini.WriteValue("Renderer", "resolution_setup", "2");
                }

                // anti_aliasing
                if (SystemConfig.isOptSet("anti_aliasing") && SystemConfig.getOptBoolean("anti_aliasing"))
                {
                    ini.WriteValue("Renderer", "anti_aliasing\\default", "false");
                    ini.WriteValue("Renderer", "anti_aliasing", "1");
                }
                else if (Features.IsSupported("anti_aliasing"))
                {
                    ini.WriteValue("Renderer", "anti_aliasing\\default", "true");
                    ini.WriteValue("Renderer", "anti_aliasing", "0");
                }

                // scaling_filter
                if (SystemConfig.isOptSet("scaling_filter") && !string.IsNullOrEmpty(SystemConfig["scaling_filter"]) && SystemConfig["scaling_filter"] != "1")
                {
                    ini.WriteValue("Renderer", "scaling_filter\\default", "false");
                    ini.WriteValue("Renderer", "scaling_filter", SystemConfig["scaling_filter"]);
                }
                else if (Features.IsSupported("scaling_filter"))
                {
                    ini.WriteValue("Renderer", "scaling_filter\\default", "true");
                    ini.WriteValue("Renderer", "scaling_filter", "1");
                }
            }
        }

        private string FromInput(Controller controller, Input input)
        {
            if (input == null)
                return null;

            if (input.Type == "key")
                return "engine:keyboard,code:" + input.Id + ",toggle:0";

            string value = "engine:sdl,port:0,guid:" + controller.Guid.ToLowerInvariant();

            if (input.Type == "button")
                value += ",button:" + input.Id;
            else if (input.Type == "hat")
                value += ",hat:" + input.Id + ",direction:" + input.Name.ToString();
            else if (input.Type == "axis")
                value += ",axis:" + input.Id + ",direction:" + (input.Value > 0 ? "+" : "-") + ",threshold:0.500000";

            return value;
        }

        private void ProcessStick(Controller controller, string player, string stickName, IniFile ini)
        {
            var cfg = controller.Config;

            string name = player + stickName;

            var leftVal = cfg[stickName == "lstick" ? InputKey.joystick1up : InputKey.joystick2up];
            var topVal = cfg[stickName == "lstick" ? InputKey.joystick1left : InputKey.joystick2left];

            if (leftVal != null && topVal != null && leftVal.Type == topVal.Type && leftVal.Type == "axis")
            {
                string value = "engine:sdl,port:0,guid:" + controller.Guid.ToLowerInvariant();
                value += ",axis_x:" + leftVal.Id + ",axis_y:" + topVal.Id + ",deadzone:0.100000,range:1.000000";

                ini.WriteValue("Controls", name + "\\default", "false");
                ini.WriteValue("Controls", name, "\"" + value + "\"");
            }
            else if (stickName == "lstick")
            {
                string value = "engine:analog_from_button";

                var left = FromInput(controller, cfg[InputKey.left]);
                if (left != null) value += ",left:" + left.Replace(":", "$0").Replace(",", "$1");

                var top = FromInput(controller, cfg[InputKey.up]);
                if (top != null) value += ",up:" + top.Replace(":", "$0").Replace(",", "$1");

                var right = FromInput(controller, cfg[InputKey.right]);
                if (right != null) value += ",right:" + right.Replace(":", "$0").Replace(",", "$1");

                var down = FromInput(controller, cfg[InputKey.down]);
                if (down != null) value += ",down:" + down.Replace(":", "$0").Replace(",", "$1");

                var modifier = FromInput(controller, cfg[InputKey.l3]);
                if (modifier != null) value += ",modifier:" + modifier.Replace(":", "$0").Replace(",", "$1");

                value += ",modifier_scale:0.500000";

                ini.WriteValue("Controls", name + "\\default", "false");
                ini.WriteValue("Controls", name, "\"" + value + "\"");
            }
            else
            {
                ini.WriteValue("Controls", name + "\\default", "false");
                ini.WriteValue("Controls", name, "[empty]");
            }
        }

        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            foreach (var controller in this.Controllers)
            {
                var cfg = controller.Config;
                if (cfg == null)
                    continue;

                string player = "player_" + (controller.PlayerIndex - 1) + "_";

                foreach (var map in Mapping)
                {
                    string name = player + map.Value;

                    string cvalue = FromInput(controller, cfg[map.Key]);

                    if (controller.Name == "Keyboard" && (map.Key == InputKey.up || map.Key == InputKey.down || map.Key == InputKey.left || map.Key == InputKey.right || map.Key == InputKey.l3))
                        cvalue = null;

                    if (string.IsNullOrEmpty(cvalue))
                    {
                        ini.WriteValue("Controls", name + "\\default", "false");
                        ini.WriteValue("Controls", name, "[empty]");
                    }
                    else
                    {
                        ini.WriteValue("Controls", name + "\\default", "false");
                        ini.WriteValue("Controls", name, "\"" + cvalue + "\"");
                    }
                }

                ProcessStick(controller, player, "lstick", ini);

                if (controller.Name != "Keyboard")
                    ProcessStick(controller, player, "rstick", ini);
            }
        }
                
        static InputKeyMapping Mapping = new InputKeyMapping()
        { 
            { InputKey.select,          "button_minus" },  
            { InputKey.start,           "button_plus" },
        
            { InputKey.b,               "button_a" },
            { InputKey.a,               "button_b" },

            { InputKey.y,               "button_y" },
            { InputKey.x,               "button_x" },  
            
            { InputKey.up,              "button_dup" }, 
            { InputKey.down,            "button_ddown" }, 
            { InputKey.left,            "button_dleft" }, 
            { InputKey.right,           "button_dright" },
            
            { InputKey.leftshoulder,    "button_l" },
            { InputKey.rightshoulder,   "button_r" },          

            { InputKey.l2,              "button_zl" },
            { InputKey.r2,              "button_zr"},

            { InputKey.l3,              "button_lstick"},
            { InputKey.r3,              "button_rstick"},
        };

        static Dictionary<string, string> DefKeys = new Dictionary<string, string>()
        {
            { "button_a", "engine:keyboard,code:67,toggle:0" },
            { "button_b","engine:keyboard,code:88,toggle:0" },
            { "button_x","engine:keyboard,code:86,toggle:0" },
            { "button_y","engine:keyboard,code:90,toggle:0" },
            { "button_lstick","engine:keyboard,code:70,toggle:0" },
            { "button_rstick","engine:keyboard,code:71,toggle:0" },
            { "button_l","engine:keyboard,code:81,toggle:0" },
            { "button_r","engine:keyboard,code:69,toggle:0" },
            { "button_zl","engine:keyboard,code:82,toggle:0" },
            { "button_zr","engine:keyboard,code:84,toggle:0" },
            { "button_plus","engine:keyboard,code:77,toggle:0" },
            { "button_minus","engine:keyboard,code:78,toggle:0" },
            { "button_dleft","engine:keyboard,code:16777234,toggle:0" },
            { "button_dup","engine:keyboard,code:16777235,toggle:0" },
            { "button_dright","engine:keyboard,code:16777236,toggle:0" },
            { "button_ddown","engine:keyboard,code:16777237,toggle:0" },
            { "button_sl","engine:keyboard,code:81,toggle:0" },
            { "button_sr","engine:keyboard,code:69,toggle:0" },
            { "button_home","engine:keyboard,code:0,toggle:0" },
            { "button_screenshot","engine:keyboard,code:0,toggle:0" },
            { "lstick","engine:analog_from_button,up:engine$0keyboard$1code$087$1toggle$00,left:engine$0keyboard$1code$065$1toggle$00,modifier:engine$0keyboard$1code$016777248$1toggle$00,down:engine$0keyboard$1code$083$1toggle$00,right:engine$0keyboard$1code$068$1toggle$00,modifier_scale:0.500000" },
            { "rstick","engine:analog_from_button,up:engine$0keyboard$1code$073$1toggle$00,left:engine$0keyboard$1code$074$1toggle$00,modifier:engine$0keyboard$1code$00$1toggle$00,down:engine$0keyboard$1code$075$1toggle$00,right:engine$0keyboard$1code$076$1toggle$00,modifier_scale:0.500000" }
        };

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            // Yuzu always returns 0xc0000005 ( null pointer !? )
            if (exitCode == unchecked((int)0xc0000005))
                return 0;
            
            return exitCode;
        }
    }
}
