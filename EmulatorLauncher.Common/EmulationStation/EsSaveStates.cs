using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;

namespace EmulatorLauncher.Common.EmulationStation
{
    public class EsSaveStates
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
                            Incremental = ElementOrAttribute(emulatorElement, "incremental") == "true",
                            AutoSave = ElementOrAttribute(emulatorElement, "autosave") == "true",
                            FirstSlot = ElementOrAttribute(emulatorElement, "firstslot").ToInteger(),
                            LastSlot = ElementOrAttribute(emulatorElement, "lastslot", "999999").ToInteger(),

                            FilePattern = ElementOrAttribute(emulatorElement, "file"),
                            ImagePattern = ElementOrAttribute(emulatorElement, "image"),
                            AutoFilePattern = ElementOrAttribute(emulatorElement, "autosave_file"),
                            AutoImagePattern = ElementOrAttribute(emulatorElement, "autosave_image")
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
        
        public bool IsEmulatorSupported(string emulator)
        {
            var emul = this[emulator];
            return this[emulator] != null;
        }
    }

    public class SaveStateEmulatorInfo
    {
        public string Name { get; set; }
        public string Directory { get; set; }
        public string DefaultCoreDirectory { get; set; }
        
        public bool Incremental { get; set; }
        public bool AutoSave { get; set; }
        public int FirstSlot { get; set; }
        public int LastSlot { get; set; }

        public string FilePattern { get; set; }
        public string ImagePattern { get; set; }
        public string AutoFilePattern { get; set; }
        public string AutoImagePattern { get; set; }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
