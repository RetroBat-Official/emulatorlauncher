using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
    partial class Mame64Generator : Generator
    {
        static readonly string xmlString =
@"<mamelayout version=""2"">
    <element name=""bezel"">
        <image file=""{image}"" />
    </element>
    <view name=""Generic Cab"">
        <screen index=""0"">
            <bounds x=""{left}"" y=""{top}"" width=""{right}"" height=""{bottom}"" />
        </screen>
        <bezel element=""bezel"">
            <bounds x=""0"" y=""0"" width=""{width}"" height=""{height}"" />
        </bezel>
    </view>
</mamelayout>";

        private void ConfigureBezels(string artworkPath, string system, string rom, ScreenResolution resolution, string emulator)
        {
            string zipFile = Path.Combine(artworkPath, Path.GetFileNameWithoutExtension(rom) + ".zip");
            if (File.Exists(zipFile))
            {
                try { File.Delete(zipFile); }
                catch { }
            }

            var bezelInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            if (bezelInfo == null)
                return;

            string pngFile = bezelInfo.PngFile;
            BezelInfo infos = bezelInfo.BezelInfos;

            bool checkVerticalGame = !infos.IsValid() || !Path.GetFileNameWithoutExtension(rom).Equals(Path.GetFileNameWithoutExtension(bezelInfo.PngFile), StringComparison.InvariantCultureIgnoreCase);

            if (!infos.IsValid())
                infos = BezelInfo.FromImage(pngFile);
            
            if (!infos.IsValid())
            {
                // BezelInfo is not valid ? Assume the Viewport is 4:3
                infos.width = (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
                infos.height = (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);
                infos.left = 0;
                infos.right = 0;
                infos.top = 0;
                infos.bottom = 0;
                
                int width = (int)((infos.height * 4) / 3);
                if (width > 0 && width < infos.width)
                {
                    infos.left = (int)((infos.width - width) / 2);
                    infos.right = infos.left;
                }
            }

            // Check if ratio is forced
            if (Features.IsSupported("mame_ratio") && SystemConfig.isOptSet("mame_ratio"))
            {
                var ratio = Program.SystemConfig["mame_ratio"].ToRatio();
                if (ratio != 0)
                {
                    int height = infos.height.GetValueOrDefault() - infos.top.GetValueOrDefault() - infos.bottom.GetValueOrDefault();
                    int width = (int) (height * ratio);
                    if (width > 0)
                    {
                        int sz = (int)((infos.width - width) / 2);
                        infos.left = sz;
                        infos.right = sz;
                    }

                    checkVerticalGame = false;
                }
            }

            // Check if game is a vertical game. If true, then assume ratio is 3:4
            if (checkVerticalGame)
            {
                var romInfo = MameGameInfo.GetGameInfo(Path.GetFileNameWithoutExtension(rom));
                if (romInfo != null && romInfo.Vertical)
                {
                    // Assume it's 4:3
                    int height = infos.height.GetValueOrDefault() - infos.top.GetValueOrDefault() - infos.bottom.GetValueOrDefault();
                    int width = height * 3 / 4;
                    if (width > 0 && width < infos.width)
                    {
                        int sz = (int)((infos.width - width) / 2);
                        infos.left = sz;
                        infos.right = sz;
                    }
                }
            }

            string xml = xmlString
                .Replace("{image}", Path.GetFileName(pngFile))
                .Replace("{width}", infos.width.ToString())
                .Replace("{height}", infos.height.ToString())
                .Replace("{left}", infos.left.ToString())
                .Replace("{top}", infos.top.ToString())
                .Replace("{right}", (infos.width - infos.right - infos.left).ToString())
                .Replace("{bottom}", (infos.height - infos.bottom - infos.top).ToString());

            string tmpPath = Path.Combine(artworkPath, ".partfilestmp");

            try
            {
                if (Directory.Exists(tmpPath))
                    Directory.Delete(tmpPath, true);

                Directory.CreateDirectory(tmpPath);

                File.WriteAllText(Path.Combine(tmpPath, "default.lay"), xml);
                File.Copy(pngFile, Path.Combine(tmpPath, Path.GetFileName(pngFile)), true);

                ZipArchive.CreateFromDirectory(tmpPath, zipFile);
                
            }
            catch { }
            finally
            {
                try { Directory.Delete(tmpPath, true); }
                catch { }
            }
        }

        public class MameGameInfo
        {
            public static MameGameInfo GetGameInfo(string rom)
            {
                var file = Path.Combine(Program.SystemConfig.GetFullPath("resources"), "arcaderoms.xml");
                if (!File.Exists(file))
                    return null;

                using (XmlReader reader = XmlReader.Create(file))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement("rom"))
                        {
                            string id = reader.GetAttribute("id");
                            if (id == rom)
                            {
                                string name = reader.GetAttribute("name");
                                string vert = reader.GetAttribute("vert");

                                var game = new MameGameInfo();
                                {
                                    game.RomName = id;
                                    game.DisplayName = name;
                                    game.Vertical = vert != null && vert.Equals("true", StringComparison.OrdinalIgnoreCase);
                                };

                                return game;
                            }
                        }
                    }
                }
                return null;
            }

            private MameGameInfo() { }

            public string RomName { get; set; }
            public string DisplayName { get; set; }
            public bool Vertical { get; set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
