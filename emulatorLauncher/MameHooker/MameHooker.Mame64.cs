using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;
using System.Diagnostics;

namespace EmulatorLauncher
{
    partial class MameHooker
    {
        public static class Mame64
        {
            public static bool ConfigureMame64(string romName)
            {
                // If MameHooker is not enabled, just return
                if (!Program.SystemConfig.isOptSet("use_mamehooker") || !Program.SystemConfig.getOptBoolean("use_mamehooker"))
                {
                    SimpleLogger.Instance.Info("[INFO] MameHooker is disabled");
                    return false;
                }

                // Get MameHooker path first to verify it exists
                if (!GetMameHookerExecutable(out string executable, out string mamehookPath))
                {
                    SimpleLogger.Instance.Warning("[WARNING] MameHooker path not found");
                    return false;
                }

                SimpleLogger.Instance.Info($"[INFO] MameHooker path: {mamehookPath}");

                // Configure lightgun COM ports
                var comPorts = LightgunComPort.GetLightgunComPorts();
                var activePorts = LightgunComPort.GetOrderedComPorts(comPorts);

                if (!activePorts.Any())
                {
                    SimpleLogger.Instance.Warning("[WARNING] No lightgun COM ports found to configure");
                    return false;
                }

                SimpleLogger.Instance.Info($"[INFO] Found {activePorts.Count} lightgun COM port(s)");
                foreach (var port in activePorts)
                    SimpleLogger.Instance.Info($"[INFO] Using COM port: {port}");

                // Update default configuration files first
                LightgunComPort.UpdateMameHookerConfig(mamehookPath, activePorts);

                // Update MHS file if it exists
                string scriptsPath = Path.Combine(mamehookPath, "scripts");
                string mhsFile = Path.Combine(scriptsPath, romName + ".mhs");

                if (File.Exists(mhsFile))
                {
                    SimpleLogger.Instance.Info($"[INFO] Found MHS file: {mhsFile}");
                    try
                    {
                        string[] lines = File.ReadAllLines(mhsFile);
                        bool modified = false;

                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i];
                            if (line.Contains("cmw"))
                            {
                                string[] parts = line.Split(',');
                                for (int j = 0; j < parts.Length && j < activePorts.Count; j++)
                                {
                                    // Extract the port number from COM string (e.g., "COM1" -> "1")
                                    string portNumber = null;
                                    if (activePorts[j].StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                                    {
                                        portNumber = activePorts[j].Substring(3);
                                        SimpleLogger.Instance.Info($"[INFO] Extracted port number {portNumber} from {activePorts[j]}");
                                    }
                                    else
                                    {
                                        SimpleLogger.Instance.Warning($"[WARNING] Invalid COM port format: {activePorts[j]}");
                                        continue;
                                    }

                                    if (string.IsNullOrEmpty(portNumber))
                                    {
                                        SimpleLogger.Instance.Warning("[WARNING] Failed to extract port number");
                                        continue;
                                    }
                                    
                                    // Replace the port number in the cmw command
                                    string oldPart = parts[j].Trim();
                                    string newPart = oldPart;

                                    // Search for pattern "cmw X" where X is a number or a star
                                    int cmwIndex = oldPart.IndexOf("cmw");
                                    if (cmwIndex >= 0)
                                    {
                                        int spaceIndex = oldPart.IndexOf(' ', cmwIndex);
                                        if (spaceIndex >= 0 && spaceIndex + 1 < oldPart.Length)
                                        {
                                            int endIndex = spaceIndex + 1;
                                            while (endIndex < oldPart.Length && (char.IsDigit(oldPart[endIndex]) || oldPart[endIndex] == '*'))
                                                endIndex++;

                                            string prefix = oldPart.Substring(0, spaceIndex + 1);
                                            string suffix = oldPart.Substring(endIndex);
                                            newPart = prefix + portNumber + suffix;

                                            SimpleLogger.Instance.Info($"[INFO] Replacing port in command: prefix='{prefix}', port='{portNumber}', suffix='{suffix}'");
                                        }
                                    }
                                    
                                    parts[j] = newPart;
                                    SimpleLogger.Instance.Info($"[INFO] Updating port in MHS: {oldPart} -> {newPart}");
                                }
                                lines[i] = string.Join(",", parts);
                                modified = true;
                            }
                        }

                        if (modified)
                        {
                            File.WriteAllLines(mhsFile, lines);
                            SimpleLogger.Instance.Info($"[INFO] Updated MHS file with COM ports");
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Error($"[ERROR] Failed to update MHS file: {ex.Message}");
                    }
                }
                else
                {
                    SimpleLogger.Instance.Info($"[INFO] No MHS file found for {romName}");
                }

                // Look for game specific INI in MAME and SUPERMODEL folders
                string[] folders = { "MAME", "SUPERMODEL" };
                foreach (string folder in folders)
                {
                    string iniPath = Path.Combine(mamehookPath, "ini", folder, romName + ".ini");
                    SimpleLogger.Instance.Info($"[INFO] Looking for game-specific INI file: {iniPath}");
                    if (File.Exists(iniPath))
                    {
                        SimpleLogger.Instance.Info($"[INFO] Found game-specific INI file: {iniPath}");
                        try
                        {
                            string[] lines = File.ReadAllLines(iniPath);
                            bool modified = false;

                            for (int i = 0; i < lines.Length; i++)
                            {
                                string line = lines[i];
                                
                                // Process each line that needs port number update
                                if (line.Contains("P1_CtmRecoil=") || 
                                    line.Contains("P1_GunRecoil=") ||
                                    line.Contains("P1_Damaged="))
                                {
                                    if (activePorts.Count > 0)
                                        lines[i] = UpdatePortNumber(line, activePorts[0]);
                                    modified = true;
                                }
                                else if (line.Contains("P2_CtmRecoil=") || 
                                       line.Contains("P2_GunRecoil=") ||
                                       line.Contains("P2_Damaged="))
                                {
                                    if (activePorts.Count > 1)
                                        lines[i] = UpdatePortNumber(line, activePorts[1]);
                                    modified = true;
                                }
                            }

                            if (modified)
                            {
                                File.WriteAllLines(iniPath, lines);
                                SimpleLogger.Instance.Info($"[INFO] Updated {folder} INI file for {romName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.Instance.Error($"[ERROR] Failed to update {folder} INI file: {ex.Message}");
                        }
                    }
                }

                // Start MameHooker process if not already running
                try
                {
                    SimpleLogger.Instance.Info("[INFO] Starting MameHooker process");
                    Process process = Process.Start(new ProcessStartInfo
                    {
                        FileName = executable,
                        WorkingDirectory = mamehookPath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    });

                    // Give MameHooker time to initialize
                    SimpleLogger.Instance.Info("[INFO] Waiting for MameHooker to initialize...");
                    System.Threading.Thread.Sleep(5000); // Increased to 5 seconds for better initialization
                    
                    // Verify process is still running
                    if (process != null && !process.HasExited)
                    {
                        SimpleLogger.Instance.Info("[INFO] MameHooker process started successfully");
                        return true;
                    }
                    else
                    {
                        SimpleLogger.Instance.Error("[ERROR] MameHooker process exited prematurely");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error($"[ERROR] Failed to start MameHooker: {ex.Message}");
                    return false;
                }
            }

            private static string UpdatePortNumber(string line, string comPort)
            {
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

            private static void KillMameHooker()
            {
                try
                {
                    var existingProcess = Process.GetProcessesByName("MameHooker").FirstOrDefault();
                    if (existingProcess != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Found existing MameHooker process - stopping it");
                        existingProcess.Kill();
                        existingProcess.WaitForExit(1000);
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error($"[ERROR] Failed to stop existing MameHooker: {ex.Message}");
                }
            }
        }
    }
} 