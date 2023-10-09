using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using EmulatorLauncher.Common.FileFormats;

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

            foreach (var upgrade in Actions.Where(i => string.IsNullOrEmpty(i.Version) || i.Version == localVersion))
            {
                if (upgrade.DeleteActions != null)
                    foreach (var action in upgrade.DeleteActions)
                        action.Execute(rootPath);

                if (upgrade.RenameActions != null)
                    foreach (var action in upgrade.RenameActions)
                        action.Execute(rootPath);
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
                try
                {
                    if (File.Exists(newPath))
                        File.Delete(newPath);

                    File.Move(oldPath, newPath);
                }
                catch { }
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
                            try { File.Delete(file); }
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
                    File.Delete(fullPath);
            }
            catch {}
        }

        private static void TryDeleteDirectory(string fullPath)
        {
            foreach (var dir in Directory.GetDirectories(fullPath))
                TryDeleteDirectory(dir);

            foreach (var file in Directory.GetFiles(fullPath))
            {
                try { File.Delete(file); }
                catch
                {
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
            catch { }
        }
    }
}
