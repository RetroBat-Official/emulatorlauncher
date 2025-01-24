using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class MameHooker
    {
        public static class Model3
        {
            private static readonly HashSet<string> SupportedGames = new HashSet<string>
            {
                "swtrilgy",  // Star Wars Trilogy
                "lamachin"   // LA Machine Guns
            };

            public static bool ConfigureModel3(string romName)
            {
                // Check if this is a supported lightgun game
                if (!SupportedGames.Contains(romName))
                {
                    SimpleLogger.Instance.Info("[INFO] Not a supported Model3 lightgun game, skipping MHS configuration");
                    return false;
                }

                // Get MameHooker path first to verify it exists
                if (!GetMameHookerExecutable(out string executable, out string mamehookPath))
                {
                    SimpleLogger.Instance.Warning("[WARNING] MameHooker path not found");
                    return false;
                }

                SimpleLogger.Instance.Info($"[INFO] MameHooker path: {mamehookPath}");

                // Configure lightgun COM ports first
                var lightguns = RawLightgun.GetRawLightguns()
                    .Where(g => g.Type == RawLighGunType.Gun4Ir || g.Type == RawLighGunType.RetroShooter)
                    .ToList();

                SimpleLogger.Instance.Info("[INFO] Configuring lightgun COM ports");
                var comPorts = LightgunComPort.GetLightgunComPorts();

                var activePorts = new List<string>();

                // Browse COM ports directly
                foreach (var port in comPorts)
                {
                    string deviceId = port.Key;
                    string comPort = port.Value;

                    // If it's a Gun4IR or RetroShooter, add the port
                    if (deviceId.Contains("VID_2341") || deviceId.Contains("VID_0483"))
                    {
                        SimpleLogger.Instance.Info($"[INFO] Adding {comPort} for device {deviceId}");
                        activePorts.Add(comPort);
                    }
                }

                // Update MameHooker config with COM ports before starting MameHook
                if (activePorts.Any())
                {
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
                            return false;
                        }
                    }
                    else
                    {
                        SimpleLogger.Instance.Info($"[INFO] No MHS file found for {romName}");
                        return false;
                    }
                }
                else
                {
                    SimpleLogger.Instance.Warning("[WARNING] No lightgun COM ports found to configure");
                    return false;
                }

                return true;
            }
        }
    }
} 