using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EmulatorLauncher.Common
{
    public static class StringExtensions
    {
        public static string ExtractString(this string html, string start, string end)
        {
            int idx1 = html.IndexOf(start);
            if (idx1 < 0)
                return "";

            int idx2 = html.IndexOf(end, idx1 + start.Length);
            if (idx2 > idx1)
                return html.Substring(idx1 + start.Length, idx2 - idx1 - start.Length);

            return "";
        }

        public static string[] ExtractStrings(this string cSearchExpression, string cBeginDelim, string cEndDelim, bool keepDelims = false, bool firstOnly = false, StringComparison comparison = StringComparison.Ordinal)
        {
            List<string> ret = new List<string>();

            if (string.IsNullOrEmpty(cSearchExpression))
                return ret.ToArray();

            if (string.IsNullOrEmpty(cBeginDelim))
                return ret.ToArray();

            if (string.IsNullOrEmpty(cEndDelim))
                return ret.ToArray();

            int startpos = cSearchExpression.IndexOf(cBeginDelim, comparison);
            while (startpos >= 0 && startpos < cSearchExpression.Length && startpos + cBeginDelim.Length <= cSearchExpression.Length)
            {
                int endpos = cSearchExpression.IndexOf(cEndDelim, startpos + cBeginDelim.Length, comparison);
                if (endpos > startpos)
                {
                    if (keepDelims)
                        ret.Add((cSearchExpression.Substring(startpos, cBeginDelim.Length) + cSearchExpression.Substring(startpos + cBeginDelim.Length, endpos - startpos - cBeginDelim.Length) + cEndDelim).Trim());
                    else
                        ret.Add(cSearchExpression.Substring(startpos + cBeginDelim.Length, endpos - startpos - cBeginDelim.Length).Trim());

                    if (firstOnly)
                        break;

                    startpos = cSearchExpression.IndexOf(cBeginDelim, endpos + cEndDelim.Length, comparison);
                }
                else
                    startpos = cSearchExpression.IndexOf(cBeginDelim, startpos + cBeginDelim.Length, comparison);
            }

            return ret.ToArray();
        }

        public static int ToInteger(this string value)
        {
            int ret = 0;
            int.TryParse(value, out ret);
            return ret;
        }

        public static ulong ToUlong(this string value)
        {
            ulong ret = 0;
            ulong.TryParse(value, out ret);
            return ret;
        }

        public static string ToIntegerString(this string value)  // To be used with sliders as they save by default with .000000 decimals
        {
            double parsedValue;

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue))
            {
                return ((int)parsedValue).ToString();
            }

            return value;
        }

        public static float ToFloat(this string value)
        {
            float ret = 0;
            float.TryParse(value,System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ret);
            return ret;
        }

        public static double ToDouble(this string value)
        {
            double ret = 0;
            double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ret);
            return ret;
        }

        public static float ToRatio(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            string[] parts = value.Split(new char[] { ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return 0;

            float numerator = parts[0].ToFloat();
            float denominator = parts[1].ToFloat();

            if (denominator == 0)
                return 0;

            return numerator / denominator;
        }

        public static string JoinArguments(this IEnumerable<string> args)
        {
            return string.Join(" ", args.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());
        }

        public static string FormatVersionString(string version)
        {
            var numbers = version.Split(new char[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (numbers.Count < 4)
                numbers.Add("0");

            return string.Join(".", numbers.Take(4).ToArray());
        }

        public static string[] SplitCommandLine(this string commandLine)
        {
            char[] parmChars = commandLine.ToCharArray();

            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++)
            {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }

            return (new string(parmChars))
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Replace("\"", ""))
                .ToArray();
        }

        public static string AsIndexedRomName(this string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            if (name.Contains("\\") || name.Contains("/"))
                name = System.IO.Path.GetFileNameWithoutExtension(name);

            StringBuilder ret = new StringBuilder(name.Length);

            bool inpar = false;
            bool inblock = false;

            foreach (var c in name.ToLowerInvariant())
            {
                if (c == '(')
                    inpar = true;
                else if (c == ')')
                    inpar = false;
                else if (c == '[')
                    inblock = true;
                else if (c == ']')
                    inblock = false;
                else if (!inpar && !inblock && (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    ret.Append(c);
            }

            return ret.ToString().Trim();
        }


    }
}
