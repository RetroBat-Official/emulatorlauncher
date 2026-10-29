using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmulatorLauncher
{
    /// <summary>
    /// Minimal reader for ArcadeDuck 'resources\arcadedb.yaml'.
    /// ArcadeDuck identifies a game by its archive file name only, and never fills the controller ports itself:
    /// we need the database to validate the set, locate the BIOS archive and companion CHDs, and know the port types/layouts.
    /// Only the few fields we use are parsed, line by line (the file uses a fixed 2-space indentation).
    /// </summary>
    class ArcadeDuckDatabase
    {
        public class Port
        {
            public string Type { get; set; }      // none, arcade, trackball, lightgun, driving, tokimeki
            public string Layout { get; set; }    // controlLayouts key, may be empty
        }

        public class Media
        {
            public string Type { get; set; }      // chd_cdrom, chd_harddisk...
            public string Name { get; set; }
        }

        public class Game
        {
            public string Id { get; set; }
            public string Archive { get; set; }
            public string System { get; set; }
            public string Parent { get; set; }
            public string Status { get; set; }
            public int MaxPlayers { get; set; }
            public List<Port> Ports { get; } = new List<Port>();
            public List<Media> Media { get; } = new List<Media>();

            public bool IsChdMedia(Media m) { return m.Type != null && m.Type.StartsWith("chd", StringComparison.InvariantCultureIgnoreCase); }

            public string GetChdFileName(Media m)
            {
                return m.Name.EndsWith(".chd", StringComparison.InvariantCultureIgnoreCase) ? m.Name : m.Name + ".chd";
            }
        }

        private readonly Dictionary<string, string> _systemBios = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Game> _gamesById = new Dictionary<string, Game>(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Game> _gamesByArchive = new Dictionary<string, Game>(StringComparer.InvariantCultureIgnoreCase);

        private static readonly Regex _entryRegex = new Regex(@"^  ([A-Za-z0-9_]+):\s*$", RegexOptions.Compiled);
        private static readonly Regex _fieldRegex = new Regex(@"^    ([A-Za-z0-9_]+):\s*""?([^""]*)""?\s*$", RegexOptions.Compiled);
        private static readonly Regex _biosArchiveRegex = new Regex(@"^      archive:\s*""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex _typeRegex = new Regex(@"\btype:\s*""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex _layoutRegex = new Regex(@"\blayout:\s*""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex _nameRegex = new Regex(@"\bname:\s*""([^""]*)""", RegexOptions.Compiled);

        public static ArcadeDuckDatabase Load(string emulatorPath)
        {
            string dbFile = Path.Combine(emulatorPath, "resources", "arcadedb.yaml");
            if (!File.Exists(dbFile))
            {
                SimpleLogger.Instance.Warning("[ArcadeDuck] Database not found : " + dbFile);
                return null;
            }

            try
            {
                var db = new ArcadeDuckDatabase();
                db.Parse(File.ReadAllLines(dbFile));
                SimpleLogger.Instance.Info("[ArcadeDuck] Database loaded : " + db._gamesById.Count + " games.");
                return db;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[ArcadeDuck] Unable to read database : " + ex.Message);
                return null;
            }
        }

        private void Parse(string[] lines)
        {
            string section = null;      // systems, controlLayouts, games
            string currentSystem = null;
            Game currentGame = null;
            string block = null;        // bios, ports, media

            foreach (var rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                if (line.Length == 0 || line.TrimStart().StartsWith("#"))
                    continue;

                // Top level section
                if (!char.IsWhiteSpace(line[0]))
                {
                    section = line.EndsWith(":") ? line.Substring(0, line.Length - 1).Trim() : null;
                    currentSystem = null;
                    currentGame = null;
                    block = null;
                    continue;
                }

                // New entry (system, layout or game)
                var entry = _entryRegex.Match(line);
                if (entry.Success)
                {
                    block = null;
                    currentSystem = null;
                    currentGame = null;

                    if (section == "systems")
                        currentSystem = entry.Groups[1].Value;
                    else if (section == "games")
                    {
                        currentGame = new Game { Id = entry.Groups[1].Value };
                        _gamesById[currentGame.Id] = currentGame;
                    }
                    continue;
                }

                // Entry fields (4 spaces)
                var field = _fieldRegex.Match(line);
                if (field.Success)
                {
                    string key = field.Groups[1].Value;
                    string value = field.Groups[2].Value.Trim();
                    block = (key == "bios" || key == "media") ? key : null;

                    if (currentGame != null)
                    {
                        switch (key)
                        {
                            case "archive":
                                // ArcadeDuck matches the archive by file title, so 'sfex.7z' also matches 'sfex.zip' (GetArchiveLookupKey)
                                currentGame.Archive = value;
                                _gamesByArchive[Path.GetFileNameWithoutExtension(value)] = currentGame;
                                break;
                            case "system": currentGame.System = value; break;
                            case "parent": currentGame.Parent = value; break;
                            case "status": currentGame.Status = value; break;
                            case "maxPlayers": currentGame.MaxPlayers = value.ToInteger(); break;
                        }
                    }
                    continue;
                }

                string trimmed = line.TrimStart();

                if (currentSystem != null && block == "bios")
                {
                    var bios = _biosArchiveRegex.Match(line);
                    if (bios.Success && !_systemBios.ContainsKey(currentSystem))
                        _systemBios[currentSystem] = bios.Groups[1].Value;
                    continue;
                }

                if (currentGame == null)
                    continue;

                if (trimmed == "ports:")
                {
                    block = "ports";
                    continue;
                }

                if (!trimmed.StartsWith("- {"))
                    continue;

                if (block == "ports")
                {
                    var type = _typeRegex.Match(trimmed);
                    var layout = _layoutRegex.Match(trimmed);
                    currentGame.Ports.Add(new Port
                    {
                        Type = type.Success ? type.Groups[1].Value : "none",
                        Layout = layout.Success ? layout.Groups[1].Value : ""
                    });
                }
                else if (block == "media")
                {
                    var type = _typeRegex.Match(trimmed);
                    var name = _nameRegex.Match(trimmed);
                    if (name.Success)
                        currentGame.Media.Add(new Media { Type = type.Success ? type.Groups[1].Value : "", Name = name.Groups[1].Value });
                }
            }
        }

        public Game FindGameByArchive(string archiveFileName)
        {
            Game game;
            if (!string.IsNullOrEmpty(archiveFileName) && _gamesByArchive.TryGetValue(Path.GetFileNameWithoutExtension(archiveFileName), out game))
                return game;

            return null;
        }

        public Game GetGame(string id)
        {
            Game game;
            if (!string.IsNullOrEmpty(id) && _gamesById.TryGetValue(id, out game))
                return game;

            return null;
        }

        /// <summary>
        /// BIOS archive required by a system. Konami GV is hardcoded in ArcadeDuck (bios.cpp) and has no 'bios' block in the database.
        /// </summary>
        public string GetBiosArchive(string systemId)
        {
            if (string.IsNullOrEmpty(systemId))
                return null;

            string archive;
            if (_systemBios.TryGetValue(systemId, out archive))
                return archive;

            if (systemId == "konami_gv")
                return "konamigv.zip";

            return null;
        }

        /// <summary>
        /// Controller ports of a game. Clones without 'controls' inherit the parent ports (ArcadeDuck itself does not),
        /// and games without any information get standard arcade controls for each player.
        /// </summary>
        public List<Port> GetPorts(Game game)
        {
            if (game == null)
                return new List<Port>();

            if (game.Ports.Count > 0)
                return game.Ports;

            var parent = GetGame(game.Parent);
            if (parent != null && parent.Ports.Count > 0)
                return parent.Ports;

            int players = Math.Max(1, Math.Min(4, game.MaxPlayers > 0 ? game.MaxPlayers : 2));
            return Enumerable.Range(0, players).Select(i => new Port { Type = "arcade", Layout = "" }).ToList();
        }
    }
}
