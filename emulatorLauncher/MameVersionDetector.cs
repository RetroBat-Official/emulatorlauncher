using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.IO.Compression;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    static class MameVersionDetector
    {
        private static MameGames games;

        public static string FindBestMameCore(string rom, string[] supportedCores = null)
        {
            return FindCompatibleMameCores(rom, supportedCores).Select(c => c.Name).FirstOrDefault();
        }

        public static CompatibleCore[] FindCompatibleMameCores(string rom, string[] supportedCores = null)
        {
            if (string.IsNullOrEmpty(rom) || Path.GetExtension(rom).ToLowerInvariant() != ".zip")
                return new CompatibleCore[] { };

            try
            {
                if (games == null)
                {
#if DEVTOOL
                    games = ReadGZipFile(@"H:\ConsoleApplication1 - Copie\output\mamecrcs.xml.gz").FromXmlString<MameGames>();
#else
                    games = GZipBytesToString(Properties.Resources.mamecrcs_xml_gz).FromXmlString<MameGames>();
#endif
                }

                if (games == null || games.Games == null)
                    return new CompatibleCore[] { };

                var game = games.Games.FirstOrDefault(ff => ff.Name == Path.GetFileNameWithoutExtension(rom).ToLowerInvariant());
                if (game == null)
                    return new CompatibleCore[] { };

                if (!string.IsNullOrEmpty(game.CoreList))
                    return game.Cores.Select(c => new CompatibleCore() { Name = c, Match = RomSetMatch.Exact}).ToArray(); 

                HashSet<string> cores = supportedCores == null ? null : new HashSet<string>(supportedCores.Select(c => c.Replace("_", "-")));
                var ret = new List<CompatibleCore>();

                using (var zip = ZipArchive.OpenRead(rom))
                {
                    var entries = zip.Entries.ToList();
                    var entriesCrcs = entries.Select(e => e.HexCrc).ToArray();
                    
                    // Exact match
                    foreach (var mm in game.Roms)
                    {
                        foreach (var core in mm.Cores)
                        {
                            if (cores != null && !cores.Contains(core))
                                continue;

                            int crcCount = mm.Crcs.Length;
                            var gameCrcs = mm.Crcs.ToArray();

                            if (entriesCrcs.Length == gameCrcs.Length && entriesCrcs.All(c => gameCrcs.Contains(c)))
                                ret.Add(new CompatibleCore() { Name = core, Match = RomSetMatch.Exact });
                        }
                    }

                    // All required CRCs are present
                    foreach (var mm in game.Roms)
                    {
                        foreach (var core in mm.Cores)
                        {
                            if (cores != null && !cores.Contains(core))
                                continue;

                            if (ret.Any(c => c.Name == core))
                                continue;

                            int crcCount = mm.Crcs.Length;
                            var gameCrcs = mm.Crcs.ToArray();

                            if (gameCrcs.Except(entriesCrcs).Count() == 0)
                                ret.Add(new CompatibleCore() { Name = core, Match = RomSetMatch.AllRequired });
                        }
                    }

                    // Some crcs are missing....
                    foreach (var mm in game.Roms)
                    {
                        foreach (var core in mm.Cores)
                        {
                            if (cores != null && !cores.Contains(core))
                                continue;

                            if (ret.Any(c => c.Name == core))
                                continue;

                            int crcCount = mm.Crcs.Length;
                            var gameCrcs = mm.Crcs.ToArray();

                            if (gameCrcs.Except(entriesCrcs).Count() == gameCrcs.Length - entriesCrcs.Length)
                                ret.Add(new CompatibleCore() { Name = core, Match = RomSetMatch.SomeMissing });
                        }
                    }

                    return ret.ToArray();
                }
            }
            catch (Exception ex)
            {
#if !DEVTOOL
                SimpleLogger.Instance.Error("[FindBestMameCore] Exception " + ex.Message, ex);
#endif
            }

            return new CompatibleCore[] { }; 
        }
 
        static string ReadGZipFile(string file)
        {
            using (FileStream reader = File.OpenRead(file))
                return ReadGZipStream(reader);
        }

        static string ReadGZipStream(Stream reader)
        {
            try 
            {
                using (var decompressedStream = new MemoryStream())
                {
                    using (GZipStream decompressionStream = new GZipStream(reader, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedStream);
                        return Encoding.UTF8.GetString(decompressedStream.ToArray());
                    }
                }
            }
            catch(Exception ex)
            {
                SimpleLogger.Instance.Error("[ReadGZipStream] Failed " + ex.Message, ex);
            }

            return null;
        }

        static string GZipBytesToString(byte[] bytes)
        {
            using (var reader = new MemoryStream(bytes))
                return ReadGZipStream(reader);
        }

        internal static string ListAllGames(string mamePath, bool forFBNEO)
        {
            StringBuilder sb = new StringBuilder();
                      
            var gamelist = EmulatorLauncher.Common.EmulationStation.GameList.Load(Path.Combine(mamePath, "gamelist.xml"));
            var dic = gamelist.Games.ToDictionary(g => Path.GetFileNameWithoutExtension(g.Path.ToLowerInvariant()), g => g);
            
            foreach (var file in Directory.GetFiles(mamePath, "*.zip"))
            {
                var cores = MameVersionDetector.FindCompatibleMameCores(file);

                if (forFBNEO)
                {
                    if (cores.Any(c => c.Name == "fbneo" && c.Match == RomSetMatch.Exact))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        EmulatorLauncher.Common.EmulationStation.Game game;
                        if (dic.TryGetValue(name, out game) && game.CheevosId > 0)
                        {
                     //       string fbNeoPath = Path.Combine(Program.AppConfig.GetFullPath("roms"), "fbneo", "mame");
                     //       File.Copy(file, Path.Combine(fbNeoPath, Path.GetFileName(file)));

                            sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> CHEEVOS");
                        }
                        else
                            sb.AppendLine(Path.GetFileNameWithoutExtension(file));
                    }
                }
                else
                {
                    var version = cores.Select(c => c.Name).FirstOrDefault();
                    if (version == null)
                        sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> unknown");
                    else
                        sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> " + version ?? "??");
                }
            }

            return sb.ToString();
        }

        internal static string CheckMame(string path)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var file in Directory.GetFiles(path, "*.zip"))
            {
                var cores = MameVersionDetector.FindCompatibleMameCores(file);

                var version = cores.Any(c => c.Name.Contains("mame"));
                if (!version)
                {
                    var bests = string.Join(", ", cores.Select(c => c.Name).ToArray());
                    if (!string.IsNullOrEmpty(bests))
                        sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> KO -> " + bests);
                    else
                        sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> KO -> Unknown core");
                }
                else
                    sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> OK");
            }

            return sb.ToString();
        }

        internal static string CheckFbNeo(string path)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var file in Directory.GetFiles(path, "*.zip"))
            {
                var cores = MameVersionDetector.FindCompatibleMameCores(file);

                var version = cores.Any(c => c.Name == "fbneo");
                if (!version)
                {
                    var bests = string.Join(", ", cores.Select(c => c.Name).ToArray());
                    if (!string.IsNullOrEmpty(bests))
                        sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> KO -> " + bests);
                    else
                        sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> KO -> Unknown core");
                }
                else
                    sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> OK");
            }

            return sb.ToString();
        }
    }

    [XmlRoot("games")]
    [XmlType("games")]
    public class MameGames
    {
        [XmlElement("game")]
        public List<MameGame> Games { get; set; }
    }

    [XmlType("game")]
    [XmlRoot("game")]
    public class MameGame
    {
        public MameGame()
        {
            Roms = new List<MameRom>();
        }

        [XmlAttribute("id")]
        public string Name { get; set; }

        [XmlAttribute("core")]
        public string CoreList { get; set; }

        [XmlElement("rom")]
        public List<MameRom> Roms { get; set; }

        private string[] _cores;

        [XmlIgnore]
        public string[] Cores
        {
            get
            {
                if (_cores == null)
                    _cores = (this.CoreList ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                return _cores;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class MameRom
    {
        private string[] _cores;

        [XmlIgnore]
        public string[] Cores
        {
            get
            {
                if (_cores == null)
                    _cores = (this.CoreList ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                return _cores;
            }
        }

        [XmlAttribute("core")]
        public string CoreList { get; set; }

        [XmlAttribute("crc")]
        public string Crc { get; set; }

        [XmlIgnore]
        public string[] Crcs 
        { 
            get 
            {
                if (string.IsNullOrEmpty(Crc))
                    return new string[] { };

                return Crc.Split(new char[] { '-' });
            } 
        }

        public override string ToString()
        {
            return CoreList + " " + Crc;
        }
    }

    public class CompatibleCore
    {
        public string Name { get; set; }
        public RomSetMatch Match { get; set; }

        public override string ToString()
        {
            return Name + " ( " + Match.ToString() + " )";
        }

    }

    public enum RomSetMatch
    {
        Exact,
        AllRequired,
        SomeMissing
    }

}
