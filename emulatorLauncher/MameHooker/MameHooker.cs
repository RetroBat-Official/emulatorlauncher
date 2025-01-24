using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    public partial class MameHooker
    {
        public static bool GetMameHookerExecutable(out string executable, out string path)
        {
            executable = "mamehook.exe";
            path = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "mamehooker");

            SimpleLogger.Instance.Info($"[INFO] Checking MameHooker path: {path}");

            if (!Directory.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] MameHooker directory not found.");
                return false;
            }

            string exePath = Path.Combine(path, executable);
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
            Process[] processes = Process.GetProcessesByName("mamehook");
            if (processes.Length > 0)
            {
                SimpleLogger.Instance.Info("[INFO] Attempting to gracefully terminate MameHooker...");
                
                // Wait 10 seconds to let the process terminate naturally
                System.Threading.Thread.Sleep(10000);

                processes = Process.GetProcessesByName("mamehook");
                if (processes.Length > 0)
                {
                    SimpleLogger.Instance.Info("[INFO] MameHooker is still running after timeout, force-killing it.");
                    foreach (Process process in processes)
                    {
                        try 
                        { 
                            process.Kill();
                            SimpleLogger.Instance.Info("[INFO] MameHooker process terminated.");
                        }
                        catch (System.Exception ex) 
                        { 
                            SimpleLogger.Instance.Error($"[ERROR] Failed to terminate MameHooker: {ex.Message}"); 
                        }
                    }
                }
            }
        }
    }
} 