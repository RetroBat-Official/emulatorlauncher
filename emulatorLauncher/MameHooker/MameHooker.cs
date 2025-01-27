using System;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System.Linq;

namespace EmulatorLauncher
{
    public partial class MameHooker
    {
        private static bool GetMameHookerExecutable(out string executable, out string mamehookPath)
        {
            executable = "mamehook.exe";
            mamehookPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "mamehooker");

            // Always kill any existing MameHooker process first
            KillMameHooker();

            SimpleLogger.Instance.Info($"[INFO] Checking MameHooker path: {mamehookPath}");

            if (!Directory.Exists(mamehookPath))
            {
                SimpleLogger.Instance.Warning("[WARNING] MameHooker directory not found.");
                return false;
            }

            string exePath = Path.Combine(mamehookPath, executable);
            if (!File.Exists(exePath))
            {
                SimpleLogger.Instance.Warning($"[WARNING] MameHooker executable not found at: {exePath}");
                return false;
            }

            SimpleLogger.Instance.Info("[INFO] MameHooker found successfully.");
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

                try
                {
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
                catch (System.Exception ex)
                {
                    SimpleLogger.Instance.Error($"[ERROR] Failed to start MameHook: {ex.Message}");
                    return null;
                }
            }

            SimpleLogger.Instance.Warning("[WARNING] Failed to launch MameHook - executable not found.");
            return null;
        }

        public static void KillMameHooker()
        {
            var existingProcess = Process.GetProcessesByName("mamehook").FirstOrDefault();
            if (existingProcess != null)
            {
                SimpleLogger.Instance.Info("[INFO] Found existing MameHooker process - stopping it");
                try
                {
                    existingProcess.Kill();
                    existingProcess.WaitForExit(1000);
                    SimpleLogger.Instance.Info("[INFO] MameHooker process terminated");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error($"[ERROR] Failed to stop existing MameHooker: {ex.Message}");
                }
            }
        }
    }
} 