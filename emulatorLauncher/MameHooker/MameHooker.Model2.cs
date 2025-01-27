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
        public static class Model2
        {
            private static readonly HashSet<string> SupportedGames = new HashSet<string>
            {
                "bel",       // Behind Enemy Lines
                "gunblade",  // Gunblade NY
                "hotd",      // House of the Dead
                "rchase2",   // Rail Chase 2
                "vcop",      // Virtua Cop
                "vcop2"      // Virtua Cop 2
            };

            public static bool ConfigureModel2(string romName)
            {
                // Check if this is a supported lightgun game
                if (!SupportedGames.Contains(romName))
                {
                    SimpleLogger.Instance.Info("[INFO] Not a supported Model2 lightgun game, skipping configuration");
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
                                
                                if (romName == "rchase2" && line.Contains("LEFT MOUSE BUTTON="))
                                {
                                    SimpleLogger.Instance.Info($"[INFO] Processing rchase2 mouse button line: {line}");
                                    
                                    // Split on comma to separate the two cmw commands
                                    string[] parts = line.Split(new[] { ',' }, 2);
                                    if (parts.Length >= 2)
                                    {
                                        string p1Part = parts[0].Trim();
                                        string p2Part = parts[1].Trim();

                                        // Update P1 if we have at least one port
                                        if (activePorts.Count > 0)
                                        {
                                            string p1Port = activePorts[0].Substring(3);
                                            int cmwIndex1 = p1Part.IndexOf("cmw");
                                            if (cmwIndex1 >= 0)
                                            {
                                                int spaceIndex = p1Part.IndexOf(' ', cmwIndex1);
                                                if (spaceIndex >= 0 && spaceIndex + 1 < p1Part.Length)
                                                {
                                                    int endIndex = spaceIndex + 1;
                                                    while (endIndex < p1Part.Length && (char.IsDigit(p1Part[endIndex]) || p1Part[endIndex] == '*'))
                                                        endIndex++;

                                                    string prefix = p1Part.Substring(0, spaceIndex + 1);
                                                    string suffix = p1Part.Substring(endIndex);
                                                    p1Part = prefix + p1Port + suffix;
                                                    modified = true;
                                                    SimpleLogger.Instance.Info($"[INFO] Updated P1 port to {p1Port}");
                                                }
                                            }
                                        }

                                        // Update P2 only if we have a second port
                                        if (activePorts.Count > 1)
                                        {
                                            string p2Port = activePorts[1].Substring(3);
                                            int cmwIndex2 = p2Part.IndexOf("cmw");
                                            if (cmwIndex2 >= 0)
                                            {
                                                int spaceIndex = p2Part.IndexOf(' ', cmwIndex2);
                                                if (spaceIndex >= 0 && spaceIndex + 1 < p2Part.Length)
                                                {
                                                    int endIndex = spaceIndex + 1;
                                                    while (endIndex < p2Part.Length && (char.IsDigit(p2Part[endIndex]) || p2Part[endIndex] == '*'))
                                                        endIndex++;

                                                    string prefix = p2Part.Substring(0, spaceIndex + 1);
                                                    string suffix = p2Part.Substring(endIndex);
                                                    p2Part = prefix + p2Port + suffix;
                                                    modified = true;
                                                    SimpleLogger.Instance.Info($"[INFO] Updated P2 port to {p2Port}");
                                                }
                                            }
                                        }

                                        // Update the line if any changes were made
                                        if (modified)
                                        {
                                            lines[i] = p1Part + ", " + p2Part;
                                            SimpleLogger.Instance.Info($"[INFO] Updated rchase2 mouse buttons line to: {lines[i]}");
                                        }
                                    }
                                }
                                else if (line.Contains("P1_CtmRecoil=") || 
                                    line.Contains("P1_GunRecoil=") ||
                                    line.Contains("P1_Damaged=")||
                                    line.Contains("P1_Gun_Recoil="))
                                {
                                    if (activePorts.Count > 0)
                                        lines[i] = UpdatePortNumber(line, activePorts[0]);
                                    modified = true;
                                }
                                else if (line.Contains("P2_CtmRecoil=") || 
                                       line.Contains("P2_GunRecoil=") ||
                                       line.Contains("P2_Damaged=")||
                                       line.Contains("P2_Gun_Recoil="))
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
                            return false;
                        }
                    }
                }

                return true;
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
        }
    }
} 