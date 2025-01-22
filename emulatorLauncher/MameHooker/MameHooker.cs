using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    public class MameHooker
    {
        private static bool GetMameHookerExecutable(out string executable, out string path)
        {
            executable = "mamehook.exe";
            path = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "mamehooker");

            if (!Directory.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] MameHooker not available.");
                return false;
            }

            return true;
        }

        public static Process StartMameHooker()
        {
            // Check if MameHook is already running
            Process[] existingProcesses = Process.GetProcessesByName("mamehook");
            if (existingProcesses.Length > 0)
            {
                SimpleLogger.Instance.Info("[INFO] MameHook is already running");
                return existingProcesses[0];
            }

            if (GetMameHookerExecutable(out string executable, out string path))
            {
                string exe = Path.Combine(path, executable);

                var p = new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                };

                Process process = new Process();
                SimpleLogger.Instance.Info("[INFO] Running MameHook: " + exe);
                process.StartInfo = p;
                process.Start();
                return process;
            }

            SimpleLogger.Instance.Warning("[WARNING] Failed to launch MameHook.");
            return null;
        }

        public static void KillMameHooker()
        {
            Process[] processes = Process.GetProcessesByName("mamehook");
            if (processes.Length > 0)
            {
                // Wait 10 seconds to let the process terminate naturally
                System.Threading.Thread.Sleep(10000);

                processes = Process.GetProcessesByName("mamehook");
                if (processes.Length > 0)
                {
                    SimpleLogger.Instance.Info("[INFO] MameHooker is still running after timeout, force-killing it.");
                    foreach (Process process in processes)
                    {
                        try { process.Kill(); }
                        catch { SimpleLogger.Instance.Info("[WARNING] Failed to terminate MameHooker."); }
                    }
                }
            }
        }
    }
} 