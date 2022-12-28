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
        private const string ReshadeFolder = "reshade-shaders";

        // -system model2 -emulator model2 -core multicpu -rom "H:\[Emulz]\roms\model2\dayton93.zip"
        // -system model3 -emulator supermodel -core  -rom "H:\[Emulz]\roms\model3\srally2.zip"
        // -system atomiswave -emulator demul -core atomiswave -rom "H:\[Emulz]\roms\atomiswave\fotns.zip"

        public static bool Setup(ReshadeBezelType type, ReshadePlatform platform, string system, string rom, string path, ScreenResolution resolution)
        {
            var bezel = BezelFiles.GetBezelFiles(system, rom, resolution);
            string shaderName = Program.SystemConfig["shader"] ?? "";

            if (bezel == null && (string.IsNullOrEmpty(shaderName) || !shaderName.Contains("@")))
            {
                UninstallReshader(type, path);
                return false;
            }

            FileInfo fileInfo = new FileInfo(InstallReshader(type, platform, path));
            if (fileInfo == null || !fileInfo.Exists)
                return false;

            var version = FileVersionInfo.GetVersionInfo(fileInfo.FullName);
            bool oldVersion = new Version(version.ProductMajorPart, version.ProductMinorPart) <= new Version(4, 6);

            var knownTechniques = LoadKnownTechniques(oldVersion);

            using (var reShadeIni = new IniFile(Path.Combine(path, "ReShade.ini")))
            {
                reShadeIni.WriteValue("GENERAL", "EffectSearchPaths", @".\"+ReshadeFolder+@"\Shaders");
                reShadeIni.WriteValue("GENERAL", "TextureSearchPaths", @".\" + ReshadeFolder  +@"\Textures");
                reShadeIni.WriteValue("GENERAL", "PresetFiles", @".\" + ReshadeFolder + @"\ReShadePreset.ini");
                reShadeIni.WriteValue("GENERAL", "PresetPath", @".\" + ReshadeFolder + @"\ReShadePreset.ini");

                if (!string.IsNullOrEmpty(Program.AppConfig["screenshots"]))
                {
                    reShadeIni.WriteValue("GENERAL", "ScreenshotPath", Program.AppConfig.GetFullPath("screenshots"));
                    reShadeIni.WriteValue("SCREENSHOTS", "SavePath", Program.AppConfig.GetFullPath("screenshots"));
                }

                var effectSearchPaths = Path.Combine(path, ReshadeFolder, "Shaders");
                Directory.CreateDirectory(effectSearchPaths);

                if (!File.Exists(Path.Combine(effectSearchPaths, "ReShade.fxh")))
                    File.WriteAllBytes(Path.Combine(effectSearchPaths, "ReShade.fxh"), Properties.Resources.ReShade);

                if (!File.Exists(Path.Combine(effectSearchPaths, "ReShadeUI.fxh")))
                    File.WriteAllBytes(Path.Combine(effectSearchPaths, "ReShadeUI.fxh"), Properties.Resources.ReShadeUI);
                
                using (var reShadePreset = new IniFile(Path.Combine(path, ReshadeFolder, "ReShadePreset.ini")))
                {                 
                    string bezelEffectName = knownTechniques[0];
                    string shaderFileName = null;

                    int split = shaderName.IndexOf("@");
                    if (split >= 0)
                    {
                        shaderFileName = shaderName.Substring(split+1);

                        if (oldVersion)
                            shaderName = shaderName.Substring(0, split);
                    }

                    // Techniques
                    var techniques = (reShadePreset.GetValue(null, "Techniques") ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(t => !knownTechniques.Contains(t)).ToList();

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
                    else if (File.Exists(Path.Combine(effectSearchPaths, "Bezel.fx")))
                        File.Delete(Path.Combine(effectSearchPaths, "Bezel.fx"));
                    
                    reShadePreset.WriteValue(null, "Techniques", string.Join(",", techniques.ToArray()));

                    if (techniques.Contains("GeomCRT"))
                    {
                        var CRTGeom = reShadePreset.GetOrCreateSection("CRTGeom.fx");
                        CRTGeom["texture_sizeX"] = "640.000000";
                        CRTGeom["texture_sizeY"] = "320.000000";
                        CRTGeom["video_sizeX"] = "640.000000";
                        CRTGeom["video_sizeY"] = "320.000000";

                        if (bezel != null)
                            CRTGeom["overscan_x"] = "103.000000";
                    }

                    // TechniqueSorting
                    var techniqueSorting = (reShadePreset.GetValue(null, "TechniqueSorting") ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(t => !knownTechniques.Contains(t)).ToList();

                    if (!string.IsNullOrEmpty(shaderFileName) && !string.IsNullOrEmpty(shaderName))
                        techniqueSorting.Add(shaderName);

                    if (bezel != null)
                        techniqueSorting.Add(bezelEffectName);

                    if (oldVersion)
                        reShadePreset.WriteValue(null, "TechniqueSorting", string.Join(",", techniqueSorting.ToArray()));

                    reShadePreset.Save();

                    return (bezel != null || techniques.Count > 0);
                }
            }
        }

        private static string InstallReshader(ReshadeBezelType type, ReshadePlatform platform, string path)
        {
            string dllName = Path.Combine(path, GetEnumDescription(type));
            if (!File.Exists(dllName))
            {
                UninstallReshader(type, path);

                if (platform == ReshadePlatform.x86)
                    GZipBytesToFile(Properties.Resources.reshader_x86_gz, Path.Combine(path, dllName));
                else
                    GZipBytesToFile(Properties.Resources.reshader_x64_gz, Path.Combine(path, dllName));
            }

            if (!File.Exists(Path.Combine(path, "ReShade.ini")))
                File.WriteAllText(Path.Combine(path, "ReShade.ini"), Properties.Resources.ReShadeIni);

            if (File.Exists(Path.Combine(path, "ReShadePreset.ini")))
                File.Delete(Path.Combine(path, "ReShadePreset.ini"));

            return dllName;
        }

        private static void UninstallReshader(ReshadeBezelType type, string path)
        {
            string dllName = Path.Combine(path, GetEnumDescription(type));
            if (File.Exists(dllName))
                File.Delete(dllName);

            if (File.Exists(Path.Combine(path, "ReShade.ini")))
                File.Delete(Path.Combine(path, "ReShade.ini"));

            if (File.Exists(Path.Combine(path, "ReShadePreset.ini")))
                File.Delete(Path.Combine(path, "ReShadePreset.ini"));

            if (Directory.Exists(Path.Combine(path, ReshadeFolder)))
                Directory.Delete(Path.Combine(path, ReshadeFolder), true);
        }

        static List<string> LoadKnownTechniques(bool oldVersion)
        {
            var knownTechniques = new List<string>() { "Bezel@Bezel.fx" };

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
