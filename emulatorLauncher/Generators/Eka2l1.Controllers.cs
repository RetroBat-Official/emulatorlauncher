using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class Eka2l1Generator : Generator
    {
        private void SetupControllers(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!Directory.Exists(Path.Combine(path, "bindings")))
                try { Directory.CreateDirectory(Path.Combine(path, "bindings")); }
                catch { }

            var ymlFile = Path.Combine(path, "bindings", "retrobat.yml");
            
            if (File.Exists(ymlFile))
                File.Delete(ymlFile);

            var controller = Program.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            if (controller == null)
                return;

            if (controller.Config == null)
                return;

            string index = controller.DirectInput != null ? controller.DirectInput.DeviceIndex.ToString() : controller.DeviceIndex.ToString();

            if (SystemConfig.isOptSet("eka2l1_controllerindex") && !string.IsNullOrEmpty(SystemConfig["eka2l1_controllerindex"]))
                index = SystemConfig["eka2l1_controllerindex"];

            List<Dictionary<string, object>> mappings = new List<Dictionary<string, object>>();

            foreach (var input in inputKeys)
            {
                string target = input.Key;
                string button = input.Value;

                if (button == "red")
                {
                    mappings.Add(new Dictionary<string, object>
                    {
                        { "source", new Dictionary<string, object>
                            {
                                { "type", "key" },
                                { "data", new Dictionary<string, object>
                                    {
                                        { "keycode", "16777267" }
                                    }
                                }
                            }
                        },
                        { "target", target }
                    });
                }

                else if (button == "green")
                {
                    mappings.Add(new Dictionary<string, object>
                    {
                        { "source", new Dictionary<string, object>
                            {
                                { "type", "key" },
                                { "data", new Dictionary<string, object>
                                    {
                                        { "keycode", "16777266" }
                                    }
                                }
                            }
                        },
                        { "target", target }
                    });
                }

                else
                {
                    mappings.Add(new Dictionary<string, object>
                    {
                        { "source", new Dictionary<string, object>
                            {
                                { "type", "controller" },
                                { "data", new Dictionary<string, object>
                                    {
                                        { "controller_id", index },
                                        { "button_id", button }
                                    }
                                }
                            }
                        },
                        { "target", target }
                    });
                }
                
            }

            string ymlContent = SerializeToYml(mappings);
            File.WriteAllText(ymlFile, ymlContent);
        }

        private Dictionary<string, string> inputKeys = new Dictionary<string, string>()
        {
            { "164", "6" },         // Left softkey - Select
            { "165", "7" },         // Right softkey - Start
            { "180", "green" },         // Green softkey - rightThumb
            { "181", "red" },          // Red softkey - N/A
            { "167", "4" },         // Middle softkey - leftThumb
            { "16", "11" },         // Up - dpad up
            { "17", "13" },         // Down - dpad down
            { "14", "14" },         // Left - dpad left
            { "15", "12" },         // Right - dpad right
            { "48", "307" },        // 0 - Right joystick up
            { "49", "2" },          // 1 - WEST
            { "50", "3" },          // 2 - NORTH
            { "51", "9" },          // 3 - L1
            { "52", "0" },          // 4 - SOUTH
            { "53", "1" },          // 5 - EAST
            { "54", "10" },         // 6 - R1
            { "55", "305" },        // 7 - Right joystick left
            { "56", "306" },        // 8 - Right joystick down
            { "57", "304" },        // 9 - Right joystick right
            { "42", "308" },        // star - L2
            { "127", "309" },       // diese - R2
        };

        static string SerializeToYml(List<Dictionary<string, object>> mappings)
        {
            StringWriter writer = new StringWriter();

            foreach (var mapping in mappings)
            {
                writer.WriteLine("- source:");
                writer.WriteLine($"    type: {(mapping["source"] as Dictionary<string, object>)["type"]}");

                var data = (Dictionary<string, object>)(mapping["source"] as Dictionary<string, object>)["data"];
                writer.WriteLine("    data:");
                foreach (var dataEntry in data)
                {
                    writer.WriteLine($"      {dataEntry.Key}: {dataEntry.Value}");
                }

                writer.WriteLine($"  target: {mapping["target"]}");
            }

            return writer.ToString();
        }
    }
}
