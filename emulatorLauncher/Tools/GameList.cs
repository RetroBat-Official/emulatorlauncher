using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.ComponentModel;
using System.IO;

namespace emulatorLauncher.Tools
{
    public interface IRelativePath
    {
        string FilePath { get; set; }
    }

    [XmlRoot("gameList")]
    [XmlType("gameList")]
    public class GameList : IRelativePath
    {
        public GameList()
        {
            DeletionRepository = new HashSet<string>();
        }

        public static GameList Load(string xmlFile)
        {
            if (File.Exists(xmlFile))
            {
                try
                {
                    GameList gl = Misc.FromXml<GameList>(xmlFile);
                    gl.FilePath = xmlFile;
                    foreach (var game in gl.Games)
                        game.GameList = gl;

                    gl.Games.ListChanged += gl.OnGameListChanged;

                    return gl;
                }
                catch { }
            }
            
            GameList gameList = new GameList();
            gameList.FilePath = xmlFile;
            gameList.Games = new BindingList<Game>();
            gameList.Games.ListChanged += gameList.OnGameListChanged;

            return gameList;
        }
        
        public void Save(bool createCopy = true)
        {
        

            foreach (var s in DeletionRepository)
            {
                if (File.Exists(s))
                {
                    try { File.Delete(s); }
                    catch { }
                }
            }

            string xml = this.ToXml();
            if (!string.IsNullOrEmpty(xml))
            {
                try { File.WriteAllText(this.FilePath, xml); }
                catch { }

                if (createCopy)
                {
                    try { File.WriteAllText(this.FilePath + "_copy", xml); }
                    catch { }
                }
            }
        }

        void OnGameListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
                Games[e.NewIndex].GameList = this;
        }

        [DefaultValue(null)]
        [XmlAttribute("defaultView")]
        public string DefaultView { get; set; }
        
        [XmlElement("game")]
        public BindingList<Game> Games { get; set; }

        [XmlElement("folder")]
        public List<Folder> Folders { get; set; }

        [XmlIgnore]
        public string FilePath { get; set; }

        [XmlIgnore]
        public bool IsDirty { get; set; }

