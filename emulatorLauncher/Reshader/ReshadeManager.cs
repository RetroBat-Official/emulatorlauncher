using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.IO.Compression;
using System.ComponentModel;
using System.Reflection;

namespace emulatorLauncher
{
    class ReshadeManager
    {
        // -system model2 -emulator model2 -core multicpu -rom "H:\[Emulz]\roms\model2\dayton93.zip"
        // -system model3 -emulator supermodel -core  -rom "H:\[Emulz]\roms\model3\srally2.zip"
        public static bool Setup(ReshadeBezelType type, ReshadePlatform platform, string system, string rom, string path, ScreenResolution resolution)
        {
            FileInfo fileInfo = null;

            string dllName = Path.Combine(path, GetEnumDescription(type));

            fileInfo = new FileInfo(dllName);

            // Install reshader if not installed
            if (fileInfo == null || !fileInfo.Exists)
            {
                if (platform == ReshadePlatform.x86)
                    GZipBytesToFile(Properties.Resources.reshader_x86_gz, Path.Combine(path, dllName));
                else
                    GZipBytesToFile(Properties.Resources.reshader_x64_gz, Path.Combine(path, dllName));

                fileInfo = new FileInfo(dllName);
            }

            if (fileInfo == null || !fileInfo.Exists)
                return false;

            FileVersionInfo version = FileVersionInfo.GetVersionInfo(fileInfo.FullName);

            bool oldVersion = new Version(version.ProductMajorPart, version.ProductMinorPart) <= new Version(4, 6);

            var knownTechniques = LoadKnownTechniques(oldVersion);

            if (!File.Exists(Path.Combine(path, "ReShade.ini")))
                File.WriteAllText(Path.Combine(path, "ReShade.ini"), Properties.Resources.ReShadeIni);
            
            var bezel = BezelFiles.GetBezelFiles(system, rom, resolution);

            using (IniFile reShadeIni = new IniFile(Path.Combine(path, "ReShade.ini")))
            {
                var effectSearchPaths = reShadeIni.GetValue("GENERAL", "EffectSearchPaths");
                if (effectSearchPaths != null)
                    effectSearchPaths = effectSearchPaths.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                if (effectSearchPaths != null && effectSearchPaths.StartsWith(".\\"))
                    effectSearchPaths = path + effectSearchPaths.Substring(1);

                Directory.CreateDirectory(effectSearchPaths);

                if (!File.Exists(Path.Combine(effectSearchPaths, "ReShade.fxh")))
                    File.WriteAllBytes(Path.Combine(effectSearchPaths, "ReShade.fxh"), Properties.Resources.ReShade);

                if (!File.Exists(Path.Combine(effectSearchPaths, "ReShadeUI.fxh")))
                    File.WriteAllBytes(Path.Combine(effectSearchPaths, "ReShadeUI.fxh"), Properties.Resources.ReShadeUI);

                if (!string.IsNullOrEmpty(Program.AppConfig["screenshots"]))
                    reShadeIni.WriteValue("SCREENSHOTS", "SavePath", Program.AppConfig.GetFullPath("screenshots"));

                var presetPath = oldVersion ? reShadeIni.GetValue("GENERAL", "PresetFiles") : reShadeIni.GetValue("GENERAL", "PresetPath");
                if (presetPath != null)
                    presetPath = presetPath.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                if (presetPath != null && presetPath.StartsWith(".\\"))
                    presetPath = path + presetPath.Substring(1);
                if (presetPath == null)
                    presetPath = "ReShadePreset.ini";

                using (IniFile reShadePreset = new IniFile(Path.Combine(path, presetPath)))
                {                 
                    string bezelEffectName = knownTechniques[0];
                    string shaderName = Program.SystemConfig["shader"]??"";
                    string shaderFileName = null;

                    int split = shaderName.IndexOf("@");
                    if (split >= 0)
                    {
                        shaderFileName = shaderName.Substring(split+1);

                        if (oldVersion)
                            shaderName = shaderName.Substring(0, split);
                    }

                    // Techniques

                    List<string> techniques = new List<string>();

                    var currentTech = reShadePreset.GetValue(null, "Techniques");
                    if (currentTech != null)
                        techniques = currentTech.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(t => !knownTechniques.Contains(t)).ToList();

                    if (!string.IsNullOrEmpty(shaderFileName))
                    {
                        string shaderPath = Path.Combine(Program.AppConfig.GetFullPath("shaders"), "configs", Program.SystemConfig["shaderset"], shaderFileName);

                        if (File.Exists(shaderPath) && !string.IsNullOrEmpty(shaderName))
                        {
                            string destShader = Path.Combine(effectSearchPaths, Path.GetFileName(shaderPath));
                            if (!File.Exists(destShader))
                                File.Copy(shaderPath, destShader);

                            techniques.Add(shaderName);
                        }
                    }

                    if (bezel != null)
                    {
                        int resX = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
                        int resY = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);

                        string bezelFx = Encoding.UTF8.GetString(Properties.Resources.Bezel);
                        bezelFx = bezelFx.Replace("#PATH#", bezel.PngFile.Replace("\\", "/"));
                        bezelFx = bezelFx.Replace("#WIDTH#", resX.ToString());
                        bezelFx = bezelFx.Replace("#HEIGHT#", resY.ToString());

                        File.WriteAllText(Path.Combine(effectSearchPaths, "Bezel.fx"), bezelFx);

                        techniques.Add(bezelEffectName);
                    }

                    reShadePreset.WriteValue(null, "Techniques", string.Join(",", techniques.ToArray()));
                    
                    // TechniqueSorting

                    techniques = new List<string>();
                    var techSort = reShadePreset.GetValue(null, "TechniqueSorting");
                    if (techSort != null)
                        techniques = techSort.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(t => !knownTechniques.Contains(t)).ToList();

                    if (!string.IsNullOrEmpty(shaderFileName) && !string.IsNullOrEmpty(shaderName))
                        techniques.Add(shaderName);

                    if (bezel != null)
                        techniques.Add(bezelEffectName);

                    if (oldVersion)
                        reShadePreset.WriteValue(null, "TechniqueSorting", string.Join(",", techniques.ToArray()));

                    reShadePreset.Save();
                }
            }

