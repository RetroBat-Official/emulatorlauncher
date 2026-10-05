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
            var existingProcesses = FindMameHookerProcesses();
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

        private static Process[] FindMameHookerProcesses()
        {
            try
            {
                return Process.GetProcesses()
                    .Where(p =>
                    {
                        try { return p.ProcessName.IndexOf("mamehook", StringComparison.OrdinalIgnoreCase) >= 0; }
                        catch { return false; }
                    })
                    .ToArray();
            }
            catch
            {
                return new Process[0];
            }
        }

        public static void KillMameHooker()
        {
            foreach (var existingProcess in FindMameHookerProcesses())
            {
                SimpleLogger.Instance.Info("[INFO] Found existing MameHooker process (" + existingProcess.ProcessName + ") - stopping it");
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

        // Replace the port number of "cmw" commands in a MameHook ini line with the given COM port (e.g. "COM3" -> 3)
        private static string UpdatePortNumber(string line, string comPort)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(comPort) || comPort.Length < 4)
                return line;

            // Extract port number from COM string (e.g., "COM1" -> "1")
            string portNumber = comPort.Substring(3);

            // Replace the port number in cmw commands
            int cmwIndex = line.IndexOf("cmw");
            if (cmwIndex >= 0)
            {
                int spaceIndex = line.IndexOf(' ', cmwIndex);
                if (spaceIndex >= 0 && spaceIndex + 1 < line.Length)
                {
                    int endIndex = spaceIndex + 1;
                    while (endIndex < line.Length && (char.IsDigit(line[endIndex]) || line[endIndex] == '*'))
                        endIndex++;

                    string prefix = line.Substring(0, spaceIndex + 1);
                    string suffix = line.Substring(endIndex);
                    return prefix + portNumber + suffix;
                }
            }
            return line;
        }
    }
} 