        [XmlIgnore]
        public HashSet<string> DeletionRepository { get; private set; }
    }

    [XmlRoot("folder")]
    public class Folder
    {
        [XmlElement("path")]
        public string Path { get; set; }

        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("image")]
        public string Image { get; set; }


    }

    [XmlRoot("game")]
    public class Game
    {
        [XmlIgnore]
        public GameList GameList { get; set; }

        private bool? _imageExists = false;
        private bool? _thumbNailExists = false;
        private bool? _marqueeExists = false;        
        private bool? _videoExists = false;

        public bool ImageExists(MetadataType type = MetadataType.Image)
        {
            if (type == MetadataType.Marquee)
            {
                if (_marqueeExists.HasValue)
                    return _marqueeExists.Value;

                if (string.IsNullOrEmpty(Video))
                    _marqueeExists = false;
                else
                    _marqueeExists = File.Exists(GetImageFile(type));

                return _marqueeExists.Value;
            }

            if (type == MetadataType.Video)
            {
                if (_videoExists.HasValue)
                    return _videoExists.Value;

                if (string.IsNullOrEmpty(Video))
                    _videoExists = false;
                else
                    _videoExists = File.Exists(GetImageFile(type));

                return _videoExists.Value;
            }

            if (type == MetadataType.Thumbnail)
            {
                if (_thumbNailExists.HasValue)
                    return _thumbNailExists.Value;

                if (string.IsNullOrEmpty(Thumbnail))
                    _thumbNailExists = false;
                else
                    _thumbNailExists = File.Exists(GetImageFile(type));

                return _thumbNailExists.Value;
            }

            if (_imageExists.HasValue)
                return _imageExists.Value;

            if (string.IsNullOrEmpty(Image))
                _imageExists = false;
            else
                _imageExists = File.Exists(GetImageFile(type));

            return _imageExists.Value;
        }


        public string GetImageFile(MetadataType type = MetadataType.Image)
        {
            string value = this.Image;
            if (type == MetadataType.Thumbnail)
                value = this.Thumbnail;
            else if (type == MetadataType.Video)
                value = this.Video;
            else if (type == MetadataType.Marquee)
                value = this.Marquee;

            if (string.IsNullOrEmpty(value))
                return null;

            return Misc.FormatPath(value, GameList);
        }

        public void SetImageFile(MetadataType type, string path)
        {
            if (type == MetadataType.Thumbnail)
                this.Thumbnail = path;
            else if (type == MetadataType.Video)
                this.Video = path;
            else if (type == MetadataType.Marquee)
                this.Marquee = path;
            else
                this.Image = path;
            
        }

        private bool? _romExists = false;

        public bool RomExists()
        {
            if (_romExists.HasValue)
                return _romExists.Value;

            if (string.IsNullOrEmpty(this.path))
                _romExists = false;
            else
                _romExists = File.Exists(GetRomFile());

            return _romExists.Value;
        }

        public string GetRomFile()
        {
            if (string.IsNullOrEmpty(this.path))
                return null;

            return Misc.FormatPath(this.path, GameList);
        }

        public override string ToString()
        {
            if (Name == null)
                return string.Empty;

            return Name;
        }

        private string _path;

        [XmlElement("path")]
        public string path
        {
            get { return _path; }
            set { _path = value; _romExists = null; }
        }

        private string _name;

        [XmlElement("name")]
        public string Name
        {
            get { return _name??""; }
            set { _name = value; }
        }
        
        [XmlElement("desc")]
        public string Description { get; set; }

        private string _image;

        [XmlElement("image")]
        public string Image
        {
            get { return formatPath(_image); }
            set 
            {
                _image = value; 
                _imageExists = null; 
            }
        }

        private string formatPath(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            s = s.Replace("\\", "/");

            if (!s.StartsWith(".") && !s.StartsWith("\\") && !s.StartsWith("/"))
            {
                s = Path.GetFullPath(s).Replace("\\", "/");

                if (GameList != null)
                {
                    string root = Path.GetFullPath(GameList.FilePath);
                    root = Path.GetDirectoryName(root).Replace("\\", "/");

                    if (s.StartsWith(root))
                    {
                        s = s.Replace(root, ".");
                        GameList.IsDirty = true;
                    }
                }
            }

            return s;
        }

        private string _thumbnail;

        [XmlElement("thumbnail")]
        public string Thumbnail
        {
            get { return formatPath(_thumbnail); }
            set { _thumbnail = value; _thumbNailExists = null; }
        }

        private string _marquee;

        [XmlElement("marquee")]
        public string Marquee
        {
            get { return formatPath(_marquee); }
            set { _marquee = value; _marqueeExists = null; }
        }

        private string _video;

        [XmlElement("video")]
        public string Video
        {
            get { return formatPath(_video); }
            set { _video = value; _videoExists = null; }
        }

        [XmlElement("rating")]
        public string Rating { get; set; }

        [XmlElement("developer")]
        [DefaultValue("")]
        public string Developer { get { return _developer ?? ""; } set { _developer = value; } }
        private string _developer;

        [XmlElement("publisher")]
        public string Publisher { get; set; }

        [XmlElement("Genre")]
        public string Genre { get; set; }

        [XmlElement("releasedate")]
        public string ReleaseDate { get; set; }

        [XmlElement("players")]
        [DefaultValue("")]
        public string Players { get { return _players ?? ""; } set { _players = value; } }
        private string _players;

        [XmlElement("playcount")]
        [DefaultValue("")]
        public string Playcount { get { return _playcount ?? ""; } set { _playcount = value; } }
        private string _playcount;

        [XmlElement("lastplayed")]
        [DefaultValue("")]
        public string LastPlayed { get { return _lastplayed ?? ""; } set { _lastplayed = value; } }
        private string _lastplayed;

        [XmlElement("hidden")]
        [DefaultValue("")]
        public string Hidden
        {
            get
            {
                if (_hidden == null || _hidden == "false")
                    return "";

                return _hidden;
            }
            set { _hidden = value; }
        }
        private string _hidden;

        [XmlElement("favorite")]
        [DefaultValue("")]
        public string Favorite { get { return _favorite ?? ""; } set { _favorite = value; } }
        private string _favorite;

        [XmlElement("ratio")]
        [DefaultValue("")]
        public string Ratio { get { return _ratio ?? ""; } set { _ratio = value; } }
        private string _ratio;

        [XmlElement("core")]
        [DefaultValue("")]
        public string Core { get { return _core ?? ""; } set { _core = value; } }
        private string _core;

        [XmlElement("emulator")]
        [DefaultValue("")]
        public string Emulator { get { return _emulator ?? ""; } set { _emulator = value; } }
        private string _emulator;
        
        [XmlIgnore]
        public System.Drawing.Image Picture { get; set; }

        [XmlIgnore]
        public object Tag { get; set; }
    }
}

public enum MetadataType
{
    Image = 0,
    Thumbnail = 1,
    Marquee = 2,
    Video = 3
}

/*type GameXML struct {
    XMLName     xml.Name `xml:"game"`
    ID          string   `xml:"id,attr"`
    Source      string   `xml:"source,attr"`
    Path        string   `xml:"path"`
    GameTitle   string   `xml:"name"`
    Overview    string   `xml:"desc"`
    Image       string   `xml:"image,omitempty"`
    Thumb       string   `xml:"thumbnail,omitempty"`
    Rating      float64  `xml:"rating,omitempty"`
    ReleaseDate string   `xml:"releasedate"`
    Developer   string   `xml:"developer"`
    Publisher   string   `xml:"publisher"`
    Genre       string   `xml:"genre"`
    Players     string   `xml:"players,omitempty"`
    PlayCount   string   `xml:"playcount,omitempty"`
    LastPlayed  string   `xml:"lastplayed,omitempty"`
    Favorite    string   `xml:"favorite,omitempty"`
    Marquee     string   `xml:"marquee,omitempty"`
    Video       string   `xml:"video,omitempty"`
    CloneOf     string   `xml:"cloneof,omitempty"`
    Hidden      string   `xml:"hidden,omitempty"`
    KidGame     string   `xml:"kidgame,omitempty"`
}*/