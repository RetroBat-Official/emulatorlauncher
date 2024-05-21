using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

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

            string index = controller.DeviceIndex.ToString();

            if (SystemConfig.isOptSet("eka2l1_controllerindex") && !string.IsNullOrEmpty(SystemConfig["eka2l1_controllerindex"]))
                index = SystemConfig["eka2l1_controllerindex"];

            var mappingInfo = new List<Eka2l1MappingInfo>();

            if (controller.IsKeyboard)
            {
                foreach (var input in keyboardDefaults)
                {
                    mappingInfo.Add(new Eka2l1MappingInfo()
                    {
                        Source = new Eka2l1MappingSource()
                        {
                            Type = "key",
                            Data = new Dictionary<string, string>()
                            {
                                { "keycode", input.Value }
                            }
                        },
                        Target = input.Key
                    });
                }
            }

            else
            {
                foreach (var input in inputKeys)
                {
                    string target = input.Key;
                    string button = input.Value;

                    if (button == "red")
                    {
                        mappingInfo.Add(new Eka2l1MappingInfo()
                        {
                            Source = new Eka2l1MappingSource()
                            {
                                Type = "key",
                                Data = new Dictionary<string, string>()
                            {
                                { "keycode", "16777267" }
                            }
                            },
                            Target = target
                        });
                    }
                    else if (button == "green")
                    {
                        mappingInfo.Add(new Eka2l1MappingInfo()
                        {
                            Source = new Eka2l1MappingSource()
                            {
                                Type = "key",
                                Data = new Dictionary<string, string>()
                            {
                                { "keycode", "16777266" }
                            }
                            },
                            Target = target
                        });
                    }
                    else
                    {
                        mappingInfo.Add(new Eka2l1MappingInfo()
                        {
                            Source = new Eka2l1MappingSource()
                            {
                                Type = "controller",
                                Data = new Dictionary<string, string>()
                            {
                                { "controller_id", index },
                                { "button_id", button }
                            }
                            },
                            Target = target
                        });
                    }
                }
            }

            string ymlContent = SerializeToYml(mappingInfo);
            File.WriteAllText(ymlFile, ymlContent);
        }

        private readonly Dictionary<string, string> inputKeys = new Dictionary<string, string>()
        {
            { "164", "6" },         // Left softkey - Select
            { "165", "7" },         // Right softkey - Start
            { "180", "green" },     // Green softkey - rightThumb
            { "181", "red" },       // Red softkey - N/A
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

        private readonly Dictionary<string, string> keyboardDefaults = new Dictionary<string, string>()
        {
            { "164", "16777264" },
            { "165", "16777265" },
            { "180", "16777266" },
            { "181", "16777267" },
            { "167", "16777220" },
            { "16", "16777235" },
            { "17", "16777237" },
            { "14", "16777234" },
            { "15", "16777236" },
            { "48", "48" },
            { "49", "49" },
            { "50", "50" },
            { "51", "51" },
            { "52", "52" },
            { "53", "53" },
            { "54", "54" },
            { "55", "55" },
            { "56", "56" },
            { "57", "57" },
            { "42", "42" },
            { "127", "47" },
            { "1", "16777219" }
        };

        class Eka2l1MappingInfo
        {
            public Eka2l1MappingSource Source { get; set; }
            public string Target { get; set; }

            public string ToYml()
            {
                var writer = new StringBuilder();

                writer.AppendLine("- source:");
                writer.AppendLine(string.Format("    type: {0}", Source.Type));

                writer.AppendLine("    data:");
                foreach (var dataEntry in Source.Data)
                    writer.AppendLine(string.Format("      {0}: {1}", dataEntry.Key, dataEntry.Value));

                writer.AppendLine(string.Format("  target: {0}", Target));

                return writer.ToString();
            }

            public override string ToString() { return ToYml(); }
        }

        class Eka2l1MappingSource
        {
            public string Type { get; set; }
            public Dictionary<string, string> Data { get; set; }
        }

        static string SerializeToYml(IEnumerable<Eka2l1MappingInfo> mappings)
        {
            var writer = new StringBuilder();

            foreach (var mapping in mappings)
                writer.Append(mapping.ToYml());

            return writer.ToString();
        }
    }
}
