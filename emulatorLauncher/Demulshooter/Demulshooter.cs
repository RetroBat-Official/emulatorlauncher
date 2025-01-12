using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class Demulshooter
    {
        private static bool GetDemulshooterExecutable(string emulator, out string executable, out string path)
        {
            executable = "DemulShooter.exe";
            path = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "demulshooter");

            if (!Directory.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] Demulshooter not available.");
                return false;
            }

            if (emulator == "flycast" || emulator == "rpcs3")
                executable = "DemulShooterX64.exe";

            return true;
        }

        private readonly List<string> chihiroRoms = new List<string>
        { "vcop3" };

        private readonly List<string> demulRoms = new List<string>
        {
            "braveff", "claychal", "confmiss", "deathcox", "deathcoxo", "hotd2", "hotd2o", "hotd2p", "lupinsho", "manicpnc", "mok",
            "ninjaslt", "ninjaslta", "ninjasltj", "ninjasltu", "pokasuka", "rangrmsn", "sprtshot", "xtrmhunt", "xtrmhnt2"
        };

        private readonly List<string> model2Roms = new List<string>
        { "bel", "gunblade", "hotd", "rchase2", "vcop", "vcop2" };

        private readonly List<string> flycastRoms = new List<string>
        { "confmiss", "deathcox", "hotd2", "hotd2o", "hotd2p", "lupinsho", "mok", "ninjaslt", "ninjaslta", "ninjasltj", "ninjasltu" };

        private readonly List<string> rpcs3Roms = new List<string>
        { "de4d", "deadstorm", "sailorz" };

        public static void StartDemulshooter(string emulator, string system, string rom, RawLightgun gun1, RawLightgun gun2 = null, RawLightgun gun3 = null, RawLightgun gun4 = null)
        {
            string iniFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "demulshooter", "config.ini");

            using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                for (int i = 1; i <= 5; i++)
                {
                    string mode = "P" + i + "Mode";
                    ini.WriteValue("", mode, "RAWINPUT");
                    string devName = "P" + i + "DeviceName";
                    ini.AppendValue("", devName, string.Empty);
                }
                
                if (gun1 != null)
                    ini.WriteValue("", "P1DeviceName", gun1.DevicePath);
                if (gun2 != null)
                    ini.WriteValue("", "P2DeviceName", gun2.DevicePath);
                if (gun3 != null)
                    ini.WriteValue("", "P3DeviceName", gun3.DevicePath);
                if (gun4 != null)
                    ini.WriteValue("", "P4DeviceName", gun4.DevicePath);

                ini.Save();
            }
            // Write code to start demulshooter
            // DemulShooter.exe -target=demul07a -rom=confmiss -noresize -widescreen
            // DemulShooterX64.exe -target=seganu -rom=lma

            if (GetDemulshooterExecutable(emulator, out string executable, out string dsPath))
            {
                string exe = Path.Combine(dsPath, executable);
                var commandArray = new List<string>();

                if (GetDemulshooterTarget(emulator, out string target))
                    commandArray.Add("-target=" + target);
                else
                {
                    SimpleLogger.Instance.Warning("[WARNING] Failed to launch DemulShooter, no target.");
                    return;
                }

                if (emulator != "dolphin")
                {
                    if (GetDemulshooterRom(rom, emulator, out string dsRom))
                        commandArray.Add("-rom=" + dsRom);
                    else
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Failed to launch DemulShooter, no rom.");
                        return;
                    }
                }
                string args = string.Join(" ", commandArray);

                var p = new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = dsPath,
                    Arguments = args,
                };
                
                using (Process process = new Process())
                {
                    process.StartInfo = p;
                    process.Start();
                }
            }
            else
            {
                SimpleLogger.Instance.Warning("[WARNING] Failed to launch DemulShooter.");
                return;
            }
        }

        public static void KillDemulShooter()
        {
            string[] processNames = { "DemulShooter", "DemulShooterX64" };

            foreach (string processName in processNames)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    foreach (Process process in processes)
                    {
                        try { process.Kill(); }
                        catch { SimpleLogger.Instance.Info("[WARNING] Failed to terminate DemulShooter."); }
                    }
                }
                
            }
        }

        private static bool GetDemulshooterTarget(string emulator, out string target)
        {
            target = null;
            bool ret = false;

            if (emulator == "m2emulator")
            {
                target = "model2";
                ret = true;
            }

            return ret;
        }

        private static bool GetDemulshooterRom(string rom, string emulator, out string dsRom)
        {
            dsRom = null;
            bool ret = false;

            if (emulator == "m2emulator")
            {
                dsRom = Path.GetFileNameWithoutExtension(rom);
                ret = true;
            }

            return ret;
        }
    }
}
