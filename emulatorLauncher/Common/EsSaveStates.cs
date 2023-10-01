using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace emulatorLauncher
{
    class EsSaveStates
    {
        public SaveStateEmulatorInfo this[string key]
        {
            get
            {
                if (Emulators != null)
                    return Emulators.FirstOrDefault(sys => sys.Name == key);

                return null;
            }
        }

        public SaveStateEmulatorInfo[] Emulators { get; private set; }

        public static EsSaveStates Load(string filename)
        {
            var ret = new EsSaveStates();

            try
            {
                if (File.Exists(filename))
                {
                    XElement root = XElement.Load(filename);

                    var emulators = new List<SaveStateEmulatorInfo>();

                    foreach (XElement emulatorElement in root.Elements("emulator"))
                    {
                        var emulator = new SaveStateEmulatorInfo
                        {
                            Name = ElementOrAttribute(emulatorElement, "name"),
                            Directory = ElementOrAttribute(emulatorElement, "directory"),
                            DefaultCoreDirectory = ElementOrAttribute(emulatorElement, "defaultCoreDirectory"),
                            Incremental = ElementOrAttribute(emulatorElement, "incremental") == "true"
                        };
                        emulators.Add(emulator);
                    }

                    ret.Emulators = emulators.ToArray();
                }
            }
            catch { }

            return ret;
        }

        private static string ElementOrAttribute(XElement elt, string name, string defaultValue = "")
        {
            var att = elt.Attribute(name);
            if (att != null)
                return att.Value;

            var child = elt.Element(name);
            if (child != null)
                return child.Value;

            return defaultValue;
        }

        public bool IsIncremental(string emulator)
        {
            bool incrementalOption = (string.IsNullOrEmpty(Program.SystemConfig["incrementalsavestates"]) ? "1" : Program.SystemConfig["incrementalsavestates"]) == "1";
            if (!incrementalOption)
                return false;

            var emul = this[emulator];
            return (emul != null && emul.Incremental);
        }

        public string GetSavePath(string system, string emulator, string core)
        {
            var saves = Program.AppConfig.GetFullPath("saves");
            if (!Directory.Exists(saves))
                return null;

            var emul = this[emulator];
            if (emul == null || string.IsNullOrEmpty(emul.Directory))
                return Path.GetFullPath(Path.Combine(saves, system));

            string ret = emul.Directory
                .Replace("{{system}}", system ?? "")
                .Replace("{{emulator}}", emulator ?? "")
                .Replace("{{core}}", core ?? "");

            if (!string.IsNullOrEmpty(emul.DefaultCoreDirectory))
            {
                var sys = Program.EsSystems[system];
                if (sys != null && emulator == sys.DefaultEmulator && core == sys.DefaultCore)
                {
                    ret = emul.DefaultCoreDirectory
                        .Replace("{{system}}", system ?? "")
                        .Replace("{{emulator}}", emulator ?? "")
                        .Replace("{{core}}", core ?? "");
                }
            }
            
            return Path.GetFullPath(Path.Combine(saves, ret));
        }
    }

    class SaveStateEmulatorInfo
    {
        public string Name { get; set; }
        public string Directory { get; set; }
        public string DefaultCoreDirectory { get; set; }
        public bool Incremental { get; set; }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
