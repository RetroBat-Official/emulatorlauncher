using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;

namespace emulatorLauncher.Tools
{
    [System.Xml.Serialization.XmlTypeAttribute("features")]
    [System.Xml.Serialization.XmlRootAttribute("features")]
    public class EsFeatures
    {
        private HashSet<string> _contextFeatures;

        public bool IsSupported(string name)
        {
            if (_contextFeatures == null)
                return true;

            if (!_contextFeatures.Contains(name))
                return false;

            return true;
        }

        public void SetFeaturesContext(string system, string emulator, string core)
        {
            HashSet<string> ret = new HashSet<string>();

            if (this.GlobalFeatures != null)
            {
                foreach (var s in this.GlobalFeatures.GetAllFeatureNames(this.SharedFeatures))
                    ret.Add(s);
            }
            
            if (this.Emulators != null && !string.IsNullOrEmpty(emulator))
            {
                foreach (var emul in Emulators.Where(e => NameContains(e.Name, emulator)))
                {
                    foreach (var name in emul.GetAllFeatureNames(this.SharedFeatures))
                        ret.Add(name);

                    if (emul.Systems != null && !string.IsNullOrEmpty(system))
                    {
                        foreach (var sys in emul.Systems.Where(c => NameContains(c.Name, system)))
                            foreach (var name in sys.GetAllFeatureNames(this.SharedFeatures))
                                ret.Add(name);
                    }

                    if (emul.Cores != null && !string.IsNullOrEmpty(core))
                    {
                        foreach (var corr in emul.Cores.Where(c => NameContains(c.Name, core)))
                        {
                            foreach (var name in corr.GetAllFeatureNames(this.SharedFeatures))
                                ret.Add(name);

                            if (corr.Systems != null && !string.IsNullOrEmpty(system))
                            {
                                foreach (var sys in corr.Systems.Where(c => NameContains(c.Name, system)))
                                    foreach (var name in sys.GetAllFeatureNames(this.SharedFeatures))
                                        ret.Add(name);
                            }
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
            var defaultFeatures = new EsFeatures();
            defaultFeatures._contextFeatures = new HashSet<string>();

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
                    "cheevos",
                    "autocontrollers"})
                defaultFeatures._contextFeatures.Add(s);

            if (!File.Exists(xmlFile))
                return defaultFeatures;            

            try
            {
                string data = File.ReadAllText(xmlFile);
                data = data.Replace("<cores>", "").Replace("</cores>", "").Replace("<systems>", "").Replace("</systems>", "");

                EsFeatures ret = data.FromXmlString<EsFeatures>();
                if (ret != null)
                    return ret;

                SimpleLogger.Instance.Warning("es_features.cfg file is null. Using default features");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("es_features.cfg file is invalid : " + ex.Message);
                throw ex;
            }

            return defaultFeatures;
        }

        [XmlElement("sharedFeatures")]
        public FeatureCollection SharedFeatures { get; set; }

        [XmlElement("globalFeatures")]
        public FeatureCollection GlobalFeatures { get; set; }

        [XmlElement("emulator")]
        public Emulator[] Emulators { get; set; }
    }

    public class Emulator : FeatureCollection
    {
        public override string ToString()
        {
            return "<emulator name=\"" + (Name ?? "null") + "\" />";
        }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("system")]
        public Systeme[] Systems { get; set; }

        [XmlElement("core")]
        public Core[] Cores { get; set; }    
    }

    public class Core : FeatureCollection
    {
        public override string ToString()
        {
            return "<core name=\"" + (Name ?? "null") + "\" />";
        }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("system")]
        public Systeme[] Systems { get; set; }
    }

    public class Feature
    {
        public override string ToString()
        {
            return "<feature value=\"" + (Value??"null") + "\" />";
        }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlElement("choice")]
        public Choice[] Choice { get; set; }

        [XmlAttribute("submenu")]
        public string SubMenu { get; set; }
    }

    public class Choice
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }
    }

    public class Systeme : FeatureCollection
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
    }

    public class FeatureCollection
    {
        [XmlAttribute("features")]
        public string CommonFeatures { get; set; }

        [XmlElement("feature")]
        public Feature[] Features { get; set; }

        [XmlElement("sharedFeature")]
        public Feature[] SharedFeatures { get; set; }

        public string[] GetAllFeatureNames(FeatureCollection sharedFeatures)
        {
            List<string> ret = new List<string>();

            if (CommonFeatures != null)
            {
                foreach (var name in CommonFeatures.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    ret.Add(name);
            }

            if (Features != null)
            {
                foreach (var feat in Features)
                    if (!string.IsNullOrEmpty(feat.Value) && !string.IsNullOrEmpty(feat.Name) && feat.Choice != null && feat.Choice.Any())
                        ret.Add(feat.Value);
            }

            if (SharedFeatures != null && sharedFeatures != null && sharedFeatures.Features != null)
            {
                foreach (var sf in SharedFeatures)
                {
                    Feature feat = null;

                    if (!string.IsNullOrEmpty(sf.Value))
                        feat = sharedFeatures.Features.FirstOrDefault(f => f.Value == sf.Value);

                    if (feat == null && !string.IsNullOrEmpty(sf.Name))
                        feat = sharedFeatures.Features.FirstOrDefault(f => f.Name == sf.Name);

                    if (feat != null)
                        ret.Add(feat.Value);
                }
            }

            return ret.ToArray();
        }
    }
}
