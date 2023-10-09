using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmulatorLauncher.Common.EmulationStation;
using System.IO;

namespace EmulatorLauncher
{
    static class EsSaveStatesExtensions
    {
        public static bool IsIncremental(this EsSaveStates state, string emulator)
        {
            bool incrementalOption = (string.IsNullOrEmpty(Program.SystemConfig["incrementalsavestates"]) ? "1" : Program.SystemConfig["incrementalsavestates"]) == "1";
            if (!incrementalOption)
                return false;

            var emul = state[emulator];
            return (emul != null && emul.Incremental);
        }

        public static string GetSavePath(this EsSaveStates state, string system, string emulator, string core)
        {
            var saves = Program.AppConfig.GetFullPath("saves");
            if (!Directory.Exists(saves))
                return null;

            var emul = state[emulator];
            if (emul == null || string.IsNullOrEmpty(emul.Directory))
                return Path.GetFullPath(Path.Combine(saves, system));

            string ret = emul.Directory
                .Replace("{{system}}", system ?? "")
                .Replace("{{emulator}}", emulator ?? "")
                .Replace("{{core}}", core ?? "");

            if (!string.IsNullOrEmpty(emul.DefaultCoreDirectory))
            {
                var sys = Program.EsSystems[system];
                if (sys != null && emulator == sys.DefaultEmulator && core == sys.DefaultCore)
                {
                    ret = emul.DefaultCoreDirectory
                        .Replace("{{system}}", system ?? "")
                        .Replace("{{emulator}}", emulator ?? "")
                        .Replace("{{core}}", core ?? "");
                }
            }

            return Path.GetFullPath(Path.Combine(saves, ret));
        }
    }
}
