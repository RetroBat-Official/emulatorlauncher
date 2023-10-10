using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Common.EmulationStation
{
    [XmlRoot("systemList")]
    [XmlType("systemList")]
    public class EsSystems : IRelativePath
    {
        public EsSystems()
        {
            Systems = new List<EsSystem>();
        }

        public EsSystem this[string key]
        {
            get
            {
                return Systems.FirstOrDefault(sys => sys.Name == key);
            }
        }

        [XmlElement("system")]
        public List<EsSystem> Systems { get; set; }
        
        public static EsSystems Load(string filename)
        {
            if (!File.Exists(filename))
                return null;

            EsSystems gl = filename.FromXml<EsSystems>();

            ((IRelativePath)gl).FilePath = Path.GetDirectoryName(filename);
            gl.Systems.ForEach(s => s.RelativePath = gl);

            return gl;
        }
     
        string IRelativePath.FilePath { get; set; }        
    }

    [XmlRoot("core")]
    [XmlType("core")]
    public class EsCore
    {
        public override string ToString()
        {
            return Name;
        }

        [XmlText]
        public string Name { get; set; }

        [XmlAttribute("default")]
        [DefaultValue(false)]
        public bool Default { get; set; }

        [XmlIgnore]
        public string EmulatorName { get; set; }
    }

    [XmlRoot("emulator")]
    [XmlType("emulator")]
    public class EsEmulator
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("command")]
        public string Command { get; set; }

        [XmlArray("cores")]
        public List<EsCore> Cores { get; set; }
    }

    [XmlRoot("system")]
    public class EsSystem
    {
        [XmlIgnore]
        internal IRelativePath RelativePath { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(FullName))
                return FullName;

            return Name;
        }

        [XmlIgnore]
        public string DefaultEmulator
        {
            get
            {
                if (Emulators == null || Emulators.Count == 0)
                    return null;

                // Seeking default="true" attribute
	            foreach (var emul in Emulators)
                {
                    if (emul.Cores == null)
                        continue;

                    foreach (var core in emul.Cores)
			            if (core.Default)
				            return emul.Name;
                }

                return Emulators.FirstOrDefault().Name;
            }
        }

        [XmlIgnore]
        public string DefaultCore
        {
            get 
            {   
                if (Emulators == null || Emulators.Count == 0)
                    return null;

	            var emul = DefaultEmulator;
	            if (string.IsNullOrEmpty(emul))
		            return null;
	
	            foreach (var it in Emulators)
	            {
                    if (it.Name != emul)
                        continue;

                    if (it.Cores == null || it.Cores.Count == 0)
                        continue;

			        foreach (var core in it.Cores)
				        if (core.Default)
					        return core.Name;


                    return it.Cores.FirstOrDefault().Name;
	            }	

	            return "";
            }
        }

        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("fullname")]
        public string FullName { get; set; }

        [XmlElement("path")]
        public string Path { get; set; }

        [XmlElement("extension")]
        public string Extension { get; set; }

        [XmlElement("command")]
        public string Command { get; set; }

        [XmlElement("platform")]
        public string Platform { get; set; }

        [XmlElement("theme")]
        public string Theme { get; set; }

        [XmlElement("manufacturer")]
        public string Manufacturer { get; set; }
        
        [XmlArray("emulators")]
        public List<EsEmulator> Emulators { get; set; }

        private GameList _gameList;

        [XmlIgnore]
        public GameList GameList
        {
            get 
            {
                if (_gameList == null)
                {                    
                    string path = System.IO.Path.Combine(RomPath, "gamelist.xml");
                    _gameList = GameList.Load(path);                    
                }

                return _gameList; 
            }            
        }

        public bool IsGameListLoaded() { return _gameList != null; }

         [XmlIgnore]
        public string RomPath
        {
            get
            {
                return System.IO.Path.GetFullPath(GameList.FormatPath(Path, RelativePath));               
            }
        }
    }
}
