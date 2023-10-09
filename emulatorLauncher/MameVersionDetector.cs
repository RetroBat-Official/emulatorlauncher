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
            return FindCompatibleMameCores(rom, supportedCores).FirstOrDefault();
        }

        public static string[] FindCompatibleMameCores(string rom, string[] supportedCores = null)
        {
            if (string.IsNullOrEmpty(rom) || Path.GetExtension(rom).ToLowerInvariant() != ".zip")
                return new string[] { };

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
                    return new string[] { };

                var game = games.Games.FirstOrDefault(ff => ff.Name == Path.GetFileNameWithoutExtension(rom).ToLowerInvariant());
                if (game == null)
                    return new string[] { };

                if (!string.IsNullOrEmpty(game.Core))
                    return new string[] { game.Core }; 

                HashSet<string> cores = supportedCores == null ? null : new HashSet<string>(supportedCores.Select(c => c.Replace("_", "-")));
                var ret = new List<string>();

                using (var zip = ZipArchive.OpenRead(rom))
                {
                    var entries = zip.Entries.ToList();
                    var entriesCrcs = entries.Select(e => e.HexCrc).ToArray();
                    
                    // Exact match
                    foreach (var mm in game.Cores)
                    {
                        if (cores != null && !cores.Contains(mm.Core))
                            continue;

                        int crcCount = mm.Crcs.Length;
                        var gameCrcs = mm.Crcs.ToArray();

                        if (entriesCrcs.Length == gameCrcs.Length && entriesCrcs.All(c => gameCrcs.Contains(c)))
                            ret.Add(mm.Core);
                    }

                    // All required CRCs are present
                    foreach (var mm in game.Cores)
                    {
                        if (cores != null && !cores.Contains(mm.Core))
                            continue;

                        if (ret.Contains(mm.Core))
                            continue;

                        int crcCount = mm.Crcs.Length;
                        var gameCrcs = mm.Crcs.ToArray();

                        if (gameCrcs.Except(entriesCrcs).Count() == 0)
                            ret.Add(mm.Core);
                    }

                    // Some crcs are missing....
                    foreach (var mm in game.Cores)
                    {
                        if (cores != null && !cores.Contains(mm.Core))
                            continue;

                        if (ret.Contains(mm.Core))
                            continue;

                        int crcCount = mm.Crcs.Length;
                        var gameCrcs = mm.Crcs.ToArray();

                        if (gameCrcs.Except(entriesCrcs).Count() == gameCrcs.Length - entriesCrcs.Length)
                            ret.Add(mm.Core);
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

            return new string[] { }; 
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

        internal static string ListAllGames(string mamePath)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var file in Directory.GetFiles(mamePath, "*.zip"))
            {
                var version = MameVersionDetector.FindBestMameCore(file);
                if (version == null)
                    sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> unknown");
                else
                    sb.AppendLine(Path.GetFileNameWithoutExtension(file) + " -> " + version ?? "??");
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
            Cores = new List<MameVersion>();
        }

        [XmlAttribute("id")]
        public string Name { get; set; }

        [XmlAttribute("core")]
        public string Core { get; set; }

        [XmlElement("rom")]
        public List<MameVersion> Cores { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class MameVersion
    {
        [XmlAttribute("core")]
        public string Core { get; set; }

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
            return Core + " " + Crc;
        }
    }
}
