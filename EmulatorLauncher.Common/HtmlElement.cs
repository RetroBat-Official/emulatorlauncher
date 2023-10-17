using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace EmulatorLauncher.Common.FileFormats
{
    public class HtmlElement
    {
        public HtmlElement()
        {            
            Attributes = new HtmlAttributeCollection();
            Style = new HtmlAttributeCollection(true);
        }

        public string Name { get; set; }
        public IList<HtmlElement> Children { get; set; }
        public string Text { get; set; }

        public HtmlAttributeCollection Style { get; private set; }
        public HtmlAttributeCollection Attributes { get; private set; }

        public string Id { get { return Attributes["id"]; } set { Attributes["id"] = value; } }
        public string Class { get { return Attributes["class"]; } set { Attributes["class"] = value; } }

        #region Parse
        public static HtmlElement Parse(string s)
        {
            var rootTag = new HtmlElement();
            var currentTag = rootTag;

            var stack = new Stack<HtmlElement>();
            stack.Push(rootTag);

            int textStart = 0;

            if (!string.IsNullOrEmpty(s))
                s = s.Replace("&#8203;", "");

            int iPos = IndexOfHtmlChar(s, '<');
            while (iPos >= 0)
            {
                string text = null;

                if (iPos > textStart)
                    text = s.Substring(textStart, iPos - textStart);

                int iEnd = s.IndexOf(">", iPos);
                if (iEnd < 0)
                    break;

                textStart = iEnd + 1;
                string tag = s.Substring(iPos, iEnd - iPos + 1);

                if (!tag.EndsWith("/>") && !tag.StartsWith("</"))
                {
                    string tagName = tag;

                    int iNameEnd = tag.IndexOfAny(new char[] { ' ', '\r', '\n', '>' });
                    if (iNameEnd >= 0)
                        tagName = FormatTag(tag.Substring(1, iNameEnd - 1));
                    else
                        tagName = FormatTag(tag.Substring(1, tag.Length - 2).Trim());

                    if (_tagsNoChildren.Contains(tagName))
                    {
                        bool shouldCloseTag = true;

                        int nextTag = IndexOfHtmlChar(s, '<', iPos + tag.Length + 1);
                        if (nextTag > 0 && nextTag + tagName.Length + 3 < s.Length)
                        {
                            if (s[nextTag + 1] == '/' && s.Substring(nextTag, tagName.Length + 3) == "</" + tagName + ">")
                                shouldCloseTag = false;
                        }

                        if (shouldCloseTag)
                            tag = tag.Substring(0, tag.Length - 1) + "/>";
                    }
                }

                if (tag.EndsWith("/>"))
                {
                    string attributes = null;

                    int iNameEnd = tag.IndexOfAny(new char[] { ' ', '\r', '\n' });
                    if (iNameEnd >= 0)
                    {
                        attributes = tag.Substring(iNameEnd + 1, tag.Length - iNameEnd - 3);
                        tag = FormatTag(tag.Substring(1, iNameEnd - 1));
                    }
                    else
                        tag = FormatTag(tag.Substring(1, tag.Length - 3).Trim());

                    //tag = formattedTag(tag.Substring(1, tag.Length - 3).Trim());

                    if (text != null && currentTag.Name != "table" && currentTag.Name != "tr" && currentTag.Name != "tbody")
                    {
                        currentTag.AddTextChild(text);
                    }

                    if (tag == "!DOCTYPE")
                    {
                        HtmlElement htag = new HtmlElement() { Name = tag };
                        htag.Text = attributes;
                        currentTag.AddChild(htag);
                    }
                    else
                    {
                        HtmlElement htag = new HtmlElement() { Name = tag };
                        htag.ParseAttributes(attributes);
                        htag._hasEmptySource = true;
                        currentTag.AddChild(htag);
                    }
                }
                else if (tag.StartsWith("</"))
                {
                    tag = FormatTag(tag.Substring(2, tag.Length - 3));

                    if (stack.Count > 1 || (stack.Count == 1 && stack.Peek().Name != null))
                    {
                        if (stack.Peek().Name != tag)
                        {
                            bool hasTag = false;

                            foreach (var itm in stack.ToArray())
                            {
                                if (itm.Name == tag)
                                {
                                    hasTag = true;
                                    break;
                                }
                            }

                            if (hasTag)
                            {
                                while (stack.Count > 1 && stack.Peek().Name != tag)
                                    stack.Pop();

                            }
                            else
                            {

                            }
                        }

                        HtmlElement htag = stack.Pop();

                        if (text != null)
                        {
                            if (currentTag.Children != null && currentTag.Children.Count > 0)
                            {
                                if (currentTag.Name != "table" && currentTag.Name != "tr" && currentTag.Name != "tbody")
                                {
                                    //if (currentTag.Name == "td")
                                    //    text = text.Replace("\t", "").Replace("\r", "").Replace("\n", "");

                                    currentTag.AddTextChild(text);
                                }
                            }
                            else
                                htag.Text = htag.FormatText(text);
                        }

                        if (stack.Count == 0)
                        {
                            currentTag = null;
                            break;
                        }
                        else
                            currentTag = stack.Peek();
                    }
                    else
                    {
                        // Anormal
                    }
                }
                else
                {
                    if (text != null && currentTag.Name != "table" && currentTag.Name != "tr" && currentTag.Name != "tbody" && !_tagsNoChildren.Contains(currentTag.Name))
                        currentTag.AddTextChild(text);

                    string attributes = null;

                    if (tag.StartsWith("<!--"))
                    {
                        int commentEnd = s.IndexOf("-->", iPos + 1);
                        if (commentEnd >= 0)
                        {
                            HtmlElement htag = new HtmlElement() { Name = "!--" };
                            htag.Text = s.Substring(iPos + 4, commentEnd - iPos - 4);

                            if (currentTag.Children == null)
                                currentTag.Children = new HtmlElementCollection(currentTag);

                            currentTag.Children.Add(htag);
                            iPos = commentEnd + 2;
                        }
                    }
                    else
                    {
                        int iNameEnd = tag.IndexOfAny(new char[] { ' ', '\r', '\n' });
                        if (iNameEnd >= 0)
                        {
                            attributes = tag.Substring(iNameEnd + 1, tag.Length - iNameEnd - 2);
                            tag = FormatTag(tag.Substring(1, iNameEnd - 1));
                        }
                        else
                            tag = FormatTag(tag.Substring(1, tag.Length - 2));

                        HtmlElement htag = new HtmlElement() { Name = tag };
                        htag.ParseAttributes(attributes);

                        if (currentTag.Children == null)
                            currentTag.Children = new HtmlElementCollection(currentTag);

                        currentTag.Children.Add(htag);
                        stack.Push(htag);
                        currentTag = stack.Peek();
                    }
                }

                iPos = IndexOfHtmlChar(s, '<', iPos + 1);
            }

            if (textStart < s.Length)
            {
                string text = s.Substring(textStart);

                if (currentTag != null)
                    currentTag.AddTextChild(text);
            }

            var tags = rootTag.Traverse().ToArray();
            foreach (var tag in tags)
            {
                if (tag.Text != null && tag.Name != null)
                    tag.Text = tag.Text.Trim();

                if (tag.Parent != null && tag.Name == null && tag.Children == null && string.IsNullOrEmpty(tag.ToText().Trim()))
                    tag.Remove();
            }

            if (rootTag.Name == null && rootTag.Text == null && rootTag.Children != null && rootTag.Children.Count == 1)
                return rootTag.Children[0];

            return rootTag;
        }
        
        public void ParseAttributes(string s)
        {
            Style.Clear();
            Attributes.Clear();

            if (string.IsNullOrEmpty(s))
                return;

            try
            {
                s = s.Replace("\r", " ").Replace("\n", "").Trim();

                int pos = 0;
                StringBuilder temp = new StringBuilder();

                string name = null;
                string value = null;

                char inQuote = '\0';

                while (pos <= s.Length)
                {
                    if (pos == s.Length)
                    {
                        value = temp.ToString();

                        if (value.StartsWith("\"") && value.EndsWith("\""))
                            value = value.Substring(1).Substring(0, value.Length - 2);
                        else if (value.StartsWith("\'") && value.EndsWith("\'"))
                            value = value.Substring(1).Substring(0, value.Length - 2);

                        if (!string.IsNullOrEmpty(name))
                        {
                            if (name == "style")
                                ParseStyle(value);
                            else
                                Attributes[name] = System.Net.WebUtility.HtmlDecode(value);                                
                        }

                        break;
                    }

                    char c = s[pos];

                    if (c != '\0' && c == inQuote)
                        inQuote = '\0';
                    else if ((c == '\'' || c == '\"') && inQuote == '\0')
                        inQuote = c;
                    else if (inQuote == '\0' && c == '=')
                    {
                        name = HtmlElement.FormatTag(temp.ToString()).Trim();
                        temp.Clear();
                        pos++;
                        continue;
                    }
                    else if (!string.IsNullOrEmpty(name) && inQuote == '\0' && (c == ' ' || c == '\r' || c == '\n'))
                    {
                        value = temp.ToString();

                        if (value.StartsWith("\"") && value.EndsWith("\""))
                            value = value.Substring(1).Substring(0, value.Length - 2);
                        else if (value.StartsWith("\'") && value.EndsWith("\'"))
                            value = value.Substring(1).Substring(0, value.Length - 2);

                        if (!string.IsNullOrEmpty(name))
                        {
                            if (name == "style")
                                ParseStyle(value);
                            else
                                Attributes[name] = System.Net.WebUtility.HtmlDecode(value);

                            name = "";
                        }

                        temp.Clear();
                        pos++;
                        continue;
                    }

                    temp.Append(c);
                    pos++;
                }
            }
            catch { }
        }

        private static Regex _stripStyleAttributesRegex = new Regex(@"(?<name>.+?):\s*(?<val>[^;]+);*\s*", RegexOptions.Compiled);

        private void ParseStyle(string tagValue)
        {
            try
            {
                MatchCollection ms = _stripStyleAttributesRegex.Matches(tagValue);
                foreach (Match mt in ms)
                {
                    var name = mt.Groups["name"].Value;
                    if (string.IsNullOrEmpty(name))
                        continue;

                    name = HtmlElement.FormatTag(name);
                    var val = mt.Groups["val"].Value;

                    Style[name] = val;
                }
            }
            catch { }
        }
        #endregion

        public void SetAttribute(string key, string value)
        {
            if (key == "style")
            {
                Style.Clear();
                ParseStyle(value);
                return;
            }

            Attributes[key] = System.Net.WebUtility.HtmlDecode(value);
        }

        public HtmlElement Parent { get; private set; }

        public IEnumerable<HtmlElement> Traverse()
        {
            var stack = new Stack<HtmlElement>();
            stack.Push(this);

            while (stack.Count != 0)
            {
                var current = stack.Pop();
                yield return current;

                if (current.Children != null)
                {
                    for (var i = current.Children.Count - 1; i >= 0; i--)
                        stack.Push(current.Children[i]);
                }
            }
        }

        public HtmlElement Trim()
        {
            return this.TrimStart().TrimEnd();
        }

        public HtmlElement TrimEnd()
        {
            if (this.Children != null)
            {
                while (this.Children.Count > 0 && this.Children[this.Children.Count - 1].Name == "p" && string.IsNullOrEmpty(this.Children[this.Children.Count - 1].Text) && (this.Children[this.Children.Count - 1].Children == null || this.Children[this.Children.Count - 1].Children.Count == 0))
                    this.Children.RemoveAt(this.Children.Count - 1);
            }

            return this;
        }

        public HtmlElement TrimStart()
        {
            if (this.Children != null)
            {
                while (this.Children.Count > 0 && (this.Children[0].Name == null || this.Children[0].Name == "p") && string.IsNullOrEmpty(this.Children[0].Text) && (this.Children[0].Children == null || this.Children[0].Children.Count == 0))
                    this.Children.RemoveAt(0);
            }

            return this;
        }

        private static HashSet<string> _commonTags = new HashSet<string>(new string[] 
        { 
            "br", "meta", "link", "p", "align", "style", "class", "div", "span", "head", "body", "table", "td", "tr", "thead" 
        }, StringComparer.InvariantCultureIgnoreCase);

        private static string FormatTag(string tagName)
        {
            if (tagName != null)
                tagName = tagName.Replace("\r", "").Replace("\n", "");

            if (_commonTags.Contains(tagName))
                return tagName.ToLowerInvariant();
            
            return tagName;
        }

        public string ToHtml(bool indent = false, Func<string, string> contentCleaner = null)
        {
            return ToHtmlInternal(indent ? 0 : -1, contentCleaner, true);
        }

        private string ToHtmlInternal(int indent, Func<string, string> contentCleaner, bool isRoot)
        {
            if (string.IsNullOrEmpty(Name) && _hasEmptySource)
                return "";

            StringBuilder sb = new StringBuilder();

            if (!isRoot && this.Name != null)
            {
                sb.AppendLine();

                if (indent > 0)
                    sb.Append(new string(' ', indent * 4));
            }

            if (Name == "!--")
            {
                sb.Append("<!--" + this.Text + "-->");
                return sb.ToString();
            }

            if (Name != null && Name.StartsWith("!"))
            {
                sb.Append("<" + this.Name + " " + this.Text + ">");
                return sb.ToString();
            }

            if (Name == "br" && Children == null && Text == null)
                return "<br />";

            if (Name == "p" && Children == null && Text == null && _hasEmptySource)
                return "<p/>";

            if (Name == "p" && Children == null && Text == "&nbsp;")
                return "<p></p>";

            if (Name == "p" && Children == null && string.IsNullOrEmpty(Text))
                return "<p></p>";

            if (Name != null)
            {
                sb.Append("<");
                sb.Append(Name);

                var sbAttributes = new StringBuilder();
               
                if (this.Attributes.Count > 0)
                    sbAttributes.Append(Attributes.ToString());

                if (Style.Count > 0)
                {
                    if (sbAttributes.Length > 0)
                        sbAttributes.Append(" ");

                    sbAttributes.Append("style=\"");
                    sbAttributes.Append(Style.ToString());
                    sbAttributes.Append("\"");
                }

                if (sbAttributes.Length > 0)
                {
                    sb.Append(" ");
                    sb.Append(sbAttributes.ToString());
                }
            }

            if (Name != "td" && Name != "u" && Name != "b" && Name != "strong" && Name != "var" && Children == null && string.IsNullOrEmpty(Text))// == null)
            {
                if (Name != null)
                {
                    if (_tagsNoChildren.Contains(Name) && Name.ToLowerInvariant() != "br")
                        sb.Append(">");
                    else
                        sb.Append("/>");
                }
            }
            else
            {
                if (Name != null)
                    sb.Append(">");

                if (Children != null)
                {
                    int newIndent = indent;

                    if (this.Name != null)
                        newIndent = indent + 1;

                    foreach (var h in Children)
                    {
                        string ht = h.ToHtmlInternal(newIndent, contentCleaner, false);
                        if (!string.IsNullOrEmpty(ht))
                            sb.Append(ht);
                    }
                }

                if (!string.IsNullOrEmpty(Text))
                {
                    if (contentCleaner != null)
                        sb.Append(contentCleaner(Text));
                    else
                        sb.Append(Text);
                }

                if (Name != null)
                {
                    if (Children != null && Children.Count > 0 && this.Name != null)
                    {
                        sb.AppendLine();
                        
                        if (indent > 0)
                            sb.Append(new string(' ', indent * 4));
                    }

                    sb.Append("</");
                    sb.Append(Name);
                    sb.Append(">");
                }
            }

       //     if (indent == 0)
        //        return sb.ToString().Trim();

            return sb.ToString();
        }

        public string OuterHtml
        {
            get
            {
                return this.ToHtml(true);
            }
        }

        public string InnerHtml
        {
            get
            {
                StringBuilder sb = new StringBuilder();

                if (Children != null)
                {
                    foreach (var h in Children)
                    {
                        string ht = h.ToHtml(true);
                        if (!string.IsNullOrEmpty(ht))
                            sb.AppendLine(ht);
                    }
                }
                else if (!string.IsNullOrEmpty(this.Text))
                    sb.Append(this.Text);

                return sb.ToString();
            }
        }

        private static HashSet<string> _tagsNoChildren = new HashSet<string>(new string[] { "br", "meta", "link", "!DOCTYPE" }, StringComparer.InvariantCultureIgnoreCase);

        private static int IndexOfHtmlChar(string s, char c, int pos = 0)
        {
            bool inString = false;
            bool inChar = false;
            bool tagWasClosed = false;

            for (int i = pos; i < s.Length; i++)
            {
                if (!tagWasClosed)
                {
                    if (s[i] == '\'')
                        inChar = !inChar;

                    if (s[i] == '\"')
                        inString = !inString;
                }

                if (c == '<' && !inString && !inChar && s[i] == '>')
                    tagWasClosed = true;

                if (!inString && !inChar && s[i] == c)
                    return i;
            }

            return -1;
        }


        public HtmlElement NextSibling
        {
            get
            {
                if (Parent == null || Parent.Children == null)
                    return null;

                int idx = Parent.Children.IndexOf(this);
                if (idx >= 0 && idx + 1 < Parent.Children.Count)
                    return Parent.Children[idx + 1];

                return null;
            }
        }

        public HtmlElement PreviousSibling
        {
            get
            {
                if (Parent == null || Parent.Children == null)
                    return null;

                int idx = Parent.Children.IndexOf(this);
                if (idx > 0)
                    return Parent.Children[idx - 1];

                return null;
            }
        }

        class HtmlElementCollection : IList<HtmlElement>
        {
            public HtmlElementCollection(HtmlElement parent)
            {
                _parent = parent;
                _innerList = new List<HtmlElement>();
            }

            private List<HtmlElement> _innerList;
            private HtmlElement _parent;

            public int IndexOf(HtmlElement item)
            {
                return _innerList.IndexOf(item);
            }

            public void Insert(int index, HtmlElement item)
            {
                item.Parent = _parent;
                _innerList.Insert(index, item);
            }

            public void RemoveAt(int index)
            {
                _innerList.RemoveAt(index);
            }

            public HtmlElement this[int index]
            {
                get
                {
                    return _innerList[index];
                }
                set
                {
                    _innerList[index] = value;
                }
            }

            public void Add(HtmlElement item)
            {
                item.Parent = _parent;
                _innerList.Add(item);
            }

            public void Clear()
            {
                _innerList.Clear();
            }

            public bool Contains(HtmlElement item)
            {
                return _innerList.Contains(item);
            }

            public void CopyTo(HtmlElement[] array, int arrayIndex)
            {
                _innerList.CopyTo(array, arrayIndex);
            }

            public int Count
            {
                get { return _innerList.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool Remove(HtmlElement item)
            {
                if (item != null)
                    item.Parent = null;

                return _innerList.Remove(item);
            }

            public IEnumerator<HtmlElement> GetEnumerator()
            {
                return _innerList.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _innerList.GetEnumerator();
            }
        }

        public bool IsEmpty { get { return _hasEmptySource; } }


        public bool HasContent
        {
            get
            {
                if (!string.IsNullOrEmpty(this.Text))
                    return true;

                if (this.Children == null)
                    return false;

                foreach (var child in Children)
                {
                    if (child.Name != null)
                        return true;

                    if (child.HasContent)
                        return true;
                }

                return false;
            }
        }

        private bool _hasEmptySource;

        private string FormatText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (this.Name != "span")
            {
                while (text.StartsWith("\r") || text.StartsWith("\n") || text.StartsWith("\t"))
                    text = text.Substring(1);
            }

            return text.Replace("\t", "").Replace("\r", " ").Replace("\n", "");
        }

        public void AddTextChild(string text)
        {
            if (text == null)
                return;

            if (text == "\r\n" && this.Parent == null)
                return;

            if (this.Children == null)
                this.Children = new HtmlElementCollection(this);

            if (this.Name == null)
                this.Children.Add(new HtmlElement() { Text = text });
            else
                this.Children.Add(new HtmlElement() { Text = FormatText(text) });
        }

        public void AddChild(HtmlElement tag)
        {
            if (tag == null)
                return;

            if (this.Children == null)
                this.Children = new HtmlElementCollection(this);

            this.Children.Add(tag);
        }


        public void InsertAfter(HtmlElement newPar)
        {
            if (this.Parent == null || this.Parent.Children == null)
                return;

            int idx = this.Parent.Children.IndexOf(this);
            if (idx < 0)
                return;

            if (idx + 1 >= this.Parent.Children.Count)
                this.Parent.AddChild(newPar);
            else
                this.Parent.Children.Insert(idx + 1, newPar);
        }

        public string ToText(bool keepBasicFormatting = false)
        {
            if (string.IsNullOrEmpty(Name) && _hasEmptySource)
                return "";

            StringBuilder sb = new StringBuilder();

            if (Name == "br" && Children == null && Text == null)
                return "\r\n";

            if (Name == "p" && Children == null && Text == null && _hasEmptySource)
                return "\r\n";

            if (Name == "p" && Children == null && Text == "&nbsp;")
                return "\r\n";

            if (Name == "p" && Children == null && string.IsNullOrEmpty(Text))
                return "\r\n";

            if (Name == "br")
                sb.Append("\r\n");

            if (keepBasicFormatting && (Name == "strong" || Name == "b" || Name == "i"))
            {
                sb.Append("<");
                sb.Append(Name == "strong" ? "b" : Name);
                sb.Append(">");
            }

            if (Children != null)
            {
                foreach (var h in Children)
                {
                    string ht = h.ToText();
                    if (!string.IsNullOrEmpty(ht))
                        sb.Append(ht);
                }
            }

            if (keepBasicFormatting && (Name == "strong" || Name == "b" || Name == "i"))
            {
                sb.Append("</");
                sb.Append(Name == "strong" ? "b" : Name);
                sb.Append(">");
            }

            if (!string.IsNullOrEmpty(Text))
                sb.Append(System.Net.WebUtility.HtmlDecode(Text).Replace((char)160, (char)32));

            if (Name == "p" || Name == "div")
                sb.Append("\r\n");

            return sb.ToString();
        }

        public void Remove()
        {
            if (Parent != null && Parent.Children != null)
                Parent.Children.Remove(this);
        }

        public override string ToString()
        {
            return ToHtml(false);
        }
    }

    #region HtmlAttributes
    public class HtmlAttributeCollection : IEnumerable<KeyValuePair<string, string>>
    {
        public HtmlAttributeCollection(bool style = false)
        {
            _style = style;
            _values = new Dictionary<string, string>();
        }

        private bool _style;

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var ss in _values)
            {
                if (sb.Length > 0)
                    sb.Append(_style ? ";" : " ");

                sb.Append(ss.Key);
                sb.Append(_style ? ":" : "=\"");

                if (ss.Value != null)
                    sb.Append(ss.Value.Replace("\"", "&quot;"));

                if (!_style)
                    sb.Append("\"");
            }

            return sb.ToString();
        }

        private Dictionary<string, string> _values;

        public string this[string key]
        {
            get
            {
                string ret;
                if (_values.TryGetValue(key, out ret))
                    return ret;

                return null;
            }
            set
            {
                if (!_style && "style".Equals(key, StringComparison.InvariantCultureIgnoreCase))
                    throw new NotSupportedException("HtmlElement.Attributes['style'] is not supported. Use HtmlElement.Style or SetAttribute() instead");

                if (value == null)
                    _values.Remove(key);
                else
                    _values[key] = value;
            }
        }

        public void Merge(HtmlAttributeCollection from)
        {
            foreach (var k in from)
            {
                if (string.IsNullOrEmpty(k.Key) || string.IsNullOrEmpty(k.Value))
                    continue;

                if (!ContainsKey(k.Key))
                    this[k.Key] = k.Value;
            }
        }

        public void Remove(string key)
        {
            _values.Remove(key);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public bool Any() 
        { 
            return _values.Count > 0; 
        }

        public int Count 
        { 
            get 
            { 
                return _values.Count; 
            } 
        }

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

    }   
    #endregion
}
