using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.Drawing;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Common.Lightguns
{
    public class SindenLightgunConfiguration
    {
        public bool ShowPrimaryBorder
        {
            get
            {
                if (_conf != null)
                {
                    var custom = _conf["controllers.guns.bordersmode"];
                    if (custom == "hidden")
                        return false;
                }

                return (this["chkShowPrimaryBorder"] ?? "1").ToInteger() != 0;
            }
        }

        public bool ShowSecondaryBorder
        {
            get
            {
                if (_conf != null)
                {
                    var custom = _conf["controllers.guns.bordersmode"];
                    if (custom == "hidden")
                        return false;
                }

                return (this["chkShowSecondaryBorder"] ?? "0").ToInteger() != 0;
            }
        }

        public float PrimaryBorderWidth
        {
            get
            {
                if (_conf != null)
                {
                    var custom = _conf["controllers.guns.borderssize"];
                    if (custom == "big" || custom == "medium")
                        return 2.0f;
                    if (custom == "thin")
                        return 1.0f;
                }

                return (this["txtPrimaryBorderWidth"] ?? "2").ToFloat();
            }
        }

        public float SecondaryBorderWidth
        {
            get
            {
                if (_conf != null)
                {
                    var custom = _conf["controllers.guns.borderssize"];
                    if (custom == "big")
                        return 1.0f;
                    if (custom == "thin" || custom == "medium")
                        return 0.0f;
                }

                return (this["txtSecondaryBorderWidth"] ?? "0").ToFloat();
            }
        }

        public Color PrimaryColor
        {
            get
            {
                if (_conf != null)
                {
                    var custom = _conf["controllers.guns.borderscolor"];
                    if (custom == "white")
                        return Color.White;
                    if (custom == "red")
                        return Color.FromArgb(255, 0, 0);
                    if (custom == "green")
                        return Color.FromArgb(0, 255, 0);
                    if (custom == "blue")
                        return Color.FromArgb(0, 0, 255);
                }

                return Color.FromArgb(
                    (this["txtColorPrimaryR"] ?? "255").ToInteger(),
                    (this["txtColorPrimaryG"] ?? "255").ToInteger(),
                    (this["txtColorPrimaryB"] ?? "255").ToInteger());
            }
        }

        public Color SecondaryColor
        {
            get
            {
                if (_conf != null)
                {
                    var custom = _conf["controllers.guns.borderscolor"];
                    if (!string.IsNullOrEmpty(custom))
                        return Color.Black;
                }

                return Color.FromArgb(
                    (this["txtColorSecondaryR"] ?? "255").ToInteger(),
                    (this["txtColorSecondaryG"] ?? "255").ToInteger(),
                    (this["txtColorSecondaryB"] ?? "255").ToInteger());
            }
        }

        private ConfigFile _conf;

        public static SindenLightgunConfiguration GetConfiguration(ConfigFile conf)
        {
            try
            {
                var px = System.Diagnostics.Process.GetProcessesByName("Lightgun").FirstOrDefault();
                if (px != null)
                {
                    var cmd = px.GetProcessCommandline().SplitCommandLine().FirstOrDefault();
                    if (cmd != null)
                    {
                        cmd = cmd.Replace("\"", "") + ".config";
                        if (File.Exists(cmd))
                        {
                            var ret = new SindenLightgunConfiguration(conf);
                            ret._docPath = cmd;
                            ret._doc = XDocument.Load(cmd);

                            foreach (var element in ret._doc.Descendants("appSettings").Elements("add"))
                            {
                                var key = element.Attribute("key");
                                var value = element.Attribute("value");

                                if (key != null && value != null)
                                    ret._values[key.Value] = value.Value;
                            }

                            return ret;
                        }
                        else
                            SimpleLogger.Instance.Error("[SindenLightgunConfiguration] Lightgun.exe.config not found");
                    }
                    else
                        SimpleLogger.Instance.Error("[SindenLightgunConfiguration] Can't parse command line");
                }
                else
                    SimpleLogger.Instance.Error("[SindenLightgunConfiguration] Lightgun.exe not found");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[SindenLightgunConfiguration] Can't load configuration", ex);
            }

            return new SindenLightgunConfiguration(conf);
        }

        private SindenLightgunConfiguration(ConfigFile conf) { _conf = conf; }

        private Dictionary<string, string> _values = new Dictionary<string, string>();
        private XDocument _doc;
        private string _docPath;
        private bool _isDirty;

        public bool IsEditable { get { return _doc != null; } }

        public string this[string key]
        {
            get
            {
                string value;
                if (!_values.TryGetValue(key, out value))
                    return null;

                return value;
            }
            set
            {
                if (!IsEditable)
                    return;

                foreach (var element in _doc.Descendants("appSettings").Elements("add"))
                {
                    var kk = element.Attribute("key");
                    if (kk != null && kk.Value == key)
                    {
                        _values[key] = value == null ? "" : value.ToString();
                        element.SetAttributeValue(key, value);
                        _isDirty = true;
                        break;
                    }
                }
            }
        }

        public void Save()
        {
            if (_doc != null && _isDirty)
                _doc.Save(_docPath);
        }
    }

}
