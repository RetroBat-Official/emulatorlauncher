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

        public static ConfigFile FromFile(string file)
        {
            var ret = new ConfigFile();
            if (!File.Exists(file))
                return ret;

            foreach (var line in File.ReadAllLines(file))
            {
                if (line.TrimStart().StartsWith("#") || line.TrimStart().StartsWith(";"))
                    continue;

                int idx = line.IndexOf("=");
                if (idx >= 0)
                {
                    string value = line.Substring(idx + 1).Trim();
                    if (value.StartsWith("\""))
                        value = value.Substring(1);

                    if (value.EndsWith("\""))
                        value = value.Substring(0, value.Length-1);

                    ret[line.Substring(0, idx).ToLower().Trim()] = value;
                }
            }

            return ret;
        }

        public void ImportOverrides(ConfigFile cfg)
        {
            foreach (var item in cfg._data)
                this[item.Name] = item.Value;
        }

        public string GetFullPath(string key)
        {
            string data = this[key];
            if (string.IsNullOrEmpty(data))
            {
                if (key == "home" && Directory.Exists(Path.Combine(Program.LocalPath, ".emulationstation")))                        
                    return Path.Combine(Program.LocalPath, ".emulationstation");

                if (key == "bios" || key == "saves" || key == "thumbnails" || key == "shaders" || key == "decorations" || key == "screenshots")
                {
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", key)));
                }
                else
                {                  
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, key))); 
                    
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "emulators", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "emulators", key)));

                    if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "system", "emulators", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "system", "emulators", key)));
                }
                
                return string.Empty;
            }

            if (data.Contains(":")) // drive letter -> Full path
                return data;

            if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "emulators", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "emulators", data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "system", "emulators", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(Program.LocalPath, "..", "system", "emulators", data)));

            return data;
        }

        private List<ConfigItem> _data = new List<ConfigItem>();

        private ConfigFile() { }

        public string this[string key]
        {
            get
            {
                var item = _data.FirstOrDefault(d => d.Name == key);
                if (item != null)
                    return item.Value;

                return string.Empty;
            }
            set
            {
                var item = _data.FirstOrDefault(d => d.Name == key);
                if (item == null)
                {
                    if (string.IsNullOrEmpty(value))
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

}