            if (bezel == null)
                return false;

            return true;
        }

        static List<string> LoadKnownTechniques(bool oldVersion)
        {
            List<string> knownTechniques = new List<string>() { "Bezel@Bezel.fx" };

            try
            {
                string shadersDirectory = Path.Combine(Program.AppConfig.GetFullPath("shaders"), "configs");

                var shaderFiles = Directory.GetDirectories(shadersDirectory).SelectMany(d => Directory.GetFiles(d, "*.fx"));
                foreach (var shaderFile in shaderFiles)
                {
                    string techniquename = File.ReadAllText(shaderFile).ExtractString("technique", "{").Trim();
                    if (!string.IsNullOrEmpty(techniquename))
                    {
                        techniquename = techniquename + "@" + Path.GetFileName(shaderFile);
                        if (!knownTechniques.Contains(techniquename))
                            knownTechniques.Add(techniquename);
                    }
                }
            }
            catch { }

            if (oldVersion)
            {
                for (int i = 0; i < knownTechniques.Count; i++)
                {
                    string tmp = knownTechniques[i];
                    int tsplit = tmp.IndexOf("@");
                    if (tsplit >= 0)
                        knownTechniques[i] = tmp.Substring(0, tsplit);
                }
            }            

            return knownTechniques;
        }

        static bool GZipBytesToFile(byte[] bytes, string fileName)
        {
            try
            {
                using (var reader = new MemoryStream(bytes))
                {
                    using (var decompressedStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    {
                        using (GZipStream decompressionStream = new GZipStream(reader, CompressionMode.Decompress))
                        {
                            decompressionStream.CopyTo(decompressedStream);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[ReadGZipStream] Failed " + ex.Message, ex);
            }

            return false;
        }

        static string GetEnumDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());
            if (fi == null)
                return value.ToString();

            var attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return (attributes.Length > 0) ? attributes[0].Description : value.ToString();
        }
    }

    enum ReshadeBezelType
    {
        [Description("d3d9.dll")]
        d3d9,
        [Description("d3d10.dll")]
        d3d10,
        [Description("d3d11.dll")]
        d3d11,
        [Description("opengl32.dll")]
        opengl,
        [Description("dxgi.dll")]
        dxgi
    }

    enum ReshadePlatform
    {
        x86,
        x64
    }

}
