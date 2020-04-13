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
            _useSpaces = false;
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
                            if (strLine.StartsWith("[") && strLine.EndsWith("]"))
                                currentSection = strLine.Substring(1, strLine.Length - 2); 
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

        public void WriteValue(string section, string key, string value)
        {
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

        public string GetValue(string section, string key)
        {
            SectionPair sectionPair;
            sectionPair.Section = section;
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
                return "["+Section+"] "+Key;
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

        /*
         public string[] EnumerateSections()
        {
            int bufferSize = 128 * 1024;

            string result = new string(' ', bufferSize);
            int size = GetPrivateProfileString(null, null, null, result, bufferSize, _path);
            if (size > 1)
                return result.Substring(0, size - 1).Split('\0');

            return new string[] { };
        }

        public string[] EnumerateKeys(string section)
        {
            int bufferSize = 128 * 1024;

            string result = new string(' ', bufferSize);
            int size = GetPrivateProfileString(section, null, null, result, bufferSize, _path);
            if (size > 1)
                return result.Substring(0, size - 1).Split('\0');           

            return new string[] { };
        }
       
        public void WriteValue(string section, string key,string value)
		{
			WritePrivateProfileString(section, key, value, _path);
		}
		
        public string GetValue(string section, string key)
		{
			StringBuilder temp = new StringBuilder(2048);

			int ret = GetPrivateProfileString(section, key, "", temp, 2048, _path);
            if (ret > 0)
                return temp.ToString();

            return string.Empty;

		}


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, int nSize, string lpFileName);

        [DllImport("KERNEL32.DLL", EntryPoint = "GetPrivateProfileStringW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern int GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, string lpReturnString, int nSize, string lpFilename);
 */
    }
}
