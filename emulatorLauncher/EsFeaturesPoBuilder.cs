using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    class EsFeaturesPoBuilder
    {
        public static void Process()
        {
            var po = new EsFeaturesPoBuilder();

            var strings = GetEsFeaturesStrings();

            string localeSource = Path.Combine(Path.GetDirectoryName(Program.AppConfig.GetFullPath("home")), "resources", "locale");
            var langs = Directory.GetDirectories(localeSource).Select(l => Path.GetFileName(l)).ToList();

            string root = Path.Combine(Program.AppConfig.GetFullPath("home"), "es_features.locale");

            langs.Insert(0, "");

            foreach (var lang in langs)
            {
                string locale = Path.Combine(root, lang);
                Directory.CreateDirectory(locale);

                var file = PoFile.Read(Path.Combine(locale, string.IsNullOrEmpty(lang) ? "es-features-template.po" : "es-features.po"));

                // Import translations from batocera-es-system.po
                if (File.Exists(Path.Combine(locale, "batocera-es-system.po")))
                {
                    var import = PoFile.Read(Path.Combine(locale, "batocera-es-system.po"));

                    foreach (var item in import.Items)
                    {
                        if (file.Items.Any(i => i.MsgId == item.MsgId))
                            continue;

                        if (!strings.Contains(item.MsgId))
                            continue;

                        file.Items.Add(new PoItem()
                        {
                            MsgCtx = item.MsgCtx,
                            MsgId = item.MsgId,
                            MsgStr = item.MsgStr,
                            Comment = item.Comment
                        });
                    }

                    File.Delete(Path.Combine(locale, "batocera-es-system.po"));
                }

                file.Items.RemoveWhere(i => !strings.Contains(i.MsgId));

                if (file.Items.Count == 0 && !string.IsNullOrEmpty(lang))
                {
                    try
                    {
                        File.Delete(Path.Combine(locale, "es-features.po"));
                        Directory.Delete(locale, true);
                    }
                    catch { }

                    continue;
                }

                foreach (var str in strings)
                {
                    if (file.Items.Any(i => i.MsgId == str))
                        continue;

                    file.Items.Add(new PoItem()
                    {
                        MsgCtx = "game_options",
                        MsgId = str
                    });
                }

                file.Save();
            }
            MessageBox.Show("Translations updated :\r\n" + root, null, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static List<string> GetEsFeaturesStrings()
        {
            var features = EsFeatures.Load(Path.Combine(Program.AppConfig.GetFullPath("home"), "es_features.cfg"));

            List<Feature> toProcess = new List<Feature>();

            if (features.SharedFeatures != null && features.SharedFeatures.Features != null)
                foreach (Feature feature in features.SharedFeatures.Features)                    
                    toProcess.Add(feature);
            
            if (features.GlobalFeatures != null && features.GlobalFeatures.Features != null)
                foreach (Feature feature in features.GlobalFeatures.Features)
                    toProcess.Add(feature);

            if (features.Emulators != null)
            {
                foreach (var emulator in features.Emulators)
                {
                    if (emulator.Features != null)
                    {
                        foreach (Feature feature in emulator.Features)
                            toProcess.Add(feature);
                    }

                    if (emulator.Cores != null)
                    {
                        foreach (var core in emulator.Cores)
                        {
                            if (core.Features != null)
                            {
                                foreach (Feature feature in core.Features)
                                    toProcess.Add(feature);
                            }

                            if (core.Systems == null)
                                continue;

                            foreach (var system in core.Systems)
                            {
                                if (system.Features != null)
                                    foreach (Feature feature in system.Features)
                                        toProcess.Add(feature);
                            }
                        }
                    }

                    if (emulator.Systems != null)
                    {
                        foreach (var system in emulator.Systems)
                        {
                            if (system.Features != null)
                                foreach (Feature feature in system.Features)
                                    toProcess.Add(feature);
                        }
                    }
                }
            }

            List<string> strings = new List<string>();

            foreach (var feature in toProcess)
            {
                if (!string.IsNullOrEmpty(feature.Name))
                    strings.Add(feature.Name);

                if (!string.IsNullOrEmpty(feature.Description))
                    strings.Add(feature.Description);

                if (!string.IsNullOrEmpty(feature.SubMenu))
                    strings.Add(feature.SubMenu);

                if (feature.Choice != null)
                {
                    foreach (var choice in feature.Choice)
                    {
                        if (!string.IsNullOrEmpty(choice.Name))
                            strings.Add(choice.Name);
                    }
                }
            }

            strings = strings
                .Where(s => !string.IsNullOrEmpty(s) && !IsExcluded(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToList();

            return strings;
        }

        static HashSet<string> _blackList;
        
        private static bool IsExcluded(string s)
        {
            if (_blackList == null)
            {
                _blackList = new HashSet<string>()
                {
                    "ON", "OFF", "YES", "NO", "NTSC", "PAL", "VGA", "SVGA", "EGA", "CGA", "OpenGL", "OPENGL", "Vulkan", "VULKAN", "DirectX 11", "DIRECTX 11", "NONE",
                    "RETROARCH", "XINPUT", "SDL2", "NINTENDO", "SEGA"
                };

                foreach (var item in Properties.Resources.blacklisted_words.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    _blackList.Add(item);
                    if (item.ToUpperInvariant() != item)
                        _blackList.Add(item.ToUpperInvariant());
                }
            }

            if (_blackList.Contains(s))
                return true;

            return s.All(c => char.IsDigit(c) || c == 'x' || c == 'X' || c == '%' || c == '/' || c == 'K' || c == 'k' || c == 'p' || c == 'P' || c == '(' || c == ')' || c == ' ' || c == '.' || c == ',');
        }


        class PoFile
        {
            public List<PoItem> Items { get; set; }
            private string _path;
            private string _lang;

            static string[] _header = new string[]
            {                                
                "Project-Id-Version: emulationstation\\n",
                "Report-Msgid-Bugs-To: \\n",
                "POT-Creation-Date: 2021-12-16 12:57+0100\\n",
                "PO-Revision-Date: \\n",
                "Language-Team: Retrobat\\n",
                "Language: %LANG%\\n",
                "MIME-Version: 1.0\\n",
                "Content-Type: text/plain; charset=UTF-8\\n",
                "Content-Transfer-Encoding: 8bit\\n",
                "Plural-Forms: nplurals=2; plural=(n > 1);\\n",
                "X-Generator: Poedit 3.0\\n"
            };

            public void Save()
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("msgid \"\"");
                sb.AppendLine("msgstr \"\"");

                foreach (var hd in _header)
                    sb.AppendLine("\"" + hd.Replace("%LANG%", _lang) + "\"");

                sb.AppendLine();

                foreach (var item in Items)
                    sb.AppendLine(item.ToString());

                File.WriteAllText(_path, sb.ToString());
            }

            private PoFile()
            {
            }

            public static PoFile Read(string fn, bool includeEmptyStrings = false)
            {
                var ret = new PoFile();
                ret._path = fn;
                ret._lang = Path.GetFileName(Path.GetDirectoryName(fn));
                int idx = ret._lang.IndexOf("_");
                if (idx > 0)
                    ret._lang = ret._lang.Substring(0, idx);

                ret.Items = new List<PoItem>();

                if (!File.Exists(fn))
                    return ret;

                string msgctxt = null;
                string msgid = null;
                string msgstr = null;
                string comment = null;

                int cur = 0;

                var lines = File.ReadAllLines(fn).ToList();
                lines.Add("");

                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        if (cur > 1 && !string.IsNullOrEmpty(msgid))
                        {
                            if (includeEmptyStrings || !string.IsNullOrEmpty(msgstr))
                            {
                                ret.Items.Add(new PoItem()
                                {
                                    MsgCtx = msgctxt,
                                    MsgId = msgid,
                                    MsgStr = msgstr,
                                    Comment = comment
                                });
                            }
                        }

                        cur = 0;
                        msgid = null;
                        msgstr = null;
                        msgctxt = null;
                        comment = null;
                        continue;
                    }

                    if (line.StartsWith("#") && cur == 0)
                    {
                        comment = line.Substring(1).Trim();
                        continue;
                    }

                    if (line.StartsWith("msgctxt"))
                    {
                        msgctxt = line.ExtractString("\"", "\"");
                        cur = 1;
                        continue;
                    }

                    if (line.StartsWith("msgid"))
                    {
                        cur = 2;
                        msgid = line.ExtractString("\"", "\"");
                        continue;
                    }

                    if (line.StartsWith("msgstr"))
                    {
                        cur = 3;
                        msgstr = line.ExtractString("\"", "\"");
                        continue;
                    }

                    if (cur == 2 && msgid != null)
                    {
                        msgid += line.ExtractString("\"", "\"");
                        continue;
                    }

                    if (cur == 3 && msgid != null)
                    {
                        msgstr += line.ExtractString("\"", "\"");
                        continue;
                    }
                }

                return ret;
            }
        }

        class PoItem
        {
            public string MsgCtx { get; set; }
            public string MsgId { get; set; }
            public string MsgStr { get; set; }

            public string Comment { get; set; }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                if (!string.IsNullOrEmpty(Comment))
                    sb.AppendLine("# " + Comment);

                sb.AppendLine("msgctxt \"" + (MsgCtx == null ? "" : MsgCtx) + "\"");
                sb.AppendLine("msgid \"" + (MsgId == null ? "" : MsgId.Replace("\"", "")) + "\"");
                sb.AppendLine("msgstr \"" + (MsgStr == null ? "" : MsgStr.Replace("\"", "")) + "\"");


                return sb.ToString();
            }
        }

    }
}
