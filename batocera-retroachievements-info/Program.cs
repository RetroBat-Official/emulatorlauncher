using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;

namespace batocera_retroachievements_info
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            process(args[0]);
        }

        static void process(string userName)
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            var req = WebRequest.Create("https://"+"retroachievements.org/user/" + userName);
            var html = ReadResponseString(req.GetResponse());
            
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = ("\t");
            settings.Encoding = Encoding.UTF8;
            settings.OmitXmlDeclaration = false;

            using (var sw = new UTF8StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sw, settings))
                {
                    writer.WriteStartElement("retroachievements");

                    string tmpFile = ExtractString(html, "<div class='username'><span class='username'>", "</div><div class='userpage recentlyplayed' >");

                    string memberSince = RemoveHtmlTags(ExtractString(tmpFile, "<br>Member Since: ", "<br>"));
                    string rank = RemoveHtmlTags(ExtractString(tmpFile, "<br>Site Rank: ", "<br>"));
                    string averageCompletion = RemoveHtmlTags(ExtractString(tmpFile, "<br>Average Completion: ", "<br>"));
                    string retroRatio = RemoveHtmlTags(ExtractString(tmpFile, "<br>Retro Ratio: ", "<br>"));
                    string accountType = RemoveHtmlTags(ExtractString(tmpFile, "<br>Account Type: ", "<br>"));
                    string lastActivity = RemoveHtmlTags(ExtractString(tmpFile, "<br>Last Activity: ", "<br>"));
                    string totalPoints = RemoveHtmlTags(ExtractString(tmpFile, "<span class='TrueRatio'> (", ")</span>"));

                    writer.WriteElementString("username", userName);
                    writer.WriteElementString("totalpoints", totalPoints);
                    writer.WriteElementString("rank", rank);
                    writer.WriteElementString("userpic", "https://"+"retroachievements.org/UserPic/" + userName + ".png");
                    writer.WriteElementString("registered", memberSince);
                    writer.WriteElementString("lastactivity", lastActivity);
                    
                    var container = ExtractString(html, "<div id=\"leftcontainer\">", "<div id=\"rightcontainer\">");
                    var games = ExtractStrings(container, "<div class='userpagegames'>", "</a></div></div>", true);
                    foreach (var game in games)
                    {
                        writer.WriteStartElement("game");

                        var name = RemoveHtmlTags(ExtractStrings(game, "<a href=", "</a>", true).FirstOrDefault());
                        var lastPlayed = RemoveHtmlTags(ExtractString(game, "<br>Last played ", "<br>"));
                        var achievements = RemoveHtmlTags(ExtractString(game, "<br>Earned ", " achievements,"));
                        var points = RemoveHtmlTags(ExtractString(game, "achievements, ", " points.<br>"));

                        var badgeDiv = ExtractStrings(game, "<div class='bb_inline'", "/>", true)
                            .Select(b => ExtractString(b, "<img ", ">"))
                            .Select(b => ExtractString(b, "src=\"", "\""))
                            .Where(b => !string.IsNullOrEmpty(b))
                            .ToArray();

                        if (badgeDiv.Length == 0)                        
                            badgeDiv = ExtractStrings(game, "<div class='bb_inline'", "/>")
                                .Select(b => ExtractString(b, "img src=\\'", "\\'"))
                                .Where(b => !string.IsNullOrEmpty(b))
                                .ToArray();

                        var badge = badgeDiv.LastOrDefault(b => !b.Contains("_lock"));
                        if (badge == null)
                            badge = badgeDiv.FirstOrDefault();

                        writer.WriteElementString("name", name);
                        writer.WriteElementString("achievements", achievements);
                        writer.WriteElementString("points", points);
                        writer.WriteElementString("lastplayed", lastPlayed);
                        if (!string.IsNullOrEmpty(badge))
                            writer.WriteElementString("badge", badge);

                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                Console.WriteLine(sw.ToString());
            }
        }

        private static string ExtractString(string html, string start, string end)
        {
            int idx1 = html.IndexOf(start);
            if (idx1 < 0)
                return "";

            int idx2 = html.IndexOf(end, idx1 + start.Length);
            if (idx2 > idx1)
                return html.Substring(idx1 + start.Length, idx2 - idx1 - start.Length); 

            return "";
        }

        static string[] ExtractStrings(string cSearchExpression, string cBeginDelim, string cEndDelim, bool keepDelims = false, bool firstOnly = false)
        {
            List<string> ret = new List<string>();

            if (string.IsNullOrEmpty(cSearchExpression))
                return ret.ToArray();

            if (string.IsNullOrEmpty(cBeginDelim))
                return ret.ToArray();

            if (string.IsNullOrEmpty(cEndDelim))
                return ret.ToArray();

            int startpos = cSearchExpression.IndexOf(cBeginDelim, StringComparison.Ordinal);
            while (startpos >= 0 && startpos < cSearchExpression.Length && startpos + cBeginDelim.Length <= cSearchExpression.Length)
            {
                int endpos = cSearchExpression.IndexOf(cEndDelim, startpos + cBeginDelim.Length, StringComparison.Ordinal);
                if (endpos > startpos)
                {
                    if (keepDelims)
                        ret.Add((cBeginDelim + cSearchExpression.Substring(startpos + cBeginDelim.Length, endpos - startpos - cBeginDelim.Length) + cEndDelim).Trim());
                    else
                        ret.Add(cSearchExpression.Substring(startpos + cBeginDelim.Length, endpos - startpos - cBeginDelim.Length).Trim());

                    if (firstOnly)
                        break;

                    startpos = cSearchExpression.IndexOf(cBeginDelim, endpos + cEndDelim.Length, StringComparison.Ordinal);
                }
                else
                    startpos = cSearchExpression.IndexOf(cBeginDelim, startpos + cBeginDelim.Length, StringComparison.Ordinal);
            }

            return ret.ToArray();
        }

        static string RemoveHtmlTags(string texte)
        {
            if (!string.IsNullOrEmpty(texte))
            {
                int start = 0, ss = 0;
                while ((start = texte.IndexOf("<", (ss = start), StringComparison.Ordinal)) >= 0)
                {
                    int end = texte.IndexOf(">", start, StringComparison.Ordinal);
                    if (end >= start)
                        texte = texte.Remove(start, end - start + 1);
                    else
                    {
                        start++;
                        if (start >= texte.Length)
                            break;
                    }
                }

                texte = System.Net.WebUtility.HtmlDecode(texte);
            }

            return texte;
        }

        static string ReadResponseString(WebResponse response)
        {
            String responseString = null;

            using (Stream stream = response.GetResponseStream())
            {
                Encoding encoding = Encoding.Default;

                HttpWebResponse wr = response as HttpWebResponse;
                if (wr != null)
                    encoding = string.IsNullOrEmpty(wr.CharacterSet) ? Encoding.UTF8 : Encoding.GetEncoding(wr.CharacterSet);

                StreamReader reader = new StreamReader(stream, encoding);
                responseString = reader.ReadToEnd();
                stream.Close();

                if (!string.IsNullOrEmpty(responseString))
                    responseString = responseString.Trim();

                return responseString;
            }
        }
    }

    class UTF8StringWriter : StringWriter
    {
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }

}
