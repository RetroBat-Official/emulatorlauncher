using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace emulatorLauncher
{
	public class IniFile : IDisposable
	{
        public IniFile(string path, bool useSpaces = false)
		{
            _useSpaces = useSpaces;
            _path = path;
            _dirty = false;

            if (!File.Exists(_path))
                return;

            try
            {
                using (TextReader iniFile = new StreamReader(_path))
                {
                    string currentSection = null;

                    string strLine = iniFile.ReadLine();
                    while (strLine != null)
                    {
                        strLine = strLine.Trim();

                        if (strLine != "")
                        {
                            if (strLine.StartsWith("["))
                            {
                                int end = strLine.IndexOf("]");
                                if (end > 0)
                                    currentSection = strLine.Substring(1, end - 1);
                            }
                            else
                            {
                                string[] keyPair = strLine.Split(new char[] { '=' }, 2);

                                SectionPair sectionPair;
                                string value = null;

                                if (currentSection == null)
                                    currentSection = "ROOT";

                                sectionPair.Section = currentSection;
                                sectionPair.Key = keyPair[0].Trim();

                                if (keyPair.Length > 1)
                                    value = keyPair[1].Trim();

                                if (!_keyPairs.ContainsKey(sectionPair))
                                    _keyPairs.Add(sectionPair, value);
                            }
                        }

                        strLine = iniFile.ReadLine();
                    }

                    iniFile.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }         
        }

        public string[] EnumerateSections()
        {
            return _keyPairs.Keys.Select(k => k.Section).Distinct().ToArray();
        }

        public string[] EnumerateKeys(string section)
        {
            List<string> ret = new List<string>();

            foreach (SectionPair pair in _keyPairs.Keys)
                if (pair.Section == section)
                    ret.Add(pair.Key);

            return ret.Distinct().ToArray();
        }

        public void ClearSection(string section)
        {
            foreach (var sectionPair in _keyPairs.Keys.ToArray())
            {
                if (sectionPair.Section == section)
                {
                    _keyPairs.Remove(sectionPair);
                    _dirty = true;
                }
            }
        }

        public void WriteValue(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(section))
                section = "ROOT";

            if (GetValue(section, key) == value)
                return;

            _dirty = true;

            SectionPair sectionPair;
            sectionPair.Section = section;
            sectionPair.Key = key;

            if (_keyPairs.ContainsKey(sectionPair))
                _keyPairs.Remove(sectionPair);

            if (!string.IsNullOrEmpty(value))
                _keyPairs.Add(sectionPair, value);
        }

        public void Remove(string section, string key)
        {
            SectionPair sectionPair;
            sectionPair.Section = section;
            sectionPair.Key = key;

            if (_keyPairs.ContainsKey(sectionPair))
                _keyPairs.Remove(sectionPair);
        }

        public string GetValue(string section, string key)
        {
            SectionPair sectionPair;
            sectionPair.Section = string.IsNullOrEmpty(section) ? "ROOT" : section;
            sectionPair.Key = key;

            if (_keyPairs.ContainsKey(sectionPair))
                return _keyPairs[sectionPair];

            return null;
        }

        public void Save()
        {
            if (!_dirty)
                return;

            ArrayList sections = new ArrayList();     

            StringBuilder sb = new StringBuilder();

            foreach (string section in EnumerateSections())
            {
                if (section != "ROOT")
                    sb.AppendLine("[" + section + "]");

                foreach (var entry in _keyPairs)
                {                    
                    if (entry.Key.Section != section)
                        continue;

                    if (string.IsNullOrEmpty(entry.Value))
                        continue;

                    sb.Append(entry.Key.Key);

                    if (_useSpaces)
                        sb.Append(" ");

                    sb.Append("=");

                    if (_useSpaces)
                        sb.Append(" ");

                    sb.AppendLine(entry.Value);                    
                }

                sb.AppendLine();
            }
            
            try
            {
                using (TextWriter tw = new StreamWriter(_path))
                {
                    tw.Write(sb.ToString());
                    tw.Close();
                }

                _dirty = false;
            }
            catch (Exception ex)
            {
              
            }
        }

        public void Dispose()
        {
            Save();
        }

        private bool _useSpaces;
        private bool _dirty;
        private string _path;
        private Dictionary<SectionPair, string> _keyPairs = new Dictionary<SectionPair, string>();

        private struct SectionPair
        {
            public string Section;
            public string Key;

            public override string ToString()
            {
                return "["+(Section??"")+"] "+(Key??"");
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is SectionPair)
                {
                    SectionPair sp = (SectionPair)obj;
                    return (Section == sp.Section && Key == sp.Key);
                }

                return base.Equals(obj);
            }
        }
    }
}
