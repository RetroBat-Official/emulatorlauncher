using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class RetrobatLauncherGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!File.Exists(rom))
                return null;

            if (Path.GetExtension(rom).ToLower() != ".menu")
                return null;

            string[] lines = File.ReadAllLines(rom);
            if (lines.Length == 0)
                return null;

            rom = lines[0];
            string folder = rom.ExtractString("\\", "\\");
            if (string.IsNullOrEmpty(folder))
                return null;
            
            Installer installer = Installer.GetInstaller(folder);
            if (installer != null && !installer.IsInstalled() && installer.CanInstall())
            {
                using (InstallerFrm frm = new InstallerFrm(installer))
                    if (frm.ShowDialog() != DialogResult.OK)
                        return null;            
            }

            string fullPath = AppConfig.GetFullPath(folder);
            if (string.IsNullOrEmpty(fullPath))
                return null;

            string path = Path.GetDirectoryName(fullPath) + rom;
            if (!File.Exists(path))
                return null;

            var ret = new ProcessStartInfo()
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path)
            };
            
            if (lines.Length > 1)
                ret.Arguments = lines[1];
                
            return ret;
        }

    }
}
