using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace RetrobatUpdater
{
    /* Sample file :
    <?xml version="1.0" encoding="UTF-8"?>
    <upgradeinfo>
      <neveroverwrite path="retrobat.ini"/>
      <neveroverwrite path="emulationstation\.emulationstation\es_settings.cfg"/>
      <neveroverwrite path="emulationstation\.emulationstation\es_input.cfg"/>
      <actions version="4.0.2"> <!-- startswith -->
        <rename path="emulators\pcsx2" to="emulators\pcsx2-16"/>
        <delete path="system\modules\rb_gui\*.dll"/>
        <delete path="system\modules\rb_gui\images\*.png"/>
        <delete path="BatGui.exe"/>
      </actions>
    </upgradeinfo>
    */
    [XmlType("upgradeinfo")]
    [XmlRoot("upgradeinfo")]
    public partial class UpgradeInformationFile
    {
        public static UpgradeInformationFile FromXml(string fileName)
        {
            try
            {
                return XmlExtensions.FromXml<UpgradeInformationFile>(fileName);
            }
            catch 
            {
                return null;
            }
        }

        [XmlElement("actions")]
        public Upgrade[] Actions { get; set; }

        [XmlElement("neveroverwrite")]
        public NeverOverride[] NeverOverrides { get; set; }

        public void Process(string rootPath, string localVersion)
        {
            if (Actions == null)
                return;

            SimpleLogger.Instance.Info("[INFO] Performing upgrade actions.");

            foreach (var upgrade in Actions.Where(i => string.IsNullOrEmpty(i.Version) || i.Version == localVersion))
            {
                if (upgrade.Version == null)
                    SimpleLogger.Instance.Info("[INFO] Performing general upgrade actions.");
                else
                    SimpleLogger.Instance.Info("[INFO] Performing upgrade actions specific to source version: " + upgrade.Version);

                if (upgrade.MoveFirstActions != null)
                    foreach (var action in upgrade.MoveFirstActions)
                    {
                        SimpleLogger.Instance.Info("[UPGRADE ACTION] Preparing to delete: " + action.path);
                        action.Execute(rootPath);
                    }

                if (upgrade.DeleteActions != null)
                    foreach (var action in upgrade.DeleteActions)
                    {
                        SimpleLogger.Instance.Info("[UPGRADE ACTION] Preparing to delete: " + action.path);
                        action.Execute(rootPath);
                    }

                if (upgrade.RenameActions != null)
                    foreach (var action in upgrade.RenameActions)
                    {
                        SimpleLogger.Instance.Info("[UPGRADE ACTION] Preparing to rename: " + action.path + " to " + action.to);
                        action.Execute(rootPath);
                    }

                if (upgrade.MoveActions != null)
                    foreach (var action in upgrade.MoveActions)
                    {
                        SimpleLogger.Instance.Info("[UPGRADE ACTION] Preparing to move: " + action.path + " to " + action.to);
                        action.Execute(rootPath);
                    }

                if (upgrade.MergeDirectoryActions != null)
                    foreach (var action in upgrade.MergeDirectoryActions)
                    {
                        SimpleLogger.Instance.Info("[UPGRADE ACTION] Preparing to merge: " + action.path + " with " + action.to);
                        action.Execute(rootPath);
                    }

                if (upgrade.MergeGamelistActions != null)
                    foreach (var action in upgrade.MergeGamelistActions)
                    {
                        SimpleLogger.Instance.Info("[UPGRADE ACTION] Preparing to merge rom folder: " + action.path + " into " + action.to);
                        action.Execute(rootPath);
                    }
            }
        }

        public bool IsOverridable(string fileName)
        {
            if (fileName == null || NeverOverrides == null)
                return true;

            fileName = NeverOverride.FormatComparable(fileName);
            return !NeverOverrides.Any(o => o.IsOverridable(fileName));
        }
    }
    
    public partial class NeverOverride
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        public static string FormatComparable(string name)
        {
            var ret = (name ?? "").Replace("\\", "/").ToLowerInvariant();

            if (ret.Contains("*"))
                ret = ret.Replace("(", "").Replace(")", "").Replace("^", "");

            return ret;
        }

        public bool IsOverridable(string fileName)
        {
            if (NormalizedPath == fileName)
                return true;

            if (_regEx != null)
            {
                bool isMatch = _regEx.IsMatch(fileName);
                return isMatch;
            }

            return false;
        }

        [XmlIgnore]
        public string NormalizedPath
        {
            get
            {
                if (_normalizedPath == null)
                {
                    _normalizedPath = NeverOverride.FormatComparable(path);

                    try
                    {
                        if (path != null && path.Contains("*"))
                            _regEx = new System.Text.RegularExpressions.Regex("^(" + _normalizedPath + ")");
                    }
                    catch { }
                }

                return _normalizedPath;
            }
        }

        private string _normalizedPath;
        private System.Text.RegularExpressions.Regex _regEx;
    }


    public partial class Upgrade
    {
        [XmlElement("rename")]
        public RenameAction[] RenameActions { get; set; }

        [XmlElement("delete")]
        public DeleteAction[] DeleteActions { get; set; }

        [XmlElement("move")]
        public MoveAction[] MoveActions { get; set; }

        [XmlElement("movefirst")]
        public MoveFirstAction[] MoveFirstActions { get; set; }

        [XmlElement("merge")]
        public MergeDirectoryAction[] MergeDirectoryActions { get; set; }

        [XmlElement("mergegamelist")]
        public MergeGamelistAction[] MergeGamelistActions { get; set; }

        [XmlAttribute("version")]
        public string Version { get; set; }
    }

    public partial class RenameAction
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        [XmlAttribute("to")]
        public string to { get; set; }

        public void Execute(string root)
        {
            string oldPath = Path.Combine(root, path);
            string newPath = Path.Combine(root, to);

            if (File.Exists(oldPath))
            {
                SimpleLogger.Instance.Info("[Upgrade] Renaming file " + oldPath + " to " + newPath);
                try
                {
                    if (File.Exists(newPath))
                    {
                        try 
                        { 
                            File.Move(newPath, newPath + ".old");
                            SimpleLogger.Instance.Info("[Upgrade] Target file " + newPath + " exists, renaming it to .old");
                        }
                        catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename existing file: " + newPath + " to .old"); }
                    }

                    try
                    {
                        File.Move(oldPath, newPath);
                        SimpleLogger.Instance.Info("[Upgrade] Renamed " + oldPath + " to " + newPath);
                    }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename: " + oldPath + " to " + newPath); }
                    
                }
                catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename: " + oldPath + " to " + newPath); }
            }

            if (Directory.Exists(oldPath))
            {
                if (Directory.Exists(newPath))
                {
                    try { Directory.Move(newPath, newPath + ".old"); }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename existing folder: " + newPath); }
                }
                if (!Directory.Exists(newPath))
                {
                    string parentPath = Path.GetDirectoryName(newPath);
                    try { Directory.CreateDirectory(parentPath); }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to create target directory parent: " + parentPath); }
                    try
                    {
                        Directory.Move(oldPath, newPath);
                        SimpleLogger.Instance.Info("[Upgrade] Renamed " + oldPath + " to " + newPath);
                    }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename: " + oldPath + " to " + newPath); }
                }
            }
        }
    }

    public partial class DeleteAction
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        public void Execute(string root)
        {
            string fullPath = Path.Combine(root, path);

            if (Path.GetFileName(fullPath).Contains("*"))
            {
                if (Directory.Exists(Path.GetDirectoryName(fullPath)))
                {
                    try
                    {
                        var files = Directory.GetFiles(Path.GetDirectoryName(fullPath), Path.GetFileName(fullPath));
                        foreach (var file in files)
                        {
                            try 
                            { 
                                File.Delete(file);
                                SimpleLogger.Instance.Info("[Upgrade] Deleted " + file);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                return;
            }

            try
            {
                if (Directory.Exists(fullPath))
                    TryDeleteDirectory(fullPath);
                else if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    SimpleLogger.Instance.Info("[Upgrade] Deled " + fullPath);
                }
            }
            catch {}
        }

        private static void TryDeleteDirectory(string fullPath)
        {
            foreach (var dir in Directory.GetDirectories(fullPath))
                TryDeleteDirectory(dir);

            foreach (var file in Directory.GetFiles(fullPath))
            {
                try 
                { 
                    File.Delete(file);
                    SimpleLogger.Instance.Info("[Upgrade] Deled " + file);
                }
                catch
                {
                    SimpleLogger.Instance.Warning("[Upgrade] Unable to delete file: " + fullPath + " , trying with 'move'");
                    try
                    {
                        string newName = Path.Combine(Path.GetDirectoryName(file), "__" + Path.GetFileName(file) + "__");
                        if (File.Exists(newName))
                            File.Delete(newName);

                        File.Move(file, newName);
                    }
                    catch { }
                }
            }

            try { Directory.Delete(fullPath); }
            catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to delete directory: " + fullPath); }
        }
    }

    public partial class MoveAction
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        [XmlAttribute("to")]
        public string to { get; set; }

        public void Execute(string root)
        {
            string oldPath = Path.Combine(root, path);
            string newPath = Path.Combine(root, to);

            if (File.Exists(oldPath))
            {
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(newPath)))
                        try { Directory.CreateDirectory(Path.GetDirectoryName(newPath)); }
                        catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to create target directory: " + newPath); }

                    if (File.Exists(newPath))
                    {
                        try
                        {
                            File.Move(newPath, newPath + ".old");
                            SimpleLogger.Instance.Info("[Upgrade] Renamed " + newPath + " to " + newPath + ".old");
                        }
                        catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename existing file: " + newPath + " to .old"); }
                    }

                    try
                    {
                        File.Move(oldPath, newPath);
                        SimpleLogger.Instance.Info("[Upgrade] Moved " + oldPath + " to " + newPath);
                    }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath); }
                }
                catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath); }
            }
            else
                SimpleLogger.Instance.Warning("[Upgrade] Source file does not exist, checking folder.");

            if (Directory.Exists(oldPath))
            {
                if (Directory.Exists(newPath))
                {
                    SimpleLogger.Instance.Info("[Upgrade] Target directory exists, renaming to .old.");
                    try { Directory.Move(newPath, newPath + ".old"); }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename existing folder: " + newPath); }
                }
                if (!Directory.Exists(newPath))
                {
                    string parentPath = Path.GetDirectoryName(newPath);
                    try { Directory.CreateDirectory(parentPath); }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to create target directory parent: " + parentPath); }

                    try
                    {
                        Directory.Move(oldPath, newPath);
                        SimpleLogger.Instance.Info("[Upgrade] Moved " + oldPath + " to " + newPath);
                    }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath); }
                }
                else
                    SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath);
            }
            else
            {
                SimpleLogger.Instance.Warning("[Upgrade] Source folder does not exist.");
            }
        }
    }

    public partial class MoveFirstAction
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        [XmlAttribute("to")]
        public string to { get; set; }

        public void Execute(string root)
        {
            string oldPath = Path.Combine(root, path);
            string newPath = Path.Combine(root, to);

            if (File.Exists(oldPath))
            {
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(newPath)))
                        try { Directory.CreateDirectory(Path.GetDirectoryName(newPath)); }
                        catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to create target directory: " + newPath); }

                    if (File.Exists(newPath))
                    {
                        try
                        {
                            File.Move(newPath, newPath + ".old");
                            SimpleLogger.Instance.Info("[Upgrade] Renamed " + newPath + " to " + newPath + ".old");
                        }
                        catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename existing file: " + newPath + " to .old"); }
                    }

                    try
                    {
                        File.Move(oldPath, newPath);
                        SimpleLogger.Instance.Info("[Upgrade] Moved " + oldPath + " to " + newPath);
                    }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath); }
                }
                catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath); }
            }
            else
                SimpleLogger.Instance.Warning("[Upgrade] Source file does not exist, checking folder.");

            if (Directory.Exists(oldPath))
            {
                if (Directory.Exists(newPath))
                {
                    SimpleLogger.Instance.Info("[Upgrade] Target directory exists, renaming to .old.");
                    try { Directory.Move(newPath, newPath + ".old"); }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to rename existing folder: " + newPath); }
                }
                if (!Directory.Exists(newPath))
                {
                    string parentPath = Path.GetDirectoryName(newPath);
                    try { Directory.CreateDirectory(parentPath); }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to create target directory parent: " + parentPath); }

                    try
                    {
                        Directory.Move(oldPath, newPath);
                        SimpleLogger.Instance.Info("[Upgrade] Moved " + oldPath + " to " + newPath);
                    }
                    catch { SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath); }
                }
                else
                    SimpleLogger.Instance.Warning("[Upgrade] Unable to move: " + oldPath + " to " + newPath);
            }
            else
            {
                SimpleLogger.Instance.Warning("[Upgrade] Source folder does not exist.");
            }
        }
    }

    public partial class MergeDirectoryAction
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        [XmlAttribute("to")]
        public string to { get; set; }

        public void Execute(string root)
        {
            string oldPath = Path.Combine(root, path);
            string newPath = Path.Combine(root, to);

            if (!Directory.Exists(oldPath))
            {
                SimpleLogger.Instance.Warning("[Upgrade] Source folder does not exist: " + oldPath);
                return;
            }

            MergeDirectoriesKeepNewest(oldPath, newPath);
            SimpleLogger.Instance.Info("[Upgrade] Merged " + oldPath + " to " + newPath);
        }

        private void MergeDirectoriesKeepNewest(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            // Merge files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));

                if (File.Exists(destFile))
                {
                    DateTime sourceTime = File.GetLastWriteTimeUtc(file);
                    DateTime destTime = File.GetLastWriteTimeUtc(destFile);

                    if (sourceTime > destTime)
                    {
                        File.Copy(file, destFile, true);
                        SimpleLogger.Instance.Info("[Upgrade] Overwrote file: " + destFile);
                    }
                }
                else
                {
                    File.Copy(file, destFile);
                }
            }

            // Merge subdirectories recursively
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(directory));
                MergeDirectoriesKeepNewest(directory, destSubDir);
            }
        }
    }

    public partial class MergeGamelistAction
    {
        [XmlAttribute("path")]
        public string path { get; set; }

        [XmlAttribute("to")]
        public string to { get; set; }

        private const string GamelistFile = "gamelist.xml";

        /// <summary>
        /// Merges a rom folder into another one : moves files (renaming them on name conflict), merges gamelist.xml entries,
        /// then deletes the source folder once everything has been moved.
        /// Safe to run several times : does nothing when the source folder no longer exists.
        /// </summary>
        public void Execute(string root)
        {
            string sourceDir = Path.GetFullPath(Path.Combine(root, path)).TrimEnd('\\', '/');
            string destDir = Path.GetFullPath(Path.Combine(root, to)).TrimEnd('\\', '/');

            if (!Directory.Exists(sourceDir))
            {
                SimpleLogger.Instance.Info("[Upgrade] Source folder does not exist, nothing to merge: " + sourceDir);
                return;
            }

            if (string.Equals(sourceDir, destDir, StringComparison.OrdinalIgnoreCase))
                return;

            try { Directory.CreateDirectory(destDir); }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Upgrade] Unable to create target directory: " + destDir + " - " + ex.Message);
                return;
            }

            // Source relative path ('/' separated) -> new relative path, for files renamed because of a name conflict
            var renamed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool filesMoved = MoveFiles(sourceDir, destDir, sourceDir, renamed);
            bool gamelistMerged = MergeGamelists(sourceDir, destDir, renamed);

            if (filesMoved && gamelistMerged)
                DeleteSourceFolder(sourceDir);
            else
                SimpleLogger.Instance.Warning("[Upgrade] Merge incomplete, source folder kept: " + sourceDir);

            SimpleLogger.Instance.Info("[Upgrade] Merged " + sourceDir + " into " + destDir);
        }

        private static bool MoveFiles(string sourceRoot, string destRoot, string currentDir, Dictionary<string, string> renamed)
        {
            bool ok = true;

            foreach (var file in Directory.GetFiles(currentDir))
            {
                string rel = GetRelativePath(sourceRoot, file);

                // Source gamelist is merged separately
                if (rel.Equals(GamelistFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                string target = Path.Combine(destRoot, rel);

                try
                {
                    string targetDir = Path.GetDirectoryName(target);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    if (File.Exists(target))
                    {
                        // Same file already in target : nothing to keep
                        if (FilesAreEqual(file, target))
                        {
                            File.Delete(file);
                            continue;
                        }

                        target = GetUniqueFileName(target);
                        renamed[rel.Replace('\\', '/')] = GetRelativePath(destRoot, target).Replace('\\', '/');
                        SimpleLogger.Instance.Info("[Upgrade] Name conflict, " + file + " renamed to " + target);
                    }

                    MoveFile(file, target);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[Upgrade] Unable to move " + file + " to " + target + " - " + ex.Message);
                    ok = false;
                }
            }

            foreach (var dir in Directory.GetDirectories(currentDir))
                ok = MoveFiles(sourceRoot, destRoot, dir, renamed) && ok;

            return ok;
        }

        private static bool MergeGamelists(string sourceDir, string destDir, Dictionary<string, string> renamed)
        {
            string sourceGamelist = Path.Combine(sourceDir, GamelistFile);
            if (!File.Exists(sourceGamelist))
                return true;

            string destGamelist = Path.Combine(destDir, GamelistFile);

            try
            {
                XDocument src = XDocument.Load(sourceGamelist);
                if (src.Root == null)
                    return true;

                XDocument dst = null;
                if (File.Exists(destGamelist))
                {
                    try { dst = XDocument.Load(destGamelist); }
                    catch (Exception ex)
                    {
                        // Never overwrite a gamelist that could not be read
                        SimpleLogger.Instance.Warning("[Upgrade] Unable to read " + destGamelist + " - " + ex.Message);
                        return false;
                    }
                }

                if (dst == null || dst.Root == null)
                    dst = new XDocument(new XElement("gameList"));

                // Existing entries indexed by normalized path
                var existing = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in dst.Root.Elements())
                {
                    string key = GetEntryKey(entry, destDir);
                    if (key != null && !existing.ContainsKey(key))
                        existing[key] = entry;
                }

                int added = 0;
                int completed = 0;

                foreach (var entry in src.Root.Elements())
                {
                    if (entry.Name.LocalName != "game" && entry.Name.LocalName != "folder")
                        continue;

                    var copy = new XElement(entry);

                    // Rebase paths (rom, image, video, manual...) on the target folder
                    foreach (var child in copy.Elements().Where(e => !e.HasElements))
                        child.Value = RemapPath(child.Value, sourceDir, renamed);

                    string key = GetEntryKey(copy, destDir);
                    if (key == null)
                        continue;

                    XElement current;
                    if (existing.TryGetValue(key, out current))
                    {
                        // Keep target metadata, only add what is missing
                        foreach (var child in copy.Elements())
                        {
                            if (current.Element(child.Name) == null)
                            {
                                current.Add(new XElement(child));
                                completed++;
                            }
                        }
                        continue;
                    }

                    dst.Root.Add(copy);
                    existing[key] = copy;
                    added++;
                }

                if (File.Exists(destGamelist) && !File.Exists(destGamelist + ".backup"))
                    File.Copy(destGamelist, destGamelist + ".backup");

                var settings = new System.Xml.XmlWriterSettings()
                {
                    Indent = true,
                    IndentChars = "\t",
                    Encoding = new UTF8Encoding(false)
                };

                using (var writer = System.Xml.XmlWriter.Create(destGamelist, settings))
                    dst.Save(writer);

                SimpleLogger.Instance.Info("[Upgrade] Gamelist merged into " + destGamelist + " : " + added + " entries added, " + completed + " fields completed.");
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Upgrade] Unable to merge " + sourceGamelist + " - " + ex.Message);
                return false;
            }
        }

        private static string GetEntryKey(XElement entry, string baseDir)
        {
            var pathElement = entry.Element("path");
            if (pathElement == null || string.IsNullOrWhiteSpace(pathElement.Value))
                return null;

            string value = pathElement.Value.Trim().Replace('\\', '/');

            if (value.StartsWith("./"))
                return value.Substring(2).TrimEnd('/');

            try
            {
                if (Path.IsPathRooted(value))
                {
                    string full = Path.GetFullPath(value);
                    if (full.StartsWith(baseDir + "\\", StringComparison.OrdinalIgnoreCase))
                        return full.Substring(baseDir.Length + 1).Replace('\\', '/').TrimEnd('/');
                }
            }
            catch { }

            return value.TrimEnd('/');
        }

        private static string RemapPath(string value, string sourceDir, Dictionary<string, string> renamed)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            string rel = null;

            try
            {
                string v = value.Trim();

                if (v.StartsWith("./") || v.StartsWith(".\\"))
                    rel = v.Substring(2);
                else if (Path.IsPathRooted(v))
                {
                    string full = Path.GetFullPath(v);
                    if (full.StartsWith(sourceDir + "\\", StringComparison.OrdinalIgnoreCase))
                        rel = full.Substring(sourceDir.Length + 1);
                }
            }
            catch { }

            // Not a path inside the source folder (description, date, url...)
            if (rel == null)
                return value;

            rel = rel.Replace('\\', '/');

            string newRel;
            if (renamed.TryGetValue(rel, out newRel))
                rel = newRel;

            return "./" + rel;
        }

        private static void DeleteSourceFolder(string sourceDir)
        {
            try
            {
                // Only the merged gamelist and empty folders should remain
                var remaining = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                    .Where(f => !string.Equals(f, Path.Combine(sourceDir, GamelistFile), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (remaining.Count > 0)
                {
                    SimpleLogger.Instance.Warning("[Upgrade] Files remaining, source folder kept: " + sourceDir);
                    return;
                }

                Directory.Delete(sourceDir, true);
                SimpleLogger.Instance.Info("[Upgrade] Deleted merged folder " + sourceDir);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Upgrade] Unable to delete merged folder " + sourceDir + " - " + ex.Message);
            }
        }

        private static string GetRelativePath(string root, string file)
        {
            return file.Substring(root.Length).TrimStart('\\', '/');
        }

        private static string GetUniqueFileName(string file)
        {
            string dir = Path.GetDirectoryName(file);
            string name = Path.GetFileNameWithoutExtension(file);
            string ext = Path.GetExtension(file);

            int i = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                i++;
            }
            while (File.Exists(candidate) || Directory.Exists(candidate));

            return candidate;
        }

        private static void MoveFile(string source, string target)
        {
            try
            {
                File.Move(source, target);
            }
            catch (IOException)
            {
                // Different volume (e.g. junction) or locked : copy then delete
                File.Copy(source, target, false);
                File.Delete(source);
            }
        }

        private static bool FilesAreEqual(string file1, string file2)
        {
            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);
            if (info1.Length != info2.Length)
                return false;

            const int bufferSize = 1024 * 64;
            using (var fs1 = File.OpenRead(file1))
            using (var fs2 = File.OpenRead(file2))
            {
                var buffer1 = new byte[bufferSize];
                var buffer2 = new byte[bufferSize];

                while (true)
                {
                    int read1 = ReadBlock(fs1, buffer1);
                    int read2 = ReadBlock(fs2, buffer2);

                    if (read1 != read2)
                        return false;

                    if (read1 == 0)
                        return true;

                    for (int i = 0; i < read1; i++)
                        if (buffer1[i] != buffer2[i])
                            return false;
                }
            }
        }

        private static int ReadBlock(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;

                total += read;
            }

            return total;
        }
    }
}
