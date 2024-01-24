using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmulatorLauncher.Common.Launchers
{
    public class LauncherGameInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LauncherUrl { get; set; }
        public string ExecutableName { get; set; }
        public string InstallDirectory { get; set; }
        public string PreviewImageUrl { get; set; }
        public GameLauncherType Launcher { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public enum GameLauncherType
    {
        Epic,
        Amazon,
        Steam
    }

}
