using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RetrobatUpdater
{
    // Keeps user es_systems_*.cfg overrides in line with the official es_systems.cfg after an update
    static class EsSystemsUpgrade
    {
        private const string EsSystemsFile = "emulationstation\\.emulationstation\\es_systems.cfg";

        // Must be called before the zip is extracted, to get the official file of the installed version
        public static XDocument LoadOfficial(string rootPath)
        {
            string path = Path.Combine(rootPath, EsSystemsFile);
            if (!File.Exists(path))
                return null;

            try { return XDocument.Load(path, LoadOptions.PreserveWhitespace); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Upgrade] Unable to read " + path + " - " + ex.Message);
                return null;
            }
        }

        // Applies the official emulator list changes to the es_systems_*.cfg overrides
        public static void Process(string rootPath, XDocument oldOfficial)
        {
            try
            {
                if (oldOfficial == null)
                    return;

                var newOfficial = LoadOfficial(rootPath);
                if (newOfficial == null)
                    return;

                var oldEmulators = GetEmulatorsBySystem(oldOfficial);
                var newEmulators = GetEmulatorsBySystem(newOfficial);

                string folder = Path.GetDirectoryName(Path.Combine(rootPath, EsSystemsFile));

                // GetFiles("*.cfg") also returns ".cfgxxx" files, check the extension
                foreach (var file in Directory.GetFiles(folder, "es_systems_*.cfg").Where(f => Path.GetExtension(f).Equals(".cfg", StringComparison.OrdinalIgnoreCase)))
                {
                    try { ProcessFile(file, oldEmulators, newEmulators); }
                    catch (Exception ex) { SimpleLogger.Instance.Warning("[Upgrade] Unable to process " + file + " - " + ex.Message); }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Upgrade] es_systems overrides not processed - " + ex.Message);
            }
        }

        private static void ProcessFile(string file, Dictionary<string, XElement> oldEmulators, Dictionary<string, XElement> newEmulators)
        {
            var doc = XDocument.Load(file, LoadOptions.PreserveWhitespace);
            if (doc.Root == null)
                return;

            string fileName = Path.GetFileName(file);

            // ES uses es_systems_custom.cfg instead of the official file: the emulator list must stay in it
            bool isMainFile = fileName.Equals("es_systems_custom.cfg", StringComparison.OrdinalIgnoreCase);
            bool changed = false;

            foreach (var system in doc.Root.Elements("system").ToList())
            {
                string name = ((string)system.Element("name") ?? "").Trim();
                var emulators = system.Element("emulators");
                if (name.Length == 0 || emulators == null)
                    continue;

                XElement oldList, newList;
                if (!oldEmulators.TryGetValue(name, out oldList) || !newEmulators.TryGetValue(name, out newList))
                    continue;

                string userKey = Canonical(emulators);
                string oldKey = Canonical(oldList);
                string newKey = Canonical(newList);

                // Already up to date, or nothing changed in the official list
                if (userKey == newKey || oldKey == newKey)
                    continue;

                // List customized by the user: official changes are applied on top of it
                if (userKey != oldKey)
                {
                    if (!emulators.HasElements)
                        continue;

                    var merged = new XElement(emulators);
                    var changes = new List<string>();

                    if (!MergeItems(merged, oldList, newList, "emulator", "", changes))
                    {
                        SimpleLogger.Instance.Warning("[Upgrade] " + fileName + " : unable to merge the custom emulator list of system '" + name + "', list kept as is.");
                        continue;
                    }

                    if (changes.Count == 0)
                        continue;

                    emulators.ReplaceWith(merged);
                    changed = true;
                    SimpleLogger.Instance.Info("[Upgrade] " + fileName + " : custom emulator list updated for system '" + name + "' (" + string.Join(", ", changes.ToArray()) + ").");
                    continue;
                }

                if (isMainFile)
                    emulators.ReplaceWith(new XElement(newList));
                else
                    RemoveElement(emulators); // ES and EmulatorLauncher fall back to the official list

                changed = true;
                SimpleLogger.Instance.Info("[Upgrade] " + fileName + " : official emulator list restored for system '" + name + "'.");
            }

            if (!changed)
                return;

            File.Copy(file, file + ".backup", true);

            var settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = doc.Declaration == null,
                Encoding = new UTF8Encoding(false)
            };

            using (var writer = XmlWriter.Create(file, settings))
                doc.Save(writer);
        }

        private static Dictionary<string, XElement> GetEmulatorsBySystem(XDocument doc)
        {
            // Same case-sensitive name matching as ES
            var ret = new Dictionary<string, XElement>(StringComparer.Ordinal);
            if (doc.Root == null)
                return ret;

            foreach (var system in doc.Root.Elements("system"))
            {
                string name = ((string)system.Element("name") ?? "").Trim();
                var emulators = system.Element("emulators");
                if (name.Length > 0 && emulators != null && !ret.ContainsKey(name))
                    ret[name] = emulators;
            }

            return ret;
        }

        // Three-way merge of an emulator or core list: user order, additions and removals are kept,
        // official additions, removals and attribute changes are applied
        private static bool MergeItems(XElement user, XElement oldList, XElement newList, string itemName, string parent, List<string> changes)
        {
            var userItems = GetItems(user, itemName);
            var oldItems = GetItems(oldList, itemName);
            var newItems = GetItems(newList, itemName);
            if (userItems == null || oldItems == null || newItems == null)
                return false; // duplicated or unnamed items

            bool isCore = itemName == "core";
            string userDefault = isCore ? GetDefault(user) : null;
            string oldDefault = isCore ? GetDefault(oldList) : null;

            // Removed from the official list
            foreach (var key in userItems.Keys.Where(k => oldItems.ContainsKey(k) && !newItems.ContainsKey(k)).ToList())
            {
                RemoveElement(userItems[key]);
                userItems.Remove(key);
                changes.Add("-" + parent + key);
            }

            // Still official: attribute changes, then cores
            foreach (var key in userItems.Keys.Where(k => oldItems.ContainsKey(k) && newItems.ContainsKey(k)).ToList())
            {
                var item = userItems[key];

                if (MergeAttributes(item, oldItems[key], newItems[key]))
                    changes.Add("*" + parent + key);

                if (isCore)
                    continue;

                var userCores = item.Element("cores");
                var oldCores = oldItems[key].Element("cores");
                var newCores = newItems[key].Element("cores");

                if (userCores != null && oldCores != null && newCores != null)
                {
                    if (!MergeItems(userCores, oldCores, newCores, "core", key + "/", changes))
                        return false;
                }
                else if (userCores == null && oldCores == null && newCores != null)
                {
                    item.Add(new XElement(newCores));
                    changes.Add("+" + key + "/cores");
                }
            }

            // Added to the official list: after the same previous item, or at the end to keep the user's default
            string previous = null;
            foreach (var newItem in newList.Elements(itemName))
            {
                string key = GetKey(newItem);

                if (!oldItems.ContainsKey(key) && !userItems.ContainsKey(key))
                {
                    var copy = new XElement(newItem);
                    InsertAfter(user, previous != null ? userItems[previous] : user.Elements(itemName).LastOrDefault(), copy);
                    userItems[key] = copy;
                    changes.Add("+" + parent + key);
                }

                if (userItems.ContainsKey(key))
                    previous = key;
            }

            // Default core chosen by the user wins over the official one
            if (isCore && userDefault != oldDefault)
            {
                foreach (var core in user.Elements(itemName))
                {
                    bool isDefault = GetKey(core) == userDefault;
                    if (isDefault && !IsDefault(core))
                        core.SetAttributeValue("default", "true");
                    else if (!isDefault && core.Attribute("default") != null)
                        core.Attribute("default").Remove();
                }
            }

            return true;
        }

        // An attribute left as in the old official list takes the new official value
        private static bool MergeAttributes(XElement user, XElement oldItem, XElement newItem)
        {
            bool changed = false;

            var names = oldItem.Attributes().Select(a => a.Name).Union(newItem.Attributes().Select(a => a.Name)).ToList();
            foreach (var attrName in names)
            {
                string userValue = (string)user.Attribute(attrName);
                string oldValue = (string)oldItem.Attribute(attrName);
                string newValue = (string)newItem.Attribute(attrName);

                if (userValue == oldValue && oldValue != newValue)
                {
                    user.SetAttributeValue(attrName, newValue);
                    changed = true;
                }
            }

            return changed;
        }

        // Items by key (emulator name or core name), null if a key is empty or duplicated
        private static Dictionary<string, XElement> GetItems(XElement list, string itemName)
        {
            var ret = new Dictionary<string, XElement>(StringComparer.Ordinal);

            foreach (var item in list.Elements(itemName))
            {
                string key = GetKey(item);
                if (key.Length == 0 || ret.ContainsKey(key))
                    return null;

                ret[key] = item;
            }

            return ret;
        }

        private static string GetKey(XElement item)
        {
            if (item.Name.LocalName == "core")
                return item.Value.Trim();

            return ((string)item.Attribute("name") ?? "").Trim();
        }

        private static string GetDefault(XElement cores)
        {
            var core = cores.Elements("core").FirstOrDefault(IsDefault);
            return core == null ? null : GetKey(core);
        }

        private static bool IsDefault(XElement core)
        {
            return string.Equals((string)core.Attribute("default"), "true", StringComparison.OrdinalIgnoreCase);
        }

        // Inserts the element after a sibling, with the same indentation
        private static void InsertAfter(XElement parent, XElement after, XElement element)
        {
            if (after == null)
            {
                parent.Add(element);
                return;
            }

            var indent = after.PreviousNode as XText;
            if (indent != null && string.IsNullOrWhiteSpace(indent.Value))
                after.AddAfterSelf(new XText(indent.Value), element);
            else
                after.AddAfterSelf(element);
        }

        // Comparable form of an element: ignores whitespace, comments and attribute order
        private static string Canonical(XElement element)
        {
            var sb = new StringBuilder();
            sb.Append("<" + element.Name.LocalName);

            foreach (var attr in element.Attributes().OrderBy(a => a.Name.LocalName, StringComparer.Ordinal))
                sb.Append(" " + attr.Name.LocalName + "=\"" + attr.Value.Trim() + "\"");

            sb.Append(">");

            if (element.HasElements)
            {
                foreach (var child in element.Elements())
                    sb.Append(Canonical(child));
            }
            else
                sb.Append(element.Value.Trim());

            sb.Append("</" + element.Name.LocalName + ">");
            return sb.ToString();
        }

        // Removes the element and its indentation
        private static void RemoveElement(XElement element)
        {
            var indent = element.PreviousNode as XText;
            if (indent != null && string.IsNullOrWhiteSpace(indent.Value))
                indent.Remove();

            element.Remove();
        }
    }
}