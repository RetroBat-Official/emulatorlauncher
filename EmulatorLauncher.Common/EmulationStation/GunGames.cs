using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Common.EmulationStation
{
    [XmlRoot("systems")]
    [XmlType("systems")]
    public class GamesDB
    {
        private GamesDB()
        {
            Systems = new List<GamesDBSystem>();
        }

        [XmlElement("system")]
        public List<GamesDBSystem> Systems { get; set; }

        public static GamesDB Load(string filename)
        {
            if (!File.Exists(filename))
                return null;

            try
            {
                GamesDB gl = filename.FromXml<GamesDB>();
                if (gl != null)
                {
                    gl._gunGamesCache = new Dictionary<string, List<GamesDBGame>>();

                    if (Path.GetFileNameWithoutExtension(filename) == "gungames")
                    {
                        foreach(var game in gl.Systems.SelectMany(s => s.Games))
                            game.Gun = new GamesDBGun() { Type = game._oldGunType, ReversedButtons = game._oldReversedButtons };
                    }

                    foreach (var sys in gl.Systems)
                    {
                        foreach (var sysName in sys.Names)
                        {
                            var gunGames = sys.Games.Where(g => !string.IsNullOrEmpty(g.Name) && g.Gun != null).OrderBy(l => l.Name.Length).ToList();
                            if (gunGames.Count > 0)
                                gl._gunGamesCache[sysName] = gunGames;
                        }
                    }
                }

                return gl;
            }
            catch(Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.Message);
#endif
                SimpleLogger.Instance.Error("[GunGames] Load error : " + ex.Message);
            }

            return new GamesDB();
        }

        private Dictionary<string, List<GamesDBGame>> _gunGamesCache;

        static string GetIndexedName(string romName)
        {
	        StringBuilder result = new StringBuilder();

	        bool inpar = false;
	        bool inblock = false;

	        foreach (char c in romName.ToLowerInvariant())
	        {
		        if (!inpar && !inblock && (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
			        result.Append(c);
		        else if (c == '(') inpar = true;
		        else if (c == ')') inpar = false;
		        else if (c == '[') inblock = true;
		        else if (c == ']') inblock = false;
	        }

	        return result.ToString();
        }
        
        public GamesDBGame FindGunGame(string system, string romName)
        {
            List<GamesDBGame> list;
            if (!_gunGamesCache.TryGetValue(system, out list) || list == null)
                return null;

            string indexedName = GetIndexedName(romName);

            var game = list.FirstOrDefault(l => l.Name == romName);
            if (game != null)
                return game;

            return list
                .FirstOrDefault(l => indexedName.IndexOf(l.Name, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        public GamesDBGame FindGame(string system, string romName)
        {
            var list = this.Systems
                .Where(s => s.Name == system)
                .SelectMany(s => s.Games);

            string indexedName = GetIndexedName(romName);

            var game = list.FirstOrDefault(l => l.Name == romName);
            if (game != null)
                return game;

            return list
                .FirstOrDefault(l => indexedName.IndexOf(l.Name, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }
    }

    [XmlRoot("system")]
    public class GamesDBSystem
    {
        public override string ToString()
        {
            return Name;
        }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("game")]
        public List<GamesDBGame> Games { get; set; }

        [XmlIgnore]
        public string[] Names
        {
            get 
            {
                if (Name == null)
                    return new string[] { };

                return Name.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            }
        }
    }

    [XmlRoot("game")]
    public class GamesDBGame
    {
        public override string ToString()
        {
            return Name;
        }

        [XmlIgnore]
        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(NameAsXmlText))
                    return NameAsXmlText;

                return NameAsAttribute;
            }
        }

        [XmlAttribute("name")]
        public string NameAsAttribute { get; set; }

        [XmlText]
        public string NameAsXmlText { get; set; }


        #region RetroCompatibility with gungame.xml
        [XmlAttribute("gun")]
        public string _oldGunType { get; set; }

        [XmlAttribute("reversedbuttons")]
        public bool _oldReversedButtons { get; set; }
        #endregion

        [XmlIgnore]
        public string GunType
        {
            get
            {
                if (Gun != null)
                    return Gun.Type;

                return _oldGunType;
            }
        }

        [XmlIgnore]
        public bool ReversedButtons
        {
            get
            {
                if (Gun != null)
                    return Gun.ReversedButtons;

                return _oldReversedButtons;
            }
        }

        [XmlElement("gun")]
        public GamesDBGun Gun { get; set; }

        [XmlElement("wheel")]
        public GamesDBWheel Wheel { get; set; }
    }

    [XmlType("wheel")]
    public class GamesDBWheel
    {
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("wheel")]
        public string Wheel { get; set; }

        [XmlAttribute("rotation")]
        public int Rotation { get; set; }

        [XmlAttribute("accelerate")]
        public string Accelerate { get; set; }

        [XmlAttribute("brake")]
        public string Brake { get; set; }

        [XmlAttribute("port1")]
        public string Port1 { get; set; }

        [XmlAttribute("controller")]
        public string Controller { get; set; }
    }

    [XmlType("gun")]
    public class GamesDBGun
    {
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("reversedbuttons")]
        public bool ReversedButtons { get; set; }

        [XmlAttribute("vertical_offset")]
        public float VerticalOffset { get; set; }

        [XmlAttribute("yaw")]
        public float Yaw { get; set; }

        [XmlAttribute("pitch")]
        public float Pitch { get; set; }

        [XmlAttribute("action")]
        public string Action { get; set; }

        [XmlAttribute("start")]
        public string Start { get; set; }

        [XmlAttribute("select")]
        public string Select { get; set; }

        [XmlAttribute("trigger")]
        public string Trigger { get; set; }

        [XmlAttribute("ir_down")]
        public string IrDown { get; set; }

        [XmlAttribute("ir_right")]
        public string IrRight { get; set; }

        [XmlAttribute("sub1")]
        public string Sub1 { get; set; }
    }
}
