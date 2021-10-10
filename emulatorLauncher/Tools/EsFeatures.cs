using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;

namespace emulatorLauncher.Tools
{
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute("features")]
    [System.Xml.Serialization.XmlRootAttribute("features")]
    public partial class EsFeatures
    {
        public bool IsSupported(string name)
        {
            if (_contextFeatures == null)
                return true;

            if (!_contextFeatures.Contains(name))
                return false;

            return true;
        }

        private HashSet<string> _contextFeatures;

        public void LoadFeaturesContext(string system, string emulator, string core)
        {
            HashSet<string> ret = new HashSet<string>();

            if (this.GlobalFeatures != null && this.GlobalFeatures.Feature != null)
                foreach (var s in this.GlobalFeatures.Feature.Select(f => f.value))
                    ret.Add(s);
            
            if (this.Emulators != null)
            {
                foreach (var emul in Emulators.Where(e => NameContains(e.Name, emulator)))
                {
                    if (emul.CommonFeatures != null)
                        foreach (var s in emul.CommonFeatures.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                            ret.Add(s);

                    if (emul.Features != null)
                        foreach (var s in emul.Features.Select(f => f.value))
                            ret.Add(s);

                    if (emul.Systems != null)
                    {
                        foreach (var sys in emul.Systems.Where(c => NameContains(c.Name, system)))
                        {
                            if (sys.CommonFeatures != null)
                                foreach (var s in sys.CommonFeatures.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    ret.Add(s);

                            if (sys.Features != null)
                                foreach (var s in sys.Features.Select(f => f.value))
                                    ret.Add(s);
                        }
                    }

                    if (emul.SystemCollection != null && emul.SystemCollection.Systemes != null)
                    {
                        foreach (var sys in emul.SystemCollection.Systemes.Where(c => NameContains(c.Name, system)))
                        {
                            if (sys.CommonFeatures != null)
                                foreach (var s in sys.CommonFeatures.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    ret.Add(s);

                            if (sys.Features != null)
                                foreach (var s in sys.Features.Select(f => f.value))
                                    ret.Add(s);
                        }
                    }

                    if (emul.Cores != null)
                    {
                        foreach (var corr in emul.Cores.Where(c => NameContains(c.Name, core)))
                        {
                            if (corr.CommonFeatures != null)
                                foreach (var s in corr.CommonFeatures.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    ret.Add(s);

                            if (corr.Features != null)
                                foreach (var s in corr.Features.Select(f => f.value))
                                    ret.Add(s);
                        }
                    }

                    if (emul.CoreCollection != null && emul.CoreCollection.Cores != null)
                    {
                        foreach (var corr in emul.CoreCollection.Cores.Where(c => NameContains(c.Name, core)))
                        {
                            if (corr.CommonFeatures != null)
                                foreach (var s in corr.CommonFeatures.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    ret.Add(s);

                            if (corr.Features != null)
                                foreach (var s in corr.Features.Select(f => f.value))
                                    ret.Add(s);
                        }
                    }
                }
            }

            _contextFeatures = ret;
        }

        private static bool NameContains(string a, string b)
        {
            if (a != null && a.Contains(","))
                return a.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(b);

            return a == b;
        }

        public static EsFeatures Load(string xmlFile)
        {
            if (!File.Exists(xmlFile))
            {
                var def = new EsFeatures();

                def._contextFeatures = new HashSet<string>();

                foreach (var s in new string[] { 
                    "ratio",
                    "rewind",
                    "smooth",
                    "shaders",
                    "pixel_perfect",
                    "decoration",
                    "latency_reduction",
                    "game_translation",
                    "autosave",
                    "netplay",
                    "fullboot",
                    "emulated_wiimotes",
                    "screen_layout",
                    "internal_resolution",
                    "videomode",
                    "colorization",
                    "padtokeyboard",
                    "joystick2pad",
                    "cheevos",
                    "autocontrollers"})
                    def._contextFeatures.Add(s);

                return def;
            }

            try
            {
                EsFeatures ret = Misc.FromXml<EsFeatures>(xmlFile);
                if (ret != null)
                    return ret;
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.Message);
#endif
                SimpleLogger.Instance.Error("EsFeatures error : " + ex.Message);
            }

            return new EsFeatures();
        }

        [XmlElement("globalFeatures")]
        public FeatureCollection GlobalFeatures { get; set; }

        [XmlElement("emulator")]
        public Emulator[] Emulators { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Emulator
    {
        [XmlElement("system")]
        public Systeme[] Systems { get; set; }

        [XmlElement("systems")]
        public SystemCollection SystemCollection { get; set; }

        [XmlElement("core")]
        public Core[] Cores { get; set; }

        [XmlElement("cores")]
        public CoreCollection CoreCollection { get; set; }

        [XmlElement("feature")]
        public Feature[] Features { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("features")]
        public string CommonFeatures { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Core
    {
        [XmlElement("system")]
        public Systeme[] Systems { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("feature")]
        public Feature[] Features { get; set; }

        [XmlAttribute("features")]
        public string CommonFeatures { get; set; }
    }


    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Feature
    {
        /// <remarks/>
        [XmlElement("choice")]
        public Choice[] choice { get; set; }

        [XmlAttribute]
        public string name { get; set; }

        [XmlAttribute]
        public string value { get; set; }

        [XmlAttribute]
        public string description { get; set; }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Choice
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }
    }


    /// <remarks/>
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class Systeme
    {
        [XmlElement("feature")]
        public Feature[] Features { get; set; }

        [XmlAttribute("features")]
        public string CommonFeatures { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class FeatureCollection
    {
        [XmlElement("feature")]
        public Feature[] Feature { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class SystemCollection
    {
        [XmlElement("system")]
        public Systeme[] Systemes { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class CoreCollection
    {
        [XmlElement("core")]
        public Core[] Cores { get; set; }
    }
    
}
