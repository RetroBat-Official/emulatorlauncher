using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;

namespace emulatorLauncher.Tools
{
    [XmlRoot("systems")]
    [XmlType("systems")]
    public class GunGames
    {
        public static GunGame GetGameInformation(string system, string romName)
        {
            if (string.IsNullOrEmpty(romName) || string.IsNullOrEmpty(system))
                return null;

            var gungames = GunGames.Load(Path.Combine(Program.AppConfig.GetFullPath("resources"), "gungames.xml"));
            if (gungames != null)
                return gungames.FindGame(system, romName);

            return null;
        }

        private GunGames()
        {
            Systems = new List<GunSystem>();
        }

        [XmlElement("system")]
        public List<GunSystem> Systems { get; set; }

        public static GunGames Load(string filename)
        {
            if (!File.Exists(filename))
                return null;

            try
            {
                GunGames gl = filename.FromXml<GunGames>();
                if (gl != null)
                {
                    gl._cache = new Dictionary<string, List<GunGame>>();

                    foreach (var sys in gl.Systems)
                        foreach (var sysName in sys.Names)
                            gl._cache[sysName] = sys.Games;
                }

                return gl;
            }
            catch(Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.Message);
#endif
                SimpleLogger.Instance.Error("GunGames.Load error : " + ex.Message);
            }

            return null;
        }

        private Dictionary<string, List<GunGame>> _cache;

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
        
        public GunGame FindGame(string system, string romName)
        {
            List<GunGame> list;
            if (!_cache.TryGetValue(system, out list) || list == null)
                return null;

            string indexedName = GetIndexedName(romName);

            var game = list.FirstOrDefault(l => l.Name != null && l.Name == romName);
            if (game != null)
                return game;

            return list.FirstOrDefault(l => l.Name != null && indexedName.IndexOf(l.Name, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }
    }

    [XmlRoot("system")]
    public class GunSystem
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("game")]
        public List<GunGame> Games { get; set; }

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
    public class GunGame
    {
        [XmlText]
        public string Name { get; set; }

        [XmlAttribute("gun")]
        public string GunType { get; set; }
    }
}
