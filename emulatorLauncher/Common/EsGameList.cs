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
        public static string FormatPath(string path, IRelativePath relativeTo)
        {
            if (!string.IsNullOrEmpty(path))
            {
                string home = GetHomePath(relativeTo);
                path = path.Replace("%HOME%", home);
                path = path.Replace("~", home);

                path = path.Replace("/", "\\");
                if (path.StartsWith("\\") && !path.StartsWith("\\\\"))
                    path = "\\" + path;

                if (relativeTo != null && path.StartsWith(".\\"))
                    path = Path.Combine(Path.GetDirectoryName(relativeTo.FilePath), path.Substring(2));
            }

            return path;
        }

        private static string GetHomePath(IRelativePath relativeTo)
        {
            if (relativeTo == null)
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string path = Path.GetDirectoryName(relativeTo.FilePath);
            if (!path.StartsWith("\\\\"))
            {
                try
                {
                    string parent = Directory.GetParent(path).FullName;
                    if (File.Exists(Path.Combine(parent, "EmulationStation.exe")))
                        return parent;
                }
                catch { }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

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
                    GameList gl = xmlFile.FromXml<GameList>();
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

                if (string.IsNullOrEmpty(Marquee))
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

            if (type != MetadataType.Image)
                return File.Exists(GetImageFile(type));

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
            else if (type == MetadataType.Fanart)
                value = this.FanArt;
            else if (type == MetadataType.TitleShot)
                value = this.Titleshot;

            if (string.IsNullOrEmpty(value))
                return null;

            return GameList.FormatPath(value, GameList);
        }

        public void SetImageFile(MetadataType type, string path)
        {
            if (type == MetadataType.Thumbnail)
                this.Thumbnail = path;
            else if (type == MetadataType.Video)
                this.Video = path;
            else if (type == MetadataType.Marquee)
                this.Marquee = path;
            else if (type == MetadataType.Fanart)
                this.FanArt = path;
            else if (type == MetadataType.TitleShot)
                this.Titleshot = path;
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
            {
                var fn = GetRomFile();
                _romExists = File.Exists(fn) || Directory.Exists(fn);
            }

            return _romExists.Value;
        }

        public string GetRomFile()
        {
            if (string.IsNullOrEmpty(this.path))
                return null;

            return GameList.FormatPath(this.path, GameList);
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
            get { return _name ?? ""; }
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

        private string _fanart;

        [XmlElement("fanart")]
        public string FanArt
        {
            get { return formatPath(_fanart); }
            set { _fanart = value; }
        }

        private string _titleshot;

        [XmlElement("titleshot")]
        public string Titleshot
        {
            get { return formatPath(_titleshot); }
            set { _titleshot = value; }
        }

        private string _map;

        [XmlElement("map")]
        public string Map
        {
            get { return formatPath(_map); }
            set { _map = value; }
        }

        private string _cartridge;

        [XmlElement("cartridge")]
        public string Cartridge
        {
            get { return formatPath(_cartridge); }
            set { _cartridge = value; }
        }

        private string _boxart;

        [XmlElement("boxart")]
        public string Boxart
        {
            get { return formatPath(_boxart); }
            set { _boxart = value; }
        }

        private string _manual;

        [XmlElement("manual")]
        public string Manual
        {
            get { return formatPath(_manual); }
            set { _manual = value; }
        }

        private string _wheel;

        [XmlElement("wheel")]
        public string Wheel
        {
            get { return formatPath(_wheel); }
            set { _wheel = value; }
        }

        private string _mix;

        [XmlElement("mix")]
        public string Mix
        {
            get { return formatPath(_mix); }
            set { _mix = value; }
        }

        private string _bezel;

        [XmlElement("bezel")]
        public string Bezel
        {
            get { return formatPath(_bezel); }
            set { _bezel = value; }
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

        [XmlAttribute("id")]
        [DefaultValue("")]
        public string GameId { get; set; }

        [XmlElement("arcadesystemname")]
        [DefaultValue("")]
        public string ArcadeSystemName { get; set; }

        [XmlElement("crc32")]
        [DefaultValue("")]
        public string Crc32 { get; set; }

        [XmlElement("md5")]
        [DefaultValue("")]
        public string Md5 { get; set; }

        [XmlElement("lang")]
        [DefaultValue("")]
        public string Language { get; set; }

        [XmlElement("region")]
        [DefaultValue("")]
        public string Region { get; set; }

        [XmlElement("cheevosHash")]
        [DefaultValue("")]
        public string CheevosHash { get; set; }

        [XmlAttribute("cheevosId")]
        [DefaultValue(0)]
        public int CheevosId { get; set; }


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
    Fanart = 3,
    TitleShot = 4,
    Video = 5
}