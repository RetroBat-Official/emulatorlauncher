using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EmulatorLauncher.Common.FileFormats
{
    public class MultiDiskImageFile
    {
        public string[] Files { get; set; }

        public static MultiDiskImageFile FromFile(string file)
        {
            var files = new List<string>();

            string path = Path.GetDirectoryName(file);
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".cue")
            {
                string start = "FILE";

                foreach (var line in File.ReadAllLines(file))
                {
                    if (string.IsNullOrEmpty(line) || !line.StartsWith(start))
                        continue;

                    var tokens = getTokens(line);
                    if (tokens.Length > 1)
                        files.Add(Path.Combine(path, tokens[1]));
                }
            }
            else if (ext == ".ccd")
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                files.Add(Path.Combine(path, stem + ".cue"));
                files.Add(Path.Combine(path, stem + ".img"));
                files.Add(Path.Combine(path, stem + ".bin"));
                files.Add(Path.Combine(path, stem + ".sub"));
            }
            else if (ext == ".m3u")
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var trim = (line ?? "").Trim();

                    if (string.IsNullOrEmpty(trim) || trim[0] == '#' || trim[0] == '\\' || trim[0] == '/')
                        continue;

                    files.Add(Path.Combine(path, trim));
                }
            }
            else if (ext == ".gdi")
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var tokens = getTokens(line);
                    if (tokens.Length > 5 && tokens[4].Contains("."))
                        files.Add(Path.Combine(path, tokens[4]));
                }
            }

            if (files.Any())
                return new MultiDiskImageFile() { Files = files.ToArray() };

            return null;
        }

        private static string[] getTokens(string line)
        {
            var tokens = new List<string>();

            bool inString = false;
            int startPos = 0;

            int i = 0;
            while(i < line.Length)
            {
                char c = line[i];

                switch (c)
                {
                    case '\"':
                        inString = !inString;
                        if (inString)
                            startPos = i + 1;

                        if (!inString)
                        {
                            string value = line.Substring(startPos, i - startPos);
                            if (!string.IsNullOrEmpty(value))
                                tokens.Add(value);

                            startPos = i + 1;
                        }
                        break;

                    case '\0':
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        if (!inString)
                        {
                            string value = line.Substring(startPos, i - startPos);
                            if (!string.IsNullOrEmpty(value))
                                tokens.Add(value);

                            startPos = i + 1;
                        }
                        break;
                }

                if (c == '\0')
                    break;

                i++;
            }

            return tokens.ToArray();
        }
    }

}
