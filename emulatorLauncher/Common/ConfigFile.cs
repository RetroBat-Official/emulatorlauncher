using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace emulatorLauncher
{
    class ConfigFile : IEnumerable<ConfigItem>
    {
        public ConfigFileOptions Options { get; set; }

        public static ConfigFile FromArguments(string[] args)
        {
            ConfigFile arguments = new ConfigFile();

            string current = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                    current = arg.Substring(1);
                else if (!string.IsNullOrEmpty(current))
                {
                    arguments[current] = arg;
                    current = null;
                }
                else
                    arguments["rom"] = arg;
            }

            return arguments;
        }

        public static ConfigFile LoadEmulationStationSettings(string file)
        {
            var ret = new ConfigFile();
            if (!File.Exists(file))
                return ret;

            try
            {
                XDocument doc = XDocument.Load(file);

                foreach (XElement element in doc.Root.Descendants())
                {
                    if (element.Attribute("name") != null && element.Attribute("value") != null)
                    {
                        string name = element.Attribute("name").Value;
                        string val = element.Attribute("value").Value;
                        ret[name] = val;
                    }
                }
            }
            catch { }

            return ret;
        }

        private static string EmptyLine = "------------EmptyLine----------------";

        public static ConfigFile FromFile(string file, ConfigFileOptions options = null)
        {
            var ret = new ConfigFile();
            ret.Options = options;
            if (!File.Exists(file))
                return ret;

            foreach (var line in File.ReadAllLines(file))
            {
                bool skip = false;

                foreach (var chr in line)
                {
                    if (chr == '#' || chr == ';')
                    {
                        skip = true;
                        break;
                    }

                    if (chr != ' ' && chr != '\t')
                        break;
                }                    

                if (skip)
                    continue;

                int idx = line.IndexOf("=", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    string value = line.Substring(idx + 1).Trim();
                    if (value.StartsWith("\""))
                        value = value.Substring(1);

                    if (value.EndsWith("\""))
                        value = value.Substring(0, value.Length-1);

                    if (options != null && options.CaseSensitive)
                        ret[line.Substring(0, idx).Trim()] = value;
                    else
                        ret[line.Substring(0, idx).ToLower().Trim()] = value;
                }
                else
                    ret[line] = EmptyLine;
            }

            return ret;
        }

        public void ImportOverrides(ConfigFile cfg)
        {
            if (cfg == null)
                return;

            foreach (var item in cfg._data)
                this[item.Name] = item.Value;
        }
        /*
        public void ImportOverrides(IniFile ini)
        {
            if (ini == null)
                return;

            foreach (var section in ini.EnumerateSections())
            {
                foreach (var key in ini.EnumerateKeys(section))
                {
                    if (string.IsNullOrEmpty(key) || key.Trim().StartsWith(";"))
                        continue;

                    string value = ini.GetValue(section, key);
                    if (string.IsNullOrEmpty(value))
                        continue;

                    this[section + "." + key] = value;
                }
            }
        }*/

        public string GetFullPath(string key)
        {
            string data = this[key];
            if (string.IsNullOrEmpty(data))
            {
                if (key == "home" && Directory.Exists(Path.Combine(LocalPath, ".emulationstation")))                        
                    return Path.Combine(LocalPath, ".emulationstation");

                if (key == "bios" || key == "saves" || key == "thumbnails" || key == "shaders" || key == "decorations" || key == "screenshots")
                {
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", key)));
                }
                else
                {                  
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, key))); 
                    
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", key)));

                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", key)));

                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", key)));
                }
                
                return string.Empty;
            }

            if (data.Contains(":")) // drive letter -> Full path
                return data;

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", data)));

            return data;
        }

        private List<ConfigItem> _data = new List<ConfigItem>();

        public ConfigFile() { }

        public string this[string key]
        {
            get
            {
                var item = _data.FirstOrDefault(d => key.Equals(d.Name, StringComparison.InvariantCultureIgnoreCase));
                if (item != null)
                    return item.Value;

                return string.Empty;
            }
            set
            {
                var item = _data.FirstOrDefault(d => key.Equals(d.Name, StringComparison.InvariantCultureIgnoreCase));
                if (item == null)
                {
                    if ((this.Options == null || !Options.KeepEmptyLines) && string.IsNullOrEmpty(value))
                        return;

                    item = new ConfigItem() { Name = key };
                    _data.Add(item);
                }

                if (item.Value != value)
                {
                    item.Value = value;
                    IsDirty = true;
                }
            }
        }

        public bool IsDirty { get; private set; }

        public void Save(string fileName, bool retroarchformat)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in _data)
            {
                sb.Append(item.Name);

                if (item.Value == EmptyLine)
                {
                    sb.AppendLine();
                    continue;
                }

                if (retroarchformat)
                {
                    sb.Append(" = ");
                    sb.Append("\"");
                }
                else
                    sb.Append("=");

                sb.Append(item.Value);

                if (retroarchformat)
                    sb.Append("\"");

                sb.AppendLine();
            }

            File.WriteAllText(fileName, sb.ToString());
        }

        public ConfigFile LoadAll(string p)
        {
            ConfigFile ret = new ConfigFile();

            if (string.IsNullOrEmpty(p))
                return ret;

            foreach (var item in _data)
                if (item.Name.StartsWith(p + "."))
                    ret[item.Name.Substring((p + ".").Length)] = item.Value;

            return ret;
        }

        public bool isOptSet(string p)
        {
            return _data.Any(d => d.Name == p);
        }

        public bool getOptBoolean(string p)
        {
            var data = _data.FirstOrDefault(d => d.Name == p);
            if (data != null)
                return data.Value != null && data.Value.ToLower() == "true" || data.Value == "1";

            return false;
        }

        public int getInteger(string p)
        {
            var data = _data.FirstOrDefault(d => d.Name == p);
            if (data != null && data.Value != null)
            {
                int ret;
                if (int.TryParse(data.Value, out ret))
                    return ret;
            }

            return 0;
        }

        public IEnumerator<ConfigItem> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        public void DisableAll(string name)
        {
            for (int i = _data.Count - 1; i >= 0; i--)
                if (_data[i].Name.Contains(name))
                    _data.RemoveAt(i);
        }

        public static string LocalPath
        {
            get
            {
                if (_localPath == null)
                    _localPath = Path.GetDirectoryName(typeof(ConfigFile).Assembly.Location);

                return _localPath;
            }
        }

        private static string _localPath;
    }
    
    class ConfigItem
    {
        public override string ToString()
        {
            return Name + " = " + Value;
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class ConfigFileOptions
    {
        public bool KeepEmptyLines { get; set; }
        public bool CaseSensitive { get; set; }
    }

}
